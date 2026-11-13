package com.capstonedesign2026.platform

import android.content.Context
import android.content.ContextWrapper
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import java.io.File

/** Only synthetic catalog IDs and isolated host-runtime files; no device records. */
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [33], shadows=[AtomicFileRenameShadow::class])
class FocusRecommendationMeasurementTest {
    private val context get() = RuntimeEnvironment.getApplication() as Context
    private val binding = "a".repeat(64)
    private val decision = "c".repeat(32)
    private val session = "d".repeat(32)
    private var since = 0L
    private fun batch() = JSONObject(MeasurementTelemetry.batch(context, binding))
    private fun events() = batch().getJSONArray("events")
    private fun record(kind: String, at: Long = since + 1, actual: String = "free-mission:M17", reason: String = "focus-method-preference") =
        JSONObject(MeasurementTelemetry.focusRecommendation(context, "focus_recommendation_" + kind, decision,
            if (kind == "shown") "" else session, "free-mission:M17", actual,
            "focus-additional-history-preference-v2", reason, at, 540)).getBoolean("ok")
    @Before fun setUp() {
        MeasurementTelemetry.configure(context, "", false, 0)
        MeasurementTelemetry.dataset = "synthetic"
        since = System.currentTimeMillis()
        assertTrue(JSONObject(MeasurementTelemetry.configure(context, binding, true, since)).getBoolean("ok"))
    }
    @Test fun visibleDecisionDifferentChoiceAndLifecycleAreJoinedWithoutFeaturePayload() {
        assertTrue(record("shown"))
        assertTrue(record("selected", actual = "free-mission:M02"))
        assertTrue(record("started", actual = "free-mission:M02"))
        assertTrue(record("completed", actual = "free-mission:M02"))
        val items = events()
        assertEquals(4, items.length())
        for (i in 0 until items.length()) {
            val item = items.getJSONObject(i)
            assertEquals(decision, item.getString("decision_id"))
            assertEquals("free-mission:M17", item.getString("recommended_activity_id"))
            assertEquals("synthetic", item.getString("dataset"))
            assertFalse(item.has("features")); assertFalse(item.has("note"))
            assertEquals(-1, item.getInt("elapsed_ms"))
        }
        assertEquals("free-mission:M02", items.getJSONObject(1).getString("activity_id"))
    }
    @Test fun rerenderRetryRestartAndAcknowledgementKeepFirstEvent() {
        assertTrue(record("shown")); assertTrue(record("shown", since + 2))
        val original = events().toString()
        assertEquals(1, events().length())
        val other = object : ContextWrapper(context) {
            override fun getFilesDir() = File(context.cacheDir, "synthetic-other-outbox").apply { mkdirs() }
        }
        MeasurementTelemetry.configure(other, "", false, 0)
        MeasurementTelemetry.configure(context, binding, true, since)
        assertEquals(original, events().toString())
        val before = batch(); val eventId = events().getJSONObject(0).getString("event_id")
        val ids = "[\"$eventId\"]"
        MeasurementTelemetry.acknowledge(context, binding, before.getString("collection_id"), ids, ids)
        assertTrue(record("shown")); assertEquals(0, events().length())
    }
    @Test fun precollectionAndInvalidTextCannotBecomeMeasurements() {
        assertFalse(record("shown", since - 1))
        assertEquals(0, events().length())
        assertFalse(record("shown", reason = "synthetic arbitrary user text"))
        assertEquals(0, events().length()); assertTrue(batch().getBoolean("storage_error"))
        MeasurementTelemetry.configure(context, "", false, 0)
        assertFalse(record("shown"))
    }
    @Test fun notificationShapeAndFocusActivityIdsStayCompatible() {
        val ticket = MeasurementTelemetry.ticket(context, "synthetic-notification", since, "daily-fixed-v1", 720, 720, 0)
        MeasurementTelemetry.notification(context, ticket, "notification_due", since + 1)
        val notice = events().getJSONObject(0)
        assertFalse(notice.has("decision_id")); assertFalse(notice.has("recommended_activity_id"))
        for (mission in listOf("course:P01", "course:P28", "free-mission:M17")) {
            assertTrue(JSONObject(MeasurementTelemetry.activity(context, "activity_started",
                MeasurementTelemetry.key("synthetic-" + mission), mission, since + 1, 540)).getBoolean("ok"))
        }
        assertEquals(4, events().length())
    }
    @Test fun unavailableEnrollmentSuspendsWithoutDeletingPendingEvidence() {
        assertTrue(record("shown"))
        val before=batch()
        val file=File(context.filesDir,"measurement-outbox.json")
        val saved=file.readBytes()
        assertTrue(JSONObject(MeasurementTelemetry.suspend()).getBoolean("ok"))
        assertEquals("{}",MeasurementTelemetry.batch(context,binding))
        assertFalse(record("selected"))
        assertArrayEquals(saved,file.readBytes())
        assertTrue(JSONObject(MeasurementTelemetry.configure(context,binding,true,since)).getBoolean("ok"))
        assertEquals(before.getString("collection_id"),batch().getString("collection_id"))
        assertEquals(before.getJSONArray("events").toString(),events().toString())
        assertTrue(record("selected"))
        assertEquals(2,events().length())
    }
    @Test fun failedShownWriteCanReplayOriginalEvidenceBeforeSelectionAndAfterRestart() {
        val file=File(context.filesDir,"measurement-outbox.json")
        val original=file.readBytes()
        // AtomicFile cannot replace a directory; fail the real durable writer.
        assertTrue(file.delete());assertTrue(file.mkdir());File(file,"synthetic-blocker").writeText("fixture")
        assertFalse(record("shown"))
        assertTrue(file.deleteRecursively());file.writeBytes(original)
        assertTrue(record("shown"))
        assertTrue(record("selected",actual="free-mission:M02"))
        val first=events().getJSONObject(0).toString()
        // A later replay must retain the observation's original timestamp/payload.
        assertTrue(record("shown",since+5000))
        assertEquals(first,events().getJSONObject(0).toString())
        assertEquals(2,events().length())
    }
    @Test fun mockModeCannotAcknowledgeActivationOrDeleteExistingLiveQueue() {
        assertTrue(record("shown"));val saved=File(context.filesDir,"measurement-outbox.json").readBytes()
        val mode=AndroidPlatformBridge::class.java.getDeclaredField("inputMode").apply{isAccessible=true}
        val app=AndroidPlatformBridge::class.java.getDeclaredField("appContext").apply{isAccessible=true}
        val originalMode=mode.get(null);val originalApp=app.get(null)
        try {
            mode.set(null,"MOCK");app.set(null,context)
            assertFalse(JSONObject(AndroidPlatformBridge.configureMeasurements(binding,true,since)).getBoolean("ok"))
            assertArrayEquals(saved,File(context.filesDir,"measurement-outbox.json").readBytes())
            mode.set(null,"LIVE")
            assertTrue(JSONObject(AndroidPlatformBridge.configureMeasurements(binding,true,since)).getBoolean("ok"))
            assertEquals(1,events().length())
        } finally {mode.set(null,originalMode);app.set(null,originalApp)}
    }
    @Test fun focusRejectionDistinguishesCapacityValidationAndTransientStorage() {
        fun response(reason:String="catalog-default-no-history",at:Long=since+1)=JSONObject(
            MeasurementTelemetry.focusRecommendation(context,"focus_recommendation_shown",decision,"",
                "free-mission:M03","free-mission:M03","focus-additional-history-preference-v2",reason,at,540))
        assertEquals("invalid",response("unrecognized-fixed-code").getString("failure"))
        assertEquals("outside-window",response(at=since-1).getString("failure"))
        val file=File(context.filesDir,"measurement-outbox.json")
        val saved=file.readBytes()
        assertTrue(file.delete());assertTrue(file.mkdir());File(file,"synthetic-blocker").writeText("fixture")
        assertEquals("storage",response().getString("failure"))
        assertTrue(file.deleteRecursively());file.writeBytes(saved)
        // Fill the real buffer with synthetic rows in one state save, so the test
        // does not perform thousands of unrelated AtomicFile writes.
        val buffer=MeasurementTelemetry::class.java.getDeclaredField("buffer").apply {isAccessible=true}.get(null) as MeasurementBuffer
        val state=buffer.state
        val rows=(1..MeasurementBuffer.CAPACITY).map {
            MeasurementRecord(it.toString(16).padStart(32,'0'),kind="notification_due",occurred=since,appVersion="synthetic",dataset="synthetic")
        }
        val full=state.copy(events=rows,receipts=rows.associate {it.eventId to it.occurred})
        MeasurementBuffer::class.java.getDeclaredField("state").apply {isAccessible=true}.set(buffer,full)
        assertEquals("capacity",response().getString("failure"))
        assertEquals(1L,batch().getLong("dropped_attempts"))
    }

}
