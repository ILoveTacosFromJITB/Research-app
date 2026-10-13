package com.resellcomps.app.ui

import android.graphics.BitmapFactory
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PhotoCamera
import androidx.compose.material.icons.filled.PhotoLibrary
import androidx.compose.material3.Button
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import com.resellcomps.app.MainViewModel
import java.io.File

@Composable
fun HomeScreen(vm: MainViewModel, photo: ByteArray?, modifier: Modifier, onNeedKeys: () -> Unit) {
    val context = LocalContext.current
    var pendingUri by rememberSaveable { mutableStateOf<Uri?>(null) }
    var hint by rememberSaveable { mutableStateOf("") }

    val takePicture = rememberLauncherForActivityResult(ActivityResultContracts.TakePicture()) { ok ->
        if (ok) pendingUri?.let(vm::onPhoto)
    }
    val pickPhoto = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        uri?.let(vm::onPhoto)
    }

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (photo == null) {
            Text("What's it worth?", style = MaterialTheme.typography.headlineSmall)
            Text(
                "Snap a photo of an item. The app identifies it, pulls real sold and active listings, " +
                    "and only gives a price when real listings back it up. Every price links to its listing.",
                style = MaterialTheme.typography.bodyMedium,
            )
        } else {
            PhotoPreview(photo)
        }

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            Button(
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
                Text(if (photo == null) "  Take photo" else "  Retake")
            }
            OutlinedButton(
                onClick = { pickPhoto.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)) },
                modifier = Modifier.weight(1f),
            ) {
                Icon(Icons.Filled.PhotoLibrary, contentDescription = null)
                Text("  Gallery")
            }
        }

        if (photo != null) {
            OutlinedTextField(
                value = hint, onValueChange = { hint = it },
                label = { Text("Anything the photo doesn't show? (optional)") },
                placeholder = { Text("e.g. size M, 256GB, tags attached") },
                modifier = Modifier.fillMaxWidth(),
            )
            Button(
                onClick = { if (vm.hasKeys) vm.identify(hint) else onNeedKeys() },
                modifier = Modifier.fillMaxWidth(),
            ) { Text("Identify item") }
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
            modifier = Modifier.fillMaxWidth().heightIn(max = 320.dp),
        )
    }
}
