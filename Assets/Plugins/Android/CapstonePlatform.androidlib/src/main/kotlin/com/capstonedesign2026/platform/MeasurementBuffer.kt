package com.capstonedesign2026.platform

/** Consent-bound, bounded outbox. Pure Kotlin; no Android identifiers or user text. */
internal data class MeasurementRecord(
    val eventId: String, val notificationId: String = "", val sessionKey: String = "",
    val kind: String, val occurred: Long, val elapsed: Long = -1, val boot: Int = -1,
    val offset: Int = 0, val scheduled: Long = 0, val policy: String = "none",
    val preferred: Int = -1, val effective: Int = -1, val samples: Int = 0,
    val reason: String = "none", val activity: String = "", val appVersion: String,
    val dataset: String = "live"
)
internal data class MeasurementState(
    val binding: String = "", val collection: String = "", val since: Long = 0,
    val enabled: Boolean = false, val events: List<MeasurementRecord> = emptyList(),
    val receipts: Map<String, Long> = emptyMap(), val dropped: Long = 0, val expired: Long = 0
)
internal interface MeasurementStore { fun load(): MeasurementState; fun save(state: MeasurementState) }
internal class MeasurementBuffer(private val store: MeasurementStore, private val newId: () -> String) {
    companion object { const val CAPACITY = 1024; const val RECEIPTS = 8192; const val RETENTION = 90L * 86400000 }
    var state = store.load(); private set
    init {
        require(state.events.size <= CAPACITY && state.receipts.size <= RECEIPTS && state.dropped >= 0 && state.expired >= 0)
        require(!state.enabled || (state.binding.matches(Regex("[a-f0-9]{64}")) && state.collection.matches(Regex("[a-f0-9]{32}")) && state.since > 0))
        require(state.enabled || state.events.isEmpty() && state.receipts.isEmpty())
        require(state.events.all { it.occurred >= state.since } && state.events.map { it.eventId }.distinct().size == state.events.size)
    }
    private fun commit(next: MeasurementState) { store.save(next); state = next }
    fun configure(binding: String, enabled: Boolean, since: Long) {
        require(!enabled || (binding.matches(Regex("[a-f0-9]{64}")) && since > 0))
        if (state.enabled == enabled && (!enabled || state.binding == binding && state.since == since)) return
        commit(if (enabled) MeasurementState(binding, newId(), since, true) else MeasurementState())
    }
    fun prune(now: Long) {
        val kept = state.events.filter { it.occurred >= now - RETENTION }
        val receipts = state.receipts.filterValues { it >= now - RETENTION }
        if (kept.size != state.events.size || receipts.size != state.receipts.size)
            commit(state.copy(events = kept, receipts = receipts, expired = state.expired + state.events.size - kept.size))
    }
    fun add(record: MeasurementRecord, now: Long): Boolean {
        if (!state.enabled || record.occurred < state.since || record.occurred < now - RETENTION || record.occurred > now + 300000) return false
        prune(now)
        if (record.eventId in state.receipts) return true
        if (state.events.size >= CAPACITY || state.receipts.size >= RECEIPTS) {
            commit(state.copy(dropped = state.dropped + 1)); return false
        }
        commit(state.copy(events = state.events + record, receipts = state.receipts + (record.eventId to record.occurred)))
        return true
    }
    fun batch(now: Long): List<MeasurementRecord> { prune(now); return if (state.enabled) state.events.take(100) else emptyList() }
    fun acknowledge(binding: String, collection: String, ids: Set<String>, sent: Set<String>) {
        if (!state.enabled || state.binding != binding || state.collection != collection) return
        require(sent.containsAll(ids)) { "Acknowledgement includes unsent measurements" }
        commit(state.copy(events = state.events.filterNot { it.eventId in ids }))
    }
}
