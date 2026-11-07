#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Explicit synthetic supplement. Temporarily replace only the injected audit
    // service, then restore it; never load, reset or save the application's store.
    public static class VisualAuditSupplementCases
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public static IEnumerable<string> Cases(WeekOneQuestDemo ui, GardenStateService service,
            VisualAuditTraversal.Clock clock)
        {
            Require(ui.Service == service && ui.HasOpenState, "Supplement requires the injected audit service.");
            Require(AndroidPlatformBridge.InputMode == PlatformInputMode.Mock, "Supplement requires mock input.");
            Require(typeof(GardenStateService).GetField("store", Hidden)?.GetValue(service)
                is VisualAuditTraversal.Store, "Supplement refuses a persistent state store.");
            var serviceField = typeof(WeekOneQuestDemo).GetField("<Service>k__BackingField", Hidden);
            var photoField = typeof(WeekOneQuestDemo).GetField("photoService", Hidden);
            Require(serviceField != null && photoField != null, "Supplement injection fields missing.");
            var previousPhoto = photoField.GetValue(ui);
            ui.ShowHome(); // Save and close any existing synthetic draft before switching.
            var fixtureClock = new VisualAuditTraversal.Clock(); // A known Monday, independent of prior traversal.
            var fixture = new GardenStateService(new VisualAuditTraversal.Store(), fixtureClock,
                new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 100 });
            Require(fixture.Open() && fixture.RefreshCycle(Array.Empty<string>()), fixture.LastError);
            var photo = new SupplementPhotoProvider(fixtureClock);
            try
            {
                serviceField.SetValue(ui, fixture);
                photoField.SetValue(ui, new PhotoVerificationService(photo));
                Require(fixture.SelectMbctProgram("2w"), fixture.LastError);
                // ShellCases finishes on the garden, so activate the activity
                // panel before looking for enabled controls beneath this UI.
                var navigation = ui.transform.root.GetComponent<MockupNavigation>();
                Require(navigation != null, "Supplement navigation is missing.");
                navigation.ShowActivities();
                ui.ShowHome(); yield return null;

                // Active and ended records expose different actions and explanations.
                ui.StartFocusBasic(); yield return null;
                Click(ui, "Focus.Action"); yield return null;
                string activeId = Latest(fixture).SessionId;
                ui.ShowFocusRecord(activeId);
                foreach (string frame in Pages(ui, "supp-active-record")) yield return frame;
                Click(ui, "저장한 단계 이어서 하기"); yield return null;
                ui.ShowFocusProgramConfirmation("4w");
                Require(!ui.GetComponentsInChildren<Button>(false).First(b => b.name == "Focus.ConfirmProgram.4w").interactable,
                    "Active course did not block course switching.");
                foreach (string frame in Pages(ui, "supp-course-switch-blocked")) yield return frame;
                Click(ui, "진행하던 기본 미션 이어가기"); yield return null;
                Click(ui, "Focus.EndParticipation"); yield return null;
                Click(ui, "Focus.AdjustmentConfirm"); yield return null;
                Require(Latest(fixture).Status == ParticipationState.Ended, "Supplement did not end the active mission.");
                ui.ShowFocusRecord(activeId);
                foreach (string frame in Pages(ui, "supp-ended-record")) yield return frame;
                Click(ui, "Focus.RecordStepsToggle");
                foreach (string frame in Pages(ui, "supp-ended-record-expanded")) yield return frame;

                // M01 has an explicit start control without a timer. With no
                // reflection it also exercises the record's no-personal-text card.
                ui.ShowHome(); ui.StartFocusAdditionalMission("free-mission:M01"); yield return null;
                Click(ui, "Focus.Action"); yield return null;
                Click(ui, "Focus.PracticeStart"); yield return "supp-practice-started";
                Require(ui.GetComponentsInChildren<Button>(false).Any(b => b.name == "Focus.PracticeStart" && !b.interactable),
                    "Started practice control did not become unavailable.");
                Click(ui, "Focus.Action"); yield return null;
                Click(ui, "Focus.SkipReflection"); yield return null;
                Click(ui, "Focus.ViewRecord");
                foreach (string frame in Pages(ui, "supp-no-reflection-record")) yield return frame;
                Click(ui, "Focus.RecordStepsToggle");
                foreach (string frame in Pages(ui, "supp-no-reflection-record-expanded")) yield return frame;

                // Reopening a registered photo uses the existing-photo action;
                // a failed retake with no file has a different fallback to the
                // already-covered unavailable result with a usable local image.
                ui.ShowHome(); ui.StartFocusAdditionalMission("free-mission:M03"); yield return null;
                int steps = 0;
                while (ui.CurrentScreen == "focus-practice")
                {
                    Require(++steps <= 8, "Supplement photo mission did not converge.");
                    Click(ui, "Focus.Action"); yield return null;
                }
                Require(ui.CurrentScreen == "focus-photo", "Photo choice did not open.");
                Click(ui, "Photo.Capture"); yield return null;
                Click(ui, "Photo.Register"); yield return null;
                Click(ui, "Photo.Edit");
                foreach (string frame in Pages(ui, "supp-photo-existing")) yield return frame;
                Require(ui.GetComponentsInChildren<Text>(false).Any(t => t.text == "이 사진으로 이어가기"),
                    "Existing-photo action is missing.");
                Click(ui, "Photo.Register"); yield return null;
                Click(ui, "Photo.Edit"); yield return null;
                photo.Status = "unavailable"; photo.CreateImage = false;
                Click(ui, "Photo.Retake");
                foreach (string frame in Pages(ui, "supp-photo-unavailable-no-image")) yield return frame;
                Require(!VisualAuditTraversal.Has(ui, "Photo.Register") && VisualAuditTraversal.Has(ui, "Photo.Capture"),
                    "Missing-image fallback offered an invalid register action.");
                Click(ui, "Photo.Skip"); yield return null;
                Click(ui, "Focus.SkipReflection"); yield return null;
                ui.ShowHome(); yield return null;

                // Seed valid historical progress through state transactions, not
                // another screenshot traversal of the same practice pages.
                for (int order = 1; order <= FocusMissionPolicy.TotalPracticesFor("2w"); order++)
                {
                    while (FocusMissionPolicy.Availability(fixture.Snapshot, "2w") != null)
                    {
                        fixtureClock.Time = fixtureClock.Time.AddDays(1);
                        Require(fixture.RefreshCycle(Array.Empty<string>()), fixture.LastError);
                    }
                    SeedCourseCompletion(fixture, order);
                    if (order == 1)
                    {
                        ui.ShowFocusProgramConfirmation("2w");
                        foreach (string frame in Pages(ui, "supp-course-continue-confirmation")) yield return frame;
                        Click(ui, "Focus.ConfirmProgram.2w");
                        foreach (string frame in Pages(ui, "supp-course-completed-today")) yield return frame;
                    }
                    if (order == 6)
                    {
                        fixtureClock.Time = fixtureClock.Time.AddDays(1); // Sunday after six Monday–Saturday sessions.
                        Require(fixture.RefreshCycle(Array.Empty<string>()), fixture.LastError);
                        Require(FocusMissionPolicy.Availability(fixture.Snapshot, "2w")?.Contains("이번 주") == true,
                            "Weekly-limit fixture did not reach the weekly boundary.");
                        ui.ShowHome();
                        foreach (string frame in Pages(ui, "supp-course-weekly-limit")) yield return frame;
                    }
                }
                ui.ShowHome();
                foreach (string frame in Pages(ui, "supp-course-finished-home")) yield return frame;
                ui.ShowFocusProgramConfirmation("2w");
                foreach (string frame in Pages(ui, "supp-course-finished-confirmation")) yield return frame;
            }
            finally
            {
                // Close fixture drafts before restoring the original UI service.
                try { ui.ShowHome(); }
                finally
                {
                    serviceField.SetValue(ui, service);
                    photoField.SetValue(ui, previousPhoto);
                    fixture.Dispose(); photo.Dispose();
                    ui.ShowHome();
                }
            }
        }

        static void SeedCourseCompletion(GardenStateService service, int order)
        {
            var mission = FocusMissionPolicy.MissionAt(order, "2w");
            string id = "synthetic-supplement-course-" + order;
            Require(service.BeginFocusSession(id, mission.Id, order, false, "2w"), service.LastError);
            Require(service.StartSession(id), service.LastError);
            for (int guard = 0; guard < 32; guard++)
            {
                var session = service.Snapshot.Sessions.First(s => s.SessionId == id);
                int index = session.InstructionStep;
                if (index >= mission.Steps.Length) break;
                var step = mission.Steps[index];
                string answer = step.Options.FirstOrDefault() ?? (step.OptionalInput ? "합성 과정 상태 검토용 기록" : null);
                if (GardenStateService.IsActualFocusPractice(step))
                    Require(service.StartFocusPracticeStep(id, index), service.LastError);
                Require(service.ConfirmFocusStep(id, index, answer), service.LastError);
            }
            var completed = service.Snapshot.Sessions.First(s => s.SessionId == id);
            Require(completed.InstructionStep == mission.Steps.Length, "Synthetic course fixture did not finish its steps.");
            if (CameraMissionPolicy.Target(completed) != null) Require(service.SkipPhoto(id), service.LastError);
            Require(service.CompleteFocusSession(id), service.LastError);
        }

        sealed class SupplementPhotoProvider : IMissionPhotoProvider, IDisposable
        {
            readonly VisualAuditTraversal.Clock clock;
            PhotoVerificationResult result;
            public string Status = "matched";
            public bool CreateImage = true;
            public string DirectoryPath { get; }
            public SupplementPhotoProvider(VisualAuditTraversal.Clock clock)
            {
                this.clock = clock;
                DirectoryPath = Path.Combine(Application.temporaryCachePath, "synthetic-visual-supplement-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath);
            }
            public PhotoVerificationResult Start(string id, string target)
            {
                result = PhotoVerificationService.Unavailable(id, target);
                result.status = Status; result.capturedUtc = clock.Time.ToString("O");
                result.label = Status == "matched" ? "cup" : ""; result.score = Status == "matched" ? .88 : 0;
                if (CreateImage)
                {
                    var texture = new Texture2D(160, 120, TextureFormat.RGB24, false);
                    try
                    {
                        texture.SetPixels(Enumerable.Repeat(new Color(.35f, .58f, .43f), 160 * 120).ToArray());
                        texture.Apply(); File.WriteAllBytes(Path.Combine(DirectoryPath, id + ".jpg"), texture.EncodeToJPG());
                    }
                    finally { if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture); }
                }
                return result;
            }
            public PhotoVerificationResult Poll(string id) => result;
            public void Delete(string id)
            {
                if (!CameraMissionPolicy.ValidId(id)) return;
                string file = Path.Combine(DirectoryPath, id + ".jpg"); if (File.Exists(file)) File.Delete(file);
            }
            public void Prune(string[] keep) { }
            public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
        }

        static ActivitySession Latest(GardenStateService service) => service.Snapshot.Sessions.Last(FocusMissionPolicy.IsFocus);
        static void Click(WeekOneQuestDemo ui, string name) => VisualAuditTraversal.Click(ui, name);
        static IEnumerable<string> Pages(WeekOneQuestDemo ui, string prefix) => VisualAuditTraversal.ScrollPages(ui, prefix);
        static void Require(bool condition, string message) => VisualAuditTraversal.Require(condition, message);
    }
}
#endif
