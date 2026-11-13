using System;
using System.IO;
using UnityEditor;

namespace CapstoneDesign.EditorTools
{
    public static class FreshMissionPreview
    {
        const string Menu="Capstone Mockup/Start Fresh Mission Preview";

        [MenuItem(Menu)]
        public static void Start()
        {
            if(!CanStart())return;
            // Each explicit test run gets its own retained save, never an erased account.
            string previewRoot=Path.GetFullPath(Path.Combine("Library","MissionPreviews",Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT",previewRoot);
            EditorApplication.isPlaying=true;
        }

        [MenuItem(Menu,true)]
        static bool CanStart()=>!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
    }
}
