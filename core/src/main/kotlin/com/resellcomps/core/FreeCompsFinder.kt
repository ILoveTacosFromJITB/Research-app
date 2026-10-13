package com.resellcomps.core

import java.util.concurrent.Callable
import java.util.concurrent.Executors

/**
 * Free-mode lookup: eBay's free Browse API for active listings (by keywords,
 * barcode and photo) and, if enabled, sold prices read from eBay's sold-search
 * page on the phone. A plain title check stands in for the AI match check.
 */
class FreeCompsFinder(
    private val ebay: EbayBrowseApi?,
    /** Loads eBay's sold page for a query and returns its sold listings; null when turned off. */
    private val soldPageReader: ((String) -> List<Listing>)?,
    private val clock: () -> Long = System::currentTimeMillis,
) {
    fun findComps(jpeg: ByteArray, item: ItemIdentification, query: String, onProgress: (String) -> Unit = {}): CompsReport {
        val notes = mutableListOf<String>()
        val all = mutableListOf<Listing>()
        val exactUrls = mutableSetOf<String>()
        val pool = Executors.newFixedThreadPool(3)
        try {
            onProgress("Searching eBay…")
            val byText = ebay?.let { e -> pool.submit(Callable { e.search(query) }) }
            val byGtin = ebay?.takeIf { item.gtin != null }?.let { e -> pool.submit(Callable { e.search(null, item.gtin) }) }
            val byImage = ebay?.let { e -> pool.submit(Callable { e.searchByImage(jpeg) }) }

            if (ebay == null) notes += "Active listings skipped: add your free eBay developer keys in Settings."
            byGtin?.let { f -> runCatching { f.get() }.onSuccess { r -> all += r; exactUrls += r.map { it.url } }.onFailure { notes += "eBay barcode search failed: ${rootMessage(it)}" } }
            byText?.let { f -> runCatching { f.get() }.onSuccess { all += it }.onFailure { notes += "eBay search failed: ${rootMessage(it)}" } }
            byImage?.let { f -> runCatching { f.get() }.onSuccess { all += it }.onFailure { notes += "eBay photo search failed: ${rootMessage(it)}" } }

            val reader = soldPageReader
            if (reader != null) {
                onProgress("Reading eBay sold listings…")
                runCatching { reader(query) }
                    .onSuccess { all += it }
                    .onFailure { notes += "Couldn't read eBay's sold page (${rootMessage(it)}). This is not the same as zero sales — use the sold-search buttons to check." }
            } else {
                notes += "Sold prices aren't fetched in free mode. Use the sold-search buttons to see real sold listings, or turn on automatic sold prices in Settings."
            }

            val checked = TitleMatcher.check(CompsFinder.dedupe(all), query, exactUrls)
            return CompsReport(
                item = item,
                query = query,
                valuation = PriceAnalyzer.valuate(checked.filter { it.isMatch }.map { it.listing }),
                checked = checked,
                notes = notes,
                retrievedAtEpochMs = clock(),
                soldSearched = reader != null,
            )
        } finally {
            pool.shutdownNow()
        }
    }

    private fun rootMessage(t: Throwable): String =
        generateSequence(t) { it.cause }.last().message ?: t.javaClass.simpleName
}
