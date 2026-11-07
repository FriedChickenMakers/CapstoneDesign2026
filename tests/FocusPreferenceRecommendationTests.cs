using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using CapstoneDesign.Runtime.LocalState;

sealed class PreferenceClock:IClock
{
    public DateTimeOffset Time=DateTimeOffset.Parse("2030-03-20T12:00:00Z");
    public DateTimeOffset UtcNow=>Time;
    public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
}
sealed class PreferenceStore:IStateStore
{
    public GardenState State;public bool Fail;
    public GardenState Load()=>State==null?null:StateCodec.Clone(State);
    public void Save(GardenState state){if(Fail)throw new IOException("Synthetic preference save failure");State=StateCodec.Clone(state);}
}
static class FocusPreferenceRecommendationTests
{
    static int assertions;
    static readonly DateTimeOffset Now=DateTimeOffset.Parse("2030-03-20T12:00:00Z");
    static void Check(bool condition,string description){assertions++;if(!condition)throw new Exception(description);}
    static GardenState State(string favorite=null,string updated=null)
    {
        var state=new GardenState {LastCycleId="2030-03-20"};
        if(favorite!=null)state.PracticePreferences.Add(new PracticeAnswer {Key="favorite",Value=favorite,UpdatedUtc=updated});
        return state;
    }
    static ActivitySession Completed(string code,int minutesAgo,params string[] choices)
    {
        var definition=FocusMissionCatalog.Find(code);var ended=Now.AddMinutes(-minutesAgo);var started=ended.AddMinutes(-10);
        var values=new Dictionary<string,string>();for(int i=0;i<choices.Length;i+=2)values[choices[i]]=choices[i+1];
        return new ActivitySession {SessionId="synthetic-preference-"+code+"-"+minutesAgo,MissionId=definition.Id,
            FocusVersion=3,FocusRole=definition.Role,Status=ParticipationState.RewardCommitted,
            SelectedUtc=started.AddSeconds(-1).ToString("O"),StartedUtc=started.ToString("O"),EndedUtc=ended.ToString("O"),
            FocusStepRecords=definition.Steps.Select((step,index)=>new FocusStepRecord {Key=step.Key,
                Value=values.ContainsKey(step.Key)?values[step.Key]:step.Options.FirstOrDefault()??"",
                ConfirmedUtc=started.AddSeconds(index+1).ToString("O"),UpdatedUtc=started.AddSeconds(index+1).ToString("O")}).ToList()};
    }
    static FocusStepRecord Record(ActivitySession session,string key)=>session.FocusStepRecords.Single(record=>record.Key==key);
    static FocusAdditionalRecommendation Decision(GardenState state)=>FocusAdditionalRecommendationPolicy.Select(state,Now);
    // Ranking fixtures intentionally include incomplete/corrupt input cases; serialize all public
    // fields without using the persistence validator to check that selection has no side effects.
    static string SerializedFixture(GardenState state)
    {using(var writer=new StringWriter()){new XmlSerializer(typeof(GardenState)).Serialize(writer,state);return writer.ToString();}}
    static void Main()
    {
        Methods();Frequency();Eligibility();NewestExplicitResponse();BoundaryPreservation();SettingPersistence();
        Console.WriteLine("Focus preference recommendations: "+assertions+" assertions PASS");
    }
    static void Methods()
    {
        foreach(var source in new[]{Completed("P07",30,"next","호흡"),Completed("P21",30,"activity","호흡","save","내 쉬는 방법으로 저장"),Completed("P28",30,"next-activity","호흡")})
        {
            var state=State();state.Sessions.Add(source);var result=Decision(state);
            Check(result.MissionId=="free-mission:M17"&&result.Reason=="focus-method-preference","explicit future breathing preference: "+source.MissionId);
            Check(result.Features[0]==1,"mapped preference is present in the immutable ranking snapshot");
        }
        foreach(var code in new[]{"P21","P28"})
        {
            var state=State();state.Sessions.Add(code=="P21"?Completed(code,30,"activity","잠시 멈춤","save","내 쉬는 방법으로 저장"):Completed(code,30,"next-activity","잠시 멈춤"));
            Check(Decision(state).MissionId=="free-mission:M09","explicit pause preference maps to rest, "+code);
        }
        foreach(string method in new[]{"몸의 접촉","주변 소리","없음"})
        {
            var state=State("호흡 관찰");state.Sessions.Add(Completed("P07",30,"next",method));
            var signal=FocusAdditionalRecommendationPolicy.PreferenceFor(state,Now);
            Check(signal!=null&&signal.MissionId==null,"unmapped/none response supersedes old preference without inventing an equivalent: "+method);
            Check(Decision(state).Features[0]==0,"unmapped preference is not applied to stretching/music");
        }
        var skip=State("호흡 관찰");skip.Sessions.Add(Completed("P21",30,"activity","잠시 멈춤","save","이번에는 건너뛰기"));
        Check(Decision(skip).MissionId=="free-mission:M17"&&Decision(skip).Reason=="saved-preference","skip saving a method does not clear an existing preference");
        var today=State("잠시 멈추기");today.Sessions.Add(Completed("P07",30,"activity","호흡","next","잘 모르겠음"));
        Check(Decision(today).MissionId=="free-mission:M09","today's method choice is not a future preference");
    }
    static void Frequency()
    {
        foreach(var response in new[]{"더 자주","지금처럼","덜 자주"})
        {
            var state=State("호흡 관찰");state.Sessions.Add(Completed("P14",30,"activity","호흡","preference",response));
            var signal=FocusAdditionalRecommendationPolicy.PreferenceFor(state,Now);
            Check(signal.Direction==(response=="덜 자주"?-1:1),"frequency keeps its own direction; as-now preserves existing input: "+response);
            if(response=="더 자주")Check(Decision(state).MissionId=="free-mission:M17"&&Decision(state).Reason=="focus-frequency-more","more often boosts eligible matching activity");
            if(response=="덜 자주")Check(Decision(state).MissionId!="free-mission:M17","less often lowers priority without deleting the activity");
            if(response=="지금처럼")Check(Decision(state).Reason=="saved-preference","as-now does not clear existing preference");
            Check(FocusMissionAdjustmentPolicy.RecommendDuration("M17","breath",60,null).RecommendedSeconds==60,"frequency does not shorten duration");
        }
        foreach(var text in new[]{"잘 모르겠음","호흡은 더 자주", "덜 자주 해 주세요","더 자주\nsynthetic free text"})
        {
            var state=State("잠시 멈추기");state.Sessions.Add(Completed("P14",30,"activity","호흡","preference",text));
            Check(Decision(state).MissionId=="free-mission:M09"&&Decision(state).Reason=="saved-preference","unknown/free text is not parsed as a preference: "+text.Replace("\n"," "));
        }
        var unmapped=State();unmapped.Sessions.Add(Completed("P14",30,"activity","몸의 접촉","preference","더 자주"));
        Check(FocusAdditionalRecommendationPolicy.PreferenceFor(unmapped,Now).MissionId==null,"body contact frequency does not become stretching frequency");
        var asNow=State();asNow.Sessions.Add(Completed("P14",60,"activity","호흡","preference","덜 자주"));asNow.Sessions.Add(Completed("P14",30,"activity","호흡","preference","지금처럼"));
        Check(FocusAdditionalRecommendationPolicy.PreferenceFor(asNow,Now).Direction==-1,"as-now also preserves an earlier less-often adjustment");
        foreach(var other in FocusMissionCatalog.Additional.Where(mission=>mission.Code!="M17"))
        {var done=Completed(other.Code,20);done.InstanceId=asNow.LastCycleId;asNow.Sessions.Add(done);}
        Check(Decision(asNow).MissionId=="free-mission:M17"&&Decision(asNow).Reason=="focus-frequency-less","less often lowers ranking but does not ban the only remaining activity");
    }
    static void Eligibility()
    {
        foreach(var status in new[]{ParticipationState.Selected,ParticipationState.InProgress,ParticipationState.Paused,ParticipationState.Ended})
        {
            var state=State("잠시 멈추기");var session=Completed("P07",30,"next","호흡");session.Status=status;state.Sessions.Add(session);
            Check(Decision(state).Reason=="saved-preference"&&Decision(state).MissionId=="free-mission:M09","incomplete source cannot teach preference: "+status);
        }
        foreach(string fault in new[]{"unconfirmed","skipped","future-confirmation","before-start","future-completion","bad-time","unsupported-version","invalid-choice","duplicate-answer"})
        {
            var state=State("잠시 멈추기");var session=Completed("P07",30,"next","호흡");var record=Record(session,"next");
            if(fault=="unconfirmed")record.ConfirmedUtc=null;
            if(fault=="skipped")record.Skipped=true;
            if(fault=="future-confirmation")record.ConfirmedUtc=Now.AddMinutes(1).ToString("O");
            if(fault=="before-start")record.ConfirmedUtc=Now.AddDays(-1).ToString("O");
            if(fault=="future-completion")session.EndedUtc=Now.AddMinutes(1).ToString("O");
            if(fault=="bad-time")session.StartedUtc="invalid";
            if(fault=="unsupported-version")session.FocusVersion=99;
            if(fault=="invalid-choice")record.Value="synthetic breathing preference";
            if(fault=="duplicate-answer")session.FocusStepRecords.Add(new FocusStepRecord {Key="next",Value="호흡",ConfirmedUtc=record.ConfirmedUtc});
            state.Sessions.Add(session);Check(Decision(state).Reason=="saved-preference"&&Decision(state).MissionId=="free-mission:M09","ineligible response ignored: "+fault);
        }
        var oldVersion=State();var completed=Completed("P07",30,"next","호흡");completed.FocusVersion=2;oldVersion.Sessions.Add(completed);
        Check(Decision(oldVersion).MissionId=="free-mission:M17","completed v2 same explicit choice is usable without rewriting its content");
    }
    static void NewestExplicitResponse()
    {
        var state=State("잠시 멈추기");state.Sessions.Add(Completed("P07",60,"next","호흡"));
        Check(Decision(state).MissionId=="free-mission:M17","dated Focus answer supersedes untimestamped legacy fallback");
        state.PracticePreferences[0].UpdatedUtc=Now.AddMinutes(-1).ToString("O");
        Check(Decision(state).MissionId=="free-mission:M09"&&Decision(state).Reason=="saved-preference","later explicit settings edit takes precedence");
        state.PracticePreferences[0].UpdatedUtc=Now.AddDays(-1).ToString("O");
        state.Sessions.Add(Completed("P21",30,"activity","잠시 멈춤","save","내 쉬는 방법으로 저장"));
        Check(Decision(state).MissionId=="free-mission:M09"&&Decision(state).Reason=="focus-method-preference","newest completed Focus choice takes precedence");
        state.Sessions.Reverse();Check(Decision(state).MissionId=="free-mission:M09","list order does not choose the winning answer");
        var edited=State("잠시 멈추기",Now.AddMinutes(-35).ToString("O"));var session=Completed("P07",30,"next","호흡");
        Record(session,"next").UpdatedUtc=Now.AddMinutes(-31).ToString("O");edited.Sessions.Add(session);
        Check(Decision(edited).MissionId=="free-mission:M17","confirmed choice edited before completion uses actual latest update time");
        foreach(var timestamp in new[]{"invalid",Now.AddDays(1).ToString("O")})
        {
            var invalidSetting=State("잠시 멈추기",timestamp);invalidSetting.Sessions.Add(Completed("P07",30,"next","호흡"));
            Check(Decision(invalidSetting).MissionId=="free-mission:M17","invalid/future setting timestamp does not eclipse observed choices");
        }
    }
    static void BoundaryPreservation()
    {
        var state=State();state.Sessions.Add(Completed("P07",30,"next","호흡"));
        var completed=Completed("M17",20);completed.InstanceId=state.LastCycleId;state.Sessions.Add(completed);
        Check(Decision(state).MissionId!="free-mission:M17","today's completed additional activity remains excluded despite preference");
        var active=Completed("M14",20);active.Status=ParticipationState.Paused;state.Sessions.Add(active);
        Check(Decision(state).MissionId=="free-mission:M14"&&Decision(state).Reason=="resume-active-activity","unfinished additional activity still has resume priority");
        string before=SerializedFixture(state);Decision(state);Check(SerializedFixture(state)==before,"selection does not mutate course/reward/preferences or past records");
        var legacy=State("몸 살펴보기");Check(Decision(legacy).MissionId=="free-mission:M05","existing manual legacy category mapping preserved");
        var neutral=State();var first=Decision(neutral);Check(first.Reason=="catalog-default-no-history","new user has explicit cold-start fallback");
        Check(Enumerable.Range(0,17).Select(day=>FocusAdditionalRecommendationPolicy.Select(neutral,Now.AddDays(day)).MissionId).Distinct().Count()==17,"cold-start daily rotation is preserved");
        Check(first.PolicyId=="focus-additional-history-preference-v2","policy change is versioned independently of content version");
    }
    static void SettingPersistence()
    {
        var store=new PreferenceStore();var clock=new PreferenceClock();var service=new GardenStateService(store,clock,new DemoBalanceConfig());
        Check(service.Open(),"open setting persistence service");
        Check(service.SavePreference("favorite","호흡 관찰"),"save explicit setting");
        var first=service.Snapshot.PracticePreferences.Single(answer=>answer.Key=="favorite");
        Check(first.UpdatedUtc==clock.UtcNow.ToString("O"),"new setting records actual update time");
        service.Dispose();service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open(),"reopen timestamped preference");
        Check(service.Snapshot.PracticePreferences.Single(answer=>answer.Key=="favorite").UpdatedUtc==first.UpdatedUtc,"setting timestamp survives XML and clone");
        clock.Time=clock.Time.AddMinutes(5);store.Fail=true;
        Check(!service.SavePreference("favorite","잠시 멈추기"),"failed setting cannot be reported saved");
        var unchanged=service.Snapshot.PracticePreferences.Single(answer=>answer.Key=="favorite");
        Check(unchanged.Value==first.Value&&unchanged.UpdatedUtc==first.UpdatedUtc,"failed write preserves value and precedence time atomically");
        store.Fail=false;Check(service.SavePreference("favorite","잠시 멈추기"),"retry setting save");
        var latest=service.Snapshot.PracticePreferences.Single(answer=>answer.Key=="favorite");
        Check(latest.UpdatedUtc==clock.UtcNow.ToString("O"),"later successful edit records its new time");
        Check(FocusAdditionalRecommendationPolicy.Select(service.Snapshot,clock.UtcNow).MissionId=="free-mission:M09","reloaded service setting reaches recommendation");
        service.Dispose();
        store.State.PracticePreferences.Single(answer=>answer.Key=="favorite").UpdatedUtc=null;
        service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open(),"old setting without timestamp remains loadable");
        Check(service.Snapshot.PracticePreferences.Single(answer=>answer.Key=="favorite").UpdatedUtc==null,"loading an old setting does not invent its precedence time");
        Check(FocusAdditionalRecommendationPolicy.Select(service.Snapshot,clock.UtcNow).MissionId=="free-mission:M09","old loaded setting remains a recommendation fallback");
        service.Dispose();
    }
}
