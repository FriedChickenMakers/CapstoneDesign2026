using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

namespace CapstoneDesign.Plants
{
    // A versioned presentation layout: saved botanical attachments and memory IDs
    // stay intact. Each head only depends on earlier heads, never the camera or
    // the current growth stage, so adding a flower cannot move an existing one.
    public sealed class FlowerPose
    {
        public PlantVector Center,Right,Up,Forward;
        public double Scale=1;
        public PlantVector Rotate(PlantVector v) => Right*v.X+Up*v.Y+Forward*v.Z;
        public PlantVector Point(PlantVector v) => Center+Rotate(v)*Scale;
        internal FlowerPose Copy() => (FlowerPose)MemberwiseClone();
    }
    public static class PlantFlowerLayout
    {
        public const int Version=5;
        public const int ViewCount=8;
        // Bounded, geometry-only memoization avoids repeating candidate searches
        // when a bud opens or the mesh is restored. Returned poses are detached.
        static readonly Dictionary<string,FlowerPose[]> Cache=new Dictionary<string,FlowerPose[]>();
        static readonly Queue<string> CacheOrder=new Queue<string>();
        internal static void ClearCache(){lock(Cache){Cache.Clear();CacheOrder.Clear();}}
        static string Key(PlantRecipe p)
        {
            var key=new StringBuilder(p.SpeciesId).Append('|').Append(p.BaseSeed).Append('|').Append(p.TargetHeads).Append('|').Append(p.Heading.ToString("R",CultureInfo.InvariantCulture));
            foreach(var h in p.Heads)
            {
                key.Append('|').Append(h.Florets).Append('/').Append(h.Petals);
                foreach(double value in new[]{h.Diameter,h.Scale,h.Anchor.X,h.Anchor.Y,h.Anchor.Z,h.Tip.X,h.Tip.Y,h.Tip.Z})key.Append('/').Append(value.ToString("R",CultureInfo.InvariantCulture));
            }
            return key.ToString();
        }
        public static double Amplification(SpeciesDefinition s) => s.Flower==FlowerShape.Rays?2.5:s.Flower==FlowerShape.Lavender?2.8:1.65;
        public static double FloretSpread(SpeciesDefinition s) => s.Flower==FlowerShape.Hydrangea||s.Id=="species:forget-me-not"?1.2:1.7;
        public static PlantVector Compact(PlantVector v) => new PlantVector(v.X,v.Y*.78,v.Z);
        public static void ViewAxes(int view,out PlantVector right,out PlantVector up)
        {
            double yaw=(-25+view*45)*Math.PI/180,pitch=27*Math.PI/180;
            right=new PlantVector(Math.Cos(yaw),0,-Math.Sin(yaw));
            up=new PlantVector(Math.Sin(yaw)*Math.Sin(pitch),Math.Cos(pitch),Math.Cos(yaw)*Math.Sin(pitch));
        }
        static double Dot(PlantVector a,PlantVector b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public struct ScreenBounds
        {
            public double Left,Right,Bottom,Top;
            public double Area => Math.Max(1e-12,(Right-Left)*(Top-Bottom));
            public double Overlap(ScreenBounds b) => Math.Max(0,Math.Min(Right,b.Right)-Math.Max(Left,b.Left))*Math.Max(0,Math.Min(Top,b.Top)-Math.Max(Bottom,b.Bottom))/Math.Min(Area,b.Area);
            public ScreenBounds Shift(double x,double y) => new ScreenBounds{Left=Left+x,Right=Right+x,Bottom=Bottom+y,Top=Top+y};
        }
        sealed class Bounds
        {
            public PlantVector Min,Max,Center;
            public double Radius,Radial;
            public ScreenBounds[] Views;
        }
        static readonly PlantVector[] ViewRight=new PlantVector[ViewCount],ViewUp=new PlantVector[ViewCount];
        static PlantFlowerLayout()
        {for(int i=0;i<ViewCount;i++)ViewAxes(i,out ViewRight[i],out ViewUp[i]);}
        static Bounds Measure(PlantVector[] points)
        {
            var b=new Bounds{Min=new PlantVector(double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity),Max=new PlantVector(double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity),Views=new ScreenBounds[ViewCount]};
            for(int view=0;view<ViewCount;view++)b.Views[view]=new ScreenBounds{Left=double.PositiveInfinity,Bottom=double.PositiveInfinity,Right=double.NegativeInfinity,Top=double.NegativeInfinity};
            foreach(var point in points)
            {
                b.Min=new PlantVector(Math.Min(b.Min.X,point.X),Math.Min(b.Min.Y,point.Y),Math.Min(b.Min.Z,point.Z));
                b.Max=new PlantVector(Math.Max(b.Max.X,point.X),Math.Max(b.Max.Y,point.Y),Math.Max(b.Max.Z,point.Z));
                b.Radial=Math.Max(b.Radial,Math.Sqrt(point.X*point.X+point.Z*point.Z));
                var compact=Compact(point);
                for(int view=0;view<ViewCount;view++)
                {
                    double x=Dot(compact,ViewRight[view]),y=Dot(compact,ViewUp[view]);var v=b.Views[view];
                    v.Left=Math.Min(v.Left,x);v.Right=Math.Max(v.Right,x);v.Bottom=Math.Min(v.Bottom,y);v.Top=Math.Max(v.Top,y);b.Views[view]=v;
                }
            }
            b.Center=(b.Min+b.Max)*.5;
            foreach(var point in points)b.Radius=Math.Max(b.Radius,Compact(point-b.Center).Length);
            return b;
        }
        static Bounds Shift(Bounds source,PlantVector center)
        {
            var result=new Bounds{Min=source.Min+center,Max=source.Max+center,Center=source.Center+center,Radius=source.Radius,Views=new ScreenBounds[ViewCount]};
            for(int view=0;view<ViewCount;view++)
            {PlantVector right,up;ViewAxes(view,out right,out up);result.Views[view]=source.Views[view].Shift(Dot(Compact(center),right),Dot(Compact(center),up));}
            return result;
        }
        public static FlowerPose[] Plan(PlantRecipe recipe)
        {
            if(recipe.SpeciesId==null)return new FlowerPose[0];
            string key=Key(recipe);
            lock(Cache)if(Cache.TryGetValue(key,out var cached))return cached.Select(p=>p.Copy()).ToArray();
            var species=PlantCatalog.Get(recipe.SpeciesId);
            var poses=new List<FlowerPose>();var occupied=new List<Bounds>();
            for(int index=0;index<recipe.Heads.Count;index++)
            {
                var h=recipe.Heads[index];var template=PlantGeometry.FlowerLayoutTemplate(recipe,species,h,index);
                // Reserve room for the locked final head count before the first
                // flower opens. A giant early sunflower cannot consume the entire
                // crown and force later flowers to disappear or become tiny.
                double radiusBudget=recipe.TargetHeads==1?.36:recipe.TargetHeads==2?.26:.22;
                double budgetScale=Math.Min(1,radiusBudget/Math.Max(.001,Measure(template).Radius));
                double baseAngle=recipe.Heading+index*137.507764;
                double tilt=(species.Flower==FlowerShape.Rays||species.Flower==FlowerShape.Rose?22:8)
                    +8*PlantMath.U(recipe.BaseSeed,"head/"+index,"outward-tilt");
                FlowerPose chosen=null;Bounds chosenBounds=null;double best=double.PositiveInfinity;
                // Scale is a last resort after considering spacing and direction.
                foreach(double fraction in new[]{1.0,.88,.76,.64,.52,.40})
                {
                    double scale=budgetScale*fraction;
                    for(int az=0;az<12;az++)
                    {
                        double angle=baseAngle+az*30,rad=tilt*Math.PI/180;
                        var outward=PlantVector.Direction(angle);
                        var pose=new FlowerPose{Scale=scale,Right=outward*Math.Cos(rad)-PlantVector.Up*Math.Sin(rad),Up=outward*Math.Sin(rad)+PlantVector.Up*Math.Cos(rad)};
                        pose.Forward=PlantVector.Cross(pose.Right,pose.Up);
                        var points=template.Select(v=>pose.Rotate(v)*scale).ToArray();var local=Measure(points);
                        foreach(double radius in new[]{-1.0,.16,.26,.36,.44})foreach(double height in new[]{0.0,.08,-.08,.16,-.16})
                        {
                            var center=radius<0?h.Tip:outward*radius+PlantVector.Up*h.Tip.Y;
                            center.Y=Math.Max(h.Anchor.Y+.012,center.Y+height);
                            // Test the enlarged mature flower, including its closed
                            // bud, before applying the compact vertical proportion.
                            if(center.Y+local.Min.Y<.001||center.Y+local.Max.Y>1.28
                                ||Math.Sqrt(center.X*center.X+center.Z*center.Z)+local.Radial>.585)continue;
                            var compactCenter=Compact(center);bool intersects=false;double overlap=0,worst=0;
                            foreach(var old in occupied)
                            {
                                if((Compact(local.Center+center)-Compact(old.Center)).Length<.72*(local.Radius+old.Radius)+.012){intersects=true;break;}
                                for(int view=0;view<ViewCount;view++)
                                {double amount=local.Views[view].Shift(Dot(compactCenter,ViewRight[view]),Dot(compactCenter,ViewUp[view])).Overlap(old.Views[view]);overlap+=amount*amount;worst=Math.Max(worst,amount);}
                            }
                            if(intersects)continue;
                            double score=overlap/ViewCount+.15*worst+.08*(center-h.Anchor).Length+.015*(center-h.Tip).Length;
                            if(score>=best)continue;
                            best=score;chosen=new FlowerPose{Center=center,Right=pose.Right,Up=pose.Up,Forward=pose.Forward,Scale=scale};chosenBounds=Shift(local,center);
                        }
                    }
                    if(chosen!=null)break;
                }
                if(chosen==null)throw new InvalidOperationException("No separated flower layout: "+species.Id+" head "+index);
                poses.Add(chosen);occupied.Add(chosenBounds);
            }
            var result=poses.ToArray();
            lock(Cache)
            {
                if(!Cache.ContainsKey(key))
                {
                    while(Cache.Count>=64)Cache.Remove(CacheOrder.Dequeue());
                    Cache.Add(key,result.Select(p=>p.Copy()).ToArray());CacheOrder.Enqueue(key);
                }
            }
            return result;
        }
    }
}
