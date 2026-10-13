package com.resellcomps.core

import kotlin.math.ceil

/**
 * The free-mode match check: plain rules instead of AI. A listing counts
 * only if its title contains the search terms (every model number, and most
 * of the other words) and isn't a lot, bundle, parts-only or box-only sale.
 */
object TitleMatcher {

    private val STOPWORDS = setOf(
        "the", "a", "an", "and", "or", "of", "for", "with", "in", "on", "by", "to",
        "new", "used", "nwt", "nwot", "nib", "euc", "vgc", "authentic", "genuine",
    )

    /** Phrases that mean "not a single, complete item". Ignored if the user searched for them. */
    private val EXCLUDE = listOf(
        "lot of", "job lot", "bundle", "for parts", "parts only", "not working", "as is",
        "broken", "box only", "empty box", "case only", "replacement", "read description",
    )

    fun tokens(text: String): List<String> =
        text.lowercase().split(Regex("[^a-z0-9]+")).filter { it.isNotEmpty() && it !in STOPWORDS }

    /** Model-number-like tokens (letters+digits, or 3+ digits) must always appear. */
    fun isModelNumber(t: String): Boolean =
        (t.any(Char::isDigit) && t.any(Char::isLetter)) || (t.length >= 3 && t.all(Char::isDigit))

    fun check(listings: List<Listing>, query: String, exactUrls: Set<String> = emptySet()): List<CheckedListing> {
        val q = tokens(query).distinct()
        val qLower = query.lowercase()
        val required = q.filter(::isModelNumber)
        val others = q.filterNot(::isModelNumber)
        val neededOthers = ceil(others.size * 0.7).toInt()

        return listings.map { l ->
            val titleLower = l.title.lowercase()
            val title = tokens(l.title).toSet()
            val bad = EXCLUDE.firstOrNull { it in titleLower && it !in qLower }
            when {
                bad != null -> CheckedListing(l, false, "Title says \"$bad\"")
                l.url in exactUrls -> CheckedListing(l, true, "Same barcode")
                q.isEmpty() -> CheckedListing(l, false, "No search terms to compare with")
                else -> {
                    val missingModel = required.filterNot { it in title }
                    val hits = others.count { it in title }
                    when {
                        missingModel.isNotEmpty() -> CheckedListing(l, false, "Missing \"${missingModel.joinToString(" ")}\"")
                        hits < neededOthers -> CheckedListing(l, false, "Title doesn't match enough search words ($hits of ${others.size})")
                        else -> CheckedListing(l, true, "Title matches search")
                    }
                }
            }
        }
    }
}
