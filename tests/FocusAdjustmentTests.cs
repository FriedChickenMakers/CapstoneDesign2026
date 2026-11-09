using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

sealed class AdjustmentClock:IClock
{
    public DateTimeOffset Time=DateTimeOffset.Parse("2030-01-01T12:00:00Z");
    public DateTimeOffset UtcNow=>Time;
    public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
}
sealed class AdjustmentStore:IStateStore
{
    public GardenState State;public bool Fail;
    public GardenState Load()=>State==null?null:StateCodec.Clone(State);
    public void Save(GardenState state){if(Fail)throw new IOException("Synthetic adjustment storage failure");State=StateCodec.Clone(state);}
}
static class FocusAdjustmentTests
{
    static int assertions;
    static void Check(bool value,string reason){assertions++;if(!value)throw new Exception(reason);}
    static ActivitySession Session(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(row=>row.SessionId==id);
    static GardenStateService Open(AdjustmentStore store,AdjustmentClock clock)
    {
        var service=new GardenStateService(store,clock,new DemoBalanceConfig());
        Check(service.Open(),"open "+service.LastError);Check(service.RefreshCycle(Array.Empty<string>()),"refresh cycle");return service;
    }
    static void Begin(GardenStateService service,string id,string code,int order=0)
    {Check(service.BeginFocusSession(id,code,order),"select "+code+" "+service.LastError);Check(service.StartSession(id),"start "+code);}
    static void Finish(GardenStateService service,string id)
    {
        var definition=FocusMissionCatalog.FindForSession(Session(service,id));
        while(Session(service,id).InstructionStep<definition.Steps.Length)
        {
            int index=Session(service,id).InstructionStep;var step=definition.Steps[index];
            string answer=step.Kind=="draw"?"DRAW1|AA_B":step.Options.FirstOrDefault()??(step.RequiredAnswer?"synthetic plan":"");
            Check(service.ConfirmFocusStep(id,index,answer,string.IsNullOrEmpty(answer)),"finish step "+definition.Code+" "+index+" "+service.LastError);
        }
        Check(service.SaveFocusReflection(id,"","",1),"omit optional reflection");
        Check(service.CompleteFocusSession(id),"complete "+service.LastError);
    }
    static void Main()
    {
        DurationAndCancelledPause();StartAndSkipEligibility();AlternativeRoutes();PlanAndRecommendation();GuidedDuration();LegacyDurationResume();CorruptContractRecords();EndedGuidedCallbacks();
        Console.WriteLine("Focus adjustment integration: "+assertions+" assertions PASS");
    }
    static void DurationAndCancelledPause()
    {
        var clock=new AdjustmentClock();var store=new AdjustmentStore();var service=Open(store,clock);
        Begin(service,"synthetic-timed","M02");
        Check(!service.StartFocusPracticeStep("synthetic-timed",0),"preparation choice is not actual practice start");
        Check(service.ConfirmFocusStep("synthetic-timed",0),"prepare scene");
        Check(!service.ShortenFocusStep("synthetic-timed",1,4),"cannot shorten before actual timer start");
        string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.StartFocusPracticeStep("synthetic-timed",1)&&StateCodec.Encode(service.Snapshot)==before,"failed actual start leaves marker and recommendation uncommitted");store.Fail=false;
        Check(service.StartFocusPracticeStep("synthetic-timed",1),"actual timed start");
        var started=Session(service,"synthetic-timed").FocusStepRecords[1];
        Check(!string.IsNullOrEmpty(started.PracticeStartedUtc)&&started.RecommendedSeconds==30,"start captures product baseline separately from elapsed");
        Check(service.SaveFocusElapsed("synthetic-timed",1,7),"actual elapsed saved");
        Check(service.PauseSession("synthetic-timed")&&service.ResumeSession("synthetic-timed"),"popup cancellation can pause and resume");
        var paused=Session(service,"synthetic-timed").FocusStepRecords[1];
        Check(paused.EndReason==null&&paused.ShortenedUtc==null&&paused.ElapsedSeconds==7,"pause/resume produces no shortening feedback");
        before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.ShortenFocusStep("synthetic-timed",1,7)&&StateCodec.Encode(service.Snapshot)==before,"failed shortening cannot advance or create feedback");store.Fail=false;
        Check(service.ShortenFocusStep("synthetic-timed",1,7),"confirmed current-segment shortening");
        var shortened=Session(service,"synthetic-timed");
        Check(shortened.InstructionStep==2&&shortened.Status==ParticipationState.InProgress&&shortened.RewardAmount==0,"shortening advances only current segment without completing mission or reward");
        Check(shortened.FocusStepRecords[1].ElapsedSeconds==7&&shortened.FocusStepRecords[1].RecommendedSeconds==30&&shortened.FocusStepRecords[1].EndReason=="too_long","actual duration, recommendation and ending reason remain distinct");
        Check(service.ShortenFocusStep("synthetic-timed",1,28)&&Session(service,"synthetic-timed").FocusStepRecords[1].ElapsedSeconds==7,"retry cannot rewrite first committed shortening");
        service=Open(store,clock);Finish(service,"synthetic-timed");
        Check(service.Snapshot.Nutrient==50&&service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"short but all-stages completion keeps normal reward only");
        clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"new daily additional allowance");
        Begin(service,"synthetic-next","M02");Check(service.ConfirmFocusStep("synthetic-next",0),"next prepare");
        Check(service.RecommendedFocusSeconds("synthetic-next",1)==7,"next attempt uses confirmed observed stopping time");
        Check(service.StartFocusPracticeStep("synthetic-next",1),"next actual start freezes recommendation");
        clock.Time=clock.Time.AddDays(1);service=Open(store,clock);
        Check(service.RecommendedFocusSeconds("synthetic-next",1)==7&&Session(service,"synthetic-next").FocusStepRecords[1].ElapsedSeconds==0,"restart/date rollover preserves suggestion without inventing performance");
    }
    static void StartAndSkipEligibility()
    {
        var clock=new AdjustmentClock();var store=new AdjustmentStore();var service=Open(store,clock);
        Check(service.BeginFocusSession("synthetic-overview","M02"),"select only");Check(service.EndFocusParticipation("synthetic-overview"),"skip introduction");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"introduction-only skip receives no growth");
        Begin(service,"synthetic-prepare","M02");Check(service.EndFocusParticipation("synthetic-prepare"),"skip before actual activity");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"session start in preparation is not practice start");
        Begin(service,"synthetic-real","M02");Check(service.ConfirmFocusStep("synthetic-real",0)&&service.StartFocusPracticeStep("synthetic-real",1),"real activity started");
        Check(service.SaveFocusElapsed("synthetic-real",1,9)&&service.PauseSession("synthetic-real"),"pause after effort");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"pause does not issue participation reward");
        Check(service.ResumeSession("synthetic-real")&&service.EndFocusParticipation("synthetic-real"),"explicit mission skip after actual start");
        Check(service.Snapshot.Nutrient==0&&service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"actual incomplete practice records effort without partial growth");
        Begin(service,"synthetic-after-skip","M02");Check(service.ConfirmFocusStep("synthetic-after-skip",0),"prepare after ordinary skip");
        Check(service.RecommendedFocusSeconds("synthetic-after-skip",1)==30,"ordinary mission skip is not silently interpreted as too-long feedback");
        Check(service.StartFocusPracticeStep("synthetic-after-skip",1)&&service.EndFocusParticipation("synthetic-after-skip"),"second incomplete same-day activity");
        Check(service.Snapshot.PlantCollection.ParticipationDecisions.Count==0,"repeated incomplete participation cannot create growth");
        service=Open(store,clock);Check(Session(service,"synthetic-real").FocusEndReason=="skipped","ending reason survives reload");
    }
    static GardenStateService AtCourse(AdjustmentStore store,AdjustmentClock clock,int target)
    {
        var service=Open(store,clock);Check(service.SelectMbctProgram("8w"),"select curriculum");
        for(int order=1;order<target;order++)
        {
            while(FocusMissionPolicy.Availability(service.Snapshot)!=null){clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"next available day");}
            string id="synthetic-source-before-"+order;Begin(service,id,FocusMissionPolicy.MissionAt(order,"8w").Code,order);Finish(service,id);
        }
        while(FocusMissionPolicy.Availability(service.Snapshot)!=null){clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"alternative day");}
        return service;
    }
    static void AlternativeRoutes()
    {
        foreach(int order in new[]{19,26})
        {
            var store=new AdjustmentStore();var clock=new AdjustmentClock();var service=AtCourse(store,clock,order);
            string code="P"+order,answer=order==19?"짧은 소리 실습":"아직 없어요";
            Begin(service,"synthetic-source",code,order);
            Check(!service.ConfirmFocusStep("synthetic-source",0,answer),"different-purpose branch cannot advance original through generic confirm "+code);
            string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
            Check(!service.BeginFocusAlternative("synthetic-source",0,answer,"synthetic-support")&&StateCodec.Encode(service.Snapshot)==before,"failed alternative leaves original and support atomic");store.Fail=false;
            Check(service.BeginFocusAlternative("synthetic-source",0,answer,"synthetic-support"),"create actual separate support activity "+service.LastError);
            var source=Session(service,"synthetic-source");var support=Session(service,"synthetic-support");
            Check(source.Status==ParticipationState.Ended&&source.FocusEndReason=="alternative"&&FocusMissionPolicy.NextOrder(service.Snapshot,"8w")==order,"source occurrence remains pending");
            Check(support.MissionId=="course:P02"&&support.FocusRole=="additional"&&support.FocusSupport&&support.FocusSourceSessionId==source.SessionId&&support.FocusSourceMissionId==source.MissionId,"support records actual sound identity and origin");
            Check(service.BeginFocusAlternative("synthetic-source",0,answer,"synthetic-support")&&service.Snapshot.Sessions.Count(s=>s.SessionId=="synthetic-support")==1,"alternative retry does not create duplicate activity");
            service=Open(store,clock);Check(service.StartSession("synthetic-support"),"start support after restart");Finish(service,"synthetic-support");
            Check(FocusMissionPolicy.NextOrder(service.Snapshot,"8w")==order&&Session(service,"synthetic-support").RewardAmount==50,"support can complete without original course progression");
            Check(!service.BeginFocusSession("synthetic-after-support-extra","M01"),"separate support consumes daily additional allowance");
            Begin(service,"synthetic-source-retry",code,order);
            string fullDay=StateCodec.Encode(service.Snapshot);
            Check(!service.BeginFocusAlternative("synthetic-source-retry",0,answer,"synthetic-second-support")&&StateCodec.Encode(service.Snapshot)==fullDay,"daily limit rejects fallback atomically without ending basic source");
            Check(Session(service,"synthetic-source-retry").FocusOrder==order,"same original occurrence remains retryable");
            var corrupt=service.Snapshot;corrupt.Sessions.Single(s=>s.SessionId=="synthetic-support").FocusSourceMissionId=order==19?"course:P26":"course:P19";
            bool rejected=false;try{StateCodec.Decode(StateCodec.Encode(corrupt));}catch(InvalidDataException){rejected=true;}
            Check(rejected,"support origin must match its actual source rather than any approved source string");
        }
    }
    static void GuidedDuration()
    {
        var clock=new AdjustmentClock();var store=new AdjustmentStore();var service=Open(store,clock);
        Check(service.SelectMbctProgram("8w"),"guided course selected");
        Check(service.BeginFocusSession("guided-short","P01",1),"guided select");
        Check(service.StartGuidedFocusSession("guided-short","코"),"guided start");
        string before=StateCodec.Encode(service.Snapshot);
        Check(!service.ShortenGuidedFocusPractice("guided-short","코","",12)&&StateCodec.Encode(service.Snapshot)==before,"guided cannot shorten before actual countdown starts");
        Check(service.StartFocusPracticeStep("guided-short",0),"guided timer start captures baseline");
        before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.ShortenGuidedFocusPractice("guided-short","코","",12)&&StateCodec.Encode(service.Snapshot)==before,"guided failed save cannot confirm practice or optional notice");store.Fail=false;
        Check(service.ShortenGuidedFocusPractice("guided-short","코","",12),"guided short choice accepted");
        var current=Session(service,"guided-short");
        Check(current.InstructionStep==2&&current.FocusStepRecords.All(r=>!string.IsNullOrEmpty(r.ConfirmedUtc))&&current.FocusStepRecords[0].EndReason=="too_long"&&current.FocusStepRecords[0].ElapsedSeconds==12&&current.RewardAmount==0,"guided short preserves both steps, actual duration, optional notice and no premature reward");
        Check(service.ShortenGuidedFocusPractice("guided-short","배","edited",55)&&Session(service,"guided-short").FocusStepRecords[0].ElapsedSeconds==12,"late retry cannot replace first actual response");
        service=Open(store,clock);Finish(service,"guided-short");
        Check(Session(service,"guided-short").RewardAmount==50&&FocusMissionPolicy.NextOrder(service.Snapshot,"8w")==2,"fully completed short guided practice receives normal coin/progression");
        before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(service.ShortenGuidedFocusPractice("guided-short","배","late",99)&&service.CompleteGuidedFocusPractice("guided-short","배","late",99)&&StateCodec.Encode(service.Snapshot)==before,"already rewarded callbacks retain idempotent success without rewriting state");store.Fail=false;
        Check(service.BeginFocusSession("guided-review","P01",1,true),"completed guided review");
        Check(service.StartGuidedFocusSession("guided-review","코")&&service.RecommendedFocusSeconds("guided-review",0)==12,"same-contract next practice uses confirmed duration");
        Check(service.EndFocusParticipation("guided-review"),"review skip");
        clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"next course day");
        Check(service.BeginFocusSession("guided-skipped","P02",2)&&service.StartGuidedFocusSession("guided-skipped",""),"next course practice");
        Check(service.EndFocusParticipation("guided-skipped")&&FocusMissionPolicy.NextOrder(service.Snapshot,"8w")==2&&Session(service,"guided-skipped").RewardAmount==0,"skip defers current basic occurrence with no coins");
        // A future definition requiring notice must remain fail-closed at the shared confirmation boundary.
        var notice=FocusMissionCatalog.Find("P01").Steps[1];bool oldRequired=notice.RequiredAnswer;
        try {
            service=Open(new AdjustmentStore(),new AdjustmentClock());Check(service.SelectMbctProgram("8w"),"separate required-notice fixture");
            Check(service.BeginFocusSession("guided-required","P01",1)&&service.StartGuidedFocusSession("guided-required","코")&&service.StartFocusPracticeStep("guided-required",0),"set up required-notice guard");
            notice.RequiredAnswer=true;before=StateCodec.Encode(service.Snapshot);
            Check(!service.ShortenGuidedFocusPractice("guided-required","코","",10)&&StateCodec.Encode(service.Snapshot)==before,"shortening cannot bypass a required notice answer");
        } finally {notice.RequiredAnswer=oldRequired;}
    }
    static void UseHistoricalDefinition(AdjustmentStore store,GardenStateService service,string id,int version)
    {
        var state=service.Snapshot;var session=state.Sessions.Single(item=>item.SessionId==id);
        var definition=FocusMissionCatalog.Find(session.MissionId,version);
        session.FocusVersion=version;session.MissionTitleSnapshot=definition.Title;session.MissionDescriptionSnapshot=definition.Overview;
        session.FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord{Step=index,Key=step.Key,Title=step.Title,Prompt=step.Body}).ToList();
        store.State=StateCodec.Clone(state);
    }
    static void LegacyDurationResume()
    {
        foreach(int version in new[]{2,3})
        {
            var store=new AdjustmentStore();var clock=new AdjustmentClock();var service=Open(store,clock);
            Check(service.SelectMbctProgram("8w")&&service.BeginFocusSession("legacy-first","P01",1),"prepare historical duration fixture");
            UseHistoricalDefinition(store,service,"legacy-first",version);service=Open(store,clock);
            Check(service.StartSession("legacy-first")&&service.ConfirmFocusStep("legacy-first",0),"historical preparation");
            Check(service.StartFocusPracticeStep("legacy-first",1)&&service.ShortenFocusStep("legacy-first",1,17),"historical breath confirms actual shorter duration");
            Finish(service,"legacy-first");
            Check(service.BeginFocusSession("legacy-next","P01",1,true),"prepare historical next-attempt fixture");
            UseHistoricalDefinition(store,service,"legacy-next",version);service=Open(store,clock);
            Check(service.RecommendedFocusSeconds("legacy-next",1)==17,"legacy breath key retains confirmed duration feedback in version "+version);
            Check(service.StartSession("legacy-next")&&service.ConfirmFocusStep("legacy-next",0)&&service.StartFocusPracticeStep("legacy-next",1),"historical next practice starts");
            service=Open(store,clock);
            Check(service.RecommendedFocusSeconds("legacy-next",1)==17&&Session(service,"legacy-next").FocusStepRecords[1].ElapsedSeconds==0,"historical recommendation freezes without inventing elapsed time");
        }
    }
    static bool InvalidState(GardenState state)
    {
        try{StateCodec.Encode(state);return false;}catch(InvalidDataException){return true;}
    }
    static void CorruptContractRecords()
    {
        foreach(int order in new[]{19,26})
        {
            var store=new AdjustmentStore();var clock=new AdjustmentClock();var service=AtCourse(store,clock,order);
            Begin(service,"integrity-source","P"+order,order);Finish(service,"integrity-source");
            string valid=StateCodec.Encode(service.Snapshot);
            foreach(int version in new[]{3,5})
            {
                var corrupt=service.Snapshot;var source=corrupt.Sessions.Single(item=>item.SessionId=="integrity-source");
                source.FocusVersion=version;source.FocusStepRecords.First(item=>item.Key=="route").Value=order==19?"짧은 소리 실습":"아직 없어요";
                Check(InvalidState(corrupt),"committed source cannot claim a different-purpose branch in version "+version+"/P"+order);
                Check(StateCodec.Encode(service.Snapshot)==valid,"rejected source corruption leaves original state intact");
            }

        }
        foreach(int version in new[]{3,5})
        {
            var store=new AdjustmentStore();var clock=new AdjustmentClock();var service=Open(store,clock);
            Begin(service,"preparation-marker","M02");var corrupt=service.Snapshot;
            var session=corrupt.Sessions.Single();session.FocusVersion=version;session.FocusStepRecords[0].PracticeStartedUtc=session.StartedUtc;
            Check(InvalidState(corrupt),"preparation-only step cannot claim actual practice start in version "+version);
            Check(Session(service,"preparation-marker").FocusStepRecords[0].PracticeStartedUtc==null,"invalid start marker cannot mutate source");
        }
    }
    static void EndedGuidedCallbacks()
    {
        foreach(bool shortened in new[]{false,true})
        {
            var store=new AdjustmentStore();var clock=new AdjustmentClock();var service=Open(store,clock);
            Check(service.SelectMbctProgram("8w")&&service.BeginFocusSession("ended-guided","P01",1)&&service.StartGuidedFocusSession("ended-guided","코")&&service.StartFocusPracticeStep("ended-guided",0),"start guided stale-callback fixture");
            Check(shortened?service.ShortenGuidedFocusPractice("ended-guided","코","",12):service.CompleteGuidedFocusPractice("ended-guided","코","",12),"confirm guided practice");
            Check(service.EndFocusParticipation("ended-guided"),"end after practice before final reward");
            string before=StateCodec.Encode(service.Snapshot);store.Fail=true;
            Check(!service.CompleteGuidedFocusPractice("ended-guided","배","late",55),"late normal completion is rejected after end");
            Check(!service.ShortenGuidedFocusPractice("ended-guided","배","late",55),"late shortening is rejected after end");store.Fail=false;
            Check(StateCodec.Encode(service.Snapshot)==before&&Session(service,"ended-guided").RewardAmount==0,"late callback keeps ended status, original answers and no reward");
        }
    }
    static void PlanAndRecommendation()
    {
        var clock=new AdjustmentClock();var store=new AdjustmentStore();var service=Open(store,clock);
        Begin(service,"synthetic-plan","M10");
        string value=FocusMissionAdjustmentPolicy.ComposePlan("차 마시기","저녁");
        Check(service.SaveFocusPlan("synthetic-plan",0,"차 마시기","", ""),"incomplete activity choice remains a resumable draft");
        service=Open(store,clock);
        Check(Session(service,"synthetic-plan").FocusStepRecords[0].PlanActivity=="차 마시기"&&Session(service,"synthetic-plan").FocusStepRecords[0].Value=="","unfinished choice is not invented into a completed plan");
        string draft=StateCodec.Encode(service.Snapshot);
        Check(!service.SaveFocusPlan("synthetic-plan",0,"차 마시기","12:00", "")&&StateCodec.Encode(service.Snapshot)==draft,"notification clock is rejected without corrupting structured plan draft");
        Check(service.SaveFocusPlan("synthetic-plan",0,"차 마시기","저녁", ""),"save personal period separately from alert time");
        Check(service.ConfirmFocusStep("synthetic-plan",0,value),"choice-only plan is valid without text or exact clock");
        service=Open(store,clock);Check(Session(service,"synthetic-plan").FocusStepRecords[0].Value==value&&Session(service,"synthetic-plan").FocusStepRecords[0].PlanTimePeriod=="저녁","coarse plan restores as actually selected");
        Finish(service,"synthetic-plan");
        clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"new additional day after plan completion");
        string before=StateCodec.Encode(service.Snapshot);var decision=FocusAdditionalRecommendationPolicy.Select(service.Snapshot,service.CurrentUtc);
        Check(StateCodec.Encode(service.Snapshot)==before&&decision.MissionId!="free-mission:M10","recommendation cannot mutate state or suggest just-completed plan while alternatives exist");
        Check(decision.PolicyId==FocusAdditionalRecommendationPolicy.PolicyId&&!string.IsNullOrEmpty(decision.Reason),"local recommendation has explainable policy snapshot");
        string mismatched=FocusMissionCatalog.Additional.First(m=>m.Id!=decision.MissionId).Id;
        Check(!service.BeginFocusSession("synthetic-mismatched",mismatched,recommendation:decision),"recommendation cannot be attached to another actual activity");
        Check(service.BeginFocusSession("synthetic-recommended",decision.MissionId,recommendation:decision),"select recommendation with frozen attribution");
        var selected=Session(service,"synthetic-recommended");
        Check(!string.IsNullOrEmpty(selected.FocusRecommendationId)&&selected.FocusRecommendationPolicy==decision.PolicyId&&selected.FocusRecommendationReason==decision.Reason&&selected.Recommendation==null,"local attribution saved separately from old remote feature model");
        string decisionId=selected.FocusRecommendationId;
        Check(service.BeginFocusSession("synthetic-recommended",decision.MissionId,recommendation:decision)&&Session(service,"synthetic-recommended").FocusRecommendationId==decisionId,"repeated selection keeps original recommendation identity");
        service=Open(store,clock);Check(Session(service,"synthetic-recommended").FocusRecommendationId==decisionId,"recommendation attribution survives restart");
    }
}
