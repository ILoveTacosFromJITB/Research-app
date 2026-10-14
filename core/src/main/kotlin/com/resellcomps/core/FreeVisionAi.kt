package com.resellcomps.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.addJsonObject
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.put
import kotlinx.serialization.json.putJsonArray
import kotlinx.serialization.json.putJsonObject
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.util.Base64

/**
 * Free AI identification through providers with no-card free tiers that host
 * vision models behind the common "chat completions" API:
 *  - Groq (keys start with "gsk_"): Meta Llama 4 vision models.
 *  - OpenRouter (keys start with "sk-or-"): its free router, which picks a free
 *    model that can read images.
 * The provider is detected from the key, so the user only pastes one key.
 */
class FreeVisionAi(
    private val apiKey: String,
    private val http: OkHttpClient = Http.client,
) {
    enum class Provider(val label: String, val baseUrl: String, val maxImages: Int) {
        GROQ("Groq", "https://api.groq.com/openai/v1", 5),
        OPENROUTER("OpenRouter", "https://openrouter.ai/api/v1", 6),
    }

    val provider: Provider = detect(apiKey)
        ?: throw IOException("That key isn't a Groq key (starts with gsk_) or an OpenRouter key (starts with sk-or-).")

    fun identify(photos: List<ByteArray>, hint: String? = null): ItemIdentification {
        val models = when (provider) {
            Provider.GROQ -> groqModels ?: pickGroqModels(get("${provider.baseUrl}/models")).also { groqModels = it }
            Provider.OPENROUTER -> listOf(OPENROUTER_FREE)
        }
        if (models.isEmpty()) throw IOException("No free vision model is available on your ${provider.label} account right now.")
        var last: IOException? = null
        for (model in models.take(2)) {
            try {
                return try {
                    chat(model, photos, hint)
                } catch (e: java.io.InterruptedIOException) {
                    chat(model, photos, hint) // one retry on a slow reply
                }
            } catch (e: LimitException) {
                last = e
            }
        }
        throw last ?: IOException("${provider.label} didn't answer.")
    }

    private class LimitException(msg: String) : IOException(msg)

    private fun chat(model: String, photos: List<ByteArray>, hint: String?): ItemIdentification {
        val body = buildJsonObject {
            put("model", model)
            put("temperature", 0.2)
            put("max_tokens", 800)
            putJsonArray("messages") {
                addJsonObject {
                    put("role", "system")
                    put("content", IdentifyPrompt.SYSTEM + "\n\n" + JSON_INSTRUCTIONS)
                }
                addJsonObject {
                    put("role", "user")
                    putJsonArray("content") {
                        addJsonObject { put("type", "text"); put("text", IdentifyPrompt.userText(photos.size, hint)) }
                        photos.take(provider.maxImages).forEach { photo ->
                            addJsonObject {
                                put("type", "image_url")
                                putJsonObject("image_url") {
                                    put("url", "data:image/jpeg;base64," + Base64.getEncoder().encodeToString(photo))
                                }
                            }
                        }
                    }
                }
            }
        }.toString()
        val req = Request.Builder()
            .url("${provider.baseUrl}/chat/completions")
            .post(body.toRequestBody("application/json".toMediaType()))
        return parseAnswer(execute(req))
    }

    private fun get(url: String): String = execute(Request.Builder().url(url).get())

    private fun execute(req: Request.Builder): String {
        val call = req.header("Authorization", "Bearer $apiKey")
            .apply { if (provider == Provider.OPENROUTER) header("X-Title", "Resell Comps") }
            .build()
        http.newCall(call).execute().use { resp ->
            val text = resp.body?.string().orEmpty()
            when {
                resp.isSuccessful -> return text
                resp.code == 401 -> throw IOException("${provider.label} rejected your key. Check it in Settings.")
                resp.code == 429 -> throw LimitException(
                    "${provider.label}'s free limit is used up for now. Try again in a minute, or tomorrow for the daily limit."
                )
                resp.code == 402 -> throw IOException("${provider.label} says this account needs credit for that request.")
                else -> throw IOException("${provider.label} error (HTTP ${resp.code}): ${errorMessage(text)}")
            }
        }
    }

    companion object {
        private val json = Json { ignoreUnknownKeys = true; isLenient = true }

        /** OpenRouter's router that picks a free model supporting the request (images included). */
        const val OPENROUTER_FREE = "openrouter/free"

        /** Used if Groq's model list can't be read; Groq's Llama 4 Scout vision model. */
        const val GROQ_FALLBACK = "meta-llama/llama-4-scout-17b-16e-instruct"

        @Volatile private var groqModels: List<String>? = null

        private val JSON_INSTRUCTIONS = """
            Reply with only a JSON object, no other text, with these keys:
            "identified" (true/false), "name", "brand" (or null), "model" (or null), "category",
            "details", "search_query", "confidence" ("high", "medium" or "low").
        """.trimIndent()

        fun detect(key: String): Provider? = when {
            key.startsWith("gsk_") -> Provider.GROQ
            key.startsWith("sk-or-") -> Provider.OPENROUTER
            else -> null
        }

        /** Groq's vision-capable models from its model list, best first: Llama 4 Maverick, Scout, then other "vision" models. */
        fun pickGroqModels(body: String): List<String> {
            val ids = ((json.parseToJsonElement(body) as? JsonObject)?.get("data") as? JsonArray).orEmpty()
                .mapNotNull { ((it as? JsonObject)?.get("id") as? JsonPrimitive)?.contentOrNull }
            fun rank(id: String) = when {
                "llama-4-maverick" in id -> 0
                "llama-4-scout" in id -> 1
                "vision" in id || "-vl" in id -> 2
                else -> null
            }
            val picked = ids.mapNotNull { id -> rank(id)?.let { id to it } }.sortedBy { it.second }.map { it.first }
            return picked.ifEmpty { listOf(GROQ_FALLBACK) }
        }

        fun parseAnswer(body: String): ItemIdentification {
            val root = json.parseToJsonElement(body) as? JsonObject ?: throw IOException("Unreadable answer from the AI.")
            val message = ((root["choices"] as? JsonArray)?.firstOrNull() as? JsonObject)?.get("message") as? JsonObject
                ?: throw IOException("The AI didn't answer.")
            val content = (message["content"] as? JsonPrimitive)?.contentOrNull.orEmpty()
            return IdentifyPrompt.fromJson(extractJsonObject(content))
        }

        /** Free models sometimes wrap the JSON in ```json fences or a sentence; take the outermost {...}. */
        fun extractJsonObject(text: String): JsonObject {
            val start = text.indexOf('{')
            val end = text.lastIndexOf('}')
            if (start < 0 || end <= start) throw IOException("The AI didn't return a usable answer. Try again.")
            return runCatching { json.parseToJsonElement(text.substring(start, end + 1)) as JsonObject }
                .getOrElse { throw IOException("The AI didn't return a usable answer. Try again.") }
        }

        private fun errorMessage(body: String): String = runCatching {
            val err = (json.parseToJsonElement(body) as JsonObject)["error"]
            ((err as? JsonObject)?.get("message") as? JsonPrimitive)?.content ?: (err as JsonPrimitive).content
        }.getOrNull() ?: body.take(200)
    }
}
