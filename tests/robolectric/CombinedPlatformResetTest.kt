package com.capstonedesign2026.platform

import android.app.Activity
import android.content.Context
import android.content.DialogInterface
import android.content.Intent
import android.os.Looper
import androidx.test.core.app.ApplicationProvider
import androidx.work.Configuration
import androidx.work.testing.SynchronousExecutor
import androidx.work.testing.WorkManagerTestInitHelper
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.Robolectric
import org.robolectric.RobolectricTestRunner
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import org.robolectric.shadows.ShadowAlertDialog
import java.io.File
import java.lang.ref.WeakReference

/** Synthetic host storage only: no camera UI, inference, network or device connection. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35], shadows=[AtomicFileRenameShadow::class])
class CombinedPlatformResetTest {
    private lateinit var context: Context
    private val id="a".repeat(32)
    @Before fun setup() {
        context=ApplicationProvider.getApplicationContext()
        WorkManagerTestInitHelper.initializeTestWorkManager(context,Configuration.Builder().setExecutor(SynchronousExecutor()).build())
        ActivityNotifications.resetTestOverrides()
    }
    private fun reset(name:String) = JSONObject(NativeLocalReset.reset(context,name)).getString("status")
    private fun result()=JSONObject().put("id",id).put("target","book").put("status","matched")
    @Test fun cameraResetErasesAllOwnedGenerationsAndLateResultCannotReturn() {
        val epoch=NativeLocalReset.epoch(context)
        assertTrue(MissionPhotoController.prepareCapture(context,id,epoch))
        MissionPhotoController.raw(context,id).writeText("synthetic raw")
        assertTrue(MissionPhotoController.commitResult(context,id,epoch,result()))
        listOf(".jpg",".jpg.bak",".jpg.new",".json.bak",".cancelled").forEach {
            File(MissionPhotoController.root(context),id+it).writeText("synthetic")
        }
        val keep=File(context.noBackupFilesDir,"unrelated.txt").apply {writeText("keep")}
        val cacheKeep=File(context.cacheDir,"unrelated.txt").apply {writeText("keep")}
        assertEquals("AVAILABLE",reset("camera-reset"))
        assertFalse(MissionPhotoController.root(context).exists())
        assertFalse(MissionPhotoController.raw(context,id).parentFile!!.exists())
        assertFalse(MissionPhotoController.commitResult(context,id,epoch,result()))
        assertFalse(MissionPhotoController.prepareCapture(context,id,epoch))
        MissionPhotoController.process(context,id,"book",epoch)
        MissionPhotoController.cancel(context,id,epoch)
        MissionPhotoController.poll(context,id)
        assertFalse(MissionPhotoController.root(context).exists())
        assertFalse(MissionPhotoController.raw(context,id).parentFile!!.exists())
        assertEquals("keep",keep.readText()); assertEquals("keep",cacheKeep.readText())
    }
    @Test fun cancellationWinsAgainstLateWorkerAndCannotBeRestartedByResume() {
        val epoch=NativeLocalReset.epoch(context)
        assertTrue(MissionPhotoController.prepareCapture(context,id,epoch))
        MissionPhotoController.raw(context,id).writeText("synthetic")
        assertTrue(MissionPhotoController.commitResult(context,id,epoch,result()))
        File(MissionPhotoController.root(context),id+".jpg").writeText("normalized")
        MissionPhotoController.cancel(context,id,epoch)
        assertFalse(MissionPhotoController.commitResult(context,id,epoch,result()))
        assertFalse(MissionPhotoController.resumeCapture(context,id,"book",epoch))
        assertFalse(MissionPhotoController.prepareCapture(context,id,epoch))
        assertFalse(MissionPhotoController.raw(context,id).exists())
        assertFalse(File(MissionPhotoController.root(context),id+".jpg").exists())
        assertEquals("cancelled",JSONObject(MissionPhotoController.poll(context,id)).getString("status"))
    }
    @Test fun pendingResetBlocksCaptureAndCompletedRetryKeepsNewPhoto() {
        LocalResetEpoch(context.filesDir).begin("pending-camera")
        assertFalse(MissionPhotoController.prepareCapture(context,id,"pending-camera"))
        assertFalse(MissionPhotoController.commitResult(context,id,"pending-camera",result()))
        assertFalse(MissionPhotoController.root(context).exists())
        assertEquals("AVAILABLE",reset("pending-camera"))
        assertTrue(MissionPhotoController.commitResult(context,id,"pending-camera",result()))
        val saved=File(MissionPhotoController.root(context),id+".json").readText()
        assertEquals("AVAILABLE",reset("pending-camera"))
        assertEquals(saved,File(MissionPhotoController.root(context),id+".json").readText())
    }
    @Test fun queuedRealWorkerCannotRecreateFilesWhenResetFinishesFirst() {
        val worker=MissionPhotoController::class.java.getDeclaredField("worker").apply {isAccessible=true}
            .get(null) as java.util.concurrent.ExecutorService
        val entered=java.util.concurrent.CountDownLatch(1)
        val release=java.util.concurrent.CountDownLatch(1)
        worker.submit {entered.countDown();release.await()}
        assertTrue(entered.await(5,java.util.concurrent.TimeUnit.SECONDS))
        try {
            val epoch=NativeLocalReset.epoch(context)
            assertTrue(MissionPhotoController.prepareCapture(context,id,epoch))
            MissionPhotoController.raw(context,id).writeText("synthetic non-image")
            MissionPhotoController.process(context,id,"book",epoch)
            assertEquals("processing",JSONObject(MissionPhotoController.poll(context,id)).getString("status"))
            assertEquals("AVAILABLE",reset("queued-worker-reset"))
        } finally {release.countDown()}
        worker.submit {}.get(5,java.util.concurrent.TimeUnit.SECONDS)
        assertFalse(MissionPhotoController.root(context).exists())
        assertFalse(MissionPhotoController.raw(context,id).parentFile!!.exists())
    }
    @Test fun duplicateCaptureRequestStartsSystemCameraOnceAndCarriesOriginalEpoch() {
        val activity=Robolectric.buildActivity(Activity::class.java).setup().get()
        val epoch=NativeLocalReset.epoch(context)
        assertEquals("capturing",JSONObject(MissionPhotoController.start(activity,id,"book")).getString("status"))
        shadowOf(Looper.getMainLooper()).idle()
        val first=shadowOf(activity).nextStartedActivity
        assertEquals(epoch,first.getStringExtra("localDataEpoch"))
        assertEquals(id,first.getStringExtra("photoId"))
        assertEquals("capturing",JSONObject(MissionPhotoController.start(activity,id,"book")).getString("status"))
        shadowOf(Looper.getMainLooper()).idle()
        assertNull(shadowOf(activity).nextStartedActivity)
        assertEquals("AVAILABLE",reset("duplicate-camera-reset"))
    }
    @Test fun restoredOldCameraActivityCannotLaunchOrCreateOutputAfterReset() {
        val old=NativeLocalReset.epoch(context)
        assertEquals("AVAILABLE",reset("activity-reset"))
        val intent=Intent(context,MissionPhotoActivity::class.java).putExtra("photoId",id)
            .putExtra("target","book").putExtra("localDataEpoch",old)
        val controller=Robolectric.buildActivity(MissionPhotoActivity::class.java,intent).create()
        assertTrue(controller.get().isFinishing)
        assertNull(shadowOf(controller.get()).nextStartedActivity)
        assertFalse(MissionPhotoController.root(context).exists())
        assertFalse(MissionPhotoController.raw(context,id).parentFile!!.exists())
        controller.destroy()
    }
    @Test fun initialTimeDialogCallbackFromBeforeResetCannotRestoreSettings() {
        val activity=Robolectric.buildActivity(Activity::class.java).setup().get()
        fun field(name:String,value:Any) { AndroidPlatformBridge::class.java.getDeclaredField(name).apply {isAccessible=true}.set(null,value) }
        field("activityReference",WeakReference(activity));field("inputMode","LIVE")
        field("notificationDialogShowing",false);field("notificationNoticeShown","")
        ActivityNotifications.initialize(context,false)
        AndroidPlatformBridge.showActivityNotificationEntryPrompt()
        val dialog=ShadowAlertDialog.getLatestAlertDialog(); assertTrue(dialog.isShowing)
        assertEquals("AVAILABLE",reset("dialog-reset"))
        dialog.getButton(DialogInterface.BUTTON_NEGATIVE).performClick()
        shadowOf(Looper.getMainLooper()).idle()
        assertTrue(context.getSharedPreferences("garden_activity_notifications",0).all.isEmpty())
        dialog.dismiss()
    }
    @Test fun measuredFocusCallbackAndAcknowledgementCannotRestoreResetOutbox() {
        val binding="b".repeat(64);val since=System.currentTimeMillis()
        assertTrue(JSONObject(MeasurementTelemetry.configure(context,binding,true,since)).getBoolean("ok"))
        assertEquals("AVAILABLE",reset("measurement-reset"))
        assertFalse(JSONObject(MeasurementTelemetry.focusRecommendation(context,"focus_recommendation_shown",
            id,"","free-mission:M03","free-mission:M03","focus-additional-history-preference-v2",
            "catalog-default-no-history",since+1,540)).getBoolean("ok"))
        assertFalse(JSONObject(MeasurementTelemetry.acknowledge(context,binding,"old","[]","[]")).getBoolean("ok"))
        assertFalse(File(context.filesDir,"measurement-outbox.json").exists())
    }
    @Test fun pendingResetDoesNotEnqueueForegroundServiceStarts() {
        val activity=Robolectric.buildActivity(Activity::class.java).setup().get()
        val app=AndroidPlatformBridge::class.java.getDeclaredField("appContext").apply {isAccessible=true}
        val original=app.get(null)
        try {
            app.set(null,context)
            LocalResetEpoch(context.filesDir).begin("pending-service")
            assertEquals("NO_DATA",JSONObject(AndroidPlatformBridge.startDailyAccelerationTrial(activity)).getString("status"))
            assertEquals("NO_DATA",JSONObject(AndroidPlatformBridge.startSensorSummaryExperiment(activity,5000,5000,"synthetic")).getString("status"))
            assertEquals("NO_DATA",JSONObject(AndroidPlatformBridge.startBackgroundSensorService()).getString("status"))
            assertEquals("NO_DATA",JSONObject(AndroidPlatformBridge.stopBackgroundSensorService()).getString("status"))
            assertNull(shadowOf(context as android.app.Application).nextStartedService)
        } finally {app.set(null,original)}
    }
    @Test fun alreadyDispatchedServicePromotesThenStopsWithoutRecreatingResetData() {
        LocalResetEpoch(context.filesDir).begin("dispatched-service")
        val daily=Robolectric.buildService(DailyAccelerationService::class.java).create()
        assertEquals(2603,shadowOf(daily.get()).lastForegroundNotificationId)
        assertNull(shadowOf(daily.get()).lastForegroundNotification)
        assertTrue(shadowOf(daily.get()).isStoppedBySelf)
        assertTrue(shadowOf(daily.get()).isForegroundStopped)
        assertTrue(context.getSharedPreferences("daily_acceleration_trial",0).all.isEmpty())
        daily.destroy()
        val sensor=Robolectric.buildService(SensorForegroundService::class.java).create()
        assertEquals(2601,shadowOf(sensor.get()).lastForegroundNotificationId)
        assertNull(shadowOf(sensor.get()).lastForegroundNotification)
        assertTrue(shadowOf(sensor.get()).isStoppedBySelf)
        assertTrue(shadowOf(sensor.get()).isForegroundStopped)
        assertFalse(File(context.filesDir,"platform/summaries").exists())
        sensor.destroy()
    }

}
