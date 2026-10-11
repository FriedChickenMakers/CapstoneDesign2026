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
    test("one reminder per calendar day even after clock reversal") {
        check(!ActivityReminderPolicy.due(eightPm, zone, 1200, day, ""))
        check(!ActivityReminderPolicy.due(eightPm - 86400000, zone, 1200, day, ""))
        check(ActivityReminderPolicy.due(eightPm + 86400000, zone, 1200, day, ""))
    }
    test("completion suppresses that day and past days after timezone changes") {
        check(!ActivityReminderPolicy.due(eightPm, zone, 1200, "", day))
        check(!ActivityReminderPolicy.due(eightPm - 86400000, zone, 1200, "", day))
        check(ActivityReminderPolicy.due(eightPm + 86400000, zone, 1200, "", day))
    }
    test("missed reminders do not arrive during quiet hours") {
        check(ActivityReminderPolicy.due(eightPm + 119*60000, zone, 1200, "", ""))
        check(!ActivityReminderPolicy.due(eightPm + 120*60000, zone, 1200, "", ""))
        check(!ActivityReminderPolicy.due(at("2026-10-12T22:30:00Z"), zone, 1200, "", ""))
    }
    test("morning reminder has a bounded catch-up window") {
        check(ActivityReminderPolicy.due(at("2026-10-12T01:59:00Z"), zone, 480, "", ""))
        check(!ActivityReminderPolicy.due(at("2026-10-12T02:01:00Z"), zone, 480, "", ""))
    }
    test("user time is clamped into waking hours") {
        check(ActivityReminderPolicy.clampMinute(-1) == 480)
        check(ActivityReminderPolicy.clampMinute(1440) == 1260)
        check(ActivityReminderPolicy.nextEpoch(eightPm, zone, 1200) == eightPm+86400000)
    }
    test("next reminder follows local calendar through daylight saving") {
        val ny = ZoneId.of("America/New_York")
        val spring = at("2026-03-08T01:00:00Z") // Previous day 20:00 EST.
        val autumn = at("2026-11-01T00:00:00Z") // Previous day 20:00 EDT.
        check(ActivityReminderPolicy.nextEpoch(spring, ny, 1200)-spring == 23*3600000L)
        check(ActivityReminderPolicy.nextEpoch(autumn, ny, 1200)-autumn == 25*3600000L)
    }
    test("learning uses recent same-day taps with at most fifteen-minute shift") {
        check(ActivityReminderPolicy.learnedMinute(1200,eightPm,eightPm+60*60000,zone)==1215)
        check(ActivityReminderPolicy.learnedMinute(1200,eightPm,eightPm+8*60000,zone)==1202)
    }
    test("clock rollback stale taps and next-day taps cannot train timing") {
        check(ActivityReminderPolicy.learnedMinute(1200,eightPm,eightPm-1,zone)==null)
        check(ActivityReminderPolicy.learnedMinute(1200,eightPm,eightPm+7*3600000,zone)==null)
        check(ActivityReminderPolicy.learnedMinute(1200,eightPm,eightPm+5*3600000,zone)==null)
        check(ActivityReminderPolicy.learnedMinute(1200,0,eightPm,zone)==null)
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
