using System;
using System.Collections.Generic;
using System.Linq;

namespace CapstoneDesign.Plants
{
    // Presentation inputs contain identifiers only; never emotion text.
    public sealed class PlantMemoryLeaf
    {
        public string OrganId;
        public int Slot;
        public bool Linked;
    }
    public sealed class PlantOrganGeometry
    {
        public string OrganId;
        public int FirstTriangle, TriangleCount;
        public PlantVector Center;
        public bool Selectable;
    }
    public static class PlantMemoryGeometry
    {
        public const int PageSize=21;
        public static PlantMeshData Build(PlantRecipe recipe,IEnumerable<PlantMemoryLeaf> memories,int page=0)
        {
            if(page<0)throw new ArgumentOutOfRangeException(nameof(page));
            var mesh=PlantGeometry.Build(recipe);
            var visible=(memories??Enumerable.Empty<PlantMemoryLeaf>()).Where(m=>m.Slot/PageSize==page).OrderBy(m=>m.Slot).ToArray();
            if(visible.Length>PageSize || visible.Select(m=>m.Slot).Distinct().Count()!=visible.Length)throw new ArgumentException("Invalid memory slots.");
            if(visible.Length==0)return mesh;
            var stem=PlantColor.Hex("56794B");
            // A separate persistent basal sprig keeps early memories at the same
            // attachment even when the main stem differentiates or stops growing.
            var root=new PlantVector(0,.012,0);
            double top=.08+(visible.Max(m=>m.Slot%PageSize)/3)*.048;
            mesh.Tube(root,new PlantVector(0,top,0),.008,.005,stem);
            foreach(var leaf in visible)
            {
                int index=leaf.Slot%PageSize;
                var at=new PlantVector(0,.08+(index/3)*.048,0);
                var direction=PlantVector.Direction(index*137.507764,.25);
                var start=at+direction*.035;double length=.17;
                int first=mesh.TriangleCount;
                mesh.Tube(at,start,.004,.0025,stem,5);
                mesh.Blade(start,direction,length,.085,0,PlantColor.Hex(leaf.Linked?"B5D879":"739764"));
                AddMemoryVein(mesh,start,direction,length,.085,PlantColor.Hex(leaf.Linked?"F0E8A6":"658553"));
                mesh.Organs.Add(new PlantOrganGeometry {OrganId=leaf.OrganId,FirstTriangle=first,TriangleCount=mesh.TriangleCount-first,
                    Center=start+direction*(length*.52),Selectable=leaf.Linked});
            }
            if(mesh.TriangleCount>8000 || mesh.Vertices.Any(v=>!PlantGenerator.InEnvelope(v)))throw new InvalidOperationException("Plant and visible memories exceed geometry budget.");
            return mesh;
        }

        // A narrow vein distinguishes record-bearing leaves without enlarging
        // their footprint. Keep identical positions after unlinking/deletion;
        // only its colour and selectability become quiet decoration.
        static void AddMemoryVein(PlantMeshData mesh,PlantVector at,PlantVector direction,double length,double width,PlantColor color)
        {
            var forward=direction.Normalized;
            var side=PlantVector.Cross(forward,PlantVector.Up).Normalized;
            var normal=PlantVector.Cross(side,forward).Normalized;
            PlantVector a,b,c;
            if(mesh.Volumetric)
            {
                double relief=Math.Min(length*.23,width*.52),bend=length*.10;
                a=at+forward*(length*.30)+normal*(relief*.82+bend*.09);
                c=at+forward*(length*.68)+normal*(relief+bend*.4624);
                b=(a+c)*.5;
            }
            else if(mesh.Refined)
            {
                double relief=Math.Min(length*.12,width*.28);
                a=at+forward*(length*.28)+normal*(relief*.28/.49);
                b=at+forward*(length*.49)+normal*relief;
                c=at+forward*(length*.72)+normal*(relief*.28/.51);
            }
            else
            {
                normal=PlantVector.Up;
                a=at+forward*(length*.25)+normal*(length*.08*Math.Sin(Math.PI*.25));
                b=at+forward*(length*.50)+normal*(length*.08);
                c=at+forward*(length*.75)+normal*(length*.08*Math.Sin(Math.PI*.75));
            }
            var lift=normal*.0017;
            mesh.Tube(a+lift,b+lift,.0018,.002,color,4);
            mesh.Tube(b+lift,c+lift,.002,.0014,color,4);
        }
    }
}
