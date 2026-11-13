package com.capstonedesign2026.platform

import android.content.Context
import android.os.Build
import android.os.SystemClock
import org.json.JSONObject

/** QA choice is frozen in each event, never applied to an existing outbox at upload. */
internal object DebugTelemetry {
    private fun prefs(c: Context) = c.getSharedPreferences("capstone_debug_telemetry", Context.MODE_PRIVATE)
    @Synchronized fun configure(c: Context, enabled: Boolean, qa: Boolean, build: String): String {
        if (!build.matches(Regex("[A-Za-z0-9._-]{1,80}"))) return "{\"ok\":false}"
        val current=prefs(c)
        if(current.getBoolean("enabled",false)==enabled && current.getBoolean("qa",false)==(enabled && qa) && current.getString("build","")==build)return "{\"ok\":true}"
        val ok = prefs(c).edit().putBoolean("enabled", enabled).putBoolean("qa", enabled && qa).putString("build", build).commit()
        return JSONObject().put("ok", ok).toString()
    }
    private var notification = ""
    private var notificationUntil = 0L
    private var notificationVisit = ""
    @Synchronized fun opened(id: String) { notification=id; notificationVisit=""; notificationUntil=SystemClock.elapsedRealtime()+60000 }
    fun context(c: Context): JSONObject {
        val p=prefs(c)
        val visit=JSONObject(MeasurementAppVisits.current())
        val visitKey=if(visit.optBoolean("foreground"))visit.optString("visit_key","") else ""
        if(notificationVisit.isEmpty() && visitKey.isNotEmpty() && SystemClock.elapsedRealtime()<=notificationUntil)notificationVisit=visitKey
        return JSONObject().put("version",2).put("classified",true).put("qa",p.getBoolean("qa",false))
            .put("debug",p.getBoolean("enabled",false)).put("visit_id",if(visit.optBoolean("foreground"))visit.optString("visit_key","") else "")
            .put("source_notification_id",if(visitKey.isNotEmpty() && visitKey==notificationVisit)notification else "")
            .put("build_id",p.getString("build","unknown")).put("os_api",Build.VERSION.SDK_INT)
    }
    @Synchronized fun transport(c: Context, category: String) {
        if(category !in setOf("none","network","auth","server","client","ack","storage"))return
        val p=prefs(c);val edit=p.edit().putString("last_error",category)
        if(category=="none")edit.putLong("last_success",System.currentTimeMillis())
        else edit.putLong("retry_count",p.getLong("retry_count",0)+1)
        edit.commit()
    }
    fun quality(c: Context, state: MeasurementState, now: Long): JSONObject {
        val p=prefs(c)
        return JSONObject().put("pending_count",state.events.size)
            .put("oldest_pending_ms",state.events.maxOfOrNull { (now-it.occurred).coerceAtLeast(0) } ?: 0).put("retry_count",p.getLong("retry_count",0))
            .put("last_error",p.getString("last_error","none")).put("last_success_utc_ms",p.getLong("last_success",0))
    }
}
