package com.resellcomps.core

import java.net.URI

/**
 * Confirms a URL Claude reports was actually returned by the web search tool,
 * so a "comp" can't be an invented link.
 */
class UrlVerifier(seenUrls: Collection<String>) {
    private val seen: Set<String> = seenUrls.mapNotNull(::normalize).toSet()

    fun isVerified(url: String): Boolean = normalize(url)?.let { it in seen } ?: false

    val size: Int get() = seen.size

    companion object {
        private val URL_REGEX = Regex("""https?://[^\s"'<>\\)\]}]+""")

        fun extractUrls(text: String): List<String> =
            URL_REGEX.findAll(text).map { it.value.trimEnd('.', ',', ';', ':') }.toList()

        /** Lowercase host, no "www.", no fragment, no trailing slash. Query is kept: it often identifies the item. */
        fun normalize(url: String): String? = runCatching {
            val u = URI(url.trim())
            val scheme = u.scheme?.lowercase() ?: return null
            if (scheme != "http" && scheme != "https") return null
            val host = u.host?.lowercase()?.removePrefix("www.") ?: return null
            val path = (u.rawPath ?: "").trimEnd('/')
            val query = u.rawQuery?.let { "?$it" } ?: ""
            "$host$path$query"
        }.getOrNull()
    }
}
