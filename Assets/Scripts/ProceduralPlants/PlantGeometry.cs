using System;
using System.Collections.Generic;
using System.Linq;

namespace CapstoneDesign.Plants
{
    // Engine-independent mesh buffers permit the exact geometry to be checked without Unity.
    public sealed class PlantMeshData
    {
        public bool Refined { get; private set; }
        public PlantMeshData(bool refined=false) { Refined=refined; }
        public readonly List<PlantVector> Vertices=new List<PlantVector>();
        public readonly List<PlantVector> Normals=new List<PlantVector>();
        public readonly List<PlantColor> Colors=new List<PlantColor>();
        public int TriangleCount { get { return Vertices.Count/3; } }
        public void Triangle(PlantVector a,PlantVector b,PlantVector c,PlantColor color)
        {
            if((PlantVector.Cross(b-a,c-a)).Length<1e-12) return;
            var n=PlantVector.Cross(b-a,c-a).Normalized;
            Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);
            for(int i=0;i<3;i++) { Normals.Add(n);Colors.Add(color); }
        }
        public void Tube(PlantVector a,PlantVector b,double r0,double r1,PlantColor color,int sides=6)
        {
            var direction=(b-a).Normalized; PlantVector u,v;Basis(direction,out u,out v);
            for(int i=0;i<sides;i++)
            {
                double t=2*Math.PI*i/sides,t1=2*Math.PI*(i+1)/sides;
                var x=u*Math.Cos(t)+v*Math.Sin(t);var y=u*Math.Cos(t1)+v*Math.Sin(t1);
                if(Refined)
                { Triangle(a+x*r0,b+y*r1,b+x*r1,color);Triangle(a+x*r0,a+y*r0,b+y*r1,color); }
                else
                { Triangle(a+x*r0,b+x*r1,b+y*r1,color);Triangle(a+x*r0,b+y*r1,a+y*r0,color); }
                Triangle(a,a+y*r0,a+x*r0,color);Triangle(b,b+x*r1,b+y*r1,color);
            }
        }
        public static void Basis(PlantVector n,out PlantVector u,out PlantVector v)
        {
            u=PlantVector.Cross(n,Math.Abs(n.Y)<.9?PlantVector.Up:new PlantVector(1,0,0)).Normalized;
            v=PlantVector.Cross(n,u).Normalized;
        }
        public void Orb(PlantVector at,PlantVector radii,PlantColor color,int sides=8,int rings=3)
        {
            for(int j=0;j<rings;j++) for(int i=0;i<sides;i++)
            {
                var a=SpherePoint(at,radii,i,j,sides,rings);var b=SpherePoint(at,radii,i+1,j,sides,rings);
                var c=SpherePoint(at,radii,i,j+1,sides,rings);var d=SpherePoint(at,radii,i+1,j+1,sides,rings);
                if(Refined) { Triangle(a,b,c,color);Triangle(b,d,c,color); }
                else { Triangle(a,c,b,color);Triangle(b,c,d,color); }
            }
        }
        static PlantVector SpherePoint(PlantVector c,PlantVector r,int i,int j,int sides,int rings)
        {
            double a=2*Math.PI*i/sides,b=Math.PI*j/rings;
            return c+new PlantVector(r.X*Math.Sin(b)*Math.Cos(a),r.Y*Math.Cos(b),r.Z*Math.Sin(b)*Math.Sin(a));
        }
        public void Blade(PlantVector at,PlantVector direction,double length,double width,double edge,PlantColor color,double exponent=.8,int segments=4,PlantVector? planeNormal=null,bool petal=false)
        {
            if(Refined) { SolidBlade(at,direction,length,width,edge,color,exponent,planeNormal??PlantVector.Up,petal);return; }
            var forward=direction.Normalized;var normal=planeNormal??PlantVector.Up;
            var cross=PlantVector.Cross(forward,normal);var side=cross.Normalized;
            if(cross.Length<1e-8) { PlantVector unused;Basis(forward,out side,out unused); }
            for(int i=0;i<segments;i++)
            {
                double t0=(double)i/segments,t1=(double)(i+1)/segments;
                double w0=width*.5*Math.Pow(Math.Sin(Math.PI*t0),exponent)*(1+edge*Math.Sin(6*Math.PI*t0));
                double w1=width*.5*Math.Pow(Math.Sin(Math.PI*t1),exponent)*(1+edge*Math.Sin(6*Math.PI*t1));
                var a=at+forward*(length*t0)+normal*(length*.08*Math.Sin(Math.PI*t0));
                var b=at+forward*(length*t1)+normal*(length*.08*Math.Sin(Math.PI*t1));
                Triangle(a-side*w0,b-side*w1,a+side*w0,color);Triangle(a+side*w0,b-side*w1,b+side*w1,color);
            }
        }
        // RefinedMeshes/Chamomile uses an eight-point outline with separate raised
        // front and back centers. Keep that closed, faceted form at any organ size.
        void SolidBlade(PlantVector at,PlantVector direction,double length,double width,double edge,PlantColor color,double exponent,PlantVector planeNormal,bool petal)
        {
            if(length<=0||width<=0) return;
            var forward=direction.Normalized;var cross=PlantVector.Cross(forward,planeNormal);
            PlantVector side=cross.Normalized;
            if(cross.Length<1e-8) { PlantVector unused;Basis(forward,out side,out unused); }
            var normal=PlantVector.Cross(side,forward).Normalized;
            double[] along=petal?new[]{0.0,.16,.50,.84,1,.84,.50,.16}:new[]{0.0,.22,.45,.73,1,.73,.45,.22};
            double[] across=petal?new[]{0.0,-.25,-.48,-.39,0,.39,.48,.25}:new[]{0.0,-.25,-.50,-.32,0,.32,.50,.25};
            var outline=new PlantVector[8];
            // Scale relief with width as well as length, so fine divided leaves do
            // not inflate into needles, and new small organs have no fixed-size bulge.
            double relief=Math.Min(length*.12,width*.28);
            for(int i=0;i<8;i++)
            {
                double t=along[i];
                double taper=Math.Pow(Math.Max(.01,Math.Sin(Math.PI*t)),Math.Max(-.25,exponent-.8));
                double w=across[i]*width*taper*(1+edge*Math.Sin(6*Math.PI*t));
                outline[i]=at+forward*(length*t)+side*w+normal*(Math.Sin(t*Math.PI)*relief*(petal?-.40:.18));
            }
            var top=at+forward*(length*.49)+normal*(relief*(petal?.45:1));
            var bottom=at+forward*(length*.49)-normal*(relief*.60);
            for(int i=0;i<8;i++)
            {
                int next=(i+1)%8;
                Triangle(top,outline[next],outline[i],color.Brightness(i<4?1.04:.94));
                Triangle(bottom,outline[i],outline[next],color.Brightness(.80));
            }
        }
    }
    public static class PlantGeometry
    {
        static readonly PlantColor Stem=PlantColor.Hex("526F4D"),Leaf=PlantColor.Hex("739764"),LightLeaf=PlantColor.Hex("A0B787");
        public static PlantMeshData Build(PlantRecipe p)
        {
            PlantGenerator.Validate(p);
            var m=new PlantMeshData(p.MeshVersion>=2);
            if(p.GrowthStep==0) m.Orb(new PlantVector(0,.035,0),new PlantVector(.035,.033,.052),PlantColor.Hex("AD8158"),10,4);
            else
            {
                double juvenile=p.SpeciesId==null?Math.Max(.08,Math.Min(.16,.04+.48*p.Growth)):.16;
                m.Tube(new PlantVector(0,0,0),new PlantVector(0,juvenile,0),.006,.005,Stem);
                // Shared cotyledons keep the same attachment and shape for the entire lifecycle.
                m.Blade(new PlantVector(0,.045,0),new PlantVector(-1,.4,0),.075,.038,0,Leaf,.65);
                m.Blade(new PlantVector(0,.049,0),new PlantVector(1,.4,0),.075,.038,0,LightLeaf,.65);
                if(p.SpeciesId!=null) BuildDifferentiated(m,p);
            }
            if(m.TriangleCount>8000) throw new InvalidOperationException("Plant mesh exceeds 8,000 triangle budget.");
            if(m.Vertices.Any(v=>!PlantGenerator.InEnvelope(v)))
            {
                var v=m.Vertices.First(x=>!PlantGenerator.InEnvelope(x));
                throw new InvalidOperationException("Plant mesh exceeds radius 0.60 / height 1.30 bounds: "+p.SpeciesId+" step "+p.GrowthStep+" ("+v.X+", "+v.Y+", "+v.Z+").");
            }
            return m;
        }
        static void BuildDifferentiated(PlantMeshData m,PlantRecipe p)
        {
            var s=PlantCatalog.Get(p.SpeciesId);
            double r=PlantMath.Lerp(.006,p.Radius,PlantMath.Smooth(p.Growth,.25,.80));
            for(int i=0;i<PlantGenerator.AxisCount(p);i++)
            {
                double visible=PlantGenerator.AxisVisible(p,i),radius=r*(i==0?1:.65);
                for(int k=0;k<4;k++) m.Tube(PlantGenerator.AxisPoint(p,i,visible*k/4),PlantGenerator.AxisPoint(p,i,visible*(k+1)/4),radius*(1-.10*k),radius*(.9-.10*k),Stem);
            }
            foreach(var l in p.Leaves)
            {
                double len=l.Length*l.Scale*(.2+.8*PlantMath.Smooth(p.Growth,l.Birth,Math.Min(1,l.Birth+.10)));
                DrawLeaf(m,s.Leaf,l.Anchor,l.Direction,len,len*l.Aspect,l.Edge);
            }
            for(int i=0;i<p.Heads.Count;i++)
            {
                var h=p.Heads[i];double open=PlantGenerator.Opening(p,i);
                // v2 terminal buds ride the growing apex, then stop before opening.
                var anchor=PlantGenerator.HeadAnchor(p,h);var tip=PlantGenerator.HeadTip(p,h);
                var offset=tip-h.Tip;
                var mid=(anchor+tip)*.5+PlantVector.Up*(p.Height*.025);
                if(p.GeneratorVersion>=2) mid.Y=Math.Min(mid.Y,Math.Max(anchor.Y,tip.Y));
                m.Tube(anchor,mid,r*.40,r*.30,Stem);m.Tube(mid,tip,r*.30,r*.20,Stem);
                for(int k=0;k<PlantGenerator.VisibleFlorets(p,h);k++)
                {
                    PlantVector at,normal;FloretPose(s,h,k,out at,out normal);
                    at+=offset;
                    if((at-tip).Length>.005) m.Tube(tip,at,.0025,.0015,Stem,5);
                    var fruit=p.Fruits.FirstOrDefault(f=>f.Head==i&&f.Floret==k&&f.Present);
                    if(fruit!=null)
                    {
                        double size=fruit.Radius*(p.Mature?1:.2+.8*PlantMath.Clip((double)(p.GrowthStep-fruit.EvaluationStep+1)/(p.MaxGrowthSteps*.25)));
                        m.Orb(at,new PlantVector(size,size*(s.Id=="species:pink-rose"?1.3:1),size),PlantColor.Hex(s.Id=="species:pink-rose"?"C46F54":"CB6E62"));
                        if(s.Id=="species:pink-rose") m.Orb(at+PlantVector.Up*(size*1.2),new PlantVector(size*.35,size*.15,size*.35),Stem,5,2);
                        continue;
                    }
                    double brightness=1+.04*(2*PlantMath.U(p.BaseSeed,"head/"+PlantMath.Number(i)+"/floret/"+PlantMath.Number(k),"light")-1);
                    double diameter=h.Diameter*h.Scale*(.25+.75*open);
                    DrawFlower(m,p,s,h,i,at,normal,diameter,open,h.Color.Brightness(brightness));
                }
            }
        }
        public sealed class FlowerEnvelope
        {
            public bool Fits;
            public PlantVector Center;
            public double Radius;
        }
        // Use the actual low-poly template instead of a large spherical estimate for
        // tall racemes or wide flat sunflower heads at a terminal growth point.
        public static FlowerEnvelope HeadEnvelope(PlantRecipe p,SpeciesDefinition s,HeadRecipe h,int index)
        {
            var m=new PlantMeshData(p.MeshVersion>=2);double d=h.Diameter*h.Scale;
            for(int k=0;k<h.Florets;k++)
            {
                PlantVector at,normal;FloretPose(s,h,k,out at,out normal);
                DrawFlower(m,p,s,h,index,at,normal,d,1,h.Color);
                DrawFlower(m,p,s,h,index,at,normal,d*.25,0,h.Color);
                if(s.FruitCap>0)
                {
                    double radius=s.Id=="species:pink-rose"?.042:.0216;
                    m.Orb(at,new PlantVector(radius,radius*(s.Id=="species:pink-rose"?1.3:1),radius),h.Color);
                }
            }
            var min=new PlantVector(m.Vertices.Min(v=>v.X),m.Vertices.Min(v=>v.Y),m.Vertices.Min(v=>v.Z));
            var max=new PlantVector(m.Vertices.Max(v=>v.X),m.Vertices.Max(v=>v.Y),m.Vertices.Max(v=>v.Z));
            var center=(min+max)*.5;
            return new FlowerEnvelope { Fits=m.Vertices.All(v=>PlantGenerator.InEnvelope(v)),Center=center,Radius=m.Vertices.Max(v=>(v-center).Length) };
        }
        static void DrawLeaf(PlantMeshData m,LeafShape shape,PlantVector at,PlantVector dir,double length,double width,double edge)
        {
            if(shape==LeafShape.Divided||shape==LeafShape.Compound)
            {
                var side=PlantVector.Cross(dir,PlantVector.Up).Normalized;
                m.Tube(at,at+dir*length,.0018,.0008,Stem,4);
                int pairs=shape==LeafShape.Compound?2:4;
                for(int j=0;j<pairs;j++) for(int sign=-1;sign<=1;sign+=2)
                {
                    double t=.20+.60*j/pairs;
                    var start=at+dir*(length*t); var outward=(dir*.32+side*sign).Normalized;
                    double size=shape==LeafShape.Compound?length*.36:length*(.27-.10*t);
                    m.Blade(start,outward,size,shape==LeafShape.Compound?width*.65:width*.35,edge,Leaf,.8,3);
                }
                if(shape==LeafShape.Compound) m.Blade(at+dir*(length*.65),dir,length*.35,width*.65,edge,LightLeaf,.8,3);
                return;
            }
            double exponent=(shape==LeafShape.Sword||shape==LeafShape.Strap||shape==LeafShape.Narrow)?1.4:shape==LeafShape.Spoon?.65:.8;
            if(shape==LeafShape.Heart) // Paired basal lobes retain a heart-like silhouette.
            {
                var side=PlantVector.Cross(dir,PlantVector.Up).Normalized;
                m.Blade(at,dir+side*.35,length*.70,width*.75,edge,Leaf,.55,5);
                m.Blade(at,dir-side*.35,length*.70,width*.75,edge,LightLeaf,.55,5);
            }
            m.Blade(at,dir,length,width,edge,Leaf,exponent,6);
        }
        public static void FloretPose(SpeciesDefinition s,HeadRecipe h,int k,out PlantVector at,out PlantVector normal)
        {
            at=h.Tip;normal=PlantVector.Up;double d=h.Diameter*h.Scale;
            if(s.Flower==FlowerShape.Hydrangea||s.Id=="species:forget-me-not")
            {
                double y=1-(k+.5)/h.Florets,angle=k*2.399963229728653;
                var sphere=new PlantVector(Math.Sqrt(1-y*y)*Math.Cos(angle),y,Math.Sqrt(1-y*y)*Math.Sin(angle));
                at+=sphere*(d*.60*Math.Sqrt(h.Florets));normal=(sphere+PlantVector.Up*.4).Normalized;
            }
            else if(s.Flower==FlowerShape.Lavender)
            { var outwards=PlantVector.Direction(k*137.507764);at+=PlantVector.Up*(k*d*.55)+outwards*(d*.25);normal=(outwards+PlantVector.Up*.3).Normalized; }
            else if(h.Florets>1)
            {
                at+=new PlantVector(d*(.50+.15*k),d*.60*k,d*.25*(k%2==0?1:-1));
                normal=s.Flower==FlowerShape.Trumpet?new PlantVector(.8,.5,0).Normalized:new PlantVector(.15,-1,0).Normalized;
            }
            else if(s.Flower==FlowerShape.Snowdrop) { at+=new PlantVector(d*.1,0,0);normal=new PlantVector(.15,-1,0).Normalized; }
        }
        static void DrawFlower(PlantMeshData m,PlantRecipe p,SpeciesDefinition s,HeadRecipe h,int head,PlantVector at,PlantVector normal,double d,double open,PlantColor color)
        {
            if(open<.02)
            { m.Orb(at,new PlantVector(d*.43,d*.65,d*.43),Leaf,7,3);return; }
            PlantVector u,v;PlantMeshData.Basis(normal,out u,out v);
            if(s.Flower==FlowerShape.Bell||s.Flower==FlowerShape.Trumpet)
            { Cup(m,at,normal,u,v,d,h.Petals,open,color);return; }
            if(s.Flower==FlowerShape.Snowdrop)
            {
                for(int j=0;j<6;j++)
                {
                    double a=2*Math.PI*(j%3)/3+(j>=3?Math.PI/3:0);
                    var dir=(normal*(1-.5*open)+(u*Math.Cos(a)+v*Math.Sin(a))*(.35+.45*open)).Normalized;
                    m.Blade(at,dir,d*(j<3?.70:.34),d*(j<3?.23:.20),0,j<3?color:PlantColor.Hex(s.Center),.8,4,normal,true);
                }
                return;
            }
            for(int k=0;k<h.Petals;k++)
            {
                double a=2*Math.PI*k/h.Petals+.03*(2*PlantMath.U(p.BaseSeed,"head/"+PlantMath.Number(head)+"/petal/"+PlantMath.Number(k),"angle")-1);
                var radial=u*Math.Cos(a)+v*Math.Sin(a);
                var dir=(radial*open+normal*(1-open+.08)).Normalized;
                double width=d*(h.Petals>=10?.18:.48);
                double length=d*.47;
                if(s.Flower==FlowerShape.Lavender) { length*=k<2?.80:1.15;width*=.8; }
                m.Blade(at+radial*(d*.06),dir,length,width,0,color,.65,4,normal,true);
            }
            double center=s.Id=="species:sunflower"?.25:s.Flower==FlowerShape.Rays?.17:.075;
            if(m.Refined&&s.Id=="species:chamomile")
            {
                // A raised golden disk with small pollen facets, as in the reference.
                var disk=at+normal*(d*.045);
                OrientedOrb(m,disk,normal,u,v,new PlantVector(d*.20,d*.095,d*.20),PlantColor.Hex(s.Center),10,4);
                for(int i=0;i<7;i++)
                {
                    double a=i*2.399963229728653;
                    var pollen=disk+(u*Math.Cos(a)+v*Math.Sin(a))*(d*.085)+normal*(d*.081);
                    OrientedOrb(m,pollen,normal,u,v,new PlantVector(d*.026,d*.018,d*.026),PlantColor.Hex(s.Center).Brightness(i%2==0?1.18:1.04),5,2);
                }
                return;
            }
            m.Orb(at+normal*(d*.03),new PlantVector(d*center,d*center*.65,d*center),PlantColor.Hex(s.Center),8,3);
        }
        static void OrientedOrb(PlantMeshData m,PlantVector at,PlantVector normal,PlantVector u,PlantVector v,PlantVector radii,PlantColor color,int sides,int rings)
        {
            var local=new PlantMeshData(true);local.Orb(new PlantVector(),radii,color,sides,rings);
            for(int i=0;i<local.Vertices.Count;i+=3)
            {
                var a=local.Vertices[i];var b=local.Vertices[i+1];var c=local.Vertices[i+2];
                // (u, normal, -v) is a right-handed frame.
                m.Triangle(at+u*a.X+normal*a.Y-v*a.Z,at+u*b.X+normal*b.Y-v*b.Z,at+u*c.X+normal*c.Y-v*c.Z,color);
            }
        }
        static void Cup(PlantMeshData m,PlantVector at,PlantVector n,PlantVector u,PlantVector v,double d,int lobes,double open,PlantColor color)
        {
            if(m.Refined) { SolidCup(m,at,n,u,v,d,lobes,open,color);return; }
            int sides=lobes*2;
            for(int ring=0;ring<3;ring++) for(int k=0;k<sides;k++)
            {
                var a=CupPoint(at,n,u,v,d,open,ring,k,sides);var b=CupPoint(at,n,u,v,d,open,ring,k+1,sides);
                var c=CupPoint(at,n,u,v,d,open,ring+1,k,sides);var e=CupPoint(at,n,u,v,d,open,ring+1,k+1,sides);
                m.Triangle(a,c,b,color);m.Triangle(b,c,e,color);
            }
        }
        static void SolidCup(PlantMeshData m,PlantVector at,PlantVector n,PlantVector u,PlantVector v,double d,int lobes,double open,PlantColor color)
        {
            int sides=lobes*2;double thickness=d*.035*(.35+.65*open);
            var outside=new PlantVector[4,sides];var inside=new PlantVector[4,sides];
            for(int ring=0;ring<4;ring++) for(int k=0;k<sides;k++)
            {
                double angle=2*Math.PI*k/sides;
                outside[ring,k]=CupPoint(at,n,u,v,d,open,ring,k,sides);
                inside[ring,k]=outside[ring,k]-(u*Math.Cos(angle)+v*Math.Sin(angle))*thickness;
            }
            for(int k=0;k<sides;k++)
            {
                int next=(k+1)%sides;
                for(int ring=0;ring<3;ring++)
                {
                    var a=outside[ring,k];var b=outside[ring,next];var c=outside[ring+1,k];var e=outside[ring+1,next];
                    m.Triangle(a,b,c,color);m.Triangle(b,e,c,color);
                    a=inside[ring,k];b=inside[ring,next];c=inside[ring+1,k];e=inside[ring+1,next];
                    m.Triangle(a,c,b,color.Brightness(.84));m.Triangle(b,c,e,color.Brightness(.84));
                }
                m.Triangle(outside[3,k],outside[3,next],inside[3,k],color.Brightness(1.05));
                m.Triangle(outside[3,next],inside[3,next],inside[3,k],color.Brightness(1.05));
                m.Triangle(outside[0,k],inside[0,k],outside[0,next],color.Brightness(.80));
                m.Triangle(outside[0,next],inside[0,k],inside[0,next],color.Brightness(.80));
            }
        }
        static PlantVector CupPoint(PlantVector at,PlantVector n,PlantVector u,PlantVector v,double d,double open,int ring,int k,int sides)
        {
            double t=ring/3.0,a=2*Math.PI*k/sides;
            double radius=d*(.09+.32*Math.Sin(t*Math.PI*.6))*(.35+.65*open);
            if(ring==3) radius*=k%2==0?1.12:.92;
            return at+n*(d*.65*t)+(u*Math.Cos(a)+v*Math.Sin(a))*radius;
        }
    }
}
