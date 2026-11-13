package com.capstonedesign2026.platform

/** Immutable identity of the health evidence needed for one walking achievement. */
internal object WalkingAchievementPolicy {
    data class Window(val sessionId: String, val startEpochMs: Long, val goalSteps: Int,
                      val virtualSteps: Long, val endEpochMs: Long, val aggregate: Boolean)

    fun valid(window: Window): Boolean = window.sessionId.isNotBlank() && window.startEpochMs > 0 &&
        window.goalSteps in 1..100_000 && window.virtualSteps >= 0 && window.endEpochMs > window.startEpochMs

    fun canRecord(current: Window?, expected: Window, steps: Long): Boolean =
        current != null && valid(current) && current == expected && steps >= 0 &&
            steps >= current.goalSteps.toLong() - current.virtualSteps
}
