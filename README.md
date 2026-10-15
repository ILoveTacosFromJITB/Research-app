# Resell Comps

An Android app for resellers: take a photo of an item and the app writes the eBay search for you
(the way you'd type it, e.g. "vintage hanes beefy" or "bose 700"), then one tap opens the real
listings. No typing needed.

## How to use it

1. **Take photos with the built-in camera**: the whole item, then a close-up of the tag, then any
   extras (up to 6; or add from the gallery). Tap **Scan** to identify them all together.
2. The free AI **recognizes the item** and writes the search. It also dates the item when the photos
   show enough (e.g. "1990s: single-stitch hem"), under *What the app saw*. Edit the search if needed.
3. Tap a button to see real listings:
   - **eBay Active**: what's listed now.
   - **eBay Sold**: what it actually sold for.
   - **Terapeak**: eBay's official sold-price research (Seller Hub → Research), free for sellers.
   - **Poshmark sold** / **Mercari sold**.

Other buttons: **Add tag photo** (re-identify with a close-up), **Search by text** on the home screen
(type a search, no photo), and **Open in Google Lens** as a backup.

## Setup: one free key

Open **Settings** (gear icon) and paste ONE free key; no card is needed and the app detects which
it is:

- **Groq**: https://console.groq.com → API Keys → Create API Key (starts with `gsk_`).
- **OpenRouter**: https://openrouter.ai → Keys → Create Key (starts with `sk-or-`).

Free tiers have per-minute and daily limits. When the per-minute limit is hit, the app waits the few
seconds the provider asks for and continues by itself; if the daily limit is used up it says so.
Without a key the app can only read barcodes and printed text, which is much less accurate.

The key is stored only on the phone and sent only to that service. The app is not connected to any
eBay account and never reads eBay's pages itself; its buttons just open eBay's own pages.

## Install

Each push builds an APK in GitHub Actions. On your phone, open the repo's **Releases** page, pick
**Latest build (this branch)**, download `ResellComps.apk`, and allow installing from your browser.

## Project layout

- `core/`: plain Kotlin: the free AI client (Groq/OpenRouter), the identify instructions, the
  no-key tag reader's search builder, and the eBay/marketplace links, with tests.
- `app/`: the Android app (Jetpack Compose, ML Kit for the no-key fallback).

Build locally with Android Studio, or `./gradlew :core:test :app:assembleRelease` with the Android SDK installed.
