package com.capstonedesign2026.platform

import android.app.Instrumentation
import android.app.NotificationManager
import android.content.Context
import android.content.ContextWrapper
import android.os.Bundle
import android.os.SystemClock
import androidx.work.WorkManager
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.time.Instant
import java.time.ZoneId
import java.util.concurrent.TimeUnit

/** Real Android adapter and PendingIntent checks; isolated synthetic package only. */
class MeasurementValidationInstrumentation : Instrumentation() {
    private var phase = "produce"
    private val checks = JSONArray()
    private val ctx get() = targetContext
    private val binding = "a".repeat(64)
    private fun verify(name: String, value: Boolean) { check(value) { name }; checks.put(name) }
    private fun batch() = JSONObject(MeasurementTelemetry.batch(ctx, binding))
    private fun events() = batch().getJSONArray("events")
    private fun ids(a: JSONArray) = JSONArray().apply { for (i in 0 until a.length()) put(a.getJSONObject(i).getString("event_id")) }.toString()
    override fun onCreate(arguments: Bundle?) { super.onCreate(arguments);phase=arguments?.getString("phase") ?: "produce";start() }
    override fun onStart() {
        val out = File(ctx.cacheDir,"measurement-validation").apply { mkdirs() }
        val manager = ctx.getSystemService(NotificationManager::class.java)
        var failure: Throwable? = null
        var isolated = false
        var producedEvents: String? = null
        try {
            check(ctx.packageName == "com.capstonedesign2026.notificationpreview")
            isolated = true
            MeasurementTelemetry.dataset="synthetic"
            if (phase == "produce") {
                MeasurementTelemetry.configure(ctx,"",false,0)
                verify("collection off has no export", JSONObject(MeasurementTelemetry.batch(ctx,binding)).length()==0)
                val since=System.currentTimeMillis()
                verify("explicit consent persists", JSONObject(MeasurementTelemetry.configure(ctx,binding,true,since)).getBoolean("ok"))
                val p=ctx.getSharedPreferences("garden_activity_notifications",Context.MODE_PRIVATE)
                p.edit().clear().commit() // This isolated synthetic package only.
                ActivityNotifications.resetTestOverrides()
                ActivityNotifications.initialize(ctx,true)
                val minute=Instant.ofEpochMilli(since).atZone(ZoneId.systemDefault()).let {it.hour*60+it.minute}
                ActivityNotifications.configure(ctx,true,minute,false)
                WorkManager.getInstance(ctx).cancelAllWork().result.get(10,TimeUnit.SECONDS)
                verify("real OS accepted due post",ActivityNotifications.postIfDue(ctx))
                val notification=manager.activeNotifications.first {it.id==2620}.notification
                SystemClock.sleep(150)
                runOnMainSync {notification.contentIntent.send()}
                val deadline=SystemClock.elapsedRealtime()+5000
                while(events().length()<3 && SystemClock.elapsedRealtime()<deadline)SystemClock.sleep(30)
                val a=events();val byKind=(0 until a.length()).map {a.getJSONObject(it)}.associateBy {it.getString("kind")}
                verify("due accepted and actual PendingIntent click recorded",byKind.keys==setOf("notification_due","notification_post_accepted","notification_opened"))
                val posted=byKind.getValue("notification_post_accepted");val click=byKind.getValue("notification_opened")
                verify("stable notification identity",byKind.values.map {it.getString("notification_id")}.distinct().size==1)
                verify("click uses actual monotonic time",click.getLong("elapsed_ms")>=posted.getLong("elapsed_ms")+100 && click.getInt("boot_count")==posted.getInt("boot_count"))
                verify("synthetic label retained",byKind.values.all {it.getString("dataset")=="synthetic"})
                runOnMainSync {notification.contentIntent.send()};SystemClock.sleep(150)
                verify("duplicate tap retains first event",events().toString()==a.toString())
                verify("preconsent activity excluded",!JSONObject(MeasurementTelemetry.activity(ctx,"activity_selected","b".repeat(32),"course:MBCT01",since-1,0)).getBoolean("ok"))
                MeasurementTelemetry.activity(ctx,"activity_selected","b".repeat(32),"course:MBCT01",System.currentTimeMillis(),0)
                verify("postconsent activity recorded",events().length()==4)
                producedEvents = events().toString()
                File(out,"pending.json").writeText(batch().toString(2))
                File(out,"expected.json").writeText(JSONObject().put("collection",batch().getString("collection_id")).put("ids",ids(events())).toString())
            } else {
                check(phase=="restore")
                val expected=JSONObject(File(out,"expected.json").readText())
                val originalEvents=JSONObject(File(out,"pending.json").readText()).getJSONArray("events")
                val first=batch();val a=first.getJSONArray("events")
                verify("new process reads original consent collection",first.getString("collection_id")==expected.getString("collection"))
                verify("process restart preserves exact pending events",a.length()==4 && ids(a)==expected.getString("ids") && a.toString()==originalEvents.toString())
                verify("full snapshot has measurement watermark",first.getLong("complete_through_utc_ms")>0)
                MeasurementTelemetry.acknowledge(ctx,binding,first.getString("collection_id"),ids(a),ids(a))
                verify("ack clears pending durable outbox",events().length()==0)
                val ticket=MeasurementTelemetry.ticket(ctx,"synthetic-stale",0,"daily-fixed-v1",0,0,0)
                MeasurementTelemetry.configure(ctx,"",false,0)
                verify("withdrawal removes export",JSONObject(MeasurementTelemetry.batch(ctx,binding)).length()==0)
                MeasurementTelemetry.configure(ctx,binding,true,System.currentTimeMillis())
                verify("new consent changes collection",batch().getString("collection_id")!=first.getString("collection_id"))
                MeasurementTelemetry.notification(ctx,ticket,"notification_opened",System.currentTimeMillis())
                verify("old consent ticket cannot enter new collection",events().length()==0)
                val state=JSONObject(File(ctx.filesDir,"measurement-outbox.json").readText())
                val epoch=state.getLong("since")
                MeasurementTelemetry.activity(ctx,"synthetic-invalid-kind","b".repeat(32),"course:MBCT01",System.currentTimeMillis(),0)
                MeasurementTelemetry.configure(ctx,binding,true,epoch)
                verify("same consent preserves storage failure",batch().getBoolean("storage_error"))
                val alternate=object: ContextWrapper(ctx) {
                    override fun getFilesDir()=File(ctx.cacheDir,"measurement-other-context").apply {mkdirs()}
                }
                fun reload() {
                    MeasurementTelemetry.configure(alternate,"",false,0)
                    MeasurementTelemetry.configure(ctx,binding,true,epoch)
                }
                reload()
                verify("reloaded outbox preserves failed health journal",batch().getBoolean("storage_error"))
                File(ctx.filesDir,"measurement-health.json").writeText(JSONObject().put("collection",state.getString("collection")).put("phase","pending").toString())
                reload()
                verify("interrupted write journal remains incomplete",batch().getBoolean("storage_error"))
                File(ctx.filesDir,"measurement-outbox.json").writeText("synthetic-corrupt-json")
                verify("withdrawal repairs corrupt outbox",JSONObject(MeasurementTelemetry.configure(ctx,"",false,0)).getBoolean("ok"))
                val disabled=JSONObject(File(ctx.filesDir,"measurement-outbox.json").readText())
                verify("withdrawal verifies empty disabled disk state",!disabled.getBoolean("enabled") && disabled.getJSONArray("events").length()==0)
                verify("withdrawal removes health journal",!File(ctx.filesDir,"measurement-health.json").exists())
            }
        } catch (problem:Throwable) {
            failure = problem
        } finally {
            // finish() may end this process. Cleanup must complete and be verified
            // before reporting PASS or calling it. PendingIntent.send() itself does
            // not perform the notification shade's auto-cancel behavior.
            if (isolated) try {
                val p = ctx.getSharedPreferences("garden_activity_notifications",Context.MODE_PRIVATE)
                val disabled = JSONObject(ActivityNotifications.configure(ctx,false,p.getInt("preferredMinute",1200),false))
                verify("cleanup persistently disables fixture reminder",disabled.getString("status")=="AVAILABLE" && !p.getBoolean("enabled",true))
                WorkManager.getInstance(ctx).cancelAllWork().result.get(10,TimeUnit.SECONDS)
                manager.cancelAll()
                val deadline = SystemClock.elapsedRealtime()+5000
                while(manager.activeNotifications.isNotEmpty() && SystemClock.elapsedRealtime()<deadline)SystemClock.sleep(30)
                verify("cleanup leaves no fixture notification",manager.activeNotifications.isEmpty())
                verify("cleanup prevents later worker repost",!ActivityNotifications.postIfDue(ctx))
                if (phase=="produce" && producedEvents!=null)
                    verify("cleanup preserves pending measurement payload",events().toString()==producedEvents)
            } catch (cleanup:Throwable) {
                if(failure==null)failure=cleanup else failure!!.addSuppressed(cleanup)
            } finally { ActivityNotifications.resetTestOverrides() }
        }
        val status = if(failure==null) "PASS" else "FAIL"
        val result = JSONObject().put("status",status).put("processId",android.os.Process.myPid()).put("syntheticOnly",true).put("checks",checks)
        failure?.let { result.put("error",it.stackTraceToString()) }
        File(out,"$phase-result.json").writeText(result.toString(2))
        finish(if(failure==null) -1 else 0,Bundle().apply {putString("stream","$status $phase ${checks.length()} checks\n")})
    }
}
