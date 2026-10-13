package com.resellcomps.core

import kotlin.math.ceil
import kotlin.math.floor

/**
 * Turns matched listings into a valuation. Deliberately deterministic and
 * model-free: every number it produces is computed from listing prices the
 * user can open and check. With no comps it returns [Valuation.NoComps]
 * rather than guessing.
 */
object PriceAnalyzer {

    fun valuate(matched: List<Listing>): Valuation {
        val sold = stats(matched.filter { it.status == ListingStatus.SOLD })
        val active = stats(matched.filter { it.status == ListingStatus.ACTIVE })
        return when {
            sold != null -> Valuation.FromSold(sold, active)
            active != null -> Valuation.OnlyActive(active)
            else -> Valuation.NoComps
        }
    }

    fun stats(listings: List<Listing>): PriceStats? {
        val valid = listings.filter { it.price > 0 }.sortedBy { it.price }
        if (valid.isEmpty()) return null
        val prices = valid.map { it.price }
        val quartiles = prices.size >= 4
        return PriceStats(
            count = valid.size,
            low = prices.first(),
            high = prices.last(),
            median = percentile(prices, 0.5),
            typicalLow = if (quartiles) percentile(prices, 0.25) else null,
            typicalHigh = if (quartiles) percentile(prices, 0.75) else null,
            listings = valid,
        )
    }

    /** Linear-interpolated percentile of an ascending list. */
    internal fun percentile(sorted: List<Double>, p: Double): Double {
        if (sorted.size == 1) return sorted[0]
        val pos = p * (sorted.size - 1)
        val lo = floor(pos).toInt()
        val hi = ceil(pos).toInt()
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo)
    }
}
