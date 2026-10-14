package com.resellcomps.core

import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull

/** The identify instructions and answer format, shared by the AI that names items. */
object IdentifyPrompt {
    /** Most photos of one item the app keeps (front, back, tag, label, barcode…). */
    const val MAX_PHOTOS = 6

    val SYSTEM = """
        You identify secondhand items from photos for a reseller who will look up comparable
        sold listings. Be as specific as the photos allow: brand, product line, model name or
        number, size, color, material, edition or year. Recognize what the item actually is
        from its design, artwork, logos and text, using your knowledge of products, brands and
        titles: stylized or partly hidden lettering should still be read as the real name. Read
        any visible labels, tags or model numbers. Do not invent a brand or model you have no
        evidence for; leave it null and lower your confidence instead. Never estimate a price.

        Always decide whether the item is vintage (roughly 20+ years old) and answer "yes", "no"
        or "unsure" in vintage. Look for evidence a reseller would use:
        - Clothing tags: tag brand and style (e.g. Hanes Beefy / 50-50, Screen Stars, Fruit of
          the Loom, Signal, Velva Sheen, Champion runner or blue bar, Nike orange or white swoosh,
          Levi's big E or orange tab, Russell Athletic gold tag), "Made in USA", an RN or WPL
          number, a union label (ILGWU, ACWA), missing care tag, old fiber blends (50/50
          poly-cotton), Talon or Scovill zippers.
        - Construction: single-stitch sleeve and hem on tees (mostly before about 1997),
          chain-stitched hems, selvedge denim, fading and cracking typical of age.
        - Prints and items: copyright dates on graphics, tour dates, old logos, discontinued
          models, packaging and fonts of an era, date codes on electronics and toys.
        Put the decade or year range in era (e.g. "1990s", "late 1980s", "2000s") and list the
        clues you actually saw in vintage_clues. If the clues aren't visible (for example no tag
        photo), answer "unsure" and say which photo would settle it; never call something
        vintage just because it looks old. In details, also name anything that makes it more
        desirable (rare colorway, early tag, limited edition).

        search_query is exactly what an experienced reseller would type into eBay's search box:
        brand first, then the product line, tag name or model number, then at most one or two
        key descriptors. For movies, games, music and books use the title plus the format. Keep
        it short, usually 2-5 words, all lowercase. Copy model numbers exactly as printed. Start
        it with "vintage" when vintage is "yes". Leave out size, color and condition unless they
        change what the item is. Examples: "vintage hanes beefy", "bose 700", "nike air max 90",
        "patagonia better sweater", "vintage levis 501 big e", "sony wh-1000xm4",
        "alvin and the chipmunks road chip blu-ray". If the photos do not show an identifiable
        item, set identified to false and explain in details.
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
        vintage = o.str("vintage")?.lowercase()?.takeIf { it in setOf("yes", "no", "unsure") },
        era = o.str("era"),
        vintageClues = o.str("vintage_clues")
            ?: (o["vintage_clues"] as? kotlinx.serialization.json.JsonArray)
                ?.mapNotNull { (it as? JsonPrimitive)?.contentOrNull }?.joinToString(", ")?.takeIf { it.isNotBlank() },
    )

    private fun JsonObject.str(key: String): String? =
        (this[key] as? JsonPrimitive)?.takeIf { it.isString }?.contentOrNull?.takeIf { it.isNotBlank() && it != "null" }
}
