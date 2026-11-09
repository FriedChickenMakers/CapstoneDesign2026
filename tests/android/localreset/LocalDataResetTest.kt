package com.capstonedesign2026.platform

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import androidx.test.core.app.ApplicationProvider
import androidx.work.Configuration
import androidx.work.testing.SynchronousExecutor
import androidx.work.testing.WorkManagerTestInitHelper
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.Robolectric
import org.robolectric.Shadows.shadowOf
import org.robolectric.annotation.Config
import java.io.File

/** Host-only Android shadows. Never launched on a phone or the product installation. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk=[35], manifest=Config.NONE)
class LocalDataResetTest {
    private lateinit var context: Context
    private val prefs=listOf("capstone_platform_permission_state","daily_acceleration_trial","garden_activity_notifications","mission_reward_reminder")
    @Before fun setup() {
        context=ApplicationProvider.getApplicationContext()
        WorkManagerTestInitHelper.initializeTestWorkManager(context,Configuration.Builder().setExecutor(SynchronousExecutor()).build())
        ActivityNotifications.resetTestOverrides()
    }
    private fun seed() {
        listOf("platform/sensor_samples.jsonl","platform/sensor_samples.previous.jsonl",
            "platform/summaries/synthetic.finished.json","platform/daily_acceleration/status.json.bak",
            "platform/daily_acceleration/daily-synthetic.jsonl","measurement-outbox.json.bak","measurement-health.json.new").forEach {
            File(context.filesDir,it).apply {parentFile!!.mkdirs();writeText("synthetic")}
        }
        prefs.forEach {check(context.getSharedPreferences(it,0).edit().putString("synthetic","old").commit())}
    }
    private fun reset(id:String)=JSONObject(NativeLocalReset.reset(context,id)).getString("status")
    @Test fun removesOnlyAppOwnedRecordsAndKeepsOsPermissions() {
        seed()
        val unrelated=File(context.filesDir,"unrelated-fixture.txt").apply {writeText("keep")}
        shadowOf(context as android.app.Application).grantPermissions(Manifest.permission.ACTIVITY_RECOGNITION)
        assertEquals("AVAILABLE",reset("reset-a"))
        assertFalse(File(context.filesDir,"platform").exists())
        assertFalse(File(context.filesDir,"measurement-outbox.json").exists())
        assertFalse(File(context.filesDir,"measurement-outbox.json.bak").exists())
        assertFalse(File(context.filesDir,"measurement-health.json.new").exists())
        prefs.forEach {assertTrue(context.getSharedPreferences(it,0).all.isEmpty())}
        assertEquals("keep",unrelated.readText())
        assertEquals(PackageManager.PERMISSION_GRANTED,context.checkSelfPermission(Manifest.permission.ACTIVITY_RECOGNITION))
        assertEquals("reset-a",NativeLocalReset.epoch(context))
        assertFalse(NativeLocalReset.pending(context))
    }
    @Test fun failedCleanupIsPendingAndSameIdRetryCompletes() {
        seed(); ActivityNotifications.preferenceCommit={false}
        assertEquals("ERROR",reset("reset-failure"))
        assertTrue(NativeLocalReset.pending(context))
        assertFalse(NativeLocalReset.write(context,""){error("old writer entered")})
        ActivityNotifications.resetTestOverrides()
        assertEquals("AVAILABLE",reset("reset-failure"))
        assertFalse(File(context.filesDir,"platform").exists())
        assertFalse(NativeLocalReset.pending(context))
    }
    @Test fun completedDuplicateDoesNotEraseNewRecords() {
        assertEquals("AVAILABLE",reset("reset-once"))
        val fresh=File(context.filesDir,"platform/new.jsonl")
        assertTrue(NativeLocalReset.write(context,"reset-once"){fresh.parentFile!!.mkdirs();fresh.writeText("new")})
        assertEquals("AVAILABLE",reset("reset-once"))
        assertEquals("new",fresh.readText())
    }
    @Test fun oldNotificationIntentAndWorkerCannotRestoreHistory() {
        assertEquals("AVAILABLE",reset("reset-notification"))
        ActivityNotifications.opened(context,"daily","",123,expectedEpoch="")
        assertTrue(context.getSharedPreferences("garden_activity_notifications",0).all.isEmpty())
        assertFalse(ActivityNotifications.postIfDue(context,expectedEpoch=""))
        assertFalse(MissionRewardNotifications.recordReady(context,"old-session",100,"HEALTH_CONNECT_AGGREGATE",expectedEpoch=""))
        assertTrue(context.getSharedPreferences("mission_reward_reminder",0).all.isEmpty())
        assertEquals("ERROR",JSONObject(ActivityNotifications.configure(context,true,100,false,expectedEpoch="")).getString("status"))
        assertTrue(context.getSharedPreferences("garden_activity_notifications",0).all.isEmpty())
    }
    @Test fun pendingResetBlocksFreshQueriesAndMeasurementReenable() {
        val fence=LocalResetEpoch(context.filesDir);fence.begin("reset-pending")
        HealthRepository.initialize(context)
        assertEquals("NO_DATA",JSONObject(HealthRepository.refresh()).getString("status"))
        assertFalse(JSONObject(MeasurementTelemetry.configure(context,"synthetic-binding",true,1)).getBoolean("ok"))
        assertFalse(File(context.filesDir,"measurement-outbox.json").exists())
    }
    @Test fun invalidIdDoesNotDeleteAnything() {
        seed()
        assertEquals("ERROR",reset("../invalid"))
        assertTrue(File(context.filesDir,"platform/sensor_samples.jsonl").exists())
        assertFalse(NativeLocalReset.pending(context))
        assertEquals("ERROR",reset("한글"))
    }
    @Test fun oldSensorServiceFinalWriteCannotRecreateDeletedFiles() {
        val intent=android.content.Intent(context,SensorForegroundService::class.java)
            .putExtra("summaryRunId","synthetic-reset-run").putExtra("summaryWindowMs",5000L)
        val controller=Robolectric.buildService(SensorForegroundService::class.java,intent).create().startCommand(0,1)
        val service=controller.get()
        assertEquals("AVAILABLE",reset("reset-sensor"))
        // Deliberately deliver onDestroy after deletion, as another Android process can.
        controller.destroy()
        val field=SensorForegroundService::class.java.getDeclaredField("disk").apply {isAccessible=true}
        assertTrue((field.get(service) as java.util.concurrent.ExecutorService).awaitTermination(3,java.util.concurrent.TimeUnit.SECONDS))
        assertFalse(File(context.filesDir,"platform").exists())
    }
    @Test fun oldDailyServiceOnDestroyCannotRestorePrefsOrLifecycleFiles() {
        val controller=Robolectric.buildService(DailyAccelerationService::class.java).create()
        assertEquals("AVAILABLE",reset("reset-daily"))
        controller.destroy()
        assertTrue(context.getSharedPreferences("daily_acceleration_trial",0).all.isEmpty())
        assertFalse(File(context.filesDir,"platform").exists())
    }
}
