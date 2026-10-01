using UnityEngine;

namespace CapstoneDesign.Runtime
{
    /// <summary>
    /// Keeps the always-on Unity loop modest while the Android sensor trial
    /// remains under the platform service's separate control.
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
