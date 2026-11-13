using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    /// <summary>Android's keyboard Back dismisses editing without reverting the draft.</summary>
    public sealed class GardenInputField : InputField
    {
        protected override void LateUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_Keyboard != null && TryDismissCanceledKeyboard(m_Keyboard.status, m_Keyboard.text)) return;
#endif
            base.LateUpdate();
        }

        // The native status/text boundary is explicit so it can be exercised
        // without a physical keyboard. Other platforms keep InputField's Escape behavior.
        public bool TryDismissCanceledKeyboard(TouchScreenKeyboard.Status status, string latestText)
        {
            if (!isFocused || status != TouchScreenKeyboard.Status.Canceled) return false;
            if (!readOnly) text = latestText ?? text;
            // Do this before InputField marks the edit canceled: its default
            // cancellation restores the pre-focus text before sending onEndEdit.
            DeactivateInputField();
            return true;
        }
    }
}
