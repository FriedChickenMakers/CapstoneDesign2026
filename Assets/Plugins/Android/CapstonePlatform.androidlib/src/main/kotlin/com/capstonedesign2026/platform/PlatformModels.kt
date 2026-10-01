package com.capstonedesign2026.platform

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject

internal object PlatformStatus {
    const val AVAILABLE = "AVAILABLE"
    const val UNSUPPORTED = "UNSUPPORTED"
    const val PERMISSION_REQUIRED = "PERMISSION_REQUIRED"
    const val PERMISSION_DENIED = "PERMISSION_DENIED"
    const val NO_DATA = "NO_DATA"
    const val SERVICE_UNAVAILABLE = "SERVICE_UNAVAILABLE"
    const val DISCONNECTED = "DISCONNECTED"
    const val STALE = "STALE"
    const val ERROR = "ERROR"
}

internal object PermissionState {
    private const val PREFERENCES = "capstone_platform_permission_state"
    private const val HEALTH_REQUEST_COMPLETED = "health_request_completed"

    fun markRuntimeRequested(context: Context, permission: String) {
        preferences(context).edit().putBoolean(runtimeKey(permission), true).apply()
    }

    fun wasRuntimeRequested(context: Context, permission: String): Boolean =
        preferences(context).getBoolean(runtimeKey(permission), false)

    fun markHealthRequestCompleted(context: Context) {
        preferences(context).edit().putBoolean(HEALTH_REQUEST_COMPLETED, true).apply()
    }

    fun wasHealthRequestCompleted(context: Context): Boolean =
        preferences(context).getBoolean(HEALTH_REQUEST_COMPLETED, false)

    private fun preferences(context: Context) =
        context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    private fun runtimeKey(permission: String): String = "runtime_requested_$permission"
}

internal fun resultJson(
    status: String,
    message: String = "",
    errorCode: String = "",
    data: JSONObject? = null,
): String = JSONObject()
    .put("status", status)
    .put("message", message)
    .put("errorCode", errorCode)
    .put("data", data ?: JSONObject.NULL)
    .toString()

internal fun JSONArray.putFloats(values: FloatArray): JSONArray {
    values.forEach { put(it.toDouble()) }
    return this
}

internal fun Throwable.safeMessage(): String =
    message?.take(240) ?: javaClass.simpleName
