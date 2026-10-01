package com.capstonedesign2026.platform

import kotlin.math.roundToLong

/** One source per health data type. A phone and a watch must never be added together. */
internal object HealthSourcePolicy {
    private const val SAMSUNG_HEALTH = "com.sec.android.app.shealth"
    private val wearableTypes = setOf(1, 4, 6, 7) // watch, ring, fitness band, chest strap

    data class Source(val packageName: String, val deviceType: Int?) {
        val id: String get() = "$packageName|${deviceType ?: 0}"
        val isWearable: Boolean get() = deviceType in wearableTypes
        val tier: Int get() = when {
            isWearable -> 0
            packageName == SAMSUNG_HEALTH && (deviceType == null || deviceType == 0) -> 1
            deviceType == 2 -> 2 // phone
            else -> 3
        }
    }

    data class Candidate<T>(val value: T, val source: Source, val measuredAtMs: Long,
                            val coverageMs: Long = 0L)
    data class Selection<T>(val source: Source, val values: List<T>)
    data class StepSpan(val id: String, val source: Source, val startMs: Long, val endMs: Long,
                        val count: Long)
    data class StepTotal(val count: Long, val source: Source, val records: Int)

    fun <T> select(input: List<Candidate<T>>, nowMs: Long, freshnessMs: Long = Long.MAX_VALUE,
                   preferCoverage: Boolean = false): Selection<T>? {
        if (input.isEmpty()) return null
        val fresh = if (freshnessMs == Long.MAX_VALUE) input else input.filter {
            it.measuredAtMs <= nowMs && nowMs - it.measuredAtMs <= freshnessMs
        }
        val eligible = if (fresh.isNotEmpty()) fresh else input
        val chosen = eligible.groupBy { it.source }.entries.sortedWith(
            compareBy<Map.Entry<Source, List<Candidate<T>>>> { it.key.tier }
                .thenByDescending { if (preferCoverage) it.value.sumOf { item -> item.coverageMs } else 0L }
                .thenByDescending { it.value.maxOf { item -> item.measuredAtMs } }
                .thenBy { it.key.id },
        ).first().key
        return Selection(chosen, input.filter { it.source == chosen }.map { it.value })
    }

    fun steps(input: List<StepSpan>, startMs: Long, endMs: Long): StepTotal? {
        require(endMs > startMs)
        val candidates = input.filter { it.count >= 0 && it.endMs > it.startMs &&
            it.startMs < endMs && it.endMs > startMs }
        val sourceCandidates = candidates.filter { it.count > 0 }.ifEmpty { candidates }
        val selected = select(sourceCandidates.map { Candidate(it, it.source, it.endMs,
            minOf(it.endMs, endMs) - maxOf(it.startMs, startMs)) }, endMs, preferCoverage = true)
            ?: return null
        // Prefer shorter, more precise records where one writer supplies overlapping intervals.
        // A longer record contributes only its uncovered portion. Query-edge records are prorated.
        val spans = selected.values.distinctBy { listOf(it.startMs, it.endMs, it.count) }
            .sortedWith(compareBy<StepSpan> { it.endMs - it.startMs }.thenBy { it.startMs })
        val covered = mutableListOf<Pair<Long, Long>>()
        var count = 0.0
        for (span in spans) {
            val from = maxOf(span.startMs, startMs)
            val until = minOf(span.endMs, endMs)
            val overlap = covered.sumOf { (a, b) -> maxOf(0L, minOf(until, b) - maxOf(from, a)) }
            val uncovered = maxOf(0L, until - from - overlap)
            count += span.count.toDouble() * uncovered / (span.endMs - span.startMs)
            addCovered(covered, from, until)
        }
        return StepTotal(count.roundToLong(), selected.source, spans.size)
    }

    private fun addCovered(covered: MutableList<Pair<Long, Long>>, from: Long, until: Long) {
        var start = from
        var end = until
        var index = 0
        while (index < covered.size) {
            val (a, b) = covered[index]
            if (b < start || a > end) { index++; continue }
            start = minOf(start, a)
            end = maxOf(end, b)
            covered.removeAt(index)
        }
        covered.add(start to end)
    }
}
