package com.capstonedesign2026.platform

import android.content.Context
import android.util.AtomicFile
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

    fun append(context: Context, row: SensorWindowAccumulator.Summary, partial: Boolean = false) {
        val json = JSONObject()
            .put("schemaVersion", 2)
            .put("partial", partial)
            .put("startEpochMs", row.startEpochMs)
            .put("endEpochMs", row.endEpochMs)
            .put("accelerationSamples", row.accelerationCount)
            .put("meanAccelerationMagnitudeMps2", row.accelerationMean ?: JSONObject.NULL)
            .put("firstEventElapsedMs", row.firstEventElapsedMs ?: JSONObject.NULL)
            .put("lastEventElapsedMs", row.lastEventElapsedMs ?: JSONObject.NULL)
            .put("status", if (row.accelerationCount == 0L) "NO_SAMPLES" else "OBSERVED")
        val target = file(context, row.startEpochMs)
        val atomic = AtomicFile(target)
        // Replacing the whole bounded daily file prevents a killed write from poisoning the next JSONL row.
        val lines = try { atomic.openRead().bufferedReader().use { it.readLines() } }
            catch (_: java.io.FileNotFoundException) { emptyList() }
        val kept = ArrayList<String>()
        for (line in lines) {
            val old = try { JSONObject(line) } catch (_: Exception) { continue }
            if (old.optLong("startEpochMs", -1) != row.startEpochMs) {
                kept.add(line)
                continue
            }
            val count = old.optLong("accelerationSamples", 0)
            val total = count + json.getLong("accelerationSamples")
            if (count > 0 && !old.isNull("meanAccelerationMagnitudeMps2")) {
                val sum = old.getDouble("meanAccelerationMagnitudeMps2") * count +
                    (if (json.isNull("meanAccelerationMagnitudeMps2")) 0.0
                     else json.getDouble("meanAccelerationMagnitudeMps2") * json.getLong("accelerationSamples"))
                json.put("meanAccelerationMagnitudeMps2", sum / total)
                json.put("accelerationSamples", total)
                json.put("status", "OBSERVED")
                if (!old.isNull("firstEventElapsedMs")) json.put("firstEventElapsedMs", old.getLong("firstEventElapsedMs"))
                if (json.isNull("lastEventElapsedMs") && !old.isNull("lastEventElapsedMs"))
                    json.put("lastEventElapsedMs", old.getLong("lastEventElapsedMs"))
            }
        }
        kept.add(json.toString())
        val bytes = (kept.joinToString("\n") + "\n").toByteArray(Charsets.UTF_8)
        check(bytes.size <= MAX_DAY_BYTES) { "Daily summary size limit reached" }
        val output = atomic.startWrite()
        try { output.write(bytes); atomic.finishWrite(output) }
        catch (error: Exception) { atomic.failWrite(output); throw error }
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
