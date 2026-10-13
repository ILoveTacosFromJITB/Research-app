# Resell Comps

An Android app for resellers: take a photo of an item (clothes, electronics, anything) and get
**real sold and active listings** for it, with a price only when listings back it up.

## Two modes

Take up to **6 photos** of an item (front, back, brand tag, label, model number, barcode). Every
photo is used to identify it. If the name looks wrong, the **Google Lens** button sends the photo to
Google's visual web search so you can copy the exact name into the search box.

**Free (default): costs nothing to run**
- **Identify:** on the phone (Google ML Kit, no account needed) it reads **barcodes** and **text on
  labels/tags**, and with eBay keys it also matches the barcode on eBay and runs **eBay photo search**.
  If nothing readable is found, it asks you to type what the item is. It never guesses.
- **Optional "Identify with AI":** names the item from your photos with Claude's cheapest model,
  roughly a tenth of a cent per scan on your own Anthropic account. Much more accurate for clothes
  and anything without a barcode.
- **Active listings:** eBay's free official Browse API (needs free developer keys, see below).
- **Sold listings:** one-tap **sold-search buttons** for eBay, Poshmark and Mercari open the real sold
  results. Optionally, **"Read eBay sold prices automatically"** (experimental, off by default) loads
  eBay's sold page on the phone and reads the prices so the app can give a sold value. It can break
  when eBay changes its site, and eBay's terms don't allow automated reading.
- **Match check:** plain rules. Every model number and most words of your search must be in the
  title, and lots, bundles, parts-only and box-only listings are excluded (shown in *Excluded*).

**Pro: paid APIs, a few cents per scan**
- Claude identifies the item from the photo, searches the web for comps (links verified against real
  search results), and checks each listing against your photo. eBay sold data via SerpApi.

**Price rules (both modes):** plain math over matched listings only.
- Sold comps found → **median sold price**, range, and every sold listing as proof.
- Only active listings → **no value is given**; asking prices are shown as reference only.
- Nothing found → **"No comps found"** and no price. If sold listings weren't searched (free mode with
  the reader off) or eBay's page couldn't be read, the app says so instead of claiming zero sales.

Every listing card opens the real listing when tapped.

## Install

Each push builds an APK in GitHub Actions. Open the repo's **Releases** page on your phone, download
`ResellComps.apk` from the latest build, and allow installing from your browser when Android asks.

## Setup in the app

Open **Settings** (gear icon).

**Free mode, eBay keys (free, recommended):**
1. Sign up at https://developer.ebay.com and create an application keyset for **Production**.
2. eBay will ask about "Marketplace Account Deletion" notifications before turning the keys on. This
   app doesn't store eBay user data, so choose the option to apply for an **exemption**.
3. Paste the **App ID (Client ID)** and **Cert ID (Client Secret)** into Settings.

The free API allows about 5,000 calls a day; each scan uses 2–4.

**Pro mode:** an Anthropic API key (https://console.anthropic.com, pay-as-you-go) and optionally a
SerpApi key (https://serpapi.com). This is billed separately from any Claude subscription.

Keys are stored only in the app's private storage on your phone.

## Project layout

- `core/` — plain Kotlin: Claude calls, SerpApi eBay client, link verification, price math, tests.
- `app/` — the Android app (Jetpack Compose).

Build locally with Android Studio, or `./gradlew :core:test :app:assembleRelease` with the Android SDK installed.
