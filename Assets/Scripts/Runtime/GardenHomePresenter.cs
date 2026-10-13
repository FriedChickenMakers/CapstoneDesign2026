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
        public PrototypeUiSurface[] progressDots;
        public bool IsGardenVisible => title!=null && title.gameObject.activeInHierarchy;
        public ProceduralPlantView ProceduralPlant { get; private set; }
        public PlantRecipe RenderedRecipe { get; private set; }
        public GardenPlantPlanter Planter { get; private set; }
        public float PlantingHeight { get; private set; }
        public bool IsOverview => backgroundPreview != null && !backgroundPreview.IsSubjectFocused;
        public float Zoom => backgroundPreview != null ? backgroundPreview.Zoom : .85f;
        Button plantViewButton, overviewButton, zoomOutButton, zoomInButton;

        Transform additions;
        string additionsSignature;
        string plantSignature;
        PrototypeGardenPreviewLayout backgroundPreview;
        GameObject overviewSurface;
        static readonly Color Green = new Color(.31f, .47f, .37f);
        static readonly Color Pale = new Color(.88f, .92f, .86f);

        private void Awake()
        {
            missionButton?.onClick.AddListener(OpenActivity);
            shopButton?.onClick.AddListener(OpenShop);
            homeTab?.onClick.AddListener(OpenHome);
            activityTab?.onClick.AddListener(OpenActivity);
            settingsTab?.onClick.AddListener(OpenSettings);
            GardenSkyEnvironment.Ensure(gardenCamera, gardenVisuals != null ? gardenVisuals.transform : null);
            RefreshFromState();
        }

        private void OnDestroy()
        {
            missionButton?.onClick.RemoveListener(OpenActivity);
            shopButton?.onClick.RemoveListener(OpenShop);
            homeTab?.onClick.RemoveListener(OpenHome);
            activityTab?.onClick.RemoveListener(OpenActivity);
            settingsTab?.onClick.RemoveListener(OpenSettings);
        }

        public static int StageForGrowth(float growth) => GardenPlantVisuals.StageForGrowth(growth);

        public void Show(bool visible)
        {
            // Disable renderers before the drag target's OnDisable resets its view.
            if (gardenVisuals != null) gardenVisuals.SetActive(visible);
            if (homeCanvas != null)
            {
                EnsureHomeBackground();
                // Keep the actual home navigation mounted across all three tabs.
                homeCanvas.gameObject.SetActive(true);
                var background=homeCanvas.transform.Find("Background");
                if(background!=null)background.gameObject.SetActive(visible);
                var content=homeCanvas.transform.Find("SafePortraitFrame/MainContent");
                if(content!=null)
                {
                    // One actual TMP brand stays mounted in the same canvas on
                    // every tab, so font metrics and safe-area placement cannot drift.
                    EnsureSharedHeader(content);
                    content.Find("Shared header background").gameObject.SetActive(!visible);
                    if (overviewSurface != null) overviewSurface.SetActive(visible);
                    if(content.Find("Brand")!=null)content.Find("Brand").gameObject.SetActive(true);
                    foreach(var name in new[]{"Date","GardenAndGrowth"})
                        if(content.Find(name)!=null)content.Find(name).gameObject.SetActive(visible);
                }
                StyleTab(homeTab,homeTabLabel,visible);
                StyleTab(activityTab,activityTabLabel,navigation!=null && navigation.activitiesPanel.activeSelf);
                StyleTab(settingsTab,settingsTabLabel,navigation!=null && navigation.settingsPanel.activeSelf);
                if (backgroundPreview != null) backgroundPreview.gameObject.SetActive(visible);
            }
            // Integrated tabs are opaque overlay canvases; rendering the legacy
            // scene behind them adds a needless camera/light pass on every frame.
            bool legacySceneVisible = homeCanvas == null && !visible;
            if (oldCamera != null) oldCamera.enabled = legacySceneVisible;
            if (oldLight != null) oldLight.enabled = legacySceneVisible;
            if (visible) RefreshFromState();
        }

        static void EnsureSharedHeader(Transform content)
        {
            if(content.Find("Shared header background")!=null)return;
            var background=GardenUi.Box(content,"Shared header background",0,1,1,0,GardenUi.Background);
            GardenUi.FromTop(background.transform,0,96);
            // This canvas is above the scrolling tab bodies. An opaque band
            // also prevents hidden controls from receiving taps behind the header.
            background.transform.SetAsFirstSibling();
        }

        void EnsureHomeBackground()
        {
            if (homeCanvas == null || backgroundPreview != null) return;
            backgroundPreview = homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
            if (backgroundPreview == null) return;
            backgroundPreview.excludedBoundsRoot = plant != null ? plant.transform : null;
            backgroundPreview.UseFullScreenBackground(homeCanvas);
            var content = homeCanvas.transform.Find("SafePortraitFrame/MainContent");
            var home = content.Find("GardenAndGrowth");
            overviewSurface = GardenUi.Box(content, "Home overview surface", 0, 1, 1, 0, GardenUi.Background);
            GardenUi.Round(overviewSurface.GetComponent<Image>(), GardenUi.Background);
            GardenUi.FromTop(overviewSurface.transform, 14, 366);
            var overview = (RectTransform)overviewSurface.transform;
            overview.offsetMin = new Vector2(24, overview.offsetMin.y);
            overview.offsetMax = new Vector2(-24, overview.offsetMax.y);
            overview.SetAsFirstSibling();
            var growth = GardenUi.Box(home, "Growth surface", 0, 0, 1, 0, GardenUi.Background);
            GardenUi.Round(growth.GetComponent<Image>(), GardenUi.Background);
            var growthRect = (RectTransform)growth.transform;
            growthRect.anchorMin = growthRect.anchorMax = Vector2.zero;
            growthRect.pivot = new Vector2(0, 1);
            growthRect.anchoredPosition = new Vector2(48, 328);
            growthRect.sizeDelta = new Vector2(624, 64);
            growthRect.SetAsFirstSibling();
            // The compact surface is higher than the old prototype row. Anchor
            // its contents to the surface, including the visible glyph bounds.
            if (stage != null)
            {
                CenterInGrowthSurface(stage.rectTransform, growthRect);
                stage.alignment = TextAlignmentOptions.Midline;
            }
            if (progressDots != null)
                foreach (var dot in progressDots)
                    if (dot != null) CenterInGrowthSurface(dot.rectTransform, growthRect);
            backgroundPreview.lowerBoundary = growthRect;
            EnsureViewControls(home);
            // A true background is behind the UI. These opaque surfaces block
            // drag gestures even where there is no text or button graphic.
            var sky = GardenSkyEnvironment.Ensure(gardenCamera, gardenVisuals != null ? gardenVisuals.transform : null);
            if (sky != null) sky.PreviewLayout = backgroundPreview;
        }

        void EnsureViewControls(Transform home)
        {
            // One compact row groups the two mutually exclusive views and
            // bounded zoom; every target is at least 96 design units high.
            var row = GardenUi.Box(home, "Garden view controls", 0, 1, 1, 0);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(48, -154);
            rect.sizeDelta = new Vector2(624, 96);
            plantViewButton = GardenUi.Button(row.transform, "가까이 보기", 0, 0, 200f / 624, 1, () => SetOverview(false));
            overviewButton = GardenUi.Button(row.transform, "정원 전체", 208f / 624, 0, 200f / 624, 1, () => SetOverview(true));
            zoomOutButton = GardenUi.Button(row.transform, "−", 416f / 624, 0, 100f / 624, 1, () => SetZoom(Zoom - .2f));
            zoomInButton = GardenUi.Button(row.transform, "+", 524f / 624, 0, 100f / 624, 1, () => SetZoom(Zoom + .2f));
            zoomOutButton.name = "축소"; zoomInButton.name = "확대";
            foreach (var button in new[] { plantViewButton, overviewButton, zoomOutButton, zoomInButton })
                button.GetComponentInChildren<Text>().fontSize = 30;
            backgroundPreview.upperBoundary = rect;
            backgroundPreview.SetSubjectFocus(false);
            UpdateViewControls();
        }

        public void SetOverview(bool overview)
        {
            backgroundPreview?.SetSubjectFocus(!overview);
            UpdateViewControls();
            RenderView();
        }

        public void SetZoom(float zoom)
        {
            backgroundPreview?.SetZoom(zoom);
            UpdateViewControls();
            RenderView();
        }

        void UpdateViewControls()
        {
            if (plantViewButton == null) return;
            GardenUi.StyleButton(plantViewButton, selected: !IsOverview);
            GardenUi.StyleButton(overviewButton, selected: IsOverview);
            zoomOutButton.interactable = Zoom > .001f;
            zoomInButton.interactable = Zoom < .999f;
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
            if (date != null) date.text = DateTime.Now.ToString("M월 d일");
            if (title != null) title.text = RenderedRecipe.SpeciesId == null ? "오늘의 식물"
                : "오늘의 " + PlantCatalog.Get(RenderedRecipe.SpeciesId).Name;
            if (guide != null) guide.text = "영양제 " + saved.Nutrient + "개 · 드래그해 둘러보세요.";
            if (stage != null) stage.text = new[] { "작은 씨앗", "새싹", "봉오리", "꽃이 피었어요" }[visualStage];
            if (progressDots != null)
                for (int i = 0; i < progressDots.Length; i++)
                    if (progressDots[i] != null) progressDots[i].color = i <= visualStage ? Green : Pale;
            var active = saved.Sessions.LastOrDefault(s => s.Status == ParticipationState.Selected || s.Status == ParticipationState.InProgress || s.Status == ParticipationState.Paused);
            var completed = saved.Sessions.LastOrDefault(s => s.InstanceId == saved.LastCycleId && s.Status == ParticipationState.RewardCommitted);
            if (dailyStatus != null) dailyStatus.text = active != null ? "진행 중인 활동이 있어요"
                : completed != null ? "오늘의 활동을 마쳤어요" : "오늘의 작은 실천";
            if (missionName != null) missionName.text = active != null ? (MindfulnessContent.FindMission(active.MissionId)?.title ?? "활동 이어가기")
                : completed != null ? (MindfulnessContent.FindMission(completed.MissionId)?.title ?? "수고했어요") : "내 속도로 한 걸음씩";
            if (missionActionLabel != null) missionActionLabel.text = active != null ? "활동 이어가기" : "오늘의 활동";
            if (homeTabLabel != null) homeTabLabel.text = "홈 · 정원";
            if (activityTabLabel != null) activityTabLabel.text = "오늘의 활동";
            if (settingsTabLabel != null) settingsTabLabel.text = "설정";
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
            float scale = 1.9f * (1 + Mathf.Clamp((growth - .3f) * .25f, 0, .18f));
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
            var today = DateTime.Now.ToString("M월 d일");
            if (date.text != today) date.text = today;
        }

        void OpenHome() => navigation?.ShowIsland();
        void OpenActivity() => navigation?.ShowActivities();
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
