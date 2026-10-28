using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace CapstoneDesign.Runtime.LocalState
{
    public interface IClock { DateTimeOffset UtcNow { get; } TimeZoneInfo TimeZone { get; } }
    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }
        public TimeZoneInfo TimeZone { get { return TimeZoneInfo.Local; } }
    }
    // Development policy only: these values are not approved product balance.
    public sealed class DemoBalanceConfig
    {
        public int ResetHourLocal = 4;
        public int CompletionNutrient = 5;
        public int EnvironmentCost = 10;
        public int GrowthCost = 10;
        public float GrowthAmount = 0.1f;
        public int VisitorChancePercent = 50;
    }
    public sealed class LegacySnapshot
    {
        public int Nutrient;
        public int GardenXp;
        public float Growth;
        public string LastUnlock = "";
        public List<string> CompletedMissionIds = new List<string>();
        public List<string> UnlockIds = new List<string>();
    }
    public sealed class PracticeAnswer { public string Key, Value; internal PracticeAnswer Copy() => (PracticeAnswer)MemberwiseClone(); }
    public sealed class ExperienceRecord
    {
        internal ExperienceRecord Copy() => (ExperienceRecord)MemberwiseClone();
        public string Id, CreatedUtc, Type, Event, Body, Emotion, Thought;
    }
    public sealed class MbctProgramProgress
    {
        public string ProgramId;
        public int NextOrder = 1;
        public List<string> CompletedIds = new List<string>();
        internal MbctProgramProgress Copy() => new MbctProgramProgress { ProgramId=ProgramId, NextOrder=NextOrder, CompletedIds=new List<string>(CompletedIds) };
    }
    public static class MbctPolicy
    {
        // Legacy identifiers and fields always remain the eight-week course.
        public const int TotalPractices=48, PracticesPerWeek=6;
        public static bool IsMbct(string id) => id != null && id.StartsWith("course:MBCT", StringComparison.Ordinal);
        // Version-1 source ordering also fixes activity-category mapping for saved short-course IDs.
        public static int[] ShortSourceOrders(string program) => program=="2w"?new[]{1,3,2,4,9,14,16,21,28,44,43,48}:program=="4w"?new[]{1,3,2,4,9,6,14,16,20,21,23,15,25,26,27,28,31,32,34,37,38,43,44,48}:throw new ArgumentException("Expected a short program.");
        public static int LegacySourceOrder(string missionId)
        {
            string program=ProgramForMission(missionId);int order;
            if(program=="8w" || missionId==null || !int.TryParse(missionId.Substring(("course:MBCT"+ProgramWeeks(program)+"W").Length),out order)
                || order<1 || order>PracticeCount(program) || missionId!=MissionId(program,order))return 0;
            return ShortSourceOrders(program)[order-1];
        }
        public static bool IsProgram(string program) => program=="2w" || program=="4w" || program=="8w";
        public static string ActiveProgram(GardenState state) => state.MbctProgramId;
        public static int ProgramWeeks(string program) => program=="2w"?2:program=="4w"?4:program=="8w"?8:throw new ArgumentException("Unknown course program.");
        public static int PracticeCount(string program) => ProgramWeeks(program)*PracticesPerWeek;
        public static string MissionId(int order) => MissionId("8w",order);
        public static string MissionId(string program,int order) => "course:MBCT"+(program=="8w"?"":ProgramWeeks(program)+"W")+order.ToString("00");
        public static string ProgramForMission(string id) => id!=null && id.StartsWith("course:MBCT2W",StringComparison.Ordinal)?"2w":id!=null && id.StartsWith("course:MBCT4W",StringComparison.Ordinal)?"4w":"8w";
        public static int NextOrder(GardenState state,string program=null)
        {
            program=program??ActiveProgram(state);
            return program=="8w"?state.MbctNextOrder:state.MbctPrograms.FirstOrDefault(p=>p.ProgramId==program)?.NextOrder??1;
        }
        public static IReadOnlyList<string> CompletedIds(GardenState state,string program=null)
        {
            program=program??ActiveProgram(state);
            return program=="8w"?state.MbctCompletedIds:state.MbctPrograms.FirstOrDefault(p=>p.ProgramId==program)?.CompletedIds??new List<string>();
        }
        public static int CompletedCount(GardenState state) => CompletedIds(state).Count;
        public static IEnumerable<string> AllCompletedIds(GardenState state) => state.MbctCompletedIds.Concat(state.MbctPrograms.SelectMany(p=>p.CompletedIds));
        internal static void Advance(GardenState state,string missionId)
        {
            string program=ProgramForMission(missionId);
            if(program=="8w") { state.MbctCompletedIds.Add(missionId);state.MbctNextOrder++;return; }
            var progress=state.MbctPrograms.FirstOrDefault(p=>p.ProgramId==program);
            if(progress==null) { progress=new MbctProgramProgress { ProgramId=program };state.MbctPrograms.Add(progress); }
            progress.CompletedIds.Add(missionId);progress.NextOrder++;
        }
        public static string WeekOf(string cycle)
        {
            var day=DateTime.ParseExact(cycle,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            return day.AddDays(-((int)day.DayOfWeek+6)%7).ToString("yyyy-MM-dd");
        }
        public static string Availability(GardenState state,string program=null)
        {
            program=program??ActiveProgram(state);
            if(NextOrder(state,program)>PracticeCount(program))return ProgramWeeks(program)+"주 활동을 마쳤어요. 배운 활동을 다시 하거나 다른 기간을 선택할 수 있어요.";
            var completedIds=new HashSet<string>(AllCompletedIds(state),StringComparer.Ordinal);
            var completed=state.Sessions.Where(s=>IsMbct(s.MissionId) && completedIds.Contains(s.MissionId)
                && state.RewardReceipts.Contains(s.InstanceId+"|"+s.MissionId)).GroupBy(s=>s.MissionId).Select(g=>g.First()).ToArray();
            if(completed.Any(s=>s.InstanceId==state.LastCycleId))return "오늘 추천 활동을 마쳤어요. 복습하거나 쉬어도 좋아요.";
            if(completed.Count(s=>WeekOf(s.InstanceId)==WeekOf(state.LastCycleId))>=PracticesPerWeek)return "이번 주 6개 활동을 마쳤어요. 쉬거나 복습한 뒤 다음 주에 이어가요.";
            return null;
        }
    }
    public enum ParticipationState { Selected, InProgress, Paused, Ended, RewardCommitted }
    // The active four-mission product uses stable source IDs and separate course occurrences.
    // These dependency-free definitions also serve the standalone persistence tests.
    public sealed class FocusMissionStep
    {
        public string Key, Title, Body, ActionLabel, InputHint;
        public string[] Options = Array.Empty<string>();
        public bool OptionalInput;
    }
    public sealed class FocusMissionDefinition
    {
        public string Id, Code, Title, Overview, Role, BackgroundHex, AccentHex, TextHex;
        public FocusMissionStep[] Steps;
    }
    public static class FocusMissionCatalog
    {
        public const int Version = 1;
        public const string BasicRole = "basic", AdditionalRole = "additional";
        static FocusMissionStep Step(string key,string title,string body,string action,string[] options=null,bool input=false,string hint=null)
            => new FocusMissionStep {Key=key,Title=title,Body=body,ActionLabel=action,Options=options??Array.Empty<string>(),OptionalInput=input,InputHint=hint};
        public static readonly FocusMissionDefinition[] All = {
            new FocusMissionDefinition {Id="course:P10",Code="P10",Title="잠시 숨 고르기",Overview="하던 일 사이에서\n잠시 멈춰봐요.",Role=BasicRole,BackgroundHex="#EFF9F7",AccentHex="#2F726E",TextHex="#224B49",Steps=new[]{
                Step("awareness","지금 살펴보기","생각·감정·몸 감각을\n있는 그대로 살펴요.","계속"),
                Step("breath","숨에 주의 모으기","자연스러운 숨이 느껴지는 곳에\n잠시 머물러요.","계속"),
                Step("expand","주의 넓히기","몸 전체와 주변으로\n주의를 넓혀봐요.","계속"),
                Step("ready","마무리하기","하던 일로 돌아갈\n준비가 되었나요?","완료")}},
            new FocusMissionDefinition {Id="course:P23",Code="P23",Title="한입에 주의 기울이기",Overview="평소 식사의 한입을\n천천히 살펴봐요.",Role=BasicRole,BackgroundHex="#FFF2DF",AccentHex="#9B5D2B",TextHex="#183845",Steps=new[]{
                Step("prepare","한입 준비","먹을 수 있는 음식 한입을\n준비해요.","준비했어요"),
                Step("observe","향과 모양","먹기 전 향과 모양을\n살펴봐요.","계속"),
                Step("bite","한입 느끼기","먹으면서 온도와 질감에\n주의를 둬요.","한입 먹었어요"),
                Step("sensation","감각 돌아보기","어떤 감각이 눈에 띄었나요?\n선택하지 않아도 괜찮아요.","완료",new[]{"향","온도","질감","특별한 감각 없음"})}},
            new FocusMissionDefinition {Id="course:P25",Code="P25",Title="말하기 전에 잠깐 멈추기",Overview="말하거나 메시지를 보내기 전\n잠시 멈춰봐요.",Role=BasicRole,BackgroundHex="#F8EDF1",AccentHex="#96516B",TextHex="#183845",Steps=new[]{
                Step("pause","잠깐 멈추기","아직 말하거나 보내지 말고\n숨을 한 번 고르세요.","멈췄어요"),
                Step("message","전할 말 확인","전하고 싶은 핵심을\n한 문장으로 떠올려요.","계속",null,true,"적거나 속으로 떠올려요"),
                Step("speak","이제 전하기","이제 편한 방식으로\n말하거나 보내세요.","말하거나 보냈어요"),
                Step("check","순서 확인","멈춘 뒤 내용을\n확인했나요?","완료")}},
            new FocusMissionDefinition {Id="free-mission:M03",Code="M03",Title="물 마시기",Overview="물 한 모금으로\n잠시 쉬어가요.",Role=AdditionalRole,BackgroundHex="#F0FAFF",AccentHex="#0B7098",TextHex="#163D50",Steps=new[]{
                Step("prepare","물 준비","마실 물 한 잔을\n가까이 두세요.","준비했어요"),
                Step("drink","한 모금 마시기","물을 마신 뒤 가운데\n물방울을 눌러요.","물 마셨어요")}}
        };
        public static FocusMissionDefinition[] Basic => All.Where(m=>m.Role==BasicRole).ToArray();
        public static FocusMissionDefinition[] Additional => All.Where(m=>m.Role==AdditionalRole).ToArray();
        public static FocusMissionDefinition Find(string idOrCode) => All.FirstOrDefault(m=>m.Id==idOrCode || m.Code==idOrCode);
    }
    public sealed class FocusStepRecord
    {
        public int Step;
        public string Key, Title, Prompt, Value = "", ConfirmedUtc, UpdatedUtc;
        public bool Skipped;
        internal FocusStepRecord Copy() => (FocusStepRecord)MemberwiseClone();
    }
    public static class FocusMissionPolicy
    {
        public const int TotalPractices = 12, PracticesPerWeek = 6;
        static readonly string[] Sequence = {"P23","P10","P25"};
        public static bool IsFocus(ActivitySession session) => session!=null && session.FocusVersion==FocusMissionCatalog.Version;
        public static bool IsActive(ActivitySession session) => session!=null && (session.Status==ParticipationState.Selected || session.Status==ParticipationState.InProgress || session.Status==ParticipationState.Paused);
        public static FocusMissionDefinition MissionAt(int order) => order<1 || order>TotalPractices?null:FocusMissionCatalog.Find(Sequence[(order-1)%Sequence.Length]);
        public static ActivitySession Active(GardenState state,string role) => state.Sessions.LastOrDefault(s=>IsFocus(s) && s.FocusRole==role && IsActive(s));
        public static IEnumerable<ActivitySession> History(GardenState state,string missionId=null) => state.Sessions.Where(s=>IsFocus(s) && (missionId==null || s.MissionId==missionId || FocusMissionCatalog.Find(missionId)?.Id==s.MissionId)).OrderByDescending(s=>s.SelectedUtc,StringComparer.Ordinal);
        public static IEnumerable<ActivitySession> Completed(GardenState state) => state.Sessions.Where(s=>IsFocus(s) && s.FocusRole==FocusMissionCatalog.BasicRole && !s.FocusReview && s.Status==ParticipationState.RewardCommitted);
        public static int CompletedCount(GardenState state) => Completed(state).Count();
        public static int NextOrder(GardenState state) => CompletedCount(state)+1;
        public static bool AdditionalCompletedToday(GardenState state) => state.Sessions.Any(s=>IsFocus(s) && s.FocusRole==FocusMissionCatalog.AdditionalRole && !s.FocusReview && s.Status==ParticipationState.RewardCommitted && s.InstanceId==state.LastCycleId);
        public static bool HasPendingToday(GardenState state)
        {
            if(state==null)return false;
            // A retained normal draft still needs attention; optional reviews do not
            // turn a completed day back into an incomplete one.
            return state.Sessions.Any(s=>IsFocus(s) && IsActive(s) && !s.FocusReview
                    && (s.FocusRole==FocusMissionCatalog.BasicRole || s.FocusRole==FocusMissionCatalog.AdditionalRole))
                || Availability(state)==null || !AdditionalCompletedToday(state);
        }
        public static string Availability(GardenState state)
        {
            if(NextOrder(state)>TotalPractices)return "2주 과정을 마쳤어요. 내 과정에서 기록을 돌아볼 수 있어요.";
            var completed=Completed(state).ToArray();
            if(completed.Any(s=>s.InstanceId==state.LastCycleId))return "오늘 기본 미션을 마쳤어요. 다음 기본 미션은 내일 열려요.";
            if(state.LastCycleId.Length>0 && completed.Count(s=>MbctPolicy.WeekOf(s.InstanceId)==MbctPolicy.WeekOf(state.LastCycleId))>=PracticesPerWeek)return "이번 주 기본 미션을 모두 마쳤어요. 다음 주에 이어가요.";
            return null;
        }
    }
    public sealed class RecommendationContext
    {
        public int UtcOffsetMinutes;
        public string DecisionId, PolicyId, AppVersion;
        public float[] Features;
        internal RecommendationContext Copy() => new RecommendationContext {DecisionId=DecisionId,PolicyId=PolicyId,AppVersion=AppVersion,UtcOffsetMinutes=UtcOffsetMinutes,Features=Features==null?null:(float[])Features.Clone()};
    }
    public sealed class ActivitySession
    {
        internal ActivitySession Copy() { var copy=(ActivitySession)MemberwiseClone(); copy.Answers=Answers?.Select(a=>a?.Copy()).ToList(); copy.Recommendation=Recommendation?.Copy(); copy.FocusStepRecords=FocusStepRecords?.Select(r=>r?.Copy()).ToList(); return copy; }
        public RecommendationContext Recommendation;
        public string SessionId, MissionId, InstanceId, SelectedUtc, StartedUtc, EndedUtc;
        public string Mood, Note;
        // Optional schema-1 additions: old sessions remain FocusVersion=0 and keep their meaning.
        public int FocusVersion, FocusOrder, RewardAmount;
        public string FocusRole, MissionTitleSnapshot, MissionDescriptionSnapshot;
        public bool FocusReview;
        public List<FocusStepRecord> FocusStepRecords = new List<FocusStepRecord>();
        public int CourseOrder;
        public int InstructionStep;
        public List<PracticeAnswer> Answers = new List<PracticeAnswer>();
        public long RealStepHighWater, VirtualSteps;
        public string StepSource = "";
        public bool GoalNotificationCommitted;
        // Zero is an older 300-step, since-start mission; never reinterpret an existing save.
        public int WalkingGoalSteps;
        public string WalkingQueryStartUtc, WalkingQueryEndUtc, WalkingTargetBasis;
        public bool WalkingSleepReduced;
        public ParticipationState Status;
    }
    public sealed class WalkingTarget
    {
        public int GoalSteps;
        public string QueryStartUtc, QueryEndUtc, Basis;
        public bool UsedBaseline, ReducedForSleep;
    }
    public sealed class DebugWalkPeriod
    {
        internal DebugWalkPeriod Copy() => (DebugWalkPeriod)MemberwiseClone();
        public string Id, StartedUtc, EndedUtc;
        public long RealStepHighWater, VirtualSteps, RewardedUnits;
        public string StepSource = "";
    }
    public sealed class DailyDecision
    {
        internal DailyDecision Copy() => (DailyDecision)MemberwiseClone();
        public string CycleId, TimeZoneId, AppliedUtc, VisitorId;
    }
    public sealed class PurchaseCommand { public string RequestId, Command; internal PurchaseCommand Copy() => (PurchaseCommand)MemberwiseClone(); }
    public sealed class GardenGrowthRecord
    {
        public string RequestId, PlantId, CreatedUtc, LinkedSessionId;
        public float BeforeGrowth, AfterGrowth;
        internal GardenGrowthRecord Copy() => (GardenGrowthRecord)MemberwiseClone();
    }
    public sealed class GardenRecordSummary
    {
        public int Completed, Ended;
        public readonly Dictionary<string,int> ExperienceTypes = new Dictionary<string,int>();
        public static GardenRecordSummary Calculate(GardenState state,DateTimeOffset fromInclusive,DateTimeOffset untilExclusive)
        {
            if(untilExclusive<fromInclusive)throw new ArgumentException("Invalid summary interval.");
            var result=new GardenRecordSummary();
            foreach(var session in state.Sessions)
            {
                DateTimeOffset ended;
                if(!DateTimeOffset.TryParse(session.EndedUtc,out ended) || ended<fromInclusive || ended>=untilExclusive)continue;
                if(session.Status==ParticipationState.RewardCommitted)result.Completed++;
                else if(session.Status==ParticipationState.Ended)result.Ended++;
            }
            foreach(var record in state.Experiences)
            {
                var created=DateTimeOffset.Parse(record.CreatedUtc,System.Globalization.CultureInfo.InvariantCulture);
                if(created<fromInclusive || created>=untilExclusive)continue;
                int count;result.ExperienceTypes.TryGetValue(record.Type,out count);result.ExperienceTypes[record.Type]=count+1;
            }
            return result;
        }
    }
    public sealed class EnvironmentPlacement { public string EnvironmentId, Slot; internal EnvironmentPlacement Copy() => (EnvironmentPlacement)MemberwiseClone(); }
    public sealed class PlantProgress { public string PlantId; public float Growth; internal PlantProgress Copy() => (PlantProgress)MemberwiseClone(); }
    public sealed class GardenState
    {
        internal GardenState Copy() => (GardenState)MemberwiseClone();
        public int SchemaVersion = 1;
        public bool MigrationCompleted;
        public string InstallationId = Guid.NewGuid().ToString("N");
        public int Nutrient, GardenXp;
        public float LegacyGrowth;
        public string LastUnlock = "";
        public int NextCourseOrder = 1;
        // Optional fields preserve schema-1 legacy course progress and garden rewards.
        public int MbctNextOrder = 1;
        public string MbctProgramId = "8w";
        public int MbctProgramSelectionVersion;
        public bool MbctProgramChosen;
        public List<MbctProgramProgress> MbctPrograms = new List<MbctProgramProgress>();
        public List<string> MbctCompletedIds = new List<string>();
        public List<PracticeAnswer> PracticePreferences = new List<PracticeAnswer>();
        public List<ExperienceRecord> Experiences = new List<ExperienceRecord>();
        public string LastCycleId = "";
        // App-only testing date; never changes the OS clock or sensor timestamps.
        public int PreviewDayOffset;
        public List<string> LegacyCompletedUnknownDate = new List<string>();
        public List<string> UnlockIds = new List<string>();
        public List<string> CompletedCourseIds = new List<string>();
        public List<string> RewardReceipts = new List<string>();
        public List<string> PurchaseReceipts = new List<string>();
        public List<PurchaseCommand> PurchaseCommands = new List<PurchaseCommand>();
        public List<GardenGrowthRecord> GrowthHistory = new List<GardenGrowthRecord>();
        public List<DailyDecision> DailyDecisions = new List<DailyDecision>();
        public List<ActivitySession> Sessions = new List<ActivitySession>();
        // Optional for existing schema-1 saves. Missing XML initializes to an empty list.
        public List<DebugWalkPeriod> DebugWalkPeriods = new List<DebugWalkPeriod>();
        public List<EnvironmentPlacement> Environments = new List<EnvironmentPlacement>();
        public List<PlantProgress> Plants = new List<PlantProgress>();
        // Optional appearance data; old schema-1 saves are migrated without changing rewards.
        public GardenPlantAppearance PlantAppearance;
        public List<string> DiscoveredAnimalIds = new List<string>();
    }
    public interface IStateStore { GardenState Load(); void Save(GardenState candidate); }
    public static class StateCodec
    {
        public static string Encode(GardenState value)
        {
            Validate(value);
            using (var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture))
            { new XmlSerializer(typeof(GardenState)).Serialize(writer, value); return writer.ToString(); }
        }
        public static GardenState Decode(string value)
        {
            using (var reader = System.Xml.XmlReader.Create(new StringReader(value), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var document=new System.Xml.XmlDocument { XmlResolver=null }; document.Load(reader);
                foreach(var required in new[]{"SchemaVersion","MigrationCompleted","InstallationId","Nutrient","NextCourseOrder","LastCycleId","LegacyCompletedUnknownDate","UnlockIds","CompletedCourseIds","RewardReceipts","PurchaseReceipts","DailyDecisions","Sessions","Environments","Plants","DiscoveredAnimalIds"})
                    if(document.DocumentElement==null || document.DocumentElement[required]==null) throw new InvalidDataException("State field missing: "+required);
                GardenState state;
                using(var nodes=new System.Xml.XmlNodeReader(document)) state=(GardenState)new XmlSerializer(typeof(GardenState)).Deserialize(nodes);
                Validate(state);
                return state;
            }
        }
        public static void Validate(GardenState s)
        {
            if (s == null || s.SchemaVersion != 1 || !s.MigrationCompleted || s.Nutrient < 0 || s.NextCourseOrder < 1 || s.NextCourseOrder > 29 || string.IsNullOrWhiteSpace(s.InstallationId) || !Finite(s.LegacyGrowth) || s.PreviewDayOffset < 0 || s.PreviewDayOffset > 36500)
                throw new InvalidDataException("Unsupported or invalid garden state; preserved for recovery.");
            Unique(s.LegacyCompletedUnknownDate); Unique(s.UnlockIds); Unique(s.CompletedCourseIds); Unique(s.RewardReceipts); Unique(s.PurchaseReceipts); Unique(s.DiscoveredAnimalIds);
            if(s.CompletedCourseIds.Count != s.NextCourseOrder-1) throw new InvalidDataException("Course progress inconsistent.");
            if(!MbctPolicy.IsProgram(s.MbctProgramId) || s.MbctProgramSelectionVersion<0 || s.MbctProgramSelectionVersion>1 || s.MbctPrograms==null)throw new InvalidDataException("Invalid MBCT program selection.");
            Unique(s.MbctPrograms.Select(p=>p?.ProgramId));
            foreach(var program in s.MbctPrograms)
            {
                if(program.ProgramId=="8w" || !MbctPolicy.IsProgram(program.ProgramId) || program.CompletedIds==null || program.NextOrder<1 || program.NextOrder>MbctPolicy.PracticeCount(program.ProgramId)+1 || program.CompletedIds.Count!=program.NextOrder-1)throw new InvalidDataException("Invalid short course progress.");
                for(int i=0;i<program.CompletedIds.Count;i++)if(program.CompletedIds[i]!=MbctPolicy.MissionId(program.ProgramId,i+1))throw new InvalidDataException("Short course progress order mismatch.");
            }
            if(s.MbctNextOrder<1 || s.MbctNextOrder>MbctPolicy.TotalPractices+1 || s.MbctCompletedIds==null || s.PracticePreferences==null || s.Experiences==null)
                throw new InvalidDataException("Invalid MBCT state.");
            Unique(s.MbctCompletedIds);
            if(s.MbctCompletedIds.Count!=s.MbctNextOrder-1 || s.MbctCompletedIds.Any(id=>!MbctPolicy.IsMbct(id)))throw new InvalidDataException("Invalid MBCT progress.");
            for(int i=0;i<s.MbctCompletedIds.Count;i++)if(s.MbctCompletedIds[i]!=MbctPolicy.MissionId(i+1))throw new InvalidDataException("MBCT progress order mismatch.");
            if(s.PracticePreferences.Any(a=>a==null || a.Value==null))throw new InvalidDataException("Invalid practice preferences.");
            Unique(s.PracticePreferences.Select(a=>a.Key));
            if(s.Experiences.Any(e=>e==null || !Timestamp(e.CreatedUtc) || !new[]{"즐거움","불편함","중립","잘 모르겠음"}.Contains(e.Type)))throw new InvalidDataException("Invalid experience.");
            Unique(s.Experiences.Select(e=>e.Id));
            if (s.Sessions == null || s.DailyDecisions == null || s.Environments == null || s.Plants == null || s.PurchaseCommands == null || s.DebugWalkPeriods == null || s.LastCycleId == null)
                throw new InvalidDataException("State collections missing; recovery required.");
            if (s.Sessions.Any(x => x == null) || s.DailyDecisions.Any(x => x == null) || s.Environments.Any(x => x == null) || s.Plants.Any(x => x == null) || s.PurchaseCommands.Any(x => x == null) || s.DebugWalkPeriods.Any(x => x == null))
                throw new InvalidDataException("State contains missing records.");
            Unique(s.Sessions.Select(x => x.SessionId)); Unique(s.DailyDecisions.Select(x => x.CycleId)); Unique(s.Environments.Select(x => x.Slot)); Unique(s.Environments.Select(x => x.EnvironmentId)); Unique(s.Plants.Select(x => x.PlantId)); Unique(s.PurchaseCommands.Select(x => x.RequestId));
            Unique(s.DebugWalkPeriods.Select(x => x.Id));
            if(s.DebugWalkPeriods.Count(x=>string.IsNullOrEmpty(x.EndedUtc))>1)throw new InvalidDataException("Multiple debug walk periods active.");
            foreach(var p in s.DebugWalkPeriods)
            {
                DateTimeOffset started,ended;
                if(!Timestamp(p.StartedUtc) || (!string.IsNullOrEmpty(p.EndedUtc) && (!Timestamp(p.EndedUtc) || !DateTimeOffset.TryParse(p.StartedUtc,out started) || !DateTimeOffset.TryParse(p.EndedUtc,out ended) || ended<started)) || p.RealStepHighWater<0 || p.VirtualSteps<0 || p.RealStepHighWater>long.MaxValue-p.VirtualSteps || p.RewardedUnits<0 || p.RewardedUnits<(p.RealStepHighWater+p.VirtualSteps)/10)
                    throw new InvalidDataException("Invalid debug walk period.");
            }
            string previous = "";
            foreach (var d in s.DailyDecisions)
            {
                if (!Cycle(d.CycleId) || string.CompareOrdinal(d.CycleId, previous) <= 0 || d.VisitorId == null || (d.VisitorId.Length > 0 && !Typed(d.VisitorId,"animal")) || !Timestamp(d.AppliedUtc) || string.IsNullOrWhiteSpace(d.TimeZoneId)) throw new InvalidDataException("Invalid daily decision.");
                previous = d.CycleId;
                if (d.VisitorId.Length > 0 && !s.DiscoveredAnimalIds.Contains(d.VisitorId)) throw new InvalidDataException("Visitor discovery missing.");
            }
            if (previous != s.LastCycleId) throw new InvalidDataException("Daily high-water mark inconsistent.");
            foreach (var a in s.Sessions)
            {
                if (!(Typed(a.MissionId,"course") || Typed(a.MissionId,"free-mission")) || !s.DailyDecisions.Any(d=>d.CycleId==a.InstanceId) || !Enum.IsDefined(typeof(ParticipationState),a.Status) || a.CourseOrder < 0 || a.CourseOrder > (MbctPolicy.IsMbct(a.MissionId)?MbctPolicy.PracticeCount(MbctPolicy.ProgramForMission(a.MissionId)):28) || !Timestamp(a.SelectedUtc)) throw new InvalidDataException("Invalid session.");
                if(a.InstructionStep<0 || a.Answers==null || a.Answers.Any(x=>x==null || x.Value==null))throw new InvalidDataException("Invalid practice draft.");
                if(MbctPolicy.IsMbct(a.MissionId) && (a.CourseOrder<1 || a.MissionId!=MbctPolicy.MissionId(MbctPolicy.ProgramForMission(a.MissionId),a.CourseOrder)))throw new InvalidDataException("MBCT mission/order mismatch.");
                Unique(a.Answers.Select(x=>x.Key));
                ValidateRecommendation(a.Recommendation);
                if (a.CourseOrder > 0 && !Typed(a.MissionId,"course")) throw new InvalidDataException("Invalid session course type.");
                if ((a.Status == ParticipationState.InProgress || a.Status == ParticipationState.Paused || a.Status == ParticipationState.RewardCommitted) && !Timestamp(a.StartedUtc)) throw new InvalidDataException("Session start missing.");
                if ((a.Status == ParticipationState.Ended || a.Status == ParticipationState.RewardCommitted) && !Timestamp(a.EndedUtc)) throw new InvalidDataException("Session end missing.");
                if(a.WalkingGoalSteps != 0)
                {
                    DateTimeOffset walkingStart, walkingEnd;
                    if(a.MissionId!="free-mission:M15" || a.WalkingGoalSteps<1000 || a.WalkingGoalSteps>10000 ||
                        !DateTimeOffset.TryParse(a.WalkingQueryStartUtc,out walkingStart) ||
                        !DateTimeOffset.TryParse(a.WalkingQueryEndUtc,out walkingEnd) || walkingEnd<=walkingStart ||
                        (walkingEnd-walkingStart).TotalHours<22 || (walkingEnd-walkingStart).TotalHours>26 ||
                        string.IsNullOrEmpty(a.WalkingTargetBasis) || a.WalkingTargetBasis.Length>160)
                        throw new InvalidDataException("Invalid personalized walking target.");
                }
                if(a.RealStepHighWater<0 || a.VirtualSteps<0 || a.RealStepHighWater>long.MaxValue-a.VirtualSteps)throw new InvalidDataException("Invalid session steps.");
            }
            ValidateFocus(s);
            if (s.Environments.Any(e=>!Typed(e.EnvironmentId,"environment") || e.Slot != e.Slot.Trim().ToLowerInvariant()) || s.Plants.Any(p=>!Typed(p.PlantId,"plant") || !Finite(p.Growth) || p.Growth < 0) || s.CompletedCourseIds.Any(id=>!Typed(id,"course")) || s.DiscoveredAnimalIds.Any(id=>!Typed(id,"animal"))) throw new InvalidDataException("Invalid permanent garden records.");
            if (s.PurchaseCommands.Any(c=>string.IsNullOrWhiteSpace(c.Command) || !s.PurchaseReceipts.Contains(c.RequestId))) throw new InvalidDataException("Purchase identity mismatch.");
            if(s.GrowthHistory==null)throw new InvalidDataException("Growth history missing.");
            Unique(s.GrowthHistory.Select(record=>record?.RequestId));
            foreach(var record in s.GrowthHistory)
            {
                if(!Timestamp(record.CreatedUtc) || !Typed(record.PlantId,"plant") || !Finite(record.BeforeGrowth) || !Finite(record.AfterGrowth)
                    || record.BeforeGrowth<0 || record.AfterGrowth<=record.BeforeGrowth || record.LinkedSessionId==null
                    || !s.PurchaseCommands.Any(command=>command.RequestId==record.RequestId && command.Command=="growth|"+record.PlantId))
                    throw new InvalidDataException("Invalid growth history.");
                if(record.LinkedSessionId.Length>0 && !s.Sessions.Any(session=>session.SessionId==record.LinkedSessionId &&
                    (session.Status==ParticipationState.RewardCommitted || session.Status==ParticipationState.Ended)))
                    throw new InvalidDataException("Growth record references an unavailable activity.");
            }
            GardenPlantVisuals.Validate(s);
        }
        static void ValidateFocus(GardenState state)
        {
            if(state.Sessions.Any(s=>s.FocusVersion<0 || s.FocusVersion>FocusMissionCatalog.Version))throw new InvalidDataException("Unsupported focus mission version.");
            var sessions=state.Sessions.Where(FocusMissionPolicy.IsFocus).ToArray();
            foreach(var session in sessions)
            {
                var definition=FocusMissionCatalog.Find(session.MissionId);
                if(definition==null || session.FocusRole!=definition.Role || session.CourseOrder!=0 || string.IsNullOrWhiteSpace(session.MissionTitleSnapshot) || string.IsNullOrWhiteSpace(session.MissionDescriptionSnapshot)
                    || session.InstructionStep>definition.Steps.Length+1 || session.FocusStepRecords==null || session.FocusStepRecords.Count!=definition.Steps.Length || session.RewardAmount<0
                    || (session.Mood!=null && session.Mood.Length>80) || (session.Note!=null && session.Note.Length>500))throw new InvalidDataException("Invalid focus mission record.");
                if(session.FocusRole==FocusMissionCatalog.BasicRole)
                {
                    if(FocusMissionPolicy.MissionAt(session.FocusOrder)?.Id!=session.MissionId)throw new InvalidDataException("Focus course occurrence mismatch.");
                }
                else if(session.FocusOrder!=0)throw new InvalidDataException("Invalid additional mission occurrence.");
                bool gap=false;DateTimeOffset previous=DateTimeOffset.MinValue;
                for(int i=0;i<session.FocusStepRecords.Count;i++)
                {
                    var record=session.FocusStepRecords[i];
                    if(record==null || record.Step!=i || record.Key!=definition.Steps[i].Key || string.IsNullOrWhiteSpace(record.Title) || string.IsNullOrWhiteSpace(record.Prompt)
                        || record.Value==null || record.Value.Length>500 || (record.Skipped && record.Value.Length>0)
                        || (!string.IsNullOrEmpty(record.UpdatedUtc) && !Timestamp(record.UpdatedUtc)))throw new InvalidDataException("Invalid focus step record.");
                    if(string.IsNullOrEmpty(record.ConfirmedUtc)){gap=true;continue;}
                    DateTimeOffset confirmed;
                    if(gap || !DateTimeOffset.TryParse(record.ConfirmedUtc,out confirmed) || confirmed<=previous || !Timestamp(session.StartedUtc) || confirmed<DateTimeOffset.Parse(session.StartedUtc))throw new InvalidDataException("Focus confirmation order mismatch.");
                    previous=confirmed;
                }
                int reached=session.FocusStepRecords.Count(r=>!string.IsNullOrEmpty(r.ConfirmedUtc));
                if(session.InstructionStep>reached && reached<definition.Steps.Length)throw new InvalidDataException("Unconfirmed focus step skipped.");
                if(session.Status==ParticipationState.RewardCommitted && (reached!=definition.Steps.Length || DateTimeOffset.Parse(session.EndedUtc)<previous))throw new InvalidDataException("Incomplete focus mission committed.");
                if(session.Status!=ParticipationState.RewardCommitted && session.RewardAmount!=0)throw new InvalidDataException("Uncommitted focus reward.");
                if(session.FocusReview && session.RewardAmount!=0)throw new InvalidDataException("Review cannot award nutrients.");
                if(session.Status==ParticipationState.RewardCommitted && !session.FocusReview)
                {
                    string receipt=session.FocusRole==FocusMissionCatalog.BasicRole?"focus-v1|basic|"+session.FocusOrder:session.InstanceId+"|focus-v1|additional|M03";
                    if(!state.RewardReceipts.Contains(receipt))throw new InvalidDataException("Focus reward receipt missing.");
                }
            }
            foreach(string role in new[]{FocusMissionCatalog.BasicRole,FocusMissionCatalog.AdditionalRole})
                if(sessions.Count(s=>s.FocusRole==role && FocusMissionPolicy.IsActive(s))>1)throw new InvalidDataException("Multiple focus drafts in the same role.");
            var completed=FocusMissionPolicy.Completed(state).OrderBy(s=>s.FocusOrder).ToArray();
            for(int i=0;i<completed.Length;i++)if(completed[i].FocusOrder!=i+1)throw new InvalidDataException("Focus course progress is not contiguous.");
            if(sessions.Where(s=>s.FocusRole==FocusMissionCatalog.AdditionalRole && s.RewardAmount>0).GroupBy(s=>s.InstanceId).Any(g=>g.Count()>1))throw new InvalidDataException("Additional reward duplicated.");
            if(sessions.Any(s=>s.FocusReview && s.FocusOrder>completed.Length))throw new InvalidDataException("Unlearned focus review.");
        }
        static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        internal static void ValidateRecommendation(RecommendationContext context)
        {
            if(context==null)return;
            Guid decision;
            if(!Guid.TryParseExact(context.DecisionId,"N",out decision)
                || new[]{context.PolicyId,context.AppVersion}.Any(value=>value==null || !System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z0-9._-]{1,48}$"))
                || context.UtcOffsetMinutes < -840 || context.UtcOffsetMinutes > 840
                || context.Features==null || context.Features.Length!=4 || context.Features.Any(value=>!Finite(value) || value<0 || value>1))
                throw new InvalidDataException("Invalid recommendation attribution.");
        }
        internal static bool Typed(string id,string prefix) { return id != null && id.StartsWith(prefix+":",StringComparison.Ordinal) && id.Length>prefix.Length+1 && !id.Contains("|"); }
        static bool Timestamp(string text) { DateTimeOffset value; return DateTimeOffset.TryParse(text,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out value); }
        static bool Cycle(string text) { DateTime value; return DateTime.TryParseExact(text,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out value); }
        static void Unique(IEnumerable<string> values)
        { if (values == null) throw new InvalidDataException("Missing state collection."); var ids=new HashSet<string>(StringComparer.Ordinal); foreach(var id in values) if(string.IsNullOrWhiteSpace(id) || !ids.Add(id)) throw new InvalidDataException("Missing or duplicate state identity."); }
        // Copies used for isolated reads/transactions do not need an XML round trip.
        // Scalar fields use MemberwiseClone; every mutable list and nested record is detached.
        public static GardenState Clone(GardenState value)
        {
            Validate(value);var copy=value.Copy();
            copy.MbctCompletedIds=new List<string>(value.MbctCompletedIds);
            copy.MbctPrograms=value.MbctPrograms.Select(p=>p.Copy()).ToList();
            copy.LegacyCompletedUnknownDate=new List<string>(value.LegacyCompletedUnknownDate);
            copy.UnlockIds=new List<string>(value.UnlockIds);copy.CompletedCourseIds=new List<string>(value.CompletedCourseIds);
            copy.RewardReceipts=new List<string>(value.RewardReceipts);copy.PurchaseReceipts=new List<string>(value.PurchaseReceipts);
            copy.DiscoveredAnimalIds=new List<string>(value.DiscoveredAnimalIds);
            copy.PracticePreferences=value.PracticePreferences.Select(a=>a.Copy()).ToList();
            copy.Experiences=value.Experiences.Select(e=>e.Copy()).ToList();
            copy.PurchaseCommands=value.PurchaseCommands.Select(c=>c.Copy()).ToList();
            copy.GrowthHistory=value.GrowthHistory.Select(record=>record.Copy()).ToList();
            copy.DailyDecisions=value.DailyDecisions.Select(d=>d.Copy()).ToList();
            copy.Sessions=value.Sessions.Select(a=>a.Copy()).ToList();
            copy.DebugWalkPeriods=value.DebugWalkPeriods.Select(d=>d.Copy()).ToList();
            copy.Environments=value.Environments.Select(e=>e.Copy()).ToList();
            copy.Plants=value.Plants.Select(p=>p.Copy()).ToList();
            copy.PlantAppearance=value.PlantAppearance?.Copy();return copy;
        }
        public static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
    }
    public static class GardenStatePaths
    {
        public static string StateFile(string persistentRoot,string inputMode,bool editor,string overrideRoot=null)
        {
            if(string.IsNullOrEmpty(inputMode))throw new ArgumentException("An input mode is required.",nameof(inputMode));
            string root=!string.IsNullOrWhiteSpace(overrideRoot)?overrideRoot:
                editor?Path.Combine(persistentRoot,"editor-mission-preview"):persistentRoot;
            return Path.Combine(root,"garden-"+inputMode.ToLowerInvariant(),"state.xml");
        }

        public static bool ShouldMigrateLegacy(bool editor,string mode) => !editor && mode=="LIVE";
    }
    // Lifetime lock prevents independent writers. A missing/corrupt primary with backup is NEVER a new account.
    public sealed class FileStateStore : IStateStore, IDisposable
    {
        readonly string path;
        readonly FileStream writerLock;
        bool disposed;
        void RequireWriter() { if(disposed) throw new ObjectDisposedException("FileStateStore"); }
        public FileStateStore(string path)
        {
            this.path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(this.path));
            writerLock = new FileStream(this.path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public GardenState Load()
        {
            RequireWriter();
            if (!File.Exists(path))
            {
                if (File.Exists(path + ".bak") || File.Exists(path + ".tmp")) throw new InvalidDataException("Primary state missing; recovery required. Backup retained.");
                return null;
            }
            string envelope = File.ReadAllText(path, Encoding.UTF8);
            int newline = envelope.IndexOf('\n');
            if (newline < 0 || StateCodec.Hash(envelope.Substring(newline + 1)) != envelope.Substring(0, newline))
                throw new InvalidDataException("State checksum failed; no automatic rollback or new grant. Backup retained.");
            return StateCodec.Decode(envelope.Substring(newline + 1));
        }
        public void Save(GardenState candidate)
        {
            RequireWriter(); StateCodec.Validate(candidate);
            string body = StateCodec.Encode(candidate);
            byte[] bytes = Encoding.UTF8.GetBytes(StateCodec.Hash(body) + "\n" + body);
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        public void Dispose() { if(!disposed) { disposed=true; writerLock.Dispose(); } }
    }
    public sealed class GardenStateService : IDisposable
    {
        readonly IStateStore store;
        readonly IClock clock;
        readonly DemoBalanceConfig config;
        readonly LegacySnapshot legacy;
        readonly object gate = new object();
        GardenState state;
        bool disposed;
        DailyDecision pendingDecision;
        string[] cycleCandidates = Array.Empty<string>();
        public string LastError { get; private set; }
        public long SnapshotReads { get; private set; }
        public DateTimeOffset CurrentUtc => EffectiveUtc(state);
        public TimeZoneInfo CurrentTimeZone => clock.TimeZone;
        DateTimeOffset EffectiveUtc(GardenState source) => clock.UtcNow.AddDays(source == null ? 0 : source.PreviewDayOffset);
        public GardenStateService(IStateStore store, IClock clock, DemoBalanceConfig config, LegacySnapshot legacy = null)
        {
            this.store = store; this.clock = clock; this.legacy = legacy ?? new LegacySnapshot();
            this.config = new DemoBalanceConfig { ResetHourLocal = config.ResetHourLocal, CompletionNutrient = config.CompletionNutrient, EnvironmentCost = config.EnvironmentCost, GrowthCost = config.GrowthCost, GrowthAmount = config.GrowthAmount, VisitorChancePercent = config.VisitorChancePercent };
            if (config.ResetHourLocal < 0 || config.ResetHourLocal > 23 || config.CompletionNutrient < 0 || config.EnvironmentCost < 0 || config.GrowthCost < 0 || config.GrowthAmount <= 0 || (float.IsInfinity(config.GrowthAmount) || float.IsNaN(config.GrowthAmount)) || config.VisitorChancePercent < 0 || config.VisitorChancePercent > 100)
                throw new ArgumentException("Invalid DEV_DEFAULT configuration.");
        }
        public GardenState Snapshot
        {
            get
            {
#if UNITY_5_3_OR_NEWER
                using var timing = CapstoneDesign.Runtime.UiPerformanceProbe.Measure("State.Snapshot");
#endif
                lock (gate) { RequireOpen(); SnapshotReads++; return StateCodec.Clone(state); }
            }
        }
        public bool Open()
        {
            lock (gate)
            {
                if (disposed) { LastError="State service disposed."; return false; }
                if (state != null) return true;
                try
                {
                    var loaded = store.Load();
                    if (loaded == null)
                    {
                        loaded = new GardenState { MigrationCompleted = true, MbctProgramSelectionVersion=1, MbctProgramChosen=false, Nutrient = Math.Max(0, legacy.Nutrient), GardenXp = legacy.GardenXp, LegacyGrowth = legacy.Growth, LastUnlock = legacy.LastUnlock };
                        loaded.LegacyCompletedUnknownDate.AddRange(legacy.CompletedMissionIds.Distinct());
                        loaded.UnlockIds.AddRange(legacy.UnlockIds.Distinct());
                        GardenPlantVisuals.Sync(loaded);
                        store.Save(loaded);
                    }
                    StateCodec.Validate(loaded);
                    bool courseMigrated=loaded.MbctProgramSelectionVersion==0;
                    if(courseMigrated) { loaded.MbctProgramSelectionVersion=1;loaded.MbctProgramChosen=true; }
                    if (GardenPlantVisuals.Sync(loaded) || courseMigrated) { StateCodec.Validate(loaded); store.Save(loaded); }
                    state = loaded; LastError = null; return true;
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        void RequireOpen() { if(disposed) throw new ObjectDisposedException("GardenStateService"); if (state == null) throw new InvalidOperationException("State unavailable. Open must succeed before play."); }
        bool Change(Action<GardenState> mutation)
        {
            lock (gate)
            {
                RequireOpen();
                try
                {
                    var next = StateCodec.Clone(state); mutation(next); StateCodec.Validate(next); store.Save(next); state = next; LastError = null; return true;
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        string Now { get { return CurrentUtc.ToString("O"); } }
        string CycleFor(GardenState source) => TimeZoneInfo.ConvertTime(EffectiveUtc(source), clock.TimeZone).DateTime.AddHours(-config.ResetHourLocal).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        public string CurrentCycleId
        {
            get { return CycleFor(state); }
        }
        public bool RefreshCycle(IEnumerable<string> eligibleAnimalIds)
        {
            lock (gate)
            {
                RequireOpen();
                cycleCandidates=NormalizeCycleCandidates(eligibleAnimalIds);
                if (string.CompareOrdinal(CurrentCycleId, state.LastCycleId) <= 0) return true;
                bool committed = Change(next => AdvanceCycle(next,cycleCandidates));
                if(committed) pendingDecision=null;
                return committed;
            }
        }
        static string[] NormalizeCycleCandidates(IEnumerable<string> ids) => (ids ?? Array.Empty<string>())
            .Where(id=>id!=null && id.StartsWith("animal:",StringComparison.Ordinal)).Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
        void AdvanceCycle(GardenState next,string[] candidates)
        {
            string cycle=CycleFor(next);
            if(string.CompareOrdinal(cycle,next.LastCycleId)<=0)return;
            if(pendingDecision==null || pendingDecision.CycleId!=cycle)
            {
                string hash=StateCodec.Hash(next.InstallationId+"|"+cycle);
                uint roll=Convert.ToUInt32(hash.Substring(0,8),16);
                string visitor=candidates.Length>0 && roll%100<config.VisitorChancePercent
                    ? candidates[Convert.ToUInt32(hash.Substring(8,8),16)%candidates.Length]:"";
                pendingDecision=new DailyDecision {CycleId=cycle,VisitorId=visitor,TimeZoneId=clock.TimeZone.Id,AppliedUtc=EffectiveUtc(next).ToString("O")};
            }
            var decision=pendingDecision;
            next.LastCycleId=cycle;
            next.DailyDecisions.Add(decision.Copy());
            if(decision.VisitorId.Length>0 && !next.DiscoveredAnimalIds.Contains(decision.VisitorId))next.DiscoveredAnimalIds.Add(decision.VisitorId);
        }

        public bool AdvancePreviewDay(IEnumerable<string> eligibleAnimalIds = null)
        {
            lock(gate)
            {
                RequireOpen();
                var candidates=eligibleAnimalIds==null?cycleCandidates:NormalizeCycleCandidates(eligibleAnimalIds);
                var previousPending=pendingDecision;
                bool saved=Change(next=>
                {
                    if(next.PreviewDayOffset>=36500)throw new InvalidOperationException("앱 날짜를 더 넘길 수 없어요.");
                    next.PreviewDayOffset++;
                    AdvanceCycle(next,candidates);
                });
                if(saved){cycleCandidates=candidates;pendingDecision=null;}
                else pendingDecision=previousPending;
                return saved;
            }
        }

        public bool ResetMissionHistory() => Change(next=>
        {
            if(next.PlantAppearance!=null)
            {
                next.PlantAppearance.ArchivedCredits.AddRange(next.PlantAppearance.Credits);
                next.PlantAppearance.Credits.Clear();
            }
            next.Sessions.Clear();next.PracticePreferences.Clear();next.Experiences.Clear();
            next.CompletedCourseIds.Clear();next.NextCourseOrder=1;
            next.MbctCompletedIds.Clear();next.MbctNextOrder=1;next.MbctPrograms.Clear();
            next.LegacyCompletedUnknownDate.Clear();next.RewardReceipts.Clear();
            foreach(var growth in next.GrowthHistory)growth.LinkedSessionId="";
        });
        static void Id(string id, string prefix) { if (!StateCodec.Typed(id,prefix)) throw new ArgumentException("Expected namespaced " + prefix + " id."); }
        public bool SelectMbctProgram(string program)
        {
            return Change(next => {
                if(!MbctPolicy.IsProgram(program))throw new ArgumentException("Unknown course program.");
                if(next.Sessions.Any(s=>s.Status==ParticipationState.Selected || s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused))
                    throw new InvalidOperationException("진행 중인 활동을 마치거나 종료한 뒤 기간을 바꿀 수 있어요.");
                next.MbctProgramId=program;next.MbctProgramChosen=true;next.MbctProgramSelectionVersion=1;
            });
        }
        public bool BeginSession(string sessionId, string missionId, int courseOrder = 0, RecommendationContext recommendation=null)
        {
            return Change(next =>
            {
                if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Contains("|")) throw new ArgumentException("sessionId required.");
                StateCodec.ValidateRecommendation(recommendation);
                var existing=next.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
                if(existing!=null)
                {
                    if(existing.MissionId!=missionId || existing.CourseOrder!=courseOrder) throw new InvalidOperationException("Session id already belongs to another activity.");
                    if(recommendation!=null && (existing.Recommendation==null || existing.Recommendation.DecisionId!=recommendation.DecisionId || existing.Recommendation.PolicyId!=recommendation.PolicyId
                        || existing.Recommendation.AppVersion!=recommendation.AppVersion || existing.Recommendation.UtcOffsetMinutes!=recommendation.UtcOffsetMinutes || !existing.Recommendation.Features.SequenceEqual(recommendation.Features)))
                        throw new InvalidOperationException("Session attribution cannot be changed by retry.");
                    return;
                }
                if(next.Sessions.Any(s=>s.Status==ParticipationState.Selected || s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused)) throw new InvalidOperationException("An activity is already in progress.");
                if (!(StateCodec.Typed(missionId,"course") || StateCodec.Typed(missionId,"free-mission"))) throw new ArgumentException("Mission id must be typed.");
                bool mbct=MbctPolicy.IsMbct(missionId);
                string program=MbctPolicy.ProgramForMission(missionId);
                int nextOrder=mbct?MbctPolicy.NextOrder(next,program):next.NextCourseOrder;
                if(mbct && program!=MbctPolicy.ActiveProgram(next) && courseOrder>=nextOrder)throw new InvalidOperationException("Choose this course before starting a new practice.");
                if(mbct && missionId!=MbctPolicy.MissionId(program,courseOrder))throw new ArgumentException("MBCT mission/order mismatch.");
                if (courseOrder < 0 || courseOrder > (mbct?MbctPolicy.PracticeCount(program):28) || (courseOrder > 0 && !missionId.StartsWith("course:")) || (missionId.StartsWith("course:") && courseOrder==0)) throw new ArgumentException("Invalid course order.");
                if(courseOrder>nextOrder) throw new InvalidOperationException("Future course step is not yet available.");
                if(next.Sessions.Any(s=>s.MissionId==missionId && s.CourseOrder!=courseOrder)) throw new InvalidOperationException("Mission course order changed without migration.");
                if (next.LastCycleId.Length == 0) throw new InvalidOperationException("Refresh daily cycle first.");
                if(mbct && courseOrder==nextOrder)
                {
                    var unavailable=MbctPolicy.Availability(next,program);
                    if(unavailable!=null)throw new InvalidOperationException(unavailable);
                }
                if(mbct)next.MbctProgramChosen=true;
                next.Sessions.Add(new ActivitySession { SessionId = sessionId, MissionId = missionId, InstanceId = next.LastCycleId, SelectedUtc = Now, Status = ParticipationState.Selected, CourseOrder = courseOrder, Recommendation=recommendation?.Copy() });
            });
        }
        public bool BeginFocusSession(string sessionId,string missionId,int order=0,bool review=false)
        {
            lock(gate)
            {
                bool saved=Change(next=>
                {
                    if(string.IsNullOrWhiteSpace(sessionId) || sessionId.Contains("|"))throw new ArgumentException("Session id required.");
                    var definition=FocusMissionCatalog.Find(missionId)??throw new ArgumentException("Unknown active mission.");
                    var existing=next.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
                    bool basic=definition.Role==FocusMissionCatalog.BasicRole;
                    int occurrence=basic?(order>0?order:existing?.FocusOrder??(review?FocusMissionPolicy.Completed(next).Where(s=>s.MissionId==definition.Id).Select(s=>s.FocusOrder).DefaultIfEmpty(0).Max():FocusMissionPolicy.NextOrder(next))):0;
                    if(existing!=null)
                    {
                        if(!FocusMissionPolicy.IsFocus(existing) || existing.MissionId!=definition.Id || existing.FocusOrder!=occurrence || existing.FocusReview!=review)throw new InvalidOperationException("Session belongs to a different mission occurrence.");
                        return;
                    }
                    if(order<0 || (!basic && order!=0))throw new ArgumentException("Invalid mission occurrence.");
                    AdvanceCycle(next,cycleCandidates);
                    if(FocusMissionPolicy.Active(next,definition.Role)!=null)throw new InvalidOperationException(basic?"진행하던 기본 미션을 이어서 해주세요.":"진행하던 추가 미션을 이어서 해주세요.");
                    if(basic)
                    {
                        if(FocusMissionPolicy.MissionAt(occurrence)?.Id!=definition.Id)throw new InvalidOperationException("기본 미션 순서를 확인해 주세요.");
                        if(review)
                        {
                            if(occurrence>=FocusMissionPolicy.NextOrder(next))throw new InvalidOperationException("완료한 미션만 복습할 수 있어요.");
                        }
                        else
                        {
                            if(occurrence!=FocusMissionPolicy.NextOrder(next))throw new InvalidOperationException("현재 순서의 기본 미션부터 시작해 주세요.");
                            var unavailable=FocusMissionPolicy.Availability(next);if(unavailable!=null)throw new InvalidOperationException(unavailable);
                        }
                    }
                    else if(review && !next.Sessions.Any(s=>FocusMissionPolicy.IsFocus(s) && s.MissionId==definition.Id && s.Status==ParticipationState.RewardCommitted))throw new InvalidOperationException("완료한 미션만 복습할 수 있어요.");
                    next.Sessions.Add(new ActivitySession {SessionId=sessionId,MissionId=definition.Id,InstanceId=next.LastCycleId,SelectedUtc=Now,Status=ParticipationState.Selected,
                        FocusVersion=FocusMissionCatalog.Version,FocusRole=definition.Role,FocusOrder=occurrence,FocusReview=review,
                        MissionTitleSnapshot=definition.Title,MissionDescriptionSnapshot=definition.Overview,
                        FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord {Step=index,Key=step.Key,Title=step.Title,Prompt=step.Body}).ToList()});
                });
                if(saved && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return saved;
            }
        }
        static ActivitySession FocusActiveSession(GardenState next,string id)
        {
            var session=Session(next,id);
            if(!FocusMissionPolicy.IsFocus(session) || (session.Status!=ParticipationState.InProgress && session.Status!=ParticipationState.Paused))throw new InvalidOperationException("진행 중인 미션을 확인해 주세요.");
            return session;
        }
        void UpdateFocusAnswer(ActivitySession session,int step,string answer,bool skipped)
        {
            var definition=FocusMissionCatalog.Find(session.MissionId);
            if(step<0 || step>=definition.Steps.Length)throw new ArgumentException("Invalid focus step.");
            var instruction=definition.Steps[step];string value=skipped?"":answer??"";
            if(value.Length>500)throw new ArgumentException("기록은 500자까지 입력할 수 있어요.");
            if(!instruction.OptionalInput && value.Length>0 && !instruction.Options.Contains(value))throw new ArgumentException("제공된 선택지 중에서 골라주세요.");
            var record=session.FocusStepRecords[step];record.Value=value;
            record.Skipped=(instruction.OptionalInput || instruction.Options.Length>0) && (skipped || value.Length==0);
            record.UpdatedUtc=SessionTime(session.StartedUtc);
            if(value.Length==0)session.Answers.RemoveAll(a=>a.Key==record.Key);else SetAnswer(session.Answers,record.Key,value);
        }
        public bool SaveFocusDraft(string id,int step,string answer=null,bool skipped=false) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(step!=session.InstructionStep)throw new InvalidOperationException("현재 화면의 입력만 저장할 수 있어요.");
            UpdateFocusAnswer(session,step,answer,skipped);
        });
        public bool ConfirmFocusStep(string id,int step,string answer=null,bool skipped=false) => Change(next=>
        {
            var session=FocusActiveSession(next,id);int count=session.FocusStepRecords.Count;
            if(step<0 || step>=count)throw new ArgumentException("Invalid focus step.");
            var record=session.FocusStepRecords[step];
            // A duplicate tap cannot update an already-advanced response or award anything twice.
            if(step<session.InstructionStep && !string.IsNullOrEmpty(record.ConfirmedUtc))return;
            if(step!=session.InstructionStep || session.FocusStepRecords.Take(step).Any(r=>string.IsNullOrEmpty(r.ConfirmedUtc)))throw new InvalidOperationException("앞선 안내를 먼저 확인해 주세요.");
            UpdateFocusAnswer(session,step,answer,skipped);
            if(string.IsNullOrEmpty(record.ConfirmedUtc))
            {
                var previous=DateTimeOffset.Parse(step==0?session.StartedUtc:session.FocusStepRecords[step-1].ConfirmedUtc);
                var confirmed=CurrentUtc;if(confirmed<=previous)confirmed=previous.AddTicks(1);
                record.ConfirmedUtc=confirmed.ToString("O");record.UpdatedUtc=record.ConfirmedUtc;
            }
            session.InstructionStep=step+1;
        });
        public bool MoveFocusStep(string id,int step) => Change(next=>
        {
            var session=FocusActiveSession(next,id);int reached=session.FocusStepRecords.Count(r=>!string.IsNullOrEmpty(r.ConfirmedUtc));
            int maximum=reached==session.FocusStepRecords.Count?reached+1:reached;
            if(step<0 || step>maximum)throw new InvalidOperationException("아직 수행하지 않은 단계를 건너뛸 수 없어요.");
            session.InstructionStep=step;
        });
        public bool SaveFocusReflection(string id,string mood,string note,int stage) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(stage<0 || stage>1 || session.FocusStepRecords.Any(r=>string.IsNullOrEmpty(r.ConfirmedUtc)))throw new InvalidOperationException("실습을 마친 뒤 기록할 수 있어요.");
            if((mood!=null && mood.Length>80) || (note!=null && note.Length>500))throw new ArgumentException("기록 길이를 확인해 주세요.");
            session.Mood=mood;session.Note=note;session.InstructionStep=session.FocusStepRecords.Count+stage;
        });
        public bool CompleteFocusSession(string id,string mood=null,string note=null)
        {
            lock(gate)
            {
                bool saved=Change(next=>
                {
                    var session=Session(next,id);
                    if(!FocusMissionPolicy.IsFocus(session))throw new InvalidOperationException("Unknown focus mission session.");
                    if(session.Status==ParticipationState.RewardCommitted)return;
                    FocusActiveSession(next,id);
                    if(session.FocusStepRecords.Any(r=>string.IsNullOrEmpty(r.ConfirmedUtc)))throw new InvalidOperationException("모든 실습 단계를 확인한 뒤 완료해 주세요.");
                    if((mood!=null && mood.Length>80) || (note!=null && note.Length>500))throw new ArgumentException("기록 길이를 확인해 주세요.");
                    AdvanceCycle(next,cycleCandidates);bool basic=session.FocusRole==FocusMissionCatalog.BasicRole;
                    if(basic && !session.FocusReview)
                    {
                        if(session.FocusOrder!=FocusMissionPolicy.NextOrder(next))throw new InvalidOperationException("기본 미션 진행 상태를 확인해 주세요.");
                        var unavailable=FocusMissionPolicy.Availability(next);if(unavailable!=null)throw new InvalidOperationException(unavailable);
                    }
                    session.InstanceId=next.LastCycleId;
                    string receipt=basic?"focus-v1|basic|"+session.FocusOrder:next.LastCycleId+"|focus-v1|additional|M03";
                    session.RewardAmount=0;
                    if(!session.FocusReview && !next.RewardReceipts.Contains(receipt))
                    {
                        bool legacyAdditionalPaid=!basic && next.RewardReceipts.Contains(next.LastCycleId+"|"+session.MissionId);
                        next.RewardReceipts.Add(receipt);session.RewardAmount=legacyAdditionalPaid?0:config.CompletionNutrient;next.Nutrient=checked(next.Nutrient+session.RewardAmount);
                    }
                    session.Mood=mood;session.Note=note;session.Status=ParticipationState.RewardCommitted;session.InstructionStep=session.FocusStepRecords.Count+1;
                    var last=DateTimeOffset.Parse(session.FocusStepRecords.Last().ConfirmedUtc);var ended=CurrentUtc;
                    session.EndedUtc=(ended<last?last:ended).ToString("O");
                    GardenPlantVisuals.Sync(next);
                });
                if(saved && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return saved;
            }
        }
        static ActivitySession Session(GardenState next, string id)
        { return next.Sessions.FirstOrDefault(s => s.SessionId == id) ?? throw new InvalidOperationException("Unknown session."); }
        public bool CancelSelection(string id)
        {
            return Change(next => {
                var selected=Session(next,id);
                if(selected.Status!=ParticipationState.Selected)throw new InvalidOperationException("Started activities must be ended, not discarded.");
                next.Sessions.Remove(selected);
            });
        }
        public bool StartSession(string id) { return Transition(id, ParticipationState.Selected, ParticipationState.InProgress); }
        public bool StartWalkingSession(string id, WalkingTarget target)
        {
            return Change(next => {
                var session=Session(next,id);
                if(session.MissionId!="free-mission:M15")throw new InvalidOperationException("Not a walking activity.");
                if(session.Status==ParticipationState.InProgress || session.Status==ParticipationState.Paused)return;
                if(session.Status!=ParticipationState.Selected)throw new InvalidOperationException("Walking activity already ended.");
                if(target==null)throw new ArgumentNullException("target");
                DateTimeOffset from,until;
                if(!DateTimeOffset.TryParse(target.QueryStartUtc,out from) || !DateTimeOffset.TryParse(target.QueryEndUtc,out until) ||
                    CurrentUtc<from || CurrentUtc>=until)throw new InvalidOperationException("Walking target must belong to today.");
                session.WalkingGoalSteps=target.GoalSteps;
                session.WalkingQueryStartUtc=target.QueryStartUtc;session.WalkingQueryEndUtc=target.QueryEndUtc;
                session.WalkingTargetBasis=target.Basis;session.WalkingSleepReduced=target.ReducedForSleep;
                session.Status=ParticipationState.InProgress;session.StartedUtc=SessionTime(session.SelectedUtc);
            });
        }
        // Only observed, completed Health Connect queries enter RealStepHighWater. Virtual steps
        // remain a separate developer tool and cannot complete a personalized activity.
        public bool TryCompleteWalkingGoal(string id, string mood = null, string note = null)
        {
            lock(gate)
            {
                RequireOpen();
                try
                {
                    var session=Session(state,id);
                    if(session.MissionId!="free-mission:M15")throw new InvalidOperationException("Not a walking activity.");
                    if(session.Status==ParticipationState.RewardCommitted)return true;
                    if(session.Status!=ParticipationState.InProgress && session.Status!=ParticipationState.Paused)return false;
                    if(session.RealStepHighWater<(session.WalkingGoalSteps>0?session.WalkingGoalSteps:300))return false;
                    return CompleteSession(id,mood,note);
                }
                catch(Exception ex){LastError=ex.Message;return false;}
            }
        }
        public bool PauseSession(string id) { return Transition(id, ParticipationState.InProgress, ParticipationState.Paused); }
        public bool ResumeSession(string id) { return Transition(id, ParticipationState.Paused, ParticipationState.InProgress); }
        bool Transition(string id, ParticipationState from, ParticipationState to)
        {
            return Change(next => { var s = Session(next, id); if (s.Status == to) return; if (s.Status != from) throw new InvalidOperationException("Invalid session transition."); s.Status = to; if (string.IsNullOrEmpty(s.StartedUtc)) s.StartedUtc = SessionTime(s.SelectedUtc); });
        }
        public bool EndParticipation(string id)
        {
            return Change(next => { var s = Session(next, id); if(s.Status==ParticipationState.Ended)return; if (s.Status == ParticipationState.RewardCommitted) throw new InvalidOperationException("Already completed."); s.Status = ParticipationState.Ended; s.EndedUtc = SessionTime(s.StartedUtc ?? s.SelectedUtc); });
        }
        public bool CompleteSession(string id, string mood = null, string note = null, IEnumerable<string> eligibleAnimalIds = null)
        {
            lock(gate)
            {
                if(state!=null && FocusMissionPolicy.IsFocus(state.Sessions.FirstOrDefault(s=>s.SessionId==id)))return CompleteFocusSession(id,mood,note);
                bool completed=Change(next =>
                {
                    var s = Session(next, id); if (s.Status == ParticipationState.RewardCommitted) return;
                    if (s.Status != ParticipationState.InProgress && s.Status != ParticipationState.Paused) throw new InvalidOperationException("Start activity before completing.");
                    bool mbct=MbctPolicy.IsMbct(s.MissionId);
                    string program=MbctPolicy.ProgramForMission(s.MissionId);
                    bool advancing=mbct && s.CourseOrder==MbctPolicy.NextOrder(next,program);
                    if(advancing)
                    {
                        // A foreground completion can race the UI's periodic reset
                        // check. Advance the calendar in the same save as its quota,
                        // reward and progress, using only caller-supplied candidates.
                        AdvanceCycle(next,eligibleAnimalIds==null?cycleCandidates:NormalizeCycleCandidates(eligibleAnimalIds));
                        var unavailable=MbctPolicy.Availability(next,program);
                        if(unavailable!=null)throw new InvalidOperationException(unavailable);
                        // Charge daily/weekly quota to completion, including sessions resumed after midnight.
                        s.InstanceId=next.LastCycleId;
                    }
                    string receipt = s.InstanceId + "|" + s.MissionId;
                    s.EndedUtc = SessionTime(s.StartedUtc ?? s.SelectedUtc); s.Mood = mood; s.Note = note; s.Status = ParticipationState.RewardCommitted;
                    if ((!mbct || advancing) && !next.RewardReceipts.Contains(receipt) && !next.LegacyCompletedUnknownDate.Contains(s.MissionId))
                    { next.RewardReceipts.Add(receipt); next.Nutrient = checked(next.Nutrient + config.CompletionNutrient); }
                    if(advancing) MbctPolicy.Advance(next,s.MissionId);
                    if(mbct) foreach(var answer in s.Answers) SetAnswer(next.PracticePreferences,answer.Key,answer.Value);
                    if (!mbct && s.CourseOrder == next.NextCourseOrder && s.CourseOrder > 0 && !next.CompletedCourseIds.Contains(s.MissionId))
                    { next.CompletedCourseIds.Add(s.MissionId); next.NextCourseOrder = Math.Min(29, next.NextCourseOrder + 1); }
                    GardenPlantVisuals.Sync(next);
                });
                if(completed && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return completed;
            }
        }
        static void SetAnswer(List<PracticeAnswer> answers,string key,string value)
        {
            if(string.IsNullOrWhiteSpace(key) || key.Length>80 || key.Contains("|"))throw new ArgumentException("Invalid answer key.");
            if(value==null || value.Length>500)throw new ArgumentException("Answer too long.");
            var answer=answers.FirstOrDefault(a=>a.Key==key);
            if(answer==null)answers.Add(new PracticeAnswer{Key=key,Value=value});else answer.Value=value;
        }
        public bool SavePracticeStep(string id,int step,string key=null,string value=null)
        {
            return Change(next=>
            {
                var session=Session(next,id);
                if(!MbctPolicy.IsMbct(session.MissionId) || session.Status!=ParticipationState.InProgress)throw new InvalidOperationException("Practice is not active.");
                if(step<0 || step>20)throw new ArgumentException("Invalid instruction step.");
                if(key!=null)SetAnswer(session.Answers,key,value??"");
                session.InstructionStep=step;
            });
        }
        public bool SkipPracticeStep(string id,int nextStep,string key)
        {
            return Change(next => {
                var session=Session(next,id);
                if(!MbctPolicy.IsMbct(session.MissionId) || session.Status!=ParticipationState.InProgress)
                    throw new InvalidOperationException("Practice is not active.");
                if(nextStep<0 || nextStep>20)throw new ArgumentException("Invalid instruction step.");
                if(string.IsNullOrWhiteSpace(key) || key.Length>80 || key.Contains("|"))throw new ArgumentException("Invalid answer key.");
                session.Answers.RemoveAll(answer=>answer.Key==key);
                session.InstructionStep=nextStep;
                // Previous completed practices' preferences remain available for later use.
            });
        }
        public bool SavePreference(string key,string value) => Change(next=>SetAnswer(next.PracticePreferences,key,value??""));
        public bool SaveExperience(string id,string type,string eventText,string body,string emotion,string thought)
        {
            return Change(next=>
            {
                if(next.Experiences.Any(e=>e.Id==id))return;
                if(string.IsNullOrWhiteSpace(id) || id.Contains("|"))throw new ArgumentException("Invalid record id.");
                if(new[]{eventText,body,emotion,thought}.Any(v=>v!=null && v.Length>500))throw new ArgumentException("Record too long.");
                next.Experiences.Add(new ExperienceRecord{Id=id,CreatedUtc=Now,Type=type,Event=eventText??"",Body=body??"",Emotion=emotion??"",Thought=thought??""});
            });
        }
        public bool EnableDebugWalk()
        {
            return Change(next =>
            {
                if(next.DebugWalkPeriods.Any(p=>string.IsNullOrEmpty(p.EndedUtc)))return;
                next.DebugWalkPeriods.Add(new DebugWalkPeriod { Id=Guid.NewGuid().ToString("N"), StartedUtc=Now });
            });
        }
        public bool DisableDebugWalk()
        {
            return Change(next =>
            {
                var period=next.DebugWalkPeriods.LastOrDefault(p=>string.IsNullOrEmpty(p.EndedUtc));
                if(period!=null)period.EndedUtc=SessionTime(period.StartedUtc);
            });
        }
        public bool AddVirtualStep()
        {
            return Change(next =>
            {
                var period=next.DebugWalkPeriods.LastOrDefault(p=>string.IsNullOrEmpty(p.EndedUtc));
                if(period==null)throw new InvalidOperationException("Enable debug walk first.");
                period.VirtualSteps=checked(period.VirtualSteps+1);
                var mission=next.Sessions.LastOrDefault(s=>s.MissionId=="free-mission:M15" &&
                    (s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused));
                if(mission!=null)mission.VirtualSteps=checked(mission.VirtualSteps+1);
                CreditDebugSteps(next,period);
            });
        }
        public bool ObserveDebugWalk(string periodId,long actualSteps,string source="legacy")
        {
            return Change(next =>
            {
                if(actualSteps<0)throw new ArgumentOutOfRangeException("actualSteps");
                if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Step source is required.");
                var period=next.DebugWalkPeriods.FirstOrDefault(p=>p.Id==periodId);
                if(period==null)throw new InvalidOperationException("Unknown debug walk period.");
                // The coordinator may finish a Health Connect read after OFF. It must query
                // exactly [StartedUtc, EndedUtc], so this late result cannot include OFF-time steps.
                // A new preferred source replaces a former all-source total. Previously
                // credited units remain a ledger high-water; they are never paid again.
                period.RealStepHighWater=period.StepSource==source
                    ? Math.Max(period.RealStepHighWater,actualSteps) : actualSteps;
                period.StepSource=source;
                CreditDebugSteps(next,period);
            });
        }
        static void CreditDebugSteps(GardenState next,DebugWalkPeriod period)
        {
            long earned=checked(period.RealStepHighWater+period.VirtualSteps)/10;
            long additional=earned-period.RewardedUnits;
            if(additional<=0)return;
            next.Nutrient=checked(next.Nutrient+checked((int)additional));
            period.RewardedUnits=earned;
        }
        public bool ObserveMissionSteps(string id,long actualSteps,string source="legacy")
        {
            return Change(next =>
            {
                if(actualSteps<0)throw new ArgumentOutOfRangeException("actualSteps");
                if(string.IsNullOrWhiteSpace(source))throw new ArgumentException("Step source is required.");
                var session=Session(next,id);
                if(session.MissionId!="free-mission:M15")throw new InvalidOperationException("Not M15.");
                session.RealStepHighWater=session.StepSource==source
                    ? Math.Max(session.RealStepHighWater,actualSteps) : actualSteps;
                session.StepSource=source;
            });
        }
        public bool MarkMissionGoalNotified(string id)
        {
            lock(gate)
            {
                RequireOpen();
                try
                {
                    var current=Session(state,id);
                    if(current.MissionId!="free-mission:M15")throw new InvalidOperationException("Not M15.");
                    if(current.Status!=ParticipationState.InProgress && current.Status!=ParticipationState.Paused)throw new InvalidOperationException("M15 is not active.");
                    if(current.GoalNotificationCommitted)return false;
                    var next=StateCodec.Clone(state);
                    Session(next,id).GoalNotificationCommitted=true;
                    StateCodec.Validate(next);store.Save(next);state=next;LastError=null;return true;
                }
                catch(Exception ex){LastError=ex.Message;return false;}
            }
        }
        string SessionTime(string minimum) { DateTimeOffset prior; return DateTimeOffset.TryParse(minimum,out prior) && prior > CurrentUtc ? prior.ToString("O") : Now; }
        public void Dispose() { lock(gate) { if(disposed)return; disposed=true; var disposable = store as IDisposable; if (disposable != null) disposable.Dispose(); } }
        static string Slot(string slot) { return (slot ?? "").Trim().ToLowerInvariant(); }
        public bool CanPurchaseEnvironment(string environmentId, string slot)
        { slot = Slot(slot); var s = Snapshot; return !string.IsNullOrWhiteSpace(slot) && environmentId != null && environmentId.StartsWith("environment:") && s.Nutrient >= config.EnvironmentCost && !s.Environments.Any(p => p.Slot == slot || p.EnvironmentId == environmentId); }
        public bool PurchaseEnvironment(string requestId, string environmentId, string slot)
        {
            return Change(next =>
            {
                if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId required.");
                Id(environmentId, "environment"); slot = Slot(slot);
                string command="environment|"+environmentId+"|"+slot;
                if(PreviouslyPurchased(next,requestId,command)) return;
                if (string.IsNullOrWhiteSpace(slot) || next.Environments.Any(p => p.Slot == slot || p.EnvironmentId == environmentId)) throw new InvalidOperationException("Placement overlaps or has no slot.");
                if (next.Nutrient < config.EnvironmentCost) throw new InvalidOperationException("Insufficient nutrient.");
                next.Nutrient -= config.EnvironmentCost; next.Environments.Add(new EnvironmentPlacement { EnvironmentId = environmentId, Slot = slot }); next.PurchaseReceipts.Add(requestId); next.PurchaseCommands.Add(new PurchaseCommand { RequestId=requestId, Command=command });
            });
        }
        static bool PreviouslyPurchased(GardenState next,string requestId,string command)
        {
            if(!next.PurchaseReceipts.Contains(requestId))return false;
            var existing=next.PurchaseCommands.FirstOrDefault(c=>c.RequestId==requestId);
            if(existing==null || existing.Command!=command) throw new InvalidOperationException("Purchase request id has different or unavailable command identity.");
            return true;
        }
        public bool GrowPlant(string requestId, string plantId, string linkedSessionId=null)
        {
            return Change(next =>
            {
                if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId required.");
                Id(plantId, "plant"); string command="growth|"+plantId;
                string link=linkedSessionId??"";
                if(PreviouslyPurchased(next,requestId,command))
                {
                    var previous=next.GrowthHistory.FirstOrDefault(record=>record.RequestId==requestId);
                    if(previous!=null && previous.LinkedSessionId!=link)throw new InvalidOperationException("Purchase record cannot be relinked by retry.");
                    return;
                }
                if(link.Length>0 && !next.Sessions.Any(session=>session.SessionId==link &&
                    (session.Status==ParticipationState.RewardCommitted || session.Status==ParticipationState.Ended)))
                    throw new InvalidOperationException("Choose a completed or ended activity record.");
                if (next.Nutrient < config.GrowthCost) throw new InvalidOperationException("Insufficient nutrient.");
                float before=plantId==GardenPlantVisuals.GrowthPlantId?GardenPlantVisuals.TotalGrowth(next):next.Plants.FirstOrDefault(plant=>plant.PlantId==plantId)?.Growth??0;
                if (plantId == GardenPlantVisuals.GrowthPlantId)
                {
                    if (!GardenPlantVisuals.ApplyGrowth(next, config.GrowthAmount))
                        throw new InvalidOperationException("Plant has reached its visible growth limit.");
                }
                else
                {
                    var plant = next.Plants.FirstOrDefault(p => p.PlantId == plantId);
                    if (plant == null) { plant = new PlantProgress { PlantId = plantId }; next.Plants.Add(plant); }
                    plant.Growth += config.GrowthAmount;
                    GardenPlantVisuals.Sync(next);
                }
                next.Nutrient -= config.GrowthCost; next.PurchaseReceipts.Add(requestId); next.PurchaseCommands.Add(new PurchaseCommand { RequestId=requestId, Command=command });
                float after=plantId==GardenPlantVisuals.GrowthPlantId?GardenPlantVisuals.TotalGrowth(next):next.Plants.First(plant=>plant.PlantId==plantId).Growth;
                next.GrowthHistory.Add(new GardenGrowthRecord {RequestId=requestId,PlantId=plantId,CreatedUtc=Now,LinkedSessionId=link,BeforeGrowth=before,AfterGrowth=after});
            });
        }
    }
}
