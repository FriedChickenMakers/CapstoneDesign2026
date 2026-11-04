using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Plants;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Attached to the existing garden RawImage so overlay controls own their taps.
    public sealed class GardenMemoryPicker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public GardenHomePresenter Home;
        readonly HashSet<int> pointers=new HashSet<int>();
        bool multiTouch;
        void Update()
        {
            if(pointers.Count==0)return;
#if ENABLE_INPUT_SYSTEM
            var screen=UnityEngine.InputSystem.Touchscreen.current;
            if(screen!=null && screen.touches.Count(t=>t.press.isPressed)>1)multiTouch=true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if(Input.touchCount>1)multiTouch=true;
#endif
        }
        public void OnPointerDown(PointerEventData e) { if(pointers.Count==0)multiTouch=false;pointers.Add(e.pointerId);if(pointers.Count>1)multiTouch=true; }
        public void OnPointerUp(PointerEventData e) { pointers.Remove(e.pointerId); }
        void OnDisable() {pointers.Clear();multiTouch=false;}
        public void OnPointerClick(PointerEventData e)
        {
            if(Home==null || !Home.IsGardenVisible || multiTouch || e.button!=PointerEventData.InputButton.Left || e.dragging
                || Vector2.Distance(e.pressPosition,e.position)>10)return;
            var view=Home.ProceduralPlant;var camera=Home.gardenCamera;
            if(view?.Geometry==null || camera==null)return;
            var rect=(RectTransform)transform;var image=GetComponent<RawImage>();
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var local) || !rect.rect.Contains(local))return;
            var uv=new Vector2((local.x-rect.rect.xMin)/rect.rect.width,(local.y-rect.rect.yMin)/rect.rect.height);
            uv=image.uvRect.min+Vector2.Scale(uv,image.uvRect.size);
            int triangle=Hit(view,camera.ViewportPointToRay(uv));
            if(Home.IsOverview){if(triangle>=0)Home.SetOverview(false);return;}
            var direct=view.Geometry.Organs.FirstOrDefault(o=>o.Selectable && triangle>=o.FirstTriangle && triangle<o.FirstTriangle+o.TriangleCount);
            if(direct!=null){Home.OpenMemories(new[]{direct.OrganId});return;}
            var candidates=new List<string>();
            foreach(var organ in view.Geometry.Organs.Where(o=>o.Selectable))
            {
                var center=view.transform.TransformPoint(V(organ.Center));var projected=camera.WorldToViewportPoint(center);
                if(projected.z<=0)continue;
                var pos=new Vector2((projected.x-image.uvRect.x)/image.uvRect.width,(projected.y-image.uvRect.y)/image.uvRect.height);
                if(pos.x<0||pos.x>1||pos.y<0||pos.y>1)continue;
                var world=rect.TransformPoint(new Vector3(rect.rect.xMin+pos.x*rect.rect.width,rect.rect.yMin+pos.y*rect.rect.height));
                if(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(e.pressEventCamera,world),e.position)>28)continue;
                // A rear leaf is not selectable through the plant's front surface.
                int first=Hit(view,camera.ViewportPointToRay(projected));
                if(first>=organ.FirstTriangle && first<organ.FirstTriangle+organ.TriangleCount)candidates.Add(organ.OrganId);
            }
            if(candidates.Count>0)Home.OpenMemories(candidates.ToArray());
        }
        static Vector3 V(PlantVector p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        static int Hit(ProceduralPlantView view,Ray ray)
        {
            var origin=view.transform.InverseTransformPoint(ray.origin);
            var direction=view.transform.InverseTransformVector(ray.direction).normalized;
            float nearest=float.PositiveInfinity;int hit=-1;var vertices=view.Geometry.Vertices;
            for(int i=0;i<vertices.Count;i+=3)
            {
                var a=V(vertices[i]);var ab=V(vertices[i+1])-a;var ac=V(vertices[i+2])-a;
                var p=Vector3.Cross(direction,ac);float det=Vector3.Dot(ab,p);
                if(Mathf.Abs(det)<.0000001f)continue;
                var t=origin-a;float u=Vector3.Dot(t,p)/det;if(u<0||u>1)continue;
                var q=Vector3.Cross(t,ab);float v=Vector3.Dot(direction,q)/det;if(v<0||u+v>1)continue;
                float distance=Vector3.Dot(ac,q)/det;
                if(distance>=0&&distance<nearest){nearest=distance;hit=i/3;}
            }
            return hit;
        }
    }
}
