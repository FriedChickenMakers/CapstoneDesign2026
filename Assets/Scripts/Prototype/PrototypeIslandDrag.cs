using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Prototype
{
    // Home preview only: no domain state and no global mouse polling.
    [RequireComponent(typeof(RawImage))]
    public sealed class PrototypeIslandDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Camera previewCamera;
        public Vector3 focus = new Vector3(0, .58f, 0);
        public const float DefaultDistance = 5.8f;
        public float distance = DefaultDistance;
        public Vector2 defaultAngles = new Vector2(-25, 28); // yaw, pitch
        public const float PitchLimit = 85;
        public Vector2 Angles { get; private set; }
        public bool IsDragging { get; private set; }
        private bool initialized;
        private int pointerId;
        private Vector2 lastLocal;

        private void OnEnable()
        {
            if (!initialized) { Angles = defaultAngles; initialized = true; }
            Apply(Angles);
        }
        private void OnDisable()
        {
            IsDragging = false;
            // Mission/result reuse this render texture, with their default composition.
            Apply(defaultAngles);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) IsDragging = false; }
        private void OnApplicationPause(bool paused) { if (paused) IsDragging = false; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || previewCamera == null || IsDragging
                || eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                eventData.pressPosition, eventData.pressEventCamera, out lastLocal)) return;
            pointerId = eventData.pointerId;
            IsDragging = true;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !IsDragging || pointerId != eventData.pointerId) return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;
            Angles = DragAngles(Angles, local - lastLocal, rect.rect.size);
            lastLocal = local;
            Apply(Angles);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) IsDragging = false;
        }
        public void ResetView()
        {
            initialized = true;
            IsDragging = false;
            Angles = defaultAngles;
            Apply(defaultAngles);
        }
        public static Vector2 DragAngles(Vector2 angles, Vector2 delta, Vector2 size)
        {
            if (size.x <= 0 || size.y <= 0) return angles;
            return new Vector2(Mathf.Repeat(angles.x + delta.x / size.x * 360 + 180, 360) - 180,
                Mathf.Clamp(angles.y - delta.y / size.y * 180, -PitchLimit, PitchLimit));
        }
        private void Apply(Vector2 angles)
        {
            if (previewCamera == null) return;
            Quaternion rotation = Quaternion.Euler(angles.y, angles.x, 0);
            previewCamera.transform.SetPositionAndRotation(focus + rotation * (Vector3.back * distance), rotation);
            // The integrated home keeps its camera disabled between changes.
            if (Application.isPlaying && !previewCamera.enabled && previewCamera.gameObject.activeInHierarchy)
                previewCamera.Render();
        }
    }
}
