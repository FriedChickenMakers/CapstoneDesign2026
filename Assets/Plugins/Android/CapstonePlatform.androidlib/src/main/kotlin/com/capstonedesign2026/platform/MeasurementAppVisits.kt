package com.capstonedesign2026.platform

import android.app.Activity
import android.app.Application
import android.os.Bundle
import android.os.SystemClock
import org.json.JSONObject
import java.util.UUID

/** Foreground means the Unity Activity is STARTED. A keyboard/time picker can
 * change focus or pause it without STOP; those are not new application visits. */
internal class ForegroundVisitClock(private val now: () -> Long, private val newId: () -> String) {
    var key = newId(); private set
    var foreground = false; private set
    private var stoppedAt: Long? = null
    fun start() {
        if (foreground) return
        val stopped = stoppedAt
        if (stopped != null && now() - stopped >= 30000) key = newId()
        foreground = true; stoppedAt = null
    }
    fun stop() {
        if (!foreground) return // Duplicate lifecycle callbacks must not move the boundary.
        stoppedAt = now(); foreground = false
    }
}

internal object MeasurementAppVisits {
    private var app: Application? = null
    private var activityClass = ""
    private val started = mutableSetOf<Activity>()
    private var visits = newClock()
    private fun newClock() = ForegroundVisitClock(SystemClock::elapsedRealtime) { UUID.randomUUID().toString().replace("-", "") }
    private val callbacks = object : Application.ActivityLifecycleCallbacks {
        override fun onActivityStarted(activity: Activity) = synchronized(MeasurementAppVisits) {
            if (activity.javaClass.name == activityClass && started.add(activity)) visits.start()
        }
        override fun onActivityStopped(activity: Activity) = stopped(activity)
        override fun onActivityDestroyed(activity: Activity) = stopped(activity)
        override fun onActivityCreated(activity: Activity, state: Bundle?) { }
        override fun onActivityResumed(activity: Activity) { }
        override fun onActivityPaused(activity: Activity) { }
        override fun onActivitySaveInstanceState(activity: Activity, state: Bundle) { }
    }
    private fun stopped(activity: Activity) = synchronized(this) {
        if (started.remove(activity) && started.isEmpty()) visits.stop()
    }
    fun initialize(activity: Activity) = synchronized(this) {
        if (app !== activity.application) {
            app?.unregisterActivityLifecycleCallbacks(callbacks)
            started.clear(); visits = newClock()
            app = activity.application
            app!!.registerActivityLifecycleCallbacks(callbacks)
        }
        activityClass = activity.javaClass.name
        // The first JNI initialization happens after this Activity's START event.
        // Subsequent START/STOP events are received directly from Android.
        if (started.add(activity)) visits.start()
    }
    fun current(): String = synchronized(this) {
        JSONObject().put("visit_key", visits.key).put("foreground", visits.foreground && started.isNotEmpty()).toString()
    }
}
