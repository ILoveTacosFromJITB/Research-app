# Resell Comps

An Android app for resellers: take a photo of an item (clothes, electronics, anything) and get
**real sold and active listings** for it, with a price only when listings back it up.

## Quick use

Take a photo. The app identifies the item right away and writes the search the way a reseller
would type it (e.g. "vintage hanes beefy", "bose 700"). Tap **eBay Active** or **eBay Sold** to open
eBay's results for it, or Poshmark/Mercari sold. If it can't read enough, tap **Add tag photo** and
take a close-up of the tag or label. **Show prices in the app** runs the full comps lookup below.

With a free **Gemini** key in Settings (aistudio.google.com → Get API key, no card), the app
recognizes the item and writes the search at no cost; Gemini's free tier has daily limits and Google
may use free-tier requests to improve its products. Alternatively, with an Anthropic key, Claude's
cheapest model writes the search (most accurate, roughly a
tenth of a cent per scan); without one, the built-in visual search and on-phone tag reading do it.

## Two modes

Take up to **6 photos** of an item (front, back, brand tag, label, model number, barcode). Every
photo is used to identify it.

**Built-in visual search (like Google Lens).** With a Google Cloud Vision key, the app finds pages
showing the same item on the web (marketplace listings first), takes Google's best guess and the brand
logo, builds the search from the listings of the same photo, and then pulls sold and active comps
automatically. The matches are listed in a **Lens** tab. Cloud Vision gives 1,000 free lookups per
month; the app uses 2 per scan (first two photos). To get a key: https://console.cloud.google.com →
enable *Cloud Vision API* → *Credentials* → *Create API key*. Google requires a card on file; set a
budget alert to stay in the free tier.

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
