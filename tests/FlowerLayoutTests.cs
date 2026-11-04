using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

class FlowerLayoutTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static double Dot(PlantVector a,PlantVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    // CPU rasterization compares each head's isolated projected pixels with the
    // nearest head in the combined flower-only image. Leaves are excluded to
    // isolate flower-to-flower occlusion; this is not a perceptual user study.
    static double Occlusion(PlantMeshData mesh,int view)
    {
        const int size=144;var ids=mesh.Flowers.Select(f=>f.Head).Distinct().ToArray();
        if(ids.Length<2)return 0;
        var alone=ids.ToDictionary(id=>id,id=>new HashSet<int>());
        var owner=Enumerable.Repeat(-1,size*size).ToArray();var depth=Enumerable.Repeat(double.NegativeInfinity,size*size).ToArray();
        PlantVector right,up;PlantFlowerLayout.ViewAxes(view,out right,out up);var forward=PlantVector.Cross(right,up)*-1;
        foreach(var flower in mesh.Flowers)
        for(int triangle=flower.FirstTriangle;triangle<flower.FirstTriangle+flower.TriangleCount;triangle++)
        {
            var v=new PlantVector[3];
            for(int k=0;k<3;k++){var p=mesh.Vertices[triangle*3+k];v[k]=new PlantVector((Dot(p,right)+.65)/1.3*size,(Dot(p,up)+.3)/1.6*size,Dot(p,forward));}
            double denominator=(v[1].Y-v[2].Y)*(v[0].X-v[2].X)+(v[2].X-v[1].X)*(v[0].Y-v[2].Y);
            if(Math.Abs(denominator)<1e-12)continue;
            int left=Math.Max(0,(int)Math.Floor(v.Min(p=>p.X))),rightEdge=Math.Min(size-1,(int)Math.Ceiling(v.Max(p=>p.X)));
            int bottom=Math.Max(0,(int)Math.Floor(v.Min(p=>p.Y))),top=Math.Min(size-1,(int)Math.Ceiling(v.Max(p=>p.Y)));
            for(int y=bottom;y<=top;y++)for(int x=left;x<=rightEdge;x++)
            {
                double a=((v[1].Y-v[2].Y)*(x+.5-v[2].X)+(v[2].X-v[1].X)*(y+.5-v[2].Y))/denominator;
                double b=((v[2].Y-v[0].Y)*(x+.5-v[2].X)+(v[0].X-v[2].X)*(y+.5-v[2].Y))/denominator;
                double c=1-a-b;if(a<0||b<0||c<0)continue;
                int pixel=y*size+x;alone[flower.Head].Add(pixel);double z=a*v[0].Z+b*v[1].Z+c*v[2].Z;
                if(z>depth[pixel]){depth[pixel]=z;owner[pixel]=flower.Head;}
            }
        }
        return ids.Average(id=>1-owner.Count(n=>n==id)/(double)Math.Max(1,alone[id].Count));
    }
    static string Poses(FlowerPose[] poses)=>string.Join("|",poses.Select(p=>PlantCodec.Encode(p)));
    sealed class Clock:IClock{public DateTimeOffset UtcNow=>DateTimeOffset.Parse("2026-11-04T12:00:00Z");public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;}
    sealed class Store:IStateStore
    {
        public GardenState State;public bool Fail;
        public GardenState Load()=>StateCodec.Clone(State);
        public void Save(GardenState s){if(Fail)throw new IOException("Synthetic migration failure");State=StateCodec.Clone(s);}
    }
    static void Main()
    {
        int seeds=int.TryParse(Environment.GetEnvironmentVariable("CAPSTONE_FLOWER_SEEDS"),out int count)?count:12;
        var timings=new List<double>();var coldTimings=new List<double>();var oldCoverage=new List<double>();var newCoverage=new List<double>();
        var lines=new List<string>{"species,seed,view,old_hidden_fraction,new_hidden_fraction"};int triangles=0;
        foreach(var species in PlantCatalog.All)for(int seed=0;seed<seeds;seed++)
        {
            var p=PlantGenerator.Create("synthetic-flower-layout-v1",species.Id+"/"+seed,21,PlantGrowthMode.MissionCompletions);
            p.Counts=species.ExampleCounts();p.MeshVersion=PlantGeometry.CurrentMeshVersion;FlowerPose[] prior=null;
            for(int step=1;step<=21;step++)
            {
                p=PlantGenerator.Advance(p);
                if(p.SpeciesId==null)continue;
                var poses=PlantFlowerLayout.Plan(p);
                if(prior!=null)Check(Poses(prior)==Poses(poses.Take(prior.Length).ToArray()),"Growing changes existing mature flower poses");
                prior=poses;
            }
            string saved=PlantCodec.Encode(p);var clock=Stopwatch.StartNew();var current=PlantGeometry.Build(p);clock.Stop();timings.Add(clock.Elapsed.TotalMilliseconds);
            Check(PlantCodec.Encode(p)==saved,"Rendering mutated saved recipe");
            PlantFlowerLayout.ClearCache();clock.Restart();var uncached=PlantFlowerLayout.Plan(p);clock.Stop();coldTimings.Add(clock.Elapsed.TotalMilliseconds);
            string original=Poses(uncached);uncached[0].Center=new PlantVector(9,9,9);
            Check(Poses(PlantFlowerLayout.Plan(p))==original,"Caller changed cached flower placement");
            Check(Poses(PlantFlowerLayout.Plan(p))==Poses(PlantFlowerLayout.Plan(PlantCodec.Decode<PlantRecipe>(saved))),"Reload changed layout");
            Check(current.Vertices.All(v=>PlantGenerator.InEnvelope(v))&&current.TriangleCount<=8000,"Geometry budget exceeded");triangles=Math.Max(triangles,current.TriangleCount);
            var legacy=PlantCodec.Clone(p);legacy.MeshVersion=4;var old=PlantGeometry.Build(legacy);
            Check(old.Flowers.Count==current.Flowers.Count,"Layout dropped flowers");
            for(int view=0;view<8;view++)
            {
                double a=Occlusion(old,view),b=Occlusion(current,view);oldCoverage.Add(a);newCoverage.Add(b);
                lines.Add(string.Join(",",species.Id,seed,view,a.ToString("R",System.Globalization.CultureInfo.InvariantCulture),b.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        Check(newCoverage.Average()<oldCoverage.Average()*.8,"Flower occlusion did not fall by at least 20% across fixed views");
        var state=new GardenState{MigrationCompleted=true,LegacyGrowth=.3f};WeeklyPlants.Migrate(state,"2026-11-04T12:00:00Z",true);
        WeeklyPlants.Active(state).Recipe.MeshVersion=4;var store=new Store{State=state};string oldLegacy=PlantCodec.Encode(state.PlantCollection.Plants[0].Recipe);
        using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig()))
        {
            Check(service.Open(),"Style migration failed");Check(WeeklyPlants.Active(service.Snapshot).Recipe.MeshVersion==PlantGeometry.CurrentMeshVersion&&PlantCodec.Encode(service.Snapshot.PlantCollection.Plants[0].Recipe)==oldLegacy,"Style migration changed legacy plant");
            Check(service.RefreshCycle(null)&&service.BeginSession("synthetic-migration-memory","free-mission:M17",0)&&service.StartSession("synthetic-migration-memory")&&service.CompleteSession("synthetic-migration-memory","중립","synthetic migration note"),"Memory fixture failed");
        }
        WeeklyPlants.Active(store.State).Recipe.MeshVersion=4;
        string beforeUpgrade=StateCodec.Encode(store.State);
        using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig()))
        {
            Check(service.Open(),"Populated style migration failed");
            Check(StateCodec.Encode(service.Snapshot)==beforeUpgrade.Replace("<MeshVersion>4</MeshVersion>","<MeshVersion>"+PlantGeometry.CurrentMeshVersion+"</MeshVersion>"),"Style upgrade changed memory IDs, records, growth, wallet or other saved data");
        }
        store=new Store{State=state,Fail=true};using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig()))
        {Check(!service.Open()&&WeeklyPlants.Active(store.State).Recipe.MeshVersion==4,"Failed style migration overwrote original");}
        timings.Sort();coldTimings.Sort();string summary=checks+" checks; "+(12*seeds)+" plants / 8 views; hidden fraction "+oldCoverage.Average().ToString("F4")+" -> "+newCoverage.Average().ToString("F4")+"; cached mesh p50/p95 "+timings[timings.Count/2].ToString("F2")+"/"+timings[(int)((timings.Count-1)*.95)].ToString("F2")+" ms; cold layout p50/p95 "+coldTimings[coldTimings.Count/2].ToString("F2")+"/"+coldTimings[(int)((coldTimings.Count-1)*.95)].ToString("F2")+" ms; max triangles "+triangles;
        string output=Environment.GetEnvironmentVariable("CAPSTONE_FLOWER_OUTPUT");
        if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllLines(Path.Combine(output,"occlusion.csv"),lines);File.WriteAllText(Path.Combine(output,"report.txt"),summary);}
        Console.WriteLine(summary);
    }
}
