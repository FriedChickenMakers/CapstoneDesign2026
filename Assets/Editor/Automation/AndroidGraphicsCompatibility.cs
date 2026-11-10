using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.EditorTools
{
    // Device-specific workaround for the reproduced A7 Android 10 vendor Vulkan
    // crashes. deviceName is the GPU name; productName is Android Build.PRODUCT.
    public static class AndroidGraphicsCompatibility
    {
        const string FilterPath = "Assets/Settings/AndroidVulkanDeviceFilters.vulkandevicefilter";
        static VulkanDeviceFilterData A7Rule => new VulkanDeviceFilterData
        {
            vendorName = "^Qualcomm$", deviceName = @"^Adreno \(TM\) 610$",
            brandName = "^samsung$", productName = "^gta4lwifixx$",
            androidOsVersionString = "^10$", vulkanApiVersionString = "", driverVersionString = ""
        };
        [Serializable] sealed class Report
        {
            public string status, assetPath, nativeDefaultSource;
            public bool autoBefore, autoAfter;
            public string[] apisBefore, apisAfter;
            public int denyBefore, denyAfter, allowBefore, allowAfter, jobsBefore, jobsAfter;
            public int unityDefaultDenyCount;
            public VulkanDeviceFilterData addedRule;
            public List<string> checks = new List<string>();
        }

        public static void ConfigureA7()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("Explicit batch mode and CAPSTONE_ARTIFACTS are required.");
            var report = new Report { autoBefore = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), addedRule = A7Rule };
            var originalApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            report.apisBefore = originalApis.Select(api => api.ToString()).ToArray();
            var filter = PlayerSettings.Android.androidVulkanDeviceFilterListAsset;
            bool create = filter == null;
            if (create)
            {
                Require(AssetDatabase.LoadMainAssetAtPath(FilterPath) == null, "An unassigned filter asset already exists; review it before replacing anything.");
                // The constructor alone returns empty lists in 6000.3.24f1.
                // Merge the verified version-pinned native defaults below.
                filter = new VulkanDeviceFilterLists("Android Vulkan compatibility");
            }
            var deny = (filter.vulkanDeviceDenyFilters ?? Array.Empty<VulkanDeviceFilterData>()).ToList();
            var allow = (filter.vulkanDeviceAllowFilters ?? Array.Empty<VulkanDeviceFilterData>()).ToList();
            var jobs = filter.vulkanGraphicsJobsDeviceFilters ?? Array.Empty<VulkanGraphicsJobsDeviceFilterData>();
            report.denyBefore = deny.Count; report.allowBefore = allow.Count; report.jobsBefore = jobs.Length;
            var oldDeny = deny.Select(Key).ToArray(); var oldAllow = allow.Select(Key).ToArray();
            var oldJobs = jobs.Select(item => JsonUtility.ToJson(item)).ToArray();
            var defaults = DefaultDenyFilters();
            report.unityDefaultDenyCount = defaults.Length;
            report.nativeDefaultSource = "version-pinned checked: Unity 6000.3.24f1 Android libunity VulkanDeviceFilterLists::GetDefaultDeviceDenyList";
            Require(Matches(defaults[0], "ARM", "Mali", "Google", "oriole", "13")
                && Matches(defaults[0], "ARM", "Mali", "google", "bluejay", "14")
                && !Matches(defaults[0], "ARM", "Mali", "Google", "other-product", "13"),
                "Pinned Google default restriction changed unexpectedly.");
            foreach (var item in defaults) AddUnique(deny, item);
            if (create) PreserveLegacyFilters(deny, allow);
            // An existing allow override needs a deliberate review, not silent removal.
            Require(!allow.Any(rule => Matches(rule, "Qualcomm", "Adreno (TM) 610", "samsung", "gta4lwifixx", "10")),
                "Existing allow criteria may override the A7 deny rule; review before configuring.");
            AddUnique(deny, A7Rule);
            filter.vulkanDeviceDenyFilters = deny.ToArray();
            filter.vulkanDeviceAllowFilters = allow.ToArray();
            filter.EnsureValidOrThrow();

            Require(Matches(A7Rule, "Qualcomm", "Adreno (TM) 610", "samsung", "gta4lwifixx", "10"), "Observed A7 must match.");
            Require(!Matches(A7Rule, "Qualcomm", "Adreno (TM) 750", "samsung", "e3qxxx", "14"), "Other Samsung phones must not match.");
            Require(!Matches(A7Rule, "Qualcomm", "Adreno (TM) 610", "other", "gta4lwifixx", "10"), "Other brands must not match.");
            Require(!Matches(A7Rule, "Qualcomm", "Adreno (TM) 610", "samsung", "other-product", "10"), "Other Adreno 610 products must not match.");
            Require(!Matches(A7Rule, "Qualcomm", "Adreno (TM) 610", "samsung", "gta4lwifixx", "11"), "Unverified newer OS must not match.");
            Require(oldDeny.All(key => deny.Any(item => Key(item) == key)) && oldAllow.All(key => allow.Any(item => Key(item) == key)),
                "Existing/default allow and deny entries must remain.");
            Require(defaults.All(item => deny.Any(candidate => Key(candidate) == Key(item))), "Unity default deny entries must remain.");
            Require(oldJobs.SequenceEqual(filter.vulkanGraphicsJobsDeviceFilters.Select(item => JsonUtility.ToJson(item))), "Graphics jobs filters must remain unchanged.");
            int count = deny.Count; AddUnique(deny, A7Rule);
            Require(deny.Count == count, "Applying the rule twice must be idempotent.");
            report.checks.AddRange(new[] { "Observed GPU/product/OS matches; other brands/products/OS do not", "Existing/default and legacy filter entries preserved", "Graphics-jobs entries unchanged", "Duplicate rule not added" });

            // Auto previously selected Vulkan on A7. Make the known fallback
            // explicit; never disable Vulkan globally or alter quality settings.
            var apis = report.autoBefore
                ? new List<GraphicsDeviceType> { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 }
                : originalApis.ToList();
            if (!apis.Contains(GraphicsDeviceType.OpenGLES3)) apis.Add(GraphicsDeviceType.OpenGLES3);
            Require(apis.Contains(GraphicsDeviceType.Vulkan), "Vulkan was already disabled; do not change that policy implicitly.");
            if (create) AssetDatabase.CreateAsset(filter, FilterPath);
            EditorUtility.SetDirty(filter);
            AssetDatabase.SaveAssetIfDirty(filter); // Do not save unrelated dynamic font/cache assets.
            PlayerSettings.Android.androidVulkanDeviceFilterListAsset = filter;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, apis.ToArray());

            report.autoAfter = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            report.apisAfter = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(api => api.ToString()).ToArray();
            report.assetPath = AssetDatabase.GetAssetPath(filter);
            report.denyAfter = filter.vulkanDeviceDenyFilters.Length;
            report.allowAfter = filter.vulkanDeviceAllowFilters.Length;
            report.jobsAfter = filter.vulkanGraphicsJobsDeviceFilters.Length;
            Require(!report.autoAfter && report.apisAfter.Contains("Vulkan") && report.apisAfter.Contains("OpenGLES3"), "Explicit Vulkan and GLES3 fallback required.");
            Require(PlayerSettings.Android.androidVulkanDeviceFilterListAsset == filter, "Filter assignment missing.");
            report.checks.Add("Filter assigned; Vulkan retained and OpenGLES3 fallback explicit");
            report.status = "pass";
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "android-graphics-compatibility.json"), JsonUtility.ToJson(report, true));
            Debug.Log("ANDROID_GRAPHICS_COMPATIBILITY_PASS " + report.assetPath + "; actual A7 GLES startup/resume validation remains required.");
        }

        static string Key(VulkanDeviceFilterData rule) => JsonUtility.ToJson(rule);
        static VulkanDeviceFilterData[] DefaultDenyFilters()
        {
            // Verified against this installed Editor's Android Release libunity.so
            // and matching libunity.sym.so, GetDefaultDeviceDenyList. Its sole
            // implicit restriction is Google oriole/raven/bluejay. A nonempty
            // custom asset bypasses that list, so retain it explicitly. Both the
            // managed constructor and Editor SmartReset returned empty arrays.
            // Re-audit the native defaults before using a newer Unity version.
            Require(Application.unityVersion == "6000.3.24f1",
                "Re-audit Unity native Vulkan defaults before changing the pinned Editor version.");
            return new[]
            {
                new VulkanDeviceFilterData
                {
                    vendorName = "", deviceName = "", brandName = "((G|g)oogle)",
                    productName = "(oriole|raven|bluejay)", androidOsVersionString = "",
                    vulkanApiVersionString = "", driverVersionString = ""
                }
            };
        }
        static void AddUnique(List<VulkanDeviceFilterData> target, VulkanDeviceFilterData item)
        { if (!target.Any(existing => Key(existing) == Key(item))) target.Add(item); }
        static bool Matches(VulkanDeviceFilterData rule, string vendor, string gpu, string brand, string product, string os)
            => Match(rule.vendorName, vendor) && Match(rule.deviceName, gpu) && Match(rule.brandName, brand)
                && Match(rule.productName, product) && Match(rule.androidOsVersionString, os);
        static bool Match(string pattern, string value) => string.IsNullOrEmpty(pattern) || Regex.IsMatch(value, pattern);

#pragma warning disable CS0618 // Read-only migration of existing legacy configuration.
        static void PreserveLegacyFilters(List<VulkanDeviceFilterData> deny, List<VulkanDeviceFilterData> allow)
        {
            foreach (var item in PlayerSettings.Android.androidVulkanDenyFilterList ?? Array.Empty<AndroidDeviceFilterData>()) AddUnique(deny, Convert(item));
            foreach (var item in PlayerSettings.Android.androidVulkanAllowFilterList ?? Array.Empty<AndroidDeviceFilterData>()) AddUnique(allow, Convert(item));
        }
        static VulkanDeviceFilterData Convert(AndroidDeviceFilterData item) => new VulkanDeviceFilterData
        {
            vendorName = item.vendorName, deviceName = item.deviceName, brandName = item.brandName,
            productName = item.productName, androidOsVersionString = item.androidOsVersionString,
            vulkanApiVersionString = item.vulkanApiVersionString, driverVersionString = item.driverVersionString
        };
#pragma warning restore CS0618
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
