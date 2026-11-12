using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    // A display-only projection of the saved course. The existing policy remains
    // the authority for progress; browsing never starts or completes a practice.
    public sealed class CourseRecordSummary
    {
        public string Program { get; private set; }
        public int Total { get; private set; }
        public int Completed { get; private set; }
        public int Weeks { get; private set; }
        public int DefaultWeek { get; private set; }
        public CourseRecordOccurrence[] Occurrences { get; private set; }

        public static CourseRecordSummary Build(GardenState state, string program)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!MbctPolicy.IsProgram(program)) throw new ArgumentException("Unknown course program.", nameof(program));

            int total = FocusMissionPolicy.TotalPracticesFor(program);
            int next = FocusMissionPolicy.NextOrder(state, program);
            var entries = state.Sessions.Where(session => FocusMissionPolicy.IsFocus(session)
                    && session.FocusRole == FocusMissionCatalog.BasicRole
                    && FocusMissionPolicy.ProgramFor(session) == program)
                .OrderByDescending(LatestTime, StringComparer.Ordinal)
                .ThenBy(session => session.SessionId, StringComparer.Ordinal)
                .Select(session => session.Copy()).ToArray();
            return new CourseRecordSummary {
                Program = program,
                Total = total,
                Completed = FocusMissionPolicy.CompletedCount(state, program),
                Weeks = MbctPolicy.ProgramWeeks(program),
                // This is curriculum position, not elapsed calendar weeks.
                DefaultWeek = (Math.Max(1, Math.Min(total, next)) - 1) / FocusMissionPolicy.PracticesPerWeek + 1,
                Occurrences = Enumerable.Range(1, total).Select(order => CourseRecordOccurrence.Build(
                    order, next, FocusMissionPolicy.MissionAt(order, program).Title,
                    entries.Where(session => session.FocusOrder == order).ToArray())).ToArray()
            };
        }

        static string LatestTime(ActivitySession session) => new[] { session.EndedUtc, session.StartedUtc, session.SelectedUtc }
            .Where(value => !string.IsNullOrEmpty(value)).OrderByDescending(value => value, StringComparer.Ordinal).FirstOrDefault() ?? "";
    }

    public sealed class CourseRecordOccurrence
    {
        public int Order { get; private set; }
        public int Week { get; private set; }
        public string Title { get; private set; }
        public ActivitySession[] Entries { get; private set; }
        public bool Completed => CompletedEntry != null;
        public ActivitySession CompletedEntry { get; private set; }
        public ActivitySession Active { get; private set; }
        public ActivitySession Latest { get; private set; }
        public string Status { get; private set; }
        public string StatusText { get; private set; }

        internal static CourseRecordOccurrence Build(int order, int next, string title, ActivitySession[] entries)
        {
            var completed = entries.FirstOrDefault(session => !session.FocusReview && session.Status == ParticipationState.RewardCommitted);
            var active = entries.FirstOrDefault(session => !session.FocusReview && FocusMissionPolicy.IsActive(session));
            var latestPrimary = entries.FirstOrDefault(session => !session.FocusReview);
            string status, text;
            if (completed != null) { status = "complete"; text = "완료"; }
            else if (active != null) { status = "active"; text = "진행 중"; }
            else if (latestPrimary != null && latestPrimary.Status == ParticipationState.Ended)
            {
                status = "interrupted";
                text = latestPrimary.FocusEndReason == "skipped" ? "넘김"
                    : latestPrimary.FocusEndReason == "alternative" ? "전환" : "중단";
            }
            else if (order == next) { status = "next"; text = "다음 실습"; }
            else { status = "upcoming"; text = "예정"; }
            return new CourseRecordOccurrence {
                Order = order,
                Week = (order - 1) / FocusMissionPolicy.PracticesPerWeek + 1,
                Title = title,
                Entries = entries,
                CompletedEntry = completed,
                Active = active,
                Latest = entries.FirstOrDefault(),
                Status = status,
                StatusText = text
            };
        }
    }
}
