package com.resellcomps.app

import android.content.Context
import com.resellcomps.core.ClaudeService

enum class Mode { FREE, PRO }

/** Keys live in this app's private storage on the phone and are only sent to their own services. */
class Settings(context: Context) {
    private val prefs = context.getSharedPreferences("settings", Context.MODE_PRIVATE)

    private fun str(key: String) = prefs.getString(key, "").orEmpty()
    private fun put(key: String, v: String) = prefs.edit().putString(key, v.trim()).apply()

    var mode: Mode
        get() = runCatching { Mode.valueOf(str("mode")) }.getOrDefault(Mode.FREE)
        set(v) = put("mode", v.name)

    // Free mode
    var ebayClientId: String
        get() = str("ebay_client_id")
        set(v) = put("ebay_client_id", v)

    var ebayClientSecret: String
        get() = str("ebay_client_secret")
        set(v) = put("ebay_client_secret", v)

    /** Free AI naming with no Google: a Groq (gsk_…) or OpenRouter (sk-or-…) key. */
    var freeAiKey: String
        get() = str("free_ai_key")
        set(v) = put("free_ai_key", v)

    /** Free AI naming: Gemini API key from Google AI Studio. */
    var geminiKey: String
        get() = str("gemini_key")
        set(v) = put("gemini_key", v)

    /** Built-in Lens: Google Cloud Vision web detection. */
    var googleVisionKey: String
        get() = str("google_vision_key")
        set(v) = put("google_vision_key", v)

    var readSoldPage: Boolean
        get() = prefs.getBoolean("read_sold_page", false)
        set(v) = prefs.edit().putBoolean("read_sold_page", v).apply()

    // Pro mode
    var anthropicKey: String
        get() = str("anthropic_key")
        set(v) = put("anthropic_key", v)

    var serpApiKey: String
        get() = str("serpapi_key")
        set(v) = put("serpapi_key", v)

    var model: String
        get() = str("model").ifBlank { ClaudeService.DEFAULT_MODEL }
        set(v) = put("model", v)

    var seenSettings: Boolean
        get() = prefs.getBoolean("seen_settings", false)
        set(v) = prefs.edit().putBoolean("seen_settings", v).apply()

    val hasEbayKeys: Boolean get() = ebayClientId.isNotBlank() && ebayClientSecret.isNotBlank()
}
