package com.resellcomps.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class TagExpertTest {
    private val first = ItemIdentification(
        name = "Adidas zip sweatshirt", brand = "Adidas", model = null, category = "Clothing", details = "",
        searchQuery = "adidas zip sweatshirt", confidence = "medium", identified = true,
        vintage = "no", era = "2000s", vintageClues = "care symbols",
    )

    private fun json(s: String) = Json.parseToJsonElement(s) as JsonObject

    @Test fun olderCallAddsVintageToSearchAndKeepsBothSidesOfTheEvidence() {
        val answer = json("""{"tag_reading":"adidas trefoil, Made in USA, 50% cotton 50% polyester, written care",
            "evidence_older":["trefoil logo tag","Made in USA","written care, no symbols"],
            "evidence_newer":["multi-language care tag"],
            "reasoning":"Most clues point to the early 1990s.",
            "vintage":"yes","era":"early 1990s","vintage_clues":"Made in USA trefoil tag, written care"}""")
        val out = TagExpert.apply(first, answer, "adidas zip sweatshirt")
        assertEquals("yes", out.vintage)
        assertEquals("early 1990s", out.era)
        assertEquals("vintage adidas zip sweatshirt", out.searchQuery)
        assertTrue(out.vintageReasoning!!.contains("Points older: trefoil logo tag; Made in USA"))
        assertTrue(out.vintageReasoning!!.contains("Points newer: multi-language care tag"))
        assertTrue(out.vintageReasoning!!.endsWith("Most clues point to the early 1990s."))
    }

    @Test fun newerCallRemovesVintageFromSearch() {
        val out = TagExpert.apply(first.copy(vintage = "yes"), json("""{"vintage":"no","era":"2000s"}"""), "vintage adidas zip sweatshirt")
        assertEquals("adidas zip sweatshirt", out.searchQuery)
    }

    @Test fun promptsCarryTheDatingGuideWithoutBrokenIndentation() {
        assertTrue(IdentifyPrompt.SYSTEM.contains("FTC allowed care symbols"))
        assertTrue(TagExpert.SYSTEM.contains("evidence_older"))
        assertFalse(TagExpert.SYSTEM.lines().any { it.startsWith("        ") })
        assertFalse(IdentifyPrompt.SYSTEM.lines().any { it.startsWith("        ") })
        assertTrue(TagExpert.userText(first).contains("don't just repeat the first answer"))
    }

    @Test fun vintageYesKeepsEveryWordAndOffersThePlainSearch() {
        val item = first.copy(searchQuery = "adidas yankees quarter zip sweatshirt", vintage = "yes",
            altQueries = listOf("vintage adidas yankees sweatshirt", "adidas yankees pullover"))
        val out = IdentifyPrompt.withVintageSearches(item)
        assertEquals("vintage adidas yankees quarter zip sweatshirt", out.searchQuery)
        assertEquals(listOf("adidas yankees quarter zip sweatshirt", "vintage adidas yankees sweatshirt", "adidas yankees pullover"), out.altQueries)
    }

    @Test fun vintageUnsureAddsAVintageChip() {
        val out = IdentifyPrompt.withVintageSearches(first.copy(searchQuery = "adidas yankees quarter zip", vintage = "unsure"))
        assertEquals("adidas yankees quarter zip", out.searchQuery)
        assertEquals("vintage adidas yankees quarter zip", out.altQueries.first())
    }

    @Test fun tagCheckCanPutAPremiumLineInTheSearch() {
        val item = first.copy(name = "Ralph Lauren cardigan", brand = "Ralph Lauren", searchQuery = "ralph lauren cotton cardigan",
            vintage = "no", altQueries = listOf("ralph lauren cardigan xxl"))
        assertTrue(TagExpert.userText(item).contains("Purple Label (from about 1994)"))
        val out = TagExpert.apply(item, json("""{"vintage":"no","era":"2010s","better_search":"Ralph Lauren Purple Label cardigan"}"""), "ralph lauren cotton cardigan")
        assertEquals("ralph lauren purple label cardigan", out.searchQuery)
        assertEquals(listOf("ralph lauren cotton cardigan", "ralph lauren cardigan xxl"), out.altQueries)
        assertEquals("ralph lauren cotton cardigan", TagExpert.apply(item, json("""{"vintage":"no","better_search":null}"""), "ralph lauren cotton cardigan").searchQuery)
    }
}
