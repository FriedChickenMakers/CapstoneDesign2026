using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.LocalState;

// Read original-branch-generated synthetic saves, never hand-edited version tags.
static class GuidedV4CompatibilityTests
{
    static int checks;
    sealed class Clock:IClock
    {
        public DateTimeOffset Time; public DateTimeOffset UtcNow=>Time;
        public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
    }
    sealed class Store:IStateStore
    {
        public string Data;public bool Fail;
        public GardenState Load()=>StateCodec.Decode(Data);
        public void Save(GardenState state){if(Fail)throw new IOException("Synthetic v4 resume write failure");Data=StateCodec.Encode(state);}
    }
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static ActivitySession Get(GardenStateService service,string id)=>service.Snapshot.Sessions.Single(s=>s.SessionId==id);
    static void Finish(GardenStateService service,string id)
    {
        var definition=FocusMissionCatalog.FindForSession(Get(service,id));
        while(Get(service,id).InstructionStep<definition.Steps.Length)
        {
            int i=Get(service,id).InstructionStep;var step=definition.Steps[i];
            string value=step.Options.FirstOrDefault()??(step.RequiredAnswer?"synthetic resumed answer":"");
            Check(service.ConfirmFocusStep(id,i,value,value.Length==0),"Original step remains confirmable: "+service.LastError);
        }
        Check(service.CompleteFocusSession(id),"Original session completes: "+service.LastError);
    }
    static void OlderGuidedFallbacks(string directory)
    {
        string raw=File.ReadAllText(Path.Combine(directory,"older-guided","p25-completed-m10-active.xml"));
        GardenState before;using(var reader=new StringReader(raw))before=(GardenState)new System.Xml.Serialization.XmlSerializer(typeof(GardenState)).Deserialize(reader);
        StateCodec.Validate(before);
        Check(before.SchemaVersion==1&&before.Sessions.All(s=>s.FocusVersion==3),"actual earlier writer emits schema1 guided3");
        var store=new Store{Data=raw};var clock=new Clock{Time=DateTimeOffset.Parse(before.LastCycleId+"T12:00:00Z")};
        using(var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0}))
        {
            Check(service.Open(),"Original guided alternate-route completions migrate: "+service.LastError);
            Check(service.Snapshot.SchemaVersion==3&&service.Snapshot.Sessions.All(s=>s.FocusVersion==4),"original guided3 resolves to immutable v4, never texture3 or new5");
            foreach(int order in new[]{19,26})
            {
                var original=before.Sessions.Single(s=>s.MissionId=="course:P"+order);
                var resumed=service.Snapshot.Sessions.Single(s=>s.SessionId==original.SessionId);
                Check(original.Status==ParticipationState.RewardCommitted&&original.FocusStepRecords.First(r=>r.Key=="route").Value==(order==19?"짧은 소리 실습":"아직 없어요"),"original writer actually completed its historical fallback P"+order);
                Check(resumed.Status==original.Status&&resumed.RewardAmount==original.RewardAmount&&resumed.FocusStepRecords.First(r=>r.Key=="route").Value==original.FocusStepRecords.First(r=>r.Key=="route").Value,"migration retains old meaning without applying current alternative rule P"+order);
            }
        }
    }
    static void Main(string[] args)
    {
        Check(args.Length==1,"Supply original v4 fixture directory");
        foreach(string name in new[]{"p01-active","p25-active","p25-completed-m10-active"})
        {
            string raw=File.ReadAllText(Path.Combine(args[0],name+".xml"));var before=StateCodec.Decode(raw);
            var clock=new Clock{Time=DateTimeOffset.Parse(before.LastCycleId+"T12:00:00Z")};var store=new Store{Data=raw};
            using(var service=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0}))
            {
                Check(service.Open(),"Open actual incoming save: "+service.LastError);
                Check(StateCodec.Encode(service.Snapshot)==StateCodec.Encode(before),"Opening current-schema v4 does not rewrite content, answers, wallet or plants");
                Check(service.Snapshot.Sessions.All(s=>s.FocusVersion==4),"Historical sessions remain v4");
                foreach(var old in before.Sessions)
                {
                    var current=Get(service,old.SessionId);var definition=FocusMissionCatalog.FindForSession(current);
                    Check(definition.Steps.Length==old.FocusStepRecords.Count&&definition.Steps.Select(s=>s.Key).SequenceEqual(old.FocusStepRecords.Select(r=>r.Key)),"Old indexes and keys retain their original definition");
                    if(old.MissionId=="course:P04")Check(CameraMissionPolicy.Target(current)=="container","v4 photo eligibility survives latest-version bump");
                }
                string active=service.Snapshot.Sessions.Single(FocusMissionPolicy.IsActive).SessionId;
                string unchanged=StateCodec.Encode(service.Snapshot);store.Fail=true;
                bool saved=name=="p01-active"?service.SaveGuidedFocusDraft(active,"코","synthetic retry",13):service.SaveFocusDraft(active,Get(service,active).InstructionStep,"synthetic retry");
                Check(!saved&&StateCodec.Encode(service.Snapshot)==unchanged&&store.Data==raw,"Failed resume preserves exact original durable bytes and live state");store.Fail=false;
                if(name=="p01-active")
                {
                    Check(FocusMissionCatalog.IsGuided(Get(service,active)),"v4 remains guided");
                    Check(service.CompleteGuidedFocusPractice(active,"코","",12),"v4 guided practice resumes");
                }
                if(name=="p25-active")
                    Check(!FocusMissionCatalog.FindForSession(Get(service,active)).Steps[2].RequiredAnswer,"v4 P25 empty speak acknowledgement remains valid");
                if(name=="p25-completed-m10-active")
                {
                    Check(Get(service,"original-v4-p25").Status==ParticipationState.RewardCommitted&&Get(service,"original-v4-p25").FocusStepRecords[2].Value=="","Old completed P25 is not rejected by v5's required choice");
                    Check(FocusMissionCatalog.FindForSession(Get(service,active)).Steps[0].Kind=="input","v4 M10 retains free-text editing");
                    Check(service.SaveFocusDraft(active,0,"synthetic edited old plan"),"v4 plan remains editable without creating structured fields");
                    Check(Get(service,active).FocusStepRecords[0].PlanActivity==null,"v4 edit has no invented structured choices");
                }
                Finish(service,active);
                Check(Get(service,active).FocusVersion==4&&Get(service,active).RewardAmount==50,"Original version completes with current one-time reward");
                string completed=store.Data;
                using(var reopened=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0}))
                    Check(reopened.Open()&&store.Data==completed,"Second open preserves completion without duplicate migration/reward");
            }
        }
        OlderGuidedFallbacks(args[0]);
        Console.WriteLine("Guided v4 source compatibility: "+checks+" assertions PASS");
    }
}
