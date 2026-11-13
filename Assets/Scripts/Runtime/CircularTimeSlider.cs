using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // Only the ring accepts a pointer; its empty centre remains a scroll surface.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircularTimeSlider : MaskableGraphic, ICanvasRaycastFilter,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IMoveHandler
    {
        public int StepMinutes { get; set; } = 15;
        public int Minute { get; private set; }
        public event Action<int> Changed;
        bool dragging;
        int pointer;
        float Radius => Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .38f;

        public void SetMinuteWithoutNotify(int minute)
        { Minute = Wrap(minute); SetVerticesDirty(); }
        public void Nudge(int direction) => Select(Minute + direction * Mathf.Max(1, StepMinutes));
        static int Wrap(int minute) => (minute % 1440 + 1440) % 1440;
        void Select(int minute)
        {
            minute = Wrap(minute);
            if (Minute == minute) return;
            Minute = minute; SetVerticesDirty(); Changed?.Invoke(minute);
        }
        public static int MinuteAt(Vector2 local, int step)
        {
            float angle = Mathf.Repeat(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg, 360);
            int increment = Mathf.Clamp(step, 1, 60);
            return Wrap(Mathf.RoundToInt(angle / 360 * 1440 / increment) * increment);
        }
        bool Point(Vector2 screen, Camera camera, out Vector2 local)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out local)) return false;
            local -= rectTransform.rect.center;
            return true;
        }
        public bool IsRaycastLocationValid(Vector2 point, Camera camera) =>
            Point(point, camera, out var local) && Mathf.Abs(local.magnitude - Radius) <= 30;
        public void OnPointerDown(PointerEventData data)
        {
            if (dragging || data.button != PointerEventData.InputButton.Left || !IsRaycastLocationValid(data.position, data.pressEventCamera)) return;
            pointer = data.pointerId; dragging = true;
            EventSystem.current?.SetSelectedGameObject(gameObject);
            UpdatePointer(data);
        }
        public void OnBeginDrag(PointerEventData data) { }
        public void OnDrag(PointerEventData data) { if (dragging && data.pointerId == pointer) UpdatePointer(data); }
        void UpdatePointer(PointerEventData data)
        {
            if (Point(data.position, data.pressEventCamera, out var local) && local.magnitude > Radius * .5f)
                Select(MinuteAt(local, StepMinutes));
        }
        public void OnPointerUp(PointerEventData data) { if (data.pointerId == pointer) dragging = false; }
        public void OnEndDrag(PointerEventData data) => OnPointerUp(data);
        protected override void OnDisable() { dragging = false; base.OnDisable(); }
        public void OnMove(AxisEventData data)
        { if (data.moveDir == MoveDirection.Left || data.moveDir == MoveDirection.Down) Nudge(-1); else if (data.moveDir == MoveDirection.Right || data.moveDir == MoveDirection.Up) Nudge(1); }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var centre = rectTransform.rect.center;
            float radius = Radius;
            for (int i = 0; i < 96; i++)
            {
                float a = i / 96f * Mathf.PI * 2, b = (i + 1) / 96f * Mathf.PI * 2;
                Quad(mesh, centre + Direction(a) * (radius - 12), centre + Direction(a) * (radius + 12),
                    centre + Direction(b) * (radius + 12), centre + Direction(b) * (radius - 12), GardenUi.Pale);
            }
            for (int hour = 0; hour < 24; hour++)
            {
                var direction = Direction(hour / 24f * Mathf.PI * 2); var side = new Vector2(direction.y, -direction.x) * 1.5f;
                var outer = centre + direction * (radius + 10); var inner = centre + direction * (radius - (hour % 6 == 0 ? 12 : 3));
                Quad(mesh, inner - side, inner + side, outer + side, outer - side, GardenUi.Muted);
            }
            var knob = centre + Direction(Minute / 1440f * Mathf.PI * 2) * radius;
            for (int i = 0; i < 32; i++)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(knob, GardenUi.Green, Vector2.zero);
                mesh.AddVert(knob + Direction(i / 32f * Mathf.PI * 2) * 19, GardenUi.Green, Vector2.zero);
                mesh.AddVert(knob + Direction((i + 1) / 32f * Mathf.PI * 2) * 19, GardenUi.Green, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
            }
        }
        static Vector2 Direction(float angle) => new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero); mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero); mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
