package com.capstonedesign2026.platform

import android.Manifest
import android.app.Activity
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import org.json.JSONObject
import java.time.ZoneId
import java.util.concurrent.TimeUnit

internal object ActivityNotifications {
    private const val PREFS = "garden_activity_notifications"
    private const val WORK = "garden_daily_activity_reminder"
    private const val CHANNEL = "garden_daily_activities"
    private const val ID = 2620
    private val lock = Any()
    private fun prefs(context: Context) = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun initialize(context: Context, live: Boolean) = synchronized(lock) {
        prefs(context).edit().putBoolean("live", live).apply()
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(NotificationChannel(CHANNEL, "오늘의 활동", NotificationManager.IMPORTANCE_DEFAULT).apply {
            description = "하루 한 번, 내 시간에 맞춘 활동 제안"
        })
        reconcile(context)
    }
    private fun reconcile(context: Context) {
        val p = prefs(context)
        val work = WorkManager.getInstance(context)
        if (!p.getBoolean("enabled", true) || !p.getBoolean("live", false)) {
            work.cancelUniqueWork(WORK)
            context.getSystemService(NotificationManager::class.java).cancel(ID)
            return
        }
        // Periodic work survives reboot and checks local time after a timezone/clock change.
        // Android may defer it in Doze; this is intentionally not an exact alarm.
        work.enqueueUniquePeriodicWork(WORK, ExistingPeriodicWorkPolicy.KEEP,
            PeriodicWorkRequestBuilder<ActivityReminderWorker>(15, TimeUnit.MINUTES).build())
    }
    fun settings(context: Context): JSONObject = synchronized(lock) {
        val p = prefs(context); val minute = effectiveMinute(context)
        JSONObject().put("enabled", p.getBoolean("enabled", true))
            .put("adaptive", p.getBoolean("adaptive", true)).put("minuteOfDay", minute)
            .put("preferredMinute", p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE))
            .put("learnedClicks", p.getInt("learnedClicks", 0))
            .put("permissionStatus", permissionStatus(context, CHANNEL))
            .put("nextReminderEpochMs", ActivityReminderPolicy.nextEpoch(System.currentTimeMillis(), ZoneId.systemDefault(), minute))
    }
    fun configure(context: Context, enabled: Boolean, minute: Int, adaptive: Boolean): String = synchronized(lock) {
        val p = prefs(context)
        val edit = p.edit().putBoolean("enabled", enabled).putBoolean("adaptive", adaptive)
            .putInt("preferredMinute", ActivityReminderPolicy.clampMinute(minute))
        if (p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE) != minute)
            edit.putInt("learnedMinute", ActivityReminderPolicy.clampMinute(minute)).putInt("learnedClicks", 0)
        if (!edit.commit()) return resultJson(PlatformStatus.ERROR, "Notification preferences could not be saved")
        reconcile(context)
        resultJson(PlatformStatus.AVAILABLE, "Activity reminders updated")
    }
    fun sync(context: Context, completedDay: String, title: String, active: Boolean): String = synchronized(lock) {
        // Unity supplies only a content title and completion day, never a user's private note.
        val p = prefs(context)
        if (!p.edit().putString("completedDay", completedDay).putString("title", title.take(100))
                .putBoolean("active", active).commit()) return resultJson(PlatformStatus.ERROR, "Reminder activity state could not be saved")
        if (completedDay == ActivityReminderPolicy.day(System.currentTimeMillis(), ZoneId.systemDefault()))
            context.getSystemService(NotificationManager::class.java).cancel(ID)
        resultJson(PlatformStatus.AVAILABLE, "Activity reminder state synchronized")
    }
    private fun effectiveMinute(context: Context): Int {
        val p = prefs(context)
        return ActivityReminderPolicy.clampMinute(if (p.getBoolean("adaptive", true) && p.getInt("learnedClicks", 0) >= 3)
            p.getInt("learnedMinute", ActivityReminderPolicy.DEFAULT_MINUTE)
        else p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE))
    }
    fun permissionStatus(context: Context, channel: String? = null): String {
        if (Build.VERSION.SDK_INT >= 33 && context.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED)
            return PlatformStatus.PERMISSION_REQUIRED
        val manager = context.getSystemService(NotificationManager::class.java)
        if (!manager.areNotificationsEnabled() || (channel != null && manager.getNotificationChannel(channel)?.importance == NotificationManager.IMPORTANCE_NONE))
            return PlatformStatus.PERMISSION_DENIED
        return PlatformStatus.AVAILABLE
    }
    fun requestPermission(activity: Activity) {
        if (Build.VERSION.SDK_INT >= 33 && activity.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED &&
            !PermissionState.wasRuntimeRequested(activity, Manifest.permission.POST_NOTIFICATIONS)) {
            PermissionState.markRuntimeRequested(activity, Manifest.permission.POST_NOTIFICATIONS)
            activity.requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 2620)
        } else if (permissionStatus(activity, CHANNEL) != PlatformStatus.AVAILABLE) {
            activity.startActivity(Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, activity.packageName))
        }
        reconcile(activity)
    }
    fun postIfDue(context: Context, now: Long = System.currentTimeMillis()): Boolean = synchronized(lock) {
        val p = prefs(context); val zone = ZoneId.systemDefault()
        if (!p.getBoolean("enabled", true) || !p.getBoolean("live", false) ||
            !ActivityReminderPolicy.due(now, zone, effectiveMinute(context), p.getString("postedDay", "") ?: "", p.getString("completedDay", "") ?: "") ||
            permissionStatus(context, CHANNEL) != PlatformStatus.AVAILABLE) return false
        val title = p.getString("title", "") ?: ""
        val body = if (p.getBoolean("active", false)) "하던 활동을 내 속도로 이어가 볼까요?" else
            if (title.isNotBlank()) "$title · 편한 만큼 함께해요." else "오늘은 어떤 작은 실천을 해볼까요? 편한 만큼 시작해요."
        val notification = Notification.Builder(context, CHANNEL).setSmallIcon(android.R.drawable.ic_menu_myplaces)
            .setContentTitle("마음 정원에서 잠깐 쉬어가요").setContentText(body)
            .setStyle(Notification.BigTextStyle().bigText(body)).setAutoCancel(true)
            // A previous day's reminder may still use this ID. The persisted day guard
            // prevents same-day repeats; a newly due day must be allowed to alert again.
            .setCategory(Notification.CATEGORY_REMINDER).setVisibility(Notification.VISIBILITY_PRIVATE).setOnlyAlertOnce(false)
            .setContentIntent(openIntent(context, "daily", "", now)).build()
        // Commit before posting: a process crash cannot create a repeated reminder the same day.
        if (!p.edit().putString("postedDay", ActivityReminderPolicy.day(now, zone)).putLong("postedAt", now).commit()) return false
        context.getSystemService(NotificationManager::class.java).notify(ID, notification)
        return true
    }
    fun openIntent(context: Context, kind: String, session: String, postedAt: Long): PendingIntent = PendingIntent.getActivity(context,
        if (kind == "daily") ID else 2615,
        Intent(context, NotificationEntryActivity::class.java).setData(Uri.parse("garden-notification://$kind/$postedAt"))
            .putExtra("kind", kind).putExtra("sessionId", session).putExtra("postedAt", postedAt),
        PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)

    fun opened(context: Context, kind: String, session: String, postedAt: Long, now: Long = System.currentTimeMillis()): Unit = synchronized(lock) {
        val p = prefs(context)
        val key = "$kind:$postedAt"
        if (p.getString("lastOpened", "") == key) return
        val edit = p.edit().putString("lastOpened", key).putString("pendingKind", kind).putString("pendingSession", session)
        if (kind == "daily" && p.getLong("postedAt", 0) == postedAt && p.getBoolean("adaptive", true)) {
            val learned = ActivityReminderPolicy.learnedMinute(p.getInt("learnedMinute", p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE)),
                postedAt, now, ZoneId.systemDefault())
            if (learned != null) edit.putInt("learnedMinute", learned).putInt("learnedClicks", p.getInt("learnedClicks", 0) + 1)
                .putLong("lastClickDelayMs", now - postedAt)
        }
        edit.commit()
    }
    fun consumeOpen(context: Context): String = synchronized(lock) {
        val p = prefs(context)
        val result = JSONObject().put("kind", p.getString("pendingKind", "")).put("sessionId", p.getString("pendingSession", ""))
        if (!p.edit().remove("pendingKind").remove("pendingSession").commit()) return "{}"
        result.toString()
    }
}

class ActivityReminderWorker(context: Context, parameters: WorkerParameters) : CoroutineWorker(context, parameters) {
    override suspend fun doWork(): Result = try {
        ActivityNotifications.postIfDue(applicationContext); Result.success()
    } catch (cancelled: kotlinx.coroutines.CancellationException) { throw cancelled }
      catch (_: Exception) { Result.retry() }
}

/** Direct notification activity target; no service/receiver notification trampoline. */
class NotificationEntryActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val kind = intent.getStringExtra("kind") ?: ""
        if (kind == "daily" || kind == "walking") ActivityNotifications.opened(this, kind,
            intent.getStringExtra("sessionId") ?: "", intent.getLongExtra("postedAt", 0))
        packageManager.getLaunchIntentForPackage(packageName)?.let {
            it.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
            startActivity(it)
        }
        finish()
    }
}
