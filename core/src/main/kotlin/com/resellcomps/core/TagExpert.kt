package com.resellcomps.core

import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull

/**
 * A second, slower look focused only on dating the tags: the AI transcribes the
 * tags, lists evidence for an older and a newer date side by side, and only then
 * decides. Used by "Dig deeper on the tag" when a quick answer looks wrong.
 */
object TagExpert {

    /** Rules of thumb resellers use to date tags. They are tendencies, not proof. */
    val DATING_GUIDE = """
        Tag-dating rules of thumb (tendencies, not proof; weigh them together):
        - Care instructions: US labels mostly used written instructions until the late 1990s; the
          FTC allowed care symbols from 1997, so a full row of laundry symbols leans late-1990s or
          later. Written-only care text leans older. No care tag at all leans pre-1970s.
        - Country of origin: "Made in USA" on basics is very common through the mid-1990s and much
          rarer after about 2000. Mexico, Honduras, El Salvador, Dominican Republic grew after 1994
          (NAFTA); China, Bangladesh, Vietnam, Cambodia dominate most brands from the 2000s.
        - Multi-language care tags (English/French/Spanish) are common from the mid-1990s on.
        - Printed "tagless" neck labels are mostly early-2000s or later.
        - Single-stitch sleeves and hems on tees are mostly before about 1995-1997; double-stitch
          after. Reproductions exist, so pair it with the tag.
        - Union labels (ILGWU, ACWA) lean before 1995, when ILGWU merged into UNITE.
        - WPL numbers mean before 1959. RN numbers identify the company, not the year.
        - Copyright dates on a print give the earliest possible year, not the exact year.
        - Fiber: 50/50 cotton-poly tees are typical of the late 1970s-1990s.
        - Recognize brand tag versions when you can (e.g. Hanes Beefy-T, Screen Stars, Champion
          blue bar/runner, Nike orange swoosh or white tag, Levi's big E, Russell gold tag, Starter,
          Majestic, Delta, Gildan) and say which version you think it is and its usual years.
        "Vintage" to resellers usually means 20+ years old (today: roughly 2005 or earlier); call
        2000s items "Y2K" or "2000s" rather than 1990s unless the tag evidence points older.
    """.trimIndent()

    val SYSTEM = """
        You are an expert at dating clothing and product tags for resellers. Work carefully:
        first transcribe everything printed on every tag you can see (brand and logo style, font,
        colors, size, fabric, country, RN/WPL, union label, care text or symbols, languages,
        stitching), then list the evidence that points older and the evidence that points newer,
        then decide. Don't round to a popular decade: if the evidence says 2000s, say 2000s; if
        it's split, say so and give the range. If a needed tag isn't visible, answer "unsure"
        and say exactly which photo would settle it. Never estimate a price.
    """.trimIndent() + "\n\n" + DATING_GUIDE + "\n\n" + """
        Reply with only a JSON object, no other text, with keys in this order:
        "tag_reading" (string: everything printed on the tags),
        "evidence_older" (array of strings), "evidence_newer" (array of strings),
        "reasoning" (string: weigh the two lists in a few sentences),
        "vintage" ("yes", "no" or "unsure"), "era" (e.g. "1990s", "late 1990s to early 2000s"),
        "vintage_clues" (string: the deciding clues).
    """.trimIndent()

    fun userText(item: ItemIdentification): String = buildString {
        append("These photos show one item: ").append(item.name.ifBlank { "an item" }).append(". ")
        if (item.vintage != null) {
            append("A quick first look said vintage = ").append(item.vintage)
            item.era?.let { append(", era ").append(it) }
            item.vintageClues?.let { append(" (based on: ").append(it).append(")") }
            append(". Another tool said it may be different, so re-check from the tags themselves; ")
            append("don't just repeat the first answer. ")
        }
        append("Date this item from its tags.")
    }

    /** Applies the expert's answer: new vintage call, and "vintage" added to or removed from the search. */
    fun apply(item: ItemIdentification, o: JsonObject, currentQuery: String): ItemIdentification {
        fun str(k: String) = (o[k] as? JsonPrimitive)?.contentOrNull?.takeIf { it.isNotBlank() && it != "null" }
        fun list(k: String) = (o[k] as? JsonArray).orEmpty().mapNotNull { (it as? JsonPrimitive)?.contentOrNull }.filter { it.isNotBlank() }

        val vintage = str("vintage")?.lowercase()?.takeIf { it in setOf("yes", "no", "unsure") } ?: item.vintage
        val older = list("evidence_older")
        val newer = list("evidence_newer")
        val reasoning = buildString {
            if (older.isNotEmpty()) append("Points older: ").append(older.joinToString("; ")).append("\n")
            if (newer.isNotEmpty()) append("Points newer: ").append(newer.joinToString("; ")).append("\n")
            str("reasoning")?.let { append(it) }
        }.trim().takeIf { it.isNotEmpty() }

        val q = currentQuery.trim()
        val query = when {
            vintage == "yes" && !q.startsWith("vintage ") && q.isNotEmpty() -> "vintage $q"
            vintage == "no" && q.startsWith("vintage ") -> q.removePrefix("vintage ").trim()
            else -> q
        }
        return item.copy(
            vintage = vintage,
            era = str("era") ?: item.era,
            vintageClues = str("vintage_clues") ?: item.vintageClues,
            tagReading = str("tag_reading") ?: item.tagReading,
            vintageReasoning = reasoning,
            searchQuery = query,
        )
    }
}
