using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

class MbctTests
{
    class Clock:IClock
    {
        public DateTimeOffset Time=DateTimeOffset.Parse("2026-10-05T12:00:00+09:00");
        public DateTimeOffset UtcNow=>Time;
        public TimeZoneInfo TimeZone=>TimeZoneInfo.CreateCustomTimeZone("KST",TimeSpan.FromHours(9),"KST","KST");
    }
    class Store:IStateStore
    {
        public string Data;public bool Fail;
        public GardenState Load()=>Data==null?null:StateCodec.Decode(Data);
        public void Save(GardenState s){if(Fail)throw new IOException("Synthetic failure");Data=StateCodec.Encode(s);}
    }
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static GardenStateService Open(Store store,Clock clock)
    {var s=new GardenStateService(store,clock,new DemoBalanceConfig());Check(s.Open(),"Open MBCT state");Check(s.RefreshCycle(new string[0]),"Refresh MBCT cycle");return s;}
    static void Begin(GardenStateService s,int order,string session)
    {Check(s.BeginSession(session,MbctPolicy.MissionId(order),order),"Begin "+order);Check(s.StartSession(session),"Start "+order);}
    static void CheckDetachedLists(object original,object copy)
    {
        foreach(var field in original.GetType().GetFields())
        {
            var list=field.GetValue(original) as System.Collections.IList;
            if(list==null)continue;var copied=(System.Collections.IList)field.GetValue(copy);
            Check(!ReferenceEquals(list,copied),"Detached collection: "+field.Name);
            for(int i=0;i<list.Count;i++)
                if(list[i]!=null && !(list[i] is string))
                {Check(!ReferenceEquals(list[i],copied[i]),"Detached record: "+field.Name);CheckDetachedLists(list[i],copied[i]);}
        }
    }
    static void Main()
    {
        Check(new DemoBalanceConfig().CompletionNutrient==5,"Default completion reward is five");
        Check(MbctContent.Course.Count==48 && MbctContent.WeekTitles.Length==8,"Eight weeks, six practices each");
        Check(MbctContent.Course.Select(m=>m.id).Distinct().Count()==48,"Unique IDs");
        for(int i=0;i<48;i++)
        {
            var m=MbctContent.Course[i];Check(m.id==MbctPolicy.MissionId(i+1) && m.recommendedOrder==i+1 && m.week==i/6+1,"Stable order");
            Check(m.steps.Length>0 && !string.IsNullOrWhiteSpace(m.purpose) && m.steps.All(x=>!string.IsNullOrWhiteSpace(x.guidance)),"Purpose and guidance");
            Check(m.mbctActivity!=6 && m.mbctActivity!=7 && !m.audioAvailable,"Diaries separate; no audio dependency");
            Check(MindfulnessContent.FindMission(m.id)==m,"New catalog lookup");
        }
        Check(MindfulnessContent.FindMission("course:P01")!=null,"Legacy session lookup remains");
        Check(MbctContent.Course.First(m=>m.mbctActivity==8).recommendedOrder>MbctContent.Course.First(m=>m.mbctActivity==4).recommendedOrder,"Breath before breathing space");
        Check(MbctContent.Course.Where(m=>m.mbctActivity==1).Count()==5,"Eating variants repeat");
        Check(MbctContent.Course.Any(m=>m.steps.Any(x=>x.guidance.Contains("창문"))),"Window suggestion present");
        Check(MbctContent.Course.Any(m=>m.steps.Any(x=>x.answerKey=="personal-event")),"Personal scenarios follow examples");
        var clock=new Clock();var store=new Store();var s=Open(store,clock);
        Check(!s.BeginSession("wrong","course:MBCT02",1) && !s.BeginSession("future","course:MBCT02",2),"Reject mismatched/future IDs");
        Begin(s,1,"one");
        Check(s.SavePracticeStep("one",1,"food","물"),"Save step draft");
        s.Dispose();s=Open(store,clock);
        Check(s.Snapshot.Sessions.Single().InstructionStep==1 && s.Snapshot.Sessions.Single().Answers.Single().Value=="물","Step/answer restored");
        store.Fail=true;var before=StateCodec.Encode(s.Snapshot);
        Check(!s.SavePracticeStep("one",2,"food","과자") && StateCodec.Encode(s.Snapshot)==before,"Draft write is atomic");
        Check(!s.CompleteSession("one") && StateCodec.Encode(s.Snapshot)==before,"Failed completion leaves reward/progress unchanged");
        store.Fail=false;Check(s.CompleteSession("one") && s.Snapshot.Nutrient==5 && s.Snapshot.MbctNextOrder==2,"Atomic five reward and progress");
        Check(s.CompleteSession("one") && s.Snapshot.Nutrient==5,"Duplicate completion has one reward");
        Check(!s.BeginSession("two-today","course:MBCT02",2),"One new practice per day");
        Begin(s,1,"review");Check(s.CompleteSession("review") && s.Snapshot.Nutrient==5 && s.Snapshot.MbctNextOrder==2,"Review does not reward or advance");
        Check(s.Snapshot.PracticePreferences.Single().Value=="물","Completed preferences persist");
        Check(s.SaveExperience("journal","불편함","메시지","가슴 답답함","울적해요","답장이 없어요"),"Independent journal save");
        Check(s.SaveExperience("journal","불편함","메시지","가슴 답답함","울적해요","답장이 없어요") && s.Snapshot.Experiences.Count==1 && s.Snapshot.Nutrient==5,"Journal retry no reward/duplicate");
        before=StateCodec.Encode(s.Snapshot);store.Fail=true;
        Check(!s.SavePreference("routine","손 씻기") && StateCodec.Encode(s.Snapshot)==before,"Preference failure atomic");store.Fail=false;
        Check(s.SavePreference("routine","손 씻기"),"Reusable routine save");
        Check(s.SavePreference("warning-sign","잠을 잘 못 잠") && s.SavePreference("support","치료진"),"Editable personal plan");
        for(int order=2;order<=6;order++)
        {clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(new string[0]),"Next practice day");Begin(s,order,"day-"+order);Check(s.CompleteSession("day-"+order),"Complete day");}
        Check(s.Snapshot.Nutrient==30 && s.Snapshot.MbctNextOrder==7,"Six rewards total thirty");
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(new string[0]),"Sunday");
        Check(!s.BeginSession("seventh","course:MBCT07",7) && s.LastError.Contains("이번 주"),"Six per Monday-Sunday week");
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(new string[0]),"Next week");Begin(s,7,"seven");
        // Resume across midnight: completion belongs to the current cycle.
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(new string[0]),"Cross-midnight");Check(s.CompleteSession("seven"),"Complete resumed session");
        Check(s.Snapshot.Sessions.Last().InstanceId==s.Snapshot.LastCycleId && !s.BeginSession("eight-same-day","course:MBCT08",8),"Completion date consumes current daily quota");
        for(int order=8;order<=48;order++)
        {
            do{clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(new string[0]),"Advance calendar");}while(MbctPolicy.Availability(s.Snapshot)!=null);
            Begin(s,order,"course-"+order);Check(s.CompleteSession("course-"+order),"Complete full course");
            s.Dispose();s=Open(store,clock);
        }
        Check(s.Snapshot.MbctNextOrder==49 && s.Snapshot.MbctCompletedIds.Count==48 && s.Snapshot.Nutrient==240,"Full course reopens with forty-eight rewards");
        Check(s.Snapshot.NextCourseOrder==1 && s.Snapshot.CompletedCourseIds.Count==0,"Legacy progress independent");
        Check(s.Snapshot.Experiences.Single().Emotion=="울적해요" && s.Snapshot.PracticePreferences.Any(a=>a.Key=="routine" && a.Value=="손 씻기"),"Journal/preferences survive full course");
        var old=new GardenState{MigrationCompleted=true,Nutrient=77};var xml=new XmlDocument();xml.LoadXml(StateCodec.Encode(old));
        foreach(string field in new[]{"MbctNextOrder","MbctCompletedIds","PracticePreferences","Experiences"})xml.DocumentElement.RemoveChild(xml.DocumentElement[field]);
        var migrated=StateCodec.Decode(xml.OuterXml);
        Check(migrated.Nutrient==77 && migrated.MbctNextOrder==1 && migrated.Experiences.Count==0,"Old schema-1 state opens without reset");
        // A caller must never be able to change persisted state through a snapshot.
        var detached=s.Snapshot;var isolatedBefore=StateCodec.Encode(s.Snapshot);
        detached.PracticePreferences[0].Value="external edit";
        detached.Experiences[0].Emotion="external edit";
        detached.Sessions[0].Status=ParticipationState.Ended;
        detached.Sessions[0].Answers.Clear();detached.DailyDecisions[0].VisitorId="external edit";
        detached.MbctCompletedIds.Clear();detached.RewardReceipts.Clear();
        Check(StateCodec.Encode(s.Snapshot)==isolatedBefore,"Nested snapshot edits cannot change service state");
        var original=s.Snapshot;var clone=StateCodec.Clone(original);
        Check(StateCodec.Encode(original)==StateCodec.Encode(clone),"Memory clone preserves serialized storage contract");
        CheckDetachedLists(original,clone);
        s.Dispose();Console.WriteLine("PASS "+checks+" MBCT assertions (full eight-week synthetic course; no Unity/device)");
    }
}
