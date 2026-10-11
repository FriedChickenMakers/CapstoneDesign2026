package com.capstonedesign2026.platform

import java.time.Duration

/** Select the latest main rest window; the Health Connect aggregate computes its actual total. */
internal object HealthPersonalizationLogic {
    data class Interval(val startMs: Long, val endMs: Long)

    fun sleepWindow(input: List<Interval>, nowMs: Long): Interval? {
        val windows = input.filter { it.startMs >= 0 && it.endMs <= nowMs && it.endMs > it.startMs &&
            it.endMs - it.startMs <= Duration.ofHours(24).toMillis() }.sortedBy { it.startMs }
        val merged = mutableListOf<Interval>()
        for (window in windows) {
            val prior = merged.lastOrNull()
            if (prior != null && window.startMs <= prior.endMs + Duration.ofMinutes(90).toMillis() &&
                maxOf(prior.endMs, window.endMs) - prior.startMs <= Duration.ofHours(16).toMillis()) {
                merged[merged.lastIndex] = Interval(prior.startMs, maxOf(prior.endMs, window.endMs))
            } else merged.add(window)
        }
        // A later short nap does not replace a main sleep window. An older main window must not
        // hide a newer one: recency is checked by the consumer against this exact end timestamp.
        return merged.lastOrNull { it.endMs - it.startMs >= Duration.ofHours(2).toMillis() }
            ?: merged.lastOrNull()
    }
}
