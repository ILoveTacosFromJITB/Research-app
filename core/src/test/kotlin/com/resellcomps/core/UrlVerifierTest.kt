package com.resellcomps.core

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class UrlVerifierTest {
    @Test fun matchesIgnoringWwwCaseTrailingSlashAndFragment() {
        val v = UrlVerifier(listOf("https://www.Poshmark.com/listing/abc-123/#photos"))
        assertTrue(v.isVerified("https://poshmark.com/listing/abc-123"))
        assertTrue(v.isVerified("http://www.poshmark.com/listing/abc-123/"))
    }

    @Test fun rejectsUrlsNotReturnedBySearch() {
        val v = UrlVerifier(listOf("https://www.mercari.com/us/item/m111/"))
        assertFalse(v.isVerified("https://www.mercari.com/us/item/m112/"))
        assertFalse(v.isVerified("not a url"))
    }

    @Test fun queryStringMatters() {
        val v = UrlVerifier(listOf("https://example.com/item?id=1"))
        assertFalse(v.isVerified("https://example.com/item?id=2"))
    }

    @Test fun extractsUrlsFromToolOutput() {
        val text = """results: [{"url": "https://grailed.com/listings/9"}, see https://stockx.com/air-max-90.]"""
        assertEquals(listOf("https://grailed.com/listings/9", "https://stockx.com/air-max-90"), UrlVerifier.extractUrls(text))
    }

    @Test fun dedupeByNormalizedUrl() {
        fun l(url: String) = Listing("t", 1.0, "$1", "USD", url, ListingStatus.SOLD, ListingSource.WEB, "x")
        assertEquals(1, CompsFinder.dedupe(listOf(l("https://www.ebay.com/itm/1"), l("https://ebay.com/itm/1/"))).size)
    }
}
