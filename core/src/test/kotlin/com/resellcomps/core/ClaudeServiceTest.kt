package com.resellcomps.core

import com.anthropic.core.JsonValue
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlin.test.Test
import kotlin.test.assertEquals

class ClaudeServiceTest {
    @Test fun toolInputRoundTripsToJson() {
        val v = JsonValue.from(mapOf("listings" to listOf(mapOf("url" to "https://a.com/1", "price" to 12.5))))
        val o = Json.parseToJsonElement(ClaudeService.jsonOf(v)) as JsonObject
        val first = o["listings"]!!.jsonArray[0].jsonObject
        assertEquals("https://a.com/1", first["url"]!!.jsonPrimitive.content)
        assertEquals("12.5", first["price"]!!.jsonPrimitive.content)
    }

    @Test fun submitCompsSchemaIsStrictCompatible() {
        val tool = Json.parseToJsonElement(ClaudeService.jsonOf(JsonValue.from(ClaudeService.SUBMIT_COMPS_TOOL))).jsonObject
        assertEquals("true", tool["strict"]!!.jsonPrimitive.content)
        val schema = tool["input_schema"]!!.jsonObject
        assertEquals("false", schema["additionalProperties"]!!.jsonPrimitive.content)
        val item = schema["properties"]!!.jsonObject["listings"]!!.jsonObject["items"]!!.jsonObject
        assertEquals("false", item["additionalProperties"]!!.jsonPrimitive.content)
        assertEquals(item["properties"]!!.jsonObject.keys, item["required"]!!.jsonArray.map { it.jsonPrimitive.content }.toSet())
    }
}
