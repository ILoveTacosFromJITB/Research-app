package com.resellcomps.core

/** What the photo shows, as identified by Claude. Nothing here is a price. */
data class ItemIdentification(
    val name: String,
    val brand: String?,
    val model: String?,
    val category: String,
    val details: String,
    val searchQuery: String,
    val confidence: String,
    val identified: Boolean,
    /** UPC/EAN read from a barcode in the photo, if any. */
    val gtin: String? = null,
)

enum class ListingStatus { SOLD, ACTIVE }

enum class ListingSource(val label: String) {
    /** Structured eBay data from SerpApi: title, price and link come straight from eBay. */
    EBAY("eBay"),

    /** eBay's official Browse API (active listings only): data straight from eBay. */
    EBAY_API("eBay"),

    /** Read on the phone from eBay's own sold-search page. */
    EBAY_PAGE("eBay"),

    /** Found by Claude's web search; the URL was confirmed to be a real search result. */
    WEB("Web"),
}

data class Listing(
    val title: String,
    val price: Double,
    val priceText: String,
    val currency: String,
    val url: String,
    val status: ListingStatus,
    val source: ListingSource,
    val marketplace: String,
    val condition: String? = null,
    val shipping: String? = null,
    val thumbnailUrl: String? = null,
    val soldDate: String? = null,
    /** For web comps: the exact text on the page Claude says shows the price. */
    val evidence: String? = null,
)

/** A listing plus whether Claude judged it to be the same item as the photo. */
data class CheckedListing(
    val listing: Listing,
    val isMatch: Boolean,
    val reason: String,
)

/** Statistics over a set of listings. Every number traces back to [listings]. */
data class PriceStats(
    val count: Int,
    val low: Double,
    val high: Double,
    val median: Double,
    /** 25th–75th percentile; only reported with 4+ comps. */
    val typicalLow: Double?,
    val typicalHigh: Double?,
    val listings: List<Listing>,
)

sealed interface Valuation {
    /** Value comes from real sold listings. */
    data class FromSold(val sold: PriceStats, val active: PriceStats?) : Valuation

    /** Nothing sold was found. Asking prices are shown, but they are not a value. */
    data class OnlyActive(val active: PriceStats) : Valuation

    /** No matching listings at all: no price is given. */
    data object NoComps : Valuation
}

data class CompsReport(
    val item: ItemIdentification,
    val query: String,
    val valuation: Valuation,
    val checked: List<CheckedListing>,
    val notes: List<String>,
    val retrievedAtEpochMs: Long,
    /** False when sold listings weren't searched at all (free mode with the sold-page reader off). */
    val soldSearched: Boolean = true,
)
