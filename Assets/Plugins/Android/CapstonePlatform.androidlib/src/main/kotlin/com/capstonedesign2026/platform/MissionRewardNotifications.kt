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

/** Demo-only reminder. The worker reads Health Connect; Unity alone writes game rewards. */
internal object MissionRewardNotifications {
    private const val PREFS = "mission_reward_reminder"
    private const val WORK = "capstone_m15_reward_ready"
    private const val CHANNEL = "capstone_mission_rewards"
    private const val NOTIFICATION_ID = 2615
    private val notificationLock = Any()

    private fun state(context: Context) = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun schedule(context: Context, sessionId: String, startEpochMs: Long, goalSteps: Int, virtualSteps: Long): String {
        synchronized(notificationLock) {
            if (sessionId.isBlank() || startEpochMs <= 0L || goalSteps !in 1..100_000 || virtualSteps < 0)
                return resultJson(PlatformStatus.ERROR, "Invalid M15 reminder", "INVALID_REMINDER")
            val prefs = state(context)
            val previousSession = prefs.getString("sessionId", "") ?: ""
            val sameSession = previousSession == sessionId
            if (sameSession &&
                prefs.getLong("startEpochMs", 0L) == startEpochMs &&
                prefs.getInt("goalSteps", 0) == goalSteps &&
                prefs.getLong("virtualSteps", 0L) == virtualSteps)
                return resultJson(PlatformStatus.AVAILABLE, "M15 reminder already scheduled")
            if (!prefs.edit().putString("sessionId", sessionId).putLong("startEpochMs", startEpochMs)
                    .putInt("goalSteps", goalSteps).putLong("virtualSteps", virtualSteps)
                    .putString("notifiedSessionId", if (sameSession) prefs.getString("notifiedSessionId", "") else "").commit())
                return resultJson(PlatformStatus.ERROR, "Reminder could not be saved", "REMINDER_SAVE_FAILED")
            if (prefs.getString("notifiedSessionId", "") == sessionId)
                return resultJson(PlatformStatus.AVAILABLE, "M15 reminder already shown")
            return try {
                if (previousSession.isNotEmpty() && previousSession != sessionId)
                    context.getSystemService(NotificationManager::class.java)
                        .cancel("m15:$previousSession", NOTIFICATION_ID)
                val work = PeriodicWorkRequestBuilder<MissionRewardWorker>(15, TimeUnit.MINUTES).build()
                WorkManager.getInstance(context).enqueueUniquePeriodicWork(WORK, ExistingPeriodicWorkPolicy.UPDATE, work)
                resultJson(PlatformStatus.AVAILABLE, "M15 reminder scheduled")
            } catch (exception: Exception) {
                resultJson(PlatformStatus.ERROR, exception.safeMessage(), "REMINDER_SCHEDULE_FAILED")
            }
        }
    }

    fun cancel(context: Context, sessionId: String): String {
        synchronized(notificationLock) {
            val prefs = state(context)
            if (prefs.getString("sessionId", "") == sessionId) {
                prefs.edit().remove("sessionId").remove("startEpochMs").remove("goalSteps")
                    .remove("virtualSteps").remove("notifiedSessionId").commit()
                WorkManager.getInstance(context).cancelUniqueWork(WORK)
            }
            context.getSystemService(NotificationManager::class.java)
                .cancel("m15:$sessionId", NOTIFICATION_ID)
            return resultJson(PlatformStatus.AVAILABLE, "M15 reminder cancelled")
        }
    }

    fun postIfReady(context: Context, sessionId: String): String {
        synchronized(notificationLock) {
            val prefs = state(context)
            if (prefs.getString("sessionId", "") != sessionId)
                return resultJson(PlatformStatus.NO_DATA, "M15 session is no longer active")
            if (prefs.getString("notifiedSessionId", "") == sessionId)
                return resultJson(PlatformStatus.AVAILABLE, "M15 reminder already shown")
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
                return resultJson(PlatformStatus.PERMISSION_REQUIRED, "Notification permission is required")
            val manager = context.getSystemService(NotificationManager::class.java)
            if (!manager.areNotificationsEnabled())
                return resultJson(PlatformStatus.PERMISSION_DENIED, "App notifications are disabled")
            manager.createNotificationChannel(NotificationChannel(CHANNEL, "미션 보상", NotificationManager.IMPORTANCE_DEFAULT))
            if (manager.getNotificationChannel(CHANNEL)?.importance == NotificationManager.IMPORTANCE_NONE)
                return resultJson(PlatformStatus.PERMISSION_DENIED, "Mission reward notifications are disabled")
            val launch = context.packageManager.getLaunchIntentForPackage(context.packageName)
            val open = launch?.let { PendingIntent.getActivity(context, 0, it,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE) }
            val notification = Notification.Builder(context, CHANNEL)
                .setSmallIcon(android.R.drawable.ic_menu_mylocation)
                .setContentTitle("걷기 미션 보상을 받을 수 있어요")
                .setContentText("목표 걸음에 도달했어요. 앱에서 미션을 직접 완료해 주세요.")
                .setContentIntent(open).setAutoCancel(true)
                .setCategory(Notification.CATEGORY_REMINDER).build()
            return try {
                // Reusing the same tag and ID makes a retry replace the visible notification
                // if the process exits after notify() but before its dedupe marker is saved.
                manager.notify("m15:$sessionId", NOTIFICATION_ID, notification)
                if (!prefs.edit().putString("notifiedSessionId", sessionId).commit())
                    return resultJson(PlatformStatus.ERROR, "Reminder state could not be saved", "REMINDER_STATE_SAVE_FAILED")
                WorkManager.getInstance(context).cancelUniqueWork(WORK)
                resultJson(PlatformStatus.AVAILABLE, "M15 reward reminder shown")
            } catch (exception: Exception) {
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

    fun active(context: Context): ReminderWindow? {
        val prefs = state(context)
        val id = prefs.getString("sessionId", "") ?: ""
        if (id.isEmpty() || prefs.getString("notifiedSessionId", "") == id) return null
        return ReminderWindow(id, prefs.getLong("startEpochMs", 0L), prefs.getInt("goalSteps", 0),
            prefs.getLong("virtualSteps", 0L))
    }
    data class ReminderWindow(val sessionId: String, val startEpochMs: Long, val goalSteps: Int, val virtualSteps: Long)
}

class MissionRewardWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result {
        val active = MissionRewardNotifications.active(applicationContext) ?: return Result.success()
        val (sessionId, startEpochMs, goalSteps, virtualSteps) = active
        if (startEpochMs <= 0L || goalSteps <= 0 || startEpochMs >= System.currentTimeMillis()) return Result.success()
        // Virtual steps are persisted by the foreground app and can independently satisfy
        // the target; a background Health Connect permission is only needed for actual steps.
        if (virtualSteps >= goalSteps.toLong()) {
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
            val query = HealthRepository.preferredSteps(client, Instant.ofEpochMilli(startEpochMs), Instant.now())
            // The periodic schedule will try again; avoid repeated large reads after a partial query.
            if (!query.complete) return Result.success()
            val count = query.total?.count ?: return Result.success()
            // A missing preferred source is NO_DATA, not zero steps or a reward signal.
            if (count >= goalSteps.toLong() - virtualSteps)
                MissionRewardNotifications.postIfReady(applicationContext, sessionId)
            Result.success()
        } catch (cancelled: kotlinx.coroutines.CancellationException) {
            throw cancelled
        } catch (_: Exception) {
            Result.retry()
        }
    }
}
