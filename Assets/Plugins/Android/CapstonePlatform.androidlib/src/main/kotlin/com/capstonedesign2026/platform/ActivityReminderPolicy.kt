package com.capstonedesign2026.platform

import java.time.Instant
import java.time.ZoneId
import kotlin.math.roundToInt

/** Local-only reminder timing. No activity/sleep diagnosis or pressure for missed days. */
internal object ActivityReminderPolicy {
    const val DEFAULT_MINUTE = 20 * 60
    const val EARLIEST = 8 * 60
    const val LATEST = 21 * 60
    fun clampMinute(minute: Int) = minute.coerceIn(EARLIEST, LATEST)
    fun day(now: Long, zone: ZoneId) = Instant.ofEpochMilli(now).atZone(zone).toLocalDate().toString()
    fun due(now: Long, zone: ZoneId, minute: Int, postedDay: String, completedDay: String): Boolean {
        val local = Instant.ofEpochMilli(now).atZone(zone)
        val today = local.toLocalDate().toString()
        val current = local.hour * 60 + local.minute
        return today > postedDay && today > completedDay && current in clampMinute(minute)..minOf(clampMinute(minute) + 180, 22 * 60 - 1)
    }
    fun nextEpoch(now: Long, zone: ZoneId, minute: Int): Long {
        val local = Instant.ofEpochMilli(now).atZone(zone)
        val chosen = clampMinute(minute)
        var next = local.toLocalDate().atTime(chosen / 60, chosen % 60).atZone(zone)
        if (!next.isAfter(local)) next = next.plusDays(1)
        return next.toInstant().toEpochMilli()
    }
    // Only actual, recent notification taps train timing; an ordinary app open cannot.
    fun learnedMinute(previous: Int, postedAt: Long, openedAt: Long, zone: ZoneId): Int? {
        if (postedAt <= 0 || openedAt < postedAt || openedAt - postedAt > 6 * 3600000L ||
            day(postedAt, zone) != day(openedAt, zone)) return null
        val local = Instant.ofEpochMilli(openedAt).atZone(zone)
        val observed = clampMinute(local.hour * 60 + local.minute)
        val step = ((observed - previous) * .25).roundToInt().coerceIn(-15, 15)
        return clampMinute(previous + step)
    }
}
