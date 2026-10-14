package com.resellcomps.core

import java.util.concurrent.Callable
import java.util.concurrent.Executors

/**
 * Free-mode lookup: eBay's free Browse API for active listings (by keywords,
 * barcode and photo). Sold prices are left to eBay's own pages (eBay Sold,
 * Terapeak). A plain title check stands in for the AI match check.
 */
class FreeCompsFinder(
    private val ebay: EbayBrowseApi?,
    private val clock: () -> Long = System::currentTimeMillis,
) {
    fun findComps(photos: List<ByteArray>, item: ItemIdentification, query: String, onProgress: (String) -> Unit = {}): CompsReport {
        val notes = mutableListOf<String>()
        val all = mutableListOf<Listing>()
        val exactUrls = mutableSetOf<String>()
        val pool = Executors.newFixedThreadPool(3)
        try {
            onProgress("Searching eBay…")
            val byText = ebay?.let { e -> pool.submit(Callable { e.search(query) }) }
            val byGtin = ebay?.takeIf { item.gtin != null }?.let { e -> pool.submit(Callable { e.search(null, item.gtin) }) }
            // eBay photo search on the first two photos (usually the clearest views of the whole item).
            val byImage = ebay?.let { e -> photos.take(2).map { p -> pool.submit(Callable { e.searchByImage(p) }) } }.orEmpty()

            if (ebay == null) notes += "No online listing search was done: add your free eBay developer keys in Settings."
            byGtin?.let { f -> runCatching { f.get() }.onSuccess { r -> all += r; exactUrls += r.map { it.url } }.onFailure { notes += "eBay barcode search failed: ${rootMessage(it)}" } }
            byText?.let { f -> runCatching { f.get() }.onSuccess { all += it }.onFailure { notes += "eBay search failed: ${rootMessage(it)}" } }
            byImage.forEach { f -> runCatching { f.get() }.onSuccess { all += it }.onFailure { notes += "eBay photo search failed: ${rootMessage(it)}" } }

            // eBay's free API has no sold data, and reading eBay's pages would break its terms;
            // sold prices are one tap away on eBay / Terapeak instead.
            notes += "Sold prices come from eBay itself: tap eBay Sold or Terapeak to see real sold listings."

            val checked = TitleMatcher.check(CompsFinder.dedupe(all), query, exactUrls)
            return CompsReport(
                item = item,
                query = query,
                valuation = PriceAnalyzer.valuate(checked.filter { it.isMatch }.map { it.listing }),
                checked = checked,
                notes = notes,
                retrievedAtEpochMs = clock(),
                soldSearched = false,
            )
        } finally {
            pool.shutdownNow()
        }
    }

    private fun rootMessage(t: Throwable): String =
        generateSequence(t) { it.cause }.last().message ?: t.javaClass.simpleName
}
