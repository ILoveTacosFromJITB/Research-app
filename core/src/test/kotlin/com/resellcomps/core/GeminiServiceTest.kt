package com.resellcomps.core

import java.io.IOException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class GeminiServiceTest {
    @Test fun picksNewestStableFlashModels() {
        val body = """{"models":[
          {"name":"models/gemini-2.5-flash","supportedGenerationMethods":["generateContent","countTokens"]},
          {"name":"models/gemini-3.5-flash-lite","supportedGenerationMethods":["generateContent"]},
          {"name":"models/gemini-3.5-flash","supportedGenerationMethods":["generateContent"]},
          {"name":"models/gemini-3.5-flash-image","supportedGenerationMethods":["generateContent"]},
          {"name":"models/gemini-3.6-flash-preview-09-2026","supportedGenerationMethods":["generateContent"]},
          {"name":"models/gemini-3.5-pro","supportedGenerationMethods":["generateContent"]},
          {"name":"models/text-embedding-004","supportedGenerationMethods":["embedContent"]}
        ]}"""
        assertEquals(listOf("gemini-3.5-flash", "gemini-3.5-flash-lite", "gemini-2.5-flash"), GeminiService.pickModels(body))
    }

    @Test fun parsesStructuredAnswer() {
        val inner = """{"identified":true,"name":"Alvin and the Chipmunks: The Road Chip","brand":null,"model":null,""" +
            """"category":"Movies","details":"Blu-ray + DVD + Digital HD","search_query":"Alvin and the Chipmunks Road Chip Blu-ray","confidence":"high"}"""
        val body = """{"candidates":[{"content":{"parts":[{"text":${kotlinx.serialization.json.JsonPrimitive(inner)}}]}}]}"""
        val id = GeminiService.parseAnswer(body)
        assertTrue(id.identified)
        assertEquals("alvin and the chipmunks road chip blu-ray", id.searchQuery)
        assertEquals(null, id.brand)
        assertEquals("high", id.confidence)
    }

    @Test fun blockedAnswerIsAnError() {
        assertFailsWith<IOException> { GeminiService.parseAnswer("""{"promptFeedback":{"blockReason":"SAFETY"}}""") }
    }
}
