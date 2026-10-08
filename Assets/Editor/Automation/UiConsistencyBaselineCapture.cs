using System;
using System.IO;
using System.Linq;
using System.Text;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Synthetic, edit-mode visual inventory. It does not touch the normal game save.
    public static class UiConsistencyBaselineCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset Time = DateTimeOffset.Parse("2026-10-05T12:00:00+09:00");
            public DateTimeOffset UtcNow => Time;
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Preview-KST", TimeSpan.FromHours(9), "KST", "KST");
        }

        sealed class Store : IStateStore
        {
            string data;
            public bool Fail;
            public GardenState Load() => data == null ? null : StateCodec.Decode(data);
            public void Save(GardenState state)
            {
                if (Fail) throw new IOException("Synthetic save failure");
                data = StateCodec.Encode(state);
            }
        }

        static readonly Vector2Int[] Sizes = { new Vector2Int(900, 1600), new Vector2Int(1200, 800) };
        static readonly StringBuilder Manifest = new StringBuilder("name,width,height,category,detail\n");
        static readonly StringBuilder TextOverflow = new StringBuilder("image,text,preferredHeight,rectHeight\n");
        static string output;
        static string filter;
        static int count;

        public static void Capture()
        {
            AndroidPlatformBridge.UseMockProvider();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Unity graphics renderer is unavailable.");
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ??
                Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "ui-consistency-20261006", "before", "expanded");
            output = Path.Combine(root, "visual");
            filter = Environment.GetEnvironmentVariable("CAPSTONE_UI_SCREEN_PREFIX");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
            var loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
            var clock = new Clock();
            var store = new Store();
            var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
            using (var service = new GardenStateService(store, clock, balance))
            {
                loop.Balance = balance;
                loop.Initialize(service, true);
                nav.ShowActivities();
                CaptureEmptyAndSettings(nav, loop);
                if (filter != "settings")
                {
                    CaptureAllCoursePages(loop);
                    CaptureAllPractices(loop, service, clock);
                    CapturePopulatedStates(nav, loop, service, store);
                }
            }
            File.WriteAllText(Path.Combine(root, "capture-manifest.csv"), Manifest.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "text-overflow.csv"), TextOverflow.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "environment.txt"),
                "Unity Editor synthetic preview\nRenderer: " + SystemInfo.graphicsDeviceName +
                "\nGraphics API: " + SystemInfo.graphicsDeviceType + "\nInput: MOCK/LIVE/REPLAY synthetic\nImages: " + count +
                "\nDevice tested: false\n", Encoding.UTF8);
            Debug.Log("UI_CONSISTENCY_BASELINE_PASS images=" + count + " output=" + output);
        }

        static void CaptureEmptyAndSettings(MockupNavigation nav, WeekOneQuestDemo loop)
        {
            loop.ShowHome(); CaptureView("home-empty", "empty", "first activity available");
            loop.ShowFree(); CaptureView("free-empty", "empty", "no learned activity");
            loop.ShowRecords(); CaptureView("records-empty", "empty", "no participation record");
            loop.ShowExperienceHistory(); CaptureView("journal-empty", "empty", "no experience record");
            loop.ShowPersonalPlan(); CaptureView("plan-empty", "empty", "no personal plan");
            loop.ShowDiscoveries(); CaptureView("discoveries-empty", "empty", "no animal discovery");
            loop.ShowShop(); CaptureView("shop-empty", "shop", "insufficient nutrients");
            loop.PreviewGrowth(); CaptureView("growth-insufficient", "shop", "growth preview no nutrients");
            bool growthClicked = InvokeButton(loop, "확정");
            CaptureView("growth-purchase-error", "error", growthClicked ? "growth insufficient nutrients" : "purchase disabled; click has no effect");
            loop.ShowShop(); loop.PreviewEnvironment("environment:E01"); CaptureView("placement-flower-insufficient", "shop", "flower preview no nutrients");
            bool flowerClicked = InvokeButton(loop, "확정");
            CaptureView("placement-flower-error", "error", flowerClicked ? "flower insufficient nutrients" : "purchase disabled; click has no effect");
            loop.ShowShop(); loop.PreviewEnvironment("environment:E02"); CaptureView("placement-shelter-insufficient", "shop", "shelter preview no nutrients");
            loop.ShowShop();

            var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
            sensor.PrepareView();
            nav.ShowSettings(); sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
            CaptureView("settings-mock", "sensor", "MOCK provider");
            AndroidPlatformBridge.UseLiveProvider(); nav.ShowSettings(); sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
            CaptureView("settings-live-editor", "sensor", "LIVE provider in Linux Editor");
            string replay = Path.Combine(output, "replay-fixture.jsonl");
            File.WriteAllText(replay, JsonUtility.ToJson(AndroidPlatformBridge.GetSnapshot()) + "\n", Encoding.UTF8);
            var loaded = AndroidPlatformBridge.UseReplayProvider(replay);
            if (loaded.status != "AVAILABLE") throw new InvalidOperationException("Replay fixture failed: " + loaded.message);
            nav.ShowSettings(); sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
            CaptureView("settings-replay", "sensor", "REPLAY provider synthetic fixture");
            AndroidPlatformBridge.UseMockProvider(); nav.ShowActivities();
        }

        static void CaptureAllCoursePages(WeekOneQuestDemo loop)
        {
            loop.ShowCourse();
            for (int week = 1; week <= 8; week++)
            {
                CaptureView("course-week-" + week.ToString("00"), "course", "locked and current activity");
                if (week < 8 && !InvokeButton(loop, "다음 주")) throw new InvalidOperationException("Next week button is disabled.");
            }
        }

        static void CaptureAllPractices(WeekOneQuestDemo loop, GardenStateService service, Clock clock)
        {
            int steps = 0;
            foreach (var mission in MbctContent.Course)
            {
                while (MbctPolicy.Availability(service.Snapshot) != null)
                {
                    clock.Time = clock.Time.AddDays(1);
                    if (!service.RefreshCycle(Array.Empty<string>())) throw new InvalidOperationException(service.LastError);
                }
                loop.SelectMission(mission);
                var id = service.Snapshot.Sessions.Last().SessionId;
                CaptureView("mbct-" + mission.recommendedOrder.ToString("00") + "-selected", "practice", mission.title + " selected");
                if (!service.StartSession(id)) throw new InvalidOperationException(service.LastError);
                for (int step = 0; step < mission.steps.Length; step++)
                {
                    loop.ShowSession();
                    CaptureView("mbct-" + mission.recommendedOrder.ToString("00") + "-step-" + (step + 1).ToString("00"),
                        "practice", mission.title + " / " + mission.steps[step].title);
                    steps++;
                    if (mission.recommendedOrder == 1 && step == 0)
                    {
                        if (!service.PauseSession(id)) throw new InvalidOperationException(service.LastError);
                        loop.ShowSession(); CaptureView("mbct-paused", "practice", "paused session");
                        if (!service.ResumeSession(id)) throw new InvalidOperationException(service.LastError);
                    }
                    if (!service.SavePracticeStep(id, step + 1)) throw new InvalidOperationException(service.LastError);
                }
                if (mission.recommendedOrder == 1)
                {
                    loop.ShowSession(); CaptureView("mbct-reflection-after-steps", "practice", "end of practice reflection");
                }
                loop.Finish(null, null);
                if (mission.recommendedOrder == 1)
                    CaptureView("mbct-completed-first", "practice", "first completion reward");
            }
            Debug.Log("UI_CONSISTENCY_STEPS " + steps);
        }

        static void CapturePopulatedStates(MockupNavigation nav, WeekOneQuestDemo loop, GardenStateService service, Store store)
        {
            loop.ShowHome(); CaptureView("home-course-complete", "course", "all 48 practices learned");
            loop.ShowFree(); CaptureView("free-learned", "course", "learned activity repeat");
            loop.ShowRecords(); CaptureView("records-populated", "records", "completed practice records");
            loop.ShowExperienceHistory(); CaptureView("journal-empty-after-course", "records", "experience record still optional");
            loop.ShowShop(); CaptureView("shop-with-nutrients", "shop", "earned nutrients");

            // Supplemental states added after the original baseline capture.
            loop.PreviewGrowth(); CaptureView("growth-affordable", "after-only supplemental", "growth affordable; balance unchanged in preview");
            if (!InvokeButton(loop, "확정")) throw new InvalidOperationException("Affordable growth should be enabled.");
            CaptureView("garden-after-growth", "after-only supplemental", "growth committed");
            nav.ShowActivities(); loop.ShowShop(); loop.PreviewEnvironment("environment:E01");
            CaptureView("placement-affordable", "after-only supplemental", "flower affordable; balance unchanged in preview");
            if (!InvokeButton(loop, "확정")) throw new InvalidOperationException("Affordable placement should be enabled.");
            CaptureView("garden-after-flower", "after-only supplemental", "flower committed");
            nav.ShowActivities(); loop.ShowShop(); loop.PreviewEnvironment("environment:E01");
            CaptureView("placement-owned", "after-only supplemental", "flower already owned; confirmation disabled");
            loop.ShowShop(); loop.PreviewEnvironment("environment:E02");
            CaptureView("placement-before-save-error", "after-only supplemental", "shelter affordable before injected save failure");
            store.Fail = true;
            try
            {
                if (!InvokeButton(loop, "확정")) throw new InvalidOperationException("Shelter should be affordable before save failure.");
            }
            finally { store.Fail = false; }
            CaptureView("purchase-save-error", "after-only supplemental", "storage failure retains nutrients and offers retry");
            loop.ShowShop();

            loop.SelectMission(MbctContent.Course[0]);
            var review = service.Snapshot.Sessions.Last();
            if (!service.StartSession(review.SessionId)) throw new InvalidOperationException(service.LastError);
            loop.ShowSession();
            var input = loop.GetComponentsInChildren<InputField>(true).FirstOrDefault(f => f.gameObject.activeInHierarchy);
            if (input == null) throw new InvalidOperationException("Practice input missing.");
            input.text = "직접 적은 물의 감각";
            CaptureView("practice-custom-input", "after-only supplemental", "custom practice text entry");
            loop.ShowReflection();
            var mood = loop.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<PrototypeMoodFace>() != null);
            if (mood == null) throw new InvalidOperationException("Mood choice missing.");
            mood.onClick.Invoke();
            CaptureView("reflection-selected-mood", "after-only supplemental", "selected mood face");
            if (!service.EndParticipation(review.SessionId)) throw new InvalidOperationException(service.LastError);

            loop.ShowExperienceTypes(); InvokeButton(loop, "불편함");
            for (int step = 1; step <= 4; step++)
            {
                CaptureView("journal-step-" + step.ToString("00"), "after-only supplemental", "experience journal step " + step);
                if (step == 3)
                {
                    var journalInput = loop.GetComponentsInChildren<InputField>(true).FirstOrDefault(f => f.gameObject.activeInHierarchy);
                    if (journalInput != null) journalInput.text = "직접 적은 감정";
                    CaptureView("journal-step-03-custom", "after-only supplemental", "custom emotion input");
                }
                if (step < 4) InvokeButton(loop, "다음");
            }
        }

        static bool InvokeButton(WeekOneQuestDemo loop, string name)
        {
            var button = loop.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == name);
            if (button == null) throw new InvalidOperationException("Button missing: " + name);
            if (!button.interactable) return false;
            button.onClick.Invoke();
            return true;
        }

        static void CaptureView(string name, string category, string detail)
        {
            if (!string.IsNullOrEmpty(filter) && !name.StartsWith(filter, StringComparison.Ordinal)) return;
            foreach (var size in Sizes)
            {
                string file = name + "-" + size.x + "x" + size.y + ".png";
                LocalLoopPreview.Render(size, Path.Combine(output, file));
                Manifest.Append(Csv(name)).Append(',').Append(size.x).Append(',').Append(size.y).Append(',')
                    .Append(Csv(category)).Append(',').Append(Csv(detail)).Append('\n');
                foreach (var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                {
                    if (!label.gameObject.activeInHierarchy || label.GetComponentInParent<ScrollRect>() != null) continue;
                    var rect = label.rectTransform.rect;
                    if (rect.height < 1 || label.preferredHeight <= rect.height + 2) continue;
                    TextOverflow.Append(Csv(file)).Append(',').Append(Csv(label.text)).Append(',')
                        .Append(label.preferredHeight.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                        .Append(rect.height.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                }
                count++;
            }
        }

        static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"").Replace("\n", " / ") + "\"";
    }
}
