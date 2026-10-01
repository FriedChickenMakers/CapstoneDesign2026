package com.capstonedesign2026.platform

/** Screen-only rollup of persisted minute means. Missing minutes remain null. */
internal object DailyAccelerationBuckets {
    data class Minute(val startEpochMs: Long, val samples: Long, val meanMagnitudeMps2: Double?)

    fun values(records: Iterable<Minute>, nowEpochMs: Long, bucketMs: Long, slots: Int): List<Double?> {
        require(bucketMs > 0 && slots > 0)
        val latestStart = nowEpochMs / bucketMs * bucketMs
        val firstStart = latestStart - (slots - 1L) * bucketMs
        val weightedSums = DoubleArray(slots)
        val counts = LongArray(slots)
        for (record in records) {
            val mean = record.meanMagnitudeMps2 ?: continue
            if (record.samples <= 0 || !mean.isFinite()) continue
            val bucket = record.startEpochMs / bucketMs * bucketMs
            if (bucket < firstStart || bucket > latestStart) continue
            val index = ((bucket - firstStart) / bucketMs).toInt()
            weightedSums[index] += mean * record.samples
            counts[index] += record.samples
        }
        return (0 until slots).map { index ->
            if (counts[index] == 0L) null else weightedSums[index] / counts[index]
        }
    }
}
