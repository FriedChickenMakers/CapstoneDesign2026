using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CapstoneDesign.Plants;

// Geometry-only integration checks: no Unity, garden state, user files or weekly policy.
static class PlantGeometryV6Tests
{
    static int checks,maxTriangles;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Reject(Action action,string message)
    {
        bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}
        Check(rejected,message);
    }
    static double Dot(PlantVector a,PlantVector b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
    static string Point(PlantVector p)=>string.Join(",",new[]{p.X,p.Y,p.Z}.Select(x=>x.ToString("R",CultureInfo.InvariantCulture)));
    static string Fingerprint(PlantMeshData mesh)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            for(int i=0;i<mesh.Vertices.Count;i++)
            {
                var v=mesh.Vertices[i];var n=mesh.Normals[i];var c=mesh.Colors[i];
                foreach(double value in new[]{v.X,v.Y,v.Z,n.X,n.Y,n.Z,c.R,c.G,c.B})writer.Write(value);
            }
            using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }
    }
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
    static void CheckVolume()
    {
        foreach(bool petal in new[]{false,true})
        foreach(var direction in new[]{new PlantVector(1,0,0),PlantVector.Up,new PlantVector(0,-1,0)})
        {
            var old=new PlantMeshData(true);var current=new PlantMeshData(true){Volumetric=true};
            foreach(var mesh in new[]{old,current})mesh.Blade(new PlantVector(),direction,.2,.1,0,PlantColor.Hex("AABBCC"),petal:petal);
            if(direction.X==1)
                Check(current.Vertices.Max(v=>v.Y)-current.Vertices.Min(v=>v.Y)>(old.Vertices.Max(v=>v.Y)-old.Vertices.Min(v=>v.Y))*1.6,"Insufficient side silhouette depth");
            ClosedVolume(current);
        }
    }
    static string Poses(IEnumerable<FlowerPose> poses)=>string.Join("|",poses.Select(p=>PlantCodec.Encode(p)));
    static void CheckSpeciesAndMemories()
    {
        foreach(var species in PlantCatalog.All)
        {
            var p=PlantGenerator.Create("synthetic-combined-v6",species.Id,21,PlantGrowthMode.MissionCompletions);
            p.Counts=species.ExampleCounts();p.MeshVersion=PlantGeometry.CurrentMeshVersion;
            var memories=Enumerable.Range(0,43).Select(i=>new PlantMemoryLeaf{Slot=i,OrganId="synthetic-organ-"+i,Linked=i%2==0}).ToArray();
            FlowerPose[] prior=null;string[] attachment=null;
            for(int step=0;step<=21;step++)
            {
                if(step>0)p=PlantGenerator.Advance(p);
                string saved=PlantCodec.Encode(p);
                var mesh=PlantMemoryGeometry.Build(p,memories);
                maxTriangles=Math.Max(maxTriangles,mesh.TriangleCount);
                Check(mesh.Organs.Count==21&&mesh.Organs.Select(o=>o.OrganId).SequenceEqual(memories.Take(21).Select(m=>m.OrganId)),"Lost memory organ identity");
                Check(mesh.Organs.All(o=>o.FirstTriangle>=0&&o.TriangleCount>0&&o.FirstTriangle+o.TriangleCount<=mesh.TriangleCount),"Broken memory picking triangle range");
                Check(mesh.Organs.Select(o=>o.Selectable).SequenceEqual(memories.Take(21).Select(m=>m.Linked)),"Deleted memory remained selectable");
                Check(mesh.Vertices.All(v=>PlantGenerator.InEnvelope(v))&&mesh.TriangleCount<=8000,"Geometry exceeds plant envelope/budget");
                Check(mesh.Normals.All(n=>Math.Abs(n.Length-1)<1e-8),"Invalid transformed normals");
                Check(saved==PlantCodec.Encode(p),"Rendering mutated recipe");
                var current=mesh.Organs.Select(o=>Point(o.Center)).ToArray();
                if(attachment!=null)Check(current.SequenceEqual(attachment),"Growth moved existing memory attachments");
                attachment=current;
                var restored=PlantCodec.Decode<PlantRecipe>(saved);
                Check(Fingerprint(mesh)==Fingerprint(PlantMemoryGeometry.Build(restored,memories)),"Reload changed mesh");
                var poses=PlantFlowerLayout.Plan(p);
                if(prior!=null)Check(Poses(prior)==Poses(poses.Take(prior.Length)),"Growth moved existing flower poses");
                prior=poses;
            }
            var none=PlantMemoryGeometry.Build(p,null);
            Check(Fingerprint(none)==Fingerprint(PlantGeometry.Build(p))&&none.Organs.Count==0,"Optional memories changed ordinary plant geometry");
            var next=PlantMemoryGeometry.Build(p,memories,1);
            Check(next.Organs.Select(o=>o.OrganId).SequenceEqual(memories.Skip(21).Take(21).Select(m=>m.OrganId)),"Second memory page mixed IDs");
            var last=PlantMemoryGeometry.Build(p,memories,2);
            Check(last.Organs.Count==1&&last.Organs[0].OrganId==memories[42].OrganId,"Last memory page was lost");
            Check(PlantMemoryGeometry.Build(p,memories,3).Organs.Count==0,"Empty page retained stale organs");
            var before=PlantMemoryGeometry.Build(p,memories);
            memories[0].Linked=false;
            var removed=PlantMemoryGeometry.Build(p,memories);
            Check(removed.Organs.Count==before.Organs.Count&&!removed.Organs[0].Selectable,"Deleting memory removed its decoration or retained picking");
            Check(removed.Vertices.SequenceEqual(before.Vertices)&&removed.Normals.SequenceEqual(before.Normals),"Deleting memory moved existing geometry");
            Reject(()=>PlantMemoryGeometry.Build(p,memories,-1),"Negative page accepted");
            Reject(()=>PlantMemoryGeometry.Build(p,new[]{memories[0],memories[0]}),"Duplicate memory slots accepted");
            string original=Poses(PlantFlowerLayout.Plan(p));
            var caller=PlantFlowerLayout.Plan(p);caller[0].Center=new PlantVector(9,9,9);
            Check(Poses(PlantFlowerLayout.Plan(p))==original,"Caller contaminated cached flower layout");
            var five=PlantCodec.Clone(p);five.MeshVersion=5;
            string old=Poses(PlantFlowerLayout.Plan(five));PlantFlowerLayout.Plan(p);
            Check(Poses(PlantFlowerLayout.Plan(five))==old,"New layout contaminated saved v5 style");
            PlantFlowerLayout.ClearCache();
            Check(Poses(PlantFlowerLayout.Plan(p))==original,"Cache eviction changed saved layout");
            Check(PlantGeometry.Build(p).Flowers.Count==PlantGeometry.Build(five).Flowers.Count,"v6 removed flowers");
        }
    }
    public static string Run()
    {
        checks=0;maxTriangles=0;CheckVolume();CheckSpeciesAndMemories();
        return checks+" v6 geometry checks; 12 species x 22 stages, 43 memories / 3 pages; maximum "+maxTriangles+" triangles.";
    }
}
