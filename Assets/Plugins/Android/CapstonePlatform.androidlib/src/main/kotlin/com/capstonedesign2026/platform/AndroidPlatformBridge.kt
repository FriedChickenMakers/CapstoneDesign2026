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
    @JvmStatic fun startMissionPhoto(activity: Activity, id: String, target: String) = MissionPhotoController.start(activity, id, target)
    @JvmStatic fun pollMissionPhoto(activity: Activity, id: String) = MissionPhotoController.poll(activity, id)
    @JvmStatic fun missionPhotoDirectory(activity: Activity) = MissionPhotoController.root(activity).absolutePath
    @JvmStatic fun cancelMissionPhoto(activity: Activity, id: String) = MissionPhotoController.cancel(activity, id)
    @JvmStatic fun pruneMissionPhotos(activity: Activity, keep: String) = MissionPhotoController.prune(activity, keep)
    private const val RUNTIME_PERMISSION_REQUEST_CODE = 2602

    private var activityReference = WeakReference<Activity>(null)
    private var appContext: Context? = null
    private var unityVersion = "unknown"
    private var inputMode = "LIVE"

    @JvmStatic
    fun resetLocalData(activity: Activity, resetId: String): String {
        appContext = activity.applicationContext
        val result=NativeLocalReset.reset(activity.applicationContext, resetId)
        activity.runOnUiThread { notificationDialog?.dismiss(); notificationDialog=null; notificationDialogShowing=false; notificationNoticeShown="" }
        return result
    }

    @JvmStatic
    fun initialize(activity: Activity, unityVersionValue: String, inputModeValue: String): String { return try {
        if (activityReference.get() !== activity) {
            notificationDialogShowing = false
            notificationNoticeShown = ""
        }
        activityReference = WeakReference(activity)
        appContext = activity.applicationContext
        unityVersion = unityVersionValue
        inputMode = inputModeValue
        if (NativeLocalReset.pending(activity)) return resultJson(PlatformStatus.ERROR, "Local reset is pending", "LOCAL_RESET_PENDING")
        MeasurementAppVisits.initialize(activity)
        if (inputMode != "LIVE") MeasurementTelemetry.suspend()
        SensorRepository.initialize(activity.applicationContext)
        HealthRepository.initialize(activity.applicationContext)
        ActivityNotifications.initialize(activity.applicationContext, inputModeValue == "LIVE")
        if (inputModeValue == "LIVE") showActivityNotificationEntryPrompt()
        resultJson(PlatformStatus.AVAILABLE, "Android platform bridge initialized")
    } catch (exception: Exception) {
        resultJson(PlatformStatus.ERROR, exception.safeMessage(), "BRIDGE_INITIALIZE_FAILED")
    } }

    @JvmStatic
    fun getPlatformSnapshotJson(): String = platformSnapshotJson(false)

    @JvmStatic
    fun getSettingsSnapshotJson(): String = platformSnapshotJson(true)

    private fun platformSnapshotJson(settingsOnly: Boolean): String {
        val context = appContext
            ?: return JSONObject()
                .put("status", PlatformStatus.SERVICE_UNAVAILABLE)
                .put("message", "Bridge is not initialized")
                .toString()
        return try {
            val healthSnapshot = if (settingsOnly) HealthRepository.settingsSnapshotJson()
                else HealthRepository.snapshotJson()
            JSONObject()
                .put("status", PlatformStatus.AVAILABLE)
                .put("message", "")
                .put("generatedAtEpochMs", System.currentTimeMillis())
                .put("inputMode", inputMode)
                .apply {
                    if (!settingsOnly) {
                        put("device", deviceJson(context))
                        put("sensorService", SensorRepository.snapshotJson())
                    }
                }
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
        if (NativeLocalReset.pending(activity)) return resultJson(PlatformStatus.NO_DATA,"Local reset is pending")
        if (activity.applicationInfo.flags and android.content.pm.ApplicationInfo.FLAG_DEBUGGABLE == 0)
            return resultJson(PlatformStatus.ERROR,"Development build required")
        val intent=Intent(activity,SensorForegroundService::class.java)
            .putExtra("summaryWindowMs",windowMs).putExtra("summaryDurationMs",durationMs).putExtra("summaryRunId",runId)
        activity.startForegroundService(intent)
        return resultJson(PlatformStatus.AVAILABLE,"Device summary experiment started")
    }

    @JvmStatic
    fun startDailyAccelerationTrial(activity: Activity): String = try {
        if (NativeLocalReset.pending(activity)) resultJson(PlatformStatus.NO_DATA,"Local reset is pending")
        else {
            activity.startForegroundService(Intent(activity, DailyAccelerationService::class.java))
            resultJson(PlatformStatus.AVAILABLE, "Daily acceleration trial start requested")
        }
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
        val epoch=NativeLocalReset.epoch(activity)
        activity.runOnUiThread {
            if(!NativeLocalReset.current(activity,epoch))return@runOnUiThread
            activity.startActivity(Intent(activity, HealthPermissionActivity::class.java)
                .putExtra("automatic", true).putExtra("localDataEpoch",epoch))
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
        if (NativeLocalReset.pending(context)) return resultJson(PlatformStatus.NO_DATA,"Local reset is pending")
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
        if (NativeLocalReset.pending(context)) {
            context.stopService(Intent(context, DailyAccelerationService::class.java))
            return resultJson(PlatformStatus.NO_DATA,"Local reset is pending")
        }
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
        val epoch=NativeLocalReset.epoch(context)
        if(!NativeLocalReset.current(context,epoch))return resultJson(PlatformStatus.NO_DATA,"Local reset is pending")
        val preferences = context.getSharedPreferences("daily_acceleration_trial", Context.MODE_PRIVATE)
        if (DailyAccelerationStatus.read(context).optString("status") != "UNKNOWN")
            return resultJson(PlatformStatus.AVAILABLE, "Manual collector state retained")
        if (preferences.getBoolean("userEnabled", false))
            return resultJson(PlatformStatus.AVAILABLE, "User-enabled acceleration trial retained")
        return try {
            if(!NativeLocalReset.write(context,epoch) { check(preferences.edit().putBoolean("active", false).commit()) })
                return resultJson(PlatformStatus.NO_DATA,"Local reset invalidated this request")
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
            val epoch=NativeLocalReset.epoch(activity)
            activity.runOnUiThread {
                if(!NativeLocalReset.current(activity,epoch))return@runOnUiThread
                if (requestable.isEmpty()) {
                    activity.startActivity(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,
                        Uri.parse("package:${activity.packageName}")))
                } else {
                    requestable.forEach { PermissionState.markRuntimeRequested(activity, it,epoch) }
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
            val epoch=NativeLocalReset.epoch(activity)
            activity.runOnUiThread {
                if(!NativeLocalReset.current(activity,epoch))return@runOnUiThread
                activity.startActivity(Intent(activity, HealthPermissionActivity::class.java).putExtra("localDataEpoch",epoch))
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
    fun refreshStepsRange(startEpochMs: Long, endEpochMs: Long, requestId: String, aggregate: Boolean): String =
        HealthRepository.refreshStepsRange(startEpochMs, endEpochMs, requestId, aggregate)

    @JvmStatic
    fun getStepRangeSnapshotJson(): String = HealthRepository.stepRangeSnapshot()

    @JvmStatic
    fun scheduleMissionRewardReminder(sessionId: String, startEpochMs: Long, goalSteps: Int, virtualSteps: Long, endEpochMs: Long, aggregate: Boolean): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        val result = MissionRewardNotifications.schedule(context, sessionId, startEpochMs, goalSteps, virtualSteps, endEpochMs, aggregate)
        val epoch=NativeLocalReset.epoch(context)
        if (MissionRewardNotifications.permissionPromptNeeded(context)) {
            activityReference.get()?.runOnUiThread {
                if(!NativeLocalReset.current(context,epoch))return@runOnUiThread
                activityReference.get()?.let { activity ->
                    activity.startActivity(Intent(activity, HealthPermissionActivity::class.java)
                        .putExtra("rewardReminder", true).putExtra("localDataEpoch",epoch))
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
    fun acknowledgeMissionAchievement(sessionId: String): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        return MissionRewardNotifications.acknowledge(context, sessionId)
    }

    @JvmStatic
    fun notifyMissionRewardReady(sessionId: String, steps: Long): String {
        val context = appContext
            ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")
        if (steps >= 0) MissionRewardNotifications.recordReady(context, sessionId, steps, "APP_VERIFIED_HEALTH_CONNECT")
        return MissionRewardNotifications.postIfReady(context, sessionId)
    }

    @JvmStatic
    fun getWalkingAchievementJson(): String = appContext?.let { MissionRewardNotifications.achievement(it) } ?: "{}"

    @JvmStatic
    fun configureDebugTelemetry(enabled: Boolean, qa: Boolean, build: String): String =
        appContext?.let { DebugTelemetry.configure(it,enabled,qa,build) } ?: "{}"
    @JvmStatic
    fun getMeasurementContext(): String = appContext?.let { DebugTelemetry.context(it).toString() } ?: "{}"
    @JvmStatic
    fun recordMeasurementTransport(category: String) { appContext?.let { DebugTelemetry.transport(it,category) } }
    @JvmStatic
    fun recordDetailedMeasurement(kind: String, session: String, activity: String, at: Long, offset: Int,
                                  metadata: String, details: String, discriminator: String): String =
        if(inputMode=="LIVE")appContext?.let { MeasurementTelemetry.detailed(it,kind,session,activity,at,offset,metadata,details,discriminator) } ?: "{}" else "{}"

    @JvmStatic
    fun suspendMeasurements(): String = MeasurementTelemetry.suspend()
    @JvmStatic
    fun configureMeasurements(binding: String, enabled: Boolean, since: Long): String =
        if(enabled && inputMode != "LIVE") { MeasurementTelemetry.suspend(); "{\"ok\":false}" }
        else appContext?.let { MeasurementTelemetry.configure(it, binding, enabled, since) } ?: "{}"
    @JvmStatic
    fun recordMeasuredFocusRecommendation(kind: String, decision: String, session: String, recommended: String, actual: String,
                                         policy: String, reason: String, occurred: Long, offset: Int): String =
        if (inputMode == "LIVE") appContext?.let { MeasurementTelemetry.focusRecommendation(it, kind, decision, session, recommended, actual, policy, reason, occurred, offset) } ?: "{}" else "{}"
    @JvmStatic
    fun recordMeasuredFocusRecommendationV2(kind: String, decision: String, session: String, recommended: String, actual: String,
                                           policy: String, reason: String, occurred: Long, offset: Int, metadata: String): String =
        if(inputMode=="LIVE")appContext?.let { MeasurementTelemetry.focusRecommendation(it,kind,decision,session,recommended,actual,policy,reason,occurred,offset,metadata) } ?: "{}" else "{}"
    @JvmStatic
    fun getMeasurementBatch(binding: String): String = appContext?.let { MeasurementTelemetry.batch(it, binding) } ?: "{}"

    @JvmStatic
    fun recordMeasuredAppSession(session: String): String =
        if (inputMode == "LIVE") appContext?.let { MeasurementTelemetry.appSession(it, session) } ?: "{}" else "{}"

    @JvmStatic
    fun getMeasuredAppVisit(): String = if (inputMode == "LIVE") MeasurementAppVisits.current() else "{}"
    @JvmStatic
    fun acknowledgeMeasurements(binding: String, collection: String, accepted: String, sent: String): String =
        appContext?.let { MeasurementTelemetry.acknowledge(it, binding, collection, accepted, sent) } ?: "{}"
    @JvmStatic
    fun recordMeasuredActivity(kind: String, session: String, activity: String, occurred: Long, offset: Int): String =
        if (inputMode == "LIVE") appContext?.let { MeasurementTelemetry.activity(it, kind, session, activity, occurred, offset) } ?: "{}" else "{}"

    @JvmStatic
    fun getActivityNotificationSettingsJson(): String = appContext?.let { ActivityNotifications.settings(it).toString() } ?: "{}"

    @JvmStatic
    fun configureActivityNotificationOptions(enabled: Boolean, adaptive: Boolean): String =
        appContext?.let { ActivityNotifications.configureOptions(it, enabled, adaptive) }
            ?: resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")

    @JvmStatic
    fun configureActivityNotifications(enabled: Boolean, minute: Int, adaptive: Boolean): String =
        appContext?.let { ActivityNotifications.configure(it, enabled, minute, adaptive) }
            ?: resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")

    // These dialogs edit the existing daily reminder. There is no separate one-off mode.
    private var notificationDialog: android.app.Dialog? = null
    private var notificationDialogShowing = false
    private var notificationNoticeShown = ""

    @JvmStatic
    fun showActivityNotificationEntryPrompt(): String {
        val activity = activityReference.get() ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Activity unavailable")
        if (inputMode != "LIVE") return resultJson(PlatformStatus.UNSUPPORTED, "Live Android mode required")
        val epoch=NativeLocalReset.epoch(activity)
        activity.runOnUiThread {
            if(!NativeLocalReset.current(activity,epoch))return@runOnUiThread
            if (activity.isFinishing || activity.isDestroyed || notificationDialogShowing) return@runOnUiThread
            try {
                val current = ActivityNotifications.settings(activity)
                if (!current.optBoolean("timeChosen", false)) {
                    showInitialNotificationTimePrompt(activity, epoch)
                    return@runOnUiThread
                }
                val permission = current.optString("permissionStatus")
                val blocked = permission == PlatformStatus.PERMISSION_REQUIRED || permission == PlatformStatus.PERMISSION_DENIED
                val failed = current.optString("lastDeliveryStatus") == "failed"
                val notice = if (blocked) permission else if (failed) "failed:" + current.optLong("lastDeliveryStatusEpochMs") else ""
                if (!current.optBoolean("enabled", true) || notice.isEmpty()) { notificationNoticeShown = ""; return@runOnUiThread }
                if (notice == notificationNoticeShown) return@runOnUiThread
                notificationDialogShowing = true
                notificationDialog = android.app.AlertDialog.Builder(activity)
                    .setTitle("알림을 확인해 주세요")
                    .setMessage(if (blocked) "기기에서 활동 알림이 허용되지 않았어요. 알림을 받으려면 권한을 확인해 주세요."
                        else "최근 활동 알림의 게시를 확인하지 못했어요. 자동으로 다시 확인하며, 알림 설정도 확인할 수 있어요.")
                    .setPositiveButton("알림 설정") { _, _ ->
                        if(!NativeLocalReset.current(activity,epoch))return@setPositiveButton
                        notificationNoticeShown = notice
                        if (blocked) ActivityNotifications.requestPermission(activity)
                        else activity.startActivity(Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                            .putExtra(Settings.EXTRA_APP_PACKAGE, activity.packageName))
                    }
                    .setNegativeButton("확인") { _, _ -> notificationNoticeShown = notice }
                    .setOnCancelListener { notificationNoticeShown = notice }
                    .setOnDismissListener { notificationDialogShowing = false }
                    .show()
            } catch (_: Exception) { notificationDialogShowing = false }
        }
        return resultJson(PlatformStatus.AVAILABLE, "Notification entry state checked")
    }

    private fun showInitialNotificationTimePrompt(activity: Activity, epoch: String) {
        if(!NativeLocalReset.current(activity,epoch))return
        notificationDialogShowing = true
        notificationDialog = android.app.AlertDialog.Builder(activity)
            .setTitle("알림을 받을 시각을 정해 주세요")
            .setMessage("하루 한 번, 편한 시간에 활동을 알려드려요. 정하지 않으면 낮 12시에 시작하며 설정에서 바꿀 수 있어요. 알림시간 자동 최적화는 기본으로 켜져 있어요.")
            .setCancelable(false)
            .setPositiveButton("시각 설정") { _, _ ->
                if(!NativeLocalReset.current(activity,epoch))return@setPositiveButton
                notificationDialogShowing = false
                showNotificationTimePicker(activity, initial = true, epoch = epoch)
            }
            .setNegativeButton("아직 정하지 못했어요") { _, _ ->
                if(!NativeLocalReset.current(activity,epoch))return@setNegativeButton
                notificationDialogShowing = false
                val current = ActivityNotifications.settings(activity)
                val result = ActivityNotifications.configure(activity, current.optBoolean("enabled", true),
                    ActivityReminderPolicy.DEFAULT_MINUTE, current.optBoolean("adaptive", true), timeSelected = true, adjustmentSource = "default", expectedEpoch = epoch)
                if (JSONObject(result).optString("status") != PlatformStatus.AVAILABLE) notificationSaveError(activity)
            }.show()
    }

    private fun notificationSaveError(activity: Activity) {
        android.widget.Toast.makeText(activity, "알림 시간을 저장하지 못했어요. 다시 시도해 주세요.", android.widget.Toast.LENGTH_LONG).show()
    }

    @JvmStatic
    fun showActivityNotificationTimePicker(): String {
        val activity = activityReference.get() ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Activity unavailable")
        val epoch=NativeLocalReset.epoch(activity)
        activity.runOnUiThread {
            if(!NativeLocalReset.current(activity,epoch))return@runOnUiThread
            if (!notificationDialogShowing && !activity.isFinishing && !activity.isDestroyed) try {
                showNotificationTimePicker(activity, initial = false, epoch = epoch)
            } catch (_: Exception) { notificationDialogShowing = false; notificationSaveError(activity) }
        }
        return resultJson(PlatformStatus.AVAILABLE, "Notification time picker opened")
    }

    private fun showNotificationTimePicker(activity: Activity, initial: Boolean, epoch: String) {
        if(!NativeLocalReset.current(activity,epoch))return
        val minute = ActivityNotifications.settings(activity).optInt("minuteOfDay", ActivityReminderPolicy.DEFAULT_MINUTE)
        // Localize only this clock. Its initial value matches the time shown in Settings.
        val dialogContext = android.view.ContextThemeWrapper(activity, R.style.CapstoneTimePickerDialog).apply {
            applyOverrideConfiguration(android.content.res.Configuration().apply { setLocale(java.util.Locale.KOREAN) })
        }
        notificationDialogShowing = true
        var completed = false
        notificationDialog = android.app.TimePickerDialog(dialogContext, R.style.CapstoneTimePickerDialog, { _, hour, selectedMinute ->
            if(!NativeLocalReset.current(activity,epoch))return@TimePickerDialog
            val current = ActivityNotifications.settings(activity)
            val result = ActivityNotifications.configure(activity, current.optBoolean("enabled", true),
                hour * 60 + selectedMinute, current.optBoolean("adaptive", true), timeSelected = true, expectedEpoch = epoch)
            completed = JSONObject(result).optString("status") == PlatformStatus.AVAILABLE
            if (!completed) notificationSaveError(activity)
        }, minute / 60, minute % 60, true).apply {
            setOnDismissListener {
                notificationDialogShowing = false
                if (initial && !completed && !activity.isFinishing && !activity.isDestroyed && NativeLocalReset.current(activity,epoch))
                    showInitialNotificationTimePrompt(activity, epoch)
            }
            show()
            getButton(android.content.DialogInterface.BUTTON_NEGATIVE).apply {
                text = "취소"; typeface = GardenTypography.face(activity, control = true)
            }
            getButton(android.content.DialogInterface.BUTTON_POSITIVE).apply {
                text = "선택"; typeface = GardenTypography.face(activity, emphasized = true, control = true)
            }
        }
    }

    @JvmStatic
    fun syncActivityReminder(completedDay: String, title: String, active: Boolean): String =
        appContext?.let { ActivityNotifications.sync(it, completedDay, title, active) }
            ?: resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Bridge is not initialized")

    @JvmStatic
    fun consumeActivityNotificationOpen(): String = appContext?.let { ActivityNotifications.consumeOpen(it) } ?: "{}"

    @JvmStatic
    fun requestActivityNotificationPermission(): String {
        val activity = activityReference.get() ?: return resultJson(PlatformStatus.SERVICE_UNAVAILABLE, "Activity unavailable")
        val epoch=NativeLocalReset.epoch(activity)
        activity.runOnUiThread { if(NativeLocalReset.current(activity,epoch))ActivityNotifications.requestPermission(activity) }
        return resultJson(PlatformStatus.AVAILABLE, "Notification permission checked")
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
