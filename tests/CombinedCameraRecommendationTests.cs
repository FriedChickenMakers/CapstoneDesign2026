using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using CapstoneDesign.Runtime.LocalState;

static class CombinedCameraRecommendationTests
{
    sealed class Clock:IClock
    {
        public DateTimeOffset Time=DateTimeOffset.Parse("2032-01-05T10:00:00Z");
        public DateTimeOffset UtcNow=>Time;
        public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
    }
    sealed class Store:IStateStore
    {
        public GardenState State; public bool Fail; public int Saves;
        public GardenState Load()=>State==null?null:StateCodec.Clone(State);
        public void Save(GardenState value)
        {if(Fail)throw new IOException("Synthetic combined storage failure");State=StateCodec.Clone(value);Saves++;}
    }
    sealed class Fixture:IDisposable
    {
        public readonly Store Store=new Store(); public readonly Clock Clock=new Clock();
        public GardenStateService Service;
        public Fixture(){Reopen();}
        public void Reopen()
        {
            Service?.Dispose();Service=new GardenStateService(Store,Clock,new DemoBalanceConfig{VisitorChancePercent=0});
            Check(Service.Open(),"Open synthetic combined state: "+Service.LastError);
        }
        public void NextDay()
        {Clock.Time=Clock.Time.AddDays(1);Check(Service.RefreshCycle(Array.Empty<string>()),"Advance synthetic day");}
        public void Dispose(){Service.Dispose();}
    }
    static int assertions;
    static void Check(bool ok,string message){assertions++;if(!ok)throw new Exception(message);}
    static ActivitySession Session(Fixture f,string id)=>f.Service.Snapshot.Sessions.Single(s=>s.SessionId==id);
    static string Xml(GardenStateService service)=>StateCodec.Encode(service.Snapshot);
    static void Failed(Fixture f,Func<bool> action,string label)
    {
        var before=Xml(f.Service);var durable=StateCodec.Encode(f.Store.State);int saves=f.Store.Saves;
        f.Store.Fail=true;try{Check(!action(),label+" reports failure");}finally{f.Store.Fail=false;}
        Check(Xml(f.Service)==before&&StateCodec.Encode(f.Store.State)==durable&&f.Store.Saves==saves,label+" is atomic in memory and storage");
    }
    static void Ready(Fixture f,string id,string code="M03",int order=0,string practice=null,string notice=null,bool review=false)
    {
        int coins=f.Service.CoinBalance,growth=f.Service.Snapshot.PracticeGrowthRecords.Count;
        Check(f.Service.BeginFocusSession(id,code,order,review,program:order>0?"8w":null),"Begin "+code+": "+f.Service.LastError);
        Check(Session(f,id).FocusVersion==FocusMissionCatalog.Version,"New session uses current purpose contract");
        var def=FocusMissionCatalog.FindForSession(Session(f,id));
        if(FocusMissionCatalog.IsGuided(Session(f,id)))
        {
            practice=practice??def.Steps[0].Options.FirstOrDefault()??"";
            notice=notice??def.Steps[1].Options.FirstOrDefault()??"";
            Check(f.Service.StartGuidedFocusSession(id,practice),"Start guided "+code);
            Check(f.Service.CompleteGuidedFocusPractice(id,practice,notice,60),"Confirm guided practice "+f.Service.LastError);
        }
        else
        {
            Check(f.Service.StartSession(id),"Start "+code);
            while(Session(f,id).InstructionStep<def.Steps.Length)
            {
                int index=Session(f,id).InstructionStep;var step=def.Steps[index];
                string value=step.Options.FirstOrDefault()??(step.RequiredAnswer?"synthetic":"");
                Check(f.Service.ConfirmFocusStep(id,index,value),"Confirm "+code+"/"+step.Key+": "+f.Service.LastError);
            }
        }
        Check(f.Service.CoinBalance==coins&&f.Service.Snapshot.PracticeGrowthRecords.Count==growth,
            "Reading/confirming practice never grants partial growth or coins");
        // Several confirmations can share a synthetic clock tick; the domain makes
        // their timestamps strictly ordered. Finalization occurs on a later real tick.
        f.Clock.Time=f.Clock.Time.AddSeconds(1);
    }
    static MissionPhoto Photo(Fixture f,string target="container",string outcome="matched",bool manual=false)=>new MissionPhoto {
        Id=Guid.NewGuid().ToString("N"),Target=target,ModelVersion=CameraMissionPolicy.ModelVersion,
        Outcome=outcome,MatchedLabel=outcome=="matched"?(target=="book"?"book":"cup"):"",
        Score=outcome=="matched"?.8:0,UserConfirmed=manual,CapturedUtc=f.Clock.Time.ToString("O")};
    static void Attach(Fixture f,string id,MissionPhoto photo)
    {Check(f.Service.BeginPhotoCapture(id,photo.Id,photo.Target),"Capture ready: "+f.Service.LastError);Check(f.Service.RegisterPhoto(id,photo),"Register photo: "+f.Service.LastError);}
    static void Main()
    {
        CameraAtomicDailyCapAndEmotion();SkipManualAndStrictDailyQuota();CaptureFailureRetakeAndBoundary();
        DeleteHistoryAndFullReset();VersionedPreferenceSignals();LivePreferencePersistence();
        Console.WriteLine("Combined camera/recommendation: "+assertions+" assertions PASS (synthetic domain, no Unity/network/device).");
    }
    static void CameraAtomicDailyCapAndEmotion()
    {
        using(var f=new Fixture())
        {
            Check(f.Service.SelectMbctProgram("8w"),"Select course");
            for(int order=1;order<=3;order++)
            {
                if(order>1)f.NextDay();Ready(f,"prior-"+order,"P"+order.ToString("00"),order);
                Check(f.Service.CompletePracticeSession("prior-"+order),"Complete predecessor");
            }
            f.NextDay();Ready(f,"basic-camera","P04",4);
            var photo=Photo(f);Attach(f,"basic-camera",photo);int coins=f.Service.CoinBalance;
            int step=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;
            Check(CameraMissionPolicy.BonusAvailable(f.Service.Snapshot,Session(f,"basic-camera"),1,1),"First daily photo bonus quoted");
            Failed(f,()=>f.Service.CompletePracticeSession("basic-camera"),"Final photo/base/growth transaction");
            f.Reopen();Check(f.Service.CompletePracticeSession("basic-camera"),"Retry final completion");
            Check(f.Service.CoinBalance==coins+51&&Session(f,"basic-camera").RewardAmount==50&&Session(f,"basic-camera").PhotoBonusAmount==1,
                "P04 pays 50 base plus one camera coin in the existing wallet");
            Check(f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==step+1&&f.Service.Snapshot.PhotoRewards.Count==1,
                "Exactly one full plant step and one bonus receipt");
            string completed=Xml(f.Service);int saves=f.Store.Saves;f.Store.Fail=true;
            Check(f.Service.CompletePracticeSession("basic-camera"),"Already committed completion acknowledges even with unavailable storage");f.Store.Fail=false;
            Check(Xml(f.Service)==completed&&f.Store.Saves==saves,"Duplicate final command is a read-only acknowledgement");
            Check(!f.Service.RegisterPhoto("basic-camera",photo),"Late result after final completion rejected");
            Ready(f,"additional-camera");var second=Photo(f,"container","not_found",true);Attach(f,"additional-camera",second);
            second.UserConfirmed=false;Check(Session(f,"additional-camera").Photo.UserConfirmed,"Caller photo object is detached");
            Check(!CameraMissionPolicy.BonusAvailable(f.Service.Snapshot,Session(f,"additional-camera"),1,1),"P and M share one daily photo allowance");
            Check(f.Service.CompletePracticeSession("additional-camera"),"Same-day additional remains independent of basic slot");
            Check(f.Service.CoinBalance==coins+101&&Session(f,"additional-camera").PhotoBonusAmount==0,
                "Second camera activity earns only its 50 base coins");
            Check(!f.Service.BeginFocusSession("second-additional","M14"),"Different additional mission blocked after today's M completion");
            Check(!f.Service.BeginSession("legacy-additional","free-mission:M14"),"Legacy additional path shares daily allowance");
            var state=f.Service.Snapshot;string cycle=state.LastCycleId;
            Check(f.Service.BeginDailyEmotion(cycle),"Third daily emotion opens after P and M");
            string recordId=DailyEmotionPolicy.RecordId(cycle);int beforeStep=state.PlantAppearance.Recipe.GrowthStep;
            int beforeGrowth=state.PracticeGrowthRecords.Count;
            Failed(f,()=>f.Service.CompleteDailyEmotion(recordId,"편안함","synthetic memo"),"Daily emotion memo/100-coin transaction");
            Check(f.Service.CompleteDailyEmotion(recordId,"편안함","synthetic memo"),"Daily emotion retry");
            state=f.Service.Snapshot;Check(state.Coins==coins+201&&state.DailyEmotionRecords.Single().RewardAmount==100,"Emotion pays 100 coins");
            Check(state.PlantAppearance.Recipe.GrowthStep==beforeStep&&state.PracticeGrowthRecords.Count==beforeGrowth,
                "Emotion grants no plant growth");
            Check(state.PlantAppearance.Memos.Count==1&&state.PhotoRewards.Count==1,"Emotion adds one memo and cannot consume or create a camera bonus");
            string final=Xml(f.Service);Check(f.Service.CompleteDailyEmotion(recordId,"다른 감정","late duplicate"),"Emotion duplicate acknowledged");
            Check(Xml(f.Service)==final,"Duplicate emotion preserves original memo and all receipts");
            f.Reopen();Check(Xml(f.Service)==final,"Completed reward and photo state restores exactly");
        }
    }
    static void SkipManualAndStrictDailyQuota()
    {
        foreach(string outcome in new[]{"skip","not_found","unavailable","matched"})
        using(var f=new Fixture())
        {
            Ready(f,"optional");
            if(outcome=="skip")Check(f.Service.SkipPhoto("optional"),"Optional photo can be skipped");
            else Attach(f,"optional",Photo(f,"container",outcome,outcome=="unavailable"));
            Check(f.Service.CompletePracticeSession("optional"),"Camera never blocks practice completion");
            int bonus=outcome=="matched"||outcome=="unavailable"?1:0;
            Check(f.Service.CoinBalance==50+bonus&&Session(f,"optional").PhotoBonusAmount==bonus,"Optional/manual outcome reward "+outcome);
            Check(f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==1,"Every successful M grants exactly one full step");
            Check(!f.Service.BeginFocusSession("another","M14"),"Daily additional limit remains global: "+outcome);
        }
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=FocusMissionCatalog.Version,MissionId="course:P04"})=="container","Current guided camera supported");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=4,MissionId="course:P04"})=="container","Historical guided v4 P04 camera supported");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=3,MissionId="free-mission:M14"})=="book","Historical texture camera supported");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=2,MissionId="free-mission:M03"})=="container","Historical v2 camera supported");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=1,MissionId="free-mission:M03"})==null,"Legacy v1 remains excluded");
        Check(CameraMissionPolicy.Target(new ActivitySession{FocusVersion=99,MissionId="free-mission:M03"})==null,"Unknown version remains excluded");
    }
    static void CaptureFailureRetakeAndBoundary()
    {
        using(var f=new Fixture())
        {
            f.Clock.Time=DateTimeOffset.Parse("2032-01-06T03:55:00Z");Check(f.Service.RefreshCycle(Array.Empty<string>()),"Pre-04:00 cycle");
            Ready(f,"boundary","M14");var old=Photo(f,"book");var current=Photo(f,"book");
            Failed(f,()=>f.Service.BeginPhotoCapture("boundary",old.Id,old.Target),"Capture registration");
            Check(f.Service.BeginPhotoCapture("boundary",old.Id,old.Target),"Capture retry");
            Check(f.Service.BeginPhotoCapture("boundary",current.Id,current.Target),"Explicit retake creates new pending ID");
            string before=Xml(f.Service);Check(!f.Service.RegisterPhoto("boundary",old)&&Xml(f.Service)==before,"Old capture callback cannot replace a retake");
            Failed(f,()=>f.Service.RegisterPhoto("boundary",current),"Photo metadata storage");
            f.Reopen();Check(Session(f,"boundary").PendingPhotoId==current.Id,"Pending capture restored");
            Check(f.Service.RegisterPhoto("boundary",current),"Retry current result after reopening");
            before=Xml(f.Service);Check(!f.Service.RegisterPhoto("boundary",current)&&Xml(f.Service)==before,"Duplicate register cannot overwrite first result");
            f.Clock.Time=f.Clock.Time.AddMinutes(10);
            Failed(f,()=>f.Service.CompletePracticeSession("boundary"),"04:00 completion and cycle reassignment");
            Check(f.Service.CompletePracticeSession("boundary"),"Retry across 04:00");
            Check(Session(f,"boundary").InstanceId=="2032-01-06"&&f.Service.Snapshot.PhotoRewards.Single().Cycle=="2032-01-06",
                "Photo bonus and practice finalization belong to the actual successful app day");
            Check(f.Service.CoinBalance==51,"Boundary retry pays once");
        }
        using(var f=new Fixture())
        {
            Ready(f,"invalid");var photo=Photo(f);
            Check(!f.Service.BeginPhotoCapture("invalid","../escape","container"),"No path identifiers");
            Check(!f.Service.BeginPhotoCapture("invalid",photo.Id,"book"),"Target tied to mission");
            Check(f.Service.BeginPhotoCapture("invalid",photo.Id,photo.Target),"Prepare invalid result tests");
            foreach(string fault in new[]{"label","nan","threshold","model"})
            {
                var value=Photo(f);value.Id=photo.Id;
                if(fault=="label")value.MatchedLabel="book";if(fault=="nan")value.Score=double.NaN;
                if(fault=="threshold")value.Score=.59;if(fault=="model")value.ModelVersion="unsupported";
                string before=Xml(f.Service);Check(!f.Service.RegisterPhoto("invalid",value)&&Xml(f.Service)==before,"Reject invalid result without losing pending capture: "+fault);
            }
            Check(f.Service.MoveFocusStep("invalid",0),"Return to practice");
            Check(Session(f,"invalid").PendingPhotoId==null&&!f.Service.RegisterPhoto("invalid",photo),"Moving back invalidates a pending camera result");
        }
    }
    static void DeleteHistoryAndFullReset()
    {
        using(var f=new Fixture())
        {
            Ready(f,"original");var photo=Photo(f);Attach(f,"original",photo);Check(f.Service.CompletePracticeSession("original"),"Complete reset source");
            Failed(f,()=>f.Service.DeletePhoto("original"),"Photo deletion metadata");
            Check(f.Service.DeletePhoto("original")&&Session(f,"original").Photo==null,"Photo can be deleted");
            Check(f.Service.CoinBalance==51&&f.Service.Snapshot.PhotoRewards.Count==1&&Session(f,"original").PhotoBonusAmount==1,
                "Deleting a photo cannot erase or reissue its reward receipt");
            f.Reopen();Check(f.Service.CoinBalance==51&&f.Service.Snapshot.PhotoRewards.Count==1,"Deletion receipt survives restart");
            var oldXml=new XmlDocument();oldXml.LoadXml(Xml(f.Service));
            foreach(XmlNode node in oldXml.SelectNodes("//Photo|//PhotoRewards|//PhotoBonusAmount|//PhotoPromptHandled|//PendingPhotoId|//PendingPhotoTarget"))node.ParentNode.RemoveChild(node);
            using(var legacy=new Fixture())
            {
                legacy.Clock.Time=f.Clock.Time;legacy.Store.State=StateCodec.Decode(oldXml.OuterXml);legacy.Reopen();
                Check(legacy.Service.CoinBalance==51&&legacy.Service.Snapshot.PhotoRewards.Count==0,"Missing optional camera fields preserve the existing wallet");
                string oldState=Xml(legacy.Service);
                Check(legacy.Service.CompletePracticeSession("original")&&Xml(legacy.Service)==oldState,"Old completed state receives no retroactive camera award");
            }
            int beforeReview=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;
            Ready(f,"review","M03",review:true);Attach(f,"review",Photo(f));
            Check(f.Service.CompletePracticeSession("review"),"Review can finalize with optional photo");
            Check(f.Service.CoinBalance==51&&Session(f,"review").RewardAmount==0&&Session(f,"review").PhotoBonusAmount==0
                &&f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==beforeReview&&f.Service.Snapshot.PhotoRewards.Count==1,
                "Review never bypasses growth/base/camera allowance");
            Check(f.Service.ResetMissionHistory(),"Reset visible mission history");
            Check(f.Service.Snapshot.PhotoRewards.Count==1,"History reset retains daily photo receipt");
            Ready(f,"after-history");Attach(f,"after-history",Photo(f));Check(f.Service.CompletePracticeSession("after-history"),"Recreated completed mission may finalize");
            Check(f.Service.CoinBalance==51&&Session(f,"after-history").RewardAmount==0&&Session(f,"after-history").PhotoBonusAmount==0,
                "History reset cannot replay base/growth/photo rewards");
            string install=f.Service.Snapshot.InstallationId;
            Failed(f,()=>f.Service.ResetAll("synthetic-full-reset",install),"Full reset first commit");
            Check(f.Service.ResetAll("synthetic-full-reset",install)&&f.Service.HasPendingFullReset,"Commit fresh reset generation");
            Check(f.Service.Snapshot.Coins==0&&f.Service.Snapshot.PhotoRewards.Count==0&&f.Service.Snapshot.Sessions.Count==0,"Full reset clears wallet and camera history");
            Check(!f.Service.RegisterPhoto("original",photo)&&!f.Service.CompletePracticeSession("original"),"Pending reset rejects old camera/completion commands");
            Failed(f,()=>f.Service.FinishFullReset("synthetic-full-reset"),"Full reset cleanup completion");
            f.Reopen();Check(f.Service.HasPendingFullReset,"Pending full reset survives reopening");
            Check(f.Service.FinishFullReset("synthetic-full-reset"),"Finish reset retry");
            string fresh=Xml(f.Service);Check(!f.Service.RegisterPhoto("original",photo)&&!f.Service.CompletePracticeSession("original")&&Xml(f.Service)==fresh,
                "Old requests cannot award coins in the new installation");
        }
    }
    static readonly DateTimeOffset Now=DateTimeOffset.Parse("2032-02-10T12:00:00Z");
    static GardenState PreferenceState()=>new GardenState {
        LastCycleId="2032-02-10",PracticePreferences=new List<PracticeAnswer>{new PracticeAnswer{Key="favorite",Value="잠시 멈추기"}}};
    static ActivitySession CompletedPreference(int version,string code,params string[] pairs)
    {
        var def=FocusMissionCatalog.Find(code,version);var values=new Dictionary<string,string>();
        for(int i=0;i<pairs.Length;i+=2)values[pairs[i]]=pairs[i+1];
        return new ActivitySession {SessionId="synthetic-preference-"+version+"-"+code,MissionId=def.Id,FocusVersion=version,FocusRole=def.Role,
            Status=ParticipationState.RewardCommitted,SelectedUtc=Now.AddHours(-1).ToString("O"),StartedUtc=Now.AddMinutes(-50).ToString("O"),EndedUtc=Now.AddMinutes(-10).ToString("O"),
            FocusStepRecords=def.Steps.Select((step,i)=>new FocusStepRecord{Key=step.Key,Value=values.ContainsKey(step.Key)?values[step.Key]:step.Options.FirstOrDefault()??"",
                ConfirmedUtc=Now.AddMinutes(-40+i).ToString("O"),UpdatedUtc=Now.AddMinutes(-40+i).ToString("O")}).ToList()};
    }
    static FocusAdditionalRecommendation Decision(GardenState state)=>FocusAdditionalRecommendationPolicy.Select(state,Now);
    static string RawXml(GardenState state){using(var writer=new StringWriter()){new XmlSerializer(typeof(GardenState)).Serialize(writer,state);return writer.ToString();}}
    static void VersionedPreferenceSignals()
    {
        Check(FocusMissionCatalog.Version==5,"Integrated purpose catalog is explicitly v5");
        Check(FocusMissionCatalog.Find("P07",4).Steps.Select(s=>s.Key).SequenceEqual(new[]{"practice","notice"}),"New guided P07 keeps two durable steps");
        Check(FocusMissionCatalog.Find("P07",3).Steps.Any(s=>s.Key=="next"),"Historical texture P07 next response remains readable");
        foreach(int version in new[]{2,3,4,5})
        {
            var state=PreferenceState();state.Sessions.Add(CompletedPreference(version,"P07",version>=4?"notice":"next","호흡"));
            var result=Decision(state);Check(result.MissionId=="free-mission:M17"&&result.Reason=="focus-method-preference","Exact future choice recognized in version "+version);
            var changed=result.Features;changed[0]=99;Check(result.Features[0]==1,"Recommendation features are detached");
        }
        foreach(int version in new[]{3,4,5})
        foreach(string fault in new[]{"current-choice","unconfirmed","draft","skipped","future","free-text"})
        {
            var state=PreferenceState();string key=version>=4?"notice":"next";
            var session=CompletedPreference(version,"P07",version>=4?"practice":"activity","호흡",key,"호흡");
            var record=session.FocusStepRecords.Single(r=>r.Key==key);
            if(fault=="current-choice")record.Value="잘 모르겠음";
            if(fault=="unconfirmed")record.ConfirmedUtc=null;
            if(fault=="draft")session.Status=ParticipationState.Paused;
            if(fault=="skipped")record.Skipped=true;
            if(fault=="future")record.ConfirmedUtc=Now.AddMinutes(1).ToString("O");
            if(fault=="free-text")record.Value="호흡을 더 자주 해주세요";
            session.Mood="호흡";session.Note="더 자주 호흡 연습";state.Sessions.Add(session);
            string before=RawXml(state);var result=Decision(state);
            Check(result.MissionId=="free-mission:M09"&&result.Reason=="saved-preference","No recommendation inference from "+version+"/"+fault+" or mood/note");
            Check(RawXml(state)==before,"Recommendation is read-only for "+fault);
        }
        var unknown=PreferenceState();unknown.Sessions.Add(CompletedPreference(4,"P07","notice","없음"));
        Check(FocusAdditionalRecommendationPolicy.PreferenceFor(unknown,Now).MissionId==null,"Explicit none supersedes a previous preference without inventing another activity");
        var v4=PreferenceState();v4.Sessions.Add(CompletedPreference(4,"P14","activity","호흡","preference","덜 자주"));
        Check(FocusAdditionalRecommendationPolicy.PreferenceFor(v4,Now).Direction==-1,"Explicit frequency preference is retained without partial-growth policy");
        var active=CompletedPreference(4,"M14");active.Status=ParticipationState.Paused;v4.Sessions.Add(active);
        Check(Decision(v4).MissionId=="free-mission:M14"&&Decision(v4).Reason=="resume-active-activity","Unfinished additional activity retains priority");
    }
    static void LivePreferencePersistence()
    {
        using(var f=new Fixture())
        {
            Check(f.Service.SelectMbctProgram("8w"),"Choose course for real P07 preference");
            for(int order=1;order<=7;order++)
            {
                if(order>1)do{f.NextDay();}while(FocusMissionPolicy.Availability(f.Service.Snapshot,"8w")!=null);
                string id="actual-basic-"+order;
                Ready(f,id,"P"+order.ToString("00"),order,order==7?"몸의 접촉":null,order==7?"호흡":null);
                if(order==7)Check(f.Service.SaveFocusReflection(id,"불편함","synthetic text prefers silence",0),"Retain an older reflection draft");
                Check(f.Service.CompletePracticeSession(id),"Complete real P"+order+": "+f.Service.LastError);
            }
            Check(f.Service.CoinBalance==350,"Seven basic missions retain 50-coin completion policy");
            f.Reopen();var decision=FocusAdditionalRecommendationPolicy.Select(f.Service.Snapshot,f.Clock.UtcNow);
            Check(decision.MissionId=="free-mission:M17"&&decision.Reason=="focus-method-preference","Saved current explicit future response reaches recommendation after reopening");
            Check(Session(f,"actual-basic-7").Mood=="불편함"&&Session(f,"actual-basic-7").Note=="synthetic text prefers silence","Old mood/note remain preserved without influencing recommendation");
            Failed(f,()=>f.Service.SavePreference("favorite","잠시 멈추기"),"Preference setting");
            f.Clock.Time=f.Clock.Time.AddMinutes(2);Check(f.Service.SavePreference("favorite","잠시 멈추기"),"Retry explicit later setting");
            f.Reopen();Check(FocusAdditionalRecommendationPolicy.Select(f.Service.Snapshot,f.Clock.UtcNow).MissionId=="free-mission:M09","Later persisted explicit setting wins");
        }
    }
}
