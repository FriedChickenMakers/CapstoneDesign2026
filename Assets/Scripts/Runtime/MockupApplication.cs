using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// Owns the intentionally small navigation surface for the first mockup.
    /// The activity/settings panels are overlays; the island remains the
    /// default view and can always be reached from the centre button.
    /// </summary>
    public sealed class MockupNavigation : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] public GameObject islandPanel;
        [SerializeField] public GameObject activitiesPanel;
        [SerializeField] public GameObject settingsPanel;

        [Header("Navigation buttons")]
        [SerializeField] public Button activitiesButton;
        [SerializeField] public Button islandButton;
        [SerializeField] public Button settingsButton;
        [SerializeField] public GardenHomePresenter home;
        [SerializeField] public GameObject legacyBottomNavigation;
        private bool presentationPrepared;

        public void PreparePresentation()
        {
            if(presentationPrepared)return;
            presentationPrepared=true;
            var scaler=GetComponent<CanvasScaler>();
            if(scaler!=null){scaler.referenceResolution=new Vector2(720,1280);scaler.matchWidthOrHeight=.5f;}
            if(home==null)return;
            var backing=GardenUi.Box(transform,"Shared page background",0,0,1,1,GardenUi.Background);
            backing.transform.SetAsFirstSibling();backing.GetComponent<Image>().raycastTarget=false;
            var frame=GardenUi.Box(transform,"Tab design frame",0,0,1,1);
            var layout=GetComponent<CapstoneDesign.Prototype.PrototypePortraitLayout>()
                ?? gameObject.AddComponent<CapstoneDesign.Prototype.PrototypePortraitLayout>();
            layout.designFrame=(RectTransform)frame.transform;
            var sensor=GetComponentInChildren<SensorRawDisplay>(true);
            foreach(var panel in new[]{islandPanel,activitiesPanel,settingsPanel,sensor==null?null:sensor.gameObject})
                if(panel!=null)panel.transform.SetParent(frame.transform,false);
            if(settingsPanel!=null)
            {
                var image=settingsPanel.GetComponent<Image>();if(image!=null)image.color=GardenUi.Background;
                foreach(Transform child in settingsPanel.transform)
                    if(child.GetComponent<Text>()!=null)child.gameObject.SetActive(false);
            }
            layout.Apply();
        }

        private void Awake()
        {
            if (activitiesButton != null)
            {
                activitiesButton.onClick.AddListener(ShowActivities);
            }

            if (islandButton != null)
            {
                islandButton.onClick.AddListener(ShowIsland);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(ShowSettings);
            }

            ShowIsland();
        }

        private void OnDestroy()
        {
            if (activitiesButton != null)
            {
                activitiesButton.onClick.RemoveListener(ShowActivities);
            }

            if (islandButton != null)
            {
                islandButton.onClick.RemoveListener(ShowIsland);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(ShowSettings);
            }
        }

        public void ShowIsland()
        {
            SetActivePanel(islandPanel);
        }

        public void ShowActivities()
        {
            SetActivePanel(activitiesPanel);
        }

        public void ShowSettings()
        {
            SetActivePanel(settingsPanel);
            GetComponentInChildren<SensorRawDisplay>(true)?.OnSettingsOpened();
        }

        private float nextCycleCheck;
        private void Update()
        {
            activitiesPanel?.GetComponent<WeekOneQuestDemo>()?.TickStepSync();
            if(Time.unscaledTime < nextCycleCheck)return;
            nextCycleCheck=Time.unscaledTime+30;
            activitiesPanel?.GetComponent<WeekOneQuestDemo>()?.RefreshCycle();
            home?.RefreshDate();
        }
        private void OnApplicationFocus(bool focus)
        {
            if(focus)
            {
                var loop=activitiesPanel?.GetComponent<WeekOneQuestDemo>();
                loop?.OnAppReturned();
                home?.RefreshFromState();
            }
        }
        private void SetActivePanel(GameObject selected)
        {
            PreparePresentation();
            GardenUi.ConstrainWidth(legacyBottomNavigation);
            if(settingsPanel!=null)
                foreach(Transform child in settingsPanel.transform)
                    if(child.GetComponent<Text>()!=null)GardenUi.ConstrainWidth(child.gameObject);
            if(legacyBottomNavigation!=null)
            {
                var card=legacyBottomNavigation.transform.Find("NavigationCard")?.GetComponent<Image>();
                if(card!=null && card.sprite==null)GardenUi.Round(card,Color.white,true);
            }
            var sensor = GetComponentInChildren<SensorRawDisplay>(true);
            if (sensor != null) sensor.gameObject.SetActive(selected == settingsPanel);
            var loop = activitiesPanel == null ? null : activitiesPanel.GetComponent<WeekOneQuestDemo>();
            if (loop != null && loop.Service != null)
            {
                if(selected!=activitiesPanel && activitiesPanel.activeSelf)loop.LeaveActivityPanel();
                loop.RefreshCycle();
            }
            if (islandPanel != null)
            {
                islandPanel.SetActive(selected == islandPanel && home == null);
            }

            if (legacyBottomNavigation != null)
                legacyBottomNavigation.SetActive(home==null && selected != islandPanel);
            SetLegacyTabActive(activitiesButton, selected == activitiesPanel);
            SetLegacyTabActive(settingsButton, selected == settingsPanel);
            SetLegacyTabActive(islandButton, selected == islandPanel);

            if (activitiesPanel != null)
            {
                activitiesPanel.SetActive(selected == activitiesPanel);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(selected == settingsPanel);
            }
            home?.Show(selected == islandPanel);
        }

        private static void SetLegacyTabActive(Button button, bool active)
        {
            if (button == null) return;
            GardenUi.StyleButton(button,selected:active);
        }
    }

}
