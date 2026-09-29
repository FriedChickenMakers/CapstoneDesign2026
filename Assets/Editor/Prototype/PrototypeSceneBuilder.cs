using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Prototype.unity";
        public const string DefinitionPath = "Assets/Settings/Prototype/PrototypeDefinition.asset";
        private const string ArtPath = "Assets/Art/Prototype";
        private static TMP_FontAsset font;
        private static readonly Color Paper = Hex("FAFAF3");
        private static readonly Color Ink = Hex("2F4438");
        private static readonly Color Muted = Hex("7B897C");
        private static readonly Color Green = Hex("527A60");
        private static readonly Color Pale = Hex("E7EEDF");

        [MenuItem("Capstone Prototype/Build Prototype Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your open scene before building Prototype.");
            Directory.CreateDirectory("Assets/Settings/Prototype");
            Directory.CreateDirectory("Assets/Materials/Prototype");
            Directory.CreateDirectory(ArtPath);
            AssetDatabase.Refresh();
            var definition = AssetDatabase.LoadAssetAtPath<PrototypeDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PrototypeDefinition>();
                PrototypeDefaults.Populate(definition);
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            PrototypeDefaults.MigrateDailyPrototype(definition);
            EditorUtility.SetDirty(definition);
            definition.ValidateDefinition();
            EnsureFont();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app = new GameObject("PrototypeApp").AddComponent<PrototypeController>();
            app.definition = definition;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.backgroundColor = Paper;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.cullingMask = 0; camera.depth = -1;
            app.plantView = BuildGarden(out RenderTexture preview);
            BuildUi(app, preview);
            PrototypeGardenUpgrade.ConfigureDrag(app, GameObject.Find("Garden Camera").GetComponent<Camera>());
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            app.plantView.Render(new PrototypeSession(definition), definition);
            for (int i = 0; i < app.pages.Length; i++) app.pages[i].SetActive(i == 0);
            app.ClosePopups();
            app.developerPanel.SetActive(true);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = app.gameObject;
            Debug.Log("PROTOTYPE_BUILD_OK daily-four-step " + ScenePath);
        }

        // Add only the requested page/navigation, preserving existing plant and scene edits.
        [MenuItem("Capstone Prototype/Apply Mood History UI")]
        public static void ApplyMoodHistory()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeController>(true)).Single();
            EnsureFont();
            app.portraitLayout = app.screenRoot.GetComponentInParent<PrototypePortraitLayout>();
            Stretch((RectTransform)app.screenRoot.transform);
            var content = app.screenRoot.transform;
            var home = content.Find("HomeTab").GetComponent<Button>();
            var tab = content.Find("MoodTab").GetComponent<Button>();
            app.homeTabLabel = home.GetComponentInChildren<TMP_Text>(true);
            app.historyTabLabel = tab.GetComponentInChildren<TMP_Text>(true);
            app.historyTabLabel.text = "마음 기록";
            while (tab.onClick.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(tab.onClick, 0);
            UnityEventTools.AddPersistentListener(tab.onClick, app.ShowMoodHistory);
            AnchorBottom((RectTransform)content.Find("BottomNavigation"), 106);
            AnchorBottom((RectTransform)home.transform, 83);
            AnchorBottom((RectTransform)tab.transform, 83);
            if (app.moodHistory == null) BuildMoodHistory(app, content);
            app.pages[(int)PrototypePage.MoodHistory].SetActive(false);
            EditorUtility.SetDirty(app);
            EditorUtility.SetDirty(tab);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_HISTORY_UI_OK " + ScenePath);
        }

        [MenuItem("Capstone Prototype/Apply Mood Note UI")]
        public static void ApplyMoodNote()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=ScenePath||scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PrototypeController>(true)).Single();
            if(app.moodHistory==null)throw new InvalidOperationException("Apply Mood History UI first.");
            EnsureFont();
            Undo.RegisterFullObjectHierarchyUndo(app.screenRoot,"Add mood note field");
            var check=app.pages[(int)PrototypePage.CheckIn].transform;
            foreach(string name in new[]{"SkipMood","SingleMissionPreview","SingleMissionPreview_Edge","MissionTag","MissionName"})
            {
                var old=check.Find(name);
                if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
            }
            if(app.moodNoteInput==null)BuildMoodNoteInput(app,check);
            app.questionText.rectTransform.anchoredPosition=new Vector2(48,-657);
            ((RectTransform)app.quietButton.transform).anchoredPosition=new Vector2(48,-729);
            ((RectTransform)app.togetherButton.transform).anchoredPosition=new Vector2(48,-837);
            app.moodHistory.transform.Find("Guide").GetComponent<TMP_Text>().text="그날의 감정과 생각을 모았어요.";
            app.historyActionLabel.text="오늘 마음 살펴보기";
            ConfigureRecordNote(app.moodHistory.rowTemplate);
            ConfigurePostMissionFlow(app);
            EditorUtility.SetDirty(app);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_NOTE_UI_OK " + ScenePath);
        }

        [MenuItem("Capstone Prototype/Apply Post Mission Flow")]
        public static void ApplyPostMissionFlow()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=ScenePath||scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PrototypeController>(true)).Single();
            EnsureFont();
            Undo.RegisterFullObjectHierarchyUndo(app.screenRoot,"Move mood recording after mission");
            ConfigurePostMissionFlow(app);
            EditorUtility.SetDirty(app);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_POST_MISSION_FLOW_OK " + ScenePath);
        }

        [MenuItem("Capstone Prototype/Apply Mission State UI")]
        public static void ApplyMissionStateUi()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeController>(true)).Single();
            EnsureFont();
            Undo.RecordObject(app, "Bind mission state presentation");
            ConfigureMissionStateUi(app);
            EditorUtility.SetDirty(app);
            EditorUtility.SetDirty(app.missionPresentation);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_MISSION_STATE_UI_OK " + ScenePath);
        }

        private static void ConfigureMissionStateUi(PrototypeController app)
        {
            var content = app.screenRoot.transform;
            var view = app.GetComponent<PrototypeMissionPresentation>();
            if (view == null) view = Undo.AddComponent<PrototypeMissionPresentation>(app.gameObject);
            else Undo.RecordObject(view, "Configure mission state presentation");
            app.missionPresentation = view;
            view.background = content.GetComponentInParent<Canvas>().transform.Find("Background").GetComponent<PrototypeUiSurface>();
            view.idleBackground = view.background.topColor;
            view.header = content.Find("Brand").GetComponent<TMP_Text>();
            var history = app.moodHistory.transform;
            // Only text directly on the background changes; white cards/buttons keep their ink.
            view.backgroundLabels = new[] {
                view.header, app.dateText, app.gardenTitle, app.gardenGuide, app.stageText,
                app.missionTitle, app.missionGuide, app.missionStatus,
                history.Find("Title").GetComponent<TMP_Text>(), history.Find("Guide").GetComponent<TMP_Text>(),
                app.moodHistory.countLabel, history.Find("EmptyHistory/EmptyLabel").GetComponent<TMP_Text>()
            };
        }

        [MenuItem("Capstone Prototype/Apply Responsive Layout")]
        public static void ApplyResponsiveLayout()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Open the saved Prototype scene and stop Play Mode first.");
            var app = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeController>(true)).Single();
            Undo.RegisterFullObjectHierarchyUndo(app.portraitLayout.designFrame.gameObject, "Unify responsive page layout");
            ConfigureResponsiveLayout(app);
            app.portraitLayout.Apply();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PROTOTYPE_RESPONSIVE_LAYOUT_OK " + ScenePath);
        }

        private static void ConfigureResponsiveLayout(PrototypeController app)
        {
            // Keep the authored 720x1280 arrangement, distributing extra height via anchors.
            foreach (var page in app.pages)
            {
                var rect = (RectTransform)page.transform;
                Stretch(rect); rect.offsetMin = new Vector2(0, 130); rect.offsetMax = new Vector2(0, -106);
            }
            var home = app.pages[(int)PrototypePage.Garden].transform;
            foreach (var item in new[] { ("MainPlant",150f), ("MainPlant_GroundGlow",345f), ("PlantStage",685f),
                ("GrowthStep_1",748f), ("GrowthStep_2",748f), ("GrowthStep_3",748f), ("GrowthStep_4",748f) })
                AnchorPageItem(home, item.Item1, item.Item2, .5f);
            foreach (var item in new[] { ("TodayMissionCard",800f), ("TodayMissionCard_Edge",798f),
                ("DailyStatus",814f), ("MissionName",855f), ("GardenAction",950f) })
                AnchorPageItem(home, item.Item1, item.Item2, 0);

            var mission = app.pages[(int)PrototypePage.Mission].transform;
            AnchorPageItem(mission, "MissionPlant", 150, .5f);
            AnchorPageItem(mission, "MissionPlant_GroundGlow", 345, .5f);
            AnchorPageItem(mission, "MissionStatus", 770, .5f);
            AnchorPageItem(mission, "MissionAction", 950, 0);

            var check = app.pages[(int)PrototypePage.CheckIn].transform;
            for (int i = 1; i <= 5; i++) AnchorPageItem(check, "Mood_" + i, 202, .5f);
            foreach (var item in new[] { ("MoodStatus",363f), ("NoteTitle",410f), ("NoteCounter",410f),
                ("MoodNoteInput",462f), ("Question",657f), ("QuietChoice",729f), ("TogetherChoice",837f) })
                AnchorPageItem(check, item.Item1, item.Item2, .5f);
            AnchorPageItem(check, "ConfirmCheckIn", 950, 0);

            AnchorPageItem(app.moodHistory.transform, "HistoryAction", 950, 0);
            AnchorPageItem(app.moodHistory.transform, "EmptyHistory", 242, .5f);
            var content = app.screenRoot.transform;
            AnchorBottom((RectTransform)content.Find("BottomNavigation"), 106);
            foreach (string name in new[] { "HomeTab", "MoodTab" })
            {
                var tab = (RectTransform)content.Find(name);
                AnchorBottom(tab, 97); tab.sizeDelta = new Vector2(tab.sizeDelta.x, 88);
                var label = tab.GetComponentInChildren<TMP_Text>(true).rectTransform;
                Stretch(label); label.offsetMin = new Vector2(12, 0); label.offsetMax = new Vector2(-12, 0);
            }
            // Modals stay centred without changing the shared frame underneath them.
            foreach (var popup in new[] { app.growthPopup, app.settingsPopup, app.resetPopup })
            {
                var rect = (RectTransform)popup.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
                rect.anchoredPosition = new Vector2(0, 640);
            }
            PrototypePreviewUpgrade.Configure(app);
        }

        private static void AnchorPageItem(Transform page, string name, float baselineTop, float anchorY)
        {
            var rect = (RectTransform)page.Find(name);
            rect.anchorMin = new Vector2(rect.anchorMin.x, anchorY);
            rect.anchorMax = new Vector2(rect.anchorMax.x, anchorY);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, (1 - anchorY) * 1044 - baselineTop);
        }

        private static void ConfigurePostMissionFlow(PrototypeController app)
        {
            var check=app.pages[(int)PrototypePage.CheckIn].transform;
            check.Find("Title").GetComponent<TMP_Text>().text="실천 후 마음은 어떤가요?";
            check.Find("Guide").GetComponent<TMP_Text>().text="지금의 마음을 짧게 남겨보세요.";
            app.checkInActionLabel.text="기록하고 성장 보기";
            app.historyActionLabel.text="오늘 시작하기";
            var old=app.pages[(int)PrototypePage.Mission].transform.Find("EditToday");
            if(old!=null)old.gameObject.SetActive(false);
        }

        private static void EnsureFont()
        {
            if (TMP_Settings.instance == null) throw new InvalidOperationException("Import TMP Essential Resources first.");
            const string path = ArtPath + "/Fonts/NotoSansKR SDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(ArtPath + "/Fonts/NotoSansKR-Regular.otf");
                if (source == null) throw new InvalidOperationException("Noto Sans KR is missing.");
                font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = "NotoSansKR SDF";
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            }
            string characters = string.Concat(Directory.GetFiles("Assets/Scripts/Prototype", "*.cs").Select(File.ReadAllText))
                + string.Concat(Directory.GetFiles("Assets/Editor/Prototype", "*.cs").Select(File.ReadAllText));
            font.TryAddCharacters(new string(characters.Distinct().ToArray()), out string _);
            EditorUtility.SetDirty(font);
        }

        private static PrototypePlantView BuildGarden(out RenderTexture preview)
            => PrototypeGardenArt.Build(out preview, out _);

        private static void BuildUi(PrototypeController app, RenderTexture preview)
        {
            var canvas=new GameObject("PrototypeCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(720,1280);scaler.matchWidthOrHeight=.5f;
            var background=Surface("Background",canvas.transform,0,0,720,1280,Paper,0);
            Stretch(background.rectTransform);background.raycastTarget=false;
            var backdrop=Surface("ModalBackdrop",canvas.transform,0,0,720,1280,new Color(.13f,.20f,.15f,.27f),0);
            Stretch(backdrop.rectTransform);app.modalBackdrop=backdrop.gameObject;
            var frame=Rect("SafePortraitFrame",canvas.transform,0,0,720,1280);
            var layout=canvas.AddComponent<PrototypePortraitLayout>();layout.designFrame=frame;
            app.portraitLayout=layout;
            app.screenRoot=Rect("MainContent",frame,0,0,720,1280).gameObject;
            Stretch((RectTransform)app.screenRoot.transform);
            var content=app.screenRoot.transform;
            Label("Brand",content,"마음 정원",48,32,260,48,26,Green);
            app.dateText=Label("Date",content,DateTime.Now.ToString("M월 d일"),370,36,190,40,21,Muted);
            app.dateText.alignment=TextAlignmentOptions.MidlineRight;
            app.developerPanel=Button("DeveloperSettings",content,"시연",586,34,86,44,app.OpenSettings,out _,true,20).gameObject;
            app.pages=new GameObject[3];
            app.pages[0]=Rect("GardenAndGrowth",content,0,106,720,1044).gameObject;
            app.pages[1]=Rect("DailyMoodAndChoice",content,0,106,720,1044).gameObject;
            app.pages[2]=Rect("SingleMission",content,0,106,720,1044).gameObject;

            var main=app.pages[0].transform;
            app.gardenTitle=Label("Title",main,"오늘의 식물",48,14,624,64,38);
            app.gardenGuide=Label("Guide",main,"섬을 드래그해 천천히 둘러보세요.",48,88,624,48,24,Muted);
            PlantPreview("MainPlant",main,preview,150);
            app.stageText=CenterLabel("PlantStage",main,"작은 씨앗",685,26,Green);
            app.progressDots=new PrototypeUiSurface[4];
            for(int i=0;i<4;i++)app.progressDots[i]=Surface("GrowthStep_"+(i+1),main,266+i*50,748,36,9,i==0?Green:Pale,1);
            Card("TodayMissionCard",main,48,800,624,118);
            app.dailyStatus=Label("DailyStatus",main,"오늘의 작은 실천",74,814,548,35,21,Muted);
            Label("MissionName",main,app.definition.missionTitle,74,855,548,45,29);
            app.gardenButton=Button("GardenAction",main,"오늘의 미션",48,950,624,86,app.GardenAction,out app.gardenActionLabel);

            var check=app.pages[1].transform;
            Label("Title",check,"실천 후 마음은 어떤가요?",48,14,624,64,35);
            Label("Guide",check,"지금의 마음을 짧게 남겨보세요.",48,88,624,44,23,Muted);
            app.moodButtons=new Button[5];app.moodFaces=new PrototypeMoodFace[5];
            UnityAction[] select={app.SelectHappy,app.SelectCalm,app.SelectNeutral,app.SelectTired,app.SelectDown};
            for(int i=0;i<5;i++)
            {
                float x=44+i*128;
                var rt=Rect("Mood_"+(i+1),check,x,202,120,150);
                var hit=rt.gameObject.AddComponent<Image>();hit.color=Color.clear;
                var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=hit;
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                UnityEventTools.AddPersistentListener(button.onClick,select[i]);app.moodButtons[i]=button;
                var face=Rect("Face",rt,15,0,90,90).gameObject.AddComponent<PrototypeMoodFace>();
                face.mood=(Mood)(i+1);face.raycastTarget=false;app.moodFaces[i]=face;
                var label=Label("MoodLabel",rt,PrototypeDefinition.MoodLabels[i+1],0,99,120,40,21);
                label.alignment=TextAlignmentOptions.Center;
            }
            app.moodStatus=CenterLabel("MoodStatus",check,"감정 선택은 자유예요",363,22,Green);
            BuildMoodNoteInput(app,check);
            app.questionText=Label("Question",check,app.definition.questions[0].question,48,657,624,52,28);
            app.quietButton=Button("QuietChoice",check,app.definition.questions[0].quietLabel,48,729,624,90,app.ChooseQuiet,out app.quietLabel,true,26);
            app.togetherButton=Button("TogetherChoice",check,app.definition.questions[0].togetherLabel,48,837,624,90,app.ChooseTogether,out app.togetherLabel,true,26);
            app.checkInAction=Button("ConfirmCheckIn",check,"기록하고 성장 보기",48,950,624,86,app.ConfirmCheckIn,out app.checkInActionLabel);
            app.checkInAction.interactable=false;

            var mission=app.pages[2].transform;
            app.missionTitle=Label("Title",mission,app.definition.missionTitle,48,14,624,64,38);
            app.missionGuide=Label("Guide",mission,app.definition.missionGuide,48,88,624,48,24,Muted);
            PlantPreview("MissionPlant",mission,preview,150);
            app.missionStatus=CenterLabel("MissionStatus",mission,"하루 한 번 · 나의 속도로",770,26,Green);
            app.missionAction=Button("MissionAction",mission,"작은 실천 완료",48,950,624,86,app.MissionAction,out _);

            BuildMoodHistory(app,content);
            AnchorBottom(Surface("BottomNavigation",content,0,1174,720,106,Color.white,.15f).rectTransform,106);
            AnchorBottom((RectTransform)Button("HomeTab",content,"홈 · 정원",48,1197,298,54,app.ShowGarden,out app.homeTabLabel,true,23).transform,83);
            AnchorBottom((RectTransform)Button("MoodTab",content,"마음 기록",374,1197,298,54,app.ShowMoodHistory,out app.historyTabLabel,true,23).transform,83);

            var growth=Popup("GrowthResult",frame,out app.growthPopup);
            app.growthTitle=CenterLabel("Title",growth,"오늘의 실천이 자랐어요",185,34);
            app.growthGuide=CenterLabel("Guide",growth,"기분과 상관없이, 해낸 행동은 남아요.",251,23,Muted);
            PlantPreview("GrownPlantPreview",growth,preview,303);
            app.growthStage=CenterLabel("GrowthStage",growth,"",815,27,Green);
            app.growthSummary=CenterLabel("GrowthSummary",growth,"",875,23,Muted);
            Button("ConfirmGrowth",growth,"메인으로",76,997,568,84,app.ConfirmGrowth,out _);

            var settings=Popup("DemoSettings",frame,out app.settingsPopup);
            CenterLabel("Title",settings,"시연 도구",285,36);
            CenterLabel("Guide",settings,"저장 없이 이번 실행에서만 진행해요.",354,23,Muted);
            app.settingsDate=CenterLabel("DemoDate",settings,"",447,25,Green);
            Button("NextDay",settings,"다음 날로",76,563,568,88,app.DemoNextDay,out _);
            Button("Reset",settings,"처음부터 시작",76,686,568,88,app.AskReset,out _,true);
            Button("CloseSettings",settings,"메인으로",196,915,328,64,app.ShowGarden,out _,true,24);

            var reset=Popup("ResetConfirmation",frame,out app.resetPopup);
            CenterLabel("Title",reset,"처음부터 시작할까요?",354,34);
            CenterLabel("Guide",reset,"이번 실행의 성장과 선택이 초기화돼요.",434,23,Muted);
            Button("ConfirmReset",reset,"초기화",76,654,568,86,app.ConfirmReset,out _);
            Button("CancelReset",reset,"취소",196,811,328,64,app.ShowGarden,out _,true);
            ConfigureMissionStateUi(app);
            ConfigureResponsiveLayout(app);
            PrototypeUiPolishUpgrade.Configure(app);
            layout.Apply();
        }

        internal static void EnsureMissionCompletedPopup(PrototypeController app)
        {
            if (app.missionCompletedPopup != null) return;
            font = app.gardenActionLabel.font;
            var popup = Popup("MissionCompleted", app.portraitLayout.designFrame, out app.missionCompletedPopup);
            var rect = (RectTransform)popup;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(0, 640);
            CenterLabel("Title", popup, "오늘의 미션을 완료했습니다!", 518, 34);
            Button("ConfirmMissionCompleted", popup, "확인", 76, 676, 568, 96, app.ShowGarden, out _);
            app.missionCompletedPopup.SetActive(false);
        }

        private static void BuildMoodHistory(PrototypeController app, Transform parent)
        {
            var page=Rect("MoodHistory",parent,0,106,720,1044);
            Stretch(page);page.offsetMin=new Vector2(0,130);page.offsetMax=new Vector2(0,-106);
            Array.Resize(ref app.pages,4);app.pages[(int)PrototypePage.MoodHistory]=page.gameObject;
            var view=page.gameObject.AddComponent<PrototypeMoodHistoryView>();app.moodHistory=view;
            Label("Title",page,"마음 기록",48,14,624,64,38);
            Label("Guide",page,"그날의 감정과 생각을 모았어요.",48,88,624,44,23,Muted);
            view.countLabel=Label("RecordCount",page,"기록 0개",48,158,624,38,22,Green);

            var scroll=Rect("HistoryScroll",page,48,208,624,700);
            Stretch(scroll);scroll.offsetMin=new Vector2(48,136);scroll.offsetMax=new Vector2(-48,-208);
            var hit=scroll.gameObject.AddComponent<Image>();hit.color=Color.clear;
            view.scroll=scroll.gameObject.AddComponent<ScrollRect>();
            view.scroll.horizontal=false;view.scroll.vertical=true;
            view.scroll.movementType=ScrollRect.MovementType.Clamped;
            view.scroll.scrollSensitivity=45;view.scroll.decelerationRate=.10f;
            var viewport=Rect("Viewport",scroll,0,0,610,700);
            Stretch(viewport);viewport.offsetMax=new Vector2(-14,0);
            viewport.gameObject.AddComponent<RectMask2D>();view.scroll.viewport=viewport;
            var items=Rect("Records",viewport,0,0,610,0);
            items.anchorMin=new Vector2(0,1);items.anchorMax=Vector2.one;
            items.sizeDelta=Vector2.zero;view.scroll.content=items;
            var column=items.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing=16;column.padding=new RectOffset(0,0,2,2);
            column.childControlWidth=column.childControlHeight=true;
            column.childForceExpandWidth=true;column.childForceExpandHeight=false;
            var fit=items.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var track=Surface("Scrollbar",scroll,618,0,6,700,Pale,1);
            track.rectTransform.anchorMin=new Vector2(1,0);track.rectTransform.anchorMax=Vector2.one;
            track.rectTransform.pivot=new Vector2(1,1);track.rectTransform.sizeDelta=new Vector2(6,0);track.rectTransform.anchoredPosition=Vector2.zero;
            var bar=track.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;
            var handle=Surface("Handle",track.transform,0,0,6,60,Green,1);
            Stretch(handle.rectTransform);bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            bar.navigation=new Navigation{mode=Navigation.Mode.None};
            view.scroll.verticalScrollbar=bar;view.scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;

            var empty=Rect("EmptyHistory",page,48,242,624,400);view.emptyState=empty.gameObject;
            var icon=Rect("EmptyIcon",empty,266,60,92,92).gameObject.AddComponent<PrototypeMoodFace>();
            icon.mood=Mood.Neutral;icon.raycastTarget=false;
            var emptyLabel=Label("EmptyLabel",empty,"아직 기록이 없어요",0,186,624,52,28,Muted);
            emptyLabel.alignment=TextAlignmentOptions.Center;
            var action=Button("HistoryAction",page,"오늘 시작하기",48,950,624,86,app.HistoryAction,out app.historyActionLabel);
            AnchorBottom((RectTransform)action.transform,94);

            var card=Surface("MoodRecordTemplate",page,0,0,610,184,Color.white,.25f);
            card.raycastTarget=false;
            var row=card.gameObject.AddComponent<PrototypeMoodHistoryRow>();view.rowTemplate=row;
            var rowSize=card.gameObject.AddComponent<LayoutElement>();rowSize.preferredHeight=rowSize.minHeight=184;
            row.dateLabel=Label("RecordDate",card.transform,"",22,16,360,34,21,Muted);
            row.timeLabel=Label("RecordTime",card.transform,"",500,16,88,34,20,Muted);
            row.timeLabel.alignment=TextAlignmentOptions.MidlineRight;
            row.timeLabel.rectTransform.anchorMin=row.timeLabel.rectTransform.anchorMax=Vector2.one;
            row.timeLabel.rectTransform.anchoredPosition=new Vector2(-110,-16);
            row.face=Rect("RecordedFace",card.transform,22,78,76,76).gameObject.AddComponent<PrototypeMoodFace>();row.face.raycastTarget=false;
            var skipped=Surface("SkippedMood",card.transform,22,78,76,76,Pale,1);skipped.raycastTarget=false;
            row.skippedIcon=skipped.gameObject;
            var dash=Label("Dash",skipped.transform,"—",0,0,76,76,26,Muted);dash.alignment=TextAlignmentOptions.Center;
            row.moodLabel=Label("RecordedMood",card.transform,"",122,70,466,48,28);
            row.moodLabel.rectTransform.anchorMax=Vector2.one;
            row.moodLabel.rectTransform.sizeDelta=new Vector2(-144,row.moodLabel.rectTransform.sizeDelta.y);
            row.noteLabel=Label("RecordedNote",card.transform,"",22,174,566,34,22,Muted);
            ConfigureRecordNote(row);
            card.gameObject.SetActive(false);
            scroll.gameObject.SetActive(false);
        }

        private static void BuildMoodNoteInput(PrototypeController app,Transform parent)
        {
            Label("NoteTitle",parent,"오늘의 기분",48,410,420,38,25);
            app.noteCounter=Label("NoteCounter",parent,"0 / "+PrototypeSession.NoteCharacterLimit,530,410,142,38,20,Muted);
            app.noteCounter.alignment=TextAlignmentOptions.MidlineRight;
            var surface=Surface("MoodNoteInput",parent,48,462,624,154,Color.white,.26f);
            var input=surface.gameObject.AddComponent<TMP_InputField>();app.moodNoteInput=input;
            input.targetGraphic=surface;
            input.navigation=new Navigation{mode=Navigation.Mode.None};
            var viewport=Rect("TextArea",surface.transform,22,18,580,118);
            viewport.gameObject.AddComponent<RectMask2D>();
            var placeholder=Label("Placeholder",viewport,"지금 나의 기분을 적어보세요 (선택)",0,0,580,118,23,Muted);
            var text=Label("NoteInputText",viewport,"",0,0,580,118,24,Ink);
            foreach(var label in new[]{text,placeholder})
            {
                label.alignment=TextAlignmentOptions.TopLeft;
                label.enableAutoSizing=false;
                label.textWrappingMode=TextWrappingModes.Normal;
                label.richText=false;label.parseCtrlCharacters=false;
                Stretch(label.rectTransform);
            }
            input.textViewport=viewport;input.textComponent=(TextMeshProUGUI)text;input.placeholder=placeholder;
            input.contentType=TMP_InputField.ContentType.Standard;
            input.lineType=TMP_InputField.LineType.MultiLineNewline;
            input.characterLimit=PrototypeSession.NoteCharacterLimit;
            input.richText=false;input.onFocusSelectAll=false;
            input.restoreOriginalTextOnEscape=false;
            input.customCaretColor=true;input.caretColor=Green;input.caretWidth=2;
            input.selectionColor=new Color(.65f,.78f,.64f,.45f);
            UnityEventTools.AddPersistentListener(input.onValueChanged,app.EditMoodNote);
        }

        private static void ConfigureRecordNote(PrototypeMoodHistoryRow row)
        {
            row.moodLabel.rectTransform.anchoredPosition=new Vector2(122,-91);
            var note=row.noteLabel;
            note.name="RecordedNote";note.text="";
            note.richText=false;note.parseCtrlCharacters=false;
            note.enableAutoSizing=false;note.fontSize=22;
            note.textWrappingMode=TextWrappingModes.Normal;note.alignment=TextAlignmentOptions.TopLeft;
            note.rectTransform.anchorMin=new Vector2(0,1);note.rectTransform.anchorMax=Vector2.one;
            note.rectTransform.anchoredPosition=new Vector2(22,-174);
            note.rectTransform.sizeDelta=new Vector2(-44,34);
        }

        private static void AnchorBottom(RectTransform rect,float topFromBottom)
        {
            rect.anchorMin=rect.anchorMax=Vector2.zero;
            rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,topFromBottom);
        }

        private static void PlantPreview(string name,Transform parent,RenderTexture preview,float top)
        {
            var halo=Surface(name+"_GroundGlow",parent,80,top+195,560,245,Hex("EBEEDF"),1);
            halo.raycastTarget=false;
            // Keep the legacy anchor available, but no decorative oval behind the island.
            halo.gameObject.SetActive(false);
            var image=Rect(name,parent,60,top,600,540).gameObject.AddComponent<RawImage>();
            image.texture=preview;image.raycastTarget=false;
        }
        private static void Card(string name,Transform parent,float x,float y,float w,float h)
        {
            Surface(name+"_Edge",parent,x-2,y-2,w+4,h+4,Hex("ECEEE4"),.28f);
            Surface(name,parent,x,y,w,h,Color.white,.28f);
        }
        private static Transform Popup(string name,Transform parent,out GameObject popup)
        {
            popup=Rect(name,parent,0,0,720,1280).gameObject;
            Surface("Card",popup.transform,28,130,664,1018,Paper,.065f);
            return popup.transform;
        }
        private static TMP_Text CenterLabel(string name,Transform parent,string text,float y,int size,Color? color=null)
        {
            var label=Label(name,parent,text,64,y,592,size*1.6f,size,color);
            label.alignment=TextAlignmentOptions.Center;return label;
        }
        private static void Stretch(RectTransform rt)
        {rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
        private static PrototypeUiSurface Surface(string name,Transform parent,float x,float y,float w,float h,Color color,float roundness)
        {
            var surface=Rect(name,parent,x,y,w,h).gameObject.AddComponent<PrototypeUiSurface>();
            surface.topColor=surface.bottomColor=color;surface.roundness=roundness;return surface;
        }
        private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        private static TMP_Text Label(string name,Transform parent,string content,float x,float y,float w,float h,int size,Color? color=null)
        {
            var text=Rect(name,parent,x,y,w,Mathf.Max(h,size*1.5f)).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font;text.fontSize=size;text.color=color??Ink;text.text=content;
            text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.NoWrap;
            text.enableAutoSizing=true;text.fontSizeMin=size-3;text.fontSizeMax=size;text.overflowMode=TextOverflowModes.Overflow;
            text.raycastTarget=false;return text;
        }
        private static Button Button(string name,Transform parent,string caption,float x,float y,float w,float h,UnityAction callback,out TMP_Text label,bool secondary=false,int size=28)
        {
            var image=Surface(name,parent,x,y,w,h,secondary ? Color.white : Green,secondary ? .40f : 1);
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.96f,.98f,.95f);
            colors.pressedColor=new Color(.80f,.88f,.79f);colors.disabledColor=new Color(.82f,.84f,.80f,.65f);button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            UnityEventTools.AddPersistentListener(button.onClick,callback);
            label=Label("Label",image.transform,caption,12,0,w-24,h,size,secondary?Green:Color.white);
            label.alignment=TextAlignmentOptions.Center;return button;
        }
        private static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
    }
}
