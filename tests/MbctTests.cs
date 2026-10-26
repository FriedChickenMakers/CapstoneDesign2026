using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Plants;

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
        public string Data;public bool Fail;public int Saves;
        public GardenState Load()=>Data==null?null:StateCodec.Decode(Data);
        public void Save(GardenState s){if(Fail)throw new IOException("Synthetic failure");Data=StateCodec.Encode(s);Saves++;}
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
    static void ProgramChecks()
    {
        Check(MbctContent.AllCourses.Count==84 && MbctContent.AllCourses.Select(m=>m.id).Distinct().Count()==84,"All three catalogs have distinct stable identities");
        foreach(string program in new[]{"2w","4w"})
        {
            var catalog=MbctContent.ForProgram(program);
            Check(catalog.Count==MbctPolicy.PracticeCount(program),program+" complete course size");
            Check(catalog.First().mbctActivity==1 && catalog.Last().mbctActivity==13,program+" introductory senses and independent maintenance ending");
            Check(catalog.Last().steps.Any(step=>step.answerKey=="restart-plan"),program+" ending includes restarting after missed days");
            Check(catalog.Any(m=>m.steps.Any(step=>step.answerKey=="support")) && catalog.Any(m=>m.steps.Any(step=>step.answerKey=="warning-action")),program+" includes support and care plan");
            Check(catalog.First(m=>m.mbctActivity==4).recommendedOrder<catalog.First(m=>m.mbctActivity==8).recommendedOrder,program+" breath foundation before breathing space");
            for(int i=0;i<catalog.Count;i++)
            {
                var mission=catalog[i];
                Check(mission.id==MbctPolicy.MissionId(program,i+1) && mission.week==i/6+1 && mission.recommendedOrder==i+1,program+" order and week mapping");
                Check(MindfulnessContent.FindMission(mission.id)==mission && mission.steps.All(step=>!string.IsNullOrWhiteSpace(step.guidance)),program+" production lookup and complete guidance");
                GrowthCategory actual,source;
                Check(GardenPlantVisuals.TryCategory(mission.id,out actual) && GardenPlantVisuals.TryCategory(mission.sourceId,out source) && actual==source,program+" short ID contributes the intended plant activity category");
            }
            var clock=new Clock();var store=new Store();var service=Open(store,clock);
            Check(!service.Snapshot.MbctProgramChosen && service.SelectMbctProgram(program),program+" new user explicitly chooses program");
            Check(MbctPolicy.NextOrder(service.Snapshot)==1 && service.Snapshot.MbctNextOrder==1,program+" leaves legacy progression untouched");
            for(int order=1;order<=catalog.Count;order++)
            {
                if(order>1)do { clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(new string[0]),program+" advance synthetic date"); } while(MbctPolicy.Availability(service.Snapshot)!=null);
                string session=program+"-"+order;
                Check(service.BeginSession(session,MbctPolicy.MissionId(program,order),order) && service.StartSession(session),program+" begin own sequence");
                Check(!service.SelectMbctProgram("8w"),program+" active practice prevents course switch");
                Check(service.CompleteSession(session),program+" complete practice");
                int before=service.Snapshot.Nutrient;
                Check(service.CompleteSession(session) && service.Snapshot.Nutrient==before,program+" repeated completion cannot repay");
                if(order<=6 && order%2==0)Check(service.GrowPlant(program+"-grow-"+order,GardenPlantVisuals.GrowthPlantId),program+" growth purchase from earned nutrients");
                if(order==6)Check(GardenPlantVisuals.StageForGrowth(GardenPlantVisuals.TotalGrowth(service.Snapshot))==3,program+" can see blossom by sixth completion before course ending");
                service.Dispose();service=Open(store,clock);
                Check(MbctPolicy.NextOrder(service.Snapshot)==order+1 && MbctPolicy.ActiveProgram(service.Snapshot)==program,program+" exact progression survives restart");
            }
            Check(MbctPolicy.CompletedCount(service.Snapshot)==catalog.Count && MbctPolicy.Availability(service.Snapshot).Contains("활동을 마쳤어요"),program+" explicit completed state");
            Check(service.Snapshot.MbctCompletedIds.Count==0 && service.Snapshot.MbctNextOrder==1,program+" original 48 IDs and progression preserved");
            Check(service.Snapshot.Nutrient==catalog.Count*5-30,program+" reward budget and growth cost unchanged");
            string finalId=MbctPolicy.MissionId(program,catalog.Count);
            Check(service.BeginSession(program+"-review",finalId,catalog.Count) && service.StartSession(program+"-review"),program+" review allowed after completion");
            int savedNutrients=service.Snapshot.Nutrient;
            Check(service.CompleteSession(program+"-review") && service.Snapshot.Nutrient==savedNutrients,program+" completed-course review pays no additional reward");
            Check(service.SelectMbctProgram("8w") && service.SelectMbctProgram(program) && MbctPolicy.CompletedCount(service.Snapshot)==catalog.Count,program+" switching preserves completed progress");
            CheckDetachedLists(service.Snapshot,StateCodec.Clone(service.Snapshot));
            service.Dispose();
        }
        var switchClock=new Clock();var switchStore=new Store();var switched=Open(switchStore,switchClock);
        Check(switched.SelectMbctProgram("2w"),"Select two-week course");
        Check(switched.BeginSession("short-first","course:MBCT2W01",1) && switched.StartSession("short-first") && switched.CompleteSession("short-first"),"Complete first short practice");
        Check(switched.SelectMbctProgram("4w") && !switched.BeginSession("same-day-other","course:MBCT4W01",1),"Cross-program switch cannot bypass daily reward limit");
        switchClock.Time=switchClock.Time.AddDays(1);Check(switched.RefreshCycle(new string[0]),"Next day for second program");
        Check(!switched.BeginSession("wrong-program","course:MBCT2W02",2),"Unselected program cannot advance");
        Check(!switched.BeginSession("wrong-order","course:MBCT4W02",1),"Short-course ID/order mismatch rejected");
        Check(switched.BeginSession("four-first","course:MBCT4W01",1) && switched.StartSession("four-first") && switched.CompleteSession("four-first"),"Begin distinct four-week course");
        Check(switched.SelectMbctProgram("2w") && MbctPolicy.NextOrder(switched.Snapshot)==2,"Resume prior short-course progress");
        var beforeSwitch=StateCodec.Encode(switched.Snapshot);switchStore.Fail=true;
        Check(!switched.SelectMbctProgram("8w") && StateCodec.Encode(switched.Snapshot)==beforeSwitch,"Course switch failure preserves progress and selection atomically");switchStore.Fail=false;
        Check(!switched.SelectMbctProgram("3w"),"Unknown course rejected");
        for(int day=2;day<6;day++)
        {
            switchClock.Time=switchClock.Time.AddDays(1);Check(switched.RefreshCycle(new string[0]),"Cross-program weekly date");
            string program=day%2==0?"2w":"4w";Check(switched.SelectMbctProgram(program),"Alternate program without resetting its progress");
            int order=MbctPolicy.NextOrder(switched.Snapshot);string id="alternating-"+day;
            Check(switched.BeginSession(id,MbctPolicy.MissionId(program,order),order) && switched.StartSession(id) && switched.CompleteSession(id),"Cross-program practice uses one global quota");
        }
        switchClock.Time=switchClock.Time.AddDays(1);Check(switched.RefreshCycle(new string[0]) && switched.SelectMbctProgram("8w"),"Sunday switch to untouched eight-week program");
        Check(!switched.BeginSession("seventh-cross-program","course:MBCT01",1) && switched.LastError.Contains("이번 주"),"Switching into a third program cannot bypass the six-per-week quota");
        switched.Dispose();
        // Old installed users stay in their established 8-week course even before their first completion.
        var old=new GardenState {MigrationCompleted=true,Nutrient=17};var xml=new XmlDocument();xml.LoadXml(StateCodec.Encode(old));
        foreach(string key in new[]{"MbctProgramId","MbctProgramSelectionVersion","MbctProgramChosen","MbctPrograms"})xml.DocumentElement.RemoveChild(xml.DocumentElement[key]);
        var oldStore=new Store {Data=xml.OuterXml};var migrated=Open(oldStore,new Clock());
        Check(migrated.Snapshot.MbctProgramChosen && migrated.Snapshot.MbctProgramId=="8w" && migrated.Snapshot.Nutrient==17,"Legacy save selects eight-week course without progress/reward reset");
        migrated.Dispose();
    }
    static void HistoryAndSummaryChecks()
    {
        var clock=new Clock();var store=new Store {Data=StateCodec.Encode(new GardenState {MigrationCompleted=true,Nutrient=100})};var service=Open(store,clock);
        Begin(service,1,"remembered");Check(service.CompleteSession("remembered","차분해요","합성 검증 기록"),"Create explicitly linkable completed activity");
        var before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.GrowPlant("linked-growth",GardenPlantVisuals.GrowthPlantId,"remembered") && StateCodec.Encode(service.Snapshot)==before,"Growth record, nutrient spend and recipe fail atomically");store.Fail=false;
        Check(!service.GrowPlant("invalid-link",GardenPlantVisuals.GrowthPlantId,"missing") && StateCodec.Encode(service.Snapshot)==before,"Missing source record cannot consume nutrients or invent a link");
        Check(service.GrowPlant("linked-growth",GardenPlantVisuals.GrowthPlantId,"remembered"),"Explicit activity link and growth commit together");
        var history=service.Snapshot.GrowthHistory.Single();int nutrient=service.Snapshot.Nutrient;
        Check(history.LinkedSessionId=="remembered" && history.BeforeGrowth==0 && history.AfterGrowth>history.BeforeGrowth && history.CreatedUtc==clock.UtcNow.ToString("O"),"Growth history preserves explicit association, actual time and before/after values");
        Check(service.GrowPlant("linked-growth",GardenPlantVisuals.GrowthPlantId,"remembered") && service.Snapshot.GrowthHistory.Count==1 && service.Snapshot.Nutrient==nutrient,"Growth retry creates no duplicate or extra charge");
        Check(!service.GrowPlant("linked-growth",GardenPlantVisuals.GrowthPlantId) && service.Snapshot.GrowthHistory.Single().LinkedSessionId=="remembered","Retry cannot silently relink a saved growth event");
        Check(service.GrowPlant("unlinked-growth",GardenPlantVisuals.GrowthPlantId) && service.Snapshot.GrowthHistory.Last().LinkedSessionId=="","Optional omission remains explicitly unlinked despite recent completion");
        service.Dispose();service=Open(store,clock);
        Check(service.Snapshot.GrowthHistory.First().LinkedSessionId=="remembered" && service.Snapshot.Sessions.Single().Note=="합성 검증 기록","Growth link and original reflection survive restart");
        var detached=service.Snapshot;detached.GrowthHistory[0].LinkedSessionId="external edit";
        Check(service.Snapshot.GrowthHistory.First().LinkedSessionId=="remembered","History snapshot cannot edit live state");
        Check(service.BeginSession("ended","free-mission:M01") && service.EndParticipation("ended"),"Create explicitly ended participation");
        Check(service.SaveExperience("experience-one","즐거움","합성 사건","","",""),"Create summary experience");
        var firstDate=clock.Time;
        var firstSummary=GardenRecordSummary.Calculate(service.Snapshot,firstDate,firstDate.AddTicks(1));
        Check(firstSummary.Completed==1 && firstSummary.Ended==1 && firstSummary.ExperienceTypes["즐거움"]==1,"Summary counts actual completed/ended records and chosen experience type");
        Check(GardenRecordSummary.Calculate(service.Snapshot,firstDate.AddTicks(1),firstDate.AddDays(1)).Completed==0,"Summary excludes records outside explicit time window");
        clock.Time=clock.Time.AddDays(8);Check(service.RefreshCycle(new string[0]),"Move synthetic week");Begin(service,2,"later");Check(service.CompleteSession("later"),"Later-week completed activity");
        Check(service.SaveExperience("experience-two","불편함","다른 합성 사건","","",""),"Later-week experience");
        var week=GardenRecordSummary.Calculate(service.Snapshot,new DateTimeOffset(clock.Time.Date,clock.Time.Offset),clock.Time.AddTicks(1));
        var all=GardenRecordSummary.Calculate(service.Snapshot,DateTimeOffset.MinValue,clock.Time.AddTicks(1));
        Check(week.Completed==1 && week.Ended==0 && week.ExperienceTypes.Count==1 && all.Completed==2 && all.Ended==1 && all.ExperienceTypes.Count==2,"Weekly and cumulative summaries stay distinct");
        Check(service.BeginSession("still-active","free-mission:M02") && service.StartSession("still-active"),"Create unfinished participation");
        Check(GardenRecordSummary.Calculate(service.Snapshot,DateTimeOffset.MinValue,clock.Time.AddTicks(1)).Completed==2,"Unfinished activity is not invented as a completion");
        // A legacy save with growth purchases has no historical timestamp or source to infer.
        var legacyXml=new XmlDocument();legacyXml.LoadXml(StateCodec.Encode(service.Snapshot));legacyXml.DocumentElement.RemoveChild(legacyXml.DocumentElement["GrowthHistory"]);
        var legacy=StateCodec.Decode(legacyXml.OuterXml);
        Check(legacy.GrowthHistory.Count==0 && legacy.PurchaseReceipts.Count==2 && legacy.Nutrient==service.Snapshot.Nutrient,"Legacy growth migrates without invented timestamps or associations");
        service.Dispose();
    }
    static void CompletionBoundaryChecks()
    {
        foreach(string program in new[]{"2w","4w","8w"})
        {
            var clock=new Clock {Time=DateTimeOffset.Parse("2026-10-06T03:59:50+09:00")};
            var store=new Store();
            var service=new GardenStateService(store,clock,new DemoBalanceConfig {VisitorChancePercent=100});
            Check(service.Open() && service.RefreshCycle(new[]{"animal:A01"}) && service.SelectMbctProgram(program),program+" prepare pre-reset state");
            Check(service.BeginSession("boundary",MbctPolicy.MissionId(program,1),1) && service.StartSession("boundary"),program+" begin before daily reset");
            clock.Time=clock.Time.AddSeconds(20); // Complete before the UI's next 30-second refresh.
            var before=StateCodec.Encode(service.Snapshot);int saves=store.Saves;
            store.Fail=true;
            Check(!service.CompleteSession("boundary",eligibleAnimalIds:new[]{"animal:A02"}) && StateCodec.Encode(service.Snapshot)==before
                && store.Data==before && store.Saves==saves,program+" reset decision, reward and completion fail atomically");
            store.Fail=false;
            Check(service.CompleteSession("boundary",eligibleAnimalIds:new[]{"animal:A02"}) && store.Saves==saves+1,program+" current cycle and completion use one successful save");
            var completed=service.Snapshot;
            Check(completed.LastCycleId==service.CurrentCycleId && completed.Sessions.Single().InstanceId==service.CurrentCycleId
                && completed.DailyDecisions.Count==2 && completed.DailyDecisions.Last().VisitorId=="animal:A02",program+" completion consumes the real current day with supplied visitor candidates");
            Check(completed.Nutrient==5 && completed.RewardReceipts.Single()==service.CurrentCycleId+"|"+MbctPolicy.MissionId(program,1),program+" one current-day reward receipt");
            Check(service.CompleteSession("boundary") && service.Snapshot.Nutrient==5 && service.Snapshot.DailyDecisions.Count==2,program+" reset completion retry cannot duplicate reward or daily decision");
            Check(!service.BeginSession("second-same-day",MbctPolicy.MissionId(program,2),2),program+" next practice stays blocked after reset-boundary completion");
            string alternate=program=="2w"?"4w":"2w";
            Check(service.SelectMbctProgram(alternate) && !service.BeginSession("switched-same-day",MbctPolicy.MissionId(alternate,1),1),program+" switching course cannot bypass reset-boundary quota");
            service.Dispose();service=Open(store,clock);
            Check(service.Snapshot.Nutrient==5 && service.Snapshot.DailyDecisions.Last().VisitorId=="animal:A02"
                && MbctPolicy.NextOrder(service.Snapshot,program)==2,program+" reset-boundary outcome survives restart");
            service.Dispose();
        }
        foreach(var candidates in new[]{Array.Empty<string>(),new[]{"animal:A01"}})
        {
            var clock=new Clock {Time=DateTimeOffset.Parse("2026-10-06T03:59:50+09:00")};
            var service=new GardenStateService(new Store(),clock,new DemoBalanceConfig {VisitorChancePercent=100});
            Check(service.Open() && service.RefreshCycle(candidates),"Prepare explicitly known visitor set");
            Begin(service,1,"default-candidates");clock.Time=clock.Time.AddSeconds(20);
            Check(service.CompleteSession("default-candidates") && service.Snapshot.DailyDecisions.Last().VisitorId==(candidates.FirstOrDefault()??""),
                "Existing completion caller reuses known candidates and never invents a visitor");
            service.Dispose();
        }
    }
    static void RecommendationAttributionChecks()
    {
        var store=new Store();var clock=new Clock();var service=Open(store,clock);
        var context=new RecommendationContext {DecisionId="00000000000000000000000000000001",PolicyId="synthetic-policy-v1",AppVersion="synthetic-build",UtcOffsetMinutes=540,Features=new[]{1f,.5f,0f,.25f}};
        var before=StateCodec.Encode(service.Snapshot);store.Fail=true;
        Check(!service.BeginSession("recommended","course:MBCT01",1,context) && StateCodec.Encode(service.Snapshot)==before,"Recommendation attribution and selection fail atomically");store.Fail=false;
        Check(service.BeginSession("recommended","course:MBCT01",1,context),"Recommendation attribution saved at selection");
        context.Features[0]=99;
        Check(service.Snapshot.Sessions.Single().Recommendation.Features[0]==1,"Caller-owned recommendation vector is detached");
        context.Features[0]=1;
        Check(service.BeginSession("recommended","course:MBCT01",1,context),"Identical recommendation selection retry succeeds");
        context.DecisionId="different-synthetic-decision";
        Check(!service.BeginSession("recommended","course:MBCT01",1,context),"Retry cannot change recommendation attribution");
        var snapshot=service.Snapshot;snapshot.Sessions[0].Recommendation.Features[1]=99;
        Check(service.Snapshot.Sessions.Single().Recommendation.Features[1]==.5f,"Snapshot recommendation vector is detached");
        service.Dispose();service=Open(store,clock);
        Check(service.Snapshot.Sessions.Single().Recommendation.DecisionId=="00000000000000000000000000000001" && service.Snapshot.Sessions.Single().Recommendation.AppVersion=="synthetic-build" && service.Snapshot.Sessions.Single().Recommendation.Features.Length==4 && service.Snapshot.Sessions.Single().Recommendation.UtcOffsetMinutes==540,"Recommendation correlation and original timezone survive process restoration");
        Check(service.CancelSelection("recommended"),"Cancel synthetic selection");
        context.Features=new[]{1f,2f};
        Check(!service.BeginSession("bad-shape","course:MBCT01",1,context),"Wrong feature shape rejected");
        context.Features=new[]{1f,float.NaN,0f,0f};
        Check(!service.BeginSession("bad-number","course:MBCT01",1,context),"Nonfinite recommendation feature rejected");
        context.Features=new[]{1f,0f,0f,0f};context.AppVersion=null;
        Check(!service.BeginSession("bad-version","course:MBCT01",1,context),"Missing recommendation version rejected");
        Check(service.BeginSession("ordinary","course:MBCT01",1) && service.Snapshot.Sessions.Single().Recommendation==null,"Ordinary local selection needs no recommendation attribution");
        service.Dispose();
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
        var skipStore=new Store();var skip=Open(skipStore,new Clock());
        Check(skip.SavePreference("food","existing preference"),"Seed previous completed preference");
        Begin(skip,1,"skip-optional");
        Check(skip.SavePracticeStep("skip-optional",0,"food","autosaved draft") &&
            skip.SavePracticeStep("skip-optional",0,"other","keep this answer"),"Autosave current optional answers");
        var beforeSkip=StateCodec.Encode(skip.Snapshot);skipStore.Fail=true;
        Check(!skip.SkipPracticeStep("skip-optional",1,"food") && StateCodec.Encode(skip.Snapshot)==beforeSkip,"Skip draft removal and step advance are atomic on save failure");
        skipStore.Fail=false;Check(skip.SkipPracticeStep("skip-optional",1,"food"),"Optional input can be skipped after autosave");
        Check(skip.Snapshot.Sessions.Single().InstructionStep==1 &&
            skip.Snapshot.Sessions.Single().Answers.All(a=>a.Key!="food") &&
            skip.Snapshot.Sessions.Single().Answers.Single().Key=="other","Only the skipped answer is removed");
        Check(skip.Snapshot.PracticePreferences.Single().Value=="existing preference","Skip preserves prior global preference");
        skip.Dispose();skip=Open(skipStore,new Clock());
        Check(skip.Snapshot.Sessions.Single().InstructionStep==1 && skip.Snapshot.Sessions.Single().Answers.All(a=>a.Key!="food"),"Skipped answer stays absent after reload");
        Check(skip.CompleteSession("skip-optional") && skip.Snapshot.PracticePreferences.Single(a=>a.Key=="food").Value=="existing preference","Completion does not leak skipped autosave into preference");
        Check(!skip.SkipPracticeStep("skip-optional",2,"other"),"Completed practice cannot be edited by skip");
        skip.Dispose();
        var existingEightProgress=s.Snapshot.MbctCompletedIds.ToArray();var existingEightNutrient=s.Snapshot.Nutrient;
        Check(s.SelectMbctProgram("2w") && s.SelectMbctProgram("4w") && s.SelectMbctProgram("8w") && s.Snapshot.MbctNextOrder==49 && s.Snapshot.MbctCompletedIds.SequenceEqual(existingEightProgress) && s.Snapshot.Nutrient==existingEightNutrient,"Existing full 48-practice progress and rewards survive all program switches");
        s.Dispose();ProgramChecks();HistoryAndSummaryChecks();CompletionBoundaryChecks();RecommendationAttributionChecks();Console.WriteLine("PASS "+checks+" MBCT assertions (complete 2/4/8-week synthetic courses; no Unity/device)");
    }
}
