package com.resellcomps.core

import kotlin.test.Test
import kotlin.test.assertTrue

class HttpTest {
    @Test fun timeoutsAllowSlowAiRepliesAndPhotoUploads() {
        // OkHttp's 10 s defaults caused "timeout" errors while the AI was still thinking.
        assertTrue(Http.client.readTimeoutMillis >= 60_000)
        assertTrue(Http.client.writeTimeoutMillis >= 60_000)
        assertTrue(Http.client.callTimeoutMillis >= Http.client.readTimeoutMillis)
    }
}
