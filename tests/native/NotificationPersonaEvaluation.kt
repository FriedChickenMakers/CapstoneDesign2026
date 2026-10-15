package com.capstonedesign2026.platform

import java.io.File
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.util.Random
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.roundToInt

/** Synthetic user availability, never wall-clock sleeps or changes to the device clock.
 *
 * A notification can only be clicked after it exists and while its persona is available.
 * Training and frozen-policy evaluation use different seeds AND different virtual dates.
 * This isolates timing quality; delivery reliability and Android deduplication are tested
 * separately, rather than being inferred from an idealized desktop simulation.
 */
internal object NotificationPersonaScenarios {
    val zone: ZoneId = ZoneId.of("Asia/Seoul")
    private const val MINUTE_MS = 60_000L
    const val TRAIN_SEED = 801_771L
    const val EVALUATION_SEED = 902_993L
    private const val TRAIN_OBSERVATIONS = 60
    private const val EVALUATION_DAYS = 90
    const val INITIAL_MINUTE = 20 * 60

    data class Window(val start: Int, val end: Int)
    data class Persona(
        val id: String,
        val windows: List<Window>,
        val jitter: Int,
        val reactionMaximum: Int,
        val unrestricted: Boolean = false,
        val requiresImprovement: Boolean = true
    ) {
        // Predeclared tolerance: the full availability-start variation, natural reaction
        // spread, plus one Android periodic-worker interval. Not a zero-delay target.
        val acceptableP90: Double get() = (2 * jitter + reactionMaximum + 15).toDouble()
    }

    val personas = listOf(
        Persona("night_shift", listOf(Window(70, 160)), 35, 8),
        Persona("student", listOf(Window(17 * 60 + 20, 18 * 60 + 10)), 15, 8),
        Persona("restaurant_owner", listOf(Window(14 * 60 + 30, 16 * 60), Window(22 * 60, 22 * 60 + 45)), 20, 8),
        Persona("office_worker", listOf(Window(20 * 60 + 20, 21 * 60 + 30)), 10, 8, requiresImprovement = false),
        Persona("frequent_phone_user", listOf(Window(0, 1440)), 0, 3, unrestricted = true, requiresImprovement = false)
    )

    private interface Learner {
        val name: String
        val minute: Int
        val accepted: Int
        val estimate: ActivityReminderPolicy.Estimate? get() = null
        fun observe(postedAt: Long, openedAt: Long)
    }

    /** Frozen copy of the production policy before this change, dated 2026-10-15.
     * Deliberately independent of ActivityReminderPolicy so its baseline cannot silently
     * improve when production code changes. Also mirrors the old >=3-click activation.
     */
    private class Legacy : Learner {
        override val name = "legacy_2026_10_15"
        private var learned = INITIAL_MINUTE
        override var accepted = 0
            private set
        override val minute: Int get() = if (accepted >= 3) learned else INITIAL_MINUTE
        override fun observe(postedAt: Long, openedAt: Long) {
            if (postedAt <= 0 || openedAt < postedAt || openedAt - postedAt > 6 * 3_600_000L ||
                !date(postedAt).isEqual(date(openedAt))) return
            val observed = minuteOfDay(openedAt).coerceIn(8 * 60, 21 * 60)
            val step = ((observed - learned) * .25).roundToInt().coerceIn(-15, 15)
            learned = (learned + step).coerceIn(8 * 60, 21 * 60)
            accepted++
        }
    }

    private class Adaptive : Learner {
        override val name = "adaptive"
        override var minute = INITIAL_MINUTE
            private set
        override var accepted = 0
            private set
        private var history = emptyList<ActivityReminderPolicy.Observation>()
        override var estimate: ActivityReminderPolicy.Estimate? = null
            private set
        override fun observe(postedAt: Long, openedAt: Long) {
            val observation = ActivityReminderPolicy.observation(postedAt, openedAt, zone) ?: return
            history = (history + observation).takeLast(ActivityReminderPolicy.MAX_HISTORY)
            val updated = ActivityReminderPolicy.estimate(minute, history)
            estimate = updated
            minute = updated.minute
            accepted++
        }
    }

    private data class Trial(
        val persona: String, val policy: String, val phase: String, val index: Int,
        val scheduledMinute: Int, val postedAt: Long, val openedAt: Long?, val windowHit: Boolean,
        val learnedMinuteAfter: Int
    ) {
        val delay: Double? get() = openedAt?.let { (it - postedAt) / MINUTE_MS.toDouble() }
    }

    private data class Summary(
        val persona: Persona, val policy: String, val finalMinute: Int, val accepted: Int,
        val firstDelay: Double?, val p50: Double, val p90: Double, val iqr: Double,
        val hitRate: Double, val noResponse: Int, val responses: Int,
        val negativeDelays: Int, val duplicatePostDays: Int, val maxTrainingDrift: Int,
        val estimate: ActivityReminderPolicy.Estimate?
    )

    private fun date(epoch: Long) = Instant.ofEpochMilli(epoch).atZone(zone).toLocalDate()
    private fun minuteOfDay(epoch: Long): Int = Instant.ofEpochMilli(epoch).atZone(zone).let { it.hour * 60 + it.minute }
    private fun at(day: LocalDate, minute: Int) = day.atStartOfDay(zone).toInstant().toEpochMilli() + minute * MINUTE_MS
    private fun circularDistance(a: Int, b: Int): Int = abs(a - b).let { minOf(it, 1440 - it) }
    private fun random(persona: Persona, day: LocalDate, seed: Long, salt: Long): Random =
        Random(seed + day.toEpochDay() * 104_729L + persona.id.hashCode().toLong() * 997L + salt)

    private fun windows(persona: Persona, day: LocalDate, seed: Long): List<Pair<Long, Long>> {
        val rng = random(persona, day, seed, 11)
        return persona.windows.map { window ->
            val offset = if (persona.jitter == 0) 0 else rng.nextInt(2 * persona.jitter + 1) - persona.jitter
            at(day, window.start + offset) to at(day, window.end + offset)
        }
    }

    fun isAvailable(persona: Persona, postedAt: Long, seed: Long): Boolean =
        windows(persona, date(postedAt), seed).any { postedAt >= it.first && postedAt < it.second }

    fun nextResponse(persona: Persona, postedAt: Long, seed: Long, permitMiss: Boolean = true): Long? {
        // An exogenous 10% missed-notification rate is identical for both policies on the
        // same virtual date. No response is not interpreted as a negative preference.
        if (permitMiss && random(persona, date(postedAt), seed, 37).nextInt(10) == 0) return null
        for (dayOffset in 0L..2L) {
            val day = date(postedAt).plusDays(dayOffset)
            windows(persona, day, seed).forEachIndexed { windowIndex, (start, end) ->
                val rng = random(persona, day, seed, 53 + windowIndex.toLong())
                val reactionMinutes = if (persona.unrestricted) rng.nextInt(persona.reactionMaximum + 1)
                    else 1 + rng.nextInt(persona.reactionMaximum)
                val openedAt = max(postedAt, start) + reactionMinutes * MINUTE_MS
                if (openedAt < end && openedAt >= start) {
                    check(openedAt >= postedAt) { "A click was generated before its notification" }
                    return openedAt
                }
            }
        }
        return null
    }

    private fun quantile(values: List<Double>, q: Double): Double {
        if (values.isEmpty()) return Double.NaN
        val sorted = values.sorted()
        val position = (sorted.lastIndex * q).coerceIn(0.0, sorted.lastIndex.toDouble())
        val low = position.toInt()
        val high = minOf(low + 1, sorted.lastIndex)
        return sorted[low] + (sorted[high] - sorted[low]) * (position - low)
    }

    private fun evaluate(persona: Persona, learner: Learner, trials: MutableList<Trial>): Summary {
        var lastPostDate = LocalDate.of(2026, 12, 31)
        var cursor = at(lastPostDate, 0)
        var maxDrift = 0
        var firstDelay: Double? = null
        repeat(TRAIN_OBSERVATIONS) { index ->
            // One outstanding synthetic notification at a time. A next-day tap can train
            // the policy; the following notification uses the next future local slot.
            var day = maxOf(lastPostDate.plusDays(1), date(cursor))
            var postedAt = at(day, learner.minute)
            if (postedAt <= cursor) { day = day.plusDays(1); postedAt = at(day, learner.minute) }
            val scheduled = learner.minute
            val openedAt = nextResponse(persona, postedAt, TRAIN_SEED, permitMiss = index > 0)
            if (index == 0) firstDelay = openedAt?.let { (it - postedAt) / MINUTE_MS.toDouble() }
            if (openedAt != null) learner.observe(postedAt, openedAt)
            maxDrift = max(maxDrift, circularDistance(INITIAL_MINUTE, learner.minute))
            trials += Trial(persona.id, learner.name, "train", index, scheduled, postedAt, openedAt,
                isAvailable(persona, postedAt, TRAIN_SEED), learner.minute)
            lastPostDate = day
            cursor = (openedAt ?: postedAt) + 1
        }
        val heldout = (0 until EVALUATION_DAYS).map { index ->
            val postedAt = at(LocalDate.of(2028, 3, 1).plusDays(index.toLong()), learner.minute)
            Trial(persona.id, learner.name, "heldout_frozen", index, learner.minute, postedAt,
                nextResponse(persona, postedAt, EVALUATION_SEED, permitMiss = true),
                isAvailable(persona, postedAt, EVALUATION_SEED), learner.minute)
        }
        trials += heldout
        val delays = heldout.mapNotNull { it.delay }
        val ownTrials = trials.filter { it.persona == persona.id && it.policy == learner.name }
        val duplicateDays = ownTrials.groupBy { it.phase to date(it.postedAt) }.values.count { it.size > 1 }
        return Summary(persona, learner.name, learner.minute, learner.accepted, firstDelay,
            quantile(delays, .5), quantile(delays, .9), quantile(delays, .75) - quantile(delays, .25),
            heldout.count { it.windowHit }.toDouble() / heldout.size,
            heldout.count { it.openedAt == null }, delays.size,
            ownTrials.count { (it.delay ?: 0.0) < 0 }, duplicateDays, maxDrift, learner.estimate)
    }

    private fun number(value: Double?) = if (value == null || !value.isFinite()) "null" else "%.3f".format(java.util.Locale.ROOT, value)
    private fun Summary.json() = """{"persona":"${persona.id}","policy":"$policy","finalMinute":$finalMinute,"acceptedTrainingSamples":$accepted,"firstDeltaMinutes":${number(firstDelay)},"heldoutMedianDeltaMinutes":${number(p50)},"heldoutP90DeltaMinutes":${number(p90)},"heldoutDeltaIqrMinutes":${number(iqr)},"heldoutWindowHitRate":${number(hitRate)},"heldoutNoResponseCount":$noResponse,"heldoutResponseCount":$responses,"negativeDelayCount":$negativeDelays,"duplicatePostDayCount":$duplicatePostDays,"maxTrainingDriftMinutes":$maxTrainingDrift,"predeclaredP90LimitMinutes":${number(persona.acceptableP90)},"estimatedWindowStartMinute":${estimate?.windowStartMinute ?: "null"},"estimatedWindowEndMinute":${estimate?.windowEndMinute ?: "null"},"estimatedSpreadMinutes":${estimate?.spreadMinutes ?: "null"},"estimateStatus":${estimate?.status?.let { "\"$it\"" } ?: "null"}}"""

    fun run(output: File, baselineOnly: Boolean) {
        output.mkdirs()
        val trials = mutableListOf<Trial>()
        val summaries = personas.flatMap { persona ->
            val learners = if (baselineOnly) listOf(Legacy()) else listOf(Legacy(), Adaptive())
            learners.map { evaluate(persona, it, trials) }
        }
        val violations = mutableListOf<String>()
        if (!baselineOnly) for (persona in personas) {
            val old = summaries.single { it.persona == persona && it.policy.startsWith("legacy") }
            val new = summaries.single { it.persona == persona && it.policy == "adaptive" }
            if (new.p90 > persona.acceptableP90) violations += "${persona.id}: p90 ${number(new.p90)} exceeds predeclared jitter/reaction/worker allowance ${persona.acceptableP90}"
            if (new.p50 > old.p50 + 15 || new.p90 > old.p90 + 15) violations += "${persona.id}: response latency regressed beyond one worker interval"
            if (persona.requiresImprovement && new.p50 > old.p50 * .30) violations += "${persona.id}: median wait did not fall by at least 70%"
            if (new.noResponse != old.noResponse) violations += "${persona.id}: exogenous heldout missing-response rate differed"
            if (new.negativeDelays != 0 || new.duplicatePostDays != 0) violations += "${persona.id}: invalid click/daily trace"
            if (persona.unrestricted && new.maxTrainingDrift > 5) violations += "${persona.id}: immediate clicks drifted more than five minutes"
        }
        output.resolve("trials.csv").printWriter().use { writer ->
            writer.println("persona,policy,phase,index,scheduledMinute,postedAtSynthetic,openedAtSynthetic,deltaMinutes,windowHit,learnedMinuteAfter")
            trials.forEach { t -> writer.println("${t.persona},${t.policy},${t.phase},${t.index},${t.scheduledMinute},${t.postedAt},${t.openedAt ?: ""},${t.delay ?: ""},${t.windowHit},${t.learnedMinuteAfter}") }
        }
        output.resolve("summary.json").writeText("""{
  "syntheticOnly":true,"changesOsClock":false,"mode":"${if (baselineOnly) "baseline_only" else "paired_comparison"}",
  "trainingSeed":$TRAIN_SEED,"evaluationSeed":$EVALUATION_SEED,"trainingTrials":$TRAIN_OBSERVATIONS,"heldoutDays":$EVALUATION_DAYS,
  "heldoutPolicyFrozen":true,"assumptions":"Daily jittered availability; clicks generated only after posting; ideal delivery; no clinical or real-user efficacy claim; Android delivery tested separately",
  "criteriaDeclaredBeforeAdaptiveResults":"p90 <= 2*jitter + reaction maximum + 15m; no median/p90 regression >15m; night/student/restaurant median improves >=70%; immediate-user drift <=5m; paired miss rates; no negative delays or duplicate calendar-day posts",
  "results":[${summaries.joinToString(",\n") { it.json() }}],
  "violations":[${violations.joinToString(",") { "\"$it\"" }}]
}
""")
        println("persona policy final-time first-delta heldout-median p90 IQR window-hit misses")
        for (s in summaries) println("${s.persona.id} ${s.policy} ${s.finalMinute / 60}:${(s.finalMinute % 60).toString().padStart(2, '0')} ${number(s.firstDelay)} ${number(s.p50)} ${number(s.p90)} ${number(s.iqr)} ${number(s.hitRate)} ${s.noResponse}/${EVALUATION_DAYS}")
        violations.forEach { System.err.println("FAIL $it") }
        check(violations.isEmpty()) { "${violations.size} predeclared persona criteria failed; see ${output.resolve("summary.json")}" }
        println("RESULT ${if (baselineOnly) "baseline evidence recorded" else "all five synthetic persona criteria passed"}; virtual dates only, zero real-time waits")
    }
}

fun main(args: Array<String>) {
    val output = File(System.getenv("NOTIFICATION_PERSONA_OUTPUT") ?: "artifacts/notification-personas")
    NotificationPersonaScenarios.run(output, "--baseline-only" in args)
}
