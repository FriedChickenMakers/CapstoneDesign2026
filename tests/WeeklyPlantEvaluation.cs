using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CapstoneDesign.Plants;

// Reproducible engineering benchmark, deliberately separate from assertion counts.
class WeeklyPlantEvaluation
{
    sealed class Sample
    {
        public int Style,Triangles;
        public double Milliseconds;
        public long Bytes;
    }
    static double Percentile(IEnumerable<double> values,double quantile)
    {var sorted=values.OrderBy(x=>x).ToArray();return sorted.Length==0?0:sorted[(int)Math.Ceiling((sorted.Length-1)*quantile)];}
    static PlantMemoryLeaf[] Memories(int count)=>Enumerable.Range(0,count).Select(i=>new PlantMemoryLeaf {OrganId="synthetic-memory-"+i,Slot=i,Linked=true}).ToArray();
    static string ShapeHash(PlantMeshData mesh)
    {
        var buffer=new StringBuilder();foreach(var v in mesh.Vertices)buffer.Append(v.X.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(v.Y.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(v.Z.ToString("R",CultureInfo.InvariantCulture)).Append(';');
        return PlantMath.Hash(buffer.ToString());
    }
    static void Main()
    {
        string output=Environment.GetEnvironmentVariable("CAPSTONE_PLANT_EVALUATION_OUT");
        if(string.IsNullOrWhiteSpace(output))throw new ArgumentException("Explicit ignored artifact output is required.");
        int seeds=int.TryParse(Environment.GetEnvironmentVariable("CAPSTONE_PLANT_SEEDS"),out var n)?n:100;
        if(seeds<1||seeds>1000)throw new ArgumentOutOfRangeException("seed count");
        Directory.CreateDirectory(output);var samples=new List<Sample>();var generation=new List<double>();
        var hashes=new Dictionary<string,HashSet<string>>();var features=new Dictionary<string,List<double[]>>();int failures=0,attempts=0;
        var memories=Memories(21);
        // Warm JIT and serializers before recording timings.
        var warm=PlantGenerator.Create("synthetic-warmup","warmup",21);warm.Counts=PlantCatalog.All[0].ExampleCounts();
        while(!warm.Mature)warm=PlantGenerator.Advance(warm);
        foreach(int style in new[]{2,3,4}){warm.MeshVersion=style;PlantMemoryGeometry.Build(warm,memories);}
        using(var csv=new StreamWriter(Path.Combine(output,"samples.csv"),false,new UTF8Encoding(false)))
        {
            csv.WriteLine("species,seed,step,style,visible_memories,triangles,mesh_ms,mesh_allocated_bytes,status");
            foreach(var species in PlantCatalog.All)for(int seed=0;seed<seeds;seed++)
            {
                var p=PlantGenerator.Create("synthetic-evaluation-v1",species.Id+"/"+seed,21,PlantGrowthMode.MissionCompletions);
                // Fixed profiles isolate geometry styles/seed diversity. They are
                // synthetic art fixtures, not the app's activity receipts.
                p.Counts=species.ExampleCounts();p.MeshVersion=4;
                for(int step=0;step<=21;step++)
                {
                    if(step>0)
                    {
                        var timer=Stopwatch.StartNew();
                        try{p=PlantGenerator.Advance(p);generation.Add(timer.Elapsed.TotalMilliseconds);}
                        catch(Exception ex){failures++;csv.WriteLine(species.Id+","+seed+","+step+",4,21,0,0,0,generation-"+ex.GetType().Name);break;}
                    }
                    foreach(int style in new[]{2,3,4})
                    {
                        attempts++;p.MeshVersion=style;
                        try
                        {
                            long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
                            var mesh=PlantMemoryGeometry.Build(p,memories);timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                            samples.Add(new Sample{Style=style,Triangles=mesh.TriangleCount,Milliseconds=timer.Elapsed.TotalMilliseconds,Bytes=allocated});
                            csv.WriteLine(string.Join(",",species.Id,seed,step,style,21,mesh.TriangleCount,timer.Elapsed.TotalMilliseconds.ToString("F6",CultureInfo.InvariantCulture),allocated,"pass"));
                            if(step==21)
                            {
                                string key=species.Id+"/mesh"+style;
                                if(!hashes.ContainsKey(key)){hashes[key]=new HashSet<string>();features[key]=new List<double[]>();}
                                hashes[key].Add(ShapeHash(mesh));
                                features[key].Add(new[]{p.Height,p.Radius*20,p.TargetAxes/8.0,p.TargetLeaves/24.0,p.TargetHeads/8.0,p.Spread/45.0});
                            }
                        }
                        catch(Exception ex){failures++;csv.WriteLine(species.Id+","+seed+","+step+","+style+",21,0,0,0,mesh-"+ex.GetType().Name);}
                    }
                    p.MeshVersion=4;
                }
            }
        }
        var stress=new List<object>();
        foreach(int total in new[]{0,3,21,42,210})
        {
            var inputs=Memories(total);var timings=new List<double>();int maxTriangles=0;
            for(int repeat=0;repeat<30;repeat++)for(int page=0;page<Math.Max(1,(total+20)/21);page++)
            {var timer=Stopwatch.StartNew();var mesh=PlantMemoryGeometry.Build(warm,inputs,page);timings.Add(timer.Elapsed.TotalMilliseconds);maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);}
            stress.Add(new{totalRecords=total,visibleLimit=21,p50Ms=Percentile(timings,.5),p95Ms=Percentile(timings,.95),maxTriangles});
        }
        var diversity=hashes.Select(pair=>
        {
            var vectors=features[pair.Key];double sum=0;int pairs=0;
            for(int i=0;i<vectors.Count;i++)for(int j=i+1;j<vectors.Count;j++)
            {sum+=Math.Sqrt(vectors[i].Zip(vectors[j],(a,b)=>(a-b)*(a-b)).Sum());pairs++;}
            return new{group=pair.Key,independentPlants=vectors.Count,distinctExactMeshes=pair.Value.Count,
                exactDuplicateRate=1.0-pair.Value.Count/(double)vectors.Count,meanStructuralDistance=pairs==0?0:sum/pairs};
        }).ToArray();
        var report=new {
            protocol="weekly-plant-engineering-v1",runtime=RuntimeInformation.FrameworkDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),
            seedCountPerSpecies=seeds,speciesCount=12,independentPlantsPerStyle=12*seeds,expectedStageObservationsPerStyle=12*seeds*22,
            attempts,failures,passed=samples.Count,visibleMemories=21,
            profile="Fixed species example counts; synthetic geometry fixtures, not activity growth receipts.",
            timing="Managed CPU generator and mesh only; not Unity frame rate, Android performance, battery, user preference, or clinical evidence.",
            diversityCaveat="Exact mesh hashes and normalized structural distances do not prove perceptual uniqueness. Stages/views are not independent plants.",
            lSystem="Deferred by user; no external baseline was executed.",
            generatorP50Ms=Percentile(generation,.5),generatorP95Ms=Percentile(generation,.95),
            styles=samples.GroupBy(s=>s.Style).Select(g=>new{meshVersion=g.Key,observations=g.Count(),p50Ms=Percentile(g.Select(s=>s.Milliseconds),.5),p95Ms=Percentile(g.Select(s=>s.Milliseconds),.95),p95AllocatedBytes=Percentile(g.Select(s=>(double)s.Bytes),.95),maxTriangles=g.Max(s=>s.Triangles)}).ToArray(),
            memoryStress=stress,diversity
        };
        File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(samples.Count+" mesh observations; "+(12*seeds)+" independent plants/style; "+failures+" failures. Report: "+Path.Combine(output,"report.json"));
        if(failures>0)Environment.Exit(1);
    }
}
