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

    fun addPhotos(uris: List<Uri>) {
        if (uris.isEmpty()) return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Loading photo…")
            runCatching {
                withContext(Dispatchers.IO) { uris.map { Images.loadJpeg(getApplication(), it) } }
            }
                .onSuccess {
                    _photos.value = (_photos.value + it).take(IdentifyPrompt.MAX_PHOTOS)
                    // No extra tap: identify as soon as a photo is in.
                    identify("")
                }
                .onFailure { _screen.value = Screen.Failed("Couldn't read that photo: ${it.message}", false) }
        }
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
                if (settings.freeAiKey.isNotBlank()) {
                    // Free AI (Groq or OpenRouter, detected from the key).
                    withContext(Dispatchers.IO) {
                        FreeVisionAi(settings.freeAiKey).identify(photos.map { Images.shrink(it) }, hint) { secs ->
                            _screen.value = Screen.Busy("Free AI limit reached for this minute — continuing in ${secs}s…")
                        }
                    }
                } else {
                    // No key yet: read barcodes and printed text on the phone.
                    val reads = photos.map { OnDeviceReader.read(it) }
                    FreeIdentification.build(
                        gtin = reads.firstNotNullOfOrNull { it.gtin },
                        textLines = reads.flatMap { it.textLines },
                        objectLabels = reads.flatMap { it.labels }.distinct(),
                    )
                }
            }
                .onSuccess { _screen.value = Screen.Identified(it) }
                .onFailure { _screen.value = Screen.Failed(describe(it), canRetryIdentify = true) }
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
