using System;
using System.Linq;
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

        Transform additions;
        string additionsSignature;
        int renderedStage=-1;
        Vector3 originalFlowerScale;
        static readonly Color Green = new Color(.31f, .47f, .37f);
        static readonly Color Pale = new Color(.88f, .92f, .86f);

        private void Awake()
        {
            if (plant != null) originalFlowerScale = plant.blossomAnchor.localScale;
            missionButton?.onClick.AddListener(OpenActivity);
            shopButton?.onClick.AddListener(OpenShop);
            homeTab?.onClick.AddListener(OpenHome);
            activityTab?.onClick.AddListener(OpenActivity);
            settingsTab?.onClick.AddListener(OpenSettings);
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

        public static int StageForGrowth(float growth)
        {
            if (growth < .099f) return 0;
            if (growth < .199f) return 1;
            if (growth < .299f) return 2;
            return 3;
        }

        public void Show(bool visible)
        {
            if (homeCanvas != null)
            {
                // Keep the actual home navigation mounted across all three tabs.
                homeCanvas.gameObject.SetActive(true);
                var background=homeCanvas.transform.Find("Background");
                if(background!=null)background.gameObject.SetActive(visible);
                var content=homeCanvas.transform.Find("SafePortraitFrame/MainContent");
                if(content!=null)
                    foreach(var name in new[]{"Brand","Date","GardenAndGrowth"})
                        if(content.Find(name)!=null)content.Find(name).gameObject.SetActive(visible);
                StyleTab(homeTab,homeTabLabel,visible);
                StyleTab(activityTab,activityTabLabel,navigation!=null && navigation.activitiesPanel.activeSelf);
                StyleTab(settingsTab,settingsTabLabel,navigation!=null && navigation.settingsPanel.activeSelf);
            }
            if (gardenVisuals != null) gardenVisuals.SetActive(visible);
            if (oldCamera != null) oldCamera.enabled = !visible;
            if (oldLight != null) oldLight.enabled = !visible;
            if (visible) RefreshFromState();
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
            if (originalFlowerScale == Vector3.zero && plant != null) originalFlowerScale = plant.blossomAnchor.localScale;

            float growth = saved.LegacyGrowth + saved.Plants.Where(p => p.PlantId == "plant:P06").Sum(p => p.Growth);
            int visualStage = StageForGrowth(growth);
            if(renderedStage!=visualStage){plant.RenderVisual(visualStage, FlowerShape.Chamomile);renderedStage=visualStage;}
            // Every later purchase remains visible even after the first blossom.
            plant.blossomAnchor.localScale = originalFlowerScale * (1 + Mathf.Clamp((growth - .3f) * .5f, 0, .8f));
            if (date != null) date.text = DateTime.Now.ToString("M월 d일");
            if (title != null) title.text = "오늘의 캐모마일";
            if (guide != null) guide.text = "영양제 " + saved.Nutrient + "개 · 섬을 드래그해 둘러보세요.";
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
                gardenCamera.Render();
            }
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
            {
                if (item.EnvironmentId == "environment:E01")
                {
                    Shape("Flower bed", PrimitiveType.Cube, new Vector3(-.9f, .19f, .35f), new Vector3(.34f, .10f, .31f), new Color(.56f, .37f, .28f));
                    for (int i = 0; i < 3; i++) Shape("Bed flower", PrimitiveType.Sphere,
                        new Vector3(-1.02f + i * .12f, .31f, .35f), Vector3.one * .12f, new Color(.96f, .79f, .65f));
                }
                else if (item.EnvironmentId == "environment:E02")
                {
                    Shape("Rest bench", PrimitiveType.Cube, new Vector3(.82f, .36f, .28f), new Vector3(.42f, .09f, .28f), new Color(.58f, .39f, .27f));
                    foreach (float x in new[] { .66f, .98f }) Shape("Bench leg", PrimitiveType.Cube,
                        new Vector3(x, .21f, .28f), new Vector3(.06f, .26f, .06f), new Color(.48f, .33f, .25f));
                }
            }
            var visitor = saved.DailyDecisions.LastOrDefault()?.VisitorId;
            if (!string.IsNullOrEmpty(visitor))
            {
                var fur = new Color(.82f, .75f, .64f);
                Shape("Visitor " + visitor, PrimitiveType.Sphere, new Vector3(.68f, .29f, -.65f),
                    new Vector3(.29f, .21f, .24f), fur);
                Shape("Visitor head", PrimitiveType.Sphere, new Vector3(.68f, .43f, -.78f),
                    Vector3.one * .18f, new Color(.89f, .82f, .70f));
                foreach (float x in new[] { .62f, .74f })
                {
                    Shape("Visitor ear", PrimitiveType.Sphere, new Vector3(x, .57f, -.77f),
                        new Vector3(.07f, .12f, .07f), fur);
                    Shape("Visitor eye", PrimitiveType.Sphere, new Vector3(x, .45f, -.94f),
                        Vector3.one * .025f, new Color(.21f, .23f, .18f));
                }
                Shape("Visitor tail", PrimitiveType.Sphere, new Vector3(.91f, .33f, -.57f),
                    Vector3.one * .13f, fur);
            }
            homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true)?.RecalculateBounds();
        }

        void Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 30;
            go.transform.SetParent(additions, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = color;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            go.AddComponent<GardenMaterialCleanup>();
        }
    }
}
