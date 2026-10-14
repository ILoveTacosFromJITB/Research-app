package com.resellcomps.app

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.resellcomps.core.ClaudeService
import com.resellcomps.core.CompsFinder
import com.resellcomps.core.CompsReport
import com.resellcomps.core.EbayBrowseApi
import com.resellcomps.core.FreeCompsFinder
import com.resellcomps.core.FreeIdentification
import com.resellcomps.core.FreeVisionAi
import com.resellcomps.core.GeminiService
import com.resellcomps.core.GoogleVision
import com.resellcomps.core.ItemIdentification
import com.resellcomps.core.SerpApiEbay
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

sealed interface Screen {
    data object Home : Screen
    data class Busy(val message: String) : Screen
    data class Identified(val item: ItemIdentification) : Screen
    data class Results(val report: CompsReport) : Screen
    data class Failed(val message: String, val canRetrySearch: Boolean, val canRetryIdentify: Boolean = false) : Screen
}

class MainViewModel(app: Application) : AndroidViewModel(app) {
    val settings = Settings(app)

    private val _screen = MutableStateFlow<Screen>(Screen.Home)
    val screen: StateFlow<Screen> = _screen

    private val _photos = MutableStateFlow<List<ByteArray>>(emptyList())
    val photos: StateFlow<List<ByteArray>> = _photos

    private var lastItem: ItemIdentification? = null

    /** Free mode works with no keys at all; Pro mode needs an Anthropic key. */
    val hasKeys: Boolean get() = settings.mode == Mode.FREE || settings.anthropicKey.isNotBlank()

    /** In free mode, an Anthropic key (if entered) is used to name the item with the cheapest model. */
    private val useAiInFree: Boolean
        get() = settings.mode == Mode.FREE && settings.anthropicKey.isNotBlank()

    private fun proFinder() = CompsFinder(
        claude = ClaudeService(settings.anthropicKey, settings.model),
        ebay = settings.serpApiKey.takeIf { it.isNotBlank() }?.let { SerpApiEbay(it) },
    )

    private fun ebayApi() = if (settings.hasEbayKeys) EbayBrowseApi(settings.ebayClientId, settings.ebayClientSecret) else null

    private fun freeFinder() = FreeCompsFinder(
        ebay = ebayApi(),
    )

    /** Built-in Lens: where these photos appear on the web. Null when no key is set; errors become a note. */
    private fun lens(photos: List<ByteArray>): Pair<GoogleVision.Result?, String?> {
        val key = settings.googleVisionKey.takeIf { it.isNotBlank() } ?: return null to null
        return runCatching { GoogleVision(key).detect(photos.take(2)) }
            .fold({ it to null }, { null to "Visual search failed: ${it.message}" })
    }

    /** Free identification: on-phone barcode/text reading, built-in Lens, and eBay barcode/photo matches. */
    private suspend fun identifyFree(photos: List<ByteArray>): ItemIdentification {
        val reads = photos.map { OnDeviceReader.read(it) }
        val seen = OnDeviceReader.Result(
            gtin = reads.firstNotNullOfOrNull { it.gtin },
            textLines = reads.flatMap { it.textLines },
            labels = reads.flatMap { it.labels }.distinct(),
        )
        val jpeg = photos.first()
        val ebay = ebayApi()
        return withContext(Dispatchers.IO) {
            val (vision, visionError) = lens(photos)
            val barcodeTitles = if (ebay != null && seen.gtin != null) {
                runCatching { ebay.search(null, seen.gtin, limit = 20).map { it.title } }.getOrDefault(emptyList())
            } else emptyList()
            val needSimilar = barcodeTitles.isEmpty() && (vision == null || vision.isEmpty())
            val similarTitles = if (ebay != null && needSimilar) {
                runCatching { ebay.searchByImage(jpeg, limit = 12).map { it.title } }.getOrDefault(emptyList())
            } else emptyList()
            val id = FreeIdentification.build(seen.gtin, seen.textLines, seen.labels, barcodeTitles, similarTitles, vision)
            if (visionError != null) {
                id.copy(details = id.details + "\n" + visionError, warning = "$visionError\n${FreeIdentification.BASIC_WARNING}")
            } else id
        }
    }

    fun addPhotos(uris: List<Uri>) {
        if (uris.isEmpty()) return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Loading photo…")
            runCatching {
                withContext(Dispatchers.IO) { uris.map { Images.loadJpeg(getApplication(), it) } }
            }
                .onSuccess {
                    _photos.value = (_photos.value + it).take(ClaudeService.MAX_PHOTOS)
                    // No extra tap: identify as soon as a photo is in.
                    if (hasKeys) identify("") else _screen.value = Screen.Home
                }
                .onFailure { _screen.value = Screen.Failed("Couldn't read that photo: ${it.message}", false) }
        }
    }

    /** Straight to the search screen with no photo: type a search and tap eBay Active/Sold. */
    fun startTextSearch() {
        val blank = ItemIdentification(
            name = "", brand = null, model = null, category = "", details = "",
            searchQuery = "", confidence = "", identified = true,
        )
        _photos.value = emptyList()
        lastItem = blank
        _screen.value = Screen.Identified(blank)
    }

    /** Back to the photo screen keeping the photos, e.g. to add a close-up of the tag. */
    fun backToPhotos() {
        _screen.value = Screen.Home
    }

    fun removePhoto(index: Int) {
        _photos.value = _photos.value.filterIndexed { i, _ -> i != index }
    }

    private var lastHint = ""

    /** Runs identification again with the same photos (e.g. after a timeout). */
    fun retryIdentify() = identify(lastHint)

    fun identify(hint: String) {
        lastHint = hint
        val photos = _photos.value.takeIf { it.isNotEmpty() } ?: return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Identifying the item…")
            runCatching {
                if (settings.mode == Mode.FREE && settings.freeAiKey.isNotBlank()) {
                    // Free AI (Groq or OpenRouter, detected from the key).
                    withContext(Dispatchers.IO) {
                        FreeVisionAi(settings.freeAiKey).identify(photos, hint)
                            .copy(visualMatches = lens(photos).first?.matches.orEmpty())
                    }
                } else if (settings.mode == Mode.FREE && settings.geminiKey.isNotBlank()) {
                    // Free AI naming first; Lens matches ride along if a Vision key is set.
                    withContext(Dispatchers.IO) {
                        GeminiService(settings.geminiKey).identify(photos, hint)
                            .copy(visualMatches = lens(photos).first?.matches.orEmpty())
                    }
                } else if (useAiInFree) {
                    withContext(Dispatchers.IO) {
                        val named = ClaudeService(settings.anthropicKey, ClaudeService.LOW_COST_MODEL).identify(photos, hint)
                        // Keep the Lens matches alongside the AI's name.
                        named.copy(visualMatches = lens(photos).first?.matches.orEmpty())
                    }
                } else if (settings.mode == Mode.FREE) {
                    identifyFree(photos).let { id ->
                        when {
                            hint.isBlank() -> id
                            // Nothing readable on the item: the seller's note becomes the search.
                            id.searchQuery.isBlank() -> id.copy(name = hint.trim(), searchQuery = hint.trim(), identified = true)
                            else -> id.copy(details = id.details + "\nYour note: " + hint.trim())
                        }
                    }
                } else {
                    withContext(Dispatchers.IO) {
                        proFinder().identify(photos, hint).copy(visualMatches = lens(photos).first?.matches.orEmpty())
                    }
                }
            }
                .onSuccess { lastItem = it; _screen.value = Screen.Identified(it) }
                .onFailure { _screen.value = Screen.Failed(describe(it), canRetrySearch = false, canRetryIdentify = true) }
        }
    }

    fun search(item: ItemIdentification, query: String) {
        // Photos are optional here: a typed search runs on text alone.
        val photos = _photos.value
        // Remember the typed search so "Edit search" comes back to it.
        lastItem = item.copy(searchQuery = query)
        viewModelScope.launch {
            _screen.value = Screen.Busy("Searching…")
            runCatching {
                withContext(Dispatchers.IO) {
                    val progress = { msg: String -> _screen.value = Screen.Busy(msg) }
                    if (settings.mode == Mode.FREE) freeFinder().findComps(photos, item, query, progress)
                    else proFinder().findComps(photos, item, query, progress)
                }
            }
                .onSuccess { _screen.value = Screen.Results(it) }
                .onFailure { _screen.value = Screen.Failed(describe(it), true) }
        }
    }

    /** Back to the editable search for the same item. */
    fun editSearch() {
        lastItem?.let { _screen.value = Screen.Identified(it) } ?: reset()
    }

    fun reset() {
        _photos.value = emptyList()
        lastItem = null
        _screen.value = Screen.Home
    }

    private fun describe(t: Throwable): String {
        val msg = generateSequence(t) { it.cause }.mapNotNull { it.message }.firstOrNull() ?: t.javaClass.simpleName
        val timedOut = generateSequence(t) { it.cause }.any { it is java.io.InterruptedIOException } ||
            msg.contains("timeout", ignoreCase = true) || msg.contains("timed out", ignoreCase = true)
        return when {
            timedOut -> "The reply took too long (timed out). Check your signal and tap Try again."
            t is java.net.UnknownHostException -> "No internet connection. Check your signal and tap Try again."
            msg.contains("401") || msg.contains("authentication", ignoreCase = true) ->
                "Your Anthropic API key was rejected. Check it in Settings."
            else -> msg
        }
    }
}
