using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    internal static class SyntheticPortraitGameView
    {
        const BindingFlags ViewMembers=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        static Type GameViewType => typeof(Editor).Assembly.GetType("UnityEditor.GameView",true);
        static object GameViewSizes()
        {
            var type=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes",true);
            return typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        }
        public static void Set(string key)
        {
            // A portrait app needs a portrait player surface, independently of the
            // batch Editor's usual 640x480 window. Do not persist a custom preset.
            if(SessionState.GetBool(key+".ViewPending",false))Restore(key);
            var views=Resources.FindObjectsOfTypeAll(GameViewType);
            var view=views.Length>0?(EditorWindow)views[0]:EditorWindow.GetWindow(GameViewType);
            var sizes=GameViewSizes();
            var group=sizes.GetType().GetProperty("currentGroup").GetValue(sizes);
            string label="Smoke "+Guid.NewGuid().ToString("N");
            SessionState.SetInt(key+".ViewId",view.GetInstanceID());
            SessionState.SetBool(key+".ViewCreated",views.Length==0);
            SessionState.SetInt(key+".ViewSelection",(int)GameViewType.GetProperty("selectedSizeIndex",ViewMembers).GetValue(view));
            SessionState.SetInt(key+".ViewGroup",Convert.ToInt32(sizes.GetType().GetProperty("currentGroupType").GetValue(sizes)));
            SessionState.SetString(key+".ViewLabel",label);
            SessionState.SetBool(key+".ViewPending",true);
            var editor=typeof(Editor).Assembly;
            var fixedSize=Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType",true),"FixedResolution");
            var size=Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize",true),fixedSize,390,844,label);
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            GameViewType.GetMethod("SizeSelectionCallback",ViewMembers).Invoke(view,new object[]{index,null});
            view.Repaint();
        }
        public static void Restore(string key)
        {
            if(!SessionState.GetBool(key+".ViewPending",false))return;
            // SessionState survives EnterPlaymode's domain reload. Identify our
            // temporary preset by name so unrelated presets cannot be removed.
            var sizes=GameViewSizes();
            var groupType=typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeGroupType",true);
            var group=sizes.GetType().GetMethod("GetGroup").Invoke(sizes,new[]{Enum.ToObject(groupType,SessionState.GetInt(key+".ViewGroup",0))});
            var view=EditorUtility.InstanceIDToObject(SessionState.GetInt(key+".ViewId",0)) as EditorWindow;
            if(view==null)throw new InvalidOperationException("Smoke Game view disappeared before restoration.");
            GameViewType.GetMethod("SizeSelectionCallback",ViewMembers).Invoke(view,new object[]{SessionState.GetInt(key+".ViewSelection",0),null});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            for(int index=count-1;index>=0;index--)
            {
                var size=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{index});
                if((string)size.GetType().GetProperty("baseText").GetValue(size)!=SessionState.GetString(key+".ViewLabel",""))continue;
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{index});
            }
            if(SessionState.GetBool(key+".ViewCreated",false))view.Close();else view.Repaint();
            SessionState.SetBool(key+".ViewPending",false);
        }
    }
}
