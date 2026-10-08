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
    public static class MbctPolicy
    {
        public const int TotalPractices=48, PracticesPerWeek=6;
        public static bool IsMbct(string id) => id != null && id.StartsWith("course:MBCT", StringComparison.Ordinal);
        public static string MissionId(int order) => "course:MBCT"+order.ToString("00");
        public static string WeekOf(string cycle)
        {
            var day=DateTime.ParseExact(cycle,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            return day.AddDays(-((int)day.DayOfWeek+6)%7).ToString("yyyy-MM-dd");
        }
        public static string Availability(GardenState state)
        {
            if(state.MbctNextOrder>TotalPractices)return "코스를 마쳤어요. 배운 활동을 다시 해볼 수 있어요.";
            var completed=state.Sessions.Where(s=>IsMbct(s.MissionId) && state.MbctCompletedIds.Contains(s.MissionId)
                && state.RewardReceipts.Contains(s.InstanceId+"|"+s.MissionId)).GroupBy(s=>s.MissionId).Select(g=>g.First()).ToArray();
            if(completed.Any(s=>s.InstanceId==state.LastCycleId))return "오늘 추천 활동을 마쳤어요. 복습하거나 쉬어도 좋아요.";
            if(completed.Count(s=>WeekOf(s.InstanceId)==WeekOf(state.LastCycleId))>=PracticesPerWeek)return "이번 주 6개 활동을 마쳤어요. 쉬거나 복습한 뒤 다음 주에 이어가요.";
            return null;
        }
    }
    public enum ParticipationState { Selected, InProgress, Paused, Ended, RewardCommitted }
    public sealed class ActivitySession
    {
        internal ActivitySession Copy() { var copy=(ActivitySession)MemberwiseClone(); copy.Answers=Answers?.Select(a=>a?.Copy()).ToList(); return copy; }
        public string SessionId, MissionId, InstanceId, SelectedUtc, StartedUtc, EndedUtc;
        public string Mood, Note;
        public int CourseOrder;
        public int InstructionStep;
        public List<PracticeAnswer> Answers = new List<PracticeAnswer>();
        public long RealStepHighWater, VirtualSteps;
        public string StepSource = "";
        public bool GoalNotificationCommitted;
        public ParticipationState Status;
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
        public List<string> MbctCompletedIds = new List<string>();
        public List<PracticeAnswer> PracticePreferences = new List<PracticeAnswer>();
        public List<ExperienceRecord> Experiences = new List<ExperienceRecord>();
        public string LastCycleId = "";
        public List<string> LegacyCompletedUnknownDate = new List<string>();
        public List<string> UnlockIds = new List<string>();
        public List<string> CompletedCourseIds = new List<string>();
        public List<string> RewardReceipts = new List<string>();
        public List<string> PurchaseReceipts = new List<string>();
        public List<PurchaseCommand> PurchaseCommands = new List<PurchaseCommand>();
        public List<DailyDecision> DailyDecisions = new List<DailyDecision>();
        public List<ActivitySession> Sessions = new List<ActivitySession>();
        // Optional for existing schema-1 saves. Missing XML initializes to an empty list.
        public List<DebugWalkPeriod> DebugWalkPeriods = new List<DebugWalkPeriod>();
        public List<EnvironmentPlacement> Environments = new List<EnvironmentPlacement>();
        public List<PlantProgress> Plants = new List<PlantProgress>();
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
            if (s == null || s.SchemaVersion != 1 || !s.MigrationCompleted || s.Nutrient < 0 || s.NextCourseOrder < 1 || s.NextCourseOrder > 29 || string.IsNullOrWhiteSpace(s.InstallationId) || !Finite(s.LegacyGrowth))
                throw new InvalidDataException("Unsupported or invalid garden state; preserved for recovery.");
            Unique(s.LegacyCompletedUnknownDate); Unique(s.UnlockIds); Unique(s.CompletedCourseIds); Unique(s.RewardReceipts); Unique(s.PurchaseReceipts); Unique(s.DiscoveredAnimalIds);
            if(s.CompletedCourseIds.Count != s.NextCourseOrder-1) throw new InvalidDataException("Course progress inconsistent.");
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
                if (!(Typed(a.MissionId,"course") || Typed(a.MissionId,"free-mission")) || !s.DailyDecisions.Any(d=>d.CycleId==a.InstanceId) || !Enum.IsDefined(typeof(ParticipationState),a.Status) || a.CourseOrder < 0 || a.CourseOrder > (MbctPolicy.IsMbct(a.MissionId)?MbctPolicy.TotalPractices:28) || !Timestamp(a.SelectedUtc)) throw new InvalidDataException("Invalid session.");
                if(a.InstructionStep<0 || a.Answers==null || a.Answers.Any(x=>x==null || x.Value==null))throw new InvalidDataException("Invalid practice draft.");
                if(MbctPolicy.IsMbct(a.MissionId) && (a.CourseOrder<1 || a.MissionId!=MbctPolicy.MissionId(a.CourseOrder)))throw new InvalidDataException("MBCT mission/order mismatch.");
                Unique(a.Answers.Select(x=>x.Key));
                if (a.CourseOrder > 0 && !Typed(a.MissionId,"course")) throw new InvalidDataException("Invalid session course type.");
                if ((a.Status == ParticipationState.InProgress || a.Status == ParticipationState.Paused || a.Status == ParticipationState.RewardCommitted) && !Timestamp(a.StartedUtc)) throw new InvalidDataException("Session start missing.");
                if ((a.Status == ParticipationState.Ended || a.Status == ParticipationState.RewardCommitted) && !Timestamp(a.EndedUtc)) throw new InvalidDataException("Session end missing.");
                if(a.RealStepHighWater<0 || a.VirtualSteps<0 || a.RealStepHighWater>long.MaxValue-a.VirtualSteps)throw new InvalidDataException("Invalid session steps.");
            }
            if (s.Environments.Any(e=>!Typed(e.EnvironmentId,"environment") || e.Slot != e.Slot.Trim().ToLowerInvariant()) || s.Plants.Any(p=>!Typed(p.PlantId,"plant") || !Finite(p.Growth) || p.Growth < 0) || s.CompletedCourseIds.Any(id=>!Typed(id,"course")) || s.DiscoveredAnimalIds.Any(id=>!Typed(id,"animal"))) throw new InvalidDataException("Invalid permanent garden records.");
            if (s.PurchaseCommands.Any(c=>string.IsNullOrWhiteSpace(c.Command) || !s.PurchaseReceipts.Contains(c.RequestId))) throw new InvalidDataException("Purchase identity mismatch.");
        }
        static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
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
            copy.LegacyCompletedUnknownDate=new List<string>(value.LegacyCompletedUnknownDate);
            copy.UnlockIds=new List<string>(value.UnlockIds);copy.CompletedCourseIds=new List<string>(value.CompletedCourseIds);
            copy.RewardReceipts=new List<string>(value.RewardReceipts);copy.PurchaseReceipts=new List<string>(value.PurchaseReceipts);
            copy.DiscoveredAnimalIds=new List<string>(value.DiscoveredAnimalIds);
            copy.PracticePreferences=value.PracticePreferences.Select(a=>a.Copy()).ToList();
            copy.Experiences=value.Experiences.Select(e=>e.Copy()).ToList();
            copy.PurchaseCommands=value.PurchaseCommands.Select(c=>c.Copy()).ToList();
            copy.DailyDecisions=value.DailyDecisions.Select(d=>d.Copy()).ToList();
            copy.Sessions=value.Sessions.Select(a=>a.Copy()).ToList();
            copy.DebugWalkPeriods=value.DebugWalkPeriods.Select(d=>d.Copy()).ToList();
            copy.Environments=value.Environments.Select(e=>e.Copy()).ToList();
            copy.Plants=value.Plants.Select(p=>p.Copy()).ToList();return copy;
        }
        public static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
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
        public string LastError { get; private set; }
        public long SnapshotReads { get; private set; }
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
                        loaded = new GardenState { MigrationCompleted = true, Nutrient = Math.Max(0, legacy.Nutrient), GardenXp = legacy.GardenXp, LegacyGrowth = legacy.Growth, LastUnlock = legacy.LastUnlock };
                        loaded.LegacyCompletedUnknownDate.AddRange(legacy.CompletedMissionIds.Distinct());
                        loaded.UnlockIds.AddRange(legacy.UnlockIds.Distinct());
                        store.Save(loaded);
                    }
                    StateCodec.Validate(loaded); state = loaded; LastError = null; return true;
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
        string Now { get { return clock.UtcNow.ToString("O"); } }
        public string CurrentCycleId
        {
            get { return TimeZoneInfo.ConvertTime(clock.UtcNow, clock.TimeZone).DateTime.AddHours(-config.ResetHourLocal).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); }
        }
        public bool RefreshCycle(IEnumerable<string> eligibleAnimalIds)
        {
            lock (gate)
            {
                RequireOpen(); string cycle = CurrentCycleId;
                if (string.CompareOrdinal(cycle, state.LastCycleId) <= 0) return true;
                var candidates = (eligibleAnimalIds ?? new string[0]).Where(id => id != null && id.StartsWith("animal:", StringComparison.Ordinal)).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
                if(pendingDecision == null || pendingDecision.CycleId != cycle)
                {
                    string hash = StateCodec.Hash(state.InstallationId + "|" + cycle);
                    uint roll = Convert.ToUInt32(hash.Substring(0, 8), 16);
                    string visitor = candidates.Length > 0 && roll % 100 < config.VisitorChancePercent ? candidates[Convert.ToUInt32(hash.Substring(8, 8), 16) % candidates.Length] : "";
                    pendingDecision = new DailyDecision { CycleId=cycle, VisitorId=visitor, TimeZoneId=clock.TimeZone.Id, AppliedUtc=Now };
                }
                bool committed = Change(next =>
                {
                    var decision=pendingDecision;
                    next.LastCycleId = cycle;
                    next.DailyDecisions.Add(new DailyDecision { CycleId=decision.CycleId, VisitorId=decision.VisitorId, TimeZoneId=decision.TimeZoneId, AppliedUtc=decision.AppliedUtc });
                    if(decision.VisitorId.Length>0 && !next.DiscoveredAnimalIds.Contains(decision.VisitorId)) next.DiscoveredAnimalIds.Add(decision.VisitorId);
                });
                if(committed) pendingDecision=null;
                return committed;
            }
        }
        static void Id(string id, string prefix) { if (!StateCodec.Typed(id,prefix)) throw new ArgumentException("Expected namespaced " + prefix + " id."); }
        public bool BeginSession(string sessionId, string missionId, int courseOrder = 0)
        {
            return Change(next =>
            {
                if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Contains("|")) throw new ArgumentException("sessionId required.");
                var existing=next.Sessions.FirstOrDefault(s=>s.SessionId==sessionId);
                if(existing!=null) { if(existing.MissionId!=missionId || existing.CourseOrder!=courseOrder) throw new InvalidOperationException("Session id already belongs to another activity."); return; }
                if(next.Sessions.Any(s=>s.Status==ParticipationState.Selected || s.Status==ParticipationState.InProgress || s.Status==ParticipationState.Paused)) throw new InvalidOperationException("An activity is already in progress.");
                if (!(StateCodec.Typed(missionId,"course") || StateCodec.Typed(missionId,"free-mission"))) throw new ArgumentException("Mission id must be typed.");
                bool mbct=MbctPolicy.IsMbct(missionId);
                if(mbct && missionId!=MbctPolicy.MissionId(courseOrder))throw new ArgumentException("MBCT mission/order mismatch.");
                if (courseOrder < 0 || courseOrder > (mbct?MbctPolicy.TotalPractices:28) || (courseOrder > 0 && !missionId.StartsWith("course:")) || (missionId.StartsWith("course:") && courseOrder==0)) throw new ArgumentException("Invalid course order.");
                if(courseOrder>(mbct?next.MbctNextOrder:next.NextCourseOrder)) throw new InvalidOperationException("Future course step is not yet available.");
                if(next.Sessions.Any(s=>s.MissionId==missionId && s.CourseOrder!=courseOrder)) throw new InvalidOperationException("Mission course order changed without migration.");
                if (next.LastCycleId.Length == 0) throw new InvalidOperationException("Refresh daily cycle first.");
                if(mbct && courseOrder==next.MbctNextOrder)
                {
                    var unavailable=MbctPolicy.Availability(next);
                    if(unavailable!=null)throw new InvalidOperationException(unavailable);
                }
                next.Sessions.Add(new ActivitySession { SessionId = sessionId, MissionId = missionId, InstanceId = next.LastCycleId, SelectedUtc = Now, Status = ParticipationState.Selected, CourseOrder = courseOrder });
            });
        }
        static ActivitySession Session(GardenState next, string id)
        { return next.Sessions.FirstOrDefault(s => s.SessionId == id) ?? throw new InvalidOperationException("Unknown session."); }
        public bool StartSession(string id) { return Transition(id, ParticipationState.Selected, ParticipationState.InProgress); }
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
        public bool CompleteSession(string id, string mood = null, string note = null)
        {
            return Change(next =>
            {
                var s = Session(next, id); if (s.Status == ParticipationState.RewardCommitted) return;
                if (s.Status != ParticipationState.InProgress && s.Status != ParticipationState.Paused) throw new InvalidOperationException("Start activity before completing.");
                bool mbct=MbctPolicy.IsMbct(s.MissionId);
                bool advancing=mbct && s.CourseOrder==next.MbctNextOrder;
                if(advancing)
                {
                    var unavailable=MbctPolicy.Availability(next);
                    if(unavailable!=null)throw new InvalidOperationException(unavailable);
                    // Charge daily/weekly quota to completion, including sessions resumed after midnight.
                    s.InstanceId=next.LastCycleId;
                }
                string receipt = s.InstanceId + "|" + s.MissionId;
                s.EndedUtc = SessionTime(s.StartedUtc ?? s.SelectedUtc); s.Mood = mood; s.Note = note; s.Status = ParticipationState.RewardCommitted;
                if ((!mbct || advancing) && !next.RewardReceipts.Contains(receipt) && !next.LegacyCompletedUnknownDate.Contains(s.MissionId))
                { next.RewardReceipts.Add(receipt); next.Nutrient = checked(next.Nutrient + config.CompletionNutrient); }
                if(advancing) { next.MbctCompletedIds.Add(s.MissionId); next.MbctNextOrder++; }
                if(mbct) foreach(var answer in s.Answers) SetAnswer(next.PracticePreferences,answer.Key,answer.Value);
                if (!mbct && s.CourseOrder == next.NextCourseOrder && s.CourseOrder > 0 && !next.CompletedCourseIds.Contains(s.MissionId))
                { next.CompletedCourseIds.Add(s.MissionId); next.NextCourseOrder = Math.Min(29, next.NextCourseOrder + 1); }
            });
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
        string SessionTime(string minimum) { DateTimeOffset prior; return DateTimeOffset.TryParse(minimum,out prior) && prior > clock.UtcNow ? prior.ToString("O") : Now; }
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
        public bool GrowPlant(string requestId, string plantId)
        {
            return Change(next =>
            {
                if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("requestId required.");
                Id(plantId, "plant"); string command="growth|"+plantId; if(PreviouslyPurchased(next,requestId,command))return; if (next.Nutrient < config.GrowthCost) throw new InvalidOperationException("Insufficient nutrient.");
                var plant = next.Plants.FirstOrDefault(p => p.PlantId == plantId);
                if (plant == null) { plant = new PlantProgress { PlantId = plantId }; next.Plants.Add(plant); }
                plant.Growth += config.GrowthAmount; next.Nutrient -= config.GrowthCost; next.PurchaseReceipts.Add(requestId); next.PurchaseCommands.Add(new PurchaseCommand { RequestId=requestId, Command=command });
            });
        }
    }
}
