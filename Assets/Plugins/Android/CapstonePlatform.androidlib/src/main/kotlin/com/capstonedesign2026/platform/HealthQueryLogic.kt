package com.capstonedesign2026.platform

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.TimeoutCancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import kotlin.coroutines.coroutineContext
import kotlinx.coroutines.withTimeout

/** Platform-independent, synthetic-testable query policy. All bounds are DEV_DEFAULT. */
internal object HealthQueryLogic {
    data class Page<T>(val records: List<T>, val nextToken: String?)
    data class Result<T>(val records: List<T>, val complete: Boolean, val reason: String, val pages: Int)
    data class Sample(val recordId: String, val sourcePackage: String, val measuredAtEpochMs: Long,
                      val beatsPerMinute: Long, val segmentId: Int = 0)
    data class Gap(val startEpochMs: Long, val endEpochMs: Long, val sourcePackage: String)
    data class Series(val samples: List<Sample>, val display: List<Sample>, val gaps: List<Gap>)

    suspend fun <T> pages(maxPages: Int = 100, timeoutMs: Long = 30_000,
                          read: suspend (String?) -> Page<T>): Result<T> {
        val records = mutableListOf<T>()
        var pages = 0
        try {
            return withTimeout(timeoutMs) {
                var token: String? = null
                val seen = mutableSetOf<String>()
                while (pages < maxPages) {
                    val page = retry { read(token) }
                    records.addAll(page.records)
                    pages++
                    val next = page.nextToken
                    if (next.isNullOrEmpty()) return@withTimeout Result(records, true, "COMPLETE", pages)
                    if (!seen.add(next)) return@withTimeout Result(records, false, "REPEATED_TOKEN", pages)
                    token = next
                }
                Result(records, false, "PAGE_LIMIT", pages)
            }
        } catch (_: TimeoutCancellationException) {
            coroutineContext.ensureActive() // A parent timeout is cancellation, not a partial query.
            return Result(records, false, "TIMEOUT", pages)
        } catch (exception: CancellationException) {
            throw exception
        } catch (exception: Exception) {
            return Result(records, false, if (exception is SecurityException) "PERMISSION_REVOKED" else "READ_FAILED", pages)
        }
    }

    // HC quota failures use IllegalStateException. Never retry permission failures.
    suspend fun <T> retry(block: suspend () -> T): T {
        var attempt = 0
        while (true) {
            try { return block() } catch (exception: IllegalStateException) {
                if (exception is CancellationException) throw exception
                if (attempt >= 2) throw exception
                delay(250L shl attempt++)
            }
        }
    }

    fun series(input: List<Sample>, start: Long, end: Long, maxDisplay: Int = 512,
               gapMs: Long = 300_000): Series {
        require(maxDisplay >= 2 && end >= start && gapMs > 0)
        val raw = input.filter { it.measuredAtEpochMs >= start && it.measuredAtEpochMs < end }
            .distinctBy { listOf(it.recordId, it.sourcePackage, it.measuredAtEpochMs, it.beatsPerMinute) }
            .sortedWith(compareBy<Sample> { it.measuredAtEpochMs }.thenBy { it.sourcePackage }.thenBy { it.recordId })
        val gaps = mutableListOf<Gap>()
        val segmented = mutableListOf<Sample>()
        var segment = 0
        raw.groupBy { it.sourcePackage }.toSortedMap().forEach { (source, samples) ->
            var previous = start
            segment++
            samples.forEachIndexed { index, sample ->
                if (sample.measuredAtEpochMs - previous > gapMs) {
                    gaps.add(Gap(previous, sample.measuredAtEpochMs, source))
                    if (index > 0) segment++
                }
                segmented.add(sample.copy(segmentId = segment))
                previous = sample.measuredAtEpochMs
            }
            if (end - previous > gapMs) gaps.add(Gap(previous, end, source))
        }
        val ordered = segmented.sortedWith(compareBy<Sample> { it.measuredAtEpochMs }.thenBy { it.sourcePackage })
        if (ordered.isEmpty()) gaps.add(Gap(start, end, ""))
        // Choose an evenly spaced view only; raw remains separate. Segment IDs prevent bridging missing data.
        val display = if (ordered.size <= maxDisplay) ordered else (0 until maxDisplay).map {
            ordered[(it.toLong() * (ordered.size - 1) / (maxDisplay - 1)).toInt()]
        }
        return Series(ordered, display, gaps)
    }
}
