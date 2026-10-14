package com.resellcomps.app.ui

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.graphics.BitmapFactory
import androidx.compose.foundation.Image
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.resellcomps.core.ItemIdentification
import com.resellcomps.core.SoldSearchLinks

/** Ready-made questions for Google Lens, picked with one tap. */
private val LENS_QUESTIONS = listOf(
    "What year is this tag?",
    "What brand is this?",
    "Is this vintage?",
    "What is this called?",
)

private fun open(context: Context, url: String) =
    context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))

/**
 * The quick screen: the search the app wrote for you, and one-tap buttons that
 * open eBay's active and sold results for it.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun IdentifiedScreen(
    item: ItemIdentification,
    photos: List<ByteArray>,
    modifier: Modifier,
    onRetake: () -> Unit,
    onAddPhoto: () -> Unit,
    onOpenSettings: () -> Unit,
    lensQuestion: String,
    onLensQuestionChange: (String) -> Unit,
) {
    val context = LocalContext.current
    var query by rememberSaveable(item) { mutableStateOf(item.searchQuery) }
    var showDetails by rememberSaveable(item) { mutableStateOf(false) }
    val q = query.trim()
    // Opened from "Search by text": no photo, just type and go.
    val textMode = photos.isEmpty()
    val focus = remember { FocusRequester() }
    LaunchedEffect(textMode) { if (textMode) focus.requestFocus() }

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        photos.firstOrNull()?.let { PhotoPreview(it) }

        item.warning?.let { warning ->
            Card(
                Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
            ) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(warning, style = MaterialTheme.typography.bodyMedium)
                    OutlinedButton(onClick = onOpenSettings) { Text("Open Settings") }
                }
            }
        }

        if (!textMode && (!item.identified || q.isEmpty())) {
            Text(
                "Couldn't read enough from the photo. Add a close-up of the tag or label, or type the search below.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.error,
            )
        }

        OutlinedTextField(
            value = query, onValueChange = { query = it },
            label = { Text(if (textMode) "Type what it is, e.g. vintage hanes beefy" else "Search") }, singleLine = true,
            textStyle = MaterialTheme.typography.titleLarge,
            keyboardOptions = KeyboardOptions(capitalization = KeyboardCapitalization.None, imeAction = ImeAction.Done),
            modifier = Modifier.fillMaxWidth().focusRequester(focus),
        )

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            Button(
                onClick = { open(context, SoldSearchLinks.ebayActive(q)) },
                enabled = q.isNotEmpty(),
                modifier = Modifier.weight(1f).height(64.dp),
            ) { Text("eBay Active", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold) }
            Button(
                onClick = { open(context, SoldSearchLinks.ebaySold(q)) },
                enabled = q.isNotEmpty(),
                modifier = Modifier.weight(1f).height(64.dp),
            ) { Text("eBay Sold", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold) }
        }

        // Terapeak on its own row (eBay's official sold research), then the other marketplaces.
        val others = SoldSearchLinks.forQuery(q).drop(1)
        listOf(others.take(1), others.drop(1)).forEach { row ->
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                row.forEach { (name, url) ->
                    OutlinedButton(onClick = { open(context, url) }, enabled = q.isNotEmpty(), modifier = Modifier.weight(1f)) {
                        Text(
                            if (name == "Terapeak") "Terapeak sold research (eBay)" else name,
                            style = MaterialTheme.typography.labelMedium, maxLines = 1,
                        )
                    }
                }
            }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            OutlinedButton(onClick = onAddPhoto, modifier = Modifier.weight(1f)) { Text(if (textMode) "Use a photo" else "Add tag photo") }
            OutlinedButton(onClick = onRetake, modifier = Modifier.weight(1f)) { Text("New item") }
        }


        if (!textMode) {

            // Lens can't be handed text by another app, so the question is copied to paste into
            // its "Add to your search" box. Empty = the search words above.
            var lensText by rememberSaveable { mutableStateOf(lensQuestion) }
            val lensSends = lensText.trim().ifEmpty { q }
            Text("Search with Google Lens", style = MaterialTheme.typography.titleSmall)
            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                (listOf("" to "My search") + LENS_QUESTIONS.map { it to it }).forEach { (value, label) ->
                    FilterChip(
                        selected = lensText.trim() == value,
                        onClick = { lensText = value; onLensQuestionChange(value) },
                        label = { Text(label) },
                    )
                }
            }
            OutlinedTextField(
                value = lensText,
                onValueChange = { lensText = it; onLensQuestionChange(it) },
                label = { Text("Question sent with the photo") },
                placeholder = { Text(q.ifEmpty { "e.g. what year is this tag" }) },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
            Text(
                "Tap a photo: Lens opens with it and your question is copied — tap \"Add to your search\" in Lens and paste.",
                style = MaterialTheme.typography.bodySmall,
            )
            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                itemsIndexed(photos) { i, photo ->
                    val bitmap = remember(photo) { BitmapFactory.decodeByteArray(photo, 0, photo.size)?.asImageBitmap() }
                    bitmap?.let {
                        Image(
                            bitmap = it,
                            contentDescription = "Search photo ${i + 1} with Google Lens",
                            contentScale = ContentScale.Crop,
                            modifier = Modifier.size(84.dp).clip(MaterialTheme.shapes.medium)
                                .clickable { GoogleLens.open(context, photo, lensSends) },
                        )
                    }
                }
            }

            TextButton(onClick = { showDetails = !showDetails }) {
                Text(if (showDetails) "Hide what the app saw" else "What the app saw")
            }
            if (showDetails) {
                Card(Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text(item.name, style = MaterialTheme.typography.titleMedium)
                        listOfNotNull(
                            item.brand?.let { "Brand: $it" },
                            item.model?.let { "Model: $it" },
                            item.category.takeIf { it.isNotBlank() }?.let { "Category: $it" },
                        ).forEach { Text(it, style = MaterialTheme.typography.bodyMedium) }
                        if (item.details.isNotBlank()) Text(item.details, style = MaterialTheme.typography.bodySmall)
                        Text("Confidence: ${item.confidence}", style = MaterialTheme.typography.labelMedium)
                    }
                }
            }
        }
    }
}
