using System;
using System.Collections.Generic;
using System.Text;

namespace CapstoneDesign.Prototype
{
    public enum PrototypeOutcome { Success, StartMissionFirst, AlreadyCompleted, StaleRequest, ChoiceRequired, Finished, InvalidChoice, CompleteMissionFirst }

    // Memory only. No preferences, files, analytics or cloud writes.
    public sealed class DailyCompletion
    {
        public DateTime Day { get; }
        public DateTime CompletedAt { get; }
        public Mood Mood { get; }
        public PlantChoice Choice { get; }
        public int QuestionIndex { get; }
        public string Note { get; }
        public DailyCompletion(DateTime day, DateTime at, Mood mood, PlantChoice choice, int question, string note = "")
        { Day = day; CompletedAt = at; Mood = mood; Choice = choice; QuestionIndex = question; Note = note ?? ""; }
    }

    // Owns daily eligibility, votes and growth; UI/animation cannot award growth.
    public sealed class PrototypeSession
    {
        public const int NoteCharacterLimit = 200;
        private readonly PrototypeDefinition definition;
        private readonly Func<DateTime> now;
        private readonly List<DailyCompletion> completed = new List<DailyCompletion>(4);
        public IReadOnlyList<DailyCompletion> Completions { get; }
        public DateTime Day { get; private set; }
        public int Round { get; private set; } = 1;
        public int Stage => completed.Count;
        public bool IsComplete => Stage == definition.maximumStage;
        public bool MissionStarted { get; private set; }
        public bool MissionCompleted { get; private set; }
        // Page navigation does not end a mission; completion or daily reset does.
        public bool IsMissionInProgress => MissionStarted && !MissionCompleted;
        public bool RecordCompleted { get; private set; }
        public bool AwaitingMoodRecord => MissionCompleted && !RecordCompleted;
        public Mood TodayMood { get; private set; }
        public string TodayNote { get; private set; } = "";
        public PlantChoice TodayChoice { get; private set; }
        public int QuestionIndex => RecordCompleted ? completed[completed.Count - 1].QuestionIndex : Math.Min(Stage, 3);
        public int QuietCount { get; private set; }
        public int TogetherCount => Stage - QuietCount;
        public FlowerDefinition FinalFlower => IsComplete ? definition.FindFlower(Winner) : null;
        public string FinalPlantId => FinalFlower?.id;
        public PlantChoice Winner => Stage == 0 ? PlantChoice.None : QuietCount == TogetherCount
            ? completed[completed.Count - 1].Choice : QuietCount > TogetherCount ? PlantChoice.Quiet : PlantChoice.Together;
        public event Action Changed;

        public PrototypeSession(PrototypeDefinition definition, Func<DateTime> clock = null)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            definition.ValidateDefinition();
            now = clock ?? (() => DateTime.Now);
            Day = now().Date;
            Completions = completed.AsReadOnly();
        }

        // Local midnight; missed days never award growth or apply a penalty.
        // Clock rollback cannot reopen a previously visited date in this session.
        public bool RefreshDay()
        {
            DateTime date = now().Date;
            if (date <= Day) return false;
            // Do not discard an action already done while its reflection is pending.
            if (AwaitingMoodRecord) return false;
            Day = date;
            Round++;
            MissionStarted = MissionCompleted = RecordCompleted = false;
            TodayMood = Mood.None;
            TodayNote = "";
            TodayChoice = PlantChoice.None;
            Changed?.Invoke();
            return true;
        }

        private PrototypeOutcome CanProceed(int round)
        {
            RefreshDay();
            if (round != Round) return PrototypeOutcome.StaleRequest;
            if (RecordCompleted) return PrototypeOutcome.AlreadyCompleted;
            return IsComplete ? PrototypeOutcome.Finished : PrototypeOutcome.Success;
        }

        private PrototypeOutcome CanEdit(int round)
        {
            var result = CanProceed(round);
            return result != PrototypeOutcome.Success ? result
                : MissionCompleted ? PrototypeOutcome.Success : PrototypeOutcome.CompleteMissionFirst;
        }

        public PrototypeOutcome SetMood(int round, Mood mood)
        {
            var result = CanEdit(round);
            if (result != PrototypeOutcome.Success) return result;
            if (!Enum.IsDefined(typeof(Mood), mood)) return PrototypeOutcome.InvalidChoice;
            if (TodayMood != mood) { TodayMood = mood; Changed?.Invoke(); }
            return PrototypeOutcome.Success;
        }

        public PrototypeOutcome SetChoice(int round, PlantChoice choice)
        {
            var result = CanEdit(round);
            if (result != PrototypeOutcome.Success) return result;
            if (choice != PlantChoice.Quiet && choice != PlantChoice.Together) return PrototypeOutcome.InvalidChoice;
            if (TodayChoice != choice) { TodayChoice = choice; Changed?.Invoke(); }
            return PrototypeOutcome.Success;
        }

        public PrototypeOutcome SetNote(int round, string note)
        {
            var result = CanEdit(round);
            if (result != PrototypeOutcome.Success) return result;
            string value = NormalizeNote(note);
            if (TodayNote != value) { TodayNote = value; Changed?.Invoke(); }
            return PrototypeOutcome.Success;
        }

        // Plain text only. Preserve Korean and newlines; cap even non-UI callers.
        private static string NormalizeNote(string note)
        {
            if (string.IsNullOrEmpty(note)) return "";
            var result = new StringBuilder(NoteCharacterLimit);
            for (int i = 0; i < note.Length && result.Length < NoteCharacterLimit; i++)
            {
                char c = note[i];
                if (c == '\r') { if (i + 1 < note.Length && note[i + 1] == '\n') i++; c = '\n'; }
                if (c == '\t') c = ' ';
                if (char.IsControl(c) && c != '\n') continue;
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < note.Length && char.IsLowSurrogate(note[i + 1]) && result.Length + 2 <= NoteCharacterLimit)
                        result.Append(c).Append(note[++i]);
                }
                else if (!char.IsLowSurrogate(c)) result.Append(c);
            }
            return result.ToString();
        }

        public PrototypeOutcome StartMission(int round)
        {
            var result = CanProceed(round);
            if (result != PrototypeOutcome.Success) return result;
            if (MissionCompleted) return PrototypeOutcome.AlreadyCompleted;
            if (!MissionStarted) { MissionStarted = true; Changed?.Invoke(); }
            return PrototypeOutcome.Success;
        }

        public PrototypeOutcome CompleteMission(int round)
        {
            var result = CanProceed(round);
            if (result != PrototypeOutcome.Success) return result;
            if (MissionCompleted) return PrototypeOutcome.AlreadyCompleted;
            if (!MissionStarted) return PrototypeOutcome.StartMissionFirst;
            MissionCompleted = true;
            Changed?.Invoke(); // Reflection is still pending: no record, vote or growth yet.
            return PrototypeOutcome.Success;
        }

        public PrototypeOutcome RecordMood(int round)
        {
            var result = CanEdit(round);
            if (result != PrototypeOutcome.Success) return result;
            if (TodayChoice == PlantChoice.None) return PrototypeOutcome.ChoiceRequired;
            var record = new DailyCompletion(Day, now(), TodayMood, TodayChoice, QuestionIndex, TodayNote.Trim());
            completed.Add(record);
            if (TodayChoice == PlantChoice.Quiet) QuietCount++;
            RecordCompleted = true;
            Changed?.Invoke(); // Record, vote and stage are observable in one transaction.
            return PrototypeOutcome.Success;
        }
    }
}
