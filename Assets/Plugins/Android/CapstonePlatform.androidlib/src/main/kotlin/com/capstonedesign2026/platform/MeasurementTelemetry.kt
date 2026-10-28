package com.capstonedesign2026.platform

import android.content.Context
import android.os.SystemClock
import android.provider.Settings
import android.util.AtomicFile
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.security.MessageDigest
import java.time.Instant
import java.time.ZoneId
import java.util.UUID

/** App-private research outbox. Default OFF; only explicit version-2 consent enables it. */
internal object MeasurementTelemetry {
    internal var dataset = "live" // Only isolated instrumentation may set "synthetic".
    private var buffer: MeasurementBuffer? = null
    private var location = ""
    private var error = false
    private var suspended = false
    private fun stateFile(context: Context) = File(context.filesDir, "measurement-outbox.json")
    private fun healthFile(context: Context) = File(context.filesDir, "measurement-health.json")
    private fun writeAtomic(file: File, value: String) {
        val atomic = AtomicFile(file)
        val stream = atomic.startWrite()
        try { stream.write(value.toByteArray(Charsets.UTF_8)); atomic.finishWrite(stream) }
        catch (failure: Exception) { atomic.failWrite(stream); throw failure }
        check(atomic.openRead().use { it.readBytes().toString(Charsets.UTF_8) } == value) { "Measurement write verification failed" }
    }
    private fun health(context: Context, collection: String, phase: String) =
        writeAtomic(healthFile(context), JSONObject().put("collection", collection).put("phase", phase).toString())
    private fun healthIsClean(context: Context, collection: String): Boolean = try {
        val j = JSONObject(AtomicFile(healthFile(context)).openRead().use { it.readBytes().toString(Charsets.UTF_8) })
        j.getString("collection") == collection && j.getString("phase") == "clean"
    } catch (_: Exception) { false }
    private fun stateJson(state: MeasurementState) = JSONObject().put("schema", 1).put("binding", state.binding)
        .put("collection", state.collection).put("since", state.since).put("enabled", state.enabled)
        .put("dropped", state.dropped).put("expired", state.expired)
        .put("events", JSONArray().apply { state.events.forEach { put(recordJson(it)) } })
        .put("receipts", JSONObject(state.receipts)).toString()
    private fun latchError(context: Context) {
        error = true
        val current = buffer?.takeIf { location == stateFile(context).path }?.state
        if (current?.enabled == true) try { health(context, current.collection, "failed") } catch (_: Exception) { }
    }
    private fun id() = UUID.randomUUID().toString().replace("-", "")
    internal fun key(value: String) = MessageDigest.getInstance("SHA-256").digest(value.toByteArray())
        .joinToString("") { "%02x".format(it) }.take(32)
    private fun recordJson(r: MeasurementRecord) = JSONObject().put("event_id", r.eventId)
        .put("notification_id", r.notificationId).put("session_key", r.sessionKey).put("kind", r.kind)
        .put("occurred_utc_ms", r.occurred).put("elapsed_ms", r.elapsed).put("boot_count", r.boot)
        .put("utc_offset_minutes", r.offset).put("scheduled_utc_ms", r.scheduled).put("policy_id", r.policy)
        .put("preferred_minute", r.preferred).put("effective_minute", r.effective).put("learned_samples", r.samples)
        .put("reason", r.reason).put("activity_id", r.activity).put("app_version", r.appVersion).put("dataset", r.dataset)
    private fun readRecord(j: JSONObject) = MeasurementRecord(j.getString("event_id"), j.getString("notification_id"),
        j.getString("session_key"), j.getString("kind"), j.getLong("occurred_utc_ms"), j.getLong("elapsed_ms"),
        j.getInt("boot_count"), j.getInt("utc_offset_minutes"), j.getLong("scheduled_utc_ms"), j.getString("policy_id"),
        j.getInt("preferred_minute"), j.getInt("effective_minute"), j.getInt("learned_samples"), j.getString("reason"),
        j.getString("activity_id"), j.getString("app_version"), j.getString("dataset"))
    private fun get(context: Context): MeasurementBuffer {
        val file = stateFile(context)
        if (buffer != null && location == file.path) return buffer!!
        error = false
        val atomic = AtomicFile(file)
        var savedCollection = ""
        val store = object : MeasurementStore {
            override fun load(): MeasurementState {
                if (!file.exists() && !File(file.path + ".bak").exists()) return MeasurementState()
                val j = JSONObject(atomic.openRead().use { it.readBytes().toString(Charsets.UTF_8) })
                require(j.getInt("schema") == 1)
                val events = j.getJSONArray("events"); val receipts = j.getJSONObject("receipts")
                require(events.length() <= MeasurementBuffer.CAPACITY && receipts.length() <= MeasurementBuffer.RECEIPTS)
                val state = MeasurementState(j.getString("binding"), j.getString("collection"), j.getLong("since"), j.getBoolean("enabled"),
                    (0 until events.length()).map { readRecord(events.getJSONObject(it)) },
                    receipts.keys().asSequence().associateWith { receipts.getLong(it) }, j.getLong("dropped"), j.getLong("expired"))
                savedCollection = state.collection
                // A killed write leaves pending; an old/missing journal cannot prove
                // that this collection avoided an unreported write failure.
                if (state.enabled && !healthIsClean(context, state.collection)) error = true
                return state
            }
            override fun save(state: MeasurementState) {
                val newCollection = state.enabled && state.collection != savedCollection
                val hadError = error && !newCollection
                try {
                    if (state.enabled) health(context, state.collection, if (hadError) "failed" else "pending")
                    writeAtomic(file, stateJson(state))
                    savedCollection = state.collection
                    error = hadError
                } catch (failure: Exception) {
                    error = true
                    if (state.enabled) try { health(context, state.collection, "failed") } catch (_: Exception) { }
                    throw failure
                }
                // If clearing the journal fails after the outbox committed, preserve
                // the new in-memory state too. Pending/failed remains conservative.
                if (state.enabled && !hadError) try { health(context, state.collection, "clean") }
                catch (_: Exception) {
                    error = true
                    try { health(context, state.collection, "failed") } catch (_: Exception) { }
                }
            }
        }
        return MeasurementBuffer(store, ::id).also { buffer = it; location = file.path }
    }
    private fun version(context: Context): String = (context.packageManager.getPackageInfo(context.packageName, 0).versionName ?: "unknown")
        .replace(Regex("[^A-Za-z0-9._-]"), "_").take(48)
    private fun base(context: Context, kind: String, at: Long) = MeasurementRecord(id(), kind = kind, occurred = at,
        elapsed = SystemClock.elapsedRealtime(), boot = Settings.Global.getInt(context.contentResolver, Settings.Global.BOOT_COUNT, -1),
        offset = Instant.ofEpochMilli(at).atZone(ZoneId.systemDefault()).offset.totalSeconds / 60,
        appVersion = version(context), dataset = dataset)
    fun configure(context: Context, binding: String, enabled: Boolean, since: Long): String = synchronized(this) {
        if (!enabled) suspended = true // Stop in memory even if the disk rejects withdrawal.
        try {
            if (!enabled) {
                // Withdrawal must work even when a previous outbox cannot be decoded.
                // Replace and read back the canonical disabled state before acknowledging.
                writeAtomic(stateFile(context), stateJson(MeasurementState()))
                AtomicFile(healthFile(context)).delete()
                check(listOf("", ".bak", ".new").none { File(healthFile(context).path + it).exists() })
                buffer = null; location = ""; error = false
            } else get(context).configure(binding, true, since)
            suspended = !enabled
            JSONObject().put("ok", true).toString()
        } catch (_: Exception) { latchError(context); JSONObject().put("ok", false).toString() }
    }
    // Tickets are internal notification metadata, never study credentials. A changed
    // consent epoch makes already displayed notifications ineligible for later collection.
    fun ticket(context: Context, identity: String, scheduled: Long, policy: String, preferred: Int, effective: Int, samples: Int): String = synchronized(this) {
        try {
            val state = get(context).state
            if (suspended || !state.enabled) return ""
            JSONObject().put("id", key(state.collection + identity)).put("collection", state.collection)
                .put("scheduled", scheduled).put("policy", policy).put("preferred", preferred)
                .put("effective", effective).put("samples", samples).toString()
        } catch (_: Exception) { latchError(context); "" }
    }
    fun notification(context: Context, ticket: String, kind: String, at: Long, reason: String = "none") = synchronized(this) {
        if (ticket.isBlank()) return@synchronized
        try {
            val b = get(context); val t = JSONObject(ticket)
            if (suspended || !b.state.enabled || b.state.collection != t.getString("collection")) return@synchronized
            val nid = t.getString("id")
            // First accepted post/click wins. Retry/duplicate taps do not alter its timestamp.
            val r = base(context, kind, at).copy(eventId = key(nid + kind + reason), notificationId = nid,
                scheduled = t.getLong("scheduled"), policy = t.getString("policy"), preferred = t.getInt("preferred"),
                effective = t.getInt("effective"), samples = t.getInt("samples"), reason = reason)
            b.add(r, at)
        } catch (_: Exception) { latchError(context) }
    }
    fun activity(context: Context, kind: String, sessionKey: String, activity: String, at: Long, offset: Int): String = synchronized(this) {
        try {
            if (suspended) return "{\"ok\":false}"
            require(kind in setOf("activity_selected", "activity_started", "activity_completed", "activity_ended"))
            require(sessionKey.matches(Regex("[a-f0-9]{32}")) && activity.matches(Regex("(?:course:MBCT(?:2W|4W)?[0-9]{2}|free-mission:M[0-9]{2})")))
            val b = get(context)
            val r = base(context, kind, at).copy(eventId = key(b.state.collection + sessionKey + kind), sessionKey = sessionKey,
                activity = activity, elapsed = -1, boot = -1, offset = offset) // recovered wall time, never fabricated monotonic time
            JSONObject().put("ok", b.add(r, System.currentTimeMillis())).toString()
        } catch (_: Exception) { latchError(context); "{\"ok\":false}" }
    }
    fun batch(context: Context, binding: String): String = synchronized(this) {
        try {
            val b = get(context)
            if (suspended || !b.state.enabled || b.state.binding != binding) return "{}"
            val snapshot = b.batchSnapshot(System.currentTimeMillis())
            JSONObject().put("schema_version", 1).put("consent_version", 2).put("collection_id", b.state.collection)
                .put("dropped_attempts", b.state.dropped).put("expired_events", b.state.expired).put("storage_error", error)
                .put("complete_through_utc_ms", snapshot.completeThrough)
                .put("events", JSONArray().apply { snapshot.events.forEach { put(recordJson(it)) } }).toString()
        } catch (_: Exception) { latchError(context); "{}" }
    }
    fun acknowledge(context: Context, binding: String, collection: String, accepted: String, sent: String): String = synchronized(this) {
        try {
            fun ids(s: String): Set<String> { val a = JSONArray(s); return (0 until a.length()).map { a.getString(it) }.toSet() }
            get(context).acknowledge(binding, collection, ids(accepted), ids(sent)); "{\"ok\":true}"
        } catch (_: Exception) { latchError(context); "{\"ok\":false}" }
    }
}
