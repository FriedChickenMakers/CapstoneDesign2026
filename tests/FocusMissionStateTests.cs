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
        Console.WriteLine("PASS "+assertions+" focus mission assertions (synthetic data)");
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
