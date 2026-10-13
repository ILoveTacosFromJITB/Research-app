package com.resellcomps.app

import android.content.Context
import com.resellcomps.core.ClaudeService

/** API keys live in this app's private storage on the phone and are only sent to their own services. */
class Settings(context: Context) {
    private val prefs = context.getSharedPreferences("settings", Context.MODE_PRIVATE)

    var anthropicKey: String
        get() = prefs.getString("anthropic_key", "").orEmpty()
        set(v) = prefs.edit().putString("anthropic_key", v.trim()).apply()

    var serpApiKey: String
        get() = prefs.getString("serpapi_key", "").orEmpty()
        set(v) = prefs.edit().putString("serpapi_key", v.trim()).apply()

    var model: String
        get() = prefs.getString("model", null)?.takeIf { it.isNotBlank() } ?: ClaudeService.DEFAULT_MODEL
        set(v) = prefs.edit().putString("model", v.trim()).apply()
}
