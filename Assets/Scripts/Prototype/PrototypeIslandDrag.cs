using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace CapstoneDesign.Prototype
{
    // View-only orbit/zoom. Presses and wheel input must reach this garden surface,
    // so buttons over the image retain their own input and never initiate zoom.
    [RequireComponent(typeof(RawImage))]
    public sealed class PrototypeIslandDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        public Camera previewCamera;
        public PrototypeGardenPreviewLayout zoomLayout;
        public Vector3 focus = new Vector3(0, .58f, 0);
        public const float DefaultDistance = 5.8f;
        public float distance = DefaultDistance;
        public Vector2 defaultAngles = new Vector2(-25, 28); // yaw, pitch
        public const float PitchLimit = 85;
        public Vector2 Angles { get; private set; }
        public bool IsDragging { get; private set; }
        public bool IsPinching { get; private set; }
        private bool initialized;
        private int pointerId;
        private Vector2 lastLocal;
        readonly Dictionary<int,Vector2> touches = new Dictionary<int,Vector2>();
        float pinchDistance;
        bool orbitNeedsBaseline;

        private void OnEnable()
        {
            if(zoomLayout==null)zoomLayout=GetComponent<PrototypeGardenPreviewLayout>();
            if (!initialized) { Angles = defaultAngles; initialized = true; }
            Apply(Angles);
        }
        private void OnDisable()
        {
            CancelInteraction();
            // Mission/result reuse this render texture, with their default composition.
            Apply(defaultAngles);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteraction(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelInteraction(); }

        void Update()
        {
            // The second finger can land on a UI control, whose pointer events do
            // not reach this image. Suspend orbit without treating that as a pinch.
            if(IsDragging && touches.Count<2 && ActiveTouchCount()>1)SuspendOrbit();
        }

        void CancelInteraction()
        { IsDragging=false;IsPinching=false;orbitNeedsBaseline=false;touches.Clear();pinchDistance=0; }
        void SuspendOrbit()
        { IsDragging=false;orbitNeedsBaseline=true; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if(!CanInteract(eventData) || !IsTouch(eventData) || !Contains(eventData,eventData.position)
                || !OwnsRaycast(eventData.pointerPressRaycast))return;
            touches[eventData.pointerId]=eventData.position;
            if(touches.Count>=2){SuspendOrbit();BeginPinch();}
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool wasPinching=IsPinching;
            touches.Remove(eventData.pointerId);
            if(touches.Count>=2){BeginPinch();return;}
            IsPinching=false;pinchDistance=0;
            if(wasPinching && touches.Count==1)
            {
                foreach(var touch in touches){pointerId=touch.Key;break;}
                // The first subsequent motion supplies a fresh orbit baseline.
                orbitNeedsBaseline=true;IsDragging=false;
            }
            else if(pointerId==eventData.pointerId)IsDragging=false;
        }

        void BeginPinch()
        {
            IsPinching=touches.Count==2 && ActiveTouchCount()<=2;
            pinchDistance=IsPinching?TouchDistance():0;
        }

        float TouchDistance()
        {
            Vector2 first=Vector2.zero;bool hasFirst=false;
            foreach(var touch in touches)
            {
                if(hasFirst)return Vector2.Distance(first,touch.Value);
                first=touch.Value;hasFirst=true;
            }
            return 0;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || previewCamera == null || IsDragging
                || eventData.button != PointerEventData.InputButton.Left) return;
            if(!Contains(eventData,eventData.pressPosition) || !OwnsRaycast(eventData.pointerPressRaycast))return;
            pointerId=eventData.pointerId;
            if(touches.Count>=2 || ActiveTouchCount()>1){SuspendOrbit();return;}
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                orbitNeedsBaseline?eventData.position:eventData.pressPosition, eventData.pressEventCamera, out lastLocal)) return;
            pointerId = eventData.pointerId;
            IsDragging = true;orbitNeedsBaseline=false;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if(!CanInteract(eventData))return;
            if(touches.ContainsKey(eventData.pointerId))touches[eventData.pointerId]=eventData.position;
            if(touches.Count>=2)
            {
                SuspendOrbit();
                if(touches.Count!=2 || ActiveTouchCount()>2){IsPinching=false;pinchDistance=0;return;}
                float separation=TouchDistance();
                if(!IsPinching){BeginPinch();return;}
                SetGestureZoom(ZoomFromPinch(CurrentZoom,pinchDistance,separation));
                pinchDistance=separation;return;
            }
            if(ActiveTouchCount()>1){SuspendOrbit();return;}
            if(pointerId!=eventData.pointerId)return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                eventData.pressEventCamera, out Vector2 local)) return;
            if(orbitNeedsBaseline){lastLocal=local;orbitNeedsBaseline=false;IsDragging=true;return;}
            if(!IsDragging)return;
            Angles = DragAngles(Angles, local - lastLocal, rect.rect.size);
            lastLocal = local;
            Apply(Angles);
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (pointerId == eventData.pointerId) IsDragging = false;
        }
        public void OnScroll(PointerEventData eventData)
        {
            if(!CanInteract(eventData) || !Contains(eventData,eventData.position) || !OwnsRaycast(eventData.pointerCurrentRaycast)
                || touches.Count>=2 || ActiveTouchCount()>1)return;
            float ticks=eventData.scrollDelta.y;
#if ENABLE_INPUT_SYSTEM
            if(EventSystem.current!=null && EventSystem.current.currentInputModule is InputSystemUIInputModule module
                && module.scrollDeltaPerTick>0)ticks/=module.scrollDeltaPerTick;
#endif
            SetGestureZoom(ZoomFromWheel(CurrentZoom,ticks));
        }

        float CurrentZoom
        {
            get
            {
                if(zoomLayout==null)zoomLayout=GetComponent<PrototypeGardenPreviewLayout>();
                return zoomLayout!=null?zoomLayout.Zoom:.85f;
            }
        }
        void SetGestureZoom(float value)
        {
            if(zoomLayout==null)zoomLayout=GetComponent<PrototypeGardenPreviewLayout>();
            if(zoomLayout==null || !zoomLayout.isActiveAndEnabled)return;
            zoomLayout.SetZoom(value);Apply(Angles);
        }
        bool CanInteract(PointerEventData data)=>isActiveAndEnabled && previewCamera!=null && data!=null
            && data.button==PointerEventData.InputButton.Left;
        bool Contains(PointerEventData data,Vector2 point)=>RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)transform,point,data.pressEventCamera??data.enterEventCamera);
        bool OwnsRaycast(RaycastResult hit)=>hit.gameObject==null || hit.gameObject.GetComponentInParent<PrototypeIslandDrag>()==this;
        static bool IsTouch(PointerEventData data)
        {
#if ENABLE_INPUT_SYSTEM
            if(data is ExtendedPointerEventData extended)return extended.pointerType==UIPointerType.Touch;
#endif
            return data.pointerId>=0;
        }
        static int ActiveTouchCount()
        {
            int count=0;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)
            {
                foreach(var touch in Touchscreen.current.touches)if(touch.press.isPressed)count++;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            count=Mathf.Max(count,UnityEngine.Input.touchCount);
#endif
            return count;
        }
        public static float ZoomFromWheel(float current,float ticks)
        {
            if(float.IsNaN(ticks) || float.IsInfinity(ticks))return current;
            return PrototypeGardenPreviewLayout.ZoomForScale(PrototypeGardenPreviewLayout.ZoomScale(current)
                * Mathf.Pow(1.18f,Mathf.Clamp(ticks,-12,12)));
        }
        public static float ZoomFromPinch(float current,float previousDistance,float currentDistance)
        {
            if(previousDistance<=.001f || currentDistance<=.001f || float.IsNaN(previousDistance)
                || float.IsNaN(currentDistance) || float.IsInfinity(previousDistance) || float.IsInfinity(currentDistance))return current;
            return PrototypeGardenPreviewLayout.ZoomForScale(PrototypeGardenPreviewLayout.ZoomScale(current)
                * currentDistance/previousDistance);
        }
        public void SetFocus(Vector3 value)
        {
            focus = value;
            if (!initialized) { Angles = defaultAngles; initialized = true; }
            Apply(Angles, false);
        }
        public void ResetView()
        {
            initialized = true;
            CancelInteraction();
            Angles = defaultAngles;
            Apply(defaultAngles);
        }
        public static Vector2 DragAngles(Vector2 angles, Vector2 delta, Vector2 size)
        {
            if (size.x <= 0 || size.y <= 0) return angles;
            return new Vector2(Mathf.Repeat(angles.x + delta.x / size.x * 360 + 180, 360) - 180,
                Mathf.Clamp(angles.y - delta.y / size.y * 180, -PitchLimit, PitchLimit));
        }
        private void Apply(Vector2 angles, bool render = true)
        {
            if (previewCamera == null) return;
            Quaternion rotation = Quaternion.Euler(angles.y, angles.x, 0);
            previewCamera.transform.SetPositionAndRotation(focus + rotation * (Vector3.back * distance), rotation);
            // The integrated home keeps its camera disabled between changes.
            if (render && Application.isPlaying && !previewCamera.enabled && previewCamera.gameObject.activeInHierarchy)
                previewCamera.Render();
        }
    }
}
