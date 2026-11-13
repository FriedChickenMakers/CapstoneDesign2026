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

@RunWith(RobolectricTestRunner::class)
@Config(sdk=[33],shadows=[AtomicFileRenameShadow::class])
class DebugTelemetryTest {
    private val context get()=RuntimeEnvironment.getApplication() as Context
    private val binding="a".repeat(64)
    private var now=0L
    private fun batch()=JSONObject(MeasurementTelemetry.batch(context,binding))
    @Before fun setup(){
        MeasurementTelemetry.configure(context,"",false,0);MeasurementTelemetry.dataset="synthetic"
        context.getSharedPreferences("capstone_debug_telemetry",Context.MODE_PRIVATE).edit().clear().commit()
        now=System.currentTimeMillis();MeasurementTelemetry.configure(context,binding,true,now)
    }
    @Test fun qaIsFrozenAcrossToggleRetryAndRestart(){
        assertFalse(DebugTelemetry.context(context).getBoolean("qa"))
        DebugTelemetry.configure(context,true,true,"synthetic-build")
        val metadata=DebugTelemetry.context(context).toString()
        val details=JSONObject().put("content_version",5).put("role","basic").put("entry_source","demo")
            .put("active_seconds",17).put("recommended_seconds",60).put("completion_mode","too_long").put("end_reason","too_long").put("step_index",-1).toString()
        assertTrue(JSONObject(MeasurementTelemetry.detailed(context,"activity_completed","b".repeat(32),"course:P01",now,540,metadata,details,"")).getBoolean("ok"))
        val frozen=batch().getJSONArray("events").toString()
        DebugTelemetry.configure(context,true,false,"synthetic-build")
        assertTrue(DebugTelemetry.context(context).getBoolean("debug"));assertFalse(DebugTelemetry.context(context).getBoolean("qa"))
        MeasurementTelemetry.detailed(context,"activity_completed","b".repeat(32),"course:P01",now,540,DebugTelemetry.context(context).toString(),details,"")
        assertEquals(frozen,batch().getJSONArray("events").toString())
        val other=object:ContextWrapper(context){override fun getFilesDir()=File(context.cacheDir,"synthetic-debug-restart").apply{mkdirs()}}
        MeasurementTelemetry.configure(other,"",false,0);MeasurementTelemetry.configure(context,binding,true,now)
        assertEquals(frozen,batch().getJSONArray("events").toString())
        assertTrue(batch().getJSONArray("events").getJSONObject(0).getJSONObject("telemetry").getBoolean("qa"))
        assertEquals(17,batch().getJSONArray("events").getJSONObject(0).getJSONObject("details").getInt("active_seconds"))
    }
    @Test fun notificationCreationAndClickKeepTheirOwnFlags(){
        DebugTelemetry.configure(context,true,true,"synthetic-build")
        val ticket=MeasurementTelemetry.ticket(context,"synthetic-notification",now,"daily-v3-auto-default",720,720,0)
        MeasurementTelemetry.notification(context,ticket,"notification_post_accepted",now)
        DebugTelemetry.configure(context,true,false,"synthetic-build")
        MeasurementTelemetry.notification(context,ticket,"notification_opened",now+1)
        val events=batch().getJSONArray("events")
        assertTrue(events.getJSONObject(0).getJSONObject("telemetry").getBoolean("qa"))
        assertFalse(events.getJSONObject(1).getJSONObject("telemetry").getBoolean("qa"))
    }
    @Test fun qualityReportsQueueAndErrorsWithoutRewritingEvents(){
        MeasurementTelemetry.appSession(context,"c".repeat(32))
        val frozen=batch().getJSONArray("events").toString()
        DebugTelemetry.transport(context,"network")
        val health=batch().getJSONObject("transport")
        assertEquals(1,health.getInt("pending_count"));assertEquals(1,health.getInt("retry_count"));assertEquals("network",health.getString("last_error"))
        DebugTelemetry.transport(context,"none");assertTrue(batch().getJSONObject("transport").getLong("last_success_utc_ms")>0)
        assertEquals(frozen,batch().getJSONArray("events").toString())
    }
    @Test fun diagnosticsCannotBeEmittedWithQaOff(){
        DebugTelemetry.configure(context,true,false,"synthetic-build")
        assertFalse(JSONObject(MeasurementTelemetry.detailed(context,"ui_layout","d".repeat(32),"",now,540,
            DebugTelemetry.context(context).toString(),"{}","synthetic-layout")).getBoolean("ok"))
        assertEquals(0,batch().getJSONArray("events").length())
    }
}
