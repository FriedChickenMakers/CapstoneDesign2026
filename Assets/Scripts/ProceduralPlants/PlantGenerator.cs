using System;
using System.Collections.Generic;
using System.Linq;

namespace CapstoneDesign.Plants
{
    public sealed class SpeciesScore
    {
        public string SpeciesId;
        public double Distance;
    }
    public static class PlantGenerator
    {
        public static PlantRecipe Create(string ownerId,string instanceId,int maxGrowthSteps=12,PlantGrowthMode growthMode=PlantGrowthMode.NutrientPurchases)
        {
            if(string.IsNullOrWhiteSpace(ownerId)||string.IsNullOrWhiteSpace(instanceId)) throw new ArgumentException("Stable IDs required.");
            ValidateSettings(maxGrowthSteps,growthMode);
            return new PlantRecipe { GeneratorVersion=2,MeshVersion=2,InstanceId=instanceId,BaseSeed=PlantMath.Hash("GardenPlant","1",ownerId,instanceId),MaxGrowthSteps=maxGrowthSteps,GrowthMode=growthMode };
        }
        public static void ValidateSettings(int maxGrowthSteps,PlantGrowthMode mode)
        {
            if(maxGrowthSteps<6||maxGrowthSteps>1000||!Enum.IsDefined(typeof(PlantGrowthMode),mode)) throw new ArgumentException("Growth target must be 6..1000 and mode must be known.");
        }
        public static int MissionGrowthStep(PlantRecipe p)
        {
            long count=p.Counts.Sum(x=>(long)x);
            int earned=(int)Math.Min(p.MaxGrowthSteps,count);
            return count<6?Math.Min(earned,p.DifferentiationStep-1):earned;
        }
        public static SpeciesScore[] Candidates(int[] counts)
        {
            var p=PlantMath.Profile(counts);
            return PlantCatalog.All.Select(s=>new SpeciesScore { SpeciesId=s.Id,
                Distance=Enumerable.Range(0,6).Sum(k=>Math.Pow(p[k]-(k==s.Primary?.60:k==s.Secondary?.25:.0375),2)) })
                .OrderBy(s=>s.Distance).ThenBy(s=>s.SpeciesId,StringComparer.Ordinal).ToArray();
        }
        // Pure transaction: never mutates the supplied recipe, including on validation failure.
        public static PlantRecipe Advance(PlantRecipe committed)
        {
            Validate(committed);
            if(committed.Mature) throw new InvalidOperationException("Plant is already mature.");
            var p=PlantCodec.Clone(committed); p.GrowthStep++;
            if(p.GrowthStep==p.DifferentiationStep)
            {
                if(p.Counts.Sum(x=>(long)x)<6) throw new InvalidOperationException("At least six credited activities are required before differentiation.");
                LockSpecies(p);
            }
            if(p.SpeciesId!=null)
            {
                var s=PlantCatalog.Get(p.SpeciesId); var profile=PlantMath.Profile(p.Counts);
                while(p.Leaves.Count<LeafCount(p)-2) AddLeaf(p,s,profile);
                while(p.Heads.Count<HeadCount(p)) AddHead(p,s,profile);
                EvaluateFruit(p,s,profile);
            }
            Validate(p);
            PlantGeometry.Build(p); // Budget and bounds failure aborts the caller's transaction.
            return p;
        }
        static double Z(PlantRecipe p,double[] profile,int a,int b,string node,string tag)
        { return PlantMath.Z(profile,a,b,p.BaseSeed,node,tag); }
        static void LockSpecies(PlantRecipe p)
        {
            var scores=Candidates(p.Counts); double best=scores[0].Distance;
            p.SpeciesId=scores.Where(x=>Math.Abs(x.Distance-best)<=1e-12)
                .OrderBy(x=>PlantMath.U(p.BaseSeed,"species-tie",x.SpeciesId)).ThenBy(x=>x.SpeciesId,StringComparer.Ordinal).First().SpeciesId;
            p.LockedProfile=PlantMath.Profile(p.Counts); var q=p.LockedProfile; var s=PlantCatalog.Get(p.SpeciesId);
            p.Height=PlantMath.Lerp(s.HeightMin,s.HeightMax,Z(p,q,2,0,"plant","height"));
            p.Radius=Math.Max(.006,p.Height*(.012+.012*Z(p,q,1,4,"plant","radius")));
            p.Bend=.12*(2*Z(p,q,0,2,"plant","bend")-1);
            p.TargetAxes=PlantMath.Integer(s.AxesMin,s.AxesMax,Z(p,q,2,0,"plant","axes"));
            p.TargetLeaves=PlantMath.Integer(s.LeavesMin,s.LeavesMax,Z(p,q,1,4,"plant","leaves"));
            p.TargetHeads=PlantMath.Integer(s.HeadsMin,s.HeadsMax,Z(p,q,5,0,"plant","heads"));
            p.Spread=15+30*Z(p,q,2,0,"plant","spread");
            if(s.Basal) p.Spread=Math.Max(5,Math.Min(s.Flower==FlowerShape.Lavender?30:25,p.Spread));
            p.Heading=360*PlantMath.U(p.BaseSeed,"plant","heading");
            p.LeafPhase=360*PlantMath.U(p.BaseSeed,"plant","leaf-phase");
            p.Axes.Add(new AxisRecipe { Parent=-1,Birth=.25,Length=p.Height-.16,Anchor=new PlantVector(0,.16,0) });
            for(int i=1;i<p.TargetAxes;i++)
            {
                string id="axis/"+PlantMath.Number(i);
                double birth=PlantMath.Birth(i,p.TargetAxes-1,1.0/3,p.GeneratorVersion>=2?.48:.75);
                double attach=s.Basal?0:.65*PlantMath.Smooth(birth,.25,p.GeneratorVersion>=2?.50:.85);
                if(p.GeneratorVersion>=2&&!s.Basal)
                    attach=Math.Min(attach,Math.Max(0,(p.Height*.90-p.Height*.30*Math.Cos(p.Spread*Math.PI/180)-.16)/(p.Height-.16)));
                var anchor=s.Basal?new PlantVector(0,.02,0):AxisPoint(p,0,attach);
                double radians=p.Spread*Math.PI/180;
                double length=p.Height*PlantMath.Lerp(.30,.65,PlantMath.U(p.BaseSeed,id,"length"));
                // Constrain the complete future axis, not just the revealed segment.
                double top=p.GeneratorVersion>=2?p.Height*.90:1.26;
                double bound=Math.Min((top-anchor.Y)/Math.Cos(radians),(.56-Math.Sqrt(anchor.X*anchor.X+anchor.Z*anchor.Z))/Math.Sin(radians));
                length=Math.Min(length,bound);
                if(length+1e-9<p.Height*.30) throw new InvalidOperationException("Axis does not fit its catalog envelope.");
                p.Axes.Add(new AxisRecipe { Parent=s.Basal?-1:0,Birth=birth,AttachT=attach,
                    Anchor=anchor,Length=length,
                    Spread=p.Spread,Azimuth=p.Heading+360.0*i/Math.Max(1,p.TargetAxes-1)+(.10*180/Math.PI)*(2*PlantMath.U(p.BaseSeed,id,"azimuth")-1) });
            }
        }
        public static int AxisCount(PlantRecipe p) { return p.GrowthStep==0?0:p.SpeciesId==null?1:1+(int)Math.Floor((p.TargetAxes-1)*PlantMath.Smooth(p.Growth,1.0/3,p.GeneratorVersion>=2?.48:.75)); }
        public static int LeafCount(PlantRecipe p) { return p.GrowthStep==0?0:p.SpeciesId==null?2:2+(int)Math.Floor((p.TargetLeaves-2)*PlantMath.Smooth(p.Growth,.25,.90)); }
        public static int HeadCount(PlantRecipe p) { return p.SpeciesId==null?0:1+(int)Math.Floor((p.TargetHeads-1)*PlantMath.Smooth(p.Growth,1.0/3,.75)); }
        public static double AxisVisible(PlantRecipe p,int i)
        {
            if(p.GeneratorVersion>=2) return i==0?PlantMath.Smooth(p.Growth,.25,.50):.15+.85*PlantMath.Smooth(p.Growth,p.Axes[i].Birth,.50);
            return i==0?PlantMath.Smooth(p.Growth,.25,.85):.15+.85*PlantMath.Smooth(p.Growth,p.Axes[i].Birth,Math.Min(.95,p.Axes[i].Birth+.15));
        }
        public static PlantVector AxisPoint(PlantRecipe p,int i,double t)
        {
            if(i==0) return new PlantVector(0,.16+(p.Height-.16)*t,0)+PlantVector.Direction(p.Heading)*(p.Bend*(p.Height-.16)*t*t);
            var a=p.Axes[i]; double angle=a.Spread*Math.PI/180;
            return a.Anchor+(PlantVector.Up*Math.Cos(angle)+PlantVector.Direction(a.Azimuth)*Math.Sin(angle))*(a.Length*t);
        }
        static int Parent(PlantRecipe p,int index) { return index%AxisCount(p); }
        static void AddLeaf(PlantRecipe p,SpeciesDefinition s,double[] q)
        {
            int i=p.Leaves.Count+2; string node="leaf/"+PlantMath.Number(i);
            int parent=Parent(p,i-2); double attach=AxisVisible(p,parent)*(.25+.65*PlantMath.U(p.BaseSeed,node,"attachment"));
            double angle=p.LeafPhase+8*(2*PlantMath.U(p.BaseSeed,node,"angle")-1);
            switch(s.Arrangement)
            {
                case LeafArrangement.Opposite: angle+=90*(i/2)+180*(i%2);break;
                case LeafArrangement.Rosette: angle+=360.0*i/p.TargetLeaves;break;
                case LeafArrangement.Basal: angle+=180*(i%2)+15*(i/2);break;
                default: angle+=i*137.507764;break;
            }
            var leaf=new LeafRecipe { Parent=parent,Attachment=attach,
                Birth=PlantMath.Birth(i-1,p.TargetLeaves-2,.25,.90),
                Length=p.Height*PlantMath.Lerp(s.LeafMin,s.LeafMax,Z(p,q,1,2,node,"length")),
                Aspect=PlantMath.Lerp(s.AspectMin,s.AspectMax,Z(p,q,1,3,node,"aspect")),
                Edge=s.EdgeMax*Z(p,q,4,3,node,"edge"),Angle=angle,
                Anchor=AxisPoint(p,parent,attach) };
            if(s.Arrangement==LeafArrangement.Basal||s.Arrangement==LeafArrangement.Rosette)
            { leaf.Parent=0;leaf.Attachment=-1;leaf.Anchor=new PlantVector(0,.02+.003*(i-2),0); }
            double rise=s.Arrangement==LeafArrangement.Basal?1.2:.30;
            foreach(double offset in new[]{0.0,15,-15,30})
            {
                leaf.Direction=PlantVector.Direction(angle+offset,rise);
                if(LeafFits(p,leaf)) { p.Leaves.Add(leaf);return; }
            }
            // Shrink only the new organ to its species minimum, retaining its profile target.
            leaf.Scale=(p.Height*s.LeafMin)/leaf.Length;
            if(LeafFits(p,leaf)) { p.Leaves.Add(leaf);return; }
            // Inward/upward fallback at the SAME attachment; old organs never move.
            leaf.Direction=(new PlantVector(-leaf.Anchor.X,.35,-leaf.Anchor.Z)).Normalized;
            if(!LeafFits(p,leaf)) throw new InvalidOperationException("No valid leaf placement.");
            p.Leaves.Add(leaf);
        }
        static bool LeafFits(PlantRecipe p,LeafRecipe leaf)
        {
            var tip=leaf.Anchor+leaf.Direction*(leaf.Length*leaf.Scale);
            double margin=leaf.Length*leaf.Scale*leaf.Aspect*.60;
            return InEnvelope(leaf.Anchor,margin)&&InEnvelope(tip,margin);
        }
        static void AddHead(PlantRecipe p,SpeciesDefinition s,double[] q)
        {
            int i=p.Heads.Count; string node="head/"+PlantMath.Number(i);
            int parent=Parent(p,i); double attach=AxisVisible(p,parent)*(.65+.30*PlantMath.U(p.BaseSeed,node,"attachment"));
            bool terminal=false;
            if(p.GeneratorVersion>=2)
            {
                int free=Enumerable.Range(0,AxisCount(p)).Where(a=>!p.Heads.Any(h=>h.Terminal&&h.Parent==a)).DefaultIfEmpty(-1).First();
                if(free>=0) { parent=free;attach=1;terminal=true; }
            }
            var head=new HeadRecipe { Parent=parent,Attachment=attach,Terminal=terminal,Anchor=AxisPoint(p,parent,attach),
                Birth=PlantMath.Birth(i,p.TargetHeads-1,1.0/3,.75),
                Florets=PlantMath.Integer(s.FloretsMin,s.FloretsMax,Z(p,q,3,4,node,"florets")),
                Petals=PlantMath.Integer(s.PetalsMin,s.PetalsMax,Z(p,q,3,1,node,"petals")),
                Diameter=p.Height*PlantMath.Lerp(s.FlowerMin,s.FlowerMax,Z(p,q,5,2,node,"size")),
                Color=PlantColor.Mix(PlantColor.Hex(s.PaletteA),PlantColor.Hex(s.PaletteB),Z(p,q,3,4,node,"color")) };
            double angle=p.Heading+i*137.507764;
            foreach(double offset in new[]{0.0,15,-15,30})
            {
                head.Tip=head.Anchor+PlantVector.Direction(angle+offset)*(terminal?.03:.10+.08*(i%2))*p.Height+PlantVector.Up*(p.Height*(terminal?.02:.14));
                if(p.GeneratorVersion>=2&&!terminal) head.Tip.Y=Math.Min(head.Tip.Y,AxisPoint(p,parent,1).Y);
                if(HeadFits(p,s,head)) { p.Heads.Add(head);return; }
            }
            head.Scale=(p.Height*s.FlowerMin)/head.Diameter;
            if(HeadFits(p,s,head)) { p.Heads.Add(head);return; }
            // Catalog-sized reserve ring, connected by a peduncle from the original anchor.
            // It is persisted and validated, never silently drops a requested head.
            for(int slot=0;slot<12;slot++)
            {
                double reserveRadius=Math.Min(.38,.585-HeadRadius(s,head));
                double reserveHeight=Math.Min(1.28-HeadRadius(s,head),Math.Max(.22,head.Anchor.Y)+.08+.12*(slot%3));
                head.Tip=PlantVector.Direction(angle+slot*30)*reserveRadius+PlantVector.Up*reserveHeight;
                if(p.GeneratorVersion>=2)
                {
                    // A terminal head cannot be placed below the axis end. Side flowers
                    // also stay below the terminal growth point on their supporting axis.
                    if(terminal) head.Tip=head.Anchor+PlantVector.Direction(angle+slot*30)*(p.Height*(.06+.04*(slot%3)))+PlantVector.Up*(p.Height*.02);
                    else head.Tip.Y=Math.Min(head.Tip.Y,AxisPoint(p,parent,1).Y);
                }
                if(HeadFits(p,s,head)) { p.Heads.Add(head);return; }
            }
            throw new InvalidOperationException("No valid flower placement: "+s.Id+" head "+i+" step "+p.GrowthStep+" radius "+HeadRadius(s,head)+".");
        }
        public static double HeadRadius(SpeciesDefinition s,HeadRecipe h)
        {
            double d=h.Diameter*h.Scale;
            if(s.Flower==FlowerShape.Hydrangea||s.Id=="species:forget-me-not") return d*(.60*Math.Sqrt(h.Florets)+.60);
            if(h.Florets>1) return d*(.65*h.Florets+.70);
            return d*.70;
        }
        static bool HeadFits(PlantRecipe p,SpeciesDefinition s,HeadRecipe h)
        {
            if(p.GeneratorVersion>=2)
            {
                var bounds=PlantGeometry.HeadEnvelope(p,s,h,p.Heads.Count);
                if(!bounds.Fits) return false;
                foreach(var old in p.Heads)
                {
                    var other=PlantGeometry.HeadEnvelope(p,s,old,p.Heads.IndexOf(old));
                    if((bounds.Center-other.Center).Length<.45*(bounds.Radius+other.Radius)) return false;
                }
                return true;
            }
            double r=HeadRadius(s,h);
            if(!InEnvelope(h.Tip,r)) return false;
            foreach(var old in p.Heads) if((old.Tip-h.Tip).Length<.60*(r+HeadRadius(s,old))) return false;
            return true;
        }
        public static bool InEnvelope(PlantVector p,double radius=0)
        { return PlantMath.Finite(p.X)&&PlantMath.Finite(p.Y)&&PlantMath.Finite(p.Z)&&Math.Sqrt(p.X*p.X+p.Z*p.Z)+radius<=.60+1e-9&&p.Y+radius<=1.30+1e-9&&p.Y>=-.001; }
        public static double Opening(PlantRecipe p,int head)
        { double start=.50+.25*head/Math.Max(1,p.TargetHeads-1);return PlantMath.Smooth(p.Growth,start,start+.15); }
        public static int VisibleFlorets(PlantRecipe p,HeadRecipe h)
        { return 1+(int)Math.Floor((h.Florets-1)*PlantMath.Smooth(p.Growth,h.Birth,Math.Min(h.Birth+.15,.95))); }
        public static PlantVector HeadAnchor(PlantRecipe p,HeadRecipe h)
        { return h.Terminal?AxisPoint(p,h.Parent,AxisVisible(p,h.Parent)):h.Anchor; }
        public static PlantVector HeadTip(PlantRecipe p,HeadRecipe h)
        { return h.Tip+(HeadAnchor(p,h)-h.Anchor); }
        static void EvaluateFruit(PlantRecipe p,SpeciesDefinition s,double[] q)
        {
            if(p.Growth<5.0/6||s.FruitCap==0) return;
            for(int i=0;i<p.Heads.Count&&p.Fruits.Count<s.FruitCap;i++)
            {
                if(Opening(p,i)<1) continue;
                for(int k=0;k<VisibleFlorets(p,p.Heads[i])&&p.Fruits.Count<s.FruitCap;k++)
                {
                    if((i==0&&k==0)||p.Fruits.Any(f=>f.Head==i&&f.Floret==k)) continue;
                    string node="fruit/head/"+PlantMath.Number(i)+"/floret/"+PlantMath.Number(k);
                    p.Fruits.Add(new FruitRecipe { Head=i,Floret=k,EvaluationStep=p.GrowthStep,
                        Present=PlantMath.U(p.BaseSeed,node,"present")<.25+.5*Z(p,q,5,4,node,"propensity"),
                        Radius=(s.Id=="species:pink-rose"?.035:.018)*(.8+.4*Z(p,q,5,2,node,"size")) });
                }
            }
        }
        public static void Validate(PlantRecipe p)
        {
            if(p==null||(p.GeneratorVersion!=1&&p.GeneratorVersion!=2)||(p.MeshVersion!=1&&p.MeshVersion!=2)||p.CatalogVersion!=1||p.ProfileVersion!=1) throw new ArgumentException("Unsupported recipe version.");
            ValidateSettings(p.MaxGrowthSteps,p.GrowthMode);
            if(p.GeneratorVersion==1&&(p.MaxGrowthSteps!=12||p.GrowthMode!=PlantGrowthMode.NutrientPurchases)) throw new ArgumentException("Legacy recipes retain their original growth settings.");
            if(string.IsNullOrWhiteSpace(p.InstanceId)||p.BaseSeed==null||p.BaseSeed.Length!=64||p.BaseSeed.Any(c=>!"0123456789abcdef".Contains(c))) throw new ArgumentException("Invalid recipe identity.");
            PlantMath.Profile(p.Counts);
            if(p.GrowthStep<0||p.GrowthStep>p.MaxGrowthSteps||p.Axes==null||p.Leaves==null||p.Heads==null||p.Fruits==null) throw new ArgumentException("Invalid recipe collections or growth.");
            if(p.GrowthStep<p.DifferentiationStep)
            {
                if(!string.IsNullOrEmpty(p.SpeciesId)||p.LockedProfile!=null||p.Axes.Count+p.Leaves.Count+p.Heads.Count+p.Fruits.Count!=0) throw new ArgumentException("Early growth must be common.");
                return;
            }
            var s=PlantCatalog.Get(p.SpeciesId);
            RequireRange(p.Height,s.HeightMin,s.HeightMax);RequireRange(p.Radius,.006,.03);RequireRange(p.Bend,-.12,.12);RequireRange(p.Spread,5,45);
            RequireRange(p.Heading,0,360);RequireRange(p.LeafPhase,0,360);
            if(p.LockedProfile==null||p.LockedProfile.Length!=6||p.LockedProfile.Any(v=>!PlantMath.Finite(v)||v<0||v>1)||Math.Abs(p.LockedProfile.Sum()-1)>1e-9) throw new ArgumentException("Invalid locked profile.");
            RequireRange(p.TargetAxes,s.AxesMin,s.AxesMax);RequireRange(p.TargetLeaves,s.LeavesMin,s.LeavesMax);RequireRange(p.TargetHeads,s.HeadsMin,s.HeadsMax);
            if(p.Axes.Count!=p.TargetAxes||p.Leaves.Count!=LeafCount(p)-2||p.Heads.Count!=HeadCount(p)||p.Fruits.Count>s.FruitCap) throw new ArgumentException("Invalid organ counts.");
            for(int i=0;i<p.Axes.Count;i++)
            {
                var a=p.Axes[i];if(a==null||a.Parent>=i||a.Parent< -1||!InEnvelope(a.Anchor)) throw new ArgumentException("Invalid axis parent.");
                RequireRange(a.Birth,.25,.75);RequireRange(a.Length,.05,.99);RequireRange(a.AttachT,0,1);RequireRange(a.Azimuth,0,800);RequireRange(a.Spread,0,45);
            }
            foreach(var l in p.Leaves)
            {
                if(l==null||l.Parent<0||l.Parent>=AxisCount(p)||!InEnvelope(l.Anchor)||!PlantMath.Finite(l.Direction.Length)||Math.Abs(l.Direction.Length-1)>1e-8) throw new ArgumentException("Invalid leaf attachment.");
                RequireRange(l.Birth,.25,.90);RequireRange(l.Length,p.Height*s.LeafMin,p.Height*s.LeafMax);RequireRange(l.Scale,0.01,1);
                RequireRange(l.Aspect,s.AspectMin,s.AspectMax);RequireRange(l.Edge,0,s.EdgeMax);RequireRange(l.Angle,-8,4000);RequireRange(l.Attachment,-1,1);
            }
            foreach(var h in p.Heads)
            {
                if(h==null||h.Parent<0||h.Parent>=AxisCount(p)||!InEnvelope(h.Anchor)||!InEnvelope(h.Tip)) throw new ArgumentException("Invalid flower attachment.");
                RequireRange(h.Birth,1.0/3,.75);RequireRange(h.Florets,s.FloretsMin,s.FloretsMax);RequireRange(h.Petals,s.PetalsMin,s.PetalsMax);
                RequireRange(h.Diameter,p.Height*s.FlowerMin,p.Height*s.FlowerMax);RequireRange(h.Scale,.01,1);RequireRange(h.Attachment,0,1);
                RequireRange(h.Color.R,0,1);RequireRange(h.Color.G,0,1);RequireRange(h.Color.B,0,1);
                if(h.Terminal&&(p.GeneratorVersion<2||h.Attachment!=1||(h.Anchor-AxisPoint(p,h.Parent,1)).Length>1e-8||h.Tip.Y<h.Anchor.Y)) throw new ArgumentException("Invalid terminal flower.");
            }
            if(p.GeneratorVersion>=2&&(!p.Heads[0].Terminal||p.Heads[0].Parent!=0||p.Heads.Where(h=>h.Terminal).GroupBy(h=>h.Parent).Any(g=>g.Count()>1))) throw new ArgumentException("Every flowering axis has a single terminal flower.");
            var seen=new HashSet<string>();
            foreach(var f in p.Fruits)
            {
                if(f==null||f.Head<0||f.Head>=p.Heads.Count||f.Floret<0||f.Floret>=p.Heads[f.Head].Florets||(f.Head==0&&f.Floret==0)||!seen.Add(f.Head+":"+f.Floret)) throw new ArgumentException("Invalid fruit source.");
                RequireRange(f.EvaluationStep,Math.Ceiling(5.0*p.MaxGrowthSteps/6),p.GrowthStep);RequireRange(f.Radius,s.Id=="species:pink-rose"?.028:.0144,s.Id=="species:pink-rose"?.042:.0216);
                double openingStart=.50+.25*f.Head/Math.Max(1,p.TargetHeads-1);
                if(PlantMath.Smooth((double)f.EvaluationStep/p.MaxGrowthSteps,openingStart,openingStart+.15)<1) throw new ArgumentException("Fruit evaluated before bloom.");
            }
        }
        static void RequireRange(double v,double min,double max)
        { if(!PlantMath.Finite(v)||v<min-1e-9||v>max+1e-9) throw new ArgumentException("Recipe value outside catalog range."); }
    }
}
