using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Runtime.Personalization;

static class MissionRecommendationTests
{
    static int assertions;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < .00001, message);
    static string Snapshot(GardenState state)
    {
        using (var writer = new StringWriter()) { new XmlSerializer(typeof(GardenState)).Serialize(writer, state); return writer.ToString(); }
    }
    static void Main()
    {
        var now = DateTimeOffset.Parse("2030-01-15T12:00:00Z");
        var rules = new ResponsePolicy();
        var state = new GardenState();
        Check(OptionalActivityRecommender.Select(state, rules, now) == null, "No learned activities means no optional recommendation");
        state.MbctCompletedIds.Add("course:MBCT99");
        Check(OptionalActivityRecommender.Select(state, rules, now) == null, "Unknown completed IDs cannot become candidates");
        state.MbctCompletedIds.Clear();
        state.MbctCompletedIds.Add("course:MBCT03"); // Breath, nominal two minutes.
        state.PracticePreferences.Add(new PracticeAnswer { Key = "favorite", Value = "호흡 관찰" });
        state.PracticePreferences.Add(new PracticeAnswer { Key = "recommendation-minutes", Value = "1" });
        state.Sessions.Add(new ActivitySession { MissionId = "course:MBCT03", StartedUtc = now.AddDays(-7).ToString("O"), Status = ParticipationState.RewardCommitted });
        state.Sessions.Add(new ActivitySession { MissionId = "course:MBCT03", StartedUtc = now.AddDays(-8).ToString("O"), Status = ParticipationState.Ended });
        state.Sessions.Add(new ActivitySession { MissionId = "course:MBCT03", Status = ParticipationState.Selected });
        string before = Snapshot(state);
        var decision = OptionalActivityRecommender.Select(state, rules, now);
        Check(decision.MissionId == "course:MBCT03" && decision.PolicyId == "local-rules-v1", "Selection preserves catalog and policy identity");
        Near(decision.Features[0], 1, "Preference is taken from saved family choice");
        Near(decision.Features[1], .5, "Unstarted selection is excluded from completion ratio");
        Near(decision.Features[2], .5, "Recency is measured at the supplied decision time");
        Near(decision.Features[3], .5, "Available minute changes duration fit, not mission duration");
        Near(decision.Score, .725, "Existing rule weights are retained");
        Check(Snapshot(state) == before, "Ranking cannot modify progress, answers, rewards or sessions");
        var exposed = decision.Features; exposed[0] = 0;
        Near(decision.Features[0], 1, "UI cannot mutate frozen exposure features");
        state.PracticePreferences[0].Value = "일상 행동";
        rules.policy_id = "later-policy";
        Check(decision.PolicyId == "local-rules-v1", "An existing decision keeps its policy identity");
        Near(decision.Features[0], 1, "Later preference changes do not rewrite the decision");
        foreach (string duration in new[] { "invalid", "0", "30" })
        {
            state.PracticePreferences[1].Value = duration;
            Near(OptionalActivityRecommender.Select(state, rules, now).Features[3], 1, "Unsupported saved duration uses existing three-minute default");
        }
        state.Sessions[0].StartedUtc = now.AddDays(1).ToString("O");
        Near(OptionalActivityRecommender.Select(state, rules, now).Features[2], 0, "Future timestamps cannot produce negative recency");

        var courses = new GardenState();
        courses.MbctCompletedIds.Add("course:MBCT03");
        courses.MbctPrograms.Add(new MbctProgramProgress { ProgramId = "2w", CompletedIds = { "course:MBCT2W02" } });
        courses.MbctPrograms.Add(new MbctProgramProgress { ProgramId = "4w", CompletedIds = { "course:MBCT4W02" } });
        var constant = new ResponsePolicy { model_type = "logistic", policy_id = "synthetic-constant", training_rows = 20, expires_utc_ms = now.AddDays(1).ToUnixTimeMilliseconds() };
        Check(OptionalActivityRecommender.Select(courses, constant, now).MissionId == "course:MBCT03", "Equal scores break ties by ordinal ID across courses");
        courses.MbctCompletedIds.Clear();
        Check(OptionalActivityRecommender.Select(courses, constant, now).MissionId == "course:MBCT2W02", "Short-course learned IDs participate without changing active course");
        courses.MbctPrograms.RemoveAt(0);
        Check(OptionalActivityRecommender.Select(courses, constant, now).MissionId == "course:MBCT4W02", "Four-week learned IDs participate");
        var learned = MbctContent.Find("course:MBCT4W02");
        Check(learned != null && learned.mbctActivity == 4, "Test fixtures use actual bundled breathing activities");
        var baseline = OptionalActivityRecommender.Select(courses, new ResponsePolicy(), now);
        var expired = OptionalActivityRecommender.Select(courses, constant, now.AddDays(2));
        Near(expired.Score, baseline.Score, "Expired policy preserves local score fallback");
        constant.weights[0] = float.NaN;
        Near(OptionalActivityRecommender.Select(courses, constant, now).Score, baseline.Score, "Invalid model preserves local score fallback");
        Check(courses.MbctProgramId == "8w" && courses.Nutrient == 0 && courses.Sessions.Count == 0, "Optional ranking cannot select a course, reward or start an activity");
        Console.WriteLine("Mission recommendation handoff: " + assertions + " assertions PASS");
    }
}
