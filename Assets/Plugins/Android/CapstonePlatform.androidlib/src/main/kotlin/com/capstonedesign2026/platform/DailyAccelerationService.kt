package com.capstonedesign2026.platform

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.ServiceInfo
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.BatteryManager
import android.os.Build
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.os.PowerManager
import android.os.SystemClock
import android.util.Log

/** Explicit, bounded recording session. All state and sensor callbacks share this process's main looper. */
class DailyAccelerationService : Service(), SensorEventListener {
    companion object {
        const val ACTION_STOP = "com.capstonedesign2026.platform.STOP_DAILY_ACCELERATION"
        private const val CHANNEL_ID = "capstone_daily_acceleration"
        private const val NOTIFICATION_ID = 2603
        private const val BURST_MS = 5_000L
        private const val QUIET_MS = 55_000L
        private const val MAX_TRIAL_MS = 48 * 60 * 60 * 1000L
        private const val TAG = "CapstoneDailyAccel"
    }
    private val state by lazy { getSharedPreferences("daily_acceleration_trial", MODE_PRIVATE) }
    private val handler = Handler(Looper.getMainLooper())
    private var manager: SensorManager? = null
    private var sensor: Sensor? = null
    private var accumulator: SensorWindowAccumulator? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var startedEpochMs = 0L
    private var deadlineElapsedMs = 0L
    private var registered = false
    private var accepting = false
    private var stopped = false
    private var lastSampleEpochMs = 0L
    private var lastWriteEpochMs = 0L

    private val endBurst: Runnable = Runnable {
        unregister()
        if (accepting && checkpoint()) {
            publish("RUNNING")
            handler.postDelayed(burst, QUIET_MS)
        }
    }
    private val burst: Runnable = object : Runnable {
        override fun run() {
            if (!accepting) return
            if (SystemClock.elapsedRealtime() >= deadlineElapsedMs) { stopTrial("DURATION_LIMIT"); return }
            if (batteryTooLow()) { stopTrial("LOW_BATTERY"); return }
            // A foreground service alone does not keep a non-wakeup accelerometer/Handler running in suspend.
            // Renew a bounded lease; a stalled service cannot hold the CPU for the whole 48-hour trial.
            try {
                wakeLock?.acquire(120_000L)
                if (!writeClosed(SystemClock.elapsedRealtime())) return
                registered = manager?.registerListener(this@DailyAccelerationService, sensor,
                    SensorManager.SENSOR_DELAY_NORMAL, 0, handler) == true
                if (!registered) { stopTrial("SENSOR_REGISTRATION_FAILED"); return }
                publish("RUNNING")
                handler.postDelayed(endBurst, BURST_MS)
            } catch (error: Exception) {
                Log.e(TAG, "Sensor registration failed", error)
                stopTrial("SENSOR_REGISTRATION_FAILED")
            }
        }
    }

    override fun onCreate() {
        super.onCreate()
        val previous = DailyAccelerationStatus.read(this)
        startedEpochMs = previous.optLong("startedEpochMs")
        lastSampleEpochMs = previous.optLong("lastSampleEpochMs")
        lastWriteEpochMs = previous.optLong("lastWriteEpochMs")
        val channel = NotificationChannel(CHANNEL_ID, "가속도 수집", NotificationManager.IMPORTANCE_LOW)
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
        val open = packageManager.getLaunchIntentForPackage(packageName)?.let {
            PendingIntent.getActivity(this, 0, it, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        }
        val stop = PendingIntent.getService(this, 1,
            Intent(this, DailyAccelerationService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val notification = Notification.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_menu_compass)
            .setContentTitle("휴대폰 가속도 수집 중")
            .setContentText("매분 5초 · 최대 48시간 · 화면이 꺼져도 수집하며 배터리를 사용해요")
            .setContentIntent(open).setOngoing(true).setCategory(Notification.CATEGORY_SERVICE)
            .addAction(Notification.Action.Builder(null, "수집 중지", stop).build()).build()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE)
            startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH)
        else startForeground(NOTIFICATION_ID, notification)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) { stopTrial("USER_STOP"); return START_NOT_STICKY }
        if (accepting) return START_STICKY
        if (stopped) return START_NOT_STICKY
        if (intent == null && !state.getBoolean("active", false)) { stopTrial("INACTIVE_RESTART"); return START_NOT_STICKY }
        val now = System.currentTimeMillis()
        startedEpochMs = if (state.getBoolean("active", false)) state.getLong("startedEpochMs", now) else now
        // Explicit Start must work with an expired active flag left by a killed process.
        // Sticky restarts retain the original deadline and cannot silently renew the trial.
        if (intent != null && now - startedEpochMs >= MAX_TRIAL_MS) startedEpochMs = now
        val remaining = MAX_TRIAL_MS - (now - startedEpochMs).coerceAtLeast(0)
        if (remaining <= 0) { stopTrial("DURATION_LIMIT"); return START_NOT_STICKY }
        if (batteryTooLow()) { stopTrial("LOW_BATTERY"); return START_NOT_STICKY }
        deadlineElapsedMs = SystemClock.elapsedRealtime() + remaining
        if (!state.edit().putBoolean("active", true).putBoolean("userEnabled", true)
                .putLong("startedEpochMs", startedEpochMs).commit()) {
            stopTrial("STATE_WRITE_FAILED"); return START_NOT_STICKY
        }
        try {
            DailyAccelerationStore.prune(this, now)
            val minute = now / 60_000L * 60_000L
            accumulator = SensorWindowAccumulator(60_000, SystemClock.elapsedRealtime() - (now - minute), minute)
            manager = getSystemService(SENSOR_SERVICE) as SensorManager
            sensor = manager?.getDefaultSensor(Sensor.TYPE_ACCELEROMETER)
            if (sensor == null) { stopTrial("SENSOR_UNAVAILABLE"); return START_NOT_STICKY }
            wakeLock = (getSystemService(POWER_SERVICE) as PowerManager)
                .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "$packageName:DailyAcceleration").apply {
                    setReferenceCounted(false)
                }
            accepting = true
            publish("RUNNING", if (intent == null) "PROCESS_RESTART" else "USER_START")
            handler.post(burst)
        } catch (error: Exception) {
            Log.e(TAG, "Trial start failed", error)
            stopTrial("START_FAILED")
            return START_NOT_STICKY
        }
        return START_STICKY
    }

    override fun onSensorChanged(event: SensorEvent?) {
        if (!accepting || event?.sensor?.type != Sensor.TYPE_ACCELEROMETER || event.values.size < 3) return
        if (SystemClock.elapsedRealtime() >= deadlineElapsedMs) { stopTrial("DURATION_LIMIT"); return }
        val elapsed = event.timestamp / 1_000_000L
        if (!writeClosed(elapsed)) return
        accumulator?.add("acceleration", elapsed, event.values[0].toDouble(), event.values[1].toDouble(), event.values[2].toDouble())
        lastSampleEpochMs = System.currentTimeMillis()
    }
    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit

    private fun writeClosed(elapsed: Long): Boolean = try {
        accumulator?.closeThrough(elapsed)?.forEach {
            DailyAccelerationStore.append(this, it)
            lastWriteEpochMs = System.currentTimeMillis()
        }
        true
    } catch (error: Exception) { writeFailed(error); false }

    private fun checkpoint(): Boolean = try {
        accumulator?.drainAcceleration()?.let {
            DailyAccelerationStore.append(this, it, partial = true)
            lastWriteEpochMs = System.currentTimeMillis()
        }
        true
    } catch (error: Exception) { writeFailed(error); false }

    private fun writeFailed(error: Exception) {
        Log.e(TAG, "Daily summary write failed", error)
        stopTrial("WRITE_FAILED", flush = false)
    }
    private fun batteryTooLow(): Boolean {
        val battery = registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED)) ?: return false
        val level = battery.getIntExtra(BatteryManager.EXTRA_LEVEL, -1)
        val scale = battery.getIntExtra(BatteryManager.EXTRA_SCALE, -1)
        // Plugged-in devices may temporarily pause charging; they should not fail a recording start.
        val plugged = battery.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0) != 0
        return !plugged && level >= 0 && scale > 0 && level * 100 / scale < 20
    }
    private fun unregister() {
        if (registered) manager?.unregisterListener(this)
        registered = false
    }
    private fun publish(status: String, reason: String = "") {
        try {
            DailyAccelerationStatus.write(this, status, reason, startedEpochMs, lastSampleEpochMs, lastWriteEpochMs)
        } catch (error: Exception) { Log.e(TAG, "Trial status write failed", error) }
    }
    private fun stopTrial(reason: String, flush: Boolean = true) {
        if (stopped) return
        stopped = true
        accepting = false
        handler.removeCallbacksAndMessages(null)
        unregister()
        var finalReason = reason
        if (flush) {
            try {
                val end = if (deadlineElapsedMs > 0) minOf(SystemClock.elapsedRealtime(), deadlineElapsedMs)
                    else SystemClock.elapsedRealtime()
                accumulator?.closeThrough(end)?.forEach { DailyAccelerationStore.append(this, it) }
                accumulator?.drainAcceleration()?.let {
                    DailyAccelerationStore.append(this, it, partial = true)
                    lastWriteEpochMs = System.currentTimeMillis()
                }
            } catch (error: Exception) { Log.e(TAG, "Final checkpoint failed", error); finalReason = "WRITE_FAILED" }
        }
        if (wakeLock?.isHeld == true) wakeLock?.release()
        state.edit().putBoolean("active", false).putBoolean("userEnabled", false)
            .putString("stopReason", finalReason).putLong("stoppedEpochMs", System.currentTimeMillis()).commit()
        publish("STOPPED", finalReason)
        Log.i(TAG, "Daily acceleration stopped: $finalReason")
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }
    override fun onDestroy() {
        stopTrial("SERVICE_DESTROYED")
        super.onDestroy()
    }
    override fun onBind(intent: Intent?): IBinder? = null
}
