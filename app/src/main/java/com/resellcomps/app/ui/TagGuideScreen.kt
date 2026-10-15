package com.resellcomps.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.resellcomps.core.TagGuide

/** Browse the built-in tag reference: general dating rules, then each brand's tag history. */
@Composable
fun TagGuideScreen(modifier: Modifier) {
    var filter by rememberSaveable { mutableStateOf("") }
    val brands = TagGuide.BRANDS.filter { b ->
        filter.isBlank() || b.name.contains(filter, ignoreCase = true) ||
            b.aliases.any { it.contains(filter.trim().lowercase()) } || b.category.contains(filter, ignoreCase = true)
    }
    LazyColumn(modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        item {
            Text(
                "Rules of thumb resellers use to date tags. Ranges are typical, not exact — tag styles overlap, " +
                    "so weigh several clues together. The app's AI uses these too.",
                style = MaterialTheme.typography.bodySmall,
                modifier = Modifier.padding(top = 8.dp),
            )
        }
        item {
            OutlinedTextField(
                value = filter, onValueChange = { filter = it },
                label = { Text("Find a brand") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
            )
        }
        if (filter.isBlank()) {
            item {
                Card(Modifier.fillMaxWidth(), colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)) {
                    Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                        Text("Works for any tag", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                        TagGuide.GENERAL_RULES.forEach { Text("• $it", style = MaterialTheme.typography.bodyMedium) }
                    }
                }
            }
        }
        items(brands, key = { it.name }) { BrandCard(it, startOpen = filter.isNotBlank()) }
        item { Text("", Modifier.padding(bottom = 16.dp)) }
    }
}

@Composable
fun BrandCard(brand: TagGuide.Brand, startOpen: Boolean = false) {
    var open by rememberSaveable(brand.name, startOpen) { mutableStateOf(startOpen) }
    Card(Modifier.fillMaxWidth()) {
        Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(brand.name, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            Text(brand.category, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            if (open) BrandNotes(brand)
            TextButton(onClick = { open = !open }) { Text(if (open) "Hide" else "Show tag history") }
        }
    }
}

/** One brand's tag eras and tips. */
@Composable
fun BrandNotes(brand: TagGuide.Brand) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        brand.eras.forEach { era ->
            Text(era.years, style = MaterialTheme.typography.labelLarge, fontWeight = FontWeight.Bold)
            Text(era.look, style = MaterialTheme.typography.bodyMedium)
        }
        brand.tips.forEach { Text("Tip: $it", style = MaterialTheme.typography.bodySmall) }
        brand.source?.let {
            Text("Source: $it", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
