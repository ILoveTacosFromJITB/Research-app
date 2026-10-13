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
    data class Failed(val message: String, val canRetrySearch: Boolean) : Screen
}

class MainViewModel(app: Application) : AndroidViewModel(app) {
    val settings = Settings(app)

    private val _screen = MutableStateFlow<Screen>(Screen.Home)
    val screen: StateFlow<Screen> = _screen

    private val _photo = MutableStateFlow<ByteArray?>(null)
    val photo: StateFlow<ByteArray?> = _photo

    private var lastItem: ItemIdentification? = null

    /** Free mode works with no keys at all; Pro mode needs an Anthropic key. */
    val hasKeys: Boolean get() = settings.mode == Mode.FREE || settings.anthropicKey.isNotBlank()

    private fun proFinder() = CompsFinder(
        claude = ClaudeService(settings.anthropicKey, settings.model),
        ebay = settings.serpApiKey.takeIf { it.isNotBlank() }?.let { SerpApiEbay(it) },
    )

    private fun ebayApi() = if (settings.hasEbayKeys) EbayBrowseApi(settings.ebayClientId, settings.ebayClientSecret) else null

    private fun freeFinder() = FreeCompsFinder(
        ebay = ebayApi(),
        soldPageReader = if (settings.readSoldPage) SoldPageReader(getApplication())::read else null,
    )

    /** Free identification: on-phone barcode/text/label reading, plus eBay barcode and photo matches when keys are set. */
    private suspend fun identifyFree(jpeg: ByteArray): ItemIdentification {
        val seen = OnDeviceReader.read(jpeg)
        val ebay = ebayApi()
        return withContext(Dispatchers.IO) {
            val barcodeTitles = if (ebay != null && seen.gtin != null) {
                runCatching { ebay.search(null, seen.gtin, limit = 20).map { it.title } }.getOrDefault(emptyList())
            } else emptyList()
            val similarTitles = if (ebay != null && barcodeTitles.isEmpty()) {
                runCatching { ebay.searchByImage(jpeg, limit = 12).map { it.title } }.getOrDefault(emptyList())
            } else emptyList()
            FreeIdentification.build(seen.gtin, seen.textLines, seen.labels, barcodeTitles, similarTitles)
        }
    }

    fun onPhoto(uri: Uri) {
        viewModelScope.launch {
            _screen.value = Screen.Busy("Loading photo…")
            runCatching { withContext(Dispatchers.IO) { Images.loadJpeg(getApplication(), uri) } }
                .onSuccess { _photo.value = it; _screen.value = Screen.Home }
                .onFailure { _screen.value = Screen.Failed("Couldn't read that photo: ${it.message}", false) }
        }
    }

    fun identify(hint: String) {
        val jpeg = _photo.value ?: return
        viewModelScope.launch {
            _screen.value = Screen.Busy("Identifying the item…")
            runCatching {
                if (settings.mode == Mode.FREE) {
                    identifyFree(jpeg).let { id ->
                        when {
                            hint.isBlank() -> id
                            // Nothing readable on the item: the seller's note becomes the search.
                            id.searchQuery.isBlank() -> id.copy(name = hint.trim(), searchQuery = hint.trim(), identified = true)
                            else -> id.copy(details = id.details + "\nYour note: " + hint.trim())
                        }
                    }
                } else {
                    withContext(Dispatchers.IO) { proFinder().identify(jpeg, hint) }
                }
            }
                .onSuccess { lastItem = it; _screen.value = Screen.Identified(it) }
                .onFailure { _screen.value = Screen.Failed(describe(it), false) }
        }
    }

    fun search(item: ItemIdentification, query: String) {
        val jpeg = _photo.value ?: return
        lastItem = item
        viewModelScope.launch {
            _screen.value = Screen.Busy("Searching…")
            runCatching {
                withContext(Dispatchers.IO) {
                    val progress = { msg: String -> _screen.value = Screen.Busy(msg) }
                    if (settings.mode == Mode.FREE) freeFinder().findComps(jpeg, item, query, progress)
                    else proFinder().findComps(jpeg, item, query, progress)
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
        _photo.value = null
        lastItem = null
        _screen.value = Screen.Home
    }

    private fun describe(t: Throwable): String {
        val msg = generateSequence(t) { it.cause }.mapNotNull { it.message }.firstOrNull() ?: t.javaClass.simpleName
        return when {
            msg.contains("401") || msg.contains("authentication", ignoreCase = true) ->
                "Your Anthropic API key was rejected. Check it in Settings."
            else -> msg
        }
    }
}
