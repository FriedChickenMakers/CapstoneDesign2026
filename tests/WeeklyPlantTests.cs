using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

class WeeklyPlantTests
{
    sealed class Clock:IClock
    {
        public DateTimeOffset Time=DateTimeOffset.Parse("2026-11-04T03:00:00Z");
        public TimeZoneInfo Zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic KST",TimeSpan.FromHours(9),"Synthetic KST","Synthetic KST");
        public DateTimeOffset UtcNow=>Time;public TimeZoneInfo TimeZone=>Zone;
    }
    sealed class Store:IStateStore
    {
        public GardenState State;public bool Fail;
        public GardenState Load()=>State==null?null:StateCodec.Clone(State);
        public void Save(GardenState s){if(Fail)throw new IOException("Synthetic failure");State=StateCodec.Decode(StateCodec.Encode(s));}
    }
    static int checks;
    static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
    static GardenStateService Open(Store store,Clock clock)
    {var s=new GardenStateService(store,clock,new DemoBalanceConfig());Check(s.Open(),"open: "+s.LastError);Check(s.RefreshCycle(null),"cycle");return s;}
    static void Complete(GardenStateService s,string id,string mood=null)
    {
        Check(s.BeginSession(id,"free-mission:M17",0),"begin: "+s.LastError);Check(s.StartSession(id),"start");
        Check(s.CompleteSession(id,mood),"complete: "+s.LastError);
    }
    static void Main()
    {
        var clock=new Clock();var store=new Store();var s=Open(store,clock);
        Check(s.Snapshot.SchemaVersion==2&&s.Snapshot.PlantCollection.Plants.Count==1,"new installation has one seed");
        Complete(s,"synthetic-1");Check(WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==1,"blank reflection still grows");
        Check(WeeklyPlants.Active(s.Snapshot).Memories.Count==0,"blank creates no memory");
        Complete(s,"synthetic-2","잘 모르겠어요");Complete(s,"synthetic-3");Complete(s,"synthetic-4","synthetic mood");
        var state=s.Snapshot;Check(WeeklyPlants.Active(state).Recipe.GrowthStep==3&&WeeklyPlants.Today(state)==3,"daily three");
        Check(state.PlantCollection.Decisions.Count==4&&WeeklyPlants.Active(state).Memories.Count==2,"capped activity still remembers");
        clock.Time=clock.Time.AddDays(1);s=Open(store,clock);
        Check(s.CompleteSession("synthetic-4")&&WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==3,"denied retry cannot grow tomorrow");
        Complete(s,"synthetic-5");Complete(s,"synthetic-6");Complete(s,"synthetic-7");
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(null),"new day");Complete(s,"synthetic-8");
        Check(WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==7&&WeeklyPlants.Active(s.Snapshot).Recipe.SpeciesId!=null,"differentiate at seven actual activities");
        string species=WeeklyPlants.Active(s.Snapshot).Recipe.SpeciesId;
        int next=9;
        while(!WeeklyPlants.Active(s.Snapshot).Recipe.Mature)
        {
            if(WeeklyPlants.Today(s.Snapshot)==3){clock.Time=clock.Time.AddDays(2);Check(s.RefreshCycle(null),"rest days do not reset");}
            Complete(s,"synthetic-"+next++);
        }
        state=s.Snapshot;Check(WeeklyPlants.Active(state).Recipe.GrowthStep==21&&WeeklyPlants.Active(state).Recipe.SpeciesId==species,"mature and locked");
        string oldId=state.PlantCollection.ActiveId;string oldRecipe=PlantCodec.Encode(WeeklyPlants.Active(state).Recipe);
        clock.Time=clock.Time.AddDays(1);Check(s.RefreshCycle(null),"next plant day");Complete(s,"synthetic-next","synthetic reflection");
        state=s.Snapshot;Check(state.PlantCollection.Plants.Count==2&&WeeklyPlants.Active(state).Recipe.GrowthStep==1,"next plant begins with next activity");
        Check(PlantCodec.Encode(state.PlantCollection.Plants[0].Recipe)==oldRecipe,"completed plant unchanged");
        Check(s.UpdateReflection("synthetic-1","synthetic late mood","synthetic late note"),"late reflection");
        Check(s.Snapshot.PlantCollection.Plants[0].Memories.Any(m=>m.RecordId=="synthetic-1"),"late reflection stays on original plant");
        string organ=WeeklyPlants.FindMemory(s.Snapshot,MemoryRecordKind.SessionReflection,"synthetic-1").OrganId;
        Check(s.UpdateReflection("synthetic-1","synthetic edit",null),"edit reflection");
        Check(WeeklyPlants.FindMemory(s.Snapshot,MemoryRecordKind.SessionReflection,"synthetic-1").OrganId==organ,"edit retains organ identity");
        for(int i=0;i<210;i++)Check(s.SaveExperience("synthetic-experience-"+i,"중립","synthetic event",null,null,null,oldId),"experience: "+s.LastError);
        state=s.Snapshot;Check(state.PlantCollection.Plants[0].Memories.Count==213,"unlimited stored memories");
        var firstPlant=state.PlantCollection.Plants[0];int maxTriangles=0;
        var inputs=firstPlant.Memories.Select(m=>new PlantMemoryLeaf{OrganId=m.OrganId,Slot=m.SlotIndex,Linked=true}).ToArray();
        for(int page=0;page<11;page++)
        {
            var mesh=PlantMemoryGeometry.Build(firstPlant.Recipe,inputs,page);maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);
            Check(mesh.Organs.Count==inputs.Count(m=>m.Slot/21==page)&&mesh.Organs.Count<=21,"visible branch paging");
            foreach(var geometry in mesh.Organs)Check(inputs.Any(m=>m.OrganId==geometry.OrganId),"geometry stable identity");
        }
        int growth=WeeklyPlants.Active(state).Recipe.GrowthStep;
        Check(s.DeleteExperience("synthetic-experience-0"),"delete experience");
        Check(s.SaveExperience("synthetic-experience-0","중립","synthetic stale retry",null,null,null)
            && !s.Snapshot.Experiences.Any(e=>e.Id=="synthetic-experience-0"),"stale save cannot resurrect deleted experience");
        Check(WeeklyPlants.FindMemory(s.Snapshot,MemoryRecordKind.Experience,"synthetic-experience-0")==null&&WeeklyPlants.Active(s.Snapshot).Recipe.GrowthStep==growth,"delete no penalty");
        Check(s.UpdateReflection("synthetic-1",null,null),"delete reflection");
        Check(s.Snapshot.PlantCollection.Plants[0].Memories.First(m=>m.OrganId==organ).Kind==MemoryRecordKind.None,"deleted leaf is decoration");
        string before=StateCodec.Encode(s.Snapshot);store.Fail=true;
        Check(!s.SaveExperience("synthetic-failed","중립","synthetic event",null,null,null),"failed save");
        Check(StateCodec.Encode(s.Snapshot)==before,"failed save leaves record and plant untouched");store.Fail=false;
        Check(s.BeginSession("synthetic-atomic","free-mission:M17",0)&&s.StartSession("synthetic-atomic"),"atomic setup");
        before=StateCodec.Encode(s.Snapshot);store.Fail=true;Check(!s.CompleteSession("synthetic-atomic","synthetic mood"),"failed completion");
        Check(StateCodec.Encode(s.Snapshot)==before,"completion wallet plant record all rollback");store.Fail=false;
        Check(s.CompleteSession("synthetic-atomic","synthetic mood"),"completion retry");
        Check(!s.GrowPlant("synthetic-purchase","plant:P06"),"new plants cannot be bought");
        int today=WeeklyPlants.Today(s.Snapshot);Check(s.ResetMissionHistory(),"reset history");
        Check(WeeklyPlants.Today(s.Snapshot)==today&&s.Snapshot.PlantCollection.Plants.SelectMany(p=>p.Memories).All(m=>m.Kind==MemoryRecordKind.None),"reset clears links but retains quota");
        Complete(s,"synthetic-atomic");Check(WeeklyPlants.Today(s.Snapshot)==today,"reset does not replay completion token");
        clock.Time=clock.Time.AddDays(-20);Check(s.RefreshCycle(null),"clock back");Check(WeeklyPlants.Today(s.Snapshot)==today,"clock rollback keeps quota");
        // Old XML is loaded and checked before a single schema transition save.
        var old=new GardenState{MigrationCompleted=true,Nutrient=77,LegacyGrowth=.3f};GardenPlantVisuals.Sync(old);
        var original=PlantCodec.Encode(old.PlantAppearance.Recipe);var migrationStore=new Store{State=old};
        var migrated=Open(migrationStore,new Clock());
        Check(migrated.Snapshot.Nutrient==77&&migrated.Snapshot.PlantCollection.Plants[0].Legacy,"legacy wallet and plant retained");
        Check(PlantCodec.Encode(migrated.Snapshot.PlantCollection.Plants[0].Recipe)==original&&WeeklyPlants.Active(migrated.Snapshot).Recipe.GrowthStep==0,"legacy exact geometry no retroactive growth");
        var bad=StateCodec.Clone(migrated.Snapshot);bad.PlantCollection.ActiveId="missing";
        bool rejected=false;try{StateCodec.Validate(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"corrupt active pointer rejected");
        migrationStore=new Store{State=old,Fail=true};var failed=new GardenStateService(migrationStore,new Clock(),new DemoBalanceConfig());
        Check(!failed.Open()&&migrationStore.State.SchemaVersion==1,"migration failure preserves original");
        string directory=Path.Combine(Path.GetTempPath(),"synthetic-memory-redaction-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path=Path.Combine(directory,"state.xml");
            using(var fileService=new GardenStateService(new FileStateStore(path),new Clock(),new DemoBalanceConfig()))
            {
                Check(fileService.Open(),"file state opens");
                Check(fileService.SaveExperience("synthetic-redact","중립","SYNTHETIC_ERASE_ME",null,null,null),"file experience saved");
                Check(fileService.SavePreference("synthetic-pref","synthetic"),"create previous-version backup");
                Check(File.ReadAllText(path+".bak").Contains("SYNTHETIC_ERASE_ME"),"fixture backup contains old synthetic record");
                Check(fileService.DeleteExperience("synthetic-redact"),"delete source and redact backup");
                Check(!File.ReadAllText(path).Contains("SYNTHETIC_ERASE_ME")&&!File.Exists(path+".bak"),"deleted text absent from live and automatic backup");
            }
        }
        finally{Directory.Delete(directory,true);}
        var boundaryClock=new Clock{Time=DateTimeOffset.Parse("2026-11-03T18:59:00Z")};var boundary=Open(new Store(),boundaryClock);
        Complete(boundary,"synthetic-before-reset");boundaryClock.Time=boundaryClock.Time.AddMinutes(2);
        Complete(boundary,"synthetic-after-reset");
        Check(WeeklyPlants.Today(boundary.Snapshot)==1&&WeeklyPlants.Active(boundary.Snapshot).Recipe.GrowthStep==2,"completion applies 04:00 boundary without UI refresh");
        var nextClock=new Clock();var nextService=Open(new Store(),nextClock);
        for(int i=0;i<20;i++)
        {
            if(i==2 || (i>2&&(i-2)%3==0)){nextClock.Time=nextClock.Time.AddDays(1);nextService.RefreshCycle(null);}
            Complete(nextService,"synthetic-rollover-"+i);
        }
        nextClock.Time=nextClock.Time.AddDays(1);nextService.RefreshCycle(null);
        Complete(nextService,"synthetic-rollover-21");Complete(nextService,"synthetic-rollover-22");Complete(nextService,"synthetic-rollover-23");Complete(nextService,"synthetic-rollover-24");
        Check(WeeklyPlants.Today(nextService.Snapshot)==3&&nextService.Snapshot.PlantCollection.Plants.Count==2&&WeeklyPlants.Active(nextService.Snapshot).Recipe.GrowthStep==2,"same-day plant rollover shares global daily quota");
        Console.WriteLine(checks+" weekly plant checks passed; visible memory budget max "+maxTriangles+" triangles.");
    }
}
