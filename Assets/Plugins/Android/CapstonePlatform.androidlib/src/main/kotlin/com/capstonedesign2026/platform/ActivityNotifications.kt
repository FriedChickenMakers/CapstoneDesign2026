package com.capstonedesign2026.platform

import android.Manifest
import android.app.Activity
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.SharedPreferences
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
import org.json.JSONArray
import java.io.IOException
import java.time.ZoneId
import java.util.concurrent.TimeUnit

internal object ActivityNotifications {
    private const val PREFS = "garden_activity_notifications"
    private const val WORK = "garden_daily_activity_reminder"
    private const val CHANNEL = "garden_daily_activities"
    private const val ID = 2620
    private val lock = Any()
    private fun prefs(context: Context) = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    // Internal dependency boundaries allow the isolated instrumentation application to
    // advance virtual time and inject delivery/storage failures without changing Android's
    // clock or adding an externally callable debug Intent to the shipping application.
    internal var clock: () -> Long = System::currentTimeMillis
    internal var notificationPoster: (Context, Int, Notification) -> Unit = { context, id, notification ->
        context.getSystemService(NotificationManager::class.java).notify(id, notification)
    }
    internal var preferenceCommit: (SharedPreferences.Editor) -> Boolean = { it.commit() }
    private var acceptedPendingKey: String? = null
    internal fun resetTestOverrides() = synchronized(lock) {
        clock = System::currentTimeMillis
        notificationPoster = { context, id, notification -> context.getSystemService(NotificationManager::class.java).notify(id, notification) }
        preferenceCommit = { it.commit() }
        acceptedPendingKey = null
    }
    private fun save(edit: SharedPreferences.Editor) {
        if (!preferenceCommit(edit)) throw IOException("Activity reminder preferences could not be saved")
    }
    private fun ensureLearningState(p: SharedPreferences) {
        if (p.getInt("learningVersion", 0) >= 2) return
        // Keep the old effective time, but never turn its aggregate count into invented
        // observations. Only new verified notification taps enter the bounded history.
        val preferred = ActivityReminderPolicy.clampMinute(p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE))
        val previous = if (p.getInt("learnedClicks", 0) >= 3)
            ActivityReminderPolicy.clampMinute(p.getInt("learnedMinute", preferred)) else preferred
        save(p.edit().putInt("learningVersion", 2).putInt("adaptiveMinute", previous)
            .putString("clickHistory", "[]").putInt("learnedClicks", 0)
            .putInt("windowStartMinute", -1).putInt("windowEndMinute", -1)
            .putInt("spreadMinutes", 0).putInt("informativeSamples", 0).putString("learningStatus", "collecting")
            .putInt("learningGeneration", p.getInt("learningGeneration", 0) + 1))
    }
    private fun history(p: SharedPreferences): List<ActivityReminderPolicy.Observation> = try {
        val data = JSONArray(p.getString("clickHistory", "[]"))
        (maxOf(0, data.length() - ActivityReminderPolicy.MAX_HISTORY) until data.length()).mapNotNull { index ->
            data.optJSONObject(index)?.let {
                val posted = it.optInt("postedMinute", -1)
                val opened = it.optInt("openedMinute", -1)
                val delay = it.optInt("delayMinutes", -1)
                if (posted in 0..1439 && opened in 0..1439 && delay in 0..1440)
                    ActivityReminderPolicy.Observation(posted, opened, delay) else null
            }
        }
    } catch (_: Exception) { emptyList() }
    private fun historyJson(observations: List<ActivityReminderPolicy.Observation>): String = JSONArray().apply {
        observations.takeLast(ActivityReminderPolicy.MAX_HISTORY).forEach {
            put(JSONObject().put("postedMinute", it.postedMinute).put("openedMinute", it.openedMinute)
                .put("delayMinutes", it.delayMinutes))
        }
    }.toString()

    fun initialize(context: Context, live: Boolean) = synchronized(lock) {
        val p = prefs(context)
        ensureLearningState(p)
        val edit = p.edit().putBoolean("live", live)
        if (p.getBoolean("live", false) != live)
            edit.putInt("learningGeneration", p.getInt("learningGeneration", 0) + 1)
        save(edit)
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
            // Recover an accepted delivery before removing its visual evidence, then
            // discard any merely reserved attempt. Disabled/MOCK cannot train old taps.
            reconcilePending(context)
            context.getSystemService(NotificationManager::class.java).cancel(ID)
            clearPending(p)
            return
        }
        // Periodic work survives reboot and checks local time after a timezone/clock change.
        // Android may defer it in Doze; this is intentionally not an exact alarm.
        work.enqueueUniquePeriodicWork(WORK, ExistingPeriodicWorkPolicy.KEEP,
            PeriodicWorkRequestBuilder<ActivityReminderWorker>(15, TimeUnit.MINUTES).build())
    }
    fun settings(context: Context): JSONObject = synchronized(lock) {
        val p = prefs(context)
        ensureLearningState(p)
        val minute = effectiveMinute(p)
        JSONObject().put("enabled", p.getBoolean("enabled", true))
            .put("adaptive", p.getBoolean("adaptive", true)).put("minuteOfDay", minute)
            .put("preferredMinute", p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE))
            .put("learnedClicks", p.getInt("learnedClicks", 0))
            .put("windowStartMinute", p.getInt("windowStartMinute", -1))
            .put("windowEndMinute", p.getInt("windowEndMinute", -1))
            .put("spreadMinutes", p.getInt("spreadMinutes", 0))
            .put("informativeSamples", p.getInt("informativeSamples", 0))
            .put("learningStatus", p.getString("learningStatus", "collecting"))
            .put("lastClickDelayMs", p.getLong("lastClickDelayMs", 0))
            .put("lastPostedAtEpochMs", p.getLong("postedAt", 0))
            .put("permissionStatus", permissionStatus(context, CHANNEL))
            .put("nextReminderEpochMs", ActivityReminderPolicy.nextEpoch(clock(), ZoneId.systemDefault(), minute))
    }
    fun configure(context: Context, enabled: Boolean, minute: Int, adaptive: Boolean, resetLearning: Boolean = false): String = synchronized(lock) {
        try {
            val p = prefs(context)
            ensureLearningState(p)
            val chosen = ActivityReminderPolicy.clampMinute(minute)
            val timeChanged = resetLearning || p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE) != chosen
            val edit = p.edit().putBoolean("enabled", enabled).putBoolean("adaptive", adaptive).putInt("preferredMinute", chosen)
            if (timeChanged) edit.putInt("adaptiveMinute", chosen).putString("clickHistory", "[]").putInt("learnedClicks", 0)
                .putInt("windowStartMinute", -1).putInt("windowEndMinute", -1).putInt("spreadMinutes", 0)
                .putInt("informativeSamples", 0).putString("learningStatus", "collecting").remove("lastClickDelayMs")
            if (timeChanged || p.getBoolean("enabled", true) != enabled || p.getBoolean("adaptive", true) != adaptive)
                edit.putInt("learningGeneration", p.getInt("learningGeneration", 0) + 1)
            save(edit)
            reconcile(context)
            resultJson(PlatformStatus.AVAILABLE, "Activity reminders updated")
        } catch (_: IOException) { resultJson(PlatformStatus.ERROR, "Notification preferences could not be saved") }
    }
    fun sync(context: Context, completedDay: String, title: String, active: Boolean): String = synchronized(lock) {
        // Unity supplies only a content title and completion day, never a user's private note.
        val p = prefs(context)
        if (!preferenceCommit(p.edit().putString("completedDay", completedDay).putString("title", title.take(100))
                .putBoolean("active", active))) return resultJson(PlatformStatus.ERROR, "Reminder activity state could not be saved")
        if (completedDay == ActivityReminderPolicy.day(clock(), ZoneId.systemDefault())) {
            reconcilePending(context)
            context.getSystemService(NotificationManager::class.java).cancel(ID)
        }
        resultJson(PlatformStatus.AVAILABLE, "Activity reminder state synchronized")
    }
    private fun effectiveMinute(p: SharedPreferences): Int = ActivityReminderPolicy.clampMinute(
        if (p.getBoolean("adaptive", true)) p.getInt("adaptiveMinute", p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE))
        else p.getInt("preferredMinute", ActivityReminderPolicy.DEFAULT_MINUTE))

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
    private fun pendingKey(context: Context, p: SharedPreferences) = "${context.packageName}:${p.getLong("pendingPostAt", 0)}:${p.getInt("pendingPostGeneration", -1)}"
    private fun clearPending(p: SharedPreferences) {
        if (!p.contains("pendingPostAt")) return
        save(p.edit().remove("pendingPostDay").remove("pendingPostAt").remove("pendingPostScheduledAt").remove("pendingPostGeneration"))
        acceptedPendingKey = null
    }
    private fun confirmPending(context: Context) {
        val p = prefs(context)
        if (p.getLong("pendingPostAt", 0) <= 0) return
        // Keep the reservation until this write is durably confirmed. SharedPreferences
        // may change its in-memory values even when commit returns false; retaining the
        // reservation lets a retry flush the same receipt without issuing another alert.
        save(p.edit().putString("postedDay", p.getString("pendingPostDay", ""))
            .putLong("postedAt", p.getLong("pendingPostAt", 0))
            .putLong("postedScheduledAt", p.getLong("pendingPostScheduledAt", 0))
            .putInt("postedGeneration", p.getInt("pendingPostGeneration", -1)))
        clearPending(p)
    }
    private fun reconcilePending(context: Context) {
        val p = prefs(context)
        val at = p.getLong("pendingPostAt", 0)
        if (at <= 0) return
        val day = p.getString("pendingPostDay", "")
        val generation = p.getInt("pendingPostGeneration", -1)
        val visible = context.getSystemService(NotificationManager::class.java).activeNotifications.any {
            it.id == ID && it.notification.extras.getLong("gardenReminderPostedAt", 0) == at &&
                it.notification.extras.getString("gardenReminderDay", "") == day &&
                it.notification.extras.getInt("gardenReminderGeneration", -1) == generation
        }
        if (visible || acceptedPendingKey == pendingKey(context, p) || p.getLong("postedAt", 0) == at)
            confirmPending(context)
        else clearPending(p)
    }
    fun postIfDue(context: Context, now: Long = clock()): Boolean = synchronized(lock) {
        val p = prefs(context)
        ensureLearningState(p)
        reconcilePending(context)
        val zone = ZoneId.systemDefault()
        val minute = effectiveMinute(p)
        if (!p.getBoolean("enabled", true) || !p.getBoolean("live", false) ||
            !ActivityReminderPolicy.due(now, zone, minute, p.getString("postedDay", "") ?: "", p.getString("completedDay", "") ?: "", p.getLong("postedAt", 0)) ||
            permissionStatus(context, CHANNEL) != PlatformStatus.AVAILABLE) return false
        val occurrence = ActivityReminderPolicy.dueOccurrence(now, zone, minute) ?: return false
        val generation = p.getInt("learningGeneration", 0)
        val title = p.getString("title", "") ?: ""
        val body = if (p.getBoolean("active", false)) "하던 활동을 내 속도로 이어가 볼까요?" else
            if (title.isNotBlank()) "$title · 편한 만큼 함께해요." else "오늘은 어떤 작은 실천을 해볼까요? 편한 만큼 시작해요."
        val notification = Notification.Builder(context, CHANNEL).setSmallIcon(android.R.drawable.ic_menu_myplaces)
            .setContentTitle("마음 정원에서 잠깐 쉬어가요").setContentText(body)
            .setStyle(Notification.BigTextStyle().bigText(body)).setAutoCancel(true)
            // Each new scheduled day may alert even if the previous day's ID remains.
            .setCategory(Notification.CATEGORY_REMINDER).setVisibility(Notification.VISIBILITY_PRIVATE).setOnlyAlertOnce(false)
            .addExtras(Bundle().apply {
                putString("gardenReminderDay", occurrence.scheduledDay)
                putLong("gardenReminderPostedAt", now)
                putInt("gardenReminderGeneration", generation)
            }).setContentIntent(openIntent(context, "daily", "", now)).build()
        save(p.edit().putString("pendingPostDay", occurrence.scheduledDay).putLong("pendingPostAt", now)
            .putLong("pendingPostScheduledAt", occurrence.scheduledEpochMs).putInt("pendingPostGeneration", generation))
        try {
            notificationPoster(context, ID, notification)
        } catch (failure: Exception) {
            // Keep the unconfirmed reservation, not a posted-day guard. The next retry
            // checks Android's visible receipt before clearing/retrying it: even an
            // ambiguous binder failure after delivery must not immediately alert twice.
            throw failure
        }
        acceptedPendingKey = pendingKey(context, p)
        confirmPending(context)
        true
    }
    fun openIntent(context: Context, kind: String, session: String, postedAt: Long): PendingIntent = PendingIntent.getActivity(context,
        if (kind == "daily") ID else 2615,
        Intent(context, NotificationEntryActivity::class.java).setData(Uri.parse("garden-notification://$kind/$postedAt"))
            .putExtra("kind", kind).putExtra("sessionId", session).putExtra("postedAt", postedAt),
        PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)

    fun opened(context: Context, kind: String, session: String, postedAt: Long, now: Long = clock()): Unit = synchronized(lock) {
        if (kind != "daily" && kind != "walking") return
        val p = prefs(context)
        ensureLearningState(p)
        // A tap itself proves delivery if the process died between notify and receipt
        // confirmation; autoCancel may already have removed the active notification.
        if (kind == "daily" && postedAt > 0 && p.getLong("pendingPostAt", 0) == postedAt) confirmPending(context)
        val key = "$kind:$postedAt"
        if (p.getString("lastOpened", "") == key) return
        val edit = p.edit().putString("lastOpened", key).putString("pendingKind", kind).putString("pendingSession", session)
        if (kind == "daily" && postedAt > 0 && p.getLong("postedAt", 0) == postedAt &&
            p.getLong("lastLearnedPostedAt", 0) != postedAt && p.getBoolean("enabled", true) && p.getBoolean("live", false) &&
            p.getBoolean("adaptive", true) && p.getInt("postedGeneration", -1) == p.getInt("learningGeneration", 0)) {
            ActivityReminderPolicy.observation(postedAt, now, ZoneId.systemDefault())?.let { observation ->
                val observations = (history(p) + observation).takeLast(ActivityReminderPolicy.MAX_HISTORY)
                val estimate = ActivityReminderPolicy.estimate(effectiveMinute(p), observations)
                edit.putString("clickHistory", historyJson(observations)).putInt("adaptiveMinute", estimate.minute)
                    .putInt("learnedClicks", estimate.sampleCount).putInt("windowStartMinute", estimate.windowStartMinute ?: -1)
                    .putInt("windowEndMinute", estimate.windowEndMinute ?: -1).putInt("spreadMinutes", estimate.spreadMinutes)
                    .putInt("informativeSamples", estimate.informativeSamples).putString("learningStatus", estimate.status)
                    .putLong("lastLearnedPostedAt", postedAt).putLong("lastClickDelayMs", now - postedAt)
            }
        }
        save(edit)
    }
    fun consumeOpen(context: Context): String = synchronized(lock) {
        val p = prefs(context)
        val result = JSONObject().put("kind", p.getString("pendingKind", "")).put("sessionId", p.getString("pendingSession", ""))
        if (!preferenceCommit(p.edit().remove("pendingKind").remove("pendingSession"))) return "{}"
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
        if (kind == "daily" || kind == "walking") try {
            ActivityNotifications.opened(this, kind,
                intent.getStringExtra("sessionId") ?: "", intent.getLongExtra("postedAt", 0))
        } catch (_: IOException) {
            // A failed local write must not crash the notification's app entry point.
            android.util.Log.w("GardenReminders", "Could not persist notification open")
        }
        packageManager.getLaunchIntentForPackage(packageName)?.let {
            it.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
            startActivity(it)
        }
        finish()
    }
}
