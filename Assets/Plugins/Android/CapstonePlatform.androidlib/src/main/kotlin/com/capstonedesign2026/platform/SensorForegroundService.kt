package com.capstonedesign2026.platform

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.Build
import android.os.Handler
import android.os.HandlerThread
import android.os.SystemClock
import android.os.PowerManager
import org.json.JSONObject
import org.json.JSONArray
import java.io.File
import java.util.UUID
import java.util.concurrent.Executors
import android.os.IBinder
import android.util.Log

class SensorForegroundService : Service(), SensorEventListener {
    companion object {
        const val ACTION_STOP = "com.capstonedesign2026.platform.STOP_SENSOR_SERVICE"
        private const val TAG = "CapstoneSensorService"
        private const val CHANNEL_ID = "capstone_sensor_collection"
        private const val NOTIFICATION_ID = 2601
    }

    @Volatile private var acceptingEvents = false
    private var sensorManager: SensorManager? = null
    private var sensorThread: HandlerThread? = null
    private var sensorHandler: Handler? = null

    private var accumulator: SensorWindowAccumulator? = null
    private var durationMs = 0L
    private var runId = ""
    private var summaryFile: File? = null
    private val disk = Executors.newSingleThreadExecutor()
    private var writeError = false
    private var rows = 0
    private val matrix = FloatArray(9)
    private val angles = FloatArray(3)
    private val ticker = object : Runnable {
        override fun run() {
            if (!acceptingEvents) return
            closeWindows(SystemClock.elapsedRealtime())
            if (acceptingEvents) sensorHandler?.postDelayed(this, 250)
        }
    }

    override fun onCreate() {
        super.onCreate()
        try {
            SensorRepository.initialize(applicationContext)
            createNotificationChannel()
            promoteToForeground(buildNotification())

            Log.i(TAG, "Foreground sensor service started")
        } catch (exception: Exception) {
            Log.e(TAG, "Sensor service initialization failed", exception)
            SensorRepository.onServiceError("SERVICE_START_FAILED", exception.safeMessage())
            stopSelf()
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            stopSelf()
            return START_NOT_STICKY
        }
        if (!acceptingEvents) {
            val requested = intent?.getLongExtra("summaryWindowMs", 600_000L) ?: 600_000L
            val interval = if (requested == 5_000L || requested == 600_000L) requested else 600_000L
            durationMs = intent?.getLongExtra("summaryDurationMs", 0L)?.coerceIn(0L, 3_600_000L) ?: 0L
            runId = (intent?.getStringExtra("summaryRunId") ?: UUID.randomUUID().toString()).replace(Regex("[^A-Za-z0-9_-]"), "_").take(80)
            val directory = File(filesDir,"platform/summaries").also { it.mkdirs() }
            summaryFile = File(directory,"$runId.jsonl")
            if (summaryFile!!.exists()) { stopSelf(); return START_NOT_STICKY }
            accumulator = SensorWindowAccumulator(interval, SystemClock.elapsedRealtime()+2_000L, System.currentTimeMillis()+2_000L)
            SensorRepository.onServiceStarted()
            acceptingEvents = true
            startSensors()
            sensorHandler?.post(ticker)
        }
        // A restarted process must explicitly start a new run: never hide lost windows.
        return START_NOT_STICKY
    }

    override fun onDestroy() {
        acceptingEvents = false
        sensorHandler?.removeCallbacks(ticker)
        SensorRepository.onServiceStopped()
        sensorManager?.unregisterListener(this)
        sensorManager = null
        sensorHandler = null
        sensorThread?.quitSafely()
        sensorThread = null
        disk.execute {
            File(filesDir,"platform/summaries/$runId.finished.json").writeText(JSONObject()
                .put("runId",runId).put("rows",rows).put("writeError",writeError)
                .put("stoppedAtEpochMs",System.currentTimeMillis()).put("rawPersisted",false).toString())
        }
        disk.shutdown()
        Log.i(TAG, "Foreground sensor service stopped")
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onSensorChanged(event: SensorEvent?) {
        if (!acceptingEvents || event == null) return
        val time = event.timestamp / 1_000_000L
        closeWindows(time)
        if (!acceptingEvents) return
        if(time < (accumulator?.startElapsedMs ?: Long.MAX_VALUE)) return
        SensorRepository.record(event)
        val values = event.values
        if(values.size < 3) return
        when(event.sensor.type) {
            Sensor.TYPE_ACCELEROMETER -> accumulator?.add("acceleration",time,values[0].toDouble(),values[1].toDouble(),values[2].toDouble())
            Sensor.TYPE_LINEAR_ACCELERATION -> accumulator?.add("linear",time,values[0].toDouble(),values[1].toDouble(),values[2].toDouble())
            Sensor.TYPE_GYROSCOPE -> accumulator?.add("gyro",time,values[0].toDouble(),values[1].toDouble(),values[2].toDouble())
            Sensor.TYPE_ROTATION_VECTOR -> {
                SensorManager.getRotationMatrixFromVector(matrix,values)
                SensorManager.getOrientation(matrix,angles)
                accumulator?.add("orientation",time,angles[0].toDouble(),angles[1].toDouble(),angles[2].toDouble())
            }
        }
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit

    private fun startSensors() {
        val manager = getSystemService(SENSOR_SERVICE) as SensorManager
        sensorManager = manager
        sensorThread = HandlerThread("CapstoneSensorEvents").also { it.start() }
        sensorHandler = Handler(requireNotNull(sensorThread).looper)

        val registrations = JSONArray()
        val selected = SensorRepository.requestedSensors().filter { it.type in setOf(Sensor.TYPE_ACCELEROMETER,Sensor.TYPE_LINEAR_ACCELERATION,Sensor.TYPE_GYROSCOPE) }.toMutableList()
        manager.getDefaultSensor(Sensor.TYPE_ROTATION_VECTOR)?.let { selected.add(it) }
        selected.forEach { sensor ->
            if (!SensorRepository.shouldRegister(sensor)) {
                Log.i(TAG, "Skipping ${sensor.name}: permission required")
                return@forEach
            }
            val registered = manager.registerListener(
                this,
                sensor,
                SensorManager.SENSOR_DELAY_NORMAL,
                0,
                sensorHandler,
            )
            registrations.put(JSONObject().put("type",sensor.type).put("name",sensor.name).put("registered",registered)
                .put("wakeUp",sensor.isWakeUpSensor).put("reportingMode",sensor.reportingMode))
            Log.i(TAG, "register type=${sensor.type} name=${sensor.name} result=$registered")
        }
        disk.execute { File(filesDir,"platform/summaries/$runId.meta.json").writeText(JSONObject()
            .put("runId",runId).put("windowMs",accumulator!!.windowMs).put("durationMs",durationMs)
            .put("startElapsedMs",accumulator!!.startElapsedMs).put("startEpochMs",accumulator!!.startEpochMs)
            .put("purpose","DEVICE_BACKGROUND_SENSOR_EXPERIMENT_ONLY").put("healthConnectUsed",false)
            .put("rawPersisted",false).put("wakeLockUsed",false).put("sensors",registrations).toString()) }
    }

    private fun closeWindows(now:Long) {
        val a = accumulator ?: return
        val stop = if(durationMs>0) a.startElapsedMs+durationMs else Long.MAX_VALUE
        a.closeThrough(minOf(now,stop)).forEach { row ->
            val json=JSONObject().put("schemaVersion",1).put("runId",runId).put("index",row.index)
                .put("windowMs",a.windowMs).put("startEpochMs",row.startEpochMs).put("endEpochMs",row.endEpochMs)
                .put("startElapsedMs",row.startElapsedMs).put("endElapsedMs",row.endElapsedMs)
                .put("movementEpisodes",row.motionEpisodes).put("motionThresholdMps2",0.8).put("motionReleaseMps2",0.3)
                .put("accelerationSamples",row.accelerationCount).put("meanAccelerationMagnitudeMps2",row.accelerationMean ?: JSONObject.NULL)
                .put("maxAccelerationMagnitudeMps2",row.accelerationMax ?: JSONObject.NULL)
                .put("linearAccelerationSamples",row.linearCount).put("meanLinearAccelerationMagnitudeMps2",row.linearMean ?: JSONObject.NULL)
                .put("gyroSamples",row.gyroCount).put("meanAngularSpeedRadPerSec",row.gyroMean ?: JSONObject.NULL)
                .put("orientationSamples",row.orientationCount).put("meanOrientationDegrees",JSONArray(row.meanAngles.map { it ?: JSONObject.NULL }))
                .put("orientationConcentration",JSONArray(row.angleConcentration.map { it ?: JSONObject.NULL }))
                .put("orientationAxes",JSONArray(listOf("azimuth","pitch","roll")))
                .put("firstEventElapsedMs",row.firstEventElapsedMs ?: JSONObject.NULL).put("lastEventElapsedMs",row.lastEventElapsedMs ?: JSONObject.NULL)
                .put("maxEventGapMs",row.maxEventGapMs).put("lateSamplesDropped",a.lateSamples)
                .put("screenInteractiveAtFlush",(getSystemService(POWER_SERVICE) as PowerManager).isInteractive)
                .put("flushedAtEpochMs",System.currentTimeMillis())
                .put("status",if(row.accelerationCount+row.gyroCount+row.orientationCount==0L)"NO_SAMPLES" else "OBSERVED")
            val line=json.toString()+"\n"
            disk.execute {
                try { val file=requireNotNull(summaryFile)
                    check(file.length()+line.toByteArray().size <= 1024*1024) { "Summary log limit reached" }
                    file.appendText(line); rows++
                } catch (_:Exception) {writeError=true;stopSelf()}
            }
        }
        if(now>=stop) { acceptingEvents=false;stopSelf() }
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(
            CHANNEL_ID,
            "Background wellness sensors",
            NotificationManager.IMPORTANCE_LOW,
        ).apply {
            description = "Shows when the app is collecting device sensor samples"
            setShowBadge(false)
        }
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    private fun buildNotification(): Notification {
        val launchIntent = packageManager.getLaunchIntentForPackage(packageName)
        val pendingIntent = launchIntent?.let {
            PendingIntent.getActivity(
                this,
                0,
                it,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
            )
        }
        val stopIntent = Intent(this, SensorForegroundService::class.java).setAction(ACTION_STOP)
        val stopPendingIntent = PendingIntent.getService(
            this,
            1,
            stopIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        return Notification.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_menu_compass)
            .setContentTitle("Wellness sensor collection")
            .setContentText("Saving interval summaries of device motion (no raw log)")
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .setCategory(Notification.CATEGORY_SERVICE)
            .addAction(Notification.Action.Builder(null, "Stop", stopPendingIntent).build())
            .build()
    }

    private fun promoteToForeground(notification: Notification) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH,
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

}
