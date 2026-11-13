using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

sealed class ResetClock : IClock
{
    public DateTimeOffset Time=DateTimeOffset.Parse("2026-11-09T12:00:00Z");
    public DateTimeOffset UtcNow=>Time;
    public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
}
sealed class ResetStore : IStateStore
{
    public GardenState State; public bool Fail; public int Saves;
    public GardenState Load()=>State==null?null:StateCodec.Clone(State);
    public void Save(GardenState value){if(Fail)throw new IOException("Synthetic reset save failure");State=StateCodec.Clone(value);Saves++;}
}
static class FullResetTests
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static GardenStateService Open(IStateStore store,ResetClock clock,LegacySnapshot legacy=null)
    {
        var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=100},legacy);
        Check(service.Open(),"Open synthetic reset fixture: "+service.LastError);return service;
    }
    static string Xml(GardenStateService service)=>StateCodec.Encode(service.Snapshot);
    static ActivitySession Session(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(item=>item.SessionId==id);
    static void Complete(GardenStateService service,string id,string code,int order=0)
    {
        Check(service.BeginFocusSession(id,code,order)&&service.StartSession(id),"Start synthetic completion "+id+": "+service.LastError);
        var mission=FocusMissionCatalog.FindForSession(Session(service,id));
        for(int i=0;i<mission.Steps.Length;i++)
        {var step=mission.Steps[i];var value=step.RequiredAnswer?step.Options.FirstOrDefault()??"synthetic answer":null;Check(service.ConfirmFocusStep(id,i,value,value==null),"Confirm synthetic step "+id);}
        Check(service.CompletePracticeSession(id),"Complete fixture "+id+": "+service.LastError);
    }
    static GardenState RichState(ResetClock clock)
    {
        var store=new ResetStore();
        using(var service=Open(store,clock,new LegacySnapshot{Nutrient=1500,GardenXp=93,Growth=.2f,LastUnlock="synthetic prior unlock",UnlockIds={"synthetic-seed"},CompletedMissionIds={"free-mission:legacy-synthetic"}}))
        {
            Check(service.RefreshCycle(new[]{"animal:A01"})&&service.SelectMbctProgram("8w"),"Prepare old course and visitor");
            Complete(service,"reset-old-p","P01",1);Complete(service,"reset-old-m","M03");
            string emotion=DailyEmotionPolicy.RecordId(service.CurrentCycleId);
            Check(service.BeginDailyEmotion(service.CurrentCycleId)&&service.CompleteDailyEmotion(emotion,"평온함","synthetic archived memo"),"Create original daily memo");
            Check(service.PurchaseEnvironment("reset-old-flower","environment:E01","left")&&service.PurchaseEnvironment("reset-old-shelter","environment:E02","right"),"Place purchased scenery");
            Check(service.SavePreference("synthetic-preference","synthetic choice")&&service.SaveExperience("reset-old-journal","중립","synthetic event","synthetic body","synthetic feeling","synthetic thought"),"Create original records");
            string first=service.Snapshot.PlantAppearance.Recipe.InstanceId;
            Check(service.SetPlantCompleted(first,true)&&service.SavePlantPhoto("reset-old-photo",first)&&service.StartNewPlant("reset-old-seed",first),"Create archived memo/photo and successor");
            Check(service.AdvancePreviewDay(new[]{"animal:A02"}),"Create app-only date offset");
            Check(service.BeginFocusSession("reset-pending-p","P02",2)&&service.StartGuidedFocusSession("reset-pending-p","")
                &&service.SaveGuidedFocusDraft("reset-pending-p","","synthetic pending sound",31)&&service.PauseSession("reset-pending-p"),"Create paused guided draft");
            Check(service.BeginFocusSession("reset-pending-m","M15")&&service.StartWalkingSession("reset-pending-m",new WalkingTarget{GoalSteps=5000,
                QueryStartUtc=new DateTimeOffset(service.CurrentUtc.UtcDateTime.Date,TimeSpan.Zero).ToString("O"),QueryEndUtc=new DateTimeOffset(service.CurrentUtc.UtcDateTime.Date.AddDays(1),TimeSpan.Zero).ToString("O"),Basis="SYNTHETIC"}),"Create pending walking occurrence");
            Check(service.EnableDebugWalk(),"Create debug walking period");var period=service.Snapshot.DebugWalkPeriods.Last().Id;
            Check(service.ObserveDebugWalk(period,10),"Create walking high-water and reward");
            var state=service.Snapshot;
            // Explicit historical fields that are still accepted by schema 1.
            state.CompletedCourseIds.Add("course:P01");state.NextCourseOrder=2;
            state.MbctCompletedIds.Add("course:MBCT01");state.MbctNextOrder=2;
            state.MbctPrograms.Add(new MbctProgramProgress{ProgramId="2w",NextOrder=2,CompletedIds={"course:MBCT2W01"}});
            state.PurchaseReceipts.Add("reset-old-paid-growth");state.PurchaseCommands.Add(new PurchaseCommand{RequestId="reset-old-paid-growth",Command="growth|plant:P06"});
            state.GrowthHistory.Add(new GardenGrowthRecord{RequestId="reset-old-paid-growth",PlantId="plant:P06",LinkedSessionId="reset-old-p",CreatedUtc=clock.UtcNow.ToString("O"),BeforeGrowth=0,AfterGrowth=.1f});
            state.Plants.Add(new PlantProgress{PlantId="plant:P06",Growth=.1f});
            StateCodec.Validate(state);return state;
        }
    }
    static void AssertFresh(GardenState value,string original,bool pending)
    {
        Check(value.InstallationId!=original&&value.MigrationCompleted&&value.FullResetPending==pending,"Fresh generation and durable reset state are explicit");
        Check(value.Coins==0&&value.GardenXp==0&&value.LegacyGrowth==0&&value.LastUnlock==""&&value.PreviewDayOffset==0&&value.StoredGrowthSteps==0,"Reset clears wallet, XP, legacy growth, date simulation and stored growth");
        Check(value.NextCourseOrder==1&&value.MbctNextOrder==1&&!value.MbctProgramChosen&&value.MbctProgramSelectionVersion==1,"Reset restores first-install unchosen course state");
        foreach(var field in typeof(GardenState).GetFields(BindingFlags.Public|BindingFlags.Instance).Where(field=>typeof(IList).IsAssignableFrom(field.FieldType)))
            Check(((IList)field.GetValue(value))?.Count==0,"Reset clears state collection "+field.Name);
        Check(value.PlantAppearance!=null&&GardenPlantVisuals.HasCurrentPlant(value)&&!GardenPlantVisuals.IsCompleted(value)
            &&value.PlantAppearance.Recipe.GrowthStep==0&&value.PlantAppearance.Memos.Count==0&&value.PlantAppearance.Credits.Count==0
            &&value.PlantAppearance.ArchivedCredits.Count==0,"Reset creates a clean initial seed with no memo or earned appearance credit");
        StateCodec.Validate(value);
    }
    static void AtomicMemoryLifecycle()
    {
        var clock=new ResetClock();var store=new ResetStore{State=RichState(clock)};
        using(var service=Open(store,clock))
        {
            string source=service.Snapshot.InstallationId,before=Xml(service);int saves=store.Saves;
            Check(!service.ResetAll("","" )&&!service.ResetAll("synthetic-reset","wrong-generation")&&Xml(service)==before&&store.Saves==saves,"Invalid or stale reset cannot erase state");
            foreach(string invalid in new[]{"bad/request","bad request","bad.request","한글요청",new string('a',129)})
                Check(!service.ResetAll(invalid,source)&&Xml(service)==before&&store.Saves==saves,"Native-incompatible request is rejected before durable commit: "+invalid.Length);
            store.Fail=true;Check(!service.ResetAll("synthetic-reset",source)&&Xml(service)==before&&StateCodec.Encode(store.State)==before,"Failure before reset commit preserves the entire prior state");store.Fail=false;
            Check(service.ResetAll("synthetic-reset",source)&&service.HasPendingFullReset,"Successful first phase commits authoritative pending generation");
            AssertFresh(service.Snapshot,source,true);string pending=Xml(service);saves=store.Saves;
            Check(service.ResetAll("synthetic-reset",source)&&Xml(service)==pending&&store.Saves==saves,"Repeated reset request acknowledges the same pending generation without another write");
            Check(!service.ResetAll("different-reset",source)&&Xml(service)==pending,"A second reset cannot replace a pending generation");
            Check(!service.SelectMbctProgram("8w")&&!service.RefreshCycle(Array.Empty<string>())&&!service.AdvancePreviewDay()
                &&!service.BeginSession("stale-new-mission","free-mission:M03")&&!service.SaveExperience("stale-note","중립","e","b","f","t")
                &&!service.PurchaseEnvironment("stale-purchase","environment:E01","left")&&!service.ResetMissionHistory()
                &&Xml(service)==pending,"Pending reset blocks ordinary app mutations");
            store.Fail=true;Check(!service.FinishFullReset("synthetic-reset")&&Xml(service)==pending&&service.HasPendingFullReset,"Failure finishing cleanup preserves pending state without rolling back old data");store.Fail=false;
            Check(!service.FinishFullReset("wrong-reset")&&Xml(service)==pending,"Mismatched completion cannot finalize reset");
            Check(service.FinishFullReset("synthetic-reset")&&!service.HasPendingFullReset,"Cleanup completion unlocks fresh state");
            AssertFresh(service.Snapshot,source,false);string complete=Xml(service);saves=store.Saves;
            store.Fail=true;Check(service.ResetAll("synthetic-reset",source)&&service.FinishFullReset("synthetic-reset")&&Xml(service)==complete&&store.Saves==saves,"Completed reset retry is read-only even while storage fails");store.Fail=false;
            Check(!service.CompletePracticeSession("reset-old-p")&&!service.CompleteDailyEmotion("daily-emotion:2026-11-09","changed","changed")
                &&!service.ObserveMissionSteps("reset-pending-m",9000,"synthetic source")&&!service.GrowPlant("reset-old-paid-growth","plant:P06")
                &&Xml(service)==complete,"Old completion, emotion, step and purchase callbacks cannot reward the new generation");
            Check(service.RefreshCycle(Array.Empty<string>())&&service.SelectMbctProgram("8w"),"Fresh account can select course");Complete(service,"fresh-p","P01",1);
            string progressed=Xml(service);Check(service.CoinBalance==50&&service.ResetAll("synthetic-reset",source)&&Xml(service)==progressed,"Old reset retry cannot erase new progress after completion");
            Check(!service.ResetAll("new-user-reset",source)&&Xml(service)==progressed,"A stale generation cannot authorize a later reset");
        }
        using(var reopened=Open(store,clock,new LegacySnapshot{Nutrient=9999,GardenXp=999,Growth=2}))
            Check(reopened.CoinBalance==50&&reopened.Snapshot.Sessions.Count==1,"Restart never reimports legacy values after a successful reset");
    }
    static void PendingRestart()
    {
        var clock=new ResetClock();var store=new ResetStore{State=RichState(clock)};string source=store.State.InstallationId,pending;
        using(var service=Open(store,clock)){Check(service.ResetAll("restart-reset",source),"Commit pending reset before process restart");pending=Xml(service);}
        using(var service=Open(store,clock,new LegacySnapshot{Nutrient=9999}))
        {
            Check(service.HasPendingFullReset&&Xml(service)==pending&&service.CoinBalance==0,"Restart opens the pending generation and never the prior wallet");
            Check(service.FinishFullReset("restart-reset"),"Restart can complete the same reset request");AssertFresh(service.Snapshot,source,false);
        }
    }
    static void Main()
    {
        AtomicMemoryLifecycle();PendingRestart();FileJournalLifecycle();InactiveStoresAndMetadata();
        Console.WriteLine("Full reset tests passed: "+checks+" assertions.");
    }
    static void InactiveStoresAndMetadata()
    {
        var clock=new ResetClock();var original=RichState(clock);
        var xml=new System.Xml.XmlDocument();xml.LoadXml(StateCodec.Encode(original));
        foreach(string field in new[]{"FullResetRequestId","FullResetSourceInstallationId","FullResetPending"})xml.DocumentElement.RemoveChild(xml.DocumentElement[field]);
        Check(StateCodec.Encode(StateCodec.Decode(xml.OuterXml))==StateCodec.Encode(original),"Old schema without reset metadata round-trips without inventing a reset");
        foreach(string fault in new[]{"null-request","null-source","orphan-pending","forged-generation"})
        {
            var bad=StateCodec.Clone(original);
            if(fault=="null-request")bad.FullResetRequestId=null;
            else if(fault=="null-source")bad.FullResetSourceInstallationId=null;
            else if(fault=="orphan-pending")bad.FullResetPending=true;
            else{bad.FullResetRequestId="forged";bad.FullResetSourceInstallationId="old-generation";}
            bool rejected=false;try{StateCodec.Validate(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid reset metadata fails closed: "+fault);
        }
        var pending=GardenStateService.CreateFullResetState("metadata-reset",original.InstallationId);pending.Coins=1;
        bool polluted=false;try{StateCodec.Validate(pending);}catch(InvalidDataException){polluted=true;}Check(polluted,"Pending reset cannot contain old or newly earned data");
        string directory=Path.Combine(Path.GetTempPath(),"capstone-inactive-reset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            foreach(string mode in new[]{"mock","replay","broken"})
            {
                string path=Path.Combine(directory,mode+".xml");
                using(var file=new FileStateStore(path)){file.Save(original);file.Save(original);}
                if(mode=="broken"){File.WriteAllText(path,"synthetic corrupted inactive state");File.WriteAllText(path+".reset","synthetic corrupted inactive journal");}
                using(var file=new FileStateStore(path))
                {
                    bool lockRejected=false;try{using(var other=new FileStateStore(path))other.ResetInactiveForFullReset("must-not-open");}catch(IOException){lockRejected=true;}
                    Check(lockRejected,"Inactive mode cleanup respects the existing writer lock: "+mode);
                    file.ResetInactiveForFullReset("all-modes-reset");var fresh=file.Load();AssertFresh(fresh,original.InstallationId,false);
                    string before=File.ReadAllText(path);file.ResetInactiveForFullReset("all-modes-reset");
                    Check(File.ReadAllText(path)==before&&!File.Exists(path+".bak")&&!File.Exists(path+".reset"),"Repeated inactive cleanup neither rewrites nor revives prior files: "+mode);
                    fresh.Coins=23;file.Save(fresh);before=File.ReadAllText(path);file.ResetInactiveForFullReset("all-modes-reset");
                    Check(File.ReadAllText(path)==before&&file.Load().Coins==23,"A duplicate cleanup request cannot erase progress earned after that mode finished reset");
                }
            }
        }
        finally{foreach(string file in Directory.GetFiles(directory))File.Delete(file);Directory.Delete(directory);}
    }
    static void FileJournalLifecycle()
    {
        string directory=Path.Combine(Path.GetTempPath(),"capstone-full-reset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"synthetic.xml"),journal=path+".reset";var clock=new ResetClock();var original=RichState(clock);
        try
        {
            using(var file=new FileStateStore(path)){file.Save(original);file.Save(original);}
            string oldPrimary=File.ReadAllText(path),oldBackup=File.ReadAllText(path+".bak"),pending;
            using(var file=new FileStateStore(path))using(var service=Open(file,clock))
            {
                string before=Xml(service);Directory.CreateDirectory(journal+".tmp");
                Check(!service.ResetAll("file-reset",original.InstallationId)&&Xml(service)==before&&File.ReadAllText(path)==oldPrimary
                    &&File.ReadAllText(path+".bak")==oldBackup&&!File.Exists(journal),"Journal staging failure preserves live state, primary and backup byte for byte");
                Directory.Delete(journal+".tmp");
                Check(service.ResetAll("file-reset",original.InstallationId)&&File.Exists(journal),"Retry publishes the authoritative reset journal");
                pending=Xml(service);AssertFresh(file.Load(),original.InstallationId,true);
                Check(StateCodec.Encode(file.Load())==pending,"A pending journal always wins over the previous primary and backup");
            }
            // Damaged old primary is irrelevant once the reset generation is authoritative.
            File.WriteAllText(path,"synthetic damaged pre-reset primary");
            using(var file=new FileStateStore(path))using(var service=Open(file,clock,new LegacySnapshot{Nutrient=9999}))
            {
                Check(Xml(service)==pending&&service.HasPendingFullReset,"Restart recovers pending reset despite damaged old primary and nonzero legacy snapshot");
                Directory.CreateDirectory(path+".tmp");
                Check(!service.FinishFullReset("file-reset")&&Xml(service)==pending&&File.Exists(journal),"Primary staging failure after reset commit retains pending recovery");
                Directory.Delete(path+".tmp");
                // An unexpected directory must fail file-only cleanup on every
                // platform. An open FileStream does not prevent unlink on Unix.
                string cleanupBlocker=journal+".tmp";
                Directory.CreateDirectory(cleanupBlocker);
                try
                {
                    Check(!service.FinishFullReset("file-reset")&&service.HasPendingFullReset&&Xml(service)==pending,
                        "Post-primary cleanup failure must remain pending rather than report success or restore old data");
                    Check(!File.ReadAllText(path).Contains("synthetic archived memo")&&StateCodec.Encode(file.Load())==pending,
                        "Fresh primary exists while authoritative pending journal still governs recovery");
                    Check(Directory.Exists(cleanupBlocker)&&File.Exists(journal),
                        "File-only cleanup preserves the unexpected directory and the authoritative pending journal");
                }
                finally{Directory.Delete(cleanupBlocker);}
            }
            using(var file=new FileStateStore(path))using(var service=Open(file,clock))
            {
                Check(service.HasPendingFullReset&&Xml(service)==pending,"Second restart after partial cleanup retains the same request and fresh generation");
                Check(service.FinishFullReset("file-reset"),"Same-request cleanup retry succeeds: "+service.LastError);AssertFresh(service.Snapshot,original.InstallationId,false);
                Check(!File.Exists(path+".bak")&&!File.Exists(path+".tmp")&&!File.Exists(journal+".tmp")&&!File.Exists(journal),"Successful cleanup leaves no old backup or reset staging files");
                Check(!Directory.GetFiles(directory).Where(item=>!item.EndsWith(".lock",StringComparison.Ordinal)).Any(item=>File.ReadAllText(item).Contains("synthetic archived memo")),"No app-save artifact retains the deleted memo content after cleanup");
            }
            using(var file=new FileStateStore(path))using(var service=Open(file,clock,new LegacySnapshot{Nutrient=9999,Growth=2}))
                AssertFresh(service.Snapshot,original.InstallationId,false);
            string completedPrimary=File.ReadAllText(path);File.Delete(path);File.WriteAllText(path+".bak",oldBackup);
            using(var file=new FileStateStore(path))
            {
                var service=new GardenStateService(file,clock,new DemoBalanceConfig(),new LegacySnapshot{Nutrient=9999});
                Check(!service.Open()&&File.ReadAllText(path+".bak")==oldBackup&&!File.Exists(path),"A missing fresh primary never resurrects an old backup or reimports legacy coins");service.Dispose();
            }
            File.WriteAllText(path,completedPrimary);File.Delete(path+".bak");
            // A corrupt authoritative reset journal must never fall back to a valid old account.
            using(var file=new FileStateStore(path)){file.Save(original);file.Save(original);}
            using(var file=new FileStateStore(path))using(var service=Open(file,clock))Check(service.ResetAll("corrupt-reset",original.InstallationId),"Prepare corruption test journal");
            string validJournal=File.ReadAllText(journal);File.WriteAllText(journal,"synthetic corrupt authoritative journal");
            using(var file=new FileStateStore(path))
            {
                var service=new GardenStateService(file,clock,new DemoBalanceConfig());Check(!service.Open(),"Corrupt reset journal fails closed instead of exposing the old primary");service.Dispose();
            }
            File.WriteAllText(journal,validJournal);
            using(var file=new FileStateStore(path))using(var service=Open(file,clock))Check(service.FinishFullReset("corrupt-reset"),"Restoring the valid journal completes its original reset without old-data recovery");
        }
        finally
        {
            foreach(string file in Directory.GetFiles(directory))File.Delete(file);
            foreach(string child in Directory.GetDirectories(directory))Directory.Delete(child);
            Directory.Delete(directory);
        }
    }
}
