package com.capstonedesign2026.platform

private fun expect(actual: Double?, expected: Double?) {
    check((actual == null && expected == null) ||
        (actual != null && expected != null && kotlin.math.abs(actual - expected) < 1e-9)) {
        "Expected $expected, got $actual"
    }
}

fun main() {
    val hour = 3_600_000L
    val minute = 60_000L
    val now = 25 * hour + 10 * minute
    val records = listOf(
        DailyAccelerationBuckets.Minute(24 * hour, 10, 8.0),
        DailyAccelerationBuckets.Minute(24 * hour + minute, 30, 12.0),
        DailyAccelerationBuckets.Minute(25 * hour, 5, 9.0),
        DailyAccelerationBuckets.Minute(23 * hour, 10, null),
        DailyAccelerationBuckets.Minute(hour, 10, 99.0),
    )
    val hours = DailyAccelerationBuckets.values(records, now, hour, 24)
    check(hours.size == 24)
    expect(hours[22], 11.0)
    expect(hours[23], 9.0)
    expect(hours[21], null)
    expect(hours[0], null)
    val minutes = DailyAccelerationBuckets.values(records, now, minute, 60)
    check(minutes.size == 60)
    expect(minutes[49], 9.0)
    expect(minutes[50], null)
    val malformed = DailyAccelerationBuckets.values(listOf(
        DailyAccelerationBuckets.Minute(25 * hour, 0, 9.0),
        DailyAccelerationBuckets.Minute(25 * hour, 2, Double.NaN)
    ), now, minute, 60)
    expect(malformed[49], null)
    println("DailyAccelerationBucketsTest passed")
}
