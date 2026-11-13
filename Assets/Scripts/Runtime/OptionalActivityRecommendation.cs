using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime.Personalization
{
    // Presentation consumes this decision; it does not recompute its features on click.
    // This is an in-memory contract, not a new save or network schema.
    public sealed class OptionalActivityRecommendation
    {
        public string MissionId { get; private set; }
        public string PolicyId { get; private set; }
        public double Score { get; private set; }
        readonly float[] features;
        public float[] Features => (float[])features.Clone();

        internal OptionalActivityRecommendation(string missionId, string policyId, float[] values, double score)
        {
            MissionId = missionId;
            PolicyId = policyId;
            features = (float[])values.Clone();
            Score = score;
        }
    }

    // Existing optional review policy, separated from Unity UI for parallel work.
    // Does not change required course order, rewards, consent or persisted state.
    public static class OptionalActivityRecommender
    {
        public static OptionalActivityRecommendation Select(GardenState state, ResponsePolicy policy, DateTimeOffset now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var learned = MbctPolicy.AllCompletedIds(state).ToArray();
            var candidates = MbctContent.AllCourses.Where(m => learned.Contains(m.id)).ToArray();
            if (candidates.Length == 0) return null;
            var preferred = state.PracticePreferences.FirstOrDefault(p => p.Key == "favorite")?.Value ?? "";
            int availableMinutes;
            if (!int.TryParse(state.PracticePreferences.FirstOrDefault(p => p.Key == "recommendation-minutes")?.Value, out availableMinutes)
                || !new[] { 1, 3, 5 }.Contains(availableMinutes)) availableMinutes = 3;
            return candidates.Select(m =>
            {
                var sessions = state.Sessions.Where(s => s.MissionId == m.id && s.StartedUtc != null).ToArray();
                var last = sessions.Select(s => DateTimeOffset.TryParse(s.StartedUtc, out var time) ? time : DateTimeOffset.MinValue)
                    .DefaultIfEmpty(DateTimeOffset.MinValue).Max();
                float[] features = {
                    RecommendationFeatures.Preference(m.mbctActivity, m.title, preferred),
                    sessions.Length == 0 ? .5f : sessions.Count(s => s.Status == ParticipationState.RewardCommitted) / (float)sessions.Length,
                    (float)Math.Max(0, Math.Min(1, (now - last).TotalDays / 14)),
                    RecommendationFeatures.DurationFit(m.suggestedDurationSeconds, availableMinutes)
                };
                return new OptionalActivityRecommendation(m.id, policy.policy_id, features, policy.Score(features, now.ToUnixTimeMilliseconds()));
            }).OrderByDescending(c => c.Score).ThenBy(c => c.MissionId, StringComparer.Ordinal).First();
        }
    }
}
