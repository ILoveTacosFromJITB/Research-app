package com.resellcomps.app.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
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
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
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
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.resellcomps.app.MainViewModel
import com.resellcomps.app.Screen

private val Green = Color(0xFF2E7D32)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun App(vm: MainViewModel) {
    val colors = if (isSystemInDarkTheme()) darkColorScheme(primary = Color(0xFF81C784))
    else lightColorScheme(primary = Green)

    MaterialTheme(colorScheme = colors) {
        val screen by vm.screen.collectAsState()
        val photo by vm.photo.collectAsState()
        var showSettings by rememberSaveable { mutableStateOf(!vm.hasKeys) }

        BackHandler(enabled = showSettings || screen !is Screen.Home || photo != null) {
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
                        if (showSettings || screen !is Screen.Home || photo != null) {
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
                    Screen.Home -> HomeScreen(vm, photo, modifier, onNeedKeys = { showSettings = true })
                    is Screen.Busy -> BusyScreen(s.message, modifier)
                    is Screen.Identified -> IdentifiedScreen(s.item, photo, modifier, onSearch = vm::search, onRetake = vm::reset)
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
    var anthropic by remember { mutableStateOf(vm.settings.anthropicKey) }
    var serp by remember { mutableStateOf(vm.settings.serpApiKey) }
    var model by remember { mutableStateOf(vm.settings.model) }

    Column(
        modifier.verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text(
            "Keys are stored only on this phone and sent only to their own service.",
            style = MaterialTheme.typography.bodyMedium,
        )
        OutlinedTextField(
            value = anthropic, onValueChange = { anthropic = it },
            label = { Text("Anthropic API key (required)") },
            supportingText = { Text("Identifies the item and searches the web. Get one at console.anthropic.com") },
            visualTransformation = PasswordVisualTransformation(), singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedTextField(
            value = serp, onValueChange = { serp = it },
            label = { Text("SerpApi key (recommended)") },
            supportingText = { Text("Pulls eBay SOLD and active listings straight from eBay. Free tier at serpapi.com") },
            visualTransformation = PasswordVisualTransformation(), singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        OutlinedTextField(
            value = model, onValueChange = { model = it },
            label = { Text("Claude model") }, singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        Button(
            onClick = {
                vm.settings.anthropicKey = anthropic
                vm.settings.serpApiKey = serp
                vm.settings.model = model
                onDone()
            },
            enabled = anthropic.isNotBlank(),
            modifier = Modifier.fillMaxWidth(),
        ) { Text("Save") }
        OutlinedButton(onClick = onDone, modifier = Modifier.fillMaxWidth()) { Text("Cancel") }
    }
}
