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
        /** Where the dates come from, when it's a specific reference. */
        val source: String? = null,
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
        "Fake and reproduction vintage tags exist; when a tag looks right but something feels off, compare it side by side with known examples (Defunkd keeps archives of fake and authentic tags).",
    )

    val BRANDS = listOf(
        Brand(
            "Hanes", listOf("hanes", "beefy"), "Blank tees & sweats",
            listOf(
                Era("1970s–80s", "Simple white tags with \"Hanes\" lettering; 50/50 and 100% cotton; Made in USA; single stitch."),
                Era("1975–1990s", "Beefy-T line (\"Beefy\" in big letters); 1980s–early 90s versions are the most collected."),
                Era("1989–1997", "Red/blue bar era: Fifty-Fifty, Beefy-T, Ultraweight, Heavyweight and 100% Cotton tags, sometimes double tags, Made in USA. Hanes had no strict tag format then, so design alone dates it loosely."),
                Era("2000s+", "ComfortSoft lines and printed tagless necks; Central America and Asia."),
            ),
            listOf("A Beefy-T with single stitch and Made in USA is a strong vintage sign."),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Fruit of the Loom", listOf("fruit of the loom", "fruit of loom"), "Blank tees & sweats",
            listOf(
                Era("1970s–1983", "Earlier tags with the fruit-cluster logo; often 50/50, Made in USA, single stitch."),
                Era("1984–1993", "Later tag styles, including \"Best\" and \"Heavy Cotton\"; Made in USA, then Central America."),
                Era("Mid-1990s–1998", "Last of the vintage-era tags before the modern ones."),
                Era("2004–2012", "Modern tags; not vintage."),
            ),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Screen Stars", listOf("screen stars", "screenstars"), "Blank tees",
            listOf(
                Era("Before 1980", "Fruit of the Loom's printable blanks had generic tags (RN 13765)."),
                Era("1980", "First branded Screen Stars tag."),
                Era("1981–1983", "The most famous version of the tag."),
                Era("c. 1983+", "Third-generation tag."),
                Era("Late 1980s–1994", "\"Screen Stars Best\" tags."),
            ),
            listOf(
                "RN 13765 is Fruit of the Loom's number, so it shows up on Screen Stars tags.",
                "Non-US origins can be genuine: Ireland, Canada, Mexico and El Salvador tags exist from the 80s–90s.",
                "Fake Screen Stars tags exist (e.g. Thailand-made reproductions) — compare closely.",
            ),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Jerzees", listOf("jerzees"), "Blank tees & sweats",
            listOf(
                Era("1985–1998", "Vintage-era Jerzees tags (a Russell brand); Made in USA, 50/50 sweats."),
                Era("2000s+", "Imported, newer logo styles."),
            ),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Tultex", listOf("tultex"), "Blank tees & sweats",
            listOf(Era("1980s–1999", "Popular for sports and souvenir sweatshirts; Made in USA.")),
            listOf("The company closed in 1999, so a Tultex tag means 1999 or earlier."),
        ),
        Brand(
            "Oneita", listOf("oneita"), "Blank tees",
            listOf(Era("1984–1999", "Power-T, Power/50, PowerPro and Colorwear tags (some 100% cotton, some 50/50; later ones Made in Mexico); Atlanta 1996 Olympics tees too.")),
            listOf("The company went under in the late 1990s, so Oneita tags are pre-2000."),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Anvil / Ched", listOf("anvil", "ched"), "Blank tees",
            listOf(
                Era("1976–1988", "Ched era: Ched Quality Knits, Ched by Anvil, Anvil Athletic and Value-T tags."),
                Era("1989–2007", "Anvil blue bar and red bar tags, sometimes double tags."),
            ),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Gildan", listOf("gildan"), "Blank tees",
            listOf(Era("Mostly 2000s+", "Canadian brand that grew huge in the US in the 2000s; most Gildan tags are not vintage.")),
            listOf("Gildan alone usually points to 2000s or later."),
        ),
        Brand(
            "Signal", listOf("signal"), "Blank tees",
            listOf(Era("1977–1994", "Signal tees: souvenir, school and tourist prints; often 50/50, Made in USA.")),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Velva Sheen", listOf("velva sheen", "velvasheen"), "Blank tees",
            listOf(
                Era("1970s–80s", "Souvenir and licensed tees (including Disney); some yellow tags with late-70s prints."),
                Era("Later tags", "Blue lettering on a white satin-like label."),
            ),
            listOf("Bought by Brazos Sportswear in 1994, which went bankrupt in 1999 — so Velva Sheen tags are pre-2000."),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Stedman / Hi Cru", listOf("stedman", "hi cru", "hi-cru", "high-cru"), "Blank tees",
            listOf(Era("1971–1997", "Hi Cru by Stedman, Golden Hi-Cru, Super Hi-Cru, Sport-T and Super 50 tags; Made in USA; later \"Stedman by Hanes\".")),
            listOf("Hanes bought Stedman in the mid-1990s and the name was gone after 2000."),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Sportswear", listOf("sportswear ii"), "Blank tees",
            listOf(
                Era("1968–1981", "Early Sportswear tags."),
                Era("1981–1990", "Sportswear and Sportswear II tags."),
            ),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Delta", listOf("delta"), "Blank tees",
            listOf(Era("1988–2014", "Delta tags, including \"fifty-fifty\" blends; early ones are vintage, later ones not.")),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Champion", listOf("champion"), "Athletic",
            listOf(
                Era("Before 1967", "Older Champion Knitwear-era tags (e.g. \"Running Man\")."),
                Era("1967–c. 1969", "Champion Products generic placeholder tag."),
                Era("1969–1970s", "Champion script logo on tags; \"blue bar\" (far more common) and red bar versions. Early ones have no care instructions on the back — just the name, RN and Made in USA."),
                Era("1980s", "Red/white/blue \"tri-color\" tags; Reverse Weave sweats Made in USA."),
                Era("Late 1990s–2000s", "\"Champion Authentic Athletic Apparel\" tags."),
            ),
            listOf("Reissues of Reverse Weave use old-style tags; modern care symbols give them away."),
            source = "Defunkd tag dating guide (defunkd.com)",
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
                Era("1936–1971", "Red tab says LEVI'S with a capital E (\"Big E\"); after 1971 it's a lowercase \"Levi's\" (\"small e\")."),
                Era("Mid-1960s and earlier", "\"501XX\" in big letters on the back patch (the XX was dropped around the mid-to-late 60s)."),
                Era("Before about 1983", "Selvedge (redline) denim on 501s — a clean edge with a red thread line inside the outseam."),
                Era("1970s–1992", "Care tag code: a month digit plus ONE year digit plus a factory number, so a \"7\" can mean 1977 or 1987 — use construction to pick the decade."),
                Era("1993+", "Care tag code is four digits, month then year: \"0594\" = May 1994, \"1196\" = November 1996."),
                Era("Until about 2003", "Made in USA (Levi's last US plants closed then)."),
            ),
            listOf(
                "The number stamped inside the top button is the factory, not the date; it should match the factory number on the care tag.",
                "Button 555 = the Valencia Street plant in San Francisco (open until 2002) — it appears on 80s and 90s pairs too, and on reissues.",
                "Levi's makes reissues (LVC) of Big E and 501XX styles, so check care tags and construction.",
            ),
            source = "Levi's dating guides (California Vintage Buyers, Flying Apple Vintage, JeansZa, Rope Dye) — dates vary slightly between sources",
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
            "Polo Ralph Lauren", listOf("polo ralph lauren", "ralph lauren", "polo by ralph lauren", "polo sport", "rrl", "polo"), "Designer",
            listOf(
                Era("1970s", "Small, simple labels focused on the name; the polo player logo starts appearing."),
                Era("1980s", "Larger labels with more information (fabric, care); many Made in USA; Hong Kong-made pieces are mostly 80s–90s."),
                Era("1990s–early 2000s", "Most common vintage finds; mainline labels on navy or green backgrounds; Polo Sport from 1993; logo-heavy pieces peak."),
                Era("2000s+", "Sri Lanka, Indonesia and China production becomes the norm."),
                Era("2010s+", "New label font; many labels printed instead of sewn."),
            ),
            listOf(
                "\"Made in USA\" on Polo is almost always vintage.",
                "Value comes from the line and logo (Polo Sport, Polo Bear, big logos, Stadium, RRL) as much as age.",
            ),
            source = "Vintage Polo Ralph Lauren tag guides (Vintage Rare USA, WEAR2AM, Watch and Gather, Vintage Clothing Guides) — label details vary between sources",
        ),
        Brand(
            "Tommy Hilfiger / Nautica", listOf("tommy", "hilfiger", "nautica"), "Designer",
            listOf(Era("1990s", "Peak era for logo pieces: Tommy flag logos and Tommy Jeans, Nautica Competition.")),
            listOf("For these, era comes more from logo style and the line name than from country of origin."),
        ),
        Brand(
            "Towncraft (JCPenney)", listOf("towncraft", "penney", "penneys", "jcpenney", "jc penney"), "Department store",
            listOf(
                Era("Before 1963", "\"PENNEY'S\" in capitals with an apostrophe."),
                Era("1963–1971", "\"Penneys\" with the \"atomic P\" logo."),
                Era("1971+", "\"JCPenney\" with the JC added back."),
                Era("1960s–1980s", "Towncraft tees, plaid and western shirts; Towncraft kept using very similar tags into the 1980s."),
            ),
            listOf(
                "A Towncraft tag alone doesn't pin the year — check the store name spelling, care tag, stitching and fabric too.",
                "Collectors compare Towncraft labels against the Vintage Fashion Guild label resource.",
            ),
            source = "Defunkd forum threads on Towncraft labels and JCPenney label histories — community knowledge, not official records",
        ),
        Brand(
            "Band / concert tee tags", listOf("brockum", "giant", "winterland", "tennessee river", "stanley desantis"), "Band tees",
            listOf(
                Era("1980s–1990s", "Brockum merch tags; tour dates printed on the back."),
                Era("1982–2008", "Winterland merch tags."),
                Era("1990s–2000s", "Giant tags are typical of 90s merch."),
            ),
            listOf("Tour dates and copyright years on the print help, but check the tag for reprints."),
            source = "Defunkd tag dating guide (defunkd.com)",
        ),
        Brand(
            "Disney", listOf("disney", "mickey"), "Character",
            listOf(
                Era("1972–early 80s", "Earlier Disney character tees; Velva Sheen and Sherry's made licensed Disney tees from the early 80s to mid-90s."),
                Era("1982–1993", "Disney Character Fashions tags."),
                Era("Late 1980s–1990s", "Mickey Inc., Mickey & Co. and Disney Designs tags."),
                Era("c. 1997–2000", "Mickey Unlimited tags."),
            ),
            listOf("Copyright line: \"Walt Disney Productions\" = before Feb 1986; \"Walt Disney Company\" or \"©Disney\" = after."),
            source = "Defunkd tag dating guide (defunkd.com)",
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
