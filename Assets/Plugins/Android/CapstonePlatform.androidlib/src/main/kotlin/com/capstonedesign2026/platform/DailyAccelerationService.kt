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
import android.os.HandlerThread
import android.os.IBinder
import android.os.SystemClock
import android.util.Log
import java.util.concurrent.Executors

/** Short accelerometer bursts for the explicitly enabled 48-hour phone trial. */
class DailyAccelerationService : Service(), SensorEventListener {
    companion object {
        const val ACTION_STOP = "com.capstonedesign2026.platform.STOP_DAILY_ACCELERATION"
        private const val CHANNEL_ID = "capstone_daily_acceleration"
        private const val NOTIFICATION_ID = 2603
        private const val BURST_MS = 5_000L
        private const val QUIET_MS = 55_000L
        private const val MAX_TRIAL_MS = 48 * 60 * 60 * 1000L
        private const val MIN_BATTERY_PERCENT = 20
        private const val TAG = "CapstoneDailyAccel"
    }

    private val state by lazy { getSharedPreferences("daily_acceleration_trial", MODE_PRIVATE) }
    private val writer = Executors.newSingleThreadExecutor()
    private var sensorManager: SensorManager? = null
    private var accelerometer: Sensor? = null
    private var thread: HandlerThread? = null
    private var handler: Handler? = null
    private var accumulator: SensorWindowAccumulator? = null
    private var startedEpochMs = 0L
    private var registered = false
    @Volatile private var accepting = false

    private val endBurst: Runnable by lazy { Runnable {
        if (registered) sensorManager?.unregisterListener(this, accelerometer)
        registered = false
        if (accepting) handler?.postDelayed(burst, QUIET_MS)
    } }

    private val burst: Runnable = object : Runnable {
        override fun run() {
            if (!accepting) return
            if (System.currentTimeMillis() - startedEpochMs >= MAX_TRIAL_MS || batteryTooLow()) {
                stopTrial()
                return
            }
            closeWindows(SystemClock.elapsedRealtime())
            if (!accepting) return
            val sensor = accelerometer
            val target = handler
            if (sensor != null && target != null) {
                registered = sensorManager?.registerListener(
                    this@DailyAccelerationService, sensor,
                    SensorManager.SENSOR_DELAY_NORMAL, 0, target
                ) == true
            }
            target?.postDelayed(endBurst, BURST_MS)
        }
    }

    override fun onCreate() {
        super.onCreate()
        val channel = NotificationChannel(
            CHANNEL_ID, "Device acceleration trial", NotificationManager.IMPORTANCE_LOW
        ).apply { description = "Short phone sensor checks each minute" }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
        val launch = packageManager.getLaunchIntentForPackage(packageName)
        val open = launch?.let {
            PendingIntent.getActivity(this, 0, it,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        }
        val stop = PendingIntent.getService(this, 1,
            Intent(this, DailyAccelerationService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val notification = Notification.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_menu_compass)
            .setContentTitle("Phone acceleration trial")
            .setContentText("5 seconds of sampling each minute · tap Stop to end")
            .setContentIntent(open)
            .setOngoing(true)
            .setCategory(Notification.CATEGORY_SERVICE)
            .addAction(Notification.Action.Builder(null, "Stop", stop).build())
            .build()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE)
            startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH)
        else startForeground(NOTIFICATION_ID, notification)
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            stopTrial()
            return START_NOT_STICKY
        }
        if (accepting) return START_STICKY
        if (intent == null && !state.getBoolean("active", false)) {
            stopSelf()
            return START_NOT_STICKY
        }
        if (batteryTooLow()) {
            stopTrial()
            return START_NOT_STICKY
        }
        val nowEpoch = System.currentTimeMillis()
        startedEpochMs = if (state.getBoolean("active", false))
            state.getLong("startedEpochMs", nowEpoch) else nowEpoch
        if (nowEpoch - startedEpochMs >= MAX_TRIAL_MS) {
            stopTrial()
            return START_NOT_STICKY
        }
        if (!state.edit().putBoolean("active", true).putLong("startedEpochMs", startedEpochMs).commit()) {
            stopSelf()
            return START_NOT_STICKY
        }
        DailyAccelerationStore.prune(this, nowEpoch)
        val nowElapsed = SystemClock.elapsedRealtime()
        val minuteEpoch = nowEpoch / 60_000L * 60_000L
        accumulator = SensorWindowAccumulator(60_000L, nowElapsed - (nowEpoch - minuteEpoch), minuteEpoch)
        sensorManager = getSystemService(SENSOR_SERVICE) as SensorManager
        accelerometer = sensorManager?.getDefaultSensor(Sensor.TYPE_ACCELEROMETER)
        if (accelerometer == null) {
            stopTrial()
            return START_NOT_STICKY
        }
        thread = HandlerThread("DailyAccelerationBursts").also { it.start() }
        handler = Handler(requireNotNull(thread).looper)
        accepting = true
        handler?.post(burst)
        Log.i(TAG, "Daily acceleration trial active; accelerometer only, 5 seconds per minute")
        return START_STICKY
    }

    override fun onSensorChanged(event: SensorEvent?) {
        if (!accepting || event?.sensor?.type != Sensor.TYPE_ACCELEROMETER || event.values.size < 3) return
        if (System.currentTimeMillis() - startedEpochMs >= MAX_TRIAL_MS) {
            stopTrial()
            return
        }
        val elapsed = event.timestamp / 1_000_000L
        closeWindows(elapsed)
        if (accepting) accumulator?.add("acceleration", elapsed,
            event.values[0].toDouble(), event.values[1].toDouble(), event.values[2].toDouble())
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit

    private fun closeWindows(nowElapsed: Long) {
        accumulator?.closeThrough(nowElapsed)?.forEach { row ->
            if (row.accelerationCount == 0L) return@forEach
            writer.execute {
                try {
                    DailyAccelerationStore.append(this, row)
                } catch (error: Exception) {
                    Log.e(TAG, "Daily summary write failed", error)
                    stopTrial()
                }
            }
        }
    }

    private fun batteryTooLow(): Boolean {
        val battery = registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED)) ?: return false
        val level = battery.getIntExtra(BatteryManager.EXTRA_LEVEL, -1)
        val scale = battery.getIntExtra(BatteryManager.EXTRA_SCALE, -1)
        val status = battery.getIntExtra(BatteryManager.EXTRA_STATUS, -1)
        if (level < 0 || scale <= 0) return false
        val charging = status == BatteryManager.BATTERY_STATUS_CHARGING ||
            status == BatteryManager.BATTERY_STATUS_FULL
        return !charging && level * 100 / scale < MIN_BATTERY_PERCENT
    }

    private fun stopTrial() {
        accepting = false
        state.edit().putBoolean("active", false).apply()
        stopSelf()
    }

    override fun onDestroy() {
        accepting = false
        handler?.removeCallbacks(burst)
        handler?.removeCallbacks(endBurst)
        if (registered) sensorManager?.unregisterListener(this)
        registered = false
        thread?.quitSafely()
        thread = null
        handler = null
        writer.shutdown()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null
}
