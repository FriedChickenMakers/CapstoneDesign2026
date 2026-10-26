using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CapstoneDesign.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Run only through test-personalization-play.py: the server validates its
    // own database before permitting withdrawal, so a local queue drain alone
    // cannot be mistaken for successful delivery.
    [InitializeOnLoad]
    public static class PersonalizationPlayValidation
    {
        const string Key = "Capstone.PersonalizationPlayValidation";
        [Serializable] sealed class Report
        {
            public string status;
            public List<string> checks = new List<string>();
            public string error;
        }
        static Report report;
        static bool finishing;
        static string Control => Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_CONTROL");

        static PersonalizationPlayValidation()
        {
            if (SessionState.GetBool(Key, false)) EditorApplication.update += WaitForPlay;
        }
        public static void Start()
        {
            string root = Environment.GetEnvironmentVariable("CAPSTONE_PERSONALIZATION_TEST_ROOT");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(Control) ||
                !Directory.Exists(root) || !Directory.Exists(Control) ||
                !File.Exists(Path.Combine(root, "personalization-enrollment.json")))
                throw new InvalidOperationException("The isolated personalization runner is required.");
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WaitForPlay;
            EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if (Expired()) { Finish(new TimeoutException("Play startup deadline exceeded")); return; }
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update += WatchRun;
            report = new Report();
            PersonalizationClient.Instance.StartCoroutine(Guarded());
        }
        static bool Expired() => EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0);
        static void WatchRun()
        {
            if (Expired()) Finish(new TimeoutException("Network/consent deadline exceeded"));
        }
        static IEnumerator Guarded()
        {
            var steps = Steps();
            while (!finishing)
            {
                object next = null;
                bool moved = false;
                Exception failure = null;
                try { moved = steps.MoveNext(); if (moved) next = steps.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { Finish(failure); yield break; }
                if (!moved) { Finish(null); yield break; }
                yield return next;
            }
        }
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            report.checks.Add(name);
        }
        static void Marker(string name, string value)
        {
            string path = Path.Combine(Control, name);
            File.WriteAllText(path + ".pending", value);
            File.Move(path + ".pending", path);
        }
        static IEnumerator Steps()
        {
            var client = PersonalizationClient.Instance;
            Check(client.Configured && !client.Consented && client.PendingCount == 0 && !client.EnrollmentRevoked,
                "configured-with-explicit-test-root");
            Check(!client.Record("recommendation_shown", Guid.NewGuid().ToString("N"), "course:MBCT2W01",
                new[] { 1f, .5f, .25f, 1f }, "local-rules-v1"), "no-recording-before-consent");
            Check(client.SetConsent(true) && client.Consented, "consent-opt-in");
            // Queue before yielding: Update must send this event in its first cycle.
            Check(client.Record("recommendation_shown", Guid.NewGuid().ToString("N"), "course:MBCT2W01",
                new[] { 1f, .5f, .25f, 1f }, "local-rules-v1") && client.PendingCount == 1,
                "synthetic-event-queued");
            while (client.PendingCount != 0) yield return null;
            Check(client.Consented && !client.DeletionPending, "upload-acknowledged");
            Marker("uploaded.json", "{\"status\":\"uploaded\"}");
            while (!File.Exists(Path.Combine(Control, "server-upload-confirmed.json"))) yield return null;
            Check(File.ReadAllText(Path.Combine(Control, "server-upload-confirmed.json")).Contains("confirmed"),
                "server-upload-confirmed");
            Check(client.SetConsent(false) && !client.Consented && client.PendingCount == 0 && client.DeletionPending,
                "withdrawal-immediate-local-clear");
            while (client.DeletionPending || !client.EnrollmentRevoked) yield return null;
            Check(!client.Consented && client.PendingCount == 0, "server-deletion-acknowledged");
            Check(!client.SetConsent(true) && !client.Consented, "revoked-enrollment-cannot-reconsent");
            Check(!client.Record("recommendation_shown", Guid.NewGuid().ToString("N"), "course:MBCT2W01",
                new[] { 1f, .5f, .25f, 1f }, "local-rules-v1") && client.PendingCount == 0,
                "revoked-enrollment-cannot-record");
        }
        static void Finish(Exception failure)
        {
            if (finishing) return;
            finishing = true;
            EditorApplication.update -= WaitForPlay;
            EditorApplication.update -= WatchRun;
            SessionState.SetBool(Key, false);
            if (report == null) report = new Report();
            report.status = failure == null ? "PASS" : "FAIL";
            // Assertion names/types suffice; enrollment and request details must
            // not be copied from arbitrary exception messages into the report.
            report.error = failure == null ? null : failure.GetType().Name;
            try { Marker("client-result.json", JsonUtility.ToJson(report, true)); }
            catch (Exception) { EditorApplication.Exit(1); return; }
            Debug.Log("Personalization Play validation: " + report.status + ", checks=" + report.checks.Count);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
