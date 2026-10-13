# Resell Comps

An Android app for resellers: take a photo of an item (clothes, electronics, anything) and get
**real sold and active listings** for it, with a price only when listings back it up.

## How it works

1. **Photo → item.** Claude looks at the photo and names the item (brand, model, size, details) and
   suggests search terms. You can correct both before searching. Claude never gives a price here.
2. **Comps.** Two sources run in parallel:
   - **eBay sold + active** via [SerpApi](https://serpapi.com/ebay-search-api): titles, prices and
     links come straight from eBay's results.
   - **The wider web** (Poshmark, Mercari, Grailed, StockX, Swappa, …) via Claude's web search. A web
     listing is kept only if its link was actually returned by the search, so links can't be made up.
3. **Match check.** Claude compares each listing with your photo and drops ones that aren't the same
   item (other models, bundles, parts-only, boxes). Dropped listings stay visible in the *Excluded* tab.
4. **Price.** Plain math over the matched listings, no AI:
   - Sold comps found → **median sold price**, the range, and every sold listing as proof.
   - Only active listings → **no value is given**; asking prices are shown as reference only.
   - Nothing found → **"No comps found"** and no price.

Every listing card opens the real listing when tapped.

## Install

Each push builds an APK in GitHub Actions. Open the repo's **Releases** page on your phone, download
`ResellComps.apk` from the latest build, and allow installing from your browser when Android asks.

## Setup in the app

Open **Settings** (gear icon) and add:

- **Anthropic API key** (required) — from https://console.anthropic.com
- **SerpApi key** (recommended) — from https://serpapi.com. Without it, eBay sold data is skipped and
  only web search is used.

Keys are stored only in the app's private storage on your phone.

Costs: each lookup makes three Claude calls (one with web search) and two SerpApi searches.

## Project layout

- `core/` — plain Kotlin: Claude calls, SerpApi eBay client, link verification, price math, tests.
- `app/` — the Android app (Jetpack Compose).

Build locally with Android Studio, or `./gradlew :core:test :app:assembleRelease` with the Android SDK installed.
