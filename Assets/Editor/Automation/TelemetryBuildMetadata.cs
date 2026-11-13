using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace CapstoneDesign.EditorTools
{
    public sealed class TelemetryBuildMetadata : IPreprocessBuildWithReport,IPostprocessBuildWithReport
    {
        const string PathName="Assets/Resources/TelemetryBuildMetadata.json";
        public int callbackOrder=>900;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(File.Exists(PathName))throw new BuildFailedException("Remove the stale generated telemetry build metadata before building.");
            var source=new StringBuilder(PlayerSettings.bundleVersion+"\n");
            using(var sha=SHA256.Create())
            {
                foreach(var file in new[]{"Assets/Scripts","Assets/Plugins"}.Where(Directory.Exists)
                    .SelectMany(root=>Directory.GetFiles(root,"*",SearchOption.AllDirectories))
                    .Where(p=>new[]{".cs",".kt",".xml"}.Contains(Path.GetExtension(p))).OrderBy(p=>p,StringComparer.Ordinal))
                    source.Append(file.Replace('\\','/')).Append(':').Append(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file)))).Append('\n');
                string id=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source.ToString()))).Replace("-","").ToLowerInvariant();
                Directory.CreateDirectory("Assets/Resources");File.WriteAllText(PathName,"{\"source_id\":\""+id+"\"}");
            }
            AssetDatabase.ImportAsset(PathName,ImportAssetOptions.ForceSynchronousImport);
        }
        public void OnPostprocessBuild(BuildReport report){AssetDatabase.DeleteAsset(PathName);}
    }
}
