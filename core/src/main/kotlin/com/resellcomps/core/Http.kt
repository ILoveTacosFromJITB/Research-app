package com.resellcomps.core

import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit

/**
 * One shared HTTP client. OkHttp's default 10-second read/write timeouts are
 * too short here: AI models often think for 10–30 s before replying, and
 * uploading several photos on mobile data can take longer than 10 s.
 */
object Http {
    val client: OkHttpClient by lazy {
        OkHttpClient.Builder()
            .connectTimeout(20, TimeUnit.SECONDS)
            .writeTimeout(90, TimeUnit.SECONDS)
            .readTimeout(120, TimeUnit.SECONDS)
            .callTimeout(180, TimeUnit.SECONDS)
            .retryOnConnectionFailure(true)
            .build()
    }
}
