using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Edit-mode captures use an in-memory store and the mock platform provider.
    // Every case opens a clean scene, so navigation and scroll state are isolated.
    public static class TabConsistencyCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset Time = DateTimeOffset.Parse("2026-10-08T12:00:00+09:00");
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

        static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(900, 1950), new Vector2Int(900, 1600), new Vector2Int(1200, 800)
        };

        static readonly string[] Cases =
        {
            "reference-home", "activities-top", "activities-bottom", "course-top", "course-bottom", "course-week5-top", "course-week5-bottom",
            "practice-selected-top", "practice-selected-bottom", "practice-step-top", "practice-step-bottom",
            "practice-paused", "reflection-top", "reflection-bottom", "complete",
            "free-empty", "free-learned", "records-empty", "records-populated-top", "records-populated-bottom",
            "record-detail", "record-heart-top", "record-heart-bottom", "journal-top", "journal-bottom", "journal-emotion", "plan",
            "stress-longest-guidance", "stress-longest-choice", "stress-longest-title", "stress-longest-input-guidance", "plan-input",
            "settings-top", "settings-bottom", "settings-diagnostics-top", "settings-diagnostics-bottom",
            "settings-chart-action-top", "settings-chart-action-bottom"
        };

        static readonly string[] RemainingCases =
        {
            "shop-insufficient-top", "shop-insufficient-bottom", "growth-insufficient-top", "growth-insufficient-bottom",
            "placement-flower-insufficient-top", "placement-shelter-insufficient-top",
            "shop-affordable-top", "shop-affordable-bottom", "growth-affordable-top", "growth-affordable-bottom",
            "growth-committed", "placement-flower-affordable-top", "placement-flower-affordable-bottom",
            "placement-flower-committed", "placement-flower-owned-top", "placement-save-error-top", "placement-save-error-bottom",
            "discoveries-empty-top", "discoveries-populated-top", "journal-history-empty-top",
            "journal-history-populated-top", "journal-history-populated-bottom", "journal-detail-top", "journal-detail-bottom",
            "practice-answers-empty-top", "practice-answers-populated-top", "practice-answers-populated-bottom",
            "record-health-unsupported-top", "record-health-permission-denied-top", "record-health-error-top",
            "settings-live-top", "settings-live-bottom", "settings-replay-top", "settings-replay-bottom"
        };

        static readonly StringBuilder Manifest = new StringBuilder();
        static readonly StringBuilder Metrics = new StringBuilder();
        static readonly StringBuilder Overflow = new StringBuilder();
        static readonly StringBuilder ScrollMetrics = new StringBuilder();
        static int count;
        static int overflows;

        public static void Capture() => CaptureInternal(false);

        // Optional broader sweep: all 48 selected practices and every real guidance
        // step, with bottom captures when the page scrolls, at all three sizes.
        public static void CaptureGuidance() => CaptureInternal(true);

        // Supplemental state inventory for purchase, discovery, journal, answer,
        // platform-status and failure views affected by the shared page layout.
        public static void CaptureRemaining()
        {
            AndroidPlatformBridge.UseMockProvider();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A graphics renderer is required for UI captures.");
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ??
                Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "tab-consistency-20261008", "remaining");
            string output = Path.Combine(root, "visual");
            string filter = Environment.GetEnvironmentVariable("CAPSTONE_TAB_CAPTURE_SCREEN");
            Directory.CreateDirectory(output);
            Manifest.Clear().AppendLine("image,width,height,case,scrollPosition");
            Metrics.Clear().AppendLine("image,path,renderer,text,fontSize,canvasScale,renderedFontPixels,rectWidth,rectHeight,preferredHeight,visibility");
            Overflow.Clear().AppendLine("image,path,text,kind,preferred,available,inScrollView");
            ScrollMetrics.Clear().AppendLine("image,path,normalizedPosition,scrollbarValue,scrollbarSize,viewportHeight,contentHeight");
            count = overflows = 0;
            foreach (var size in Sizes)
            foreach (string name in RemainingCases)
            {
                if (!string.IsNullOrEmpty(filter) && !name.StartsWith(filter, StringComparison.Ordinal)) continue;
                Debug.Log("TAB_CAPTURE_REMAINING_BEGIN " + name + " " + size.x + "x" + size.y);
                AndroidPlatformBridge.UseMockProvider();
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                var clock = new Clock();
                var store = new Store();
                var balance = new DemoBalanceConfig { VisitorChancePercent = name == "discoveries-populated-top" ? 100 : 0 };
                int nutrients = name.Contains("affordable") || name.Contains("committed") || name.Contains("owned") || name.Contains("save-error") ? 50 : 0;
                using (var service = new GardenStateService(store, clock, balance, new LegacySnapshot { Nutrient = nutrients }))
                {
                    loop.Balance = balance;
                    loop.Initialize(service, true);
                    nav.ShowActivities();
                    PrepareRemaining(name, nav, loop, service, store, clock, output);
                    CaptureView(name, size, nav, loop, output);
                }
            }
            AndroidPlatformBridge.UseMockProvider();
            File.WriteAllText(Path.Combine(root, "capture-manifest.csv"), Manifest.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "text-metrics.csv"), Metrics.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "text-overflow.csv"), Overflow.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "scroll-metrics.csv"), ScrollMetrics.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "environment.txt"),
                "Unity Editor synthetic supplemental UI captures\nInput: MOCK/LIVE/REPLAY synthetic\n" +
                "State: in-memory; no application save written\nRenderer: " + SystemInfo.graphicsDeviceName +
                "\nGraphics API: " + SystemInfo.graphicsDeviceType + "\nImages: " + count +
                "\nPotential text overflows: " + overflows + "\nDevice tested: false\n", Encoding.UTF8);
            Debug.Log("TAB_CONSISTENCY_REMAINING_PASS images=" + count + " overflows=" + overflows + " output=" + output);
        }

        static void CaptureInternal(bool allGuidance)
        {
            AndroidPlatformBridge.UseMockProvider();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A graphics renderer is required for UI captures.");
            string root = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ??
                Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "tab-consistency-20261008");
            string output = Path.Combine(root, "visual");
            string filter = Environment.GetEnvironmentVariable("CAPSTONE_TAB_CAPTURE_SCREEN");
            Directory.CreateDirectory(output);
            Manifest.Clear().AppendLine("image,width,height,case,scrollPosition");
            Metrics.Clear().AppendLine("image,path,renderer,text,fontSize,canvasScale,renderedFontPixels,rectWidth,rectHeight,preferredHeight,visibility");
            Overflow.Clear().AppendLine("image,path,text,kind,preferred,available,inScrollView");
            ScrollMetrics.Clear().AppendLine("image,path,normalizedPosition,scrollbarValue,scrollbarSize,viewportHeight,contentHeight");
            count = overflows = 0;
            if (allGuidance) CaptureAllGuidance(output);
            else foreach (var size in Sizes)
            foreach (string name in Cases)
            {
                if (!string.IsNullOrEmpty(filter) && !name.StartsWith(filter, StringComparison.Ordinal)) continue;
                Debug.Log("TAB_CAPTURE_BEGIN " + name + " " + size.x + "x" + size.y);
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                var clock = new Clock();
                var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
                using (var service = new GardenStateService(new Store(), clock, balance))
                {
                    loop.Balance = balance;
                    loop.Initialize(service, true);
                    nav.ShowActivities();
                    Prepare(name, nav, loop, service, clock);
                    CaptureView(name, size, nav, loop, output);
                }
            }
            File.WriteAllText(Path.Combine(root, "capture-manifest.csv"), Manifest.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "text-metrics.csv"), Metrics.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "text-overflow.csv"), Overflow.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "scroll-metrics.csv"), ScrollMetrics.ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(root, "environment.txt"),
                "Unity Editor synthetic UI captures\nInput: MOCK\nState: in-memory; no application save written\n" +
                "Renderer: " + SystemInfo.graphicsDeviceName + "\nGraphics API: " + SystemInfo.graphicsDeviceType +
                "\nImages: " + count + "\nPotential text overflows: " + overflows +
                "\nSettings chart action: Editor unsupported feedback only; native SensorHistoryActivity requires an Android device.\n" +
                "Record-heart cases: Unity HeartHistoryView with exact-range synthetic heart samples.\n" +
                "\nFont pixels = font size × screenshot width / root Canvas width × local scale.\n" +
                "Entirely masked labels are excluded from overflow findings. Vertically fitted content labels are not treated as clipping.\n" +
                "Device tested: false\n", Encoding.UTF8);
            Debug.Log("TAB_CONSISTENCY_CAPTURE_PASS images=" + count + " overflows=" + overflows + " output=" + output);
        }

        static void CaptureView(string name, Vector2Int size, MockupNavigation nav, WeekOneQuestDemo loop, string output)
        {
            string file = name + "-" + size.x + "x" + size.y + ".png";
            string path = Path.Combine(output, file);
            // Warm-up lets width constraints and fitted text settle at this target size.
            LocalLoopPreview.Render(size, path);
            var scope = name.StartsWith("settings", StringComparison.Ordinal)
                ? nav.GetComponentInChildren<SensorRawDisplay>(true).transform : loop.transform;
            bool bottom = name.EndsWith("-bottom", StringComparison.Ordinal);
            SetOuterScroll(scope, bottom ? 0 : 1);
            if (name.StartsWith("journal-detail", StringComparison.Ordinal) ||
                name.StartsWith("practice-answers", StringComparison.Ordinal))
            {
                foreach (var scroll in scope.GetComponentsInChildren<ScrollRect>())
                    if (scroll.isActiveAndEnabled && scroll.gameObject.name == "Scrollable record")
                        scroll.verticalNormalizedPosition = bottom ? 0 : 1;
            }
            if (name == "settings-diagnostics-top")
            {
                var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
                ScrollTo(sensor.PrimaryScroll, sensor.PrimaryScroll.content.Find("Detailed diagnostics") as RectTransform);
            }
            RefreshScrollVisuals(scope);
            LocalLoopPreview.Render(size, path);
            AppendMetrics(file, size);
            foreach (var scroll in scope.GetComponentsInChildren<ScrollRect>())
            {
                if (!scroll.isActiveAndEnabled || scroll.content == null || scroll.viewport == null) continue;
                ScrollMetrics.Append(Csv(file)).Append(',').Append(Csv(Hierarchy(scroll.transform))).Append(',')
                    .Append(Number(scroll.verticalNormalizedPosition)).Append(',')
                    .Append(scroll.verticalScrollbar == null ? "none" : Number(scroll.verticalScrollbar.value)).Append(',')
                    .Append(scroll.verticalScrollbar == null ? "none" : Number(scroll.verticalScrollbar.size)).Append(',')
                    .Append(Number(scroll.viewport.rect.height)).Append(',').Append(Number(scroll.content.rect.height)).AppendLine();
            }
            Manifest.Append(Csv(file)).Append(',').Append(size.x).Append(',').Append(size.y).Append(',')
                .Append(Csv(name)).Append(',').Append(bottom ? "bottom" : "top").AppendLine();
            count++;
        }

        static void CaptureAllGuidance(string output)
        {
            foreach (var size in Sizes)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                var nav = UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var loop = nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                var clock = new Clock();
                var balance = new DemoBalanceConfig { VisitorChancePercent = 0 };
                using (var service = new GardenStateService(new Store(), clock, balance))
                {
                    loop.Balance = balance;
                    loop.Initialize(service, true);
                    nav.ShowActivities();
                    foreach (var mission in MbctContent.Course)
                    {
                        AllowNext(service, clock);
                        loop.SelectMission(mission);
                        string id = service.Snapshot.Sessions.Last().SessionId;
                        string prefix = "practice-" + mission.recommendedOrder.ToString("00");
                        CaptureView(prefix + "-selected", size, nav, loop, output);
                        Require(service.StartSession(id), service);
                        for (int step = 0; step < mission.steps.Length; step++)
                        {
                            loop.ShowSession();
                            string name = prefix + "-step-" + (step + 1).ToString("00");
                            CaptureView(name + "-top", size, nav, loop, output);
                            if (loop.GetComponentsInChildren<ScrollRect>().Any(scroll => scroll.content != null &&
                                scroll.viewport != null && scroll.content.rect.height > scroll.viewport.rect.height + 2))
                                CaptureView(name + "-bottom", size, nav, loop, output);
                            Require(service.SavePracticeStep(id, step + 1), service);
                        }
                        loop.Finish(null, null);
                    }
                }
            }
        }

        static void Prepare(string name, MockupNavigation nav, WeekOneQuestDemo loop, GardenStateService service, Clock clock)
        {
            if (name == "reference-home") { nav.ShowIsland(); return; }
            if (name.StartsWith("settings", StringComparison.Ordinal))
            {
                var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
                sensor.PrepareView();
                nav.ShowSettings();
                sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
                if (name.Contains("diagnostics"))
                {
                    sensor.ToggleDiagnostics();
                }
                if (name.Contains("chart-action")) Click(sensor.transform, "심박 차트 보기");
                return;
            }
            if (name.StartsWith("activities", StringComparison.Ordinal)) { loop.ShowHome(); return; }
            if (name.StartsWith("course", StringComparison.Ordinal))
            {
                loop.ShowCourse();
                if (name.StartsWith("course-week5", StringComparison.Ordinal))
                    for (int week = 1; week < 5; week++) Click(loop.transform, "다음 주");
                return;
            }
            if (name == "free-empty") { loop.ShowFree(); return; }
            if (name == "records-empty") { loop.ShowRecords(); return; }
            if (name == "plan") { loop.ShowPersonalPlan(); return; }
            if (name == "plan-input")
            {
                Require(service.SavePreference("warning-action", "잠시 쉬며 상태를 확인하고, 도움이 필요하면 믿을 만한 사람에게 이야기하기"), service);
                loop.ShowPlanField("warning-action");
                return;
            }
            if (name.StartsWith("stress-", StringComparison.Ordinal))
            {
                var candidates = MbctContent.Course.SelectMany(mission => mission.steps.Select((step, index) =>
                    new { Mission = mission, Step = step, Index = index }));
                if (name == "stress-longest-input-guidance") candidates = candidates.Where(item => item.Step.answerKey != null);
                var chosen = candidates.OrderByDescending(item => name == "stress-longest-choice"
                    ? item.Step.options.Select(option => option.Length).DefaultIfEmpty(0).Max()
                    : name == "stress-longest-title" ? item.Mission.title.Length : item.Step.guidance.Length).First();
                SeedBefore(service, clock, chosen.Mission.recommendedOrder);
                loop.SelectMission(chosen.Mission);
                string stressId = service.Snapshot.Sessions.Last().SessionId;
                Require(service.StartSession(stressId), service);
                Require(service.SavePracticeStep(stressId, chosen.Index), service);
                loop.ShowSession();
                Debug.Log("TAB_CAPTURE_STRESS " + name + " mission=" + chosen.Mission.id + " step=" + (chosen.Index + 1));
                return;
            }
            if (name.StartsWith("journal", StringComparison.Ordinal))
            {
                loop.ShowExperienceTypes();
                if (name == "journal-emotion")
                {
                    Click(loop.transform, "불편함");
                    Click(loop.transform, "다음");
                    Click(loop.transform, "다음");
                }
                return;
            }
            if (name == "free-learned" || name.StartsWith("records-populated", StringComparison.Ordinal) ||
                name == "record-detail" || name.StartsWith("record-heart", StringComparison.Ordinal))
            {
                for (int i = 1; i <= 5; i++)
                {
                    while (MbctPolicy.Availability(service.Snapshot) != null)
                    {
                        clock.Time = clock.Time.AddDays(1);
                        Require(service.RefreshCycle(Array.Empty<string>()), service);
                    }
                    Require(service.BeginSession("fixture-" + i, MbctPolicy.MissionId(i), i), service);
                    Require(service.StartSession("fixture-" + i), service);
                    clock.Time = clock.Time.AddMinutes(5);
                    Require(service.CompleteSession("fixture-" + i), service);
                }
                if (name == "free-learned") loop.ShowFree();
                else if (name == "record-detail" || name.StartsWith("record-heart", StringComparison.Ordinal))
                {
                    if (name.StartsWith("record-heart", StringComparison.Ordinal))
                    {
                        var session = service.Snapshot.Sessions.First(item => item.SessionId == "fixture-1");
                        long start = DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds();
                        long end = DateTimeOffset.Parse(session.EndedUtc).ToUnixTimeMilliseconds();
                        var metric = new HealthMetricSnapshot
                        {
                            status = "AVAILABLE", queryComplete = true, queryStartEpochMs = start, queryEndEpochMs = end,
                            sampleCount = 4, displaySampleCount = 4,
                            heartRateSamples = new[]
                            {
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 1000, beatsPerMinute = 73, sourcePackage = "합성 출처 A", segmentId = 0 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 70000, beatsPerMinute = 76, sourcePackage = "합성 출처 A", segmentId = 0 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 190000, beatsPerMinute = 75, sourcePackage = "합성 출처 B", segmentId = 1 },
                                new HeartRateSampleSnapshot { measuredAtEpochMs = start + 240000, beatsPerMinute = 72, sourcePackage = "합성 출처 B", segmentId = 1 }
                            }
                        };
                        loop.PreviewHealth = new AndroidPlatformSnapshot
                        {
                            inputMode = "MOCK", healthConnect = new HealthConnectSnapshot { heartRate = metric }
                        };
                    }
                    loop.ShowRecord("fixture-1");
                }
                else loop.ShowRecords();
                return;
            }
            loop.SelectMission(MbctContent.Course[0]);
            string id = service.Snapshot.Sessions.Last().SessionId;
            if (name.StartsWith("practice-selected", StringComparison.Ordinal)) return;
            Require(service.StartSession(id), service);
            if (name.StartsWith("reflection", StringComparison.Ordinal)) loop.ShowReflection();
            else if (name == "complete") loop.Finish(null, null);
            else
            {
                if (name == "practice-paused") Require(service.PauseSession(id), service);
                loop.ShowSession();
            }
        }

        static void PrepareRemaining(string name, MockupNavigation nav, WeekOneQuestDemo loop,
            GardenStateService service, Store store, Clock clock, string output)
        {
            if (name.StartsWith("settings-", StringComparison.Ordinal))
            {
                if (name.StartsWith("settings-live", StringComparison.Ordinal)) AndroidPlatformBridge.UseLiveProvider();
                else
                {
                    string replay = Path.Combine(output, "replay-fixture.jsonl");
                    File.WriteAllText(replay, JsonUtility.ToJson(AndroidPlatformBridge.GetSnapshot()) + "\n", Encoding.UTF8);
                    var loaded = AndroidPlatformBridge.UseReplayProvider(replay);
                    if (loaded.status != "AVAILABLE") throw new InvalidOperationException(loaded.message);
                }
                var sensor = nav.GetComponentInChildren<SensorRawDisplay>(true);
                sensor.PrepareView();
                nav.ShowSettings();
                sensor.SendMessage("RefreshText", SendMessageOptions.RequireReceiver);
                return;
            }
            if (name.StartsWith("shop-", StringComparison.Ordinal)) { loop.ShowShop(); return; }
            if (name.StartsWith("growth-", StringComparison.Ordinal))
            {
                loop.ShowShop();
                loop.PreviewGrowth();
                if (name == "growth-committed") Click(loop.transform, "확정");
                return;
            }
            if (name.StartsWith("placement-", StringComparison.Ordinal))
            {
                if (name.StartsWith("placement-flower-owned", StringComparison.Ordinal))
                    Require(service.PurchaseEnvironment("fixture-owned", "environment:E01", "left"), service);
                loop.ShowShop();
                loop.PreviewEnvironment(name.StartsWith("placement-shelter", StringComparison.Ordinal)
                    || name.StartsWith("placement-save-error", StringComparison.Ordinal) ? "environment:E02" : "environment:E01");
                if (name.StartsWith("placement-save-error", StringComparison.Ordinal))
                {
                    store.Fail = true;
                    try { Click(loop.transform, "확정"); }
                    finally { store.Fail = false; }
                }
                else if (name == "placement-flower-committed") Click(loop.transform, "확정");
                return;
            }
            if (name.StartsWith("discoveries-", StringComparison.Ordinal))
            {
                if (name.StartsWith("discoveries-populated", StringComparison.Ordinal))
                {
                    clock.Time = clock.Time.AddDays(1);
                    Require(service.RefreshCycle(new[] { "animal:A07" }), service);
                }
                loop.ShowDiscoveries();
                return;
            }
            if (name.StartsWith("journal-", StringComparison.Ordinal))
            {
                if (!name.StartsWith("journal-history-empty", StringComparison.Ordinal))
                {
                    string sentence = "햇살 아래서 잠시 멈추고 숨을 골랐어요. 마음이 천천히 가라앉는 것을 느꼈어요. ";
                    string longText = string.Concat(Enumerable.Repeat(sentence, 4));
                    for (int i = 0; i < 6; i++)
                        Require(service.SaveExperience("fixture-experience-" + i, "불편함", longText,
                            longText, longText, longText), service);
                }
                if (name.StartsWith("journal-detail", StringComparison.Ordinal))
                    loop.ShowExperienceRecord("fixture-experience-0");
                else loop.ShowExperienceHistory();
                return;
            }
            if (name.StartsWith("practice-answers", StringComparison.Ordinal))
            {
                loop.SelectMission(MbctContent.Course[0]);
                string id = service.Snapshot.Sessions.Last().SessionId;
                Require(service.StartSession(id), service);
                if (name.StartsWith("practice-answers-populated", StringComparison.Ordinal))
                {
                    string answer = string.Concat(Enumerable.Repeat("숨을 고르며 몸과 마음을 천천히 살펴봤어요. ", 6));
                    var keys = MbctContent.Course.SelectMany(mission => mission.steps)
                        .Where(step => step.answerKey != null).Select(step => step.answerKey).Distinct().Take(5).ToArray();
                    for (int i = 0; i < keys.Length; i++)
                        Require(service.SavePracticeStep(id, 0, keys[i], answer), service);
                }
                Require(service.CompleteSession(id), service);
                loop.ShowPracticeAnswers(id);
                return;
            }
            if (name.StartsWith("record-health", StringComparison.Ordinal))
            {
                Require(service.BeginSession("fixture-health", MbctPolicy.MissionId(1), 1), service);
                Require(service.StartSession("fixture-health"), service);
                clock.Time = clock.Time.AddMinutes(5);
                Require(service.CompleteSession("fixture-health"), service);
                var session = service.Snapshot.Sessions.Last();
                string status = name.Contains("permission-denied") ? "PERMISSION_DENIED" :
                    name.Contains("unsupported") ? "UNSUPPORTED" : "ERROR";
                loop.PreviewHealth = new AndroidPlatformSnapshot
                {
                    inputMode = "MOCK",
                    healthConnect = new HealthConnectSnapshot
                    {
                        heartRate = new HealthMetricSnapshot
                        {
                            status = status, queryComplete = false,
                            queryStartEpochMs = DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds(),
                            queryEndEpochMs = DateTimeOffset.Parse(session.EndedUtc).ToUnixTimeMilliseconds()
                        }
                    }
                };
                loop.ShowRecord("fixture-health");
                return;
            }
            throw new InvalidOperationException("Unknown remaining capture case: " + name);
        }

        static void Require(bool success, GardenStateService service)
        {
            if (!success) throw new InvalidOperationException(service.LastError);
        }

        static void AllowNext(GardenStateService service, Clock clock)
        {
            while (MbctPolicy.Availability(service.Snapshot) != null)
            {
                clock.Time = clock.Time.AddDays(1);
                Require(service.RefreshCycle(Array.Empty<string>()), service);
            }
        }

        static void SeedBefore(GardenStateService service, Clock clock, int order)
        {
            for (int index = 1; index < order; index++)
            {
                AllowNext(service, clock);
                string id = "stress-seed-" + index;
                Require(service.BeginSession(id, MbctPolicy.MissionId(index), index), service);
                Require(service.StartSession(id), service);
                Require(service.CompleteSession(id), service);
            }
            AllowNext(service, clock);
        }

        static void Click(Transform scope, string name)
        {
            var button = scope.GetComponentsInChildren<Button>(true).FirstOrDefault(item =>
                item.gameObject.activeInHierarchy && item.name == name);
            if (button == null || !button.interactable) throw new InvalidOperationException("Enabled button missing: " + name);
            button.onClick.Invoke();
        }

        static void SetOuterScroll(Transform scope, float position)
        {
            foreach (var scroll in scope.GetComponentsInChildren<ScrollRect>(true))
            {
                if (!scroll.isActiveAndEnabled || scroll.content == null) continue;
                // Leave inner record/diagnostic text at its own initial position.
                var parentScroll = scroll.transform.parent == null ? null : scroll.transform.parent.GetComponentInParent<ScrollRect>();
                if (parentScroll != null && parentScroll.isActiveAndEnabled) continue;
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = position;
            }
        }

        static void ScrollTo(ScrollRect scroll, RectTransform target)
        {
            if (scroll == null || target == null) throw new InvalidOperationException("Diagnostics section is missing.");
            float top = scroll.content.rect.yMax - scroll.content.InverseTransformPoint(
                target.TransformPoint(new Vector3(0, target.rect.yMax, 0))).y;
            float extent = scroll.content.rect.height - scroll.viewport.rect.height;
            if (extent > 0) scroll.verticalNormalizedPosition = 1 - Mathf.Clamp01(top / extent);
        }

        static void RefreshScrollVisuals(Transform scope)
        {
            foreach (var scroll in scope.GetComponentsInChildren<ScrollRect>())
            {
                if (!scroll.isActiveAndEnabled || scroll.content == null) continue;
                // SetNormalizedPosition moves only content in UGUI; the normal
                // player loop updates scrollbar values and AutoHide in LateUpdate.
                // Execute that same maintenance during this synchronous editor capture.
                scroll.Rebuild(CanvasUpdate.PostLayout);
                scroll.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                // UGUI deliberately keeps AutoHide bars visible in edit mode.
                // Match the player's visibility rule for this rendered preview.
                if (!Application.isPlaying && scroll.verticalScrollbar != null && scroll.viewport != null &&
                    scroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHide)
                    scroll.verticalScrollbar.gameObject.SetActive(scroll.content.rect.height > scroll.viewport.rect.height + .01f);
            }
        }

        static void AppendMetrics(string image, Vector2Int size)
        {
            foreach (var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                var rect = text.rectTransform;
                bool visible = IntersectsMasks(rect);
                AppendMetric(image, rect, "Text", text.text, text.fontSize, text.preferredHeight, visible, size);
                if (!visible) continue;
                var fit = text.GetComponent<ContentSizeFitter>();
                bool fitsVertically = fit != null && fit.isActiveAndEnabled && fit.verticalFit == ContentSizeFitter.FitMode.PreferredSize;
                if (!fitsVertically && text.preferredHeight > rect.rect.height + .25f)
                    AppendOverflow(image, rect, text.text, "height", text.preferredHeight, rect.rect.height);
                if (text.horizontalOverflow == HorizontalWrapMode.Overflow && text.preferredWidth > rect.rect.width + 2)
                    AppendOverflow(image, rect, text.text, "width", text.preferredWidth, rect.rect.width);
            }
            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                var rect = text.rectTransform;
                bool visible = IntersectsMasks(rect);
                float height = text.GetPreferredValues(text.text, rect.rect.width, float.PositiveInfinity).y;
                AppendMetric(image, rect, "TMP", text.text, text.fontSize, height, visible, size);
                if (visible && text.isTextOverflowing && !text.enableAutoSizing)
                    AppendOverflow(image, rect, text.text, "TMP-overflow", height, rect.rect.height);
            }
        }

        static void AppendMetric(string image, RectTransform rect, string renderer, string value, float fontSize,
            float preferredHeight, bool visible, Vector2Int size)
        {
            var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            float scale = 1;
            if (canvas != null)
            {
                var canvasRect = (RectTransform)canvas.transform;
                scale = size.x / Mathf.Max(1, canvasRect.rect.width) * Mathf.Abs(rect.lossyScale.x / canvasRect.lossyScale.x);
            }
            Metrics.Append(Csv(image)).Append(',').Append(Csv(Hierarchy(rect))).Append(',').Append(renderer).Append(',')
                .Append(Csv(value)).Append(',').Append(Number(fontSize)).Append(',').Append(Number(scale)).Append(',')
                .Append(Number(fontSize * scale)).Append(',').Append(Number(rect.rect.width)).Append(',')
                .Append(Number(rect.rect.height)).Append(',').Append(Number(preferredHeight)).Append(',')
                .Append(visible ? "visible-or-partial" : "outside-mask").AppendLine();
        }

        static void AppendOverflow(string image, RectTransform rect, string value, string kind, float preferred, float available)
        {
            Overflow.Append(Csv(image)).Append(',').Append(Csv(Hierarchy(rect))).Append(',').Append(Csv(value)).Append(',')
                .Append(kind).Append(',').Append(Number(preferred)).Append(',').Append(Number(available)).Append(',')
                .Append(rect.GetComponentInParent<ScrollRect>() != null ? "true" : "false").AppendLine();
            overflows++;
        }

        static bool IntersectsMasks(RectTransform label)
        {
            for (Transform ancestor = label.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var rectMask = ancestor.GetComponent<RectMask2D>();
                var mask = ancestor.GetComponent<Mask>();
                if ((rectMask == null || !rectMask.isActiveAndEnabled) && (mask == null || !mask.isActiveAndEnabled)) continue;
                var viewport = (RectTransform)ancestor;
                var corners = new Vector3[4];
                label.GetWorldCorners(corners);
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = viewport.InverseTransformPoint(corner);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                if (!viewport.rect.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y))) return false;
            }
            return true;
        }

        static string Hierarchy(Transform target) => target.parent == null ? target.name : Hierarchy(target.parent) + "/" + target.name;
        static string Number(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
        static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"").Replace("\n", " / ").Replace("\r", "") + "\"";
    }
}
