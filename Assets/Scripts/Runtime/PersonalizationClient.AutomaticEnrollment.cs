using System;
using System.IO;
using System.Text;
using CapstoneDesign.Runtime.Personalization;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public sealed partial class PersonalizationClient
    {
        // Only Android player builds contain this generated APK asset. Editor
        // fixtures never read production build configuration or auto-enroll.
        const string AutomaticEnrollmentAsset = "capstone-enrollment-config.json";
#if UNITY_EDITOR
        static Func<AutomaticEnrollmentRecord> automaticConfigOverride;
#endif
        static bool CreateAutomaticEnrollment(string directory)
        {
            var saved = new FileEventQueueStore(Path.Combine(directory, "behavior-queue.xml")).Load();
            // Losing credentials must not generate a different identity over an
            // existing participant, pending deletion or a revoked enrollment.
            if (saved == null || saved.DeletePending || saved.EnrollmentRevoked
                || !string.IsNullOrEmpty(saved.EnrollmentFingerprint) || saved.Enabled
                || saved.Events == null || saved.Events.Count != 0 || saved.Recorded == null
                || saved.Recorded.Count != 0) return false;
            var config = ReadAutomaticEnrollmentConfiguration();
            if (config == null) return false;
            var result = AutomaticEnrollment.EnsureCreated(directory, config.endpoint, config.automaticEnrollmentKey,
                value => JsonUtility.ToJson(value), json => JsonUtility.FromJson<AutomaticEnrollmentRecord>(json));
            return result != AutomaticEnrollmentResult.Blocked;
        }

        static AutomaticEnrollmentRecord ReadAutomaticEnrollmentConfiguration()
        {
#if UNITY_EDITOR
            return automaticConfigOverride?.Invoke();
#elif UNITY_ANDROID
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var assets = activity.Call<AndroidJavaObject>("getAssets"))
            using (var input = assets.Call<AndroidJavaObject>("open", AutomaticEnrollmentAsset))
            using (var decoder = new AndroidJavaObject("java.io.InputStreamReader", input, "UTF-8"))
            using (var reader = new AndroidJavaObject("java.io.BufferedReader", decoder))
            {
                try
                {
                    var json = new StringBuilder();
                    string line;
                    while ((line = reader.Call<string>("readLine")) != null)
                    {
                        if (json.Length + line.Length > 4096) throw new InvalidDataException("Invalid enrollment configuration size.");
                        json.Append(line);
                    }
                    return JsonUtility.FromJson<AutomaticEnrollmentRecord>(json.ToString());
                }
                finally { reader.Call("close"); }
            }
#else
            return null;
#endif
        }
    }
}
