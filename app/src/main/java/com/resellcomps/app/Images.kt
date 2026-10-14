package com.resellcomps.app

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.ImageDecoder
import android.net.Uri
import android.os.Build
import java.io.ByteArrayOutputStream
import kotlin.math.max

object Images {
    /** Long edge sent to Claude; larger images are downscaled server-side anyway. */
    private const val MAX_EDGE = 1568

    /** Loads a photo (honoring its rotation), shrinks it and returns JPEG bytes. */
    fun loadJpeg(context: Context, uri: Uri): ByteArray {
        val bitmap = if (Build.VERSION.SDK_INT >= 28) {
            val source = ImageDecoder.createSource(context.contentResolver, uri)
            ImageDecoder.decodeBitmap(source) { decoder, info, _ ->
                val longEdge = max(info.size.width, info.size.height)
                if (longEdge > MAX_EDGE) {
                    val scale = MAX_EDGE.toFloat() / longEdge
                    decoder.setTargetSize((info.size.width * scale).toInt(), (info.size.height * scale).toInt())
                }
                decoder.allocator = ImageDecoder.ALLOCATOR_SOFTWARE
            }
        } else {
            val raw = context.contentResolver.openInputStream(uri).use { BitmapFactory.decodeStream(it) }
                ?: error("Could not read the photo")
            val longEdge = max(raw.width, raw.height)
            if (longEdge > MAX_EDGE) {
                val scale = MAX_EDGE.toFloat() / longEdge
                Bitmap.createScaledBitmap(raw, (raw.width * scale).toInt(), (raw.height * scale).toInt(), true)
            } else raw
        }
        return ByteArrayOutputStream().use { out ->
            bitmap.compress(Bitmap.CompressFormat.JPEG, 85, out)
            out.toByteArray()
        }
    }

    /**
     * A smaller copy for free AI tiers, whose per-minute limits count image size: 1024 px is
     * still plenty to read tags and labels, and uses far fewer of the free tokens.
     */
    fun shrink(jpeg: ByteArray, maxEdge: Int = 1024): ByteArray {
        val bitmap = BitmapFactory.decodeByteArray(jpeg, 0, jpeg.size) ?: return jpeg
        val longEdge = max(bitmap.width, bitmap.height)
        if (longEdge <= maxEdge) return jpeg
        val scale = maxEdge.toFloat() / longEdge
        val small = Bitmap.createScaledBitmap(bitmap, (bitmap.width * scale).toInt(), (bitmap.height * scale).toInt(), true)
        return ByteArrayOutputStream().use { out ->
            small.compress(Bitmap.CompressFormat.JPEG, 80, out)
            out.toByteArray()
        }
    }
}
