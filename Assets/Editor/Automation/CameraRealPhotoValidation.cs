using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Displays recorded REAL model inference in the actual UI. This is not an Android camera test.
    // Input photos/results stay in ignored artifacts; this provider never supplies invented scores.
    public static class CameraRealPhotoValidation
    {
        [Serializable] sealed class InferenceRun
        {
            public string utc, modelVersion, modelSha256, policyVersion, mediapipeVersion;
            public double threshold;
            public Case[] cases;
        }
        [Serializable] sealed class Detection { public string label; public double score; }
        [Serializable] sealed class Case
        {
            public string caseId, sampleId, target, status, label, imageFile, imageSha256;
            public double score;
            public bool expectedPresent;
            public Detection[] detections;
        }
        [Serializable] sealed class CaptureEntry
        {
            public string caseId, status, label, imageSha256, screenshot, screenshotSha256, visibleText;
            public double score;
        }
        [Serializable] sealed class CaptureManifest
        {
            public string environment = "Windows MediaPipe inference replayed in Unity Editor; isolated mission state; no Android camera exercised.";
            public string resultsSha256, modelSha256;
            public List<CaptureEntry> captures = new List<CaptureEntry>();
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }

        sealed class RecordedInferenceProvider : IMissionPhotoProvider
        {
            readonly Case sample;
            readonly string image, capturedUtc;
            PhotoVerificationResult result;
            public string DirectoryPath { get; }
            public RecordedInferenceProvider(string root, Case sample, string capturedUtc)
            {
                this.sample = sample; this.capturedUtc = capturedUtc;
                Require(Regex.IsMatch(sample.caseId ?? "", "^[a-z0-9-]+$"), "Invalid case ID");
                image = Path.GetFullPath(Path.Combine(root, sample.imageFile));
                Require(image.StartsWith(Path.GetFullPath(Path.Combine(root, "normalized")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Photo must be in normalized artifacts");
                Require(Hash(image) == sample.imageSha256, "Photo does not match actual inference input");
                DirectoryPath = Path.Combine(root, "ui-photos", sample.caseId);
                Directory.CreateDirectory(DirectoryPath);
            }
            public PhotoVerificationResult Start(string id, string target)
            {
                Require(CameraMissionPolicy.ValidId(id) && target == sample.target, "Wrong request binding");
                Require(Hash(image) == sample.imageSha256, "Photo changed after validation");
                File.Copy(image, Path.Combine(DirectoryPath, id + ".jpg"), true);
                return result = new PhotoVerificationResult { id = id, target = target, status = sample.status,
                    label = sample.label, score = sample.score, modelVersion = CameraMissionPolicy.ModelVersion, capturedUtc = capturedUtc };
            }
            public PhotoVerificationResult Poll(string id) => result;
            public void Delete(string id)
            {
                if (CameraMissionPolicy.ValidId(id)) File.Delete(Path.Combine(DirectoryPath, id + ".jpg"));
            }
            public void Prune(string[] keep) { }
        }

        [MenuItem("Capstone Mockup/Capture Real Photo Inference Results")]
        public static void Run()
        {
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Require(!string.IsNullOrEmpty(root), "Set CAPSTONE_ARTIFACTS to the private real-inference run directory");
            string input = Path.Combine(root, "results.json");
            var run = JsonUtility.FromJson<InferenceRun>(File.ReadAllText(input));
            Require(run.modelVersion == CameraMissionPolicy.ModelVersion && run.policyVersion == CameraMissionPolicy.Version
                && run.threshold == CameraMissionPolicy.Threshold && run.mediapipeVersion == "0.10.35", "Inference policy/runtime mismatch");
            Require(Hash("Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/assets/models/efficientdet_lite0_int8_v1.tflite") == run.modelSha256, "Model hash mismatch");
            Require(run.cases != null && run.cases.Length > 0, "No real inference cases");
            var manifest = new CaptureManifest { resultsSha256 = Hash(input), modelSha256 = run.modelSha256 };
            var report = new FocusMissionValidation.Report { runId = "real-photos", utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
            try
            {
                foreach (var sample in run.cases)
                {
                    Require(sample.detections != null, "Missing raw detections");
                    foreach (var d in sample.detections)
                        Require(CameraMissionPolicy.AllowedLabel(sample.target, d.label) && d.score >= run.threshold && d.score <= 1 && !double.IsNaN(d.score), "Invalid raw detection");
                    var best = sample.detections.OrderByDescending(d => d.score).FirstOrDefault();
                    Require(sample.status == (best == null ? "not_found" : "matched") && sample.label == (best?.label ?? "") && sample.score == (best?.score ?? 0), "Outcome differs from raw inference");
                    using (var f = new FocusMissionValidation.Fixture(report, true))
                    {
                        f.Clock.UtcNow = DateTimeOffset.Parse(run.utc); f.Ui.RefreshCycle();
                        f.Ui.SetPhotoProviderForValidation(new RecordedInferenceProvider(root, sample, f.Clock.UtcNow.ToString("O")));
                        var mission = FocusMissionCatalog.Find(sample.target == "book" ? "M14" : "M03");
                        f.Invoke("StartFocusAdditionalMission", mission.Id); f.Click("시작");
                        f.CompletePractice(mission, false, keepPhotoChoice: true); f.Click("Photo.Capture");
                        Require(f.Ui.CurrentScreen == "focus-photo", "Expected photo result screen");
                        f.AssertVisibleAssets();
                        var status = f.Ui.GetComponentsInChildren<Text>(false).Single(t => t.name == "Photo.Status");
                        Require(status.text.Contains(sample.status == "matched" ? "찾았어요" : "이대로 등록할까요?"), "UI disagrees with real inference");
                        f.RevealGraphic(status);
                        string name = "real-" + sample.caseId + "-result";
                        f.Capture(name, true);
                        string shot = Path.Combine(root, "focus-validation", name + ".png");
                        manifest.captures.Add(new CaptureEntry { caseId = sample.caseId, status = sample.status, label = sample.label,
                            score = sample.score, imageSha256 = sample.imageSha256, screenshot = "focus-validation/" + name + ".png",
                            screenshotSha256 = Hash(shot), visibleText = f.VisibleText });
                        // Preserve genuine negatives on the result screen; validate completion for actual matches.
                        if (sample.status == "matched")
                        {
                            f.Click("Photo.Register");
                            Require(f.Latest.Photo != null && !f.Latest.Photo.UserConfirmed && f.Latest.Photo.Score == sample.score && f.Latest.Photo.MatchedLabel == sample.label, "Saved photo differs from inference");
                            f.Click("Focus.SkipReflection");
                            Require(f.Latest.RewardAmount == 30 && f.Latest.PhotoBonusAmount == 10, "Additional mission and actual match completion reward");
                            f.Capture("real-" + sample.caseId + "-completion", true);
                            MissionCompletionUiValidation.OpenJournalRecordFromCompletion(f, f.Latest.SessionId); f.AssertVisibleAssets();
                            f.Capture("real-" + sample.caseId + "-record", true);
                        }
                        report.passed++;
                    }
                }
            }
            catch { report.failed++; throw; }
            finally
            {
                File.WriteAllText(Path.Combine(root, "capture-manifest.json"), JsonUtility.ToJson(manifest, true));
                File.WriteAllText(Path.Combine(root, "ui-report.json"), JsonUtility.ToJson(report, true));
            }
            Debug.Log("REAL_PHOTO_UI passed=" + report.passed + " clicks=" + report.buttonClicks + " captures=" + report.captures.Count);
        }
    }
}
