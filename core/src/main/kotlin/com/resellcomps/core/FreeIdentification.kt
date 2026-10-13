package com.resellcomps.core

/**
 * Builds a free-mode identification from what the phone could read on its own
 * (barcode, label text, a generic object label) plus, optionally, the titles of
 * eBay listings that share the barcode or look like the photo. No guessing:
 * when nothing usable is found, the user is asked to type the item.
 */
object FreeIdentification {

    /** A line of text read from the photo; [height] is its on-screen size (bigger usually = brand/name). */
    data class TextLine(val text: String, val height: Int)

    fun build(
        gtin: String?,
        textLines: List<TextLine>,
        objectLabels: List<String>,
        barcodeTitles: List<String>,
        similarTitles: List<String>,
    ): ItemIdentification {
        val fromBarcode = commonWords(barcodeTitles, minShare = 0.5)
        val fromText = queryFromText(textLines)
        val fromSimilar = commonWords(similarTitles.take(12), minShare = 0.5)

        val (query, source, confidence) = when {
            fromBarcode.isNotBlank() -> Triple(fromBarcode, "barcode match on eBay", "high")
            fromText.isNotBlank() && fromSimilar.isNotBlank() -> Triple(mergeQueries(fromText, fromSimilar), "label text + similar eBay listings", "medium")
            fromText.isNotBlank() -> Triple(fromText, "label text", "low")
            fromSimilar.isNotBlank() -> Triple(fromSimilar, "similar-looking eBay listings", "low")
            else -> Triple("", "nothing", "low")
        }
        val details = buildList {
            gtin?.let { add("Barcode: $it") }
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

    /** Largest text first (usually brand / product name), plus any model numbers, capped at ~6 words. */
    fun queryFromText(lines: List<TextLine>): String {
        val usable = lines.map { it.copy(text = it.text.trim()) }
            .filter { l -> l.text.length >= 2 && l.text.count(Char::isLetterOrDigit) >= 2 }
        if (usable.isEmpty()) return ""
        val words = LinkedHashSet<String>()
        usable.sortedByDescending { it.height }.take(2).forEach { words += TitleMatcher.tokens(it.text).take(4) }
        usable.flatMap { TitleMatcher.tokens(it.text) }.filter(TitleMatcher::isModelNumber).take(1).forEach { words += it }
        return words.take(6).joinToString(" ")
    }

    private fun mergeQueries(a: String, b: String): String {
        val words = LinkedHashSet<String>()
        words += a.split(" ")
        b.split(" ").filter { it in words || TitleMatcher.isModelNumber(it) || words.size < 4 }.forEach { words += it }
        return words.take(7).joinToString(" ")
    }
}
