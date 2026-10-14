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

    @Test fun picksGroqVisionModelsBestFirst() {
        val body = """{"data":[
          {"id":"llama-3.3-70b-versatile"},
          {"id":"meta-llama/llama-4-scout-17b-16e-instruct"},
          {"id":"whisper-large-v3"},
          {"id":"meta-llama/llama-4-maverick-17b-128e-instruct"}
        ]}"""
        assertEquals(
            listOf("meta-llama/llama-4-maverick-17b-128e-instruct", "meta-llama/llama-4-scout-17b-16e-instruct"),
            FreeVisionAi.pickGroqModels(body),
        )
        assertEquals(listOf(FreeVisionAi.GROQ_FALLBACK), FreeVisionAi.pickGroqModels("""{"data":[{"id":"llama-3.3-70b-versatile"}]}"""))
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
}
