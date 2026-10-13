package com.resellcomps.app

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.resellcomps.core.ClaudeService
import com.resellcomps.core.CompsFinder
import com.resellcomps.core.CompsReport
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

    val hasKeys: Boolean get() = settings.anthropicKey.isNotBlank()

    private fun finder() = CompsFinder(
        claude = ClaudeService(settings.anthropicKey, settings.model),
        ebay = settings.serpApiKey.takeIf { it.isNotBlank() }?.let { SerpApiEbay(it) },
    )

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
            runCatching { withContext(Dispatchers.IO) { finder().identify(jpeg, hint) } }
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
                    finder().findComps(jpeg, item, query) { msg -> _screen.value = Screen.Busy(msg) }
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
