using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.Personalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Synchronous real Awake/initialization/recreation gate. Each client is
    // destroyed before Update, so no synthetic credential is sent over a network.
    [InitializeOnLoad]
    public static class AutomaticEnrollmentPlayValidation
    {
        const string Key = "Capstone.AutomaticEnrollmentPlayValidation";
        const BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        [Serializable] sealed class Report
        {
            public string status = "FAIL", error, failedCheck;
            public bool networkRequestsStarted, staticStateRestored;
            public List<string> checks = new List<string>();
        }
        static readonly FieldInfo ConfigHook = typeof(PersonalizationClient).GetField("automaticConfigOverride", StaticFlags);
        static readonly FieldInfo MeasurementHook = typeof(PersonalizationClient).GetField("measurementCallOverride", StaticFlags);
        static readonly FieldInfo Blocked = typeof(PersonalizationClient).GetField("localResetBlocked", StaticFlags);
        static readonly FieldInfo Instance = typeof(PersonalizationClient).GetField("instance", StaticFlags);
        static readonly FieldInfo Request = typeof(PersonalizationClient).GetField("activeRequest", InstanceFlags);
        static readonly MethodInfo Initialize = typeof(PersonalizationClient).GetMethod("InitializeEnrollment", InstanceFlags);
        static Report report;
        static string root;

        static AutomaticEnrollmentPlayValidation()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += WaitForPlay;
        }
        public static void Start()
        {
            string directory = Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory)
                || !Directory.Exists(directory) || Directory.GetFileSystemEntries(directory).Length != 0
                || string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output)
                || File.Exists(Path.Combine(output, "automatic-enrollment-play.json")))
                throw new InvalidOperationException("Use new explicit isolated test-root and artifact directories.");
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 90);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WaitForPlay;
            EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
            { Finish(new TimeoutException()); return; }
            if (!EditorApplication.isPlaying) return;
            EditorApplication.update -= WaitForPlay;
            Exception failure = null;
            try { Run(); }
            catch (Exception error) { failure = error; }
            Finish(failure);
        }
        static void Check(bool condition, string name)
        { if (!condition) { report.failedCheck = name; throw new InvalidOperationException(name); } report.checks.Add(name); }
        static void DestroyClient()
        {
            var current = Instance.GetValue(null) as PersonalizationClient;
            if (current != null)
            {
                if (Request.GetValue(current) != null) report.networkRequestsStarted = true;
                UnityEngine.Object.DestroyImmediate(current.gameObject);
            }
            Instance.SetValue(null, null);
        }
        static string UseCase(string name)
        {
            DestroyClient();
            Blocked.SetValue(null, false);
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT", directory);
            ConfigHook.SetValue(null, (Func<AutomaticEnrollmentRecord>)(() => new AutomaticEnrollmentRecord
                { endpoint = "https://telemetry.example.test", automaticEnrollmentKey = new string('a', 64) }));
            return directory;
        }
        static string Enrollment(string directory) => Path.Combine(directory, AutomaticEnrollment.FileName);
        static void CheckDeletionHeader(PersonalizationClient client, bool automatic)
        {
            var method = typeof(PersonalizationClient).GetMethod("Request", InstanceFlags);
            using (var request = (UnityEngine.Networking.UnityWebRequest)method.Invoke(client, new object[] { "DELETE", "/participant", null }))
            {
                Check(automatic ? request.GetRequestHeader("X-Enrollment-Key") == new string('a', 64)
                    : string.IsNullOrEmpty(request.GetRequestHeader("X-Enrollment-Key")),
                    automatic ? "offline-auto-withdrawal-includes-admission-header" : "manual-withdrawal-remains-bearer-only");
            }
            Request.SetValue(client, null); // Built for inspection only; never sent.
        }
        static FileEventQueueStore Store(string directory) => new FileEventQueueStore(Path.Combine(directory, "behavior-queue.xml"));
        static void Run()
        {
            report = new Report();
            root = Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            object priorConfig = ConfigHook.GetValue(null), priorMeasurements = MeasurementHook.GetValue(null), priorBlocked = Blocked.GetValue(null);
            if (Instance.GetValue(null) as PersonalizationClient != null)
                throw new InvalidOperationException("Existing client must not be touched by an isolated gate.");
            try
            {
                MeasurementHook.SetValue(null, (Func<string, object[], string>)((method, args) => "{\"ok\":true}"));
                var first = UseCase("offline-first-start");
                var client = PersonalizationClient.Instance;
                Check(client.Configured && client.Consented && client.MeasurementConsented, "first-start-activates-local-collection-without-network");
                string firstBytes = File.ReadAllText(Enrollment(first));
                var firstRecord = JsonUtility.FromJson<AutomaticEnrollmentRecord>(firstBytes);
                Check(AutomaticEnrollment.IsLowerHex64(firstRecord.token), "first-start-persists-canonical-per-install-token");
                Check(client.Record("recommendation_shown", Guid.NewGuid().ToString("N"), "course:MBCT2W01",
                    new[] { 1f, .5f, .25f, 1f }, "local-rules-v1"), "offline-behavior-record-persists-before-server-registration");
                long boundary = client.ConsentStartedUtcMs;
                DestroyClient(); client = PersonalizationClient.Instance;
                Check(File.ReadAllText(Enrollment(first)) == firstBytes && client.ConsentStartedUtcMs == boundary
                    && client.PendingCount == 1 && client.MeasurementConsented, "restart-keeps-identity-boundary-and-offline-outbox");

                var manual = UseCase("manual-preserved");
                const string manualBytes = "{\"endpoint\":\"https://manual.example.test\",\"token\":\"synthetic-manual-participant-token\"}";
                File.WriteAllText(Enrollment(manual), manualBytes);
                ConfigHook.SetValue(null, (Func<AutomaticEnrollmentRecord>)(() => { throw new InvalidOperationException("Manual config was overridden"); }));
                client = PersonalizationClient.Instance;
                Check(client.Configured && client.MeasurementConsented && File.ReadAllText(Enrollment(manual)) == manualBytes,
                    "existing-manual-registration-bypasses-automatic-config-and-keeps-bytes");
                CheckDeletionHeader(client, false);

                var missing = UseCase("bound-queue-no-credentials");
                new EventQueue(Store(missing)).Bind("synthetic-existing-fingerprint");
                string savedQueue = File.ReadAllText(Path.Combine(missing, "behavior-queue.xml"));
                client = PersonalizationClient.Instance;
                Check(!client.Configured && !File.Exists(Enrollment(missing))
                    && File.ReadAllText(Path.Combine(missing, "behavior-queue.xml")) == savedQueue,
                    "missing-credentials-cannot-replace-existing-bound-identity");
                foreach (bool revoked in new[] { false, true })
                {
                    var withdrawn = UseCase(revoked ? "revoked-no-credentials" : "deletion-no-credentials");
                    var queue = new EventQueue(Store(withdrawn));
                    queue.Bind("synthetic-withdrawn-fingerprint");
                    queue.SetConsent(true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); queue.SetConsent(false);
                    if (revoked) queue.CompleteDeletion();
                    client = PersonalizationClient.Instance;
                    Check(!client.Configured && !client.Consented && !File.Exists(Enrollment(withdrawn)),
                        revoked ? "revoked-participant-not-automatically-recreated" : "pending-deletion-not-automatically-recreated");
                }
                var withdrawal = UseCase("withdrawal-existing-credentials");
                client = PersonalizationClient.Instance;
                Check(client.SetConsent(false), "withdrawal-uses-existing-deletion-protocol");
                CheckDeletionHeader(client, true);
                string withdrawnBytes = File.ReadAllText(Enrollment(withdrawal));
                DestroyClient(); client = PersonalizationClient.Instance;
                Check(client.DeletionPending && !client.Consented && !client.MeasurementConsented
                    && File.ReadAllText(Enrollment(withdrawal)) == withdrawnBytes, "withdrawal-restart-keeps-credentials-for-delete-without-reactivation");
                DestroyClient(); new EventQueue(Store(withdrawal)).CompleteDeletion();
                client = PersonalizationClient.Instance;
                Check(client.EnrollmentRevoked && !client.Consented && !client.MeasurementConsented,
                    "acknowledged-withdrawal-stays-revoked-on-restart");

                var reset = UseCase("local-reset");
                File.WriteAllText(Path.Combine(reset, AutomaticEnrollment.ResetMarker), "synthetic-reset");
                client = PersonalizationClient.Instance;
                Check(!client.Configured && !File.Exists(Enrollment(reset)), "local-reset-marker-blocks-automatic-enrollment");

                var unavailable = UseCase("missing-build-config"); ConfigHook.SetValue(null, null);
                client = PersonalizationClient.Instance;
                Check(!client.Configured && !File.Exists(Enrollment(unavailable)), "editor-without-explicit-test-config-cannot-auto-enroll");
                var invalid = UseCase("invalid-build-config");
                ConfigHook.SetValue(null, (Func<AutomaticEnrollmentRecord>)(() => new AutomaticEnrollmentRecord
                    { endpoint = "http://telemetry.example.test", automaticEnrollmentKey = new string('a', 64) }));
                client = PersonalizationClient.Instance;
                Check(!client.Configured && !File.Exists(Enrollment(invalid)), "invalid-config-cannot-activate-client");

                var failed = UseCase("rename-failure-retry"); Directory.CreateDirectory(Enrollment(failed));
                client = PersonalizationClient.Instance;
                Check(!client.Configured && !client.Consented && File.Exists(Enrollment(failed) + ".new"),
                    "failed-atomic-commit-does-not-enable-collection");
                string pendingBytes = File.ReadAllText(Enrollment(failed) + ".new");
                Directory.Delete(Enrollment(failed)); Initialize.Invoke(client, null);
                Check(client.Configured && client.MeasurementConsented && File.ReadAllText(Enrollment(failed)) == pendingBytes,
                    "client-retry-recovers-original-pending-identity");

                var incomplete = UseCase("incomplete-first-write");
                File.WriteAllText(Enrollment(incomplete) + ".new", "{\"token\":");
                client = PersonalizationClient.Instance;
                Check(client.Configured && client.MeasurementConsented && !File.Exists(Enrollment(incomplete) + ".new")
                    && AutomaticEnrollment.IsLowerHex64(JsonUtility.FromJson<AutomaticEnrollmentRecord>(File.ReadAllText(Enrollment(incomplete))).token),
                    "interrupted-first-write-recovers-without-manual-registration");

                var queueFailure = UseCase("queue-storage-failure");
                Directory.CreateDirectory(Path.Combine(queueFailure, "behavior-queue.xml.tmp"));
                client = PersonalizationClient.Instance;
                Check(!client.Configured && !client.Consented && File.Exists(Enrollment(queueFailure)),
                    "queue-persistence-failure-blocks-collection-after-identity-commit");
                string committedBytes = File.ReadAllText(Enrollment(queueFailure));
                Directory.Delete(Path.Combine(queueFailure, "behavior-queue.xml.tmp")); Initialize.Invoke(client, null);
                Check(client.MeasurementConsented && File.ReadAllText(Enrollment(queueFailure)) == committedBytes,
                    "queue-storage-recovery-retains-committed-identity");
                DestroyClient();
                Check(!report.networkRequestsStarted, "all-synthetic-lifecycle-checks-finish-before-network-update");
            }
            finally
            {
                DestroyClient();
                ConfigHook.SetValue(null, priorConfig); MeasurementHook.SetValue(null, priorMeasurements); Blocked.SetValue(null, priorBlocked);
                Environment.SetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT", root);
                report.staticStateRestored = Equals(ConfigHook.GetValue(null), priorConfig)
                    && Equals(MeasurementHook.GetValue(null), priorMeasurements) && Equals(Blocked.GetValue(null), priorBlocked);
            }
        }
        static void Finish(Exception failure)
        {
            EditorApplication.update -= WaitForPlay;
            SessionState.SetBool(Key, false);
            if (report == null) report = new Report();
            report.status = failure == null && report.staticStateRestored && !report.networkRequestsStarted ? "PASS" : "FAIL";
            report.error = failure?.GetType().Name;
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "automatic-enrollment-play.json"), JsonUtility.ToJson(report, true));
            Debug.Log("AUTOMATIC_ENROLLMENT_PLAY_" + report.status + " checks=" + report.checks.Count);
            EditorApplication.Exit(report.status == "PASS" ? 0 : 1);
        }
    }
}
