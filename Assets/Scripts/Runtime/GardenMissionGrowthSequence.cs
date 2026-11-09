using System;
using System.Collections.Generic;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime.LocalState;
using CapstoneDesign.Plants;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    // A saved practice result enters here. This presenter never awards growth or
    // writes a save; replacing its effect cannot change the domain transaction.
    [DefaultExecutionOrder(1100), DisallowMultipleComponent]
    public sealed class GardenMissionGrowthSequence : MonoBehaviour
    {
        [SerializeField] GardenGrowthEffect effectPrefab;
        public const float Duration = 4.6f;
        public bool IsPlaying { get; private set; }
        public string Phase { get; private set; } = "idle";
        public float Progress { get; private set; }
        public int PeakParticleCount { get; private set; }
        public bool OrdinaryUiHidden => IsPlaying && canvases.All(item => item.Value == null || !item.Value.enabled);
        public RenderTexture RenderTarget => camera == null ? null : camera.targetTexture;
        readonly List<KeyValuePair<bool, Canvas>> canvases = new List<KeyValuePair<bool, Canvas>>();
        readonly List<KeyValuePair<bool, Behaviour>> suspended = new List<KeyValuePair<bool, Behaviour>>();
        GardenHomePresenter home;
        GardenState after;
        Action completed;
        Camera camera;
        CameraState oldCamera;
        GameObject screen, effects;
        GardenGrowthEffect effect;
        bool visualsWereActive, revealed, restoring;
        float started, yaw, initialSize;
        Vector3 focus, initialFocus;
        RenderTexture oldActive;

        public static bool TryPlay(GardenHomePresenter home, GardenState before, GardenState after, Action finished)
        {
            if (!Application.isPlaying || home == null || home.gardenCamera == null || home.gardenVisuals == null
                || home.gardenCamera.targetTexture == null || !GardenPlantVisuals.HasCurrentPlant(before)
                || !GardenPlantVisuals.HasCurrentPlant(after)) return false;
            bool sameIndividual=before.PlantAppearance.Recipe.InstanceId==after.PlantAppearance.Recipe.InstanceId;
            if(sameIndividual && before.PlantAppearance.Recipe.GrowthStep>=after.PlantAppearance.Recipe.GrowthStep)return false;
            if(!sameIndividual && !after.PracticeGrowthRecords.Any(record=>record.PlantInstanceId==after.PlantAppearance.Recipe.InstanceId
                && record.AfterStep>record.BeforeStep && !before.PracticeGrowthRecords.Any(prior=>prior.SessionId==record.SessionId)))return false;
            var owner = home.gameObject.GetComponent<GardenMissionGrowthSequence>();
            if (owner == null) owner = home.gameObject.AddComponent<GardenMissionGrowthSequence>();
            if (owner.IsPlaying) return false;
            try { owner.Begin(home, before, after, finished); return true; }
            catch (Exception exception)
            {
                Debug.LogException(exception); owner.Restore(); return false;
            }
        }

        void Suspend(Behaviour behaviour)
        {
            if (behaviour == null || suspended.Any(item => item.Value == behaviour)) return;
            suspended.Add(new KeyValuePair<bool, Behaviour>(behaviour.enabled, behaviour));
            behaviour.enabled = false;
        }
        void Begin(GardenHomePresenter presenter, GardenState before, GardenState savedAfter, Action finished)
        {
            home = presenter; after = savedAfter; completed = finished; camera = home.gardenCamera;
            oldCamera = new CameraState(camera); oldActive = RenderTexture.active;
            visualsWereActive = home.gardenVisuals.activeSelf;
            IsPlaying = true; Phase = "swirl"; Progress = 0; PeakParticleCount = 0; revealed = false;
            // Keep all controllers mounted, but stop navigation and raycasts while
            // their canvases are hidden. No OnDisable-driven draft is discarded.
            Suspend(home.navigation); Suspend(home); Suspend(home.activity);
            foreach (var layout in FindObjectsByType<PrototypeGardenPreviewLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (layout.orbit != null && layout.orbit.previewCamera == camera) { Suspend(layout); Suspend(layout.orbit); }
            foreach (var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Suspend(raycaster);
            home.gardenVisuals.SetActive(true);
            bool newIndividual=before.PlantAppearance.Recipe.InstanceId!=after.PlantAppearance.Recipe.InstanceId;
            home.RefreshFromState(newIndividual?after:before);
            if(newIndividual)home.RenderGrowthAppearance(NextPlantBeforeGrowth(after),1.9f);
            Suspend(home.gardenVisuals.GetComponent<GardenSkyEnvironment>());
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                canvases.Add(new KeyValuePair<bool, Canvas>(canvas.enabled, canvas)); canvas.enabled = false;
            }
            EventSystem.current?.SetSelectedGameObject(null);
            screen = new GameObject("Mission growth full-screen garden", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var overlay = screen.GetComponent<Canvas>(); overlay.renderMode = RenderMode.ScreenSpaceOverlay; overlay.sortingOrder = 32760;
            var imageNode = new GameObject("Unobstructed garden", typeof(RectTransform), typeof(RawImage)); imageNode.transform.SetParent(screen.transform, false);
            var image = imageNode.GetComponent<RawImage>(); image.texture = oldCamera.Target; image.color = Color.white; image.raycastTarget = true;
            var rect = (RectTransform)imageNode.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            camera.enabled = false; camera.aspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
            camera.targetTexture = oldCamera.Target;
            var bounds = PlantBounds(); focus = initialFocus = bounds.center;
            initialSize = Mathf.Max(.72f, bounds.extents.magnitude * 1.45f / Mathf.Min(1, camera.aspect));
            yaw = oldCamera.Rotation.eulerAngles.y;
            effects = new GameObject("Mission growth effects (temporary)");
            effects.transform.SetParent(home.gardenVisuals.transform, false);
            effect = effectPrefab != null ? Instantiate(effectPrefab, effects.transform) : effects.AddComponent<GardenPlantEvolutionEffect>();
            effect.Begin(home.ProceduralPlant.transform);
            started = Time.unscaledTime;
            RenderFrame(0);
        }

        Bounds PlantBounds()
        {
            var bounds = new Bounds(home.ProceduralPlant.transform.position + Vector3.up * .25f, Vector3.one * .5f);
            foreach (var renderer in home.ProceduralPlant.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            if (home.Planter != null) foreach (var renderer in home.Planter.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        void LateUpdate()
        {
            if (!IsPlaying || restoring) return;
            try
            {
                float progress = Mathf.Clamp01((Time.unscaledTime - started) / Duration);
                RenderFrame(progress);
                if (progress >= 1) Restore();
            }
            catch (Exception exception) { Debug.LogException(exception); Restore(); }
        }
        // Recreate only the committed new seed's presentation before this mission.
        // Historical stored credits replay in their saved order; no domain state is changed.
        internal static PlantRecipe NextPlantBeforeGrowth(GardenState state)
        {
            var current=state.PlantAppearance.Recipe;
            var recipe=PlantGenerator.Create(state.InstallationId,current.InstanceId,current.MaxGrowthSteps,current.GrowthMode);
            recipe.MeshVersion=current.MeshVersion;
            var command=state.PlantStartCommands.FirstOrDefault(c=>c.PlantInstanceId==current.InstanceId);
            if(command==null)return recipe;
            int offset=state.PlantStartCommands.TakeWhile(c=>c!=command).Sum(c=>c.AppliedGrowthSteps);
            foreach(var credit in state.PracticeGrowthRecords.Where(r=>r.Outcome==PracticeGrowthOutcome.Stored)
                .Skip(offset).Take(command.AppliedGrowthSteps))
            {
                GrowthCategory category;
                if(credit.MissionId==DailyEmotionPolicy.MissionId)category=GrowthCategory.Reflection;
                else if(!GardenPlantVisuals.TryCategory(credit.MissionId,out category))throw new InvalidOperationException("Stored growth category is missing.");
                recipe.Counts[(int)category]++; recipe=PlantGenerator.Advance(recipe);
            }
            return recipe;
        }

        void RenderFrame(float progress)
        {
            Progress = progress;
            if (!revealed && progress >= .56f)
            {
                effect.BeforePlantChanges();
                home.RefreshFromState(after);
                effect.Reveal(home.ProceduralPlant.transform);
                focus = PlantBounds().center; revealed = true;
            }
            Phase = progress < .43f ? "swirl" : progress < .61f ? "transform" : "burst";
            effect.Sample(progress); PeakParticleCount = Mathf.Max(PeakParticleCount, effect.ParticleCount);
            // About five degrees per second, with a gentle start/stop. The same
            // offscreen camera and render target continue to feed a full-screen image.
            float orbit = Mathf.SmoothStep(0, 1, progress) * 24;
            var rotation = Quaternion.Euler(23, yaw + orbit, 0);
            var center = Vector3.Lerp(initialFocus, focus, Mathf.InverseLerp(.56f, .9f, progress));
            camera.transform.SetPositionAndRotation(center + rotation * Vector3.back * 5.8f, rotation);
            camera.orthographicSize = initialSize;
            camera.ResetProjectionMatrix();
            camera.Render();
        }

        public void Interrupt() { if (IsPlaying) Restore(); }
        void OnApplicationPause(bool paused) { if (paused) Interrupt(); }
        void OnApplicationFocus(bool focused) { if (!focused) Interrupt(); }
        void OnDisable() { if (IsPlaying) Restore(); }
        void OnDestroy() { if (IsPlaying) Restore(); }
        void Restore()
        {
            if (restoring || !IsPlaying) return;
            restoring = true;
            var continuation = completed; completed = null;
            try
            {
                if (effect != null) effect.Finish();
                if (home != null && home.activity != null && home.activity.HasOpenState)
                    home.RefreshFromState(); // Show committed growth even after an interrupted effect.
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                RestoreSafely(() => { if (effects != null) { effects.SetActive(false); Destroy(effects); } });
                RestoreSafely(() => { if (screen != null) { screen.SetActive(false); Destroy(screen); } });
                RestoreSafely(() => { if (home != null && home.gardenVisuals != null) home.gardenVisuals.SetActive(visualsWereActive); });
                foreach (var item in canvases) RestoreSafely(() => { if (item.Value != null) item.Value.enabled = item.Key; });
                // Orbit.OnEnable can set a pose: restore the precise captured
                // camera only after all original behaviours are re-enabled.
                for (int i = suspended.Count - 1; i >= 0; i--)
                {
                    var item = suspended[i]; RestoreSafely(() => { if (item.Value != null) item.Value.enabled = item.Key; });
                }
                RestoreSafely(() => { if (camera != null) oldCamera.Restore(camera); });
                RenderTexture.active = oldActive;
                canvases.Clear(); suspended.Clear(); effect = null; effects = null; screen = null;
                IsPlaying = false; Phase = "idle"; restoring = false;
            }
            // Always release the caller's one-shot latch. The caller checks its
            // own lifetime before rendering a page after disable or destruction.
            continuation?.Invoke();
        }

        static void RestoreSafely(Action action)
        {
            try { action(); } catch (Exception exception) { Debug.LogException(exception); }
        }

        sealed class CameraState
        {
            public readonly RenderTexture Target;
            public readonly Quaternion Rotation;
            readonly Vector3 position;
            readonly Matrix4x4 projection;
            readonly float size, aspect;
            readonly bool enabled;
            public CameraState(Camera camera)
            {
                Target = camera.targetTexture; Rotation = camera.transform.rotation; position = camera.transform.position;
                projection = camera.projectionMatrix; size = camera.orthographicSize; aspect = camera.aspect; enabled = camera.enabled;
            }
            public void Restore(Camera camera)
            {
                camera.targetTexture = Target; camera.transform.SetPositionAndRotation(position, Rotation);
                camera.orthographicSize = size; camera.aspect = aspect; camera.projectionMatrix = projection; camera.enabled = enabled;
            }
        }
    }
}
