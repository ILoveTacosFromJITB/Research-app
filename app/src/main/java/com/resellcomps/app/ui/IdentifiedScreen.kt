package com.resellcomps.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.resellcomps.core.ItemIdentification

@Composable
fun IdentifiedScreen(
    item: ItemIdentification,
    photos: List<ByteArray>,
    modifier: Modifier,
    offlineWarning: Boolean,
    onSearch: (ItemIdentification, String) -> Unit,
    onRetake: () -> Unit,
    onOpenSettings: () -> Unit,
) {
    val context = LocalContext.current
    var query by rememberSaveable(item) { mutableStateOf(item.searchQuery) }
    var name by rememberSaveable(item) { mutableStateOf(item.name) }

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        photos.firstOrNull()?.let { PhotoPreview(it) }
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                Text(
                    if (item.identified) "Looks like" else "Couldn't clearly identify this",
                    style = MaterialTheme.typography.labelLarge,
                )
                Text(item.name, style = MaterialTheme.typography.titleLarge)
                listOfNotNull(
                    item.brand?.let { "Brand: $it" },
                    item.model?.let { "Model: $it" },
                    item.category.takeIf { it.isNotBlank() }?.let { "Category: $it" },
                ).forEach { Text(it, style = MaterialTheme.typography.bodyMedium) }
                if (item.details.isNotBlank()) Text(item.details, style = MaterialTheme.typography.bodySmall)
                Text("ID confidence: ${item.confidence}", style = MaterialTheme.typography.labelMedium)
            }
        }
        OutlinedButton(
            onClick = { photos.firstOrNull()?.let { GoogleLens.open(context, it) } },
            modifier = Modifier.fillMaxWidth(),
        ) { Text("Not right? Search this photo with Google Lens") }
        Text(
            "Google Lens searches the whole web by image. Copy the exact name it finds into the box below.",
            style = MaterialTheme.typography.bodySmall,
        )
        if (offlineWarning) {
            Card(
                Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
            ) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(
                        "Online search is off. With no eBay keys, the app can't look up listings — " +
                            "you'll only get sold-search buttons.",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    OutlinedButton(onClick = onOpenSettings) { Text("Open Settings") }
                }
            }
        }
        Text(
            "Fix anything that's wrong — a precise search gives better comps.",
            style = MaterialTheme.typography.bodyMedium,
        )
        OutlinedTextField(
            value = name, onValueChange = { name = it },
            label = { Text("Item") }, modifier = Modifier.fillMaxWidth(),
        )
        OutlinedTextField(
            value = query, onValueChange = { query = it },
            label = { Text("Search terms") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
        )
        Button(
            onClick = { onSearch(item.copy(name = name.trim()), query.trim()) },
            enabled = query.isNotBlank(),
            modifier = Modifier.fillMaxWidth(),
        ) { Text("Find sold & active comps") }
        OutlinedButton(onClick = onRetake, modifier = Modifier.fillMaxWidth()) { Text("New photo") }
    }
}
