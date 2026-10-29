using System;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // The imported prototype is only a view. The existing activity component
    // remains the sole owner of GardenStateService and its persisted purchases.
    [DefaultExecutionOrder(-50)]
    public sealed class GardenHomePresenter : MonoBehaviour
    {
        public Canvas homeCanvas;
        public GameObject gardenVisuals;
        public Camera oldCamera;
        public Light oldLight;
        public Camera gardenCamera;
        public WeekOneQuestDemo activity;
        public MockupNavigation navigation;
        public PrototypePlantView plant;
        public Transform gardenRoot;
        public TMP_Text date;
        public TMP_Text title;
        public TMP_Text guide;
        public TMP_Text stage;
        public TMP_Text dailyStatus;
        public TMP_Text missionName;
        public TMP_Text missionActionLabel;
        public TMP_Text homeTabLabel;
        public TMP_Text activityTabLabel;
        public TMP_Text settingsTabLabel;
        public Button missionButton;
        public Button shopButton;
        public Button homeTab;
        public Button activityTab;
        public Button settingsTab;
        // Keep the serialized third-tab reference; its destination is now My Course.
        public Button TemporarySettingsButton { get; private set; }
        public MissionBottomNavigation NavigationBar { get; private set; }
        public PrototypeUiSurface[] progressDots;
        public bool IsGardenVisible => title!=null && title.gameObject.activeInHierarchy;
        public ProceduralPlantView ProceduralPlant { get; private set; }
        public PlantRecipe RenderedRecipe { get; private set; }
        public GardenPlantPlanter Planter { get; private set; }
        public float PlantingHeight { get; private set; }
        public bool IsOverview => backgroundPreview != null && !backgroundPreview.IsSubjectFocused;
        public float Zoom => backgroundPreview != null ? backgroundPreview.Zoom : .85f;
        public bool ViewControlsExpanded { get; private set; }
        public RectTransform OverlayTopBoundary { get; private set; }
        string growthConfirmation;
        float growthConfirmationUntil;

        Transform additions;
        string additionsSignature;
        string plantSignature;
        PrototypeGardenPreviewLayout backgroundPreview;
        static readonly Color Green = new Color(.31f, .47f, .37f);
        static readonly Color Pale = new Color(.88f, .92f, .86f);

        private void Awake()
        {
            missionButton?.onClick.AddListener(OpenActivity);
            shopButton?.onClick.AddListener(OpenShop);
            ConfigureNavigation();
            GardenSkyEnvironment.Ensure(gardenCamera, gardenVisuals != null ? gardenVisuals.transform : null);
            RefreshFromState();
        }

        private void OnDestroy()
        {
            missionButton?.onClick.RemoveListener(OpenActivity);
            shopButton?.onClick.RemoveListener(OpenShop);
            homeTab?.onClick.RemoveListener(OpenHome);
            activityTab?.onClick.RemoveListener(OpenActivity);
            settingsTab?.onClick.RemoveListener(OpenCourse);
            TemporarySettingsButton?.onClick.RemoveListener(OpenSettings);
        }

        private void Update()
        {
            if(IsGardenVisible)RefreshOverlayTextColor();
            if (growthConfirmation != null && Time.unscaledTime >= growthConfirmationUntil)
            {
                growthConfirmation = null;
                if (IsGardenVisible) RefreshFromState();
            }
        }

        public static int StageForGrowth(float growth) => GardenPlantVisuals.StageForGrowth(growth);

        public void Show(bool visible)
        {
            ConfigureNavigation();
            if(TemporarySettingsButton!=null)TemporarySettingsButton.gameObject.SetActive(visible);
            // Disable renderers before the drag target's OnDisable resets its view.
            if (gardenVisuals != null) gardenVisuals.SetActive(visible);
            if (homeCanvas != null)
            {
                EnsureHomeBackground();
                // Keep the actual home navigation mounted across all three tabs.
                // Mission pages own the Figma header and tabs, including the
                // distraction-free practice view. The garden canvas is restored on return.
                bool missionSurface=navigation!=null && navigation.activitiesPanel!=null && navigation.activitiesPanel.activeSelf;
                homeCanvas.gameObject.SetActive(!missionSurface);
                var background=homeCanvas.transform.Find("Background");
                if(background!=null)background.gameObject.SetActive(visible);
                var content=homeCanvas.transform.Find("SafePortraitFrame/MainContent");
                if(content!=null)
                {
                    // One actual TMP brand stays mounted in the same canvas on
                    // every tab, so font metrics and safe-area placement cannot drift.
                    EnsureSharedHeader(content);
                    content.Find("Shared header background").gameObject.SetActive(!visible);
                    if(content.Find("Brand")!=null)content.Find("Brand").gameObject.SetActive(!visible);
                    if(content.Find("GardenAndGrowth")!=null)content.Find("GardenAndGrowth").gameObject.SetActive(visible);
                    foreach(var label in new[]{title,date,stage,guide})if(label!=null)label.gameObject.SetActive(visible);
                }
                RefreshMissionStatus();
                if (backgroundPreview != null) backgroundPreview.gameObject.SetActive(visible);
            }
            // Integrated tabs are opaque overlay canvases; rendering the legacy
            // scene behind them adds a needless camera/light pass on every frame.
            bool legacySceneVisible = homeCanvas == null && !visible;
            if (oldCamera != null) oldCamera.enabled = legacySceneVisible;
            if (oldLight != null) oldLight.enabled = legacySceneVisible;
            if (visible) RefreshFromState();
        }

        public void ConfigureNavigation()
        {
            var content=homeCanvas==null?null:homeCanvas.transform.Find("SafePortraitFrame/MainContent");
            if(content==null)return;
            foreach(var name in new[]{"BottomNavigation","HomeTab","ActivityTab","SettingsTab"})
                if(content.Find(name)!=null)content.Find(name).gameObject.SetActive(false);
            var garden=content.Find("GardenAndGrowth");
            if(garden!=null)
                foreach(var name in new[]{"TodayMissionCard","TodayMissionCard_Edge","DailyStatus","MissionName","MissionEmblem"})
                    if(garden.Find(name)!=null)garden.Find(name).gameObject.SetActive(false);
            if(NavigationBar==null)
            {
                var frame=GardenUi.Box(content,"Shared mission navigation",0,0,1,1);
                var design=FigmaMissionUi.Rect(frame.transform,"Figma 390 x 844",0,0,390,844);
                var fit=frame.AddComponent<FigmaMissionCanvasFit>();fit.Design=design;fit.Fit();
                NavigationBar=MissionBottomNavigation.Create(design,"홈",OpenHome,OpenActivity,OpenCourse,null);
                homeTab=NavigationBar.HomeButton;activityTab=NavigationBar.MissionButton;settingsTab=NavigationBar.CourseButton;
                if(shopButton!=null)
                {
                    var shopRect=(RectTransform)shopButton.transform;
                    shopRect.anchoredPosition=new Vector2(shopRect.anchoredPosition.x,168);
                }
            }
            RefreshMissionStatus();
            if(TemporarySettingsButton!=null)return;
            TemporarySettingsButton=FigmaMissionUi.Button(content,"설정",552,12,120,72,OpenSettings,GardenUi.Pale,GardenUi.Green,28);
            TemporarySettingsButton.name="TemporarySettingsButton";
            GardenThemeSurface.Apply(TemporarySettingsButton.gameObject,GardenThemeSurface.Style.Wood);
            var rect=(RectTransform)TemporarySettingsButton.transform;
            rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-48,-12);
            if(date!=null && backgroundPreview==null)
            {
                var dateRect=date.rectTransform;
                dateRect.anchorMin=dateRect.anchorMax=new Vector2(1,1);dateRect.pivot=new Vector2(1,1);
                dateRect.anchoredPosition=new Vector2(-188,-32);dateRect.sizeDelta=new Vector2(160,48);
            }
        }

        public void RefreshMissionStatus()
        {
            if(NavigationBar==null)return;
            var loop=activity!=null?activity:navigation==null||navigation.activitiesPanel==null?null:navigation.activitiesPanel.GetComponent<WeekOneQuestDemo>();
            NavigationBar.Refresh("홈",loop!=null&&loop.HasOpenState?(bool?)FocusMissionPolicy.HasPendingToday(loop.Service.Snapshot):null);
        }

        static void EnsureSharedHeader(Transform content)
        {
            if(content.Find("Shared header background")!=null)return;
            var background=GardenUi.Box(content,"Shared header background",0,1,1,0,GardenUi.Background);
            GardenThemeSurface.Apply(background,GardenThemeSurface.Style.Header);
            GardenUi.FromTop(background.transform,0,96);
            // This canvas is above the scrolling tab bodies. An opaque band
            // also prevents hidden controls from receiving taps behind the header.
            background.transform.SetAsFirstSibling();
        }

        void EnsureHomeBackground()
        {
            if (homeCanvas == null || backgroundPreview != null) return;
            foreach (var label in homeCanvas.GetComponentsInChildren<TMP_Text>(true)) GardenTypography.ApplyBody(label);
            GardenTypography.ApplyDisplay(title);
            GardenTypography.ApplyDisplay(stage);
            var brand = homeCanvas.transform.Find("SafePortraitFrame/MainContent/Brand")?.GetComponent<TMP_Text>();
            GardenTypography.ApplyDisplay(brand);
            if(brand!=null)brand.color=GardenUi.Green;
            if(date!=null)date.color=GardenUi.Muted;
            if(title!=null)title.color=GardenUi.Ink;
            if(guide!=null)guide.color=GardenUi.Muted;
            if(dailyStatus!=null)dailyStatus.color=GardenUi.Muted;
            if(missionName!=null)missionName.color=GardenUi.Ink;
            GardenButtonBotanicals.Apply(shopButton, true);
            if(shopButton!=null)foreach(var label in shopButton.GetComponentsInChildren<TMP_Text>(true))label.color=Color.white;
            backgroundPreview = homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
            if (backgroundPreview == null) return;
            backgroundPreview.excludedBoundsRoot = plant != null ? plant.transform : null;
            backgroundPreview.UseFullScreenBackground(homeCanvas);
            var content = homeCanvas.transform.Find("SafePortraitFrame/MainContent");
            var home = content.Find("GardenAndGrowth");
            PlaceOverlayText(title,content,48,32,420,58,40,false);
            PlaceOverlayText(date,content,48,98,420,40,26,false);
            PlaceOverlayText(guide,content,48,100,180,40,24,true);
            var growth=home.Find("Growth surface") as RectTransform;
            if(growth==null)growth=(RectTransform)GardenUi.Box(home,"Growth surface",0,0,1,0,GardenUi.Background).transform;
            growth.gameObject.SetActive(true);GardenUi.Round(growth.GetComponent<Image>(),GardenUi.Background);
            GardenThemeSurface.Apply(growth.gameObject,GardenThemeSurface.Style.Paper);
            growth.GetComponent<Image>().raycastTarget=false;
            growth.anchorMin=growth.anchorMax=Vector2.zero;growth.pivot=new Vector2(0,1);
            growth.anchoredPosition=new Vector2(48,328);growth.sizeDelta=new Vector2(624,64);growth.SetAsFirstSibling();
            if(stage!=null)
            {
                var rect=stage.rectTransform;rect.SetParent(growth,false);
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(300,60);
                stage.fontSize=26;stage.color=Green;stage.alignment=TextAlignmentOptions.Midline;
                stage.raycastTarget=false;stage.gameObject.SetActive(true);
            }
            if(progressDots!=null)for(int i=0;i<progressDots.Length;i++)if(progressDots[i]!=null)
            {
                var rect=(RectTransform)progressDots[i].transform;rect.SetParent(growth,false);
                rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.pivot=new Vector2(0,.5f);
                rect.anchoredPosition=new Vector2(350+60*i,0);rect.sizeDelta=new Vector2(48,34);progressDots[i].gameObject.SetActive(true);
                var symbol=i==0?PrototypeUiIcon.Symbol.Seed:i==1?PrototypeUiIcon.Symbol.Leaf:
                    i==2?PrototypeUiIcon.Symbol.Pot:PrototypeUiIcon.Symbol.Flower;
                var icon=GardenUi.Icon(rect,symbol,.5f,.5f,0,0);
                icon.rectTransform.sizeDelta=new Vector2(30,30);
            }
            foreach(var name in new[]{"Home overview surface","Brand"})
                if(content.Find(name)!=null)content.Find(name).gameObject.SetActive(false);
            if(home!=null)foreach(var name in new[]{"Garden view controls","보기 +","보기 −"})
                if(home.Find(name)!=null)home.Find(name).gameObject.SetActive(false);
            OverlayTopBoundary=(RectTransform)GardenUi.Box(content,"Garden top safe boundary",0,1,1,0).transform;
            GardenUi.FromTop(OverlayTopBoundary,0,148);
            backgroundPreview.upperBoundary=OverlayTopBoundary;
            backgroundPreview.lowerBoundary=growth;
            backgroundPreview.SetSubjectFocus(false);
            RefreshOverlayTextColor();
            var sky = GardenSkyEnvironment.Ensure(gardenCamera, gardenVisuals != null ? gardenVisuals.transform : null);
            if (sky != null) sky.PreviewLayout = backgroundPreview;
        }

        static void PlaceOverlayText(TMP_Text label,Transform parent,float x,float y,float width,float height,int size,bool right)
        {
            if(label==null)return;var rect=label.rectTransform;rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(right?1:0,1);
            rect.anchoredPosition=new Vector2(right?-x:x,-y);rect.sizeDelta=new Vector2(width,height);
            label.fontSize=size;label.enableAutoSizing=false;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.overflowMode=TextOverflowModes.Ellipsis;label.alignment=right?TextAlignmentOptions.MidlineRight:TextAlignmentOptions.MidlineLeft;
            label.raycastTarget=false;
        }

        public static Color OverlayTextColor(float daylight)=>daylight>=.5f?Color.black:Color.white;
        public static string CurrentPlantName(PlantRecipe recipe)=>recipe==null?"식물":PlantCatalog.Get(PlantGenerator.SpeciesFor(recipe)).Name;

        public void RefreshOverlayTextColor()
        {
            var sky=gardenVisuals==null?null:gardenVisuals.GetComponent<GardenSkyEnvironment>();
            double hour=GardenVisualClock.CurrentHour;
            Color ink=OverlayTextColor(sky!=null?sky.Daylight:hour>=6&&hour<18?1:0);
            if(title!=null)title.color=ink;
            if(date!=null)date.color=ink;
            if(guide!=null)guide.color=ink;
        }

        public void SetViewControlsExpanded(bool expanded)
        {
            ViewControlsExpanded=false;
        }

        // A presentation preset changes only the view, never the saved garden.
        public void ResetView()
        {
            EnsureHomeBackground();
            backgroundPreview?.SetSubjectFocus(false);
            backgroundPreview?.SetZoom(.85f);
            backgroundPreview?.orbit?.ResetView();
            SetViewControlsExpanded(false);
            RenderView();
        }

        public void ShowGrowthConfirmation(int nutrientCost)
        {
            growthConfirmation = "성장 완료 · 영양제 " + nutrientCost + "개 사용";
            growthConfirmationUntil = Time.unscaledTime + 3.5f;
            RefreshFromState();
        }

        public void SetOverview(bool overview)
        {
            backgroundPreview?.SetSubjectFocus(!overview);
            RenderView();
        }

        public void SetZoom(float zoom)
        {
            backgroundPreview?.SetZoom(zoom);
            RenderView();
        }

        void RenderView()
        {
            backgroundPreview?.Apply();
            if (Application.isPlaying && gardenVisuals != null && gardenVisuals.activeInHierarchy && gardenCamera != null)
                gardenCamera.Render();
        }

        static void CenterInGrowthSurface(RectTransform item, RectTransform surface)
        {
            float x = item.anchoredPosition.x - surface.anchoredPosition.x;
            item.SetParent(surface, false);
            item.anchorMin = item.anchorMax = new Vector2(0, .5f);
            item.pivot = new Vector2(item.pivot.x, .5f);
            item.anchoredPosition = new Vector2(x, 0);
        }

        static void StyleTab(Button button,TMP_Text label,bool selected)
        {
            if(button==null)return;
            var surface=button.GetComponent<PrototypeUiSurface>();
            if(surface!=null)
            {
                surface.topColor=surface.bottomColor=selected?GardenUi.Pale:Color.clear;
                surface.color=Color.white;surface.SetAllDirty();
            }
            if(label!=null){label.color=selected?GardenUi.Green:GardenUi.Muted;label.fontStyle=selected?FontStyles.Bold:FontStyles.Normal;}
            var icon=button.GetComponentInChildren<PrototypeUiIcon>();
            if(icon!=null)icon.color=selected?GardenUi.Green:GardenUi.Muted;
            CenterTabContents(button,label,icon);
            GardenThemeSurface.Apply(button.gameObject,selected?GardenThemeSurface.Style.Selected:GardenThemeSurface.Style.Paper);
        }

        static void CenterTabContents(Button button,TMP_Text label,PrototypeUiIcon icon)
        {
            if(label==null || icon==null)return;
            // Center the icon and its actual text width together, including
            // the wider bold label when this tab becomes selected.
            var layout=button.GetComponent<HorizontalLayoutGroup>() ?? button.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment=TextAnchor.MiddleCenter;
            layout.padding=new RectOffset(8,8,0,0);
            layout.spacing=8;
            layout.childControlWidth=layout.childControlHeight=true;
            layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            icon.transform.SetAsFirstSibling();
            var iconSize=icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
            iconSize.minWidth=iconSize.preferredWidth=32;
            iconSize.minHeight=iconSize.preferredHeight=32;
            iconSize.flexibleWidth=iconSize.flexibleHeight=0;
            label.margin=Vector4.zero;
            label.textWrappingMode=TextWrappingModes.NoWrap;
            label.alignment=TextAlignmentOptions.MidlineLeft;
        }

        public void RefreshFromState() => RefreshFromState(activity != null && activity.HasOpenState ? activity.Service.Snapshot : null);

        public void RefreshFromState(GardenState saved)
        {
            using var timing = UiPerformanceProbe.Measure("Home.RefreshFromState");
            RefreshMissionStatus();
            EnsureHomeBackground();
            GardenSkyEnvironment.Ensure(gardenCamera, gardenVisuals != null ? gardenVisuals.transform : null);
            if (activity == null || !activity.HasOpenState || saved == null)
            {
                if (gardenVisuals != null) gardenVisuals.SetActive(false);
                if (title != null) title.text = "저장 상태 확인 필요";
                if (guide != null) guide.text = "기존 데이터를 보존했습니다. 저장 공간을 확인해 주세요.";
                if (stage != null) stage.text = "";
                if (dailyStatus != null) dailyStatus.text = "정원을 표시할 수 없어요";
                if (missionName != null) missionName.text = "활동 화면에서 자세한 안내를 확인해 주세요";
                return;
            }
            // The service owns persistence. This detached snapshot also supports
            // unpaid previews without changing the saved recipe or wallet.
            GardenPlantVisuals.Sync(saved);
            float growth = GardenPlantVisuals.TotalGrowth(saved);
            int visualStage = StageForGrowth(growth);
            RenderPlant(saved.PlantAppearance.Recipe, growth);
            if (date != null) date.text = CurrentDateText();
            if (title != null) title.text = CurrentPlantName(RenderedRecipe);
            if (guide != null) guide.text = "영양제 " + saved.Nutrient + "개";
            if (stage != null) stage.text = new[] { "작은 씨앗", "새싹", "봉오리", "꽃이 피었어요" }[visualStage];
            if (progressDots != null)
                for (int i = 0; i < progressDots.Length; i++)
                    if (progressDots[i] != null)
                    {
                        progressDots[i].color = i <= visualStage ? Green : Pale;
                        GardenThemeSurface.Apply(progressDots[i].gameObject,i<=visualStage?
                            GardenThemeSurface.Style.Selected:GardenThemeSurface.Style.Paper);
                        var icon=progressDots[i].GetComponentInChildren<PrototypeUiIcon>();
                        if(icon!=null)icon.color=i<=visualStage?Color.white:new Color(1,1,1,.40f);
                    }
            var active = saved.Sessions.LastOrDefault(s => FocusMissionPolicy.IsFocus(s) && FocusMissionPolicy.IsActive(s));
            var completed = saved.Sessions.LastOrDefault(s => FocusMissionPolicy.IsFocus(s) && s.InstanceId == saved.LastCycleId && s.Status == ParticipationState.RewardCommitted);
            if (dailyStatus != null) dailyStatus.text = active != null ? "이어서 하기"
                : completed != null ? "오늘 완료한 활동" : "오늘의 활동";
            if (missionName != null) missionName.text = active != null ? (active.MissionTitleSnapshot ?? FocusMissionCatalog.Find(active.MissionId)?.Title ?? "활동 이어가기")
                : completed != null ? (completed.MissionTitleSnapshot ?? FocusMissionCatalog.Find(completed.MissionId)?.Title ?? "참여 기록 보기") : "활동 선택하기";
            if (missionActionLabel != null) missionActionLabel.text = active != null ? "활동 이어가기" : "오늘의 활동";
            if (homeTabLabel != null) homeTabLabel.text = "홈";
            if (activityTabLabel != null) activityTabLabel.text = "미션";
            if (settingsTabLabel != null) settingsTabLabel.text = "내 과정";
            RefreshAdditions(saved);
            if (Application.isPlaying && homeCanvas.gameObject.activeInHierarchy
                && gardenVisuals.activeInHierarchy && gardenCamera != null)
            {
                homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true)?.Apply();
                gardenVisuals.GetComponent<GardenSkyEnvironment>()?.UpdateClouds(Time.unscaledTime);
                gardenCamera.Render();
            }
        }

        void RenderPlant(PlantRecipe recipe, float growth)
        {
            if (gardenRoot == null) return;
            // Keep serialized prototype assets for the standalone scene and a
            // clean revert; only the integrated home replaces its visible model.
            if (plant != null)
            {
                plant.seed.SetActive(false);
                plant.sprout.SetActive(false);
                plant.bud.gameObject.SetActive(false);
                plant.blossomAnchor.gameObject.SetActive(false);
            }
            if (ProceduralPlant == null)
            {
                var model = new GameObject("Saved procedural garden plant");
                model.transform.SetParent(gardenRoot, false);
                ProceduralPlant = model.AddComponent<ProceduralPlantView>();
                GardenSceneModels.ConfigureRenderers(model);
            }
            string signature = PlantCodec.Encode(recipe);
            bool meshChanged = signature != plantSignature;
            if (meshChanged)
            {
                ProceduralPlant.Render(recipe);
                plantSignature = signature;
            }
            RenderedRecipe = recipe;
            // The generated plant is the garden's main subject, while each
            // species keeps its own proportions and exact saved mesh.
            float scale = GardenPlantVisuals.ScaleForGrowth(growth);
            bool scaleChanged = ProceduralPlant.transform.localScale != Vector3.one * scale;
            ProceduralPlant.transform.localScale = Vector3.one * scale;
            // A growing branch changes the bounds, never the stem's soil point.
            var footprint = new Vector2(0, .12f);
            float ground = GardenSceneModels.GroundHeight(gardenRoot, footprint);
            if (Planter == null) Planter = GardenPlantPlanter.Create(gardenRoot);
            Planter.transform.localPosition = new Vector3(footprint.x, ground + .002f, footprint.y);
            PlantingHeight = ground + .002f + GardenPlantPlanter.SoilHeight + .002f;
            ProceduralPlant.transform.localPosition = new Vector3(footprint.x, PlantingHeight, footprint.y);
            backgroundPreview?.SetSubject(ProceduralPlant.transform, Planter.transform);
            if (meshChanged || scaleChanged)
                homeCanvas?.GetComponentInChildren<PrototypeGardenPreviewLayout>(true)?.RecalculateBounds();
        }

        public void RefreshDate()
        {
            if (date == null || homeCanvas == null || !homeCanvas.gameObject.activeInHierarchy) return;
            var today = CurrentDateText();
            if (date.text != today) date.text = today;
        }

        string CurrentDateText()=>activity!=null&&activity.HasOpenState
            ?TimeZoneInfo.ConvertTime(activity.Service.CurrentUtc,activity.Service.CurrentTimeZone).ToString("M월 d일")
            :DateTime.Now.ToString("M월 d일");

        void OpenHome() => navigation?.ShowIsland();
        void OpenActivity() => navigation?.ShowActivities();
        void OpenCourse() => navigation?.ShowCourse();
        void OpenSettings() => navigation?.ShowSettings();
        void OpenShop()
        {
            navigation?.ShowActivities();
            activity?.ShowShop();
        }

        void RefreshAdditions(GardenState saved)
        {
            if (gardenRoot == null) return;
            var signature=string.Join("|",saved.Environments.Select(e=>e.EnvironmentId+":"+e.Slot))+"/"+saved.DailyDecisions.LastOrDefault()?.VisitorId;
            if(additions!=null && signature==additionsSignature)return;additionsSignature=signature;
            if (additions != null)
            {
                additions.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(additions.gameObject);
                else DestroyImmediate(additions.gameObject);
            }
            additions = new GameObject("Saved garden additions").transform;
            additions.SetParent(gardenRoot, false);
            foreach (var item in saved.Environments)
                GardenSceneModels.CreateEnvironment(item.EnvironmentId, additions);
            var visitor = saved.DailyDecisions.LastOrDefault()?.VisitorId;
            if (!string.IsNullOrEmpty(visitor)) GardenSceneModels.CreateVisitor(visitor, additions);
            homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true)?.RecalculateBounds();
        }

    }
}
