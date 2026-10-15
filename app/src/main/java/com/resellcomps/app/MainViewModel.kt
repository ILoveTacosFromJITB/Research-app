package com.resellcomps.app

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.resellcomps.core.FreeIdentification
import com.resellcomps.core.FreeVisionAi
import com.resellcomps.core.IdentifyPrompt
import com.resellcomps.core.ItemIdentification
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

sealed interface Screen {
    data object Home : Screen
    data class Busy(val message: String) : Screen
    data class Identified(val item: ItemIdentification) : Screen
    data class Failed(val message: String, val canRetryIdentify: Boolean) : Screen
}

class MainViewModel(app: Application) : AndroidViewModel(app) {
    val settings = Settings(app)

    private val _screen = MutableStateFlow<Screen>(Screen.Home)
    val screen: StateFlow<Screen> = _screen

    private val _photos = MutableStateFlow<List<ByteArray>>(emptyList())
    val photos: StateFlow<List<ByteArray>> = _photos

    private var lastHint = ""

    /** Gallery photos join the ones on the camera screen; Scan identifies them all together. */
    fun addPhotos(uris: List<Uri>) {
        if (uris.isEmpty()) return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Loading photo…")
            runCatching {
                withContext(Dispatchers.IO) { uris.map { Images.loadJpeg(getApplication(), it) } }
            }
                .onSuccess { _photos.value = (_photos.value + it).take(IdentifyPrompt.MAX_PHOTOS) }
                .onFailure { _screen.value = Screen.Failed("Couldn't read that photo: ${it.message}", false); return@launch }
            _screen.value = Screen.Home
        }
    }

    /** A shot from the in-app camera. */
    fun addCapturedPhoto(jpeg: ByteArray) {
        _photos.value = (_photos.value + jpeg).take(IdentifyPrompt.MAX_PHOTOS)
    }

    fun removeLastPhoto() {
        _photos.value = _photos.value.dropLast(1)
    }

    /** Straight to the search screen with no photo: type a search and tap eBay Active/Sold. */
    fun startTextSearch() {
        _photos.value = emptyList()
        _screen.value = Screen.Identified(
            ItemIdentification(
                name = "", brand = null, model = null, category = "", details = "",
                searchQuery = "", confidence = "", identified = true,
            )
        )
    }

    /** Back to the photo screen keeping the photos, e.g. to add a close-up of the tag. */
    fun backToPhotos() {
        _screen.value = Screen.Home
    }

    fun removePhoto(index: Int) {
        _photos.value = _photos.value.filterIndexed { i, _ -> i != index }
    }

    /** Runs identification again with the same photos (e.g. after a timeout). */
    fun retryIdentify() = identify(lastHint)

    fun identify(hint: String) {
        lastHint = hint
        val photos = _photos.value.takeIf { it.isNotEmpty() } ?: return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Identifying the item…")
            runCatching {
                if (!hasAi) return@runCatching basicReading(photos)
                try {
                    runAi(
                        cloud = { ai -> ai.identify(photos.map { Images.shrink(it) }, hint, ::showLimitWait) },
                        phone = { ai -> ai.identify(photos, hint, ::showProgress) },
                    )
                } catch (e: OnPhoneAi.Unavailable) {
                    // Only the on-phone AI was set up and this phone can't run it: read the tags instead.
                    if (hasCloud) throw e else basicReading(photos)
                }
            }
                .onSuccess { first ->
                    if (shouldAutoDigDeeper(first)) {
                        _screen.value = Screen.Busy("Checking the tag closely…")
                        val deeper = runCatching { digDeeperWithAi(photos, first, first.searchQuery) }
                        // If the second look fails, the first answer still stands.
                        _screen.value = Screen.Identified(deeper.getOrDefault(first))
                    } else {
                        _screen.value = Screen.Identified(first)
                    }
                }
                .onFailure { _screen.value = Screen.Failed(describe(it), canRetryIdentify = true) }
        }
    }

    /** No AI at all: read barcodes and printed text on the phone. */
    private suspend fun basicReading(photos: List<ByteArray>): ItemIdentification {
        val reads = photos.map { OnDeviceReader.read(it) }
        return FreeIdentification.build(
            gtin = reads.firstNotNullOfOrNull { it.gtin },
            textLines = reads.flatMap { it.textLines },
            objectLabels = reads.flatMap { it.labels }.distinct(),
        )
    }

    private suspend fun digDeeperWithAi(photos: List<ByteArray>, item: ItemIdentification, query: String): ItemIdentification =
        runAi(
            cloud = { ai -> ai.digDeeper(photos.map { Images.shrink(it, 1280) }, item, query, ::showLimitWait) },
            phone = { ai -> ai.digDeeper(photos, item, query, ::showProgress) },
        )

    private fun showLimitWait(secs: Int) {
        _screen.value = Screen.Busy("Free AI limit reached for this minute — continuing in ${secs}s…")
    }

    private fun showProgress(message: String) {
        _screen.value = Screen.Busy(message)
    }

    private val onPhoneAi by lazy { OnPhoneAi() }

    override fun onCleared() {
        runCatching { onPhoneAi.close() }
    }

    /**
     * Cloud first (your server, then the Groq/OpenRouter key). When the free limit is used up,
     * there's no signal, or no cloud AI is set up, the on-phone AI takes over (if turned on).
     */
    private suspend fun <T> runAi(cloud: (FreeVisionAi) -> T, phone: suspend (OnPhoneAi) -> T): T {
        if (hasCloud) {
            try {
                return withContext(Dispatchers.IO) { withAi(cloud) }
            } catch (e: Exception) {
                val reason = phoneFallbackReason(e)
                if (!settings.phoneAi || reason == null) throw e
                _screen.value = Screen.Busy(reason)
            }
        }
        return phone(onPhoneAi)
    }

    /** Why the cloud AI can't answer right now, if it's a case the on-phone AI can cover. */
    private fun phoneFallbackReason(e: Throwable): String? {
        val chain = generateSequence(e) { it.cause }.toList()
        return when {
            chain.any { it is FreeVisionAi.LimitException } -> "Free AI limit used up — using the on-phone AI…"
            chain.any {
                it is java.net.UnknownHostException || it is java.net.ConnectException ||
                    it is java.net.NoRouteToHostException || it is FreeVisionAi.ServerOffline
            } -> "No connection — using the on-phone AI…"
            else -> null
        }
    }

    /**
     * Run the tag-focused second look right away when it matters: tags were read, or the item
     * may be vintage. Items without tags (media, electronics) skip it and stay fast.
     */
    private fun shouldAutoDigDeeper(item: ItemIdentification): Boolean =
        settings.autoDigDeeper && hasAi &&
            (item.tagReading != null || item.vintage == "yes" || item.vintage == "unsure")

    /** True when the free AI is set up, so "Dig deeper" can run. */
    val canDigDeeper: Boolean get() = hasAi

    /** A cloud AI is set up: your own server and/or a Groq/OpenRouter key. */
    private val hasCloud: Boolean get() = settings.serverUrl.isNotBlank() || settings.freeAiKey.isNotBlank()

    /** Any AI: cloud, or the on-phone backup. */
    private val hasAi: Boolean get() = hasCloud || settings.phoneAi

    /**
     * Runs [request] on your own server when one is set, falling back to the Groq/OpenRouter key
     * when the computer is off. Runs on the calling (IO) thread.
     */
    private fun <T> withAi(request: (FreeVisionAi) -> T): T {
        val server = settings.serverUrl.trim()
        if (server.isNotEmpty()) {
            try {
                return request(FreeVisionAi("", server))
            } catch (e: FreeVisionAi.ServerOffline) {
                if (settings.freeAiKey.isBlank()) {
                    throw FreeVisionAi.ServerOffline("Your computer is off or asleep, and there's no Groq/OpenRouter key to fall back on.")
                }
                val provider = FreeVisionAi.detect(settings.freeAiKey)?.label ?: "the backup key"
                _screen.value = Screen.Busy("Your computer is off — using $provider…")
            }
        }
        return request(FreeVisionAi(settings.freeAiKey))
    }

    /**
     * Second, tag-focused look at the vintage call. Keeps the user on the result screen:
     * on failure the reason shows there instead of an error page.
     */
    fun digDeeper(item: ItemIdentification, currentQuery: String) {
        val photos = _photos.value.takeIf { it.isNotEmpty() } ?: return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Studying the tags…")
            val result = runCatching { digDeeperWithAi(photos, item, currentQuery) }
            _screen.value = Screen.Identified(
                result.getOrElse { item.copy(searchQuery = currentQuery, warning = "Dig deeper didn't finish: ${describe(it)}") }
            )
        }
    }

    fun reset() {
        _photos.value = emptyList()
        _screen.value = Screen.Home
    }

    private fun describe(t: Throwable): String {
        val msg = generateSequence(t) { it.cause }.mapNotNull { it.message }.firstOrNull() ?: t.javaClass.simpleName
        val timedOut = generateSequence(t) { it.cause }.any { it is java.io.InterruptedIOException } ||
            msg.contains("timeout", ignoreCase = true) || msg.contains("timed out", ignoreCase = true)
        return when {
            timedOut -> "The reply took too long (timed out). Check your signal and tap Try again."
            t is java.net.UnknownHostException -> "No internet connection. Check your signal and tap Try again."
            else -> msg
        }
    }
}
