package com.resellcomps.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.doubleOrNull
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.OkHttpClient
import okhttp3.Request
import java.io.IOException

/**
 * eBay sold and active listings through SerpApi's eBay engine
 * (https://serpapi.com/ebay-search-api). Titles, prices and links come
 * straight from eBay's results page; nothing is inferred.
 */
class SerpApiEbay(
    private val apiKey: String,
    private val http: OkHttpClient = Http.client,
) {

    fun search(query: String, status: ListingStatus, limit: Int = 50): List<Listing> {
        val url = "https://serpapi.com/search.json".toHttpUrl().newBuilder()
            .addQueryParameter("engine", "ebay")
            .addQueryParameter("ebay_domain", "ebay.com")
            .addQueryParameter("_nkw", query)
            .addQueryParameter("_ipg", "60")
            .apply {
                if (status == ListingStatus.SOLD) {
                    addQueryParameter("LH_Sold", "1")
                    addQueryParameter("LH_Complete", "1")
                }
            }
            .addQueryParameter("api_key", apiKey)
            .build()
        http.newCall(Request.Builder().url(url).build()).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                throw IOException("SerpApi eBay search failed (HTTP ${resp.code}): ${errorMessage(body)}")
            }
            return parse(body, status).take(limit)
        }
    }

    companion object {
        private val json = Json { ignoreUnknownKeys = true }

        private fun errorMessage(body: String): String = runCatching {
            (json.parseToJsonElement(body) as JsonObject).string("error")
        }.getOrNull() ?: body.take(200)

        /** Parses a SerpApi eBay response. Results without a usable price or link are dropped. */
        fun parse(body: String, status: ListingStatus): List<Listing> {
            val root = json.parseToJsonElement(body) as? JsonObject ?: return emptyList()
            root.string("error")?.let { err ->
                // "no results" is a normal outcome, anything else is a real failure
                if (err.contains("hasn't returned any results", ignoreCase = true)) return emptyList()
                throw IOException("SerpApi: $err")
            }
            val results = root["organic_results"] as? JsonArray ?: return emptyList()
            return results.mapNotNull { el ->
                val o = el as? JsonObject ?: return@mapNotNull null
                val title = o.string("title") ?: return@mapNotNull null
                val link = o.string("link") ?: return@mapNotNull null
                val (price, priceText) = price(o["price"]) ?: return@mapNotNull null
                // eBay pads results with "Shop on eBay" placeholders
                if (title.equals("Shop on eBay", ignoreCase = true)) return@mapNotNull null
                Listing(
                    title = title,
                    price = price,
                    priceText = priceText,
                    currency = "USD",
                    url = link,
                    status = status,
                    source = ListingSource.EBAY,
                    marketplace = "eBay",
                    condition = o.string("condition"),
                    shipping = shipping(o["shipping"]),
                    thumbnailUrl = o.string("thumbnail"),
                    soldDate = o.string("sold_date") ?: o.string("date")
                        ?: (o["extensions"] as? JsonArray)?.mapNotNull { it.primitiveString() }
                            ?.firstOrNull { it.startsWith("Sold", ignoreCase = true) },
                )
            }
        }

        /**
         * Price is either {raw, extracted} or a range {from:{...}, to:{...}}.
         * Ranges (multi-variant listings) are skipped: no single price backs them.
         */
        private fun price(el: JsonElement?): Pair<Double, String>? {
            val o = el as? JsonObject ?: return null
            if (o.containsKey("from") || o.containsKey("to")) return null
            val extracted = (o["extracted"] as? JsonPrimitive)?.doubleOrNull ?: return null
            val raw = o.string("raw") ?: "$%.2f".format(extracted)
            if (raw.contains(" to ", ignoreCase = true)) return null
            return extracted to raw
        }

        private fun shipping(el: JsonElement?): String? = when (el) {
            is JsonPrimitive -> el.contentOrNull
            is JsonObject -> el.string("raw")
                ?: (el["extracted"] as? JsonPrimitive)?.doubleOrNull?.let { "+$%.2f shipping".format(it) }
            else -> null
        }

        private fun JsonObject.string(key: String): String? = this[key].primitiveString()

        private fun JsonElement?.primitiveString(): String? =
            (this as? JsonPrimitive)?.takeIf { it.isString }?.contentOrNull?.takeIf { it.isNotBlank() }
    }
}
