using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

public static class ActivityJournalTests
{
    static int assertions;
    static void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static readonly TimeZoneInfo Seoul = TimeZoneInfo.CreateCustomTimeZone("Synthetic+09", TimeSpan.FromHours(9), "Synthetic+09", "Synthetic+09");
    static ActivitySession Session(string id, string date, string utc, ParticipationState status = ParticipationState.RewardCommitted)
        => new ActivitySession { SessionId = id, MissionId = "course:P01", InstanceId = date, SelectedUtc = utc, EndedUtc = utc, Status = status };

    public static void Main()
    {
        var empty = ActivityJournal.Build(new GardenState(), Seoul);
        Check(empty.Days.Length == 0 && empty.Undated.Count == 0 && empty.ForDate("2026-11-09").Count == 0, "Empty calendar has no invented records.");
        var state = new GardenState();
        state.Sessions.Add(Session("basic", "2026-11-09", "2026-11-09T10:00:00Z"));
        state.Sessions.Add(Session("additional", "2026-11-09", "2026-11-09T11:00:00Z", ParticipationState.Paused));
        state.Sessions[1].MissionId = "free-mission:M03";
        state.Sessions.Add(Session("legacy", "2026-11-09", "2026-11-09T09:00:00Z", ParticipationState.Ended));
        state.DailyEmotionRecords.Add(new DailyEmotionRecord { Id = "emotion", CycleId = "2026-11-09", BasicSessionId = "basic", AdditionalSessionId = "additional", Mood = "편안함", Note = "합성 기록" });
        var index = ActivityJournal.Build(state, Seoul);
        var day = index.ForDate("2026-11-09");
        Check(day.Count == 4 && day.SessionIds.Length == 3 && day.EmotionIds.Length == 1, "Linked sessions appear once; emotion does not duplicate their rows.");
        Check(day.SessionIds.SequenceEqual(new[] { "additional", "basic", "legacy" }), "Newest mission time sorts first, including paused and legacy entries.");
        Check(day.EmotionIds.Single() == "emotion", "Unfinished emotion remains visible.");
        Check(state.DailyEmotionRecords[0].Mood == "편안함" && state.Sessions[1].Status == ParticipationState.Paused, "Projection does not finalize or alter records.");
        day.SessionIds[0] = "changed-view";
        Check(state.Sessions[1].SessionId == "additional", "View data cannot mutate the source.");
        Check(ActivityJournal.DayFor(Session("before", null, "2026-11-08T18:59:59Z"), Seoul) == "2026-11-08", "03:59:59 belongs to prior business day.");
        Check(ActivityJournal.DayFor(Session("at", null, "2026-11-08T19:00:00Z"), Seoul) == "2026-11-09", "04:00 starts a new business day.");
        Check(ActivityJournal.DayFor(Session("saved", "2026-11-07", "2026-11-09T23:00:00Z"), Seoul) == "2026-11-07", "Saved business date wins over later reflection or changed time zone.");
        Check(ActivityJournal.DayFor(Session("month", null, "2026-03-01T00:30:00Z"), TimeZoneInfo.Utc) == "2026-02-28", "Month boundary follows reset hour.");
        Check(ActivityJournal.DayFor(Session("leap", null, "2024-03-01T00:30:00Z"), TimeZoneInfo.Utc) == "2024-02-29", "Leap day is retained.");
        Check(ActivityJournal.DayFor(Session("year", null, "2026-01-01T01:00:00Z"), TimeZoneInfo.Utc) == "2025-12-31", "Year boundary follows reset hour.");
        Check(ActivityJournal.TryDay("2024-02-29", out _) && !ActivityJournal.TryDay("2026-02-29", out _), "Calendar keys use valid invariant dates.");
        state.Sessions.Add(Session("unknown", "legacy", null));
        state.DailyEmotionRecords.Add(new DailyEmotionRecord { Id = "unknown-emotion", CycleId = "unknown" });
        index = ActivityJournal.Build(state, Seoul);
        Check(index.Undated.Count == 2 && index.Days.Sum(item => item.Count) == 4, "Unknown dates remain reachable without appearing on today.");
        state.Sessions.Add(state.Sessions[0].Copy());
        state.DailyEmotionRecords.Add(state.DailyEmotionRecords[0].Copy());
        Check(ActivityJournal.Build(state, Seoul).ForDate("2026-11-09").Count == 4, "Repeated source identities never produce duplicate rows.");
        state.DailyEmotionRecords.RemoveAll(record => record.Id == "emotion");
        Check(ActivityJournal.Build(state, Seoul).ForDate("2026-11-09").EmotionIds.Length == 0, "Deleted emotion is not reconstructed from linked missions or plant snapshots.");
        var fresh = new GardenState();
        for (int i = 0; i < 12000; i++)
        {
            string key = ActivityJournal.Key(new DateTime(2020, 1, 1).AddDays(i / 3));
            fresh.Sessions.Add(Session("large-" + i, key, new DateTimeOffset(2020, 1, 1, 10, 0, 0, TimeSpan.Zero).AddMinutes(i).ToString("O")));
        }
        var large = ActivityJournal.Build(fresh, Seoul);
        Check(large.Days.Length == 4000 && large.Days.Sum(item => item.Count) == 12000, "Large history has no page cap or data loss.");
        Check(large.Days.All(item => item.SessionIds.Length == 3), "Large index groups each day independently.");
        Check(ActivityJournal.Build(null, Seoul).Days.Length == 0, "Null source is an empty read-only view.");
        bool rejected = false; try { ActivityJournal.Build(state, Seoul, 24); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Invalid reset hour is rejected.");
        Console.WriteLine("Activity journal pure tests: " + assertions + " PASS");
    }
}
