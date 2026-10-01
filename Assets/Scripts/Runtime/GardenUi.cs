using System;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Shared relative layout: usable at phone and tablet aspect ratios.
    public static class GardenUi
    {
        public static Font Font;
        public static Font ResolveFont() => Font != null ? Font : (Font = Resources.Load<Font>("Fonts/NanumGothic-Regular"));
        public static readonly Color Ink = new Color(.88f,.94f,.93f);
        public static GameObject Box(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(x,y); r.anchorMax = new Vector2(x+w,y+h);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            if (color.HasValue) go.AddComponent<Image>().color = color.Value;
            return go;
        }
        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size = 28)
        {
            var t = Box(parent,"Label",x,y,w,h).AddComponent<Text>();
            t.font = ResolveFont() ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text; t.color = Ink; t.fontSize=size; t.alignment=TextAnchor.MiddleLeft;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate;
            t.raycastTarget=false;
            return t;
        }
        public static Button Button(Transform parent, string label, float x, float y, float w, float h, Action action)
        {
            var go = Box(parent,label,x,y,w,h,new Color(.15f,.28f,.30f,.98f));
            var b = go.AddComponent<Button>(); b.targetGraphic=go.GetComponent<Image>();
            var t=Label(go.transform,label,.05f,0,.9f,1,28); t.alignment=TextAnchor.MiddleCenter;
            b.onClick.AddListener(()=>action()); return b;
        }
        public static InputField Input(Transform parent, string hint, float x,float y,float w,float h)
        {
            var go=Box(parent,"Optional note",x,y,w,h,new Color(.09f,.16f,.21f));
            var input=go.AddComponent<InputField>();
            input.textComponent=Label(go.transform,"",.03f,.05f,.94f,.9f,26);
            input.placeholder=Label(go.transform,hint,.03f,.05f,.94f,.9f,26);
            input.characterLimit=500; input.lineType=InputField.LineType.MultiLineNewline;
            return input;
        }
    }
}
