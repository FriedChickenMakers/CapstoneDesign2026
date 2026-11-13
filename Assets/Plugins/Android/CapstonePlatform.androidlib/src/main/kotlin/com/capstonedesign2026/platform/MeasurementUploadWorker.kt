package com.capstonedesign2026.platform

import android.content.Context
import androidx.work.*
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.security.MessageDigest
import java.util.concurrent.TimeUnit

/** Sends the existing native outbox without starting Unity. Participant credentials
 * stay out of WorkManager input. Private per-install enrollment is reread on every
 * attempt; binding, collection and reset epoch fence stale requests. */
internal object MeasurementUpload {
    internal const val PERIODIC = "garden_measurement_upload_periodic"
    internal const val SOON = "garden_measurement_upload_soon"
    private val uploadLock = Any()
    private var lastScheduleAt = -10000L
    private var lastScheduleRoot = ""
    internal data class Enrollment(val endpoint: String, val token: String, val binding: String,
                                   val automaticEnrollmentKey: String? = null)
    internal data class Reply(val status: Int, val body: String)
    internal enum class Outcome { COMPLETE, RETRY, INACTIVE }

    internal fun enrollment(context: Context): Enrollment? = try {
        val file = File(context.filesDir, "personalization-enrollment.json")
        if (!file.isFile || file.length() > 8192 || File(context.filesDir, "personalization-local-reset").exists()) null
        else {
            val json = JSONObject(file.readText())
            val endpoint = json.getString("endpoint").trimEnd('/')
            val uri = URI(endpoint)
            val token = json.getString("token")
            require(uri.scheme == "https" && !uri.host.isNullOrEmpty() && uri.rawUserInfo == null
                && uri.rawQuery == null && uri.rawFragment == null && uri.rawPath.isNullOrEmpty())
            require(token.length in 32..512 && token.all { it.code in 33..126 })
            val automaticKey = if (json.has("automaticEnrollmentKey")) {
                val key = json.get("automaticEnrollmentKey")
                require(key is String && key.matches(Regex("[a-f0-9]{64}")))
                require(token.matches(Regex("[a-f0-9]{64}")))
                key
            } else null
            val binding = MessageDigest.getInstance("SHA-256").digest((endpoint + "\n" + token).toByteArray(Charsets.UTF_8))
                .joinToString("") { "%02x".format(it) }
            Enrollment(endpoint, token, binding, automaticKey)
        }
    } catch (_: Exception) { null }

    private fun live(context: Context) = context.getSharedPreferences("garden_activity_notifications", Context.MODE_PRIVATE)
        .getBoolean("live", false)

    /** KEEP avoids resetting a retry's backoff whenever another event is written.
     * Periodic work closes the enqueue/worker-completion race and sends empty quality
     * heartbeats needed to distinguish nonresponse from an unuploaded click. */
    @Synchronized fun schedule(context: Context) {
        try {
            if (!live(context) || NativeLocalReset.pending(context) || enrollment(context) == null) return
            val now = android.os.SystemClock.elapsedRealtime()
            if (lastScheduleRoot == context.filesDir.path && now - lastScheduleAt in 0 until 5000) return
            val work = WorkManager.getInstance(context)
            val constraints = Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build()
            val data = Data.Builder().putString("localDataEpoch", NativeLocalReset.epoch(context)).build()
            work.enqueueUniquePeriodicWork(PERIODIC, ExistingPeriodicWorkPolicy.KEEP,
                PeriodicWorkRequestBuilder<MeasurementUploadWorker>(15, TimeUnit.MINUTES)
                    .setConstraints(constraints).setInputData(data)
                    .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 1, TimeUnit.MINUTES).build())
            work.enqueueUniqueWork(SOON, ExistingWorkPolicy.KEEP,
                OneTimeWorkRequestBuilder<MeasurementUploadWorker>().setConstraints(constraints).setInputData(data)
                    .setInitialDelay(5, TimeUnit.SECONDS)
                    .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 1, TimeUnit.MINUTES).build())
            lastScheduleAt = now; lastScheduleRoot = context.filesDir.path
        } catch (_: Exception) {
            // Scheduling cannot undo an already durable observation. The foreground
            // uploader remains available and the next successful configure retries.
        }
    }

    @Synchronized fun cancel(context: Context) {
        lastScheduleAt = -10000; lastScheduleRoot = ""
        try {
            val work = WorkManager.getInstance(context)
            work.cancelUniqueWork(SOON); work.cancelUniqueWork(PERIODIC)
        } catch (_: Exception) { }
    }

    internal fun post(enrollment: Enrollment, payload: String): Reply {
        return httpPost(enrollment.endpoint + "/measurements/batch", payload,
            "Authorization", "Bearer " + enrollment.token)
    }

    internal fun register(enrollment: Enrollment, appVersion: String,
                          open: (URL) -> HttpURLConnection = { it.openConnection() as HttpURLConnection }): Reply {
        val key = enrollment.automaticEnrollmentKey ?: return Reply(401, "")
        require(key.matches(Regex("[a-f0-9]{64}")) && enrollment.token.matches(Regex("[a-f0-9]{64}")))
        require(appVersion.matches(Regex("[A-Za-z0-9._-]{1,48}")))
        // The build key is a header only; never attach it to measurement payloads.
        val payload = JSONObject().put("token", enrollment.token).put("app_version", appVersion).toString()
        return httpPost(enrollment.endpoint + "/enrollments", payload, "X-Enrollment-Key", key, open)
    }

    internal fun httpPost(address: String, payload: String, header: String, credential: String,
                          open: (URL) -> HttpURLConnection = { it.openConnection() as HttpURLConnection }): Reply {
        require(URI(address).scheme == "https")
        val connection = open(URI(address).toURL())
        try {
            connection.requestMethod = "POST"
            connection.instanceFollowRedirects = false // Never forward participant credentials.
            connection.connectTimeout = 10000; connection.readTimeout = 15000
            connection.doOutput = true
            connection.setRequestProperty(header, credential)
            connection.setRequestProperty("Content-Type", "application/json")
            val bytes = payload.toByteArray(Charsets.UTF_8)
            connection.setFixedLengthStreamingMode(bytes.size)
            connection.outputStream.use { it.write(bytes) }
            val code = connection.responseCode
            if (code !in 200..299) return Reply(code, "")
            val body = connection.inputStream.use { stream ->
                val result = java.io.ByteArrayOutputStream()
                val chunk = ByteArray(4096)
                while (true) {
                    val count = stream.read(chunk)
                    if (count < 0) break
                    require(result.size() + count <= 65536) { "Oversize acknowledgement" }
                    result.write(chunk, 0, count)
                }
                result.toString("UTF-8")
            }
            return Reply(code, body)
        } finally { connection.disconnect() }
    }

    internal fun upload(context: Context, epoch: String, stopped: () -> Boolean = { false },
                        registrationTransport: (Enrollment, String) -> Reply = { registration, version -> register(registration, version) },
                        transport: (Enrollment, String) -> Reply = ::post): Outcome = synchronized(uploadLock) {
        try {
            // Serialize native workers; concurrent foreground C# batches are safe:
            // the server deduplicates IDs and native ACK removes only sent IDs.
            var registrationAttempted = false
            repeat(4) {
                if (stopped()) return@synchronized Outcome.RETRY
                if (!NativeLocalReset.current(context, epoch) || !live(context)) return@synchronized Outcome.INACTIVE
                val registration = enrollment(context) ?: return@synchronized Outcome.INACTIVE
                val body = MeasurementTelemetry.batch(context, registration.binding)
                val batch = JSONObject(body)
                if (!batch.has("collection_id")) return@synchronized Outcome.INACTIVE
                val events = batch.getJSONArray("events")
                val sent = (0 until events.length()).map { events.getJSONObject(it).getString("event_id") }.toSet()
                fun currentCollection(): Boolean = NativeLocalReset.current(context, epoch) && live(context)
                    && enrollment(context) == registration
                    && JSONObject(MeasurementTelemetry.batch(context, registration.binding))
                        .optString("collection_id", "") == batch.getString("collection_id")
                var reply = transport(registration, body)
                if (reply.status == 401 && registration.automaticEnrollmentKey != null && !registrationAttempted) {
                    if (stopped()) return@synchronized Outcome.RETRY
                    if (!currentCollection()) return@synchronized Outcome.INACTIVE
                    registrationAttempted = true
                    val version = (context.packageManager.getPackageInfo(context.packageName, 0).versionName ?: "unknown")
                        .replace(Regex("[^A-Za-z0-9._-]"), "_").take(48).ifEmpty { "unknown" }
                    val registered = registrationTransport(registration, version)
                    if (stopped()) return@synchronized Outcome.RETRY
                    if (!currentCollection()) return@synchronized Outcome.INACTIVE
                    if (registered.status != 200 || JSONObject(registered.body).opt("registered") != true)
                        return@synchronized Outcome.RETRY
                    // One retry of exactly the same batch, never a regenerated event or key.
                    reply = transport(registration, body)
                }
                if (reply.status !in 200..299) {
                    DebugTelemetry.transport(context, if(reply.status==401) "auth" else if(reply.status>=500) "server" else "client")
                    return@synchronized Outcome.RETRY
                }
                val accepted = try { JSONObject(reply.body).getJSONArray("accepted_event_ids") }
                    catch (_: Exception) { DebugTelemetry.transport(context,"ack"); return@synchronized Outcome.RETRY }
                val ids = (0 until accepted.length()).map { accepted.getString(it) }.toSet()
                if (!sent.containsAll(ids)) { DebugTelemetry.transport(context,"ack"); return@synchronized Outcome.RETRY }
                if (stopped()) return@synchronized Outcome.RETRY
                if (!NativeLocalReset.current(context, epoch) || enrollment(context) != registration || !live(context))
                    return@synchronized Outcome.INACTIVE
                val ack = JSONObject(MeasurementTelemetry.acknowledge(context, registration.binding,
                    batch.getString("collection_id"), accepted.toString(), JSONArray(sent.toList()).toString()))
                if (!ack.optBoolean("ok") || ids.size < sent.size) { DebugTelemetry.transport(context,"ack"); return@synchronized Outcome.RETRY }
                DebugTelemetry.transport(context,"none")
                if (events.length() < 100 && batch.optLong("complete_through_utc_ms",0)>0) return@synchronized Outcome.COMPLETE
            }
            Outcome.RETRY // Bound each run; remaining batches survive and retry with backoff.
        } catch (_: Exception) { DebugTelemetry.transport(context,"network"); Outcome.RETRY }
    }
}

class MeasurementUploadWorker(context: Context, parameters: WorkerParameters) : Worker(context, parameters) {
    override fun doWork(): Result = when (MeasurementUpload.upload(applicationContext,
        inputData.getString("localDataEpoch") ?: "", { isStopped })) {
        MeasurementUpload.Outcome.RETRY -> Result.retry()
        else -> Result.success()
    }
}
