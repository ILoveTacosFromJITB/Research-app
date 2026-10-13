package com.resellcomps.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.addJsonObject
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.put
import kotlinx.serialization.json.putJsonArray
import kotlinx.serialization.json.putJsonObject
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.net.URI
import java.util.Base64
import java.util.concurrent.TimeUnit

/** A web page that shows the same (or nearly the same) image, like a Google Lens result. */
data class VisualMatch(val title: String, val url: String, val site: String, val isMarketplace: Boolean)

/**
 * The app's built-in "Lens": Google Cloud Vision web detection
 * (https://cloud.google.com/vision/docs/detecting-web) finds where the photo
 * appears on the web and Google's best guess for what it is, plus logo detection
 * for the brand. 1,000 free lookups per feature per month.
 */
class GoogleVision(
    private val apiKey: String,
    private val http: OkHttpClient = OkHttpClient.Builder().callTimeout(60, TimeUnit.SECONDS).build(),
) {
    data class Result(
        val bestGuesses: List<String>,
        val entities: List<String>,
        val logos: List<String>,
        val matches: List<VisualMatch>,
    ) {
        fun isEmpty() = bestGuesses.isEmpty() && entities.isEmpty() && logos.isEmpty() && matches.isEmpty()
    }

    /** Looks up each photo (web + logo detection) in one request and merges the answers. */
    fun detect(photos: List<ByteArray>): Result {
        val body = buildJsonObject {
            putJsonArray("requests") {
                photos.forEach { photo ->
                    addJsonObject {
                        putJsonObject("image") { put("content", Base64.getEncoder().encodeToString(photo)) }
                        putJsonArray("features") {
                            addJsonObject { put("type", "WEB_DETECTION"); put("maxResults", 30) }
                            addJsonObject { put("type", "LOGO_DETECTION"); put("maxResults", 3) }
                        }
                        putJsonObject("imageContext") {
                            putJsonObject("webDetectionParams") { put("includeGeoResults", false) }
                        }
                    }
                }
            }
        }.toString()
        val url = "https://vision.googleapis.com/v1/images:annotate".toHttpUrl().newBuilder()
            .addQueryParameter("key", apiKey).build()
        val req = Request.Builder().url(url).post(body.toRequestBody("application/json".toMediaType())).build()
        http.newCall(req).execute().use { resp ->
            val text = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) throw IOException("Google Vision error (HTTP ${resp.code}): ${errorMessage(text)}")
            return parse(text)
        }
    }

    companion object {
        private val json = Json { ignoreUnknownKeys = true }

        private val MARKETPLACES = listOf(
            "ebay.", "poshmark.", "mercari.", "grailed.", "depop.", "etsy.", "stockx.", "goat.",
            "amazon.", "offerup.", "reverb.", "swappa.", "therealreal.", "vestiairecollective.",
            "thredup.", "backmarket.", "walmart.", "facebook.com/marketplace", "vinted.", "whatnot.",
        )

        fun isMarketplace(url: String) = MARKETPLACES.any { url.lowercase().contains(it) }

        private fun errorMessage(body: String): String = runCatching {
            (((json.parseToJsonElement(body) as JsonObject)["error"] as JsonObject)["message"] as JsonPrimitive).content
        }.getOrNull() ?: body.take(200)

        fun parse(body: String): Result {
            val root = json.parseToJsonElement(body) as? JsonObject ?: return Result(emptyList(), emptyList(), emptyList(), emptyList())
            val responses = (root["responses"] as? JsonArray).orEmpty().mapNotNull { it as? JsonObject }
            val guesses = LinkedHashSet<String>()
            val entities = mutableListOf<Pair<String, Double>>()
            val logos = LinkedHashSet<String>()
            val matches = LinkedHashMap<String, VisualMatch>()
            for (r in responses) {
                (r["error"] as? JsonObject)?.let { e ->
                    throw IOException("Google Vision: " + ((e["message"] as? JsonPrimitive)?.content ?: "error"))
                }
                (r["logoAnnotations"] as? JsonArray).orEmpty().mapNotNull { (it as? JsonObject)?.str("description") }.forEach { logos += it }
                val web = r["webDetection"] as? JsonObject ?: continue
                (web["bestGuessLabels"] as? JsonArray).orEmpty().mapNotNull { (it as? JsonObject)?.str("label") }.forEach { guesses += it }
                (web["webEntities"] as? JsonArray).orEmpty().mapNotNull { it as? JsonObject }.forEach { e ->
                    val d = e.str("description") ?: return@forEach
                    entities += d to ((e["score"] as? JsonPrimitive)?.doubleOrNull ?: 0.0)
                }
                (web["pagesWithMatchingImages"] as? JsonArray).orEmpty().mapNotNull { it as? JsonObject }.forEach { p ->
                    val url = p.str("url") ?: return@forEach
                    if (url in matches) return@forEach
                    val site = runCatching { URI(url).host.removePrefix("www.") }.getOrNull() ?: return@forEach
                    val title = cleanTitle(p.str("pageTitle") ?: site)
                    matches[url] = VisualMatch(title, url, site, isMarketplace(url))
                }
            }
            return Result(
                bestGuesses = guesses.toList(),
                entities = entities.sortedByDescending { it.second }.map { it.first }.distinctBy { it.lowercase() },
                logos = logos.toList(),
                // Marketplace listings first, like the "shopping" results in Lens.
                matches = matches.values.sortedByDescending { it.isMarketplace },
            )
        }

        /** Vision returns page titles with <b> tags and HTML entities. */
        fun cleanTitle(t: String): String = t
            .replace(Regex("<[^>]+>"), "")
            .replace("&amp;", "&").replace("&quot;", "\"").replace("&#39;", "'")
            .replace("&lt;", "<").replace("&gt;", ">")
            .replace(Regex("\\s+"), " ")
            .replace(Regex("""\s*[|\-–]\s*(eBay|Poshmark|Mercari|Grailed|Depop|Etsy|Amazon\.com|StockX)\s*$""", RegexOption.IGNORE_CASE), "")
            .trim()

        /**
         * A search built the way a reseller would use Lens: the words that keep
         * showing up in marketplace listings of the same photo, else Google's best
         * guess, with the detected brand in front.
         */
        fun query(result: Result): String {
            val marketTitles = result.matches.filter { it.isMarketplace }.map { it.title }
            val fromListings = if (marketTitles.size >= 2) FreeIdentification.commonWords(marketTitles, minShare = 0.4) else ""
            val base = when {
                fromListings.split(" ").size >= 2 -> fromListings
                result.bestGuesses.isNotEmpty() -> result.bestGuesses.first()
                marketTitles.isNotEmpty() -> TitleMatcher.tokens(marketTitles.first()).take(6).joinToString(" ")
                else -> result.entities.take(2).joinToString(" ")
            }.lowercase().trim()
            val brand = result.logos.firstOrNull()?.lowercase()
            return if (brand != null && TitleMatcher.tokens(brand).any { it !in TitleMatcher.tokens(base) }) "$brand $base".trim() else base
        }

        private fun JsonObject.str(key: String): String? =
            (this[key] as? JsonPrimitive)?.takeIf { it.isString }?.contentOrNull?.takeIf { it.isNotBlank() }

    }
}
