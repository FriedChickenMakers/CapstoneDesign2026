package com.capstonedesign2026.platform

import kotlinx.coroutines.*

fun main() = runBlocking {
    var passed = 0
    suspend fun test(name: String, block: suspend () -> Unit) { block(); passed++; println("PASS $name") }
    test("multiple pages and empty terminal token") {
        val result = HealthQueryLogic.pages { token -> if (token == null) HealthQueryLogic.Page(listOf(1), "next") else HealthQueryLogic.Page(listOf(2), "") }
        check(result.complete && result.records == listOf(1, 2) && result.pages == 2)
    }
    test("repeated token bounded") {
        val result = HealthQueryLogic.pages { HealthQueryLogic.Page(listOf(1), "same") }
        check(!result.complete && result.reason == "REPEATED_TOKEN" && result.pages == 2)
    }
    test("page limit partial") {
        var n = 0
        val result = HealthQueryLogic.pages(maxPages = 2) { HealthQueryLogic.Page(listOf(++n), n.toString()) }
        check(result.reason == "PAGE_LIMIT" && result.records.size == 2)
    }
    test("permission revoked mid pagination preserves partial data without retry") {
        var n = 0
        val result = HealthQueryLogic.pages { if (++n == 1) HealthQueryLogic.Page(listOf(42), "next") else throw SecurityException() }
        check(result.reason == "PERMISSION_REVOKED" && result.records == listOf(42) && n == 2)
    }
    test("quota retry bounded") {
        var n = 0
        val result = HealthQueryLogic.pages<Int> { n++; throw IllegalStateException() }
        check(result.reason == "READ_FAILED" && n == 3)
    }
    test("timeout preserves earlier pages") {
        val result = HealthQueryLogic.pages(timeoutMs = 20) { token -> if (token == null) HealthQueryLogic.Page(listOf(1), "next") else { delay(100); HealthQueryLogic.Page(listOf(2), null) } }
        check(result.reason == "TIMEOUT" && result.records == listOf(1))
    }
    test("external cancellation propagates") {
        var cancelled = false
        val job = launch { try { HealthQueryLogic.pages<Int> { delay(1000); HealthQueryLogic.Page(emptyList(), null) } } catch (e: CancellationException) { cancelled = true } }
        yield(); job.cancelAndJoin(); check(cancelled)
    }
    test("explicit read cancellation is not retried") {
        var attempts = 0
        var cancelled = false
        try { HealthQueryLogic.pages<Int> { attempts++; throw CancellationException("cancel read") } }
        catch (_: CancellationException) { cancelled = true }
        check(cancelled && attempts == 1)
    }
    test("parent timeout propagates instead of becoming partial") {
        var propagated = false
        try { withTimeout(20) { HealthQueryLogic.pages<Int> { delay(100); HealthQueryLogic.Page(emptyList(), null) } } }
        catch (_: TimeoutCancellationException) { propagated = true }
        check(propagated)
    }
    test("empty read complete distinct from incomplete") {
        val result = HealthQueryLogic.pages<Int> { HealthQueryLogic.Page(emptyList(), null) }
        check(result.complete && result.records.isEmpty())
    }
    fun sample(t: Long, source: String = "a", id: String = "record") = HealthQueryLogic.Sample(id, source, t, 70)
    test("sample timestamps filter half open window independently of record interval") {
        val result = HealthQueryLogic.series(listOf(sample(9), sample(10), sample(19), sample(20)), 10, 20)
        check(result.samples.map { it.measuredAtEpochMs } == listOf(10L,19L))
    }
    test("latest on later page sorted with replay duplicates removed") {
        val result = HealthQueryLogic.series(listOf(sample(30), sample(10), sample(30), sample(50)), 0, 100)
        check(result.samples.size == 3 && result.samples.last().measuredAtEpochMs == 50L)
    }
    test("different origins never joined; missing periods and isolated sample kept") {
        val result = HealthQueryLogic.series(listOf(sample(10), sample(800_000), sample(12, "b")), 0, 1_500_000)
        check(result.samples.map { it.segmentId }.distinct().size == 3 && result.gaps.size == 3)
    }
    test("no samples entire window missing") {
        val result = HealthQueryLogic.series(emptyList(), 0, 100)
        check(result.gaps.single().endEpochMs == 100L && result.display.isEmpty())
    }
    test("display downsample preserves raw count and endpoints") {
        val result = HealthQueryLogic.series((0L..999L).map { sample(it) }, 0, 1000, maxDisplay = 20)
        check(result.samples.size == 1000 && result.display.size == 20)
        check(result.display.first().measuredAtEpochMs == 0L && result.display.last().measuredAtEpochMs == 999L)
    }
    test("local midnight uses fixed instant and timezone including DST") {
        val instant = java.time.Instant.parse("2026-03-08T15:00:00Z")
        val zone = java.time.ZoneId.of("America/New_York")
        check(instant.atZone(zone).toLocalDate().atStartOfDay(zone).toInstant().toString() == "2026-03-08T05:00:00Z")
    }
    val watch = HealthSourcePolicy.Source("com.sec.android.app.shealth", 1)
    val phone = HealthSourcePolicy.Source("com.android.healthconnect.phone", 2)
    val samsungUnknown = HealthSourcePolicy.Source("com.sec.android.app.shealth", null)
    fun span(id: String, source: HealthSourcePolicy.Source, from: Long, until: Long, count: Long) =
        HealthSourcePolicy.StepSpan(id, source, from, until, count)
    test("watch beats duplicate phone steps without adding origins") {
        val selected = HealthSourcePolicy.steps(listOf(span("watch", watch, 0, 100, 10),
            span("phone", phone, 0, 100, 10)), 0, 100)!!
        check(selected.count == 10L && selected.source == watch)
    }
    test("Samsung unknown device is preferred over phone, but phone is the fallback") {
        val samsung = HealthSourcePolicy.steps(listOf(span("s", samsungUnknown, 0, 100, 8),
            span("p", phone, 0, 100, 10)), 0, 100)!!
        val fallback = HealthSourcePolicy.steps(listOf(span("p", phone, 0, 100, 10)), 0, 100)!!
        check(samsung.count == 8L && samsung.source == samsungUnknown)
        check(fallback.count == 10L && fallback.source == phone)
    }
    test("zero watch record falls back to actual phone movement") {
        val selected = HealthSourcePolicy.steps(listOf(span("w", watch, 0, 100, 0),
            span("p", phone, 0, 100, 10)), 0, 100)!!
        check(selected.count == 10L && selected.source == phone)
    }
    test("same-source replay and overlapping intervals are not added twice") {
        val total = HealthSourcePolicy.steps(listOf(span("a", watch, 0, 100, 10),
            span("b", watch, 0, 100, 10), span("c", watch, 50, 150, 10)), 0, 150)!!
        check(total.count == 15L)
    }
    test("records crossing the requested start are clipped") {
        val total = HealthSourcePolicy.steps(listOf(span("a", phone, 0, 100, 10)), 50, 100)!!
        check(total.count == 5L)
    }
    test("health types choose sources independently and stale watch heart rate falls back") {
        val steps = HealthSourcePolicy.steps(listOf(span("p", phone, 0, 100, 10)), 0, 100)!!
        val heart = HealthSourcePolicy.select(listOf(
            HealthSourcePolicy.Candidate(70, watch, 1L),
            HealthSourcePolicy.Candidate(80, phone, 100L)), 100L, freshnessMs = 20L)!!
        val sleep = HealthSourcePolicy.select(listOf(
            HealthSourcePolicy.Candidate(420, watch, 90L),
            HealthSourcePolicy.Candidate(360, phone, 100L)), 100L)!!
        check(steps.source == phone && heart.source == phone && heart.values.single() == 80)
        check(sleep.source == watch && sleep.values.single() == 420)
    }
    test("sensor log bounded rotation retains previous generation") {
        val directory = java.nio.file.Files.createTempDirectory("synthetic-sensor-log").toFile()
        try {
            val file = java.io.File(directory, "sensor_samples.jsonl")
            val log = BoundedSensorLog(file, 20)
            log.append(listOf("SYNTHETIC-A")); log.append(listOf("SYNTHETIC-B"))
            check(file.length() <= 20 && log.tail(1) == listOf("SYNTHETIC-B"))
            check(java.io.File(directory, "sensor_samples.previous.jsonl").readText() == "SYNTHETIC-A\n")
        } finally { directory.deleteRecursively() }
    }
    test("sensor truncated tail cannot swallow next complete sample") {
        val directory = java.nio.file.Files.createTempDirectory("synthetic-sensor-tail").toFile()
        try {
            val file = java.io.File(directory, "sensor_samples.jsonl")
            file.writeText("complete\nbroken")
            val log = BoundedSensorLog(file, 100)
            log.append(listOf("new-complete"))
            check(log.tail(2) == listOf("broken", "new-complete"))
        } finally { directory.deleteRecursively() }
    }
    test("sensor failed rotation preserves active log") {
        val directory = java.nio.file.Files.createTempDirectory("synthetic-sensor-rotation").toFile()
        try {
            val file = java.io.File(directory, "sensor_samples.jsonl")
            file.writeText("SYNTHETIC-A\n")
            val previous = java.io.File(directory, "sensor_samples.previous.jsonl")
            previous.mkdir(); java.io.File(previous, "block").writeText("synthetic")
            var failed = false
            try { BoundedSensorLog(file, 20).append(listOf("SYNTHETIC-B")) } catch (_: IllegalStateException) { failed = true }
            check(failed && file.readText() == "SYNTHETIC-A\n")
        } finally { directory.deleteRecursively() }
    }
    test("sensor oversized batch rejected before replacing log") {
        val directory = java.nio.file.Files.createTempDirectory("synthetic-sensor-batch").toFile()
        try {
            val file = java.io.File(directory, "sensor_samples.jsonl")
            file.writeText("ok\n")
            var failed = false
            try { BoundedSensorLog(file, 10).append(listOf("a".repeat(11))) } catch (_: IllegalArgumentException) { failed = true }
            check(failed && file.readText() == "ok\n")
        } finally { directory.deleteRecursively() }
    }
    println("RESULT $passed passed, 0 failed; synthetic JVM only")
}
