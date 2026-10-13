package com.resellcomps.core

import java.io.IOException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class GoogleVisionTest {
    private val sample = """
    {"responses": [
      {"logoAnnotations": [{"description": "Patagonia", "score": 0.93}],
       "webDetection": {
         "bestGuessLabels": [{"label": "patagonia better sweater", "languageCode": "en"}],
         "webEntities": [{"description": "Fleece jacket", "score": 0.6}, {"description": "Patagonia", "score": 0.9}],
         "pagesWithMatchingImages": [
           {"url": "https://www.patagonia.com/product/mens-better-sweater", "pageTitle": "Men's <b>Better Sweater</b> Fleece Jacket"},
           {"url": "https://www.ebay.com/itm/1234567890", "pageTitle": "<b>Patagonia</b> Better Sweater Fleece Jacket Mens M Gray | eBay"},
           {"url": "https://poshmark.com/listing/abc", "pageTitle": "Patagonia Better Sweater Fleece Jacket Men&#39;s L - Poshmark"},
           {"url": "https://www.mercari.com/us/item/m1/", "pageTitle": "Patagonia better sweater fleece jacket | Mercari"}
         ]}},
      {"webDetection": {"pagesWithMatchingImages": [
           {"url": "https://www.ebay.com/itm/1234567890", "pageTitle": "duplicate"}]}}
    ]}
    """.trimIndent()

    @Test fun parsesLensStyleResultsMarketplacesFirst() {
        val r = GoogleVision.parse(sample)
        assertEquals(listOf("patagonia better sweater"), r.bestGuesses)
        assertEquals(listOf("Patagonia"), r.logos)
        assertEquals("Patagonia", r.entities.first())
        assertEquals(4, r.matches.size) // duplicate URL merged
        assertTrue(r.matches.take(3).all { it.isMarketplace })
        assertEquals("Patagonia Better Sweater Fleece Jacket Mens M Gray", r.matches.first { it.site == "ebay.com" }.title)
        assertEquals("Patagonia Better Sweater Fleece Jacket Men's L", r.matches.first { it.site == "poshmark.com" }.title)
    }

    @Test fun queryComesFromMarketplaceListingsOfTheSameImage() {
        assertEquals("patagonia better sweater fleece jacket", GoogleVision.query(GoogleVision.parse(sample)))
    }

    @Test fun fallsBackToBestGuessWithBrand() {
        val r = GoogleVision.Result(listOf("hooded sweatshirt"), emptyList(), listOf("Carhartt"), emptyList())
        assertEquals("carhartt hooded sweatshirt", GoogleVision.query(r))
    }

    @Test fun apiErrorsThrow() {
        assertFailsWith<IOException> { GoogleVision.parse("""{"responses":[{"error":{"code":7,"message":"API key not valid"}}]}""") }
    }

    @Test fun visionDrivesFreeIdentification() {
        val id = FreeIdentification.build(null, emptyList(), emptyList(), emptyList(), emptyList(), GoogleVision.parse(sample))
        assertEquals("patagonia better sweater fleece jacket", id.searchQuery)
        assertEquals("high", id.confidence)
        assertEquals(4, id.visualMatches.size)
    }
}
