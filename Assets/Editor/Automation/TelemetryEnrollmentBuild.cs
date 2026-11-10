using System;
using System.IO;
using System.Text;
using CapstoneDesign.Runtime.Personalization;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Keep admission material outside Assets and Git. Embed it only into the
    // generated Android Gradle project, including builds from Unity's normal UI.
    public sealed class TelemetryEnrollmentBuild : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        [Serializable] sealed class Configuration { public string endpoint, automaticEnrollmentKey; }
        const string AssetName = "capstone-enrollment-config.json";
        static Configuration prepared;
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            prepared = null;
            if (report.summary.platform == BuildTarget.Android) prepared = ReadConfiguration();
        }

        internal static string ConfigurationPath()
        {
            var path = Environment.GetEnvironmentVariable("CAPSTONE_TELEMETRY_BUILD_CONFIG");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "capstone", "telemetry-build.json");
            var full = Path.GetFullPath(path);
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!Path.IsPathRooted(path) || full.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Telemetry build configuration must be an absolute private path outside the repository.");
            // Reject aliases instead of depending on APIs missing from Unity's
            // managed profile. A symlink/junction outside Assets can otherwise
            // point at a versioned file inside it, especially across platforms.
            for (var current = full; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new BuildFailedException("Telemetry build configuration must not use symbolic links or junctions.");
            return full;
        }

        static Configuration ReadConfiguration()
        {
            try
            {
                var path = ConfigurationPath();
                if (!File.Exists(path) || new FileInfo(path).Length > 4096) throw new InvalidDataException();
                var candidate = JsonUtility.FromJson<AutomaticEnrollmentRecord>(File.ReadAllText(path));
                if (candidate == null || !string.IsNullOrEmpty(candidate.token)
                    || !AutomaticEnrollment.ValidConfiguration(candidate.endpoint, candidate.automaticEnrollmentKey))
                    throw new InvalidDataException();
                return new Configuration { endpoint = candidate.endpoint.TrimEnd('/'), automaticEnrollmentKey = candidate.automaticEnrollmentKey };
            }
            catch (Exception)
            {
                // Do not attach a parse exception: it may contain secret input.
                throw new BuildFailedException("Automatic telemetry configuration is missing or invalid. Set CAPSTONE_TELEMETRY_BUILD_CONFIG to a private JSON file; see docs/AUTOMATIC_TELEMETRY_ENROLLMENT.md.");
            }
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var configuration = prepared ?? ReadConfiguration();
            var directory = Path.Combine(path, "src", "main", "assets");
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, AssetName);
            var json = JsonUtility.ToJson(configuration);
            // Always overwrite generated content, including incremental builds
            // after endpoint/key rotation. No source asset is created or changed.
            File.WriteAllText(destination, json, new UTF8Encoding(false));
            if (File.ReadAllText(destination) != json) throw new BuildFailedException("Telemetry APK asset verification failed.");
            Debug.Log("AUTOMATIC_TELEMETRY_BUILD_CONFIG_EMBEDDED");
        }
    }
}
