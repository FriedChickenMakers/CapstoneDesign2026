using UnityEngine;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// Keeps foreground interaction responsive. Background sensor collection
    /// remains under the Android service's separate control.
    /// </summary>
    public sealed class MockupRuntime : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
    }
}
