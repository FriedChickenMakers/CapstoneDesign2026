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
    // Runtime/test options. New product prices and practice rewards are shared
    // through GardenEconomyPolicy; legacy fields remain for older integrations.
    public sealed class DemoBalanceConfig
    {
        public int ResetHourLocal = 4;
        public int CompletionNutrient = 5; // Legacy option; practice receipts use GardenEconomyPolicy.
        public int PhotoVerificationNutrient = GardenEconomyPolicy.PhotoBonusCoins, PhotoVerificationDailyLimit = 1;
        public int EnvironmentCost = 10; // Fallback for catalog entries not sold in the current shop.
        public int GrowthCost = 10;
        public float GrowthAmount = 0.1f;
        public int VisitorChancePercent = 50;
    }
    public sealed class LegacySnapshot
    {
        // The adapter distinguishes a genuinely new installation from an existing
        // legacy save whose balance/progress happen to be zero.
        public bool HasExistingState;
        public int Nutrient;
        public int GardenXp;
        public float Growth;
        public string LastUnlock = "";
        public List<string> CompletedMissionIds = new List<string>();
        public List<string> UnlockIds = new List<string>();
    }
    public sealed class PracticeAnswer { public string Key, Value, UpdatedUtc; internal PracticeAnswer Copy() => (PracticeAnswer)MemberwiseClone(); }
    public sealed class ExperienceRecord
    {
        internal ExperienceRecord Copy() => (ExperienceRecord)MemberwiseClone();
        public string Id, CreatedUtc, Type, Event, Body, Emotion, Thought;
    }
    public sealed class DailyEmotionRecord
    {
        public string Id, CycleId, BasicSessionId, AdditionalSessionId;
        public string UnlockedUtc, StartedUtc, UpdatedUtc, CompletedUtc;
        public string Mood="", Note="";
        public int Stage;
        // Version 0 keeps its historical growth award. Version 1 completes with
        // coins and one plant memo; old completed records are never re-awarded.
        public int RewardVersion, RewardAmount;
        public string MemoId="";
        [XmlIgnore] public bool IsCompleted => !string.IsNullOrEmpty(CompletedUtc);
        internal DailyEmotionRecord Copy() => (DailyEmotionRecord)MemberwiseClone();
    }
    public static class DailyEmotionPolicy
    {
        public const string MissionId="emotion:daily", RecordPrefix="daily-emotion:";
        public static string RecordId(string cycleId)=>RecordPrefix+cycleId;
        internal static string GrowthReceipt(string cycleId)=>"daily-emotion|"+cycleId;
        public static DailyEmotionRecord Current(GardenState state)=>state?.DailyEmotionRecords.FirstOrDefault(record=>record.CycleId==state.LastCycleId);
        public static bool IsExpired(GardenState state,DailyEmotionRecord record)=>record!=null && (state==null || record.CycleId!=state.LastCycleId);
        public static string Availability(GardenState state)
        {
            if(state==null)return "미션 상태를 먼저 불러와 주세요.";
            if(Current(state)?.IsCompleted==true)return "오늘의 감정 기록을 마쳤어요.";
            if(FocusMissionPolicy.CompletedPracticeForCycle(state,FocusMissionCatalog.BasicRole,state.LastCycleId)==null
                || FocusMissionPolicy.CompletedPracticeForCycle(state,FocusMissionCatalog.AdditionalRole,state.LastCycleId)==null)
                return "오늘의 기본 미션과 추가 미션을 마치면 감정을 기록할 수 있어요.";
            return null;
        }
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
            return FocusMissionPolicy.DailyAvailability(state,FocusMissionCatalog.BasicRole);
        }
    }
    public enum ParticipationState { Selected, InProgress, Paused, Ended, RewardCommitted }
    public sealed class FocusStepRecord
    {
        public string PlanActivity, PlanTimePeriod, PlanNote;
        public int Step;
        public string Key, Title, Prompt, Value = "", ConfirmedUtc, UpdatedUtc;
        // Additive fields: opening a mission is not evidence of starting its practice.
        public string PracticeStartedUtc, RecommendationPolicy, EndReason, ShortenedUtc;
        public int RecommendedSeconds;
        public bool Skipped;
        public int ElapsedSeconds;
        internal FocusStepRecord Copy() => (FocusStepRecord)MemberwiseClone();
    }
    public static class FocusMissionPolicy
    {
        // Keep the former example sequence addressable for saved version-1 records.
        public const int TotalPractices = 12, PracticesPerWeek = 6;
        public const string LegacyProgram = "legacy", AdditionalProgram = "additional";
        static readonly string[] LegacySequence = {"P23","P10","P25"};
        static readonly string[] TwoWeek = {"P04","P01","P03","P06","P22","P10","P02","P11","P17","P21","P27","P28"};
        static readonly string[] FourWeek = Enumerable.Range(1,28).Where(i=>i!=5 && i!=7 && i!=14 && i!=18).Select(i=>"P"+i.ToString("00")).ToArray();
        static readonly string[] EightWeek = Enumerable.Range(1,28).Select(i=>"P"+i.ToString("00")).Concat(new[]{"P01","P03","P02","P10","P23","P06","P05","P09","P11","P13","P16","P17","P18","P20","P21","P22","P24","P25","P27","P28"}).ToArray();
        public static bool IsFocus(ActivitySession session) => session!=null && session.FocusVersion>0 && session.FocusVersion<=FocusMissionCatalog.Version;
        public static bool IsActive(ActivitySession session) => session!=null && (session.Status==ParticipationState.Selected || session.Status==ParticipationState.InProgress || session.Status==ParticipationState.Paused);
        public static string ProgramFor(ActivitySession session) => !IsFocus(session)?null:session.FocusRole==FocusMissionCatalog.AdditionalRole?AdditionalProgram:session.FocusVersion==1?LegacyProgram:session.FocusProgramId;
        public static string[] CourseCodes(string program) => program=="2w"?(string[])TwoWeek.Clone():program=="4w"?(string[])FourWeek.Clone():program=="8w"?(string[])EightWeek.Clone():program==LegacyProgram?Enumerable.Range(0,12).Select(i=>LegacySequence[i%3]).ToArray():throw new ArgumentException("Unknown focus course.");
        public static int TotalPracticesFor(string program) => program==LegacyProgram?12:MbctPolicy.PracticeCount(program);
        public static FocusMissionDefinition MissionAt(int order,string program="2w")
        {
            var codes=CourseCodes(program);return order<1 || order>codes.Length?null:FocusMissionCatalog.Find(codes[order-1],program==LegacyProgram?1:FocusMissionCatalog.Version);
        }
        static string Program(GardenState state,string program) => program??state.MbctProgramId;
        public static ActivitySession Active(GardenState state,string role,string program=null) => state.Sessions.LastOrDefault(s=>IsFocus(s) && s.FocusRole==role && IsActive(s) && (role==FocusMissionCatalog.AdditionalRole || ProgramFor(s)==Program(state,program)));
        public static IEnumerable<ActivitySession> History(GardenState state,string missionId=null) => state.Sessions.Where(s=>IsFocus(s) && (missionId==null || s.MissionId==missionId || FocusMissionCatalog.Find(missionId)?.Id==s.MissionId)).OrderByDescending(s=>s.SelectedUtc,StringComparer.Ordinal);
        public static IEnumerable<ActivitySession> Completed(GardenState state,string program=null) => state.Sessions.Where(s=>IsFocus(s) && s.FocusRole==FocusMissionCatalog.BasicRole && !s.FocusReview && s.Status==ParticipationState.RewardCommitted && ProgramFor(s)==Program(state,program));
        public static int CompletedCount(GardenState state,string program=null) => Completed(state,program).Count();
        public static int NextOrder(GardenState state,string program=null) => CompletedCount(state,program)+1;
        public static bool AdditionalCompletedToday(GardenState state,string missionId=null) => state.Sessions.Any(s=>IsFocus(s) && s.FocusRole==FocusMissionCatalog.AdditionalRole && !s.FocusReview && s.Status==ParticipationState.RewardCommitted && s.InstanceId==state.LastCycleId && (missionId==null || s.MissionId==(FocusMissionCatalog.Find(missionId)?.Id??missionId)));
        internal static string RoleFor(ActivitySession session) => session==null?null:IsFocus(session)?session.FocusRole
            :session.MissionId!=null && session.MissionId.StartsWith("free-mission:",StringComparison.Ordinal)?FocusMissionCatalog.AdditionalRole
            :session.MissionId!=null && session.MissionId.StartsWith("course:",StringComparison.Ordinal)?FocusMissionCatalog.BasicRole:null;
        static Dictionary<string,ActivitySession> ClaimedPractices(GardenState state,string role)
        {
            var claims=new Dictionary<string,ActivitySession>(StringComparer.Ordinal);
            if(state==null)return claims;
            // All generations share the same daily allowance. Keep old receipt keys
            // so historical multiple-M records remain valid and are never rewritten.
            // A history reset deliberately removes sessions; its retained growth
            // ledger still prevents duplicate growth without reserving the new schedule.
            foreach(var record in state.PracticeGrowthRecords.Where(r=>r.Outcome==PracticeGrowthOutcome.Grown || r.Outcome==PracticeGrowthOutcome.Stored))
            {
                var session=state.Sessions.FirstOrDefault(s=>s.SessionId==record.SessionId);
                if(RoleFor(session)==role && (!IsFocus(session) || !session.FocusReview))claims[record.EligibilityKey]=session;
            }
            foreach(var session in state.Sessions.Where(s=>RoleFor(s)==role && s.Status==ParticipationState.RewardCommitted && (!IsFocus(s) || !s.FocusReview)))
            {
                string receipt=IsFocus(session)?RewardReceipt(session):session.InstanceId+"|"+session.MissionId;
                if(state.RewardReceipts.Contains(receipt) && !claims.ContainsKey(receipt))claims.Add(receipt,session);
            }
            return claims;
        }
        public static ActivitySession TodayPractice(GardenState state,string role) => ClaimedPractices(state,role).Values
            .Where(s=>s.InstanceId==state.LastCycleId).OrderByDescending(s=>s.StartedUtc??s.SelectedUtc,StringComparer.Ordinal).FirstOrDefault();
        public static bool PracticeClaimedToday(GardenState state,string role) => TodayPractice(state,role)!=null;
        internal static ActivitySession CompletedPracticeForCycle(GardenState state,string role,string cycle) => ClaimedPractices(state,role).Values
            .Where(s=>s.InstanceId==cycle && s.Status==ParticipationState.RewardCommitted)
            .OrderByDescending(s=>s.EndedUtc,StringComparer.Ordinal).FirstOrDefault();
        public static string AdditionalAvailability(GardenState state) => DailyAvailability(state,FocusMissionCatalog.AdditionalRole);
        internal static string DailyAvailability(GardenState state,string role)
        {
            var claims=ClaimedPractices(state,role).Values;
            if(claims.Any(s=>s.InstanceId==state.LastCycleId))return role==FocusMissionCatalog.BasicRole
                ?"오늘 기본 미션을 마쳤어요. 다음 기본 미션은 내일 열려요.":"오늘 추가 미션을 마쳤어요. 다음 추가 미션은 내일 열려요.";
            if(role==FocusMissionCatalog.BasicRole && state.LastCycleId.Length>0
                && claims.Count(s=>MbctPolicy.WeekOf(s.InstanceId)==MbctPolicy.WeekOf(state.LastCycleId))>=PracticesPerWeek)
                return "이번 주 기본 미션을 모두 마쳤어요. 다음 주에 이어가요.";
            return null;
        }
        public static bool HasPendingToday(GardenState state)
        {
            if(state==null)return false;
            return state.Sessions.Any(s=>IsFocus(s) && IsActive(s) && !s.FocusReview)
                || !state.MbctProgramChosen || Availability(state)==null
                || AdditionalAvailability(state)==null || DailyEmotionPolicy.Availability(state)==null;
        }
        public static string Availability(GardenState state,string program=null)
        {
            program=Program(state,program);
            if(program!=LegacyProgram && !state.MbctProgramChosen)return "먼저 나에게 맞는 코스를 선택해 주세요.";
            if(NextOrder(state,program)>TotalPracticesFor(program))return (program==LegacyProgram?"예시":MbctPolicy.ProgramWeeks(program)+"주")+" 과정을 마쳤어요. 기록 탭에서 실천을 돌아볼 수 있어요.";
            return DailyAvailability(state,FocusMissionCatalog.BasicRole);
        }
        internal static string RewardReceipt(ActivitySession session) => session.FocusVersion==1
            ?(session.FocusRole==FocusMissionCatalog.BasicRole?"focus-v1|basic|"+session.FocusOrder:session.InstanceId+"|focus-v1|additional|M03")
            :session.FocusRole==FocusMissionCatalog.BasicRole?"focus-v2|"+session.FocusProgramId+"|basic|"+session.FocusOrder:session.InstanceId+"|focus-v2|additional|"+session.MissionId;
    }
    public sealed class RecommendationContext
    {
        public int UtcOffsetMinutes;
        public string DecisionId, PolicyId, AppVersion;
        public float[] Features;
        internal RecommendationContext Copy() => new RecommendationContext {DecisionId=DecisionId,PolicyId=PolicyId,AppVersion=AppVersion,UtcOffsetMinutes=UtcOffsetMinutes,Features=Features==null?null:(float[])Features.Clone()};
    }
    // The recommendation actually visible before this selection; no free-text or health inputs.
    public sealed class FocusRecommendationExposure
    {
        public string DecisionId, MissionId, PolicyId, Reason, ShownUtc;
        internal FocusRecommendationExposure Copy() => (FocusRecommendationExposure)MemberwiseClone();
    }
    public sealed class ActivitySession
    {
        internal ActivitySession Copy() { var copy=(ActivitySession)MemberwiseClone(); copy.Answers=Answers?.Select(a=>a?.Copy()).ToList(); copy.Recommendation=Recommendation?.Copy(); copy.FocusStepRecords=FocusStepRecords?.Select(r=>r?.Copy()).ToList(); copy.FocusRecommendationExposure=FocusRecommendationExposure?.Copy();copy.Photo=Photo?.Copy();return copy; }
        public MissionPhoto Photo;
        public bool PhotoPromptHandled;
        public string PendingPhotoId, PendingPhotoTarget;
        public int PhotoBonusAmount;
        public RecommendationContext Recommendation;
        public FocusRecommendationExposure FocusRecommendationExposure;
        public string SessionId, MissionId, InstanceId, SelectedUtc, StartedUtc, EndedUtc;
        public string Mood, Note;
        // Optional schema-1 additions: old sessions remain FocusVersion=0 and keep their meaning.
        public int FocusVersion, FocusOrder, RewardAmount;
        public string FocusRole, FocusProgramId, MissionTitleSnapshot, MissionDescriptionSnapshot;
        public bool FocusReview, FocusSupport;
        public string FocusSourceSessionId, FocusSourceMissionId, FocusEndReason;
        public string FocusRecommendationId, FocusRecommendationPolicy, FocusRecommendationReason;
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
        // Optional on historical records; new current-plant growth records identify
        // the individual separately from the legacy catalog growth key.
        public string PlantInstanceId = "";
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
        public List<PhotoRewardReceipt> PhotoRewards=new List<PhotoRewardReceipt>();
        public long PrivacyRevision;
        internal GardenState Copy() => (GardenState)MemberwiseClone();
        public int SchemaVersion = 1;
        public bool MigrationCompleted;
        public string InstallationId = Guid.NewGuid().ToString("N");
        // Operational reset receipt only: no former user record or balance survives.
        // The source generation makes delayed reset commands unable to clear a
        // later garden; pending blocks normal writes until app-local cleanup ends.
        public string FullResetRequestId="", FullResetSourceInstallationId="";
        public bool FullResetPending;
        public int Nutrient, GardenXp;
        // Preserve the schema-1 wallet element while exposing its current product
        // name. This alias never creates a second balance or XML element.
        [XmlIgnore] public int Coins { get => Nutrient; set => Nutrient = value; }
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
        // Independent daily reflection; absent in older saves and never inferred from old Mood/Note values.
        public List<DailyEmotionRecord> DailyEmotionRecords = new List<DailyEmotionRecord>();
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
        public PlantCollectionState PlantCollection;
        // Optional for previous schema-1 saves. Albums never supply growth or rewards.
        public List<GardenPlantPhoto> PlantPhotos = new List<GardenPlantPhoto>();
        public List<GardenPlantPhotoCommand> PhotoSaveCommands = new List<GardenPlantPhotoCommand>();
        public List<GardenPlantStartCommand> PlantStartCommands = new List<GardenPlantStartCommand>();
        public List<PracticeGrowthRecord> PracticeGrowthRecords = new List<PracticeGrowthRecord>();
        public int StoredGrowthSteps;
        public List<string> DiscoveredAnimalIds = new List<string>();
    }
    public interface IStateStore { GardenState Load(); void Save(GardenState candidate); }
    public interface IFullResetStateStore
    {
        void BeginFullReset(GardenState candidate);
        void FinishFullReset(GardenState candidate);
    }
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
                CombinedPlants.TranslateLegacyFocus(state);
                Validate(state);
                return state;
            }
        }
        public static void Validate(GardenState s)
        {
            if (s == null || s.SchemaVersion < 1 || s.SchemaVersion > 3 || !s.MigrationCompleted || s.Nutrient < 0 || s.PrivacyRevision<0 || s.NextCourseOrder < 1 || s.NextCourseOrder > 29 || string.IsNullOrWhiteSpace(s.InstallationId) || !Finite(s.LegacyGrowth) || s.PreviewDayOffset < 0 || s.PreviewDayOffset > 36500)
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
            if(s.PracticePreferences.Any(a=>a==null || a.Value==null || (!string.IsNullOrEmpty(a.UpdatedUtc) && !Timestamp(a.UpdatedUtc))))throw new InvalidDataException("Invalid practice preferences.");
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
                ValidateFocusExposure(a);
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
                    || record.BeforeGrowth<0 || record.AfterGrowth<=record.BeforeGrowth || record.LinkedSessionId==null || record.PlantInstanceId==null
                    || !s.PurchaseCommands.Any(command=>command.RequestId==record.RequestId && command.Command=="growth|"+record.PlantId))
                    throw new InvalidDataException("Invalid growth history.");
                if(record.LinkedSessionId.Length>0 && !s.Sessions.Any(session=>session.SessionId==record.LinkedSessionId &&
                    (session.Status==ParticipationState.RewardCommitted || session.Status==ParticipationState.Ended)))
                    throw new InvalidDataException("Growth record references an unavailable activity.");
                if(record.PlantInstanceId.Length>0 && (record.PlantId!=GardenPlantVisuals.GrowthPlantId
                    || (s.PlantAppearance?.Recipe.InstanceId!=record.PlantInstanceId
                        && !s.PlantPhotos.Any(photo=>photo.SourcePlantInstanceId==record.PlantInstanceId))))
                    throw new InvalidDataException("Growth record references an unavailable plant individual.");
            }
            ValidatePlantMemos(s);
            ValidateDailyEmotions(s);
            ValidatePracticeGrowth(s);
            GardenPlantVisuals.Validate(s);
            ValidatePlantPhotos(s);
            WeeklyPlants.Validate(s);
            CameraMissionPolicy.Validate(s);
            ValidateFullReset(s);
        }
        internal static string ResetInstallationId(string requestId,string sourceId) => Hash("full-reset|"+sourceId+"|"+requestId);
        internal static string ResetPlantId(string installationId) => "garden-plant:reset-"+Hash(installationId+"|initial-seed");
        internal static bool IsFullResetRequestId(string value) => !string.IsNullOrEmpty(value) && value.Length<=128
            && value.All(c=>(c>='A' && c<='Z') || (c>='a' && c<='z') || (c>='0' && c<='9') || c=='-' || c=='_' || c==':');
        static void ValidateFullReset(GardenState state)
        {
            if(state.FullResetRequestId==null || state.FullResetSourceInstallationId==null)
                throw new InvalidDataException("Full reset receipt missing.");
            bool hasReset=state.FullResetRequestId.Length>0 || state.FullResetSourceInstallationId.Length>0;
            if(!hasReset)
            {
                if(state.FullResetPending)throw new InvalidDataException("Pending reset has no command identity.");
                return;
            }
            if(!IsFullResetRequestId(state.FullResetRequestId)
                || string.IsNullOrWhiteSpace(state.FullResetSourceInstallationId)
                || state.InstallationId!=ResetInstallationId(state.FullResetRequestId,state.FullResetSourceInstallationId))
                throw new InvalidDataException("Full reset generation mismatch.");
            if(!state.FullResetPending)return;
            if(state.Coins!=0 || state.GardenXp!=0 || state.LegacyGrowth!=0 || state.PreviewDayOffset!=0
                || state.PrivacyRevision!=0 || state.PhotoRewards.Count!=0
                || state.LastUnlock!="" || state.LastCycleId!="" || state.NextCourseOrder!=1 || state.MbctNextOrder!=1
                || state.MbctProgramId!="8w" || state.MbctProgramSelectionVersion!=1 || state.MbctProgramChosen
                || state.StoredGrowthSteps!=0 || state.LegacyCompletedUnknownDate.Count!=0 || state.UnlockIds.Count!=0
                || state.CompletedCourseIds.Count!=0 || state.RewardReceipts.Count!=0 || state.PurchaseReceipts.Count!=0
                || state.MbctPrograms.Count!=0 || state.MbctCompletedIds.Count!=0 || state.PracticePreferences.Count!=0
                || state.Experiences.Count!=0 || state.DailyEmotionRecords.Count!=0 || state.PurchaseCommands.Count!=0
                || state.GrowthHistory.Count!=0 || state.DailyDecisions.Count!=0 || state.Sessions.Count!=0
                || state.DebugWalkPeriods.Count!=0 || state.Environments.Count!=0 || state.Plants.Count!=0
                || state.PlantPhotos.Count!=0 || state.PhotoSaveCommands.Count!=0 || state.PlantStartCommands.Count!=0
                || state.PracticeGrowthRecords.Count!=0 || state.DiscoveredAnimalIds.Count!=0
                || state.PlantAppearance==null || state.PlantAppearance.Recipe.InstanceId!=ResetPlantId(state.InstallationId)
                || state.PlantAppearance.Recipe.GrowthStep!=0 || state.PlantAppearance.IsCompleted || state.PlantAppearance.IsArchived
                || state.PlantAppearance.Memos.Count!=0 || state.PlantAppearance.Credits.Count!=0
                || state.PlantAppearance.ArchivedCredits.Count!=0 || state.PlantAppearance.PreviousActivityKeys.Count!=0)
                throw new InvalidDataException("A pending full reset must remain an untouched initial garden.");
            if(state.SchemaVersion==3 && (state.PlantCollection.Plants.Count!=1||state.PlantCollection.Decisions.Count!=0||state.PlantCollection.ParticipationDecisions.Count!=0||state.PlantCollection.Plants[0].Memories.Count!=0))
                throw new InvalidDataException("A pending full reset retains old plant records.");
        }
        static void ValidateDailyEmotions(GardenState state)
        {
            if(state.DailyEmotionRecords==null || state.DailyEmotionRecords.Any(record=>record==null))throw new InvalidDataException("Daily emotion records missing.");
            Unique(state.DailyEmotionRecords.Select(record=>record.Id));Unique(state.DailyEmotionRecords.Select(record=>record.CycleId));
            foreach(var record in state.DailyEmotionRecords)
            {
                if(!Cycle(record.CycleId) || record.Id!=DailyEmotionPolicy.RecordId(record.CycleId)
                    || !state.DailyDecisions.Any(day=>day.CycleId==record.CycleId) || state.Sessions.Any(session=>session.SessionId==record.Id)
                    || !Timestamp(record.UnlockedUtc) || !Timestamp(record.StartedUtc) || !Timestamp(record.UpdatedUtc)
                    || DateTimeOffset.Parse(record.StartedUtc)<DateTimeOffset.Parse(record.UnlockedUtc)
                    || DateTimeOffset.Parse(record.UpdatedUtc)<DateTimeOffset.Parse(record.StartedUtc)
                    || record.Stage<0 || record.Stage>1 || record.Mood==null || record.Mood.Length>80 || record.Note==null || record.Note.Length>500
                    || record.RewardVersion<0 || record.RewardVersion>1 || record.RewardAmount<0 || record.MemoId==null
                    || (!record.IsCompleted && (record.RewardVersion!=0 || record.RewardAmount!=0 || record.MemoId.Length!=0)))
                    throw new InvalidDataException("Invalid daily emotion draft.");
                foreach(var role in new[]{FocusMissionCatalog.BasicRole,FocusMissionCatalog.AdditionalRole})
                {
                    string sourceId=role==FocusMissionCatalog.BasicRole?record.BasicSessionId:record.AdditionalSessionId;
                    var source=state.Sessions.FirstOrDefault(session=>session.SessionId==sourceId);
                    if(source==null || FocusMissionPolicy.RoleFor(source)!=role || source.InstanceId!=record.CycleId
                        || source.Status!=ParticipationState.RewardCommitted || (FocusMissionPolicy.IsFocus(source) && source.FocusReview)
                        || !state.RewardReceipts.Contains(FocusMissionPolicy.IsFocus(source)?FocusMissionPolicy.RewardReceipt(source):source.InstanceId+"|"+source.MissionId)
                        || DateTimeOffset.Parse(source.EndedUtc)>DateTimeOffset.Parse(record.UnlockedUtc))
                        throw new InvalidDataException("Daily emotion prerequisite mismatch.");
                }
                if(record.IsCompleted)
                {
                    if(!Timestamp(record.CompletedUtc) || DateTimeOffset.Parse(record.CompletedUtc)<DateTimeOffset.Parse(record.UpdatedUtc)
                        || record.Stage!=1 || (string.IsNullOrWhiteSpace(record.Mood) && string.IsNullOrWhiteSpace(record.Note)))
                        throw new InvalidDataException("Invalid completed daily emotion.");
                    if(record.RewardVersion==0)
                    {
                        if(record.RewardAmount!=0 || record.MemoId.Length!=0 || state.PracticeGrowthRecords==null
                            || !state.PracticeGrowthRecords.Any(growth=>growth.SessionId==record.Id
                                && growth.MissionId==DailyEmotionPolicy.MissionId && growth.CycleId==record.CycleId))
                            throw new InvalidDataException("Historical daily emotion has no matching growth receipt.");
                    }
                    else
                    {
                        if(!state.RewardReceipts.Contains(DailyEmotionPolicy.GrowthReceipt(record.CycleId))
                            || (record.RewardAmount!=0 && record.RewardAmount!=100)
                            || (record.RewardAmount==0 && record.MemoId.Length!=0))
                            throw new InvalidDataException("Daily emotion coin receipt mismatch.");
                        if(record.RewardAmount==100 && !AllPlantMemos(state).Any(memo=>memo.Id==record.MemoId
                            && memo.DailyEmotionRecordId==record.Id && memo.Mood==record.Mood && memo.Note==record.Note
                            && memo.CreatedUtc==record.CompletedUtc))
                            throw new InvalidDataException("Daily emotion memo receipt mismatch.");
                        if(record.RewardAmount==100)
                        {
                            var basic=state.Sessions.First(session=>session.SessionId==record.BasicSessionId);
                            var additional=state.Sessions.First(session=>session.SessionId==record.AdditionalSessionId);
                            foreach(var memo in AllPlantMemos(state).Where(item=>item.Id==record.MemoId))
                                if((memo.BasicSessionId.Length>0 && (memo.BasicSessionId!=basic.SessionId || memo.BasicMissionId!=basic.MissionId
                                        || memo.BasicMissionTitle!=(basic.MissionTitleSnapshot??"")))
                                    || (memo.AdditionalSessionId.Length>0 && (memo.AdditionalSessionId!=additional.SessionId || memo.AdditionalMissionId!=additional.MissionId
                                        || memo.AdditionalMissionTitle!=(additional.MissionTitleSnapshot??""))))
                                    throw new InvalidDataException("Plant memo mission snapshot disagrees with its original record.");
                        }
                    }
                }
            }
        }
        static IEnumerable<GardenPlantMemo> AllPlantMemos(GardenState state) =>
            (state.PlantAppearance?.Memos ?? new List<GardenPlantMemo>()).Concat(state.PlantPhotos.SelectMany(photo=>photo.Memos));
        static void ValidatePlantMemos(GardenState state)
        {
            if(state.PlantPhotos==null || state.PlantPhotos.Any(photo=>photo==null))throw new InvalidDataException("Plant photos missing.");
            var owners=new List<KeyValuePair<string,List<GardenPlantMemo>>>();
            if(state.PlantAppearance!=null)owners.Add(new KeyValuePair<string,List<GardenPlantMemo>>(state.PlantAppearance.Recipe?.InstanceId,state.PlantAppearance.Memos));
            owners.AddRange(state.PlantPhotos.Select(photo=>new KeyValuePair<string,List<GardenPlantMemo>>(photo.SourcePlantInstanceId,photo.Memos)));
            var identities=new Dictionary<string,GardenPlantMemo>(StringComparer.Ordinal);
            foreach(var owner in owners)
            {
                if(owner.Value==null || owner.Value.Any(memo=>memo==null))throw new InvalidDataException("Plant memo list missing.");
                Unique(owner.Value.Select(memo=>memo.Id));Unique(owner.Value.Select(memo=>memo.DailyEmotionRecordId));
                foreach(var memo in owner.Value)
                {
                    string cycle=memo.DailyEmotionRecordId!=null && memo.DailyEmotionRecordId.StartsWith(DailyEmotionPolicy.RecordPrefix,StringComparison.Ordinal)
                        ?memo.DailyEmotionRecordId.Substring(DailyEmotionPolicy.RecordPrefix.Length):"";
                    if(!Cycle(cycle) || memo.Id!="plant-memo:"+Hash(memo.DailyEmotionRecordId)
                        || string.IsNullOrWhiteSpace(memo.PlantInstanceId) || memo.PlantInstanceId!=owner.Key || !Timestamp(memo.CreatedUtc)
                        || memo.Mood==null || memo.Mood.Length>80 || memo.Note==null || memo.Note.Length>500
                        || (string.IsNullOrWhiteSpace(memo.Mood) && string.IsNullOrWhiteSpace(memo.Note))
                        || !state.RewardReceipts.Contains(DailyEmotionPolicy.GrowthReceipt(cycle)))
                        throw new InvalidDataException("Invalid plant memo ownership or completion.");
                    ValidateMemoMission(memo.BasicSessionId,memo.BasicMissionId,memo.BasicMissionTitle,"course");
                    ValidateMemoMission(memo.AdditionalSessionId,memo.AdditionalMissionId,memo.AdditionalMissionTitle,"free-mission");
                    GardenPlantMemo prior;
                    if(identities.TryGetValue(memo.Id,out prior))
                    {
                        // The archived current appearance retains its first photo's
                        // exact memo snapshot until the next individual starts.
                        if(prior.PlantInstanceId!=memo.PlantInstanceId || prior.DailyEmotionRecordId!=memo.DailyEmotionRecordId
                            || prior.CreatedUtc!=memo.CreatedUtc || prior.Mood!=memo.Mood || prior.Note!=memo.Note
                            || prior.BasicSessionId!=memo.BasicSessionId || prior.BasicMissionId!=memo.BasicMissionId
                            || prior.BasicMissionTitle!=memo.BasicMissionTitle || prior.AdditionalSessionId!=memo.AdditionalSessionId
                            || prior.AdditionalMissionId!=memo.AdditionalMissionId || prior.AdditionalMissionTitle!=memo.AdditionalMissionTitle)
                            throw new InvalidDataException("A plant memo was copied to another individual or changed after capture.");
                    }
                    else identities.Add(memo.Id,memo);
                }
            }
            if(state.PlantAppearance?.IsArchived==true)
            {
                var photo=state.PlantPhotos.FirstOrDefault(item=>item.SourcePlantInstanceId==state.PlantAppearance.Recipe?.InstanceId);
                if(photo!=null && !state.PlantAppearance.Memos.Select(memo=>memo.Id).SequenceEqual(photo.Memos.Select(memo=>memo.Id)))
                    throw new InvalidDataException("Archived plant memos disagree with its frozen photo.");
            }
        }
        static void ValidateMemoMission(string sessionId,string missionId,string title,string prefix)
        {
            if(sessionId==null || missionId==null || title==null
                || (sessionId.Length==0 ? missionId.Length!=0 || title.Length!=0
                    : string.IsNullOrWhiteSpace(sessionId) || !Typed(missionId,prefix)))
                throw new InvalidDataException("Invalid plant memo mission snapshot.");
            // A legacy session may have no stored title; retain its exact ID without
            // substituting a current catalog title for historical evidence.
        }
        static void ValidatePracticeGrowth(GardenState state)
        {
            if (state.PracticeGrowthRecords == null || state.PracticeGrowthRecords.Any(record => record == null))
                throw new InvalidDataException("Practice growth records missing.");
            Unique(state.PracticeGrowthRecords.Select(record => record.SessionId));
            Unique(state.PracticeGrowthRecords.Where(record => record.Outcome == PracticeGrowthOutcome.Grown || record.Outcome == PracticeGrowthOutcome.Stored)
                .Select(record => record.EligibilityKey));
            if (state.StoredGrowthSteps < 0 || state.PlantStartCommands == null
                || (long)state.StoredGrowthSteps != state.PracticeGrowthRecords.LongCount(record => record.Outcome == PracticeGrowthOutcome.Stored)
                    - state.PlantStartCommands.Sum(command => (long)command.AppliedGrowthSteps))
                throw new InvalidDataException("Stored practice growth disagrees with its credits and uses.");
            foreach (var record in state.PracticeGrowthRecords)
            {
                if (string.IsNullOrWhiteSpace(record.EligibilityKey) || !Cycle(record.CycleId) || !Timestamp(record.CommittedUtc)
                    || !(Typed(record.MissionId,"course") || Typed(record.MissionId,"free-mission") || record.MissionId==DailyEmotionPolicy.MissionId) || record.PlantInstanceId == null
                    || !Enum.IsDefined(typeof(PracticeGrowthOutcome),record.Outcome) || record.BeforeStep < 0 || record.AfterStep < record.BeforeStep
                    || !Finite(record.BeforeGrowth) || !Finite(record.AfterGrowth) || record.BeforeGrowth < 0 || record.AfterGrowth < record.BeforeGrowth
                    || !state.DailyDecisions.Any(day => day.CycleId == record.CycleId))
                    throw new InvalidDataException("Invalid practice growth record.");
                var session = state.Sessions.FirstOrDefault(item => item.SessionId == record.SessionId);
                if(record.MissionId==DailyEmotionPolicy.MissionId)
                {
                    var emotion=state.DailyEmotionRecords.FirstOrDefault(item=>item.Id==record.SessionId);
                    if(session!=null || record.SessionId!=DailyEmotionPolicy.RecordId(record.CycleId)
                        || record.EligibilityKey!=DailyEmotionPolicy.GrowthReceipt(record.CycleId)
                        || (record.Outcome!=PracticeGrowthOutcome.Grown && record.Outcome!=PracticeGrowthOutcome.Stored)
                        || (emotion!=null && emotion.CycleId!=record.CycleId))
                        throw new InvalidDataException("Daily emotion growth source mismatch.");
                }
                if (session != null && (session.MissionId != record.MissionId || session.InstanceId != record.CycleId
                    || session.Status == ParticipationState.Selected)) throw new InvalidDataException("Practice growth session mismatch.");
                if (record.Outcome == PracticeGrowthOutcome.Grown)
                {
                    var recipe = state.PlantAppearance?.Recipe.InstanceId == record.PlantInstanceId ? state.PlantAppearance.Recipe
                        : state.PlantPhotos.FirstOrDefault(photo => photo.SourcePlantInstanceId == record.PlantInstanceId)?.Recipe;
                    if (recipe == null || record.AfterStep != record.BeforeStep + 1 || record.AfterStep > recipe.GrowthStep)
                        throw new InvalidDataException("Practice growth has no matching plant stage.");
                }
                else if (record.BeforeStep != record.AfterStep || record.BeforeGrowth != record.AfterGrowth)
                    throw new InvalidDataException("An unapplied practice cannot advance a plant.");
            }
            foreach (var group in state.PracticeGrowthRecords.Where(record => record.Outcome == PracticeGrowthOutcome.Grown).GroupBy(record => record.PlantInstanceId))
            {
                var ordered = group.ToArray();
                for (int i=1;i<ordered.Length;i++)
                    if (ordered[i].BeforeStep != ordered[i-1].AfterStep) throw new InvalidDataException("Practice growth stage chain is broken.");
            }
        }
        internal static void ValidatePlantPeriod(string startedUtc,string startedCycleId,string savedUtc=null,string savedCycleId=null)
        {
            if(startedUtc==null || startedCycleId==null
                || (startedUtc.Length==0 ? startedCycleId.Length!=0 : !Timestamp(startedUtc) || (startedCycleId.Length>0 && !Cycle(startedCycleId))))
                throw new InvalidDataException("Invalid plant start period.");
            if(savedUtc==null && savedCycleId==null)return;
            if(savedCycleId==null || (savedCycleId.Length>0 && !Cycle(savedCycleId))
                || (startedUtc.Length>0 && (!Timestamp(savedUtc) || DateTimeOffset.Parse(savedUtc)<DateTimeOffset.Parse(startedUtc)))
                || (startedCycleId.Length>0 && savedCycleId.Length>0 && string.CompareOrdinal(savedCycleId,startedCycleId)<0))
                throw new InvalidDataException("Invalid plant photo period.");
        }
        static void ValidatePlantPhotos(GardenState state)
        {
            if (state.PlantPhotos == null || state.PhotoSaveCommands == null || state.PlantStartCommands == null
                || state.PlantPhotos.Any(photo => photo == null) || state.PhotoSaveCommands.Any(command => command == null)
                || state.PlantStartCommands.Any(command => command == null))
                throw new InvalidDataException("Plant album records missing.");
            Unique(state.PlantPhotos.Select(photo => photo.Id));
            Unique(state.PlantPhotos.Select(photo => photo.SourcePlantInstanceId));
            Unique(state.PhotoSaveCommands.Select(command => command.RequestId));
            Unique(state.PlantStartCommands.Select(command => command.RequestId));
            Unique(state.PlantStartCommands.Select(command => command.PlantInstanceId));
            Unique(state.PlantStartCommands.Select(command => command.ArchivedPlantInstanceId));
            foreach (var photo in state.PlantPhotos)
            {
                if (photo.Version != 1 || !Typed(photo.Id, "plant-photo") || !Timestamp(photo.SavedUtc)
                    || string.IsNullOrWhiteSpace(photo.DisplayName) || !Finite(photo.Growth) || photo.Growth < 0
                    || !Finite(photo.Scale) || photo.Scale <= 0 || photo.Recipe == null
                    || photo.Recipe.InstanceId != photo.SourcePlantInstanceId)
                    throw new InvalidDataException("Invalid plant photo snapshot.");
                CapstoneDesign.Plants.PlantGenerator.Validate(photo.Recipe);
                ValidatePlantPeriod(photo.StartedUtc,photo.StartedCycleId,photo.SavedUtc,photo.SavedCycleId);
                if(state.PlantAppearance?.IsArchived==true && state.PlantAppearance.Recipe.InstanceId==photo.SourcePlantInstanceId
                    && (photo.StartedUtc!=state.PlantAppearance.StartedUtc || photo.StartedCycleId!=state.PlantAppearance.StartedCycleId))
                    throw new InvalidDataException("Archived plant period differs from its frozen photo.");
                if (!state.PhotoSaveCommands.Any(command => command.PhotoId == photo.Id))
                    throw new InvalidDataException("Plant photo save identity missing.");
            }
            foreach (var command in state.PhotoSaveCommands)
                if (state.PurchaseReceipts.Contains(command.RequestId) || state.PlantStartCommands.Any(start => start.RequestId == command.RequestId)
                    || !state.PlantPhotos.Any(photo => photo.Id == command.PhotoId && photo.SourcePlantInstanceId == command.PlantInstanceId))
                    throw new InvalidDataException("Plant photo command identity mismatch.");
            foreach (var command in state.PlantStartCommands)
                if (state.PurchaseReceipts.Contains(command.RequestId) || command.PlantInstanceId == command.ArchivedPlantInstanceId
                    || command.AppliedGrowthSteps < 0
                    || !state.PlantPhotos.Any(photo => photo.SourcePlantInstanceId == command.ArchivedPlantInstanceId)
                    || (state.PlantAppearance?.Recipe.InstanceId != command.PlantInstanceId
                        && !state.PlantPhotos.Any(photo => photo.SourcePlantInstanceId == command.PlantInstanceId)))
                    throw new InvalidDataException("Plant start command identity mismatch.");
            foreach (var command in state.PlantStartCommands)
            {
                var recipe = state.PlantAppearance?.Recipe.InstanceId == command.PlantInstanceId ? state.PlantAppearance.Recipe
                    : state.PlantPhotos.First(photo => photo.SourcePlantInstanceId == command.PlantInstanceId).Recipe;
                if (command.AppliedGrowthSteps > recipe.GrowthStep) throw new InvalidDataException("New plant stage is behind its stored-growth application.");
            }
        }
        static void ValidateFocus(GardenState state)
        {
            if(state.Sessions.Any(s=>s.FocusVersion<0 || s.FocusVersion>FocusMissionCatalog.Version))throw new InvalidDataException("Unsupported focus mission version.");
            var sessions=state.Sessions.Where(FocusMissionPolicy.IsFocus).ToArray();
            foreach(var session in sessions)
            {
                var definition=state.SchemaVersion==1 && session.FocusVersion==3
                    ?FocusMissionCatalog.Find(session.MissionId,4):FocusMissionCatalog.FindForSession(session);
                if(definition==null || session.FocusRole!=definition.Role || session.CourseOrder!=0 || string.IsNullOrWhiteSpace(session.MissionTitleSnapshot) || string.IsNullOrWhiteSpace(session.MissionDescriptionSnapshot)
                    || session.InstructionStep>definition.Steps.Length+1 || session.FocusStepRecords==null || session.FocusStepRecords.Count!=definition.Steps.Length || session.RewardAmount<0
                    || (session.Mood!=null && session.Mood.Length>80) || (session.Note!=null && session.Note.Length>500))throw new InvalidDataException("Invalid focus mission record.");
                if(session.FocusRole==FocusMissionCatalog.BasicRole)
                {
                    if((session.FocusVersion>1 && !MbctPolicy.IsProgram(session.FocusProgramId)) || FocusMissionPolicy.MissionAt(session.FocusOrder,FocusMissionPolicy.ProgramFor(session))?.Id!=session.MissionId)throw new InvalidDataException("Focus course occurrence mismatch.");
                }
                else if(session.FocusOrder!=0 || (session.FocusVersion>1 && !string.IsNullOrEmpty(session.FocusProgramId)))throw new InvalidDataException("Invalid additional mission occurrence.");
                ValidateFocusExposure(session);
                if(session.FocusSupport && ((session.FocusVersion!=FocusMissionCatalog.TextureVersion&&session.FocusVersion!=FocusMissionCatalog.Version) || session.FocusRole!=FocusMissionCatalog.AdditionalRole
                    || session.MissionId!="course:P02" || session.FocusReview || (session.FocusSourceMissionId!="course:P19"&&session.FocusSourceMissionId!="course:P26")
                    || string.IsNullOrWhiteSpace(session.FocusSourceSessionId)))throw new InvalidDataException("Invalid support activity source.");
                if(session.FocusSupport)
                {
                    var source=state.Sessions.FirstOrDefault(s=>s.SessionId==session.FocusSourceSessionId);
                    if(source!=null&&(source.Status!=ParticipationState.Ended||source.FocusEndReason!="alternative"||source.MissionId!=session.FocusSourceMissionId))throw new InvalidDataException("Support source mismatch.");
                }
                if(!string.IsNullOrEmpty(session.FocusEndReason) && (!new[]{"skipped","stopped","alternative"}.Contains(session.FocusEndReason)||session.Status!=ParticipationState.Ended))throw new InvalidDataException("Invalid practice ending reason.");
                bool gap=false;DateTimeOffset previous=DateTimeOffset.MinValue;
                for(int i=0;i<session.FocusStepRecords.Count;i++)
                {
                    var record=session.FocusStepRecords[i];
                    if(record!=null && (record.PlanActivity!=null || record.PlanTimePeriod!=null || record.PlanNote!=null))
                    {
                        if((session.FocusVersion!=FocusMissionCatalog.TextureVersion&&session.FocusVersion!=FocusMissionCatalog.Version) || definition.Steps[i].Kind!="plan" || (record.PlanNote?.Length??0)>450)throw new InvalidDataException("Invalid plan draft.");
                        string composed;
                        try {composed=ComposeHistoricalPlan(record.PlanActivity,record.PlanTimePeriod,record.PlanNote);}
                        catch(ArgumentException) {throw new InvalidDataException("Invalid plan choices.");}
                        if(record.Value!=composed)throw new InvalidDataException("Plan draft does not match recorded value.");
                    }
                    if(record==null || record.Step!=i || record.Key!=definition.Steps[i].Key || string.IsNullOrWhiteSpace(record.Title) || string.IsNullOrWhiteSpace(record.Prompt)
                        || record.Value==null || record.Value.Length>500 || (record.Skipped && record.Value.Length>0) || record.ElapsedSeconds<0 || record.ElapsedSeconds>86400
                        || record.RecommendedSeconds<0 || record.RecommendedSeconds>86400 || (record.PlanNote?.Length??0)>450
                        || (!string.IsNullOrEmpty(record.PracticeStartedUtc)&&(!GardenStateService.IsActualFocusPractice(definition.Steps[i])||!Timestamp(record.PracticeStartedUtc)||!Timestamp(session.StartedUtc)||DateTimeOffset.Parse(record.PracticeStartedUtc)<DateTimeOffset.Parse(session.StartedUtc)))
                        || (!string.IsNullOrEmpty(record.ShortenedUtc)&&(!Timestamp(record.ShortenedUtc)||string.IsNullOrEmpty(record.PracticeStartedUtc)||record.EndReason!="too_long"||DateTimeOffset.Parse(record.ShortenedUtc)<DateTimeOffset.Parse(record.PracticeStartedUtc)))
                        || (!string.IsNullOrEmpty(record.EndReason)&&!new[]{"completed","too_long","not_applicable"}.Contains(record.EndReason))
                        || (!string.IsNullOrEmpty(record.UpdatedUtc) && !Timestamp(record.UpdatedUtc)))throw new InvalidDataException("Invalid focus step record.");
                    if(string.IsNullOrEmpty(record.ConfirmedUtc)){gap=true;continue;}
                    if(session.FocusVersion>1 && definition.Steps[i].Kind=="draw" && FocusDrawingData.Decode(record.Value).Count==0)throw new InvalidDataException("Confirmed drawing is missing.");
                    if(session.FocusVersion>1 && definition.Steps[i].RequiredAnswer && string.IsNullOrWhiteSpace(record.Value))throw new InvalidDataException("Confirmed required answer is missing.");
                    DateTimeOffset confirmed;
                    if(gap || !DateTimeOffset.TryParse(record.ConfirmedUtc,out confirmed) || confirmed<=previous || !Timestamp(session.StartedUtc) || confirmed<DateTimeOffset.Parse(session.StartedUtc))throw new InvalidDataException("Focus confirmation order mismatch.");
                    previous=confirmed;
                }
                int reached=session.FocusStepRecords.Count(r=>!string.IsNullOrEmpty(r.ConfirmedUtc));
                if(session.InstructionStep>reached && reached<definition.Steps.Length)throw new InvalidDataException("Unconfirmed focus step skipped.");
                if(session.Status==ParticipationState.RewardCommitted && FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(definition,"route",session.FocusStepRecords.FirstOrDefault(r=>r.Key=="route")?.Value)!=null)throw new InvalidDataException("Different-purpose activity cannot complete its source mission.");
                if(session.Status==ParticipationState.RewardCommitted && (reached!=definition.Steps.Length || DateTimeOffset.Parse(session.EndedUtc)<previous))throw new InvalidDataException("Incomplete focus mission committed.");
                if(session.FocusVersion>1 && session.MissionId=="free-mission:M15" && session.Status==ParticipationState.RewardCommitted && session.WalkingGoalSteps==0)throw new InvalidDataException("Completed walking mission target is missing.");
                if(session.Status!=ParticipationState.RewardCommitted && session.RewardAmount!=0)throw new InvalidDataException("Uncommitted focus reward.");
                if(session.FocusReview && session.RewardAmount!=0)throw new InvalidDataException("Review cannot award nutrients.");
                if(session.Status==ParticipationState.RewardCommitted && !session.FocusReview)
                {
                    string receipt=FocusMissionPolicy.RewardReceipt(session);
                    if(!state.RewardReceipts.Contains(receipt))throw new InvalidDataException("Focus reward receipt missing.");
                }
            }
            foreach(string role in new[]{FocusMissionCatalog.BasicRole,FocusMissionCatalog.AdditionalRole})
                if(sessions.Count(s=>s.FocusRole==role && FocusMissionPolicy.IsActive(s))>1)throw new InvalidDataException("Multiple focus drafts in the same role.");
            foreach(string program in new[]{"2w","4w","8w",FocusMissionPolicy.LegacyProgram})
            {
                var completed=FocusMissionPolicy.Completed(state,program).OrderBy(s=>s.FocusOrder).ToArray();
                for(int i=0;i<completed.Length;i++)if(completed[i].FocusOrder!=i+1)throw new InvalidDataException("Focus course progress is not contiguous.");
                if(sessions.Any(s=>s.FocusRole==FocusMissionCatalog.BasicRole && s.FocusReview && FocusMissionPolicy.ProgramFor(s)==program && s.FocusOrder>completed.Length))throw new InvalidDataException("Unlearned focus review.");
            }
            if(sessions.Where(s=>s.FocusRole==FocusMissionCatalog.AdditionalRole && s.RewardAmount>0).GroupBy(s=>s.InstanceId+"|"+s.MissionId).Any(g=>g.Count()>1))throw new InvalidDataException("Additional reward duplicated.");
        }
        static string ComposeHistoricalPlan(string activity,string period,string note)
        {
            if(!string.IsNullOrEmpty(activity)&&!new[]{"차 마시기","창문 열기","물건 하나 정리","잠깐 쉬기"}.Contains(activity))throw new ArgumentException("Unknown plan activity.");
            if(!string.IsNullOrEmpty(period)&&!new[]{"아침","점심","저녁","생활 행동 뒤","나중에 정하기"}.Contains(period))throw new ArgumentException("Unknown plan period.");
            string text=(note??"").Trim();
            if(text.Length==0){if(string.IsNullOrEmpty(activity)||string.IsNullOrEmpty(period))return "";text=activity;}
            string value=string.IsNullOrEmpty(period)?text:period+" · "+text;
            if(value.Length>500)throw new ArgumentException("Plan text is too long.");
            return value;
        }
        internal static void ValidateFocusExposure(ActivitySession session)
        {
            var value=session.FocusRecommendationExposure;if(value==null)return;
            Guid id;DateTimeOffset shown,selected;var definition=FocusMissionCatalog.Find(value.MissionId);
            string[] reasons={"resume-active-activity","saved-preference","participation-history","catalog-default-no-history","focus-method-preference","focus-frequency-more","focus-frequency-less"};
            if(!FocusMissionPolicy.IsFocus(session)||session.FocusRole!=FocusMissionCatalog.AdditionalRole||session.FocusReview||session.FocusSupport
                ||!Guid.TryParseExact(value.DecisionId,"N",out id)||definition==null||definition.Role!=FocusMissionCatalog.AdditionalRole
                ||(value.PolicyId!="focus-additional-history-v1"&&value.PolicyId!="focus-additional-history-preference-v2")||!reasons.Contains(value.Reason)
                ||!DateTimeOffset.TryParse(value.ShownUtc,out shown)||!DateTimeOffset.TryParse(session.SelectedUtc,out selected)||shown>selected||selected-shown>TimeSpan.FromHours(24)
                ||session.FocusRecommendationId!=value.DecisionId||session.FocusRecommendationPolicy!=value.PolicyId||session.FocusRecommendationReason!=value.Reason)
                throw new InvalidDataException("Invalid observed focus recommendation.");
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
            copy.DailyEmotionRecords=value.DailyEmotionRecords.Select(record=>record.Copy()).ToList();
            copy.PurchaseCommands=value.PurchaseCommands.Select(c=>c.Copy()).ToList();
            copy.GrowthHistory=value.GrowthHistory.Select(record=>record.Copy()).ToList();
            copy.DailyDecisions=value.DailyDecisions.Select(d=>d.Copy()).ToList();
            copy.Sessions=value.Sessions.Select(a=>a.Copy()).ToList();
            copy.PhotoRewards=value.PhotoRewards.Select(r=>r.Copy()).ToList();
            copy.DebugWalkPeriods=value.DebugWalkPeriods.Select(d=>d.Copy()).ToList();
            copy.Environments=value.Environments.Select(e=>e.Copy()).ToList();
            copy.Plants=value.Plants.Select(p=>p.Copy()).ToList();
            copy.PlantPhotos=value.PlantPhotos.Select(photo=>photo.Copy()).ToList();
            copy.PhotoSaveCommands=value.PhotoSaveCommands.Select(command=>command.Copy()).ToList();
            copy.PlantStartCommands=value.PlantStartCommands.Select(command=>command.Copy()).ToList();
            copy.PracticeGrowthRecords=value.PracticeGrowthRecords.Select(record=>record.Copy()).ToList();
            copy.PlantAppearance=value.PlantAppearance?.Copy();copy.PlantCollection=value.PlantCollection?.Copy();return copy;
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
    public sealed class FileStateStore : IStateStore, IFullResetStateStore, IDisposable
    {
        readonly string path;
        readonly FileStream writerLock;
        long savedPrivacyRevision;
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
            // The reset journal is the commit point for a whole-app reset. Until
            // every cleanup completes, old primary/backup bytes are never loaded.
            if(File.Exists(path+".reset") || Directory.Exists(path+".reset"))
            {
                var pending=ReadEnvelope(path+".reset");
                if(!pending.FullResetPending)throw new InvalidDataException("Invalid completed reset journal.");
                return pending;
            }
            if (!File.Exists(path))
            {
                if (File.Exists(path + ".bak") || File.Exists(path + ".tmp") || File.Exists(path+".reset.tmp")) throw new InvalidDataException("Primary state missing; recovery required. Backup retained.");
                return null;
            }
            return ReadEnvelope(path);
        }
        GardenState ReadEnvelope(string source)
        {
            string envelope = File.ReadAllText(source, Encoding.UTF8);
            int newline = envelope.IndexOf('\n');
            if (newline < 0 || StateCodec.Hash(envelope.Substring(newline + 1)) != envelope.Substring(0, newline))
                throw new InvalidDataException("State checksum failed; no automatic rollback or new grant. Backup retained.");
            var loaded=StateCodec.Decode(envelope.Substring(newline + 1));savedPrivacyRevision=loaded.PrivacyRevision;return loaded;
        }
        public void Save(GardenState candidate)
        {
            RequireWriter();
            if(File.Exists(path+".reset") || Directory.Exists(path+".reset"))
                throw new InvalidOperationException("Complete the pending full reset before saving garden changes.");
            SavePrimary(candidate);
        }
        void SavePrimary(GardenState candidate)
        {
            RequireWriter(); StateCodec.Validate(candidate);
            string temp = path + ".tmp";
            WriteEnvelope(temp,candidate);
            if (File.Exists(path))
            {
                // A redaction must not leave the previous private contents in the backup.
                bool redacted=candidate.PrivacyRevision>savedPrivacyRevision;
                if(redacted)File.Delete(path+".bak");
                File.Replace(temp,path,redacted?null:path+".bak");
            }
            else File.Move(temp, path);
            savedPrivacyRevision=candidate.PrivacyRevision;
        }
        static void WriteEnvelope(string destination,GardenState candidate)
        {
            string body=StateCodec.Encode(candidate);
            byte[] bytes=Encoding.UTF8.GetBytes(StateCodec.Hash(body)+"\n"+body);
            using(var stream=new FileStream(destination,FileMode.Create,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        }
        static bool SameReset(GardenState a,GardenState b) => a.InstallationId==b.InstallationId
            && a.FullResetRequestId==b.FullResetRequestId && a.FullResetSourceInstallationId==b.FullResetSourceInstallationId;
        void WriteResetJournal(GardenState candidate)
        {
            StateCodec.Validate(candidate);
            if(!candidate.FullResetPending)throw new InvalidOperationException("A reset journal must be pending.");
            string journal=path+".reset", temp=journal+".tmp";
            WriteEnvelope(temp,candidate);
            if(File.Exists(journal))File.Replace(temp,journal,null);
            else File.Move(temp,journal);
            // No fallible work follows this authoritative commit point.
        }
        public void BeginFullReset(GardenState candidate)
        {
            RequireWriter();StateCodec.Validate(candidate);
            if(File.Exists(path+".reset") || Directory.Exists(path+".reset"))
            {
                var pending=ReadEnvelope(path+".reset");
                if(!pending.FullResetPending || !SameReset(pending,candidate))throw new InvalidOperationException("Another full reset is already pending.");
                return;
            }
            WriteResetJournal(candidate);
        }
        public void FinishFullReset(GardenState candidate)
        {
            RequireWriter();StateCodec.Validate(candidate);
            if(candidate.FullResetPending || string.IsNullOrEmpty(candidate.FullResetRequestId))
                throw new InvalidOperationException("Expected the completed reset candidate.");
            var pending=ReadEnvelope(path+".reset");
            if(!pending.FullResetPending || !SameReset(pending,candidate))throw new InvalidOperationException("Reset completion identity mismatch.");
            SavePrimary(candidate);
            DeleteResetFile(path+".bak");DeleteResetFile(path+".tmp");DeleteResetFile(path+".reset.tmp");
            // Delete the journal last. Any earlier failure/restart still loads the
            // fresh pending state instead of resurrecting an older primary/backup.
            File.Delete(path+".reset");
        }
        static void DeleteResetFile(string target)
        {
            // Deliberately fail on directories rather than recursively deleting an
            // unexpected path. The pending reset remains safely retryable.
            if(File.Exists(target) || Directory.Exists(target))File.Delete(target);
        }
        public void ResetInactiveForFullReset(string requestId)
        {
            RequireWriter();
            if(!StateCodec.IsFullResetRequestId(requestId))throw new ArgumentException("A valid reset request is required.");
            GardenState current=null;
            try { current=Load(); } catch(InvalidDataException) { } catch(System.Xml.XmlException) { } catch(InvalidOperationException) { }
            if(current!=null && current.FullResetRequestId==requestId)
            {
                if(!current.FullResetPending)return;
            }
            else
            {
                // Called only for a known inactive app-owned mode while the active
                // store's reset journal blocks normal work. Explicit full reset can
                // discard unreadable old records without importing legacy values.
                var created=DateTimeOffset.UtcNow;
                current=GardenStateService.CreateFullResetState(requestId,"inactive-store:"+StateCodec.Hash(path),
                    created.ToString("O",System.Globalization.CultureInfo.InvariantCulture),
                    TimeZoneInfo.ConvertTime(created,TimeZoneInfo.Local).DateTime.AddHours(-4).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture));
                WriteResetJournal(current);
            }
            var completed=StateCodec.Clone(current);completed.FullResetPending=false;
            FinishFullReset(completed);
        }
        public void Dispose() { if(!disposed) { disposed=true; writerLock.Dispose(); } }
    }
    public sealed partial class GardenStateService : IDisposable
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
        // A scalar from the committed state; status polling must not clone every
        // session, drawing and plant recipe just to display the applied date.
        public string AppliedCycleId { get { lock(gate) { RequireOpen(); return state.LastCycleId; } } }
        public int CoinBalance { get { lock(gate) { RequireOpen(); return state.Coins; } } }
        public bool HasPendingFullReset { get { lock(gate) { RequireOpen(); return state.FullResetPending; } } }
        public string MissionPlantAvailability { get { lock(gate) { RequireOpen(); return MissionPlantUnavailable(state); } } }
        static string MissionPlantUnavailable(GardenState value) => GardenPlantVisuals.HasCurrentPlant(value)
            ? value.FullResetPending?"전체 초기화를 마친 뒤 미션을 시작해 주세요.":null
            : "새 식물 키우기로 씨앗을 심은 뒤 미션을 시작해 주세요.";
        static void RequireMissionPlant(GardenState value)
        {
            string unavailable=MissionPlantUnavailable(value);
            if(unavailable!=null)throw new InvalidOperationException(unavailable);
        }
        public int NutrientBalance => CoinBalance;
        public DateTimeOffset CurrentUtc => EffectiveUtc(state);
        public TimeZoneInfo CurrentTimeZone => clock.TimeZone;
        DateTimeOffset EffectiveUtc(GardenState source) => clock.UtcNow.AddDays(source == null ? 0 : source.PreviewDayOffset);
        public GardenStateService(IStateStore store, IClock clock, DemoBalanceConfig config, LegacySnapshot legacy = null)
        {
            this.store = store; this.clock = clock; this.legacy = legacy ?? new LegacySnapshot();
            this.config = new DemoBalanceConfig { ResetHourLocal = config.ResetHourLocal, CompletionNutrient = config.CompletionNutrient, EnvironmentCost = config.EnvironmentCost, GrowthCost = config.GrowthCost, GrowthAmount = config.GrowthAmount, VisitorChancePercent = config.VisitorChancePercent };
            this.config.PhotoVerificationNutrient=config.PhotoVerificationNutrient;this.config.PhotoVerificationDailyLimit=config.PhotoVerificationDailyLimit;
            if(config.PhotoVerificationNutrient<0||config.PhotoVerificationNutrient>100||config.PhotoVerificationDailyLimit<0||config.PhotoVerificationDailyLimit>100)throw new ArgumentException("Invalid photo reward configuration.");
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
                        if (!HasLegacyState())
                        {
                            CapturePlantDate(loaded,null,null,out var created,out var createdCycle);
                            CombinedPlants.InitializeFresh(loaded,GardenPlantVisuals.InstanceId,created,createdCycle);
                        }
                        else CombinedPlants.Migrate(loaded,EffectiveUtc(loaded).ToString("O"),false,CycleFor(loaded));
                        StateCodec.Validate(loaded);
                        store.Save(loaded);
                    }
                    bool schemaMigrated=CombinedPlants.Migrate(loaded,EffectiveUtc(loaded).ToString("O"),false,CycleFor(loaded));
                    StateCodec.Validate(loaded);
                    if(loaded.FullResetPending) { state=loaded;LastError=null;return true; }
                    bool courseMigrated=loaded.MbctProgramSelectionVersion==0;
                    if(courseMigrated) { loaded.MbctProgramSelectionVersion=1;loaded.MbctProgramChosen=true; }
                    bool memoMetadataUpdated=BackfillPlantMemoMissions(loaded);
                    if(memoMetadataUpdated)CombinedPlants.CaptureHistoricalMemoMetadata(loaded);
                    if (GardenPlantVisuals.Sync(loaded) || courseMigrated || memoMetadataUpdated || schemaMigrated)
                    { CombinedPlants.CaptureCurrent(loaded,EffectiveUtc(loaded).ToString("O"),CycleFor(loaded));StateCodec.Validate(loaded); store.Save(loaded); }
                    state = loaded; LastError = null; return true;
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        void RequireOpen() { if(disposed) throw new ObjectDisposedException("GardenStateService"); if (state == null) throw new InvalidOperationException("State unavailable. Open must succeed before play."); }
        void RequireReady()
        {
            RequireOpen();
            if(state.FullResetPending)throw new InvalidOperationException("전체 초기화가 진행 중이에요. 초기화를 다시 시도해 주세요.");
        }
        bool Change(Action<GardenState> mutation)
        {
            lock (gate)
            {
                RequireOpen();
                try
                {
                    RequireReady();
                    var next = StateCodec.Clone(state); mutation(next); CameraMissionPolicy.Reconcile(next);CombinedPlants.CaptureCurrent(next,Now,CycleFor(next));StateCodec.Validate(next); store.Save(next); state = next; LastError = null; return true;
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        string Now { get { return CurrentUtc.ToString("O"); } }
        bool HasLegacyState() => legacy.HasExistingState || legacy.Nutrient!=0 || legacy.GardenXp!=0 || legacy.Growth!=0
            || !string.IsNullOrEmpty(legacy.LastUnlock) || legacy.CompletedMissionIds.Count!=0 || legacy.UnlockIds.Count!=0;
        void CapturePlantDate(GardenState source,string minimumUtc,string minimumCycle,out string utc,out string cycle)
        {
            // Capture once so a clock crossing 04:00 cannot split the timestamp
            // and business date across different reads in one saved operation.
            var captured=EffectiveUtc(source).ToUniversalTime();
            var value=captured;
            if(DateTimeOffset.TryParse(minimumUtc,out var minimum) && minimum>value)value=minimum.ToUniversalTime();
            utc=value.ToString("O",System.Globalization.CultureInfo.InvariantCulture);
            cycle=TimeZoneInfo.ConvertTime(captured,clock.TimeZone).DateTime.AddHours(-config.ResetHourLocal).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            // Follow the app's existing monotonic cycle/time policy after clock
            // rollback; retries never rewrite an already committed period.
            if(string.CompareOrdinal(source.LastCycleId,cycle)>0)cycle=source.LastCycleId;
            if(string.CompareOrdinal(minimumCycle,cycle)>0)cycle=minimumCycle;
        }
        void StampPlantStart(GardenState source,string minimumUtc=null,string minimumCycle=null)
        {
            CapturePlantDate(source,minimumUtc,minimumCycle,out source.PlantAppearance.StartedUtc,out source.PlantAppearance.StartedCycleId);
            var plant=WeeklyPlants.Active(source);if(plant!=null){plant.CreatedUtc=source.PlantAppearance.StartedUtc;plant.StartedCycleId=source.PlantAppearance.StartedCycleId;}
        }
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
                if(state.FullResetPending){LastError="전체 초기화를 먼저 마쳐 주세요.";return false;}
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

        internal static GardenState CreateFullResetState(string requestId,string sourceInstallationId,string startedUtc="",string startedCycleId="")
        {
            var fresh=new GardenState { MigrationCompleted=true,MbctProgramSelectionVersion=1,MbctProgramChosen=false,
                InstallationId=StateCodec.ResetInstallationId(requestId,sourceInstallationId),
                FullResetRequestId=requestId,FullResetSourceInstallationId=sourceInstallationId,FullResetPending=true };
            CombinedPlants.InitializeFresh(fresh,StateCodec.ResetPlantId(fresh.InstallationId),startedUtc,startedCycleId);
            StateCodec.Validate(fresh);
            return fresh;
        }
        public bool ResetAll(string requestId,string expectedInstallationId)
        {
            lock(gate)
            {
                RequireOpen();
                try
                {
                    if(!StateCodec.IsFullResetRequestId(requestId) || string.IsNullOrWhiteSpace(expectedInstallationId))
                        throw new ArgumentException("A reset request and original generation are required.");
                    if(state.FullResetRequestId==requestId)
                    {
                        if(state.FullResetSourceInstallationId!=expectedInstallationId)throw new InvalidOperationException("Reset request was reused for another generation.");
                        LastError=null;return true;
                    }
                    if(state.FullResetPending)throw new InvalidOperationException("먼저 진행 중인 전체 초기화를 마쳐 주세요.");
                    if(state.InstallationId!=expectedInstallationId)throw new InvalidOperationException("This reset request belongs to an older garden.");
                    var next=CreateFullResetState(requestId,expectedInstallationId);
                    StampPlantStart(next);
                    StateCodec.Validate(next);
                    var resetStore=store as IFullResetStateStore;
                    if(resetStore!=null)resetStore.BeginFullReset(next);else store.Save(next);
                    state=next;pendingDecision=null;cycleCandidates=Array.Empty<string>();LastError=null;return true;
                }
                catch(Exception ex){LastError=ex.Message;return false;}
            }
        }
        public bool FinishFullReset(string requestId)
        {
            lock(gate)
            {
                RequireOpen();
                try
                {
                    if(string.IsNullOrWhiteSpace(requestId) || state.FullResetRequestId!=requestId)
                        throw new InvalidOperationException("Reset completion belongs to another request.");
                    if(!state.FullResetPending){LastError=null;return true;}
                    var next=StateCodec.Clone(state);next.FullResetPending=false;
                    var resetStore=store as IFullResetStateStore;
                    if(resetStore!=null)resetStore.FinishFullReset(next);else store.Save(next);
                    state=next;pendingDecision=null;cycleCandidates=Array.Empty<string>();LastError=null;return true;
                }
                catch(Exception ex){LastError=ex.Message;return false;}
            }
        }
        public bool ResetMissionHistory() => Change(next=>
        {
            // Preserve demonstrably linked titles before removing their only source.
            BackfillPlantMemoMissions(next);
            CombinedPlants.CaptureHistoricalMemoMetadata(next);
            if(next.PlantAppearance!=null)
            {
                next.PlantAppearance.ArchivedCredits.AddRange(next.PlantAppearance.Credits);
                next.PlantAppearance.Credits.Clear();
            }
            if(next.Sessions.Count>0 || next.PracticePreferences.Count>0 || next.Experiences.Count>0 || next.DailyEmotionRecords.Count>0)
                next.PrivacyRevision=checked(next.PrivacyRevision+1);
            next.PlantCollection.DeletedExperienceTokens.AddRange(next.Experiences.Select(e=>WeeklyPlants.Token(e.Id))
                .Where(token=>!next.PlantCollection.DeletedExperienceTokens.Contains(token)));
            next.Sessions.Clear();next.PracticePreferences.Clear();next.Experiences.Clear();next.DailyEmotionRecords.Clear();
            foreach(var memory in next.PlantCollection.Plants.SelectMany(p=>p.Memories).Where(m=>m.Kind==MemoryRecordKind.SessionReflection||m.Kind==MemoryRecordKind.Experience))WeeklyPlants.Unlink(memory);
            next.CompletedCourseIds.Clear();next.NextCourseOrder=1;
            next.MbctCompletedIds.Clear();next.MbctNextOrder=1;next.MbctPrograms.Clear();
            // Clear the visible course history, not durable award claims. Otherwise
            // restarting the same course/date could mint coins a second time.
            foreach(var growth in next.GrowthHistory)growth.LinkedSessionId="";
        });

        // Integration point for early completion by the lifecycle owner. The final
        // recipe stage is always completed and cannot be reopened for paid growth.
        public bool SetPlantCompleted(string plantInstanceId, bool completed) => Change(next =>
        {
            if (string.IsNullOrWhiteSpace(plantInstanceId) || !GardenPlantVisuals.HasCurrentPlant(next)
                || next.PlantAppearance.Recipe.InstanceId != plantInstanceId)
                throw new InvalidOperationException("The current plant instance is required.");
            if (!completed && (next.PlantAppearance.Recipe.Mature||next.PlantPhotos.Any(p=>p.SourcePlantInstanceId==plantInstanceId)))
                throw new InvalidOperationException("A plant at its final growth stage must remain completed.");
            next.PlantAppearance.IsCompleted = completed;
        });

        public bool SavePlantPhoto(string requestId, string plantInstanceId)
        {
            lock (gate)
            {
                RequireOpen();
                try
                {
                    RequireReady();
                    if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(plantInstanceId))
                        throw new ArgumentException("A photo request and plant instance are required.");
                    if (state.PurchaseReceipts.Contains(requestId) || state.PlantStartCommands.Any(command => command.RequestId == requestId))
                        throw new InvalidOperationException("Request id belongs to another garden action.");
                    var prior = state.PhotoSaveCommands.FirstOrDefault(command => command.RequestId == requestId);
                    if (prior != null)
                    {
                        if (prior.PlantInstanceId != plantInstanceId)
                            throw new InvalidOperationException("Photo request id was reused for another plant.");
                        // Already committed, including after another plant becomes current.
                        // No new storage write is needed to acknowledge a retry.
                        LastError = null;
                        return true;
                    }
                    if (state.PlantAppearance == null || state.PlantAppearance.Recipe.InstanceId != plantInstanceId)
                        throw new InvalidOperationException("The current plant instance is required.");
                    var existing = state.PlantPhotos.FirstOrDefault(photo => photo.SourcePlantInstanceId == plantInstanceId);
                    if (existing == null && !state.PlantAppearance.IsCompleted)
                        throw new InvalidOperationException("Only a completed plant can be saved to the album.");
                    return Change(next =>
                    {
                        BackfillPlantMemoMissions(next);
                        var photo = next.PlantPhotos.FirstOrDefault(item => item.SourcePlantInstanceId == plantInstanceId);
                        if (photo == null)
                        {
                            var recipe = next.PlantAppearance.Recipe;
                            float growth = GardenPlantVisuals.TotalGrowth(next);
                            CapturePlantDate(next,next.PlantAppearance.StartedUtc,next.PlantAppearance.StartedCycleId,out var savedUtc,out var savedCycleId);
                            photo = new GardenPlantPhoto {
                                Id = "plant-photo:" + StateCodec.Hash(plantInstanceId),
                                SourcePlantInstanceId = plantInstanceId,
                                StartedUtc = next.PlantAppearance.StartedUtc,
                                StartedCycleId = next.PlantAppearance.StartedCycleId,
                                SavedUtc = savedUtc,
                                SavedCycleId = savedCycleId,
                                DisplayName = recipe.SpeciesId == null ? (recipe.GrowthStep == 0 ? "작은 씨앗" : "새싹")
                                    : CapstoneDesign.Plants.PlantCatalog.Get(recipe.SpeciesId).Name,
                                Recipe = GardenPlantVisuals.CopyRecipe(recipe),
                                Growth = growth, Scale = GardenPlantVisuals.ScaleForGrowth(growth),
                                Memos = next.PlantAppearance.Memos.Select(memo => memo.Copy()).ToList()
                            };
                            next.PlantPhotos.Add(photo);
                        }
                        // Additional button requests acknowledge the same first picture,
                        // never recapture a changed plant or overwrite its original date.
                        next.PhotoSaveCommands.Add(new GardenPlantPhotoCommand {
                            RequestId = requestId, PlantInstanceId = plantInstanceId, PhotoId = photo.Id
                        });
                        // Automatic album retention leaves this completed plant visible
                        // until the next eligible practice starts a new individual.
                        next.PlantAppearance.IsArchived = false;
                    });
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        public bool StartNewPlant(string requestId, string archivedPlantInstanceId)
        {
            lock (gate)
            {
                RequireOpen();
                try
                {
                    RequireReady();
                    if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(archivedPlantInstanceId))
                        throw new ArgumentException("A start request and archived plant instance are required.");
                    if (state.PurchaseReceipts.Contains(requestId) || state.PhotoSaveCommands.Any(command => command.RequestId == requestId))
                        throw new InvalidOperationException("Request id belongs to another garden action.");
                    var prior = state.PlantStartCommands.FirstOrDefault(command => command.RequestId == requestId);
                    if (prior != null)
                    {
                        if (prior.ArchivedPlantInstanceId != archivedPlantInstanceId)
                            throw new InvalidOperationException("Plant start request was reused for another archived plant.");
                        LastError = null;
                        return true;
                    }
                    if (state.PlantAppearance == null || !state.PlantAppearance.IsCompleted
                        || state.PlantAppearance.Recipe.InstanceId != archivedPlantInstanceId)
                        throw new InvalidOperationException("The expected archived plant and an empty garden are required.");
                    return Change(next =>
                    {
                        CombinedPlants.EnsureGrowing(next,Now,CycleFor(next),requestId,false);
                        next.PlantStartCommands.Last().RequestId=requestId;
                    });
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        static void Id(string id, string prefix) { if (!StateCodec.Typed(id,prefix)) throw new ArgumentException("Expected namespaced " + prefix + " id."); }
        public bool SelectMbctProgram(string program)
        {
            return Change(next => {
                if(!MbctPolicy.IsProgram(program))throw new ArgumentException("Unknown course program.");
                if(next.MbctProgramChosen && next.MbctProgramId==program)return;
                if(next.Sessions.Any(s=>StateCodec.Typed(s.MissionId,"course") && !s.FocusSupport && FocusMissionPolicy.IsActive(s)))
                    throw new InvalidOperationException("진행 중인 기본 미션을 마치거나 종료한 뒤 기간을 바꿀 수 있어요.");
                next.MbctProgramId=program;next.MbctProgramChosen=true;next.MbctProgramSelectionVersion=1;
            });
        }
        public bool BeginSession(string sessionId, string missionId, int courseOrder = 0, RecommendationContext recommendation=null)
        {
            return Change(next =>
            {
                RequireMissionPlant(next);
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
                if(sessionId.StartsWith(DailyEmotionPolicy.RecordPrefix,StringComparison.Ordinal))throw new ArgumentException("Reserved daily emotion record id.");
                if (!(StateCodec.Typed(missionId,"course") || StateCodec.Typed(missionId,"free-mission"))) throw new ArgumentException("Mission id must be typed.");
                string role=missionId.StartsWith("course:",StringComparison.Ordinal)?FocusMissionCatalog.BasicRole:FocusMissionCatalog.AdditionalRole;
                if(next.Sessions.Any(s=>FocusMissionPolicy.RoleFor(s)==role && FocusMissionPolicy.IsActive(s)))throw new InvalidOperationException(role==FocusMissionCatalog.BasicRole?"진행하던 기본 미션을 이어서 해주세요.":"진행하던 추가 미션을 이어서 해주세요.");
                bool mbct=MbctPolicy.IsMbct(missionId);
                string program=MbctPolicy.ProgramForMission(missionId);
                int nextOrder=mbct?MbctPolicy.NextOrder(next,program):next.NextCourseOrder;
                if(mbct && program!=MbctPolicy.ActiveProgram(next) && courseOrder>=nextOrder)throw new InvalidOperationException("Choose this course before starting a new practice.");
                if(mbct && missionId!=MbctPolicy.MissionId(program,courseOrder))throw new ArgumentException("MBCT mission/order mismatch.");
                if (courseOrder < 0 || courseOrder > (mbct?MbctPolicy.PracticeCount(program):28) || (courseOrder > 0 && !missionId.StartsWith("course:")) || (missionId.StartsWith("course:") && courseOrder==0)) throw new ArgumentException("Invalid course order.");
                if(courseOrder>nextOrder) throw new InvalidOperationException("Future course step is not yet available.");
                if(next.Sessions.Any(s=>s.MissionId==missionId && s.CourseOrder!=courseOrder)) throw new InvalidOperationException("Mission course order changed without migration.");
                if (next.LastCycleId.Length == 0) throw new InvalidOperationException("Refresh daily cycle first.");
                AdvanceCycle(next,cycleCandidates);
                if(mbct && courseOrder==nextOrder)
                {
                    var unavailable=MbctPolicy.Availability(next,program);
                    if(unavailable!=null)throw new InvalidOperationException(unavailable);
                }
                else if(!mbct)
                {
                    var unavailable=FocusMissionPolicy.DailyAvailability(next,role);
                    if(unavailable!=null)throw new InvalidOperationException(unavailable);
                }
                if(mbct)next.MbctProgramChosen=true;
                next.Sessions.Add(new ActivitySession { SessionId = sessionId, MissionId = missionId, InstanceId = next.LastCycleId, SelectedUtc = Now, Status = ParticipationState.Selected, CourseOrder = courseOrder, Recommendation=recommendation?.Copy() });
            });
        }
        public bool BeginFocusSession(string sessionId,string missionId,int order=0,bool review=false,string program=null,FocusAdditionalRecommendation recommendation=null,FocusRecommendationExposure exposure=null)
        {
            lock(gate)
            {
                bool saved=Change(next=>
                {
                    RequireMissionPlant(next);
                    if(string.IsNullOrWhiteSpace(sessionId) || sessionId.Contains("|"))throw new ArgumentException("Session id required.");
                    string selected=program??next.MbctProgramId;
                    int version=selected==FocusMissionPolicy.LegacyProgram?1:FocusMissionCatalog.Version;
                    var definition=FocusMissionCatalog.Find(missionId,version)??throw new ArgumentException("Unknown active mission.");
                    var existing=next.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
                    bool basic=definition.Role==FocusMissionCatalog.BasicRole;
                    if(recommendation!=null && (basic || recommendation.MissionId!=definition.Id))throw new ArgumentException("Recommendation does not match selected activity.");
                    int occurrence=basic?(order>0?order:existing?.FocusOrder??(review?FocusMissionPolicy.Completed(next,selected).Where(s=>s.MissionId==definition.Id).Select(s=>s.FocusOrder).DefaultIfEmpty(0).Max():FocusMissionPolicy.NextOrder(next,selected))):0;
                    if(existing!=null)
                    {
                        if(!FocusMissionPolicy.IsFocus(existing) || existing.MissionId!=definition.Id || existing.FocusOrder!=occurrence || existing.FocusReview!=review || (basic && FocusMissionPolicy.ProgramFor(existing)!=selected))throw new InvalidOperationException("Session belongs to a different mission occurrence.");
                        return;
                    }
                    if(sessionId.StartsWith(DailyEmotionPolicy.RecordPrefix,StringComparison.Ordinal))throw new ArgumentException("Reserved daily emotion record id.");
                    if(order<0 || (!basic && order!=0))throw new ArgumentException("Invalid mission occurrence.");
                    AdvanceCycle(next,cycleCandidates);
                    if(next.Sessions.Any(s=>FocusMissionPolicy.RoleFor(s)==definition.Role && FocusMissionPolicy.IsActive(s)))throw new InvalidOperationException(basic?"진행하던 기본 미션을 이어서 해주세요.":"진행하던 추가 미션을 이어서 해주세요.");
                    if(basic)
                    {
                        if(selected!=FocusMissionPolicy.LegacyProgram && !MbctPolicy.IsProgram(selected))throw new ArgumentException("Unknown course program.");
                        if(FocusMissionPolicy.MissionAt(occurrence,selected)?.Id!=definition.Id)throw new InvalidOperationException("기본 미션 순서를 확인해 주세요.");
                        if(review)
                        {
                            if(occurrence>=FocusMissionPolicy.NextOrder(next,selected))throw new InvalidOperationException("완료한 미션만 복습할 수 있어요.");
                        }
                        else
                        {
                            if(version>1 && (!next.MbctProgramChosen || selected!=next.MbctProgramId))throw new InvalidOperationException("먼저 나에게 맞는 코스를 선택해 주세요.");
                            if(occurrence!=FocusMissionPolicy.NextOrder(next,selected))throw new InvalidOperationException("현재 순서의 기본 미션부터 시작해 주세요.");
                            var unavailable=FocusMissionPolicy.Availability(next,selected);if(unavailable!=null)throw new InvalidOperationException(unavailable);
                        }
                    }
                    else if(review)
                    {
                        if(!next.Sessions.Any(s=>FocusMissionPolicy.IsFocus(s) && s.MissionId==definition.Id && s.Status==ParticipationState.RewardCommitted))throw new InvalidOperationException("완료한 미션만 복습할 수 있어요.");
                    }
                    else
                    {
                        var unavailable=FocusMissionPolicy.AdditionalAvailability(next);
                        if(unavailable!=null)throw new InvalidOperationException(unavailable);
                    }
                    next.Sessions.Add(new ActivitySession {SessionId=sessionId,MissionId=definition.Id,InstanceId=next.LastCycleId,SelectedUtc=Now,Status=ParticipationState.Selected,
                        FocusVersion=version,FocusRole=definition.Role,FocusProgramId=basic && version>1?selected:"",FocusOrder=occurrence,FocusReview=review,
                        MissionTitleSnapshot=definition.Title,MissionDescriptionSnapshot=definition.Overview,
                        FocusRecommendationId=exposure?.DecisionId??(recommendation!=null?Guid.NewGuid().ToString("N"):null),
                        FocusRecommendationPolicy=exposure?.PolicyId??recommendation?.PolicyId,FocusRecommendationReason=exposure?.Reason??recommendation?.Reason,
                        FocusRecommendationExposure=exposure?.Copy(),
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
            var definition=FocusMissionCatalog.FindForSession(session);
            if(step<0 || step>=definition.Steps.Length)throw new ArgumentException("Invalid focus step.");
            var instruction=definition.Steps[step];string value=skipped?"":answer??"";
            if(value.Length>500)throw new ArgumentException("기록은 500자까지 입력할 수 있어요.");
            if(!instruction.OptionalInput && value.Length>0 && !instruction.Options.Contains(value))throw new ArgumentException("제공된 선택지 중에서 골라주세요.");
            var record=session.FocusStepRecords[step];
            if(session.FocusVersion>1 && record.Value!=value && !string.IsNullOrEmpty(record.ConfirmedUtc))
                foreach(var later in session.FocusStepRecords.Skip(step)){later.ConfirmedUtc=null;}
            // Historical choice-based M10 drafts now use the simple text editor.
            // Preserve their original choices until the user actually changes the
            // value; after an edit, old choices must not contradict the new text.
            if((session.FocusVersion==FocusMissionCatalog.TextureVersion||session.FocusVersion==FocusMissionCatalog.Version) && instruction.Kind=="plan" && record.Value!=value)
                record.PlanActivity=record.PlanTimePeriod=record.PlanNote=null;
            record.Value=value;
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
        public bool SaveFocusElapsed(string id,int step,int elapsedSeconds) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(step<0 || step>=session.FocusStepRecords.Count || elapsedSeconds<0 || elapsedSeconds>86400)throw new ArgumentException("Invalid focus time.");
            var record=session.FocusStepRecords[step];
            record.ElapsedSeconds=Math.Max(record.ElapsedSeconds,elapsedSeconds);
        });
        public bool SaveFocusPlan(string id,int step,string activity,string period,string note) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(step!=session.InstructionStep || step<0 || step>=session.FocusStepRecords.Count || FocusMissionCatalog.FindForSession(session).Steps[step].Kind!="plan")throw new InvalidOperationException("계획 화면에서 저장해 주세요.");
            string value=FocusMissionAdjustmentPolicy.ComposePlan(activity,period,note);
            UpdateFocusAnswer(session,step,value,string.IsNullOrEmpty(value));
            var record=session.FocusStepRecords[step];record.PlanActivity=activity;record.PlanTimePeriod=period;record.PlanNote=note;
            if(value.Length>0 && string.IsNullOrEmpty(record.PracticeStartedUtc))record.PracticeStartedUtc=SessionTime(session.StartedUtc);
        });
        public int RecommendedFocusSeconds(string id,int step)
        {
            lock(gate) { RequireOpen();return FocusDuration(state,Session(state,id),step).RecommendedSeconds; }
        }
        static FocusDurationRecommendation FocusDuration(GardenState source,ActivitySession session,int step)
        {
            var definition=FocusMissionCatalog.FindForSession(session);
            if(definition==null || step<0 || step>=definition.Steps.Length)throw new ArgumentException("Invalid practice step.");
            var record=session.FocusStepRecords[step];
            if(record.RecommendedSeconds>0)return new FocusDurationRecommendation {BaselineSeconds=definition.Steps[step].RecommendedSeconds,RecommendedSeconds=record.RecommendedSeconds,PolicyId=record.RecommendationPolicy,Reason="saved_at_start"};
            var history=source.Sessions.Where(a=>FocusMissionPolicy.IsFocus(a) && a.SessionId!=session.SessionId && a.FocusVersion==session.FocusVersion)
                .SelectMany(a=>a.FocusStepRecords.Where(r=>r.EndReason=="too_long" && !string.IsNullOrEmpty(r.ShortenedUtc))
                    .Select(r=>new FocusDurationFeedback {MissionId=a.MissionId,StepKey=r.Key,ActualSeconds=r.ElapsedSeconds,ConfirmedTooLong=true,ConfirmedUtc=r.ShortenedUtc}));
            return FocusMissionAdjustmentPolicy.RecommendDuration(definition,definition.Steps[step].Key,definition.Steps[step].RecommendedSeconds,history);
        }
        public static bool IsActualFocusPractice(FocusMissionStep step)
            =>step!=null && (FocusMissionAdjustmentPolicy.IsTimedPractice(step) || step.Kind=="draw" || step.Kind=="observe" || step.Kind=="listen" || new[]{"light","wash","feeling","plan","recall","word","message","trigger"}.Contains(step.Key));
        public bool StartFocusPracticeStep(string id,int step) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(step!=session.InstructionStep || step<0 || step>=session.FocusStepRecords.Count)throw new InvalidOperationException("현재 실습에서 시작해 주세요.");
            if(!IsActualFocusPractice(FocusMissionCatalog.FindForSession(session).Steps[step]))throw new InvalidOperationException("준비가 아닌 실제 실습에서 시작해 주세요.");
            var record=session.FocusStepRecords[step];
            if(!string.IsNullOrEmpty(record.ConfirmedUtc))throw new InvalidOperationException("이미 마친 단계예요.");
            if(string.IsNullOrEmpty(record.PracticeStartedUtc))record.PracticeStartedUtc=SessionTime(session.StartedUtc);
            if(record.RecommendedSeconds==0)
            {
                var recommendation=FocusDuration(next,session,step);
                record.RecommendedSeconds=recommendation.RecommendedSeconds;record.RecommendationPolicy=recommendation.PolicyId;
            }
        });
        public bool ShortenFocusStep(string id,int step,int elapsedSeconds,string answer=null) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(FocusMissionCatalog.IsGuided(session))throw new InvalidOperationException("안내형 실습은 안내형 마무리 경로를 사용해 주세요.");
            if(step<0 || step>=session.FocusStepRecords.Count || elapsedSeconds<0 || elapsedSeconds>86400)throw new ArgumentException("Invalid practice duration.");
            var record=session.FocusStepRecords[step];
            if(step<session.InstructionStep && record.EndReason=="too_long")return;
            if(step!=session.InstructionStep || string.IsNullOrEmpty(record.PracticeStartedUtc) || !FocusMissionAdjustmentPolicy.IsTimedPractice(FocusMissionCatalog.FindForSession(session).Steps[step]))
                throw new InvalidOperationException("실습을 시작한 뒤 줄일 수 있어요.");
            record.ElapsedSeconds=Math.Max(record.ElapsedSeconds,elapsedSeconds);
            ConfirmFocusStep(session,step,answer,string.IsNullOrEmpty(answer));
            record.EndReason="too_long";record.ShortenedUtc=SessionTime(record.PracticeStartedUtc);
        });
        public bool EndFocusParticipation(string id,string reason="skipped") => Change(next=>
        {
            var session=Session(next,id);
            if(!FocusMissionPolicy.IsFocus(session))throw new InvalidOperationException("미션을 확인해 주세요.");
            if(session.Status==ParticipationState.Ended)return;
            if(session.Status==ParticipationState.RewardCommitted)throw new InvalidOperationException("이미 완료한 미션이에요.");
            if(reason!="skipped" && reason!="stopped")throw new ArgumentException("Unknown ending reason.");
            AdvanceCycle(next,cycleCandidates);
            session.Status=ParticipationState.Ended;session.FocusEndReason=reason;session.EndedUtc=SessionTime(session.StartedUtc??session.SelectedUtc);
            // An unfinished practice records actual participation only: no coins or partial growth.
            GardenPlantVisuals.Sync(next);
        });
        public bool BeginFocusAlternative(string sourceId,int step,string answer,string newId) => Change(next=>
        {
            if(string.IsNullOrWhiteSpace(newId))throw new ArgumentException("Activity identity required.");
            var existing=next.Sessions.FirstOrDefault(a=>a.SessionId==newId);
            if(existing!=null) { if(existing.FocusSupport && existing.FocusSourceSessionId==sourceId)return;throw new InvalidOperationException("Activity identity already used."); }
            var source=FocusActiveSession(next,sourceId);var original=FocusMissionCatalog.FindForSession(source);
            if(step!=source.InstructionStep || step<0 || step>=original.Steps.Length)throw new InvalidOperationException("현재 선택에서 전환해 주세요.");
            string alternative=FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(original,original.Steps[step].Key,answer);
            if(alternative!="course:P02")throw new InvalidOperationException("검토된 대체 활동을 선택해 주세요.");
            if(next.Sessions.Any(a=>FocusMissionPolicy.IsFocus(a)&&a.FocusRole==FocusMissionCatalog.AdditionalRole&&FocusMissionPolicy.IsActive(a)))
                throw new InvalidOperationException("진행 중인 추가 활동을 먼저 마치거나 넘겨 주세요.");
            AdvanceCycle(next,cycleCandidates);
            var unavailable=FocusMissionPolicy.AdditionalAvailability(next);if(unavailable!=null)throw new InvalidOperationException(unavailable);
            UpdateFocusAnswer(source,step,answer,false);
            source.Status=ParticipationState.Ended;source.FocusEndReason="alternative";source.EndedUtc=SessionTime(source.StartedUtc);

            var definition=FocusMissionCatalog.Find(alternative);
            next.Sessions.Add(new ActivitySession {SessionId=newId,MissionId=definition.Id,InstanceId=next.LastCycleId,SelectedUtc=Now,Status=ParticipationState.Selected,
                FocusVersion=FocusMissionCatalog.Version,FocusRole=FocusMissionCatalog.AdditionalRole,FocusSupport=true,FocusSourceSessionId=sourceId,FocusSourceMissionId=source.MissionId,
                MissionTitleSnapshot=definition.Title,MissionDescriptionSnapshot=definition.Overview,
                FocusStepRecords=definition.Steps.Select((instruction,index)=>new FocusStepRecord {Step=index,Key=instruction.Key,Title=instruction.Title,Prompt=instruction.Body}).ToList()});
            GardenPlantVisuals.Sync(next);
        });
        static ActivitySession GuidedFocusDraftSession(GardenState next,string id,bool allowSelected)
        {
            var session=Session(next,id);
            if(!FocusMissionCatalog.IsGuided(session) || session.InstructionStep!=0
                || (session.Status!=ParticipationState.InProgress && session.Status!=ParticipationState.Paused
                    && (!allowSelected || session.Status!=ParticipationState.Selected)))
                throw new InvalidOperationException("현재 실습 화면의 입력만 저장할 수 있어요.");
            return session;
        }
        void UpdateGuidedFocusDraft(ActivitySession session,string practiceChoice,string reflection,int elapsedSeconds)
        {
            if(elapsedSeconds<0 || elapsedSeconds>86400)throw new ArgumentException("Invalid focus time.");
            UpdateFocusAnswer(session,0,practiceChoice,false);
            UpdateFocusAnswer(session,1,reflection,false);
            session.FocusStepRecords[0].ElapsedSeconds=Math.Max(session.FocusStepRecords[0].ElapsedSeconds,elapsedSeconds);
        }
        public bool SaveGuidedFocusDraft(string id,string practiceChoice,string reflection,int elapsedSeconds) => Change(next=>
        {
            var session=GuidedFocusDraftSession(next,id,true);
            UpdateGuidedFocusDraft(session,practiceChoice,reflection,elapsedSeconds);
        });
        public bool StartGuidedFocusSession(string id,string practiceChoice)
        {
            lock(gate)
            {
                RequireOpen();
                var current=state.Sessions.FirstOrDefault(s=>s.SessionId==id);
                if(MissionPlantUnavailable(state)!=null){LastError=MissionPlantUnavailable(state);return false;}
                // A delayed start callback must not overwrite a later selection or restart the timer.
                if(FocusMissionCatalog.IsGuided(current)
                    && (current.Status==ParticipationState.InProgress || current.Status==ParticipationState.Paused))
                { LastError=null;return true; }
                return Change(next=>
                {
                    var session=GuidedFocusDraftSession(next,id,true);
                    if(session.Status!=ParticipationState.Selected)throw new InvalidOperationException("이미 시작하거나 마친 실습이에요.");
                    session.Status=ParticipationState.InProgress;session.StartedUtc=SessionTime(session.SelectedUtc);
                    UpdateFocusAnswer(session,0,practiceChoice,false);
                });
            }
        }
        public bool CompleteGuidedFocusPractice(string id,string practiceChoice,string reflection,int elapsedSeconds)
        {
            lock(gate)
            {
                RequireOpen();
                var current=state.Sessions.FirstOrDefault(s=>s.SessionId==id);
                // Only an explicit move back to practice permits editing an already-confirmed response.
                if(FocusMissionCatalog.IsGuided(current) && current.InstructionStep>=2
                    && (FocusMissionPolicy.IsActive(current)||current.Status==ParticipationState.RewardCommitted)
                    && current.FocusStepRecords.All(record=>!string.IsNullOrEmpty(record.ConfirmedUtc)))
                { LastError=null;return true; }
                return Change(next=>
                {
                    RequireMissionPlant(next);
                    var session=GuidedFocusDraftSession(next,id,false);
                    UpdateGuidedFocusDraft(session,practiceChoice,reflection,elapsedSeconds);
                    ConfirmFocusStep(session,0,practiceChoice,false);
                    ConfirmFocusStep(session,1,reflection,false);
                });
            }
        }
        public bool ShortenGuidedFocusPractice(string id,string practiceChoice,string reflection,int elapsedSeconds)
        {
            lock(gate)
            {
                RequireOpen();
                var current=state.Sessions.FirstOrDefault(item=>item.SessionId==id);
                if(FocusMissionCatalog.IsGuided(current)&&current.InstructionStep>=2&&current.FocusStepRecords[0].EndReason=="too_long"
                    && (FocusMissionPolicy.IsActive(current)||current.Status==ParticipationState.RewardCommitted))
                {LastError=null;return true;}
                return Change(next=>
                {
                    RequireMissionPlant(next);
                    var session=GuidedFocusDraftSession(next,id,false);var practice=session.FocusStepRecords[0];
                    if(string.IsNullOrEmpty(practice.PracticeStartedUtc)||!FocusMissionAdjustmentPolicy.IsTimedPractice(FocusMissionCatalog.FindForSession(session).Steps[0]))
                        throw new InvalidOperationException("실습을 시작한 뒤 줄일 수 있어요.");
                    UpdateGuidedFocusDraft(session,practiceChoice,reflection,elapsedSeconds);
                    ConfirmFocusStep(session,0,practiceChoice,false);ConfirmFocusStep(session,1,reflection,false);
                    practice.EndReason="too_long";practice.ShortenedUtc=SessionTime(practice.PracticeStartedUtc);
                });
            }
        }
        void SkipInapplicableFocusSteps(ActivitySession session)
        {
            if(session.FocusVersion<2)return;
            var definition=FocusMissionCatalog.FindForSession(session);
            while(session.InstructionStep<definition.Steps.Length && !FocusMissionCatalog.IsApplicable(definition,session.FocusStepRecords,session.InstructionStep))
            {
                int step=session.InstructionStep;var record=session.FocusStepRecords[step];
                var previous=DateTimeOffset.Parse(step==0?session.StartedUtc:session.FocusStepRecords[step-1].ConfirmedUtc);
                var confirmed=CurrentUtc;if(confirmed<=previous)confirmed=previous.AddTicks(1);
                record.Value="";record.Skipped=true;record.EndReason="not_applicable";record.ShortenedUtc=null;record.ConfirmedUtc=confirmed.ToString("O");record.UpdatedUtc=record.ConfirmedUtc;
                session.Answers.RemoveAll(a=>a.Key==record.Key);session.InstructionStep++;
            }
        }
        public bool ConfirmFocusStep(string id,int step,string answer=null,bool skipped=false) => Change(next=>
        {
            RequireMissionPlant(next);
            ConfirmFocusStep(FocusActiveSession(next,id),step,answer,skipped);
        });
        void ConfirmFocusStep(ActivitySession session,int step,string answer,bool skipped)
        {
            int count=session.FocusStepRecords.Count;
            if(step<0 || step>=count)throw new ArgumentException("Invalid focus step.");
            var record=session.FocusStepRecords[step];
            // A duplicate tap cannot update an already-advanced response or award anything twice.
            if(step<session.InstructionStep && !string.IsNullOrEmpty(record.ConfirmedUtc))return;
            if(step!=session.InstructionStep || session.FocusStepRecords.Take(step).Any(r=>string.IsNullOrEmpty(r.ConfirmedUtc)))throw new InvalidOperationException("앞선 안내를 먼저 확인해 주세요.");
            if(session.FocusVersion>1)
            {
                var definition=FocusMissionCatalog.FindForSession(session);var instruction=definition.Steps[step];
                if(FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(definition,instruction.Key,answer)!=null)throw new InvalidOperationException("다른 목적의 활동은 별도 활동으로 시작해 주세요.");
                if(instruction.Kind=="draw" && FocusDrawingData.Decode(skipped?"":answer).Count==0)throw new InvalidOperationException("선을 하나 이상 그려주세요.");
                if(instruction.RequiredAnswer && (skipped || string.IsNullOrWhiteSpace(answer)))throw new InvalidOperationException("짧게 입력하거나 제공된 선택지에서 골라주세요.");
            }
            UpdateFocusAnswer(session,step,answer,skipped);
            if(string.IsNullOrEmpty(record.ConfirmedUtc))
            {
                var previous=DateTimeOffset.Parse(step==0?session.StartedUtc:session.FocusStepRecords[step-1].ConfirmedUtc);
                var confirmed=CurrentUtc;if(confirmed<=previous)confirmed=previous.AddTicks(1);
                record.ConfirmedUtc=confirmed.ToString("O");record.UpdatedUtc=record.ConfirmedUtc;
            }
            if(record.EndReason!="too_long")record.EndReason="completed";
            session.InstructionStep=step+1;
            SkipInapplicableFocusSteps(session);
        }
        public bool MoveFocusStep(string id,int step) => Change(next=>
        {
            RequireMissionPlant(next);
            var session=FocusActiveSession(next,id);int reached=session.FocusStepRecords.Count(r=>!string.IsNullOrEmpty(r.ConfirmedUtc));
            int maximum=reached==session.FocusStepRecords.Count?reached+1:reached;
            if(step<0 || step>maximum)throw new InvalidOperationException("아직 수행하지 않은 단계를 건너뛸 수 없어요.");
            if(session.FocusVersion>1 && step<session.FocusStepRecords.Count && !FocusMissionCatalog.IsApplicable(FocusMissionCatalog.FindForSession(session),session.FocusStepRecords,step))throw new InvalidOperationException("선택한 실습 경로의 단계로 이동해 주세요.");
            session.InstructionStep=step;
        });
        public bool SaveFocusReflection(string id,string mood,string note,int stage) => Change(next=>
        {
            var session=FocusActiveSession(next,id);
            if(stage<0 || stage>1 || session.FocusStepRecords.Any(r=>string.IsNullOrEmpty(r.ConfirmedUtc)))throw new InvalidOperationException("실습을 마친 뒤 기록할 수 있어요.");
            if((mood!=null && mood.Length>80) || (note!=null && note.Length>500))throw new ArgumentException("기록 길이를 확인해 주세요.");
            session.Mood=mood;session.Note=note;session.InstructionStep=session.FocusStepRecords.Count+stage;
        });
        public PracticeGrowthRecord GetPracticeGrowth(string sessionId)
        {
            lock (gate) { RequireOpen(); return state.PracticeGrowthRecords.FirstOrDefault(record => record.SessionId == sessionId)?.Copy(); }
        }
        public bool CommitPracticeGrowth(string sessionId)
        {
            lock (gate)
            {
                RequireOpen();
                var session = state.Sessions.FirstOrDefault(item => item.SessionId == sessionId);
                if (session != null && (session.Status == ParticipationState.RewardCommitted
                    || state.PracticeGrowthRecords.Any(record => record.SessionId == sessionId)))
                { LastError = null; return true; }
                bool saved = Change(next => CommitPracticeGrowth(next, Session(next,sessionId)));
                if (saved && pendingDecision != null && pendingDecision.CycleId == state.LastCycleId) pendingDecision = null;
                return saved;
            }
        }
        public bool TryCommitWalkingPracticeGrowth(string sessionId)
        {
            lock (gate)
            {
                RequireOpen();
                try
                {
                    var session = Session(state,sessionId);
                    if (session.MissionId != "free-mission:M15") throw new InvalidOperationException("Not a walking activity.");
                    if (state.PracticeGrowthRecords.Any(record => record.SessionId == sessionId) || session.Status == ParticipationState.RewardCommitted) return true;
                    if (session.RealStepHighWater < (session.WalkingGoalSteps > 0 ? session.WalkingGoalSteps : 300)) return false;
                    return CommitPracticeGrowth(sessionId);
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
        static bool ClaimsPracticeOccurrence(PracticeGrowthRecord record) => record.Outcome != PracticeGrowthOutcome.Ineligible
            && record.Outcome != PracticeGrowthOutcome.AlreadyClaimed;
        void CommitPracticeGrowth(GardenState next, ActivitySession session, IEnumerable<string> eligibleAnimalIds = null)
        {
            if (session.Status == ParticipationState.RewardCommitted || next.PracticeGrowthRecords.Any(record => record.SessionId == session.SessionId)) return;
            RequireMissionPlant(next);
            if (session.Status != ParticipationState.InProgress && session.Status != ParticipationState.Paused)
                throw new InvalidOperationException("Start practice before finishing it.");
            bool focus = FocusMissionPolicy.IsFocus(session), mbct = !focus && MbctPolicy.IsMbct(session.MissionId);
            bool basic = focus && session.FocusRole == FocusMissionCatalog.BasicRole;
            string program = focus ? FocusMissionPolicy.ProgramFor(session) : MbctPolicy.ProgramForMission(session.MissionId);
            bool advancing = mbct && session.CourseOrder == MbctPolicy.NextOrder(next,program);
            if (focus)
            {
                if(basic&&FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(session)!=null)throw new InvalidOperationException("다른 목적의 활동은 기본 회차 완료로 기록할 수 없어요.");
                if (session.FocusStepRecords.Any(record => string.IsNullOrEmpty(record.ConfirmedUtc)))
                    throw new InvalidOperationException("모든 실습 단계를 확인한 뒤 마쳐 주세요.");
                if (session.FocusVersion > 1 && session.MissionId == "free-mission:M15" && session.WalkingGoalSteps == 0)
                    throw new InvalidOperationException("걷기 목표를 먼저 확인해 주세요.");
            }
            else if (mbct)
            {
                var definition = CapstoneDesign.Runtime.MbctContent.Find(session.MissionId);
                if (definition == null || session.InstructionStep < definition.steps.Length)
                    throw new InvalidOperationException("모든 실습 단계를 마쳐 주세요.");
            }
            AdvanceCycle(next,eligibleAnimalIds == null ? cycleCandidates : NormalizeCycleCandidates(eligibleAnimalIds));
            if (basic && !session.FocusReview)
            {
                if (session.FocusOrder != FocusMissionPolicy.NextOrder(next,program)) throw new InvalidOperationException("기본 미션 진행 상태를 확인해 주세요.");
                var unavailable = FocusMissionPolicy.Availability(next,program); if (unavailable != null) throw new InvalidOperationException(unavailable);
            }
            if (advancing)
            {
                var unavailable = MbctPolicy.Availability(next,program); if (unavailable != null) throw new InvalidOperationException(unavailable);
            }
            if (focus && !basic && !session.FocusReview)
            {
                var unavailable=FocusMissionPolicy.AdditionalAvailability(next);if(unavailable!=null)throw new InvalidOperationException(unavailable);
            }
            else if (!focus && !mbct)
            {
                var unavailable=FocusMissionPolicy.DailyAvailability(next,FocusMissionPolicy.RoleFor(session));
                if(unavailable!=null)throw new InvalidOperationException(unavailable);
            }
            // This business cycle belongs to the finished practice. Optional
            // reflection on another day cannot move or repeat its growth claim.
            session.InstanceId = next.LastCycleId;
            string eligibility = focus ? FocusMissionPolicy.RewardReceipt(session) : session.InstanceId + "|" + session.MissionId;
            bool eligible = focus ? !session.FocusReview : (!mbct || advancing) && !next.LegacyCompletedUnknownDate.Contains(session.MissionId);
            bool legacyAdditional = focus && !basic && (next.RewardReceipts.Contains(session.InstanceId + "|" + session.MissionId)
                || next.Sessions.Any(item => item != session && FocusMissionPolicy.IsFocus(item) && item.MissionId == session.MissionId
                    && item.InstanceId == session.InstanceId && item.RewardAmount > 0));
            bool claimed = legacyAdditional || next.RewardReceipts.Contains(eligibility)
                || next.PlantCollection.Decisions.Any(d=>d.SessionToken==WeeklyPlants.Token(session.SessionId))
                || next.PracticeGrowthRecords.Any(record => record.EligibilityKey == eligibility && ClaimsPracticeOccurrence(record));
            var result = new PracticeGrowthRecord {
                SessionId = session.SessionId, EligibilityKey = eligibility, CycleId = session.InstanceId, MissionId = session.MissionId,
                CommittedUtc = Now, Outcome = !eligible ? PracticeGrowthOutcome.Ineligible : claimed ? PracticeGrowthOutcome.AlreadyClaimed : PracticeGrowthOutcome.Grown
            };
            ApplyPracticeGrowth(next,result);
        }
        static void ApplyPracticeGrowth(GardenState next,PracticeGrowthRecord result)
        {
            if (result.Outcome == PracticeGrowthOutcome.Grown)
            {
                CombinedPlants.EnsureGrowing(next,result.CommittedUtc,result.CycleId,result.SessionId);
                result.PlantInstanceId = next.PlantAppearance.Recipe.InstanceId;
                result.BeforeStep = next.PlantAppearance.Recipe.GrowthStep;
                result.BeforeGrowth = GardenPlantVisuals.TotalGrowth(next);
                next.PracticeGrowthRecords.Add(result);
                CapstoneDesign.Plants.GrowthCategory category;
                if(!GardenPlantVisuals.TryCategory(result.MissionId,out category))throw new InvalidOperationException("Unknown plant growth activity category.");
                next.PlantAppearance.Recipe.Counts[(int)category]=checked(next.PlantAppearance.Recipe.Counts[(int)category]+1);
                GardenPlantVisuals.AdvanceMissionGrowth(next);
                result.AfterStep = next.PlantAppearance.Recipe.GrowthStep;
                result.AfterGrowth = GardenPlantVisuals.TotalGrowth(next);
                CombinedPlants.RecordGrowth(next,result,(int)category);
            }
            else next.PracticeGrowthRecords.Add(result);
        }
        public bool CompletePracticeSession(string id)
        {
            lock(gate)
            {
                RequireOpen();
                var session=state.Sessions.FirstOrDefault(item=>item.SessionId==id);
                if(session?.Status==ParticipationState.RewardCommitted){LastError=null;return true;}
                // CompleteSession already commits practice growth and final status in
                // one Change. Preserve any reflection draft from an older app version.
                return CompleteSession(id,session?.Mood,session?.Note);
            }
        }
        public bool CompleteFocusSession(string id,string mood=null,string note=null)
        {
            lock(gate)
            {
                bool saved=Change(next=>
                {
                    var session=Session(next,id);
                    if(!FocusMissionPolicy.IsFocus(session))throw new InvalidOperationException("Unknown focus mission session.");
                    if(session.Status==ParticipationState.RewardCommitted)return;
                    RequireMissionPlant(next);
                    FocusActiveSession(next,id);
                    if(session.FocusStepRecords.Any(r=>string.IsNullOrEmpty(r.ConfirmedUtc)))throw new InvalidOperationException("모든 실습 단계를 확인한 뒤 완료해 주세요.");
                    if(session.FocusVersion>1 && session.MissionId=="free-mission:M15" && session.WalkingGoalSteps==0)throw new InvalidOperationException("걷기 목표를 먼저 확인해 주세요.");
                    if((mood!=null && mood.Length>80) || (note!=null && note.Length>500))throw new ArgumentException("기록 길이를 확인해 주세요.");
                    CommitPracticeGrowth(next,session);
                    bool basic=session.FocusRole==FocusMissionCatalog.BasicRole;
                    string program=FocusMissionPolicy.ProgramFor(session);
                    if(basic && !session.FocusReview)
                    {
                        if(session.FocusOrder!=FocusMissionPolicy.NextOrder(next,program))throw new InvalidOperationException("기본 미션 진행 상태를 확인해 주세요.");
                    }
                    string receipt=FocusMissionPolicy.RewardReceipt(session);
                    session.RewardAmount=0;
                    if(!session.FocusReview && !next.RewardReceipts.Contains(receipt))
                    {
                        AwardPracticeCoins(next,session);
                        next.RewardReceipts.Add(receipt);
                    }
                    session.Mood=mood;session.Note=note;session.Status=ParticipationState.RewardCommitted;session.InstructionStep=session.FocusStepRecords.Count+1;
                    var last=DateTimeOffset.Parse(session.FocusStepRecords.Last().ConfirmedUtc);var ended=CurrentUtc;
                    session.EndedUtc=(ended<last?last:ended).ToString("O");
                    session.PendingPhotoId=null;session.PendingPhotoTarget=null;
                    CameraMissionPolicy.Award(next,session,config.PhotoVerificationNutrient,config.PhotoVerificationDailyLimit);
                    GardenPlantVisuals.Sync(next);
                });
                if(saved && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return saved;
            }
        }
        static ActivitySession Session(GardenState next, string id)
        { return next.Sessions.FirstOrDefault(s => s.SessionId == id) ?? throw new InvalidOperationException("Unknown session."); }
        static void AwardPracticeCoins(GardenState next,ActivitySession session)
        {
            // Growth receipts survive history reset and identify the real eligible
            // practice, including old practice-first sessions awaiting final save.
            var growth=next.PracticeGrowthRecords.FirstOrDefault(record=>record.SessionId==session.SessionId);
            if(growth==null || !ClaimsPracticeOccurrence(growth))return;
            int reward=GardenEconomyPolicy.PracticeReward(session);
            next.Coins=checked(next.Coins+reward);
            session.RewardAmount=reward;
        }
        public bool CancelSelection(string id)
        {
            return Change(next => {
                var selected=Session(next,id);
                if(selected.Status!=ParticipationState.Selected)throw new InvalidOperationException("Started activities must be ended, not discarded.");
                next.Sessions.Remove(selected);
            });
        }
        public bool StartSession(string id)
        {
            return Change(next=>
            {
                RequireMissionPlant(next);
                var session=Session(next,id);
                if(session.Status==ParticipationState.InProgress)return;
                if(session.Status!=ParticipationState.Selected)throw new InvalidOperationException("Invalid participation transition.");
                session.Status=ParticipationState.InProgress;session.StartedUtc=SessionTime(session.SelectedUtc);
                if(FocusMissionPolicy.IsFocus(session))SkipInapplicableFocusSteps(session);
            });
        }
        void AssignWalkingTarget(ActivitySession session,WalkingTarget target)
        {
            if(target==null)throw new ArgumentNullException("target");
            DateTimeOffset from,until;
            if(target.GoalSteps<1000 || target.GoalSteps>10000 || !DateTimeOffset.TryParse(target.QueryStartUtc,out from) || !DateTimeOffset.TryParse(target.QueryEndUtc,out until)
                || CurrentUtc<from || CurrentUtc>=until || (until-from).TotalHours<22 || (until-from).TotalHours>26
                || string.IsNullOrWhiteSpace(target.Basis) || target.Basis.Length>160)throw new InvalidOperationException("Walking target must belong to today.");
            session.WalkingGoalSteps=target.GoalSteps;
            session.WalkingQueryStartUtc=target.QueryStartUtc;session.WalkingQueryEndUtc=target.QueryEndUtc;
            session.WalkingTargetBasis=target.Basis;session.WalkingSleepReduced=target.ReducedForSleep;
        }
        public bool EnsureFocusWalkingTarget(string id,WalkingTarget target) => Change(next=>
        {
            var session=Session(next,id);
            if(!FocusMissionPolicy.IsFocus(session) || session.FocusVersion<2 || session.MissionId!="free-mission:M15" || !FocusMissionPolicy.IsActive(session))throw new InvalidOperationException("Not an active walking mission.");
            // A delayed health refresh or date change must never reinterpret an already selected goal.
            if(session.WalkingGoalSteps>0)return;
            AssignWalkingTarget(session,target);
        });
        public bool StartWalkingSession(string id, WalkingTarget target)
        {
            return Change(next => {
                RequireMissionPlant(next);
                var session=Session(next,id);
                if(session.MissionId!="free-mission:M15")throw new InvalidOperationException("Not a walking activity.");
                if(session.Status==ParticipationState.InProgress || session.Status==ParticipationState.Paused)return;
                if(session.Status!=ParticipationState.Selected)throw new InvalidOperationException("Walking activity already ended.");
                if(session.WalkingGoalSteps==0)AssignWalkingTarget(session,target);
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
            return Change(next => { if(to==ParticipationState.InProgress)RequireMissionPlant(next); var s = Session(next, id); if (s.Status == to) return; if (s.Status != from) throw new InvalidOperationException("Invalid session transition."); s.Status = to; if (string.IsNullOrEmpty(s.StartedUtc)) s.StartedUtc = SessionTime(s.SelectedUtc); });
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
                    RequireMissionPlant(next);
                    if (s.Status != ParticipationState.InProgress && s.Status != ParticipationState.Paused) throw new InvalidOperationException("Start activity before completing.");
                    CommitPracticeGrowth(next,s,eligibleAnimalIds);
                    bool mbct=MbctPolicy.IsMbct(s.MissionId);
                    string program=MbctPolicy.ProgramForMission(s.MissionId);
                    bool advancing=mbct && s.CourseOrder==MbctPolicy.NextOrder(next,program);
                    string receipt = s.InstanceId + "|" + s.MissionId;
                    s.EndedUtc = SessionTime(s.StartedUtc ?? s.SelectedUtc); s.Mood = mood; s.Note = note; s.Status = ParticipationState.RewardCommitted;
                    if ((!mbct || advancing) && !next.RewardReceipts.Contains(receipt) && !next.LegacyCompletedUnknownDate.Contains(s.MissionId))
                    {
                        // Reopening an older course step is a review, not another
                        // paid completion, even though its historical growth path remains.
                        if(s.CourseOrder==0 || (mbct?advancing:s.CourseOrder==next.NextCourseOrder))AwardPracticeCoins(next,s);
                        next.RewardReceipts.Add(receipt);
                    }
                    if(advancing) MbctPolicy.Advance(next,s.MissionId);
                    if(mbct) foreach(var answer in s.Answers) SetAnswer(next.PracticePreferences,answer.Key,answer.Value,Now);
                    if (!mbct && s.CourseOrder == next.NextCourseOrder && s.CourseOrder > 0 && !next.CompletedCourseIds.Contains(s.MissionId))
                    { next.CompletedCourseIds.Add(s.MissionId); next.NextCourseOrder = Math.Min(29, next.NextCourseOrder + 1); }
                    GardenPlantVisuals.Sync(next);
                });
                if(completed && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return completed;
            }
        }
        static void SetAnswer(List<PracticeAnswer> answers,string key,string value,string updatedUtc=null)
        {
            if(string.IsNullOrWhiteSpace(key) || key.Length>80 || key.Contains("|"))throw new ArgumentException("Invalid answer key.");
            if(value==null || value.Length>500)throw new ArgumentException("Answer too long.");
            var answer=answers.FirstOrDefault(a=>a.Key==key);
            if(answer==null){answer=new PracticeAnswer{Key=key};answers.Add(answer);}
            answer.Value=value;if(updatedUtc!=null)answer.UpdatedUtc=updatedUtc;
        }
        public bool SavePracticeStep(string id,int step,string key=null,string value=null)
        {
            return Change(next=>
            {
                var session=Session(next,id);
                if(step!=session.InstructionStep)RequireMissionPlant(next);
                if(!MbctPolicy.IsMbct(session.MissionId) || session.Status!=ParticipationState.InProgress)throw new InvalidOperationException("Practice is not active.");
                if(step<0 || step>20)throw new ArgumentException("Invalid instruction step.");
                if(key!=null)SetAnswer(session.Answers,key,value??"");
                session.InstructionStep=step;
            });
        }
        public bool SkipPracticeStep(string id,int nextStep,string key)
        {
            return Change(next => {
                RequireMissionPlant(next);
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
        public bool SavePreference(string key,string value) => Change(next=>SetAnswer(next.PracticePreferences,key,value??"",Now));
        string EffectiveBusinessCycle()
        {
            string current=CycleFor(state);
            return string.CompareOrdinal(current,state.LastCycleId)>0?current:state.LastCycleId;
        }
        public DailyEmotionRecord GetDailyEmotion(string id)
        {
            lock(gate){RequireOpen();return state.DailyEmotionRecords.FirstOrDefault(record=>record.Id==id)?.Copy();}
        }
        public bool IsDailyEmotionExpired(string id)
        {
            lock(gate)
            {
                RequireOpen();var record=state.DailyEmotionRecords.FirstOrDefault(item=>item.Id==id);
                return record==null || record.CycleId!=EffectiveBusinessCycle();
            }
        }
        public bool BeginDailyEmotion(string cycleId)
        {
            lock(gate)
            {
                RequireOpen();
                if(MissionPlantUnavailable(state)!=null){LastError=MissionPlantUnavailable(state);return false;}
                if(cycleId==EffectiveBusinessCycle() && state.DailyEmotionRecords.Any(record=>record.CycleId==cycleId))
                {LastError=null;return true;}
                bool saved=Change(next=>
                {
                    AdvanceCycle(next,cycleCandidates);
                    if(cycleId!=next.LastCycleId)throw new InvalidOperationException("감정 기록은 해당 날짜에만 작성할 수 있어요.");
                    string unavailable=DailyEmotionPolicy.Availability(next);
                    if(unavailable!=null)throw new InvalidOperationException(unavailable);
                    string id=DailyEmotionPolicy.RecordId(cycleId);
                    if(next.Sessions.Any(session=>session.SessionId==id))throw new InvalidOperationException("감정 기록 식별자를 확인해 주세요.");
                    var basic=FocusMissionPolicy.CompletedPracticeForCycle(next,FocusMissionCatalog.BasicRole,cycleId);
                    var additional=FocusMissionPolicy.CompletedPracticeForCycle(next,FocusMissionCatalog.AdditionalRole,cycleId);
                    var unlocked=DateTimeOffset.Parse(basic.EndedUtc)>DateTimeOffset.Parse(additional.EndedUtc)?basic.EndedUtc:additional.EndedUtc;
                    string started=SessionTime(unlocked);
                    next.DailyEmotionRecords.Add(new DailyEmotionRecord {Id=id,CycleId=cycleId,BasicSessionId=basic.SessionId,
                        AdditionalSessionId=additional.SessionId,UnlockedUtc=unlocked,StartedUtc=started,UpdatedUtc=started});
                });
                if(saved && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return saved;
            }
        }
        DailyEmotionRecord DailyEmotionDraft(GardenState next,string id)
        {
            AdvanceCycle(next,cycleCandidates);
            var record=next.DailyEmotionRecords.FirstOrDefault(item=>item.Id==id)??throw new InvalidOperationException("감정 기록을 먼저 시작해 주세요.");
            if(record.CycleId!=next.LastCycleId)throw new InvalidOperationException("지난 날짜의 감정 기록은 읽기만 할 수 있어요.");
            if(record.IsCompleted)throw new InvalidOperationException("이미 마친 감정 기록이에요.");
            return record;
        }
        void UpdateDailyEmotionDraft(DailyEmotionRecord record,string mood,string note,int stage)
        {
            if(stage<0 || stage>1 || (mood!=null && mood.Length>80) || (note!=null && note.Length>500))
                throw new ArgumentException("기록 길이와 입력 단계를 확인해 주세요.");
            record.Mood=mood??"";record.Note=note??"";record.Stage=stage;record.UpdatedUtc=SessionTime(record.UpdatedUtc);
        }
        public bool SaveDailyEmotionDraft(string id,string mood,string note,int stage) => Change(next=>
        {
            var record=DailyEmotionDraft(next,id);UpdateDailyEmotionDraft(record,mood,note,stage);
        });
        static bool BackfillPlantMemoMissions(GardenState source)
        {
            bool changed=false;
            foreach(var memo in (source.PlantAppearance?.Memos??new List<GardenPlantMemo>()).Concat(source.PlantPhotos.SelectMany(photo=>photo.Memos)))
                changed=CaptureMemoMissions(source,memo)||changed;
            return changed;
        }
        static bool CaptureMemoMissions(GardenState source,GardenPlantMemo memo)
        {
            // A daily ID can be reused after a history reset. Only its exact original
            // completed record proves which two practices accompanied this paper.
            var record=source.DailyEmotionRecords.FirstOrDefault(item=>item.Id==memo.DailyEmotionRecordId
                && item.IsCompleted && item.MemoId==memo.Id && item.CompletedUtc==memo.CreatedUtc
                && item.Mood==memo.Mood && item.Note==memo.Note);
            if(record==null)return false;
            var basic=source.Sessions.FirstOrDefault(item=>item.SessionId==record.BasicSessionId);
            var additional=source.Sessions.FirstOrDefault(item=>item.SessionId==record.AdditionalSessionId);
            if(!MemoMissionSource(basic,record.CycleId,FocusMissionCatalog.BasicRole)
                || !MemoMissionSource(additional,record.CycleId,FocusMissionCatalog.AdditionalRole))return false;
            bool changed=false;
            if(memo.BasicSessionId.Length==0)
            {
                memo.BasicSessionId=basic.SessionId;memo.BasicMissionId=basic.MissionId;
                memo.BasicMissionTitle=basic.MissionTitleSnapshot??"";changed=true;
            }
            if(memo.AdditionalSessionId.Length==0)
            {
                memo.AdditionalSessionId=additional.SessionId;memo.AdditionalMissionId=additional.MissionId;
                memo.AdditionalMissionTitle=additional.MissionTitleSnapshot??"";changed=true;
            }
            return changed;
        }
        static bool MemoMissionSource(ActivitySession session,string cycle,string role) => session!=null
            && session.Status==ParticipationState.RewardCommitted && session.InstanceId==cycle
            && FocusMissionPolicy.RoleFor(session)==role && (!FocusMissionPolicy.IsFocus(session) || !session.FocusReview);
        public bool CompleteDailyEmotion(string id,string mood,string note)
        {
            lock(gate)
            {
                RequireOpen();
                var completed=state.DailyEmotionRecords.FirstOrDefault(record=>record.Id==id);
                // Successful commands remain acknowledgeable after the day changes,
                // including when storage is temporarily unavailable.
                if(completed?.IsCompleted==true){LastError=null;return true;}
                bool saved=Change(next=>
                {
                    RequireMissionPlant(next);
                    var record=DailyEmotionDraft(next,id);
                    if(string.IsNullOrWhiteSpace(mood) && string.IsNullOrWhiteSpace(note))
                        throw new InvalidOperationException("감정을 고르거나 메모를 한 줄 남겨 주세요.");
                    UpdateDailyEmotionDraft(record,mood,note,1);record.CompletedUtc=record.UpdatedUtc;
                    record.RewardVersion=1;record.RewardAmount=0;record.MemoId="";
                    string receipt=DailyEmotionPolicy.GrowthReceipt(record.CycleId);
                    bool alreadyClaimed=next.RewardReceipts.Contains(receipt)
                        || next.PracticeGrowthRecords.Any(growth=>growth.SessionId==id);
                    if(!alreadyClaimed)
                    {
                        next.Coins=checked(next.Coins+GardenEconomyPolicy.EmotionRecordCoins);
                        record.RewardAmount=GardenEconomyPolicy.EmotionRecordCoins;
                        record.MemoId="plant-memo:"+StateCodec.Hash(record.Id);
                        var memo=new GardenPlantMemo {
                            Id=record.MemoId,DailyEmotionRecordId=record.Id,PlantInstanceId=next.PlantAppearance.Recipe.InstanceId,
                            CreatedUtc=record.CompletedUtc,Mood=record.Mood,Note=record.Note };
                        if(!CaptureMemoMissions(next,memo))throw new InvalidOperationException("감정 기록의 기본·추가 미션을 확인해 주세요.");
                        next.PlantAppearance.Memos.Add(memo);
                    }
                    if(!next.RewardReceipts.Contains(receipt))next.RewardReceipts.Add(receipt);
                });
                if(saved && pendingDecision!=null && pendingDecision.CycleId==state.LastCycleId)pendingDecision=null;
                return saved;
            }
        }
        public bool SaveExperience(string id,string type,string eventText,string body,string emotion,string thought)
        {
            return Change(next=>
            {
                if(next.Experiences.Any(e=>e.Id==id))return;
                if(next.PlantCollection.DeletedExperienceTokens.Contains(WeeklyPlants.Token(id)))throw new InvalidOperationException("삭제한 기록을 다시 저장할 수 없어요.");
                if(string.IsNullOrWhiteSpace(id) || id.Contains("|"))throw new ArgumentException("Invalid record id.");
                if(new[]{eventText,body,emotion,thought}.Any(v=>v!=null && v.Length>500))throw new ArgumentException("Record too long.");
                next.Experiences.Add(new ExperienceRecord{Id=id,CreatedUtc=Now,Type=type,Event=eventText??"",Body=body??"",Emotion=emotion??"",Thought=thought??""});
                WeeklyPlants.AddMemory(next,next.PlantCollection.ActiveId,MemoryRecordKind.Experience,id,Now,RecordOffset);
            });
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit test-tool action; use the same atomic wallet save as gameplay.
        public bool AddTestCoins() => Change(next => next.Coins = checked(next.Coins + 100));
        public bool AddTestNutrients() => AddTestCoins();
#endif

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
                    RequireReady();
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
        public int EnvironmentPrice(string environmentId) => GardenEconomyPolicy.EnvironmentPrice(environmentId,config.EnvironmentCost);
        public bool CanPurchaseEnvironment(string environmentId, string slot)
        { slot = Slot(slot); var s = Snapshot; return !string.IsNullOrWhiteSpace(slot) && environmentId != null && environmentId.StartsWith("environment:") && s.Coins >= EnvironmentPrice(environmentId) && !s.Environments.Any(p => p.Slot == slot || p.EnvironmentId == environmentId); }
        public bool PurchaseEnvironment(string requestId, string environmentId, string slot)
        {
            return Change(next =>
            {
                if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId required.");
                Id(environmentId, "environment"); slot = Slot(slot);
                string command="environment|"+environmentId+"|"+slot;
                if(PreviouslyPurchased(next,requestId,command)) return;
                if (string.IsNullOrWhiteSpace(slot) || next.Environments.Any(p => p.Slot == slot || p.EnvironmentId == environmentId)) throw new InvalidOperationException("Placement overlaps or has no slot.");
                int price=EnvironmentPrice(environmentId);
                if (next.Coins < price) throw new InvalidOperationException("코인이 부족해요.");
                next.Coins -= price; next.Environments.Add(new EnvironmentPlacement { EnvironmentId = environmentId, Slot = slot }); next.PurchaseReceipts.Add(requestId); next.PurchaseCommands.Add(new PurchaseCommand { RequestId=requestId, Command=command });
            });
        }
        static bool PreviouslyPurchased(GardenState next,string requestId,string command)
        {
            if(next.PhotoSaveCommands.Any(photo=>photo.RequestId==requestId) || next.PlantStartCommands.Any(start=>start.RequestId==requestId))
                throw new InvalidOperationException("Request id belongs to a plant lifecycle action.");
            if(!next.PurchaseReceipts.Contains(requestId))return false;
            var existing=next.PurchaseCommands.FirstOrDefault(c=>c.RequestId==requestId);
            if(existing==null || existing.Command!=command) throw new InvalidOperationException("Purchase request id has different or unavailable command identity.");
            return true;
        }
        public bool GrowPlant(string requestId, string plantId, string linkedSessionId=null)
        {
            lock (gate)
            {
                RequireOpen();
                try
                {
                    RequireReady();
                    if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId required.");
                    Id(plantId, "plant"); string command="growth|"+plantId;
                    string link=linkedSessionId??"";
                    if(PreviouslyPurchased(state,requestId,command))
                    {
                        var previous=state.GrowthHistory.FirstOrDefault(record=>record.RequestId==requestId);
                        if(previous!=null && previous.LinkedSessionId!=link)throw new InvalidOperationException("Purchase record cannot be relinked by retry.");
                        LastError = null; return true;
                    }
                    throw new InvalidOperationException("식물은 실습을 마치면 자라요. 코인으로 성장시킬 수 없어요.");
                }
                catch (Exception ex) { LastError = ex.Message; return false; }
            }
        }
    }
}
