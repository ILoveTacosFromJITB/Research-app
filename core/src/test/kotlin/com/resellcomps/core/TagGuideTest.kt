package com.resellcomps.core

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class TagGuideTest {
    @Test fun findsBrandsFromNameOrTagReading() {
        assertEquals(listOf("Champion"), TagGuide.matching("Champion Reverse Weave", null).map { it.name })
        assertEquals(listOf("Russell Athletic"), TagGuide.matching(null, "RUSSELL ATHLETIC, Made in USA").map { it.name })
        assertTrue(TagGuide.matching("Fleece jacket").none { it.name.startsWith("Lee") }) // "lee" only as a whole word
    }

    @Test fun tagCheckGetsTheMatchingBrandNotes() {
        val item = ItemIdentification(
            name = "Yankees jersey", brand = "Russell Athletic", model = null, category = "Clothing", details = "",
            searchQuery = "russell athletic yankees jersey", confidence = "high", identified = true,
        )
        val text = TagExpert.userText(item)
        assertTrue(text.contains("Russell Athletic tags:"))
        assertTrue(text.contains("Diamond Collection"))
    }

    @Test fun everyBrandHasErasAndAliases() {
        TagGuide.BRANDS.forEach { b ->
            assertTrue(b.eras.isNotEmpty(), b.name)
            assertTrue(b.aliases.all { it == it.lowercase() }, b.name)
        }
    }

    @Test fun findsTheNewLabelBrands() {
        assertEquals(listOf("Towncraft (JCPenney)"), TagGuide.matching("Towncraft flannel shirt").map { it.name })
        assertEquals(listOf("Ralph Lauren (Polo, Purple Label, RRL…)"), TagGuide.matching("Polo by Ralph Lauren oxford").map { it.name })
        assertEquals(listOf("Levi's"), TagGuide.matching(null, "LEVI'S 501, care tag 0594").map { it.name })
    }

    @Test fun findsTheSecondBatchOfBrands() {
        val cases = mapOf(
            "Carhartt detroit jacket" to "Carhartt", "Dickies 874" to "Dickies", "Stussy tee" to "Stüssy",
            "Reebok windbreaker" to "Reebok", "Brockum Metallica tee" to "Brockum", "Winterland Grateful Dead" to "Winterland",
            "Pendleton board shirt" to "Pendleton", "L.L.Bean Freeport Maine" to "L.L. Bean", "Woolrich buffalo plaid" to "Woolrich",
            "Eddie Bauer goose down" to "Eddie Bauer", "Calvin Klein jeans" to "Calvin Klein", "Wrangler 13MWZ" to "Wrangler",
            "Lee Riders" to "Lee", "Tommy Hilfiger flag" to "Tommy Hilfiger", "Chaps sweater" to "Ralph Lauren (Polo, Purple Label, RRL…)",
        )
        cases.forEach { (text, brand) -> assertEquals(listOf(brand), TagGuide.matching(text).map { it.name }, text) }
    }
}
