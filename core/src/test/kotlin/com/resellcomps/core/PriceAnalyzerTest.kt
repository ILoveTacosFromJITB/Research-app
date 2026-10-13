package com.resellcomps.core

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNull

class PriceAnalyzerTest {
    private fun l(price: Double, status: ListingStatus) = Listing(
        title = "x", price = price, priceText = "$$price", currency = "USD",
        url = "https://ebay.com/itm/$price", status = status,
        source = ListingSource.EBAY, marketplace = "eBay",
    )

    @Test fun noCompsGivesNoPrice() {
        assertEquals(Valuation.NoComps, PriceAnalyzer.valuate(emptyList()))
    }

    @Test fun onlyActiveIsNotAValue() {
        val v = PriceAnalyzer.valuate(listOf(l(40.0, ListingStatus.ACTIVE), l(60.0, ListingStatus.ACTIVE)))
        assertIs<Valuation.OnlyActive>(v)
        assertEquals(50.0, v.active.median)
    }

    @Test fun soldDrivesValueAndActiveIsSeparate() {
        val v = PriceAnalyzer.valuate(listOf(
            l(10.0, ListingStatus.SOLD), l(30.0, ListingStatus.SOLD), l(20.0, ListingStatus.SOLD),
            l(999.0, ListingStatus.ACTIVE),
        ))
        assertIs<Valuation.FromSold>(v)
        assertEquals(3, v.sold.count)
        assertEquals(20.0, v.sold.median)
        assertEquals(10.0, v.sold.low)
        assertEquals(30.0, v.sold.high)
        assertNull(v.sold.typicalLow) // fewer than 4 comps
        assertEquals(999.0, v.active?.median)
    }

    @Test fun quartilesWithFourOrMore() {
        val s = PriceAnalyzer.stats(listOf(10.0, 20.0, 30.0, 40.0, 50.0).map { l(it, ListingStatus.SOLD) })!!
        assertEquals(20.0, s.typicalLow)
        assertEquals(40.0, s.typicalHigh)
        assertEquals(30.0, s.median)
    }

    @Test fun zeroPricesIgnored() {
        assertNull(PriceAnalyzer.stats(listOf(l(0.0, ListingStatus.SOLD))))
    }
}
