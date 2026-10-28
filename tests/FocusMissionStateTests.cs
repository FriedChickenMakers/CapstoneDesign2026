using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Runtime.LocalState;

sealed class FocusClock : IClock
{
    public DateTimeOffset Time = DateTimeOffset.Parse("2026-10-26T10:00:00Z");
    public DateTimeOffset UtcNow => Time;
    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
}
sealed class FocusStore : IStateStore
{
    public GardenState State;
    public bool Fail;
    public GardenState Load() => State==null?null:StateCodec.Clone(State);
    public void Save(GardenState candidate)
    {
        if(Fail)throw new IOException("Synthetic disk failure");
        State=StateCodec.Clone(candidate);
    }
}
static class FocusMissionStateTests
{
    static int assertions;
    static void Check(bool result,string message){assertions++;if(!result)throw new Exception(message);}
    static GardenStateService Open(IStateStore store,FocusClock clock)
    {
        var service=new GardenStateService(store,clock,new DemoBalanceConfig());
        Check(service.Open(),"open: "+service.LastError);
        Check(service.RefreshCycle(Array.Empty<string>()),"cycle");return service;
    }
    static ActivitySession Get(GardenStateService service,string id) => service.Snapshot.Sessions.Single(s=>s.SessionId==id);
    static void Start(GardenStateService service,string id,string code,int order=0,bool review=false)
    {
        Check(service.BeginFocusSession(id,code,order,review),"select "+id+": "+service.LastError);
        Check(Get(service,id).Status==ParticipationState.Selected,"overview before start");
        Check(service.StartSession(id),"start "+id);
    }
    static void ConfirmAll(GardenStateService service,string id)
    {
        var definition=FocusMissionCatalog.Find(Get(service,id).MissionId);
        for(int i=0;i<definition.Steps.Length;i++)
            Check(service.ConfirmFocusStep(id,i,null,true),"confirm "+id+" "+i+": "+service.LastError);
    }
    static void Complete(GardenStateService service,string id,string code,int order,bool review=false)
    {
        Start(service,id,code,order,review);ConfirmAll(service,id);
        Check(service.SaveFocusReflection(id,null,null,0),"optional emotion");
        Check(service.SaveFocusReflection(id,null,null,1),"optional note");
        Check(service.CompleteFocusSession(id),"complete: "+service.LastError);
    }
    static void Main()
    {
        Check(FocusMissionCatalog.All.Length==4,"four active definitions");
        Check(string.Join(",",FocusMissionCatalog.Basic.Select(m=>m.Code))=="P10,P23,P25","three P basics");
        Check(FocusMissionCatalog.Additional.Single().Code=="M03","M03 only additional");
        Check(Enumerable.Range(1,12).All(i=>FocusMissionPolicy.MissionAt(i).Code==new[]{"P23","P10","P25"}[(i-1)%3]),"stable repeated 12-slot schedule");
        var clock=new FocusClock();var store=new FocusStore();var service=Open(store,clock);
        Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"initial day has basic and additional missions left");
        Check(!FocusMissionPolicy.HasPendingToday(null),"unavailable state does not invent pending missions");
        Check(service.BeginSession("legacy-kept","course:P01",1) && service.StartSession("legacy-kept"),"legacy active record seeded");
        Check(!service.BeginFocusSession("wrong","P10",1),"reject mismatched occurrence");
        Start(service,"basic-1","P23",1);
        Check(!service.BeginFocusSession("parallel-basic","P23",1),"one basic draft");
        Start(service,"extra-1","M03");
        Check(FocusMissionPolicy.Active(service.Snapshot,"basic").SessionId=="basic-1" && FocusMissionPolicy.Active(service.Snapshot,"additional").SessionId=="extra-1","separate basic and additional drafts");
        Check(!service.CompleteSession("basic-1"),"legacy completion API cannot bypass focus steps");
        Check(!service.ConfirmFocusStep("basic-1",2),"cannot skip prerequisite steps");
        Check(!service.MoveFocusStep("basic-1",3),"cannot jump forward");
        Check(!service.SaveFocusReflection("basic-1","평온함","synthetic note",0),"cannot reflect before practice");
        for(int i=0;i<3;i++)Check(service.ConfirmFocusStep("basic-1",i),"P23 step "+i);
        Check(service.SaveFocusDraft("basic-1",3,"질감"),"save sensation draft");
        Check(!service.SaveFocusDraft("basic-1",3,"invalid choice"),"invalid option rejected");
        store.Fail=true;
        Check(!service.SaveFocusDraft("basic-1",3,"온도") && Get(service,"basic-1").FocusStepRecords[3].Value=="질감","failed draft keeps previous durable response");
        store.Fail=false;service=Open(store,clock);
        Check(Get(service,"basic-1").InstructionStep==3 && Get(service,"basic-1").FocusStepRecords[3].Value=="질감","draft restores after restart");
        Check(service.MoveFocusStep("basic-1",1) && service.MoveFocusStep("basic-1",3),"back and reached-step forward");
        Check(Get(service,"basic-1").FocusStepRecords[3].Value=="질감","back preserves answer");
        Check(service.ConfirmFocusStep("basic-1",3,"특별한 감각 없음"),"explicit no-sensation response");
        Check(!Get(service,"basic-1").FocusStepRecords[3].Skipped,"explicit none differs from skipped");
        var confirmed=Get(service,"basic-1").FocusStepRecords[3].ConfirmedUtc;
        Check(service.ConfirmFocusStep("basic-1",3,"온도") && Get(service,"basic-1").FocusStepRecords[3].Value=="특별한 감각 없음" && Get(service,"basic-1").FocusStepRecords[3].ConfirmedUtc==confirmed,"duplicate click cannot rewrite advanced answer");
        Check(service.SaveFocusReflection("basic-1","평온함","synthetic reflection",1),"save optional reflection draft");
        Check(service.SaveFocusReflection("basic-1","평온함","synthetic reflection",0) && service.SaveFocusReflection("basic-1","평온함","synthetic reflection",1),"reflection back preserves both inputs");
        store.Fail=true;int before=service.Snapshot.Nutrient;
        Check(!service.CompleteFocusSession("basic-1","평온함","synthetic reflection"),"completion failure surfaced");
        Check(service.Snapshot.Nutrient==before && FocusMissionPolicy.NextOrder(service.Snapshot)==1 && Get(service,"basic-1").Status==ParticipationState.InProgress,"failed completion leaves progress reward and session unchanged");
        Check(Get(service,"basic-1").Note=="synthetic reflection","failed save keeps reflection draft");
        store.Fail=false;
        Check(service.CompleteFocusSession("basic-1","평온함","synthetic reflection"),"completion retry");
        Check(service.Snapshot.Nutrient==5 && Get(service,"basic-1").RewardAmount==5 && FocusMissionPolicy.NextOrder(service.Snapshot)==2,"first scheduled occurrence reward");
        Check(service.CompleteFocusSession("basic-1","changed","changed") && service.Snapshot.Nutrient==5 && Get(service,"basic-1").Note=="synthetic reflection","completed retry immutable");
        Check(service.Snapshot.NextCourseOrder==1 && service.Snapshot.MbctNextOrder==1 && Get(service,"legacy-kept").Status==ParticipationState.InProgress,"legacy progress and session preserved");
        Check(!service.BeginFocusSession("too-soon","P10",2),"daily basic quota");
        Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"basic completion alone leaves the additional mission pending");
        ConfirmAll(service,"extra-1");Check(service.CompleteFocusSession("extra-1"),"additional completes while basic day done");
        Check(service.Snapshot.Nutrient==10 && FocusMissionPolicy.NextOrder(service.Snapshot)==2,"additional does not advance course");
        Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot),"both daily missions complete despite a retained legacy draft");
        Start(service,"extra-repeat","M03",0);
        Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"normal additional draft needs attention even after an earlier daily completion");
        Check(service.PauseSession("extra-repeat") && FocusMissionPolicy.HasPendingToday(service.Snapshot),"paused normal additional draft remains pending");
        Check(service.ResumeSession("extra-repeat"),"resume additional badge fixture");
        ConfirmAll(service,"extra-repeat");Check(service.CompleteFocusSession("extra-repeat"),"repeat additional complete");
        Check(Get(service,"extra-repeat").RewardAmount==0 && service.Snapshot.Nutrient==10,"same-day extra no duplicate reward");
        Start(service,"extra-review","M03",0,true);
        Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot),"optional additional review does not undo a complete day");
        ConfirmAll(service,"extra-review");Check(service.CompleteFocusSession("extra-review"),"additional review complete");
        Check(Get(service,"extra-review").RewardAmount==0 && service.Snapshot.Nutrient==10,"explicit additional review has no reward");
        clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"day two");
        Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"daily rollover restores pending status");
        Complete(service,"extra-day2","M03",0);
        Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"additional completion alone leaves the basic mission pending");
        Complete(service,"basic-2","P10",2);
        Check(service.Snapshot.Nutrient==20,"additional reward resets on new daily cycle");
        Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot),"daily completion works regardless of mission order");
        clock.Time=clock.Time.AddDays(1);service.RefreshCycle(Array.Empty<string>());
        Start(service,"basic-3","P25",3);
        Check(service.ConfirmFocusStep("basic-3",0),"pause confirmation");
        Check(service.SaveFocusDraft("basic-3",1,"synthetic concise message"),"P25 optional text");
        Check(service.PauseSession("basic-3") && service.ResumeSession("basic-3"),"pause resume preserves draft");
        Check(Get(service,"basic-3").FocusStepRecords[1].Value=="synthetic concise message","P25 draft retained");
        Check(service.ConfirmFocusStep("basic-3",1,null,true) && service.ConfirmFocusStep("basic-3",2) && service.ConfirmFocusStep("basic-3",3),"mental confirmation and action sequence");
        var p25=Get(service,"basic-3");
        Check(p25.FocusStepRecords[1].Skipped && p25.FocusStepRecords[1].Value=="","mental confirmation stores no invented message");
        Check(p25.FocusStepRecords.Zip(p25.FocusStepRecords.Skip(1),(a,b)=>DateTimeOffset.Parse(a.ConfirmedUtc)<DateTimeOffset.Parse(b.ConfirmedUtc)).All(x=>x),"confirmation timestamps preserve strict action order even on same clock tick");
        Check(service.CompleteFocusSession("basic-3"),"P25 complete");
        for(int order=4;order<=6;order++)
        {clock.Time=clock.Time.AddDays(1);service.RefreshCycle(Array.Empty<string>());Complete(service,"basic-"+order,FocusMissionPolicy.MissionAt(order).Code,order);}
        clock.Time=clock.Time.AddDays(1);service.RefreshCycle(Array.Empty<string>());
        Check(FocusMissionPolicy.Availability(service.Snapshot)!=null && !service.BeginFocusSession("week-cap","P23",7),"six-per-calendar-week cap");
        Complete(service,"extra-week-cap","M03",0);
        Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot),"locked future basic missions do not keep the weekly rest day pending");
        for(int order=7;order<=12;order++)
        {clock.Time=clock.Time.AddDays(1);service.RefreshCycle(Array.Empty<string>());Complete(service,"basic-"+order,FocusMissionPolicy.MissionAt(order).Code,order);}
        Check(FocusMissionPolicy.NextOrder(service.Snapshot)==13 && FocusMissionPolicy.CompletedCount(service.Snapshot)==12,"two-week completion");
        Check(!service.BeginFocusSession("beyond","P23",13),"cannot exceed twelve occurrences");
        Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"finished basic course still has today's additional mission pending");
        Complete(service,"extra-course-done","M03",0);
        Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot),"finished course with today's additional complete has no pending missions");
        before=service.Snapshot.Nutrient;Start(service,"review","P23",1,true);
        Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot),"optional basic review does not undo a complete day");
        ConfirmAll(service,"review");Check(service.CompleteFocusSession("review"),"basic review complete");
        Check(Get(service,"review").RewardAmount==0 && service.Snapshot.Nutrient==before && FocusMissionPolicy.NextOrder(service.Snapshot)==13,"review no reward/progress");
        Check(FocusMissionPolicy.History(service.Snapshot,"P23").Count()==5,"repeated template stores four distinct occurrences and separate review");
        var detached=service.Snapshot;detached.Sessions.First(s=>s.SessionId=="basic-1").FocusStepRecords[0].Prompt="changed";
        Check(Get(service,"basic-1").FocusStepRecords[0].Prompt!="changed","nested record snapshots are detached");
        var corrupt=service.Snapshot;corrupt.Sessions.Single(s=>s.SessionId=="basic-3").FocusStepRecords[2].ConfirmedUtc=corrupt.Sessions.Single(s=>s.SessionId=="basic-3").FocusStepRecords[0].ConfirmedUtc;
        bool rejected=false;try{StateCodec.Validate(corrupt);}catch(InvalidDataException){rejected=true;}Check(rejected,"out-of-order persisted confirmations rejected");
        var legacyXml=new XmlDocument();legacyXml.LoadXml(StateCodec.Encode(service.Snapshot));
        var old=legacyXml.DocumentElement["Sessions"].ChildNodes.Cast<XmlNode>().Single(n=>n["SessionId"].InnerText=="legacy-kept");
        foreach(string field in new[]{"FocusVersion","FocusOrder","RewardAmount","FocusRole","MissionTitleSnapshot","MissionDescriptionSnapshot","FocusReview","FocusStepRecords"})if(old[field]!=null)old.RemoveChild(old[field]);
        Check(StateCodec.Decode(legacyXml.OuterXml).Sessions.Single(s=>s.SessionId=="legacy-kept").FocusVersion==0,"pre-focus schema remains readable");
        var migration=Open(new FocusStore(),new FocusClock());
        Check(migration.BeginSession("old-water","free-mission:M03") && migration.StartSession("old-water") && migration.CompleteSession("old-water"),"old water completion seeded");
        Complete(migration,"new-water","M03",0);
        Check(migration.Snapshot.Nutrient==5 && Get(migration,"new-water").RewardAmount==0,"upgrade does not duplicate an existing same-day M03 reward");
        RealFileReload();
        EditorProfileIsolation();
        ResetAndAppDate();
        ResetAndDateFileReload();
        Console.WriteLine("PASS "+assertions+" focus mission assertions (synthetic data)");
    }
    static void ResetAndAppDate()
    {
        var clock=new FocusClock();var store=new FocusStore();
        var service=new GardenStateService(store,clock,new DemoBalanceConfig(),new LegacySnapshot{Nutrient=50,GardenXp=7,LastUnlock="synthetic-unlock",UnlockIds={"synthetic-unlock"},CompletedMissionIds={"free-mission:M04"}});
        Check(service.Open()&&service.RefreshCycle(Array.Empty<string>()),"reset fixture opens");
        Check(service.SelectMbctProgram("2w"),"reset fixture retains selected program");
        foreach(var record in new[]{new[]{"retired-course","course:P01"},new[]{"retired-mbct","course:MBCT01"},new[]{"retired-short","course:MBCT2W01"}})
        {
            if(record[0]=="retired-short"){clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"advance retired course fixture day");}
            if(MbctPolicy.IsMbct(record[1]))Check(service.SelectMbctProgram(MbctPolicy.ProgramForMission(record[1])),"select retired course fixture");
            Check(service.BeginSession(record[0],record[1],1)&&service.StartSession(record[0])&&service.CompleteSession(record[0],"중립","synthetic retired note"),"seed retired history "+record[0]+": "+service.LastError);
        }
        Complete(service,"reset-basic","P23",1);Complete(service,"reset-extra","M03",0);
        Check(service.SavePreference("food","synthetic preference")&&service.SaveExperience("reset-experience","중립","synthetic event","synthetic body","synthetic emotion","synthetic thought"),"seed detailed practice records");
        Check(service.GrowPlant("reset-growth","plant:P06","reset-basic")&&service.PurchaseEnvironment("reset-environment","environment:E01","left"),"seed permanent garden purchases");
        Start(service,"reset-review-draft","P23",1,true);ConfirmAll(service,"reset-review-draft");
        Check(service.SaveFocusReflection("reset-review-draft","편안함","synthetic draft",1),"seed optional draft reflection");
        Start(service,"reset-additional-draft","M03");
        var before=service.Snapshot;string beforeXml=StateCodec.Encode(before),recipe=CapstoneDesign.Plants.PlantCodec.Encode(before.PlantAppearance.Recipe);
        var oldXml=new XmlDocument();oldXml.LoadXml(beforeXml);oldXml.DocumentElement.RemoveChild(oldXml.DocumentElement["PreviewDayOffset"]);
        var oldArchive=oldXml.DocumentElement["PlantAppearance"]["ArchivedCredits"];oldArchive.ParentNode.RemoveChild(oldArchive);
        var decoded=StateCodec.Decode(oldXml.OuterXml);
        Check(decoded.PreviewDayOffset==0&&decoded.PlantAppearance.ArchivedCredits.Count==0,"old saves default to real date and no archived credits");
        store.Fail=true;var utcBefore=service.CurrentUtc;var pendingField=typeof(GardenStateService).GetField("pendingDecision",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);var pendingBefore=pendingField.GetValue(service);
        Check(!service.AdvancePreviewDay(new[]{"animal:A07"}),"failed app-date save is surfaced");
        Check(service.CurrentUtc==utcBefore&&StateCodec.Encode(service.Snapshot)==beforeXml&&ReferenceEquals(pendingField.GetValue(service),pendingBefore),"failed app-date save leaves clock, state and pending decision untouched");
        Check(!service.ResetMissionHistory()&&StateCodec.Encode(service.Snapshot)==beforeXml,"failed mission reset rolls back all records, receipts and appearance");store.Fail=false;
        Check(service.ResetMissionHistory(),"mission reset commits");var reset=service.Snapshot;
        Check(reset.Sessions.Count==0&&reset.PracticePreferences.Count==0&&reset.Experiences.Count==0,"mission sessions, input preferences and experience detail are removed");
        Check(reset.NextCourseOrder==1&&reset.CompletedCourseIds.Count==0&&reset.MbctNextOrder==1&&reset.MbctCompletedIds.Count==0&&reset.MbctPrograms.Count==0,"all course progress resets to the beginning");
        Check(reset.RewardReceipts.Count==0&&reset.LegacyCompletedUnknownDate.Count==0,"mission reward and imported completion flags clear");
        Check(reset.Nutrient==before.Nutrient&&reset.GardenXp==before.GardenXp&&reset.LastUnlock==before.LastUnlock&&reset.UnlockIds.SequenceEqual(before.UnlockIds),"reset does not change the wallet or unlocks");
        Check(reset.PurchaseReceipts.SequenceEqual(before.PurchaseReceipts)&&reset.PurchaseCommands.Count==before.PurchaseCommands.Count&&reset.Environments.Single().EnvironmentId==before.Environments.Single().EnvironmentId,"reset preserves purchases and scenery");
        Check(reset.GrowthHistory.Single().LinkedSessionId==""&&reset.GrowthHistory.Single().AfterGrowth==before.GrowthHistory.Single().AfterGrowth&&reset.Plants.Single().Growth==before.Plants.Single().Growth,"reset detaches removed mission references but preserves paid growth");
        Check(reset.LastCycleId==before.LastCycleId&&reset.DailyDecisions.Count==before.DailyDecisions.Count&&reset.MbctProgramId=="2w"&&reset.MbctProgramChosen,"reset preserves app date, visitors and course selection");
        Check(reset.PlantAppearance.Credits.Count==0&&reset.PlantAppearance.ArchivedCredits.Count==before.PlantAppearance.Credits.Count&&CapstoneDesign.Plants.PlantCodec.Encode(reset.PlantAppearance.Recipe)==recipe,"appearance-only accounting preserves the exact plant recipe");
        Check(FocusMissionPolicy.NextOrder(reset)==1&&FocusMissionPolicy.Availability(reset)==null&&FocusMissionPolicy.HasPendingToday(reset)&&!FocusMissionPolicy.AdditionalCompletedToday(reset),"reset makes both mission types unfinished and basic order one");
        var detached=service.Snapshot;detached.PlantAppearance.ArchivedCredits[0].Category=99;
        Check(service.Snapshot.PlantAppearance.ArchivedCredits[0].Category!=99,"appearance archive snapshots are detached");
        string resetXml=StateCodec.Encode(reset);Check(service.ResetMissionHistory()&&StateCodec.Encode(service.Snapshot)==resetXml,"a repeated empty reset is stable");
        Complete(service,"replayed-basic","P23",1);Complete(service,"replayed-extra","M03",0);
        Check(CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,"same-day replay does not duplicate appearance credits");
        Check(service.Snapshot.Nutrient==before.Nutrient+10,"only explicitly completed replay missions receive new rewards");
        var physicalNow=clock.Time;int decisions=service.Snapshot.DailyDecisions.Count;var dayBefore=service.CurrentCycleId;
        Check(service.AdvancePreviewDay(),"next app day commits");
        Check(clock.Time==physicalNow&&service.CurrentUtc==physicalNow.AddDays(1)&&service.Snapshot.PreviewDayOffset==1,"date button changes only the app clock");
        Check(service.CurrentCycleId!=dayBefore&&service.Snapshot.LastCycleId==service.CurrentCycleId&&service.Snapshot.DailyDecisions.Count==decisions+1,"date button advances the daily cycle once");
        Check(DateTimeOffset.Parse(service.Snapshot.DailyDecisions.Last().AppliedUtc)==service.CurrentUtc,"daily application timestamp uses the advanced date");
        Check(FocusMissionPolicy.Availability(service.Snapshot)==null&&!FocusMissionPolicy.AdditionalCompletedToday(service.Snapshot)&&FocusMissionPolicy.NextOrder(service.Snapshot)==2,"next day opens new basic and additional missions without deleting history");
        Complete(service,"next-day-basic","P10",2);
        var nextSession=Get(service,"next-day-basic");
        Check(DateTimeOffset.Parse(nextSession.SelectedUtc)==service.CurrentUtc&&DateTimeOffset.Parse(nextSession.StartedUtc)==service.CurrentUtc&&DateTimeOffset.Parse(nextSession.EndedUtc)>=service.CurrentUtc&&nextSession.FocusStepRecords.All(step=>DateTimeOffset.Parse(step.ConfirmedUtc)>=service.CurrentUtc),"all mission timestamps use the app testing date");
        Check(service.ResetMissionHistory()&&service.Snapshot.PreviewDayOffset==1&&service.CurrentUtc==physicalNow.AddDays(1),"reset preserves the advanced testing date");
        var invalid=service.Snapshot;invalid.PreviewDayOffset=-1;bool rejected=false;try{StateCodec.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"negative app-date offsets are rejected");
        invalid=service.Snapshot;invalid.PreviewDayOffset=36501;rejected=false;try{StateCodec.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"unsupported app-date offsets are rejected");
        invalid=service.Snapshot;var archived=invalid.PlantAppearance.ArchivedCredits[0];invalid.PlantAppearance.Credits.Add(new GardenAppearanceCredit{CycleId=archived.CycleId,MissionId=archived.MissionId,Category=archived.Category});rejected=false;try{StateCodec.Validate(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"appearance archives cannot duplicate current credits");
        var limitStore=new FocusStore{State=service.Snapshot};limitStore.State.PreviewDayOffset=36500;var limit=Open(limitStore,clock);string limitXml=StateCodec.Encode(limit.Snapshot);
        Check(!limit.AdvancePreviewDay()&&StateCodec.Encode(limit.Snapshot)==limitXml,"date limit failure preserves saved state");
    }

    static void ResetAndDateFileReload()
    {
        var directory=Path.Combine(Path.GetTempPath(),"capstone-reset-date-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var path=Path.Combine(directory,"state.xml");var clock=new FocusClock();string recipe;int nutrient;
        try
        {
            using(var service=Open(new FileStateStore(path),clock))
            {
                Complete(service,"persist-reset-basic","P23",1);Complete(service,"persist-reset-extra","M03",0);
                Check(service.GrowPlant("persist-reset-growth","plant:P06","persist-reset-basic"),"seed linked paid growth before reset persistence");
                Check(service.AdvancePreviewDay(),"persist advanced app date");
                recipe=CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);nutrient=service.Snapshot.Nutrient;string saved=StateCodec.Encode(service.Snapshot);
                Directory.CreateDirectory(path+".tmp");Check(!service.ResetMissionHistory()&&StateCodec.Encode(service.Snapshot)==saved,"real staging-write failure retains all mission history and app date");Directory.Delete(path+".tmp");
                Check(service.ResetMissionHistory(),"persist reset after retry");
            }
            using(var service=Open(new FileStateStore(path),clock))
            {
                Check(service.Snapshot.Sessions.Count==0&&service.Snapshot.RewardReceipts.Count==0&&FocusMissionPolicy.NextOrder(service.Snapshot)==1,"file restart keeps mission history reset");
                Check(service.Snapshot.PreviewDayOffset==1&&service.CurrentUtc==clock.Time.AddDays(1),"file restart restores the app date offset");
                Check(service.Snapshot.Nutrient==nutrient&&CapstoneDesign.Plants.PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe&&service.Snapshot.GrowthHistory.Single().LinkedSessionId=="","file restart preserves garden growth and detached appearance accounting");
                Check(FocusMissionPolicy.HasPendingToday(service.Snapshot),"file restart after reset offers unfinished missions");
            }
        }
        finally{Directory.Delete(directory,true);}
    }
    static void EditorProfileIsolation()
    {
        var temporaryRoot=Path.GetFullPath(Path.GetTempPath());
        var directory=Path.Combine(temporaryRoot,"capstone-editor-state-paths-"+Guid.NewGuid().ToString("N"));
        var livePath=GardenStatePaths.StateFile(directory,"LIVE",false);
        var editorPath=GardenStatePaths.StateFile(directory,"LIVE",true);
        var overrideRoot=Path.Combine(directory,"explicit-preview");
        Check(livePath==Path.Combine(directory,"garden-live","state.xml"),"device live storage path remains unchanged");
        Check(editorPath==Path.Combine(directory,"editor-mission-preview","garden-live","state.xml"),"editor state has its own profile directory");
        Check(GardenStatePaths.StateFile(directory,"MOCK",true)==Path.Combine(directory,"editor-mission-preview","garden-mock","state.xml"),"editor input modes stay isolated");
        Check(GardenStatePaths.StateFile(directory,"REPLAY",false)==Path.Combine(directory,"garden-replay","state.xml"),"device replay path remains unchanged");
        Check(GardenStatePaths.StateFile(directory,"MOCK",true,overrideRoot)==Path.Combine(overrideRoot,"garden-mock","state.xml"),"explicit editor override has highest priority");
        Check(GardenStatePaths.StateFile(directory,"LIVE",false,overrideRoot)==Path.Combine(overrideRoot,"garden-live","state.xml"),"explicit device override has highest priority");
        Check(GardenStatePaths.StateFile(directory,"LIVE",true,"")==editorPath,"empty override keeps editor isolation");
        Check(!Directory.Exists(directory),"path selection has no filesystem side effects");
        Check(GardenStatePaths.ShouldMigrateLegacy(false,"LIVE"),"device live keeps legacy migration");
        Check(!GardenStatePaths.ShouldMigrateLegacy(true,"LIVE") && !GardenStatePaths.ShouldMigrateLegacy(true,"MOCK"),"editor profiles never import old PlayerPrefs");
        Check(!GardenStatePaths.ShouldMigrateLegacy(false,"MOCK") && !GardenStatePaths.ShouldMigrateLegacy(false,"REPLAY"),"nonlive device profiles never import old PlayerPrefs");
        var clock=new FocusClock();Directory.CreateDirectory(directory);
        try
        {
            using(var live=Open(new FileStateStore(livePath),clock))
            {
                Complete(live,"synthetic-live-basic","P23",1);Complete(live,"synthetic-live-extra","M03",0);
                Check(!FocusMissionPolicy.HasPendingToday(live.Snapshot),"synthetic older live profile has completed its day");
            }
            string originalLive=File.ReadAllText(livePath);
            using(var editor=Open(new FileStateStore(editorPath),clock))
            {
                Check(editor.Snapshot.Sessions.Count==0 && editor.Snapshot.Nutrient==0,"first editor launch has no old sessions or rewards");
                Check(FocusMissionPolicy.CompletedCount(editor.Snapshot)==0 && !FocusMissionPolicy.AdditionalCompletedToday(editor.Snapshot),"first editor launch does not inherit live completion flags");
                Check(FocusMissionPolicy.Availability(editor.Snapshot)==null && FocusMissionPolicy.HasPendingToday(editor.Snapshot),"first editor launch offers unfinished missions");
                Complete(editor,"synthetic-editor-basic","P23",1);
            }
            using(var editor=Open(new FileStateStore(editorPath),clock))
            {
                Check(FocusMissionPolicy.CompletedCount(editor.Snapshot)==1 && editor.Snapshot.Nutrient==5,"editor replay preserves explicitly completed practice");
                Check(Get(editor,"synthetic-editor-basic").Status==ParticipationState.RewardCommitted,"editor restart restores its own completion");
                Check(!FocusMissionPolicy.AdditionalCompletedToday(editor.Snapshot),"editor restart keeps untouched additional mission available");
            }
            Check(File.ReadAllText(livePath)==originalLive,"editor testing preserves the original live save bytes");
        }
        finally
        {
            string resolved=Path.GetFullPath(directory);
            Check(resolved.StartsWith(temporaryRoot.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal),"synthetic profile cleanup stays within its temporary directory");
            Directory.Delete(resolved,true);
        }
    }
    static void RealFileReload()
    {
        var directory=Path.Combine(Path.GetTempPath(),"capstone-focus-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"state.xml");var clock=new FocusClock();
        try
        {
            using(var service=Open(new FileStateStore(path),clock))
            {
                Start(service,"file-draft","P23",1);Check(service.ConfirmFocusStep("file-draft",0),"file draft step");
                Check(service.PauseSession("file-draft"),"file draft paused");
            }
            using(var service=Open(new FileStateStore(path),clock))
            {
                var draft=FocusMissionPolicy.Active(service.Snapshot,"basic");
                Check(draft.SessionId=="file-draft" && draft.InstructionStep==1 && draft.FocusStepRecords[0].ConfirmedUtc!=null,"file restart restores timestamp and current step");
                Check(service.ResumeSession(draft.SessionId),"file resume");
                for(int i=1;i<4;i++)Check(service.ConfirmFocusStep(draft.SessionId,i,null,true),"file confirm remaining");
                Check(service.SaveFocusReflection(draft.SessionId,"평온함","synthetic file note",1),"file reflection draft");
            }
            using(var service=Open(new FileStateStore(path),clock))
            {
                Check(Get(service,"file-draft").Note=="synthetic file note" && Get(service,"file-draft").InstructionStep==5,"file reflection restored");
                Directory.CreateDirectory(path+".tmp");
                Check(!service.CompleteFocusSession("file-draft","평온함","synthetic file note") && service.Snapshot.Nutrient==0,"real disk staging failure leaves reward uncommitted");
                Directory.Delete(path+".tmp");
                Check(service.CompleteFocusSession("file-draft","평온함","synthetic file note"),"real disk retry");
            }
            using(var service=Open(new FileStateStore(path),clock))
                Check(service.CompleteFocusSession("file-draft") && service.Snapshot.Nutrient==5 && Get(service,"file-draft").Note=="synthetic file note","file restart retry no duplicate or data overwrite");
        }
        finally {Directory.Delete(directory,true);}
    }
}
