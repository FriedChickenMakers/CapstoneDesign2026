using System;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

static class FocusMissionContractTests
{
    static int assertions;
    static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    static FocusMissionStep Step(string code,string key,int version=FocusMissionCatalog.Version)=>FocusMissionCatalog.Find(code,version).Steps.Single(step=>step.Key==key);
    static void Main()
    {
        VersionCompatibility();DurationEvidence();AlternativeIdentity();Planning();Recommendations();
        Console.WriteLine("Focus mission contracts: "+assertions+" assertions PASS");
    }
    static void VersionCompatibility()
    {
        Check(FocusMissionCatalog.Version==5&&FocusMissionCatalog.All.Length==45&&FocusMissionCatalog.AllV2.Length==45&&FocusMissionCatalog.AllV4.Length==45,"v5 preserves all45 versioned missions");
        foreach(var old in FocusMissionCatalog.AllV4)
        {
            var current=FocusMissionCatalog.Find(old.Id);
            Check(old.Steps.Select(s=>s.Key).SequenceEqual(current.Steps.Select(s=>s.Key)),"stable step keys for "+old.Code);
            Check(current.ContentVersion==5&&!string.IsNullOrEmpty(current.Purpose)&&current.ReviewStatus.Contains("clinical-review-pending"),"purpose contract is not clinical approval for "+old.Code);
        }
        Check(Step("P25","speak",4).Kind=="observe"&&!Step("P25","speak",4).RequiredAnswer,"incoming v4 speak remains compatible with empty completed answers");
        Check(Step("M10","plan",4).Kind=="input","incoming v4 free-text M10 is retained");
        Check(FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(FocusMissionCatalog.Find("P19",4),"route","짧은 소리 실습")==null,"incoming v4 fallback semantics remain unchanged");
        Check(FocusMissionCatalog.Find("P25",1).Steps[2].Body.Contains("말하거나 보내세요"),"v1 wording preserved");
        Check(Step("P25","speak",2).Kind=="observe"&&!Step("P25","speak",2).RequiredAnswer,"v2 original speak action preserved");
        Check(Step("P25","speak").Kind=="choice"&&Step("P25","speak").Options.Contains("전하지 않기"),"new mindful communication permits intentionally not speaking");
        Check(Step("M10","plan",2).Kind=="input"&&Step("M10","plan").Kind=="plan","old required text remains oldversion;new plans can use choices");
        Check(Step("P09","contact",2).Body.Contains("접촉")&&Step("P09","contact").Body.Contains("마음"),"new emotion-body cue does not rewrite old prompt");
        Check(!ReferenceEquals(Step("P18","practice",2).Options,Step("P18","practice").Options),"versioned options are deep copies");
        Check(FocusMissionCatalog.Find("P02",99)==null,"unsupported version fails closed");
        var methods=FocusMissionAdjustmentPolicy.AllowedMethods(FocusMissionCatalog.Find("P22"),"activity");methods[0]="synthetic change";
        Check(Step("P22","activity").Options[0]=="걷기","caller cannot expand allowed route by mutating snapshot");
        Check(FocusMissionAdjustmentPolicy.AllowedMethods(FocusMissionCatalog.Find("P19"),"route").SequenceEqual(new[]{"산 명상"}),"different-purpose sound is not advertised as same-mission method");
        Check(FocusMissionAdjustmentPolicy.AllowedMethods(FocusMissionCatalog.Find("P09"),"notice").Length==0,"emotion answers are not personalizable methods");
        Check(FocusMissionAdjustmentPolicy.PreservedStepKeys(FocusMissionCatalog.Find("P10")).SequenceEqual(new[]{"awareness","breath","expand","ready"}),"duration adaptation cannot remove breathing-space stages");
        Check(FocusMissionAdjustmentPolicy.CanPersonalizeQuantity(FocusMissionCatalog.Find("M15"))&&!FocusMissionAdjustmentPolicy.CanPersonalizeQuantity(FocusMissionCatalog.Find("P22")),"step target permission does not leak into mindful movement");
    }
    static FocusDurationFeedback Feedback(string mission,string step,int seconds,bool confirmed,string date="2030-01-01T12:00:00Z")
        =>new FocusDurationFeedback {MissionId=mission,StepKey=step,ActualSeconds=seconds,ConfirmedTooLong=confirmed,ConfirmedUtc=date};
    static void DurationEvidence()
    {
        var empty=FocusMissionAdjustmentPolicy.RecommendDuration("P01","practice",60,null);
        Check(empty.RecommendedSeconds==60&&empty.EvidenceCount==0&&empty.Reason=="catalog-default","default until confirmed usable evidence");
        var ignored=new[]{Feedback("P01","practice",20,false),Feedback("P02","sound",10,true),Feedback("P01","prepare",5,true),Feedback("P01","practice",0,true),Feedback("P01","practice",10,true,"invalid"),Feedback("P01","practice",90,true)};
        Check(FocusMissionAdjustmentPolicy.RecommendDuration("P01","practice",60,ignored).RecommendedSeconds==60,"cancel/otherobjective/nonpractice/unobserved/invalid/longer records cannot shorten");
        var history=ignored.Concat(new[]{Feedback("P01","practice",20,true),Feedback("course:P01","practice",35,true,"2030-01-02T12:00:00Z")}).ToArray();
        var decision=FocusMissionAdjustmentPolicy.RecommendDuration("P01","practice",60,history);
        Check(decision.RecommendedSeconds==35&&decision.BaselineSeconds==60&&decision.EvidenceCount==2,"latest actual confirmed stopping time used, not arbitrary reduction ratio");
        history.Last().ActualSeconds=1;
        Check(decision.RecommendedSeconds==35,"decision snapshot does not change with later history");
        Check(FocusMissionAdjustmentPolicy.RecommendDuration("P01","prepare",60,history).RecommendedSeconds==60,"non-timed steps cannot gain invented duration");
        Check(FocusMissionAdjustmentPolicy.RecommendDuration("P01","practice",0,history).RecommendedSeconds==0,"zero baseline remains non-timed");
        foreach(int version in new[]{2,3})
        {
            var definition=FocusMissionCatalog.Find("P01",version);
            Check(FocusMissionAdjustmentPolicy.RecommendDuration(definition,"breath",60,new[]{Feedback("P01","breath",17,true)}).RecommendedSeconds==17,"saved-version definition recognizes old breath key in "+version);
        }
        Check(FocusMissionAdjustmentPolicy.DurationBasis.Contains("not-clinical-minimum"),"source scope explicitly product adapted");
    }
    static void AlternativeIdentity()
    {
        Check(FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(FocusMissionCatalog.Find("P19"),"route","짧은 소리 실습")=="course:P02","mountain fallback identifies actual sound practice");
        Check(FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(FocusMissionCatalog.Find("P26"),"route","아직 없어요")=="course:P02","appreciation fallback identifies actual sound practice");
        Check(FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(FocusMissionCatalog.Find("P19",2),"route","짧은 소리 실습")==null,"saved v2 branch meaning is not silently reinterpreted");
        Check(FocusMissionAdjustmentPolicy.DifferentPurposeAlternative(FocusMissionCatalog.Find("P22"),"activity","앉아서 살피기")==null,"same-purpose seated route remains original mission");
        var support=FocusMissionCatalog.FindForSession(new ActivitySession {FocusVersion=3,MissionId="course:P02",FocusSupport=true});
        Check(FocusMissionCatalog.FindForSession(new ActivitySession{FocusVersion=FocusMissionCatalog.Version,MissionId="course:P02",FocusSupport=true}).Role==FocusMissionCatalog.AdditionalRole,"new support uses current guided actual-sound contract");
        Check(support.Code=="P02"&&support.Role==FocusMissionCatalog.AdditionalRole,"support preserves real activity while leaving basic progress alone");
        Check(FocusMissionCatalog.Find("P02").Role==FocusMissionCatalog.BasicRole,"support role is not leaked into the shared catalog");
        Check(FocusMissionCatalog.FindForSession(new ActivitySession {FocusVersion=3,MissionId="course:P19",FocusSupport=true})==null,"unapproved support identity rejected");
    }
    static void Planning()
    {
        Check(FocusMissionAdjustmentPolicy.ComposePlan("차 마시기","저녁")=="저녁 · 차 마시기","plan captures coarse personal period without alert setting");
        Check(FocusMissionAdjustmentPolicy.ComposePlan("","", "synthetic self-chosen action")=="synthetic self-chosen action","existing written plans remain usable");
        Check(FocusMissionAdjustmentPolicy.ComposePlan("차 마시기","")=="","choice plan requires both activity and period");
        Check(FocusMissionAdjustmentPolicy.ComposePlan("차 마시기","나중에 정하기").StartsWith("나중에 정하기"),"not-ready time remains explicit rather than invented alarm");
        bool rejected=false;try{FocusMissionAdjustmentPolicy.ComposePlan("차 마시기","12:00");}catch(ArgumentException){rejected=true;}
        Check(rejected,"exact notification time is not a plan period");
    }
    static void Recommendations()
    {
        var now=DateTimeOffset.Parse("2030-01-01T12:00:00Z");var state=new GardenState {LastCycleId="2030-01-01"};
        var first=FocusAdditionalRecommendationPolicy.Select(state,now);
        Check(first.MissionId==FocusAdditionalRecommendationPolicy.Select(state,now).MissionId&&first.Reason=="catalog-default-no-history","cold start is reproducible and explicitly non-personalized");
        Check(Enumerable.Range(0,17).Select(i=>FocusAdditionalRecommendationPolicy.Select(state,now.AddDays(i)).MissionId).Distinct().Count()==17,"equal-evidence cold start is not permanently M03-first");
        state.PracticePreferences.Add(new PracticeAnswer{Key="favorite",Value="호흡 관찰"});
        var preference=FocusAdditionalRecommendationPolicy.Select(state,now);
        Check(preference.MissionId=="free-mission:M17"&&preference.Reason=="saved-preference","explicit saved preference affects eligible recommendation");
        var features=preference.Features;features[0]=0;Check(preference.Features[0]==1,"recommendation features frozen for UI reuse");
        state.Sessions.Add(new ActivitySession {SessionId="synthetic-completed",MissionId="free-mission:M17",FocusVersion=3,FocusRole="additional",StartedUtc=now.AddMinutes(-2).ToString("O"),InstanceId=state.LastCycleId,Status=ParticipationState.RewardCommitted});
        Check(FocusAdditionalRecommendationPolicy.Select(state,now).MissionId!="free-mission:M17","today-completed optional activity is excluded while others remain");
        state.Sessions.Add(new ActivitySession {SessionId="synthetic-active",MissionId="free-mission:M14",FocusVersion=2,FocusRole="additional",Status=ParticipationState.Paused});
        var resumed=FocusAdditionalRecommendationPolicy.Select(state,now);
        Check(resumed.MissionId=="free-mission:M14"&&resumed.Reason=="resume-active-activity","existing saved additional session gets resume priority");
        Check(state.Nutrient==0&&state.Sessions.Count==2&&!state.MbctProgramChosen,"ranking cannot start, complete, reward or select course");
    }
}
