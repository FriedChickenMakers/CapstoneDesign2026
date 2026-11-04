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
        internal PlantGrowthDecision Copy() => (PlantGrowthDecision)MemberwiseClone();
    }
    public sealed class PlantCollectionState
    {
        public int PolicyVersion = 1;
        public string ActiveId;
        public List<PlantInstanceState> Plants = new List<PlantInstanceState>();
        public List<PlantGrowthDecision> Decisions = new List<PlantGrowthDecision>();
        public List<string> DeletedExperienceTokens = new List<string>();
        internal PlantCollectionState Copy() => new PlantCollectionState {
            PolicyVersion=PolicyVersion,ActiveId=ActiveId,Plants=Plants.Select(p=>p.Copy()).ToList(),
            Decisions=Decisions.Select(d=>d.Copy()).ToList(),DeletedExperienceTokens=new List<string>(DeletedExperienceTokens) };
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
            recipe.MeshVersion=4;
            var plant=new PlantInstanceState{Id=id,Ordinal=ordinal,CreatedUtc=now,Recipe=recipe};
            state.PlantCollection.Plants.Add(plant);state.PlantCollection.ActiveId=id;return plant;
        }
        public static void Complete(GardenState state,ActivitySession session,string now,int utcOffset)
        {
            if(state.SchemaVersion!=2)return;
            var collection=state.PlantCollection;string token=Token(session.SessionId);
            if(collection.Decisions.Any(d=>d.SessionToken==token))return;
            GrowthCategory category;
            if(!GardenPlantVisuals.TryCategory(session.MissionId,out category))return;
            var plant=Active(state);bool granted=Today(state)<DailyLimit;
            if(granted)
            {
                if(plant.Recipe.Mature)plant=NewPlant(state,now);
                plant.Recipe.Counts[(int)category]++;
                plant.Recipe=PlantGenerator.Advance(plant.Recipe);
                if(plant.Recipe.Mature)plant.CompletedUtc=now;
            }
            collection.Decisions.Add(new PlantGrowthDecision{SessionToken=token,PlantId=plant.Id,CycleId=state.LastCycleId,
                CreatedUtc=now,Granted=granted,Step=plant.Recipe.GrowthStep,Category=(int)category});
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
            if(c==null || c.PolicyVersion!=1 || c.Plants==null || c.Decisions==null || c.DeletedExperienceTokens==null || c.Plants.Any(p=>p==null) || c.Decisions.Any(d=>d==null))throw new InvalidDataException("Invalid plant collection.");
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
