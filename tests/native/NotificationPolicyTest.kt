package com.capstonedesign2026.platform

import java.time.Instant
import java.time.ZoneId

fun main() {
    var passed = 0
    fun test(name: String, block: () -> Unit) { block(); passed++; println("PASS $name") }
    val zone = ZoneId.of("Asia/Seoul")
    fun at(value: String) = Instant.parse(value).toEpochMilli()
    val day = "2026-10-12"
    val eightPm = at("2026-10-12T11:00:00Z")
    test("daily reminder only becomes due at preferred time") {
        check(!ActivityReminderPolicy.due(eightPm - 1, zone, 1200, "", ""))
        check(ActivityReminderPolicy.due(eightPm, zone, 1200, "", ""))
    }
    test("one reminder per scheduled day even after clock reversal") {
        check(!ActivityReminderPolicy.due(eightPm, zone, 1200, day, ""))
        check(!ActivityReminderPolicy.due(eightPm - 86400000, zone, 1200, day, ""))
        check(ActivityReminderPolicy.due(eightPm + 86400000, zone, 1200, day, ""))
    }
    test("completion suppresses that day and past days after timezone changes") {
        check(!ActivityReminderPolicy.due(eightPm, zone, 1200, "", day))
        check(!ActivityReminderPolicy.due(eightPm - 86400000, zone, 1200, "", day))
        check(ActivityReminderPolicy.due(eightPm + 86400000, zone, 1200, "", day))
    }
    test("late reminder catches up through midnight using its scheduled day") {
        val afterMidnight = at("2026-10-12T15:15:00Z")
        val occurrence = ActivityReminderPolicy.dueOccurrence(afterMidnight, zone, 23 * 60 + 30)!!
        check(occurrence.scheduledDay == day)
        check(occurrence.scheduledEpochMs == at("2026-10-12T14:30:00Z"))
        check(ActivityReminderPolicy.due(afterMidnight, zone, 1410, "", ""))
        check(!ActivityReminderPolicy.due(afterMidnight, zone, 1410, day, ""))
        check(ActivityReminderPolicy.due(at("2026-10-13T14:30:00Z"), zone, 1410, day, "", afterMidnight))
        check(!ActivityReminderPolicy.due(afterMidnight, zone, 1410, "", "2026-10-13"))
        check(!ActivityReminderPolicy.due(afterMidnight, zone, 1410, "", day))
    }
    test("catch-up is bounded to three hours for every hour including late night") {
        check(ActivityReminderPolicy.due(eightPm + 180 * 60000, zone, 1200, "", ""))
        check(!ActivityReminderPolicy.due(eightPm + 180 * 60000 + 1, zone, 1200, "", ""))
        check(!ActivityReminderPolicy.due(at("2026-10-12T22:30:00Z"), zone, 1200, "", ""))
    }
    test("recent delivery prevents bursts after a schedule or timezone change") {
        check(!ActivityReminderPolicy.due(eightPm, zone, 1200, "2026-10-11", "", eightPm - 11 * 3600000L))
        check(ActivityReminderPolicy.due(eightPm, zone, 1200, "2026-10-11", "", eightPm - 12 * 3600000L))
        check(!ActivityReminderPolicy.due(eightPm, zone, 1200, "2026-10-11", "", eightPm + 1))
    }
    test("manual and learned hours support the full local day") {
        check(ActivityReminderPolicy.clampMinute(-1) == 0)
        check(ActivityReminderPolicy.clampMinute(1440) == 1439)
        check(ActivityReminderPolicy.clampMinute(3 * 60) == 3 * 60)
        check(ActivityReminderPolicy.due(at("2026-10-11T18:00:00Z"), zone, 180, "", ""))
        check(ActivityReminderPolicy.nextEpoch(eightPm, zone, 1200) == eightPm + 86400000)
    }
    test("next reminder follows local calendar through daylight saving") {
        val ny = ZoneId.of("America/New_York")
        val spring = at("2026-03-08T01:00:00Z")
        val autumn = at("2026-11-01T00:00:00Z")
        check(ActivityReminderPolicy.nextEpoch(spring, ny, 1200) - spring == 23 * 3600000L)
        check(ActivityReminderPolicy.nextEpoch(autumn, ny, 1200) - autumn == 25 * 3600000L)
        val beforeGap = at("2026-03-08T06:00:00Z")
        check(ActivityReminderPolicy.nextEpoch(beforeGap, ny, 150) == at("2026-03-08T07:30:00Z"))
        check(ActivityReminderPolicy.dueOccurrence(at("2026-03-08T07:40:00Z"), ny, 150)!!.scheduledDay == "2026-03-08")
        check(ActivityReminderPolicy.nextEpoch(at("2026-03-08T08:00:00Z"), ny, 150) == at("2026-03-09T06:30:00Z"))
    }
    test("actual delayed taps remain valid across midnight and into the following afternoon") {
        val night = ActivityReminderPolicy.observation(eightPm, eightPm + 7 * 3600000L, zone)!!
        check(night.openedMinute == 180 && night.delayMinutes == 420)
        val student = ActivityReminderPolicy.observation(eightPm, eightPm + 1290 * 60000L, zone)!!
        check(student.openedMinute == 1050 && student.delayMinutes == 1290)
        check(ActivityReminderPolicy.observation(eightPm, eightPm + 24 * 3600000L, zone) != null)
    }
    test("invalid and stale taps cannot train timing") {
        check(ActivityReminderPolicy.observation(eightPm, eightPm - 1, zone) == null)
        check(ActivityReminderPolicy.observation(eightPm, eightPm + 24 * 3600000L + 1, zone) == null)
        check(ActivityReminderPolicy.observation(0, eightPm, zone) == null)
    }
    fun observation(opened: Int, delay: Int = 120) = ActivityReminderPolicy.Observation(
        Math.floorMod(opened - delay, 1440), opened, delay)
    test("at least three consistent informative taps are needed before adapting") {
        val estimate = ActivityReminderPolicy.estimate(1200, listOf(observation(180), observation(190)))
        check(estimate.minute == 1200 && estimate.status == "collecting")
        val learned = ActivityReminderPolicy.estimate(1200, listOf(observation(180), observation(190), observation(200)))
        check(learned.minute == 1320 && learned.status == "adapting")
    }
    test("midnight observations form one circular mode rather than averaging to noon") {
        val estimate = ActivityReminderPolicy.estimate(1380, listOf(observation(1430), observation(0), observation(10)))
        check(estimate.windowStartMinute == 1434 && estimate.windowEndMinute == 6)
        check(estimate.spreadMinutes == 12 && estimate.minute == 1429)
    }
    test("two daily breaks are not averaged into the unavailable hours between them") {
        val estimate = ActivityReminderPolicy.estimate(1200, listOf(900, 910, 920, 1320, 1330, 1340).map { observation(it) })
        check(estimate.windowStartMinute in 1320..1340)
        check(estimate.minute in 1315..1340)
    }
    test("immediate reactions do not drift later even across a long history") {
        val estimate = ActivityReminderPolicy.estimate(1200, (0 until 60).map { observation(1201 + it % 3, 1 + it % 3) })
        check(estimate.minute == 1200 && estimate.status == "responsive")
        check(estimate.sampleCount == ActivityReminderPolicy.MAX_HISTORY && estimate.informativeSamples == 0)
    }
    test("response jitter within the learned spread reaches a stable plateau") {
        val estimate = ActivityReminderPolicy.estimate(105, listOf(100, 120, 140, 160, 180).map { observation(it, 40) })
        check(estimate.spreadMinutes == 48)
        check(estimate.minute == 105 && estimate.status == "stable")
    }
    test("one extreme delayed tap cannot pull a confirmed responsive schedule") {
        val estimate = ActivityReminderPolicy.estimate(1200, listOf(
            observation(1210, 60), observation(1220, 70), observation(1230, 80),
            observation(1202, 2), observation(1203, 3), observation(500, 740)))
        check(estimate.minute == 1200 && estimate.status == "stable")
    }
    test("widely dispersed responses do not claim a reliable availability window") {
        val estimate = ActivityReminderPolicy.estimate(1200, (0 until 12).map { observation(it * 120, 60) })
        check(estimate.minute == 1200 && estimate.status == "uncertain")
        check(estimate.windowStartMinute == null)
    }
    test("invalid persisted observations are ignored and old timing history expires") {
        val bad = listOf(ActivityReminderPolicy.Observation(-1, 30, 60),
            ActivityReminderPolicy.Observation(0, 1440, 60), ActivityReminderPolicy.Observation(0, 30, -1),
            ActivityReminderPolicy.Observation(0, 30, 1441))
        check(ActivityReminderPolicy.estimate(1200, bad).sampleCount == 0)
        val old = (0 until 28).map { observation(180 + it % 3) }
        val recent = (0 until 28).map { observation(1202, 2) }
        val estimate = ActivityReminderPolicy.estimate(1200, old + recent)
        check(estimate.minute == 1200 && estimate.status == "responsive")
    }
    val window=WalkingAchievementPolicy.Window("synthetic-session",1_000_000,5000,0,87_400_000,true)
    test("walking achievement requires actual target rather than mere data presence") {
        check(!WalkingAchievementPolicy.canRecord(window,window,4999))
        check(WalkingAchievementPolicy.canRecord(window,window,5000))
        check(!WalkingAchievementPolicy.canRecord(window,window,-1))
    }
    test("cancelled session cannot accept an in-flight query") {
        check(!WalkingAchievementPolicy.canRecord(null,window,9000))
        check(!WalkingAchievementPolicy.canRecord(window.copy(sessionId="new-session"),window,9000))
    }
    test("same session cannot borrow evidence from an obsolete target or interval") {
        check(!WalkingAchievementPolicy.canRecord(window.copy(goalSteps=6000),window,9000))
        check(!WalkingAchievementPolicy.canRecord(window.copy(startEpochMs=2_000_000),window,9000))
        check(!WalkingAchievementPolicy.canRecord(window.copy(endEpochMs=80_000_000),window,9000))
        check(!WalkingAchievementPolicy.canRecord(window.copy(aggregate=false),window,9000))
        check(!WalkingAchievementPolicy.canRecord(window.copy(virtualSteps=10),window,9000))
    }
    test("debug virtual steps preserve their separate threshold without overflow") {
        val debug=window.copy(goalSteps=10,virtualSteps=10,aggregate=false)
        check(WalkingAchievementPolicy.canRecord(debug,debug,0))
        val huge=debug.copy(virtualSteps=Long.MAX_VALUE)
        check(WalkingAchievementPolicy.canRecord(huge,huge,Long.MAX_VALUE))
    }
    test("invalid persisted walking window cannot generate evidence") {
        check(!WalkingAchievementPolicy.valid(window.copy(sessionId="")))
        check(!WalkingAchievementPolicy.valid(window.copy(goalSteps=0)))
        check(!WalkingAchievementPolicy.valid(window.copy(startEpochMs=0)))
        check(!WalkingAchievementPolicy.valid(window.copy(endEpochMs=window.startEpochMs)))
        check(!WalkingAchievementPolicy.valid(window.copy(virtualSteps=-1)))
    }
    println("RESULT $passed passed, 0 failed; synthetic JVM only")
}
