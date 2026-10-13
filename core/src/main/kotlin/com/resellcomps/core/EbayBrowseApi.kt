package com.resellcomps.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.longOrNull
import kotlinx.serialization.json.put
import okhttp3.Credentials
import okhttp3.FormBody
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.io.IOException
import java.util.Base64
import java.util.concurrent.TimeUnit

/**
 * eBay's official Browse API (https://developer.ebay.com/api-docs/buy/browse/overview.html).
 * Free with an eBay developer account. It returns ACTIVE listings only; eBay
 * does not offer sold data through its free APIs.
 */
class EbayBrowseApi(
    private val clientId: String,
    private val clientSecret: String,
    private val http: OkHttpClient = OkHttpClient.Builder().callTimeout(60, TimeUnit.SECONDS).build(),
) {
    /** Keyword and/or barcode search. Fixed-price listings only, since an auction's current bid isn't a price. */
    fun search(query: String?, gtin: String? = null, limit: Int = 50): List<Listing> {
        val url = searchUrl("search", limit).newBuilder()
            .apply { if (!query.isNullOrBlank()) addQueryParameter("q", query) }
            .apply { if (!gtin.isNullOrBlank()) addQueryParameter("gtin", gtin) }
            .build()
        return parseSearch(execute(Request.Builder().url(url).get()))
    }

    /** eBay's visual search: listings that look like the photo. */
    fun searchByImage(jpeg: ByteArray, limit: Int = 50): List<Listing> {
        val body = buildJsonObject { put("image", Base64.getEncoder().encodeToString(jpeg)) }.toString()
        val req = Request.Builder()
            .url(searchUrl("search_by_image", limit))
            .post(body.toRequestBody("application/json".toMediaType()))
        return parseSearch(execute(req))
    }

    private fun searchUrl(endpoint: String, limit: Int): HttpUrl =
        "https://api.ebay.com/buy/browse/v1/item_summary/$endpoint".toHttpUrl().newBuilder()
            .addQueryParameter("limit", limit.coerceIn(1, 200).toString())
            .addQueryParameter("filter", "buyingOptions:{FIXED_PRICE}")
            .build()

    private fun execute(req: Request.Builder): String {
        val call = req
            .header("Authorization", "Bearer ${token()}")
            .header("X-EBAY-C-MARKETPLACE-ID", "EBAY_US")
            .build()
        http.newCall(call).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) throw IOException("eBay API error (HTTP ${resp.code}): ${errorMessage(body)}")
            return body
        }
    }

    private fun token(): String {
        val now = System.currentTimeMillis()
        tokens[clientId]?.takeIf { it.second > now + 60_000 }?.let { return it.first }
        val req = Request.Builder()
            .url("https://api.ebay.com/identity/v1/oauth2/token")
            .header("Authorization", Credentials.basic(clientId, clientSecret))
            .post(FormBody.Builder()
                .add("grant_type", "client_credentials")
                .add("scope", "https://api.ebay.com/oauth/api_scope")
                .build())
            .build()
        http.newCall(req).execute().use { resp ->
            val body = resp.body?.string().orEmpty()
            if (!resp.isSuccessful) {
                throw IOException("eBay rejected your Client ID / Secret (HTTP ${resp.code}). Check them in Settings.")
            }
            val o = json.parseToJsonElement(body) as JsonObject
            val token = (o["access_token"] as JsonPrimitive).content
            val expiresIn = (o["expires_in"] as? JsonPrimitive)?.longOrNull ?: 3600
            tokens[clientId] = token to now + expiresIn * 1000
            return token
        }
    }

    companion object {
        private val json = Json { ignoreUnknownKeys = true }
        private val tokens = java.util.concurrent.ConcurrentHashMap<String, Pair<String, Long>>()

        private fun errorMessage(body: String): String = runCatching {
            val errors = (json.parseToJsonElement(body) as JsonObject)["errors"] as JsonArray
            ((errors[0] as JsonObject)["message"] as JsonPrimitive).content
        }.getOrNull() ?: body.take(200)

        fun parseSearch(body: String): List<Listing> {
            val root = json.parseToJsonElement(body) as? JsonObject ?: return emptyList()
            val items = root["itemSummaries"] as? JsonArray ?: return emptyList()
            return items.mapNotNull { el ->
                val o = el as? JsonObject ?: return@mapNotNull null
                val title = o.str("title") ?: return@mapNotNull null
                val url = o.str("itemWebUrl") ?: return@mapNotNull null
                val price = o["price"] as? JsonObject ?: return@mapNotNull null
                val value = (price["value"] as? JsonPrimitive)?.contentOrNull?.toDoubleOrNull() ?: return@mapNotNull null
                val currency = price.str("currency") ?: "USD"
                if (value <= 0) return@mapNotNull null
                Listing(
                    title = title,
                    price = value,
                    priceText = formatMoney(value, currency),
                    currency = currency,
                    url = url,
                    status = ListingStatus.ACTIVE,
                    source = ListingSource.EBAY_API,
                    marketplace = "eBay",
                    condition = o.str("condition"),
                    shipping = shipping(o),
                    thumbnailUrl = (o["image"] as? JsonObject)?.str("imageUrl")
                        ?: ((o["thumbnailImages"] as? JsonArray)?.firstOrNull() as? JsonObject)?.str("imageUrl"),
                )
            }
        }

        private fun shipping(o: JsonObject): String? {
            val opt = (o["shippingOptions"] as? JsonArray)?.firstOrNull() as? JsonObject ?: return null
            val cost = opt["shippingCost"] as? JsonObject ?: return null
            val v = (cost["value"] as? JsonPrimitive)?.contentOrNull?.toDoubleOrNull() ?: return null
            return if (v == 0.0) "Free shipping" else "+${formatMoney(v, cost.str("currency") ?: "USD")} shipping"
        }

        fun formatMoney(v: Double, currency: String): String =
            if (currency == "USD") "$" + "%,.2f".format(v) else "%,.2f %s".format(v, currency)

        private fun JsonObject.str(key: String): String? =
            (this[key] as? JsonPrimitive)?.takeIf { it.isString }?.contentOrNull?.takeIf { it.isNotBlank() }

    }
}
