using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    public static class PrototypePlaySmoke
    {
        private static IEnumerator routine;
        private static double next;
        private static readonly List<string> evidence=new List<string>();
        private static Dictionary<string,Rect> shellBounds;
        [MenuItem("Capstone Prototype/Run Play Mode Smoke")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode on Prototype first.");
            if(routine!=null)throw new InvalidOperationException("Smoke test already running.");
            var app=UnityEngine.Object.FindFirstObjectByType<PrototypeController>();
            if(app==null)throw new InvalidOperationException("Open Prototype first.");
            evidence.Clear();shellBounds=null;Directory.CreateDirectory("artifacts/prototype");
            routine=Journey(app);next=0;EditorApplication.update+=Step;
        }
        private static void Step()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            if(!EditorApplication.isPlaying){Stop();return;}
            try {
                if(!routine.MoveNext()){
                    File.WriteAllLines("artifacts/prototype/daily-play-smoke-"+Screen.width+"x"+Screen.height+".txt",evidence);
                    Debug.Log("PROTOTYPE_PLAY_SMOKE_OK daily "+evidence.Count+" checks "+Screen.width+"x"+Screen.height);Stop();
                }
                next=EditorApplication.timeSinceStartup+.12;
            }catch(Exception e){
                evidence.Add("FAIL "+e);
                File.WriteAllLines("artifacts/prototype/daily-play-smoke-"+Screen.width+"x"+Screen.height+".txt",evidence);
                Debug.LogException(e);Stop();
            }
        }
        private static void Stop(){EditorApplication.update-=Step;(routine as IDisposable)?.Dispose();routine=null;}
        private static IEnumerator Journey(PrototypeController app)
        {
            // Shared state, UI and change events must agree across navigation and reset.
            Click(app,"DeveloperSettings");Click(app,"Reset");Click(app,"ConfirmReset");
            yield return null;CheckStableShell(app);CheckAllAngleFraming(app);
            Click(app,"DeveloperSettings");yield return null;CheckUi(app);Capture("polish-demo-settings");yield return null;
            Click(app,"Reset");yield return null;CheckUi(app);Capture("polish-reset-confirmation");yield return null;
            Click(app,"CancelReset");yield return null;
            foreach (var step in CheckInputPipeline(app)) yield return step;
            // Repeated rapid navigation: check both immediately and after layout settles.
            for(int i=0;i<8;i++)
            {
                Click(app,"MoodTab");CheckStableShell(app);yield return null;CheckStableShell(app);
                Click(app,"HomeTab");CheckStableShell(app);yield return null;CheckStableShell(app);
            }
            CheckMissionPresentation(app,false);
            var transitions = new List<bool>();
            Action<bool> onMissionChanged = active => transitions.Add(active);
            app.MissionInProgressChanged += onMissionChanged;
            try
            {
                Click(app,"GardenAction");CheckMissionPresentation(app,true);
                app.Back();CheckMissionPresentation(app,true);
                yield return null;CheckUi(app);Capture("mission-active-home");yield return null;
                Click(app,"MoodTab");CheckMissionPresentation(app,true);
                yield return null;CheckUi(app);Capture("mission-active-history");yield return null;
                Click(app,"HistoryAction");CheckMissionPresentation(app,true);
                Check(transitions.SequenceEqual(new[]{true}),"Navigation/resuming emits no duplicate mission event");
                Click(app,"MissionAction");CheckMissionPresentation(app,false);
                Check(transitions.SequenceEqual(new[]{true,false}),"Completion ends mission state before recording");
                Click(app,"QuietChoice");Click(app,"ConfirmCheckIn");CheckMissionPresentation(app,false);
                Click(app,"ConfirmGrowth");
                Check(transitions.SequenceEqual(new[]{true,false}),"Recording and growth do not reactivate mission state");
                Click(app,"DeveloperSettings");Click(app,"NextDay");
                Click(app,"GardenAction");CheckMissionPresentation(app,true);
                Click(app,"DeveloperSettings");Click(app,"NextDay");CheckMissionPresentation(app,false);
                Click(app,"GardenAction");CheckMissionPresentation(app,true);
                Click(app,"DeveloperSettings");Click(app,"Reset");Click(app,"ConfirmReset");CheckMissionPresentation(app,false);
                Check(transitions.SequenceEqual(new[]{true,false,true,false,true,false}),"Date rollover and session reset notify external subscribers");
            }
            finally { app.MissionInProgressChanged -= onMissionChanged; }
            foreach(string route in new[]{"QTQ","QTT"})
            {
                Click(app,"DeveloperSettings");Click(app,"Reset");Click(app,"ConfirmReset");
                Check(app.Session.Stage==0&&app.plantView.seed.activeSelf,"Fresh seed, no restoration");
                yield return null;CheckUi(app);CheckPlant(app);Capture("garden-seed");yield return null;
                Click(app,"MoodTab");
                yield return null;CheckHistory(app,0);CheckUi(app);Capture("history-empty");yield return null;
                Click(app,"HistoryAction");
                Check(app.CurrentPage==PrototypePage.Mission,"Empty history starts with the mission, not reflection");
                app.Back();
                for(int day=0;day<3;day++)
                {
                    if(day>0){
                        var previous=app.Session.Day;Click(app,"DeveloperSettings");Click(app,"NextDay");
                        Check(app.Session.Day==previous.AddDays(1),"Demo uses a new local day, without changing system clock");
                        Check(app.Session.TodayChoice==PlantChoice.None&&app.Session.TodayMood==Mood.None&&app.Session.TodayNote=="","Daily selections and note reset");
                    }
                    Click(app,"GardenAction");
                    Check(app.CurrentPage==PrototypePage.Mission&&app.Session.MissionStarted,"Main opens mission before emotion entry");
                    app.OpenCheckIn();app.ConfirmCheckIn();
                    Check(app.CurrentPage==PrototypePage.Mission&&app.Session.Stage==day,"Premature reflection cannot bypass mission");
                    yield return null;CheckUi(app);if(day==0){Capture("single-mission");yield return null;}
                    Click(app,"MissionAction");
                    Check(app.Session.MissionCompleted&&app.Session.AwaitingMoodRecord,"Mission completion unlocks reflection");
                    Check(app.Session.Stage==day&&app.Session.Completions.Count==day&&!app.growthPopup.activeSelf,"Mission alone awards no growth or history");
                    Check(app.CurrentPage==PrototypePage.CheckIn,"Daily emotion and branch share a page");
                    Check(!app.checkInAction.interactable,"Branch choice required, mood optional");
                    Click(app,"Mood_"+(day+1));Click(app,route[day]=='Q'?"QuietChoice":"TogetherChoice");
                    Check(app.Session.Stage==day,"Draft choices award no growth");
                    if(day==1)Click(app,"Mood_"+(day+1));
                    Check(day!=1||app.Session.TodayMood==Mood.None,"Tapping the selected emotion clears it without a skip button");
                    string note=day==0?"오늘은 조금 지쳤지만, 내 속도로 해보고 싶어요.\n잠깐 쉬니 한결 차분해요."
                        :day==1?"":string.Concat(Enumerable.Repeat("오늘의 작은 마음을 천천히 돌아봐요. ",14));
                    app.moodNoteInput.text=note;
                    string draft=app.Session.TodayNote;
                    Check(draft==note.Substring(0,Math.Min(note.Length,PrototypeSession.NoteCharacterLimit)),"Input event updates bounded note draft");
                    Check(app.noteCounter.text==draft.Length+" / 200","Character counter follows draft");
                    Click(app,"MoodTab");yield return null;
                    CheckHistory(app,day);
                    Click(app,"HistoryAction");
                    Check(app.CurrentPage==PrototypePage.CheckIn,"History preserves unfinished daily draft");
                    Check(app.Session.TodayChoice==(route[day]=='Q'?PlantChoice.Quiet:PlantChoice.Together),"History navigation keeps branch draft");
                    Check(app.Session.TodayNote==draft&&app.moodNoteInput.text==draft,"History navigation keeps note draft");
                    Click(app,"HomeTab");
                    Check(app.gardenActionLabel.text=="마음 기록 이어하기"&&app.Session.Stage==day,"Home offers pending reflection without growth");
                    Click(app,"GardenAction");
                    Check(app.CurrentPage==PrototypePage.CheckIn&&app.moodNoteInput.text==draft,"Home resumes reflection without repeating mission");
                    app.MissionAction();
                    Check(app.Session.Stage==day&&app.CurrentPage==PrototypePage.CheckIn,"Stale mission callback cannot skip recording");
                    yield return null;CheckUi(app);if(day==0){Capture("mood-and-branch");yield return null;}
                    Click(app,"ConfirmCheckIn");
                    Check(app.Session.RecordCompleted&&app.Session.Stage==day+1&&app.Session.Completions.Count==day+1,"Recording atomically captures answers and grows");
                    Check(app.Session.Completions[day].Note==draft.Trim(),"Completion freezes the note with the emotion");
                    Check(app.growthPopup.activeSelf&&!app.screenRoot.activeSelf,"Growth popup shown only after recording");
                    app.ConfirmCheckIn();app.MissionAction();Check(app.Session.Stage==day+1,"Duplicate input cannot grow twice");
                    Check(app.plantView.bud.gameObject.activeSelf==(day==1),"Bud appears at visible stage three");
                    Check(app.plantView.blossomAnchor.gameObject.activeSelf==(day==2),"Bloom appears at visible stage four");
                    yield return null;CheckUi(app);CheckPlant(app);Capture("growth-"+(day+1)+"-"+route);yield return null;
                    Click(app,"ConfirmGrowth");
                    Check(app.CurrentPage==PrototypePage.Garden&&app.screenRoot.activeSelf,"Popup closes to main");
                    Click(app,"MoodTab");
                    yield return null;CheckHistory(app,day+1);CheckUi(app);
                    if(day==0){Capture("history-one");yield return null;}
                    if(day==2)
                    {
                        Capture("history-three");yield return null;
                        var scroll=app.moodHistory.scroll;
                        float before=scroll.content.anchoredPosition.y;
                        bool overflow=scroll.content.rect.height>scroll.viewport.rect.height;
                        scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-12)});
                        yield return null;
                        Check(overflow ? scroll.content.anchoredPosition.y>before : Mathf.Abs(scroll.content.anchoredPosition.y-before)<1,
                            "Scroll moves overflowing list; short list stays clamped");
                        scroll.verticalNormalizedPosition=0;yield return null;
                        var oldest=scroll.content.GetComponentsInChildren<PrototypeMoodHistoryRow>().Last();
                        var corners=new Vector3[4];((RectTransform)oldest.transform).GetWorldCorners(corners);
                        float bottom=scroll.viewport.InverseTransformPoint(corners[0]).y;
                        Check(bottom>=scroll.viewport.rect.yMin-1,"Last record is fully reachable");
                        Capture("history-bottom");yield return null;
                    }
                    Click(app,"HomeTab");Click(app,"MoodTab");yield return null;
                    CheckHistory(app,day+1);
                    Check(Mathf.Abs(app.moodHistory.scroll.content.anchoredPosition.y)<1,"Reopening history returns to newest record");
                    Click(app,"HistoryAction");
                    Check(app.CurrentPage==PrototypePage.Garden,"Completed history CTA returns to garden");
                    app.OpenCheckIn();
                    Check(!app.moodButtons[0].interactable&&!app.quietButton.interactable,"Confirmed daily answers are read-only");
                    Check(app.moodNoteInput.readOnly,"Completed note is read-only");
                    Click(app,"ConfirmCheckIn");
                    Check(app.gardenActionLabel.text=="미션 완료"&&app.gardenButton.interactable,"Completed mission has a clickable notice button");
                    Click(app,"GardenAction");
                    Check(app.missionCompletedPopup.activeSelf&&!app.growthPopup.activeSelf&&!app.screenRoot.activeSelf,
                        "Same-day CTA opens only the mission-completed notice");
                    Check(app.missionCompletedPopup.GetComponentInChildren<TMP_Text>().text=="오늘의 미션을 완료했습니다!","Exact mission completion copy");
                    app.GardenAction();app.MissionAction();app.ConfirmCheckIn();app.ShowMoodHistory();
                    Check(app.missionCompletedPopup.activeSelf&&app.Session.Stage==day+1&&app.Session.Completions.Count==day+1,
                        "Repeated callbacks during notice cannot start a mission, navigate or grow again");
                    yield return null;CheckUi(app);if(day==0){Capture("mission-completed-notice");yield return null;}
                    Click(app,"ConfirmMissionCompleted");
                    Check(app.screenRoot.activeSelf&&!app.missionCompletedPopup.activeSelf&&app.CurrentPage==PrototypePage.Garden,
                        "Notice confirmation returns to home");
                    yield return null;CheckUi(app);if(day==0){Capture("mission-completed-home");yield return null;}
                    Click(app,"GardenAction");
                    app.Back();
                    Check(!app.missionCompletedPopup.activeSelf&&app.screenRoot.activeSelf,"Back also closes completion notice");
                }
                string id=route=="QTQ"?"chamomile":"hydrangea";
                Check(app.Session.IsComplete&&app.Session.FinalPlantId==id,"Three-answer majority selects: "+id);
                Check(app.plantView.chamomileFlower.activeSelf==(id=="chamomile"),"Chamomile visibility");
                Check(app.plantView.hydrangeaFlower.activeSelf==(id=="hydrangea"),"Hydrangea visibility");
                yield return null;CheckUi(app);Capture("final-"+id);yield return null;
                foreach (var step in CheckOrbit(app, id)) yield return step;
                Click(app,"DeveloperSettings");Click(app,"NextDay");Click(app,"GardenAction");
                Check(app.Session.Stage==3&&app.growthPopup.activeSelf,"Final growth cap persists across midnight in this run");
                app.Back();
                Click(app,"MoodTab");yield return null;CheckHistory(app,3);CheckUi(app);
                Check(app.Session.Stage==3,"Reviewing history after final day never changes growth");
                app.Back();
            }
            // A completed action is not lost if the player reflects after midnight.
            Click(app,"DeveloperSettings");Click(app,"Reset");Click(app,"ConfirmReset");
            Click(app,"GardenAction");Click(app,"MissionAction");
            var pendingDay=app.Session.Day;
            Click(app,"Mood_2");Click(app,"QuietChoice");app.moodNoteInput.text="늦은 밤의 작은 실천";
            Click(app,"DeveloperSettings");Click(app,"NextDay");
            Check(app.Session.Day==pendingDay&&app.Session.AwaitingMoodRecord,"Date change preserves completed action awaiting reflection");
            Click(app,"GardenAction");
            Check(app.moodNoteInput.text=="늦은 밤의 작은 실천","Pending note survives midnight");
            Click(app,"ConfirmCheckIn");
            yield return null;
            // Wait beyond the controller date-check interval while the popup is open.
            for(int i=0;i<12;i++)yield return null;
            Check(app.growthPopup.activeSelf&&app.Session.Stage==1,"Midnight does not auto-dismiss growth popup");
            Check(app.Session.Completions[0].Day==pendingDay,"Late reflection retains the action date");
            Click(app,"ConfirmGrowth");
            Check(app.Session.Day==pendingDay.AddDays(1)&&!app.Session.MissionCompleted,"Closing result unlocks the new day");
            Click(app,"GardenAction");
            Check(app.CurrentPage==PrototypePage.Mission,"New day starts with its own mission");
            app.Back();
            Click(app,"DeveloperSettings");Click(app,"Reset");Click(app,"ConfirmReset");
            Click(app,"MoodTab");yield return null;CheckHistory(app,0);app.Back();
        }
        private static IEnumerable CheckInputPipeline(PrototypeController app)
        {
            // Real InputSystem -> UIInputModule -> raycast -> drag handlers, not direct handler calls.
            // A temporary virtual device keeps this test independent of OS mouse injection/coalescing.
            var image = app.pages[0].transform.Find("MainPlant").GetComponent<RawImage>();
            var drag = image.GetComponent<PrototypeIslandDrag>();
            var rect = image.rectTransform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center + new Vector2(110,60)));
            Mouse mouse = InputSystem.AddDevice<Mouse>("PrototypeOrbitSmoke");
            var touch = InputSystem.AddDevice<Touchscreen>("PrototypeOrbitTouchSmoke");
            var actions = EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset;
            var previousDevices = actions.devices;
            try
            {
                // Keep live OS mouse/pen events out of this short integration test.
                // Restore the original action device filter even on failure/cancellation.
                actions.devices = new InputDevice[] { mouse, touch };
                foreach (var frame in InputFrames()) yield return frame;
                drag.ResetView();
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); foreach (var frame in InputFrames()) yield return frame;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 }); foreach (var frame in InputFrames()) yield return frame;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end, buttons = 1 }); foreach (var frame in InputFrames()) yield return frame;
                Check(drag.IsDragging && Vector2.Distance(drag.Angles, drag.defaultAngles) > 20,
                    "InputSystem mouse press/move reaches island through actual UI bindings");
                CheckFraming(app);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = end }); foreach (var frame in InputFrames()) yield return frame;
                Check(!drag.IsDragging, "InputSystem mouse release ends the gesture");
                var before = drag.Angles;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); foreach (var frame in InputFrames()) yield return frame;
                Check(drag.Angles == before, "Mouse movement without held button cannot rotate island");
                Capture("input-mouse-orbit"); yield return null;
                drag.ResetView();
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = point }); foreach (var frame in InputFrames()) yield return frame;
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = end }); foreach (var frame in InputFrames()) yield return frame;
                Check(drag.IsDragging && Vector2.Distance(drag.Angles, drag.defaultAngles) > 20,
                    "InputSystem single-finger movement reaches island through actual UI bindings");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = end }); foreach (var frame in InputFrames()) yield return frame;
                Check(!drag.IsDragging, $"Touch release ends the gesture (press={touch.primaryTouch.press.isPressed}, phase={touch.primaryTouch.phase.ReadValue()}, focused={Application.isFocused})");
                Capture("input-touch-orbit"); yield return null;
            }
            finally
            {
                actions.devices = previousDevices;
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(touch); drag.ResetView();
            }
        }
        private static IEnumerable InputFrames()
        {
            // EditorApplication.update is not a PlayerLoop frame. Give both the input update
            // and EventSystem.Update time to consume each state, with a bounded failure.
            int first = Time.frameCount;
            double deadline = EditorApplication.timeSinceStartup + 5;
            do
            {
                yield return null;
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Input smoke requires a running, unpaused Game view.");
            } while (Time.frameCount < first + 2);
        }
        private static void CheckPlant(PrototypeController app)
        {
            var plant = app.plantView;
            var stages = new[] { plant.seed, plant.sprout, plant.bud.gameObject, plant.blossomAnchor.gameObject };
            Check(stages.Count(go => go.activeSelf) == 1 && stages[app.Session.Stage].activeSelf, "Exactly one of four authored stages is visible");
            Check(app.progressDots.Count(dot => dot.color == new Color(.31f,.47f,.37f)) == app.Session.Stage + 1, "Progress includes seed as stage one");
            CheckFraming(app);
        }
        private static void CheckAllAngleFraming(PrototypeController app)
        {
            var drag = app.pages[0].transform.Find("MainPlant").GetComponent<PrototypeIslandDrag>();
            var camera = drag.previewCamera;
            float radius = 0, horizontalRadius = 0;
            int vertices = 0;
            // A sphere about the orbit focus contains every stage, including inactive flowers.
            // Fitting that sphere protects all yaw/pitch combinations, not just sampled drags.
            foreach (var filter in app.plantView.transform.parent.GetComponentsInChildren<MeshFilter>(true))
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var delta = filter.transform.TransformPoint(vertex) - drag.focus;
                    radius = Mathf.Max(radius, delta.magnitude);
                    horizontalRadius = Mathf.Max(horizontalRadius, new Vector2(delta.x, delta.z).magnitude);
                    vertices++;
                }
            Check(camera.orthographic && vertices > 0, "Orthographic garden has authored geometry");
            Check(radius < camera.orthographicSize * .97f && horizontalRadius < camera.orthographicSize * camera.aspect * .97f,
                $"All stages/species fit every orbit angle: vertical={radius:F3}, horizontal={horizontalRadius:F3}, size={camera.orthographicSize:F3}, aspect={camera.aspect:F3}");
            Check(drag.distance - radius > camera.nearClipPlane && drag.distance + radius < camera.farClipPlane,
                "Closer orbit keeps all stages inside camera depth planes");
            var rect = (RectTransform)drag.transform;
            float gain = (rect.rect.height / (2 * camera.orthographicSize)) / (540 / (2 * 1.7f));
            Check(rect.rect.width > 600 && rect.rect.height > 540, $"UI preview itself is expanded: {rect.rect.size}");
            Check(gain > (app.portraitLayout.designFrame.rect.height >= 1500 ? 1.35f : 1.04f),
                $"Expanded home UI makes the actual plant larger: {gain:F3}x previous full-island view");
        }
        private static void CheckFraming(PrototypeController app)
        {
            Canvas.ForceUpdateCanvases();
            var drag = app.pages[0].transform.Find("MainPlant").GetComponent<PrototypeIslandDrag>();
            var camera = drag.previewCamera;
            var min = Vector2.one * float.MaxValue; var max = Vector2.one * float.MinValue;
            int vertices = 0, renderers = 0;
            foreach (var filter in app.plantView.transform.parent.GetComponentsInChildren<MeshFilter>())
            {
                renderers++;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point); vertices++;
                }
            }
            Check(vertices > 0 && min.x > .015f && min.y > .015f && max.x < .985f && max.y < .985f,
                $"Entire garden stays in preview: {min}..{max}");
            Check(vertices / 3 <= 12000 && renderers <= 32, "Active garden remains within 12k triangles / 32 renderers");
        }
        private static IEnumerable CheckOrbit(PrototypeController app, string species)
        {
            var image = app.pages[0].transform.Find("MainPlant").GetComponent<RawImage>();
            var drag = image.GetComponent<PrototypeIslandDrag>();
            var rect = image.rectTransform;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 size = corners[2] - corners[0];
            Vector2 center = (corners[0] + corners[2]) * .5f;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, hits);
            Check(hits.Count > 0 && hits[0].gameObject == image.gameObject, "Home island is reachable through UI raycast");
            int stage = app.Session.Stage, records = app.Session.Completions.Count;
            foreach (var sample in new[] { new Vector2(.25f,0), new Vector2(.5f,0), new Vector2(.75f,0), new Vector2(0,.5f), new Vector2(0,-.65f) })
            {
                drag.ResetView();
                var pointer = new PointerEventData(EventSystem.current) { pointerId = 42, button = PointerEventData.InputButton.Left, pressPosition = center, position = center };
                ExecuteEvents.Execute(image.gameObject, pointer, ExecuteEvents.beginDragHandler);
                pointer.position = center + Vector2.Scale(size, sample);
                ExecuteEvents.Execute(image.gameObject, pointer, ExecuteEvents.dragHandler);
                Check(drag.IsDragging && drag.Angles != drag.defaultAngles, "Drag updates home orbit");
                ExecuteEvents.Execute(image.gameObject, pointer, ExecuteEvents.endDragHandler);
                Check(!drag.IsDragging, "Release stops rotation");
                yield return null; CheckFraming(app);
                Capture("orbit-" + species + "-" + sample.x + "-" + sample.y); yield return null;
            }
            var angles = drag.Angles; var homeRotation = drag.previewCamera.transform.rotation;
            Click(app,"MoodTab"); yield return null;
            Check(!drag.IsDragging && Quaternion.Angle(drag.previewCamera.transform.rotation, Quaternion.Euler(drag.defaultAngles.y,drag.defaultAngles.x,0)) < .01f,
                "History navigation cancels input and restores composed preview");
            app.moodHistory.scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0,-4) });
            Check(drag.Angles == angles, "Record scrolling cannot rotate island");
            Click(app,"HomeTab"); yield return null;
            Check(Quaternion.Angle(drag.previewCamera.transform.rotation,homeRotation)<.01f, "Returning home restores orbit without resuming drag");
            Click(app,"GardenAction"); yield return null;
            Check(Quaternion.Angle(drag.previewCamera.transform.rotation, Quaternion.Euler(drag.defaultAngles.y,drag.defaultAngles.x,0))<.01f, "Completion notice suspends home orbit");
            Click(app,"ConfirmMissionCompleted"); yield return null;
            Check(Quaternion.Angle(drag.previewCamera.transform.rotation,homeRotation)<.01f,"Closing notice restores the previous home orbit");
            Check(app.Session.Stage == stage && app.Session.Completions.Count == records, "Orbit never changes growth, votes or records");
            drag.ResetView();
        }
        private static void CheckHistory(PrototypeController app,int expected)
        {
            Canvas.ForceUpdateCanvases();
            var view=app.moodHistory;
            Check(app.CurrentPage==PrototypePage.MoodHistory&&view.gameObject.activeInHierarchy,"Mind history page is active");
            Check(app.historyTabLabel.text=="마음 기록","Navigation renamed to mind history");
            Check(view.VisibleCount==expected&&view.countLabel.text=="기록 "+expected+"개","History count tracks completed days only");
            Check(view.emptyState.activeSelf==(expected==0)&&view.scroll.gameObject.activeSelf==(expected>0),"Empty/list state toggles correctly");
            var rows=view.scroll.content.GetComponentsInChildren<PrototypeMoodHistoryRow>();
            Check(rows.Length==expected,"No duplicate or stale rows");
            for(int i=0;i<rows.Length;i++)
            {
                var record=app.Session.Completions[expected-1-i];var row=rows[i];
                Check(row.Day==record.Day&&row.RecordedMood==record.Mood,"Newest-first immutable completion binding");
                Check(row.moodLabel.text==(record.Mood==Mood.None?"감정 미선택":PrototypeDefinition.MoodLabels[(int)record.Mood]),"Mood label distinguishes skipped emotion");
                Check(row.face.gameObject.activeSelf==(record.Mood!=Mood.None)&&row.skippedIcon.activeSelf==(record.Mood==Mood.None),"Skipped emotion does not display a happy face");
                Check(Mathf.Abs(((RectTransform)row.transform).rect.width-view.scroll.viewport.rect.width)<1,"Card width follows viewport");
                Check(row.noteLabel.text==record.Note&&!row.noteLabel.richText,"History displays the literal note, not mission information");
                Check(row.noteLabel.gameObject.activeSelf==!string.IsNullOrEmpty(record.Note),"Empty notes have no placeholder or mission fallback");
                if(row.noteLabel.gameObject.activeSelf)
                {
                    row.noteLabel.ForceMeshUpdate();
                    Check(row.noteLabel.textBounds.size.y<=row.noteLabel.rectTransform.rect.height+2,"Wrapped note fits its dynamic body");
                    Check(((RectTransform)row.transform).rect.height>=row.noteTop+row.noteLabel.rectTransform.rect.height+row.noteBottomPadding-1,"Card grows around entire note");
                }
            }
            var frame=app.portraitLayout.designFrame;var points=new Vector3[4];frame.GetWorldCorners(points);
            float frameHeight=points[1].y-points[0].y;
            Check(Mathf.Abs(frameHeight-Screen.safeArea.height)<2,"History uses full safe-area height");
        }
        private static Button Find(PrototypeController app,string name)=>app.gameObject.scene.GetRootGameObjects()
            .SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Single(b=>b.name==name);
        private static void Click(PrototypeController app,string name)
        {
            var b=Find(app,name);Check(b.gameObject.activeInHierarchy&&b.interactable,"Enabled button: "+name);
            b.onClick.Invoke();
        }
        private static void CheckUi(PrototypeController app)
        {
            CheckMissionPresentation(app,app.Session.IsMissionInProgress);
            CheckStableShell(app);
            Canvas.ForceUpdateCanvases();int guides=0;
            foreach (var preview in app.portraitLayout.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true))
            {
                if (!preview.isActiveAndEnabled) continue;
                Rect bounds = ScreenBounds((RectTransform)preview.transform);
                Rect upper = ScreenBounds(preview.upperBoundary), lower = ScreenBounds(preview.lowerBoundary);
                var camera = preview.orbit.previewCamera;
                Check(bounds.xMin >= Screen.safeArea.xMin - 1 && bounds.xMax <= Screen.safeArea.xMax + 1
                    && bounds.yMax < upper.yMin && bounds.yMin > lower.yMax,
                    "Expanded preview stays inside safe area and between guide/footer: " + preview.name);
                Check(Mathf.Abs(camera.aspect - bounds.width / bounds.height) < .001f,
                    "Shared camera matches visible UI aspect without stretching: " + preview.name);
                CheckFraming(app);
            }
            var selection=app.selectionView;
            bool history=app.CurrentPage==PrototypePage.MoodHistory;
            Check(selection!=null && selection.homeSurface.topColor==(history?Color.clear:selection.selectedFill)
                && selection.historySurface.topColor==(history?selection.selectedFill:Color.clear),"Selected tab has an explicit background and icon treatment");
            for(int i=0;i<5;i++)
                Check(selection.moodChecks[i].activeSelf==((int)app.Session.TodayMood==i+1),"Emotion selection check matches domain state");
            Check(selection.quietCheck.gameObject.activeSelf==(app.Session.TodayChoice==PlantChoice.Quiet)
                && selection.togetherCheck.gameObject.activeSelf==(app.Session.TodayChoice==PlantChoice.Together),"Branch checkmarks match the selected choice");
            foreach(var t in app.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMP_Text>(true)))
            {
                if(!t.gameObject.activeInHierarchy)continue;t.ForceMeshUpdate();
                if(t.name=="Guide")guides++;
                Check(t.fontSize>=26&&!t.enableAutoSizing,"Readable minimum type without automatic shrinking: "+t.name);
                if(t.name=="RecordedNote"||t.name=="NoteInputText")Check(t.fontSize>=30,"Large note typography: "+t.name);
                bool body=t.name=="RecordedNote"||t.name=="NoteInputText";
                if(!body)Check(!t.text.Contains("\n")&&t.textInfo.lineCount<=1,"Single line: "+t.name);
                if(t.name!="NoteInputText")Check(t.textBounds.size.y<=t.rectTransform.rect.height+3&&t.textBounds.size.x<=t.rectTransform.rect.width+3,
                    "Text fits: "+t.transform.parent.name+"/"+t.name+" "+t.textBounds.size+" in "+t.rectTransform.rect.size);
                else Check(t.GetComponentInParent<RectMask2D>()!=null&&!t.richText,"Multiline editor is masked and plain text");
            }
            Check(guides<=1,"One explanatory line per screen");
            foreach(var icon in app.portraitLayout.GetComponentsInChildren<PrototypeUiIcon>(true))
                Check(!icon.raycastTarget,"Decorative icon never intercepts input: "+icon.name);
            if(app.CurrentPage==PrototypePage.CheckIn&&app.screenRoot.activeSelf)
            {
                Check(!app.pages[(int)PrototypePage.CheckIn].GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="오늘의 미션"||t.text==app.definition.missionTitle),"No mission preview in emotion screen");
                var input=app.moodNoteInput;var inputRect=(RectTransform)input.transform;
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,inputRect.TransformPoint(inputRect.rect.center));
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==input.gameObject,"Note input receives pointer events");
            }
            foreach(var b in app.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true)))
            {
                if(!b.gameObject.activeInHierarchy||!b.interactable)continue;
                var rect=(RectTransform)b.transform;
                Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                Rect bounds=ScreenBounds(rect);
                Check(bounds.xMin>=Screen.safeArea.xMin-1&&bounds.yMin>=Screen.safeArea.yMin-1
                    &&bounds.xMax<=Screen.safeArea.xMax+1&&bounds.yMax<=Screen.safeArea.yMax+1,"Full button in safe area: "+b.name);
                var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
                Check(hits.Count>0&&(hits[0].gameObject==b.gameObject||hits[0].gameObject.transform.IsChildOf(b.transform)),"Button hit target: "+b.name);
            }
        }
        private static Rect ScreenBounds(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
        }
        private static void CheckStableShell(PrototypeController app)
        {
            Canvas.ForceUpdateCanvases();
            var content=app.screenRoot.transform;
            var current=new Dictionary<string,Rect>();
            current.Add("Frame",ScreenBounds(app.portraitLayout.designFrame));
            foreach(string name in new[]{"Brand","Date","BottomNavigation","HomeTab","MoodTab"})
                current.Add(name,ScreenBounds((RectTransform)content.Find(name)));
            // Inactive elements still have geometry, so opening a modal must not move the shell.
            if(shellBounds==null)shellBounds=current;
            foreach(var pair in current)
            {
                Rect prior=shellBounds[pair.Key];Rect now=pair.Value;
                Check(Vector2.Distance(prior.position,now.position)<.5f&&Vector2.Distance(prior.size,now.size)<.5f,
                    "Stable across tabs/popups: "+pair.Key);
            }
            Check(Mathf.Abs(current["Frame"].height-Screen.safeArea.height)<2,"Every page uses the full safe-area height");
            if(!app.screenRoot.activeSelf)return;
            string[] actions={"GardenAction","ConfirmCheckIn","MissionAction","HistoryAction"};
            var page=app.pages[(int)app.CurrentPage].transform;
            Rect action=ScreenBounds((RectTransform)page.Find(actions[(int)app.CurrentPage]));
            Rect nav=current["BottomNavigation"];
            Check(action.yMin>nav.yMax,"Primary action never overlaps navigation");
            if(!shellBounds.ContainsKey("PrimaryAction"))shellBounds.Add("PrimaryAction",action);
            Check(Vector2.Distance(shellBounds["PrimaryAction"].position,action.position)<.5f,
                "Primary action stays at same position across pages");
            Rect title=ScreenBounds((RectTransform)page.Find("Title"));
            if(!shellBounds.ContainsKey("PageTitle"))shellBounds.Add("PageTitle",title);
            Check(Vector2.Distance(shellBounds["PageTitle"].position,title.position)<.5f,"Page title does not jump between tabs");
            if(app.CurrentPage==PrototypePage.MoodHistory)
            {
                Rect viewport=ScreenBounds(app.moodHistory.scroll.viewport);
                Rect count=ScreenBounds(app.moodHistory.countLabel.rectTransform);
                Check(viewport.yMin>action.yMax&&viewport.yMax<=count.yMin+1,"History viewport stays between count and action");
            }
            if(app.CurrentPage==PrototypePage.Garden)
            {
                Rect card=ScreenBounds((RectTransform)page.Find("TodayMissionCard"));
                Rect dots=ScreenBounds(app.progressDots[0].rectTransform);
                Check(card.yMin>action.yMax&&dots.yMin>card.yMax,"Plant indicators, mission card and action stay separated");
            }
            if(app.CurrentPage==PrototypePage.CheckIn)
            {
                Rect note=ScreenBounds((RectTransform)app.moodNoteInput.transform);
                Rect question=ScreenBounds(app.questionText.rectTransform);
                Rect lastChoice=ScreenBounds((RectTransform)app.togetherButton.transform);
                Check(note.yMin>question.yMax&&lastChoice.yMin>action.yMax,"Note, question and action never overlap");
            }
        }
        private static void CheckMissionPresentation(PrototypeController app,bool expected)
        {
            var view=app.missionPresentation;
            Check(app.IsMissionInProgress==expected&&app.Session.IsMissionInProgress==expected,"Shared mission flag matches expected state");
            Color background=expected?view.missionBackground:view.idleBackground;
            Check(view.background.topColor==background&&view.background.bottomColor==background,"Background follows mission state only");
            Check(view.header.text==(expected?"미션 진행 중":"마음 정원"),"Header visibly identifies an active mission");
            Check(view.backgroundLabels.All(t=>expected?t.color==view.missionForeground:t.color!=view.missionForeground),"Backdrop text contrast is applied and restored");
            if(expected)Check(app.missionStatus.text.StartsWith("미션 진행 중")&&app.dailyStatus.text=="미션 진행 중","Mission and home show ongoing status");
            foreach(var button in view.primaryButtons)
            {
                var surface=(PrototypeUiSurface)button.targetGraphic;
                bool completed=app.Session.RecordCompleted&&!expected&&button==view.completedMissionButton;
                Check(surface.topColor==(completed?view.completedButtonFill:expected?view.activeButtonFill:view.idleButtonFill)
                    &&button.GetComponentInChildren<TMP_Text>(true).color==(completed?view.completedButtonInk:expected?view.activeButtonInk:Color.white),
                    "Primary button contrast follows mission background: "+button.name);
            }
            Check(app.gardenActionLabel.text==(app.Session.RecordCompleted?"미션 완료":app.Session.IsComplete?"꽃 다시 보기"
                :app.Session.AwaitingMoodRecord?"마음 기록 이어하기":app.Session.MissionStarted?"미션 이어하기":"오늘의 미션"),
                "Home CTA matches daily completion, pending reflection and new-day state");
        }
        private static void Capture(string name)=>ScreenCapture.CaptureScreenshot("artifacts/prototype/daily-"+name+"-"+Screen.width+"x"+Screen.height+".png");
        private static void Check(bool ok,string detail){if(!ok)throw new InvalidOperationException("Daily smoke: "+detail);evidence.Add("PASS "+detail);}
    }
}
