package com.resellcomps.core

import java.net.URLEncoder

/** eBay's own sold-search page for a query (opened in the browser or eBay app). */
object EbaySoldPage {
    fun searchUrl(query: String): String =
        "https://www.ebay.com/sch/i.html?_nkw=${URLEncoder.encode(query, "UTF-8")}&LH_Sold=1&LH_Complete=1"
}

/** One-tap sold searches on other sites, so the user can see the proof there. */
object SoldSearchLinks {
    /** eBay's normal search: what's listed right now. */
    fun ebayActive(query: String): String =
        "https://www.ebay.com/sch/i.html?_nkw=${URLEncoder.encode(query, "UTF-8")}"

    fun ebaySold(query: String): String = EbaySoldPage.searchUrl(query)

    /** Terapeak (eBay Seller Hub → Research): eBay's official sold-price research, free for sellers. */
    fun terapeak(query: String): String =
        "https://www.ebay.com/sh/research?marketplace=EBAY-US&keywords=${URLEncoder.encode(query, "UTF-8")}" +
            "&dayRange=90&tabName=SOLD"

    fun forQuery(query: String): List<Pair<String, String>> {
        val q = URLEncoder.encode(query, "UTF-8")
        return listOf(
            "eBay sold" to EbaySoldPage.searchUrl(query),
            "Terapeak" to terapeak(query),
            "Poshmark sold" to "https://poshmark.com/search?query=$q&availability=sold_out",
            "Mercari sold" to "https://www.mercari.com/search/?keyword=$q&itemStatuses=2",
        )
    }
}
