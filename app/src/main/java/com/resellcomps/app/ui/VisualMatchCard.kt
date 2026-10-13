package com.resellcomps.app.ui

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.resellcomps.core.VisualMatch

/** A page where the same image appears on the web. Tap to open it. */
@Composable
fun VisualMatchCard(m: VisualMatch) {
    val context = LocalContext.current
    Card(
        Modifier.fillMaxWidth().clickable {
            context.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(m.url)))
        },
    ) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(
                m.site + if (m.isMarketplace) " · listing" else "",
                style = MaterialTheme.typography.labelMedium,
                color = if (m.isMarketplace) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                fontWeight = if (m.isMarketplace) FontWeight.Bold else FontWeight.Normal,
            )
            Text(m.title, style = MaterialTheme.typography.bodyMedium, maxLines = 2, overflow = TextOverflow.Ellipsis)
        }
    }
}
