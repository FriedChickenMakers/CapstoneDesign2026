package com.capstonedesign2026.platform

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.HealthConnectFeatures
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import java.time.Instant
import java.util.concurrent.TimeUnit

/** Background goal evidence and achievement alerts. Unity alone commits game rewards. */
internal object MissionRewardNotifications {
    private const val PREFS = "mission_reward_reminder"
    private const val WORK = "capstone_m15_reward_ready"
    private const val CHANNEL = "capstone_mission_rewards"
    private const val NOTIFICATION_ID = 2615
    private val notificationLock = Any()

    private fun state(context: Context) = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun schedule(context: Context, sessionId: String, startEpochMs: Long, goalSteps: Int, virtualSteps: Long, endEpochMs: Long = Long.MAX_VALUE, aggregate: Boolean = false): String {
        synchronized(notificationLock) {
            val requested = WalkingAchievementPolicy.Window(sessionId, startEpochMs, goalSteps, virtualSteps, endEpochMs, aggregate)
            if (!WalkingAchievementPolicy.valid(requested))
                return resultJson(PlatformStatus.ERROR, "Invalid M15 reminder", "INVALID_REMINDER")
            val prefs = state(context)
            val previous = storedWindow(context)
            val previousSession = previous?.sessionId ?: ""
            val sameEvidence = previous == requested
            if (!sameEvidence && !prefs.edit().putString("sessionId", sessionId).putLong("startEpochMs", startEpochMs)
                    .putInt("goalSteps", goalSteps).putLong("virtualSteps", virtualSteps)
                    .putLong("endEpochMs", endEpochMs).putBoolean("aggregate", aggregate)
                    .remove("achievement").remove("notifiedSessionId").commit())
                return resultJson(PlatformStatus.ERROR, "Reminder could not be saved", "REMINDER_SAVE_FAILED")
            if (prefs.getString("notifiedSessionId", "") == sessionId)
                return resultJson(PlatformStatus.AVAILABLE, "M15 reminder already shown")
            return try {
                if (previousSession.isNotEmpty() && previousSession != sessionId)
                    context.getSystemService(NotificationManager::class.java)
                        .cancel("m15:$previousSession", NOTIFICATION_ID)
                val work = PeriodicWorkRequestBuilder<MissionRewardWorker>(15, TimeUnit.MINUTES).build()
                // Re-enqueue even for identical preferences. A process may have exited after
                // committing the target but before WorkManager persisted the first request.
                WorkManager.getInstance(context).enqueueUniquePeriodicWork(WORK,
                    if (sameEvidence) ExistingPeriodicWorkPolicy.KEEP else ExistingPeriodicWorkPolicy.UPDATE, work)
                resultJson(PlatformStatus.AVAILABLE, "M15 reminder scheduled")
            } catch (exception: Exception) {
                resultJson(PlatformStatus.ERROR, exception.safeMessage(), "REMINDER_SCHEDULE_FAILED")
            }
        }
    }

    /** Unity has committed the reward. Keep its achievement visible until the user dismisses it. */
    fun acknowledge(context: Context, sessionId: String): String = finish(context, sessionId, false)

    /** Explicitly abandoning/cancelling a mission also removes its visible notification. */
    fun cancel(context: Context, sessionId: String): String = finish(context, sessionId, true)

    private fun finish(context: Context, sessionId: String, dismiss: Boolean): String {
        synchronized(notificationLock) {
            val prefs = state(context)
            if (prefs.getString("sessionId", "") == sessionId) {
                if (!prefs.edit().remove("sessionId").remove("startEpochMs").remove("goalSteps")
                    .remove("virtualSteps").remove("notifiedSessionId").remove("endEpochMs").remove("aggregate").remove("achievement").commit())
                    return resultJson(PlatformStatus.ERROR, "Reminder completion could not be saved", "REMINDER_SAVE_FAILED")
                WorkManager.getInstance(context).cancelUniqueWork(WORK)
            }
            if (dismiss) context.getSystemService(NotificationManager::class.java)
                .cancel("m15:$sessionId", NOTIFICATION_ID)
            return resultJson(PlatformStatus.AVAILABLE, if (dismiss) "M15 reminder cancelled" else "M15 achievement acknowledged")
        }
    }

    fun achievement(context: Context): String = synchronized(notificationLock) {
        state(context).getString("achievement", "")?.takeIf { it.isNotBlank() } ?: "{}"
    }

    fun recordReady(context: Context, sessionId: String, steps: Long, source: String,
                    expected: WalkingAchievementPolicy.Window? = null): Boolean = synchronized(notificationLock) {
        val prefs = state(context)
        val current = storedWindow(context) ?: return false
        if (current.sessionId != sessionId || source.isBlank() ||
            !WalkingAchievementPolicy.canRecord(current, expected ?: current, steps)) return false
        val value = org.json.JSONObject().put("sessionId", sessionId).put("steps", steps).put("source", source)
            .put("startEpochMs", prefs.getLong("startEpochMs", 0)).put("endEpochMs", prefs.getLong("endEpochMs", Long.MAX_VALUE))
            .put("goalSteps", prefs.getInt("goalSteps", 0)).put("queryComplete", true)
            .put("achievedAtEpochMs", System.currentTimeMillis())
        prefs.edit().putString("achievement", value.toString()).commit()
    }

    fun postIfReady(context: Context, sessionId: String): String {
        synchronized(notificationLock) {
            val prefs = state(context)
            if (prefs.getString("sessionId", "") != sessionId)
                return resultJson(PlatformStatus.NO_DATA, "M15 session is no longer active")
            if (prefs.getString("notifiedSessionId", "") == sessionId)
                return resultJson(PlatformStatus.AVAILABLE, "M15 reminder already shown")
            if (!verifiedAchievement(context))
                return resultJson(PlatformStatus.NO_DATA, "No verified walking achievement")
            val at = System.currentTimeMillis()
            val measurement = MeasurementTelemetry.ticket(context, "walking:$sessionId", 0, "walking-goal-v1", -1, -1, 0)
            MeasurementTelemetry.notification(context, measurement, "notification_due", at)
            val permission = ActivityNotifications.permissionStatus(context)
            if (permission != PlatformStatus.AVAILABLE)
                MeasurementTelemetry.notification(context, measurement, "notification_blocked", at, permission.lowercase())
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
                return resultJson(PlatformStatus.PERMISSION_REQUIRED, "Notification permission is required")
            val manager = context.getSystemService(NotificationManager::class.java)
            if (!manager.areNotificationsEnabled())
                return resultJson(PlatformStatus.PERMISSION_DENIED, "App notifications are disabled")
            manager.createNotificationChannel(NotificationChannel(CHANNEL, "미션 보상", NotificationManager.IMPORTANCE_DEFAULT))
            if (manager.getNotificationChannel(CHANNEL)?.importance == NotificationManager.IMPORTANCE_NONE) {
                MeasurementTelemetry.notification(context, measurement, "notification_blocked", at, "channel_disabled")
                return resultJson(PlatformStatus.PERMISSION_DENIED, "Mission reward notifications are disabled")
            }
            val open = ActivityNotifications.openIntent(context, "walking", sessionId, at, measurement)
            val notification = Notification.Builder(context, CHANNEL)
                .setSmallIcon(android.R.drawable.ic_menu_mylocation)
                .setContentTitle("오늘의 걷기 목표를 달성했어요")
                .setContentText("차곡차곡 쌓인 걸음이 정원을 자라게 해요. 달성 기록을 확인해 보세요.")
                .setVisibility(Notification.VISIBILITY_PRIVATE).setOnlyAlertOnce(true)
                .setContentIntent(open).setAutoCancel(true)
                .setCategory(Notification.CATEGORY_REMINDER).build()
            return try {
                // Reusing the same tag and ID makes a retry replace the visible notification
                // if the process exits after notify() but before its dedupe marker is saved.
                manager.notify("m15:$sessionId", NOTIFICATION_ID, notification)
                MeasurementTelemetry.notification(context, measurement, "notification_post_accepted", System.currentTimeMillis())
                if (!prefs.edit().putString("notifiedSessionId", sessionId).commit())
                    return resultJson(PlatformStatus.ERROR, "Reminder state could not be saved", "REMINDER_STATE_SAVE_FAILED")
                WorkManager.getInstance(context).cancelUniqueWork(WORK)
                resultJson(PlatformStatus.AVAILABLE, "M15 reward reminder shown")
            } catch (exception: Exception) {
                MeasurementTelemetry.notification(context, measurement, "notification_failed", System.currentTimeMillis(), "notify_exception")
                resultJson(PlatformStatus.ERROR, exception.safeMessage(), "REMINDER_POST_FAILED")
            }
        }
    }

    fun permissionPromptNeeded(context: Context): Boolean {
        val prefs = state(context)
        if (prefs.getBoolean("prompted", false)) return false
        prefs.edit().putBoolean("prompted", true).apply()
        return true
    }

    private fun storedWindow(context: Context): WalkingAchievementPolicy.Window? {
        val prefs = state(context)
        val id = prefs.getString("sessionId", "") ?: ""
        if (id.isEmpty()) return null
        return WalkingAchievementPolicy.Window(id, prefs.getLong("startEpochMs", 0L), prefs.getInt("goalSteps", 0),
            prefs.getLong("virtualSteps", 0L), prefs.getLong("endEpochMs", Long.MAX_VALUE), prefs.getBoolean("aggregate", false))
    }
    fun hasReady(context: Context, sessionId: String): Boolean = synchronized(notificationLock) {
        storedWindow(context)?.sessionId == sessionId && verifiedAchievement(context)
    }
    private fun verifiedAchievement(context: Context): Boolean {
        val current = storedWindow(context) ?: return false
        return try {
            val value = org.json.JSONObject(state(context).getString("achievement", "") ?: "")
            value.optString("sessionId") == current.sessionId && value.optLong("startEpochMs") == current.startEpochMs &&
                value.optLong("endEpochMs") == current.endEpochMs && value.optInt("goalSteps") == current.goalSteps &&
                value.optBoolean("queryComplete") && value.optString("source").isNotBlank() &&
                WalkingAchievementPolicy.canRecord(current, current, value.optLong("steps", -1))
        } catch (_: Exception) { false }
    }
    fun active(context: Context): WalkingAchievementPolicy.Window? = synchronized(notificationLock) {
        val window = storedWindow(context) ?: return null
        if (state(context).getString("notifiedSessionId", "") == window.sessionId || !WalkingAchievementPolicy.valid(window)) null else window
    }
}

class MissionRewardWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        val active = MissionRewardNotifications.active(applicationContext) ?: return Result.success()
        val (sessionId, startEpochMs, goalSteps, virtualSteps, endEpochMs, aggregate) = active
        // A notification permission/channel may have been disabled when evidence arrived.
        // Retry the existing achievement without requiring a second health query or permission.
        if (MissionRewardNotifications.hasReady(applicationContext, sessionId)) {
            MissionRewardNotifications.postIfReady(applicationContext, sessionId)
            return Result.success()
        }
        if (startEpochMs <= 0L || goalSteps <= 0 || startEpochMs >= System.currentTimeMillis()) return Result.success()
        // Virtual steps are persisted by the foreground app and can independently satisfy
        // the target; a background Health Connect permission is only needed for actual steps.
        if (virtualSteps >= goalSteps.toLong()) {
            if (MissionRewardNotifications.recordReady(applicationContext, sessionId, 0, "DEBUG_VIRTUAL", active))
                MissionRewardNotifications.postIfReady(applicationContext, sessionId)
            return Result.success()
        }
        return try {
            if (HealthConnectClient.getSdkStatus(applicationContext) != HealthConnectClient.SDK_AVAILABLE)
                return Result.success()
            val client = HealthConnectClient.getOrCreate(applicationContext)
            if (client.features.getFeatureStatus(HealthConnectFeatures.FEATURE_READ_HEALTH_DATA_IN_BACKGROUND)
                != HealthConnectFeatures.FEATURE_STATUS_AVAILABLE) return Result.success()
            val granted = client.permissionController.getGrantedPermissions()
            if (!granted.contains(HealthPermission.getReadPermission(StepsRecord::class)) ||
                !granted.contains(HealthPermission.PERMISSION_READ_HEALTH_DATA_IN_BACKGROUND)) return Result.success()
            val start = Instant.ofEpochMilli(startEpochMs)
            val end = Instant.ofEpochMilli(minOf(System.currentTimeMillis(), endEpochMs))
            if (end <= start) return Result.success()
            val count: Long
            val source: String
            if (aggregate) {
                val query = HealthRepository.aggregateSteps(client, start, end)
                if (!query.optBoolean("queryComplete") || !query.optBoolean("hasValue") || query.optString("status") != "AVAILABLE") return Result.success()
                count = query.optLong("count"); source = "HEALTH_CONNECT_AGGREGATE"
            } else {
                val query = HealthRepository.preferredSteps(client, start, end)
                if (!query.complete) return Result.success()
                count = query.total?.count ?: return Result.success(); source = "HEALTH_CONNECT_PREFERRED"
            }
            if (count >= goalSteps.toLong() - virtualSteps &&
                MissionRewardNotifications.recordReady(applicationContext, sessionId, count, source, active))
                MissionRewardNotifications.postIfReady(applicationContext, sessionId)
            Result.success()
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            throw cancelled
        } catch (_: Exception) {
            Result.retry()
        }
    }
}
