using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;

namespace CapstoneDesign.Runtime.LocalState
{
    // Schema 3 owns each individual once in the collection. Appearance/photos are
    // validated compatibility projections for the existing garden and album UI.
    // No wallet, file path, random source or independent transaction lives here.
    public static class CombinedPlants
    {
        public const int SchemaVersion=3;
        public static PlantInstanceState Find(GardenState state,string id) => state.PlantCollection?.Plants.FirstOrDefault(p=>p.Id==id);
        public static bool TranslateLegacyFocus(GardenState state)
        {
            if(state.SchemaVersion!=1)return false;
            bool changed=false;
            foreach(var session in state.Sessions.Where(s=>s.FocusVersion==3)){session.FocusVersion=4;changed=true;}
            return changed;
        }
        public static void InitializeFresh(GardenState state,string id,string now,string cycle)
        {
            state.SchemaVersion=SchemaVersion;state.PlantCollection=new PlantCollectionState();
            state.PlantCollection.ImportedLedgerDigest=LedgerDigest(state.PlantCollection);
            AddNew(state,id,now,cycle);
        }
        static PlantInstanceState AddNew(GardenState state,string id,string now,string cycle)
        {
            if(Find(state,id)!=null)throw new InvalidOperationException("Plant identity was already used.");
            var recipe=PlantGenerator.Create(state.InstallationId,id,WeeklyPlants.Target,PlantGrowthMode.MissionCompletions);
            recipe.MeshVersion=PlantGeometry.CurrentMeshVersion;
            var plant=new PlantInstanceState{Id=id,Ordinal=state.PlantCollection.Plants.Select(p=>p.Ordinal).DefaultIfEmpty(0).Max()+1,
                CreatedUtc=now,StartedCycleId=cycle,Recipe=recipe,RenderScale=GardenPlantVisuals.ScaleForGrowth(0)};
            state.PlantCollection.Plants.Add(plant);state.PlantCollection.ActiveId=id;
            state.PlantAppearance=new GardenPlantAppearance{Recipe=GardenPlantVisuals.CopyRecipe(recipe),StartedUtc=now,StartedCycleId=cycle,
                UsesIndividualGrowth=true,MissionGrowthVersion=1,BaselineProfileCounts=new int[6]};
            return plant;
        }
        static PlantInstanceState FromPhoto(GardenPlantPhoto photo,int ordinal) => new PlantInstanceState {
            Id=photo.SourcePlantInstanceId,Ordinal=ordinal,Legacy=true,CreatedUtc=photo.StartedUtc,StartedCycleId=photo.StartedCycleId,
            CompletedUtc=photo.SavedUtc,SavedCycleId=photo.SavedCycleId,PhotoId=photo.Id,DisplayName=photo.DisplayName,Completed=true,
            LegacyGrowth=photo.Growth,RenderScale=photo.Scale,Recipe=GardenPlantVisuals.CopyRecipe(photo.Recipe),
            BaselineStep=photo.Recipe.GrowthStep,BaselineCounts=(int[])photo.Recipe.Counts.Clone(),Memos=photo.Memos.Select(m=>m.Copy()).ToList()};
        public static bool Migrate(GardenState state,string now,bool fresh,string cycle="")
        {
            if(state.SchemaVersion==SchemaVersion)return false;
            if(state.SchemaVersion!=1 && state.SchemaVersion!=2)throw new InvalidDataException("Unsupported plant migration source.");
            TranslateLegacyFocus(state);
            StateCodec.Validate(state); // Validate source before changing progress or its version.
            int source=state.SchemaVersion;
            if(source==1)
            {
                GardenPlantVisuals.Sync(state);
                var collection=new PlantCollectionState();state.PlantCollection=collection;
                foreach(var photo in state.PlantPhotos)collection.Plants.Add(FromPhoto(photo,collection.Plants.Count+1));
                var appearance=state.PlantAppearance;string id=appearance.Recipe.InstanceId;
                var active=collection.Plants.FirstOrDefault(p=>p.Id==id);
                if(active==null)
                {
                    active=new PlantInstanceState{Id=id,Ordinal=collection.Plants.Count+1,Legacy=true,
                        CreatedUtc=appearance.StartedUtc,StartedCycleId=appearance.StartedCycleId,
                        Recipe=GardenPlantVisuals.CopyRecipe(appearance.Recipe),LegacyGrowth=GardenPlantVisuals.TotalGrowth(state),
                        RenderScale=GardenPlantVisuals.ScaleForGrowth(GardenPlantVisuals.TotalGrowth(state)),Completed=appearance.IsCompleted,
                        BaselineStep=appearance.Recipe.GrowthStep,BaselineCounts=(int[])appearance.Recipe.Counts.Clone(),
                        Memos=appearance.Memos.Select(m=>m.Copy()).ToList()};
                    collection.Plants.Add(active);
                }
                collection.ActiveId=id;appearance.IsArchived=false;
            }
            else
            {
                // The schema-2 validator already verifies the complete historical
                // completion/partial ledger. Retain every unit and saved mesh.
                foreach(var plant in state.PlantCollection.Plants)
                {
                    plant.BaselineStep=plant.Recipe.GrowthStep;plant.BaselineCounts=(int[])plant.Recipe.Counts.Clone();
                    plant.Completed=plant.Legacy||plant.Recipe.Mature;
                    plant.LegacyGrowth=plant.Legacy?GardenPlantVisuals.TotalGrowth(state):.3f*(float)plant.Recipe.Growth;
                    plant.RenderScale=plant.Legacy?GardenPlantVisuals.ScaleForGrowth(plant.LegacyGrowth):1.9f*WeeklyPlants.VisualScale(state,plant.Id);
                    if(plant.Completed && string.IsNullOrEmpty(plant.CompletedUtc))plant.CompletedUtc=now;
                    if(plant.Completed)plant.SavedCycleId=state.PlantCollection.Decisions.LastOrDefault(d=>d.PlantId==plant.Id&&d.Granted&&d.Step==plant.Recipe.GrowthStep)?.CycleId??"";
                    plant.StartedCycleId=""; // A stored UTC time alone does not reveal its original app day.
                }
                var active=WeeklyPlants.Active(state);
                state.PlantAppearance=AppearanceFor(active);
            }
            state.SchemaVersion=SchemaVersion;
            state.PlantCollection.ImportedLedgerDigest=LedgerDigest(state.PlantCollection);
            foreach(var plant in state.PlantCollection.Plants)
            {
                foreach(var memo in plant.Memos)
                    WeeklyPlants.AddMemory(state,plant.Id,MemoryRecordKind.DailyEmotion,memo.Id,memo.CreatedUtc,0);
                if(plant.Completed)EnsurePhoto(state,plant,now,cycle);
            }
            // Old 12-stage state remains its original individual; only a genuinely
            // fresh installation is initialized through InitializeFresh by Open.
            SyncProjections(state);return true;
        }
        static GardenPlantAppearance AppearanceFor(PlantInstanceState plant) => new GardenPlantAppearance {
            Recipe=GardenPlantVisuals.CopyRecipe(plant.Recipe),StartedUtc=plant.CreatedUtc??"",StartedCycleId=plant.StartedCycleId??"",
            IsCompleted=plant.Completed,UsesIndividualGrowth=true,MissionGrowthVersion=1,
            MissionGrowthBaseStep=plant.Recipe.GrowthStep,MissionGrowthBaseGrowth=plant.LegacyGrowth,
            BaselineProfileCounts=(int[])plant.Recipe.Counts.Clone(),Memos=plant.Memos.Select(m=>m.Copy()).ToList()};
        public static void SyncProjections(GardenState state)
        {
            if(state.SchemaVersion!=SchemaVersion)return;
            var active=WeeklyPlants.Active(state);
            if(active!=null)
            {
                if(state.PlantAppearance?.Recipe?.InstanceId!=active.Id)state.PlantAppearance=AppearanceFor(active);
                state.PlantAppearance.Recipe=GardenPlantVisuals.CopyRecipe(active.Recipe);
                state.PlantAppearance.IsArchived=false;state.PlantAppearance.IsCompleted=active.Completed;
                state.PlantAppearance.Memos=active.Memos.Select(m=>m.Copy()).ToList();
            }
            foreach(var photo in state.PlantPhotos)
            {
                var plant=Find(state,photo.SourcePlantInstanceId);
                if(plant==null)throw new InvalidDataException("Photo source is missing from collection.");
                photo.Recipe=GardenPlantVisuals.CopyRecipe(plant.Recipe);photo.Memos=plant.Memos.Select(m=>m.Copy()).ToList();
                photo.Growth=plant.LegacyGrowth;photo.Scale=plant.RenderScale;
            }
        }
        public static void CaptureCurrent(GardenState state,string now,string cycle)
        {
            if(state.SchemaVersion!=SchemaVersion)return;
            var appearance=state.PlantAppearance;var plant=Find(state,appearance?.Recipe?.InstanceId);
            if(plant==null)throw new InvalidDataException("Current plant is not in its collection.");
            if(plant.Id!=state.PlantCollection.ActiveId)throw new InvalidDataException("Current plant identity changed outside lifecycle.");
            plant.Recipe=GardenPlantVisuals.CopyRecipe(appearance.Recipe);
            plant.Completed=appearance.IsCompleted||appearance.Recipe.Mature;appearance.IsCompleted=plant.Completed;appearance.IsArchived=false;
            plant.Memos=appearance.Memos.Select(m=>m.Copy()).ToList();
            foreach(var memo in plant.Memos)WeeklyPlants.AddMemory(state,plant.Id,MemoryRecordKind.DailyEmotion,memo.Id,memo.CreatedUtc,0);
            plant.LegacyGrowth=GardenPlantVisuals.TotalGrowth(state);
            if(!plant.Legacy)plant.RenderScale=1.9f*WeeklyPlants.VisualScale(state,plant.Id);
            if(plant.Completed)EnsurePhoto(state,plant,now,cycle);
            SyncProjections(state);
        }
        static void EnsurePhoto(GardenState state,PlantInstanceState plant,string now,string cycle)
        {
            if(!plant.Completed)return;
            var photo=state.PlantPhotos.FirstOrDefault(p=>p.SourcePlantInstanceId==plant.Id);
            if(photo==null)
            {
                string saved=string.IsNullOrEmpty(plant.CompletedUtc)?now:plant.CompletedUtc;
                if(string.IsNullOrEmpty(saved))throw new InvalidDataException("Completed plant archive time is missing.");
                if(!string.IsNullOrEmpty(plant.CreatedUtc) && DateTimeOffset.Parse(saved)<DateTimeOffset.Parse(plant.CreatedUtc))saved=plant.CreatedUtc;
                string savedCycle=plant.SavedCycleId??"";
                if(savedCycle.Length==0 && !string.IsNullOrEmpty(cycle))savedCycle=cycle;
                if(!string.IsNullOrEmpty(plant.StartedCycleId) && string.CompareOrdinal(savedCycle,plant.StartedCycleId)<0)savedCycle=plant.StartedCycleId;
                photo=new GardenPlantPhoto{Id="plant-photo:"+StateCodec.Hash(plant.Id),SourcePlantInstanceId=plant.Id,
                    StartedUtc=plant.CreatedUtc??"",StartedCycleId=plant.StartedCycleId??"",SavedUtc=saved,SavedCycleId=savedCycle,
                    DisplayName=plant.Recipe.SpeciesId==null?(plant.Recipe.GrowthStep==0?"작은 씨앗":"새싹"):PlantCatalog.Get(plant.Recipe.SpeciesId).Name,
                    Recipe=GardenPlantVisuals.CopyRecipe(plant.Recipe),Growth=plant.LegacyGrowth,Scale=plant.RenderScale,
                    Memos=plant.Memos.Select(m=>m.Copy()).ToList()};
                state.PlantPhotos.Add(photo);
                state.PhotoSaveCommands.Add(new GardenPlantPhotoCommand{RequestId="auto-photo:"+StateCodec.Hash(plant.Id),PlantInstanceId=plant.Id,PhotoId=photo.Id});
            }
            plant.PhotoId=photo.Id;plant.CompletedUtc=photo.SavedUtc;plant.SavedCycleId=photo.SavedCycleId;plant.DisplayName=photo.DisplayName;
        }
        public static void EnsureGrowing(GardenState state,string now,string cycle,string commandId,bool untilGrowing=true)
        {
            var prior=WeeklyPlants.Active(state);
            if(prior==null)throw new InvalidDataException("Current plant is missing.");
            while(prior.Completed)
            {
                EnsurePhoto(state,prior,now,cycle);
                string id="garden-plant:"+StateCodec.Hash(state.InstallationId+"|next|"+prior.Id+"|"+commandId);
                var plant=AddNew(state,id,now,cycle);
                int used=state.PlantStartCommands.Sum(c=>c.AppliedGrowthSteps),applied=Math.Min(state.StoredGrowthSteps,WeeklyPlants.Target);
                var earned=state.PracticeGrowthRecords.Where(r=>r.Outcome==PracticeGrowthOutcome.Stored).Skip(used).Take(applied).ToArray();
                if(earned.Length!=applied)throw new InvalidDataException("Stored growth source is missing.");
                foreach(var record in earned)
                {
                    GrowthCategory category;
                    if(record.MissionId==DailyEmotionPolicy.MissionId)category=GrowthCategory.Reflection;
                    else if(!GardenPlantVisuals.TryCategory(record.MissionId,out category))throw new InvalidDataException("Stored growth category is unknown.");
                    state.PlantAppearance.Recipe.Counts[(int)category]++;
                    state.PlantAppearance.Recipe=PlantGenerator.Advance(state.PlantAppearance.Recipe);
                }
                plant.BaselineStep=state.PlantAppearance.Recipe.GrowthStep;plant.BaselineCounts=(int[])state.PlantAppearance.Recipe.Counts.Clone();
                state.PlantAppearance.MissionGrowthBaseStep=plant.BaselineStep;state.PlantAppearance.MissionGrowthBaseGrowth=.3f*plant.BaselineStep/WeeklyPlants.Target;
                state.PlantAppearance.BaselineProfileCounts=(int[])plant.BaselineCounts.Clone();
                state.StoredGrowthSteps-=applied;
                state.PlantStartCommands.Add(new GardenPlantStartCommand{RequestId="auto-start:"+commandId+":"+plant.Ordinal,ArchivedPlantInstanceId=prior.Id,PlantInstanceId=id,AppliedGrowthSteps=applied});
                CaptureCurrent(state,now,cycle);prior=plant;
                if(!untilGrowing)break;
            }
        }
        static string LedgerDigest(PlantCollectionState collection) => StateCodec.Hash(PlantCodec.Encode(collection.Decisions.Where(d=>d.AccountingVersion<2).ToArray())
            +PlantCodec.Encode(collection.ParticipationDecisions)+PlantCodec.Encode(collection.ParticipationPolicy));
        public static void CaptureHistoricalMemoMetadata(GardenState state)
        {
            if(state.SchemaVersion!=3)return;
            foreach(var photo in state.PlantPhotos)
            {
                var plant=Find(state,photo.SourcePlantInstanceId);
                if(plant!=null&&plant.Id!=state.PlantCollection.ActiveId)plant.Memos=photo.Memos.Select(m=>m.Copy()).ToList();
            }
        }
        public static void RecordGrowth(GardenState state,PracticeGrowthRecord growth,int category)
        {
            var collection=state.PlantCollection;
            if(collection.Decisions.Any(d=>d.SessionToken==WeeklyPlants.Token(growth.SessionId)))throw new InvalidOperationException("Growth command was already applied.");
            int sequence=1+Math.Max(collection.Decisions.Select(d=>d.AccountingSequence).DefaultIfEmpty(0).Max(),collection.ParticipationDecisions.Select(d=>d.AccountingSequence).DefaultIfEmpty(0).Max());
            collection.Decisions.Add(new PlantGrowthDecision{SessionToken=WeeklyPlants.Token(growth.SessionId),PlantId=growth.PlantInstanceId,
                CycleId=growth.CycleId,CreatedUtc=growth.CommittedUtc,Granted=true,Step=growth.AfterStep,Category=category,
                AccountingVersion=2,AccountingSequence=sequence,AwardedUnits=100});
        }
        static bool Timestamp(string value)=>DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _);
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        public static void Validate(GardenState state)
        {
            var c=state.PlantCollection;
            Require(c!=null&&c.PolicyVersion==1&&c.Plants!=null&&c.Plants.Count>0&&c.Decisions!=null&&c.ParticipationDecisions!=null&&c.ParticipationPolicy!=null&&c.DeletedExperienceTokens!=null&&c.DeletedEmotionTokens!=null,"Invalid combined plant collection.");
            Require(c.Plants.All(p=>p!=null)&&c.Plants.Select(p=>p.Id).Distinct().Count()==c.Plants.Count&&WeeklyPlants.Active(state)!=null,"Invalid collection identities.");
            Require(c.ImportedLedgerDigest==LedgerDigest(c),"Historical growth accounting changed.");
            var organs=new HashSet<string>();var links=new HashSet<string>();
            foreach(var plant in c.Plants)
            {
                PlantGenerator.Validate(plant.Recipe);
                Require(plant.Id==plant.Recipe.InstanceId&&plant.Ordinal>=0&&plant.Memories!=null&&plant.Memos!=null&&plant.BaselineCounts!=null&&plant.BaselineCounts.Length==6
                    &&plant.BaselineCounts.All(n=>n>=0)&&plant.BaselineStep>=0&&plant.BaselineStep<=plant.Recipe.GrowthStep
                    &&(!plant.Recipe.Mature||plant.Completed)&&(!plant.Completed||Timestamp(plant.CompletedUtc))
                    &&plant.RenderScale>0&&!float.IsNaN(plant.RenderScale)&&!float.IsInfinity(plant.RenderScale),"Invalid individual plant state.");
                // Schema 2 stored UTC creation but no business day. Do not invent one.
                Require(string.IsNullOrEmpty(plant.CreatedUtc)||Timestamp(plant.CreatedUtc),"Invalid plant creation time.");
                if(!string.IsNullOrEmpty(plant.StartedCycleId))StateCodec.ValidatePlantPeriod(plant.CreatedUtc,plant.StartedCycleId);
                Require(plant.Id==c.ActiveId||plant.Completed||plant.Legacy,"Unfinished archived plant.");
                var newGrowth=c.Decisions.Where(d=>d.PlantId==plant.Id&&d.AccountingVersion==2&&d.Granted).ToArray();
                Require(plant.Recipe.GrowthStep==plant.BaselineStep+newGrowth.Length,"Plant stage differs from committed growth.");
                var counts=(int[])plant.BaselineCounts.Clone();foreach(var d in newGrowth){Require(d.Category>=0&&d.Category<6,"Invalid growth category.");counts[d.Category]++;}
                Require(counts.SequenceEqual(plant.Recipe.Counts),"Plant profile differs from committed growth.");
                for(int i=0;i<plant.Memories.Count;i++)
                {
                    var m=plant.Memories[i];
                    Require(m!=null&&m.SlotIndex==i&&m.OrganId==plant.Id+"/memory/"+i&&organs.Add(m.OrganId)&&m.StyleVersion==1&&Timestamp(m.CreatedUtc)&&m.UtcOffsetMinutes>=-840&&m.UtcOffsetMinutes<=840&&Enum.IsDefined(typeof(MemoryRecordKind),m.Kind),"Invalid memory organ.");
                    if(m.Kind==MemoryRecordKind.None){Require(m.RecordId==null,"Deleted memory retains its source.");continue;}
                    Require(!string.IsNullOrEmpty(m.RecordId)&&links.Add(m.Kind+"|"+m.RecordId),"Duplicate memory source.");
                    if(m.Kind==MemoryRecordKind.DailyEmotion)Require(plant.Memos.Any(memo=>memo.Id==m.RecordId),"Missing emotion snapshot.");
                    if(m.Kind==MemoryRecordKind.Experience)Require(state.Experiences.Any(e=>e.Id==m.RecordId),"Missing memory experience.");
                    if(m.Kind==MemoryRecordKind.SessionReflection)Require(state.Sessions.Any(s=>s.SessionId==m.RecordId&&s.Status==ParticipationState.RewardCommitted&&(!string.IsNullOrWhiteSpace(s.Mood)||!string.IsNullOrWhiteSpace(s.Note))),"Missing memory reflection.");
                }
                var photo=state.PlantPhotos.FirstOrDefault(p=>p.SourcePlantInstanceId==plant.Id);
                Require(!plant.Completed||photo!=null,"Completed plant is absent from album.");
                if(photo!=null)Require(photo.Id==plant.PhotoId&&PlantCodec.Encode(photo.Recipe)==PlantCodec.Encode(plant.Recipe)&&PlantCodec.Encode(photo.Memos)==PlantCodec.Encode(plant.Memos)&&photo.Scale==plant.RenderScale&&photo.Growth==plant.LegacyGrowth,"Album projection differs from individual.");
            }
            Require(c.Decisions.All(d=>d!=null)&&c.Decisions.Select(d=>d.SessionToken).Distinct().Count()==c.Decisions.Count,"Duplicate growth command.");
            foreach(var d in c.Decisions)
            {
                Require(d.SessionToken!=null&&d.SessionToken.Length==64&&Find(state,d.PlantId)!=null&&Timestamp(d.CreatedUtc)&&state.DailyDecisions.Any(day=>day.CycleId==d.CycleId)&&d.Category>=0&&d.Category<6&&d.AccountingVersion>=0&&d.AccountingVersion<=2,"Invalid retained growth ledger.");
                if(d.AccountingVersion==2)Require(d.Granted&&d.AwardedUnits==100&&state.PracticeGrowthRecords.Any(r=>WeeklyPlants.Token(r.SessionId)==d.SessionToken&&r.PlantInstanceId==d.PlantId&&r.AfterStep==d.Step&&r.Outcome==PracticeGrowthOutcome.Grown),"Growth decision has no atomic practice receipt.");
            }
            foreach(var d in c.ParticipationDecisions)Require(d!=null&&d.Units>=0&&Find(state,d.PlantId)!=null&&Timestamp(d.CreatedUtc),"Invalid historical participation credit.");
            Require(c.ParticipationDecisions.Select(d=>d.SessionToken).Distinct().Count()==c.ParticipationDecisions.Count,"Duplicate historical participation credit.");
            Require(c.DeletedExperienceTokens.Distinct().Count()==c.DeletedExperienceTokens.Count&&c.DeletedEmotionTokens.Distinct().Count()==c.DeletedEmotionTokens.Count,"Duplicate deleted-record token.");
            Require(!state.Experiences.Any(e=>c.DeletedExperienceTokens.Contains(WeeklyPlants.Token(e.Id))),"Deleted experience restored.");
            var active=WeeklyPlants.Active(state);
            Require(state.PlantAppearance!=null&&!state.PlantAppearance.IsArchived&&state.PlantAppearance.Recipe.InstanceId==active.Id
                &&PlantCodec.Encode(state.PlantAppearance.Recipe)==PlantCodec.Encode(active.Recipe)
                &&PlantCodec.Encode(state.PlantAppearance.Memos)==PlantCodec.Encode(active.Memos),"Garden projection differs from current individual.");
        }
    }
}
