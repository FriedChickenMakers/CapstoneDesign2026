using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed class FocusMissionEntrance : MonoBehaviour
    {
        CanvasGroup group;
        float elapsed;
        void Awake() { group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>(); group.alpha = FigmaMissionUi.ReducedMotion?1:.65f; }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = FigmaMissionUi.ReducedMotion?1:Mathf.Lerp(.65f,1,Mathf.Clamp01(elapsed / .18f));
            if (elapsed >= .18f) { group.alpha = 1; Destroy(this); }
        }
    }

    // Central illustration hit targets remain transparent but acknowledge a press.
    public sealed class FocusMissionTapFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Button Button;
        public void OnPointerDown(PointerEventData eventData)
        { if (Button != null && Button.interactable) Button.targetGraphic.color = new Color(1, 1, 1, .12f); }
        public void OnPointerUp(PointerEventData eventData) => Clear();
        public void OnPointerExit(PointerEventData eventData) => Clear();
        void Clear() { if (Button != null) Button.targetGraphic.color = Color.clear; }
    }
}
