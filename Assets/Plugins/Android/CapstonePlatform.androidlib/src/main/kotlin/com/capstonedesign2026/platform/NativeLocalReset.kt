package com.capstonedesign2026.platform

import android.app.NotificationManager
import android.content.Context
import android.content.Intent

/** Local data only. Never revokes OS/Health Connect permission or sends a server request. */
internal object NativeLocalReset {
    fun epoch(context: Context): String = LocalResetEpoch(context.filesDir).snapshot().id
    fun pending(context: Context): Boolean = LocalResetEpoch(context.filesDir).snapshot().pending
    fun current(context: Context, epoch: String): Boolean = LocalResetEpoch(context.filesDir).isCurrent(epoch)
    fun write(context: Context, epoch: String, action: () -> Unit): Boolean = LocalResetEpoch(context.filesDir).write(epoch, action)

    @Synchronized fun reset(context: Context, resetId: String): String = try {
        val fence = LocalResetEpoch(context.filesDir)
        if (fence.begin(resetId)) {
            // All prior process/task leases are now invalid, including onDestroy checkpoints.
            HealthRepository.resetLocalData()
            SensorRepository.resetLocalData()
            context.stopService(Intent(context, SensorForegroundService::class.java))
            context.stopService(Intent(context, DailyAccelerationService::class.java))
            ActivityNotifications.resetLocalData(context)
            MissionRewardNotifications.resetLocalData(context)
            androidx.work.WorkManager.getInstance(context).pruneWork().result.get(250,java.util.concurrent.TimeUnit.MILLISECONDS)
            MeasurementTelemetry.resetLocalData(context)
            listOf("capstone_platform_permission_state", "daily_acceleration_trial").forEach {
                check(context.getSharedPreferences(it, Context.MODE_PRIVATE).edit().clear().commit()) { "Preference cleanup failed" }
            }
            context.getSystemService(NotificationManager::class.java).apply { cancel(2601); cancel(2603) }
            fence.finish(resetId) {
                // No parent-root delete: retain this fence, Unity state, OS settings and unrelated files.
                listOf("platform", "measurement-outbox.json", "measurement-health.json").forEach { name ->
                    fence.eraseOwned(name)
                    if (name != "platform") listOf(".bak", ".new", ".tmp").forEach { fence.eraseOwned(name + it) }
                }
            }
        }
        resultJson(PlatformStatus.AVAILABLE, "App-local platform records reset")
    } catch (_: Exception) {
        resultJson(PlatformStatus.ERROR, "Local records could not be reset. Retry the same operation.", "LOCAL_RESET_PENDING")
    }
}
