package com.resellcomps.core

/** What the photos show. Nothing here is a price: prices are checked on eBay itself. */
data class ItemIdentification(
    val name: String,
    val brand: String?,
    val model: String?,
    val category: String,
    val details: String,
    val searchQuery: String,
    val confidence: String,
    val identified: Boolean,
    /** UPC/EAN read from a barcode in the photo, if any. */
    val gtin: String? = null,
    /** Other ways to search for it (with "vintage"/era, more specific, broader), best first. */
    val altQueries: List<String> = emptyList(),
    /** "yes", "no" or "unsure" — the AI's call on whether the item is vintage. */
    val vintage: String? = null,
    /** Decade or year range, e.g. "1990s". */
    val era: String? = null,
    /** What the vintage call is based on, e.g. "single-stitch hem, made in USA tag". */
    val vintageClues: String? = null,
    /** Everything the AI read on the tags, written before it decided on vintage. */
    val tagReading: String? = null,
    /** The tag expert's side-by-side reasoning (older vs newer), from "Dig deeper". */
    val vintageReasoning: String? = null,
    /** Shown prominently when the name came from a weak source. */
    val warning: String? = null,
)
