package com.resellcomps.core

import kotlinx.serialization.json.JsonPrimitive
import java.io.IOException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertNull

class FreeVisionAiTest {
    @Test fun detectsProviderFromKey() {
        assertEquals(FreeVisionAi.Provider.GROQ, FreeVisionAi.detect("gsk_abc"))
        assertEquals(FreeVisionAi.Provider.OPENROUTER, FreeVisionAi.detect("sk-or-v1-abc"))
        assertNull(FreeVisionAi.detect("AIzaSy-google"))
    }

    @Test fun ranksLikelyGroqImageModelsFirstAndDropsTextOnlyOnes() {
        val body = """{"data":[
          {"id":"llama-3.3-70b-versatile","active":true},
          {"id":"openai/gpt-oss-120b","active":true},
          {"id":"whisper-large-v3","active":true},
          {"id":"qwen/qwen3.6-27b","active":true},
          {"id":"qwen/qwen3.8-27b","active":true},
          {"id":"meta-llama/llama-guard-4-12b","active":true},
          {"id":"meta-llama/llama-4-maverick-17b-128e-instruct","active":false}
        ]}"""
        assertEquals(
            listOf("qwen/qwen3.8-27b", "qwen/qwen3.6-27b", "llama-3.3-70b-versatile"),
            FreeVisionAi.rankGroqModels(body),
        )
    }

    @Test fun parsesAnswerEvenWhenWrappedInCodeFences() {
        val content = "Here you go:\n```json\n{\"identified\": true, \"name\": \"Hanes Beefy T-Shirt\", \"brand\": \"Hanes\", " +
            "\"model\": null, \"category\": \"Clothing\", \"details\": \"single stitch\", " +
            "\"search_query\": \"Vintage Hanes Beefy\", \"confidence\": \"high\"}\n```"
        val body = """{"choices":[{"message":{"role":"assistant","content":${JsonPrimitive(content)}}}]}"""
        val id = FreeVisionAi.parseAnswer(body)
        assertEquals("vintage hanes beefy", id.searchQuery)
        assertEquals("Hanes", id.brand)
    }

    @Test fun noJsonIsAClearError() {
        val body = """{"choices":[{"message":{"content":"I can't tell what this is."}}]}"""
        assertFailsWith<IOException> { FreeVisionAi.parseAnswer(body) }
    }

    @Test fun readsHowLongToWaitFromGroqErrors() {
        val perMinute = """{"error":{"message":"Rate limit reached for model `x` on tokens per minute (TPM): Limit 6000, Used 5900, Requested 1800. Please try again in 16.95s."}}"""
        assertEquals(16.95, FreeVisionAi.retryAfterSeconds(null, perMinute))
        assertEquals(63.2, FreeVisionAi.retryAfterSeconds(null, "Please try again in 1m3.2s."))
        assertEquals(7 * 3600.0 + 12 * 60, FreeVisionAi.retryAfterSeconds(null, "requests per day (RPD) ... Please try again in 7h12m."))
        assertEquals(0.25, FreeVisionAi.retryAfterSeconds(null, "Please try again in 250ms."))
        assertEquals(9.0, FreeVisionAi.retryAfterSeconds("9", "anything"))
        assertNull(FreeVisionAi.retryAfterSeconds(null, "slow down"))
    }

    @Test fun readsVintageCall() {
        val content = """{"identified":true,"name":"Hanes Beefy tee","brand":"Hanes","model":null,"category":"Clothing",
            "details":"","search_query":"vintage hanes beefy","confidence":"high","vintage":"Yes","era":"1990s",
            "vintage_clues":["single-stitch hem","made in USA tag"]}"""
        val body = """{"choices":[{"message":{"content":${JsonPrimitive(content)}}}]}"""
        val id = FreeVisionAi.parseAnswer(body)
        assertEquals("yes", id.vintage)
        assertEquals("1990s", id.era)
        assertEquals("single-stitch hem, made in USA tag", id.vintageClues)
    }

    @Test fun readsAlternativeSearchesWithoutDuplicates() {
        val content = """{"identified":true,"name":"Mets jersey","search_query":"russell athletic mets jersey",
            "alt_queries":["Vintage Russell Athletic Mets Jersey","russell athletic mets jersey","mets diamond collection jersey",
            "mets jersey","mets jersey","90s mets baby blue jersey"],"confidence":"high"}"""
        val body = """{"choices":[{"message":{"content":${JsonPrimitive(content)}}}]}"""
        assertEquals(
            listOf("vintage russell athletic mets jersey", "mets diamond collection jersey", "mets jersey", "90s mets baby blue jersey"),
            FreeVisionAi.parseAnswer(body).altQueries,
        )
    }

    @Test fun ownServerAddressIsNormalized() {
        assertEquals("http://100.64.1.2:11434/v1", FreeVisionAi.normalizeServerUrl("100.64.1.2:11434"))
        assertEquals("http://my-pc:11434/v1", FreeVisionAi.normalizeServerUrl(" http://my-pc:11434/ "))
        assertEquals("https://pc.tailnet.ts.net/v1", FreeVisionAi.normalizeServerUrl("https://pc.tailnet.ts.net/v1"))
        assertEquals(FreeVisionAi.Provider.SELF, FreeVisionAi("", "100.64.1.2:11434").provider)
    }

    @Test fun ranksOllamaVisionModelsFirst() {
        val body = """{"data":[{"id":"llama3.1:8b"},{"id":"qwen2.5vl:7b"},{"id":"nomic-embed-text"},{"id":"gemma3:12b"}]}"""
        assertEquals(listOf("qwen2.5vl:7b", "gemma3:12b", "llama3.1:8b"), FreeVisionAi.rankGroqModels(body))
    }

    @Test fun unreachableOwnServerIsReportedAsOffline() {
        val ai = FreeVisionAi("", "127.0.0.1:9")  // nothing listens on port 9
        assertFailsWith<FreeVisionAi.ServerOffline> { ai.identify(listOf(ByteArray(10))) }
    }
}
