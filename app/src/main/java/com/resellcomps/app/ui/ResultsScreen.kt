package com.resellcomps.app.ui

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Tab
import androidx.compose.material3.ScrollableTabRow
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import com.resellcomps.core.CheckedListing
import com.resellcomps.core.CompsReport
import com.resellcomps.core.ListingSource
import com.resellcomps.core.ListingStatus
import com.resellcomps.core.PriceStats
import com.resellcomps.core.SoldSearchLinks
import com.resellcomps.core.Valuation
import java.text.DateFormat
import java.util.Date

private fun money(v: Double) = "$" + "%,.2f".format(v)

@Composable
fun ResultsScreen(report: CompsReport, modifier: Modifier, onEditSearch: () -> Unit, onNewItem: () -> Unit) {
    val matched = report.checked.filter { it.isMatch }
    val sold = matched.filter { it.listing.status == ListingStatus.SOLD }.sortedBy { it.listing.price }
    val active = matched.filter { it.listing.status == ListingStatus.ACTIVE }.sortedBy { it.listing.price }
    val rejected = report.checked.filterNot { it.isMatch }
    val lens = report.item.visualMatches
    var tab by rememberSaveable { mutableIntStateOf(0) }
    val shown = when (tab) { 0 -> sold; 1 -> active; 2 -> emptyList(); else -> rejected }

    LazyColumn(modifier.padding(horizontal = 16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
        item {
            Spacer(Modifier.height(8.dp))
            Text(report.item.name, style = MaterialTheme.typography.titleMedium)
            Text(
                "Search: \"${report.query}\" · " +
                    DateFormat.getDateTimeInstance(DateFormat.MEDIUM, DateFormat.SHORT).format(Date(report.retrievedAtEpochMs)),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        item { ValuationCard(report.valuation, report.soldSearched) }
        item { SoldLinks(report.query) }
        if (report.notes.isNotEmpty()) {
            item {
                Column {
                    report.notes.forEach {
                        Text("• $it", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }
        }
        item {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = onEditSearch, modifier = Modifier.weight(1f)) { Text("Edit search") }
                Button(onClick = onNewItem, modifier = Modifier.weight(1f)) { Text("New item") }
            }
        }
        item {
            ScrollableTabRow(selectedTabIndex = tab, edgePadding = 0.dp) {
                Tab(selected = tab == 0, onClick = { tab = 0 }, text = { Text("Sold (${sold.size})") })
                Tab(selected = tab == 1, onClick = { tab = 1 }, text = { Text("Active (${active.size})") })
                Tab(selected = tab == 2, onClick = { tab = 2 }, text = { Text("Lens (${lens.size})") })
                Tab(selected = tab == 3, onClick = { tab = 3 }, text = { Text("Excluded (${rejected.size})") })
            }
        }
        if (tab == 2) {
            item {
                Text(
                    if (lens.isEmpty()) "No visual matches. Add a Google Vision key in Settings to turn on built-in visual search."
                    else "Pages showing the same item, like Google Lens. Prices aren't read from these; tap to view. " +
                        "They are not used in any price.",
                    style = MaterialTheme.typography.bodySmall,
                )
            }
            items(lens, key = { "lens:" + it.url }) { VisualMatchCard(it) }
        }
        if (tab == 3 && rejected.isNotEmpty()) {
            item {
                Text(
                    "Listings found but judged not the same item. They are not used in any price.",
                    style = MaterialTheme.typography.bodySmall,
                )
            }
        }
        if (shown.isEmpty() && tab != 2) {
            item {
                Text(
                    when (tab) {
                        0 -> "No matching sold listings found."
                        1 -> "No matching active listings found."
                        else -> "Nothing excluded."
                    },
                    modifier = Modifier.padding(vertical = 16.dp),
                )
            }
        }
        items(shown, key = { it.listing.url + it.listing.status }) { ListingCard(it) }
        item { Spacer(Modifier.height(24.dp)) }
    }
}

@Composable
private fun ValuationCard(v: Valuation, soldSearched: Boolean) {
    Card(
        Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer),
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            when (v) {
                is Valuation.FromSold -> {
                    Text("Sold value", style = MaterialTheme.typography.labelLarge)
                    Text(money(v.sold.median), style = MaterialTheme.typography.displaySmall, fontWeight = FontWeight.Bold)
                    Text("Median of ${v.sold.count} sold ${plural(v.sold.count, "listing")} — see the Sold tab for every one.")
                    RangeLines(v.sold)
                    v.active?.let {
                        Spacer(Modifier.height(4.dp))
                        Text(
                            "Currently listed: ${money(it.low)} – ${money(it.high)} across ${it.count} active (asking prices, not sales).",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                    if (v.sold.count < 3) {
                        Text(
                            "Only ${v.sold.count} sold comp${if (v.sold.count == 1) "" else "s"} — treat this as a thin signal.",
                            style = MaterialTheme.typography.bodySmall, fontStyle = FontStyle.Italic,
                        )
                    }
                }
                is Valuation.OnlyActive -> {
                    Text(
                        if (soldSearched) "No sold comps found" else "Sold prices not checked",
                        style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold,
                    )
                    Text(
                        if (soldSearched) "No value is given because no sale backs one up."
                        else "No value is given. Tap a sold-search button below to see what it actually sells for.",
                    )
                    Spacer(Modifier.height(4.dp))
                    Text(
                        "For reference only, ${v.active.count} active ${plural(v.active.count, "listing")} ask " +
                            "${money(v.active.low)} – ${money(v.active.high)} (median ${money(v.active.median)}). " +
                            "Asking prices are not what items sell for.",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                }
                Valuation.NoComps -> {
                    Text("No comps found", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                    Text(
                        (if (soldSearched) "No sold or active listings matched this item, so no price is given. "
                        else "No active listings matched and sold prices weren't checked, so no price is given. ") +
                            "Try editing the search terms (fewer words, or the exact model number).",
                    )
                }
            }
        }
    }
}

@Composable
private fun SoldLinks(query: String) {
    val context = LocalContext.current
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text("Check sold listings yourself", style = MaterialTheme.typography.labelLarge)
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            SoldSearchLinks.forQuery(query).forEach { (name, url) ->
                OutlinedButton(
                    onClick = { context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url))) },
                    modifier = Modifier.weight(1f),
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 4.dp, vertical = 8.dp),
                ) { Text(name, style = MaterialTheme.typography.labelMedium, maxLines = 1) }
            }
        }
    }
}

@Composable
private fun RangeLines(s: PriceStats) {
    Text("Range: ${money(s.low)} – ${money(s.high)}", style = MaterialTheme.typography.bodyMedium)
    if (s.typicalLow != null && s.typicalHigh != null) {
        Text("Middle half sold for: ${money(s.typicalLow!!)} – ${money(s.typicalHigh!!)}", style = MaterialTheme.typography.bodyMedium)
    }
}

private fun plural(n: Int, word: String) = if (n == 1) word else word + "s"

@Composable
private fun ListingCard(c: CheckedListing) {
    val context = LocalContext.current
    val l = c.listing
    Card(
        Modifier.fillMaxWidth().clickable {
            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(l.url)))
        },
    ) {
        Row(Modifier.padding(12.dp), verticalAlignment = Alignment.Top) {
            if (l.thumbnailUrl != null) {
                AsyncImage(model = l.thumbnailUrl, contentDescription = null, modifier = Modifier.size(72.dp))
                Spacer(Modifier.width(12.dp))
            }
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(l.priceText, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
                    Spacer(Modifier.width(8.dp))
                    Text(
                        (if (l.status == ListingStatus.SOLD) "SOLD" else "ACTIVE") + " · " + l.marketplace,
                        style = MaterialTheme.typography.labelMedium,
                        color = if (l.status == ListingStatus.SOLD) MaterialTheme.colorScheme.primary
                        else MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Text(l.title, style = MaterialTheme.typography.bodyMedium, maxLines = 3, overflow = TextOverflow.Ellipsis)
                listOfNotNull(l.condition, l.soldDate, l.shipping).takeIf { it.isNotEmpty() }?.let {
                    Text(it.joinToString(" · "), style = MaterialTheme.typography.bodySmall)
                }
                if (l.source == ListingSource.EBAY_PAGE) {
                    Text(
                        "Read from eBay's sold-search page — open the link to confirm.",
                        style = MaterialTheme.typography.bodySmall, fontStyle = FontStyle.Italic,
                    )
                }
                if (l.source == ListingSource.WEB) {
                    Text(
                        "Found by web search; price read from the search result — open the link to confirm.",
                        style = MaterialTheme.typography.bodySmall, fontStyle = FontStyle.Italic,
                    )
                    l.evidence?.takeIf { it.isNotBlank() }?.let {
                        Text("“$it”", style = MaterialTheme.typography.bodySmall, maxLines = 3, overflow = TextOverflow.Ellipsis)
                    }
                }
                if (!c.isMatch) {
                    Text("Excluded: ${c.reason}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                }
                Text(
                    l.url, style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.primary, maxLines = 1, overflow = TextOverflow.Ellipsis,
                )
            }
        }
    }
}
