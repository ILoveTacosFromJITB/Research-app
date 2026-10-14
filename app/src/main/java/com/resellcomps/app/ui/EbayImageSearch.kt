package com.resellcomps.app.ui

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.widget.Toast
import androidx.core.content.FileProvider
import java.io.File

/**
 * Hands a photo to the eBay app, the way the Google Lens button does. eBay has no
 * official way for other apps to start its image search, so this only shows when
 * the installed eBay app says it accepts shared photos.
 */
object EbayImageSearch {
    private const val EBAY_APP = "com.ebay.mobile"

    private fun shareIntent(context: Context, jpeg: ByteArray? = null): Intent =
        Intent(Intent.ACTION_SEND).apply {
            type = "image/jpeg"
            setPackage(EBAY_APP)
            if (jpeg != null) {
                val dir = File(context.cacheDir, "photos").apply { mkdirs() }
                val file = File(dir, "ebay_${jpeg.contentHashCode()}.jpg").apply { writeBytes(jpeg) }
                putExtra(Intent.EXTRA_STREAM, FileProvider.getUriForFile(context, "${context.packageName}.files", file))
                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            }
        }

    /** True when the eBay app is installed and accepts a shared photo. */
    fun isAvailable(context: Context): Boolean =
        context.packageManager.queryIntentActivities(shareIntent(context), 0).isNotEmpty()

    fun open(context: Context, jpeg: ByteArray) {
        try {
            context.startActivity(shareIntent(context, jpeg))
        } catch (e: ActivityNotFoundException) {
            Toast.makeText(context, "The eBay app didn't accept the photo.", Toast.LENGTH_LONG).show()
        }
    }
}
