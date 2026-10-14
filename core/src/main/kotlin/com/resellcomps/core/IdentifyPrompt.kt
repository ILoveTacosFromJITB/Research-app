package com.resellcomps.core

import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull

/** The identify instructions and answer format, shared by every AI that names items (Claude, Gemini). */
object IdentifyPrompt {
    val SYSTEM = """
        You identify secondhand items from photos for a reseller who will look up comparable
        sold listings. Be as specific as the photo allows: brand, product line, model name or
        number, size, color, material, edition or year. Recognize what the item actually is
        from its design, artwork, logos and text, using your knowledge of products, brands and
        titles: stylized or partly hidden lettering should still be read as the real name. Read
        any visible labels, tags or model numbers. Do not invent a brand or model you have no
        evidence for; leave it null and lower your confidence instead. Never estimate a price.
        search_query is exactly what an experienced reseller would type into eBay's search box:
        brand first, then the product line, tag name or model number, then at most one or two
        key descriptors. For movies, games, music and books use the title plus the format. Keep it short, usually 2-5 words, all
        lowercase. Copy model numbers exactly as printed. Add "vintage" only when the tag or
        construction clearly looks vintage (for example single-stitch tees or old-style tags).
        Leave out size, color and condition unless they change what the item is. Examples:
        "vintage hanes beefy", "bose 700", "nike air max 90", "patagonia better sweater",
        "levis 501 big e", "sony wh-1000xm4", "alvin and the chipmunks road chip blu-ray". If the photo does not show an identifiable item,
        set identified to false and explain in details.
    """.trimIndent()

    fun userText(photoCount: Int, hint: String?): String = buildString {
        if (photoCount > 1) append("These $photoCount photos all show the same item (e.g. front, back, tag, label). ")
        append("Identify the item so a reseller can look up comparable listings.")
        if (!hint.isNullOrBlank()) append("\n\nThe seller adds: ").append(hint)
    }

    fun fromJson(o: JsonObject): ItemIdentification = ItemIdentification(
        // Some free models drop this field; a usable search means it was identified.
        identified = (o["identified"] as? JsonPrimitive)?.booleanOrNull ?: !o.str("search_query").isNullOrBlank(),
        name = o.str("name") ?: "Unknown item",
        brand = o.str("brand"),
        model = o.str("model"),
        category = o.str("category") ?: "",
        details = o.str("details") ?: "",
        searchQuery = o.str("search_query")?.trim()?.lowercase() ?: "",
        confidence = o.str("confidence") ?: "low",
    )

    private fun JsonObject.str(key: String): String? =
        (this[key] as? JsonPrimitive)?.takeIf { it.isString }?.contentOrNull?.takeIf { it.isNotBlank() && it != "null" }
}
