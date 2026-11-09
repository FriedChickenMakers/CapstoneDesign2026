using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Player;
using Mono.Cecil;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Explicit isolated batch only. No poll, automatic refresh, scene asset or real save access.
    public static class DailyMissionValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, environment;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>();
            public List<string> gesturePixels = new List<string>();
        }
        static Report report;
        static string output;
        static bool capture;
        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-dailyReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || !(Application.productName.StartsWith("DailyMissions-", StringComparison.Ordinal) || Application.productName=="MemoPopup-v07") || index < 0)
                throw new InvalidOperationException("DailyMissionValidation requires a separate synthetic batch project.");
            string path = args[index + 1]; output = Path.GetDirectoryName(path); capture = args.Contains("-dailyCapture");
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                environment = "Hidden isolated Unity edit-mode batch; actual runtime page factories, memory state, pointer raycasts/clicks; capture=" + capture };
            Application.logMessageReceived += Observe;
            try
            {
                Run("Water practice pointer cancellation and keyboard focus 360x640", () => IconGestureFlow(360,640));
                Run("Water practice pointer cancellation and keyboard focus 390x844", () => IconGestureFlow(390,844));
                Run("Basic overview pointer cancellation and keyboard focus 360x640", () => BasicIconGestureFlow(360,640));
                Run("Basic overview pointer cancellation and keyboard focus 390x844", () => BasicIconGestureFlow(390,844));
                Run("Activity calendar dates, grouped records and read-only navigation 360x640", () => CalendarFlow(360,640));
                Run("Activity calendar dates, grouped records and read-only navigation 390x844", () => CalendarFlow(390,844));
                Run("All three mission cards without scrolling 360x640", () => HomeResponsiveFlow(360,640));
                Run("All three mission cards without scrolling 390x844", () => HomeResponsiveFlow(390,844));
                Run("All three mission cards without scrolling 320x568", () => HomeResponsiveFlow(320,568));
                Run("Daily slots, carry-over and ordinary navigation 360x640", () => DailyFlow(360,640));
                Run("Daily slots, carry-over and ordinary navigation 390x844", () => DailyFlow(390,844));
                Run("One current course and compact course switching 360x640", () => CourseFlow(360,640));
                Run("One current course and compact course switching 390x844", () => CourseFlow(390,844));
                Run("Third daily emotion mission 360x640", () => EmotionFlow(360,640));
                Run("Third daily emotion mission 390x844", () => EmotionFlow(390,844));
                Run("Automatic archive and next seed 360x640", () => EmptyPlantFlow(360,640));
                Run("Automatic archive and next seed 390x844", () => EmptyPlantFlow(390,844));
                Run("Old per-mission emotion draft survives direct completion and history", OldReflectionDraft);
                Run("Legacy completion retry and background walking refresh", LegacyCompletionRoutes);
                Run("All 45 isolated development missions", DebugFlow);
                Run("Old unfinished sessions remain accessible from today's cards", LegacyCarry);
                Run("Development presenter teardown disposes its owned services", DebugTeardown);
                Run("Non-development player excludes debug implementation", ReleaseScripts);
            }
            finally
            {
                Application.logMessageReceived -= Observe;
                if (report.errors.Count > 0) { report.failed++; report.checks.Add(new Check { name = "Unity errors", status = "fail", detail = string.Join("\n", report.errors) }); }
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            Debug.Log("DAILY_MISSIONS " + (report.failed == 0 ? "PASS" : "FAIL") + " assertions=" + report.assertions);
            EditorApplication.Exit(report.failed == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Run(string name, Action action)
        { try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
          catch (Exception ex) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = ex.ToString() }); } }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static string Screen(WeekOneQuestDemo ui) => (string)typeof(WeekOneQuestDemo).GetField("screen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);

        static string Snapshot(GardenStateService service) => StateCodec.Encode(service.Snapshot);
        static void CalendarFlow(int width,int height)
        {
            using(var f=new Fixture(width,height,instant:DateTimeOffset.Parse("2026-10-30T10:00:00Z")))
            {
                Assert(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Assert(f.Service.BeginSession("journal-legacy","free-mission:M01")&&f.Service.StartSession("journal-legacy")
                    &&f.Service.CompletePracticeSession("journal-legacy"),f.Service.LastError);
                Assert(f.Session("journal-legacy").FocusVersion==0,"Historical fixture uses the real legacy session API.");
                f.Clock.Time=DateTimeOffset.Parse("2026-10-31T10:00:00Z");Assert(f.Service.RefreshCycle(Array.Empty<string>()),f.Service.LastError);
                CompleteRoleFixture(f,"journal-oct-basic","P01",1);CompleteRoleFixture(f,"journal-oct-additional","M03");
                string emotion=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);
                Assert(f.Service.BeginDailyEmotion(f.Service.CurrentCycleId)&&f.Service.CompleteDailyEmotion(emotion,"평온함","합성 달력 기록"),f.Service.LastError);
                string entryState=Snapshot(f.Service);int entrySaves=f.Store.Saves;
                f.Ui.ShowHome();f.Layout();f.Click("DailyEmotion.CardAction");f.Click("DailyEmotion.History");
                f.Click("DailyEmotion.HistoryRow."+emotion);
                Assert(Screen(f.Ui)=="daily-emotion"&&f.Ui.TryNavigateBack(),"Home emotion detail can enter the journal and reopen that same record.");f.Layout();
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.SelectedDate").text=="2026.10.31",
                    "An earlier emotion page key cannot erase the calendar return path.");
                f.Capture("journal-home-detail-reentry");
                Assert(Snapshot(f.Service)==entryState&&f.Store.Saves==entrySaves,"Re-entering the same daily record never writes or pays a reward.");
                f.Ui.ShowHome();f.Layout();
                f.Clock.Time=DateTimeOffset.Parse("2026-11-01T10:00:00Z");Assert(f.Service.RefreshCycle(Array.Empty<string>()),f.Service.LastError);
                Assert(f.Service.BeginFocusSession("journal-nov-basic","P02",2)&&f.Service.BeginFocusSession("journal-nov-additional","M03")
                    &&f.Service.StartSession("journal-nov-additional"),f.Service.LastError);
                string before=Snapshot(f.Service);int saves=f.Store.Saves;
                f.Ui.ShowFocusCompletion("journal-oct-basic");f.Layout();
                AssertCompletionJournalRoute(f,"journal-oct-basic","2026-10-31","journal-completion-previous-month");
                f.Ui.ShowHome();f.Layout();
                var navigation=f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
                Assert(navigation.CourseButton!=null&&ReferenceEquals(f.Button(navigation.CourseButton.name),navigation.CourseButton),
                    "Mission context exposes the actual shared record-tab button.");
                f.Click(navigation.CourseButton.name);
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.Month").text=="2026년 11월"
                    &&f.Text("ActivityJournal.SelectedDate").text=="2026.11.01","Mission record-tab click opens the calendar at the actual current app day and month.");
                f.Capture("journal-from-mission-record-tab");
                bool HasSprout(string date)=>f.Ui.GetComponentsInChildren<PrototypeUiIcon>(false).Any(icon=>icon.name=="ActivityJournal.Sprout."+date);
                void VerifySprout(string date)
                {
                    var icon=f.Ui.GetComponentsInChildren<PrototypeUiIcon>(false).Single(item=>item.name=="ActivityJournal.Sprout."+date);
                    var cell=f.Button("ActivityJournal.Day."+date);var scroll=icon.GetComponentInParent<ScrollRect>();
                    float position=scroll.verticalNormalizedPosition;
                    try
                    {
                        f.RevealRect(icon.rectTransform);var area=f.Bounds(icon.rectTransform);var cellArea=f.Bounds((RectTransform)cell.transform);
                        Assert(icon.rectTransform.rect.width>0&&icon.rectTransform.rect.height>0&&area.width>0&&area.height>0
                            &&area.xMin>=cellArea.xMin-.5f&&area.xMax<=cellArea.xMax+.5f&&area.yMin>=cellArea.yMin-.5f&&area.yMax<=cellArea.yMax+.5f,
                            "Recorded-day sprout has positive geometry contained in its actual date cell.");
                        f.AssertFullyVisible(icon.rectTransform,"Recorded-day marker is reachable inside its calendar viewport");
                        var shown=f.Pixels();var hidden=f.WithoutGraphicPixels(icon);
                        Assert(f.ChangedPixels(shown,hidden,area,-1)>=4,"Recorded-day sprout contributes real GPU pixels, not merely a named object.");
                    }
                    finally{scroll.verticalNormalizedPosition=position;f.Layout();}
                }
                void OneDayCard()=>Assert(f.Ui.GetComponentsInChildren<Transform>(false).Count(node=>node.name=="ActivityJournal.DateCard")==1,
                    "The selected date has one combined record box.");
                OneDayCard();Assert(HasSprout("2026-11-01")&&!HasSprout("2026-11-02"),"Recorded date has a sprout; an empty date does not.");
                VerifySprout("2026-11-01");
                Assert(f.Button("ActivityJournal.Mission.journal-nov-basic")!=null&&f.Button("ActivityJournal.Mission.journal-nov-additional")!=null,
                    "Selected and ongoing mission records remain accessible, not only completed missions.");
                f.Capture("journal-current-month");
                f.Click("ActivityJournal.PreviousMonth");
                Assert(f.Text("ActivityJournal.Month").text=="2026년 10월"&&f.Text("ActivityJournal.SelectedDate").text=="2026.11.01"
                    &&HasSprout("2026-10-31"),"Changing the displayed month preserves selected date and exposes past recorded days.");
                VerifySprout("2026-10-31");
                f.Capture("journal-previous-month");f.Click("ActivityJournal.Day.2026-10-31");OneDayCard();
                Assert(f.Text("ActivityJournal.SelectedDate").text=="2026.10.31"&&f.Button("DailyEmotion.HistoryRow."+emotion)!=null
                    &&f.Button("ActivityJournal.Mission.journal-oct-basic")!=null&&f.Button("ActivityJournal.Mission.journal-oct-additional")!=null,
                    "A single date box combines that day's basic, additional and emotion records.");
                Assert(f.Button("ActivityJournal.Mission.journal-nov-basic",false)==null,"Date selection never mixes another day's records.");
                f.Capture("journal-selected-date");
                var mission=f.Button("ActivityJournal.Mission.journal-oct-basic");f.RevealRect((RectTransform)mission.transform);
                f.AssertFullyVisible((RectTransform)mission.transform,"Last mission row is reachable by the real calendar scroll");f.Capture("journal-selected-date-last-row");
                f.Click(mission.name);Assert(Screen(f.Ui)=="focus-record","Calendar mission row opens the existing mission record detail.");
                float expectedScroll=(float)typeof(WeekOneQuestDemo).GetField("activityJournalScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f.Ui);
                Assert(expectedScroll<.99f,"Detail entry remembers an actually scrolled calendar position.");
                f.Click("기록 목록");
                var scroll=f.Ui.GetComponentsInChildren<ScrollRect>(false).Single(item=>item.name=="ActivityJournal.Scroll");
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.SelectedDate").text=="2026.10.31"
                    &&f.Text("ActivityJournal.Month").text=="2026년 10월"&&Mathf.Abs(scroll.verticalNormalizedPosition-expectedScroll)<.02f,
                    "Mission detail return restores the selected day, month and list position.");
                f.Click("DailyEmotion.HistoryRow."+emotion);
                Assert(Screen(f.Ui)=="daily-emotion"&&f.Button("DailyEmotion.Save",false)==null&&f.Service.GetDailyEmotion(emotion).Note=="합성 달력 기록",
                    "Calendar emotion entry opens the correct immutable saved record.");
                f.Capture("journal-emotion-detail");f.Click("DailyEmotion.History");
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.SelectedDate").text=="2026.10.31",
                    "Emotion record-list action returns to its source calendar date.");
                f.Click("ActivityJournal.Day.2026-10-30");f.Click("ActivityJournal.Mission.journal-legacy");
                Assert(Screen(f.Ui)=="record","Legacy calendar row opens the existing historical record detail.");
                f.Capture("journal-legacy-detail");f.Click("기록 목록");
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.SelectedDate").text=="2026.10.30"
                    &&f.Text("ActivityJournal.Month").text=="2026년 10월","Legacy record-list action returns to its source calendar day and month.");
                f.Click("ActivityJournal.NextMonth");f.Click("ActivityJournal.Day.2026-11-02");OneDayCard();
                Assert(f.Text("ActivityJournal.SelectedDate").text=="2026.11.02"&&f.Text("ActivityJournal.Empty")!=null
                    &&!f.Ui.GetComponentsInChildren<Button>(false).Any(button=>button.name.StartsWith("ActivityJournal.Mission.",StringComparison.Ordinal)
                        ||button.name.StartsWith("DailyEmotion.HistoryRow.",StringComparison.Ordinal)),"Empty date has clear guidance and no stale record buttons.");
                f.RevealAndRead(f.Text("ActivityJournal.Empty"));f.Capture("journal-empty-day");
                f.Click("ActivityJournal.Today");Assert(f.Text("ActivityJournal.Month").text=="2026년 11월"
                    &&f.Text("ActivityJournal.SelectedDate").text=="2026.11.01","Today returns to the current app date.");
                f.Click("ActivityJournal.Mission.journal-nov-additional");Assert(Screen(f.Ui)=="focus-record","Ongoing record is still readable from calendar.");
                Assert(f.Ui.TryNavigateBack(),"Existing back navigation handles calendar record detail.");f.Layout();
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.SelectedDate").text=="2026.11.01","Back retains the current calendar selection.");
                f.Click("ActivityJournal.Back");
                Assert(Screen(f.Ui)=="course"&&f.Button("Focus.CourseRecords.8w")!=null&&f.Button("PlantAlbum.Open")!=null,
                    "The calendar's explicit back action preserves access to the existing course and album record hub.");
                f.Capture("journal-return-record-hub");
                Assert(Snapshot(f.Service)==before&&f.Store.Saves==saves,"All calendar reads, month/date selections and detail returns preserve exact state with zero writes or rewards.");
            }
        }
        static void AssertCompletionJournalRoute(Fixture f,string id,string expectedDay,string captureName)
        {
            string before=Snapshot(f.Service);int saves=f.Store.Saves;
            Assert(Screen(f.Ui)=="focus-complete"&&f.Session(id).Status==ParticipationState.RewardCommitted,
                "Record navigation starts from the actual completed mission page.");
            Assert(ActivityJournal.DayFor(f.Session(id),f.Service.CurrentTimeZone)==expectedDay,
                "The completion fixture has the expected app-day ownership, including carry-over.");
            var action=f.Button("Focus.ViewRecord");
            Assert(action.GetComponentInChildren<Text>().text=="기록으로 이동","Completion clearly labels the calendar destination.");
            f.RevealRect((RectTransform)action.transform);f.AssertFullyVisible((RectTransform)action.transform,"Completion record action is reachable");
            f.Capture(captureName+"-action");f.Click("Focus.ViewRecord");
            var date=DateTime.ParseExact(expectedDay,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            string expectedMonth=date.Year+"년 "+date.Month+"월",selected=expectedDay.Replace('-','.');
            void AssertDate()=>Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Text("ActivityJournal.Month").text==expectedMonth
                &&f.Text("ActivityJournal.SelectedDate").text==selected&&f.Button("ActivityJournal.Mission."+id)!=null,
                "Completion selects its own record date and month, rather than today's date or a stale selection.");
            AssertDate();f.Capture(captureName);
            f.Click("ActivityJournal.Mission."+id);
            Assert(Screen(f.Ui)=="focus-record","The selected calendar mission opens its existing read-only detail.");
            f.Click("기록 목록");AssertDate();
            f.Click("ActivityJournal.Mission."+id);
            Assert(f.Ui.TryNavigateBack(),"System back handles a mission detail opened from completion's calendar.");f.Layout();AssertDate();
            Assert(Snapshot(f.Service)==before&&f.Store.Saves==saves,
                "Completion-to-calendar, detail, record-list and back navigation perform zero writes, growth or coin rewards.");
        }
        static void BasicIconGestureFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                Assert(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                f.Ui.ShowHome();f.Layout();f.Click("Focus.BasicAction");
                Assert(f.Text("Guided.Overview")!=null,"Basic regression reaches the actual first overview shown in the reported screen.");
                f.WithEventSystem(()=>
                {
                    var button=f.Button("Focus.IconAction");var rect=(RectTransform)button.transform;var normal=rect.localScale;
                    var bounds=f.Bounds(rect);string before=Snapshot(f.Service);int saves=f.Store.Saves;var baseline=f.Pixels();
                    f.Capture("basic-overview-baseline");var failures=new List<string>();
                    void Inspect(string label)
                    {
                        var shown=f.Pixels();var withoutHitGraphic=f.WithoutTargetPixels(button);
                        int green=f.AddedGreenPixels(withoutHitGraphic,shown,bounds,10);
                        report.gesturePixels.Add(width+"x"+height+" basic overview "+label+": darkGreenInterior="+green);
                        if(green>8)failures.Add(label+" adds an opaque green face through the transparent overview hit graphic ("+green+" pixels).");
                    }
                    var data=f.Down(button);f.Capture("basic-overview-down");Inspect("pointer down");
                    data.dragging=true;data.eligibleForClick=false;data.delta=new Vector2(42,16);data.position+=data.delta;
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.dragHandler);
                    f.Capture("basic-overview-drag");Inspect("drag");
                    data.position=new Vector2(bounds.xMax+24,bounds.center.y);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.endDragHandler);
                    f.Capture("basic-overview-cancelled");Inspect("cancelled release");
                    Assert(rect.localScale==normal&&f.Text("Guided.Overview")!=null&&Snapshot(f.Service)==before&&f.Store.Saves==saves,
                        "Dragging out retains overview, restores its scale and performs no practice or save.");
                    f.Select(button,false);f.Capture("basic-overview-keyboard-focus");var focused=f.Pixels();
                    Assert(f.ChangedPixels(baseline,focused,bounds,10)<=8&&f.ChangedPixels(baseline,focused,bounds,-5)>=4,
                        "Basic overview keyboard focus has a visible border and unchanged centre.");
                    data=f.Down(button);f.Capture("basic-overview-keyboard-to-pointer");Inspect("keyboard then pointer");
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
                    f.Select(null,false);f.Click("Focus.IconAction");
                    Assert(f.Text("Guided.Preparation")!=null&&f.Text("Guided.Overview")==null&&Snapshot(f.Service)==before&&f.Store.Saves==saves,
                        "One ordinary click after cancellation advances exactly one guidance slide without starting practice.");
                    Assert(failures.Count==0,string.Join("\n",failures));
                });
            }
        }
        static void IconGestureFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                Assert(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                f.Ui.ShowHome();f.Layout();f.Click("Focus.AdditionalAction");
                var session=f.Service.Snapshot.Sessions.Single(s=>s.FocusRole==FocusMissionCatalog.AdditionalRole);
                Assert(session.MissionId=="free-mission:M03","Gesture fixture reaches the actual water practice.");
                f.Click("Focus.IconAction");Assert(f.Session(session.SessionId).InstructionStep==0,"Central icon starts the actual preparation step.");
                f.Click("Focus.IconAction");Assert(f.Session(session.SessionId).InstructionStep==1,"One central click advances preparation exactly once to drinking.");
                f.WithEventSystem(()=>
                {
                    var button=f.Button("Focus.IconAction");var rect=(RectTransform)button.transform;
                    var normal=rect.localScale;var bounds=f.Bounds(rect);string before=Snapshot(f.Service);int saves=f.Store.Saves;
                    var baseline=f.Pixels();f.Capture("water-gesture-baseline");
                    var failures=new List<string>();
                    void Inspect(string label,Color32[] pixels,bool keyboard=false)
                    {
                        int green=f.AddedGreenPixels(baseline,pixels,bounds,10);int interior=f.ChangedPixels(baseline,pixels,bounds,10);
                        int total=f.ChangedPixels(baseline,pixels,bounds,-5);
                        report.gesturePixels.Add(width+"x"+height+" "+label+": darkGreenInterior="+green+", changedInterior="+interior+", changedWithBorder="+total);
                        if(green>8)failures.Add(label+" adds an opaque green fill inside the illustration ("+green+" pixels).");
                        if(keyboard&&(interior>8||total<4))failures.Add(label+" must show a border while retaining the unchanged illustration interior.");
                    }
                    var data=f.Down(button);f.Capture("water-gesture-down");Inspect("pointer down",f.Pixels());
                    // Model the event-module cancellation route: a drag cannot dispatch a click.
                    data.dragging=true;data.eligibleForClick=false;data.delta=new Vector2(42,16);data.position+=data.delta;
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.dragHandler);
                    f.Capture("water-gesture-drag");Inspect("drag",f.Pixels());
                    data.position=new Vector2(bounds.xMax+24,bounds.center.y);
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);
                    f.Capture("water-gesture-exit");Inspect("pointer exit",f.Pixels());
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.endDragHandler);
                    f.Capture("water-gesture-cancelled");Inspect("cancelled release",f.Pixels());
                    Assert(rect.localScale==normal&&Snapshot(f.Service)==before&&f.Store.Saves==saves,"Dragging out restores scale and never confirms a practice step or writes a reward.");
                    data=f.Down(button);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
                    f.Capture("water-gesture-released");Inspect("pointer release without click",f.Pixels());
                    // Keyboard focus legitimately reveals a control in a short viewport.
                    // Compare its border after that layout movement, not against the old scroll position.
                    FocusKeyboardNavigation.Reveal(button);f.Layout();bounds=f.Bounds(rect);baseline=f.Pixels();
                    f.Capture("water-keyboard-unfocused");
                    f.Select(button,false);f.Capture("water-keyboard-focus");Inspect("keyboard focus",f.Pixels(),true);
                    data=f.Down(button);f.Capture("water-keyboard-to-pointer");Inspect("keyboard focus then pointer",f.Pixels());
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerExitHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
                    f.Select(null,false);button.gameObject.SetActive(false);button.gameObject.SetActive(true);f.Layout();
                    Assert(rect.localScale==normal&&Snapshot(f.Service)==before,"Disable/reentry restores the target without altering the practice.");
                    var ordinary=f.Button("Focus.Action");var ordinaryBounds=f.Bounds((RectTransform)ordinary.transform);var ordinaryBefore=f.Pixels();
                    f.Select(ordinary,false);f.Capture("water-keyboard-ordinary-action");var ordinaryAfter=f.Pixels();
                    Assert(f.ChangedPixels(ordinaryBefore,ordinaryAfter,ordinaryBounds,10)<=8&&f.ChangedPixels(ordinaryBefore,ordinaryAfter,ordinaryBounds,-5)>=4,
                        "Keyboard focus on the ordinary filled action also preserves its interior and has a visible border.");
                    f.Select(null,false);var retained=button.onClick;f.Click("Focus.IconAction");
                    Assert(f.Session(session.SessionId).InstructionStep==2,"A normal click after cancelled gestures confirms the drinking step once.");
                    string finished=Snapshot(f.Service);int committedSaves=f.Store.Saves;retained.Invoke();f.Layout();
                    Assert(Snapshot(f.Service)==finished&&f.Store.Saves==committedSaves,"Retained previous-step callback cannot repeat completion or rewards.");
                    Assert(failures.Count==0,string.Join("\n",failures));
                });
            }
        }
        static void HomeResponsiveFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                Assert(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                f.Ui.ShowHome();AssertHomeWithoutScroll(f);f.Capture("home-fit-locked");
                var exposure=f.Ui.GetComponentsInChildren<RecommendationExposure>(false).Single();
                int observed=0;var original=exposure.OnVisible;
                exposure.OnVisible=()=>{observed++;original?.Invoke();};
                try
                {
                    // Edit-mode batch has no regular LateUpdate. Exercise the
                    // existing visibility calculation, not its forced-report API.
                    var update=typeof(RecommendationExposure).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                    update.Invoke(exposure,null);update.Invoke(exposure,null);
                    Assert(observed==1,"Visible recommendation reports once even though home has no ScrollRect");
                }
                finally{exposure.OnVisible=original;}
                Assert(!f.Button("DailyEmotion.CardAction",false).IsInteractable(),"Visible emotion action keeps its ordinary locked policy");
                CompleteRoleFixture(f,"home-fit-basic","P01",1);
                CompleteRoleFixture(f,"home-fit-additional","M03");
                f.Ui.ShowHome();AssertHomeWithoutScroll(f);f.Capture("home-fit-unlocked");
                Assert(f.Button("DailyEmotion.CardAction").IsInteractable(),"Both completed roles unlock the visible emotion action");
                string before=Snapshot(f.Service);f.Store.Fail=true;
                try{f.Click("DailyEmotion.CardAction");}finally{f.Store.Fail=false;}
                Assert(Screen(f.Ui)=="home"&&Snapshot(f.Service)==before,"Failed emotion entry stays on home and preserves the whole saved state");
                Assert(f.Text("Focus.SaveNotice")!=null,"Actual failed save exposes a notice on the three-card home");
                AssertHomeWithoutScroll(f);f.Capture("home-fit-save-notice");
                f.Click("DailyEmotion.CardAction");
                Assert(Screen(f.Ui)=="daily-emotion","Still-visible emotion action retries successfully without scrolling");
                string id=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);
                Assert(f.Service.CompleteDailyEmotion(id,"편안함","합성 화면 검사 메모"),f.Service.LastError);
                f.Ui.ShowHome();AssertHomeWithoutScroll(f);f.Capture("home-fit-completed");
                Assert(f.Button("DailyEmotion.CardAction").GetComponentInChildren<Text>().text=="기록 보기",
                    "Completed emotion retains the visible record-reading action");
            }
            // Session title snapshots may outlive catalog wording. Use a separate
            // synthetic store to exercise genuine saved titles rather than resize
            // labels by hand or alter any product catalog definition.
            var store=new Store();const string basicTitle="몸과 마음에 남은 작은 변화를 천천히 알아차리기";
            const string additionalTitle="주변의 소리와 몸의 느낌을 차분하게 알아차리기";
            using(var service=new GardenStateService(store,new Clock(),new DemoBalanceConfig(),new LegacySnapshot{Nutrient=137}))
            {
                Assert(service.Open()&&service.SelectMbctProgram("8w"),service.LastError);
                Assert(service.BeginFocusSession("home-fit-long-basic","P01",1)&&service.StartSession("home-fit-long-basic"),service.LastError);
                Assert(service.BeginFocusSession("home-fit-long-additional","M03")&&service.StartSession("home-fit-long-additional"),service.LastError);
                var state=service.Snapshot;
                state.Sessions.Single(item=>item.SessionId=="home-fit-long-basic").MissionTitleSnapshot=basicTitle;
                state.Sessions.Single(item=>item.SessionId=="home-fit-long-additional").MissionTitleSnapshot=additionalTitle;
                store.Save(state);
            }
            using(var f=new Fixture(width,height,store))
            {
                f.Ui.ShowHome();AssertHomeWithoutScroll(f);
                foreach(string title in new[]{basicTitle,additionalTitle})
                    Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(text=>text.text==title),"Long saved mission title remains complete on its card");
                Assert(f.Button("Focus.BasicAction").GetComponentInChildren<Text>().text.Contains("이어서")
                    &&f.Button("Focus.AdditionalAction").GetComponentInChildren<Text>().text.Contains("이어서"),
                    "Both longer resume states retain their own actions");
                f.Capture("home-fit-long-saved-titles");
            }
        }
        static bool Overlaps(Rect a,Rect b) => Mathf.Min(a.xMax,b.xMax)>Mathf.Max(a.xMin,b.xMin)+.5f
            &&Mathf.Min(a.yMax,b.yMax)>Mathf.Max(a.yMin,b.yMin)+.5f;
        static bool Contains(Rect outer,Rect inner) => inner.xMin>=outer.xMin-.5f&&inner.xMax<=outer.xMax+.5f
            &&inner.yMin>=outer.yMin-.5f&&inner.yMax<=outer.yMax+.5f;
        static void AssertHomeWithoutScroll(Fixture f)
        {
            string before=Snapshot(f.Service);f.Layout();
            Assert(Screen(f.Ui)=="home","Three-card check targets the real mission home page");
            Assert(!f.Ui.GetComponentsInChildren<ScrollRect>(false).Any(scroll=>scroll.isActiveAndEnabled&&(scroll.vertical||scroll.horizontal)),
                "Mission home has no active scrolling owner");
            var actions=new[]{f.Button("Focus.BasicAction",false),f.Button("Focus.AdditionalAction",false),f.Button("DailyEmotion.CardAction",false)};
            Assert(actions.All(button=>button!=null),"All three mission actions exist together, including locked and completed actions");
            var cards=actions.Select(button=>(RectTransform)button.transform.parent).ToArray();
            Assert(cards.Distinct().Count()==3,"Each mission action belongs to its own card");
            var bar=f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single();
            var nav=f.Bounds((RectTransform)bar.transform);f.AssertFullyVisible((RectTransform)bar.transform,"Bottom navigation");
            var launcher=f.Button("Focus.DebugLauncher");var debug=f.Bounds((RectTransform)launcher.transform);
            f.AssertFullyVisible((RectTransform)launcher.transform,"Development launcher");
            Assert(!Overlaps(nav,debug),"Development launcher does not cover bottom navigation");
            var notice=f.Text("Focus.SaveNotice");Rect? noticeBounds=null;
            if(notice!=null&&!string.IsNullOrWhiteSpace(notice.text))
            {
                noticeBounds=f.Bounds(notice.rectTransform);f.AssertFullyVisible(notice.rectTransform,"Actual save failure notice");
                Assert(!Overlaps(noticeBounds.Value,nav)&&!Overlaps(noticeBounds.Value,debug),"Save failure notice has its own region above navigation and developer entry");
                Assert(notice.preferredHeight<=notice.rectTransform.rect.height+.5f,"Full save failure message fits its allocated height");
            }
            for(int i=0;i<cards.Length;i++)
            {
                var card=f.Bounds(cards[i]);var action=f.Bounds((RectTransform)actions[i].transform);
                f.AssertFullyVisible(cards[i],actions[i].name+" complete card");
                f.AssertFullyVisible((RectTransform)actions[i].transform,actions[i].name+" action without reveal");
                Assert(Contains(card,action),actions[i].name+" remains inside its own card");
                Assert(!Overlaps(card,nav)&&!Overlaps(card,debug)&&(!noticeBounds.HasValue||!Overlaps(card,noticeBounds.Value)),
                    actions[i].name+" card avoids navigation, developer entry and storage notice");
                for(int j=0;j<i;j++)Assert(!Overlaps(card,f.Bounds(cards[j])),"Mission cards never overlap each other");
                foreach(var text in cards[i].GetComponentsInChildren<Text>(false).Where(item=>!string.IsNullOrWhiteSpace(item.text)))
                {
                    var bounds=f.Bounds(text.rectTransform);f.AssertFullyVisible(text.rectTransform,text.text+" card text");
                    Assert(Contains(card,bounds),"Card text stays inside its card: "+text.text);
                    Assert(text.preferredHeight<=text.rectTransform.rect.height+.5f,"All wrapped card text remains readable: "+text.text);
                    if(!text.transform.IsChildOf(actions[i].transform))Assert(!Overlaps(bounds,action),"Card guidance does not overlap its action: "+text.text);
                }
                f.AssertHitWithoutReveal(actions[i]);
            }
            f.AssertHitWithoutReveal(launcher);
            foreach(var button in new[]{bar.HomeButton,bar.MissionButton,bar.CourseButton})f.AssertHitWithoutReveal(button);
            Assert(Snapshot(f.Service)==before,"Responsive home layout and hit testing do not change saved progress or rewards");
        }
        static void Ready(GardenStateService service, string id)
        {
            var session=service.Snapshot.Sessions.Single(s=>s.SessionId==id);
            Assert(service.StartSession(id),service.LastError);
            var definition=FocusMissionCatalog.FindForSession(session);
            for(int i=session.InstructionStep;i<definition.Steps.Length;i++)
            {
                var step=definition.Steps[i]; string value=step.Kind=="draw"?"DRAW1|AA_B":step.RequiredAnswer?"synthetic response":null;
                Assert(service.ConfirmFocusStep(id,i,value,value==null),service.LastError);
            }
            if(CameraMissionPolicy.Ready(service.Snapshot.Sessions.Single(x=>x.SessionId==id)))
                Assert(service.SkipPhoto(id),service.LastError);
        }
        static void StartGuidedPractice(Fixture f,string choice=null)
        {
            var selected=f.Ui.Service.Snapshot.Sessions.Single(s=>FocusMissionCatalog.IsGuided(s)&&FocusMissionPolicy.IsActive(s));
            var mission=FocusMissionCatalog.FindForSession(selected);
            Assert(Screen(f.Ui)=="focus-practice"&&selected.Status==ParticipationState.Selected&&f.Text("Guided.Overview")!=null
                &&f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null,"Guided entry starts with the overview and no running practice clock");
            f.Click("Focus.IconAction");Assert(f.Text("Guided.Preparation")!=null,"Central icon opens the preparation slide");
            f.Click("Focus.IconAction");
            if(mission.Steps[0].Options.Length>0)
            {
                Assert(f.Text("Guided.ChoiceTitle")!=null,"Preparation opens the optional practice choice before guidance");
                if(choice==null)f.Click("Focus.IconAction");else f.Click("Guided.Choice."+choice);
            }
            for(int cue=0;cue<3;cue++)
            {
                Assert(f.Text("Guided.Cue."+cue)!=null
                    &&f.Ui.GetComponentsInChildren<Text>(false).Count(t=>t.name.StartsWith("Guided.Cue.",StringComparison.Ordinal))==1,
                    "Guidance presents exactly one cue at a time in order: "+cue);
                Assert(f.Ui.Service.Snapshot.Sessions.Single(s=>s.SessionId==selected.SessionId).Status==ParticipationState.Selected
                    &&f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null,"Reading a cue does not start the session or timer");
                f.Click("Focus.IconAction");
            }
            var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);
            Assert(f.Text("Guided.PracticeHint")!=null&&clock!=null&&clock.Running
                &&f.Ui.Service.Snapshot.Sessions.Single(s=>s.SessionId==selected.SessionId).Status==ParticipationState.InProgress,
                "The last cue starts the actual practice and its clock");
        }
        static void DailyFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                Assert(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                f.Ui.ShowHome();f.Layout();
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(t=>t.text=="오늘의 미션")
                    && f.Ui.GetComponentsInChildren<Text>(false).Any(t=>t.text=="추가 미션"),"Mission home uses today's and additional mission labels");
                Assert(!f.Ui.GetComponentsInChildren<Button>(false).Any(b=>b.name.Contains("선택 목록") || b.name.Contains("다른 미션") || b.name.Contains("전체 미션")),"Normal home has no free mission selection list");
                var launcher=f.Button("Focus.DebugLauncher");f.AssertFullyVisible((RectTransform)launcher.transform,"Small development entry remains in viewport");
                f.AssertFullyVisible((RectTransform)f.Button("Focus.AdditionalAction").transform,"Initial additional CTA is fully visible without scrolling");
                var launcherRect=f.Bounds((RectTransform)launcher.transform);
                Assert(launcherRect.center.x>width*.7f && launcherRect.center.y<height*.25f && launcherRect.height<=52,"Development launcher is small and at the lower right");
                f.Capture("home-available");
                Assert(f.Button("Focus.AdditionalAction").GetComponentInChildren<Text>().text=="시작하기","Idle additional card clearly says start");
                f.Click("Focus.AdditionalAction");Assert(Screen(f.Ui)=="focus-practice","One additional card click opens actual mission guidance without a recommendation detour");
                Assert(!f.Ui.GetComponentsInChildren<Button>(false).Any(b=>b.name.StartsWith("Focus.Debug.M",StringComparison.Ordinal)),"Ordinary start never displays arbitrary M selectors");
                var m=f.Service.Snapshot.Sessions.Single(s=>s.FocusRole==FocusMissionCatalog.AdditionalRole);
                Assert(m.MissionId=="free-mission:M03" && m.Status==ParticipationState.Selected,"Normal additional entry retains assigned M identity");
                f.Capture("additional-direct-guidance");
                string chosen=m.SessionId;f.Ui.ShowHome();f.Layout();
                f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),"Advance unfinished selected M to next day");
                f.Ui.ShowHome();f.Layout();f.Capture("home-carry-selected");
                Assert(f.Button("Focus.AdditionalAction").GetComponentInChildren<Text>().text=="이어서 하기","Existing additional participation uses the concise resume label");
                f.Click("Focus.AdditionalAction");Assert(f.Service.Snapshot.Sessions.Single(s=>s.SessionId==chosen).Status==ParticipationState.Selected,"Next day resumes selected M instead of creating another");
                Ready(f.Service,chosen);string beforeFinish=Snapshot(f.Service);f.Store.Fail=true;f.Ui.SelectFocusSession(chosen);f.Layout();f.Store.Fail=false;
                Assert(Screen(f.Ui)=="practice-complete-error" && Snapshot(f.Service)==beforeFinish,"P/M final-save failure preserves complete state before any growth");
                f.Capture("additional-completion-save-failed");f.Click("PracticeCompletion.Retry");
                Assert(Screen(f.Ui)=="focus-complete" && f.Service.GetPracticeGrowth(chosen)!=null && f.Session(chosen).Status==ParticipationState.RewardCommitted,
                    "P/M practice immediately commits growth and completion without per-mission emotion/note screens");
                Assert(f.Text("Focus.CoinReward")!=null&&f.Text("Focus.CoinReward").text.Contains("50"),"Completion explicitly displays its 50 coin reward");
                f.RevealAndRead(f.Text("Focus.CoinReward"));f.Capture("additional-coin-complete");
                Assert(f.Button("Focus.SkipReflection",false)==null && f.Button("Focus.ReflectionNext",false)==null,"Completed P/M no longer offers the removed emotion/note transition");
                AssertCompletionJournalRoute(f,chosen,f.Service.CurrentCycleId,"journal-additional-carry-completion");
                f.Ui.ShowFocusCompletion(chosen);f.Layout();
                f.Click("Focus.ConfirmHome");
                var additional=f.Button("Focus.AdditionalAction",false);Assert(additional!=null&&!additional.IsInteractable(),"After final M completion, ordinary additional card is disabled");
                Assert(!f.Service.BeginFocusSession("other-today","M01"),"Different M cannot bypass used allowance");
                f.Capture("home-additional-complete");
                f.Click("Focus.BasicAction");Assert(Screen(f.Ui)=="focus-practice" && f.Text("Guided.Overview")!=null,"Independent P allowance remains available");
                var p=f.Service.Snapshot.Sessions.Single(s=>s.FocusRole==FocusMissionCatalog.BasicRole);
                StartGuidedPractice(f,"배");var clock=f.Ui.GetComponentInChildren<FocusPracticeClock>(false);clock.ElapsedSeconds=19;Assert(clock.Flush(),"Persist active P clock");
                f.Click("Focus.Close");f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),"Advance active P day");
                f.Ui.ShowHome();f.Layout();f.Capture("home-carry-active");f.Click("Focus.BasicAction");
                Assert(f.Service.Snapshot.Sessions.Single(s=>s.SessionId==p.SessionId).FocusStepRecords[0].Value=="배"
                    && f.Text("Guided.PracticeHint")!=null&&f.Ui.GetComponentInChildren<FocusPracticeClock>(false).ElapsedSeconds==19,"Active P identity, choice and elapsed time survive next-day entry");
                f.Click("Focus.IconAction");Assert(Screen(f.Ui)=="focus-complete"&&f.Session(p.SessionId).Status==ParticipationState.RewardCommitted,"Carried guided P finishes immediately");
                Assert(f.Session(p.SessionId).InstanceId!=p.InstanceId,"Carried unfinished practice belongs to its actual completion day.");
                AssertCompletionJournalRoute(f,p.SessionId,f.Service.CurrentCycleId,"journal-basic-carry-completion");
                f.Ui.ShowFocusCompletion(p.SessionId);f.Layout();f.Click("Focus.ConfirmHome");
                Assert(f.Service.CoinBalance==237,"Two independent completed roles pay exactly 50 coins each");
                f.Ui.ShowCourse();f.Layout();Assert(!f.Ui.GetComponentsInChildren<Button>(false).Any(b=>b.name.Contains("다시 연습")||b.name.Contains("선택 목록")),"Course history preserves records without free replay/selection CTA");
                f.Capture("course-history");
            }
        }
        static void AssertCoursePage(Fixture f,string program)
        {
            Assert(Screen(f.Ui)=="course","Current course page is open");
            var cards=f.Ui.GetComponentsInChildren<Button>(false).Where(button=>button.name.StartsWith("Focus.CourseRecords.",StringComparison.Ordinal)).ToArray();
            Assert(cards.Length==1&&cards[0].name=="Focus.CourseRecords."+program,"Only the chosen course is listed, even when other courses retain history");
            var change=f.Button("Focus.ChangeProgram");var label=change.GetComponentInChildren<Text>();
            Assert(label.text=="코스 선택·변경"&&label.fontSize==14&&change.targetGraphic.color.a==0
                &&change.GetComponent<GardenThemeSurface>()==null,"Course switching is a small borderless text control");
            var cardBounds=f.Bounds((RectTransform)cards[0].transform);var linkBounds=f.Bounds((RectTransform)change.transform);
            Assert(cardBounds.yMin>=linkBounds.yMax-.5f&&cardBounds.yMin-linkBounds.yMax<=20
                &&Mathf.Abs(cardBounds.xMax-linkBounds.xMax)<.5f,"Course switching sits directly below the current course and aligns to its right edge");
            Assert(linkBounds.height>=40&&linkBounds.width<cardBounds.width/2,"Small visual link keeps a usable touch height");
            f.AssertFullyVisible((RectTransform)change.transform,"Compact course switching link");
            foreach(string removed in new[]{"추가 미션 기록","정원·건강 기록","미션 창으로 돌아가기"})
                Assert(!f.Ui.GetComponentsInChildren<Text>(false).Any(text=>text.text==removed),"Removed course menu is absent: "+removed);
            Assert(f.Button("DailyEmotion.History")!=null&&f.Button("PlantAlbum.Open")!=null,"Daily emotion history and plant photo album remain accessible");
        }
        static void CourseFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                f.Ui.ShowCourse();f.Layout();string empty=Snapshot(f.Service);int emptyWrites=f.Store.Saves;
                Assert(!f.Ui.GetComponentsInChildren<Button>(false).Any(button=>button.name.StartsWith("Focus.CourseRecords.",StringComparison.Ordinal))
                    &&f.Button("Focus.ChangeProgram")!=null,"Before choosing a course there is no invented active course card");
                f.Capture("course-not-chosen");f.Click("Focus.ChangeProgram");
                Assert(Screen(f.Ui)=="focus-programs"&&Snapshot(f.Service)==empty&&f.Store.Saves==emptyWrites,"Opening course selection does not choose a course or write progress");
                Assert(f.Button("Focus.TodayAdditional").GetComponentInChildren<Text>().text=="추가 미션 시작하기","Course selection exposes a direct additional start action");
                f.Click("Focus.TodayAdditional");
                var independent=f.Service.Snapshot.Sessions.Single();
                Assert(Screen(f.Ui)=="focus-practice"&&independent.MissionId=="free-mission:M03"&&independent.Status==ParticipationState.Selected
                    &&!f.Service.Snapshot.MbctProgramChosen,"Additional start from course selection directly opens its guidance without choosing a course");
                f.Ui.ShowProgramSelection();f.Layout();f.Click("Focus.TodayAdditional");
                Assert(Screen(f.Ui)=="focus-practice"&&f.Service.Snapshot.Sessions.Single().SessionId==independent.SessionId,"Repeated course-selection start resumes the same additional participation");
                Ready(f.Service,independent.SessionId);Assert(f.Service.CompletePracticeSession(independent.SessionId),"Complete synthetic additional once");
                f.Ui.ShowProgramSelection();f.Layout();
                Assert(!f.Button("Focus.TodayAdditional",false).IsInteractable(),"Used additional allowance also disables the course-selection entry");

                Assert(f.Service.SelectMbctProgram("2w"),"Choose first synthetic course");CompleteRoleFixture(f,"course-old-2w",FocusMissionPolicy.MissionAt(1,"2w").Code,1);
                f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>())&&f.Service.SelectMbctProgram("4w"),"Move to a second course on another day");
                CompleteRoleFixture(f,"course-old-4w",FocusMissionPolicy.MissionAt(1,"4w").Code,1);Assert(f.Service.SelectMbctProgram("8w"),"Choose current course while retaining two older histories");
                string sessions=CapstoneDesign.Plants.PlantCodec.Encode(f.Service.Snapshot.Sessions);int coins=f.Service.CoinBalance;
                f.Ui.ShowCourse();f.Layout();AssertCoursePage(f,"8w");f.Capture("course-current-only");
                string before=Snapshot(f.Service);int writes=f.Store.Saves;
                f.Click("Focus.CourseRecords.8w");Assert(Screen(f.Ui)=="focus-course","Current course card opens its saved course detail");
                f.Ui.ShowCourse();f.Click("Focus.ChangeProgram");Assert(Screen(f.Ui)=="focus-programs","Borderless course link opens all course choices");
                Assert(f.Ui.GetComponentsInChildren<Button>(false).Count(button=>button.name.StartsWith("Focus.SelectProgram.",StringComparison.Ordinal))==3,"Selection still offers all three course durations");
                f.Ui.ShowCourse();f.Layout();AssertCoursePage(f,"8w");
                Assert(Snapshot(f.Service)==before&&f.Store.Saves==writes,"Leaving selection without confirmation preserves current course and all saved data");
                f.Click("Focus.ChangeProgram");f.Click("Focus.SelectProgram.2w");
                Assert(Screen(f.Ui)=="focus-program-confirm"&&f.Service.Snapshot.MbctProgramId=="8w","Viewing another course does not switch it before confirmation");
                f.Click("Focus.ConfirmProgram.2w");f.Ui.ShowCourse();f.Layout();AssertCoursePage(f,"2w");f.Capture("course-switched-retained-progress");
                Assert(CapstoneDesign.Plants.PlantCodec.Encode(f.Service.Snapshot.Sessions)==sessions&&f.Service.CoinBalance==coins
                    &&FocusMissionPolicy.CompletedCount(f.Service.Snapshot,"2w")==1&&FocusMissionPolicy.CompletedCount(f.Service.Snapshot,"4w")==1,
                    "Switching the one visible card preserves old session records, course progress and coins");
                f.Click("PlantAlbum.Open");Assert(Screen(f.Ui)=="plant-album","Retained plant photo album button opens its real page");
                f.Ui.ShowCourse();f.Click("DailyEmotion.History");Assert(Screen(f.Ui)=="daily-emotion-history","Retained daily emotion history button opens its real page");
            }
        }
        static void DebugFlow()
        {
            using(var f=new Fixture(390,844))
            {
                Assert(f.Service.SelectMbctProgram("8w")&&f.Service.RefreshCycle(Array.Empty<string>()),"Choose original developer-test course");
                Assert(f.Service.BeginSession("original-old-p","course:P01",1)&&f.Service.StartSession("original-old-p")&&f.Service.CompletePracticeSession("original-old-p")
                    &&f.Service.BeginSession("original-old-m","free-mission:M03")&&f.Service.StartSession("original-old-m")&&f.Service.CompletePracticeSession("original-old-m"),"Seed original prior-day role records");
                Assert(f.Service.BeginDailyEmotion(f.Service.CurrentCycleId)&&f.Service.SaveDailyEmotionDraft(DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId),"평온함","synthetic original daily note",1),"Seed original emotion draft that debug must preserve");
                f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>())&&f.Service.BeginFocusSession("real-active","P01",1),"Seed original unfinished P next day");
                Assert(f.Service.SaveGuidedFocusDraft("real-active","가슴",FocusMissionCatalog.Find("P01").Steps[1].Options[0],23),"Preserve original draft before developer tests: "+f.Service.LastError);
                f.Ui.ShowHome();f.Layout();string original=Snapshot(f.Service);int originalWrites=f.Store.Saves;
                foreach(var mission in FocusMissionCatalog.All)
                {
                    f.Click("Focus.DebugLauncher");Assert(Screen(f.Ui)=="mission-debug-picker","Actual development launcher opens picker");
                    if(mission.Code=="P01")f.Capture("development-picker");
                    f.Click("Focus.Debug."+mission.Code);
                    Assert(f.Ui.IsMissionDebugActive && !ReferenceEquals(f.Ui.Service,f.Service),"Developer "+mission.Code+" uses a separate memory service");
                    var sandbox=f.Ui.Service;
                    var active=f.Ui.Service.Snapshot.Sessions.Single(s=>FocusMissionPolicy.IsActive(s));
                    bool guided=FocusMissionCatalog.IsGuided(active);
                    bool basicSlides=active.FocusVersion>=3&&mission.Role==FocusMissionCatalog.BasicRole&&!guided;
                    Assert(active.MissionId==mission.Id && active.Status==(guided||basicSlides?ParticipationState.Selected:ParticipationState.InProgress)
                        && Screen(f.Ui)=="focus-practice","Developer "+mission.Code+" enters its requested mission at the appropriate initial state");
                    if(guided)StartGuidedPractice(f);
                    else if(basicSlides)
                    {
                        Assert(f.Text("Basic.Overview")!=null&&f.Text("Basic.Body")==null
                            &&f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null,"Developer "+mission.Code+" opens its overview before practice starts");
                        f.Click("Focus.IconAction");
                        var started=sandbox.Snapshot.Sessions.Single(s=>s.SessionId==active.SessionId);
                        Assert(f.Text("Basic.Overview")==null&&f.Text("Basic.Body")!=null
                            &&started.Status==ParticipationState.InProgress&&started.InstructionStep==0,
                            "Developer "+mission.Code+" starts the first saved practice step from the central icon");
                    }
                    Assert(Snapshot(f.Service)==original && f.Store.Saves==originalWrites,"Developer "+mission.Code+" entry leaves original saved progress/wallet/plant byte-identical");
                    if(new[]{"P01","P28","M03","M15","M17"}.Contains(mission.Code))f.Capture("development-"+mission.Code);
                    if(mission.Code=="P01"||mission.Code=="M03")
                    {
                        if(mission.Code=="P01")f.Click("Focus.IconAction");
                        else {Ready(f.Ui.Service,active.SessionId);f.Ui.SelectFocusSession(active.SessionId);f.Layout();}
                        Assert(Screen(f.Ui)=="focus-complete" && sandbox.Snapshot.Sessions.Single(s=>s.SessionId==active.SessionId).Status==ParticipationState.RewardCommitted
                            &&f.Button("Focus.DebugExit")!=null,"Development practice completes directly and includes explicit exit");
                        f.Capture("development-complete-"+mission.Code);f.Click("Focus.DebugExit");
                    }
                    else f.Click("Focus.Close");
                    Assert(!f.Ui.IsMissionDebugActive && ReferenceEquals(f.Ui.Service,f.Service) && Screen(f.Ui)=="home","Closing "+mission.Code+" restores actual presenter service and home");
                    Assert(!sandbox.Open(),"Closed developer sandbox cannot be reopened after disposal");
                    Assert(Snapshot(f.Service)==original && f.Store.Saves==originalWrites,"Development completion/exit never changes original progress, choices, notes, coins or growth");
                }
                f.Click("Focus.DebugLauncher");f.Click("Focus.DebugPickerClose");Assert(ReferenceEquals(f.Ui.Service,f.Service)&&Snapshot(f.Service)==original,"Picker cancellation is side-effect free");
                f.Click("Focus.DebugLauncher");f.Click("Focus.Debug.P01");var leaving=f.Ui.Service;
                f.Ui.LeaveActivityPanel();
                Assert(!f.Ui.IsMissionDebugActive&&ReferenceEquals(f.Ui.Service,f.Service)&&!leaving.Open()&&Snapshot(f.Service)==original,
                    "Navigation away from activity panel restores original service and discards developer results");
                f.Ui.ShowHome();f.Layout();
                f.Click("Focus.BasicAction");Assert(f.Service.Snapshot.Sessions.Single(s=>s.SessionId=="real-active").FocusStepRecords[0].Value=="가슴"
                    && f.Text("Guided.Overview")!=null&&f.Ui.GetComponentInChildren<FocusPracticeClock>(false)==null,
                    "Original selected real practice restores its draft at the overview after all developer tests");
            }
        }
        static void CompleteRoleFixture(Fixture f,string id,string code,int order=0)
        {
            Assert(f.Service.BeginFocusSession(id,code,order),f.Service.LastError);Ready(f.Service,id);
            Assert(f.Service.CompletePracticeSession(id),f.Service.LastError);
        }
        static void EmotionFlow(int width,int height)
        {
            var store=new Store();DateTimeOffset reopenTime;string recordId;
            using(var f=new Fixture(width,height,store))
            {
                Assert(f.Service.SelectMbctProgram("8w"),"Choose daily emotion course");f.Ui.ShowHome();f.Layout();
                var locked=f.Button("DailyEmotion.CardAction",false);
                Assert(locked!=null&&!locked.IsInteractable(),"Third daily card is locked before P and M completion");
                f.AssertFullyVisible((RectTransform)locked.transform,"Locked third card action is readable without scrolling");f.Capture("emotion-locked");
                f.Click("Focus.BasicAction");StartGuidedPractice(f);f.Click("Focus.IconAction");
                Assert(Screen(f.Ui)=="focus-complete"&&f.Service.Snapshot.Sessions.Single().Status==ParticipationState.RewardCommitted,"Actual guided P finish commits directly to completion");f.Click("Focus.ConfirmHome");
                Assert(!f.Button("DailyEmotion.CardAction",false).IsInteractable(),"One finished role keeps third card locked");
                f.Click("Focus.AdditionalAction");Assert(Screen(f.Ui)=="focus-practice","Additional starts directly while the completed P remains intact");
                for(int attempt=0;Screen(f.Ui)=="focus-practice"&&attempt<6;attempt++)f.Click("Focus.Action");
                if(Screen(f.Ui)=="focus-photo")f.Click("Photo.Skip");
                Assert(Screen(f.Ui)=="focus-complete"&&f.Service.Snapshot.Sessions.Count(s=>s.Status==ParticipationState.RewardCommitted)==2,"Actual M actions complete the second role without emotion/note detour");f.Click("Focus.ConfirmHome");
                f.AssertFullyVisible((RectTransform)f.Button("DailyEmotion.CardAction").transform,"Unlocked third action is visible without scrolling");f.Capture("emotion-unlocked");
                int step=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;f.Click("DailyEmotion.CardAction");
                Assert(Screen(f.Ui)=="daily-emotion","Third activity opens a distinct daily form");recordId=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);
                Assert(f.Service.GetDailyEmotion(recordId)!=null&&!f.Button("DailyEmotion.Save",false).IsInteractable(),"Blank daily form cannot complete");
                Assert(f.Ui.GetComponentsInChildren<Button>(false).Count(b=>b.name.StartsWith("DailyEmotion.Emotion.",StringComparison.Ordinal))==9,"Initial form offers eight emotions plus unknown");
                f.Click("DailyEmotion.MoreEmotions");
                Assert(f.Ui.GetComponentsInChildren<Button>(false).Count(b=>b.name.StartsWith("DailyEmotion.Emotion.",StringComparison.Ordinal))==25,"Expanded form offers 24 emotions plus unknown");
                var extended=f.Ui.GetComponentsInChildren<Button>(false).First(b=>b.name.StartsWith("DailyEmotion.Emotion.",StringComparison.Ordinal)
                    &&!new[]{"기쁨","편안함","뿌듯함","무덤덤함","슬픔","불안함","답답함","화남","잘 모르겠어요"}.Contains(b.name.Substring("DailyEmotion.Emotion.".Length)));
                string extendedMood=extended.name.Substring("DailyEmotion.Emotion.".Length);f.Click(extended.name);f.Click("DailyEmotion.MoreEmotions");
                Assert(f.Text("DailyEmotion.Status").text.Contains(extendedMood),"A selected emotion outside the first eight stays identified when the choices are collapsed");
                f.RevealAndRead(f.Text("DailyEmotion.Status"));f.Capture("emotion-collapsed-selection");
                f.Click("DailyEmotion.Emotion.잘 모르겠어요");
                var note=f.Ui.GetComponentsInChildren<InputField>(false).Single(i=>i.name=="DailyEmotion.Note");note.text="synthetic daily note";note.onEndEdit.Invoke(note.text);f.Layout();
                Assert(f.Ui.SaveDailyEmotionUiDraft(),"Persist daily choice and optional note");
                f.RevealRect((RectTransform)note.transform);f.AssertFullyVisible((RectTransform)note.transform,"Daily note is visible by actual scrolling");f.Capture("emotion-draft-note");
                Assert(f.Service.GetDailyEmotion(recordId).Mood=="잘 모르겠어요"&&f.Service.GetDailyEmotion(recordId).Note=="synthetic daily note"
                    &&f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==step,"Writing daily content preserves plant until final save");
                f.Click("DailyEmotion.Back");Assert(Screen(f.Ui)=="home","Daily back returns to mission home with draft retained");
                reopenTime=f.Clock.Time;
            }
            using(var f=new Fixture(width,height,store,reopenTime))
            {
                f.Ui.ShowHome();f.Layout();f.Click("DailyEmotion.CardAction");
                var record=f.Service.GetDailyEmotion(recordId);var note=f.Ui.GetComponentsInChildren<InputField>(false).Single(i=>i.name=="DailyEmotion.Note");
                Assert(record.Mood=="잘 모르겠어요"&&note.text=="synthetic daily note","Recreated presenter restores the saved daily draft and fields");
                string before=Snapshot(f.Service);int stage=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;
                f.Store.Fail=true;f.Click("DailyEmotion.Save");f.Store.Fail=false;
                Assert(Screen(f.Ui)=="daily-emotion"&&Snapshot(f.Service)==before&&f.Text("DailyEmotion.Error")!=null,"Daily final-save failure keeps content, completion and growth atomic");
                f.RevealAndRead(f.Text("DailyEmotion.Error"));f.Capture("emotion-save-failed");
                var oldSave=f.Button("DailyEmotion.Save").onClick;f.Click("DailyEmotion.Save");
                Assert(Screen(f.Ui)=="home"&&f.Service.GetDailyEmotion(recordId).IsCompleted&&f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==stage&&f.Service.CoinBalance==337
                    &&f.Service.GetPracticeGrowth(recordId)==null&&f.Service.Snapshot.PlantAppearance.Memos.Count==1
                    &&f.Service.GetDailyEmotion(recordId).RewardAmount==100,"Successful daily save returns home with one memo and 100 coins without growth");
                Assert(!Resources.FindObjectsOfTypeAll<GardenMissionGrowthSequence>().Any(sequence=>sequence.IsPlaying),"Daily coin and memo completion never begins the plant growth cinematic");
                string complete=Snapshot(f.Service);oldSave.Invoke();f.Layout();
                Assert(Snapshot(f.Service)==complete&&Screen(f.Ui)=="home","Old daily save callback cannot duplicate growth or reopen a stale form");
                f.RevealRect((RectTransform)f.Button("DailyEmotion.CardAction").transform);f.Capture("emotion-home-complete");
                f.Click("DailyEmotion.CardAction");
                Assert(Screen(f.Ui)=="daily-emotion"&&(f.Button("DailyEmotion.Save",false)==null||!f.Button("DailyEmotion.Save",false).IsInteractable()),"Completed daily card opens immutable history view");
                f.Capture("emotion-completed-readonly");f.Ui.ShowDailyEmotionHistory();f.Layout();
                Assert(Screen(f.Ui)=="daily-emotion-history"&&f.Button("DailyEmotion.HistoryRow."+recordId)!=null,"Daily history has its own record row");
                f.Capture("emotion-history");f.Click("DailyEmotion.HistoryRow."+recordId);
                Assert(f.Service.GetDailyEmotion(recordId).Note=="synthetic daily note"&&Snapshot(f.Service)==complete,"Reading saved daily history does not mutate growth or content");
                string completedSource=f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(f.Service.SetPlantCompleted(completedSource,true)&&f.Service.SavePlantPhoto("daily-readonly-archive",completedSource),"Archive completed memo-bearing individual");
                f.Ui.ShowDailyEmotion();f.Layout();
                Assert(Screen(f.Ui)=="daily-emotion"&&f.Button("DailyEmotion.Save",false)==null&&f.Text("DailyEmotion.Status").text.Contains("100"),
                    "Parameterless completed daily entry stays readable after automatic archive and identifies its earned coins");
                f.RevealAndRead(f.Text("DailyEmotion.Status"));f.Capture("emotion-archived-current-readonly");
                Assert(f.Service.Snapshot.PlantPhotos.Count==1,"Completed plant already appears in the album before the next mission");
                f.Ui.ShowHome();f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),"Advance to second emotion day");
                CompleteRoleFixture(f,"next-p","P02",2);CompleteRoleFixture(f,"next-m","M03");f.Ui.ShowHome();f.Layout();f.Click("DailyEmotion.CardAction");
                string expired=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);note=f.Ui.GetComponentsInChildren<InputField>(false).Single(i=>i.name=="DailyEmotion.Note");
                note.text="synthetic expired draft";note.onEndEdit.Invoke(note.text);Assert(f.Ui.SaveDailyEmotionUiDraft(),"Save draft before expiry");
                var expiredSave=f.Button("DailyEmotion.Save").onClick;int beforeExpiry=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;
                f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),"Expire old emotion business day");
                expiredSave.Invoke();f.Layout();
                Assert(!f.Service.GetDailyEmotion(expired).IsCompleted&&f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==beforeExpiry,"Stale save after 04:00 never completes or rewards yesterday's draft");
                f.Ui.ShowDailyEmotion(expired);f.Layout();
                Assert(f.Button("DailyEmotion.Save",false)==null||!f.Button("DailyEmotion.Save",false).IsInteractable(),"Expired draft is read-only rather than carried into today's form");
                f.Capture("emotion-expired-draft");f.Ui.ShowHome();f.Layout();
                Assert(!f.Button("DailyEmotion.CardAction",false).IsInteractable(),"New day locks emotion until today's own P and M finish");
            }
        }
        static void EmptyPlantFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                Assert(f.Service.SelectMbctProgram("8w")&&f.Service.BeginFocusSession("auto-pending","M03"),"Prepare selected mission before completion");
                Ready(f.Service,"auto-pending");
                string source=f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(f.Service.SetPlantCompleted(source,true),"Complete the current individual");
                Assert(f.Service.Snapshot.PlantPhotos.Count==1&&GardenPlantVisuals.HasCurrentPlant(f.Service.Snapshot),
                    "Completion automatically archives and retains the visible current plant");
                string photo=CapstoneDesign.Plants.PlantCodec.Encode(f.Service.Snapshot.PlantPhotos),before=Snapshot(f.Service);
                f.Store.Fail=true;f.Ui.SelectFocusSession("auto-pending");f.Layout();f.Store.Fail=false;
                Assert(Screen(f.Ui)=="practice-complete-error"&&Snapshot(f.Service)==before,
                    "Failed automatic next-seed completion preserves archive, active plant and mission");
                var stale=f.Button("PracticeCompletion.Retry").onClick;f.Click("PracticeCompletion.Retry");
                Assert(Screen(f.Ui)=="focus-complete"&&f.Service.CoinBalance==187
                    &&f.Service.Snapshot.PlantAppearance.Recipe.InstanceId!=source
                    &&f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==1
                    &&f.Service.Snapshot.PlantAppearance.Recipe.MaxGrowthSteps==21
                    &&CapstoneDesign.Plants.PlantCodec.Encode(f.Service.Snapshot.PlantPhotos)==photo,
                    "Pending practice automatically starts and grows only a new 21-step seed, preserving the archived plant");
                string committed=Snapshot(f.Service);stale.Invoke();f.Layout();
                Assert(Snapshot(f.Service)==committed,"Stale retry cannot create another seed or pay twice");
                f.Capture("automatic-next-seed-complete");
            }
            using(var f=new Fixture(width,height))
            {
                Assert(f.Service.SelectMbctProgram("8w"),"Prepare daily record on a completed plant");
                CompleteRoleFixture(f,"auto-daily-p","P01",1);CompleteRoleFixture(f,"auto-daily-m","M03");
                string id=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);
                Assert(f.Service.BeginDailyEmotion(f.Service.CurrentCycleId)&&f.Service.SaveDailyEmotionDraft(id,"평온함","synthetic retained memory",1),"Save daily draft");
                string source=f.Service.Snapshot.PlantAppearance.Recipe.InstanceId;
                Assert(f.Service.SetPlantCompleted(source,true),"Automatically archive before finishing the daily record");
                int step=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;
                f.Ui.ShowDailyEmotion(id);f.Layout();f.Click("DailyEmotion.Save");
                var state=f.Service.Snapshot;var memo=state.PlantAppearance.Memos.Single();
                Assert(Screen(f.Ui)=="home"&&f.Service.CoinBalance==337&&state.PlantAppearance.Recipe.GrowthStep==step
                    &&memo.PlantInstanceId==source&&state.PlantPhotos.Single().Memos.Single().Id==memo.Id,
                    "Daily completion keeps its current completed plant and updates the album memory with 100 coins, no new seed or growth");
                f.RevealRect((RectTransform)f.Button("DailyEmotion.CardAction").transform);f.Capture("completed-plant-daily-memory");
            }
        }
        static void LegacyCarry()
        {
            using(var f=new Fixture(360,640))
            {
                Assert(f.Service.SelectMbctProgram("8w")&&f.Service.RefreshCycle(Array.Empty<string>()),"Prepare old session fixture");
                Assert(f.Service.BeginSession("old-p","course:P01",1)&&f.Service.StartSession("old-p")
                    &&f.Service.BeginSession("old-m","free-mission:M03")&&f.Service.StartSession("old-m"),"Seed old active basic and additional sessions");
                f.Clock.Time=f.Clock.Time.AddDays(1);Assert(f.Service.RefreshCycle(Array.Empty<string>()),"Advance old-session day");
                f.Ui.ShowHome();f.Layout();f.Capture("home-legacy-carry");
                f.Click("Focus.AdditionalAction");Assert(Screen(f.Ui)=="session"&&f.Service.Snapshot.Sessions.Count==2,"Old additional CTA resumes its existing legacy runner");
                f.Ui.ShowHome();f.Layout();f.Click("Focus.BasicAction");
                Assert(Screen(f.Ui)=="session"&&f.Service.Snapshot.Sessions.Count==2,"Old basic CTA resumes its existing legacy runner without replacing history");
            }
        }
        static void OldReflectionDraft()
        {
            using(var f=new Fixture(360,640))
            {
                Assert(f.Service.SelectMbctProgram("8w")&&f.Service.BeginFocusSession("old-reflection","P01",1),"Prepare pre-update active mission");
                Ready(f.Service,"old-reflection");Assert(f.Service.CommitPracticeGrowth("old-reflection")
                    &&f.Service.SaveFocusReflection("old-reflection","평온함","synthetic retained per-mission note",1),"Seed old already-grown optional reflection draft");
                int step=f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep;f.Ui.SelectFocusSession("old-reflection");f.Layout();
                var session=f.Session("old-reflection");
                Assert(Screen(f.Ui)=="focus-complete"&&session.Status==ParticipationState.RewardCommitted&&session.Mood=="평온함"&&session.Note=="synthetic retained per-mission note"
                    &&f.Service.Snapshot.PlantAppearance.Recipe.GrowthStep==step,"Resuming old reflection completes directly, preserving its data and prior growth");
                f.Ui.ShowFocusRecord("old-reflection");f.Layout();
                Assert(f.Ui.GetComponentsInChildren<Text>(false).Any(t=>t.text.Contains("synthetic retained per-mission note"))
                    &&f.Service.Snapshot.DailyEmotionRecords.Count==0,"Old per-mission note remains readable in original history without conversion into today's third record");
                f.Capture("old-per-mission-record-preserved");
            }
        }
        static void LegacyCompletionRoutes()
        {
            using(var f=new Fixture(360,640))
            {
                Assert(f.Service.SelectMbctProgram("8w")&&f.Service.RefreshCycle(Array.Empty<string>())
                    &&f.Service.BeginSession("retained-finish","free-mission:M03")&&f.Service.StartSession("retained-finish"),"Prepare legacy Finish caller");
                f.Ui.SelectFocusSession("retained-finish");f.Layout();string before=Snapshot(f.Service);
                f.Store.Fail=true;f.Ui.Finish("평온함","synthetic explicit legacy argument");f.Layout();f.Store.Fail=false;
                Assert(Screen(f.Ui)=="practice-complete-error"&&Snapshot(f.Service)==before,"Failed retained Finish keeps old state and exposes explicit retry");
                f.Click("PracticeCompletion.Retry");var ended=f.Session("retained-finish");
                Assert(Screen(f.Ui)=="complete"&&ended.Status==ParticipationState.RewardCommitted&&ended.Mood=="평온함"&&ended.Note=="synthetic explicit legacy argument",
                    "Retry preserves the originally submitted legacy emotion/note rather than replacing them with old stored values");
            }
            foreach(bool detail in new[]{false,true})using(var f=new Fixture(360,640))
            {
                Assert(f.Service.SelectMbctProgram("8w"),"Choose background walking course");CompleteRoleFixture(f,"walking-basic","P01",1);
                // Open the page before the background session arrives. Entering
                // recommendation with an existing legacy session intentionally resumes it.
                f.Ui.ShowHome();if(detail)f.Ui.ShowFocusRecommendation();f.Layout();string expectedScreen=Screen(f.Ui);
                var day=new DateTimeOffset(f.Clock.Time.UtcDateTime.Date,TimeSpan.Zero);
                Assert(f.Service.BeginSession("background-walk","free-mission:M15")&&f.Service.StartWalkingSession("background-walk",new WalkingTarget{
                    GoalSteps=5000,QueryStartUtc=day.ToString("O"),QueryEndUtc=day.AddDays(1).ToString("O"),Basis="DEFAULT"}),"Start legacy synthetic walking target");
                Assert(f.Service.ObserveMissionSteps("background-walk",5000,WalkingPersonalization.AggregateSource),"Provide synthetic verified walking evidence without devices");
                var callback=typeof(WeekOneQuestDemo).GetMethod("CompleteWalkingGoal",BindingFlags.NonPublic|BindingFlags.Instance);
                string before=Snapshot(f.Service);f.Store.Fail=true;callback.Invoke(f.Ui,new object[]{"background-walk"});f.Layout();f.Store.Fail=false;
                Assert(Screen(f.Ui)==expectedScreen&&Snapshot(f.Service)==before,"Failed background walking completion keeps the current page and full state");
                callback.Invoke(f.Ui,new object[]{"background-walk"});f.Layout();
                Assert(Screen(f.Ui)==expectedScreen&&f.Session("background-walk").Status==ParticipationState.RewardCommitted,"Background walking completion refreshes the current card page without opening another flow");
                if(detail)Assert(f.Button("오늘 완료했어요",false)!=null&&!f.Button("오늘 완료했어요",false).IsInteractable(),"Open additional detail becomes completed after background walking");
                else Assert(f.Button("DailyEmotion.CardAction").IsInteractable(),"Open home unlocks third mission immediately after background walking");
                string completed=Snapshot(f.Service);callback.Invoke(f.Ui,new object[]{"background-walk"});
                Assert(Snapshot(f.Service)==completed,"Repeated walking callback cannot duplicate completion or growth");
                f.Capture(detail?"walking-background-detail-complete":"walking-background-home-unlocked");
            }
        }
        static void DebugTeardown()
        {
            using(var f=new Fixture(360,640))
            {
                Assert(f.Service.SelectMbctProgram("8w"),"Choose teardown fixture program");f.Ui.ShowHome();f.Layout();
                f.Click("Focus.DebugLauncher");f.Click("Focus.Debug.P01");var sandbox=f.Ui.Service;
                string original=Snapshot(f.Service);int writes=f.Store.Saves;
                int gardenBefore=Resources.FindObjectsOfTypeAll<GardenHomePresenter>().Length;
                // Edit-mode does not guarantee MonoBehaviour lifecycle callbacks;
                // invoke this production teardown explicitly, then actually destroy its object.
                typeof(WeekOneQuestDemo).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f.Ui,null);
                Assert(ReferenceEquals(f.Ui.Service,f.Service)&&!f.Ui.IsMissionDebugActive,"Teardown restores original ownership before disposing");
                Assert(!sandbox.Open()&&!f.Service.Open(),"Teardown disposes temporary and original presenter-owned services");
                Assert(StateCodec.Encode(f.Store.Load())==original&&f.Store.Saves==writes,"Teardown does not write synthetic debug results into original store");
                UnityEngine.Object.DestroyImmediate(f.Ui.gameObject);
                Assert(Resources.FindObjectsOfTypeAll<GardenHomePresenter>().Length==gardenBefore,"Teardown does not rebuild garden objects while being destroyed");
            }
        }
        static void ReleaseScripts()
        {
            string folder=Path.Combine(output,"release-scripts");Directory.CreateDirectory(folder);
            var result=PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings{target=BuildTarget.StandaloneWindows64,group=BuildTargetGroup.Standalone,options=ScriptCompilationOptions.None},folder);
            string path=Path.Combine(folder,"Assembly-CSharp.dll");Assert(File.Exists(path)&&result.assemblies.Any(a=>Path.GetFileName(a)=="Assembly-CSharp.dll"),"Actual non-development player scripts compile");
            using(var assembly=AssemblyDefinition.ReadAssembly(path))
            {
                var ui=assembly.MainModule.Types.Single(t=>t.FullName=="CapstoneDesign.Runtime.WeekOneQuestDemo");
                Assert(!ui.NestedTypes.Any(t=>t.Name=="MissionDebugMemoryStore"||t.Name=="MissionDebugClock"),"Release DLL excludes temporary development store and clock types");
                Assert(!ui.Fields.Any(f=>f.Name.StartsWith("missionDebugOriginal",StringComparison.Ordinal)),"Release DLL excludes development original-service state");
                foreach(string method in new[]{"ShowMissionDebugPicker","StartMissionDebug","RestoreMissionDebug","BuildMissionDebugLauncher"})
                {
                    var body=ui.Methods.Single(m=>m.Name==method).Body;
                    Assert(body.Instructions.All(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ret||i.OpCode.Code==Mono.Cecil.Cil.Code.Nop),"Release "+method+" contains only a no-op stub");
                }
                Assert(!ui.Methods.Any(m=>m.Name=="CreateMissionDebugService"||m.Name=="CompleteMissionDebugPredecessor"),"Release DLL cannot construct development progress fixtures");
            }
        }
        sealed class Store : IStateStore
        { string xml; public bool Fail; public int Saves; public GardenState Load() => xml == null ? null : StateCodec.Decode(xml);
          public void Save(GardenState value) { if (Fail) throw new IOException("Synthetic daily UI save failure"); xml = StateCodec.Encode(value); Saves++; } }
        sealed class Clock : IClock
        { public DateTimeOffset Time = DateTimeOffset.Parse("2026-11-09T03:00:00Z"); public DateTimeOffset UtcNow => Time; public TimeZoneInfo TimeZone => TimeZoneInfo.Utc; }
        sealed class Fixture : IDisposable
        {
            public readonly Store Store; public readonly Clock Clock = new Clock();
            public readonly GardenStateService Service; public readonly WeekOneQuestDemo Ui;
            readonly GameObject root; readonly Scene scene; readonly Camera camera; readonly RenderTexture target;
            readonly EventSystem events; readonly GraphicRaycaster raycaster; readonly int width, height;
            public Fixture(int width, int height, Store store = null, DateTimeOffset? instant = null)
            {
                this.width = width; this.height = height; Store = store ?? new Store();
                if(instant.HasValue)Clock.Time=instant.Value;
                Service = new GardenStateService(Store, Clock, new DemoBalanceConfig { VisitorChancePercent = 0 }, new LegacySnapshot { Nutrient = 137 });
                Assert(Service.Open(), Service.LastError);
                scene = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Synthetic daily UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                root.SetActive(false); SceneManager.MoveGameObjectToScene(root, scene);
                var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(width, height);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                var cameraObject = new GameObject("Synthetic daily capture camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                target = new RenderTexture(width, height, 24); camera.targetTexture = target; camera.aspect = width / (float)height;
                canvas.worldCamera = camera; raycaster = root.GetComponent<GraphicRaycaster>();
                var eventObject = new GameObject("Synthetic pointer", typeof(EventSystem)); eventObject.transform.SetParent(root.transform, false); events = eventObject.GetComponent<EventSystem>(); events.enabled = false;
                var panel = GardenUi.Box(root.transform, "Synthetic activity panel", 0, 0, 1, 1);
                if(!Service.Snapshot.PracticePreferences.Any(item=>item.Key=="favorite"))
                    Assert(Service.SavePreference("favorite","물 마시기"),"Synthetic deterministic additional recommendation");
                Ui = panel.AddComponent<WeekOneQuestDemo>(); Ui.Initialize(Service, true);
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
                    foreach (var home in root.GetComponentsInChildren<FocusMissionHomeLayout>(false)) home.Refresh();
                    Canvas.ForceUpdateCanvases();
                }
                // Graphic depths/raycast ordering are populated by a real render.
                // Null graphics cannot establish that evidence; capture runs use an owned offscreen GPU target.
                if (capture) camera.Render();
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
            public void WithEventSystem(Action check)
            {
                var previous=EventSystem.current;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                // Edit-mode fixtures do not receive EventSystem's runtime registration.
                typeof(EventSystem).GetMethod("OnEnable",flags).Invoke(events,null);
                EventSystem.current=events;
                try{check();}
                finally
                {
                    events.SetSelectedGameObject(null);
                    typeof(EventSystem).GetMethod("OnDisable",flags).Invoke(events,null);
                    if(previous!=null)EventSystem.current=previous;
                }
            }
            public PointerEventData Down(Button button)
            {
                Layout();var point=ScreenRect((RectTransform)button.transform).center;
                var data=new PointerEventData(events){pointerId=-1,position=point,pressPosition=point,button=PointerEventData.InputButton.Left,eligibleForClick=true};
                var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);report.raycasts++;
                Assert(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Central illustration is the actual top pointer hit.");
                data.pointerPressRaycast=data.pointerCurrentRaycast=hits[0];
                ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);
                Assert(events.currentSelectedGameObject==button.gameObject,"Actual Button.OnPointerDown selects the fixture's control.");Layout();return data;
            }
            public void Select(Button button,bool pointer)
            {
                events.SetSelectedGameObject(null);
                if(button!=null)events.SetSelectedGameObject(button.gameObject,pointer?(BaseEventData)new PointerEventData(events):new BaseEventData(events));
                Layout();
            }
            public Color32[] Pixels()
            {
                Layout();camera.Render();var prior=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();return image.GetPixels32();}
                finally{RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);}
            }
            public Color32[] WithoutTargetPixels(Button button)
            {
                // Keep the actual child illustration and current press scale; isolate
                // whether the transparent parent hit graphic paints an opaque face.
                return WithoutGraphicPixels(button.targetGraphic);
            }
            public Color32[] WithoutGraphicPixels(Graphic graphic)
            {
                bool enabled=graphic.enabled;
                try{graphic.enabled=false;return Pixels();}
                finally{graphic.enabled=enabled;Layout();}
            }
            public int ChangedPixels(Color32[] before,Color32[] after,Rect area,float inset)
                => CountPixels(before,after,area,inset,false);
            public int AddedGreenPixels(Color32[] before,Color32[] after,Rect area,float inset)
                => CountPixels(before,after,area,inset,true);
            int CountPixels(Color32[] before,Color32[] after,Rect area,float inset,bool greenOnly)
            {
                int changed=0;float scale=width/390f;inset*=scale;
                for(int y=Mathf.Max(0,Mathf.CeilToInt(area.yMin+inset));y<Mathf.Min(height,Mathf.FloorToInt(area.yMax-inset));y++)
                    for(int x=Mathf.Max(0,Mathf.CeilToInt(area.xMin+inset));x<Mathf.Min(width,Mathf.FloorToInt(area.xMax-inset));x++)
                    {
                        var a=before[y*width+x];var b=after[y*width+x];
                        bool different=Math.Abs(a.r-b.r)>8||Math.Abs(a.g-b.g)>8||Math.Abs(a.b-b.b)>8;
                        if(different&&(!greenOnly||(b.g>b.r+8&&b.g>b.b+5&&a.r-b.r>30&&a.g-b.g>20)))changed++;
                    }
                return changed;
            }
            public void AssertFooter(string name)
            {
                var button = Button(name); var rect = (RectTransform)button.transform;
                Assert(rect.parent.name == "Mobile.Actions", "Main action is in fixed footer");
                var points = new Vector3[4]; rect.GetWorldCorners(points);
                foreach (var corner in points) { var p = camera.WorldToScreenPoint(corner); Assert(p.x >= 0 && p.x <= width && p.y >= 0 && p.y <= height, "Fixed footer button fully in viewport"); }
                Assert(rect.rect.height * rect.lossyScale.y >= 44, "Main action meets 44px height at actual output ratio");
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
            public Rect Bounds(RectTransform rect) => ScreenRect(rect);
            public void AssertHitWithoutReveal(Button button)
            {
                // Do not call Reveal or move a ScrollRect: every sample must be
                // reachable in the first rendered frame at the current position.
                var bounds=ScreenRect((RectTransform)button.transform);
                float x=Mathf.Min(12,bounds.width*.2f),y=Mathf.Min(12,bounds.height*.25f);
                var points=new[]{bounds.center,new Vector2(bounds.xMin+x,bounds.yMin+y),new Vector2(bounds.xMax-x,bounds.yMin+y),
                    new Vector2(bounds.xMin+x,bounds.yMax-y),new Vector2(bounds.xMax-x,bounds.yMax-y)};
                foreach(var point in points)
                {
                    var data=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
                    var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);report.raycasts++;
                    Assert(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,
                        "Unrevealed action is the top pointer target at "+point+": "+button.name+"; hit="+(hits.Count==0?"none":hits[0].gameObject.name));
                }
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
                var rects = Ui.GetComponentsInChildren<RectTransform>(false);
                var body = rects.Single(r => r.name == "Mobile.Viewport"); var footer = rects.Single(r => r.name == "Mobile.Actions");
                var notice = Text("Focus.SaveNotice"); Assert(notice != null && !string.IsNullOrEmpty(notice.text), "Storage failure notice is visible");
                var bodyBounds = ScreenRect(body); var footerBounds = ScreenRect(footer); var noticeBounds = ScreenRect(notice.rectTransform);
                Assert(bodyBounds.yMin >= noticeBounds.yMax - .5f && noticeBounds.yMin >= footerBounds.yMax - .5f,
                    "Scrollable guidance, storage error notice, and fixed footer have separate visible regions");
                AssertFullyVisible(notice.rectTransform, "Storage failure notice");
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

