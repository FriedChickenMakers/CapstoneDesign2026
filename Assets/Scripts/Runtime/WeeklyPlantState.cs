using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;

namespace CapstoneDesign.Runtime.LocalState
{
    public enum MemoryRecordKind { None, SessionReflection, Experience }
    public sealed class MemoryOrganState
    {
        public string OrganId, RecordId, CreatedUtc;
        public MemoryRecordKind Kind;
        public int SlotIndex, UtcOffsetMinutes, StyleVersion = 1;
        internal MemoryOrganState Copy() => (MemoryOrganState)MemberwiseClone();
    }
    public sealed class PlantInstanceState
    {
        public string Id, CreatedUtc, CompletedUtc;
        public int Ordinal;
        public bool Legacy;
        public float LegacyGrowth;
        public PlantRecipe Recipe;
        public List<MemoryOrganState> Memories = new List<MemoryOrganState>();
        internal PlantInstanceState Copy()
        {
            var copy=(PlantInstanceState)MemberwiseClone();
            copy.Recipe=GardenPlantVisuals.CopyRecipe(Recipe);
            copy.Memories=Memories.Select(m=>m.Copy()).ToList();return copy;
        }
    }
    public sealed class PlantGrowthDecision
    {
        // Keep only an opaque completion token after detailed records are deleted.
        public string SessionToken, PlantId, CycleId, CreatedUtc;
        public bool Granted;
        public int Step, Category;
        // Version 0 is an existing completion: its saved geometry/scale is unchanged.
        public int AccountingVersion, AccountingSequence, AwardedUnits;
        public string SettledParticipationToken, ParticipationEligibilityToken;
        internal PlantGrowthDecision Copy() => (PlantGrowthDecision)MemberwiseClone();
    }
    public sealed class ParticipationGrowthPolicy
    {
        // Reversible engineering defaults, not clinical dose or final reward tuning.
        public const int NormalCompletionUnits = 100;
        public int Version = 1, PartialUnits = 20, DailyPartialUnits = 60, OutstandingPlantUnits = 100;
        internal ParticipationGrowthPolicy Copy() => (ParticipationGrowthPolicy)MemberwiseClone();
    }
    public sealed class ParticipationGrowthDecision
    {
        // These hashes deliberately outlive deletion of detailed mission history.
        public string SessionToken, EligibilityToken, PlantId, CycleId, CreatedUtc, Reason;
        public int Units, Category, AccountingSequence, ConfiguredPartialUnits, DailyLimitUnits, OutstandingLimitUnits, PolicyVersion = 1;
        internal ParticipationGrowthDecision Copy() => (ParticipationGrowthDecision)MemberwiseClone();
    }
    public sealed class PlantCollectionState
    {
        public int PolicyVersion = 1;
        public string ActiveId;
        public List<PlantInstanceState> Plants = new List<PlantInstanceState>();
        public List<PlantGrowthDecision> Decisions = new List<PlantGrowthDecision>();
        public List<string> DeletedExperienceTokens = new List<string>();
        public ParticipationGrowthPolicy ParticipationPolicy = new ParticipationGrowthPolicy();
        public List<ParticipationGrowthDecision> ParticipationDecisions = new List<ParticipationGrowthDecision>();
        internal PlantCollectionState Copy() => new PlantCollectionState {
            PolicyVersion=PolicyVersion,ActiveId=ActiveId,Plants=Plants.Select(p=>p.Copy()).ToList(),
            Decisions=Decisions.Select(d=>d.Copy()).ToList(),DeletedExperienceTokens=new List<string>(DeletedExperienceTokens),
            ParticipationPolicy=ParticipationPolicy.Copy(),ParticipationDecisions=ParticipationDecisions.Select(d=>d.Copy()).ToList() };
    }
    public static class WeeklyPlants
    {
        public const int Target = 21, DailyLimit = 3, PageSize = 21;
        public static string Token(string id) => StateCodec.Hash("plant-completion-v1|"+id);
        public static PlantInstanceState Active(GardenState state) => state.PlantCollection?.Plants.FirstOrDefault(p=>p.Id==state.PlantCollection.ActiveId);
        public static int Today(GardenState state) => state.PlantCollection?.Decisions.Count(d=>d.Granted && d.CycleId==state.LastCycleId)??0;
        public static string Title(PlantInstanceState p) => p.Legacy?"이전 정원의 식물":p.Ordinal+"번째 식물";
        public static bool Migrate(GardenState state,string now,bool preserveLegacy)
        {
            if(state.SchemaVersion==2)return false;
            if(state.SchemaVersion!=1 || state.PlantCollection!=null)throw new InvalidDataException("Unsupported plant migration.");
            GardenPlantVisuals.Sync(state);
            var collection=new PlantCollectionState();state.PlantCollection=collection;
            if(preserveLegacy)collection.Plants.Add(new PlantInstanceState {Id=GardenPlantVisuals.InstanceId,Ordinal=0,Legacy=true,
                CreatedUtc=now,LegacyGrowth=GardenPlantVisuals.TotalGrowth(state),Recipe=GardenPlantVisuals.CopyRecipe(state.PlantAppearance.Recipe)});
            NewPlant(state,now);state.SchemaVersion=2;return true;
        }
        static PlantInstanceState NewPlant(GardenState state,string now)
        {
            int ordinal=state.PlantCollection.Plants.Where(p=>!p.Legacy).Select(p=>p.Ordinal).DefaultIfEmpty(0).Max()+1;
            string id="weekly-"+ordinal.ToString(CultureInfo.InvariantCulture);
            var recipe=PlantGenerator.Create(state.InstallationId,id,Target,PlantGrowthMode.MissionCompletions);
            recipe.MeshVersion=PlantGeometry.CurrentMeshVersion;
            var plant=new PlantInstanceState{Id=id,Ordinal=ordinal,CreatedUtc=now,Recipe=recipe};
            state.PlantCollection.Plants.Add(plant);state.PlantCollection.ActiveId=id;return plant;
        }
        public static bool UpgradeFlowerLayout(GardenState state)
        {
            bool changed=false;
            foreach(var plant in state.PlantCollection.Plants.Where(p=>!p.Legacy && p.Recipe.MeshVersion>=4 && p.Recipe.MeshVersion<PlantGeometry.CurrentMeshVersion))
            {
                plant.Recipe.MeshVersion=PlantGeometry.CurrentMeshVersion;
                PlantGeometry.Build(plant.Recipe); // A failed layout aborts the migration save.
                changed=true;
            }
            return changed;
        }
        // An occurrence key never includes policy version: changing tuning cannot pay it twice.
        public static string ParticipationKey(ActivitySession session,string cycle)
        {
            if(!FocusMissionPolicy.IsFocus(session))return null;
            string identity=session.FocusRole==FocusMissionCatalog.BasicRole
                ? "basic|"+FocusMissionPolicy.ProgramFor(session)+"|"+session.FocusOrder.ToString(CultureInfo.InvariantCulture)
                : "additional|"+session.MissionId+"|"+cycle;
            return StateCodec.Hash("participation-eligibility|"+identity);
        }
        public static int VisualCreditUnits(GardenState state,string plantId) =>
            state.PlantCollection.Decisions.Where(d=>d.PlantId==plantId && d.AccountingVersion==1).Sum(d=>d.AwardedUnits)
            +state.PlantCollection.ParticipationDecisions.Where(d=>d.PlantId==plantId).Sum(d=>d.Units);
        // Geometry still advances at real completions only. Saved credits add persistent,
        // monotonic physical size, including while the first plant is still a seed.
        public static float VisualScale(GardenState state,string plantId) =>
            1f+.06f*(float)Math.Sqrt((double)VisualCreditUnits(state,plantId)/ParticipationGrowthPolicy.NormalCompletionUnits);
        static int NextAccountingSequence(PlantCollectionState c) => checked(1+Math.Max(
            c.Decisions.Select(d=>d.AccountingSequence).DefaultIfEmpty(0).Max(),
            c.ParticipationDecisions.Select(d=>d.AccountingSequence).DefaultIfEmpty(0).Max()));
        public static void EndParticipation(GardenState state,ActivitySession session,string now)
        {
            if(state.SchemaVersion!=2 || !FocusMissionPolicy.IsFocus(session) || session.FocusReview
                || session.Status!=ParticipationState.Ended || !new[]{"skipped","stopped","alternative"}.Contains(session.FocusEndReason)
                || !session.FocusStepRecords.Any(r=>!string.IsNullOrEmpty(r.PracticeStartedUtc)))return;
            GrowthCategory category;
            if(!GardenPlantVisuals.TryCategory(session.MissionId,out category))return;
            var c=state.PlantCollection;string token=Token(session.SessionId);
            if(c.ParticipationDecisions.Any(d=>d.SessionToken==token))return;
            string key=ParticipationKey(session,state.LastCycleId);
            var plant=Active(state);var policy=c.ParticipationPolicy;int units=0;string reason;
            if(c.ParticipationDecisions.Any(d=>d.EligibilityToken==key && d.Units>0))reason="already-credited";
            else if(c.ParticipationDecisions.Where(d=>d.CycleId==state.LastCycleId).Sum(d=>d.Units)+policy.PartialUnits>policy.DailyPartialUnits)reason="daily-participation-limit";
            else
            {
                if(plant.Recipe.Mature)plant=NewPlant(state,now);
                var settled=new HashSet<string>(c.Decisions.Where(d=>d.SettledParticipationToken!=null).Select(d=>d.SettledParticipationToken));
                int outstanding=c.ParticipationDecisions.Where(d=>d.PlantId==plant.Id && !settled.Contains(d.SessionToken)).Sum(d=>d.Units);
                if(outstanding+policy.PartialUnits>policy.OutstandingPlantUnits)reason="outstanding-participation-limit";
                else {units=policy.PartialUnits;reason="started-then-ended";}
            }
            c.ParticipationDecisions.Add(new ParticipationGrowthDecision {SessionToken=token,EligibilityToken=key,
                PlantId=plant.Id,CycleId=state.LastCycleId,CreatedUtc=now,Units=units,Category=(int)category,Reason=reason,PolicyVersion=policy.Version,
                AccountingSequence=NextAccountingSequence(c),ConfiguredPartialUnits=policy.PartialUnits,
                DailyLimitUnits=policy.DailyPartialUnits,OutstandingLimitUnits=policy.OutstandingPlantUnits});
        }
        public static void Complete(GardenState state,ActivitySession session,string now,int utcOffset)
        {
            if(state.SchemaVersion!=2)return;
            var collection=state.PlantCollection;string token=Token(session.SessionId);
            if(collection.Decisions.Any(d=>d.SessionToken==token))return;
            GrowthCategory category;
            if(!GardenPlantVisuals.TryCategory(session.MissionId,out category))return;
            var plant=Active(state);bool granted=Today(state)<DailyLimit;
            var settled=new HashSet<string>(collection.Decisions.Where(d=>d.SettledParticipationToken!=null).Select(d=>d.SettledParticipationToken));
            string key=ParticipationKey(session,state.LastCycleId);
            var participation=key==null?null:collection.ParticipationDecisions.FirstOrDefault(d=>d.EligibilityToken==key && d.Units>0 && !settled.Contains(d.SessionToken));
            if(granted)
            {
                if(plant.Recipe.Mature)plant=NewPlant(state,now);
                plant.Recipe.Counts[(int)category]++;
                plant.Recipe=PlantGenerator.Advance(plant.Recipe);
                if(plant.Recipe.Mature)plant.CompletedUtc=now;
            }
            collection.Decisions.Add(new PlantGrowthDecision{SessionToken=token,PlantId=plant.Id,CycleId=state.LastCycleId,
                CreatedUtc=now,Granted=granted,Step=plant.Recipe.GrowthStep,Category=(int)category,AccountingVersion=1,AccountingSequence=NextAccountingSequence(collection),
                AwardedUnits=granted?ParticipationGrowthPolicy.NormalCompletionUnits-(participation?.Units??0):0,
                ParticipationEligibilityToken=key,SettledParticipationToken=granted?participation?.SessionToken:null});
            Reflection(state,session,now,utcOffset);
        }
        public static void Reflection(GardenState state,ActivitySession session,string now,int utcOffset)
        {
            if(state.SchemaVersion!=2)return;
            var existing=FindMemory(state,MemoryRecordKind.SessionReflection,session.SessionId);
            bool hasText=!string.IsNullOrWhiteSpace(session.Mood)||!string.IsNullOrWhiteSpace(session.Note);
            if(!hasText) { if(existing!=null)Unlink(existing);return; }
            if(existing!=null)return;
            var decision=state.PlantCollection.Decisions.FirstOrDefault(d=>d.SessionToken==Token(session.SessionId));
            // A deliberate late edit can link an old record; migration never does so.
            AddMemory(state,decision?.PlantId??state.PlantCollection.ActiveId,MemoryRecordKind.SessionReflection,session.SessionId,now,utcOffset);
        }
        public static MemoryOrganState FindMemory(GardenState state,MemoryRecordKind kind,string id) =>
            state.PlantCollection?.Plants.SelectMany(p=>p.Memories).FirstOrDefault(m=>m.Kind==kind && m.RecordId==id);
        public static void AddMemory(GardenState state,string plantId,MemoryRecordKind kind,string recordId,string now,int offset)
        {
            if(FindMemory(state,kind,recordId)!=null)return;
            var plant=state.PlantCollection.Plants.FirstOrDefault(p=>p.Id==plantId);
            if(plant==null || plant.Legacy)throw new InvalidOperationException("기록을 연결할 식물을 선택해 주세요.");
            int slot=plant.Memories.Count;
            plant.Memories.Add(new MemoryOrganState{OrganId=plant.Id+"/memory/"+slot,SlotIndex=slot,Kind=kind,
                RecordId=recordId,CreatedUtc=now,UtcOffsetMinutes=offset});
        }
        public static void Unlink(MemoryOrganState memory) { memory.Kind=MemoryRecordKind.None;memory.RecordId=null; }
        public static void Validate(GardenState state)
        {
            var c=state.PlantCollection;
            if(state.SchemaVersion==1) { if(c!=null)throw new InvalidDataException("Plant collection requires schema 2.");return; }
            if(c==null || c.PolicyVersion!=1 || c.Plants==null || c.Decisions==null || c.DeletedExperienceTokens==null || c.ParticipationDecisions==null || c.ParticipationPolicy==null || c.Plants.Any(p=>p==null) || c.Decisions.Any(d=>d==null))throw new InvalidDataException("Invalid plant collection.");
            if(c.DeletedExperienceTokens.Any(t=>t==null||t.Length!=64) || c.DeletedExperienceTokens.Distinct().Count()!=c.DeletedExperienceTokens.Count
                || state.Experiences.Any(e=>c.DeletedExperienceTokens.Contains(Token(e.Id))))throw new InvalidDataException("Deleted experience restored by retry.");
            if(c.Plants.Select(p=>p.Id).Distinct().Count()!=c.Plants.Count || c.Plants.Count(p=>p.Legacy)>1 || Active(state)==null || Active(state).Legacy)throw new InvalidDataException("Invalid active plant.");
            var recordKeys=new HashSet<string>();int ordinal=0;
            foreach(var p in c.Plants)
            {
                PlantGenerator.Validate(p.Recipe);
                if(p.Id!=p.Recipe.InstanceId || !Timestamp(p.CreatedUtc) || p.Memories==null)throw new InvalidDataException("Invalid plant identity.");
                if(p.Legacy)
                {
                    if(p.Id!=GardenPlantVisuals.InstanceId || p.Ordinal!=0 || p.Memories.Count!=0 || p.LegacyGrowth!=GardenPlantVisuals.TotalGrowth(state)
                        || p.Recipe.BaseSeed!=state.PlantAppearance.Recipe.BaseSeed || p.Recipe.GrowthStep!=state.PlantAppearance.Recipe.GrowthStep || p.Recipe.MeshVersion!=state.PlantAppearance.Recipe.MeshVersion)throw new InvalidDataException("Legacy plant changed.");
                }
                else
                {
                    if(p.Ordinal!=++ordinal || p.Id!="weekly-"+ordinal || p.Recipe.MaxGrowthSteps!=Target || p.Recipe.GrowthMode!=PlantGrowthMode.MissionCompletions
                        || p.Recipe.Counts.Sum()!=p.Recipe.GrowthStep || (p.Recipe.Mature?!Timestamp(p.CompletedUtc):!string.IsNullOrEmpty(p.CompletedUtc)))throw new InvalidDataException("Invalid weekly progress.");
                    if(p.Id!=c.ActiveId && !p.Recipe.Mature)throw new InvalidDataException("Unfinished archived plant.");
                }
                for(int i=0;i<p.Memories.Count;i++)
                {
                    var m=p.Memories[i];
                    if(m==null || m.SlotIndex!=i || m.OrganId!=p.Id+"/memory/"+i || m.StyleVersion!=1 || !Timestamp(m.CreatedUtc)
                        || m.UtcOffsetMinutes< -840 || m.UtcOffsetMinutes>840 || !Enum.IsDefined(typeof(MemoryRecordKind),m.Kind))throw new InvalidDataException("Invalid memory organ.");
                    if(m.Kind==MemoryRecordKind.None) { if(m.RecordId!=null)throw new InvalidDataException("Deleted memory retains reference.");continue; }
                    if(string.IsNullOrWhiteSpace(m.RecordId) || !recordKeys.Add(m.Kind+"|"+m.RecordId))throw new InvalidDataException("Duplicate memory reference.");
                    if(m.Kind==MemoryRecordKind.Experience && !state.Experiences.Any(e=>e.Id==m.RecordId))throw new InvalidDataException("Missing experience.");
                    if(m.Kind==MemoryRecordKind.SessionReflection && !state.Sessions.Any(s=>s.SessionId==m.RecordId && s.Status==ParticipationState.RewardCommitted
                        && (!string.IsNullOrWhiteSpace(s.Mood)||!string.IsNullOrWhiteSpace(s.Note))))throw new InvalidDataException("Missing reflection.");
                }
            }
            ValidateParticipation(state);
            if(Active(state).Ordinal!=ordinal)throw new InvalidDataException("Active plant order mismatch.");
            var seen=new HashSet<string>();var steps=new Dictionary<string,int>();var counts=new Dictionary<string,int[]>();var daily=new Dictionary<string,int>();
            foreach(var d in c.Decisions)
            {
                if(d.SessionToken==null || d.SessionToken.Length!=64 || !seen.Add(d.SessionToken) || !Timestamp(d.CreatedUtc)
                    || !state.DailyDecisions.Any(day=>day.CycleId==d.CycleId) || !c.Plants.Any(p=>p.Id==d.PlantId&&!p.Legacy) || d.Category<0 || d.Category>5)throw new InvalidDataException("Invalid growth decision.");
                if(!steps.ContainsKey(d.PlantId)) {steps[d.PlantId]=0;counts[d.PlantId]=new int[6];}
                if(!daily.ContainsKey(d.CycleId))daily[d.CycleId]=0;
                if(d.Granted) {steps[d.PlantId]++;counts[d.PlantId][d.Category]++;daily[d.CycleId]++;}
                else if(daily[d.CycleId]!=DailyLimit)throw new InvalidDataException("Unjustified daily growth limit.");
                if(d.Step!=steps[d.PlantId] || daily[d.CycleId]>DailyLimit)throw new InvalidDataException("Growth decision mismatch.");
            }
            foreach(var p in c.Plants.Where(p=>!p.Legacy))
                if(p.Recipe.GrowthStep!=(steps.TryGetValue(p.Id,out int step)?step:0) || (counts.ContainsKey(p.Id)&&!p.Recipe.Counts.SequenceEqual(counts[p.Id])))throw new InvalidDataException("Recipe differs from growth decisions.");
        }
        static bool ValidPolicy(int partial,int daily,int outstanding) => partial>0 && partial<ParticipationGrowthPolicy.NormalCompletionUnits
            && daily>=partial && daily<=ParticipationGrowthPolicy.NormalCompletionUnits
            && outstanding>=partial && outstanding<=ParticipationGrowthPolicy.NormalCompletionUnits;
        static void ValidateParticipation(GardenState state)
        {
            var c=state.PlantCollection;var policy=c.ParticipationPolicy;
            if(policy.Version!=1 || !ValidPolicy(policy.PartialUnits,policy.DailyPartialUnits,policy.OutstandingPlantUnits))
                throw new InvalidDataException("Invalid participation growth policy.");
            // Detailed history may be deleted and an old command ID retried. Validate the
            // durable ledger itself, not a new live session that happens to reuse that ID.
            var seen=new HashSet<string>();var sequences=new HashSet<int>();
            foreach(var d in c.ParticipationDecisions)
            {
                if(d==null || !Hash(d.SessionToken) || !seen.Add(d.SessionToken) || !Hash(d.EligibilityToken) || !Timestamp(d.CreatedUtc)
                    || !state.DailyDecisions.Any(day=>day.CycleId==d.CycleId) || !c.Plants.Any(p=>p.Id==d.PlantId&&!p.Legacy)
                    || d.Category<0 || d.Category>5 || d.PolicyVersion!=1 || d.AccountingSequence<1 || !sequences.Add(d.AccountingSequence)
                    || !ValidPolicy(d.ConfiguredPartialUnits,d.DailyLimitUnits,d.OutstandingLimitUnits))
                    throw new InvalidDataException("Invalid participation growth receipt.");
            }
            foreach(var d in c.Decisions)
            {
                if(d.AccountingVersion==0)
                {
                    if(d.AccountingSequence!=0 || d.AwardedUnits!=0 || d.SettledParticipationToken!=null || d.ParticipationEligibilityToken!=null)
                        throw new InvalidDataException("Legacy growth accounting changed.");
                    continue;
                }
                if(d.AccountingVersion!=1 || d.AccountingSequence<1 || !sequences.Add(d.AccountingSequence)
                    || (d.ParticipationEligibilityToken!=null && !Hash(d.ParticipationEligibilityToken)))throw new InvalidDataException("Unknown growth accounting.");
            }
            if(sequences.Count>0 && (sequences.Max()!=sequences.Count || sequences.Min()!=1))throw new InvalidDataException("Growth accounting event missing.");
            var events=new SortedDictionary<int,object>();
            foreach(var d in c.ParticipationDecisions)events.Add(d.AccountingSequence,d);
            foreach(var d in c.Decisions.Where(d=>d.AccountingVersion==1))events.Add(d.AccountingSequence,d);
            var eligibility=new HashSet<string>();var paid=new Dictionary<string,ParticipationGrowthDecision>();
            var settled=new HashSet<string>();var daily=new Dictionary<string,int>();var outstanding=new Dictionary<string,int>();
            foreach(var entry in events)
            {
                var participation=entry.Value as ParticipationGrowthDecision;
                if(participation!=null)
                {
                    var d=participation;
                    if(!daily.ContainsKey(d.CycleId))daily[d.CycleId]=0;
                    if(!outstanding.ContainsKey(d.PlantId))outstanding[d.PlantId]=0;
                    string reason=eligibility.Contains(d.EligibilityToken)?"already-credited"
                        : daily[d.CycleId]+d.ConfiguredPartialUnits>d.DailyLimitUnits?"daily-participation-limit"
                        : outstanding[d.PlantId]+d.ConfiguredPartialUnits>d.OutstandingLimitUnits?"outstanding-participation-limit"
                        : "started-then-ended";
                    int expected=reason=="started-then-ended"?d.ConfiguredPartialUnits:0;
                    if(d.Reason!=reason || d.Units!=expected)throw new InvalidDataException("Participation amount or limit reason differs from its ledger.");
                    if(d.Units>0)
                    {
                        eligibility.Add(d.EligibilityToken);paid.Add(d.SessionToken,d);
                        daily[d.CycleId]+=d.Units;outstanding[d.PlantId]+=d.Units;
                    }
                    continue;
                }
                var completion=(PlantGrowthDecision)entry.Value;
                var previous=completion.Granted&&completion.ParticipationEligibilityToken!=null
                    ? paid.Values.FirstOrDefault(p=>p.EligibilityToken==completion.ParticipationEligibilityToken&&!settled.Contains(p.SessionToken)):null;
                if(completion.SettledParticipationToken!=previous?.SessionToken
                    || completion.AwardedUnits!=(completion.Granted?ParticipationGrowthPolicy.NormalCompletionUnits-(previous?.Units??0):0))
                    throw new InvalidDataException("Completion growth total differs from A settlement.");
                if(previous!=null) {settled.Add(previous.SessionToken);outstanding[previous.PlantId]-=previous.Units;}
            }
        }
        static bool Hash(string text) => text!=null && text.Length==64 && text.All(c=>"0123456789ABCDEF".Contains(c));
        static bool Timestamp(string text) => DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _);
    }

    public sealed partial class GardenStateService
    {
        int RecordOffset => (int)clock.TimeZone.GetUtcOffset(CurrentUtc).TotalMinutes;
        public bool UpdateReflection(string id,string mood,string note) => Change(next=>
        {
            var session=Session(next,id);
            if(session.Status!=ParticipationState.RewardCommitted || (mood?.Length??0)>80 || (note?.Length??0)>500)throw new InvalidOperationException("완료한 활동의 기록을 확인해 주세요.");
            if(session.Mood!=mood || session.Note!=note)next.PrivacyRevision=checked(next.PrivacyRevision+1);
            session.Mood=mood;session.Note=note;WeeklyPlants.Reflection(next,session,Now,RecordOffset);
        });
        public bool UpdateExperience(string id,string eventText,string body,string emotion,string thought) => Change(next=>
        {
            if(new[]{eventText,body,emotion,thought}.Any(v=>(v?.Length??0)>500))throw new ArgumentException("기록 길이를 확인해 주세요.");
            var e=next.Experiences.FirstOrDefault(x=>x.Id==id)??throw new InvalidOperationException("기록을 찾을 수 없어요.");
            if(e.Event!=(eventText??"") || e.Body!=(body??"") || e.Emotion!=(emotion??"") || e.Thought!=(thought??""))next.PrivacyRevision=checked(next.PrivacyRevision+1);
            e.Event=eventText??"";e.Body=body??"";e.Emotion=emotion??"";e.Thought=thought??"";
        });
        public bool DeleteExperience(string id) => Change(next=>
        {
            if(next.Experiences.RemoveAll(e=>e.Id==id)>0)
            {
                next.PrivacyRevision=checked(next.PrivacyRevision+1);
                next.PlantCollection.DeletedExperienceTokens.Add(WeeklyPlants.Token(id));
            }
            var memory=WeeklyPlants.FindMemory(next,MemoryRecordKind.Experience,id);if(memory!=null)WeeklyPlants.Unlink(memory);
        });
    }
}
