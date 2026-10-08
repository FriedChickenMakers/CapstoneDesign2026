package com.capstonedesign2026.platform

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.health.connect.HealthConnectManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.health.connect.client.HealthConnectClient
import org.json.JSONObject
import java.lang.ref.WeakReference

object AndroidPlatformBridge {
    private const val RUNTIME_PERMISSION_REQUEST_CODE = 2602

    private var activityReference = WeakReference<Activity>(null)
    private var appContext: Context? = null
    private var unityVersion = "unknown"
    private var inputMode = "LIVE"

    @JvmStatic
    fun initialize(activity: Activity, unityVersionValue: String, inputModeValue: String): String = try {
        activityReference = WeakReference(activity)
        appContext = activity.applicationContext
        unityVersion = unityVersionValue
        inputMode = inputModeValue
        SensorRepository.initialize(activity.applicationContext)
        HealthRepository.initialize(activity.applicationContext)
        resultJson(PlatformStatus.AVAILABLE, "Android platform bridge initialized")
    } catch (exception: Exception) {
        resultJson(PlatformStatus.ERROR, exception.safeMessage(), "BRIDGE_INITIALIZE_FAILED")
    }

    @JvmStatic
    fun getPlatformSnapshotJson(): String {
        val context = appContext
            ?: return JSONObject()
                .put("status", PlatformStatus.SERVICE_UNAVAILABLE)
                .put("message", "Bridge is not initialized")
                .toString()
        return try {
            val sensorSnapshot = SensorRepository.snapshotJson()
            val healthSnapshot = HealthRepository.snapshotJson()
            JSONObject()
                .put("status", PlatformStatus.AVAILABLE)
                .put("message", "")
                .put("generatedAtEpochMs", System.currentTimeMillis())
                .put("inputMode", inputMode)
                .put("device", deviceJson(context))
                .put("sensorService", sensorSnapshot)
                .put("dailyAcceleration", DailyAccelerationStatus.read(context))
                .put("healthConnect", healthSnapshot)
                .put("runtimePermissions", runtimePermissionsJson(context))
                .toString()
        } catch (exception: Exception) {
            JSONObject()
                .put("status", PlatformStatus.ERROR)
                .put("message", exception.safeMessage())
                .put("errorCode", "SNAPSHOT_FAILED")
                .toString()
        }
    }

    @JvmStatic
    fun startSensorSummaryExperiment(activity: Activity, windowMs: Long, durationMs: Long, runId: String): String {
        if (activity.applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE == 0)
            return resultJson(PlatformStatus.ERROR,"Development build required")
        val intent=Intent(activity,SensorForegroundService::class.java)
            .putExtra("summaryWindowMs",windowMs).putExtra("summaryDurationMs",durationMs).putExtra("summaryRunId",runId)
        activity.startForegroundService(intent)
        return resultJson(PlatformStatus.AVAILABLE,"Device summary experiment started")
    }

    @JvmStatic
    fun startDailyAccelerationTrial(activity: Activity): String = try {
        activity.startForegroundService(Intent(activity, DailyAccelerationService::class.java))
        resultJson(PlatformStatus.AVAILABLE, "Daily acceleration trial start requested")
    } catch (exception: Exception) {
        resultJson(PlatformStatus.ERROR, exception.safeMessage(), "DAILY_TRIAL_START_FAILED")
    }

    @JvmStatic
    fun openSensorHistory(activity: Activity): String = try {
        activity.startActivity(Intent(activity, SensorHistoryActivity::class.java))
        resultJson(PlatformStatus.AVAILABLE, "Hourly heart-rate chart opened")
    } catch (exception: Exception) {
        resultJson(PlatformStatus.ERROR, exception.safeMessage(), "CHART_OPEN_FAILED")
    }

    @JvmStatic
    fun captureDebugSensors(): String = SensorRepository.captureDebugBurst()

    @JvmStatic
    fun startAutomaticPermissionFlow(activity: Activity): String = try {
        activity.runOnUiThread {
            activity.startActivity(Intent(activity, HealthPermissionActivity::class.java)
                .putExtra("automatic", true))
        }
        resultJson(PlatformStatus.AVAILABLE, "Missing permissions are being checked")
    } catch (exception: Exception) {
        resultJson(PlatformStatus.ERROR, exception.safeMessage(), "AUTO_PERMISSION_FLOW_FAILED")
    }

    @JvmStatic
    fun openHealthPermissionSettings(): String {
        val activity = activityReference.get()
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Unity activity is unavailable")
        return try {
            activity.runOnUiThread {
                val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE)
                    Intent(HealthConnectManager.ACTION_MANAGE_HEALTH_PERMISSIONS)
                        .putExtra(Intent.EXTRA_PACKAGE_NAME, activity.packageName)
                else Intent(HealthConnectClient.ACTION_HEALTH_CONNECT_SETTINGS)
                try {
                    activity.startActivity(intent)
                } catch (_: Exception) {
                    activity.startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:${activity.packageName}")))
                }
            }
            resultJson(PlatformStatus.AVAILABLE, "Health permission settings opened")
        } catch (exception: Exception) {
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "HEALTH_SETTINGS_FAILED")
        }
    }

    @JvmStatic
    fun startBackgroundSensorService(): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        return try {
            val intent = Intent(context, DailyAccelerationService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
            resultJson(PlatformStatus.AVAILABLE, "Sensor service start requested")
        } catch (security: SecurityException) {
            resultJson(PlatformStatus.PERMISSION_DENIED, security.safeMessage(), "FGS_PERMISSION_DENIED")
        } catch (exception: Exception) {
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "FGS_START_FAILED")
        }
    }

    @JvmStatic
    fun stopBackgroundSensorService(): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        return try {
            context.startForegroundService(Intent(context, DailyAccelerationService::class.java)
                .setAction(DailyAccelerationService.ACTION_STOP))
            resultJson(PlatformStatus.AVAILABLE, "가속도 수집 중지 요청됨")
        } catch (exception: Exception) {
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "FGS_STOP_FAILED")
        }
    }

    @JvmStatic
    fun stopLegacyAutoAccelerationTrial(): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        val preferences = context.getSharedPreferences("daily_acceleration_trial", Context.MODE_PRIVATE)
        if (DailyAccelerationStatus.read(context).optString("status") != "UNKNOWN")
            return resultJson(PlatformStatus.AVAILABLE, "Manual collector state retained")
        if (preferences.getBoolean("userEnabled", false))
            return resultJson(PlatformStatus.AVAILABLE, "User-enabled acceleration trial retained")
        return try {
            preferences.edit().putBoolean("active", false).commit()
            val stopped = context.stopService(Intent(context, DailyAccelerationService::class.java))
            resultJson(PlatformStatus.AVAILABLE,
                if (stopped) "Old automatic acceleration trial stopped" else "No automatic trial running")
        } catch (exception: Exception) {
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "AUTO_TRIAL_STOP_FAILED")
        }
    }

    @JvmStatic
    fun requestSensorPermissions(): String {
        val activity = activityReference.get()
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Unity activity is unavailable")
        val permissions = mutableListOf<String>()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q &&
            activity.checkSelfPermission(Manifest.permission.ACTIVITY_RECOGNITION) !=
            PackageManager.PERMISSION_GRANTED
        ) {
            permissions.add(Manifest.permission.ACTIVITY_RECOGNITION)
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
            activity.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) !=
            PackageManager.PERMISSION_GRANTED
        ) {
            permissions.add(Manifest.permission.POST_NOTIFICATIONS)
        }
        if (permissions.isEmpty()) {
            return resultJson(PlatformStatus.AVAILABLE, "Sensor and notification permissions are granted")
        }
        return try {
            val requestable = permissions.filter { !PermissionState.wasRuntimeRequested(activity, it) }
            activity.runOnUiThread {
                if (requestable.isEmpty()) {
                    activity.startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:${activity.packageName}")))
                } else {
                    requestable.forEach { PermissionState.markRuntimeRequested(activity, it) }
                    activity.requestPermissions(requestable.toTypedArray(), RUNTIME_PERMISSION_REQUEST_CODE)
                }
            }
            resultJson(PlatformStatus.PERMISSION_REQUIRED,
                if (requestable.isEmpty()) "App permission settings opened" else "Runtime permission request opened")
        } catch (exception: Exception) {
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "RUNTIME_PERMISSION_REQUEST_FAILED")
        }
    }

    @JvmStatic
    fun requestHealthConnectPermissions(): String {
        val activity = activityReference.get()
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Unity activity is unavailable")
        val (status, message) = HealthRepository.availabilityStatus()
        if (status != PlatformStatus.AVAILABLE) return resultJson(status, message)
        return try {
            activity.runOnUiThread {
                activity.startActivity(Intent(activity, HealthPermissionActivity::class.java))
            }
            resultJson(PlatformStatus.PERMISSION_REQUIRED, "Health Connect permission request opened")
        } catch (exception: Exception) {
            resultJson(PlatformStatus.ERROR, exception.safeMessage(), "HEALTH_PERMISSION_REQUEST_FAILED")
        }
    }

    @JvmStatic
    fun refreshHealthData(): String = HealthRepository.refresh()

    @JvmStatic
    fun refreshHealthRange(startEpochMs: Long, endEpochMs: Long): String =
        HealthRepository.refreshRange(startEpochMs, endEpochMs)

    @JvmStatic
    fun refreshStepsRange(startEpochMs: Long, endEpochMs: Long, requestId: String): String =
        HealthRepository.refreshStepsRange(startEpochMs, endEpochMs, requestId)

    @JvmStatic
    fun getStepRangeSnapshotJson(): String = HealthRepository.stepRangeSnapshot()

    @JvmStatic
    fun scheduleMissionRewardReminder(sessionId: String, startEpochMs: Long, goalSteps: Int, virtualSteps: Long): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        val result = MissionRewardNotifications.schedule(context, sessionId, startEpochMs, goalSteps, virtualSteps)
        if (MissionRewardNotifications.permissionPromptNeeded(context)) {
            activityReference.get()?.runOnUiThread {
                activityReference.get()?.let { activity ->
                    activity.startActivity(Intent(activity, HealthPermissionActivity::class.java)
                        .putExtra("rewardReminder", true))
                }
            }
        }
        return result
    }

    @JvmStatic
    fun cancelMissionRewardReminder(sessionId: String): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        return MissionRewardNotifications.cancel(context, sessionId)
    }

    @JvmStatic
    fun notifyMissionRewardReady(sessionId: String): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        return MissionRewardNotifications.postIfReady(context, sessionId)
    }

    @JvmStatic
    fun getRecentSensorSamplesJson(limit: Int): String = SensorRepository.recentSamplesJson(limit)

    private fun deviceJson(context: Context): JSONObject {
        val packageInfo = context.packageManager.getPackageInfo(context.packageName, 0)
        return JSONObject()
            .put("status", PlatformStatus.AVAILABLE)
            .put("manufacturer", Build.MANUFACTURER)
            .put("model", Build.MODEL)
            .put("androidVersion", Build.VERSION.RELEASE)
            .put("androidApi", Build.VERSION.SDK_INT)
            .put("appVersion", packageInfo.versionName ?: "unknown")
            .put("unityVersion", unityVersion)
    }

    private fun runtimePermissionsJson(context: Context): JSONObject {
        val activityRecognition = runtimePermissionStatus(
            context,
            Manifest.permission.ACTIVITY_RECOGNITION,
            Build.VERSION_CODES.Q,
        )
        val notifications = runtimePermissionStatus(
            context,
            Manifest.permission.POST_NOTIFICATIONS,
            Build.VERSION_CODES.TIRAMISU,
        )
        return JSONObject()
            .put("activityRecognition", activityRecognition)
            .put("notifications", notifications)
    }

    private fun runtimePermissionStatus(
        context: Context,
        permission: String,
        introducedApi: Int,
    ): String {
        if (Build.VERSION.SDK_INT < introducedApi ||
            context.checkSelfPermission(permission) == PackageManager.PERMISSION_GRANTED
        ) {
            return PlatformStatus.AVAILABLE
        }
        return if (PermissionState.wasRuntimeRequested(context, permission)) {
            PlatformStatus.PERMISSION_DENIED
        } else {
            PlatformStatus.PERMISSION_REQUIRED
        }
    }
}
