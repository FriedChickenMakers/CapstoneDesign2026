package com.capstonedesign2026.platform

import android.app.Activity
import android.app.TimePickerDialog
import android.content.Context
import android.content.DialogInterface
import android.widget.TimePicker
import android.os.Looper
import org.robolectric.Shadows.shadowOf
import java.lang.ref.WeakReference
import androidx.work.Configuration
import androidx.work.WorkManager
import org.json.JSONArray
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.Robolectric
import org.robolectric.shadows.ShadowAlertDialog
import org.robolectric.shadows.ShadowDialog
import org.robolectric.annotation.Config
import java.time.Instant

/** Host Android runtime tests use only this test application's synthetic preferences. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [33], shadows=[AtomicFileRenameShadow::class])
class ActivityNotificationSettingsTest {
    private val context get() = RuntimeEnvironment.getApplication() as Context
    private val prefs get() = context.getSharedPreferences("garden_activity_notifications", Context.MODE_PRIVATE)
    private val history = "[{\"postedMinute\":720,\"openedMinute\":810,\"delayMinutes\":90}]"
    @Before fun setUp() {
        prefs.edit().clear().commit()
        ActivityNotifications.resetTestOverrides()
        try { WorkManager.initialize(context, Configuration.Builder().build()) } catch (_: IllegalStateException) { }
        ActivityNotifications.initialize(context, false)
    }
    private fun nativeActivity(): Activity {
        val activity = Robolectric.buildActivity(Activity::class.java).setup().get()
        fun field(name: String, value: Any) {
            AndroidPlatformBridge::class.java.getDeclaredField(name).apply { isAccessible = true }.set(null, value)
        }
        field("activityReference", WeakReference(activity))
        field("inputMode", "LIVE")
        field("notificationDialogShowing", false)
        field("notificationNoticeShown", "")
        return activity
    }
    @Test fun initialPromptUsesOrdinaryDailyDefault() {
        nativeActivity()
        AndroidPlatformBridge.showActivityNotificationEntryPrompt()
        val dialog = ShadowAlertDialog.getLatestAlertDialog()
        assertNotNull(dialog)
        dialog.getButton(DialogInterface.BUTTON_NEGATIVE).performClick()
        shadowOf(Looper.getMainLooper()).idle()
        assertFalse(dialog.isShowing)
        val saved = ActivityNotifications.settings(context)
        assertTrue(saved.getBoolean("timeChosen"))
        assertEquals(720, saved.getInt("minuteOfDay"))
        assertEquals("default", saved.getString("timeAdjustmentSource"))
    }
    @Test fun duplicateEntryAndCancelledInitialClockDoNotCommitOrStackPrompts() {
        nativeActivity()
        AndroidPlatformBridge.showActivityNotificationEntryPrompt()
        val first = ShadowAlertDialog.getLatestAlertDialog()
        AndroidPlatformBridge.showActivityNotificationEntryPrompt()
        assertSame(first, ShadowAlertDialog.getLatestAlertDialog())
        first.getButton(DialogInterface.BUTTON_POSITIVE).performClick()
        shadowOf(Looper.getMainLooper()).idle()
        val clock = ShadowDialog.getLatestDialog() as TimePickerDialog
        clock.cancel()
        shadowOf(Looper.getMainLooper()).idle()
        assertFalse(ActivityNotifications.settings(context).getBoolean("timeChosen"))
        assertEquals(720, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        val returnedPrompt = ShadowAlertDialog.getLatestAlertDialog()
        assertNotSame(first, returnedPrompt)
        assertTrue(returnedPrompt.isShowing)
        AndroidPlatformBridge.showActivityNotificationEntryPrompt()
        assertSame(returnedPrompt, ShadowAlertDialog.getLatestAlertDialog())
        returnedPrompt.getButton(DialogInterface.BUTTON_NEGATIVE).performClick()
        shadowOf(Looper.getMainLooper()).idle()
        ActivityNotifications.initialize(context, false)
        assertTrue(ActivityNotifications.settings(context).getBoolean("timeChosen"))
    }
    @Test fun nativeClockPickerUsesEffectiveTimeAndKeepsHistoryWhenEdited() {
        nativeActivity()
        prefs.edit().putInt("adaptiveMinute", 825).putInt("preferredMinute", 1200)
            .putString("clickHistory", history).putInt("learnedClicks", 1).commit()
        AndroidPlatformBridge.showActivityNotificationTimePicker()
        val dialog = ShadowDialog.getLatestDialog() as TimePickerDialog
        val picker = dialog.findViewById<TimePicker>(context.resources.getIdentifier("timePicker", "id", "android"))
        assertEquals(13, picker.hour)
        assertEquals(45, picker.minute)
        picker.hour = 15; picker.minute = 30
        dialog.getButton(DialogInterface.BUTTON_POSITIVE).performClick()
        shadowOf(Looper.getMainLooper()).idle()
        assertEquals(930, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        assertEquals(history, prefs.getString("clickHistory", ""))
    }
    @Test fun newAndExistingMigration() {
        val fresh = ActivityNotifications.settings(context)
        assertEquals(720, fresh.getInt("minuteOfDay"))
        assertTrue(fresh.getBoolean("adaptive"))
        assertFalse(fresh.getBoolean("timeChosen"))
        prefs.edit().clear().putInt("learningVersion", 2).putInt("adaptiveMinute", 825)
            .putInt("preferredMinute", 1200).putBoolean("adaptive", true)
            .putString("clickHistory", history).putInt("learnedClicks", 1).commit()
        val migrated = ActivityNotifications.settings(context)
        assertEquals(825, migrated.getInt("minuteOfDay"))
        assertTrue(migrated.getBoolean("timeChosen"))
        assertEquals(history, prefs.getString("clickHistory", ""))
        ActivityNotifications.initialize(context, false)
        assertEquals(825, ActivityNotifications.settings(context).getInt("minuteOfDay"))
    }
    @Test fun legacyOffUsesDisplayedFixedTimeWithoutDiscardingHistory() {
        prefs.edit().clear().putInt("learningVersion", 2).putInt("adaptiveMinute", 825)
            .putInt("preferredMinute", 90).putBoolean("adaptive", false)
            .putString("clickHistory", history).putInt("learnedClicks", 1).commit()
        assertEquals(90, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        assertEquals(history, prefs.getString("clickHistory", ""))
    }
    @Test fun noTimeChoiceIsPersistentNoonWithOptimization() {
        ActivityNotifications.configure(context, true, 720, true, timeSelected = true, adjustmentSource = "default")
        ActivityNotifications.initialize(context, false)
        val state = ActivityNotifications.settings(context)
        assertTrue(state.getBoolean("timeChosen"))
        assertTrue(state.getBoolean("adaptive"))
        assertEquals(720, state.getInt("minuteOfDay"))
        assertEquals("default", state.getString("timeAdjustmentSource"))
    }
    @Test fun togglesNeverRevertTheEffectiveHourOrClearHistory() {
        prefs.edit().putInt("adaptiveMinute", 825).putInt("preferredMinute", 1200)
            .putString("clickHistory", history).putInt("learnedClicks", 1).commit()
        ActivityNotifications.configure(context, true, 825, false)
        assertEquals(825, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        ActivityNotifications.initialize(context, false)
        assertEquals(825, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        ActivityNotifications.configure(context, false, 825, false)
        ActivityNotifications.configure(context, true, 825, true)
        assertEquals(825, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        assertEquals(history, prefs.getString("clickHistory", ""))
        assertEquals(1200, prefs.getInt("preferredMinute", 0))
    }
    @Test fun editPreservesHistoryAndRejectsOldTapThenNewTapCanLearn() {
        val posted = Instant.parse("2032-01-05T12:00:00Z").toEpochMilli()
        val generation = prefs.getInt("learningGeneration", 0)
        prefs.edit().putString("clickHistory", history).putInt("learnedClicks", 1)
            .putInt("adaptiveMinute", 825).putInt("preferredMinute", 1200).putBoolean("live", true)
            .putLong("postedAt", posted).putInt("postedGeneration", generation).commit()
        // Choose the original preferred hour explicitly; it must replace the learned hour.
        ActivityNotifications.configure(context, true, 1200, true, timeSelected = true)
        assertEquals(history, prefs.getString("clickHistory", ""))
        assertEquals(1200, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        ActivityNotifications.opened(context, "daily", "", posted, posted + 60_000)
        assertEquals(history, prefs.getString("clickHistory", ""))
        assertEquals(1200, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        val next = posted + 86_400_000
        prefs.edit().putLong("postedAt", next).putInt("postedGeneration", prefs.getInt("learningGeneration", 0)).commit()
        ActivityNotifications.opened(context, "daily", "", next, next + 30 * 60_000)
        assertEquals(2, JSONArray(prefs.getString("clickHistory", "[]")).length())
        assertEquals(2, ActivityNotifications.settings(context).getInt("learnedClicks"))
    }
    @Test fun optionOnlyToggleCannotRestoreAStaleUnityTimeSnapshot() {
        val staleDisplayMinute = ActivityNotifications.settings(context).getInt("minuteOfDay")
        prefs.edit().putString("clickHistory", history).putInt("learnedClicks", 1).commit()
        ActivityNotifications.configure(context, true, 930, true, timeSelected = true)
        assertNotEquals(staleDisplayMinute, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        // Unity still holds staleDisplayMinute, but the options command never transmits it.
        ActivityNotifications.configureOptions(context, true, false)
        assertEquals(930, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        ActivityNotifications.configureOptions(context, false, false)
        ActivityNotifications.configureOptions(context, true, true)
        assertEquals(930, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        assertEquals(history, prefs.getString("clickHistory", ""))
        assertEquals("user", ActivityNotifications.settings(context).getString("timeAdjustmentSource"))
    }
    @Test fun optimizationOffDoesNotLearnFromClicks() {
        val posted = Instant.parse("2032-01-05T12:00:00Z").toEpochMilli()
        ActivityNotifications.configure(context, true, 720, false)
        prefs.edit().putBoolean("live", true).putLong("postedAt", posted)
            .putInt("postedGeneration", prefs.getInt("learningGeneration", 0)).commit()
        ActivityNotifications.opened(context, "daily", "", posted, posted + 90 * 60_000)
        assertEquals(720, ActivityNotifications.settings(context).getInt("minuteOfDay"))
        assertEquals(0, JSONArray(prefs.getString("clickHistory", "[]")).length())
    }
}
