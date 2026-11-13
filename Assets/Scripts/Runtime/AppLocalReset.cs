using System;
using System.IO;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    // Called only after the garden's durable reset journal has committed. Each
    // step is repeatable; failures leave that journal pending for next startup.
    public static class AppLocalReset
    {
        public static PlatformActionResult Cleanup(string requestId, string activeStateFile)
        {
            try
            {
                var personal = PersonalizationClient.ResetLocalDataForAppReset(requestId);
                if (personal == null || personal.status != "AVAILABLE") return personal;
                var native = AndroidPlatformBridge.ResetLocalData(requestId);
                if (native == null || native.status != "AVAILABLE") return native;
                ResetOtherModes(requestId, activeStateFile);
                AndroidPlatformBridge.StopSnapshotRecording();
                UiPerformanceProbe.DiscardForFullReset();
                foreach (var probe in UnityEngine.Object.FindObjectsByType<MusicDeviceProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    probe.enabled = false;
                // Only app-owned defaults: caller-provided replay/recording paths
                // outside the app directory do not authorize recursive deletion.
                foreach (string name in new[] { "platform_snapshots.jsonl", "ui-profile.json", "capstone-music-probe.jsonl" })
                {
                    string path = Path.Combine(Application.persistentDataPath, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                bool debugAuthorized=DebugAccess.Authorized;
                PlayerPrefs.DeleteAll();
                if(debugAuthorized)PlayerPrefs.SetInt("capstone.debug.authorized.v1",1);
                PlayerPrefs.Save();
                GardenMusicPlayer.RestoreInitialSettings();
                GardenSfxPlayer.RestoreInitialSettings();
                GardenVisualClock.UseSystemTime();
                return new PlatformActionResult { status = "AVAILABLE", message = "App local data reset." };
            }
            catch (Exception)
            {
                return new PlatformActionResult { status = "ERROR", message = "Local reset cleanup must be retried." };
            }
        }

        static void ResetOtherModes(string requestId, string activeStateFile)
        {
            string active = Path.GetFullPath(activeStateFile);
            string root = Directory.GetParent(Path.GetDirectoryName(active)).FullName;
            foreach (string mode in new[] { "live", "mock", "replay" })
            {
                string path = Path.GetFullPath(Path.Combine(root, "garden-" + mode, "state.xml"));
                if (string.Equals(path, active, StringComparison.OrdinalIgnoreCase)) continue;
                bool exists = false;
                foreach (string suffix in new[] { "", ".bak", ".tmp", ".reset", ".reset.tmp" })
                    exists |= File.Exists(path + suffix) || Directory.Exists(path + suffix);
                if (!exists) continue;
                using (var store = new FileStateStore(path))
                    store.ResetInactiveForFullReset(StateCodec.Hash(requestId + "|" + mode));
            }
        }
    }
}
