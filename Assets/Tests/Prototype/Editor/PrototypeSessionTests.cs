using System;
using CapstoneDesign.Prototype;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.Tests
{
    public sealed class PrototypeSessionTests
    {
        private PrototypeDefinition definition;
        private PrototypeSession session;
        private DateTime now;
        [SetUp] public void SetUp()
        {
            definition=AssetDatabase.LoadAssetAtPath<PrototypeDefinition>("Assets/Settings/Prototype/PrototypeDefinition.asset");
            Assert.That(definition,Is.Not.Null);
            now=new DateTime(2026,9,26,12,0,0);
            session=new PrototypeSession(definition,()=>now);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void AllEightVotePathsFinishAtFourthVisibleStageAfterThreeCompletions(int bits)
        {
            int quiet=0;
            PlantChoice last=PlantChoice.None;
            for(int day=0;day<3;day++)
            {
                if(day>0){now=now.AddDays(1);Assert.That(session.RefreshDay(),Is.True);}
                last=(bits&(1<<day))==0?PlantChoice.Quiet:PlantChoice.Together;
                if(last==PlantChoice.Quiet)quiet++;
                Assert.That(session.QuestionIndex,Is.EqualTo(day));
                Complete(last,Mood.Down);
                Assert.That(session.Stage,Is.EqualTo(day+1));
                Assert.That(session.Completions.Count,Is.EqualTo(day+1));
                Assert.That(session.CompleteMission(session.Round),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
                Assert.That(session.FinalFlower==null,Is.EqualTo(day<2));
            }
            var winner=quiet>=2?PlantChoice.Quiet:PlantChoice.Together;
            Assert.That(session.FinalPlantId,Is.EqualTo(winner==PlantChoice.Quiet?"chamomile":"hydrangea"));
            Assert.That(session.QuietCount,Is.EqualTo(quiet));
            now=now.AddDays(1);session.RefreshDay();
            Assert.That(session.StartMission(session.Round),Is.EqualTo(PrototypeOutcome.Finished));
            Assert.That(session.SetChoice(session.Round,PlantChoice.Quiet),Is.EqualTo(PrototypeOutcome.Finished));
            Assert.That(session.CompleteMission(session.Round),Is.EqualTo(PrototypeOutcome.Finished));
            Assert.That(session.Stage,Is.EqualTo(3));
        }
        [Test] public void MissionInProgressEndsAtCompletionNotAtRecording()
        {
            Assert.That(session.IsMissionInProgress, Is.False);
            session.StartMission(session.Round);
            Assert.That(session.IsMissionInProgress, Is.True);
            session.StartMission(session.Round);
            Assert.That(session.IsMissionInProgress, Is.True);
            session.CompleteMission(session.Round);
            Assert.That(session.IsMissionInProgress, Is.False);
            Assert.That(session.AwaitingMoodRecord, Is.True);
            session.SetChoice(session.Round, PlantChoice.Quiet);
            session.RecordMood(session.Round);
            Assert.That(session.IsMissionInProgress, Is.False);
        }
        [Test] public void MissionInProgressIsObservableInChangeNotifications()
        {
            var states = new System.Collections.Generic.List<bool>();
            session.Changed += () => states.Add(session.IsMissionInProgress);
            session.StartMission(session.Round);
            session.StartMission(session.Round); // Idempotent start emits no duplicate.
            session.CompleteMission(session.Round);
            Assert.That(states, Is.EqualTo(new[] { true, false }));
        }
        [Test] public void MidnightClearsActiveMissionAndRejectsItsStaleCallback()
        {
            int previousRound = session.Round;
            session.StartMission(previousRound);
            now = now.AddDays(1);
            Assert.That(session.RefreshDay(), Is.True);
            Assert.That(session.IsMissionInProgress, Is.False);
            Assert.That(session.StartMission(previousRound), Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.IsMissionInProgress, Is.False);
            session.StartMission(session.Round);
            Assert.That(session.IsMissionInProgress, Is.True);
            Assert.That(session.CompleteMission(previousRound), Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.IsMissionInProgress, Is.True);
        }
        [Test] public void NewSessionDoesNotRestoreAnActiveMission()
        {
            session.StartMission(session.Round);
            Assert.That(new PrototypeSession(definition, () => now).IsMissionInProgress, Is.False);
        }
        [Test] public void FailedMissionOperationsDoNotActivateMissionState()
        {
            Assert.That(session.CompleteMission(session.Round), Is.EqualTo(PrototypeOutcome.StartMissionFirst));
            Assert.That(session.IsMissionInProgress, Is.False);
            Complete();
            Assert.That(session.StartMission(session.Round), Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(session.IsMissionInProgress, Is.False);
        }
        [Test] public void StartsWithSeedAndNoVotes()
        {
            Assert.That(session.Stage,Is.Zero);Assert.That(session.QuietCount,Is.Zero);
            Assert.That(session.TodayMood,Is.EqualTo(Mood.None));Assert.That(session.TodayChoice,Is.EqualTo(PlantChoice.None));
            Assert.That(session.IsComplete,Is.False);Assert.That(session.Winner,Is.EqualTo(PlantChoice.None));
        }
        [TestCase(Mood.None)] [TestCase(Mood.Happy)] [TestCase(Mood.Calm)]
        [TestCase(Mood.Neutral)] [TestCase(Mood.Tired)] [TestCase(Mood.Down)]
        public void EveryMoodIncludingSkippedHasIdenticalGrowth(Mood mood)
        {
            Complete(PlantChoice.Quiet,mood);
            Assert.That(session.Stage,Is.EqualTo(1));Assert.That(session.QuietCount,Is.EqualTo(1));
            Assert.That(session.Completions[0].Mood,Is.EqualTo(mood));
        }
        [Test] public void RequiresBranchButMoodCanBeSkipped()
        {
            Assert.That(session.CompleteMission(1),Is.EqualTo(PrototypeOutcome.StartMissionFirst));
            Assert.That(session.StartMission(1),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.CompleteMission(1),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.RecordMood(1),Is.EqualTo(PrototypeOutcome.ChoiceRequired));
            session.SetChoice(1,PlantChoice.Together);
            Assert.That(session.RecordMood(1),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.Completions[0].Mood,Is.EqualTo(Mood.None));
        }
        [Test] public void DraftChangesDoNotCountAndCompletionLocksAnswers()
        {
            PrepareReflection();
            session.SetChoice(1,PlantChoice.Quiet);session.SetMood(1,Mood.Happy);
            session.SetChoice(1,PlantChoice.Together);session.SetMood(1,Mood.Down);
            Assert.That(session.Stage,Is.Zero);Assert.That(session.Winner,Is.EqualTo(PlantChoice.None));
            session.RecordMood(1);
            Assert.That(session.SetMood(1,Mood.Happy),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(session.SetChoice(1,PlantChoice.Quiet),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(session.Completions[0].Choice,Is.EqualTo(PlantChoice.Together));
            Assert.That(session.Completions[0].Mood,Is.EqualTo(Mood.Down));
        }
        [Test] public void LocalMidnightResetsDailyStateOnly()
        {
            now=new DateTime(2026,9,26,23,59,59);Complete();
            var previous=session.Completions[0];now=now.AddSeconds(1);
            Assert.That(session.RefreshDay(),Is.True);Assert.That(session.Day,Is.EqualTo(now.Date));
            Assert.That(session.Stage,Is.EqualTo(1));Assert.That(session.Round,Is.EqualTo(2));
            Assert.That(session.TodayMood,Is.EqualTo(Mood.None));Assert.That(session.TodayChoice,Is.EqualTo(PlantChoice.None));
            Assert.That(session.MissionStarted||session.MissionCompleted,Is.False);
            Assert.That(session.Completions[0],Is.SameAs(previous));
        }
        [Test] public void OldCompletionAcrossMidnightCannotCompleteNewDay()
        {
            session.StartMission(1);
            now=now.AddDays(1);
            Assert.That(session.CompleteMission(1),Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.Stage,Is.Zero);Assert.That(session.MissionStarted,Is.False);
            Assert.That(session.TodayChoice,Is.EqualTo(PlantChoice.None));
        }
        [Test] public void OldMoodAndChoiceRequestsAreRejected()
        {
            now=now.AddDays(1);
            Assert.That(session.SetMood(1,Mood.Happy),Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.SetChoice(1,PlantChoice.Quiet),Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.TodayMood,Is.EqualTo(Mood.None));
        }
        [Test] public void SkippedDaysHaveNoPenaltyAndNoAutomaticGrowth()
        {
            Complete();now=now.AddDays(100);
            session.RefreshDay();
            Assert.That(session.Stage,Is.EqualTo(1));Assert.That(session.Completions.Count,Is.EqualTo(1));
            Assert.That(session.QuietCount,Is.EqualTo(1));Assert.That(session.QuestionIndex,Is.EqualTo(1));
        }
        [Test] public void ClockRollbackCannotReopenOldDate()
        {
            Complete();var day=session.Day;now=now.AddDays(-1);
            Assert.That(session.RefreshDay(),Is.False);
            Assert.That(session.CompleteMission(1),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(session.Day,Is.EqualTo(day));Assert.That(session.Stage,Is.EqualTo(1));
        }
        [Test] public void DuplicateInputHasOnlyOneAtomicNotification()
        {
            PrepareReflection();session.SetChoice(1,PlantChoice.Quiet);
            int count=0;
            session.Changed+=()=>{
                count++;Assert.That(session.Stage,Is.EqualTo(1));Assert.That(session.RecordCompleted,Is.True);
                Assert.That(session.Completions.Count,Is.EqualTo(1));Assert.That(session.QuietCount,Is.EqualTo(1));
            };
            session.RecordMood(1);session.RecordMood(1);session.CompleteMission(1);
            Assert.That(count,Is.EqualTo(1));
        }
        [Test] public void RepeatedStartIsIdempotent()
        {
            int count=0;session.Changed+=()=>count++;
            session.StartMission(1);session.StartMission(1);
            Assert.That(count,Is.EqualTo(1));Assert.That(session.Stage,Is.Zero);
        }
        [Test] public void FreshSessionDoesNotRestoreProgress()
        {
            Complete();var fresh=new PrototypeSession(definition,()=>now);
            Assert.That(fresh.Stage,Is.Zero);Assert.That(fresh.Completions,Is.Empty);
            Assert.That(fresh.MissionCompleted,Is.False);Assert.That(fresh.TodayChoice,Is.EqualTo(PlantChoice.None));
        }
        [Test] public void InvalidEnumsDoNotChangeState()
        {
            PrepareReflection();
            Assert.That(session.SetMood(1,(Mood)100),Is.EqualTo(PrototypeOutcome.InvalidChoice));
            Assert.That(session.SetChoice(1,PlantChoice.None),Is.EqualTo(PrototypeOutcome.InvalidChoice));
            Assert.That(session.SetChoice(1,(PlantChoice)100),Is.EqualTo(PrototypeOutcome.InvalidChoice));
            Assert.That(session.TodayChoice,Is.EqualTo(PlantChoice.None));
        }
        [Test] public void HistoryRetainsActualDatesAndLastConfirmedMoodAcrossGaps()
        {
            var first=now;
            PrepareReflection();
            session.SetMood(1,Mood.Happy);
            Complete(PlantChoice.Quiet,Mood.Tired);
            now=now.AddDays(7);session.RefreshDay();
            Complete(PlantChoice.Together,Mood.None);
            var records=session.Completions;
            Assert.That(records.Count,Is.EqualTo(2));
            Assert.That(records[0].Day,Is.EqualTo(first.Date));
            Assert.That(records[0].CompletedAt,Is.EqualTo(first));
            Assert.That(records[0].Mood,Is.EqualTo(Mood.Tired));
            Assert.That(records[1].Day,Is.EqualTo(now.Date));
            Assert.That(records[1].Mood,Is.EqualTo(Mood.None));
            now=now.AddDays(1);session.RefreshDay();PrepareReflection();session.SetMood(session.Round,Mood.Calm);
            Assert.That(records.Count,Is.EqualTo(2),"Uncompleted drafts are not history");
            Assert.That(session.Stage,Is.EqualTo(2),"Reading history never awards growth");
        }
        [Test] public void HistoryCannotBeMutatedByAViewAndNewSessionIsEmpty()
        {
            Complete();
            var mutable=session.Completions as System.Collections.Generic.IList<DailyCompletion>;
            Assert.That(mutable,Is.Not.Null);
            Assert.Throws<NotSupportedException>(()=>mutable.Clear());
            var fresh=new PrototypeSession(definition,()=>now);
            Assert.That(fresh.Completions.Count,Is.Zero);
            Assert.That(session.Completions.Count,Is.EqualTo(1));
        }
        [TestCase("count")] [TestCase("mapping")] [TestCase("stage")]
        [TestCase("multiline")] [TestCase("shape")] [TestCase("question")]
        public void InvalidConfigurationRejected(string scenario)
        {
            var clone=UnityEngine.Object.Instantiate(definition);
            try {
                if(scenario=="count")clone.flowers=new[]{clone.flowers[0]};
                if(scenario=="mapping")clone.flowers[1].affinity=clone.flowers[0].affinity;
                if(scenario=="stage")clone.maximumStage=28;
                if(scenario=="multiline")clone.missionGuide="first\nsecond";
                if(scenario=="shape")clone.flowers[0].shape=(FlowerShape)0;
                if(scenario=="question")clone.questions[2].quietLabel="";
                Assert.Throws<InvalidOperationException>(()=>new PrototypeSession(clone,()=>now));
            } finally {UnityEngine.Object.DestroyImmediate(clone);}
        }
        private void Complete(PlantChoice choice=PlantChoice.Quiet,Mood mood=Mood.Calm)
        {
            if(!session.MissionCompleted)PrepareReflection();
            Assert.That(session.SetChoice(session.Round,choice),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.SetMood(session.Round,mood),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.RecordMood(session.Round),Is.EqualTo(PrototypeOutcome.Success));
        }

        private void PrepareReflection()
        {
            Assert.That(session.StartMission(session.Round),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.CompleteMission(session.Round),Is.EqualTo(PrototypeOutcome.Success));
        }

        [Test] public void NoteIsDraftUntilRecordConfirmedAndThenImmutable()
        {
            PrepareReflection();
            session.SetNote(1,"처음에는 조금 지쳤어요.");
            session.SetNote(1,"  쉬고 나니 차분해요.\n천천히 해보고 싶어요.  ");
            Assert.That(session.Completions,Is.Empty);
            Assert.That(session.Stage,Is.Zero);
            Complete(PlantChoice.Quiet,Mood.Calm);
            var record=session.Completions[0];
            Assert.That(record.Note,Is.EqualTo("쉬고 나니 차분해요.\n천천히 해보고 싶어요."));
            Assert.That(record.Mood,Is.EqualTo(Mood.Calm));
            Assert.That(session.SetNote(1,"변경"),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(record.Note,Is.EqualTo("쉬고 나니 차분해요.\n천천히 해보고 싶어요."));
        }
        [TestCase(null)] [TestCase("")] [TestCase("  \n\t ")]
        public void NoteIsOptionalAndWhitespaceDoesNotCreateBody(string value)
        {
            PrepareReflection();
            Assert.That(session.SetNote(1,value),Is.EqualTo(PrototypeOutcome.Success));
            Complete(PlantChoice.Together,Mood.None);
            Assert.That(session.Completions[0].Note,Is.Empty);
            Assert.That(session.Stage,Is.EqualTo(1));
        }
        [Test] public void NoteClearsAtMidnightWithoutChangingOlderRecords()
        {
            PrepareReflection();
            session.SetNote(1,"어제의 마음");Complete();
            now=now.AddDays(1);session.RefreshDay();
            Assert.That(session.TodayNote,Is.Empty);
            Assert.That(session.Completions[0].Note,Is.EqualTo("어제의 마음"));
            now=now.AddDays(1);
            Assert.That(session.SetNote(2,"오래된 입력"),Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.TodayNote,Is.Empty);
            Assert.That(session.Completions.Count,Is.EqualTo(1));
        }
        [Test] public void NoteHasBoundedPlainTextAndDoesNotSplitSurrogates()
        {
            PrepareReflection();
            session.SetNote(1,new string('가',250));
            Assert.That(session.TodayNote,Is.EqualTo(new string('가',PrototypeSession.NoteCharacterLimit)));
            session.SetNote(1,new string('나',199)+"\uD83C\uDF31");
            Assert.That(session.TodayNote.Length,Is.EqualTo(199));
            session.SetNote(1,"첫 줄\r\n둘째\r셋째\t말\0<b>그대로</b>");
            Assert.That(session.TodayNote,Is.EqualTo("첫 줄\n둘째\n셋째 말<b>그대로</b>"));
        }
        [Test] public void RepeatedNoteEditIsIdempotentAndCompletionPublishesOneSnapshot()
        {
            PrepareReflection();session.SetChoice(1,PlantChoice.Quiet);session.SetMood(1,Mood.Down);
            session.SetNote(1,"오늘도 내 속도로");int count=0;
            session.Changed+=()=>{
                count++;Assert.That(session.Stage,Is.EqualTo(1));
                Assert.That(session.Completions[0].Mood,Is.EqualTo(Mood.Down));
                Assert.That(session.Completions[0].Note,Is.EqualTo("오늘도 내 속도로"));
            };
            session.SetNote(1,"오늘도 내 속도로");
            session.RecordMood(1);session.RecordMood(1);
            Assert.That(count,Is.EqualTo(1));
        }
        [Test] public void NewSessionRestoresNeitherNotesNorRecords()
        {
            PrepareReflection();
            session.SetNote(1,"실행 중의 기록");Complete();
            var fresh=new PrototypeSession(definition,()=>now);
            Assert.That(fresh.TodayNote,Is.Empty);Assert.That(fresh.Completions,Is.Empty);
        }
        [Test] public void NotesDoNotAffectSpeciesAndCannotContinueAfterFourStages()
        {
            for(int i=0;i<3;i++)
            {
                if(i>0){now=now.AddDays(1);session.RefreshDay();}
                PrepareReflection();
                session.SetNote(session.Round,new string('가',200));Complete(PlantChoice.Together,Mood.Down);
            }
            now=now.AddDays(1);session.RefreshDay();
            Assert.That(session.SetNote(session.Round,"추가"),Is.EqualTo(PrototypeOutcome.Finished));
            Assert.That(session.FinalPlantId,Is.EqualTo("hydrangea"));
            Assert.That(session.Completions.Count,Is.EqualTo(3));
        }

        [Test] public void MissionCompletionOnlyUnlocksReflection()
        {
            PrepareReflection();
            Assert.That(session.AwaitingMoodRecord,Is.True);
            Assert.That(session.RecordCompleted,Is.False);
            Assert.That(session.Stage,Is.Zero);Assert.That(session.Completions,Is.Empty);
            Assert.That(session.QuietCount,Is.Zero);Assert.That(session.QuestionIndex,Is.Zero);
            Assert.That(session.CompleteMission(1),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(session.StartMission(1),Is.EqualTo(PrototypeOutcome.AlreadyCompleted));
            Assert.That(session.Stage,Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void ReflectionCannotBeEnteredBeforeMissionCompletion(bool started)
        {
            if(started)session.StartMission(1);
            Assert.That(session.SetMood(1,Mood.Calm),Is.EqualTo(PrototypeOutcome.CompleteMissionFirst));
            Assert.That(session.SetNote(1,"아직 기록할 수 없음"),Is.EqualTo(PrototypeOutcome.CompleteMissionFirst));
            Assert.That(session.SetChoice(1,PlantChoice.Quiet),Is.EqualTo(PrototypeOutcome.CompleteMissionFirst));
            Assert.That(session.RecordMood(1),Is.EqualTo(PrototypeOutcome.CompleteMissionFirst));
            Assert.That(session.TodayMood,Is.EqualTo(Mood.None));Assert.That(session.TodayNote,Is.Empty);
            Assert.That(session.TodayChoice,Is.EqualTo(PlantChoice.None));Assert.That(session.Stage,Is.Zero);
        }
        [Test] public void PendingReflectionSurvivesMidnightAndKeepsOriginalDay()
        {
            now=new DateTime(2026,9,26,23,59,59);PrepareReflection();
            session.SetNote(1,"실천 후의 마음");session.SetMood(1,Mood.Calm);
            session.SetChoice(1,PlantChoice.Quiet);now=now.AddDays(2);
            Assert.That(session.RefreshDay(),Is.False);
            Assert.That(session.Day,Is.EqualTo(new DateTime(2026,9,26)));
            Assert.That(session.TodayNote,Is.EqualTo("실천 후의 마음"));Assert.That(session.Stage,Is.Zero);
            Assert.That(session.RecordMood(1),Is.EqualTo(PrototypeOutcome.Success));
            Assert.That(session.Completions[0].Day,Is.EqualTo(new DateTime(2026,9,26)));
            Assert.That(session.Completions[0].CompletedAt,Is.EqualTo(now));
            Assert.That(session.RefreshDay(),Is.True);Assert.That(session.Day,Is.EqualTo(now.Date));
            Assert.That(session.AwaitingMoodRecord||session.RecordCompleted,Is.False);
            Assert.That(session.TodayNote,Is.Empty);Assert.That(session.Stage,Is.EqualTo(1));
            Assert.That(session.RecordMood(1),Is.EqualTo(PrototypeOutcome.StaleRequest));
            Assert.That(session.StartMission(2),Is.EqualTo(PrototypeOutcome.Success));
        }
        [Test] public void PendingReflectionIsNotRestoredInNewSession()
        {
            PrepareReflection();session.SetNote(1,"이번 실행만");
            var fresh=new PrototypeSession(definition,()=>now);
            Assert.That(fresh.AwaitingMoodRecord||fresh.MissionCompleted||fresh.RecordCompleted,Is.False);
            Assert.That(fresh.TodayNote,Is.Empty);Assert.That(fresh.Stage,Is.Zero);
        }
    }
}
