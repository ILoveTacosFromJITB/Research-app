package com.resellcomps.app

import android.content.Context

/** The key lives in this app's private storage on the phone and is only sent to its own service. */
class Settings(context: Context) {
    private val prefs = context.getSharedPreferences("settings", Context.MODE_PRIVATE)

    /** Free AI identification: a Groq (gsk_…) or OpenRouter (sk-or-…) key. */
    var freeAiKey: String
        get() = prefs.getString("free_ai_key", "").orEmpty()
        set(v) = prefs.edit().putString("free_ai_key", v.trim()).apply()

    var seenSettings: Boolean
        get() = prefs.getBoolean("seen_settings", false)
        set(v) = prefs.edit().putBoolean("seen_settings", v).apply()
}
