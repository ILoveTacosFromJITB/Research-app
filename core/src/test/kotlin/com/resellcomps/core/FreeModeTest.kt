package com.resellcomps.core

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue

class FreeModeTest {
    private fun l(title: String, url: String = "https://ebay.com/itm/" + title.hashCode()) =
        Listing(title, 10.0, "$10.00", "USD", url, ListingStatus.ACTIVE, ListingSource.EBAY_API, "eBay")

    // ------------------------------------------------------------ TitleMatcher

    @Test fun modelNumbersAreRequired() {
        val r = TitleMatcher.check(listOf(
            l("Nintendo Switch OLED HEG-001 White"),
            l("Nintendo Switch HAC-001 Neon"),
        ), "nintendo switch heg-001")
        assertTrue(r[0].isMatch)
        assertFalse(r[1].isMatch)
    }

    @Test fun lotsAndPartsAreExcludedUnlessSearched() {
        val r = TitleMatcher.check(listOf(l("Levis 501 jeans lot of 3"), l("Levis 501 jeans 32x30")), "levis 501 jeans")
        assertFalse(r[0].isMatch)
        assertTrue(r[0].reason.contains("lot of"))
        assertTrue(r[1].isMatch)
        assertTrue(TitleMatcher.check(listOf(l("Levis 501 jeans lot of 3")), "levis 501 jeans lot of 3")[0].isMatch)
    }

    @Test fun mostWordsMustMatch() {
        val r = TitleMatcher.check(listOf(l("Patagonia fleece jacket")), "patagonia better sweater jacket")
        assertFalse(r[0].isMatch) // 2 of 4 words
    }

    @Test fun barcodeMatchesCountEvenWithDifferentWording() {
        val x = l("Some totally different wording", "https://ebay.com/itm/9")
        assertTrue(TitleMatcher.check(listOf(x), "sony wh-1000xm4", setOf(x.url))[0].isMatch)
    }

    // ------------------------------------------------------- eBay sold page

    @Test fun parsesSoldRowsAndSkipsJunk() {
        val js = """{"blocked":false,"rows":[
          {"title":"New ListingSony WH-1000XM4 Black","url":"https://www.ebay.com/itm/Sony-WH/256123456789?hash=x","price":"$129.99","sold":"Sold Sep 30, 2026","condition":"Pre-Owned","img":"https://i.ebayimg.com/1.jpg"},
          {"title":"Shop on eBay","url":"https://www.ebay.com/itm/123456789012","price":"$20.00"},
          {"title":"Sony WH-1000XM4 variants","url":"https://www.ebay.com/itm/256000000001","price":"$90.00 to $140.00"},
          {"title":"Sony WH-1000XM4 UK","url":"https://www.ebay.com/itm/256000000002","price":"GBP 99.00"}
        ]}"""
        val out = EbaySoldPage.parse(js)
        assertEquals(1, out.size)
        assertEquals("Sony WH-1000XM4 Black", out[0].title)
        assertEquals("https://www.ebay.com/itm/256123456789", out[0].url)
        assertEquals(129.99, out[0].price)
        assertEquals(ListingStatus.SOLD, out[0].status)
        assertEquals("Sold Sep 30, 2026", out[0].soldDate)
    }

    @Test fun blockedPageIsAnErrorNotZeroComps() {
        assertFailsWith<EbaySoldPage.PageUnreadable> { EbaySoldPage.parse("""{"blocked":true,"rows":[]}""") }
    }

    @Test fun parseUsd() {
        assertEquals(1234.5, EbaySoldPage.parseUsd("$1,234.50"))
        assertNull(EbaySoldPage.parseUsd("$1.00 to $2.00"))
        assertNull(EbaySoldPage.parseUsd("C $5.00"))
    }

    // ------------------------------------------------------ Browse API parse

    @Test fun parsesBrowseApiResults() {
        val body = """{"itemSummaries":[
          {"title":"Apple AirPods Pro 2","itemWebUrl":"https://www.ebay.com/itm/1","price":{"value":"149.99","currency":"USD"},
           "condition":"Used","image":{"imageUrl":"https://i.ebayimg.com/a.jpg"},
           "shippingOptions":[{"shippingCost":{"value":"0.00","currency":"USD"}}]},
          {"title":"no price","itemWebUrl":"https://www.ebay.com/itm/2"}
        ]}"""
        val out = EbayBrowseApi.parseSearch(body)
        assertEquals(1, out.size)
        assertEquals(149.99, out[0].price)
        assertEquals("$149.99", out[0].priceText)
        assertEquals("Free shipping", out[0].shipping)
        assertEquals(ListingStatus.ACTIVE, out[0].status)
        assertEquals(emptyList(), EbayBrowseApi.parseSearch("{}"))
    }

    // ------------------------------------------------------- identification

    @Test fun barcodeTitlesWin() {
        val id = FreeIdentification.build(
            gtin = "0711719541028",
            textLines = emptyList(), objectLabels = listOf("Gadget"),
            barcodeTitles = listOf("Sony PlayStation 5 Console Disc", "Sony PlayStation 5 Disc Edition Console", "PlayStation 5 Console Sony"),
            similarTitles = emptyList(),
        )
        assertEquals("high", id.confidence)
        assertTrue(id.searchQuery.contains("playstation"))
        assertTrue(id.searchQuery.contains("console"))
        assertEquals("0711719541028", id.gtin)
    }

    @Test fun nothingFoundMeansAskTheUser() {
        val id = FreeIdentification.build(null, emptyList(), listOf("Jacket"), emptyList(), emptyList())
        assertFalse(id.identified)
        assertEquals("", id.searchQuery)
    }

    @Test fun labelTextUsesBiggestTextAndModelNumbers() {
        val q = FreeIdentification.queryFromText(listOf(
            FreeIdentification.TextLine("Made in China", 10),
            FreeIdentification.TextLine("BOSE", 80),
            FreeIdentification.TextLine("QuietComfort 35 II", 40),
            FreeIdentification.TextLine("Model 789564-0010", 8),
        ))
        assertEquals("bose quietcomfort 35 ii 789564", q)
    }

    @Test fun quickButtonsOpenActiveAndSoldSearches() {
        assertEquals("https://www.ebay.com/sch/i.html?_nkw=vintage+hanes+beefy", SoldSearchLinks.ebayActive("vintage hanes beefy"))
        val sold = SoldSearchLinks.ebaySold("bose 700")
        assertTrue(sold.contains("_nkw=bose+700") && sold.contains("LH_Sold=1") && sold.contains("LH_Complete=1"))
    }

    @Test fun soldLinksAreEncoded() {
        val links = SoldSearchLinks.forQuery("air max 90")
        assertTrue(links.first().second.contains("_nkw=air+max+90"))
        assertTrue(links.first().second.contains("LH_Sold=1"))
    }
}
