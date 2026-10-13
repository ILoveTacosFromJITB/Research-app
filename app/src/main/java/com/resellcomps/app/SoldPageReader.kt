package com.resellcomps.app

import android.annotation.SuppressLint
import android.content.Context
import android.os.Handler
import android.os.Looper
import android.webkit.WebView
import android.webkit.WebViewClient
import com.resellcomps.core.EbaySoldPage
import com.resellcomps.core.Listing
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonPrimitive
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference

/**
 * Experimental: loads eBay's sold-search page in an invisible browser on the
 * phone and reads the sold listings off it. Breaks if eBay changes the page.
 * Must be called off the main thread; it blocks until the page is read.
 */
class SoldPageReader(private val context: Context) {

    @SuppressLint("SetJavaScriptEnabled")
    fun read(query: String): List<Listing> {
        val main = Handler(Looper.getMainLooper())
        val done = CountDownLatch(1)
        val result = AtomicReference<String?>(null)
        val webViewRef = AtomicReference<WebView?>(null)

        main.post {
            val web = WebView(context.applicationContext)
            webViewRef.set(web)
            web.settings.javaScriptEnabled = true
            web.settings.domStorageEnabled = true
            web.webViewClient = object : WebViewClient() {
                private var extracted = false
                override fun onPageFinished(view: WebView, url: String) {
                    if (extracted) return
                    extracted = true
                    // Give the page a moment to finish rendering its results.
                    main.postDelayed({
                        view.evaluateJavascript(EbaySoldPage.EXTRACT_JS) { value ->
                            result.set(value)
                            done.countDown()
                        }
                    }, 1500)
                }
            }
            web.loadUrl(EbaySoldPage.searchUrl(query))
        }

        val finished = done.await(30, TimeUnit.SECONDS)
        main.post { webViewRef.get()?.destroy() }
        if (!finished) throw EbaySoldPage.PageUnreadable("eBay's page took too long to load")

        // evaluateJavascript hands back the JS string as a JSON string literal.
        val raw = result.get() ?: throw EbaySoldPage.PageUnreadable("Nothing came back from the page")
        val unwrapped = runCatching { (Json.parseToJsonElement(raw) as JsonPrimitive).content }
            .getOrElse { throw EbaySoldPage.PageUnreadable("Unexpected page content") }
        return EbaySoldPage.parse(unwrapped)
    }
}
