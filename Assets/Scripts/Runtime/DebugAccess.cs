using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Local presentation gate, remembered for this installation. No password is stored.
    public static class DebugAccess
    {
        const string AuthorizedKey="capstone.debug.authorized.v1", EnabledKey="capstone.debug.enabled.v1", QaKey="capstone.debug.qa.v1";
        const string Verifier="ad3ab449499c5819aa272573e27d576766389293953a90e3c571eb7c57a0ade0";
        public static event Action Changed;
#if UNITY_EDITOR
        public static Func<string,int,int> ReadForValidation;
        public static Func<string,bool> VerifyForValidation;
        public static Action<string,int> WriteForValidation;
#endif
        static int Read(string key,int fallback=0)
        {
#if UNITY_EDITOR
            if(ReadForValidation!=null)return ReadForValidation(key,fallback);
#endif
            return PlayerPrefs.GetInt(key,fallback);
        }
        static void Write(string key,int value)
        {
#if UNITY_EDITOR
            if(WriteForValidation!=null){WriteForValidation(key,value);return;}
#endif
            PlayerPrefs.SetInt(key,value);
        }
        public static bool Authorized=>Read(AuthorizedKey)==1;
        public static bool Enabled=>Authorized && Read(EnabledKey)==1;
        public static bool Qa=>Enabled && Read(QaKey)==1;
        public static bool Matches(string value)
        {
#if UNITY_EDITOR
            if(VerifyForValidation!=null)return VerifyForValidation(value);
#endif
            if(value==null || value.Length!=8)return false;
            using(var hash=SHA256.Create())
            {
                string actual=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("capstone-local-demo-gate-v1:"+value))).Replace("-","").ToLowerInvariant();
                int different=0;for(int i=0;i<Verifier.Length;i++)different|=actual[i]^Verifier[i];
                return different==0;
            }
        }
        public static bool Enter(string password=null)
        {
            if(!Authorized && !Matches(password))return false;
            if(!NativeState(true,true))return false;
            Write(AuthorizedKey,1);Write(EnabledKey,1);Write(QaKey,1);Save();Changed?.Invoke();return true;
        }
        public static bool SetQa(bool enabled)
        {
            if(!Enabled || !NativeState(true,enabled))return false;
            Write(QaKey,enabled?1:0);Save();Changed?.Invoke();return true;
        }
        public static bool Exit()
        {
            if(!NativeState(false,false))return false;
            Write(EnabledKey,0);Write(QaKey,0);Save();Changed?.Invoke();return true;
        }
        public static bool SynchronizeNative()=>NativeState(Enabled,Qa);
        static void Save()
        {
#if UNITY_EDITOR
            if(WriteForValidation!=null)return;
#endif
            PlayerPrefs.Save();
        }
        static bool NativeState(bool enabled,bool qa)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try{using(var bridge=new AndroidJavaClass("com.capstonedesign2026.platform.AndroidPlatformBridge"))
                return bridge.CallStatic<string>("configureDebugTelemetry",enabled,qa,TelemetryMeasurementContext.BuildId).Contains("\"ok\":true");}
            catch(Exception){return false;}
#else
            return true;
#endif
        }
    }
}
