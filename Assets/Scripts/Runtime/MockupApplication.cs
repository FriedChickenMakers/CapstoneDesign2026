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
                legacyBottomNavigation.SetActive(selected != islandPanel);

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
    }

    /// <summary>
    /// Small runtime quality guard. It is deliberately independent of the
    /// project's Editor generated scene so it can also be used by a hand-made
    /// test scene later.
    /// </summary>
    public sealed class MockupRuntime : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 30;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
    }
}
