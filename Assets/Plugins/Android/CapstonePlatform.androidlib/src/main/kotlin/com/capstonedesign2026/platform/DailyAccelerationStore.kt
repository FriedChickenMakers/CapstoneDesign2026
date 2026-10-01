package com.capstonedesign2026.platform

import android.content.Context
import org.json.JSONObject
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone

/** App-private minute summaries; no individual sensor events are written here. */
internal object DailyAccelerationStore {
    private const val MAX_DAY_BYTES = 512 * 1024L
    private const val RETAIN_MS = 7 * 24 * 60 * 60 * 1000L

    private fun directory(context: Context) = File(context.filesDir, "platform/daily_acceleration").also { it.mkdirs() }

    private fun file(context: Context, startEpochMs: Long): File {
        val formatter = SimpleDateFormat("yyyyMMdd", Locale.US).apply { timeZone = TimeZone.getTimeZone("UTC") }
        return File(directory(context), "daily-${formatter.format(Date(startEpochMs))}.jsonl")
    }

    fun append(context: Context, row: SensorWindowAccumulator.Summary) {
        val json = JSONObject()
            .put("schemaVersion", 1)
            .put("startEpochMs", row.startEpochMs)
            .put("endEpochMs", row.endEpochMs)
            .put("accelerationSamples", row.accelerationCount)
            .put("meanAccelerationMagnitudeMps2", row.accelerationMean ?: JSONObject.NULL)
            .put("firstEventElapsedMs", row.firstEventElapsedMs ?: JSONObject.NULL)
            .put("lastEventElapsedMs", row.lastEventElapsedMs ?: JSONObject.NULL)
            .put("status", if (row.accelerationCount == 0L) "NO_SAMPLES" else "OBSERVED")
        val line = json.toString() + "\n"
        val target = file(context, row.startEpochMs)
        check(target.length() + line.toByteArray().size <= MAX_DAY_BYTES) { "Daily summary size limit reached" }
        target.appendText(line)
    }

    fun prune(context: Context, nowEpochMs: Long) {
        directory(context).listFiles()?.forEach { entry ->
            if (entry.name.matches(Regex("daily-[0-9]{8}\\.jsonl")) &&
                entry.lastModified() < nowEpochMs - RETAIN_MS) entry.delete()
        }
    }

    fun readRecent(context: Context, nowEpochMs: Long): List<DailyAccelerationBuckets.Minute> {
        val earliest = nowEpochMs - 25 * 60 * 60 * 1000L
        val result = ArrayList<DailyAccelerationBuckets.Minute>()
        directory(context).listFiles()?.filter {
            it.name.matches(Regex("daily-[0-9]{8}\\.jsonl")) && it.length() <= MAX_DAY_BYTES
        }?.sortedBy { it.name }?.forEach { entry ->
            entry.forEachLine { line ->
                try {
                    val json = JSONObject(line)
                    val start = json.getLong("startEpochMs")
                    val count = json.optLong("accelerationSamples", 0L)
                    val mean = if (json.isNull("meanAccelerationMagnitudeMps2")) null
                        else json.optDouble("meanAccelerationMagnitudeMps2").takeIf { it.isFinite() }
                    if (start >= earliest && start <= nowEpochMs && count > 0)
                        result.add(DailyAccelerationBuckets.Minute(start, count, mean))
                } catch (_: Exception) {
                    // A process death can leave one incomplete JSONL tail; older lines remain usable.
                }
            }
        }
        return result
    }
}
