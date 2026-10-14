package com.resellcomps.app.ui

import android.graphics.BitmapFactory
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material3.Button
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import com.resellcomps.app.MainViewModel
import com.resellcomps.core.IdentifyPrompt
import java.io.File

@Composable
fun HomeScreen(vm: MainViewModel, photos: List<ByteArray>, modifier: Modifier) {
    val context = LocalContext.current
    var pendingUri by rememberSaveable { mutableStateOf<Uri?>(null) }
    val room = IdentifyPrompt.MAX_PHOTOS - photos.size

    val takePicture = rememberLauncherForActivityResult(ActivityResultContracts.TakePicture()) { ok ->
        if (ok) pendingUri?.let { vm.addPhotos(listOf(it)) }
    }
    val pickPhotos = rememberLauncherForActivityResult(
        ActivityResultContracts.PickMultipleVisualMedia(IdentifyPrompt.MAX_PHOTOS)
    ) { uris -> vm.addPhotos(uris) }

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (photos.isEmpty()) {
            Text("What's it worth?", style = MaterialTheme.typography.headlineSmall)
            Text(
                "Take a photo (add the tag or label for a better match) and the app writes the search for you. " +
                    "Then tap eBay Active or eBay Sold to see real listings.",
                style = MaterialTheme.typography.bodyMedium,
            )
        } else {
            PhotoStrip(photos, onRemove = vm::removePhoto)
            Text(
                "${photos.size} of ${IdentifyPrompt.MAX_PHOTOS} photos. Add the tag, label or barcode if you haven't.",
                style = MaterialTheme.typography.bodySmall,
            )
        }

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            Button(
                enabled = room > 0,
                onClick = {
                    val dir = File(context.cacheDir, "photos").apply { mkdirs() }
                    val file = File(dir, "capture_${System.currentTimeMillis()}.jpg")
                    val uri = FileProvider.getUriForFile(context, "${context.packageName}.files", file)
                    pendingUri = uri
                    takePicture.launch(uri)
                },
                modifier = Modifier.weight(1f),
            ) {
                Icon(Icons.Filled.PhotoCamera, contentDescription = null)
                Text(if (photos.isEmpty()) "  Take photo" else "  Add photo")
            }
            OutlinedButton(
                enabled = room > 0,
                onClick = { pickPhotos.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)) },
                modifier = Modifier.weight(1f),
            ) {
                Icon(Icons.Filled.PhotoLibrary, contentDescription = null)
                Text("  Gallery")
            }
        }

        if (photos.isEmpty()) {
            OutlinedButton(onClick = vm::startTextSearch, modifier = Modifier.fillMaxWidth()) {
                Icon(Icons.Filled.Keyboard, contentDescription = null)
                Text("  Search by text")
            }
        }

        if (photos.isNotEmpty()) {
            Button(
                onClick = { vm.identify("") },
                modifier = Modifier.fillMaxWidth(),
            ) { Text("Identify again with these photos") }
        }
    }
}

@Composable
private fun PhotoStrip(photos: List<ByteArray>, onRemove: (Int) -> Unit) {
    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        itemsIndexed(photos) { i, photo ->
            Box {
                val bitmap = remember(photo) { BitmapFactory.decodeByteArray(photo, 0, photo.size)?.asImageBitmap() }
                bitmap?.let {
                    Image(
                        bitmap = it, contentDescription = "Photo ${i + 1}",
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.size(110.dp).clip(MaterialTheme.shapes.medium),
                    )
                }
                IconButton(
                    onClick = { onRemove(i) },
                    modifier = Modifier.align(Alignment.TopEnd).padding(4.dp).size(28.dp)
                        .background(Color.Black.copy(alpha = 0.55f), CircleShape),
                ) {
                    Icon(Icons.Filled.Close, contentDescription = "Remove photo", tint = Color.White, modifier = Modifier.size(18.dp))
                }
            }
        }
    }
}

@Composable
fun PhotoPreview(photo: ByteArray) {
    val bitmap = remember(photo) { BitmapFactory.decodeByteArray(photo, 0, photo.size)?.asImageBitmap() }
    bitmap?.let {
        Image(
            bitmap = it, contentDescription = "Your photo",
            contentScale = ContentScale.Fit,
            modifier = Modifier.fillMaxWidth().heightIn(max = 260.dp),
        )
    }
}
