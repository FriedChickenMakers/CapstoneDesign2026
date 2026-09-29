using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    public static class PrototypeValidation
    {
        [MenuItem("Capstone Prototype/Run Edit Mode Tests")]
        public static void RunTests()
        {
            var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new TestResults());
            runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "Capstone.Prototype.Tests" } }));
        }

        private sealed class TestResults : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory("artifacts/prototype");
                TestRunnerApi.SaveResultToFile(result, "artifacts/prototype/editmode-results.xml");
                Debug.Log($"PROTOTYPE_TESTS {result.TestStatus}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
            }
        }

        [MenuItem("Capstone Prototype/Validate Scene")]
        public static void ValidateScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != PrototypeSceneBuilder.ScenePath) throw new InvalidOperationException("Open Prototype.unity first.");
            var app = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PrototypeController>(true)).SingleOrDefault();
            if (app == null || app.definition == null || app.plantView == null || app.pages.Length != 4)
                throw new InvalidOperationException($"Prototype scene references are incomplete: app={app}, definition={app?.definition}, plant={app?.plantView}, pages={app?.pages?.Length}.");
            app.definition.ValidateDefinition();
            var previews = app.portraitLayout.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true);
            if (previews.Length != 3 || previews.Any(p => p.upperBoundary == null || p.lowerBoundary == null
                || p.orbit == null || p.gardenRoot == null))
                throw new InvalidOperationException("Apply Expand Plant UI Area: three responsive garden previews must be wired.");
            if (app.portraitLayout.GetComponentsInChildren<PrototypeUiSurface>(true)
                .Any(surface => surface.name.EndsWith("_GroundGlow") && surface.gameObject.activeSelf))
                throw new InvalidOperationException("Decorative garden ovals must stay hidden on home, mission and growth previews.");
            foreach (var page in app.pages)
            {
                var rect = (RectTransform)page.transform;
                if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one
                    || rect.offsetMin != new Vector2(0,130) || rect.offsetMax != new Vector2(0,-106))
                    throw new InvalidOperationException("Apply Responsive Layout: all pages must share the safe-area shell.");
            }
            foreach (string name in new[] { "HomeTab", "MoodTab" })
            {
                var rect = (RectTransform)app.screenRoot.transform.Find(name);
                if (rect.anchorMin.y != 0 || rect.anchorMax.y != 0 || rect.sizeDelta.y < 88)
                    throw new InvalidOperationException("Bottom tabs must have fixed anchors and sufficient hit height.");
            }
            var missionView = app.missionPresentation;
            if (missionView == null || missionView.background == null || missionView.header == null
                || missionView.backgroundLabels == null || missionView.backgroundLabels.Length != 12
                || missionView.backgroundLabels.Any(label => label == null)
                || missionView.background.raycastTarget || missionView.background.color != Color.white
                || missionView.missionBackground.grayscale >= missionView.idleBackground.grayscale)
                throw new InvalidOperationException("Apply Mission State UI: background/foreground bindings are incomplete.");
            foreach (var field in typeof(PrototypeController).GetFields())
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && field.GetValue(app) == null)
                    throw new InvalidOperationException("Missing controller reference: " + field.Name);
            int buttons = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        throw new InvalidOperationException("Missing script on " + t.name);
                foreach (var b in root.GetComponentsInChildren<Button>(true))
                {
                    buttons++;
                    if (b.onClick.GetPersistentEventCount() != 1 || b.onClick.GetPersistentTarget(0) == null)
                        throw new InvalidOperationException("Unbound button: " + b.name);
                }
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                    if (t.font == null) throw new InvalidOperationException("Missing font: " + t.name);
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.canvasRenderer == null) throw new InvalidOperationException("Missing CanvasRenderer: " + graphic.name);
            }
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null) throw new InvalidOperationException("Missing EventSystem.");
            if (app.moodButtons.Length != 5 || app.moodFaces.Length != 5 || app.progressDots.Length != 4)
                throw new InvalidOperationException("Expected five moods and four growth indicators.");
            if(app.selectionView==null||app.selectionView.moodTiles.Length!=5||app.selectionView.moodChecks.Length!=5)
                throw new InvalidOperationException("Apply Readable UI Polish to bind explicit selection presentation.");
            foreach(var text in app.portraitLayout.GetComponentsInChildren<TMP_Text>(true))
                if(text.fontSize<26||text.enableAutoSizing)
                    throw new InvalidOperationException("Polished UI must keep readable type without shrinking: "+text.name);
            if (missionView.completedMissionButton != app.gardenButton
                || app.missionCompletedPopup.transform.Find("Title").GetComponent<TMP_Text>().text != "오늘의 미션을 완료했습니다!")
                throw new InvalidOperationException("Apply Readable UI Polish to bind the completed-mission button and notice.");
            if(app.moodNoteInput.characterLimit!=PrototypeSession.NoteCharacterLimit||app.moodNoteInput.richText
                ||app.moodNoteInput.onValueChanged.GetPersistentEventCount()!=1
                ||app.moodNoteInput.onValueChanged.GetPersistentMethodName(0)!=nameof(PrototypeController.EditMoodNote)
                ||app.moodNoteInput.textViewport.GetComponent<RectMask2D>()==null
                ||app.moodHistory.rowTemplate.noteLabel.richText)
                throw new InvalidOperationException("Note input/binding/plain-text display is incomplete.");
            var check=app.pages[(int)PrototypePage.CheckIn].transform;
            var premature=app.pages[(int)PrototypePage.Mission].transform.Find("EditToday");
            if(premature!=null&&premature.gameObject.activeSelf)
                throw new InvalidOperationException("Hide the pre-mission mood entry point.");
            if(check.Find("Title").GetComponent<TMP_Text>().text!="실천 후 마음은 어떤가요?")
                throw new InvalidOperationException("Apply Post Mission Flow to this scene.");
            if(check.Find("SkipMood")!=null||check.Find("SingleMissionPreview")!=null||check.Find("MissionTag")!=null||check.Find("MissionName")!=null)
                throw new InvalidOperationException("Remove legacy skip button and mission preview from check-in.");
            int triangles = app.plantView.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
            if (triangles > 18000) throw new InvalidOperationException("All prototype plant stages exceed the 18k triangle budget.");
            if (app.definition.maximumStage != 3 || app.plantView.sprout == null)
                throw new InvalidOperationException("Apply Garden Art: seed must be included in four visible stages.");
            var drag = app.pages[0].transform.Find("MainPlant").GetComponent<PrototypeIslandDrag>();
            if (drag == null || drag.previewCamera == null || !drag.GetComponent<RawImage>().raycastTarget
                || app.portraitLayout.GetComponentsInChildren<PrototypeIslandDrag>(true).Length != 1)
                throw new InvalidOperationException("Exactly the home preview must have an orbit input surface.");
            if (app.plantView.GetComponentsInChildren<MeshRenderer>(true).Length > 40)
                throw new InvalidOperationException("Plant geometry must be material-batched, not one renderer per petal.");
            if (app.moodHistory.scroll.content == null || app.moodHistory.scroll.viewport == null
                || app.moodHistory.rowTemplate == null || app.moodHistory.rowTemplate.gameObject.activeSelf
                || app.moodHistory.scroll.horizontal || !app.moodHistory.scroll.vertical
                || app.moodHistory.scroll.viewport.GetComponent<RectMask2D>() == null
                || app.historyTabLabel.text != "마음 기록")
                throw new InvalidOperationException("History template/scroll/navigation is incomplete.");
            Debug.Log($"PROTOTYPE_SCENE_OK {buttons} buttons, 2 flowers, 4 pages, plant triangles={triangles}");
            var input = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (input == null || input.point == null || input.leftClick == null)
                throw new InvalidOperationException("Missing UI input actions.");
            Debug.Log($"PROTOTYPE_INPUT devices={InputSystem.devices.Count}, mouse={Mouse.current != null}, pointEnabled={input.point.action.enabled}, clickEnabled={input.leftClick.action.enabled}");
        }

        [MenuItem("Capstone Prototype/Build Android Development APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support for Unity 6000.3.24f1 in Unity Hub first.");
            Build(BuildTarget.Android, "artifacts/prototype/CapstonePrototype.apk");
        }

        [MenuItem("Capstone Prototype/Build Windows Development")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "artifacts/prototype/windows/CapstonePrototype.exe");

        private static void Build(BuildTarget target, string path)
        {
            ValidateScene();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { PrototypeSceneBuilder.ScenePath }, locationPathName = path,
                target = target, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Prototype build failed: " + report.summary.result);
            Debug.Log("PROTOTYPE_PLAYER_OK " + path);
        }
    }
}
