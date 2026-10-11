using System;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public static class ActivityNotificationBridge
    {
        const string Native="com.capstonedesign2026.platform.AndroidPlatformBridge";
        static string Call(string method,params object[] args)
        {
            if(AndroidPlatformBridge.InputMode!=PlatformInputMode.Live)return "{}";
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using(var bridge=new AndroidJavaClass(Native))return bridge.CallStatic<string>(method,args); }
            catch(Exception ex){Debug.LogWarning("Notification bridge unavailable: "+ex.GetType().Name);}
#endif
            return "{}";
        }
        static T Read<T>(string method,params object[] args) where T:new()
        {
            try { var value=JsonUtility.FromJson<T>(Call(method,args));return value==null?new T():value; }
            catch(ArgumentException) { return new T(); }
        }
        public static ActivityNotificationSettings Settings() => Read<ActivityNotificationSettings>("getActivityNotificationSettingsJson");
        public static WalkingAchievement Achievement() => Read<WalkingAchievement>("getWalkingAchievementJson");
        public static ActivityNotificationOpen ConsumeOpen() => Read<ActivityNotificationOpen>("consumeActivityNotificationOpen");
        public static PlatformActionResult Configure(bool enabled,int minute,bool adaptive) => Read<PlatformActionResult>("configureActivityNotifications",enabled,minute,adaptive);
        public static void Permission() => Call("requestActivityNotificationPermission");
        public static PlatformActionResult Sync(string completedDay,string title,bool active) => Read<PlatformActionResult>("syncActivityReminder",completedDay,title,active);
    }
}
