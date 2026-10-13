package com.resellcomps.app

import android.graphics.BitmapFactory
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import com.google.mlkit.vision.label.ImageLabeling
import com.google.mlkit.vision.label.defaults.ImageLabelerOptions
import com.google.mlkit.vision.text.TextRecognition
import com.google.mlkit.vision.text.latin.TextRecognizerOptions
import com.resellcomps.core.FreeIdentification
import kotlinx.coroutines.tasks.await

/** Free, on-phone reading of the photo with ML Kit: barcode, printed text, and a generic object label. */
object OnDeviceReader {

    data class Result(
        val gtin: String?,
        val textLines: List<FreeIdentification.TextLine>,
        val labels: List<String>,
    )

    private val PRODUCT_FORMATS = setOf(
        Barcode.FORMAT_UPC_A, Barcode.FORMAT_UPC_E, Barcode.FORMAT_EAN_13, Barcode.FORMAT_EAN_8,
    )

    suspend fun read(jpeg: ByteArray): Result {
        val bitmap = BitmapFactory.decodeByteArray(jpeg, 0, jpeg.size) ?: error("Could not read the photo")
        val image = InputImage.fromBitmap(bitmap, 0)

        val barcodes = runCatching { BarcodeScanning.getClient().process(image).await() }.getOrDefault(emptyList())
        val gtin = barcodes.firstOrNull { it.format in PRODUCT_FORMATS }?.rawValue

        val text = runCatching {
            TextRecognition.getClient(TextRecognizerOptions.DEFAULT_OPTIONS).process(image).await()
        }.getOrNull()
        val lines = text?.textBlocks.orEmpty().flatMap { it.lines }.map {
            FreeIdentification.TextLine(it.text, it.boundingBox?.height() ?: 0)
        }

        val labels = runCatching {
            ImageLabeling.getClient(ImageLabelerOptions.DEFAULT_OPTIONS).process(image).await()
        }.getOrDefault(emptyList())
            .filter { it.confidence >= 0.6f }
            .sortedByDescending { it.confidence }
            .map { it.text }

        return Result(gtin, lines, labels)
    }
}
