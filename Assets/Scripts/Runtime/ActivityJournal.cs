using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapstoneDesign.Runtime.LocalState
{
    // A read-only index of existing records. No saved IDs, dates or rewards change.
    public sealed class ActivityJournalDay
    {
        public string DateKey { get; internal set; }
        public string[] SessionIds { get; internal set; } = new string[0];
        public string[] EmotionIds { get; internal set; } = new string[0];
        public int Count => SessionIds.Length + EmotionIds.Length;
    }

    public sealed class ActivityJournalIndex
    {
        readonly Dictionary<string, ActivityJournalDay> days;
        public ActivityJournalDay Undated { get; private set; }
        public ActivityJournalDay[] Days => days.Values.OrderByDescending(day => day.DateKey, StringComparer.Ordinal).ToArray();
        internal ActivityJournalIndex(Dictionary<string, ActivityJournalDay> days, ActivityJournalDay undated)
        { this.days = days; Undated = undated; }
        public ActivityJournalDay ForDate(string date)
        { ActivityJournalDay day; return date != null && days.TryGetValue(date, out day) ? day : new ActivityJournalDay { DateKey = date }; }
    }

    public static class ActivityJournal
    {
        sealed class Group
        {
            public readonly List<ActivitySession> Sessions = new List<ActivitySession>();
            public readonly List<DailyEmotionRecord> Emotions = new List<DailyEmotionRecord>();
            public ActivityJournalDay Project(string date) => new ActivityJournalDay {
                DateKey = date,
                SessionIds = Sessions.OrderByDescending(session => Latest(session.EndedUtc, session.StartedUtc, session.SelectedUtc), StringComparer.Ordinal)
                    .ThenBy(session => session.SessionId, StringComparer.Ordinal).Select(session => session.SessionId).ToArray(),
                EmotionIds = Emotions.OrderByDescending(record => Latest(record.CompletedUtc, record.UpdatedUtc, record.StartedUtc, record.UnlockedUtc), StringComparer.Ordinal)
                    .ThenBy(record => record.Id, StringComparer.Ordinal).Select(record => record.Id).ToArray()
            };
        }

        public static bool TryDay(string key, out DateTime day) => DateTime.TryParseExact(key, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        public static string Key(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        static string Latest(params string[] values) => values.Where(value => !string.IsNullOrEmpty(value))
            .OrderByDescending(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "";
        static string FallbackDay(TimeZoneInfo zone, int resetHour, params string[] times)
        {
            foreach (var time in times)
                if (DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant))
                {
                    var local = TimeZoneInfo.ConvertTime(instant, zone ?? TimeZoneInfo.Utc).DateTime;
                    // DateTime.MinValue must remain an unknown historical date.
                    if (local.Ticks < TimeSpan.FromHours(resetHour).Ticks) return null;
                    return Key(local.AddHours(-resetHour));
                }
            return null;
        }
        public static string DayFor(ActivitySession session, TimeZoneInfo zone, int resetHour = 4)
        {
            if (session == null) return null;
            return TryDay(session.InstanceId, out _) ? session.InstanceId
                : FallbackDay(zone, resetHour, session.EndedUtc, session.StartedUtc, session.SelectedUtc);
        }
        public static string DayFor(DailyEmotionRecord record, TimeZoneInfo zone, int resetHour = 4)
        {
            if (record == null) return null;
            return TryDay(record.CycleId, out _) ? record.CycleId
                : FallbackDay(zone, resetHour, record.CompletedUtc, record.UpdatedUtc, record.StartedUtc, record.UnlockedUtc);
        }
        public static ActivityJournalIndex Build(GardenState state, TimeZoneInfo zone, int resetHour = 4)
        {
            if (resetHour < 0 || resetHour > 23) throw new ArgumentOutOfRangeException(nameof(resetHour));
            var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
            var undated = new Group();
            Func<string, Group> groupFor = date => {
                if (date == null) return undated;
                Group group;
                if (!groups.TryGetValue(date, out group)) groups.Add(date, group = new Group());
                return group;
            };
            var seenSessions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var session in state?.Sessions ?? new List<ActivitySession>())
                if (session != null && !string.IsNullOrEmpty(session.SessionId) && seenSessions.Add(session.SessionId))
                    groupFor(DayFor(session, zone, resetHour)).Sessions.Add(session);
            var seenEmotions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in state?.DailyEmotionRecords ?? new List<DailyEmotionRecord>())
                if (record != null && !string.IsNullOrEmpty(record.Id) && seenEmotions.Add(record.Id))
                    groupFor(DayFor(record, zone, resetHour)).Emotions.Add(record);
            return new ActivityJournalIndex(groups.ToDictionary(pair => pair.Key, pair => pair.Value.Project(pair.Key), StringComparer.Ordinal), undated.Project(null));
        }
    }
}
