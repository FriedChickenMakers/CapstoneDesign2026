using System;
using System.IO;
using CapstoneDesign.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    public static class VisualAuditBuild
    {
        public static void Build()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("CAPSTONE_ARTIFACTS required");
            Directory.CreateDirectory(output);
            const string temporaryScene = "Assets/Editor/Automation/TemporaryVisualAudit.unity";
            string original = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                new GameObject("Explicit isolated visual audit").AddComponent<VisualAuditBootstrap>();
                EditorSceneManager.SaveScene(scene,temporaryScene);
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,VisualAuditBootstrap.Package);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { temporaryScene }, locationPathName = Path.Combine(output,"visual-audit.apk"),
                    target = BuildTarget.Android, options = BuildOptions.Development | BuildOptions.AllowDebugging
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Visual audit APK failed: "+report.summary.result);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,original);
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                AssetDatabase.DeleteAsset(temporaryScene);
            }
        }
    }
}
