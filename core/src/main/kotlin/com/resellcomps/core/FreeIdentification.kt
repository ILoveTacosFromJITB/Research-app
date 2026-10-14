package com.resellcomps.core

/**
 * The fallback when no AI key is set: builds a search from what the phone could
 * read on its own (barcode, label text, a generic object label). No guessing:
 * when nothing usable is found, the user is asked to type the item.
 */
object FreeIdentification {

    const val BASIC_WARNING =
        "Basic tag reading only — the app read the text but didn't recognize the item. Add a free AI key " +
            "(Groq or OpenRouter) in Settings so it actually identifies what's in the photo."

    /** A line of text read from the photo; [height] is its on-screen size (bigger usually = brand/name). */
    data class TextLine(val text: String, val height: Int)

    fun build(gtin: String?, textLines: List<TextLine>, objectLabels: List<String>): ItemIdentification {
        val query = queryFromText(textLines)
        val details = buildList {
            gtin?.let { add("Barcode: $it") }
            if (textLines.isNotEmpty()) add("Text on item: " + textLines.sortedByDescending { it.height }.take(6).joinToString(" | ") { it.text })
            if (objectLabels.isNotEmpty()) add("Looks like: " + objectLabels.take(3).joinToString(", "))
            add(if (query.isBlank()) "Couldn't work out the item — please type what it is." else "Search built from label text only. Check it before searching.")
        }.joinToString("\n")

        return ItemIdentification(
            name = query.ifBlank { objectLabels.firstOrNull() ?: "Unknown item" },
            brand = null,
            model = null,
            category = objectLabels.firstOrNull() ?: "",
            details = details,
            searchQuery = query,
            confidence = "low",
            identified = query.isNotBlank(),
            gtin = gtin,
            warning = BASIC_WARNING,
        )
    }

    private val STOPWORDS = setOf("the", "a", "an", "and", "or", "of", "for", "with", "in", "on", "by", "to")

    fun tokens(text: String): List<String> =
        text.lowercase().split(Regex("[^a-z0-9]+")).filter { it.isNotEmpty() && it !in STOPWORDS }

    /** Model-number-like tokens: letters+digits, or 3+ digits. */
    fun isModelNumber(t: String): Boolean =
        (t.any(Char::isDigit) && t.any(Char::isLetter)) || (t.length >= 3 && t.all(Char::isDigit))

    /** Printed-on-the-box words that never help a search. */
    private val NOISE = setOf(
        "digital", "hd", "coupons", "coupon", "bonus", "features", "watch", "anywhere", "made", "china",
        "rated", "widescreen", "includes", "over", "value", "free", "inside", "official", "licensed",
    )

    private val ROMAN = setOf("ii", "iii", "iv", "vi", "vii", "viii", "ix")

    /** Keeps real words (3+ letters) and model numbers; drops OCR fragments like "al" or "i". */
    private fun usefulWords(text: String): List<String> = tokens(text).filter { t ->
        t !in NOISE && (isModelNumber(t) || t in ROMAN || (t.all(Char::isLetter) && t.length >= 3) || (t.all(Char::isDigit) && t.length >= 2))
    }

    /**
     * The biggest readable text (usually the brand or title), plus any model number.
     * Lines whose words are all OCR junk are skipped rather than put in the search.
     */
    fun queryFromText(lines: List<TextLine>): String {
        val usable = lines.mapNotNull { l -> usefulWords(l.text).takeIf { it.isNotEmpty() }?.let { l to it } }
        if (usable.isEmpty()) return ""
        val tallest = usable.maxOf { it.first.height }
        val words = LinkedHashSet<String>()
        usable.filter { it.first.height >= tallest * 0.4 }
            .sortedByDescending { it.first.height }
            .take(2)
            .forEach { words += it.second.take(4) }
        // A plain 3-digit number on packaging is usually a price or count, not a model.
        usable.flatMap { it.second }
            .filter { isModelNumber(it) && (it.any(Char::isLetter) || it.length >= 4) }
            .take(1).forEach { words += it }
        return words.take(5).joinToString(" ")
    }
}
