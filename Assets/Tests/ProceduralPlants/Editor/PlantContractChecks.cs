using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;

namespace CapstoneDesign.PlantTests
{
    public static class PlantContractChecks
    {
        static int assertions,maxTriangles;
        sealed class Store : IPlantStateStore
        {
            public PlantGardenState State;
            public bool Fail;
            public PlantGardenState Load() { return State==null?null:PlantCodec.Clone(State); }
            public void Save(PlantGardenState s) { if(Fail) throw new IOException("Synthetic write failure");State=PlantCodec.Clone(s); }
        }
        static void Check(bool condition,string message) { assertions++;if(!condition) throw new Exception(message); }
        static void Reject(Action action,string message)
        {
            bool threw=false;
            try { action(); } catch(ArgumentException) { threw=true; } catch(InvalidOperationException) { threw=true; }
            Check(threw,message);
        }
        static string MeshFingerprint(PlantMeshData mesh)
        {
            using(var ms=new MemoryStream()) using(var w=new BinaryWriter(ms))
            {
                for(int i=0;i<mesh.Vertices.Count;i++) { var v=mesh.Vertices[i];var c=mesh.Colors[i];w.Write(v.X);w.Write(v.Y);w.Write(v.Z);w.Write(c.R);w.Write(c.G);w.Write(c.B); }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
        static string PortableFingerprint(PlantMeshData mesh)
        {
            // Quantized coordinates/colors tolerate Math.Sin/Pow differences between
            // Windows Mono and .NET; exact replay in one runtime is checked separately.
            using(var ms=new MemoryStream()) using(var w=new BinaryWriter(ms))
            {
                for(int i=0;i<mesh.Vertices.Count;i++)
                {
                    var v=mesh.Vertices[i];var c=mesh.Colors[i];
                    foreach(double value in new[]{v.X,v.Y,v.Z,c.R,c.G,c.B}) w.Write((long)Math.Round(value*1e8));
                }
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(ms.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        static void CheckSolidBlade(bool petal,PlantVector direction)
        {
            var mesh=new PlantMeshData(true);
            mesh.Blade(new PlantVector(),direction,.2,.08,.12,PlantColor.Hex("739764"),.8,4,PlantVector.Up,petal);
            var edges=new Dictionary<string,int>();double volume=0;
            Func<PlantVector,string> key=v=>string.Join(",",new[]{v.X,v.Y,v.Z}.Select(x=>Math.Round(x,10).ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
            for(int i=0;i<mesh.Vertices.Count;i+=3)
            {
                var a=mesh.Vertices[i];var b=mesh.Vertices[i+1];var c=mesh.Vertices[i+2];var cross=PlantVector.Cross(b,c);
                volume+=(a.X*cross.X+a.Y*cross.Y+a.Z*cross.Z)/6;
                for(int k=0;k<3;k++)
                {
                    string from=key(mesh.Vertices[i+k]),to=key(mesh.Vertices[i+(k+1)%3]);
                    string edge=string.CompareOrdinal(from,to)<0?from+"|"+to:to+"|"+from;
                    int count;edges.TryGetValue(edge,out count);edges[edge]=count+1;
                }
            }
            Check(mesh.TriangleCount==16&&edges.Values.All(n=>n==2),"refined blade has a closed eight-point outline, with no open back");
            Check(volume>1e-8,"refined blade has physical volume and outward winding");
            Check(mesh.Normals.All(n=>PlantMath.Finite(n.Length)&&Math.Abs(n.Length-1)<1e-9),"refined blade normals remain valid for vertical directions");
        }
        public static string Run()
        {
            assertions=0;maxTriangles=0;
            foreach(bool petal in new[]{false,true}) foreach(var direction in new[]{new PlantVector(1,.3,0),PlantVector.Up,new PlantVector(0,-1,0)}) CheckSolidBlade(petal,direction);
            Check(PlantCatalog.All.Count==12,"twelve species");
            Check(PlantMath.Hash("ab","c")!=PlantMath.Hash("a","bc"),"length-prefixed domain separation");
            Check(PlantGenerator.Create("synthetic-owner","synthetic-instance").BaseSeed=="709641479271863cb51427febff276be63e7cda5c3f0b55230eafa0761286652","independent SHA-256 golden vector");
            Check(PlantMath.Profile(new int[6]).All(x=>x==1.0/6),"zero history prior");
            Reject(()=>PlantMath.Profile(new[]{-1,0,0,0,0,0}),"negative activity counts");
            Reject(()=>PlantGenerator.Create("synthetic","bad",5),"growth goal below minimum");
            Reject(()=>PlantGenerator.Create("synthetic","bad",1001),"growth goal above maximum");
            var longCourse=PlantGenerator.Create("synthetic","long-course",1000);longCourse=PlantGenerator.Advance(longCourse);
            Check(PlantGeometry.Build(longCourse).Vertices.Where(v=>Math.Abs(v.X)<.007&&Math.Abs(v.Z)<.007).Max(v=>v.Y)>=.08,"long targets keep cotyledons supported by the juvenile stem");
            var legacy=PlantGenerator.Create("synthetic-legacy","plant");legacy.GeneratorVersion=1;legacy.MeshVersion=1;legacy.Counts=PlantCatalog.All[0].ExampleCounts();
            for(int i=0;i<12;i++) legacy=PlantGenerator.Advance(legacy);
            Check(PortableFingerprint(PlantGeometry.Build(legacy))=="5c45bedabdf4e1c98670b767c324b233b17344b51b8b0c4fb4eb4ac721237b29","pre-update v1 geometry golden hash at 1e-8 precision");
            string oldXml=PlantCodec.Encode(legacy).Replace("<MeshVersion>1</MeshVersion>","").Replace("<MaxGrowthSteps>12</MaxGrowthSteps>","").Replace("<GrowthMode>NutrientPurchases</GrowthMode>","").Replace("<Terminal>false</Terminal>","");
            var legacyRestored=PlantCodec.Decode<PlantRecipe>(oldXml);PlantGenerator.Validate(legacyRestored);
            Check(legacyRestored.MaxGrowthSteps==12&&legacyRestored.GrowthMode==PlantGrowthMode.NutrientPurchases&&legacyRestored.Mature,"missing new XML fields retain v1 semantics");
            Check(legacyRestored.MeshVersion==1&&MeshFingerprint(PlantGeometry.Build(legacyRestored))==MeshFingerprint(PlantGeometry.Build(legacy)),"old XML retains flat geometry");
            var oldV2=PlantGenerator.Create("synthetic-legacy","plant");oldV2.MeshVersion=1;oldV2.Counts=PlantCatalog.All[0].ExampleCounts();
            for(int i=0;i<12;i++) oldV2=PlantGenerator.Advance(oldV2);
            oldV2=PlantCodec.Decode<PlantRecipe>(PlantCodec.Encode(oldV2).Replace("<MeshVersion>1</MeshVersion>",""));
            Check(oldV2.MeshVersion==1&&PortableFingerprint(PlantGeometry.Build(oldV2))=="c3e1825caf89769a20f617e5efbe3f37fa545ba81a063cf3ff082ef5498b62f0","pre-update v2 growth and geometry are preserved");
            Check(PlantGenerator.Create("synthetic","new-mesh").MeshVersion==2,"new plants use refined geometry");
            var commonA=PlantGenerator.Create("synthetic-a","seed");var commonB=PlantGenerator.Create("synthetic-b","seed");
            commonA.Counts=PlantCatalog.All[0].ExampleCounts();commonB.Counts=PlantCatalog.All[8].ExampleCounts();
            for(int j=0;j<=3;j++)
            {
                Check(MeshFingerprint(PlantGeometry.Build(commonA))==MeshFingerprint(PlantGeometry.Build(commonB)),"identical juvenile mesh across IDs and histories");
                if(j<3) { commonA=PlantGenerator.Advance(commonA);commonB=PlantGenerator.Advance(commonB); }
            }
            var gated=PlantGenerator.Create("synthetic","gated");for(int i=0;i<3;i++) gated=PlantGenerator.Advance(gated);
            string gatedBefore=PlantCodec.Encode(gated);Reject(()=>PlantGenerator.Advance(gated),"six-credit differentiation gate");Check(gatedBefore==PlantCodec.Encode(gated),"failed advance immutable");
            bool anyFruit=false,anyNoFruit=false;
            foreach(var species in PlantCatalog.All)
            {
                Check(PlantGenerator.Candidates(species.ExampleCounts())[0].SpeciesId==species.Id,"species reachable: "+species.Id);
                for(int variant=0;variant<16;variant++)
                {
                    var plant=PlantGenerator.Create("synthetic-owner-"+variant,"synthetic-"+species.Id);
                    plant.Counts=species.ExampleCounts();
                    int leaves=0,heads=0,axes=0;
                    for(int j=0;j<=12;j++)
                    {
                        PlantGenerator.Validate(plant);
                        Check(PlantGenerator.LeafCount(plant)>=leaves&&PlantGenerator.HeadCount(plant)>=heads&&PlantGenerator.AxisCount(plant)>=axes,"organ counts monotonic");
                        leaves=PlantGenerator.LeafCount(plant);heads=PlantGenerator.HeadCount(plant);axes=PlantGenerator.AxisCount(plant);
                        var mesh=PlantGeometry.Build(plant);maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);
                        Check(mesh.TriangleCount<=8000&&mesh.Vertices.All(v=>PlantGenerator.InEnvelope(v)),"geometry budget and bounds");
                        Check(mesh.Vertices.Count==mesh.Colors.Count&&mesh.Vertices.Count==mesh.Normals.Count,"mesh buffers aligned");
                        if(j>=4) Check(plant.SpeciesId==species.Id,"locked species unchanged");
                        if(j<10||species.FruitCap==0) Check(plant.Fruits.Count==0,"fruit eligibility");
                        var restored=PlantCodec.Decode<PlantRecipe>(PlantCodec.Encode(plant));
                        Check(restored.MeshVersion==2,"refined geometry version survives save/reload");
                        Check(PlantCodec.Encode(restored)==PlantCodec.Encode(plant),"recipe roundtrip");
                        if(j==12)
                        {
                            Check(PlantGenerator.LeafCount(plant)==plant.TargetLeaves&&plant.Heads.Count==plant.TargetHeads,"mature targets reached");
                            Check(plant.Heads.All(h=>PlantGenerator.VisibleFlorets(plant,h)==h.Florets),"all florets reached maturity");
                            Check(!plant.Fruits.Any(f=>f.Head==0&&f.Floret==0),"first flower preserved");
                            if(species.FruitCap>0) { anyFruit|=plant.Fruits.Any(f=>f.Present);anyNoFruit|=plant.Fruits.Any(f=>!f.Present); }
                            Check(MeshFingerprint(mesh)==MeshFingerprint(PlantGeometry.Build(restored)),"restored exact mesh");
                            Reject(()=>PlantGenerator.Advance(plant),"mature growth rejected");break;
                        }
                        var beforeLeaves=plant.Leaves.Select(PlantCodec.Encode).ToArray();var beforeHeads=plant.Heads.Select(PlantCodec.Encode).ToArray();var beforeFruit=plant.Fruits.Select(PlantCodec.Encode).ToArray();
                        string before=PlantCodec.Encode(plant);
                        if(j>=4&&variant%2==1) plant.Counts[(j+variant)%6]+=3; // new behavior affects future organs only
                        var next=PlantGenerator.Advance(plant);
                        if(j<4||variant%2==0) Check(before==PlantCodec.Encode(plant),"successful advance does not mutate input");
                        Check(beforeLeaves.SequenceEqual(next.Leaves.Take(beforeLeaves.Length).Select(PlantCodec.Encode)),"old leaves frozen");
                        Check(beforeHeads.SequenceEqual(next.Heads.Take(beforeHeads.Length).Select(PlantCodec.Encode)),"old flowers frozen");
                        Check(beforeFruit.SequenceEqual(next.Fruits.Take(beforeFruit.Length).Select(PlantCodec.Encode)),"fruit success AND failure never rerolled");
                        plant=next;
                    }
                }
            }
            Check(anyFruit&&anyNoFruit,"both fruit evaluation outcomes exercised");
            // Exercise extreme and mixed histories beyond the twelve near-prototype profiles.
            var random=new Random(713);
            for(int sample=0;sample<240;sample++)
            {
                var p=PlantGenerator.Create("synthetic-boundary-"+sample,"boundary");
                if(sample<6) p.Counts[sample]=10000;
                else for(int k=0;k<6;k++) p.Counts[k]=random.Next(0,300);
                for(int j=0;j<12;j++) p=PlantGenerator.Advance(p);
                var mesh=PlantGeometry.Build(p);maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);
                Check(p.Mature&&mesh.TriangleCount<=8000,"extreme or mixed history reaches maturity");
            }
            foreach(int target in new[]{6,7,12,28,48,56,91}) foreach(var s in PlantCatalog.All)
            {
                var p=PlantGenerator.Create("synthetic-goal-"+target,s.Id,target,PlantGrowthMode.MissionCompletions);
                p.Counts=s.ExampleCounts();PlantVector[] stopped=null;
                var flowerPositions=new Dictionary<int,PlantVector>();
                for(int step=1;step<=target;step++)
                {
                    p=PlantGenerator.Advance(p);
                    Check(p.GrowthStep==step&&p.Mature==(step==target),"configured target is exact");
                    Check((p.SpeciesId!=null)==(step>=p.DifferentiationStep),"species lock threshold scales with goal");
                    if(p.SpeciesId==null) continue;
                    Check(p.Heads[0].Terminal&&p.Heads[0].Parent==0,"main axis ends in a flower bud");
                    foreach(var h in p.Heads.Where(h=>h.Terminal))
                    {
                        Check((PlantGenerator.HeadAnchor(p,h)-PlantGenerator.AxisPoint(p,h.Parent,PlantGenerator.AxisVisible(p,h.Parent))).Length<1e-9,"bud follows growing axis tip without floating");
                        Check(PlantGenerator.HeadTip(p,h).Y>=PlantGenerator.HeadAnchor(p,h).Y,"stem cannot project above terminal attachment");
                    }
                    if(p.Growth>=.5)
                    {
                        Check(PlantGenerator.AxisCount(p)==p.TargetAxes,"all structural axes exist before flowering");
                        var tips=Enumerable.Range(0,p.TargetAxes).Select(i=>PlantGenerator.AxisPoint(p,i,PlantGenerator.AxisVisible(p,i))).ToArray();
                        if(stopped==null) stopped=tips;
                        Check(tips.Select((tip,i)=>(tip-stopped[i]).Length).All(d=>d<1e-12),"no axis elongation after flowering begins");
                        Check(tips.All(t=>t.Y<=PlantGenerator.HeadTip(p,p.Heads[0]).Y),"no structural axis above the apical flower");
                        for(int h=0;h<p.Heads.Count;h++)
                        {
                            var tip=PlantGenerator.HeadTip(p,p.Heads[h]);
                            if(flowerPositions.ContainsKey(h)) Check((tip-flowerPositions[h]).Length<1e-12,"opened flower position stays fixed");
                            else flowerPositions.Add(h,tip);
                        }
                    }
                    var mesh=PlantGeometry.Build(p);maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);
                    Check(mesh.Vertices.All(v=>PlantGenerator.InEnvelope(v)),"rescaled schedule keeps every vertex inside bounds");
                    var restored=PlantCodec.Clone(p);Check(restored.MaxGrowthSteps==target&&restored.GrowthStep==step,"target survives save/reload");
                }
            }
            var a=PlantGenerator.Advance(commonA);var b=PlantGenerator.Advance(PlantCodec.Clone(commonA));
            Check(PlantCodec.Encode(a)==PlantCodec.Encode(b),"deterministic recipe");
            var unknown=PlantCodec.Clone(a);unknown.GeneratorVersion=999;Reject(()=>PlantGenerator.Advance(unknown),"future version rejected");
            unknown=PlantCodec.Clone(a);unknown.MeshVersion=999;Reject(()=>PlantGeometry.Build(unknown),"unknown mesh version rejected before rendering");
            var broken=PlantCodec.Clone(a);broken.Height=double.NaN;Reject(()=>PlantGenerator.Validate(broken),"nonfinite target rejected");
            var store=new Store();var service=new PlantGrowthService(store,120);
            Check(service.StartPlant("start","synthetic","plant-1"),"start plant");
            store.Fail=true;
            Check(!service.CompleteActivity("failed-credit","plant-1","2026-01-01","failed",GrowthCategory.Body)&&service.Snapshot.Credits.Count==0&&service.Snapshot.Nutrients==120,"failed activity save does not credit or reward");
            store.Fail=false;
            for(int i=0;i<4;i++) Check(service.CompleteActivity("c"+i,"plant-1","2026-01-01","mission-"+i,GrowthCategory.Breath),"manual activity");
            Check(service.Snapshot.Credits.Count==3&&service.Snapshot.Nutrients==140,"daily credit cap independent of rewards");
            Check(service.CompleteActivity("duplicate","plant-1","2026-01-01","mission-0",GrowthCategory.Breath)&&service.Snapshot.Nutrients==140,"per cycle/mission deduplication");
            Check(!service.CompleteActivity("not-done","plant-1","2026-01-02","incomplete",GrowthCategory.Breath,true,false),"timer alone does not count");
            for(int i=0;i<3;i++) Check(service.CompleteActivity("review"+i,"plant-1","2026-01-02","review-"+i,GrowthCategory.Breath,false),"review credits");
            Check(service.Snapshot.Credits.Count==6&&service.Snapshot.Nutrients==140,"review gives credit without reward");
            store.Fail=true;string stateBefore=PlantCodec.Encode(service.Snapshot);
            Check(!service.Grow("g1","plant-1")&&stateBefore==PlantCodec.Encode(service.Snapshot),"failed persistence atomic");
            store.Fail=false;Check(service.Grow("g1","plant-1"),"growth retry");
            service=new PlantGrowthService(store);
            Check(service.Grow("g1","plant-1")&&service.Snapshot.Nutrients==130&&service.Snapshot.Plants[0].GrowthStep==1,"restart duplicate debits once");
            Check(!service.Grow("g1","another-plant"),"request payload conflict");
            var concurrent=Enumerable.Range(0,8).Select(i=>System.Threading.Tasks.Task.Run(()=>service.Grow("g1","plant-1"))).ToArray();
            System.Threading.Tasks.Task.WaitAll(concurrent);
            Check(concurrent.All(t=>t.Result)&&service.Snapshot.Nutrients==130,"concurrent retries apply once");
            for(int i=2;i<=12;i++) Check(service.Grow("g"+i,"plant-1"),"growth transaction "+i+": "+service.LastError);
            Check(!service.Grow("g13","plant-1")&&service.Snapshot.Nutrients==20,"no charge after maturity");
            string matureBefore=PlantCodec.Encode(service.Snapshot.Plants[0]);
            Check(service.CompleteActivity("late","plant-1","2026-01-03","late",GrowthCategory.Movement,false),"late completion accepted");
            Check(matureBefore==PlantCodec.Encode(service.Snapshot.Plants[0]),"mature plant preserved");
            Check(service.StartPlant("start2","synthetic","plant-2"),"new seed preserves mature plant");
            Check(service.Grow("g1","plant-1")&&service.Snapshot.Plants[1].GrowthStep==0,"old retry cannot grow new plant");
            var detached=service.Snapshot;detached.Plants[0].Counts[0]=999;
            Check(service.Snapshot.Plants[0].Counts[0]!=999,"snapshot detached");
            Reject(()=>PlantGrowthService.Validate(detached),"corrupt counts rejected");
            foreach(int goal in new[]{6,12,28,48,56})
            {
                var missionStore=new Store();var missions=new PlantGrowthService(missionStore);
                Check(missions.StartPlant("start","synthetic-course","course",goal,PlantGrowthMode.MissionCompletions),"start configured mission course");
                Check(!missions.StartPlant("start","synthetic-course","course",goal+1,PlantGrowthMode.MissionCompletions),"goal is part of request identity");
                for(int n=1;n<=goal;n++)
                {
                    string day=new DateTime(2026,1,1).AddDays(n-1).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
                    string request="course-"+n;
                    if(n==10)
                    {
                        missionStore.Fail=true;string before=PlantCodec.Encode(missions.Snapshot);
                        Check(!missions.CompleteActivity(request,"course",day,"mission-"+n,GrowthCategory.Breath),"automatic growth save failure surfaced");
                        Check(before==PlantCodec.Encode(missions.Snapshot),"credit, reward, species and automatic growth roll back together");missionStore.Fail=false;
                    }
                    Check(missions.CompleteActivity(request,"course",day,"mission-"+n,GrowthCategory.Breath),"one completion grows course plant");
                    var snapshot=missions.Snapshot;var course=snapshot.Plants[0];
                    Check(course.GrowthStep==PlantGenerator.MissionGrowthStep(course)&&snapshot.Nutrients==n*5,"automatic growth never spends nutrients");
                    Check(course.Mature==(n==goal),"course matures at target completion count");
                    Check(missions.CompleteActivity(request,"course",day,"mission-"+n,GrowthCategory.Breath)&&PlantCodec.Encode(snapshot)==PlantCodec.Encode(missions.Snapshot),"duplicate mission never grows twice");
                    Check(!missions.Grow("purchase-"+n,"course")&&PlantCodec.Encode(snapshot)==PlantCodec.Encode(missions.Snapshot),"mission mode rejects manual purchase without charge");
                    missions=new PlantGrowthService(missionStore);
                }
            }
            var capStore=new Store();var capped=new PlantGrowthService(capStore);
            capped.StartPlant("start","synthetic","cap",28,PlantGrowthMode.MissionCompletions);
            for(int n=0;n<4;n++) Check(capped.CompleteActivity("cap-"+n,"cap","2026-02-01","m"+n,GrowthCategory.Body,false),"review eligibility for automatic mode");
            Check(capped.Snapshot.Plants[0].GrowthStep==3&&capped.Snapshot.Credits.Count==3&&capped.Snapshot.Nutrients==0,"daily credit cap also caps auto-growth while reviews remain free");
            var purchased=new PlantGrowthService(new Store(),280);
            purchased.StartPlant("start","synthetic","purchased",28,PlantGrowthMode.NutrientPurchases);
            for(int n=0;n<6;n++) purchased.CompleteActivity("c"+n,"purchased",n<3?"2026-02-01":"2026-02-02","m"+n,GrowthCategory.Breath,false);
            Check(purchased.Snapshot.Plants[0].GrowthStep==0,"manual mode does not auto-grow after activities");
            for(int n=1;n<=28;n++) Check(purchased.Grow("grow-"+n,"purchased"),"configured manual purchases support steps beyond twelve");
            Check(purchased.Snapshot.Nutrients==0&&purchased.Snapshot.Plants[0].Mature,"twenty-eight manual purchases debit 280 nutrients");
            string dir=Path.Combine(Path.GetTempPath(),"synthetic-plant-tests-"+Guid.NewGuid().ToString("N"));
            try
            {
                var xml=new PlantXmlStore(Path.Combine(dir,"synthetic.xml"));xml.Save(service.Snapshot);xml.Save(service.Snapshot);
                Check(PlantCodec.Encode(xml.Load())==PlantCodec.Encode(service.Snapshot),"atomic XML replacement roundtrip");
            }
            finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
            return assertions+" assertions passed; baseline + 240 boundary profiles + 7 schedules x 12 species + mission transactions; max "+maxTriangles+" triangles.";
        }
    }
}
