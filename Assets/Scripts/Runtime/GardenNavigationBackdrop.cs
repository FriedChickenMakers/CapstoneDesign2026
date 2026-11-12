using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // A single timber image and UV frame for every tab. Garden clipping changes
    // only the perimeter mesh, so grain never jumps when the active page changes.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GardenNavigationBackdrop : MaskableGraphic
    {
        static Texture2D timber;
        public bool GardenMask { get; private set; }
        public override Texture mainTexture => timber!=null?timber:
            (timber=Resources.Load<Texture2D>("UI/Textures/LightWoodTitleUI"));

        public void SetGardenMask(bool enabled)
        {
            if(GardenMask==enabled)return;
            GardenMask=enabled;SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();Rect full=GetPixelAdjustedRect();
            if(full.width<=0 || full.height<=0)return;
            Rect visible=GardenMask?Rect.MinMaxRect(full.xMin+MissionBottomNavigation.SideInset,
                full.yMin+MissionBottomNavigation.BottomInset,full.xMax-MissionBottomNavigation.SideInset,full.yMax):full;
            if(visible.width<=0 || visible.height<=0)return;
            float radius=GardenMask?Mathf.Min(18,Mathf.Min(visible.width,visible.height)*.5f):0;
            const int segments=12, count=4*(segments+1);
            Vertex(mesh,visible.center,full);
            for(int corner=0;corner<4;corner++)
            {
                var centre=new Vector2(corner==0 || corner==3?visible.xMax-radius:visible.xMin+radius,
                    corner<2?visible.yMax-radius:visible.yMin+radius);
                for(int step=0;step<=segments;step++)
                {
                    float angle=(corner*90f+step*90f/segments)*Mathf.Deg2Rad;
                    Vertex(mesh,centre+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,full);
                }
            }
            for(int i=0;i<count;i++)mesh.AddTriangle(0,i+1,(i+1)%count+1);
        }

        void Vertex(VertexHelper mesh,Vector2 point,Rect full)
        {
            var texture=mainTexture;
            Vector2 uv=Vector2.zero;
            if(texture!=null)
            {
                // The final 32 pixels are the atlas's plain-color strip. Keep
                // the established shelf zoom, sampled against the full image
                // rectangle even when its visible garden silhouette is inset.
                float height=texture.height-32;
                float scale=Mathf.Min(texture.width/full.width,height/full.height)*(.72f/2.5f);
                Vector2 delta=(point-full.center)*scale;
                uv=new Vector2(.5f+delta.x/texture.width,(height*.5f+delta.y)/texture.height);
            }
            float nx=(point.x-full.xMin)/full.width,ny=(point.y-full.yMin)/full.height;
            float light=.945f+.035f*ny+.02f*(1-nx);
            Color tint=new Color(.96f*light,.91f*light,.83f*light,1)*color;
            mesh.AddVert(point,tint,uv);
        }
    }
}
