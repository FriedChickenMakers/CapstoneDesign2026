using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public sealed class FocusMissionEntrance : MonoBehaviour
    {
        CanvasGroup group;
        float elapsed;
        void Awake() { group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(elapsed / .16f);
            if (elapsed >= .16f) Destroy(this);
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
