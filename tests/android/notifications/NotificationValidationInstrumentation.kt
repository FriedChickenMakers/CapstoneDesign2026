package com.capstonedesign2026.platform

import android.app.Instrumentation
import android.app.Notification
import android.app.NotificationManager
import android.content.Context
import android.os.Bundle
import android.os.SystemClock
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkInfo
import androidx.work.WorkManager
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.time.LocalDate
import java.time.ZoneId
import java.util.concurrent.TimeUnit

/** Only the isolated test APK includes this class. No OS clock or product data changes. */
class NotificationValidationInstrumentation : Instrumentation() {
    private val checks = JSONArray()
    private val personas = JSONArray()
    private val trace = JSONArray()
    private val zone = ZoneId.systemDefault()
    private var virtualNow = 0L
    private val day = LocalDate.of(2032, 1, 5)
    private val ctx get() = targetContext
    private val manager get() = ctx.getSystemService(NotificationManager::class.java)
    private val prefs get() = ctx.getSharedPreferences("garden_activity_notifications", Context.MODE_PRIVATE)
    private fun at(date: LocalDate, minute: Int) = date.atTime(minute / 60, minute % 60).atZone(zone).toInstant().toEpochMilli()
    private fun verify(name: String, condition: Boolean) { check(condition) { name }; checks.put(name) }
    private fun until(name: String, predicate: () -> Boolean) {
        val deadline = SystemClock.elapsedRealtime() + 8_000
        while (!predicate() && SystemClock.elapsedRealtime() < deadline) SystemClock.sleep(40)
        verify(name, predicate())
    }
    private fun visible() = manager.activeNotifications.firstOrNull { it.id == 2620 }
    private fun visibleAt(epoch: Long) = visible()?.notification?.extras?.getLong("gardenReminderPostedAt") == epoch
    private fun reset(minute: Int = 1200) {
        WorkManager.getInstance(ctx).cancelAllWork().result.get(10, TimeUnit.SECONDS)
        manager.cancelAll()
        prefs.edit().clear().commit()
        ActivityNotifications.resetTestOverrides()
        ActivityNotifications.clock = { virtualNow }
        virtualNow = at(day, minute) - 1
        ActivityNotifications.initialize(ctx, true)
        ActivityNotifications.configure(ctx, true, minute, true)
        WorkManager.getInstance(ctx).cancelAllWork().result.get(10, TimeUnit.SECONDS)
        until("reset clears only the synthetic package notifications", { visible() == null })
        virtualNow = at(day, minute)
    }
    private fun post(epoch: Long) {
        virtualNow = epoch
        check(ActivityNotifications.postIfDue(ctx)) { "Due notification was not posted at virtual $epoch" }
        until("notification reached Android at requested virtual instant", { visibleAt(epoch) })
    }
    private fun click(epoch: Long) {
        virtualNow = epoch
        val intent = visible()!!.notification.contentIntent
        val expected = prefs.getLong("postedAt", 0)
        runOnMainSync { intent.send() }
        until("actual PendingIntent routes virtual click", { prefs.getString("lastOpened", "") == "daily:$expected" })
        ActivityNotifications.consumeOpen(ctx)
    }
    override fun onCreate(arguments: Bundle?) { super.onCreate(arguments); start() }
    override fun onStart() {
        val output = File(ctx.cacheDir, "notification-validation").apply { mkdirs() }
        val started = SystemClock.elapsedRealtime()
        try {
            check(ctx.packageName == "com.capstonedesign2026.notificationpreview")
            verify("isolated synthetic package", ctx.packageName != "com.capstonedesign2026.mockup")
            val due = at(day, 1200)
            reset()
            verify("notification permission available", ActivityNotifications.permissionStatus(ctx, "garden_daily_activities") == "AVAILABLE")
            val channel = manager.getNotificationChannel("garden_daily_activities")
            File(output, "channel.json").writeText(JSONObject().put("importance", channel.importance)
                .put("hasSound", channel.sound != null).put("vibrationRequested", channel.shouldVibrate())
                .put("interruptionFilter", manager.currentInterruptionFilter)
                .put("note", "OS posting/effect eligibility; this is not a microphone listening test").toString(2))
            virtualNow = due - 1
            verify("before due time stays quiet", !ActivityNotifications.postIfDue(ctx))
            post(due)
            verify("same occurrence cannot repeat", !ActivityNotifications.postIfDue(ctx))
            verify("new occurrence may alert over an old notification", visible()!!.notification.flags and Notification.FLAG_ONLY_ALERT_ONCE == 0)
            click(due + 30 * 60_000)
            verify("actual tap persists a timing observation", JSONArray(prefs.getString("clickHistory", "[]")).length() == 1)
            ActivityNotifications.opened(ctx, "daily", "", due, virtualNow + 60_000)
            verify("duplicate tap cannot train again", JSONArray(prefs.getString("clickHistory", "[]")).length() == 1)
            post(at(day.plusDays(1), 1200))
            verify("next day updates exactly one visible notification", manager.activeNotifications.count { it.id == 2620 } == 1)

            reset()
            ActivityNotifications.notificationPoster = { _, _, _ -> throw IllegalStateException("Synthetic notify failure") }
            var failed = false
            try { ActivityNotifications.postIfDue(ctx) } catch (_: Exception) { failed = true }
            verify("notify failure is retryable rather than marked delivered", failed && prefs.getString("postedDay", "").isNullOrEmpty())
            ActivityNotifications.resetTestOverrides(); ActivityNotifications.clock = { virtualNow }
            post(due)
            verify("retry after notify failure confirms delivery", prefs.getLong("postedAt", 0) == due)

            reset()
            ActivityNotifications.preferenceCommit = { false }
            var storageFailed = false
            try { ActivityNotifications.postIfDue(ctx) } catch (_: java.io.IOException) { storageFailed = true }
            verify("reservation storage failure cannot emit an untracked notification", storageFailed && visible() == null)
            ActivityNotifications.resetTestOverrides(); ActivityNotifications.clock = { virtualNow }
            post(due)

            reset()
            var commits = 0
            ActivityNotifications.preferenceCommit = { edit -> commits++; if (commits == 2) false else edit.commit() }
            try { ActivityNotifications.postIfDue(ctx) } catch (_: Exception) { /* worker retries */ }
            until("delivery accepted before confirmation storage failure", { visibleAt(due) })
            ActivityNotifications.resetTestOverrides(); ActivityNotifications.clock = { virtualNow }
            var repeated = 0
            ActivityNotifications.notificationPoster = { _, _, _ -> repeated++ }
            ActivityNotifications.postIfDue(ctx)
            verify("restart recovers visible pending receipt without a second alert", repeated == 0 && prefs.getLong("postedAt", 0) == due)

            reset(23 * 60 + 30)
            post(at(day.plusDays(1), 15))
            verify("midnight catch-up belongs to previous scheduled day", prefs.getString("postedDay", "") == day.toString())
            post(at(day.plusDays(1), 23 * 60 + 30))
            verify("next scheduled occurrence survives midnight catch-up", prefs.getString("postedDay", "") == day.plusDays(1).toString())
            reset(90)
            post(at(day, 90))
            verify("nighttime preferred time can actually post", visibleAt(at(day, 90)))

            reset()
            post(due + 26 * 60_000)
            click(due + 36 * 60_000)
            verify("learning delta excludes OS delivery delay", prefs.getLong("lastClickDelayMs", -1) == 10 * 60_000L)
            val oldPost = prefs.getLong("postedAt", 0)
            prefs.edit().putInt("adaptiveMinute", 90).commit()
            ActivityNotifications.configure(ctx, true, 1200, true, resetLearning = true)
            verify("explicit manual selection resets even the original preferred hour", ActivityNotifications.settings(ctx).optInt("minuteOfDay") == 1200 && JSONArray(prefs.getString("clickHistory", "[]")).length() == 0)
            ActivityNotifications.opened(ctx, "walking", "synthetic-unrelated", oldPost + 1, virtualNow)
            ActivityNotifications.opened(ctx, "daily", "", oldPost, virtualNow + 60_000)
            verify("old notification cannot undo a manual time change", JSONArray(prefs.getString("clickHistory", "[]")).length() == 0)

            reset()
            ActivityNotifications.sync(ctx, day.toString(), "Synthetic completed activity", false)
            verify("completed occurrence suppresses reminder", !ActivityNotifications.postIfDue(ctx))
            reset()
            ActivityNotifications.configure(ctx, false, 1200, true)
            verify("disabled setting suppresses reminder", !ActivityNotifications.postIfDue(ctx))
            ActivityNotifications.configure(ctx, true, 1200, true)
            ActivityNotifications.initialize(ctx, false)
            verify("MOCK mode suppresses reminder", !ActivityNotifications.postIfDue(ctx))

            reset()
            val request = OneTimeWorkRequestBuilder<ActivityReminderWorker>().setInitialDelay(1, TimeUnit.SECONDS).build()
            WorkManager.getInstance(ctx).enqueue(request).result.get(10, TimeUnit.SECONDS)
            until("actual WorkManager worker creates due notification", { visibleAt(due) })
            until("actual worker succeeds", { WorkManager.getInstance(ctx).getWorkInfoById(request.id).get(5, TimeUnit.SECONDS)?.state == WorkInfo.State.SUCCEEDED })

            // The long virtual cohort uses a quiet channel in this isolated package only.
            // Real OS delivery above was checked with the channel's original sound settings.
            manager.createNotificationChannel(android.app.NotificationChannel("garden_daily_activities", "Synthetic timing cohort", NotificationManager.IMPORTANCE_LOW))
            for (persona in NotificationPersonaScenarios.personas) runPersona(persona)
            File(output, "results.json").writeText(JSONObject().put("status", "PASS").put("syntheticOnly", true)
                .put("clock", "virtual timestamps; device clock unchanged").put("checks", checks).put("personas", personas)
                .put("realElapsedMs", SystemClock.elapsedRealtime() - started).toString(2))
            File(output, "virtual-trace.json").writeText(trace.toString(2))
            finish(-1, Bundle().apply { putString("stream", "PASS ${checks.length()} checks, ${personas.length()} virtual personas\n") })
        } catch (failure: Throwable) {
            File(output, "results.json").writeText(JSONObject().put("status", "FAIL").put("checks", checks)
                .put("personas", personas).put("error", failure.stackTraceToString()).toString(2))
            File(output, "virtual-trace.json").writeText(trace.toString(2))
            finish(0, Bundle().apply { putString("stream", "FAIL $failure\n") })
        } finally {
            WorkManager.getInstance(ctx).cancelAllWork()
            manager.cancelAll(); prefs.edit().clear().commit()
            ActivityNotifications.resetTestOverrides()
        }
    }
    private fun runPersona(persona: NotificationPersonaScenarios.Persona) {
        reset()
        var cursor = at(day, 0)
        var lastPost = 0L
        repeat(40) { index ->
            val minute = ActivityNotifications.settings(ctx).optInt("minuteOfDay")
            val next = ActivityReminderPolicy.nextEpoch(maxOf(cursor, lastPost + 12 * 3_600_000L), zone, minute)
            post(next)
            val opened = NotificationPersonaScenarios.nextResponse(persona, next, NotificationPersonaScenarios.TRAIN_SEED, false)!!
            click(opened)
            trace.put(JSONObject().put("persona", persona.id).put("observation", index)
                .put("scheduledMinute", minute).put("postedAtVirtual", next).put("openedAtVirtual", opened)
                .put("delayMinutes", (opened - next) / 60_000.0))
            lastPost = next; cursor = opened
            // Avoid OS binder flood; elapsed time has no relationship to virtual days.
            SystemClock.sleep(160)
        }
        val settings = ActivityNotifications.settings(ctx)
        val finalMinute = settings.optInt("minuteOfDay")
        val delays = (0L until 90L).mapNotNull { offset ->
            val epoch = at(LocalDate.of(2033, 4, 1).plusDays(offset), finalMinute)
            NotificationPersonaScenarios.nextResponse(persona, epoch, NotificationPersonaScenarios.EVALUATION_SEED)?.let { (it - epoch) / 60_000.0 }
        }.sorted()
        fun q(fraction: Double): Double {
            val position = delays.lastIndex * fraction; val low = position.toInt(); val high = minOf(low + 1, delays.lastIndex)
            return delays[low] + (delays[high] - delays[low]) * (position - low)
        }
        verify("${persona.id}: held-out delay covers availability spread", q(.9) <= persona.acceptableP90)
        verify("${persona.id}: history stays bounded", JSONArray(prefs.getString("clickHistory", "[]")).length() <= ActivityReminderPolicy.MAX_HISTORY)
        if (persona.unrestricted) verify("frequent phone use does not drift the reminder", finalMinute == 1200)
        // Repeated reads and reinitialization are not new learning observations.
        repeat(20) { check(ActivityNotifications.settings(ctx).optInt("minuteOfDay") == finalMinute) }
        ActivityNotifications.initialize(ctx, true)
        verify("${persona.id}: restart preserves learned time", ActivityNotifications.settings(ctx).optInt("minuteOfDay") == finalMinute)
        WorkManager.getInstance(ctx).cancelAllWork().result.get(10, TimeUnit.SECONDS)
        personas.put(JSONObject().put("id", persona.id).put("actualAndroidTrainingNotifications", 40)
            .put("finalMinute", finalMinute).put("heldOutP50Minutes", q(.5)).put("heldOutP90Minutes", q(.9)))
    }
}
