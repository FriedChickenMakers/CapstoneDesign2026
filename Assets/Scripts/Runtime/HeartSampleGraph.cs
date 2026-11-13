using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HeartSampleGraph : MaskableGraphic
    {
        public const float PlotPadding=8;
        public HeartRateSampleSnapshot[] Samples=Array.Empty<HeartRateSampleSnapshot>();
        public long WindowStart,WindowEnd;
        public double AxisMinimum=>Samples.Length==0?0:Samples.Min(s=>s.beatsPerMinute)-5;
        public double AxisMaximum=>Samples.Length==0?0:Samples.Max(s=>s.beatsPerMinute)+5;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(WindowEnd<=WindowStart || Samples.Length==0)return;
            var rect=rectTransform.rect;
            double min=AxisMinimum, max=AxisMaximum;
            foreach(var group in Samples.GroupBy(s=>s.sourcePackage+"|"+s.segmentId))
            {
                Vector2? previous=null;long previousTime=0;
                foreach(var sample in group.OrderBy(s=>s.measuredAtEpochMs))
                {
                    var point=new Vector2(rect.xMin+PlotPadding+(rect.width-2*PlotPadding)*(float)((double)(sample.measuredAtEpochMs-WindowStart)/(WindowEnd-WindowStart)),rect.yMin+PlotPadding+(rect.height-2*PlotPadding)*(float)((sample.beatsPerMinute-min)/(max-min)));
                    Quad(vh,point-new Vector2(3,3),point+new Vector2(3,-3),point+new Vector2(3,3),point+new Vector2(-3,3));
                    if(previous.HasValue && sample.measuredAtEpochMs-previousTime<=300000)
                    {
                        var dir=point-previous.Value;var n=new Vector2(-dir.y,dir.x).normalized*1.5f;
                        Quad(vh,previous.Value-n,point-n,point+n,previous.Value+n);
                    }
                    previous=point;previousTime=sample.measuredAtEpochMs;
                }
            }
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            int n=vh.currentVertCount;var color=GardenUi.Green;
            vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
