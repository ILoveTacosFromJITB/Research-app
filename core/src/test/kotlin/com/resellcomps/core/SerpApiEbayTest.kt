package com.resellcomps.core

import java.io.IOException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class SerpApiEbayTest {
    private val sample = """
        {
          "search_metadata": {"status": "Success"},
          "organic_results": [
            {"title": "Nike Air Max 90 Infrared Size 10", "link": "https://www.ebay.com/itm/111",
             "condition": "Pre-Owned", "price": {"raw": "$85.00", "extracted": 85.0},
             "shipping": "+$12.00 shipping", "thumbnail": "https://i.ebayimg.com/a.jpg",
             "extensions": ["Sold  Sep 30, 2026"]},
            {"title": "Nike Air Max 90 various sizes", "link": "https://www.ebay.com/itm/222",
             "price": {"from": {"raw": "$60.00", "extracted": 60.0}, "to": {"raw": "$90.00", "extracted": 90.0}}},
            {"title": "Shop on eBay", "link": "https://www.ebay.com/itm/123456", "price": {"raw": "$20.00", "extracted": 20.0}},
            {"title": "No price here", "link": "https://www.ebay.com/itm/333"},
            {"title": "Nike Air Max 90 Infrared 10.5", "link": "https://www.ebay.com/itm/444",
             "price": {"raw": "$95.50", "extracted": 95.5}, "shipping": {"extracted": 0.0}}
          ]
        }
    """.trimIndent()

    @Test fun parsesOnlyListingsWithASinglePriceAndLink() {
        val out = SerpApiEbay.parse(sample, ListingStatus.SOLD)
        assertEquals(listOf("https://www.ebay.com/itm/111", "https://www.ebay.com/itm/444"), out.map { it.url })
        val first = out[0]
        assertEquals(85.0, first.price)
        assertEquals("$85.00", first.priceText)
        assertEquals("Pre-Owned", first.condition)
        assertEquals("+$12.00 shipping", first.shipping)
        assertEquals("Sold  Sep 30, 2026", first.soldDate)
        assertTrue(out.all { it.status == ListingStatus.SOLD && it.source == ListingSource.EBAY })
    }

    @Test fun noResultsIsEmptyNotAnError() {
        val body = """{"error": "eBay hasn't returned any results for this query."}"""
        assertEquals(emptyList(), SerpApiEbay.parse(body, ListingStatus.ACTIVE))
    }

    @Test fun realErrorsThrow() {
        assertFailsWith<IOException> { SerpApiEbay.parse("""{"error": "Invalid API key."}""", ListingStatus.SOLD) }
    }
}
