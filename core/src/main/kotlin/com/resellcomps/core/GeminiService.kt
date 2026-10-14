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
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.util.Base64

/**
 * Names the item with Google's Gemini API, which has a free tier (key from
 * Google AI Studio, no card). Model names change often, so the best available
 * "Flash" model is picked from the key's own model list instead of hardcoding one.
 */
class GeminiService(
    private val apiKey: String,
    private val http: OkHttpClient = Http.client,
) {
    fun identify(photos: List<ByteArray>, hint: String? = null): ItemIdentification {
        val models = cachedModels ?: listModels().also { cachedModels = it }
        if (models.isEmpty()) throw IOException("Your Gemini key has no Flash models available.")
        var lastError: IOException? = null
        // Best model first; if its free quota is used up, try the next (Flash-Lite has higher free limits).
        for (model in models.take(3)) {
            try {
                return try {
                    generate(model, photos, hint)
                } catch (e: java.io.InterruptedIOException) {
                    // One retry on a slow reply or a dropped mobile connection.
                    generate(model, photos, hint)
                }
            } catch (e: QuotaException) {
                lastError = e
            }
        }
        throw lastError ?: IOException("Gemini didn't answer.")
    }

    private class QuotaException(msg: String) : IOException(msg)

    private fun listModels(): List<String> {
        val url = "$BASE/models".toHttpUrl().newBuilder().addQueryParameter("pageSize", "1000").build()
        val body = call(Request.Builder().url(url).get())
        return pickModels(body)
    }

    private fun generate(model: String, photos: List<ByteArray>, hint: String?): ItemIdentification {
        val req = buildJsonObject {
            putJsonObject("systemInstruction") {
                putJsonArray("parts") { addJsonObject { put("text", IdentifyPrompt.SYSTEM) } }
            }
            putJsonArray("contents") {
                addJsonObject {
                    put("role", "user")
                    putJsonArray("parts") {
                        photos.take(ClaudeService.MAX_PHOTOS).forEach { photo ->
                            addJsonObject {
                                putJsonObject("inline_data") {
                                    put("mime_type", "image/jpeg")
                                    put("data", Base64.getEncoder().encodeToString(photo))
                                }
                            }
                        }
                        addJsonObject { put("text", IdentifyPrompt.userText(photos.size, hint)) }
                    }
                }
            }
            putJsonObject("generationConfig") {
                put("responseMimeType", "application/json")
                put("responseSchema", RESPONSE_SCHEMA)
            }
        }.toString()
        val url = "$BASE/models/$model:generateContent".toHttpUrl()
        val body = call(Request.Builder().url(url).post(req.toRequestBody("application/json".toMediaType())))
        return parseAnswer(body)
    }

    private fun call(req: Request.Builder): String {
        http.newCall(req.header("x-goog-api-key", apiKey).build()).execute().use { resp ->
            val text = resp.body?.string().orEmpty()
            when {
                resp.isSuccessful -> return text
                resp.code == 402 -> throw IOException(
                    "This Gemini key is on Google's paid plan and its prepaid credit is used up, so it isn't using the free tier. " +
                        "Fix: aistudio.google.com → Get API key → Create API key → \"Create API key in new project\" " +
                        "(a project with no billing), check it says Free, then paste it in Settings."
                )
                resp.code == 429 -> throw QuotaException("Gemini's free limit is used up for now. Try again in a minute, or tomorrow for the daily limit.")
                resp.code == 400 && text.contains("API_KEY_INVALID") -> throw IOException("Gemini rejected your API key. Check it in Settings.")
                resp.code == 403 -> throw IOException("Gemini refused the key (HTTP 403): ${errorMessage(text)}")
                else -> throw IOException("Gemini error (HTTP ${resp.code}): ${errorMessage(text)}")
            }
        }
    }

    companion object {
        private const val BASE = "https://generativelanguage.googleapis.com/v1beta"
        private val json = Json { ignoreUnknownKeys = true }

        @Volatile private var cachedModels: List<String>? = null

        private val RESPONSE_SCHEMA = buildJsonObject {
            put("type", "OBJECT")
            putJsonObject("properties") {
                putJsonObject("identified") { put("type", "BOOLEAN") }
                putJsonObject("name") { put("type", "STRING") }
                putJsonObject("brand") { put("type", "STRING"); put("nullable", true) }
                putJsonObject("model") { put("type", "STRING"); put("nullable", true) }
                putJsonObject("category") { put("type", "STRING") }
                putJsonObject("details") { put("type", "STRING") }
                putJsonObject("search_query") { put("type", "STRING") }
                putJsonObject("confidence") { put("type", "STRING") }
            }
            putJsonArray("required") {
                listOf("identified", "name", "category", "details", "search_query", "confidence").forEach { add(JsonPrimitive(it)) }
            }
        }

        private val FLASH = Regex("""^models/gemini-(\d+(?:\.\d+)?)-flash(-lite)?$""")

        /**
         * From a ListModels response, the stable "gemini-X-flash" models that can generate
         * content, newest first, with full Flash ahead of Flash-Lite at the same version.
         */
        fun pickModels(body: String): List<String> {
            val root = json.parseToJsonElement(body) as? JsonObject ?: return emptyList()
            return (root["models"] as? JsonArray).orEmpty().mapNotNull { el ->
                val o = el as? JsonObject ?: return@mapNotNull null
                val name = (o["name"] as? JsonPrimitive)?.contentOrNull ?: return@mapNotNull null
                val methods = (o["supportedGenerationMethods"] as? JsonArray).orEmpty().mapNotNull { (it as? JsonPrimitive)?.contentOrNull }
                if ("generateContent" !in methods) return@mapNotNull null
                val m = FLASH.find(name) ?: return@mapNotNull null
                Triple(name.removePrefix("models/"), m.groupValues[1].toDouble(), m.groupValues[2].isNotEmpty())
            }
                .sortedWith(compareByDescending<Triple<String, Double, Boolean>> { it.second }.thenBy { it.third })
                .map { it.first }
        }

        fun parseAnswer(body: String): ItemIdentification {
            val root = json.parseToJsonElement(body) as? JsonObject ?: throw IOException("Gemini returned an unreadable answer.")
            val candidate = (root["candidates"] as? JsonArray)?.firstOrNull() as? JsonObject
                ?: throw IOException("Gemini didn't answer (the photo may have been blocked by its safety filter).")
            val text = ((candidate["content"] as? JsonObject)?.get("parts") as? JsonArray).orEmpty()
                .mapNotNull { ((it as? JsonObject)?.get("text") as? JsonPrimitive)?.contentOrNull }
                .joinToString("")
            val o = runCatching { json.parseToJsonElement(text) as JsonObject }
                .getOrElse { throw IOException("Gemini returned an unreadable answer.") }
            return IdentifyPrompt.fromJson(o)
        }

        private fun errorMessage(body: String): String = runCatching {
            (((json.parseToJsonElement(body) as JsonObject)["error"] as JsonObject)["message"] as JsonPrimitive).content
        }.getOrNull() ?: body.take(200)
    }
}
