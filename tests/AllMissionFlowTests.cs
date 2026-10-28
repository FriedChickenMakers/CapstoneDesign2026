using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

sealed class AllMissionClock:IClock
{
    public DateTimeOffset Time=DateTimeOffset.Parse("2026-10-26T09:00:00Z");
    public DateTimeOffset UtcNow=>Time;
    public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
}
sealed class AllMissionStore:IStateStore
{
    public GardenState State;public bool Fail;
    public GardenState Load()=>State==null?null:StateCodec.Clone(State);
    public void Save(GardenState next){if(Fail)throw new IOException("Synthetic storage fault");State=StateCodec.Clone(next);}
}
static class AllMissionFlowTests
{
    static int assertions;static void Check(bool value,string reason){assertions++;if(!value)throw new Exception(reason);}
    static GardenStateService Open(AllMissionStore store,AllMissionClock clock)
    {var service=new GardenStateService(store,clock,new DemoBalanceConfig());Check(service.Open(),"open "+service.LastError);Check(service.RefreshCycle(Array.Empty<string>()),"refresh");return service;}
    static ActivitySession Get(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(s=>s.SessionId==id);
    static string Drawing()=>FocusDrawingData.Encode(new List<List<FocusDrawingPoint>>{new List<FocusDrawingPoint>{new FocusDrawingPoint(1,2),new FocusDrawingPoint(31,40),new FocusDrawingPoint(60,61)}});
    static GardenStateService Perform(GardenStateService service,AllMissionStore store,AllMissionClock clock,string id,FocusMissionDefinition mission,int order=0)
    {
        Check(service.BeginFocusSession(id,mission.Code,order),"begin "+mission.Code+" "+service.LastError);Check(service.StartSession(id),"start "+mission.Code);
        if(mission.Code=="M15")
        {
            var day=new DateTimeOffset(clock.Time.UtcDateTime.Date,TimeSpan.Zero);
            Check(service.EnsureFocusWalkingTarget(id,new WalkingTarget {GoalSteps=5000,QueryStartUtc=day.ToString("O"),QueryEndUtc=day.AddDays(1).ToString("O"),Basis="DEFAULT"}),"persist walking target before flow");
        }
        while(Get(service,id).InstructionStep<mission.Steps.Length)
        {
            var session=Get(service,id);int index=session.InstructionStep;var step=mission.Steps[index];
            Check(FocusMissionCatalog.IsApplicable(mission,session.FocusStepRecords,index),"runner never lands on excluded branch");
            string answer=step.Kind=="draw"?Drawing():step.Options.FirstOrDefault()??(step.OptionalInput?"synthetic reflection":"");
            if(step.RecommendedSeconds>0)
            {
                Check(service.SaveFocusElapsed(id,index,step.RecommendedSeconds),"timer progress "+mission.Code);
                Check(Get(service,id).Status==ParticipationState.InProgress&&Get(service,id).InstructionStep==index,"elapsed time cannot complete or advance mission");
            }
            Check(service.SaveFocusDraft(id,index,answer,string.IsNullOrEmpty(answer)),"save draft "+mission.Code+" "+service.LastError);
            Check(service.PauseSession(id),"pause draft");service=Open(store,clock);Check(service.ResumeSession(id),"resume stored draft");
            Check(Get(service,id).FocusStepRecords[index].Value==answer,"response survives restart "+mission.Code);
            if(step.Kind=="wait")Check(Get(service,id).Status!=ParticipationState.RewardCommitted,"P27 returning later is not completion");
            Check(service.ConfirmFocusStep(id,index,answer,string.IsNullOrEmpty(answer)),"confirm "+mission.Code+" "+index+" "+service.LastError);
        }
        int nutrient=service.Snapshot.Nutrient;
        Check(service.SaveFocusReflection(id,"","",1),"optional reflection");
        store.Fail=true;Check(!service.CompleteFocusSession(id),"failed completion "+mission.Code);store.Fail=false;
        Check(Get(service,id).Status==ParticipationState.InProgress&&service.Snapshot.Nutrient==nutrient,"failed commit preserves session and reward");
        Check(service.CompleteFocusSession(id),"complete "+mission.Code+" "+service.LastError);int reward=Get(service,id).RewardAmount;
        Check(service.CompleteFocusSession(id)&&service.Snapshot.Nutrient==nutrient+reward,"idempotent completion "+mission.Code);
        service=Open(store,clock);Check(Get(service,id).FocusStepRecords.All(s=>!string.IsNullOrEmpty(s.ConfirmedUtc)),"completed records retain branch decisions");
        return service;
    }
    static void Main()
    {
        Check(FocusMissionCatalog.All.Select(m=>m.Code).Distinct().Count()==45,"all mission IDs retained");
        var store=new AllMissionStore();var clock=new AllMissionClock();var service=Open(store,clock);
        foreach(var mission in FocusMissionCatalog.Additional)service=Perform(service,store,clock,"free-"+mission.Code,mission);
        Check(!service.Snapshot.MbctProgramChosen,"all recommendations work before course selection");
        Check(service.SelectMbctProgram("8w"),"choose full curriculum");
        for(int order=1;order<=48;order++)
        {
            while(FocusMissionPolicy.Availability(service.Snapshot)!=null){clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"next curriculum day");}
            service=Perform(service,store,clock,"course-"+order,FocusMissionPolicy.MissionAt(order,"8w"),order);
        }
        Check(FocusMissionPolicy.CompletedCount(service.Snapshot,"8w")==48,"all 8-week course occurrences completed");
        var completed=service.Snapshot.Sessions.Where(s=>s.Status==ParticipationState.RewardCommitted).ToArray();
        Check(FocusMissionCatalog.All.All(m=>completed.Any(s=>s.MissionId==m.Id)),"every one of45 missions completed through domain flow");
        var draw=completed.Single(s=>s.MissionId=="free-mission:M11").FocusStepRecords.Single(s=>s.Key=="drawing");
        Check(FocusDrawingData.Decode(draw.Value).Sum(s=>s.Count)==3,"drawing strokes preserved in final record");
        Check(completed.Where(s=>s.MissionId=="course:P19").All(s=>s.FocusStepRecords.Single(r=>r.Key=="sound-listen").Skipped),"mountain route excludes sound exercise explicitly");
        DrawingValidation();Console.WriteLine("All mission flow tests passed: "+assertions+" assertions.");
    }
    static void DrawingValidation()
    {
        var strokes=Enumerable.Range(0,40).Select(s=>Enumerable.Range(0,40).Select(i=>new FocusDrawingPoint(i+s,i)).ToList()).ToList();
        string encoded=FocusDrawingData.Encode(strokes);Check(encoded.Length<=500,"drawing stays within storage boundary");
        var decoded=FocusDrawingData.Decode(encoded);Check(decoded.Sum(s=>s.Count)<=FocusDrawingData.MaximumPoints&&decoded.Count<=FocusDrawingData.MaximumStrokes,"bounded stroke decoding");
        Check(FocusDrawingData.Decode("DRAW1|ABC").Count==0,"incomplete point rejected");Check(FocusDrawingData.Decode("DRAW1|@@").Count==0,"unsupported point rejected");
        Check(FocusDrawingData.Decode(new string('a',501)).Count==0,"oversized drawing rejected");
        Check(FocusDrawingData.Encode(FocusDrawingData.Decode(Drawing()))==Drawing(),"stroke serialization roundtrip");
        Check(FocusDrawingData.Encode(new List<List<FocusDrawingPoint>>())=="","clear serializes as empty");
    }
}
