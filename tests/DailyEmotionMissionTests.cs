using System;
using System.IO;
using System.Linq;
using System.Xml;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

sealed class EmotionClock : IClock
{
    public DateTimeOffset Time=DateTimeOffset.Parse("2026-11-09T12:00:00Z");
    public DateTimeOffset UtcNow=>Time;
    public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
}
sealed class EmotionStore : IStateStore
{
    public GardenState State; public bool Fail; public int Saves;
    public GardenState Load()=>State==null?null:StateCodec.Clone(State);
    public void Save(GardenState candidate)
    {if(Fail)throw new IOException("Synthetic daily emotion storage failure");State=StateCodec.Clone(candidate);Saves++;}
}
static class DailyEmotionMissionTests
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static GardenStateService Open(IStateStore store,EmotionClock clock)
    {
        var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0},new LegacySnapshot{Nutrient=137});
        Check(service.Open()&&service.RefreshCycle(Array.Empty<string>()),"Open synthetic state: "+service.LastError);return service;
    }
    static ActivitySession Get(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(s=>s.SessionId==id);
    static string Encode(GardenStateService service)=>StateCodec.Encode(service.Snapshot);
    static void Failed(EmotionStore store,GardenStateService service,Func<bool> operation,string label)
    {
        string live=Encode(service),durable=StateCodec.Encode(store.State);int saves=store.Saves;store.Fail=true;
        try{Check(!operation(),label+" reports failure");}finally{store.Fail=false;}
        Check(Encode(service)==live&&StateCodec.Encode(store.State)==durable&&store.Saves==saves,label+" keeps the complete live/durable state unchanged");
    }
    static void Ready(GardenStateService service,string id,string code,int order=0)
    {
        Check(service.BeginFocusSession(id,code,order),"Select "+id+": "+service.LastError);Check(service.StartSession(id),"Start "+id);
        var mission=FocusMissionCatalog.FindForSession(Get(service,id));
        for(int i=0;i<mission.Steps.Length;i++)
        {var step=mission.Steps[i];string answer=step.RequiredAnswer?step.Options.FirstOrDefault()??"synthetic input":null;
         Check(service.ConfirmFocusStep(id,i,answer,answer==null),"Confirm "+id+": "+service.LastError);}
    }
    static string Unlock(GardenStateService service,string suffix="")
    {
        Check(service.SelectMbctProgram("8w"),"Choose synthetic course");int order=FocusMissionPolicy.NextOrder(service.Snapshot,"8w");
        Ready(service,"p"+suffix,FocusMissionPolicy.MissionAt(order,"8w").Code,order);
        Check(service.CompletePracticeSession("p"+suffix),"Complete basic immediately: "+service.LastError);
        Ready(service,"m"+suffix,"M03");Check(service.CompletePracticeSession("m"+suffix),"Complete additional immediately: "+service.LastError);
        Check(DailyEmotionPolicy.Availability(service.Snapshot)==null,"Two completed roles unlock today's emotion mission");
        return DailyEmotionPolicy.RecordId(service.CurrentCycleId);
    }
    static void Main()
    {
        AtomicCoreCompletion();Prerequisites();AtomicDailyRecord();ExpiryAndDayBoundary();CompletedAndArchivedBank();CompatibilityAndValidation();HistoricalGrowthCompatibility();EmptyMissionResumeGuards();RewardOverflowRollbacks();ResetPreservesGrowthReceipts();ArchivedMemoKeepsOriginalRecord();MemoMissionAssociations();LegacyMemoAssociationBackfill();RealFileReload();
        Console.WriteLine("Daily emotion mission tests passed: "+checks+" assertions.");
    }
    static void AtomicCoreCompletion()
    {
        foreach(bool receiptAlreadySaved in new[]{false,true})
        {
            var clock=new EmotionClock();var store=new EmotionStore();var service=Open(store,clock);
            Check(service.SelectMbctProgram("8w"),"Select core completion course");Ready(service,"old-draft","P01",1);
            Check(service.SaveFocusReflection("old-draft","평온함","synthetic retained old note",1),"Seed pre-update reflection draft");
            int step=service.Snapshot.PlantAppearance.Recipe.GrowthStep;
            if(receiptAlreadySaved)Check(service.CommitPracticeGrowth("old-draft"),"Seed old saved practice receipt");
            Failed(store,service,()=>service.CompletePracticeSession("old-draft"),"Core completion failure");
            Check(service.CompletePracticeSession("old-draft"),"Core completion retry: "+service.LastError);
            var ended=Get(service,"old-draft");
            Check(ended.Status==ParticipationState.RewardCommitted&&ended.Mood=="평온함"&&ended.Note=="synthetic retained old note",
                "Immediate completion retains existing emotion/note draft exactly");
            Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep==step+1&&service.Snapshot.PracticeGrowthRecords.Count(r=>r.SessionId=="old-draft")==1
                &&service.CoinBalance==187&&ended.RewardAmount==50,"Core completion, 50 coins, course progress and one growth share the durable result");
            string completed=Encode(service);int saves=store.Saves;store.Fail=true;
            Check(service.CompletePracticeSession("old-draft")&&store.Saves==saves&&Encode(service)==completed,"Completed core retry is read-only even while the store fails");store.Fail=false;
            service.Dispose();service=Open(store,clock);Check(Encode(service)==completed,"Completed core result and old reflection survive reopening");
            service.Dispose();
        }
    }
    static void Prerequisites()
    {
        var clock=new EmotionClock();var store=new EmotionStore();
        using(var service=Open(store,clock))
        {
            Check(service.SelectMbctProgram("8w"),"Choose prerequisite course");string today=service.CurrentCycleId;
            Check(DailyEmotionPolicy.Current(service.Snapshot)==null&&DailyEmotionPolicy.Availability(service.Snapshot)!=null
                &&!service.BeginDailyEmotion(today),"No prerequisites means locked with no invented record");
            Ready(service,"p","P01",1);Check(service.CompletePracticeSession("p"),"Complete P first");
            Check(DailyEmotionPolicy.Availability(service.Snapshot)!=null&&!service.BeginDailyEmotion(today),"P alone cannot unlock emotion mission");
            Ready(service,"m","M03");Check(service.CommitPracticeGrowth("m"),"Seed old pre-final M growth milestone");
            Check(DailyEmotionPolicy.Availability(service.Snapshot)!=null&&!service.BeginDailyEmotion(today),"Growth milestone without final M completion is insufficient");
            Check(service.CompletePracticeSession("m"),"Finalize old pending M without extra growth");
            Check(DailyEmotionPolicy.Availability(service.Snapshot)==null&&service.Snapshot.DailyEmotionRecords.Count==0,"Both final records unlock without automatically creating emotion content");
            Check(!service.BeginDailyEmotion("2026-11-08"),"Cannot start a record for an arbitrary past date");
            Check(service.BeginDailyEmotion(today),"Begin today's unlocked mission");var record=DailyEmotionPolicy.Current(service.Snapshot);
            Check(record!=null&&record.Id==DailyEmotionPolicy.RecordId(today)&&record.BasicSessionId=="p"&&record.AdditionalSessionId=="m"&&!record.IsCompleted,
                "Daily record retains exact prerequisite session identities and date");
            int step=service.Snapshot.PlantAppearance.Recipe.GrowthStep;string before=Encode(service);
            Check(!service.CompleteDailyEmotion(record.Id,"  ","\t\n")&&Encode(service)==before,"Whitespace-only emotion record cannot complete");
            Check(!service.CompleteDailyEmotion("missing","평온함","")&&service.Snapshot.PlantAppearance.Recipe.GrowthStep==step,"Unknown record cannot award growth");
        }
        // Different business days cannot be combined into an unlock.
        using(var service=Open(new EmotionStore(),clock))
        {
            Check(service.SelectMbctProgram("8w"),"Choose split-day course");Ready(service,"yesterday-p","P01",1);Check(service.CompletePracticeSession("yesterday-p"),"Yesterday P");
            clock.Time=clock.Time.AddDays(1);Check(service.RefreshCycle(Array.Empty<string>()),"Advance split day");Ready(service,"today-m","M03");Check(service.CompletePracticeSession("today-m"),"Today M");
            Check(DailyEmotionPolicy.Availability(service.Snapshot)!=null&&!service.BeginDailyEmotion(service.CurrentCycleId),"Yesterday P plus today's M does not unlock today");
        }
    }
    static void AtomicDailyRecord()
    {
        foreach(bool noteOnly in new[]{false,true})
        {
            var clock=new EmotionClock();var store=new EmotionStore();var service=Open(store,clock);string id=Unlock(service);
            int stage=service.Snapshot.PlantAppearance.Recipe.GrowthStep;string recipe=PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            Failed(store,service,()=>service.BeginDailyEmotion(service.CurrentCycleId),"Emotion start failure");
            Check(service.BeginDailyEmotion(service.CurrentCycleId),"Emotion start retry");
            string mood=noteOnly?"":"잘 모르겠어요",note=noteOnly?"synthetic note only":"";
            Failed(store,service,()=>service.SaveDailyEmotionDraft(id,mood,note,0),"Emotion draft failure");
            Check(service.SaveDailyEmotionDraft(id,mood,note,0)&&service.SaveDailyEmotionDraft(id,mood,note,1),"Save emotion and note draft stages");
            var draft=service.GetDailyEmotion(id);draft.Mood="detached mutation";
            Check(service.GetDailyEmotion(id).Mood==mood&&PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe&&service.CoinBalance==217,"Draft is detached and retains the 50+30 mission rewards without awarding growth/coins");
            string saved=Encode(service);service.Dispose();service=Open(store,clock);
            Check(Encode(service)==saved&&service.GetDailyEmotion(id).Mood==mood&&service.GetDailyEmotion(id).Note==note,"Draft and stage survive reopening exactly");
            Failed(store,service,()=>service.CompleteDailyEmotion(id,mood,note),"Emotion completion/growth failure");
            Check(service.GetPracticeGrowth(id)==null&&!service.GetDailyEmotion(id).IsCompleted,"Failed save creates neither growth ledger nor completion");
            int completionWrites=store.Saves;
            var completions=Enumerable.Range(0,12).Select(_=>System.Threading.Tasks.Task.Run(()=>service.CompleteDailyEmotion(id,mood,note))).ToArray();
            System.Threading.Tasks.Task.WaitAll(completions);
            Check(completions.All(call=>call.Result)&&store.Saves==completionWrites+1,"Concurrent emotion completion retries commit exactly one write: "+service.LastError);
            var result=service.GetDailyEmotion(id);var memo=service.Snapshot.PlantAppearance.Memos.Single();
            Check(result.IsCompleted&&result.Stage==1&&result.Mood==mood&&result.Note==note&&service.GetPracticeGrowth(id)==null
                &&result.RewardVersion==1&&result.RewardAmount==100&&result.MemoId==memo.Id
                &&memo.DailyEmotionRecordId==id&&memo.Mood==mood&&memo.Note==note
                &&memo.PlantInstanceId==service.Snapshot.PlantAppearance.Recipe.InstanceId,"Emotion or note alone validly completes with one individually owned memo");
            Check(service.Snapshot.PlantAppearance.Recipe.GrowthStep==stage&&service.CoinBalance==317&&PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,"Third activity gives 100 coins without changing recipe or growth");
            string done=Encode(service);int writes=store.Saves;store.Fail=true;
            Check(service.CompleteDailyEmotion(id,"changed","changed")&&store.Saves==writes&&Encode(service)==done,"Duplicate completed command cannot overwrite the saved emotion or repeat growth");store.Fail=false;
            service.Dispose();service=Open(store,clock);Check(Encode(service)==done,"Completed daily record, growth and input survive reopening");
            service.Dispose();
        }
    }
    static void ExpiryAndDayBoundary()
    {
        var clock=new EmotionClock{Time=DateTimeOffset.Parse("2026-11-09T23:59:00Z")};var store=new EmotionStore();
        using(var service=Open(store,clock))
        {
            string id=Unlock(service);Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.SaveDailyEmotionDraft(id,"평온함","synthetic unfinished",1),"Seed daily draft before midnight");
            clock.Time=DateTimeOffset.Parse("2026-11-10T00:01:00Z");Check(service.RefreshCycle(Array.Empty<string>())&&!DailyEmotionPolicy.IsExpired(service.Snapshot,service.GetDailyEmotion(id)),"Midnight does not expire a 04:00 business-day record");
            clock.Time=DateTimeOffset.Parse("2026-11-10T03:59:00Z");Failed(store,service,()=>service.CompleteDailyEmotion(id,"평온함","synthetic unfinished"),"Last-minute emotion completion failure");
            string before=Encode(service);clock.Time=DateTimeOffset.Parse("2026-11-10T04:00:00Z");
            Check(!service.CompleteDailyEmotion(id,"평온함","synthetic unfinished")&&Encode(service)==before,"After-reset retry cannot commit yesterday's failed growth even before UI refresh");
            Check(!service.SaveDailyEmotionDraft(id,"changed","changed",0)&&Encode(service)==before,"Expired draft cannot be overwritten by an old callback");
            Check(service.RefreshCycle(Array.Empty<string>()),"Advance persisted business day");before=Encode(service);
            Check(DailyEmotionPolicy.IsExpired(service.Snapshot,service.GetDailyEmotion(id))&&DailyEmotionPolicy.Current(service.Snapshot)==null
                &&service.GetDailyEmotion(id).Note=="synthetic unfinished"&&!service.GetDailyEmotion(id).IsCompleted,"Expired draft remains readable history, never today's writable mission");
            Check(!service.BeginDailyEmotion(service.CurrentCycleId)&&Encode(service)==before,"Previous-day prerequisites cannot start today's emotion record");
            string next=Unlock(service,"-next");Check(service.BeginDailyEmotion(service.CurrentCycleId)&&next!=id,"Today's two new role completions create a separate emotion identity");
            Check(service.CompleteDailyEmotion(next,"평온함",""),"Complete new day's emotion");string done=Encode(service);
            clock.Time=clock.Time.AddDays(1);store.Fail=true;
            Check(service.CompleteDailyEmotion(next,"changed","changed")&&Encode(service)==done,"Already-completed prior-day retry remains read-only despite expiry and failing storage");store.Fail=false;
        }
    }
    static void CompletedAndArchivedBank()
    {
        var clock=new EmotionClock();var store=new EmotionStore();using(var service=Open(store,clock))
        {
            string source=service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(service.SetPlantCompleted(source,true),"Explicitly complete synthetic plant");
            string archived=PlantCodec.Encode(service.Snapshot.PlantPhotos.Single());string id=Unlock(service);
            string active=service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(active!=source&&service.Snapshot.StoredGrowthSteps==0&&service.Snapshot.PlantAppearance.Recipe.GrowthStep==2
                &&service.BeginDailyEmotion(service.CurrentCycleId),"P/M automatically start and grow one successor without banking new growth");
            string recipe=PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            Failed(store,service,()=>service.CompleteDailyEmotion(id,"평온함","synthetic successor memo"),"Successor memo completion failure");
            Check(service.CompleteDailyEmotion(id,"평온함","synthetic successor memo")&&service.Snapshot.StoredGrowthSteps==0&&service.GetPracticeGrowth(id)==null
                &&service.CoinBalance==317,"Daily emotion adds 100 coins and no growth");
            Check(PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe&&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==archived,
                "Emotion preserves the active recipe and the earlier archived plant");
            Check(service.Snapshot.PlantAppearance.Memos.Single().PlantInstanceId==active,"Memo belongs to the actual current successor");
            string stable=Encode(service);Check(service.CompleteDailyEmotion(id,"changed","changed")&&Encode(service)==stable,"Duplicate emotion remains read-only");
            Check(service.SetPlantCompleted(active,true),"Complete successor for automatic archive");
            string memos=PlantCodec.Encode(service.Snapshot.PlantAppearance.Memos);
            Check(PlantCodec.Encode(service.Snapshot.PlantPhotos.Single(p=>p.SourcePlantInstanceId==active).Memos)==memos,
                "Automatic archive includes the successor's own memo");
            string after=Encode(service);Check(service.CompleteDailyEmotion(id,"changed","changed")&&Encode(service)==after,"Retry cannot duplicate coins or archived memo");
        }
        clock=new EmotionClock();store=new EmotionStore();using(var service=Open(store,clock))
        {
            string id=Unlock(service);Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.SaveDailyEmotionDraft(id,"평온함","synthetic pending memo",1),"Prepare unfinished daily draft");
            string source=service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(service.SetPlantCompleted(source,true),"Automatic archive while emotion draft is pending");
            string recipe=PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);
            Check(service.MissionPlantAvailability==null&&GardenPlantVisuals.HasCurrentPlant(service.Snapshot),"Completed archived plant remains available for today's emotion");
            Failed(store,service,()=>service.CompleteDailyEmotion(id,"평온함","synthetic pending memo"),"Mature current memo save failure");
            Check(service.CompleteDailyEmotion(id,"평온함","synthetic pending memo")&&service.Snapshot.PlantAppearance.Memos.Single().PlantInstanceId==source
                &&PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe&&service.Snapshot.PlantPhotos.Single().Memos.Single().PlantInstanceId==source&&service.CoinBalance==317,
                "Pending emotion stays with the visible mature individual, updates album memory and never creates a new seed");
        }
    }
    static void CompatibilityAndValidation()
    {
        var clock=new EmotionClock();var store=new EmotionStore();using(var service=Open(store,clock))
        {
            string id=Unlock(service);var old=service.Snapshot;
            var document=new XmlDocument();document.LoadXml(StateCodec.Encode(old));var optional=document.DocumentElement["DailyEmotionRecords"];optional.ParentNode.RemoveChild(optional);
            var migrated=StateCodec.Decode(document.OuterXml);
            Check(migrated.DailyEmotionRecords!=null&&migrated.DailyEmotionRecords.Count==0&&migrated.Coins==217&&migrated.Sessions.Count==2,"Old schema without emotion list loads without fabricated records or rewards");
            Check(service.BeginDailyEmotion(service.CurrentCycleId),"Create validation record");string before=Encode(service);
            foreach(var bad in new[]{new[]{new string('a',81),""},new[]{"",new string('b',501)}})
                Check(!service.SaveDailyEmotionDraft(id,bad[0],bad[1],0)&&!service.CompleteDailyEmotion(id,bad[0],bad[1])&&Encode(service)==before,"Oversized emotion/note cannot mutate storage");
            Check(!service.SaveDailyEmotionDraft(id,"평온함","",-1)&&!service.SaveDailyEmotionDraft(id,"평온함","",2)&&Encode(service)==before,"Invalid draft stage is rejected");
            var duplicate=service.Snapshot;duplicate.DailyEmotionRecords.Add(service.GetDailyEmotion(id));ExpectInvalid(duplicate,"Duplicate daily record");
            var wrongRole=service.Snapshot;wrongRole.DailyEmotionRecords.Single().BasicSessionId="m";ExpectInvalid(wrongRole,"Incorrect prerequisite role");
            var unknown=service.Snapshot;unknown.DailyEmotionRecords.Single().AdditionalSessionId="missing";ExpectInvalid(unknown,"Missing prerequisite source");
            var wrongDate=service.Snapshot;wrongDate.DailyEmotionRecords.Single().CycleId="2026-11-08";ExpectInvalid(wrongDate,"Record identity/date mismatch");
        }
    }
    static void HistoricalGrowthCompatibility()
    {
        foreach(bool mature in new[]{false,true})
        {
            var clock=new EmotionClock();var store=new EmotionStore();string id;
            using(var service=Open(store,clock))
            {
                id=Unlock(service);Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.SaveDailyEmotionDraft(id,"평온함","synthetic historical v0 note",1),"Prepare explicit old-schema record fixture");
                if(mature)Check(service.SetPlantCompleted(service.Snapshot.PlantAppearance.Recipe.InstanceId,true),"Prepare historical completed plant");
                var old=service.Snapshot;old.Coins=137;foreach(var session in old.Sessions)session.RewardAmount=0;
                var daily=old.DailyEmotionRecords.Single();daily.CompletedUtc=daily.UpdatedUtc;
                var growth=new PracticeGrowthRecord {SessionId=id,EligibilityKey=DailyEmotionPolicy.GrowthReceipt(daily.CycleId),CycleId=daily.CycleId,
                    MissionId=DailyEmotionPolicy.MissionId,CommittedUtc=daily.CompletedUtc,Outcome=mature?PracticeGrowthOutcome.Stored:PracticeGrowthOutcome.Grown};
                if(mature){old.PracticeGrowthRecords.Add(growth);old.StoredGrowthSteps++;}
                else
                {
                    growth.PlantInstanceId=old.PlantAppearance.Recipe.InstanceId;growth.BeforeStep=old.PlantAppearance.Recipe.GrowthStep;
                    growth.BeforeGrowth=GardenPlantVisuals.TotalGrowth(old);old.PracticeGrowthRecords.Add(growth);
                    GardenPlantVisuals.AdvanceMissionGrowth(old);growth.AfterStep=old.PlantAppearance.Recipe.GrowthStep;growth.AfterGrowth=GardenPlantVisuals.TotalGrowth(old);
                    WeeklyPlants.Active(old).BaselineStep++; // Historical v0 award predates the new completion ledger.
                }
                CombinedPlants.CaptureCurrent(old,clock.Time.ToString("O"),old.LastCycleId);
                StateCodec.Validate(old);
                var xml=new XmlDocument();xml.LoadXml(StateCodec.Encode(old));
                foreach(XmlNode record in xml.DocumentElement["DailyEmotionRecords"].ChildNodes)
                    foreach(string field in new[]{"RewardVersion","RewardAmount","MemoId"})if(record[field]!=null)record.RemoveChild(record[field]);
                var memos=xml.DocumentElement["PlantAppearance"]["Memos"];memos.ParentNode.RemoveChild(memos);
                store.State=StateCodec.Decode(xml.OuterXml);
            }
            string expected=StateCodec.Encode(store.State);
            using(var service=Open(store,clock))
            {
                Check(Encode(service)==expected&&service.GetDailyEmotion(id).RewardVersion==0&&service.GetDailyEmotion(id).RewardAmount==0
                    &&service.CoinBalance==137&&service.Snapshot.PlantAppearance.Memos.Count==0,"Old daily growth and wallet load with no retroactive coin or memo");
                store.Fail=true;Check(service.CompleteDailyEmotion(id,"changed","changed")&&Encode(service)==expected,"Old completed retry remains read-only while store fails");store.Fail=false;
                int stored=service.Snapshot.StoredGrowthSteps,steps=service.Snapshot.PlantAppearance.Recipe.GrowthStep;
                Check(service.ResetMissionHistory()&&service.Snapshot.StoredGrowthSteps==stored&&service.Snapshot.PlantAppearance.Recipe.GrowthStep==steps,"Reset preserves historical grown/stored daily reward");
                string repeated=Unlock(service,"-historical-reset");Check(repeated==id&&service.BeginDailyEmotion(service.CurrentCycleId),"Explicit reset creates same-day record without erasing previous reward evidence");
                Check(service.CompleteDailyEmotion(id,"편안함","synthetic explicitly replaced record")&&service.GetDailyEmotion(id).RewardAmount==0
                    &&service.GetDailyEmotion(id).MemoId==""&&service.CoinBalance==137&&service.Snapshot.PlantAppearance.Memos.Count==0
                    &&service.Snapshot.StoredGrowthSteps==stored&&service.Snapshot.PlantAppearance.Recipe.GrowthStep==steps,
                    "A historical daily growth receipt cannot be converted into new coins or a memo after reset");
            }
        }
    }
    static void EmptyMissionResumeGuards()
    {
        foreach(bool paused in new[]{false,true})
        {
            var clock=new EmotionClock();var store=new EmotionStore();using(var service=Open(store,clock))
            {
                Check(service.SelectMbctProgram("8w")&&service.BeginFocusSession("pending-p","P01",1),"Select guided mission before automatic archive");
                string reflection=FocusMissionCatalog.FindForSession(Get(service,"pending-p")).Steps[1].Options.First();
                Check(service.SaveGuidedFocusDraft("pending-p","배",reflection,17),"Preserve selected guided draft: "+service.LastError);
                if(paused)Check(service.StartGuidedFocusSession("pending-p","배")&&service.PauseSession("pending-p"),"Pause active guided mission");
                string source=service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Check(service.SetPlantCompleted(source,true),"Automatically archive while selected or paused");
                string session=PlantCodec.Encode(Get(service,"pending-p")),photo=PlantCodec.Encode(service.Snapshot.PlantPhotos.Single());
                Check(service.MissionPlantAvailability==null&&PlantCodec.Encode(Get(service,"pending-p"))==session,"Automatic archive preserves resumable draft and visible plant");
                Check(paused?service.ResumeSession("pending-p"):service.StartGuidedFocusSession("pending-p","배"),"Resume original draft without a manual seed action");
                Check(service.CompleteGuidedFocusPractice("pending-p","배",reflection,17),"Confirm original practice");
                Failed(store,service,()=>service.CompletePracticeSession("pending-p"),"Atomic successor creation and practice save failure");
                Check(service.CompletePracticeSession("pending-p")&&service.CoinBalance==187
                    &&service.GetPracticeGrowth("pending-p").PlantInstanceId==service.Snapshot.PlantAppearance.Recipe.InstanceId
                    &&service.Snapshot.PlantAppearance.Recipe.InstanceId!=source&&service.Snapshot.PlantAppearance.Recipe.GrowthStep==1
                    &&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==photo,
                    "Resumed practice creates one independent successor and rewards once while preserving the old photograph");
            }
        }
    }
    static void RewardOverflowRollbacks()
    {
        var clock=new EmotionClock();var store=new EmotionStore();
        using(var seed=Open(store,clock)){Ready(seed,"overflow-practice","M03");store.State.Coins=int.MaxValue-29;}
        using(var service=Open(store,clock))
        {
            string before=Encode(service);int writes=store.Saves;
            Check(!service.CompletePracticeSession("overflow-practice")&&Encode(service)==before&&store.Saves==writes,
                "Coin overflow rolls back practice growth, completion, receipt and wallet together");
            Check(service.PurchaseEnvironment("overflow-room","environment:E01","left")&&service.CompletePracticeSession("overflow-practice")
                &&service.CoinBalance==int.MaxValue-399&&service.GetPracticeGrowth("overflow-practice")!=null,
                "After making wallet space the same practice retries once without losing its earned growth");
        }
        store=new EmotionStore();string id;
        using(var seed=Open(store,clock)){id=Unlock(seed);Check(seed.BeginDailyEmotion(seed.CurrentCycleId),"Prepare daily overflow draft");store.State.Coins=int.MaxValue-99;}
        using(var service=Open(store,clock))
        {
            string before=Encode(service);int writes=store.Saves;
            Check(!service.CompleteDailyEmotion(id,"평온함","synthetic overflow memo")&&Encode(service)==before&&store.Saves==writes,
                "Coin overflow creates neither daily completion nor memo or coin receipt");
            Check(service.PurchaseEnvironment("overflow-daily-room","environment:E01","left")&&service.CompleteDailyEmotion(id,"평온함","synthetic overflow memo")
                &&service.CoinBalance==int.MaxValue-399&&service.Snapshot.PlantAppearance.Memos.Count==1,
                "A safe wallet-space retry creates exactly one daily memo and 100-coin reward");
        }
    }
    static void ExpectInvalid(GardenState value,string label)
    {bool invalid=false;try{StateCodec.Validate(value);}catch(InvalidDataException){invalid=true;}Check(invalid,label+" fails closed");}
    static void RealFileReload()
    {
        string directory=Path.Combine(Path.GetTempPath(),"capstone-daily-emotion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"synthetic.xml"),id,expected;var clock=new EmotionClock();
        try
        {
            using(var store=new FileStateStore(path))using(var service=Open(store,clock))
            {id=Unlock(service);Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.SaveDailyEmotionDraft(id,"평온함","synthetic disk record",1),"Write isolated real-file draft");expected=Encode(service);}
            using(var store=new FileStateStore(path))using(var service=Open(store,clock))
            {Check(Encode(service)==expected,"Actual file reopens exact draft");Check(service.CompleteDailyEmotion(id,"평온함","synthetic disk record"),"Complete actual isolated file record");expected=Encode(service);}
            using(var store=new FileStateStore(path))using(var service=Open(store,clock))
                Check(Encode(service)==expected&&service.CompleteDailyEmotion(id,"changed","changed")&&Encode(service)==expected,"Actual file restore and duplicate command preserve exact completed data");
        }
        finally{foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
    static void ClearMemoAssociations(GardenState state)
    {
        foreach (var memo in state.PlantAppearance.Memos.Concat(state.PlantPhotos.SelectMany(photo => photo.Memos)).Concat(state.PlantCollection.Plants.SelectMany(plant=>plant.Memos)))
        { memo.BasicSessionId = memo.BasicMissionId = memo.BasicMissionTitle = ""; memo.AdditionalSessionId = memo.AdditionalMissionId = memo.AdditionalMissionTitle = ""; }
    }
    static void MemoMissionAssociations()
    {
        var clock = new EmotionClock { Time = DateTimeOffset.Parse("2026-11-10T03:59:00Z") }; var store = new EmotionStore();
        using (var service = Open(store, clock))
        {
            string id = Unlock(service, "-association"); Check(service.BeginDailyEmotion(service.CurrentCycleId), "Begin linked daily record");
            Failed(store, service, () => service.CompleteDailyEmotion(id, "평온함", "synthetic linked memo"), "Associated memo transaction");
            Check(service.CompleteDailyEmotion(id, "평온함", "synthetic linked memo"), "Retry associated memo creation");
            var memo = service.Snapshot.PlantAppearance.Memos.Single(); var basic = Get(service, "p-association"); var additional = Get(service, "m-association");
            Check(memo.BasicSessionId == basic.SessionId && memo.BasicMissionId == basic.MissionId && memo.BasicMissionTitle == basic.MissionTitleSnapshot
                && memo.AdditionalSessionId == additional.SessionId && memo.AdditionalMissionId == additional.MissionId && memo.AdditionalMissionTitle == additional.MissionTitleSnapshot,
                "New memo freezes exact completed basic/additional occurrence IDs, mission IDs and title snapshots");
            Check(memo.DailyEmotionRecordId == "daily-emotion:2026-11-09" && basic.InstanceId == additional.InstanceId,
                "Pre-04:00 memo links the actual prior business-day prerequisite pair");
            string frozen = PlantCodec.Encode(memo); string state = Encode(service); int writes = store.Saves;
            var detached = service.Snapshot.PlantAppearance.Memos.Single(); detached.BasicMissionTitle = "mutated"; detached.AdditionalSessionId = "mutated";
            Check(Encode(service) == state, "Association snapshot reads detach every mutable record");
            Check(service.CompleteDailyEmotion(id, "ignored", "ignored") && store.Saves == writes && Encode(service) == state,
                "Repeated daily completion does not rewrite source associations");
            string source = service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(service.SetPlantCompleted(source, true) && service.SavePlantPhoto("synthetic-linked-photo", source), "Archive linked memo");
            string photo = PlantCodec.Encode(service.Snapshot.PlantPhotos.Single());
            Check(PlantCodec.Encode(service.Snapshot.PlantPhotos.Single().Memos.Single()) == frozen, "Photo keeps exact per-day mission associations");
            Check(service.ResetMissionHistory() && service.Snapshot.Sessions.Count == 0 && service.Snapshot.DailyEmotionRecords.Count == 0
                && PlantCodec.Encode(service.Snapshot.PlantPhotos.Single()) == photo, "Deleting original histories preserves photographed occurrence titles and IDs");
        }
    }
    static void LegacyMemoAssociationBackfill()
    {
        var clock = new EmotionClock(); var original = new EmotionStore(); GardenState seed;
        using (var service = Open(original, clock))
        {
            string id = Unlock(service, "-old-links"); Check(service.BeginDailyEmotion(service.CurrentCycleId)
                && service.CompleteDailyEmotion(id, "평온함", "synthetic old linked memo"), "Create trustworthy backfill sources");
            string source = service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(service.SetPlantCompleted(source, true) && service.SavePlantPhoto("synthetic-old-linked-photo", source), "Create old photo fixture");
            seed = service.Snapshot; ClearMemoAssociations(seed);
        }
        var store = new EmotionStore { State = StateCodec.Clone(seed), Fail = true }; string before = StateCodec.Encode(store.State);
        using (var failed = new GardenStateService(store, clock, new DemoBalanceConfig()))
            Check(!failed.Open() && StateCodec.Encode(store.State) == before, "Failed backfill open preserves original old save exactly");
        store.Fail = false;
        using (var service = Open(store, clock))
        {
            var memo = service.Snapshot.PlantPhotos.Single().Memos.Single();
            Check(memo.BasicSessionId == "p-old-links" && memo.AdditionalSessionId == "m-old-links"
                && !string.IsNullOrEmpty(memo.BasicMissionTitle) && !string.IsNullOrEmpty(memo.AdditionalMissionTitle),
                "Exact old record identity, completion instant, mood and note authorize source association backfill");
            string frozen = PlantCodec.Encode(service.Snapshot.PlantPhotos); int writes = store.Saves;
            using (var reopened = Open(store, clock))
                Check(PlantCodec.Encode(reopened.Snapshot.PlantPhotos) == frozen && store.Saves == writes,
                    "Backfill is repeatable without another save or snapshot change");
        }
        foreach (string mismatch in new[] { "note", "mood", "time", "missing" })
        {
            var candidate = StateCodec.Clone(seed);
            if (mismatch == "missing") { candidate.DailyEmotionRecords.Clear(); candidate.Sessions.Clear(); }
            else
            {
                var record = candidate.DailyEmotionRecords.Single();
                if (mismatch == "note") record.Note = "different reused-day note";
                if (mismatch == "mood") record.Mood = "다른 감정";
                if (mismatch == "time") record.CompletedUtc = DateTimeOffset.Parse(record.CompletedUtc).AddMinutes(1).ToString("O");
                // A reset-created duplicate has no memo ownership; its date alone is not provenance.
                record.RewardVersion = 1; record.RewardAmount = 0; record.MemoId = "";
            }
            var mismatchStore = new EmotionStore { State = candidate };
            using (var service = Open(mismatchStore, clock))
            {
                var memo = service.Snapshot.PlantPhotos.Single().Memos.Single();
                Check(memo.BasicSessionId == "" && memo.AdditionalSessionId == "" && memo.BasicMissionTitle == "" && memo.AdditionalMissionTitle == "",
                    "Missing or reused-day " + mismatch + " evidence cannot invent a historical mission link");
            }
        }
        using (var service = Open(new EmotionStore { State = StateCodec.Clone(seed) }, clock))
        {
            Check(service.ResetMissionHistory(), "Reset exact-source legacy history");
            var memo = service.Snapshot.PlantPhotos.Single().Memos.Single();
            Check(memo.BasicSessionId == "p-old-links" && memo.AdditionalSessionId == "m-old-links" && service.Snapshot.Sessions.Count == 0,
                "Known source snapshots survive history reset");
        }
    }

    static void ArchivedMemoKeepsOriginalRecord()
    {
        foreach(var original in new[]{
            new[]{"평온함","첫째 줄\n<b>저장한 글 그대로</b> & 한글\n마지막 줄  "},
            new[]{"잘 모르겠어요",""},
            new[]{"","감정 선택 없이 남긴 메모"}})
        {
            // Before 04:00 the business date is the previous day. The memo's
            // record identity retains that date independently from its UTC time.
            var clock=new EmotionClock{Time=DateTimeOffset.Parse("2026-11-10T03:59:00Z")};
            var store=new EmotionStore();var service=Open(store,clock);
            string recordId=Unlock(service,"-memo-original"),source=service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(recordId=="daily-emotion:2026-11-09"&&service.BeginDailyEmotion(service.CurrentCycleId)
                &&service.CompleteDailyEmotion(recordId,original[0],original[1]),"Complete original memo before the business-day boundary");
            var record=service.GetDailyEmotion(recordId);var memo=service.Snapshot.PlantAppearance.Memos.Single();
            Check(memo.Id==record.MemoId&&memo.DailyEmotionRecordId==recordId&&memo.PlantInstanceId==source
                &&memo.CreatedUtc==record.CompletedUtc&&memo.Mood==original[0]&&memo.Note==original[1],
                "Memo snapshot preserves exact owner, source, completed time, empty fields and literal multiline contents");
            Check(DateTimeOffset.Parse(memo.CreatedUtc).UtcDateTime.ToString("yyyy-MM-dd")=="2026-11-10"
                &&memo.DailyEmotionRecordId.Substring(DailyEmotionPolicy.RecordPrefix.Length)=="2026-11-09",
                "Saved memo retains the original business date even when its completed calendar date differs");
            string memoXml=PlantCodec.Encode(memo);int coins=service.CoinBalance;
            Check(service.SetPlantCompleted(source,true)&&service.SavePlantPhoto("memo-original-photo",source),"Archive original plant and its memo");
            var detached=service.Snapshot;var photo=detached.PlantPhotos.Single();string photoId=photo.Id,photoXml=PlantCodec.Encode(photo);
            Check(PlantCodec.Encode(photo.Memos.Single())==memoXml,"Photo freezes every original memo field at archive time");
            detached.PlantAppearance.Memos[0].Mood="detached current mutation";photo.Memos[0].Note="detached photo mutation";
            Check(PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==photoXml
                &&PlantCodec.Encode(service.Snapshot.PlantAppearance.Memos.Single())==memoXml,
                "Detached current and photo snapshots cannot alter durable memo contents");
            Check(service.StartNewPlant("memo-next-plant",source)&&service.Snapshot.PlantAppearance.Recipe.InstanceId!=source
                &&service.Snapshot.PlantAppearance.Memos.Count==0&&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==photoXml,
                "A new plant has no copied memo and preserves the archived original snapshot");
            Check(service.ResetMissionHistory()&&service.GetDailyEmotion(recordId)==null
                &&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==photoXml,
                "Removing source mission history leaves the photo memo readable without a DailyEmotionRecord");
            service.Dispose();service=Open(store,clock);
            Check(service.GetDailyEmotion(recordId)==null&&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==photoXml,
                "Reopening retains the original memo even while its source record is absent");
            string reused=Unlock(service,"-memo-replacement");
            Check(reused==recordId&&service.BeginDailyEmotion(service.CurrentCycleId)
                &&service.CompleteDailyEmotion(reused,"편안함","동일 날짜에 다시 작성한 다른 내용"),
                "Explicit history reset can create different content under the same daily record identity");
            var replacement=service.GetDailyEmotion(recordId);
            var frozen=service.Snapshot.PlantPhotos.Single(item=>item.Id==photoId).Memos.Single();
            Check(replacement.Mood=="편안함"&&replacement.Note=="동일 날짜에 다시 작성한 다른 내용"
                &&replacement.RewardAmount==0&&replacement.MemoId==""&&service.CoinBalance==coins,
                "Replacement record is distinct content with no duplicate memo or coins");
            Check(frozen.DailyEmotionRecordId==replacement.Id&&PlantCodec.Encode(frozen)==memoXml
                &&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single())==photoXml&&service.Snapshot.PlantAppearance.Memos.Count==0,
                "A popup must use the clicked frozen memo, never substitute the different record found by the reused ID");
            string expected=Encode(service);service.Dispose();service=Open(store,clock);
            Check(Encode(service)==expected&&PlantCodec.Encode(service.Snapshot.PlantPhotos.Single().Memos.Single())==memoXml,
                "Restart preserves both replacement history and unchanged archived memo exactly");
            service.Dispose();
        }
    }
    static void ResetPreservesGrowthReceipts()
    {
        var clock=new EmotionClock();var store=new EmotionStore();using(var service=Open(store,clock))
        {
            string id=Unlock(service);Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.CompleteDailyEmotion(id,"평온함","synthetic first record"),"Complete third mission before history reset");
            string recipe=PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe);int receipts=service.Snapshot.PracticeGrowthRecords.Count;
            Failed(store,service,()=>service.ResetMissionHistory(),"History reset failure");
            Check(service.ResetMissionHistory()&&service.Snapshot.DailyEmotionRecords.Count==0&&service.Snapshot.PracticeGrowthRecords.Count==receipts
                &&PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,"History reset removes daily contents while preserving growth evidence and appearance");
            string repeated=Unlock(service,"-reset");Check(repeated==id&&service.BeginDailyEmotion(service.CurrentCycleId),"Same-day reset reuses stable daily identity");
            Check(service.CompleteDailyEmotion(id,"편안함","synthetic replacement after explicit reset")&&service.GetDailyEmotion(id).Note=="synthetic replacement after explicit reset"
                &&service.Snapshot.PracticeGrowthRecords.Count(r=>r.Outcome==PracticeGrowthOutcome.Grown||r.Outcome==PracticeGrowthOutcome.Stored)==receipts
                &&service.Snapshot.PracticeGrowthRecords.Count(r=>r.SessionId==id)==0&&PlantCodec.Encode(service.Snapshot.PlantAppearance.Recipe)==recipe,
                "Explicit reset allows new contents but retained receipts prevent duplicate growths");
            Check(!FocusMissionPolicy.HasPendingToday(service.Snapshot)&&service.CoinBalance==317
                &&service.GetDailyEmotion(id).RewardAmount==0&&service.GetDailyEmotion(id).MemoId==""
                &&service.Snapshot.PlantAppearance.Memos.Count==1,"History reset retains coin receipts and original memo without paying or attaching again");
        }
    }
}
