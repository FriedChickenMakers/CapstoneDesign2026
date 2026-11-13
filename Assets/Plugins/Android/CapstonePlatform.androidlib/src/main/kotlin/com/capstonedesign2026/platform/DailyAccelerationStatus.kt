package com.capstonedesign2026.platform

import android.content.Context
import android.os.Process
import android.util.AtomicFile
import org.json.JSONObject
import java.io.File

/** Read through disk on every call: SharedPreferences caches are not coherent across service/UI processes. */
internal object DailyAccelerationStatus {
    private fun directory(context: Context) = File(context.filesDir, "platform/daily_acceleration").also { it.mkdirs() }
    private fun statusFile(context: Context) = AtomicFile(File(directory(context), "status.json"))

    fun write(context: Context, status: String, reason: String, started: Long, sample: Long, written: Long) {
        val now = System.currentTimeMillis()
        val json = JSONObject().put("status", status).put("reason", reason)
            .put("startedEpochMs", started).put("updatedEpochMs", now)
            .put("lastSampleEpochMs", sample).put("lastWriteEpochMs", written).put("pid", Process.myPid())
        val atomic = statusFile(context)
        val output = atomic.startWrite()
        try { output.write(json.toString().toByteArray(Charsets.UTF_8)); atomic.finishWrite(output) }
        catch (error: Exception) { atomic.failWrite(output); throw error }
        if (reason.isNotEmpty()) {
            val events = File(directory(context), "lifecycle.jsonl")
            if (events.length() > 128 * 1024) {
                val previous = File(directory(context), "lifecycle.previous.jsonl")
                previous.delete()
                events.renameTo(previous)
            }
            events.appendText(json.toString() + "\n")
        }
    }

    fun read(context: Context): JSONObject = try {
        // AtomicFile.openRead may delete an in-flight .new file. UI readers must only open
        // the committed base file while the service is publishing from another process.
        val json = statusFile(context).baseFile.bufferedReader().use { JSONObject(it.readText()) }
        if (json.optString("status") == "RUNNING" &&
            System.currentTimeMillis() - json.optLong("updatedEpochMs") > 120_000) {
            json.put("status", "INTERRUPTED").put("reason", "HEARTBEAT_EXPIRED")
        }
        json
    } catch (_: Exception) {
        JSONObject().put("status", "UNKNOWN").put("reason", "NO_STATUS_RECORD")
    }
}
