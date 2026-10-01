package com.capstonedesign2026.platform

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.Build
import android.os.Handler
import android.os.HandlerThread
import android.util.Log
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.util.ArrayDeque
import java.util.UUID
import java.util.concurrent.LinkedBlockingQueue
import java.util.concurrent.ThreadPoolExecutor
import java.util.concurrent.TimeUnit

internal object SensorRepository {
    private const val TAG = "CapstoneSensors"
    private const val MAX_MEMORY_SAMPLES = 512
    private const val MAX_FILE_BYTES = 4L * 1024L * 1024L
    private const val FLUSH_SAMPLE_COUNT = 25
    private const val FLUSH_INTERVAL_MS = 2_000L
    private const val SENSOR_STALE_AFTER_MS = 60_000L

    private data class SensorDefinition(val key: String, val type: Int)

    private data class SensorSample(
        val key: String,
        val type: Int,
        val values: FloatArray,
        val eventTimestampNanos: Long,
        val receivedAtEpochMs: Long,
        val accuracy: Int,
        val captureSessionId: String = "legacy-unknown",
    ) {
        fun toJson(): JSONObject = JSONObject()
            .put("sensorType", key)
            .put("androidType", type)
            .put("values", JSONArray().putFloats(values))
            .put("eventTimestampNanos", eventTimestampNanos)
            .put("receivedAtEpochMs", receivedAtEpochMs)
            .put("accuracy", accuracy)
            .put("captureSessionId", captureSessionId)
            .put("source", "Android SensorManager")
    }

    private val requested = listOf(
        SensorDefinition("ACCELEROMETER", Sensor.TYPE_ACCELEROMETER),
        SensorDefinition("GYROSCOPE", Sensor.TYPE_GYROSCOPE),
        SensorDefinition("GRAVITY", Sensor.TYPE_GRAVITY),
        SensorDefinition("LINEAR_ACCELERATION", Sensor.TYPE_LINEAR_ACCELERATION),
        SensorDefinition("MAGNETIC_FIELD", Sensor.TYPE_MAGNETIC_FIELD),
        SensorDefinition("LIGHT", Sensor.TYPE_LIGHT),
        SensorDefinition("PRESSURE", Sensor.TYPE_PRESSURE),
        SensorDefinition("STEP_COUNTER", Sensor.TYPE_STEP_COUNTER),
    )

    private val lock = Any()
    private val recentSamples = ArrayDeque<SensorSample>()
    private val pendingLines = ArrayList<String>()
    private val latestByType = HashMap<Int, SensorSample>()
    private val sensorByType = HashMap<Int, Sensor>()

    private var appContext: Context? = null
    private var initialized = false
    private var running = false
    private var serviceMessage = "Service has not been started"
    private var serviceErrorCode = ""
    private var persistedSampleCount = 0L
    private var lastFlushEpochMs = 0L
    private var persistenceDisabled = false
    private var flushScheduled = false
    private var droppedPendingSamples = 0L
    private var malformedTailLines = 0L
    private var diagnosticThread: HandlerThread? = null
    @Volatile private var captureSessionId = "not-started"
    // Exactly one drain task may be queued/running. Idle worker expires after 30 s.
    private val writer = ThreadPoolExecutor(0, 1, 30, TimeUnit.SECONDS, LinkedBlockingQueue<Runnable>())
    private val receivedIntervalByType = HashMap<Int, Long>()

    fun initialize(context: Context) {
        synchronized(lock) {
            if (initialized) return
            appContext = context.applicationContext
            val manager = context.getSystemService(Context.SENSOR_SERVICE) as SensorManager
            manager.getSensorList(Sensor.TYPE_ALL).forEach { sensor ->
                Log.i(
                    TAG,
                    "sensor name=${sensor.name} vendor=${sensor.vendor} type=${sensor.type} " +
                        "version=${sensor.version} resolution=${sensor.resolution} " +
                        "maxRange=${sensor.maximumRange} power=${sensor.power} minDelay=${sensor.minDelay}",
                )
            }
            requested.forEach { definition ->
                manager.getDefaultSensor(definition.type)?.let { sensorByType[definition.type] = it }
            }
            loadPersistedTailLocked()
            initialized = true
        }
    }

    fun requestedSensors(): List<Sensor> = synchronized(lock) {
        requested.mapNotNull { sensorByType[it.type] }
    }

    fun shouldRegister(sensor: Sensor): Boolean {
        if (sensor.type != Sensor.TYPE_STEP_COUNTER || Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            return true
        }
        val context = appContext ?: return false
        return context.checkSelfPermission(Manifest.permission.ACTIVITY_RECOGNITION) ==
            PackageManager.PERMISSION_GRANTED
    }

    fun onServiceStarted(message: String = "Foreground sensor service is collecting") = synchronized(lock) {
        running = true
        captureSessionId = UUID.randomUUID().toString()
        receivedIntervalByType.clear()
        if (persistenceDisabled) {
            serviceMessage = "Sensor collection is live, but persistence is disabled after an error"
            serviceErrorCode = "SENSOR_PERSISTENCE_FAILED"
        } else {
            serviceMessage = message
            serviceErrorCode = ""
        }
    }

    fun onServiceStopped(message: String = "Service stopped") = synchronized(lock) {
        running = false
        serviceMessage = message
        flushPendingLocked(force = true)
    }

    fun onServiceError(errorCode: String, message: String) = synchronized(lock) {
        running = false
        serviceErrorCode = errorCode
        serviceMessage = message
        flushPendingLocked(force = true)
    }

    /** A short foreground-only capture for the debug panel; no ongoing raw collection. */
    fun captureDebugBurst(): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Sensors are not initialized")
        val thread = synchronized(lock) {
            if (diagnosticThread != null)
                return resultJson(PlatformStatus.AVAILABLE, "Debug sensor capture is already running")
            HandlerThread("SensorDebugBurst").also { it.start(); diagnosticThread = it }
        }
        val manager = context.getSystemService(Context.SENSOR_SERVICE) as SensorManager
        val handler = Handler(thread.looper)
        val listener = object : SensorEventListener {
            override fun onSensorChanged(event: SensorEvent) = record(event)
            override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit
        }
        return try {
            onServiceStarted("Brief debug sensor capture is collecting")
            val registered = requestedSensors().count { sensor ->
                shouldRegister(sensor) && manager.registerListener(
                    listener, sensor, SensorManager.SENSOR_DELAY_NORMAL, handler
                )
            }
            if (registered == 0) {
                onServiceStopped("No permitted sensors were available")
                synchronized(lock) { diagnosticThread = null }
                thread.quitSafely()
                resultJson(PlatformStatus.NO_DATA, "No permitted sensors were available")
            } else {
                handler.postDelayed({
                    manager.unregisterListener(listener)
                    onServiceStopped("2-second debug sensor capture complete")
                    synchronized(lock) { if (diagnosticThread === thread) diagnosticThread = null }
                    thread.quitSafely()
                }, 2_000L)
                resultJson(PlatformStatus.AVAILABLE, "2-second debug sensor capture started")
            }
        } catch (exception: Exception) {
            manager.unregisterListener(listener)
            onServiceError("DEBUG_CAPTURE_FAILED", exception.safeMessage())
            synchronized(lock) { diagnosticThread = null }
            thread.quitSafely()
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "DEBUG_CAPTURE_FAILED")
        }
    }

    fun record(event: SensorEvent) {
        val definition = requested.firstOrNull { it.type == event.sensor.type } ?: return
        if (event.values.any { !it.isFinite() }) {
            Log.w(TAG, "Ignoring non-finite ${definition.key} sensor sample")
            return
        }
        val sample = SensorSample(
            key = definition.key,
            type = event.sensor.type,
            values = event.values.clone(),
            eventTimestampNanos = event.timestamp,
            receivedAtEpochMs = System.currentTimeMillis(),
            accuracy = event.accuracy,
            captureSessionId = captureSessionId,
        )
        synchronized(lock) {
            if (!running) return
            latestByType[sample.type]?.let { previous ->
                if (previous.captureSessionId == sample.captureSessionId)
                    receivedIntervalByType[sample.type] = sample.receivedAtEpochMs - previous.receivedAtEpochMs
            }
            latestByType[sample.type] = sample
            recentSamples.addLast(sample)
            while (recentSamples.size > MAX_MEMORY_SAMPLES) recentSamples.removeFirst()
            // Raw readings remain transient in the debug ring; only window summaries go to disk.

        }
    }

    fun snapshotJson(): JSONObject = synchronized(lock) {
        val context = appContext
        val now = System.currentTimeMillis()
        val sensors = JSONArray()
        requested.forEach { definition ->
            val sensor = sensorByType[definition.type]
            val sample = latestByType[definition.type]
            var status = when {
                sensor == null -> PlatformStatus.UNSUPPORTED
                definition.type == Sensor.TYPE_STEP_COUNTER &&
                    Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q &&
                    context?.checkSelfPermission(Manifest.permission.ACTIVITY_RECOGNITION) !=
                    PackageManager.PERMISSION_GRANTED -> if (
                        context != null && PermissionState.wasRuntimeRequested(
                            context,
                            Manifest.permission.ACTIVITY_RECOGNITION,
                        )
                    ) {
                        PlatformStatus.PERMISSION_DENIED
                    } else {
                        PlatformStatus.PERMISSION_REQUIRED
                    }
                sample == null -> PlatformStatus.NO_DATA
                // On-change sensors may legitimately keep the same value while no new event arrives.
                sensor.reportingMode == Sensor.REPORTING_MODE_CONTINUOUS &&
                    now - sample.receivedAtEpochMs > SENSOR_STALE_AFTER_MS -> PlatformStatus.STALE
                else -> PlatformStatus.AVAILABLE
            }
            if (!running && sample == null && status == PlatformStatus.NO_DATA) {
                status = PlatformStatus.NO_DATA
            }
            sensors.put(
                JSONObject()
                    .put("key", definition.key)
                    .put("status", status)
                    .put("name", sensor?.name ?: "")
                    .put("vendor", sensor?.vendor ?: "")
                    .put("androidType", definition.type)
                    .put("version", sensor?.version ?: 0)
                    .put("resolution", sensor?.resolution?.toDouble() ?: 0.0)
                    .put("maxRange", sensor?.maximumRange?.toDouble() ?: 0.0)
                    .put("power", sensor?.power?.toDouble() ?: 0.0)
                    .put("minDelay", sensor?.minDelay ?: 0)
                    .put("reportingMode", sensor?.reportingMode ?: -1)
                    .put("isWakeUpSensor", sensor?.isWakeUpSensor ?: false)
                    .put("receivedIntervalMs", receivedIntervalByType[definition.type] ?: 0L)
                    .put("values", sample?.let { JSONArray().putFloats(it.values) } ?: JSONArray())
                    .put("eventTimestampNanos", sample?.eventTimestampNanos ?: 0L)
                    .put("receivedAtEpochMs", sample?.receivedAtEpochMs ?: 0L)
                    .put("source", if (sensor == null) "" else "Android SensorManager"),
            )
        }

        val lastEvent = latestByType.values.maxOfOrNull { it.receivedAtEpochMs } ?: 0L
        JSONObject()
            .put(
                "serviceStatus",
                when {
                    serviceErrorCode.isNotEmpty() -> PlatformStatus.ERROR
                    running -> PlatformStatus.AVAILABLE
                    latestByType.values.any { now - it.receivedAtEpochMs < SENSOR_STALE_AFTER_MS } ->
                        PlatformStatus.AVAILABLE
                    latestByType.isNotEmpty() -> PlatformStatus.STALE
                    else -> PlatformStatus.NO_DATA
                },
            )
            .put("running", running)
            .put("message", serviceMessage)
            .put("errorCode", serviceErrorCode)
            .put("sampleCount", recentSamples.size)
            .put("persistedSampleCount", persistedSampleCount)
            .put("lastEventEpochMs", lastEvent)
            .put("droppedPendingSamples", droppedPendingSamples)
            .put("malformedTailLines", malformedTailLines)
            .put("pendingSampleCount", pendingLines.size)
            .put("captureSessionId", captureSessionId)
            .put("sensors", sensors)
    }

    fun recentSamplesJson(limit: Int): String = synchronized(lock) {
        val safeLimit = limit.coerceIn(1, MAX_MEMORY_SAMPLES)
        val samples = JSONArray()
        recentSamples.toList().takeLast(safeLimit).forEach { samples.put(it.toJson()) }
        JSONObject()
            .put("status", if (samples.length() == 0) PlatformStatus.NO_DATA else PlatformStatus.AVAILABLE)
            .put("mode", "LIVE")
            .put("samples", samples)
            .toString()
    }

    private fun storageFileLocked(): File {
        val context = requireNotNull(appContext)
        val directory = File(context.filesDir, "platform")
        if (!directory.exists()) directory.mkdirs()
        return File(directory, "sensor_samples.jsonl")
    }

    private fun flushPendingLocked(force: Boolean) {
        if (persistenceDisabled || pendingLines.isEmpty() || flushScheduled) return
        val now = System.currentTimeMillis()
        if (!force && pendingLines.size < FLUSH_SAMPLE_COUNT && now - lastFlushEpochMs < FLUSH_INTERVAL_MS) return
        flushScheduled = true
        writer.execute {
            while (true) {
                val lines = synchronized(lock) {
                    if (pendingLines.isEmpty() || persistenceDisabled) {
                        flushScheduled = false
                        return@execute
                    }
                    pendingLines.toList().also { pendingLines.clear() }
                }
                try {
                    // File I/O is isolated from sensor callbacks and the Unity snapshot lock.
                    BoundedSensorLog(storageFileLocked(), MAX_FILE_BYTES).append(lines)
                    synchronized(lock) {
                        persistedSampleCount += lines.size
                        lastFlushEpochMs = System.currentTimeMillis()
                    }
                } catch (_: Exception) {
                    synchronized(lock) {
                        serviceErrorCode = "SENSOR_PERSISTENCE_FAILED"
                        serviceMessage = "Sensor log could not be saved; existing data retained"
                        droppedPendingSamples += lines.size + pendingLines.size
                        pendingLines.clear()
                        persistenceDisabled = true
                        flushScheduled = false
                    }
                    Log.e(TAG, "Sensor persistence failed")
                    return@execute
                }
            }
        }
    }

    private fun loadPersistedTailLocked() {
        try {
            val file = storageFileLocked()
            if (!file.exists()) return
            val lines = BoundedSensorLog(file, MAX_FILE_BYTES).tail(MAX_MEMORY_SAMPLES)
            lines.forEach { line ->
                try {
                val json = JSONObject(line)
                val valuesJson = json.optJSONArray("values") ?: JSONArray()
                val values = FloatArray(valuesJson.length()) { index ->
                    valuesJson.optDouble(index, 0.0).toFloat()
                }
                val type = json.optInt("androidType", -1)
                if (type >= 0) {
                    val sample = SensorSample(
                        key = json.optString("sensorType", "UNKNOWN"),
                        type = type,
                        values = values,
                        eventTimestampNanos = json.optLong("eventTimestampNanos", 0L),
                        receivedAtEpochMs = json.optLong("receivedAtEpochMs", 0L),
                        accuracy = json.optInt("accuracy", 0),
                        captureSessionId = json.optString("captureSessionId", "legacy-unknown"),
                    )
                    recentSamples.addLast(sample)
                    latestByType[type] = sample
                }
                } catch (_: Exception) { malformedTailLines++ }
            }
        } catch (exception: Exception) {
            serviceErrorCode = "SENSOR_RESTORE_FAILED"
            serviceMessage = "Previous sensor samples could not be restored"
            Log.w(TAG, "Persisted sensor tail could not be loaded")
        }
    }
}
