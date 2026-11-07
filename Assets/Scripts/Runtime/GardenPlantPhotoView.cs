using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Prototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CapstoneDesign.Runtime
{
    // Album views own only their image and detached recipe. All images are rendered
    // on demand by one temporary studio, never by one camera/mesh per album entry.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RawImage))]
    public sealed class GardenPlantPhotoView : MonoBehaviour, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler
    {
        static readonly HashSet<GardenPlantPhotoView> active = new HashSet<GardenPlantPhotoView>();
        static PhotoStudio studio;
        static int ownedTextures;
        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        readonly HashSet<int> draggingPointers = new HashSet<int>();
        readonly PrototypeTapGesture memoTap = new PrototypeTapGesture();
        GardenMemoryPicker.Snapshot memoPicking;
        Vector3 pickOrigin, pickDirection, pickHorizontal, pickVertical;
        public event Action<string> MemoClicked;
        PlantRecipe recipe;
        GardenPlantMemo[] memos = Array.Empty<GardenPlantMemo>();
        PlantMemoryLeaf[] memoryLeaves = Array.Empty<PlantMemoryLeaf>();
        public int MemoryPage { get; private set; }
        public int MemoryPageCount => Math.Max(1, memoryLeaves.Select(m => m.Slot / PlantMemoryGeometry.PageSize + 1).DefaultIfEmpty(1).Max());
        string signature;
        float scale, pitch = 8, nextRender;
        bool dirty, registered;
        int renderedMinute = -1;
        RawImage image;
        RenderTexture texture;
        public bool IsInteractive { get; private set; }
        public float Zoom { get; private set; } = 1;
        public float Yaw { get; private set; } = -18;
        public float Pitch => pitch;
        public string EnvironmentKey => recipe == null ? "" : "album-nature-v1:" + (int)PlantAlbumEnvironment.Select(recipe.InstanceId, recipe.BaseSeed);
        public float LastRenderedEnvironmentOpacity { get; private set; }
        public static int OwnedEnvironmentMeshCount => studio?.EnvironmentMeshCount ?? 0;
        public static int EnvironmentBuildCount => studio?.EnvironmentBuildCount ?? 0;
        // User-facing elevation 90 degrees is the horizontal view (orbit pitch 0).
        // Keep the whole field opaque above that boundary. Below it, 90 -> 60
        // degrees (pitch 0 -> -30) fades the entire surface smoothly from 1 to 0.
        public static float EnvironmentOpacity(float angle) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-30, 0, angle));
        public int RenderCount { get; private set; }
        public double LastRenderedHour { get; private set; }
        public float LastRenderedDaylight { get; private set; }
        public bool UsesGrassSurface => studio != null && studio.HasGrassSurface;
        public static int ActiveViewCount => active.Count;
        public static int SharedCameraCount => studio == null ? 0 : 1;
        public static int OwnedTextureCount => ownedTextures;
        public int MemoCount => memoryLeaves.Count(m => m.Linked);
        public IReadOnlyList<string> RenderedMemoIds { get; private set; } = Array.Empty<string>();
        public Bounds RenderedMemoBounds { get; private set; }
        public string MemoRepresentationKey => GardenMemoryPicker.RepresentationKey;

        public void Initialize(PlantRecipe savedRecipe, float savedScale, bool interactive, IReadOnlyList<GardenPlantMemo> savedMemos = null,
            IReadOnlyList<PlantMemoryLeaf> savedLeaves = null, int memoryPage = 0)
        {
            if (savedRecipe == null) throw new ArgumentNullException(nameof(savedRecipe));
            if (float.IsNaN(savedScale) || float.IsInfinity(savedScale) || savedScale <= 0)
                throw new ArgumentOutOfRangeException(nameof(savedScale));
            PlantGenerator.Validate(savedRecipe);
            recipe = GardenPlantVisuals.CopyRecipe(savedRecipe);
            memos = new GardenPlantMemo[savedMemos?.Count ?? 0];
            for (int i = 0; i < memos.Length; i++) memos[i] = savedMemos[i].Copy();
            memoryLeaves = savedLeaves == null ? memos.Select((m, index) => new PlantMemoryLeaf {
                OrganId = m.Id, Slot = index, Linked = true }).ToArray() : savedLeaves.Select(m => new PlantMemoryLeaf {
                OrganId = m.OrganId, Slot = m.Slot, Linked = m.Linked }).ToArray();
            MemoryPage = Mathf.Clamp(memoryPage, 0, MemoryPageCount - 1);
            RenderedMemoIds = Array.Empty<string>(); RenderedMemoBounds = default;
            signature = PlantCodec.Encode(recipe);
            scale = savedScale;
            IsInteractive = interactive;
            image = GetComponent<RawImage>();
            image.raycastTarget = interactive;
            image.color = Color.white;
            Register();
            ResetView();
        }

        void Register()
        {
            if (registered || recipe == null || !isActiveAndEnabled) return;
            registered = true; active.Add(this);
        }
        void OnEnable()
        {
            GardenVisualClock.Changed -= RefreshClock;
            GardenVisualClock.Changed += RefreshClock;
            Register(); dirty = true;
        }
        void RefreshClock() { dirty = true; nextRender = 0; }
        void OnDisable()
        {
            GardenVisualClock.Changed -= RefreshClock;
            pointers.Clear(); draggingPointers.Clear();
            memoTap.Clear(); memoPicking=null;
            ReleaseTexture();
            if (registered) { registered = false; active.Remove(this); }
            if (active.Count == 0 && studio != null) { studio.Dispose(); studio = null; }
        }
        void OnDestroy() => OnDisable();
        void OnRectTransformDimensionsChange() { dirty = true; }
        void OnApplicationPause(bool paused)
        {
            memoTap.Clear();
            pointers.Clear(); draggingPointers.Clear();
            if (!paused) { dirty = true; nextRender = 0; }
        }
        void OnApplicationFocus(bool focused)
        {
            memoTap.Clear();
            pointers.Clear(); draggingPointers.Clear();
            if (focused) { dirty = true; nextRender = 0; }
        }

        void LateUpdate()
        {
            memoTap.Poll();
            if(GardenMemoPopup.IsOpen){memoTap.Cancel();pointers.Clear();draggingPointers.Clear();}
            if (renderedMinute != GardenVisualClock.CurrentMinute) dirty = true;
            if (texture != null && !texture.IsCreated()) dirty = true;
            if (!dirty || recipe == null || Time.unscaledTime < nextRender || !Visible()) return;
            RenderNow();
            nextRender = Time.unscaledTime + 1f / 30;
        }
        bool Visible()
        {
            if (image == null || image.canvasRenderer.cull) return false;
            var viewport = GetComponentInParent<ScrollRect>();
            if (viewport == null || viewport.viewport == null) return true;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport.viewport, transform);
            return viewport.viewport.rect.Overlaps(new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
        }

        public bool RenderNow()
        {
            if (!isActiveAndEnabled || recipe == null) return false;
            Register();
            var rect = ((RectTransform)transform).rect;
            float aspect = rect.width > 0 && rect.height > 0 ? rect.width / rect.height : 1;
            int edge = IsInteractive ? 768 : 256;
            int width = Mathf.Max(32, Mathf.RoundToInt(edge * Mathf.Min(1, aspect)));
            int height = Mathf.Max(32, Mathf.RoundToInt(edge / Mathf.Max(1, aspect)));
            if (texture == null || texture.width != width || texture.height != height)
            {
                ReleaseTexture();
                texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { name = "Plant album image", hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1, filterMode = FilterMode.Bilinear };
                texture.Create(); ownedTextures++;
                image.texture = texture;
            }
            else if (!texture.IsCreated()) texture.Create();
            if (studio == null) studio = new PhotoStudio();
            LastRenderedHour = GardenVisualClock.CurrentHour;
            var skyTime = GardenSkyEnvironment.EvaluateTime(LastRenderedHour);
            studio.Render(recipe, signature, scale, memoryLeaves, MemoryPage, texture, Yaw, pitch, Zoom, skyTime, IsInteractive);
            LastRenderedEnvironmentOpacity = EnvironmentOpacity(pitch);
            memoPicking=IsInteractive?studio.Picking:null;
            studio.GetPickingRays(out pickOrigin,out pickDirection,out pickHorizontal,out pickVertical);
            RenderedMemoIds = studio.MemoIds;
            RenderedMemoBounds = studio.MemoBounds;
            LastRenderedDaylight = skyTime.Daylight;
            renderedMinute = GardenVisualClock.CurrentMinute;
            dirty = false; RenderCount++;
            return true;
        }

        public void ResetView() { Yaw = -18; pitch = 8; Zoom = 1; dirty = true; }
        public void SetMemoryPage(int page)
        {
            int next = Mathf.Clamp(page, 0, MemoryPageCount - 1);
            if (next == MemoryPage) return;
            MemoryPage = next; memoPicking = null; dirty = true;
        }
        public void Rotate(float degrees)
        {
            if (!IsInteractive || float.IsNaN(degrees) || float.IsInfinity(degrees)) return;
            Yaw = Mathf.Repeat(Yaw + degrees + 180, 360) - 180; dirty = true;
        }
        public void SetZoom(float value)
        {
            if (!IsInteractive || float.IsNaN(value) || float.IsInfinity(value)) return;
            Zoom = Mathf.Clamp(value, .75f, 1.65f); dirty = true;
        }
        bool Local(PointerEventData data, out Vector2 point)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, data.position, data.pressEventCamera, out point);
        public void OnPointerDown(PointerEventData data)
        {
            if (!CanUsePointer(data) || !OwnsSurface(data.pointerPressRaycast) || !Inside(data))return;
            if(Local(data,out var point)) {pointers[data.pointerId]=point;memoTap.Down(data);}
        }
        public void OnPointerUp(PointerEventData data)
        {
            bool tap=memoTap.Up(data);
            pointers.Remove(data.pointerId); draggingPointers.Remove(data.pointerId);
            if(tap && CanUsePointer(data) && OwnsSurface(data.pointerCurrentRaycast) && Inside(data)
                && TryPickMemo(data.position,data.pressEventCamera,out var id))MemoClicked?.Invoke(id);
        }
        public void OnBeginDrag(PointerEventData data) { memoTap.Cancel(); if (CanUsePointer(data)) draggingPointers.Add(data.pointerId); }
        public void OnDrag(PointerEventData data)
        {
            memoTap.Cancel();
            if (!CanUsePointer(data) || !draggingPointers.Contains(data.pointerId) || !Local(data, out var point)) return;
            if (!pointers.TryGetValue(data.pointerId, out var previous)) { pointers[data.pointerId] = point; return; }
            if (pointers.Count > 1)
            {
                foreach (var other in pointers)
                    if (other.Key != data.pointerId)
                    {
                        float before = Vector2.Distance(previous, other.Value), after = Vector2.Distance(point, other.Value);
                        if (before > 1 && after > 1) SetZoom(Zoom * after / before);
                        break;
                    }
            }
            else
            {
                var size = ((RectTransform)transform).rect.size;
                if (size.x > 0 && size.y > 0)
                {
                    // Use the garden's yaw wrapping, pitch limits and drag sensitivity.
                    var angles = PrototypeIslandDrag.DragAngles(new Vector2(Yaw, pitch), point - previous, size);
                    Yaw = angles.x; pitch = angles.y; dirty = true;
                }
            }
            pointers[data.pointerId] = point;
            data.Use();
        }
        public void OnEndDrag(PointerEventData data) { memoTap.Cancel(); draggingPointers.Remove(data.pointerId); pointers.Remove(data.pointerId); }
        public void OnScroll(PointerEventData data)
        {
            memoTap.Cancel();
            if (!CanUsePointer(data)) return;
            float ticks = data.scrollDelta.y;
#if ENABLE_INPUT_SYSTEM
            if (EventSystem.current != null && EventSystem.current.currentInputModule is InputSystemUIInputModule module
                && module.scrollDeltaPerTick > 0) ticks /= module.scrollDeltaPerTick;
#endif
            SetZoom(Zoom * Mathf.Exp(ticks * .10f)); data.Use();
        }

        bool CanUsePointer(PointerEventData data)=>IsInteractive && isActiveAndEnabled && gameObject.activeInHierarchy
            && !GardenMemoPopup.IsOpen && data!=null && data.button==PointerEventData.InputButton.Left;
        bool OwnsSurface(RaycastResult hit)=>hit.gameObject==null || hit.gameObject==gameObject;
        bool Inside(PointerEventData data)=>RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform,data.position,data.pressEventCamera)
            && image!=null && image.Raycast(data.position,data.pressEventCamera);
        public bool TryPickMemo(Vector2 screenPoint,Camera eventCamera,out string memoId)
        {
            memoId=null;
            if(!IsInteractive || !isActiveAndEnabled || !gameObject.activeInHierarchy || GardenMemoPopup.IsOpen
                || memoPicking==null || image==null || texture==null || !texture.IsCreated()
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screenPoint,eventCamera,out var local))return false;
            var rect=((RectTransform)transform).rect;
            if(!rect.Contains(local) || rect.width<=0 || rect.height<=0 || !image.Raycast(screenPoint,eventCamera))return false;
            var uv=image.uvRect;
            var point=new Vector2(uv.x+(local.x-rect.xMin)/rect.width*uv.width,uv.y+(local.y-rect.yMin)/rect.height*uv.height);
            if(point.x<0 || point.x>1 || point.y<0 || point.y>1)return false;
            // Retain each view's eye and unnormalised corner directions while the
            // shared camera renders other photos. Direction vectors avoid losing
            // near-plane precision at the isolated studio's distant world position.
            return memoPicking.TryPick(new Ray(pickOrigin, pickDirection + pickHorizontal * point.x + pickVertical * point.y),out memoId,out _);
        }

        void ReleaseTexture()
        {
            if (texture == null) return;
            if (image != null && image.texture == texture) image.texture = null;
            texture.Release(); Release(texture); texture = null; ownedTextures--;
        }
        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        sealed class PhotoStudio : IDisposable
        {
            const int Layer = 31;
            readonly GameObject root;
            readonly Camera camera;
            readonly Light light;
            readonly ProceduralPlantView plant;
            readonly Dictionary<PlantAlbumEnvironmentKind, Mesh> environments = new Dictionary<PlantAlbumEnvironmentKind, Mesh>();
            readonly MeshFilter environmentFilter;
            readonly MeshRenderer environmentRenderer;
            readonly Material grassMaterial, skyMaterial;
            string renderedSignature;
            string renderedMemories;
            string[] memoIds = Array.Empty<string>();
            Bounds memoBounds;
            float renderedScale;
            Bounds bounds;
            float radius;
            public GardenMemoryPicker.Snapshot Picking {get;private set;}
            Vector3 pickOrigin,pickDirection,pickHorizontal,pickVertical;
            public void GetPickingRays(out Vector3 origin,out Vector3 direction,out Vector3 horizontal,out Vector3 vertical)
            {origin=pickOrigin;direction=pickDirection;horizontal=pickHorizontal;vertical=pickVertical;}
            public bool HasGrassSurface => environmentFilter.sharedMesh != null && grassMaterial != null;
            public int EnvironmentMeshCount => environments.Count;
            public int EnvironmentBuildCount { get; private set; }
            public IReadOnlyList<string> MemoIds => memoIds;
            public Bounds MemoBounds => memoBounds;

            public PhotoStudio()
            {
                root = new GameObject("Plant album shared studio") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                root.transform.position = new Vector3(0, -10000, 0);
                var cameraNode = new GameObject("Plant album shared camera"); cameraNode.transform.SetParent(root.transform, false);
                camera = cameraNode.AddComponent<Camera>(); camera.enabled = false;
                // The same studio camera now gives distant scenery its natural
                // smaller scale and a horizon; garden orbit limits remain unchanged.
                camera.orthographic = false; camera.fieldOfView = 35;
                camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.Skybox;
                var skyShader = Resources.Load<Shader>("GardenAtmosphere/Sky");
                skyMaterial = new Material(skyShader) { name = "Plant album garden-time sky", hideFlags = HideFlags.HideAndDontSave };
                cameraNode.AddComponent<Skybox>().material = skyMaterial;
                camera.cullingMask = 1 << Layer; camera.allowHDR = false; camera.allowMSAA = false;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.volumeLayerMask = 0;
                data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
                var lightNode = new GameObject("Plant album studio light"); lightNode.transform.SetParent(root.transform, false);
                light = lightNode.AddComponent<Light>(); light.type = LightType.Directional;
                light.cullingMask = 1 << Layer; light.shadows = LightShadows.None;
                var grass = new GameObject("Plant album natural environment"); grass.transform.SetParent(root.transform, false);
                grassMaterial = new Material(Resources.Load<Shader>("GardenAtmosphere/AlbumMeadow"))
                    { name = "Plant album grass (owned)", hideFlags = HideFlags.HideAndDontSave };
                environmentFilter = grass.AddComponent<MeshFilter>();
                environmentRenderer = grass.AddComponent<MeshRenderer>(); environmentRenderer.sharedMaterial = grassMaterial;
                environmentRenderer.shadowCastingMode = ShadowCastingMode.Off;
                var plantNode = new GameObject("Saved plant"); plantNode.transform.SetParent(root.transform, false);
                plantNode.transform.localPosition = Vector3.up * .008f;
                plant = plantNode.AddComponent<ProceduralPlantView>();
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                { child.gameObject.layer = Layer; child.gameObject.hideFlags = HideFlags.HideAndDontSave; }
            }

            public void Render(PlantRecipe savedRecipe, string signature, float scale, IReadOnlyList<PlantMemoryLeaf> savedLeaves, int memoryPage,
                RenderTexture target, float yaw, float pitch, float zoom,
                GardenSkyEnvironment.SkyTime skyTime, bool capturePicking)
            {
                var oldSun = RenderSettings.sun;
                var oldAmbient = RenderSettings.ambientProbe;
                bool oldFog = RenderSettings.fog;
                var oldTarget = RenderTexture.active;
                try
                {
                    root.SetActive(true);
                    var environment = PlantAlbumEnvironment.Select(savedRecipe.InstanceId, savedRecipe.BaseSeed);
                    if (!environments.TryGetValue(environment, out var environmentMesh))
                    {
                        // Four frozen presets share one studio. Individual identity
                        // selects a preset; orbit, growth and clock never rebuild it.
                        environmentMesh = PlantAlbumEnvironment.Build(environment, 7919 * ((int)environment + 1));
                        environments.Add(environment, environmentMesh); EnvironmentBuildCount++;
                    }
                    environmentFilter.sharedMesh = environmentMesh;
                    string memorySignature = GardenMemoryPicker.Signature(savedLeaves, memoryPage);
                    bool memoChanged = renderedMemories != memorySignature;
                    bool plantChanged = renderedSignature != signature || renderedScale != scale || memoChanged;
                    if (plantChanged)
                    {
                        plant.Render(savedRecipe, savedLeaves, memoryPage); plant.transform.localScale = Vector3.one * scale;
                        renderedSignature = signature; renderedScale = scale;
                        renderedMemories = memorySignature;
                        memoIds = plant.Geometry.Organs.Where(o => o.Selectable).Select(o => o.OrganId).ToArray();
                    }
                    if (plantChanged || memoChanged)
                    {
                        Picking=null;
                        // The surrounding meadow must not shrink the plant to fit
                        // its landscape. Frame only the plant and a small root area.
                        bounds = new Bounds(root.transform.position + Vector3.up * .18f, new Vector3(.75f, .36f, .75f));
                        foreach (var renderer in plant.GetComponentsInChildren<MeshRenderer>()) bounds.Encapsulate(renderer.bounds);
                        memoBounds = GardenMemoryPicker.Capture(plant).WorldBounds;
                        radius = bounds.extents.magnitude;
                    }
                    if(capturePicking && Picking==null)Picking=GardenMemoryPicker.Capture(plant, root.transform);
                    camera.targetTexture = target; camera.aspect = target.width / (float)target.height;
                    float halfVertical = camera.fieldOfView * Mathf.Deg2Rad * .5f;
                    float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * camera.aspect);
                    float distance = radius / Mathf.Sin(Mathf.Min(halfVertical, halfHorizontal)) * 1.07f / zoom;
                    var rotation = Quaternion.Euler(pitch, yaw, 0);
                    camera.transform.SetPositionAndRotation(bounds.center + rotation * (Vector3.back * distance), rotation);
                    camera.ResetProjectionMatrix();
                    pickOrigin = camera.transform.position;
                    pickHorizontal = camera.transform.right * (2 * Mathf.Tan(halfHorizontal));
                    pickVertical = camera.transform.up * (2 * Mathf.Tan(halfVertical));
                    pickDirection = camera.transform.forward - (pickHorizontal + pickVertical) * .5f;
                    GardenSkyEnvironment.ConfigureSkyMaterial(skyMaterial, skyTime, camera.aspect);
                    grassMaterial.SetColor("_AmbientTint", skyTime.AmbientSky * .6f + skyTime.AmbientEquator * .4f);
                    grassMaterial.SetColor("_LightTint", skyTime.LightColor * skyTime.LightIntensity * Mathf.Lerp(.14f, .48f, skyTime.Daylight));
                    grassMaterial.SetVector("_LightDirection", skyTime.LightDirection);
                    float opacity = EnvironmentOpacity(pitch);
                    environmentRenderer.enabled = opacity > 0;
                    grassMaterial.SetFloat("_EnvironmentOpacity", opacity);
                    grassMaterial.SetFloat("_ZWrite", opacity >= .9999f ? 1 : 0);
                    camera.backgroundColor = skyTime.Background;
                    light.transform.rotation = Quaternion.LookRotation(-skyTime.LightDirection.normalized, Vector3.up);
                    light.color = skyTime.LightColor; light.intensity = skyTime.LightIntensity;
                    var ambient = new SphericalHarmonicsL2();
                    ambient.AddAmbientLight(skyTime.AmbientSky * .4f + skyTime.AmbientEquator * .4f + skyTime.AmbientGround * .2f);
                    RenderSettings.ambientProbe = ambient; RenderSettings.sun = light; RenderSettings.fog = false;
                    camera.Render();
                }
                finally
                {
                    camera.targetTexture = null;
                    root.SetActive(false);
                    RenderSettings.sun = oldSun; RenderSettings.ambientProbe = oldAmbient; RenderSettings.fog = oldFog;
                    RenderTexture.active = oldTarget;
                }
            }
            public void Dispose()
            {
                root.SetActive(false);
                // Runtime-only plant/planter components do not receive all lifecycle
                // callbacks in editor preview scenes. Explicitly release our studio's
                // private graphics resources there as well as destroying its objects.
                if (!Application.isPlaying)
                {
                    foreach (var filter in plant.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null) Release(filter.sharedMesh);
                    foreach (var renderer in plant.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer.sharedMaterial != null) Release(renderer.sharedMaterial);
                }
                foreach (var mesh in environments.Values) Release(mesh);
                environments.Clear();
                Release(grassMaterial); Release(skyMaterial);
                Release(root);
            }

        }
    }
}
