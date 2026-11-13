using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.Personalization;

namespace CapstoneDesign.Runtime
{
    // Only structural identifiers and geometry. Never labels, input text or pixels.
    public sealed class QaLayoutTelemetry : MonoBehaviour
    {
        public string ScreenId;
        float next;
        string last,stable;
        int emitted;
        [Serializable] sealed class Control { public string id,visibility; public int[] bounds; }
        [Serializable] sealed class Layout { public string screen_id; public int width,height,scale_milli;public int[] safe_area;public Control[] controls; }
        static int[] Bounds(Rect r)=>new[]{Mathf.RoundToInt(r.x),Mathf.RoundToInt(r.y),Mathf.RoundToInt(r.width),Mathf.RoundToInt(r.height)};
        static Rect ScreenRect(RectTransform transform,Camera camera)
        {
            var corners=new Vector3[4];transform.GetWorldCorners(corners);
            var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(camera,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        static Rect Intersect(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
        void Update()
        {
            if(!Application.isPlaying || !DebugAccess.Qa || Time.unscaledTime<next || emitted>=12)return;
            next=Time.unscaledTime+2;
            if(!Regex.IsMatch(ScreenId??"","^[A-Za-z0-9._-]{1,64}$"))return;
            var canvas=GetComponentInParent<Canvas>();if(canvas==null)return;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var screen=new Rect(0,0,Screen.width,Screen.height);
            var controls=new List<Control>();
            foreach(var button in GetComponentsInChildren<Button>(false))
            {
                if(!Regex.IsMatch(button.name,"^(Focus|Guided|Basic|Settings|GardenTime|M01)[.][A-Za-z0-9._-]{1,48}$"))continue;
                var bounds=ScreenRect((RectTransform)button.transform,camera);var visible=Intersect(bounds,screen);
                foreach(var mask in button.GetComponentsInParent<RectMask2D>(false))
                    if(mask.enabled)visible=Intersect(visible,ScreenRect((RectTransform)mask.transform,camera));
                bool hidden=visible.width<=0 || visible.height<=0;
                bool required=button.name=="Focus.TimerToggle"||button.name=="Guided.ReflectionToggle"||button.name=="Focus.TooLong";
                if(hidden && !required)continue; // Offscreen scroll content is expected.
                string status=hidden?"hidden":visible.width<bounds.width-1 || visible.height<bounds.height-1?"partial":"visible";
                controls.Add(new Control{id=button.name,bounds=Bounds(bounds),visibility=status});
            }
            var layout=new Layout{screen_id=ScreenId,width=Screen.width,height=Screen.height,safe_area=Bounds(Screen.safeArea),
                scale_milli=Mathf.Max(1,Mathf.RoundToInt(canvas.scaleFactor*1000)),controls=controls.OrderBy(c=>c.visibility=="visible").ThenBy(c=>c.id).Take(16).ToArray()};
            string json=JsonUtility.ToJson(layout),signature=EventQueue.Fingerprint(ScreenId,json);
            if(stable!=signature){stable=signature;return;}
            if(last==signature)return;
            if(PersonalizationClient.Instance.RecordLayout(ScreenId,json,signature)){last=signature;emitted++;}
        }
    }
}
