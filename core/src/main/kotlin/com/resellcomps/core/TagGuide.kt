package com.resellcomps.core

/**
 * A built-in reference of common resale tags and the eras each tag style points to, written as
 * the rules of thumb resellers use. Ranges are typical, not exact: tag styles overlap, so the
 * AI weighs them with every other clue. Used two ways: the tag check gets the notes for the
 * brands it sees, and the Tag Guide screen lets the user browse and learn them.
 */
object TagGuide {

    data class Era(val years: String, val look: String)

    data class Brand(
        val name: String,
        /** Lowercase words that identify the brand in a name or tag reading. */
        val aliases: List<String>,
        val category: String,
        val eras: List<Era>,
        val tips: List<String> = emptyList(),
    )

    /** Facts that date any tag, whatever the brand. */
    val GENERAL_RULES = listOf(
        "Countries that no longer exist set an upper limit: \"Made in West Germany\" = before 1990; \"USSR\" = before 1992; \"Yugoslavia\" = before 1992; \"Czechoslovakia\" = before 1993; \"Burma\" = mostly before 1989.",
        "Written care instructions only, no laundry symbols = usually before about 1997 (US care symbols were allowed from 1997). No care tag at all = often before the 1970s.",
        "\"Made in USA\" on basic tees and sweatshirts is common through the mid-1990s and rare after about 2000. Mexico, Honduras, El Salvador and the Dominican Republic take over from the mid-1990s; China, Bangladesh and Vietnam dominate in the 2000s.",
        "Single-stitch sleeves and hems on T-shirts = mostly before about 1995–1997. Double stitch after. Reproductions exist, so check the tag too.",
        "50/50 cotton-polyester tees = typical of the late 1970s to early 1990s.",
        "Union labels (ILGWU, ACWA) = mostly before 1995, when the ILGWU merged into UNITE.",
        "WPL numbers = before 1959. RN numbers identify the company, not the year.",
        "Printed (tagless) neck labels = mostly early 2000s and later.",
        "A copyright date on a graphic is the earliest possible year, not the year it was made.",
        "Multi-language care tags (English/French/Spanish) = mostly mid-1990s and later.",
    )

    val BRANDS = listOf(
        Brand(
            "Hanes", listOf("hanes", "beefy"), "Blank tees & sweats",
            listOf(
                Era("1970s–80s", "Simple white tags with \"Hanes\" lettering; 50/50 and 100% cotton; Made in USA; single stitch."),
                Era("1975–1990s", "Beefy-T line (\"Beefy\" in big letters); 1980s–early 90s versions are the most collected; Made in USA."),
                Era("1990s", "\"Heavyweight\" and \"Her Way\" tags; Made in USA giving way to Honduras/El Salvador late in the decade."),
                Era("2000s+", "ComfortSoft lines and printed tagless necks; Central America and Asia."),
            ),
            listOf("A Beefy-T with single stitch and Made in USA is a strong vintage sign."),
        ),
        Brand(
            "Fruit of the Loom", listOf("fruit of the loom", "fruit of loom"), "Blank tees & sweats",
            listOf(
                Era("1970s–80s", "Tags with the fruit-cluster logo, often 50/50, Made in USA, single stitch."),
                Era("Late 1980s–1990s", "\"Best\" and \"Heavy Cotton\" tags; Made in USA, later Honduras/El Salvador/Mexico."),
                Era("2000s+", "Tagless prints and Central American / Asian production."),
            ),
        ),
        Brand(
            "Screen Stars", listOf("screen stars", "screenstars"), "Blank tees",
            listOf(Era("1980s–early 1990s", "Staple for printed and concert tees; \"Screen Stars Best\" 50/50 tags are classic mid-to-late 80s; Made in USA, single stitch.")),
            listOf("Seeing Screen Stars at all leans 1980s."),
        ),
        Brand(
            "Jerzees", listOf("jerzees"), "Blank tees & sweats",
            listOf(
                Era("1980s–1990s", "Russell-made brand launched in the early 1980s; Made in USA tags, 50/50 sweats."),
                Era("2000s+", "Imported, newer logo styles."),
            ),
        ),
        Brand(
            "Tultex", listOf("tultex"), "Blank tees & sweats",
            listOf(Era("1980s–1999", "Popular for sports and souvenir sweatshirts; Made in USA.")),
            listOf("The company closed in 1999, so a Tultex tag means 1999 or earlier."),
        ),
        Brand(
            "Oneita", listOf("oneita"), "Blank tees",
            listOf(Era("1980s–late 1990s", "Common on 80s–90s printed and band tees; often Made in USA.")),
            listOf("The company went under in the late 1990s, so Oneita tags are pre-2000."),
        ),
        Brand(
            "Anvil", listOf("anvil"), "Blank tees",
            listOf(
                Era("1980s–1990s", "Made in USA tags on printed tees and souvenir shirts."),
                Era("2000s", "Imported; later absorbed by Gildan."),
            ),
        ),
        Brand(
            "Gildan", listOf("gildan"), "Blank tees",
            listOf(Era("Mostly 2000s+", "Canadian brand that grew huge in the US in the 2000s; most Gildan tags are not vintage.")),
            listOf("Gildan alone usually points to 2000s or later."),
        ),
        Brand(
            "Signal / Velva Sheen", listOf("signal", "velva sheen", "velvasheen"), "Blank tees",
            listOf(Era("1970s–1980s", "Souvenir, school and tourist tees; often 50/50, Made in USA, single stitch.")),
        ),
        Brand(
            "Champion", listOf("champion"), "Athletic",
            listOf(
                Era("1950s–60s", "Early \"Running Man\" tags."),
                Era("Late 1960s–70s", "\"Blue Bar\" tags (Champion Products, Rochester NY)."),
                Era("1980s", "Red/white/blue \"tri-color\" (trico) tags; Reverse Weave sweats Made in USA."),
                Era("1990s", "Tags with the \"C\" logo; Reverse Weave still often Made in USA early in the decade."),
                Era("2000s+", "Imported; modern reissues copy old tags, so check country and care tag."),
            ),
            listOf("Reissues of Reverse Weave use old-style tags; modern care symbols give them away."),
        ),
        Brand(
            "Nike", listOf("nike"), "Athletic",
            listOf(
                Era("1970s–early 80s", "Orange swoosh tags (pre-1978 items may say Blue Ribbon Sports)."),
                Era("1980s", "Navy/blue tags with white lettering."),
                Era("Late 1980s–1990s", "Grey tags and white tags with a black swoosh; many 90s pieces Made in USA or Asia."),
                Era("2000s+", "Modern white/black tags with Dri-FIT and multi-language care."),
            ),
        ),
        Brand(
            "Adidas", listOf("adidas"), "Athletic",
            listOf(
                Era("1970s–1980s", "Trefoil logo; many tags say Made in West Germany or France (Ventex)."),
                Era("1991–late 1990s", "\"Performance\" three-bar logo and the EQT (Equipment) line."),
                Era("2000s+", "ClimaLite / ClimaCool tags; Asian production."),
            ),
            listOf("\"Made in West Germany\" means before 1990."),
        ),
        Brand(
            "Russell Athletic", listOf("russell"), "Athletic / sports",
            listOf(
                Era("1970s–1980s", "Gold-and-white tags; Made in USA."),
                Era("1990s–early 2000s", "Russell logo tags; \"Diamond Collection\" MLB jerseys."),
            ),
            listOf("Russell Diamond Collection MLB jerseys are mostly 1990s to early 2000s."),
        ),
        Brand(
            "Starter", listOf("starter"), "Sports",
            listOf(Era("1980s–1990s", "Star logo tags on satin and pullover jackets; peak late 80s–90s.")),
            listOf("The original company went bankrupt in 1999, so classic Starter tags are pre-2000."),
        ),
        Brand(
            "Majestic", listOf("majestic"), "Sports",
            listOf(
                Era("1990s", "Smaller supplier of MLB apparel."),
                Era("2005–2019", "Official MLB on-field jersey maker; Cool Base jerseys."),
            ),
            listOf("Majestic MLB jerseys are usually 2005–2019, so not vintage yet."),
        ),
        Brand(
            "Salem / Nutmeg / Logo 7 / Pro Player", listOf("salem", "nutmeg", "logo 7", "logo7", "pro player", "chalk line", "apex one", "logo athletic"), "Sports",
            listOf(Era("Late 1980s–1990s", "The big licensed sports-apparel brands of the era; most of these brands faded or were sold around 2000.")),
        ),
        Brand(
            "Levi's", listOf("levi", "levis", "levi's"), "Denim",
            listOf(
                Era("Before 1971", "\"Big E\" — LEVI'S in all capitals on the red tab."),
                Era("1971+", "\"Small e\" — Levi's on the red tab; orange tabs are common in the 1970s."),
                Era("Before mid-1980s", "Selvedge (redline) denim on 501s."),
                Era("Until about 2003", "Made in USA (the last US plants closed then)."),
            ),
            listOf("Big E means pre-1971; Levi's makes reissues, so check care tags and construction."),
        ),
        Brand(
            "Lee / Wrangler", listOf("lee", "wrangler"), "Denim",
            listOf(
                Era("1970s–80s", "\"Union Made\" and Made in USA tags; Wrangler tags may say Blue Bell (its parent until 1986)."),
                Era("1990s", "Made in USA giving way to Mexico."),
            ),
        ),
        Brand(
            "Patagonia", listOf("patagonia"), "Outdoor",
            listOf(
                Era("1970s", "Early white labels."),
                Era("1980s", "Big labels with the mountain logo; Made in USA."),
                Era("1990s+", "Smaller labels; production moves overseas."),
            ),
        ),
        Brand(
            "The North Face", listOf("north face"), "Outdoor",
            listOf(
                Era("1960s–70s", "Brown labels."),
                Era("1980s–90s", "Blue/navy and later white labels; Made in USA on many 80s pieces."),
            ),
        ),
        Brand(
            "Polo Ralph Lauren / Tommy Hilfiger / Nautica", listOf("polo", "ralph lauren", "tommy", "hilfiger", "nautica"), "Designer",
            listOf(Era("1990s", "Peak era for logo pieces: Polo Sport (from 1993), Tommy flag logos and Tommy Jeans, Nautica Competition.")),
            listOf("For these, era comes more from logo style and the line name than from country of origin."),
        ),
        Brand(
            "Band / concert tee tags", listOf("brockum", "giant", "winterland", "tennessee river", "stanley desantis"), "Band tees",
            listOf(
                Era("1980s–1990s", "Brockum and Winterland merch tags; tour dates printed on the back."),
                Era("1990s–2000s", "Giant tags are typical of 90s merch."),
            ),
            listOf("Tour dates and copyright years on the print help, but check the tag for reprints."),
        ),
        Brand(
            "Disney", listOf("disney", "mickey"), "Character",
            listOf(
                Era("1970s–80s", "Character Fashions and Velva Sheen tags."),
                Era("Late 1980s–1990s", "Mickey Inc., Mickey & Co. and Disney Designs tags."),
            ),
        ),
    )

    /** Brands whose aliases appear in the item's name, brand or what was read on the tag. */
    fun matching(vararg texts: String?): List<Brand> {
        val hay = texts.filterNotNull().joinToString(" ").lowercase()
        if (hay.isBlank()) return emptyList()
        return BRANDS.filter { b -> b.aliases.any { alias -> Regex("\\b${Regex.escape(alias)}\\b").containsMatchIn(hay) } }
    }

    fun matching(item: ItemIdentification): List<Brand> =
        matching(item.brand, item.name, item.tagReading, item.searchQuery)

    /** Reference notes for the matched brands, to hand to the AI's tag check. */
    fun notesFor(brands: List<Brand>): String = brands.joinToString("\n") { b ->
        "${b.name} tags: " + b.eras.joinToString("; ") { "${it.years}: ${it.look}" } +
            (if (b.tips.isNotEmpty()) " Tip: " + b.tips.joinToString(" ") else "")
    }
}
