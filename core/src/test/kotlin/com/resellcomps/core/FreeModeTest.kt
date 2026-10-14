package com.resellcomps.core

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class FreeModeTest {
    // -------------------------------------------------- no-key tag reading

    @Test fun nothingReadableMeansAskTheUser() {
        val id = FreeIdentification.build(null, emptyList(), listOf("Jacket"))
        assertFalse(id.identified)
        assertEquals("", id.searchQuery)
        assertEquals(FreeIdentification.BASIC_WARNING, id.warning)
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

    @Test fun ocrJunkIsDroppedFromSearch() {
        // What the phone read off an "Alvin and the Chipmunks: The Road Chip" Blu-ray cover.
        val q = FreeIdentification.queryFromText(listOf(
            FreeIdentification.TextLine("BLU-RAY + DVD + DIGITAL HD", 10),
            FreeIdentification.TextLine("AL I", 60),
            FreeIdentification.TextLine("AND", 15),
            FreeIdentification.TextLine("THE CHIPMUNKS", 22),
            FreeIdentification.TextLine("ROAD CHIP", 28),
            FreeIdentification.TextLine("$100 COUPONS", 10),
        ))
        assertEquals("road chip chipmunks", q)
    }

    // --------------------------------------------------------- quick links

    @Test fun quickButtonsOpenActiveAndSoldSearches() {
        assertEquals("https://www.ebay.com/sch/i.html?_nkw=vintage+hanes+beefy", SoldSearchLinks.ebayActive("vintage hanes beefy"))
        val sold = SoldSearchLinks.ebaySold("bose 700")
        assertTrue(sold.contains("_nkw=bose+700") && sold.contains("LH_Sold=1") && sold.contains("LH_Complete=1"))
    }

    @Test fun terapeakOpensSoldResearchForTheQuery() {
        val url = SoldSearchLinks.terapeak("vintage hanes beefy")
        assertTrue(url.startsWith("https://www.ebay.com/sh/research?"))
        assertTrue(url.contains("keywords=vintage+hanes+beefy") && url.contains("tabName=SOLD"))
    }

    @Test fun marketplaceLinksInOrder() {
        assertEquals(listOf("eBay sold", "Terapeak", "Poshmark sold", "Mercari sold"), SoldSearchLinks.forQuery("x").map { it.first })
    }
}
