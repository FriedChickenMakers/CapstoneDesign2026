using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Runtime.LocalState;

static class CameraMissionTests
{
    sealed class Clock:IClock { public DateTimeOffset Time=DateTimeOffset.Parse("2026-11-04T10:00:00Z");public DateTimeOffset UtcNow=>Time;public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc; }
    sealed class Store:IStateStore {public GardenState State;public bool Fail;public GardenState Load()=>State==null?null:StateCodec.Clone(State);public void Save(GardenState s){if(Fail)throw new IOException("Synthetic storage failure");State=StateCodec.Clone(s);}}
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static ActivitySession Get(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(s=>s.SessionId==id);
    static void Ready(GardenStateService service,string id,string code="M03",bool review=false)
    {
        Check(service.BeginFocusSession(id,code,0,review),"begin "+service.LastError);
        Check(service.StartSession(id),"start");
        var def=FocusMissionCatalog.FindForSession(Get(service,id));
        for(int i=0;i<def.Steps.Length;i++)Check(service.ConfirmFocusStep(id,i,def.Steps[i].RequiredAnswer?"synthetic":null,!def.Steps[i].RequiredAnswer),"step "+i+" "+service.LastError);
    }
    static MissionPhoto Photo(string target="container",string outcome="matched",bool manual=false)=>new MissionPhoto{
        Id=Guid.NewGuid().ToString("N"),Target=target,ModelVersion=CameraMissionPolicy.ModelVersion,Outcome=outcome,
        CapturedUtc="2026-11-04T10:00:00Z",MatchedLabel=outcome=="matched"?(target=="book"?"book":"cup"):"",Score=outcome=="matched"?.8:0,UserConfirmed=manual};
    static void Attach(GardenStateService service,string id,MissionPhoto p)
    {Check(service.BeginPhotoCapture(id,p.Id,p.Target),"capture "+service.LastError);Check(service.RegisterPhoto(id,p),"register "+service.LastError);}
    static void Main()
    {
        var store=new Store();var clock=new Clock();var svc=new GardenStateService(store,clock,new DemoBalanceConfig());Check(svc.Open(),"open");Check(svc.RefreshCycle(Array.Empty<string>()),"cycle");
        Ready(svc,"skip");Check(svc.SkipPhoto("skip"),"skip");Check(svc.CompleteFocusSession("skip"),"complete without photo");Check(svc.Snapshot.Nutrient==5,"baseline 5");
        Ready(svc,"duplicate");Attach(svc,"duplicate",Photo());Check(!CameraMissionPolicy.BonusAvailable(svc.Snapshot,Get(svc,"duplicate"),1,1),"duplicate quote");Check(svc.CompleteFocusSession("duplicate"),"repeat");Check(svc.Snapshot.Nutrient==5,"no repeated base or bonus");
        Ready(svc,"book","M14");var p=Photo("book","not_found",true);Attach(svc,"book",p);p.UserConfirmed=false;
        Check(Get(svc,"book").Photo.UserConfirmed,"detached input");var copy=svc.Snapshot;copy.Sessions.Single(s=>s.SessionId=="book").Photo.Target="container";
        Check(Get(svc,"book").Photo.Target=="book","detached snapshot");Check(CameraMissionPolicy.BonusAvailable(svc.Snapshot,Get(svc,"book"),1,1),"bonus quote");
        store.Fail=true;Check(!svc.CompleteFocusSession("book"),"atomic save failure");Check(svc.Snapshot.Nutrient==5&&svc.Snapshot.PhotoRewards.Count==0,"failure changes neither ledger nor balance");store.Fail=false;
        Check(svc.CompleteFocusSession("book"),"retry");Check(svc.Snapshot.Nutrient==11&&Get(svc,"book").RewardAmount==5&&Get(svc,"book").PhotoBonusAmount==1,"manual plus base");Check(svc.CompleteFocusSession("book")&&svc.Snapshot.Nutrient==11,"retry idempotent");
        Check(svc.DeletePhoto("book")&&Get(svc,"book").Photo==null&&Get(svc,"book").PhotoBonusAmount==1&&svc.Snapshot.PhotoRewards.Count==1,"delete keeps reward");
        Ready(svc,"review","M14",true);Attach(svc,"review",Photo("book"));Check(svc.CompleteFocusSession("review")&&svc.Snapshot.Nutrient==11,"review no rewards");
        Check(svc.ResetMissionHistory(),"history reset");Ready(svc,"cap");Attach(svc,"cap",Photo());Check(!CameraMissionPolicy.BonusAvailable(svc.Snapshot,Get(svc,"cap"),1,1),"daily cap survives history reset");Check(svc.CompleteFocusSession("cap")&&Get(svc,"cap").PhotoBonusAmount==0,"cap");
        Ready(svc,"midnight","M14");var pending=Photo("book");Check(svc.BeginPhotoCapture("midnight",pending.Id,pending.Target),"pending");
        clock.Time=clock.Time.AddDays(1);Check(svc.RefreshCycle(Array.Empty<string>()),"new day");Check(CameraMissionPolicy.BonusAvailable(svc.Snapshot,Get(svc,"midnight"),1,1),"quote uses new completion day");
        var newer=Photo("book");Check(svc.BeginPhotoCapture("midnight",newer.Id,newer.Target),"retake");Check(!svc.RegisterPhoto("midnight",pending),"stale callback rejected");
        store.Fail=true;Check(!svc.RegisterPhoto("midnight",newer)&&Get(svc,"midnight").PendingPhotoId==newer.Id,"failed registration retains pending");store.Fail=false;
        var restarted=new GardenStateService(store,clock,new DemoBalanceConfig());Check(restarted.Open(),"restart pending");svc=restarted;
        Check(svc.RegisterPhoto("midnight",newer),"register after restart");Check(!svc.RegisterPhoto("midnight",newer),"duplicate callback");Check(svc.CompleteFocusSession("midnight")&&Get(svc,"midnight").PhotoBonusAmount==1,"new day bonus");
        Ready(svc,"invalid");Check(!svc.BeginPhotoCapture("invalid","../escape","container"),"reject paths");Check(!svc.BeginPhotoCapture("invalid",Guid.NewGuid().ToString("N"),"book"),"reject target");
        var invalid=Photo();Check(svc.BeginPhotoCapture("invalid",invalid.Id,invalid.Target),"pending invalid fixture");invalid.MatchedLabel="book";Check(!svc.RegisterPhoto("invalid",invalid),"disallowed label");invalid.MatchedLabel="cup";invalid.Score=double.NaN;Check(!svc.RegisterPhoto("invalid",invalid),"NaN");invalid.Score=.5;Check(!svc.RegisterPhoto("invalid",invalid),"threshold");invalid.Score=.8;invalid.ModelVersion="wrong";Check(!svc.RegisterPhoto("invalid",invalid),"model version");
        Check(svc.SkipPhoto("invalid")&&!svc.RegisterPhoto("invalid",Photo()),"cancel rejects callbacks");Check(svc.CompleteFocusSession("invalid"),"fallback completes");
        Ready(svc,"back","M03",true);var backPhoto=Photo();Check(svc.BeginPhotoCapture("back",backPhoto.Id,backPhoto.Target),"capture before back");
        Check(svc.MoveFocusStep("back",0)&&Get(svc,"back").PendingPhotoId==null&&!svc.RegisterPhoto("back",backPhoto),"returning to practice invalidates pending result");
        var xml=StateCodec.Encode(svc.Snapshot);Check(StateCodec.Decode(xml).PhotoRewards.Count==2,"new XML round trip");
        var legacy=new XmlDocument();legacy.LoadXml(xml);foreach(XmlNode node in legacy.SelectNodes("//Photo|//PhotoRewards|//PhotoBonusAmount|//PhotoPromptHandled|//PendingPhotoId|//PendingPhotoTarget"))node.ParentNode.RemoveChild(node);
        var decoded=StateCodec.Decode(legacy.OuterXml);Check(decoded.PhotoRewards.Count==0&&decoded.Nutrient==svc.Snapshot.Nutrient,"schema 1 without optional fields keeps balance");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=2,MissionId="course:P04"})=="container","P04 mapping");Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=1,MissionId="free-mission:M03"})==null,"legacy not enrolled");Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=2,MissionId="free-mission:M12"})==null,"plants out of scope");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=3,MissionId="free-mission:M03"})=="container","current v3 mapping");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=4,MissionId="free-mission:M03"})==null,"unknown version not enrolled");
        Console.WriteLine("Camera mission state: "+checks+" assertions passed.");
    }
}
