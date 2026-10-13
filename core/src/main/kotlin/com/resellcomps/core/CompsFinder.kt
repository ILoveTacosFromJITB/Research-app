package com.resellcomps.core

import java.util.concurrent.Callable
import java.util.concurrent.Executors

/**
 * Runs the whole lookup: eBay sold + active (if a SerpApi key is set) and a
 * web search in parallel, then a match check, then the price math.
 */
class CompsFinder(
    private val claude: ClaudeService,
    private val ebay: SerpApiEbay?,
    private val clock: () -> Long = System::currentTimeMillis,
) {
    fun identify(photos: List<ByteArray>, hint: String? = null): ItemIdentification = claude.identify(photos, hint)

    fun findComps(
        photos: List<ByteArray>,
        item: ItemIdentification,
        query: String,
        onProgress: (String) -> Unit = {},
    ): CompsReport {
        val notes = mutableListOf<String>()
        val pool = Executors.newFixedThreadPool(3)
        try {
            onProgress("Searching eBay sold & active listings and the web…")
            val sold = ebay?.let { e -> pool.submit(Callable { e.search(query, ListingStatus.SOLD) }) }
            val active = ebay?.let { e -> pool.submit(Callable { e.search(query, ListingStatus.ACTIVE) }) }
            val web = pool.submit(Callable { claude.findWebComps(photos, item, query) })

            val all = mutableListOf<Listing>()
            if (ebay == null) notes += "eBay search skipped: add a SerpApi key in Settings for eBay sold data."
            sold?.let { f -> runCatching { f.get() }.onSuccess { all += it }.onFailure { notes += "eBay sold search failed: ${rootMessage(it)}" } }
            active?.let { f -> runCatching { f.get() }.onSuccess { all += it }.onFailure { notes += "eBay active search failed: ${rootMessage(it)}" } }
            runCatching { web.get() }
                .onSuccess { r ->
                    all += r.listings
                    r.note?.let { notes += it }
                    if (r.rejectedUnverified > 0) {
                        notes += "${r.rejectedUnverified} web result(s) dropped because their link could not be confirmed as a real search result."
                    }
                }
                .onFailure { notes += "Web search failed: ${rootMessage(it)}" }

            val unique = dedupe(all)
            onProgress("Checking ${unique.size} listings against your photo…")
            val checked = if (unique.isEmpty()) emptyList() else claude.checkMatches(photos, item, unique)
            val matched = checked.filter { it.isMatch }.map { it.listing }
            onProgress("Done")
            return CompsReport(
                item = item,
                query = query,
                valuation = PriceAnalyzer.valuate(matched),
                checked = checked,
                notes = notes,
                retrievedAtEpochMs = clock(),
            )
        } finally {
            pool.shutdownNow()
        }
    }

    companion object {
        fun dedupe(listings: List<Listing>): List<Listing> =
            listings.distinctBy { UrlVerifier.normalize(it.url) ?: it.url }

        private fun rootMessage(t: Throwable): String {
            var c = t
            while (c.cause != null && c.cause !== c) c = c.cause!!
            return c.message ?: c.javaClass.simpleName
        }
    }
}
