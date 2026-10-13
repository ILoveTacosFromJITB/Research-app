package com.resellcomps.core

import com.anthropic.client.AnthropicClient
import com.anthropic.client.okhttp.AnthropicOkHttpClient
import com.anthropic.core.JsonValue
import com.anthropic.models.messages.Base64ImageSource
import com.anthropic.models.messages.ContentBlockParam
import com.anthropic.models.messages.ImageBlockParam
import com.anthropic.models.messages.JsonOutputFormat
import com.anthropic.models.messages.Message
import com.anthropic.models.messages.MessageCreateParams
import com.anthropic.models.messages.OutputConfig
import com.anthropic.models.messages.StopReason
import com.anthropic.models.messages.TextBlockParam
import com.anthropic.models.messages.Tool
import com.anthropic.models.messages.WebSearchTool20260209
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import java.util.Base64

class ClaudeException(message: String) : Exception(message)

/**
 * The three things Claude does in this app: name the item in the photo, search
 * the web for listings of it, and judge which listings are the same item.
 * Claude never produces a price on its own: prices come from listings.
 */
class ClaudeService(
    apiKey: String,
    private val model: String = DEFAULT_MODEL,
) {
    private val client: AnthropicClient = AnthropicOkHttpClient.builder()
        .apiKey(apiKey)
        .build()

    // ---------------------------------------------------------------- identify

    fun identify(photos: List<ByteArray>, userHint: String? = null): ItemIdentification {
        val prompt = buildString {
            append(
                if (photos.size > 1) "These ${photos.size} photos all show the same item (e.g. front, back, tag, label). "
                else ""
            )
            append("Identify the item so a reseller can look up comparable listings.")
            if (!userHint.isNullOrBlank()) append("\n\nThe seller adds: ").append(userHint)
        }
        val params = base(OutputConfig.Effort.MEDIUM, IDENTIFY_SCHEMA)
            .system(IDENTIFY_SYSTEM)
            .addUserMessageOfBlockParams(images(photos) + text(prompt))
            .build()
        val o = parseJsonText(send(params))
        return ItemIdentification(
            identified = o.bool("identified") ?: false,
            name = o.str("name") ?: "Unknown item",
            brand = o.str("brand"),
            model = o.str("model"),
            category = o.str("category") ?: "",
            details = o.str("details") ?: "",
            searchQuery = o.str("search_query") ?: "",
            confidence = o.str("confidence") ?: "low",
        )
    }

    // ------------------------------------------------------------- web comps

    data class WebCompsResult(val listings: List<Listing>, val rejectedUnverified: Int, val note: String?)

    /**
     * Lets Claude search the web for listings, then keeps only the ones whose
     * URL really came back from the search tool.
     */
    fun findWebComps(photos: List<ByteArray>, item: ItemIdentification, query: String): WebCompsResult {
        val prompt = """
            Item: ${item.name}
            Brand: ${item.brand ?: "unknown"} | Model: ${item.model ?: "unknown"} | Category: ${item.category}
            Details: ${item.details}
            Suggested search: $query

            Find sold listings and active listings of this exact item on resale marketplaces and
            other sites, then call submit_comps.
        """.trimIndent()

        val builder = base(OutputConfig.Effort.MEDIUM, null)
            .system(WEB_SYSTEM)
            .addTool(WebSearchTool20260209.builder().maxUses(6L).build())
            .addTool(SUBMIT_COMPS_TOOL)
            .addUserMessageOfBlockParams(images(photos) + text(prompt))

        val seenUrls = mutableListOf<String>()
        // Server-side search can pause a long turn; resume by sending the turn back.
        var msg = send(builder.build())
        collectServerUrls(msg, seenUrls)
        var resumes = 0
        while (msg.stopReason().orElse(null) == StopReason.PAUSE_TURN && resumes++ < 3) {
            builder.addMessage(msg)
            msg = send(builder.build())
            collectServerUrls(msg, seenUrls)
        }
        val call = msg.content().firstNotNullOfOrNull { b ->
            b.toolUse().orElse(null)?.takeIf { it.name() == SUBMIT_COMPS }
        } ?: return WebCompsResult(emptyList(), 0, "Web search finished without reporting any listings.")

        val input = Json.parseToJsonElement(jsonOf(call._input())) as? JsonObject
            ?: return WebCompsResult(emptyList(), 0, "Web search returned an unreadable result.")
        val verifier = UrlVerifier(seenUrls)
        var rejected = 0
        val listings = (input["listings"] as? JsonArray).orEmpty().mapNotNull { el ->
            val o = el as? JsonObject ?: return@mapNotNull null
            val url = o.str("url") ?: return@mapNotNull null
            val price = (o["price"] as? JsonPrimitive)?.contentOrNull?.toDoubleOrNull()
            val title = o.str("title")
            if (price == null || price <= 0 || title == null) return@mapNotNull null
            if (!verifier.isVerified(url)) {
                rejected++
                return@mapNotNull null
            }
            Listing(
                title = title,
                price = price,
                priceText = o.str("price_text") ?: "$%.2f".format(price),
                currency = o.str("currency") ?: "USD",
                url = url,
                status = if (o.str("status") == "sold") ListingStatus.SOLD else ListingStatus.ACTIVE,
                source = ListingSource.WEB,
                marketplace = o.str("marketplace") ?: hostOf(url),
                condition = o.str("condition"),
                soldDate = o.str("sold_date"),
                evidence = o.str("evidence"),
            )
        }
        return WebCompsResult(listings, rejected, null)
    }

    // -------------------------------------------------------- relevance check

    /**
     * Asks Claude which listings are the same item as the photo. Only a yes/no
     * per listing comes back; prices are never touched here.
     */
    fun checkMatches(photos: List<ByteArray>, item: ItemIdentification, listings: List<Listing>): List<CheckedListing> {
        if (listings.isEmpty()) return emptyList()
        val table = listings.mapIndexed { i, l ->
            "[$i] ${l.marketplace} | ${l.status.name.lowercase()} | ${l.priceText} | " +
                "${l.condition ?: "condition n/a"} | ${l.title}"
        }.joinToString("\n")
        val prompt = """
            The photo shows: ${item.name}
            Brand: ${item.brand ?: "unknown"} | Model: ${item.model ?: "unknown"}
            Details: ${item.details}

            For every listing below, decide whether it is the same item (same product, model and
            variant) as the photo, so its price is a fair comparison. Return a verdict for every index.

            $table
        """.trimIndent()
        val params = base(OutputConfig.Effort.LOW, MATCH_SCHEMA)
            .system(MATCH_SYSTEM)
            .addUserMessageOfBlockParams(images(photos) + text(prompt))
            .build()
        val verdicts = (parseJsonText(send(params))["verdicts"] as? JsonArray).orEmpty()
            .mapNotNull { it as? JsonObject }
            .mapNotNull { v -> (v["index"] as? JsonPrimitive)?.intOrNull?.let { it to v } }
            .toMap()
        return listings.mapIndexed { i, l ->
            val v = verdicts[i]
            CheckedListing(
                listing = l,
                isMatch = v?.bool("match") ?: false,
                reason = v?.str("reason") ?: "Not checked",
            )
        }
    }

    // ---------------------------------------------------------------- plumbing

    private fun base(effort: OutputConfig.Effort, schema: Map<String, Any>?): MessageCreateParams.Builder {
        val output = OutputConfig.builder().effort(effort)
        if (schema != null) {
            output.format(
                JsonOutputFormat.builder()
                    .schema(JsonOutputFormat.Schema.builder()
                        .putAllAdditionalProperties(schema.mapValues { JsonValue.from(it.value) })
                        .build())
                    .build()
            )
        }
        val builder = MessageCreateParams.builder()
            .model(model)
            .maxTokens(16000L)
            .outputConfig(output.build())
        if (supportsServerFallback(model)) {
            // If a safety classifier declines, retry server-side on a fallback model.
            builder.putAdditionalHeader("anthropic-beta", "server-side-fallback-2026-07-01")
                .putAdditionalBodyProperty("fallbacks", JsonValue.from("default"))
        }
        return builder
    }

    private fun send(params: MessageCreateParams): Message {
        val msg = client.messages().create(params)
        when (msg.stopReason().orElse(null)) {
            StopReason.REFUSAL -> throw ClaudeException(
                "Claude declined this request" +
                    (msg.stopDetails().orElse(null)?.explanation()?.orElse(null)?.let { ": $it" } ?: ".")
            )
            StopReason.MAX_TOKENS -> throw ClaudeException("Claude's answer was cut off. Please try again.")
            else -> {}
        }
        return msg
    }

    private fun parseJsonText(msg: Message): JsonObject {
        val textOut = msg.content().mapNotNull { it.text().orElse(null)?.text() }.joinToString("")
        return runCatching { Json.parseToJsonElement(textOut) as JsonObject }
            .getOrElse { throw ClaudeException("Claude returned an unreadable answer.") }
    }

    private fun collectServerUrls(msg: Message, into: MutableList<String>) {
        for (block in msg.content()) {
            block.webSearchToolResult().ifPresent { r ->
                r.content().resultBlocks().ifPresent { results -> results.forEach { into += it.url() } }
            }
            // With dynamic filtering, search results can also surface inside code-execution output.
            block.codeExecutionToolResult().ifPresent { into += UrlVerifier.extractUrls(it.toString()) }
            block.bashCodeExecutionToolResult().ifPresent { into += UrlVerifier.extractUrls(it.toString()) }
            block.webFetchToolResult().ifPresent { into += UrlVerifier.extractUrls(it.toString()) }
        }
    }

    private fun images(photos: List<ByteArray>): List<ContentBlockParam> =
        photos.take(MAX_PHOTOS).map(::image)

    private fun image(jpeg: ByteArray) = ContentBlockParam.ofImage(
        ImageBlockParam.builder()
            .source(Base64ImageSource.builder()
                .mediaType(Base64ImageSource.MediaType.IMAGE_JPEG)
                .data(Base64.getEncoder().encodeToString(jpeg))
                .build())
            .build()
    )

    private fun text(s: String) = ContentBlockParam.ofText(TextBlockParam.builder().text(s).build())

    companion object {
        const val DEFAULT_MODEL = "claude-opus-5-5"

        /** Cheapest model; used only to name the item in free mode (no web search). */
        const val LOW_COST_MODEL = "claude-haiku-5-5"

        const val MAX_PHOTOS = 6

        internal fun supportsServerFallback(model: String) =
            model.startsWith("claude-opus") || model.startsWith("claude-sonnet") || model.startsWith("claude-fable")
        private const val SUBMIT_COMPS = "submit_comps"

        private fun hostOf(url: String) =
            runCatching { java.net.URI(url).host.removePrefix("www.") }.getOrNull() ?: "web"

        internal fun jsonOf(v: JsonValue): String =
            com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(v.convert(Any::class.java))

        private fun JsonObject.str(key: String): String? =
            (this[key] as? JsonPrimitive)?.takeIf { it.isString }?.contentOrNull?.takeIf { it.isNotBlank() }

        private fun JsonObject.bool(key: String): Boolean? = (this[key] as? JsonPrimitive)?.booleanOrNull

        private fun nullableString(description: String) =
            mapOf("type" to listOf("string", "null"), "description" to description)

        private fun obj(props: Map<String, Any>) = mapOf(
            "type" to "object",
            "properties" to props,
            "required" to props.keys.toList(),
            "additionalProperties" to false,
        )

        private val IDENTIFY_SYSTEM = """
            You identify secondhand items from photos for a reseller who will look up comparable
            sold listings. Be as specific as the photo allows: brand, product line, model name or
            number, size, color, material, edition or year. Read any visible labels, tags, logos or
            model numbers. Do not guess a brand or model you cannot see evidence for; leave it null
            and lower your confidence instead. Never estimate a price or value.
            search_query is what a reseller would type into eBay to find this exact item: the most
            identifying terms only, usually 3-8 words, no condition words. If the photo does not
            show an identifiable item, set identified to false and explain in details.
        """.trimIndent()

        private val IDENTIFY_SCHEMA = obj(mapOf(
            "identified" to mapOf("type" to "boolean"),
            "name" to mapOf("type" to "string", "description" to "Short descriptive name of the item"),
            "brand" to nullableString("Brand, or null if not visible"),
            "model" to nullableString("Model name/number, or null if not visible"),
            "category" to mapOf("type" to "string"),
            "details" to mapOf("type" to "string", "description" to "Visible identifying details: size, color, material, tags"),
            "search_query" to mapOf("type" to "string"),
            "confidence" to mapOf("type" to "string", "enum" to listOf("high", "medium", "low")),
        ))

        private val WEB_SYSTEM = """
            You find resale comparables for a reseller. Use web_search to look for this exact item on
            marketplaces such as eBay, Poshmark, Mercari, Grailed, Depop, Etsy, StockX, GOAT, Reverb,
            Swappa, Back Market, Facebook Marketplace and similar sites. Prefer sold listings; also
            collect active listings.

            Rules that matter more than finding many results:
            - Only report a listing you actually saw in a search result. Copy its URL exactly as it
              appeared in the result. Never construct, shorten or guess a URL.
            - Only report a price that is shown for that specific listing. Copy the price text
              exactly into price_text and quote the text that shows it in evidence. Never estimate,
              average or infer a price. If a result has no visible price, leave it out.
            - Mark status "sold" only when the result explicitly says the item sold; otherwise "active".
            - Skip listings for a different model or variant, lots and bundles, parts-only, and
              accessories or empty boxes.
            - Finding nothing is a valid result: submit an empty list rather than weak matches.

            When you are done searching, call submit_comps exactly once.
        """.trimIndent()

        internal val SUBMIT_COMPS_TOOL: Tool = run {
            val listing = obj(mapOf(
                "title" to mapOf("type" to "string"),
                "url" to mapOf("type" to "string", "description" to "Exact URL from the search result"),
                "price" to mapOf("type" to "number", "description" to "Numeric price shown on the listing"),
                "price_text" to mapOf("type" to "string", "description" to "Price exactly as shown, e.g. \"\$45.00\""),
                "currency" to mapOf("type" to "string", "description" to "ISO code, e.g. USD"),
                "status" to mapOf("type" to "string", "enum" to listOf("sold", "active")),
                "marketplace" to mapOf("type" to "string"),
                "condition" to nullableString("Condition if shown"),
                "sold_date" to nullableString("Sold date if shown"),
                "evidence" to mapOf("type" to "string", "description" to "Verbatim text from the result that shows the price"),
            ))
            Tool.builder()
                .name(SUBMIT_COMPS)
                .description("Report the comparable listings you found. Call once, at the end. An empty list is allowed.")
                .strict(true)
                .inputSchema(Tool.InputSchema.builder()
                    .properties(Tool.InputSchema.Properties.builder()
                        .putAdditionalProperty("listings", JsonValue.from(mapOf("type" to "array", "items" to listing)))
                        .build())
                    .required(listOf("listings"))
                    .putAdditionalProperty("additionalProperties", JsonValue.from(false))
                    .build())
                .build()
        }

        private val MATCH_SYSTEM = """
            You check whether marketplace listings are the same item as the one in a reseller's
            photo, so that only true comparables are used for pricing. A listing matches when it is
            the same product, model and variant (size, capacity, colorway or edition where those
            change the value). Different condition still matches. These do not match: a different
            model or generation, lots or bundles of several items, items sold for parts or not
            working when the photo item looks intact, accessories, replacement parts, boxes or
            manuals only. When the title is too vague to tell, it does not match. Judge from the
            listing text only; do not use the price to decide.
        """.trimIndent()

        private val MATCH_SCHEMA = obj(mapOf(
            "verdicts" to mapOf(
                "type" to "array",
                "items" to obj(mapOf(
                    "index" to mapOf("type" to "integer"),
                    "match" to mapOf("type" to "boolean"),
                    "reason" to mapOf("type" to "string", "description" to "A few words"),
                )),
            ),
        ))
    }
}
