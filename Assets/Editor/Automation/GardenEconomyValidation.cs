using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Exercise the real transaction service with memory-only stores. No scene,
    // user preferences, network, native camera, or production save is opened.
    public static class GardenEconomyValidation
    {
        [Serializable] sealed class Result { public string name,status,detail; }
        [Serializable] sealed class Report
        {
            public string utc,unityVersion;
            public string environment="Synthetic memory-only economy transactions; no production saves or device";
            public int passed,failed,assertions;
            public List<Result> checks=new List<Result>();
        }
        static Report report;
        public static void RunBatch()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-gardenEconomyReport");
            if(!Application.isBatchMode||Application.companyName!="CapstoneSyntheticValidation"||at<0||at+1>=args.Length)
                throw new InvalidOperationException("Use a copied synthetic batch project and -gardenEconomyReport path.");
            string path=Path.GetFullPath(args[at+1]),directory=Path.GetDirectoryName(path);
            if(directory==Path.GetFullPath(Application.persistentDataPath)||File.Exists(path))
                throw new InvalidOperationException("A fresh, isolated report path is required.");
            Directory.CreateDirectory(directory);
            report=new Report{utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            try
            {
                Run("Basic 50, additional 30 plus photo 10, emotion 100 and atomic retry",DailyRewards);
                Run("Basic photo pays 60 and the existing shared daily photo limit remains",BasicPhoto);
                Run("Unconfirmed, skipped, manual and automatic photo eligibility",PhotoEligibility);
                Run("Review completion never grants practice or photo coins",Review);
                Run("Per-product price, affordability, save failure, duplicate and reopen",ProductTransactions);
                Run("Both furniture products deduct their distinct prices from one wallet",BothProducts);
                Run("Historical 50+1 rewards and old purchase receipts survive policy changes",HistoricalReceipts);
            }
            finally { File.WriteAllText(path,JsonUtility.ToJson(report,true)); }
            Debug.Log("GARDEN_ECONOMY_VALIDATION passed="+report.passed+" failed="+report.failed+" assertions="+report.assertions);
            EditorApplication.Exit(report.failed==0?0:1);
        }
        static void Run(string name,Action action)
        {
            try{action();report.passed++;report.checks.Add(new Result{name=name,status="pass"});}
            catch(Exception error){report.failed++;report.checks.Add(new Result{name=name,status="fail",detail=error.ToString()});}
        }
        static void Check(bool value,string message)
        {report.assertions++;if(!value)throw new InvalidOperationException(message);}
        sealed class Clock:IClock
        {
            public DateTimeOffset Time=DateTimeOffset.Parse("2032-01-05T10:00:00Z");
            public DateTimeOffset UtcNow=>Time;
            public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
        }
        sealed class Store:IStateStore
        {
            public GardenState State;public bool Fail;public int Saves;
            public GardenState Load()=>State==null?null:StateCodec.Clone(State);
            public void Save(GardenState next)
            {
                if(Fail)throw new IOException("Synthetic economy storage failure");
                State=StateCodec.Clone(next);Saves++;
            }
        }
        sealed class Fixture:IDisposable
        {
            public readonly Store Store=new Store();public readonly Clock Clock=new Clock();
            public GardenStateService Service;
            readonly int initialCoins;
            public Fixture(int coins=0){initialCoins=coins;Reopen();}
            public void Reopen()
            {
                Service?.Dispose();Service=new GardenStateService(Store,Clock,new DemoBalanceConfig{VisitorChancePercent=0},
                    new LegacySnapshot{Nutrient=initialCoins,HasExistingState=initialCoins>0});
                Check(Service.Open(),"Open synthetic state: "+Service.LastError);
            }
            public void NextDay(){Clock.Time=Clock.Time.AddDays(1);Check(Service.RefreshCycle(Array.Empty<string>()),Service.LastError);}
            public void Dispose()=>Service.Dispose();
        }
        static ActivitySession Session(Fixture f,string id)=>f.Service.Snapshot.Sessions.Single(s=>s.SessionId==id);
        static string Saved(Fixture f)=>StateCodec.Encode(f.Service.Snapshot);
        static void FailsAtomically(Fixture f,Func<bool> action,string label)
        {
            string before=Saved(f),durable=StateCodec.Encode(f.Store.State);int writes=f.Store.Saves;
            f.Store.Fail=true;
            try{Check(!action(),label+" reports the storage failure");}finally{f.Store.Fail=false;}
            Check(Saved(f)==before&&StateCodec.Encode(f.Store.State)==durable&&f.Store.Saves==writes,
                label+" preserves both the live and durable state");
        }
        static void Ready(Fixture f,string id,string code,int order=0,bool review=false)
        {
            Check(f.Service.BeginFocusSession(id,code,order,review,program:order>0?"8w":null),"Begin "+code+": "+f.Service.LastError);
            var session=Session(f,id);var mission=FocusMissionCatalog.FindForSession(session);
            if(FocusMissionCatalog.IsGuided(session))
            {
                string choice=mission.Steps[0].Options.FirstOrDefault()??"";
                string notice=mission.Steps[1].Options.FirstOrDefault()??"";
                Check(f.Service.StartGuidedFocusSession(id,choice),f.Service.LastError);
                Check(f.Service.CompleteGuidedFocusPractice(id,choice,notice,60),f.Service.LastError);
            }
            else
            {
                Check(f.Service.StartSession(id),f.Service.LastError);
                while(Session(f,id).InstructionStep<mission.Steps.Length)
                {
                    int index=Session(f,id).InstructionStep;var step=mission.Steps[index];
                    string value=step.Options.FirstOrDefault()??(step.RequiredAnswer?"synthetic":"");
                    Check(f.Service.ConfirmFocusStep(id,index,value),f.Service.LastError);
                }
            }
            f.Clock.Time=f.Clock.Time.AddSeconds(1);
        }
        static void Attach(Fixture f,string id,string outcome="matched",bool confirmed=false)
        {
            string target=CameraMissionPolicy.Target(Session(f,id));
            var photo=new MissionPhoto{Id=Guid.NewGuid().ToString("N"),Target=target,
                ModelVersion=CameraMissionPolicy.ModelVersion,Outcome=outcome,UserConfirmed=confirmed,
                MatchedLabel=outcome=="matched"?(target=="book"?"book":"cup"):"",Score=outcome=="matched"?.8:0,
                CapturedUtc=f.Clock.Time.ToString("O")};
            Check(f.Service.BeginPhotoCapture(id,photo.Id,photo.Target),f.Service.LastError);
            Check(f.Service.RegisterPhoto(id,photo),f.Service.LastError);
        }
        static void Complete(Fixture f,string id)=>Check(f.Service.CompletePracticeSession(id),f.Service.LastError);
        static void DailyRewards()
        {
            using(var f=new Fixture())
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Ready(f,"basic","P01",1);Complete(f,"basic");
                Check(f.Service.CoinBalance==50&&Session(f,"basic").RewardAmount==50,"Main mission pays exactly 50");
                Ready(f,"additional","M03");Attach(f,"additional");
                Check(f.Service.CoinBalance==50,"Photo registration alone does not pay coins");
                FailsAtomically(f,()=>f.Service.CompletePracticeSession("additional"),"Practice + photo completion");
                f.Reopen();Complete(f,"additional");
                Check(f.Service.CoinBalance==90&&Session(f,"additional").RewardAmount==30&&Session(f,"additional").PhotoBonusAmount==10,
                    "Additional practice pays 30 plus its eligible 10-coin photo");
                string committed=Saved(f);int writes=f.Store.Saves;
                f.Store.Fail=true;Complete(f,"additional");f.Store.Fail=false;
                Check(Saved(f)==committed&&f.Store.Saves==writes,"A duplicate completed command is a read-only acknowledgement");
                string cycle=f.Service.CurrentCycleId,id=DailyEmotionPolicy.RecordId(cycle);
                Check(f.Service.BeginDailyEmotion(cycle),f.Service.LastError);
                int growth=f.Service.Snapshot.PracticeGrowthRecords.Count;
                FailsAtomically(f,()=>f.Service.CompleteDailyEmotion(id,"편안함","합성 경제 검증 메모"),"Emotion + memo reward");
                Check(f.Service.CompleteDailyEmotion(id,"편안함","합성 경제 검증 메모"),f.Service.LastError);
                var state=f.Service.Snapshot;
                Check(state.Coins==190&&state.DailyEmotionRecords.Single().RewardAmount==100,"The completed day pays exactly 50+30+10+100");
                Check(state.PracticeGrowthRecords.Count==growth&&state.PlantAppearance.Memos.Count==1&&state.PhotoRewards.Count==1,
                    "Emotion adds one linked memo, no growth or additional photo receipt");
                committed=Saved(f);writes=f.Store.Saves;
                Check(f.Service.CompleteDailyEmotion(id,"다른 감정","중복 요청"),f.Service.LastError);
                Check(Saved(f)==committed&&f.Store.Saves==writes,"Emotion replay preserves its first memo and payment");
                f.Reopen();Check(Saved(f)==committed,"Daily rewards restore without repricing");
            }
        }
        static void BasicPhoto()
        {
            using(var f=new Fixture())
            {
                Check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                for(int order=1;order<=3;order++)
                {if(order>1)f.NextDay();Ready(f,"prior-"+order,"P"+order.ToString("00"),order);Complete(f,"prior-"+order);}
                f.NextDay();Ready(f,"basic-photo","P04",4);Attach(f,"basic-photo");int before=f.Service.CoinBalance;
                Complete(f,"basic-photo");
                Check(f.Service.CoinBalance==before+60&&Session(f,"basic-photo").RewardAmount==50&&Session(f,"basic-photo").PhotoBonusAmount==10,
                    "A related basic mission pays exactly 60 including its photo");
                Ready(f,"second-photo","M14");Attach(f,"second-photo","not_found",true);Complete(f,"second-photo");
                Check(f.Service.CoinBalance==before+90&&Session(f,"second-photo").RewardAmount==30&&Session(f,"second-photo").PhotoBonusAmount==0
                    &&f.Service.Snapshot.PhotoRewards.Count==1,"The existing one-photo-bonus-per-day limit remains shared across roles");
            }
        }
        static void PhotoEligibility()
        {
            foreach(string outcome in new[]{"skip","not_found","unavailable","matched"})
            using(var f=new Fixture())
            {
                Ready(f,"photo","M03");
                if(outcome=="skip")Check(f.Service.SkipPhoto("photo"),f.Service.LastError);
                else Attach(f,"photo",outcome,outcome=="unavailable");
                Complete(f,"photo");int bonus=outcome=="matched"||outcome=="unavailable"?10:0;
                Check(f.Service.CoinBalance==30+bonus&&Session(f,"photo").PhotoBonusAmount==bonus,
                    "Existing automatic/manual confirmation rules determine the 10-coin bonus: "+outcome);
            }
        }
        static void Review()
        {
            using(var f=new Fixture())
            {
                Ready(f,"original","M03");Attach(f,"original");Complete(f,"original");f.NextDay();
                Ready(f,"review","M03",review:true);Attach(f,"review");int coins=f.Service.CoinBalance;
                Complete(f,"review");Check(f.Service.CoinBalance==coins&&Session(f,"review").RewardAmount==0
                    &&Session(f,"review").PhotoBonusAmount==0,"Review pays neither mission nor photo coins");
            }
        }
        static void ProductTransactions()
        {
            foreach(string id in new[]{"environment:E01","environment:E02"})
            {
                int cost=id.EndsWith("E01",StringComparison.Ordinal)?400:1000;
                string slot=id.EndsWith("E01",StringComparison.Ordinal)?"left":"right";
                using(var f=new Fixture(cost-1))
                {
                    Check(f.Service.EnvironmentPrice(id)==cost,"Product has its requested price");
                    string before=Saved(f);int writes=f.Store.Saves;
                    Check(!f.Service.CanPurchaseEnvironment(id,slot)&&!f.Service.PurchaseEnvironment("short",id,slot),"One coin short cannot purchase");
                    Check(Saved(f)==before&&f.Store.Saves==writes,"Insufficient balance creates no furniture, receipt or charge");
                }
                using(var f=new Fixture(cost))
                {
                    Check(f.Service.CanPurchaseEnvironment(id,slot),"Exact balance can buy this product");
                    FailsAtomically(f,()=>f.Service.PurchaseEnvironment("purchase",id,slot),"Furniture purchase");
                    Check(f.Service.PurchaseEnvironment("purchase",id,slot),f.Service.LastError);
                    var state=f.Service.Snapshot;
                    Check(state.Coins==0&&state.Environments.Count==1&&state.PurchaseReceipts.Count==1,"Retry deducts the exact price once");
                    string saved=Saved(f);f.Reopen();
                    Check(Saved(f)==saved&&f.Service.PurchaseEnvironment("purchase",id,slot)&&Saved(f)==saved,"Reopening and replay preserve one purchase");
                    Check(!f.Service.PurchaseEnvironment("purchase",id=="environment:E01"?"environment:E02":"environment:E01",slot),"Reusing a request for another item is rejected");
                    Check(!f.Service.PurchaseEnvironment("another",id,slot)&&Saved(f)==saved,"A second command cannot charge for already-owned furniture");
                }
            }
        }
        static void BothProducts()
        {
            using(var f=new Fixture(1400))
            {
                Check(f.Service.PurchaseEnvironment("flower","environment:E01","left"),f.Service.LastError);
                Check(f.Service.CoinBalance==1000,"Mini flower bed deducts 400");
                Check(f.Service.PurchaseEnvironment("shelter","environment:E02","right"),f.Service.LastError);
                Check(f.Service.CoinBalance==0&&f.Service.Snapshot.Environments.Count==2,"Shelter deducts 1000 from the same wallet");
            }
        }
        static void HistoricalReceipts()
        {
            using(var f=new Fixture())
            {
                Ready(f,"historical-photo","M03");Attach(f,"historical-photo");Complete(f,"historical-photo");
                var old=f.Service.Snapshot;var session=old.Sessions.Single();
                session.RewardAmount=50;session.PhotoBonusAmount=1;old.PhotoRewards.Single().Amount=1;old.Coins=51;
                StateCodec.Validate(old);f.Store.State=StateCodec.Clone(old);f.Reopen();
                string saved=Saved(f);int writes=f.Store.Saves;
                f.Store.Fail=true;Complete(f,"historical-photo");f.Store.Fail=false;
                Check(f.Service.CoinBalance==51&&Session(f,"historical-photo").RewardAmount==50&&Session(f,"historical-photo").PhotoBonusAmount==1
                    &&Saved(f)==saved&&f.Store.Saves==writes,"A historical 50+1 completion remains unchanged and cannot claim the new photo amount");
            }
            using(var f=new Fixture(500))
            {
                Check(f.Service.PurchaseEnvironment("historical-purchase","environment:E01","left"),f.Service.LastError);
                var old=f.Service.Snapshot;old.Coins=490;StateCodec.Validate(old);f.Store.State=StateCodec.Clone(old);f.Reopen();
                string saved=Saved(f);
                Check(f.Service.PurchaseEnvironment("historical-purchase","environment:E01","left")&&Saved(f)==saved
                    &&f.Service.CoinBalance==490&&f.Service.Snapshot.Environments.Count==1,"Old 10-coin purchase stays owned and its receipt causes no repricing");
            }
        }
    }
}
