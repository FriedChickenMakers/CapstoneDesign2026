using System;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Presentation only. Never use this clock for health, activities or reminders.
    public static class GardenVisualClock
    {
        public static double? OverrideHour { get; private set; }
        public static double CurrentHour => OverrideHour ?? DateTime.Now.TimeOfDay.TotalHours;
        public static int CurrentMinute => OverrideHour.HasValue ? (int)Math.Round(OverrideHour.Value * 60)
            : (int)DateTime.Now.TimeOfDay.TotalMinutes;
        public static event Action Changed;

        public static void SetOverrideMinutes(int minute)
        {
            double hour = ((minute % 1440 + 1440) % 1440) / 60d;
            if (OverrideHour == hour) return;
            OverrideHour = hour;
            Changed?.Invoke();
        }
        public static void UseSystemTime()
        {
            if (!OverrideHour.HasValue) return;
            OverrideHour = null;
            Changed?.Invoke();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { OverrideHour = null; Changed = null; }
    }
}
