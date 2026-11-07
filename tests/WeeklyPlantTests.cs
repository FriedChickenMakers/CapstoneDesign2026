using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

class WeeklyPlantTests
{
    sealed class Clock:IClock
    {
        public DateTimeOffset Time=DateTimeOffset.Parse("2026-11-04T03:00:00Z");
        public TimeZoneInfo Zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic KST",TimeSpan.FromHours(9),"Synthetic KST","Synthetic KST");
        public DateTimeOffset UtcNow=>Time;public TimeZoneInfo TimeZone=>Zone;
    }
    sealed class Store:IStateStore
    {
        public GardenState State;public bool Fail;
        public GardenState Load()=>State==null?null:StateCodec.Clone(State);
        public void Save(GardenState s){if(Fail)throw new IOException("Synthetic failure");State=StateCodec.Decode(StateCodec.Encode(s));}
    }
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static GardenStateService Open(Store store,Clock clock)
    {var s=new GardenStateService(store,clock,new DemoBalanceConfig());Check(s.Open(),"open: "+s.LastError);Check(s.RefreshCycle(null),"cycle");return s;}
    static void Complete(GardenStateService s,string id,string mood=null)
    {
        Check(s.BeginSession(id,"free-mission:M17",0),"begin: "+s.LastError);Check(s.StartSession(id),"start");
        Check(s.CompleteSession(id,mood),"complete: "+s.LastError);
    }
    const string GrowthNow="2026-11-04T03:00:00Z";
    static ActivitySession GrowthSession(string id,string mission="free-mission:M17",bool basic=false,int order=1)
    {
        return new ActivitySession {SessionId=id,MissionId=mission,FocusVersion=FocusMissionCatalog.Version,FocusProgramId="2w",
            FocusRole=basic?FocusMissionCatalog.BasicRole:FocusMissionCatalog.AdditionalRole,FocusOrder=basic?order:0,
            Status=ParticipationState.Ended,FocusEndReason="skipped",FocusStepRecords=new System.Collections.Generic.List<FocusStepRecord> {
                new FocusStepRecord {PracticeStartedUtc=GrowthNow} }};
    }
    static void GrowthDay(GardenState state,int days)
    {
        var time=DateTimeOffset.Parse(GrowthNow).AddDays(days);state.LastCycleId=time.ToString("yyyy-MM-dd");
        state.DailyDecisions.Add(new DailyDecision {CycleId=state.LastCycleId,AppliedUtc=time.ToString("O"),VisitorId="",TimeZoneId="Synthetic KST"});
    }
    static void ValidateRejected(GardenState state,string message)
    {
        bool rejected=false;try{StateCodec.Validate(state);}catch(InvalidDataException){rejected=true;}Check(rejected,message);
    }
    static void ParticipationGrowthChecks()
    {
        var state=Open(new Store(),new Clock()).Snapshot;var plant=WeeklyPlants.Active(state);
        string recipe=PlantCodec.Encode(plant.Recipe);string seed=plant.Recipe.BaseSeed;
        var intro=GrowthSession("synthetic-partial-intro");intro.FocusStepRecords[0].PracticeStartedUtc=null;
        WeeklyPlants.EndParticipation(state,intro,GrowthNow);
        var pause=GrowthSession("synthetic-partial-pause");pause.Status=ParticipationState.Paused;WeeklyPlants.EndParticipation(state,pause,GrowthNow);
        var close=GrowthSession("synthetic-partial-close");close.FocusEndReason=null;WeeklyPlants.EndParticipation(state,close,GrowthNow);
        var review=GrowthSession("synthetic-partial-review");review.FocusReview=true;WeeklyPlants.EndParticipation(state,review,GrowthNow);
        Check(state.PlantCollection.ParticipationDecisions.Count==0,"intro, pause, close and review do not create participation rewards");
        var first=GrowthSession("synthetic-partial-first");WeeklyPlants.EndParticipation(state,first,GrowthNow);
        Check(state.PlantCollection.ParticipationDecisions.Single().Units==20,"actual start and explicit stop gives provisional 20 units");
        Check(PlantCodec.Encode(plant.Recipe)==recipe&&plant.Recipe.BaseSeed==seed&&WeeklyPlants.Today(state)==0,"partial does not fake completed steps, quota, category or seed");
        Check(state.Nutrient==0&&state.Sessions.Count==0,"pure growth receipt grants no nutrient or completion");
        float before=WeeklyPlants.VisualScale(state,plant.Id);
        Check(before>1.02f&&before<1.03f,"partial growth is persistent physical size, not pulse only");
        state=StateCodec.Decode(StateCodec.Encode(state));
        Check(WeeklyPlants.VisualScale(state,plant.Id)==before,"XML reload retains exact partial visual scale");
        WeeklyPlants.EndParticipation(state,first,GrowthNow);
        WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-partial-repeat"),GrowthNow);
        Check(state.PlantCollection.ParticipationDecisions.Count==2&&state.PlantCollection.ParticipationDecisions.Last().Reason=="already-credited","same session and mission-day duplicate payout prevented");
        var completion=GrowthSession("synthetic-partial-complete");WeeklyPlants.Complete(state,completion,GrowthNow,540);
        Check(state.PlantCollection.Decisions.Last().AwardedUnits==80&&WeeklyPlants.VisualCreditUnits(state,plant.Id)==100,"A settlement partial 20 plus completion 80 equals 100");
        Check(WeeklyPlants.VisualScale(state,plant.Id)>before&&WeeklyPlants.Active(state).Recipe.GrowthStep==1,"completion increases scale and real completion count without shrink");
        WeeklyPlants.Complete(state,completion,GrowthNow,540);
        Check(WeeklyPlants.VisualCreditUnits(state,plant.Id)==100,"completion retry cannot repeat top-up");
        WeeklyPlants.Complete(state,GrowthSession("synthetic-partial-new-complete"),GrowthNow,540);
        Check(state.PlantCollection.Decisions.Last().AwardedUnits==100,"new normal repeat retains full growth after first settlement");
        StateCodec.Validate(state);
        var bad=StateCodec.Clone(state);bad.PlantCollection.Decisions.Last().SettledParticipationToken=WeeklyPlants.Token(first.SessionId);
        bad.PlantCollection.Decisions.Last().AwardedUnits=80;ValidateRejected(bad,"one participation credit cannot be settled by two completions");
        bad=StateCodec.Clone(state);bad.PlantCollection.Decisions[0].AwardedUnits=100;ValidateRejected(bad,"invalid A total rejected");
        bad=StateCodec.Clone(state);bad.PlantCollection.ParticipationPolicy.PartialUnits=100;ValidateRejected(bad,"full-sized partial policy rejected");
        bad=StateCodec.Clone(state);bad.PlantCollection.ParticipationDecisions[0].Units=40;ValidateRejected(bad,"forged partial amount rejected by recorded policy");
        bad=StateCodec.Clone(state);bad.PlantCollection.ParticipationDecisions[0].AccountingSequence=99;ValidateRejected(bad,"missing or duplicate event ordering rejected");
        var retuned=StateCodec.Clone(state);retuned.PlantCollection.ParticipationPolicy.PartialUnits=10;
        StateCodec.Validate(retuned);Check(WeeklyPlants.VisualCreditUnits(retuned,plant.Id)==200,"future tuning does not recalculate immutable previous grants");
        // Deleting mission history leaves only opaque payout guards, and therefore cannot replay growth.
        var deleted=StateCodec.Clone(state);deleted.Sessions.Clear();WeeklyPlants.EndParticipation(deleted,GrowthSession("synthetic-partial-after-delete"),GrowthNow);
        Check(deleted.PlantCollection.ParticipationDecisions.Last().Units==0,"history deletion cannot reset participation entitlement");StateCodec.Validate(deleted);
        GrowthDay(state,1);WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-partial-next-day"),"2026-11-05T03:00:00Z");
        Check(state.PlantCollection.ParticipationDecisions.Last().Units==20,"additional mission gets next-day entitlement");StateCodec.Validate(state);
        // Course occurrence is independent of day, mission-code reuse, and tuning version.
        state=Open(new Store(),new Clock()).Snapshot;
        WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-basic-stop","course:P04",true),GrowthNow);
        GrowthDay(state,1);WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-basic-retry","course:P04",true),"2026-11-05T03:00:00Z");
        Check(state.PlantCollection.ParticipationDecisions.Last().Units==0,"basic same occurrence is once across days");
        WeeklyPlants.Complete(state,GrowthSession("synthetic-basic-done","course:P04",true),"2026-11-05T03:00:00Z",540);
        Check(state.PlantCollection.Decisions.Last().AwardedUnits==80,"basic later day completion settles original occurrence");StateCodec.Validate(state);
        state=Open(new Store(),new Clock()).Snapshot;
        for(int i=1;i<=4;i++)WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-budget-"+i,"free-mission:M"+i.ToString("00")),GrowthNow);
        Check(state.PlantCollection.ParticipationDecisions.Take(3).All(d=>d.Units==20)&&state.PlantCollection.ParticipationDecisions.Last().Reason=="daily-participation-limit","partial daily 60-unit limit has explicit denial reason");
        GrowthDay(state,1);
        for(int i=5;i<=7;i++)WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-budget-"+i,"free-mission:M"+i.ToString("00")),"2026-11-05T03:00:00Z");
        Check(state.PlantCollection.ParticipationDecisions.Last().Reason=="outstanding-participation-limit"&&WeeklyPlants.VisualCreditUnits(state,state.PlantCollection.ActiveId)==100,"pending per-plant 100-unit cap prevents unlimited unfinished growth");StateCodec.Validate(state);
        // Daily completion cap never consumes or retracts outstanding participation.
        state=Open(new Store(),new Clock()).Snapshot;
        var delayed=GrowthSession("synthetic-capped-basic","course:P04",true);WeeklyPlants.EndParticipation(state,delayed,GrowthNow);
        for(int i=0;i<3;i++)WeeklyPlants.Complete(state,GrowthSession("synthetic-cap-completion-"+i),GrowthNow,540);
        WeeklyPlants.Complete(state,GrowthSession("synthetic-cap-denied","course:P04",true),GrowthNow,540);
        Check(state.PlantCollection.Decisions.Last().AwardedUnits==0&&state.PlantCollection.Decisions.Last().SettledParticipationToken==null,"capped completion does not consume partial credit");
        GrowthDay(state,1);WeeklyPlants.Complete(state,GrowthSession("synthetic-cap-later","course:P04",true),"2026-11-05T03:00:00Z",540);
        Check(state.PlantCollection.Decisions.Last().AwardedUnits==80,"later eligible completion settles exactly once");StateCodec.Validate(state);
        // A receipt stays on its original plant when completion occurs after rollover.
        state=Open(new Store(),new Clock()).Snapshot;
        WeeklyPlants.EndParticipation(state,GrowthSession("synthetic-rollover-partial","course:P04",true),GrowthNow);
        for(int i=0;i<21;i++)
        {
            if(i>0&&i%3==0)GrowthDay(state,i/3);
            WeeklyPlants.Complete(state,GrowthSession("synthetic-rollover-completion-"+i),DateTimeOffset.Parse(GrowthNow).AddDays(i/3).ToString("O"),540);
        }
        string matureId=state.PlantCollection.ActiveId;float matureScale=WeeklyPlants.VisualScale(state,matureId);
        var nextByPartial=StateCodec.Clone(state);GrowthDay(nextByPartial,7);
        WeeklyPlants.EndParticipation(nextByPartial,GrowthSession("synthetic-next-seed-partial","free-mission:M01"),"2026-11-11T03:00:00Z");
        Check(nextByPartial.PlantCollection.Plants.Count==2&&WeeklyPlants.Active(nextByPartial).Recipe.GrowthStep==0
            &&WeeklyPlants.VisualCreditUnits(nextByPartial,nextByPartial.PlantCollection.ActiveId)==20
            &&WeeklyPlants.VisualScale(nextByPartial,matureId)==matureScale,"partial after maturity starts a real next seed and leaves mature plant unchanged");StateCodec.Validate(nextByPartial);
        string matureRecipe=PlantCodec.Encode(WeeklyPlants.Active(state).Recipe);
        GrowthDay(state,7);
        WeeklyPlants.Complete(state,GrowthSession("synthetic-rollover-settlement","course:P04",true),"2026-11-11T03:00:00Z",540);
        Check(state.PlantCollection.Plants.Count==2&&WeeklyPlants.Active(state).Recipe.GrowthStep==1,"21 real completions still mature then next completion starts next plant");
        Check(state.PlantCollection.Decisions.Last().AwardedUnits==80&&WeeklyPlants.VisualScale(state,matureId)==matureScale
            &&PlantCodec.Encode(state.PlantCollection.Plants[0].Recipe)==matureRecipe,"rollover settlement leaves old plant recipe and scale unchanged");StateCodec.Validate(state);
        // Pre-extension XML carries no retroactive scale change.
        foreach(var d in state.PlantCollection.Decisions){d.AccountingVersion=0;d.AccountingSequence=0;d.AwardedUnits=0;d.SettledParticipationToken=null;d.ParticipationEligibilityToken=null;}
        state.PlantCollection.ParticipationDecisions.Clear();
        string oldXml=StateCodec.Encode(state);
        var xml=new System.Xml.XmlDocument();xml.LoadXml(oldXml);
        var collection=xml.DocumentElement["PlantCollection"];collection.RemoveChild(collection["ParticipationPolicy"]);collection.RemoveChild(collection["ParticipationDecisions"]);
        var oldState=StateCodec.Decode(xml.OuterXml);
        Check(WeeklyPlants.VisualScale(oldState,matureId)==1&&oldState.PlantCollection.ParticipationDecisions.Count==0,"legacy XML defaults retain existing size without fabricated credit");
    }
    static void StartTimedActivity(GardenStateService service,string id)
    {
        Check(service.BeginFocusSession(id,"free-mission:M17"),"begin real focus: "+service.LastError);
        Check(service.StartSession(id),"start introduction");
        Check(service.ConfirmFocusStep(id,0),"confirm preparation");
        Check(service.StartFocusPracticeStep(id,1),"start real timed practice: "+service.LastError);
    }
    static void ParticipationServiceChecks()
    {
        var clock=new Clock();var store=new Store();var service=Open(store,clock);
        Check(service.BeginFocusSession("synthetic-service-intro","free-mission:M17")&&service.StartSession("synthetic-service-intro"),"intro service setup");
        Check(service.EndFocusParticipation("synthetic-service-intro"),"intro skip saves pending outcome");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"intro ending cannot gain growth through real service");
        StartTimedActivity(service,"synthetic-service-partial");
        string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.EndFocusParticipation("synthetic-service-partial"),"participation save failure reported");
        Check(StateCodec.Encode(service.Snapshot)==before,"end outcome and partial receipt rollback atomically");store.Fail=false;
        Check(service.EndFocusParticipation("synthetic-service-partial","stopped"),"explicit stop retry succeeds: "+service.LastError);
        var saved=service.Snapshot;
        Check(saved.Sessions.Last().Status==ParticipationState.Ended&&saved.Sessions.Last().FocusEndReason=="stopped"
            &&saved.Nutrient==0&&WeeklyPlants.VisualCreditUnits(saved,saved.PlantCollection.ActiveId)==20,"service stop records actual incomplete participation with no nutrient");
        service=Open(store,clock);
        Check(service.EndFocusParticipation("synthetic-service-partial","stopped")&&service.Snapshot.PlantCollection.ParticipationDecisions.Count==1,"reload repeated ending idempotent");
        StartTimedActivity(service,"synthetic-service-complete");
        Check(service.ConfirmFocusStep("synthetic-service-complete",1),"normal practice step completion");
        before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.CompleteFocusSession("synthetic-service-complete"),"A completion save failure reported");
        Check(StateCodec.Encode(service.Snapshot)==before,"A top-up completion wallet and history rollback together");store.Fail=false;
        Check(service.CompleteFocusSession("synthetic-service-complete"),"A completion retry succeeds: "+service.LastError);
        saved=service.Snapshot;
        Check(saved.Sessions.Last().Status==ParticipationState.RewardCommitted&&saved.Nutrient==5
            &&WeeklyPlants.VisualCreditUnits(saved,saved.PlantCollection.ActiveId)==100,"normal service completion retains nutrient and A total100");
        Check(service.ResetMissionHistory(),"history reset after A settlement");
        StartTimedActivity(service,"synthetic-service-after-reset");Check(service.EndFocusParticipation("synthetic-service-after-reset"),"end after reset");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Last().Reason=="already-credited","real history reset cannot repeat same-day grant");
        var basic=Open(new Store(),new Clock());Check(basic.SelectMbctProgram("2w"),"basic course chosen");
        Check(basic.BeginFocusSession("synthetic-basic-service","course:P04",1)&&basic.StartSession("synthetic-basic-service"),"basic real setup");
        Check(basic.ConfirmFocusStep("synthetic-basic-service",0)&&basic.ConfirmFocusStep("synthetic-basic-service",1)
            &&basic.StartFocusPracticeStep("synthetic-basic-service",2),"basic reaches real sensory practice");
        Check(basic.EndFocusParticipation("synthetic-basic-service"),"basic explicit skip");
        Check(FocusMissionPolicy.NextOrder(basic.Snapshot,"2w")==1&&basic.Snapshot.Nutrient==0
            &&WeeklyPlants.Active(basic.Snapshot).Recipe.GrowthStep==0,"partial basic course stays pending with no fake completion");
        // The 04:00 boundary is refreshed by the ending command itself, not a UI refresh.
        clock=new Clock {Time=DateTimeOffset.Parse("2026-11-03T18:59:00Z")};service=Open(new Store(),clock);
        StartTimedActivity(service,"synthetic-boundary-partial-first");Check(service.EndFocusParticipation("synthetic-boundary-partial-first"),"partial before reset");
        clock.Time=clock.Time.AddMinutes(2);StartTimedActivity(service,"synthetic-boundary-partial-second");
        Check(service.EndFocusParticipation("synthetic-boundary-partial-second"),"partial after reset without refresh");
        saved=service.Snapshot;
        Check(saved.PlantCollection.ParticipationDecisions.Count==2&&saved.PlantCollection.ParticipationDecisions.All(d=>d.Units==20)
            &&saved.PlantCollection.ParticipationDecisions.Select(d=>d.CycleId).Distinct().Count()==2,"04:00 creates actual next-day additional entitlement");
        clock.Time=clock.Time.AddDays(-2);StartTimedActivity(service,"synthetic-clockback-partial");
        Check(service.EndFocusParticipation("synthetic-clockback-partial"),"clockback end saves");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Last().Reason=="already-credited","clock rollback cannot reopen earlier entitlement");
    }
    static void Main()
    {
        var clock=new Clock();var store=new Store();var s=Open(store,clock);
        Check(s.Snapshot.SchemaVersion==2&&s.Snapshot.PlantCollection.Plants.Count==1,"new installation has one seed");
        Complete(s,"synthetic-1");Check(WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==1,"blank reflection still grows");
        Check(WeeklyPlants.Active(s.Snapshot).Memories.Count==0,"blank creates no memory");
        Complete(s,"synthetic-2","잘 모르겠어요");Complete(s,"synthetic-3");Complete(s,"synthetic-4","synthetic mood");
        var state=s.Snapshot;Check(WeeklyPlants.Active(state).Recipe.GrowthStep==3&&WeeklyPlants.Today(state)==3,"daily three");
        Check(state.PlantCollection.Decisions.Count==4&&WeeklyPlants.Active(state).Memories.Count==2,"capped activity still remembers");
        clock.Time=clock.Time.AddDays(1);s=Open(store,clock);
        Check(s.CompleteSession("synthetic-4")&&WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==3,"denied retry cannot grow tomorrow");
        Complete(s,"synthetic-5");Complete(s,"synthetic-6");Complete(s,"synthetic-7");
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(null),"new day");Complete(s,"synthetic-8");
        Check(WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==7&&WeeklyPlants.Active(s.Snapshot).Recipe.SpeciesId!=null,"differentiate at seven actual activities");
        string species=WeeklyPlants.Active(s.Snapshot).Recipe.SpeciesId;
        int next=9;
        while(!WeeklyPlants.Active(s.Snapshot).Recipe.Mature)
        {
            if(WeeklyPlants.Today(s.Snapshot)==3){clock.Time=clock.Time.AddDays(2);Check(s.RefreshCycle(null),"rest days do not reset");}
            Complete(s,"synthetic-"+next++);
        }
        state=s.Snapshot;Check(WeeklyPlants.Active(state).Recipe.GrowthStep==21&&WeeklyPlants.Active(state).Recipe.SpeciesId==species,"mature and locked");
        string oldId=state.PlantCollection.ActiveId;string oldRecipe=PlantCodec.Encode(WeeklyPlants.Active(state).Recipe);
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(null),"next plant day");Complete(s,"synthetic-next","synthetic reflection");
        state=s.Snapshot;Check(state.PlantCollection.Plants.Count==2&&WeeklyPlants.Active(state).Recipe.GrowthStep==1,"next plant begins with next activity");
        Check(PlantCodec.Encode(state.PlantCollection.Plants[0].Recipe)==oldRecipe,"completed plant unchanged");
        Check(s.UpdateReflection("synthetic-1","synthetic late mood","synthetic late note"),"late reflection");
        Check(s.Snapshot.PlantCollection.Plants[0].Memories.Any(m=>m.RecordId=="synthetic-1"),"late reflection stays on original plant");
        string organ=WeeklyPlants.FindMemory(s.Snapshot,MemoryRecordKind.SessionReflection,"synthetic-1").OrganId;
        Check(s.UpdateReflection("synthetic-1","synthetic edit",null),"edit reflection");
        Check(WeeklyPlants.FindMemory(s.Snapshot,MemoryRecordKind.SessionReflection,"synthetic-1").OrganId==organ,"edit retains organ identity");
        for(int i=0;i<210;i++)Check(s.SaveExperience("synthetic-experience-"+i,"중립","synthetic event",null,null,null,oldId),"experience: "+s.LastError);
        state=s.Snapshot;Check(state.PlantCollection.Plants[0].Memories.Count==213,"unlimited stored memories");
        var firstPlant=state.PlantCollection.Plants[0];int maxTriangles=0;
        var inputs=firstPlant.Memories.Select(m=>new PlantMemoryLeaf{OrganId=m.OrganId,Slot=m.SlotIndex,Linked=true}).ToArray();
        for(int page=0;page<11;page++)
        {
            var mesh=PlantMemoryGeometry.Build(firstPlant.Recipe,inputs,page);maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);
            Check(mesh.Organs.Count==inputs.Count(m=>m.Slot/21==page)&&mesh.Organs.Count<=21,"visible branch paging");
            foreach(var geometry in mesh.Organs)Check(inputs.Any(m=>m.OrganId==geometry.OrganId),"geometry stable identity");
        }
        int growth=WeeklyPlants.Active(state).Recipe.GrowthStep;
        Check(s.DeleteExperience("synthetic-experience-0"),"delete experience");
        Check(s.SaveExperience("synthetic-experience-0","중립","synthetic stale retry",null,null,null)
            && !s.Snapshot.Experiences.Any(e=>e.Id=="synthetic-experience-0"),"stale save cannot resurrect deleted experience");
        Check(WeeklyPlants.FindMemory(s.Snapshot,MemoryRecordKind.Experience,"synthetic-experience-0")==null&&WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==growth,"delete no penalty");
        Check(s.UpdateReflection("synthetic-1",null,null),"delete reflection");
        Check(s.Snapshot.PlantCollection.Plants[0].Memories.First(m=>m.OrganId==organ).Kind==MemoryRecordKind.None,"deleted leaf is decoration");
        string before=StateCodec.Encode(s.Snapshot);store.Fail=true;
        Check(!s.SaveExperience("synthetic-failed","중립","synthetic event",null,null,null),"failed save");
        Check(StateCodec.Encode(s.Snapshot)==before,"failed save leaves record and plant untouched");store.Fail=false;
        Check(s.BeginSession("synthetic-atomic","free-mission:M17",0)&&s.StartSession("synthetic-atomic"),"atomic setup");
        before=StateCodec.Encode(s.Snapshot);store.Fail=true;Check(!s.CompleteSession("synthetic-atomic","synthetic mood"),"failed completion");
        Check(StateCodec.Encode(s.Snapshot)==before,"completion wallet plant record all rollback");store.Fail=false;
        Check(s.CompleteSession("synthetic-atomic","synthetic mood"),"completion retry");
        Check(!s.GrowPlant("synthetic-purchase","plant:P06"),"new plants cannot be bought");
        int today=WeeklyPlants.Today(s.Snapshot);Check(s.ResetMissionHistory(),"reset history");
        Check(WeeklyPlants.Today(s.Snapshot)==today&&s.Snapshot.PlantCollection.Plants.SelectMany(p=>p.Memories).All(m=>m.Kind==MemoryRecordKind.None),"reset clears links but retains quota");
        Complete(s,"synthetic-atomic");Check(WeeklyPlants.Today(s.Snapshot)==today,"reset does not replay completion token");
        clock.Time=clock.Time.AddDays(-20);Check(s.RefreshCycle(null),"clock back");Check(WeeklyPlants.Today(s.Snapshot)==today,"clock rollback keeps quota");
        // Old XML is loaded and checked before a single schema transition save.
        var old=new GardenState{MigrationCompleted=true,Nutrient=77,LegacyGrowth=.3f};GardenPlantVisuals.Sync(old);
        var original=PlantCodec.Encode(old.PlantAppearance.Recipe);var migrationStore=new Store{State=old};
        var migrated=Open(migrationStore,new Clock());
        Check(migrated.Snapshot.Nutrient==77&&migrated.Snapshot.PlantCollection.Plants[0].Legacy,"legacy wallet and plant retained");
        Check(PlantCodec.Encode(migrated.Snapshot.PlantCollection.Plants[0].Recipe)==original&&WeeklyPlants.Active(migrated.Snapshot).Recipe.GrowthStep==0,"legacy exact geometry no retroactive growth");
        var bad=StateCodec.Clone(migrated.Snapshot);bad.PlantCollection.ActiveId="missing";
        bool rejected=false;try{StateCodec.Validate(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"corrupt active pointer rejected");
        migrationStore=new Store{State=old,Fail=true};var failed=new GardenStateService(migrationStore,new Clock(),new DemoBalanceConfig());
        Check(!failed.Open()&&migrationStore.State.SchemaVersion==1,"migration failure preserves original");
        string directory=Path.Combine(Path.GetTempPath(),"synthetic-memory-redaction-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path=Path.Combine(directory,"state.xml");
            using(var fileService=new GardenStateService(new FileStateStore(path),new Clock(),new DemoBalanceConfig()))
            {
                Check(fileService.Open(),"file state opens");
                Check(fileService.SaveExperience("synthetic-redact","중립","SYNTHETIC_ERASE_ME",null,null,null),"file experience saved");
                Check(fileService.SavePreference("synthetic-pref","synthetic"),"create previous-version backup");
                Check(File.ReadAllText(path+".bak").Contains("SYNTHETIC_ERASE_ME"),"fixture backup contains old synthetic record");
                Check(fileService.DeleteExperience("synthetic-redact"),"delete source and redact backup");
                Check(!File.ReadAllText(path).Contains("SYNTHETIC_ERASE_ME")&&!File.Exists(path+".bak"),"deleted text absent from live and automatic backup");
            }
        }
        finally{Directory.Delete(directory,true);}
        var boundaryClock=new Clock{Time=DateTimeOffset.Parse("2026-11-03T18:59:00Z")};var boundary=Open(new Store(),boundaryClock);
        Complete(boundary,"synthetic-before-reset");boundaryClock.Time=boundaryClock.Time.AddMinutes(2);
        Complete(boundary,"synthetic-after-reset");
        Check(WeeklyPlants.Today(boundary.Snapshot)==1&&WeeklyPlants.Active(boundary.Snapshot).Recipe.GrowthStep==2,"completion applies 04:00 boundary without UI refresh");
        var nextClock=new Clock();var nextService=Open(new Store(),nextClock);
        for(int i=0;i<20;i++)
        {
            if(i==2 || (i>2&&(i-2)%3==0)){nextClock.Time=nextClock.Time.AddDays(1);nextService.RefreshCycle(null);}
            Complete(nextService,"synthetic-rollover-"+i);
        }
        nextClock.Time=nextClock.Time.AddDays(1);nextService.RefreshCycle(null);
        Complete(nextService,"synthetic-rollover-21");Complete(nextService,"synthetic-rollover-22");Complete(nextService,"synthetic-rollover-23");Complete(nextService,"synthetic-rollover-24");
        Check(WeeklyPlants.Today(nextService.Snapshot)==3&&nextService.Snapshot.PlantCollection.Plants.Count==2&&WeeklyPlants.Active(nextService.Snapshot).Recipe.GrowthStep==2,"same-day plant rollover shares global daily quota");
        ParticipationGrowthChecks();
        ParticipationServiceChecks();
        Console.WriteLine(checks+" weekly plant checks passed; visible memory budget max "+maxTriangles+" triangles.");
    }
}
