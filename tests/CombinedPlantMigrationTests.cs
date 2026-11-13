using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

static class CombinedPlantMigrationTests
{
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    sealed class Clock:IClock
    {
        public DateTimeOffset Now=DateTimeOffset.Parse("2026-11-09T10:00:00Z");
        public DateTimeOffset UtcNow=>Now;public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
    }
    sealed class Store:IStateStore
    {
        public GardenState State;public bool Fail;public int Saves;
        public GardenState Load()=>State==null?null:StateCodec.Clone(State);
        public void Save(GardenState candidate){if(Fail)throw new IOException("Synthetic save failure");State=StateCodec.Clone(candidate);Saves++;}
    }
    static GardenStateService Open(Store store,Clock clock)
    {var service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open(),"Open: "+service.LastError);return service;}
    static void Complete(GardenStateService service,string id,string mission="free-mission:M17")
    {
        Check(service.RefreshCycle(null),service.LastError);
        Check(service.BeginSession(id,mission,mission.StartsWith("course:")?1:0),"Begin: "+service.LastError);
        Check(service.StartSession(id),"Start: "+service.LastError);
        Check(service.CompleteSession(id,null,null),"Complete: "+service.LastError);
    }
    static void NewLifecycle()
    {
        var clock=new Clock();var store=new Store();
        using(var service=Open(store,clock))
        {
            var initial=service.Snapshot;string first=initial.PlantAppearance.Recipe.InstanceId;
            Check(initial.SchemaVersion==3&&initial.PlantCollection.Plants.Count==1&&initial.PlantAppearance.Recipe.MaxGrowthSteps==21&&initial.PlantAppearance.Recipe.MeshVersion==6,"Fresh v6/21 seed missing");
            Check(initial.PlantAppearance.Recipe.Counts.Sum()==0,"New seed inherited old history");
            for(int i=0;i<21;i++){Complete(service,"growth-"+i);clock.Now=clock.Now.AddDays(1);}
            var mature=service.Snapshot;
            Check(mature.PlantAppearance.Recipe.GrowthStep==21&&mature.PlantAppearance.IsCompleted&&!mature.PlantAppearance.IsArchived,"Mature current plant was hidden");
            Check(mature.PlantPhotos.Count==1&&mature.Coins==21*30&&mature.StoredGrowthSteps==0,"Automatic archive/reward mismatch");
            string photo=PlantCodec.Encode(mature.PlantPhotos[0]);int saves=store.Saves;
            Check(service.CompletePracticeSession("growth-20")&&store.Saves==saves,"Duplicate completion wrote or rewarded again");
            Check(service.BeginSession("next","free-mission:M17",0)&&service.StartSession("next"),service.LastError);
            string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
            Check(!service.CompleteSession("next",null,null)&&StateCodec.Encode(service.Snapshot)==before,"Failed new-seed growth changed committed state");
            store.Fail=false;Check(service.CompleteSession("next",null,null),service.LastError);
            var next=service.Snapshot;
            Check(next.PlantAppearance.Recipe.InstanceId!=first&&next.PlantAppearance.Recipe.GrowthStep==1&&next.PlantAppearance.Recipe.Counts.Sum()==1,"Next mission did not start independent seed");
            Check(PlantCodec.Encode(next.PlantPhotos[0])==photo,"New growth changed archived picture");
            Check(next.Coins==22*30&&next.PlantCollection.ParticipationDecisions.Count==0,"Unexpected growth policy/reward");
        }
        using(var reopened=Open(store,clock))Check(reopened.Snapshot.PlantCollection.Plants.Count==2&&reopened.Snapshot.PlantPhotos.Count==1,"Restart lost collection");
    }
    static void SchemaOne()
    {
        var clock=new Clock();var legacy=new GardenState{MigrationCompleted=true,Nutrient=137,LegacyGrowth=.2f};
        GardenPlantVisuals.Sync(legacy);var recipe=PlantCodec.Encode(legacy.PlantAppearance.Recipe);
        var store=new Store{State=legacy,Fail=true};
        using(var failed=new GardenStateService(store,clock,new DemoBalanceConfig()))Check(!failed.Open()&&store.State.SchemaVersion==1&&PlantCodec.Encode(store.State.PlantAppearance.Recipe)==recipe,"Failed migration changed source");
        store.Fail=false;
        using(var service=Open(store,clock))
        {
            var state=service.Snapshot;
            Check(state.SchemaVersion==3&&state.Coins==137&&state.LegacyGrowth==.2f,"Legacy wallet/growth changed");
            Check(PlantCodec.Encode(state.PlantAppearance.Recipe)==recipe&&state.PlantAppearance.Recipe.MaxGrowthSteps==12,"Legacy recipe was rerolled or rescaled");
            Check(state.PlantAppearance.StartedUtc==""&&state.PlantCollection.Plants.Count==1,"Unknown start was fabricated");
            Complete(service,"legacy-step");
            Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep==5&&service.Snapshot.Coins==137+30,"Legacy plant did not keep remaining 12-stage path");
        }
        int count=store.State.PlantCollection.Plants.Count;
        using(var again=Open(store,clock))Check(again.Snapshot.PlantCollection.Plants.Count==count,"Migration repeated individual");
    }
    static void SchemaTwo()
    {
        var clock=new Clock();string now=clock.Now.ToString("O"),cycle="2026-11-09";
        var old=new GardenState{MigrationCompleted=true,SchemaVersion=2,Nutrient=71,LastCycleId=cycle};GardenPlantVisuals.Sync(old);
        old.DailyDecisions.Add(new DailyDecision{CycleId=cycle,AppliedUtc=now,TimeZoneId="UTC",VisitorId=""});
        var recipe=PlantGenerator.Create(old.InstallationId,"weekly-1",21,PlantGrowthMode.MissionCompletions);recipe.MeshVersion=6;recipe.Counts[0]++;recipe=PlantGenerator.Advance(recipe);
        old.PlantCollection=new PlantCollectionState{ActiveId="weekly-1"};
        old.PlantCollection.Plants.Add(new PlantInstanceState{Id="weekly-1",Ordinal=1,CreatedUtc=now,Recipe=recipe});
        old.PlantCollection.Decisions.Add(new PlantGrowthDecision{SessionToken=WeeklyPlants.Token("old-growth"),PlantId="weekly-1",CycleId=cycle,CreatedUtc=now,Granted=true,Step=1,Category=0,AccountingVersion=1,AccountingSequence=1,AwardedUnits=100});
        old.PlantCollection.ParticipationDecisions.Add(new ParticipationGrowthDecision{SessionToken=WeeklyPlants.Token("old-partial"),EligibilityToken=StateCodec.Hash("old-partial-occurrence"),PlantId="weekly-1",CycleId=cycle,CreatedUtc=now,Reason="started-then-ended",Units=20,Category=1,AccountingSequence=2,ConfiguredPartialUnits=20,DailyLimitUnits=60,OutstandingLimitUnits=100});
        string exact=PlantCodec.Encode(recipe),partial=PlantCodec.Encode(old.PlantCollection.ParticipationDecisions);float scale=1.9f*WeeklyPlants.VisualScale(old,"weekly-1");
        var store=new Store{State=old};
        using(var service=Open(store,clock))
        {
            var state=service.Snapshot;
            Check(state.SchemaVersion==3&&state.Coins==71&&PlantCodec.Encode(state.PlantAppearance.Recipe)==exact,"Texture source changed during migration");
            Check(PlantCodec.Encode(state.PlantCollection.ParticipationDecisions)==partial&&WeeklyPlants.Active(state).RenderScale==scale,"Historical partial growth regressed");
            Complete(service,"after-texture");
            Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep==2&&service.Snapshot.PlantCollection.ParticipationDecisions.Count==1,"New growth repeated old partial policy");
        }
    }
    static void EmotionsAndReset()
    {
        var clock=new Clock();var store=new Store();using(var service=Open(store,clock))
        {
            Complete(service,"p","course:P01");Complete(service,"m");
            string id=DailyEmotionPolicy.RecordId(service.Snapshot.LastCycleId);
            Check(service.BeginDailyEmotion(service.Snapshot.LastCycleId),"Begin emotion: "+service.LastError);
            Check(service.CompleteDailyEmotion(id,"편안함","synthetic memory"),service.LastError);
            var state=service.Snapshot;string memo=state.PlantAppearance.Memos.Single().Id;
            Check(state.Coins==50+30+100&&state.PlantAppearance.Recipe.GrowthStep==2,"Emotion grew plant or wrong coins");
            Check(WeeklyPlants.Active(state).Memories.Single().RecordId==memo,"Emotion leaf missing");
            Check(service.SetPlantCompleted(state.PlantAppearance.Recipe.InstanceId,true),service.LastError);
            Check(service.UpdateDailyEmotion(memo,"고마움","edited synthetic memory"),service.LastError);
            Check(service.Snapshot.PlantPhotos.Single().Memos.Single().Note=="edited synthetic memory","Album retained stale edited text");
            Check(service.DeleteDailyEmotion(memo),service.LastError);
            state=service.Snapshot;Check(state.DailyEmotionRecords.Count==0&&state.PlantAppearance.Memos.Count==0&&state.PlantPhotos.Single().Memos.Count==0&&WeeklyPlants.Active(state).Memories.Single().Kind==MemoryRecordKind.None&&state.Coins==50+30+100,"Deletion leaked text or removed reward");
            string install=state.InstallationId;Check(service.ResetAll("synthetic-full-reset",install),service.LastError);
            Check(service.HasPendingFullReset&&service.Snapshot.PlantCollection.Plants.Count==1&&service.Snapshot.PlantPhotos.Count==0&&service.Snapshot.Coins==0,"Reset did not clear collection");
            Check(service.FinishFullReset("synthetic-full-reset"),service.LastError);
        }
    }
    static void StoredGrowthAndValidation()
    {
        var clock=new Clock();var old=new GardenState{MigrationCompleted=true,Nutrient=29,LegacyGrowth=.3f};GardenPlantVisuals.Sync(old);
        var at=DateTimeOffset.Parse("2026-09-01T10:00:00Z");
        for(int i=0;i<43;i++)
        {
            string cycle=at.AddDays(i).ToString("yyyy-MM-dd");old.LastCycleId=cycle;
            old.DailyDecisions.Add(new DailyDecision{CycleId=cycle,AppliedUtc=at.AddDays(i).ToString("O"),TimeZoneId="UTC",VisitorId=""});
            old.PracticeGrowthRecords.Add(new PracticeGrowthRecord{SessionId="stored-"+i,EligibilityKey=cycle+"|free-mission:M17",CycleId=cycle,MissionId="free-mission:M17",CommittedUtc=at.AddDays(i).ToString("O"),Outcome=PracticeGrowthOutcome.Stored});
        }
        old.StoredGrowthSteps=43;var store=new Store{State=old};
        using(var service=Open(store,clock))
        {
            Check(service.Snapshot.StoredGrowthSteps==43&&service.Snapshot.PlantPhotos.Count==1,"Migration consumed bank or omitted old mature archive");
            Check(service.RefreshCycle(null)&&service.BeginSession("consume-bank","free-mission:M17",0)&&service.StartSession("consume-bank"),service.LastError);
            string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
            Check(!service.CompleteSession("consume-bank",null,null)&&StateCodec.Encode(service.Snapshot)==before,"Failed stored-growth consumption lost credits");
            store.Fail=false;Check(service.CompleteSession("consume-bank",null,null),service.LastError);
            var state=service.Snapshot;
            Check(state.StoredGrowthSteps==0&&state.PlantStartCommands.Sum(c=>c.AppliedGrowthSteps)==43&&state.PlantCollection.Plants.Count==4&&state.PlantPhotos.Count==3,"Stored growth overflow was lost");
            var receipt=state.PracticeGrowthRecords.Single(r=>r.SessionId=="consume-bank");
            Check(receipt.BeforeStep==1&&receipt.AfterStep==2&&state.PlantAppearance.Recipe.GrowthStep==2&&state.Coins==29+30,"Current mission was counted twice with stored credits");
            string stable=StateCodec.Encode(state);Check(service.CompletePracticeSession("consume-bank")&&StateCodec.Encode(service.Snapshot)==stable,"Stored-growth retry changed collection");
            var corrupt=StateCodec.Clone(state);corrupt.PlantCollection.Plants[0].Recipe.GrowthStep--;
            bool rejected=false;try{StateCodec.Validate(corrupt);}catch(Exception){rejected=true;}Check(rejected,"Corrupt canonical recipe was accepted");
            corrupt=StateCodec.Clone(state);corrupt.PlantPhotos[0].Memos.Add(new GardenPlantMemo());
            rejected=false;try{StateCodec.Validate(corrupt);}catch(Exception){rejected=true;}Check(rejected,"Corrupt album projection was accepted");
        }
    }
    static void ArchivedEmotionFailureAndReset()
    {
        var clock=new Clock();var store=new Store();using(var service=Open(store,clock))
        {
            Complete(service,"p","course:P01");Complete(service,"m");
            Check(service.SetPlantCompleted(service.Snapshot.PlantAppearance.Recipe.InstanceId,true),service.LastError);
            string cycle=service.Snapshot.LastCycleId,id=DailyEmotionPolicy.RecordId(cycle);
            Check(service.BeginDailyEmotion(cycle),service.LastError);
            string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
            Check(!service.CompleteDailyEmotion(id,"편안함","archived synthetic memory")&&StateCodec.Encode(service.Snapshot)==before,"Failed mature emotion changed album/reward");
            store.Fail=false;Check(service.CompleteDailyEmotion(id,"편안함","archived synthetic memory"),service.LastError);
            var state=service.Snapshot;string memo=state.PlantAppearance.Memos.Single().Id,source=state.PlantAppearance.Recipe.InstanceId;
            Check(state.PlantPhotos.Single().Memos.Single().Id==memo&&state.Coins==50+30+100&&state.PlantAppearance.Recipe.GrowthStep==2,"Mature emotion did not stay with current plant");
            clock.Now=clock.Now.AddDays(1);Complete(service,"new-growth");
            Check(service.Snapshot.PlantAppearance.Recipe.InstanceId!=source&&service.Snapshot.PlantAppearance.Memos.Count==0,"Old memo moved to new seed");
            Check(service.ResetMissionHistory(),service.LastError);
            Check(service.Snapshot.PlantPhotos.Single().Memos.Single().Note=="archived synthetic memory","History reset erased retained memory");
            before=StateCodec.Encode(service.Snapshot);store.Fail=true;
            Check(!service.DeleteDailyEmotion(memo)&&StateCodec.Encode(service.Snapshot)==before,"Failed delete removed snapshot");
            store.Fail=false;Check(service.UpdateDailyEmotion(memo,"고마움","edited after source reset"),service.LastError);
            Check(service.Snapshot.PlantPhotos.Single().Memos.Single().Note=="edited after source reset","Detached history snapshot was not editable");
            Check(service.DeleteDailyEmotion(memo),service.LastError);
            Check(service.Snapshot.PlantPhotos.Single().Memos.Count==0&&service.Snapshot.PlantCollection.Plants.First(p=>p.Id==source).Memories.Single().Kind==MemoryRecordKind.None,"Archived deletion left private text");
        }
    }
    static void HistoricalPlanResume()
    {
        foreach(bool completeChoices in new[]{false,true})
        {
            var clock=new Clock();var store=new Store();
            using(var service=Open(store,clock))
            {
                Check(service.RefreshCycle(null)&&service.BeginFocusSession("historical-plan","M10")&&service.StartSession("historical-plan"),"Prepare synthetic historical plan");
                var old=service.Snapshot;var session=old.Sessions.Single();
                session.FocusVersion=FocusMissionCatalog.TextureVersion;
                var definition=FocusMissionCatalog.FindForSession(session);
                session.FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord{Step=index,Key=step.Key,Title=step.Title,Prompt=step.Body}).ToList();
                var plan=session.FocusStepRecords[0];
                plan.PlanActivity="차 마시기";plan.PlanTimePeriod=completeChoices?"저녁":"";plan.PlanNote="";
                plan.Value=completeChoices?"저녁 · 차 마시기":"";
                old.SchemaVersion=2;
                var legacyDisplay=new GardenState{MigrationCompleted=true};GardenPlantVisuals.Sync(legacyDisplay);
                old.PlantAppearance=legacyDisplay.PlantAppearance;
                old.PlantCollection=new PlantCollectionState{ActiveId="weekly-1"};
                old.PlantCollection.Plants.Add(new PlantInstanceState{Id="weekly-1",Ordinal=1,CreatedUtc=clock.Now.ToString("O"),Recipe=PlantGenerator.Create(old.InstallationId,"weekly-1",21,PlantGrowthMode.MissionCompletions)});
                store.Save(old);
            }
            string original=PlantCodec.Encode(store.State.Sessions.Single().FocusStepRecords);
            using(var service=Open(store,clock))
            {
                Check(service.Snapshot.SchemaVersion==3&&PlantCodec.Encode(service.Snapshot.Sessions.Single().FocusStepRecords)==original,"Migration preserves complete and incomplete historical plan choices exactly");
                string value=service.Snapshot.Sessions.Single().FocusStepRecords[0].Value;
                Check(service.SaveFocusDraft("historical-plan",0,value),"Unchanged historical plan remains saveable");
                var plan=service.Snapshot.Sessions.Single().FocusStepRecords[0];
                Check(plan.PlanActivity=="차 마시기"&&plan.PlanTimePeriod==(completeChoices?"저녁":"")&&plan.PlanNote=="","Unchanged save retains historical structured choices");
                string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
                Check(!service.SaveFocusDraft("historical-plan",0,"synthetic updated plan")&&StateCodec.Encode(service.Snapshot)==before,"Failed explicit edit preserves old text and choices atomically");store.Fail=false;
                Check(service.SaveFocusDraft("historical-plan",0,"synthetic updated plan"),"Historical plan can be edited through the current simple input");
                plan=service.Snapshot.Sessions.Single().FocusStepRecords[0];
                Check(plan.Value=="synthetic updated plan"&&plan.PlanActivity==null&&plan.PlanTimePeriod==null&&plan.PlanNote==null,"Only an explicit text change clears conflicting historical choices");
            }
            using(var service=Open(store,clock))
            {
                Check(service.Snapshot.Sessions.Single().FocusStepRecords[0].Value=="synthetic updated plan","Edited historical plan survives restart");
                Check(service.ConfirmFocusStep("historical-plan",0,"synthetic updated plan"),"Resumed historical plan can advance after editing");
            }
        }
    }
    static int Main()
    {
        try{NewLifecycle();SchemaOne();SchemaTwo();EmotionsAndReset();StoredGrowthAndValidation();ArchivedEmotionFailureAndReset();HistoricalPlanResume();Console.WriteLine(checks+" combined plant migration/lifecycle checks passed.");return 0;}
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
