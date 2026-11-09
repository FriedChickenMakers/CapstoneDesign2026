using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit isolated batch only. No poll, automatic refresh, scene asset or real save access.
    public static class BasicFocusSlidesValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, environment;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<Digest> digests = new List<Digest>();
            public List<string> captures = new List<string>(), errors = new List<string>();
            public List<string> visualMeasurements = new List<string>();
        }
        [Serializable] sealed class Digest { public string key, sha256, content; }
        static Report report;
        static string output;
        static bool capture;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-basicSlidesReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || (!Application.productName.StartsWith("BasicSlides-", StringComparison.Ordinal) && Application.productName != "MemoPopup-v07") || index < 0)
                throw new InvalidOperationException("BasicFocusSlidesValidation requires a separate synthetic batch project.");
            string path = args[index + 1]; output = Path.GetDirectoryName(path); Directory.CreateDirectory(output); capture = args.Contains("-basicSlidesCapture");
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                environment = "Hidden isolated Unity edit-mode batch; actual runtime page factories, memory state, pointer raycasts/clicks; capture=" + capture };
            Application.logMessageReceived += Observe;
            try
            {
                Run("Additional M01-M17 actual page digests", AdditionalDigests);
                string baseline = Arg("-basicBaselinePath");
                if (!string.IsNullOrEmpty(baseline)) Run("M UI matches preserved pre-change baseline", () => CompareDigests(baseline));
                if (!args.Contains("-basicBaseline"))
                {
                    foreach (var size in new[] { new Vector2Int(320, 568), new Vector2Int(360, 640), new Vector2Int(390, 844) })
                        Run("P08-P28 complete fixed-icon slide flow " + size.x + "x" + size.y, () => BasicFlows(size.x, size.y));
                    Run("V5 separate P19/P26 alternatives and persisted V4 branches/audio", SpecialBranches);
                    Run("Persisted P10 version1/version2 keep original UI", LegacyPages);
                    Run("Review edit, branch reversal and unsaved elapsed failure recovery", EditingAndTimerRecovery);
                }

            }
            finally
            {
                Application.logMessageReceived -= Observe;
                if (report.errors.Count > 0) { report.failed++; report.checks.Add(new Check { name = "Unity errors", status = "fail", detail = string.Join("\n", report.errors) }); }
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            Debug.Log("BASIC_SLIDES " + (report.failed == 0 ? "PASS" : "FAIL") + " assertions=" + report.assertions);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Run(string name, Action action)
        { try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
          catch (Exception ex) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = ex.ToString() }); } }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static string Screen(WeekOneQuestDemo ui) => (string)typeof(WeekOneQuestDemo).GetField("screen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        static string Arg(string key)
        { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        static void NextDay(Fixture f)
        {
            do { f.Clock.Time = f.Clock.Time.AddDays(1); Assert(f.Service.RefreshCycle(Array.Empty<string>()), f.Service.LastError); }
            while (FocusMissionPolicy.Availability(f.Service.Snapshot, "8w") != null);
        }
        static void SeedBefore(Fixture f, int order)
        {
            Assert(f.Service.SelectMbctProgram("8w"), f.Service.LastError);
            for (int n = 1; n < order; n++)
            {
                if (n > 1) NextDay(f);
                string id = "synthetic-predecessor-" + n; var def = FocusMissionCatalog.Find("P" + n.ToString("00"));
                Assert(f.Service.BeginFocusSession(id, def.Code, n, program: "8w") && f.Service.StartSession(id), f.Service.LastError);
                int limit = 0;
                while (f.Session(id).InstructionStep < def.Steps.Length)
                {
                    Assert(++limit <= def.Steps.Length, "Bounded synthetic predecessor");
                    int step = f.Session(id).InstructionStep;
                    var instruction = def.Steps[step];
                    string answer = instruction.RequiredAnswer ? instruction.Options.FirstOrDefault() ?? "synthetic required answer" : null;
                    Assert(f.Service.ConfirmFocusStep(id, step, answer, answer == null), f.Service.LastError);
                }
                Assert(f.Service.CompletePracticeSession(id), f.Service.LastError);
            }
            NextDay(f);
        }
        static void BasicFlows(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                SeedBefore(f, 8);
                for (int order = 8; order <= 28; order++)
                {
                    if (order > 8) NextDay(f);
                    string id = "synthetic-basic-ui-" + order, code = "P" + order.ToString("00"); var def = FocusMissionCatalog.Find(code);
                    Assert(f.Service.BeginFocusSession(id, code, order, program: "8w"), f.Service.LastError);
                    f.Ui.SelectFocusSession(id); f.Layout();
                    f.AssertBasicBody("Basic.Overview"); Rect anchor = f.AssertIcon();
                    Assert(f.Button("Focus.Close").GetComponentInChildren<Text>().text == "나가기", code + " actual exit label");
                    Assert(f.Session(id).Status == ParticipationState.Selected && f.Button("Focus.Action", false) == null,
                        code + " overview keeps original selected domain state and central action");
                    if (order == 8) f.AssertCenteredPress();
                    f.Capture(code + "-overview");
                    var overviewPixels = order == 8 ? f.Pixels() : null;
                    Rect overviewArea = f.ArtBounds();
                    var oldStart = f.Button("Focus.IconAction").onClick;
                    f.Ui.gameObject.SetActive(false); oldStart.Invoke(); f.Ui.gameObject.SetActive(true); f.Layout();
                    Assert(f.Session(id).Status == ParticipationState.Selected && f.Text("Basic.Overview") != null, "Hidden old basic callback is ignored");
                    f.Click("Focus.IconAction"); oldStart.Invoke(); f.Layout();
                    Assert(f.Session(id).InstructionStep == 0 && f.Session(id).Status == ParticipationState.InProgress,
                        code + " stale overview action cannot skip first persisted step");
                    anchor = f.AssertIcon();
                    var firstStepPixels = order == 8 ? f.Pixels() : null;
                    Rect firstStepArea = f.ArtBounds();
                    f.Click("Basic.Previous"); f.AssertBasicBody("Basic.Overview");
                    if (order == 8) Assert(f.ChangedPixels(overviewPixels, f.Pixels(), overviewArea) == 0,
                        "P08 first-step previous restores the original introduction picture without residual stage effects.");
                    Assert(Screen(f.Ui) == "focus-practice", code + " first-step previous opens overview instead of leaving mission");
                    f.Click("Focus.IconAction");
                    if (order == 8) Assert(f.ChangedPixels(firstStepPixels, f.Pixels(), firstStepArea) == 0,
                        "P08 advancing again restores the original feeling picture without duplicating the start.");
                    var beforeGrowth = f.Service.Snapshot; var beforePlant = WeeklyPlants.Active(beforeGrowth);
                    int coins = f.Service.CoinBalance;
                    FinishBasic(f, id, def, anchor, true);
                    Assert(f.Session(id).Status == ParticipationState.RewardCommitted && Screen(f.Ui) == "focus-complete"
                        && f.Service.CoinBalance == coins + 50, code + " completed once with 50 coins");
                    var completedArt = f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false);
                    Assert(completedArt != null && completedArt.MissionCode == code && completedArt.StepKey == "reflection",
                        code + " completion keeps its mission illustration.");
                    if (code == "P12") Assert(completedArt.Choice == "햇볕", "P12 completion preserves the observed experience.");
                    if (code == "P18") Assert(completedArt.Choice == "주변 소리", "P18 completion preserves the last practiced target, not the original choice.");
                    if (code == "P26") Assert(completedArt.Choice == "사람", "P26 completion preserves the chosen kind of help.");
                    var growth = f.Service.GetPracticeGrowth(id);
                    var afterGrowth = f.Service.Snapshot; var afterPlant = WeeklyPlants.Active(afterGrowth);
                    Assert(growth != null && growth.Outcome == PracticeGrowthOutcome.Grown
                        && growth.PlantInstanceId == afterPlant.Id && growth.AfterStep == growth.BeforeStep + 1
                        && afterPlant.Recipe.GrowthStep == growth.AfterStep
                        && afterGrowth.PracticeGrowthRecords.Count == beforeGrowth.PracticeGrowthRecords.Count + 1
                        && afterGrowth.PlantCollection.Decisions.Count == beforeGrowth.PlantCollection.Decisions.Count + 1
                        && afterGrowth.StoredGrowthSteps == beforeGrowth.StoredGrowthSteps,
                        code + " exactly one full growth receipt applies to its identified individual");
                    if (beforePlant.Completed)
                        Assert(afterPlant.Id != beforePlant.Id && growth.BeforeStep == 0 && growth.AfterStep == 1
                            && afterPlant.Recipe.MaxGrowthSteps == 21 && afterGrowth.PlantCollection.Plants.Count == beforeGrowth.PlantCollection.Plants.Count + 1
                            && afterGrowth.PlantPhotos.Count == beforeGrowth.PlantPhotos.Count
                            && afterGrowth.PlantPhotos.Any(p => p.SourcePlantInstanceId == beforePlant.Id)
                            && CapstoneDesign.Plants.PlantCodec.Encode(CombinedPlants.Find(afterGrowth, beforePlant.Id).Recipe)
                                == CapstoneDesign.Plants.PlantCodec.Encode(beforePlant.Recipe),
                            code + " completed legacy individual stays archived while its next 21-step plant grows once");
                    else
                        Assert(afterPlant.Id == beforePlant.Id && growth.BeforeStep == beforePlant.Recipe.GrowthStep
                            && afterPlant.Recipe.GrowthStep == beforePlant.Recipe.GrowthStep + 1,
                            code + " unfinished individual advances exactly one stage without replacement");
                    f.Capture(code + "-complete");
                }
            }
        }
        static void FinishBasic(Fixture f, string id, FocusMissionDefinition def, Rect anchor, bool exercise, string route = null)
        {
            int limit = 0;
            while (f.Session(id).Status != ParticipationState.RewardCommitted)
            {
                Assert(++limit <= def.Steps.Length + 1, "Bounded basic progression " + def.Code);
                int step = f.Session(id).InstructionStep; var instruction = def.Steps[step];
                f.AssertBasicBody("Basic.Body"); f.AssertIcon(anchor);
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Count(t => t.name == "Basic.Body") == 1, def.Code + " displays one instruction at a time");
                if (exercise && (step == 0 || instruction.Kind == "listen" || instruction.Kind == "wait" || instruction.Kind == "review"))
                    f.Capture(def.Code + "-" + instruction.Key);
                if (exercise && f.Button("Basic.HelpToggle", false) != null)
                {
                    int elapsed = f.Ui.GetComponentInChildren<FocusPracticeClock>(false)?.ElapsedSeconds ?? 0;
                    var priorClock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                    f.Click("Basic.HelpToggle");
                    f.AssertBasicBody("Basic.Help"); f.AssertIcon(anchor);
                    Assert(f.Text("Basic.Help").text == string.Join("\n", FocusBasicSlideContent.Help(def.Code, instruction.Key))
                        && !f.Text("Basic.Progress").text.Contains("1/") && f.Session(id).InstructionStep == step
                        && f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null,
                        def.Code + " shows all concise help on one page without confirmation or a running timer");
                    if (step == 0) f.Capture(def.Code + "-help-single");
                    f.Click("Focus.IconAction");
                    Assert(f.Text("Basic.Help") == null && f.Text("Basic.Body") != null && f.Session(id).InstructionStep == step,
                        def.Code + " one central help click returns directly to the same practice step");
                    Assert(priorClock == null || !priorClock.Running, def.Code + " help pauses prior timer");
                    Assert((f.Ui.GetComponentInChildren<FocusPracticeClock>(false)?.ElapsedSeconds ?? elapsed) == elapsed,
                        def.Code + " help return preserves elapsed");
                }
                var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                if (clock != null)
                {
                    if (!clock.Running) f.Click("Focus.TimerToggle");
                    Assert(clock.Running, def.Code + " timer starts on demand"); clock.ElapsedSeconds = 13;
                    Assert(clock.Flush(), "Save current timed step");
                    f.Click("Focus.TimerToggle"); Assert(!clock.Running, def.Code + " timer pauses");
                    if (def.Code == "P18" && instruction.Options.Length > 0)
                    {
                        var targetPixels = f.Pixels(); Rect targetArea = f.ArtBounds();
                        f.Click("Focus.TimerToggle"); f.Click("Focus.Option." + instruction.Options.Last());
                        var updated = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                        Assert(f.Session(id).InstructionStep == step && updated != null && updated.Running && updated.ElapsedSeconds == 13
                            && f.Session(id).FocusStepRecords[step].Value == instruction.Options.Last(),
                            "P18 target change preserves active step and elapsed running timer");
                        Assert(f.Text("Basic.Body").text.Contains("소리"), "P18 changed target has concrete sound guidance");
                        f.AssertBasicBody("Basic.Body");
                        var art = f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false);
                        Assert(art.Choice == instruction.Options.Last() && f.ChangedPixels(targetPixels, f.Pixels(), targetArea) >= 24,
                            "P18 target change also updates the actual illustration while preserving its timer and step.");
                        f.Click("Focus.TimerToggle");
                    }
                }
                if (instruction.Kind == "input")
                {
                    var input = f.Ui.GetComponentsInChildren<InputField>(false).Single(i => i.name == "Focus.AnswerInput");
                    bool soundNotice = def.Code == "P19" && instruction.Key == "notice"
                        && f.Session(id).FocusStepRecords.Any(record => record.Key == "route" && record.Value == "짧은 소리 실습");
                    if (instruction.Options.Length > 0)
                    {
                        f.Click("Focus.Option." + instruction.Options[0]);
                        Assert(f.Session(id).InstructionStep == step && input.text == instruction.Options[0],
                            def.Code + " input suggestion fills draft without confirming");
                        if (soundNotice) Assert(f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false).Choice == "짧은 소리 실습",
                            "P19 selecting a reflection suggestion preserves the sound-route picture instead of changing it to a mountain.");
                    }
                    input.text = "synthetic " + def.Code + " " + instruction.Key; input.onEndEdit.Invoke(input.text); f.Layout();
                    f.RevealRect((RectTransform)input.transform); f.AssertFullyVisible((RectTransform)input.transform, "Optional free-text input"); f.AssertIcon(anchor);
                    if (soundNotice)
                    {
                        Assert(Screen(f.Ui) == "focus-practice" && f.Session(id).InstructionStep == step
                            && f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false).Choice == "짧은 소리 실습",
                            "P19 free-text reflection keeps the chosen listening route while remaining on the same page.");
                        string before = StateCodec.Encode(f.Service.Snapshot), retry = "synthetic P19 sound reflection retry";
                        f.Store.Fail = true; input.text = retry; input.onEndEdit.Invoke(retry); f.Store.Fail = false; f.Layout();
                        var art = f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false);
                        Assert(StateCodec.Encode(f.Service.Snapshot) == before && Screen(f.Ui) == "focus-practice"
                            && f.Session(id).InstructionStep == step && input.text == retry
                            && art.MissionCode == "P19" && art.StepKey == "notice" && art.Choice == "짧은 소리 실습",
                            "Failed P19 reflection saving retains the local text and sound illustration without overwriting durable state or the route.");
                        f.AssertBasicBody("Basic.Body"); f.Capture("P19-sound-notice-save-failed");
                        input.onEndEdit.Invoke(retry); f.Layout();
                        Assert(f.Session(id).FocusStepRecords[step].Value == retry
                            && f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false).Choice == "짧은 소리 실습",
                            "P19 reflection retry saves the response while keeping its original sound-route illustration.");
                    }
                    if (exercise && def.Code == "P08")
                    {
                        string value = input.text; f.Click("Focus.Close"); Assert(Screen(f.Ui) == "home", "Exit returns to mission home");
                        f.Ui.SelectFocusSession(id); f.Layout();
                        Assert(f.Session(id).InstructionStep == step && f.Ui.GetComponentInChildren<InputField>(false).text == value,
                            "Exit and reentry preserve current basic input draft");
                    }
                }
                if (instruction.Kind == "listen" && f.Button("Focus.AudioTranscript", false) != null)
                {
                    var audio = f.Ui.GetComponentInChildren<FocusAudioPlayer>(false);
                    Assert(audio != null && audio.Source != null && audio.Source.clip != null && audio.Source.clip.length > 0,
                        "P24 keeps existing audio resource");
                    f.Click("Focus.AudioToggle");
                    Assert(audio.Source.isPlaying, "P24 actual AudioSource starts playing");
                    audio.Source.time = Math.Min(2f, audio.Source.clip.length * .25f);
                    float resumePosition = audio.Source.time;
                    f.Click("Focus.AudioTranscript"); int pages = 0; var transcript = new StringBuilder();
                    while (f.Text("Basic.Transcript") != null)
                    {
                        Assert(++pages <= 40, "Bounded transcript pages"); f.AssertBasicBody("Basic.Transcript"); f.AssertIcon(anchor);
                        transcript.Append(f.Text("Basic.Transcript").text); f.Click("Focus.IconAction");
                    }
                    Assert(pages > 1 && transcript.Length > 50 && f.Session(id).InstructionStep == step
                        && f.Button("Focus.AudioToggle", false) != null, "P24 transcript pages return to the same listening step without confirming");
                    var returnedAudio = f.Ui.GetComponentInChildren<FocusAudioPlayer>(false);
                    Assert(returnedAudio != null && !returnedAudio.Source.isPlaying && Math.Abs(returnedAudio.PlaybackPosition - resumePosition) < .25f && returnedAudio.Duration.text.StartsWith("0:02", StringComparison.Ordinal),
                        "P24 restored paused playback position and visible duration after transcript; expected=" + resumePosition
                        + "; actual=" + (returnedAudio == null ? "missing" : returnedAudio.PlaybackPosition.ToString())
                        + "; playing=" + (returnedAudio != null && returnedAudio.Source.isPlaying)
                        + "; retained=" + typeof(WeekOneQuestDemo).GetField("basicAudioPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Ui));
                    f.Capture("P24-transcript-return"); f.Click("Focus.AudioToggle");
                    Assert(returnedAudio.Source.isPlaying && returnedAudio.Source.time >= resumePosition - .25f,
                        "P24 actual playback resumes on replacement AudioSource at saved position");
                    f.Click("Focus.AudioToggle");
                    Assert(!returnedAudio.Source.isPlaying, "P24 resumed playback can pause again");
                }
                if (instruction.Kind == "wait")
                {
                    f.Click("Focus.ContinueLater"); Assert(Screen(f.Ui) == "home", "P27 explicit continue-later exits");
                    f.Clock.Time = f.Clock.Time.AddDays(1); Assert(f.Service.RefreshCycle(Array.Empty<string>()), f.Service.LastError);
                    f.Ui.SelectFocusSession(id); f.Layout();
                    Assert(f.Session(id).InstructionStep == step && f.Text("Basic.Body") != null,
                        "P27 next-day reentry retains occurrence and wait step");
                }
                if (instruction.Kind == "review")
                {
                    var expected = f.Session(id).FocusStepRecords.Take(step).Where(r => !r.Skipped && !string.IsNullOrEmpty(r.Value)).Select(r => r.Value).ToArray();
                    var seen = new List<string>(); int pages = 0;
                    while (f.Session(id).Status != ParticipationState.RewardCommitted && f.Session(id).InstructionStep == step)
                    {
                        Assert(++pages <= expected.Length + 1, "Bounded review pages");
                        var value = f.Text("Basic.ReviewValue"); Assert(value != null, "Review shows an actual persisted answer"); seen.Add(value.text);
                        var reviewArt = f.Ui.GetComponentInChildren<FocusPracticeIllustration>(false);
                        Assert(reviewArt != null && reviewArt.Choice == value.text,
                            "Review illustration follows the displayed saved answer rather than the first activity.");
                        f.AssertIcon(anchor); if (exercise) f.Capture(def.Code + "-review-" + pages);
                        f.Click("Focus.IconAction");
                    }
                    Assert(expected.All(answer => seen.Any(text => text.Contains(answer))), "Review pages preserve every actual prior answer");
                    continue;
                }
                string action = instruction.Kind == "choice" && instruction.Options.Length > 0
                    ? "Focus.Option." + (instruction.Key == "route" && route != null ? route : instruction.Options[0]) : "Focus.IconAction";
                if (exercise && (def.Code == "P08" && step == 0 || def.Code == "P12" && step == 0 || step == def.Steps.Length - 1))
                {
                    string before = StateCodec.Encode(f.Service.Snapshot);
                    f.Store.Fail = true; f.Click(action); f.Store.Fail = false;
                    Assert(f.Session(id).InstructionStep == step && StateCodec.Encode(f.Service.Snapshot) == before,
                        def.Code + " failed confirmation is atomic and leaves same step");
                    f.AssertBasicFailure(); if (def.Code == "P08") f.Capture(def.Code + "-save-failed");
                }
                var stale = f.Button(action).onClick; f.Click(action);
                string after = StateCodec.Encode(f.Service.Snapshot); int next = f.Session(id).InstructionStep; string screen = Screen(f.Ui);
                stale.Invoke(); f.Layout();
                Assert(StateCodec.Encode(f.Service.Snapshot) == after && f.Session(id).InstructionStep == next && Screen(f.Ui) == screen,
                    def.Code + " old slide callback cannot skip, overwrite or duplicate completion");
                Assert(next > step || f.Session(id).Status == ParticipationState.RewardCommitted, def.Code + " primary action confirms original domain step");
            }
        }
        // Construct a genuinely persisted prior-version session. Current sessions must
        // never regain historical sound branches that counted as mountain/help completion.
        static Fixture LegacyReviewFixture(string id, string code, int order)
        {
            Store saved; DateTimeOffset time;
            using (var seed = new Fixture(360, 640))
            {
                SeedBefore(seed, 29);
                Assert(seed.Service.BeginFocusSession(id, code, order, true, "8w"), seed.Service.LastError);
                var state = seed.Service.Snapshot; var session = state.Sessions.Single(x => x.SessionId == id);
                var definition = FocusMissionCatalog.Find(code, FocusMissionCatalog.GuidedVersion);
                session.FocusVersion = FocusMissionCatalog.GuidedVersion;
                session.MissionTitleSnapshot = definition.Title; session.MissionDescriptionSnapshot = definition.Overview;
                session.FocusStepRecords = definition.Steps.Select((step, index) => new FocusStepRecord
                    { Step = index, Key = step.Key, Title = step.Title, Prompt = step.Body }).ToList();
                saved = new Store(); saved.Save(state); time = seed.Clock.Time;
            }
            return new Fixture(360, 640, saved, time);
        }
        static void CurrentAlternatives()
        {
            foreach (int order in new[] { 19, 26 })
            using (var f = new Fixture(360, 640))
            {
                SeedBefore(f, order); string code = "P" + order, id = "current-alternative-" + order;
                Assert(f.Service.BeginFocusSession(id, code, order, program: "8w"), f.Service.LastError);
                f.Ui.SelectFocusSession(id); f.Layout(); f.Click("Focus.IconAction");
                int coins = f.Service.CoinBalance, receipts = f.Service.Snapshot.PracticeGrowthRecords.Count;
                string action = "Focus.Option." + (order == 19 ? "짧은 소리 실습" : "아직 없어요");
                string before = StateCodec.Encode(f.Service.Snapshot); f.Store.Fail = true;
                f.Click(action); f.Store.Fail = false;
                Assert(StateCodec.Encode(f.Service.Snapshot) == before && f.Session(id).Status == ParticipationState.InProgress,
                    code + " failed alternative leaves the original occurrence and rewards unchanged");
                f.Click(action);
                var support = f.Service.Snapshot.Sessions.Single(x => x.FocusSupport && x.FocusSourceSessionId == id);
                Assert(f.Session(id).Status == ParticipationState.Ended && f.Session(id).FocusEndReason == "alternative"
                    && FocusMissionPolicy.NextOrder(f.Service.Snapshot, "8w") == order
                    && support.MissionId == "course:P02" && support.FocusRole == FocusMissionCatalog.AdditionalRole
                    && support.FocusSourceMissionId == "course:" + code
                    && f.Service.CoinBalance == coins && f.Service.Snapshot.PracticeGrowthRecords.Count == receipts,
                    code + " actual UI opens a distinct P02 additional activity without completing or rewarding its source");
                Assert(f.Text("Guided.Overview") != null && support.Status == ParticipationState.Selected,
                    "Separate support starts with the actual P02 introduction");
                f.Capture(code + "-separate-P02");
                for (int n = 0; n < 8 && f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null; n++) f.Click("Focus.IconAction");
                var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                Assert(clock != null && clock.Running, "Separate P02 starts its own observed practice timer");
                clock.ElapsedSeconds = 29; Assert(clock.Flush(), "Save actual support elapsed time");
                f.Click("Guided.ReflectionToggle");
                Assert(f.Text("Guided.ReflectionPrompt") != null, "Optional separate sound reflection stays inside P02");
                f.Click("Focus.IconAction");
                Assert(f.Session(support.SessionId).Status == ParticipationState.RewardCommitted
                    && f.Service.CoinBalance == coins + 50 && f.Service.GetPracticeGrowth(support.SessionId) != null
                    && f.Service.GetPracticeGrowth(id) == null && FocusMissionPolicy.NextOrder(f.Service.Snapshot, "8w") == order,
                    "Only actual P02 completion receives one reward while the original occurrence remains pending");
                Assert(!f.Service.BeginFocusSession("second-additional-" + order, "M01"),
                    "Separate sound practice consumes the same one-additional-per-day allowance");
                f.Capture(code + "-separate-P02-complete");
            }
        }
        static void SpecialBranches()
        {
            CurrentAlternatives();
            foreach (var spec in new[] { "P19|짧은 소리 실습", "P26|아직 없어요", "P24|앱의 짧은 이야기" })
            {
                string[] parts = spec.Split('|'); string id = "legacy-review-" + parts[0]; int order = int.Parse(parts[0].Substring(1));
                using (var f = LegacyReviewFixture(id, parts[0], order))
                {
                    var def = FocusMissionCatalog.FindForSession(f.Session(id));
                    Assert(f.Session(id).FocusVersion == FocusMissionCatalog.GuidedVersion, "Explicit prior-version saved branch");
                    f.Ui.SelectFocusSession(id); f.Layout(); var anchor = f.AssertIcon(); f.Click("Focus.IconAction");
                    int coins = f.Service.CoinBalance, stage = f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep, stored = f.Service.Snapshot.StoredGrowthSteps;
                    FinishBasic(f, id, def, anchor, false, parts[1]);
                    Assert(f.Service.CoinBalance == coins && f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep == stage
                        && f.Service.Snapshot.StoredGrowthSteps == stored, def.Code + " explicit historical review does not grant rewards");
                    if (def.Code == "P19") Assert(f.Session(id).FocusStepRecords.Where(r => r.Key.StartsWith("mountain-", StringComparison.Ordinal)).All(r => r.Skipped)
                        && !f.Session(id).FocusStepRecords.Single(r => r.Key == "sound-listen").Skipped, "Persisted V4 P19 retains its historical branch");
                    if (def.Code == "P26") Assert(f.Session(id).FocusStepRecords.Single(r => r.Key == "help").Skipped
                        && !f.Session(id).FocusStepRecords.Single(r => r.Key == "sound").Skipped, "Persisted V4 P26 retains its historical branch");
                }
            }
        }
        static void EditingAndTimerRecovery()
        {
            const string routeId = "review-changing-route";
            using (var f = LegacyReviewFixture(routeId, "P19", 19))
            {
                int coins = f.Service.CoinBalance, stored = f.Service.Snapshot.StoredGrowthSteps;
                f.Ui.SelectFocusSession(routeId); f.Layout(); Rect anchor = f.AssertIcon(); f.Click("Focus.IconAction");
                f.Click("Focus.Option.산 명상"); f.Click("Focus.IconAction");
                Assert(f.Session(routeId).InstructionStep == 2, "P19 confirms actual mountain preparation");
                f.Click("Basic.Previous"); f.Click("Basic.Previous");
                Assert(f.Session(routeId).InstructionStep == 0, "P19 previous returns to original route selection");
                f.Click("Focus.Option.짧은 소리 실습");
                Assert(f.Session(routeId).InstructionStep == 6 && f.Session(routeId).FocusStepRecords.Where(r => r.Key.StartsWith("mountain-", StringComparison.Ordinal)).All(r => r.Skipped),
                    "Changing P19 route skips obsolete mountain confirmations");
                f.Click("Basic.Previous"); Assert(f.Session(routeId).InstructionStep == 0, "Previous skips the currently inapplicable mountain branch");
                f.Click("Focus.Option.산 명상");
                Assert(f.Session(routeId).InstructionStep == 1, "Route can be changed back without skipping applicable preparation");
                FinishBasic(f, routeId, FocusMissionCatalog.FindForSession(f.Session(routeId)), anchor, false);

                const string planId = "review-edit-plan";
                Assert(f.Service.BeginFocusSession(planId, "P28", 28, true, "8w"), f.Service.LastError);
                f.Ui.SelectFocusSession(planId); f.Layout(); f.Click("Focus.IconAction");
                f.Click("Focus.Option.호흡"); f.Click("Focus.IconAction"); f.Click("Focus.Option.주변 소리"); f.Click("Focus.Option.저녁");
                Assert(f.Session(planId).InstructionStep == 4 && f.Text("Basic.ReviewValue").text == "호흡", "Review starts with original saved first answer");
                var staleEdit = f.Button("Basic.ReviewEdit").onClick; f.Click("Basic.ReviewEdit"); staleEdit.Invoke(); f.Layout();
                Assert(f.Session(planId).InstructionStep == 0 && f.Text("Basic.Body") != null, "Review edit opens precisely the represented answer step");
                f.Click("Focus.Option.몸의 접촉"); f.Click("Focus.IconAction"); f.Click("Focus.Option.호흡"); f.Click("Focus.Option.아침");
                Assert(f.Text("Basic.ReviewValue").text == "몸의 접촉", "Review displays updated answer after re-confirming changed plan");
                f.Click("Focus.IconAction"); Assert(f.Text("Basic.ReviewValue").text == "호흡", "Review next shows second saved choice");
                f.Click("Basic.Previous");
                Assert(f.Session(planId).InstructionStep == 4 && f.Text("Basic.ReviewValue").text == "몸의 접촉", "Review previous moves only within review pages");
                f.Capture("P28-review-edited"); FinishBasic(f, planId, FocusMissionCatalog.Find("P28"), anchor, false);

                const string timeId = "review-elapsed-failure";
                Assert(f.Service.BeginFocusSession(timeId, "P18", 18, true, "8w"), f.Service.LastError);
                f.Ui.SelectFocusSession(timeId); f.Layout(); f.Click("Focus.IconAction"); f.Click("Focus.Option.호흡");
                var clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false); Assert(clock != null, "P18 timed practice with reading help available");
                f.Click("Focus.TimerToggle"); clock.ElapsedSeconds = 23;
                string before = StateCodec.Encode(f.Service.Snapshot); f.Store.Fail = true;
                f.Click("Basic.HelpToggle");
                Assert(f.Text("Basic.Help") != null && f.Ui.GetComponentInChildren<FocusPracticeClock>(false) == null && StateCodec.Encode(f.Service.Snapshot) == before,
                    "Failed elapsed write still allows reading help without durable mutation");
                f.AssertBasicFailure(); f.Click("Basic.Previous");
                clock = f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
                Assert(clock != null && !clock.Running && clock.ElapsedSeconds == 23 && StateCodec.Encode(f.Service.Snapshot) == before,
                    "Reading return retains unsaved elapsed value and leaves timer paused");
                f.Store.Fail = false; f.Click("Focus.Close");
                Assert(Screen(f.Ui) == "home" && f.Session(timeId).FocusStepRecords[1].ElapsedSeconds == 23,
                    "Successful exit retry persists elapsed draft before leaving");
                f.Ui.SelectFocusSession(timeId); f.Layout();
                Assert(f.Session(timeId).InstructionStep == 1 && f.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds == 23,
                    "Reentry resumes original timed step and retained elapsed value");
                f.Click("Focus.IconAction"); f.Click("Focus.Option.호흡");
                Assert(f.Session(timeId).Status == ParticipationState.RewardCommitted, "Recovered timed review completes through actual controls");
                Assert(f.Service.CoinBalance == coins && f.Service.Snapshot.StoredGrowthSteps == stored,
                    "Revisiting branches, review answers and timer failures grants no review rewards");
            }
        }
        static void LegacyPages()
        {
            foreach (int version in new[] { 1, 2 })
            using (var f = new Fixture(390, 844))
            {
                Store store = f.Store;
                if (version == 1)
                {
                    Assert(f.Service.BeginFocusSession("legacy-p23", "P23", 1, program: "legacy") && f.Service.StartSession("legacy-p23"), f.Service.LastError);
                    var first = FocusMissionCatalog.Find("P23", 1);
                    for (int step = 0; step < first.Steps.Length; step++) Assert(f.Service.ConfirmFocusStep("legacy-p23", step, null, true), f.Service.LastError);
                    Assert(f.Service.CompletePracticeSession("legacy-p23"), f.Service.LastError);
                    f.Clock.Time = f.Clock.Time.AddDays(1); Assert(f.Service.RefreshCycle(Array.Empty<string>()), f.Service.LastError);
                    Assert(f.Service.BeginFocusSession("legacy-p10", "P10", 2, program: "legacy"), f.Service.LastError);
                }
                else
                {
                    SeedBefore(f, 10); Assert(f.Service.BeginFocusSession("legacy-p10", "P10", 10, program: "8w"), f.Service.LastError);
                    var saved = f.Service.Snapshot; var session = saved.Sessions.Single(x => x.SessionId == "legacy-p10"); var def = FocusMissionCatalog.Find("P10", version);
                    session.FocusVersion = version; session.MissionTitleSnapshot = def.Title; session.MissionDescriptionSnapshot = def.Overview;
                    session.FocusStepRecords = def.Steps.Select((step, index) => new FocusStepRecord { Step = index, Key = step.Key, Title = step.Title, Prompt = step.Body }).ToList();
                    store = new Store(); store.Save(saved);
                }
                using (var restored = new Fixture(390, 844, store))
                {
                    restored.Ui.SelectFocusSession("legacy-p10"); restored.Layout();
                    Assert(restored.Text("Basic.Overview") == null && restored.Button("Focus.Action") != null, "Legacy version " + version + " retains original overview");
                    restored.Click("Focus.Action"); Assert(restored.Text("Basic.Body") == null && restored.Session("legacy-p10").FocusVersion == version,
                        "Legacy version " + version + " keeps original practice renderer");
                }
            }
        }
        static void AdditionalDigests()
        {
            foreach (var mission in FocusMissionCatalog.Additional)
            using (var f = new Fixture(390, 844))
            {
                string id = "synthetic-digest-" + mission.Code;
                Assert(f.Service.BeginFocusSession(id, mission.Code), f.Service.LastError);
                var definition = FocusMissionCatalog.FindForSession(f.Session(id));
                f.Ui.SelectFocusSession(id); f.Layout();
                AddDigest(f, mission.Code + "/overview"); f.Click("Focus.Action");
                int count = 0;
                while (f.Session(id).Status != ParticipationState.RewardCommitted)
                {
                    Assert(++count <= definition.Steps.Length + 2, "Bounded additional progression " + mission.Code);
                    if (Screen(f.Ui) == "focus-photo")
                    {
                        Assert(CameraMissionPolicy.Ready(f.Session(id)) && f.Service.GetPracticeGrowth(id) == null && f.Service.CoinBalance == 137,
                            mission.Code + " optional photo choice waits before growth and coins");
                        AddDigest(f, mission.Code + "/optional-photo"); f.Click("Photo.Skip");
                        Assert(f.Session(id).PhotoPromptHandled && f.Session(id).Photo == null && f.Session(id).PhotoBonusAmount == 0,
                            mission.Code + " real skip preserves normal rewards without a photo bonus");
                        continue;
                    }
                    var session = f.Session(id); int step = session.InstructionStep;
                    Assert(step < definition.Steps.Length, mission.Code + " active instruction belongs to its saved catalog version");
                    var instruction = definition.Steps[step];
                    AddDigest(f, mission.Code + "/" + step + "/empty");
                    if (instruction.Kind == "plan")
                    {
                        Assert(f.Button("Focus.Action", false) != null && !f.Button("Focus.Action", false).interactable,
                            "M10 empty structured plan cannot advance before a choice");
                        f.Click("Focus.PlanActivity.0"); f.Click("Focus.PlanPeriod.0");
                        var saved = f.Session(id).FocusStepRecords[step];
                        Assert(!string.IsNullOrEmpty(saved.PlanActivity) && !string.IsNullOrEmpty(saved.PlanTimePeriod),
                            "M10 persists both actual activity and comfortable-period choices");
                    }
                    else if (instruction.Kind == "draw")
                    {
                        f.DrawSyntheticStroke();
                    }
                    else
                    {
                        var input = f.Ui.GetComponentsInChildren<InputField>(false).FirstOrDefault(i => i.name == "Focus.AnswerInput");
                        if (input != null)
                        { input.text = "synthetic additional answer"; input.onEndEdit.Invoke(input.text); f.Layout(); }
                        else if (instruction.Options.Length > 0)
                        {
                            string choice = mission.Code == "M15" ? instruction.Options.Last() : instruction.Options[0];
                            f.Click("Focus.Option." + choice);
                        }
                    }
                    AddDigest(f, mission.Code + "/" + step + "/ready");
                    f.Click("Focus.Action");
                    Assert(f.Session(id).InstructionStep > step || f.Session(id).Status == ParticipationState.RewardCommitted,
                        "Additional action advances original runner " + mission.Code + "/" + step);
                }
                Assert(f.Service.CoinBalance == 187 && f.Service.GetPracticeGrowth(id) != null && Screen(f.Ui) == "focus-complete",
                    mission.Code + " retains its one growth and 50 coin completion");
                AddDigest(f, mission.Code + "/complete");
            }
        }
        static void AddDigest(Fixture f, string key)
        {
            f.Layout(); var value = new StringBuilder();
            foreach (var t in f.Ui.GetComponentsInChildren<Transform>(false))
            {
                string path = t.name; for (var parent = t.parent; parent != null && parent != f.Ui.transform; parent = parent.parent) path = parent.name + "/" + path;
                value.Append(path).Append("|types=").Append(string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "missing" : c.GetType().FullName)));
                var rect = t as RectTransform;
                if (rect != null) value.Append("|rect=").Append(V(rect.anchoredPosition)).Append(';').Append(V(rect.rect.size)).Append(';')
                    .Append(V(rect.anchorMin)).Append(';').Append(V(rect.anchorMax)).Append(';').Append(V(rect.pivot)).Append(';').Append(V(rect.localScale));
                var text = t.GetComponent<Text>();
                if (text != null) value.Append("|text=").Append(text.text.Replace("\n", "\\n")).Append('|').Append(text.fontSize).Append('|').Append(text.fontStyle)
                    .Append('|').Append(text.alignment).Append('|').Append(C(text.color)).Append('|').Append(text.font == null ? "" : text.font.name);
                var button = t.GetComponent<Button>(); if (button != null) value.Append("|button=").Append(button.interactable).Append('|').Append(button.transition);
                var input = t.GetComponent<InputField>(); if (input != null) value.Append("|input=").Append(input.text).Append('|').Append(input.characterLimit).Append('|').Append(input.lineType);
                var raw = t.GetComponent<RawImage>(); if (raw != null) value.Append("|raw=").Append(raw.texture == null ? "" : AssetDatabase.GetAssetPath(raw.texture)).Append('|').Append(C(raw.color));
                var image = t.GetComponent<Image>(); if (image != null) value.Append("|image=").Append(image.sprite == null ? "" : image.sprite.name).Append('|').Append(C(image.color)).Append('|').Append(image.raycastTarget);
                var audio = t.GetComponent<AudioSource>(); if (audio != null) value.Append("|audio=").Append(audio.clip == null ? "" : AssetDatabase.GetAssetPath(audio.clip));
                value.Append('\n');
            }
            string content = value.ToString(); string digest;
            using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "");
            report.digests.Add(new Digest { key = key, sha256 = digest, content = content });
        }
        static string N(float n) => n.ToString("0.###", CultureInfo.InvariantCulture);
        static string V(Vector2 v) => N(v.x) + "," + N(v.y);
        static string V(Vector3 v) => N(v.x) + "," + N(v.y) + "," + N(v.z);
        static string C(Color c) => N(c.r) + "," + N(c.g) + "," + N(c.b) + "," + N(c.a);
        static void CompareDigests(string path)
        {
            var baseline = JsonUtility.FromJson<Report>(File.ReadAllText(path));
            Assert(baseline.failed == 0 && baseline.digests.Count == report.digests.Count, "Pre-change baseline is complete and has equal page count");
            foreach (var current in report.digests)
            {
                var before = baseline.digests.Single(d => d.key == current.key);
                Assert(current.sha256 == before.sha256, "Additional UI content/component/layout mismatch: " + current.key);
            }
        }
        sealed class Store : IStateStore
        { string xml; public bool Fail; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
          public void Save(GardenState value) { if (Fail) throw new IOException("Synthetic guided UI save failure"); xml = StateCodec.Encode(value); } }
        sealed class Clock : IClock
        { public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-09T03:00:00Z"); public DateTimeOffset UtcNow => Time; public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class Fixture : IDisposable
        {
            public readonly Store Store; public readonly Clock Clock = new Clock();
            public readonly GardenStateService Service; public readonly WeekOneQuestDemo Ui;
            readonly GameObject root; readonly Scene scene; readonly Camera camera; readonly RenderTexture target;
            readonly EventSystem events; readonly GraphicRaycaster raycaster; readonly int width, height;
            readonly HashSet<Rect> stableIconAnchors = new HashSet<Rect>();
            public Fixture(int width, int height, Store store = null, DateTimeOffset? time = null)
            {
                this.width = width; this.height = height; Store = store ?? new Store();
                if (time.HasValue) Clock.Time = time.Value;
                Service = new GardenStateService(Store, Clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 137 });
                Assert(Service.Open(), Service.LastError);
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Synthetic guided UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(width, height);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var cameraObject = new GameObject("Synthetic guided capture camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                target = new RenderTexture(width, height, 24); camera.targetTexture = target; camera.aspect = width / (float)height;
                canvas.worldCamera = camera; raycaster = root.GetComponent<GraphicRaycaster>();
                var eventObject = new GameObject("Synthetic pointer", typeof(EventSystem)); eventObject.transform.SetParent(root.transform, false); events = eventObject.GetComponent<EventSystem>(); events.enabled = false;
                var panel = GardenUi.Box(root.transform, "Synthetic activity panel", 0, 0, 1, 1);
                Ui = panel.AddComponent<WeekOneQuestDemo>(); Ui.PreviewHealth = new AndroidPlatformSnapshot { inputMode = "MOCK", status = "NO_DATA" }; Ui.Initialize(Service, true);
                root.SetActive(true); Layout();
                Assert(ReferenceEquals(Ui.Service, Service), "Actual page presenter keeps injected synthetic service");
            }
            public ActivitySession Session(string id) => Service.Snapshot.Sessions.Single(s => s.SessionId == id);
            public Text Text(string name) => Ui.GetComponentsInChildren<Text>(false).FirstOrDefault(t => t.name == name);
            public Button Button(string name, bool required = true)
            {
                var result = Ui.GetComponentsInChildren<Button>(false).FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
                if (required) Assert(result != null && result.IsInteractable(), "Visible enabled button missing: " + name); return result;
            }
            public void Layout()
            {
                for (int i = 0; i < 3; i++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var fit in root.GetComponentsInChildren<FigmaMissionCanvasFit>(false)) fit.Fit();
                    foreach (var page in root.GetComponentsInChildren<FocusMobilePageLayout>(false)) page.Refresh();
                    foreach (var slide in root.GetComponentsInChildren<FocusGuidedSlideLayout>(false)) slide.Refresh();
                    Canvas.ForceUpdateCanvases();
                }
                // Graphic depths/raycast ordering are populated by a real render.
                // Null graphics cannot establish that evidence; capture runs use an owned offscreen GPU target.
                if (capture) camera.Render();
            }
            public void DrawSyntheticStroke()
            {
                var surface = Ui.GetComponentsInChildren<FocusDrawingSurface>(false).Single(s => !s.ReadOnly);
                RevealRect(surface.rectTransform); Layout();
                var point = camera.WorldToScreenPoint(surface.rectTransform.TransformPoint(surface.rectTransform.rect.center));
                var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left,
                    pointerPressRaycast = new RaycastResult { module = raycaster } };
                var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                Assert(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<FocusDrawingSurface>() == surface, "Drawing receives actual pointer hit");
                surface.OnPointerDown(data); data.position += new Vector2(24, 24); surface.OnDrag(data); surface.OnPointerUp(data); Layout();
                Assert(FocusDrawingData.Decode(surface.Value).Any(stroke => stroke.Count > 1), "Actual drawing gesture preserves multiple points");
            }
            public void Click(string name)
            {
                var button = Button(name); FocusKeyboardNavigation.Reveal(button); Layout();
                var rect = (RectTransform)button.transform; var point = camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
                Assert(point.x > 0 && point.x < width && point.y > 0 && point.y < height, "Button center is within visible capture: " + name + " " + point);
                var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); raycaster.Raycast(data, hits); report.raycasts++;
                Assert(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
                    "Actual visible button is first pointer target: " + name + "; hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name)
                    + "; graphicDepth=" + button.targetGraphic.depth + "; culled=" + button.targetGraphic.canvasRenderer.cull);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
                report.clicks++; Layout();
            }
            public void AssertBasicBody(string name)
            {
                Layout(); var body = Text(name); Assert(body != null && !string.IsNullOrWhiteSpace(body.text), "Visible basic slide body " + name);
                AssertFullyVisible(body.rectTransform, name);
                Assert(body.preferredHeight <= body.rectTransform.rect.height + .75f, name + " wraps completely within its authored region");
                var centered = new[] { TextAnchor.UpperCenter, TextAnchor.MiddleCenter, TextAnchor.LowerCenter };
                Assert(centered.Contains(body.alignment) && body.fontSize == 22, "Current basic instructions remain centered at 22 points.");
                string currentId = (string)typeof(WeekOneQuestDemo).GetField("sessionId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Ui);
                var session = Session(currentId); var definition = FocusMissionCatalog.FindForSession(session);
                var title = Text("Basic.Title"); var context = Text("Basic.Mission");
                var layout = body.GetComponentInParent<FocusGuidedSlideLayout>();
                if (name == "Basic.Overview")
                {
                    Assert(title != null && context != null && title.text == definition.Title && context.text == "기본 미션"
                        && body.text == definition.Overview && title.fontSize == 30 && context.fontSize == 16,
                        "Every basic introduction retains its original mission title and overview at 30/22/16 points.");
                    foreach (var header in new[] { title, context })
                    {
                        Assert(centered.Contains(header.alignment) && header.preferredHeight <= header.rectTransform.rect.height + .75f,
                            header.name + " fits its measured introduction text");
                        AssertFullyVisible(header.rectTransform, header.name);
                    }
                    Assert(ScreenRect(context.rectTransform).yMin >= ScreenRect(title.rectTransform).yMax - .75f
                        && ScreenRect(title.rectTransform).yMin >= ScreenRect(body.rectTransform).yMax - .75f,
                        "Long introduction titles remain separate from their context and body.");
                }
                else
                {
                    Assert(title == null && context == null && layout.Heading == null
                        && !layout.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Basic.Title" || t.name == "Basic.Mission"),
                        "Practice, help, transcript and review do not create extra title or mission-context objects.");
                    Assert(Mathf.Abs(-FigmaMissionUi.TopLeftPosition(body.rectTransform).y - 126) < .1f
                        && body.rectTransform.rect.height >= 54 - .1f,
                        "The concise basic instruction starts in the shared reserved reading area.");
                }
                var central = Button("Focus.IconAction", false);
                Assert(central != null, "The central illustration exists even while a required choice disables advancing");
                if (name == "Basic.Body" && definition.Steps[session.InstructionStep].RequiredAnswer
                    && string.IsNullOrWhiteSpace(session.FocusStepRecords[session.InstructionStep].Value))
                {
                    Assert(!central.interactable, "Required unanswered choice cannot be skipped by the central action");
                    Assert(Text("Basic.ActionHint").text == "아래에서 한 가지를 선택해 주세요",
                        "Required selection guidance never incorrectly offers skipping");
                }
                var textBounds = ScreenRect(body.rectTransform); var icon = ScreenRect((RectTransform)central.transform);
                Assert(textBounds.yMin >= icon.yMax - .75f
                    && ScreenRect((RectTransform)Button("Focus.Close").transform).yMin >= textBounds.yMax - .75f,
                    name + " is separated from both the exit and central icon");
                var art = Ui.GetComponentInChildren<FocusPracticeIllustration>(false);
                string key = name == "Basic.Overview" ? "overview" : name == "Basic.Help" ? "guide-help"
                    : name == "Basic.Transcript" ? "transcript" : definition.Steps[session.InstructionStep].Key;
                Assert(art != null && !art.raycastTarget && art.MissionCode == definition.Code && art.StepKey == key,
                    "The actual illustrated page follows its mission and current step without intercepting its controls.");
                var shown = Pixels(); bool enabled = art.enabled; Color32[] hidden;
                try { art.enabled = false; hidden = Pixels(); }
                finally { art.enabled = enabled; Layout(); }
                int painted = ChangedPixels(hidden, shown, ArtBounds());
                report.visualMeasurements.Add(width + "x" + height + " " + definition.Code + "/" + key + ": " + painted + " painted pixels");
                Assert(painted >= 128, definition.Code + "/" + key + " paints its actual illustration on the GPU.");
            }
            public void AssertBasicFailure()
            {
                var text = Text("Focus.SaveNotice"); Assert(text != null && !string.IsNullOrEmpty(text.text), "Basic save failure has visible notice");
                AssertFullyVisible(text.rectTransform, "Save failure notice"); AssertIcon();
            }
            public void AssertSlide(string name, string expected)
            {
                Layout(); var text = Text(name);
                Assert(Screen(Ui) == "focus-practice" && text != null && text.text == expected, "Expected single slide " + name);
                var names = new[] { "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" };
                Assert(Ui.GetComponentsInChildren<Text>(false).Count(t => names.Contains(t.name)) == 1, "Exactly one piece of guidance is visible on " + name);
                AssertFullyVisible(text.rectTransform, name);
                Assert(text.preferredHeight <= text.rectTransform.rect.height + .5f, name + " fits its complete wrapped body");
                var centered = new[] { TextAnchor.UpperCenter, TextAnchor.MiddleCenter, TextAnchor.LowerCenter };
                Assert(centered.Contains(text.alignment) && centered.Contains(Text("Guided.Title").alignment),
                    "Guided title and current instruction are horizontally centered");
                var body = ScreenRect(text.rectTransform); var icon = ScreenRect((RectTransform)Button("Focus.IconAction").transform);
                Assert(body.yMin >= icon.yMax - .5f, name + " does not overlap the fixed illustration");
                Assert(Button("Focus.Action", false) == null, "Guided progress uses the central icon rather than an old footer action");
            }
            public Rect AssertIcon(Rect? expected = null)
            {
                Layout(); var central = Button("Focus.IconAction", false); Assert(central != null, "Visible central illustration exists");
                var rect = (RectTransform)central.transform;
                AssertFullyVisible(rect, "Fixed central icon");
                var slide = rect.GetComponentInParent<FocusGuidedSlideLayout>();
                bool constrained = ((RectTransform)slide.transform).rect.height < 672
                    || slide.Notice != null && slide.Notice.gameObject.activeInHierarchy
                    || slide.Heading != null && slide.Heading.preferredHeight > 42.5f || slide.Copy != null && slide.Copy.preferredHeight > 64;
                Assert(Vector2.Distance(rect.pivot, new Vector2(.5f, .5f)) < .001f && Mathf.Abs(rect.rect.width - rect.rect.height) < .01f,
                    "Central icon stays square with a centered press pivot");
                Assert(slide.Copy != null && slide.MinimumAnswerHeight == 100,
                    "Basic slides reserve the 88-point input field and its 12-point breathing room.");
                float copyBottom = -FigmaMissionUi.TopLeftPosition(slide.Copy.rectTransform).y + slide.Copy.rectTransform.rect.height;
                float lowerEdge = slide.Notice != null && slide.Notice.gameObject.activeInHierarchy
                    ? -FigmaMissionUi.TopLeftPosition(slide.Notice.rectTransform).y : -FigmaMissionUi.TopLeftPosition(slide.Navigation).y - 8;
                float designHeight = ((RectTransform)slide.transform).rect.height;
                float down = Mathf.Lerp(14, 32, Mathf.InverseLerp(692, 844, designHeight));
                float minimumTop = Mathf.Max(254, copyBottom + 18) + down;
                float expectedSize = Mathf.Clamp(lowerEdge - minimumTop - 40 - 100, 160, 280);
                Assert(Mathf.Abs(rect.rect.width - expectedSize) < .1f
                    && Mathf.Abs(-FigmaMissionUi.TopLeftPosition(rect).y - Mathf.Max(minimumTop, designHeight * .48f - expectedSize / 2 + down)) < .1f,
                    "Basic illustrations use the available measured reading area within 160–280 points and retain the responsive downward offset.");
                var bounds = ScreenRect(rect);
                Assert(Mathf.Abs(bounds.center.x - width * .5f) < .75f,"Central icon remains horizontally centered: " + bounds);
                var body = Ui.GetComponentsInChildren<Text>(false).Single(text => new[] { "Basic.Overview", "Basic.Body", "Basic.Help", "Basic.Transcript", "Guided.Overview", "Guided.Preparation", "Guided.ChoiceTitle", "Guided.Cue.0", "Guided.Cue.1", "Guided.Cue.2", "Guided.PracticeHint", "Guided.ReflectionPrompt", "Guided.Help" }.Contains(text.name));
                Assert(ScreenRect(body.rectTransform).yMin >= bounds.yMax-.75f,"Enlarged icon leaves current guidance unobscured");
                Assert(bounds.width >= 44 && bounds.height >= 44, "Icon target is at least 44 pixels");
                var hint = Text("Basic.ActionHint");
                Assert(bounds.yMin >= ScreenRect(hint.rectTransform).yMax - .75f
                    && ScreenRect(hint.rectTransform).yMin >= ScreenRect(slide.Viewport).yMax - .75f
                    && ScreenRect(slide.Viewport).yMin >= ScreenRect(slide.Navigation).yMax - .75f,
                    "The illustration, action hint, local answer area and bottom navigation never overlap.");
                // A long first instruction has a measured responsive rectangle of its own.
                // Compare fixed anchors only when both pages use the unconstrained layout.
                if (!expected.HasValue && !constrained) stableIconAnchors.Add(bounds);
                if (expected.HasValue && stableIconAnchors.Contains(expected.Value) && !constrained) Assert(Vector2.Distance(bounds.center, expected.Value.center) < .75f
                    && Vector2.Distance(bounds.size, expected.Value.size) < .75f, "All slides retain the same central icon rectangle");
                return bounds;
            }
            public Rect ArtBounds() => ScreenRect(Ui.GetComponentInChildren<FocusPracticeIllustration>(false).rectTransform);
            public Color32[] Pixels()
            {
                Layout(); camera.Render(); var prior = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); return image.GetPixels32(); }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public int ChangedPixels(Color32[] before, Color32[] after, Rect area)
            {
                int count = 0;
                for (int y = Mathf.Max(0, Mathf.CeilToInt(area.yMin)); y < Mathf.Min(height, Mathf.FloorToInt(area.yMax)); y++)
                    for (int x = Mathf.Max(0, Mathf.CeilToInt(area.xMin)); x < Mathf.Min(width, Mathf.FloorToInt(area.xMax)); x++)
                    {
                        var a = before[y * width + x]; var b = after[y * width + x];
                        if (Math.Abs(a.r - b.r) > 8 || Math.Abs(a.g - b.g) > 8 || Math.Abs(a.b - b.b) > 8) count++;
                    }
                return count;
            }
            public void AssertCenteredPress()
            {
                var button = Button("Focus.IconAction"); var rect = (RectTransform)button.transform;
                var before = ScreenRect(rect); Vector3 normal = rect.localScale;
                bool reduced = FigmaMissionUi.ReducedMotion; FigmaMissionUi.ReducedMotion = false;
                var data = new PointerEventData(events) { position = before.center, button = PointerEventData.InputButton.Left };
                try
                {
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    var pressed = ScreenRect(rect);
                    Assert(Vector2.Distance(pressed.center, before.center) < .01f && Mathf.Abs(pressed.width / before.width - .97f) < .001f,
                        "Press contracts symmetrically around illustration center");
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    Assert(Vector3.Distance(rect.localScale, normal * .97f) < .001f, "Repeated pointer down does not compound scale");
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                    Assert(Vector3.Distance(rect.localScale, normal) < .001f && Vector2.Distance(ScreenRect(rect).center, before.center) < .01f,
                        "Release restores position and scale without navigation");
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                    Assert(Vector3.Distance(rect.localScale, normal) < .001f, "Pointer exit restores central scale");
                }
                finally { FigmaMissionUi.ReducedMotion = reduced; }
            }
            public void RevealRect(RectTransform rect)
            {
                foreach (var scroll in rect.GetComponentsInParent<ScrollRect>(false).Where(s => s.enabled && s.content != null && s.viewport != null))
                {
                    var center = scroll.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                    scroll.content.anchoredPosition = new Vector2(0, Mathf.Clamp(-center.y - scroll.viewport.rect.height / 2,
                        0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)));
                }
                Layout();
            }
            Rect ScreenRect(RectTransform rect)
            {
                var points = new Vector3[4]; rect.GetWorldCorners(points); var a = camera.WorldToScreenPoint(points[0]); var b = camera.WorldToScreenPoint(points[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            public void AssertFullyVisible(RectTransform rect, string label)
            {
                var bounds = ScreenRect(rect); const float tolerance = .5f;
                Assert(bounds.xMin >= -tolerance && bounds.xMax <= width + tolerance && bounds.yMin >= -tolerance && bounds.yMax <= height + tolerance, label + " in output viewport");
                foreach (var mask in rect.GetComponentsInParent<RectMask2D>(false))
                {
                    var visible = ScreenRect(mask.rectTransform);
                    Assert(bounds.xMin >= visible.xMin - tolerance && bounds.xMax <= visible.xMax + tolerance
                        && bounds.yMin >= visible.yMin - tolerance && bounds.yMax <= visible.yMax + tolerance, label + " inside scrolling mask");
                }
            }
            public void RevealAndRead(Text text)
            {
                Assert(text != null, "Required guidance text exists"); RevealRect(text.rectTransform);
                AssertFullyVisible(text.rectTransform, text.name);
                Assert(text.preferredHeight <= text.rectTransform.rect.height + .5f, text.name + " reserves its complete wrapped text height");
            }
            public void AssertReadingSeparation()
            {
                Layout(); var notice = Text("Focus.SaveNotice");
                Assert(notice != null && !string.IsNullOrEmpty(notice.text), "Storage failure notice is visible");
                var viewport = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.AnswersViewport");
                var navigation = Ui.GetComponentsInChildren<RectTransform>(false).Single(r => r.name == "Guided.Navigation");
                var body = ScreenRect(viewport); var bottom = ScreenRect(navigation); var error = ScreenRect(notice.rectTransform);
                Assert(body.yMin >= error.yMax - .75f && error.yMin >= bottom.yMax - .75f,
                    "Local answers, failure notice and navigation have separate visible regions");
                AssertFullyVisible(notice.rectTransform, "Storage failure notice"); AssertIcon();
            }
            public void Capture(string name)
            {
                if (!capture) return; Layout(); camera.Render();
                var prior = RenderTexture.active; var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                    string filename = name + "-" + width + "x" + height + ".png";
                    File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG()); report.captures.Add(filename);
                }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Dispose()
            {
                camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root); Service.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
