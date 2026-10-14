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
    /** Shown prominently when the name came from a weak source. */
    val warning: String? = null,
)
