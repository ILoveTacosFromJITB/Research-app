package com.resellcomps.app.ui

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import androidx.core.content.FileProvider
import java.io.File

/** Hands a photo to the Google app's Lens (free visual web search); falls back to the share sheet. */
object GoogleLens {
    private const val GOOGLE_APP = "com.google.android.googlequicksearchbox"

    fun open(context: Context, jpeg: ByteArray) {
        val dir = File(context.cacheDir, "photos").apply { mkdirs() }
        val file = File(dir, "lens.jpg").apply { writeBytes(jpeg) }
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.files", file)
        val send = Intent(Intent.ACTION_SEND).apply {
            type = "image/jpeg"
            putExtra(Intent.EXTRA_STREAM, uri)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
        try {
            context.startActivity(Intent(send).setPackage(GOOGLE_APP))
        } catch (e: ActivityNotFoundException) {
            context.startActivity(Intent.createChooser(send, "Search image with…"))
        }
    }
}
