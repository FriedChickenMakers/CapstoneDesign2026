using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Only perimeter triangles are emitted: the camera remains visible through
    // the middle even when the frame is tinted or the day/night lighting changes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GardenOutlineFrame : MaskableGraphic
    {
        public float Radius=24;
        public float Thickness=2.5f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect=GetPixelAdjustedRect();
            float half=Mathf.Min(rect.width,rect.height)*.5f;
            if(half<=0 || Thickness<=0)return;
            float edge=Mathf.Min(Thickness,half);
            float radius=Mathf.Clamp(Radius,edge,half);
            const int segments=12, count=4*(segments+1);
            for(int corner=0;corner<4;corner++)
            {
                var centre=new Vector2(corner==0 || corner==3?rect.xMax-radius:rect.xMin+radius,
                    corner<2?rect.yMax-radius:rect.yMin+radius);
                for(int step=0;step<=segments;step++)
                {
                    float angle=(corner*90f+step*90f/segments)*Mathf.Deg2Rad;
                    var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    mesh.AddVert(centre+direction*radius,color,Vector2.zero);
                    mesh.AddVert(centre+direction*(radius-edge),color,Vector2.zero);
                }
            }
            for(int i=0;i<count;i++)
            {
                int outer=i*2,next=(i+1)%count*2;
                mesh.AddTriangle(outer,next,outer+1);
                mesh.AddTriangle(outer+1,next,next+1);
            }
        }
    }
}
