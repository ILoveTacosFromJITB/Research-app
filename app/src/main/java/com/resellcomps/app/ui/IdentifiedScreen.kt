package com.resellcomps.app.ui

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.resellcomps.core.ItemIdentification
import com.resellcomps.core.SoldSearchLinks

private fun open(context: Context, url: String) =
    context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))

/**
 * The quick screen: the search the app wrote for you, and one-tap buttons that
 * open eBay's active and sold results for it.
 */
@Composable
fun IdentifiedScreen(
    item: ItemIdentification,
    photos: List<ByteArray>,
    modifier: Modifier,
    onSearch: (ItemIdentification, String) -> Unit,
    onRetake: () -> Unit,
    onAddPhoto: () -> Unit,
) {
    val context = LocalContext.current
    var query by rememberSaveable(item) { mutableStateOf(item.searchQuery) }
    var showDetails by rememberSaveable(item) { mutableStateOf(false) }
    val q = query.trim()

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        photos.firstOrNull()?.let { PhotoPreview(it) }

        if (!item.identified || q.isEmpty()) {
            Text(
                "Couldn't read enough from the photo. Add a close-up of the tag or label, or type the search below.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.error,
            )
        }

        OutlinedTextField(
            value = query, onValueChange = { query = it },
            label = { Text("Search") }, singleLine = true,
            textStyle = MaterialTheme.typography.titleLarge,
            modifier = Modifier.fillMaxWidth(),
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

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            SoldSearchLinks.forQuery(q).drop(1).forEach { (name, url) ->
                OutlinedButton(onClick = { open(context, url) }, enabled = q.isNotEmpty(), modifier = Modifier.weight(1f)) {
                    Text(name, style = MaterialTheme.typography.labelMedium, maxLines = 1)
                }
            }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
            OutlinedButton(onClick = onAddPhoto, modifier = Modifier.weight(1f)) { Text("Add tag photo") }
            OutlinedButton(onClick = onRetake, modifier = Modifier.weight(1f)) { Text("New item") }
        }

        OutlinedButton(
            onClick = { onSearch(item, q) },
            enabled = q.isNotEmpty(),
            modifier = Modifier.fillMaxWidth(),
        ) { Text("Show prices in the app") }

        TextButton(onClick = { photos.firstOrNull()?.let { GoogleLens.open(context, it) } }) {
            Text("Wrong item? Open in Google Lens")
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
            if (item.visualMatches.isNotEmpty()) {
                Text("Same photo found on the web", style = MaterialTheme.typography.titleSmall)
                item.visualMatches.take(6).forEach { VisualMatchCard(it) }
            }
        }
    }
}
