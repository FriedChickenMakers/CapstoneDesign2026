using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;

class PlantVolumeTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static double Dot(PlantVector a,PlantVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    static string Point(PlantVector p)=>p.X.ToString("R")+","+p.Y.ToString("R")+","+p.Z.ToString("R");
    static void ClosedVolume(PlantMeshData mesh)
    {
        var edges=new Dictionary<string,int>();double volume=0;
        for(int i=0;i<mesh.Vertices.Count;i+=3)
        {
            var a=mesh.Vertices[i];var b=mesh.Vertices[i+1];var c=mesh.Vertices[i+2];
            volume+=Dot(a,PlantVector.Cross(b,c))/6;
            Check(Dot(mesh.Normals[i],PlantVector.Cross(b-a,c-a).Normalized)>.99999,"Invalid face normal");
            for(int k=0;k<3;k++)
            {
                string first=Point(mesh.Vertices[i+k]),second=Point(mesh.Vertices[i+(k+1)%3]);
                bool order=string.CompareOrdinal(first,second)<0;string key=order?first+"/"+second:second+"/"+first;
                edges[key]=(edges.ContainsKey(key)?edges[key]:0)+(order?1:-1);
            }
        }
        Check(edges.Values.All(n=>n==0),"Open or inconsistently wound blade");
        Check(volume>1e-6,"Blade has no positive enclosed volume");
    }
    sealed class Clock:IClock{public DateTimeOffset UtcNow=>DateTimeOffset.Parse("2026-11-04T12:00:00Z");public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;}
    sealed class Store:IStateStore
    {
        public GardenState State;public bool Fail;
        public GardenState Load()=>StateCodec.Clone(State);
        public void Save(GardenState s){if(Fail)throw new IOException("Synthetic migration failure");State=StateCodec.Clone(s);}
    }
    static void Main()
    {
        foreach(bool petal in new[]{false,true})
        {
            var old=new PlantMeshData(true);var current=new PlantMeshData(true){Volumetric=true};
            foreach(var mesh in new[]{old,current})mesh.Blade(new PlantVector(),new PlantVector(1,0,0),.2,.1,0,PlantColor.Hex("AABBCC"),petal:petal);
            double oldDepth=old.Vertices.Max(v=>v.Y)-old.Vertices.Min(v=>v.Y),depth=current.Vertices.Max(v=>v.Y)-current.Vertices.Min(v=>v.Y);
            Check(depth>oldDepth*1.6,"Insufficient side silhouette depth");ClosedVolume(current);
        }
        int max=0;
        foreach(var species in PlantCatalog.All)
        {
            var p=PlantGenerator.Create("synthetic-volume-contract",species.Id,21,PlantGrowthMode.MissionCompletions);p.Counts=species.ExampleCounts();p.MeshVersion=6;
            var memories=Enumerable.Range(0,21).Select(i=>new PlantMemoryLeaf{Slot=i,OrganId="synthetic-volume-memory-"+i,Linked=i%2==0}).ToArray();
            for(int step=0;step<=21;step++)
            {
                if(step>0)p=PlantGenerator.Advance(p);
                string saved=PlantCodec.Encode(p);var mesh=PlantMemoryGeometry.Build(p,memories);max=Math.Max(max,mesh.TriangleCount);
                Check(mesh.Organs.Count==21&&mesh.Organs.Select(o=>o.OrganId).SequenceEqual(memories.Select(m=>m.OrganId)),"Lost memory organ identity");
                Check(mesh.Organs.All(o=>o.FirstTriangle>=0&&o.FirstTriangle+o.TriangleCount<=mesh.TriangleCount),"Broken picking triangle range");
                Check(mesh.Normals.All(n=>Math.Abs(n.Length-1)<1e-8),"Invalid transformed normals");
                Check(saved==PlantCodec.Encode(p),"Rendering changed save");
            }
            var five=PlantCodec.Clone(p);five.MeshVersion=5;
            PlantFlowerLayout.ClearCache();string original=PlantCodec.Encode(PlantFlowerLayout.Plan(five));
            PlantFlowerLayout.Plan(p);
            Check(PlantCodec.Encode(PlantFlowerLayout.Plan(five))==original,"Versioned layout cache contaminated old style");
            Check(PlantGeometry.Build(p).Flowers.Count==PlantGeometry.Build(five).Flowers.Count,"Volume removed flowers");
        }
        var state=new GardenState{MigrationCompleted=true};WeeklyPlants.Migrate(state,"2026-11-04T12:00:00Z",false);
        var store=new Store{State=state};
        using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig()))
        {Check(service.Open()&&service.RefreshCycle(null)&&service.BeginSession("synthetic-volume-session","free-mission:M17",0)&&service.StartSession("synthetic-volume-session")&&service.CompleteSession("synthetic-volume-session","중립","synthetic volume note"),"Migration fixture failed");}
        state=StateCodec.Clone(store.State);WeeklyPlants.Active(state).Recipe.MeshVersion=5;store.State=state;string before=StateCodec.Encode(state);
        using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig()))
        {Check(service.Open(),"v5 upgrade failed");Check(StateCodec.Encode(service.Snapshot)==before.Replace("<MeshVersion>5</MeshVersion>","<MeshVersion>6</MeshVersion>"),"Upgrade changed nonvisual data");Check(service.Open(),"Repeated load failed");}
        store=new Store{State=state,Fail=true};using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig()))
        {Check(!service.Open()&&StateCodec.Encode(store.State)==before,"Failed upgrade destroyed old save");}
        Console.WriteLine(checks+" volume checks; all 12 species x 22 stages with 21 memory leaves; max "+max+" triangles");
    }
}
