package com.resellcomps.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import java.net.URLEncoder

/**
 * Reads sold listings from eBay's public sold-search page, which the app loads
 * on the phone. eBay changes this page from time to time; when it can't be
 * read, the app says so instead of reporting "no comps".
 */
object EbaySoldPage {

    fun searchUrl(query: String): String =
        "https://www.ebay.com/sch/i.html?_nkw=${enc(query)}&LH_Sold=1&LH_Complete=1&_ipg=120&rt=nc"

    /**
     * Runs in the page and returns a JSON array of {title, url, price, sold, img, condition}.
     * Stops at eBay's "results matching fewer words" divider so loose matches aren't counted.
     */
    val EXTRACT_JS = """
        (function() {
          var out = [];
          var list = document.querySelector('ul.srp-results') || document.querySelector('.srp-river-results ul') || document;
          var rows = list.querySelectorAll(':scope > li');
          if (!rows.length) rows = document.querySelectorAll('li.s-item, li.s-card');
          for (var i = 0; i < rows.length; i++) {
            var li = rows[i];
            var cls = li.className || '';
            if (cls.indexOf('srp-river-answer') >= 0) break;
            var a = li.querySelector('a[href*="/itm/"]');
            if (!a) continue;
            var t = li.querySelector('.s-item__title, .s-card__title, [role="heading"]');
            var p = li.querySelector('.s-item__price, .s-card__price');
            var text = li.innerText || '';
            var m = text.match(/Sold\s+[A-Z][a-z]{2}\s+\d{1,2},\s+\d{4}/);
            var c = li.querySelector('.SECONDARY_INFO, .s-card__subtitle');
            var img = li.querySelector('img');
            out.push({
              title: t ? t.innerText : '',
              url: a.href,
              price: p ? p.innerText : '',
              sold: m ? m[0] : '',
              condition: c ? c.innerText : '',
              img: img ? (img.getAttribute('src') || img.getAttribute('data-src') || '') : ''
            });
          }
          var blocked = !!document.querySelector('#captcha_form, iframe[src*="captcha"]') ||
                        /verify yourself|security measure/i.test(document.title + ' ' + (document.body ? document.body.innerText.slice(0, 2000) : ''));
          return JSON.stringify({blocked: blocked, rows: out});
        })();
    """.trimIndent()

    class PageUnreadable(message: String) : Exception(message)

    /** Parses what [EXTRACT_JS] returned. Throws [PageUnreadable] if eBay blocked the page. */
    fun parse(jsResult: String): List<Listing> {
        val root = Json.parseToJsonElement(jsResult) as? JsonObject ?: throw PageUnreadable("Unexpected page content")
        if ((root["blocked"] as? JsonPrimitive)?.content == "true") {
            throw PageUnreadable("eBay asked for a human check, so sold prices couldn't be read")
        }
        val rows = root["rows"] as? JsonArray ?: return emptyList()
        return rows.mapNotNull { el ->
            val o = el as? JsonObject ?: return@mapNotNull null
            val title = o.str("title")?.removePrefix("New Listing")?.trim()?.takeIf { it.isNotEmpty() } ?: return@mapNotNull null
            if (title.equals("Shop on eBay", ignoreCase = true)) return@mapNotNull null
            val url = o.str("url")?.let(::cleanItemUrl) ?: return@mapNotNull null
            val priceText = o.str("price")?.trim() ?: return@mapNotNull null
            val price = parseUsd(priceText) ?: return@mapNotNull null
            Listing(
                title = title,
                price = price,
                priceText = priceText,
                currency = "USD",
                url = url,
                status = ListingStatus.SOLD,
                source = ListingSource.EBAY_PAGE,
                marketplace = "eBay",
                condition = o.str("condition"),
                thumbnailUrl = o.str("img")?.takeIf { it.startsWith("http") },
                soldDate = o.str("sold"),
            )
        }
    }

    /** "$1,234.56" → 1234.56. Ranges ("$10.00 to $20.00") and non-USD prices are rejected. */
    fun parseUsd(text: String): Double? {
        if (text.contains(" to ", ignoreCase = true)) return null
        val m = Regex("""^\$\s?([0-9][0-9,]*(?:\.[0-9]{1,2})?)$""").find(text.trim()) ?: return null
        return m.groupValues[1].replace(",", "").toDoubleOrNull()?.takeIf { it > 0 }
    }

    /** https://www.ebay.com/itm/Some-Title/123456?hash=... → https://www.ebay.com/itm/123456 */
    fun cleanItemUrl(url: String): String? {
        val id = Regex("""/itm/(?:[^/?#]+/)?(\d{9,15})""").find(url)?.groupValues?.get(1) ?: return null
        return "https://www.ebay.com/itm/$id"
    }

    private fun enc(s: String) = URLEncoder.encode(s, "UTF-8")

    private fun JsonObject.str(key: String): String? =
        (this[key] as? JsonPrimitive)?.contentOrNull?.takeIf { it.isNotBlank() }
}

/** One-tap sold searches on other sites, so the user can see the proof there. */
object SoldSearchLinks {
    /** eBay's normal search: what's listed right now. */
    fun ebayActive(query: String): String =
        "https://www.ebay.com/sch/i.html?_nkw=${URLEncoder.encode(query, "UTF-8")}"

    fun ebaySold(query: String): String = EbaySoldPage.searchUrl(query)

    fun forQuery(query: String): List<Pair<String, String>> {
        val q = URLEncoder.encode(query, "UTF-8")
        return listOf(
            "eBay sold" to EbaySoldPage.searchUrl(query),
            "Poshmark sold" to "https://poshmark.com/search?query=$q&availability=sold_out",
            "Mercari sold" to "https://www.mercari.com/search/?keyword=$q&itemStatuses=2",
        )
    }
}
