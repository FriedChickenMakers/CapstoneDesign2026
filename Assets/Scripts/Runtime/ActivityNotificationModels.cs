using System;

namespace CapstoneDesign.Runtime
{
    [Serializable] public sealed class ActivityNotificationSettings
    {
        public bool enabled=true, adaptive=true;
        public int minuteOfDay=1200, preferredMinute=1200, learnedClicks;
        public int windowStartMinute=-1, windowEndMinute=-1, spreadMinutes, informativeSamples;
        public string learningStatus="collecting";
        public string permissionStatus="UNSUPPORTED";
        public long nextReminderEpochMs, lastClickDelayMs, lastPostedAtEpochMs;
    }
    [Serializable] public sealed class ActivityNotificationOpen { public string kind,sessionId; }
    [Serializable] public sealed class WalkingAchievement
    {
        public string sessionId,source;
        public long startEpochMs,endEpochMs,steps,achievedAtEpochMs;
        public int goalSteps;
        public bool queryComplete;
    }
}
