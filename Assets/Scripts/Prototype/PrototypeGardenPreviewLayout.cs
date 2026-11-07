using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CapstoneDesign.Prototype
{
    // Fit the island between UI surfaces; the integrated home can extend its sky
    // beyond that composition area to every edge of the canvas.
    [ExecuteAlways, DefaultExecutionOrder(-20), RequireComponent(typeof(RawImage))]
    public sealed class PrototypeGardenPreviewLayout : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public RectTransform upperBoundary, lowerBoundary;
        public PrototypeIslandDrag orbit;
        public Transform gardenRoot;
        // The integrated home replaces these serialized assets at runtime. The
        // standalone prototype still measures all its stages for stable framing.
        [System.NonSerialized] public Transform excludedBoundsRoot;
        public float horizontalPadding = 8;
        public float boundaryGap = 12;
        [Range(0, .2f)] public float edgeMargin = .02f;
        private bool boundsReady;
        private Vector3 measuredFocus;
        private Transform measuredRoot;
        private float horizontalRadius, radius;
        private bool fullScreen;
        private RawImage image;
        private Canvas backgroundCanvas;
        private RenderTexture ownedTexture, originalTexture;
        public bool IsFullScreen => fullScreen;
        public bool IsSubjectFocused { get; private set; }
        public float Zoom { get; private set; } = .85f;
        private Transform subject, subjectBase;
        private Vector3 overviewFocus;
        private bool hasOverviewFocus;
        readonly PrototypeTapGesture memoTap=new PrototypeTapGesture();
        public Func<Ray,string> MemoRayPicker {get;set;}
        public Func<bool> MemoPickingAllowed {get;set;}
        public Func<bool> InputBlocked {get;set;}
        public event Action<string> MemoClicked;

        // View-only state: switching/zooming never changes a saved plant recipe.
        public void SetSubject(Transform plant, Transform planter)
        {
            if (subject == plant && subjectBase == planter) return;
            subject = plant; subjectBase = planter;
            if (!hasOverviewFocus && orbit != null) { overviewFocus = orbit.focus; hasOverviewFocus = true; }
            RecalculateBounds();
        }

        public void SetSubjectFocus(bool focused)
        {
            if (IsSubjectFocused == focused) return;
            IsSubjectFocused = focused;
            RecalculateBounds();
            Apply();
        }

        public void SetZoom(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            Zoom = Mathf.Clamp01(value);
            Apply();
        }
        public static float ZoomScale(float value)
        {
            if(float.IsNaN(value) || float.IsInfinity(value))return 1;
            value=Mathf.Clamp01(value);
            return value<=.85f?Mathf.Lerp(.5f,1,value/.85f):Mathf.Lerp(1,2.25f,(value-.85f)/.15f);
        }
        public static float ZoomForScale(float scale)
        {
            if(float.IsNaN(scale) || float.IsInfinity(scale))return .85f;
            return scale<=1?Mathf.Clamp01((scale-.5f)/.5f)*.85f:
                .85f+Mathf.Clamp01((scale-1)/1.25f)*.15f;
        }
        public Rect VisibleAreaInViewport { get; private set; } = new Rect(0, 0, 1, 1);

        private void OnEnable()
        {
            boundsReady = false;
            Apply();
        }
        // SRP can rebuild canvases inside Camera.Render. Camera projection and
        // target textures must never change in preWillRenderCanvases callbacks.
        // Run before the sky's LateUpdate; it owns the periodic render request.
        private void LateUpdate() {memoTap.Poll();if(InputBlocked?.Invoke()==true)memoTap.Cancel();Apply();}
        private void OnValidate() => boundsReady = false;
        private void OnDisable()=>memoTap.Clear();
        private void OnApplicationFocus(bool focused){if(!focused)memoTap.Clear();}
        private void OnApplicationPause(bool paused){if(paused)memoTap.Clear();}

        bool CanPick=>isActiveAndEnabled && gameObject.activeInHierarchy && InputBlocked?.Invoke()!=true
            && MemoRayPicker!=null && MemoPickingAllowed?.Invoke()==true
            && orbit!=null && orbit.enabled && orbit.previewCamera!=null;
        bool OwnsSurface(RaycastResult hit)=>hit.gameObject==null || hit.gameObject==gameObject;
        bool Inside(PointerEventData data)
        {
            if(image==null)image=GetComponent<RawImage>();
            return data!=null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,data.position,data.pressEventCamera)
                && image.Raycast(data.position,data.pressEventCamera);
        }
        public void OnPointerDown(PointerEventData data)
        {if(CanPick && data!=null && OwnsSurface(data.pointerPressRaycast) && Inside(data))memoTap.Down(data);}
        public void OnPointerUp(PointerEventData data)
        {
            bool tap=memoTap.Up(data);
            if(tap && CanPick && OwnsSurface(data.pointerCurrentRaycast) && Inside(data)
                && TryPickMemo(data.position,data.pressEventCamera,out var id))MemoClicked?.Invoke(id);
        }
        public void OnBeginDrag(PointerEventData data)=>memoTap.Cancel();
        public void OnDrag(PointerEventData data)=>memoTap.Cancel();
        public void OnEndDrag(PointerEventData data)=>memoTap.Cancel();
        public void OnScroll(PointerEventData data)=>memoTap.Cancel();

        public bool TryPickMemo(Vector2 screenPoint,Camera eventCamera,out string memoId)
        {
            memoId=null;
            if(!CanPick || !RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screenPoint,eventCamera,out var local))return false;
            var rect=((RectTransform)transform).rect;
            if(!rect.Contains(local) || rect.width<=0 || rect.height<=0)return false;
            if(image==null)image=GetComponent<RawImage>();
            if(!image.Raycast(screenPoint,eventCamera))return false;
            var uv=image.uvRect;
            var point=new Vector2(uv.x+(local.x-rect.xMin)/rect.width*uv.width,uv.y+(local.y-rect.yMin)/rect.height*uv.height);
            if(point.x<0 || point.x>1 || point.y<0 || point.y>1)return false;
            // ViewportPointToRay respects the home's shifted projection matrix.
            var ray=orbit.previewCamera.ViewportPointToRay(point);
            memoId=MemoRayPicker(ray);
            return !string.IsNullOrEmpty(memoId);
        }

        public void UseFullScreenBackground(Canvas canvas)
        {
            if (fullScreen || canvas == null) return;
            fullScreen = true;
            backgroundCanvas = canvas;
            image = GetComponent<RawImage>();
            transform.SetParent(canvas.transform, false);
            // The original paper backdrop remains underneath this home-only sky.
            transform.SetSiblingIndex(1);
            var rounded = GetComponent<PrototypeRoundedPreview>();
            if (rounded != null) rounded.enabled = false;
            image.raycastTarget = true;
            horizontalPadding = 48;
            boundaryGap = 28;
            Apply();
        }

        public void UseCardSurface()
        {
            if (fullScreen) return;
            horizontalPadding = 48;
            boundaryGap = 24;
            if (GetComponent<PrototypeRoundedPreview>() == null) gameObject.AddComponent<PrototypeRoundedPreview>();
            Apply();
        }

        public void Apply()
        {
            if (upperBoundary == null || lowerBoundary == null || orbit == null || gardenRoot == null) return;
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            float upper = parent.InverseTransformPoint(upperBoundary.TransformPoint(new Vector3(0, upperBoundary.rect.yMin, 0))).y;
            float lower = parent.InverseTransformPoint(lowerBoundary.TransformPoint(new Vector3(0, lowerBoundary.rect.yMax, 0))).y;
            float unitScale = Mathf.Abs(parent.InverseTransformVector(upperBoundary.TransformVector(Vector3.right)).x);
            if (!TryGetArea(parent.rect, upper, lower, horizontalPadding * (fullScreen ? unitScale : 1),
                boundaryGap * (fullScreen ? unitScale : 1), out Rect area)) return;
            Rect display = fullScreen ? parent.rect : area;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var position = display.center - parent.rect.center;
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != display.size) rect.sizeDelta = display.size;

            // Only the visible preview controls the shared camera/texture.
            if (!isActiveAndEnabled || orbit.previewCamera == null || !orbit.previewCamera.orthographic) return;
            if (!boundsReady || measuredRoot != gardenRoot || measuredFocus != orbit.focus) RecalculateBounds();
            if (!boundsReady) return;
            float aspect = display.width / display.height;
            if (!TryGetComposition(display, area, horizontalRadius, radius, edgeMargin,
                out float size, out Vector2 projectionCenter)) return;
            // .85 is the original complete-garden composition. Gestures can
            // zoom out to half scale or move closer for details without changing
            // the stored plant, camera orbit or the initial fit.
            float zoomScale=ZoomScale(Zoom);
            size/=zoomScale;projectionCenter/=zoomScale;
            VisibleAreaInViewport = new Rect((area.xMin - display.xMin) / display.width,
                (area.yMin - display.yMin) / display.height, area.width / display.width, area.height / display.height);
            var camera = orbit.previewCamera;
            // The RT is shared; override projection aspect to match the final UI rectangle.
            // This compensates the RawImage stretch without resizing/allocating a texture each frame.
            if (Mathf.Abs(camera.aspect - aspect) > .00001f) camera.aspect = aspect;
            if (Mathf.Abs(camera.orthographicSize - size) > .00001f) camera.orthographicSize = size;
            if (fullScreen)
            {
                var projection = Matrix4x4.Ortho(-size * aspect + projectionCenter.x, size * aspect + projectionCenter.x,
                    -size + projectionCenter.y, size + projectionCenter.y, camera.nearClipPlane, camera.farClipPlane);
                if (camera.projectionMatrix != projection) camera.projectionMatrix = projection;
                EnsureTexture(camera);
            }
        }

        bool EnsureTexture(Camera camera)
        {
            var pixels = backgroundCanvas.pixelRect.size;
            float scale = Mathf.Min(1, 1600 / Mathf.Max(1, Mathf.Max(pixels.x, pixels.y)));
            int width = Mathf.Max(1, Mathf.RoundToInt(pixels.x * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(pixels.y * scale));
            if (ownedTexture != null && ownedTexture.width == width && ownedTexture.height == height) return false;
            if (originalTexture == null) originalTexture = camera.targetTexture;
            var previous = ownedTexture;
            // URP inherits the external target format for its intermediate color
            // buffer. Preserve HDR highlights until bloom and tone mapping finish.
            ownedTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
                { name = "Home background", antiAliasing = 1 };
            camera.targetTexture = ownedTexture;
            image.texture = ownedTexture;
            if (previous != null) { previous.Release(); if (Application.isPlaying) Destroy(previous); else DestroyImmediate(previous); }
            return true;
        }

        void OnDestroy()
        {
            if (ownedTexture == null) return;
            if (orbit != null && orbit.previewCamera != null && orbit.previewCamera.targetTexture == ownedTexture)
            { orbit.previewCamera.targetTexture = originalTexture; orbit.previewCamera.ResetProjectionMatrix(); }
            ownedTexture.Release();
            if (Application.isPlaying) Destroy(ownedTexture); else DestroyImmediate(ownedTexture);
        }

        public void RecalculateBounds()
        {
            boundsReady = false;
            horizontalRadius = radius = 0;
            if (gardenRoot == null || orbit == null) return;
            if (subject != null && IsSubjectFocused)
            {
                // A minimum envelope keeps the seed/sprout recognizably small
                // instead of magnifying it to the size of a mature flower.
                var seedBase = subject.position;
                var bounds = new Bounds(seedBase + Vector3.up * .35f, new Vector3(.60f, .80f, .60f));
                Encapsulate(subject, ref bounds);
                if (subjectBase != null) Encapsulate(subjectBase, ref bounds);
                orbit.SetFocus(bounds.center);
                Measure(subject);
                if (subjectBase != null) Measure(subjectBase);
                horizontalRadius = Mathf.Max(horizontalRadius, .32f);
                radius = Mathf.Max(radius, .48f);
            }
            else
            {
                if (subject != null && hasOverviewFocus) orbit.SetFocus(overviewFocus);
                Measure(gardenRoot);
            }
            measuredRoot = gardenRoot; measuredFocus = orbit.focus;
            boundsReady = radius > 0;
        }

        static void Encapsulate(Transform root, ref Bounds bounds)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    foreach (var vertex in filter.sharedMesh.vertices)
                        bounds.Encapsulate(filter.transform.TransformPoint(vertex));
        }

        void Measure(Transform root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (excludedBoundsRoot != null && filter.transform.IsChildOf(excludedBoundsRoot)) continue;
                if (filter.sharedMesh == null) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var delta = filter.transform.TransformPoint(vertex) - orbit.focus;
                    horizontalRadius = Mathf.Max(horizontalRadius, new Vector2(delta.x, delta.z).magnitude);
                    radius = Mathf.Max(radius, delta.magnitude);
                }
            }
        }

        public static bool TryGetArea(Rect parent, float upper, float lower, float padding, float gap, out Rect area)
        {
            padding = Mathf.Max(0, padding); gap = Mathf.Max(0, gap);
            area = Rect.MinMaxRect(parent.xMin + padding, Mathf.Max(parent.yMin, lower + gap),
                parent.xMax - padding, Mathf.Min(parent.yMax, upper - gap));
            return area.width > 0 && area.height > 0;
        }

        public static bool TryGetSize(float aspect, float horizontalRadius, float radius, float margin, out float size)
        {
            size = 0;
            if (aspect <= 0 || horizontalRadius <= 0 || radius <= 0 || float.IsNaN(aspect) || float.IsInfinity(aspect)) return false;
            // Yaw changes camera right in XZ only; pitch does not change it. The full radius
            // bounds camera up at every pitch. Use both bounds without unnecessary square padding.
            size = Mathf.Max(radius, horizontalRadius / aspect) / (1 - 2 * Mathf.Clamp(margin, 0, .2f));
            return true;
        }

        public static bool TryGetComposition(Rect display, Rect visible, float horizontalRadius, float radius,
            float margin, out float size, out Vector2 projectionCenter)
        {
            size = 0; projectionCenter = Vector2.zero;
            if (display.width <= 0 || display.height <= 0 || visible.width <= 0 || visible.height <= 0
                || !TryGetSize(visible.width / visible.height, horizontalRadius, radius, margin, out float fitted)) return false;
            size = fitted * display.height / visible.height;
            projectionCenter = (display.center - visible.center) * (2 * size / display.height);
            return true;
        }
    }
    // Shared tap discrimination for the garden and saved-photo surfaces. Once a
    // gesture becomes drag/pinch/scroll it cannot turn into a tap on finger-up.
    public sealed class PrototypeTapGesture
    {
        sealed class Press {public Vector2 Start;public bool Cancelled;}
        readonly Dictionary<int,Press> presses=new Dictionary<int,Press>();
        public void Down(PointerEventData data)
        {
            if(data==null || data.button!=PointerEventData.InputButton.Left)return;
            bool multiple=presses.Count>0 || ActiveTouches()>1;if(multiple)Cancel();
            presses[data.pointerId]=new Press {Start=data.position,Cancelled=multiple};
        }
        public void Move(PointerEventData data)
        {
            if(data==null || !presses.TryGetValue(data.pointerId,out var press))return;
            float threshold=EventSystem.current==null?10:Mathf.Max(1,EventSystem.current.pixelDragThreshold);
            if((data.position-press.Start).sqrMagnitude>threshold*threshold)press.Cancelled=true;
            Poll();
        }
        public bool Up(PointerEventData data)
        {
            Move(data);
            if(data==null || !presses.TryGetValue(data.pointerId,out var press))return false;
            presses.Remove(data.pointerId);return !press.Cancelled && data.button==PointerEventData.InputButton.Left;
        }
        public void Poll(){if(ActiveTouches()>1)Cancel();}
        public void Cancel(){foreach(var press in presses.Values)press.Cancelled=true;}
        public void Clear()=>presses.Clear();
        static int ActiveTouches()
        {
            int count=0;
#if ENABLE_INPUT_SYSTEM
            if(Touchscreen.current!=null)foreach(var touch in Touchscreen.current.touches)if(touch.press.isPressed)count++;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            count=Mathf.Max(count,UnityEngine.Input.touchCount);
#endif
            return count;
        }
    }
}
