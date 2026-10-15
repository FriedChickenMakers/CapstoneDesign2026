package com.capstonedesign2026.platform

import java.time.Instant
import java.time.ZoneId
import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.roundToInt

/** Local-only reminder timing. No activity/sleep diagnosis or pressure for missed days. */
internal object ActivityReminderPolicy {
    const val DEFAULT_MINUTE = 20 * 60
    const val MAX_HISTORY = 28
    const val RESPONSIVE_MINUTES = 10
    private const val MIN_SAMPLES = 3
    private const val MODE_WIDTH = 120
    private const val MAX_SHIFT = 120

    /** Only local clock minutes and the reaction interval are retained, never a timestamp. */
    data class Observation(val postedMinute: Int, val openedMinute: Int, val delayMinutes: Int)
    data class Estimate(
        val minute: Int,
        val windowStartMinute: Int?,
        val windowEndMinute: Int?,
        val spreadMinutes: Int,
        val sampleCount: Int,
        val informativeSamples: Int,
        val status: String
    )

    fun observation(postedAt: Long, openedAt: Long, zone: ZoneId): Observation? {
        if (postedAt <= 0 || openedAt < postedAt || openedAt - postedAt > 24 * 3600000L) return null
        val posted = Instant.ofEpochMilli(postedAt).atZone(zone)
        val opened = Instant.ofEpochMilli(openedAt).atZone(zone)
        return Observation(posted.hour * 60 + posted.minute, opened.hour * 60 + opened.minute,
            ceil((openedAt - postedAt) / 60000.0).toInt())
    }

    /**
     * Infer a response window, not a single supposedly ideal time. Fast reactions do not
     * reveal an availability time and must not move the reminder later each day. A dense
     * circular mode avoids averaging midnight into noon or two breaks into a busy period.
     */
    fun estimate(previousMinute: Int, observations: List<Observation>): Estimate {
        val previous = normalizeMinute(previousMinute)
        val history = observations.filter { it.postedMinute in 0..1439 && it.openedMinute in 0..1439 &&
            it.delayMinutes in 0..1440 }.takeLast(MAX_HISTORY)
        val informative = history.filter { it.delayMinutes > RESPONSIVE_MINUTES }
        fun result(status: String, minute: Int = previous, start: Int? = null, end: Int? = null, spread: Int = 0) =
            Estimate(minute, start, end, spread, history.size, informative.size, status)
        if (history.size < MIN_SAMPLES) return result("collecting")
        val recentDelay = quantile(history.takeLast(MIN_SAMPLES).map { it.delayMinutes }.sorted(), .5)
        if (informative.size < MIN_SAMPLES)
            return result(if (recentDelay <= RESPONSIVE_MINUTES) "responsive" else "collecting")

        data class Mode(val start: Int, val offsets: List<Int>) {
            val count get() = offsets.size
            val width get() = offsets.last() - offsets.first()
            fun minute(q: Double) = normalizeMinute(start + quantile(offsets, q))
        }
        val modes = informative.map { candidate ->
            Mode(candidate.openedMinute, informative.map { normalizeMinute(it.openedMinute - candidate.openedMinute) }
                .filter { it <= MODE_WIDTH }.sorted())
        }
        val mode = modes.sortedWith(compareByDescending<Mode> { it.count }
            .thenBy { abs(circularDifference(previous, it.minute(.2))) }
            .thenBy { it.width }).first()
        if (mode.count < MIN_SAMPLES || mode.count < ceil(informative.size * .35).toInt())
            return result(if (recentDelay <= RESPONSIVE_MINUTES) "responsive" else "uncertain")

        val start = mode.minute(.2)
        val end = mode.minute(.8)
        val spread = normalizeMinute(end - start)
        val tolerance = maxOf(RESPONSIVE_MINUTES, spread.coerceAtMost(90))
        if (recentDelay <= tolerance) return result("stable", start = start, end = end, spread = spread)
        val target = normalizeMinute(start - 5)
        val shift = circularDifference(previous, target).coerceIn(-MAX_SHIFT, MAX_SHIFT)
        return result("adapting", normalizeMinute(previous + shift), start, end, spread)
    }

    private fun normalizeMinute(minute: Int) = Math.floorMod(minute, 1440)
    private fun circularDifference(from: Int, to: Int) = Math.floorMod(to - from + 720, 1440) - 720
    private fun quantile(sorted: List<Int>, fraction: Double): Int {
        val index = (sorted.size - 1) * fraction
        val low = index.toInt()
        val high = minOf(low + 1, sorted.lastIndex)
        return (sorted[low] + (sorted[high] - sorted[low]) * (index - low)).roundToInt()
    }
    const val EARLIEST = 0
    const val LATEST = 24 * 60 - 1
    fun clampMinute(minute: Int) = minute.coerceIn(EARLIEST, LATEST)
    fun day(now: Long, zone: ZoneId) = Instant.ofEpochMilli(now).atZone(zone).toLocalDate().toString()
    data class Occurrence(val scheduledDay: String, val scheduledEpochMs: Long)

    /** The previous day's late reminder may still be due after midnight. */
    fun dueOccurrence(now: Long, zone: ZoneId, minute: Int): Occurrence? {
        val local = Instant.ofEpochMilli(now).atZone(zone)
        val chosen = clampMinute(minute)
        return listOf(local.toLocalDate(), local.toLocalDate().minusDays(1)).map { date ->
            Occurrence(date.toString(), date.atTime(chosen / 60, chosen % 60).atZone(zone).toInstant().toEpochMilli())
        }.filter { now - it.scheduledEpochMs in 0..3 * 3600000L }.maxByOrNull { it.scheduledEpochMs }
    }
    fun due(now: Long, zone: ZoneId, minute: Int, postedDay: String, completedDay: String,
            postedAt: Long = 0): Boolean {
        val occurrence = dueOccurrence(now, zone, minute) ?: return false
        if (occurrence.scheduledDay <= postedDay || occurrence.scheduledDay <= completedDay ||
            day(now, zone) <= completedDay) return false
        // A new timezone or a newly learned hour must not produce two close reminders.
        if (postedAt > 0 && now - postedAt < 12 * 3600000L) return false
        return true
    }
    fun nextEpoch(now: Long, zone: ZoneId, minute: Int): Long {
        val local = Instant.ofEpochMilli(now).atZone(zone)
        val chosen = clampMinute(minute)
        var next = local.toLocalDate().atTime(chosen / 60, chosen % 60).atZone(zone)
        if (!next.isAfter(local)) next = local.toLocalDate().plusDays(1)
            .atTime(chosen / 60, chosen % 60).atZone(zone)
        return next.toInstant().toEpochMilli()
    }
}
