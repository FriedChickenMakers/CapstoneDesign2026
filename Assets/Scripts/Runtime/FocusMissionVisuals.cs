using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FocusMissionEntrance : MonoBehaviour
    {
        CanvasGroup group;
        float elapsed;
        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = FigmaMissionUi.ReducedMotion?1:.65f;
        }
        void Update()
        {
            if (group == null) { Destroy(this); return; }
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
        { if (eventData.button == PointerEventData.InputButton.Left && Button != null && Button.IsInteractable() && Button.targetGraphic != null) Button.targetGraphic.color = new Color(1, 1, 1, .12f); }
        public void OnPointerUp(PointerEventData eventData) => Clear();
        public void OnPointerExit(PointerEventData eventData) => Clear();
        void OnDisable() => Clear();
        void Clear() { if (Button != null && Button.targetGraphic != null) Button.targetGraphic.color = Color.clear; }
    }
}
