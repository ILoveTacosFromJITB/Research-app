package com.resellcomps.core

/**
 * Builds a free-mode identification from what the phone could read on its own
 * (barcode, label text, a generic object label) plus, optionally, the titles of
 * eBay listings that share the barcode or look like the photo. No guessing:
 * when nothing usable is found, the user is asked to type the item.
 */
object FreeIdentification {

    const val BASIC_WARNING =
        "Basic tag reading only — the app read the text but didn't recognize the item. Add a free AI key " +
            "(Groq or OpenRouter) in Settings so it actually identifies what's in the photo."

    /** A line of text read from the photo; [height] is its on-screen size (bigger usually = brand/name). */
    data class TextLine(val text: String, val height: Int)

    fun build(
        gtin: String?,
        textLines: List<TextLine>,
        objectLabels: List<String>,
        barcodeTitles: List<String>,
        similarTitles: List<String>,
        vision: GoogleVision.Result? = null,
    ): ItemIdentification {
        val fromBarcode = commonWords(barcodeTitles, minShare = 0.5)
        val fromVision = vision?.let(GoogleVision::query).orEmpty()
        val fromText = queryFromText(textLines)
        val fromSimilar = commonWords(similarTitles.take(12), minShare = 0.5)

        val (query, source, confidence) = when {
            fromBarcode.isNotBlank() -> Triple(fromBarcode, "barcode match on eBay", "high")
            fromVision.isNotBlank() -> Triple(
                fromVision, "visual search (same image found on the web)",
                if (vision!!.matches.count { it.isMarketplace } >= 2) "high" else "medium",
            )
            fromText.isNotBlank() && fromSimilar.isNotBlank() -> Triple(mergeQueries(fromText, fromSimilar), "label text + similar eBay listings", "medium")
            fromText.isNotBlank() -> Triple(fromText, "label text only", "low")
            fromSimilar.isNotBlank() -> Triple(fromSimilar, "similar-looking eBay listings", "low")
            else -> Triple("", "nothing", "low")
        }
        val details = buildList {
            gtin?.let { add("Barcode: $it") }
            vision?.bestGuesses?.firstOrNull()?.let { add("Google's best guess: $it") }
            vision?.logos?.takeIf { it.isNotEmpty() }?.let { add("Logo: " + it.joinToString(", ")) }
            vision?.entities?.takeIf { it.isNotEmpty() }?.let { add("Related: " + it.take(5).joinToString(", ")) }
            if (textLines.isNotEmpty()) add("Text on item: " + textLines.sortedByDescending { it.height }.take(6).joinToString(" | ") { it.text })
            if (objectLabels.isNotEmpty()) add("Looks like: " + objectLabels.take(3).joinToString(", "))
            add(if (query.isBlank()) "Couldn't work out the item — please type what it is." else "Search built from $source. Check it before searching.")
        }.joinToString("\n")

        return ItemIdentification(
            name = query.ifBlank { objectLabels.firstOrNull() ?: "Unknown item" },
            brand = null,
            model = null,
            category = objectLabels.firstOrNull() ?: "",
            details = details,
            searchQuery = query,
            confidence = confidence,
            identified = query.isNotBlank(),
            gtin = gtin,
            visualMatches = vision?.matches.orEmpty(),
            warning = if (confidence == "low") BASIC_WARNING else null,
        )
    }

    /** Words that appear in at least [minShare] of the titles, in the order they first appear. */
    fun commonWords(titles: List<String>, minShare: Double, maxWords: Int = 7): String {
        if (titles.size < 2) return titles.firstOrNull()?.let { TitleMatcher.tokens(it).take(maxWords).joinToString(" ") } ?: ""
        val perTitle = titles.map { TitleMatcher.tokens(it).toSet() }
        val needed = (titles.size * minShare).coerceAtLeast(2.0)
        val order = LinkedHashSet<String>()
        titles.forEach { order += TitleMatcher.tokens(it) }
        return order.filter { w -> perTitle.count { w in it } >= needed }
            .filter { it.length > 1 }
            .take(maxWords)
            .joinToString(" ")
    }

    /** Printed-on-the-box words that never help a search. */
    private val NOISE = setOf(
        "digital", "hd", "coupons", "coupon", "bonus", "features", "watch", "anywhere", "made", "china",
        "rated", "widescreen", "includes", "over", "value", "free", "inside", "official", "licensed",
    )

    private val ROMAN = setOf("ii", "iii", "iv", "vi", "vii", "viii", "ix")

    /** Keeps real words (3+ letters) and model numbers; drops OCR fragments like "al" or "i". */
    private fun usefulWords(text: String): List<String> = TitleMatcher.tokens(text).filter { t ->
        t !in NOISE && (TitleMatcher.isModelNumber(t) || t in ROMAN || (t.all(Char::isLetter) && t.length >= 3) || (t.all(Char::isDigit) && t.length >= 2))
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
            .filter { TitleMatcher.isModelNumber(it) && (it.any(Char::isLetter) || it.length >= 4) }
            .take(1).forEach { words += it }
        return words.take(5).joinToString(" ")
    }

    private fun mergeQueries(a: String, b: String): String {
        val words = LinkedHashSet<String>()
        words += a.split(" ")
        b.split(" ").filter { it in words || TitleMatcher.isModelNumber(it) || words.size < 4 }.forEach { words += it }
        return words.take(7).joinToString(" ")
    }
}
