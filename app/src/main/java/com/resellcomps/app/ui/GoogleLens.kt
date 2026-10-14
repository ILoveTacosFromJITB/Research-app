package com.resellcomps.app.ui

import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.widget.Toast
import androidx.core.content.FileProvider
import java.io.File

/** Hands a photo to the Google app's Lens (free visual web search); falls back to the share sheet. */
object GoogleLens {
    private const val GOOGLE_APP = "com.google.android.googlequicksearchbox"

    /**
     * @param text the current search. Lens has no way for another app to fill its "Add to your
     *   search" box, so the text is copied to the clipboard to paste there (and also attached to
     *   the share, which Lens may pick up).
     */
    fun open(context: Context, jpeg: ByteArray, text: String? = null) {
        val dir = File(context.cacheDir, "photos").apply { mkdirs() }
        val file = File(dir, "lens_${jpeg.contentHashCode()}.jpg").apply { writeBytes(jpeg) }
        val uri = FileProvider.getUriForFile(context, "${context.packageName}.files", file)
        val send = Intent(Intent.ACTION_SEND).apply {
            type = "image/jpeg"
            putExtra(Intent.EXTRA_STREAM, uri)
            if (!text.isNullOrBlank()) putExtra(Intent.EXTRA_TEXT, text)
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
        if (!text.isNullOrBlank()) {
            val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
            clipboard.setPrimaryClip(ClipData.newPlainText("search", text))
            Toast.makeText(
                context,
                "Search copied — in Lens tap \"Add to your search\" and paste it",
                Toast.LENGTH_LONG,
            ).show()
        }
        try {
            context.startActivity(Intent(send).setPackage(GOOGLE_APP))
        } catch (e: ActivityNotFoundException) {
            context.startActivity(Intent.createChooser(send, "Search image with…"))
        }
    }
}
