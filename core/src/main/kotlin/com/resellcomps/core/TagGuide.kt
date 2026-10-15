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
                Era("1970s", "White tag with an orange stripe; Beefy-T introduced 1975; 50/50 and 100% cotton; Made in USA; single stitch."),
                Era("1980s", "Red/blue \"Fifty-Fifty\" tags."),
                Era("c. 1985–1995", "Block-letter red/blue Beefy-T tag with a separate size flag — the most collected Beefy."),
                Era("1989–1997", "Red/blue bar era: Fifty-Fifty, Beefy-T, Ultraweight, Heavyweight and 100% Cotton tags, sometimes double tags, Made in USA. Hanes had no strict tag format then, so design alone dates it loosely."),
                Era("Late 1990s–early 2000s", "Black tag with gold or silver lettering."),
                Era("2000s+", "ComfortSoft lines and printed tagless necks; Central America and Asia."),
            ),
            listOf(
                "A Beefy-T with single stitch and Made in USA is a strong vintage sign.",
                "Winterland band tees from 1988–91 are mostly printed on Hanes blanks.",
            ),
            source = "Defunkd tag dating guide (defunkd.com), KeepItClassicLV",
        ),
        Brand(
            "Fruit of the Loom", listOf("fruit of the loom", "fruit of loom"), "Blank tees & sweats",
            listOf(
                Era("1970s–1983", "Earlier tags with the fruit-cluster logo; often 50/50, Made in USA, single stitch."),
                Era("1984–1993", "Later tag styles, including \"Best\", \"Lofteez\" and \"Heavy Cotton\" (RN 13765); Made in USA, then Central America."),
                Era("Mid-1990s–1998", "Last of the vintage-era tags; often two neck tags from the 90s on."),
                Era("2004–2012", "Modern tags; not vintage."),
            ),
            listOf("\"Best\" tags ran a long time (seen on dated shirts from 1989 to 2004), so check stitching and origin too."),
            source = "Defunkd tag dating guide (defunkd.com), NeonVTG",
        ),
        Brand(
            "Screen Stars", listOf("screen stars", "screenstars"), "Blank tees",
            listOf(
                Era("Before 1980", "Fruit of the Loom's printable blanks had generic tags (RN 13765)."),
                Era("1980", "First branded Screen Stars tag: blue/white/orange, likely 50/50 only."),
                Era("1981–1983", "The \"Funky A\" tag — the most famous version — then a plain-A variant."),
                Era("c. 1983+", "Third-generation tag."),
                Era("1986/87–mid 1990s", "\"Screen Stars Best\" tags (seen on dated shirts 1990–93)."),
            ),
            listOf(
                "RN 13765 is Fruit of the Loom's number, so it shows up on Screen Stars tags.",
                "Non-US origins can be genuine: Ireland, Canada, Mexico and El Salvador tags exist from the 80s–90s.",
                "Fake Screen Stars tags exist (e.g. Thailand-made reproductions) — compare closely.",
            ),
            source = "Defunkd tag dating guide (defunkd.com), NeonVTG",
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
            listOf(
                Era("Mid–late 1980s", "Red/white/blue tag with a blue block and white \"Tultex\" text; Made in USA."),
                Era("c. 1989–1992", "Transitional tag."),
                Era("1992–1994", "Squarer tag with a new logo in a black block."),
                Era("Mid-1990s", "Smaller black block, more text on the tag."),
                Era("1993–1996", "Giant/Tultex co-branded band tee tags."),
                Era("2004+", "Relaunched as a modern blank brand — not vintage."),
            ),
            listOf(
                "The original Virginia company filed for bankruptcy in December 1999, so classic Tultex tags are 1999 or earlier.",
                "Prints usually went on within 2–3 years of the blank being made.",
            ),
            source = "NeonVTG, Sonny Decru, Defunkd — collector estimates",
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
                Era("Before 1976", "Ched tag (Anvil grew out of BVD)."),
                Era("1976–1985", "Black-and-gold 100% cotton Anvil tag; also Ched by Anvil, Anvil Athletic and Value-T."),
                Era("1988–1990", "Blue-bar tag."),
                Era("1991–1997", "Woven blue-and-red tag."),
                Era("1992–1999", "Red-bar tag."),
                Era("2004+", "Double-tag styles; Gildan bought Anvil in 2012."),
            ),
            listOf(
                "An Anvil tag on a band tee sold as 1980s merch is a red flag — Anvil band tees are mostly late 90s.",
                "The plain \"Made in USA\" Anvil tag was used for decades, so it dates poorly alone.",
            ),
            source = "Defunkd tag dating guide (defunkd.com), NeonVTG",
        ),
        Brand(
            "Gildan", listOf("gildan"), "Blank tees",
            listOf(Era("Mostly 2000s+", "Canadian brand that grew huge in the US in the 2000s; most Gildan tags are not vintage.")),
            listOf("Gildan alone usually points to 2000s or later."),
        ),
        Brand(
            "Signal", listOf("signal", "signal sports", "signal artwear"), "Blank tees",
            listOf(
                Era("1977–1994", "Signal tees: souvenir, school, tourist and band prints (one of the big \"rock jersey\" makers); often 50/50, Made in USA."),
                Era("1990s", "Signal Sports and Signal Artwear variant tags; merged with Shirt Shed in 1991."),
            ),
            source = "Defunkd tag dating guide (defunkd.com), NeonVTG",
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
            "Champion", listOf("champion", "reverse weave"), "Athletic",
            listOf(
                Era("Before 1967", "Champion Knitwear era; the rare \"Running Man\" tag (1950s–60s)."),
                Era("1967–c. 1969", "Champion Products generic placeholder tag."),
                Era("1969–late 1970s/early 80s", "Blue bar tag with the script logo (red bar version is rarer). Early ones have no care instructions — just the name, RN and Made in USA."),
                Era("1980s–mid 1990s", "Red/white/blue \"tri-color\" C tag; Reverse Weave sweats Made in USA. 1981–90 tees may have a plainer blue script tag with care symbols."),
                Era("Early–mid 1990s", "Red bar transition tag; RN 26094 and larger care tags appear mid-90s."),
                Era("Late 1990s–2000s", "\"Champion Authentic Athletic Apparel\" tags; Made in Mexico, Honduras or El Salvador."),
            ),
            listOf(
                "Blue bar + Made in USA + heavy weight is the top value tier.",
                "Reissues of Reverse Weave use old-style tags; modern care symbols give them away.",
            ),
            source = "Defunkd tag dating guide (defunkd.com), Vintage Rare USA, Underpriced — end dates vary between sources",
        ),
        Brand(
            "Nike", listOf("nike"), "Athletic",
            listOf(
                Era("Before 1978", "Blue Ribbon Sports tags."),
                Era("c. 1978–1982", "\"Pinwheel\" logo tag."),
                Era("c. 1978–1983", "Orange swoosh tag."),
                Era("c. 1984–1987/88", "Blue tag (marathon tees, early Jordan)."),
                Era("c. 1987/89–1993/94", "Grey tag, printed or woven; red logo starts replacing blue/orange."),
                Era("c. 1994–1999", "White tag — the most common vintage Nike tag; red/black tag about 1997–99."),
                Era("Late 1990s–2000s", "Silver tag (sources split: late 90s vs early–mid 2000s); then Dri-FIT and multi-language care."),
            ),
            listOf(
                "Made in USA, Ireland or UK almost always means pre-2000.",
                "Don't price a white tag as early 90s.",
                "The small inner label's style code: its first characters give the year and season.",
            ),
            source = "Thriftcon, TRUSS Archive, Wicked Threadz, Vintage Rare USA — eras overlap a year or two",
        ),
        Brand(
            "Adidas", listOf("adidas"), "Athletic",
            listOf(
                Era("1972–1990s", "Trefoil logo tags; many say Made in West Germany, France (Ventex), Canada or Korea."),
                Era("1991–late 1990s", "\"Equipment\" (EQT) logo: three bars over \"adidas EQUIPMENT\"."),
                Era("1996–1997", "Badge of Sport; the three-bar \"mountain\" Performance logo becomes the main logo in 1997."),
                Era("2000+", "Trefoil kept only for the Originals line; ClimaLite / ClimaCool tags; Asian production."),
            ),
            listOf(
                "\"Made in West Germany\" means before 1990.",
                "A trefoil alone doesn't mean vintage — Originals has used it since 2000.",
            ),
            source = "adidas official history, Sneaker Freaker, Vintage Rare USA",
        ),
        Brand(
            "Reebok", listOf("reebok"), "Athletic",
            listOf(
                Era("1977–early 1990s", "Union Jack flag logo beside a deep-blue wordmark; 80s font has the R's leg dropping below the line."),
                Era("Early–mid 1990s", "Rounder e's; the \"vector\" swoosh-like logo appears (dated 1986, 1992 or 1993 depending on source)."),
                Era("Mid–late 1990s", "Less blocky font, R's leg level with the text; vector logo common."),
            ),
            listOf("The Union Jack came back on throwback lines after 2002 — date by font and emblem together."),
            source = "Fonts In Use, Creative Bloq, Vintage Clothing Guides, Defunkd forum",
        ),
        Brand(
            "Stüssy", listOf("stussy", "stüssy"), "Streetwear",
            listOf(
                Era("1980s", "Black tag: white embroidered signature, size and MADE IN USA on one label; no ® at the top right = even earlier."),
                Era("Early–mid 1990s", "White tag with black embroidery."),
                Era("Mid–late 1990s", "Navy tag with white embroidery (some say navy started late 80s)."),
                Era("2000s+", "Logo tag with the size as a big red letter; often Made in Mexico, later overseas."),
            ),
            listOf(
                "The signature logo has been used since 1984, so the logo alone won't date it — the tag color does.",
                "Made outside the USA or Australia = likely late 90s or later; large shiny care tags start late 90s.",
            ),
            source = "Vintage Clothing Guides, Archive by X, legitcheck.app",
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
            listOf(
                Era("1971–1987", "Woven main label, mostly without ®."),
                Era("1988–1993", "Script \"Starter\" with the star, ® by about 1990; Made in USA or Korea."),
                Era("1993–1996", "Pro Line era (premium NFL) — the peak of satin and pullover jackets."),
                Era("2007+", "Brand revived under new owners; reissues are real Starter but not vintage."),
            ),
            listOf(
                "The original company lost its NFL license in 1997 and went bankrupt in 1999, so classic Starter tags are pre-2000.",
                "RN 71271 = Starter Corp 1988–99. Made in Pakistan or Taiwan with no RN = likely a 90s bootleg.",
                "Check the team logo matches the tag era (e.g. no Panthers or Jaguars before 1995).",
            ),
            source = "Keep It Classic, Vintage Rare USA, Gametime Vintage",
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
            "Lee", listOf("lee", "lee riders"), "Denim",
            listOf(
                Era("1940s–1950s", "\"Lee Riders\" name; red on the Riders waistband tag until the mid-50s; Lazy-S back pocket stitch (from about 1944)."),
                Era("1960s", "Black-and-gold tag; an \"R\" (registered) mark added."),
                Era("c. 1970+", "\"MR\" next to the Lee name (transitional R + MR tags around 1970)."),
                Era("1970s–80s", "Union Made and Made in USA tags (sources disagree when Union Made ended)."),
                Era("1990s", "Made in USA giving way to Mexico."),
            ),
            listOf(
                "MR on the tag = about 1970 or later.",
                "Older tags show an RN number, no UPC barcode.",
                "Lee's modern \"Union Made\" line can be mistaken for vintage.",
            ),
            source = "California Vintage Buyers, Heddels, Fedora Lounge, Vintage Fashion Guild",
        ),
        Brand(
            "Wrangler", listOf("wrangler", "blue bell"), "Denim",
            listOf(
                Era("1947–mid 1960s", "Cream tags with rope-script \"Wrangler\" and the Blue Bell logo above it; \"SANFORIZED\" on the fly label."),
                Era("c. 1966–1970", "Blue Bell dropped from the label (Canadian labels kept it longer); 13MWZ from 1966."),
                Era("1970s–80s", "Black tag with yellow/gold rope script; Made in USA; boot cut and flares."),
                Era("1990s", "Made in USA giving way to Mexico."),
            ),
            listOf(
                "Blue Bell owned Wrangler until VF bought it in 1986.",
                "Factories used up old labels after changes, and Blue Bell 13MWZ reproductions exist.",
            ),
            source = "denimbro, mydenimblog, Fedora Lounge, Vintage Clothing Guides",
        ),
        Brand(
            "Carhartt", listOf("carhartt"), "Workwear",
            listOf(
                Era("c. 1900–mid 1960s", "Heart logo labels with \"Union Made\"; \"Master Cloth\" wording on later versions."),
                Era("1966/67+", "Modern \"C\" logo — garment labels barely changed after this."),
                Era("1970s", "White cloth neck tag with the logo, no model number, and a printed round union stamp."),
                Era("1980s–1990s", "Blocky logo, clear Made in USA, often a small red-and-blue round union label."),
                Era("Late 1990s+", "Vinyl-type tags with style codes; much production offshore around 2000."),
            ),
            listOf(
                "Care tag date code is month then year: 598 = May 1998, 0301 = March 2001 (often faded or cut).",
                "ILGWU union label = before 1995.",
                "Fakes and reworked pieces are common; Made in USA alone isn't proof of age.",
            ),
            source = "Thriftwagon, Nostos, Vintage Rare USA, Vintage Clothing Guides — sources disagree on early dates",
        ),
        Brand(
            "Dickies", listOf("dickies", "williamson-dickie"), "Workwear",
            listOf(
                Era("1940s–2005", "Logo with \"Guaranteed\" under \"Dickies\" — used for decades, so it dates poorly alone."),
                Era("1967+", "874 work pant (65/35 poly-cotton)."),
                Era("1980s", "Simple rectangular white-and-blue tags; Made in USA."),
                Era("1990s+", "Mexico and Central America more common."),
                Era("2005+", "Red oval logo."),
            ),
            listOf(
                "Little Dickies-specific dating exists — combine clues: no care label = before 1971; metal Talon zipper leans pre-1980.",
                "RN numbers: 5 digits roughly 1960s–70s, 6 digits 1980s or later (rough rule).",
            ),
            source = "Heddels, Vintage Clothing Guides, Vintage Fashion Guild — limited sources",
        ),
        Brand(
            "Patagonia", listOf("patagonia", "chouinard"), "Outdoor",
            listOf(
                Era("Early 1970s", "Clothing sold under the Chouinard Equipment name (\"big label Chouinard\")."),
                Era("1976–early 1980s", "First Patagonia labels (spring 1976): simple mountain-range logo, sometimes \"Ventura, CA\"."),
                Era("1980s", "The big mountain-logo label, the most collected; many Made in USA."),
                Era("1988+", "A style/product code appears on the inside label — look it up to get the exact season and year."),
                Era("1995+", "Labels add a 3–4 letter color code next to the style code."),
            ),
            listOf(
                "No color code on the label = the older one: a code ending S9 could be 1989 or 1999, and the color code settles it.",
                "Made in USA leans older, but it isn't proof by itself.",
            ),
            source = "Truss Archive, A Continuous Lean, Highsnobiety — collector sources",
        ),
        Brand(
            "The North Face", listOf("north face", "tnf"), "Outdoor",
            listOf(
                Era("1960s", "Large rectangular fabric tags stating US manufacture."),
                Era("1970s–early/mid 1980s", "Brown label: brown Half Dome logo on white (logo from about 1971); \"Made in USA\" wording from the late 1970s."),
                Era("Early 1980s+", "White logo on a navy label (white on black on some earlier pieces — the colors overlap)."),
                Era("1985+", "Production dates printed on the wash/care tag."),
            ),
            listOf(
                "Brown label + Gore-Tex = about 1977 to the early 80s.",
                "Before 1985 date it by the label; after 1985 look for the date on the care tag.",
                "Today's \"White Label\" is a separate Asia-only line — not a vintage era.",
            ),
            source = "Basecamp Vintage, Vintage Clothing Guides — collector sources",
        ),
        Brand(
            "Pendleton", listOf("pendleton"), "Outdoor / wool",
            listOf(
                Era("1930s–1940s", "Blue label with gold lettering and no size on it (sometimes a small white size tag below)."),
                Era("1950s", "Size moves onto the label, lower-right corner."),
                Era("1950s–1990s", "Ornate script \"Pendleton\" in a box with \"WARRANTED TO BE A\" and \"TRADE MARK REG.\""),
                Era("1990s–2000s+", "Yellow border dropped; plain straight name with \"Warranted to be a Pendleton\" and \"Since 1863\"."),
            ),
            listOf(
                "No Woolmark symbol often points to before 1964, but it's soft evidence — collectors disagree on single labels.",
                "Size on a separate tag instead of on the label leans 1940s or earlier.",
            ),
            source = "Vintage Clothing Guides, Vintage Fashion Guild forums — approximate ranges",
        ),
        Brand(
            "L.L. Bean", listOf("l.l. bean", "l.l.bean", "ll bean", "llbean", "l l bean"), "Outdoor",
            listOf(
                Era("1940s–1950s", "Gold script on black."),
                Era("1950s–1970s (maybe into the 80s)", "Green script on white."),
                Era("1980s–1990s", "Block letters \"L.L.Bean Freeport, Maine\" (sources split on 80s vs 90s)."),
                Era("2000s+", "\"Freeport, Maine\" dropped from the tag."),
            ),
            listOf(
                "\"Freeport, Maine\" on the tag usually means 1970s–1990s (also on 90s licensed tees, e.g. made by Champion).",
                "Logos overlapped, so give a range, not a single year.",
            ),
            source = "L.L.Bean archivist quoted on collector forums, Vintage Fashion Guild, Vintage Clothing Guides",
        ),
        Brand(
            "Woolrich", listOf("woolrich"), "Outdoor / wool",
            listOf(
                Era("c. 1933–1936", "Green-border label with a sheep in profile."),
                Era("c. 1937–1940", "Green-border \"ALL WOOL\" label."),
                Era("1943–1945", "Wartime \"Emergency Garment Label\"; lighter, part-recycled wool."),
                Era("1949–1965", "Label with the ® symbol (trademark registered 1949)."),
                Era("1965–1990s", "The next label design (some say from the late 60s)."),
            ),
            listOf(
                "A Woolblend mark means 1971 or later.",
                "\"EU\" next to Made in U.S.A. is French for United States, not the European Union.",
                "Old hardware helps: grommeted zippers about 1933–38, Talon zippers about 1939–42.",
            ),
            source = "Fedora Lounge and Vintage Fashion Guild forums — collector knowledge",
        ),
        Brand(
            "Eddie Bauer", listOf("eddie bauer"), "Outdoor",
            listOf(
                Era("1930s–1960s", "Black \"Sun\" label with red \"Bauer\"; \"Blizzard Proof\" wording points to the 1960s."),
                Era("Mid-1960s–2023", "Gold cursive script logo — covers most vintage finds."),
                Era("2023+", "New goose logo with block letters and \"1920\" — not vintage."),
            ),
            listOf(
                "Ownership helps narrow it: General Mills from 1971, Spiegel from 1988.",
                "Goose logo = 2023 or later.",
            ),
            source = "Vintage Fashion Guild, Permanent Style, Fast Company, Heddels",
        ),
        Brand(
            "Polo Ralph Lauren", listOf("polo ralph lauren", "ralph lauren", "polo by ralph lauren", "polo sport", "rrl", "double rl", "polo country", "chaps", "polo"), "Designer",
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
                "Lines: Polo Country late 1980s–early 90s (some pieces from 1984; RRL's predecessor); Polo Sport 1992+ (a few 1990–91); RRL 1993+; Chaps since 1978.",
                "\"Chaps Ralph Lauren\" crest labels are mostly 1990s–early 2000s; a plain \"Chaps\" label is later and cheaper — often mislisted as Ralph Lauren.",
                "RRL and Polo Country bring the most money.",
            ),
            source = "Vintage Polo Ralph Lauren tag guides (Vintage Rare USA, WEAR2AM, Watch and Gather, Vintage Clothing Guides) — label details vary between sources",
        ),
        Brand(
            "Tommy Hilfiger", listOf("tommy hilfiger", "hilfiger", "tommy jeans"), "Designer",
            listOf(
                Era("1985+", "Brand founded; the flag neck tag was redesigned many times."),
                Era("1990s", "Peak logo era: big flag logos, Tommy Jeans; Hilfiger Athletic slanted logo (dropped early 2000s)."),
                Era("Late 1990s", "Crest logo: a lion holding a sword with the founding year in Roman numerals."),
            ),
            listOf("Athletic or crest logos usually mean the 1990s; some care tags print a year."),
            source = "Vintage Clothing Guides, 1stDibs — no dated chart of flag labels exists",
        ),
        Brand(
            "Nautica", listOf("nautica"), "Designer",
            listOf(Era("1990s", "Peak era for logo pieces: Nautica Competition, big sailing-flag logos.")),
            listOf("Era comes more from logo style and the line name than from country of origin."),
        ),
        Brand(
            "Calvin Klein", listOf("calvin klein", "ck jeans", "ckj"), "Designer",
            listOf(
                Era("1977/78–1983", "Calvin Klein Jeans made under license by Puritan Fashions."),
                Era("1983+", "Klein buys the jeans business back."),
                Era("1980s–1990s", "Many jeans Made in USA; leather back tab with matching inside label on 80s pairs."),
            ),
            listOf(
                "Logo stamp color: grey on jeans, black on high fashion, white on underwear.",
                "Levi's-style date codes don't apply to CK; watch for later reissues of the 80s styles.",
            ),
            source = "Highsnobiety, WWD, Vintage Fashion Guild — no tag-by-era chart exists",
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
            "Giant", listOf("giant", "giant merchandising"), "Band tees",
            listOf(
                Era("1991–1995", "Three original tag styles used interchangeably; almost all 100% cotton (rare legit 50/50)."),
                Era("1993–1996", "Giant/Tultex co-tag (first on Nirvana \"Heart-Shaped Box\"); also Giant by Tee Jays and Giant/Anvil."),
                Era("1996–1999", "Non-woven \"Made in El Salvador\" tag most common; Mexico version from 1997; website on some tags from 1998."),
                Era("2004–2008", "Skull tag."),
            ),
            listOf(
                "Many Metallica Giant shirts say ©1994 — the year the band signed with Giant, not the shirt year.",
                "Real Giant by Tee Jays tags have a speckled thread texture; thick lettering is a fake sign.",
            ),
            source = "Defunkd Giant tag timeline (defunkd.com)",
        ),
        Brand(
            "Brockum", listOf("brockum"), "Band tees",
            listOf(
                Era("1984–1987", "Usually NO Brockum tag — a generic \"Made in Canada\" tag with RN 69761."),
                Era("1988–early 1990s", "Silver \"OG\" tags, plus \"Collection\" (phased out early 90s) and \"Group\" tags."),
                Era("c. 1989", "\"Sewn in Dominican Republic\" tag."),
                Era("1991–1994", "Black \"Worldwide\" tag; by 1993 all Group or Worldwide."),
                Era("c. 1997", "Bought out by Nice Man."),
            ),
            listOf(
                "A Brockum-named tag on a shirt claimed as 1984–87 is suspect.",
                "Worldwide \"Made in China\" tags are a known fake; fake Brockum fonts are taller and thinner.",
                "Brockum printed on Fruit of the Loom blanks without naming them.",
            ),
            source = "Defunkd Brockum tag timeline and fake/authentic archive (defunkd.com)",
        ),
        Brand(
            "Winterland", listOf("winterland"), "Band tees",
            listOf(
                Era("1976–1981", "No tag — a small Winterland print on the front of the shirt instead."),
                Era("1982–1988", "First Winterland tags and their variations."),
                Era("1988–1991", "MCA era; most shirts printed on Hanes blanks."),
                Era("1990s–2000s", "Later Winterland tags (Defunkd's timeline runs to 2011)."),
            ),
            source = "Defunkd Winterland tag timeline (defunkd.com)",
        ),
        Brand(
            "Other band tee tags", listOf("tennessee river", "stanley desantis"), "Band tees",
            listOf(Era("1980s–1990s", "Tennessee River, Stanley DeSantis and other merch tags common on 80s–90s band and tour tees.")),
            listOf(
                "Tour dates and copyright years on the print help, but a copyright year is only the earliest possible date.",
                "Tags can be moved between shirts — look for extra stitching or old needle holes at the neck.",
            ),
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
