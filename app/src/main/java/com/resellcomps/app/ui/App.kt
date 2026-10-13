package com.resellcomps.app.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.resellcomps.app.MainViewModel
import com.resellcomps.app.Mode
import com.resellcomps.app.Screen

private val Green = Color(0xFF2E7D32)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun App(vm: MainViewModel) {
    val colors = if (isSystemInDarkTheme()) darkColorScheme(primary = Color(0xFF81C784))
    else lightColorScheme(primary = Green)

    MaterialTheme(colorScheme = colors) {
        val screen by vm.screen.collectAsState()
        val photos by vm.photos.collectAsState()
        var showSettings by rememberSaveable { mutableStateOf(!vm.settings.seenSettings) }

        BackHandler(enabled = showSettings || screen !is Screen.Home || photos.isNotEmpty()) {
            when {
                showSettings -> showSettings = false
                screen is Screen.Results -> vm.editSearch()
                else -> vm.reset()
            }
        }

        Scaffold(
            topBar = {
                TopAppBar(
                    title = { Text(if (showSettings) "Settings" else "Resell Comps") },
                    navigationIcon = {
                        if (showSettings || screen !is Screen.Home || photos.isNotEmpty()) {
                            IconButton(onClick = {
                                when {
                                    showSettings -> showSettings = false
                                    screen is Screen.Results -> vm.editSearch()
                                    else -> vm.reset()
                                }
                            }) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back") }
                        }
                    },
                    actions = {
                        if (!showSettings) {
                            IconButton(onClick = { showSettings = true }) {
                                Icon(Icons.Filled.Settings, contentDescription = "Settings")
                            }
                        }
                    },
                )
            },
        ) { padding ->
            val modifier = Modifier.padding(padding).fillMaxSize()
            when {
                showSettings -> SettingsScreen(vm, modifier) { showSettings = false }
                else -> when (val s = screen) {
                    Screen.Home -> HomeScreen(vm, photos, modifier, onNeedKeys = { showSettings = true })
                    is Screen.Busy -> BusyScreen(s.message, modifier)
                    is Screen.Identified -> IdentifiedScreen(
                        s.item, photos, modifier,
                        onSearch = vm::search, onRetake = vm::reset,
                        onAddPhoto = vm::backToPhotos,
                    )
                    is Screen.Results -> ResultsScreen(s.report, modifier, onEditSearch = vm::editSearch, onNewItem = vm::reset)
                    is Screen.Failed -> FailedScreen(s, modifier, onBack = { if (s.canRetrySearch) vm.editSearch() else vm.reset() })
                }
            }
        }
    }
}

@Composable
private fun BusyScreen(message: String, modifier: Modifier) {
    Column(modifier, horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
        CircularProgressIndicator()
        Spacer(Modifier.height(16.dp))
        Text(message, style = MaterialTheme.typography.bodyLarge)
        Spacer(Modifier.height(8.dp))
        Text(
            "Searching listings can take a minute.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun FailedScreen(s: Screen.Failed, modifier: Modifier, onBack: () -> Unit) {
    Column(modifier.padding(24.dp), verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally) {
        Text("Something went wrong", style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(8.dp))
        Text(s.message)
        Spacer(Modifier.height(16.dp))
        Button(onClick = onBack) { Text(if (s.canRetrySearch) "Back to search" else "Start over") }
    }
}

@Composable
private fun SettingsScreen(vm: MainViewModel, modifier: Modifier, onDone: () -> Unit) {
    val st = vm.settings
    var mode by remember { mutableStateOf(st.mode) }
    var ebayId by remember { mutableStateOf(st.ebayClientId) }
    var ebaySecret by remember { mutableStateOf(st.ebayClientSecret) }
    var readSold by remember { mutableStateOf(st.readSoldPage) }
    var visionKey by remember { mutableStateOf(st.googleVisionKey) }
    var anthropic by remember { mutableStateOf(st.anthropicKey) }
    var serp by remember { mutableStateOf(st.serpApiKey) }
    var model by remember { mutableStateOf(st.model) }

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("Mode", style = MaterialTheme.typography.titleMedium)
        ModeOption(
            selected = mode == Mode.FREE, onClick = { mode = Mode.FREE },
            title = "Free",
            body = "Reads barcodes and label text on your phone, finds eBay listings with eBay's free API, " +
                "and gives one-tap sold searches. No cost.",
        )
        ModeOption(
            selected = mode == Mode.PRO, onClick = { mode = Mode.PRO },
            title = "Pro (paid APIs)",
            body = "AI identifies anything from your photos, pulls eBay sold data and searches the whole web. " +
                "Costs a few cents per scan on your own API accounts.",
        )

        HorizontalDivider()
        if (mode == Mode.FREE) {
            Text("Free mode", style = MaterialTheme.typography.titleMedium)
            Text("Built-in visual search (like Google Lens)", style = MaterialTheme.typography.bodyLarge)
            Text(
                "Finds the same item on the web from your photo, names it, then searches sold and active " +
                    "listings automatically. Uses Google Cloud Vision: 1,000 free lookups a month (about 500 scans). " +
                    "Get a key at console.cloud.google.com → enable \"Cloud Vision API\" → Credentials → Create API key. " +
                    "Google asks for a card on file; set a budget alert to stay free.",
                style = MaterialTheme.typography.bodySmall,
            )
            OutlinedTextField(
                value = visionKey, onValueChange = { visionKey = it },
                label = { Text("Google Cloud Vision API key") }, singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                modifier = Modifier.fillMaxWidth(),
            )
            Text(
                "Optional but recommended: free eBay developer keys turn on active listings and eBay photo search. " +
                    "Sign up at developer.ebay.com, create a Production keyset, and paste the App ID (Client ID) " +
                    "and Cert ID (Client Secret).",
                style = MaterialTheme.typography.bodySmall,
            )
            OutlinedTextField(
                value = ebayId, onValueChange = { ebayId = it },
                label = { Text("eBay App ID (Client ID)") }, singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedTextField(
                value = ebaySecret, onValueChange = { ebaySecret = it },
                label = { Text("eBay Cert ID (Client Secret)") }, singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                modifier = Modifier.fillMaxWidth(),
            )
            Text("AI naming (optional, most accurate)", style = MaterialTheme.typography.bodyLarge)
            Text(
                "Add an Anthropic key and the app reads tags, logos and model numbers like you would and writes " +
                    "the search for you (e.g. \"vintage hanes beefy\", \"bose 700\"). Uses Claude's cheapest model, " +
                    "roughly a tenth of a cent per scan.",
                style = MaterialTheme.typography.bodySmall,
            )
            OutlinedTextField(
                value = anthropic, onValueChange = { anthropic = it },
                label = { Text("Anthropic API key") },
                supportingText = { Text("console.anthropic.com → API Keys") },
                visualTransformation = PasswordVisualTransformation(), singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text("Read eBay sold prices automatically", style = MaterialTheme.typography.bodyLarge)
                    Text(
                        "Experimental. Loads eBay's sold-search page on your phone and reads the prices. " +
                            "Can stop working whenever eBay changes its site, and eBay's terms don't allow automated reading.",
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                Switch(checked = readSold, onCheckedChange = { readSold = it })
            }
        } else {
            Text("Pro mode", style = MaterialTheme.typography.titleMedium)
            OutlinedTextField(
                value = anthropic, onValueChange = { anthropic = it },
                label = { Text("Anthropic API key (required)") },
                supportingText = { Text("Identifies the item and searches the web. console.anthropic.com") },
                visualTransformation = PasswordVisualTransformation(), singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedTextField(
                value = serp, onValueChange = { serp = it },
                label = { Text("SerpApi key (recommended)") },
                supportingText = { Text("eBay SOLD and active listings straight from eBay. serpapi.com") },
                visualTransformation = PasswordVisualTransformation(), singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
            OutlinedTextField(
                value = model, onValueChange = { model = it },
                label = { Text("Claude model") },
                supportingText = { Text("claude-sonnet-5-5 costs about half as much") },
                singleLine = true, modifier = Modifier.fillMaxWidth(),
            )
        }
        Text(
            "Keys are stored only on this phone and sent only to their own service.",
            style = MaterialTheme.typography.bodySmall,
        )
        Button(
            onClick = {
                st.mode = mode
                st.ebayClientId = ebayId
                st.ebayClientSecret = ebaySecret
                st.readSoldPage = readSold
                st.googleVisionKey = visionKey
                st.anthropicKey = anthropic
                st.serpApiKey = serp
                st.model = model
                st.seenSettings = true
                onDone()
            },
            enabled = mode == Mode.FREE || anthropic.isNotBlank(),
            modifier = Modifier.fillMaxWidth(),
        ) { Text("Save") }
        OutlinedButton(onClick = { st.seenSettings = true; onDone() }, modifier = Modifier.fillMaxWidth()) { Text("Cancel") }
    }
}

@Composable
private fun ModeOption(selected: Boolean, onClick: () -> Unit, title: String, body: String) {
    Row(
        Modifier.fillMaxWidth().selectable(selected = selected, onClick = onClick, role = Role.RadioButton),
        verticalAlignment = Alignment.Top,
    ) {
        RadioButton(selected = selected, onClick = null)
        Column(Modifier.padding(start = 8.dp)) {
            Text(title, style = MaterialTheme.typography.bodyLarge)
            Text(body, style = MaterialTheme.typography.bodySmall)
        }
    }
}
