using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CapstoneDesign.Runtime
{
    // A stylized local-clock sky, not astronomical/geolocation tracking.
    // The garden remains lit at night; neither time nor weather affects rewards.
    public sealed class GardenSkyEnvironment : MonoBehaviour
    {
        Camera view;
        Light key;
        Material sky, cloudMaterial;
        Texture3D cloudNoise;
        ParticleSystem clouds;
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[24];
        double? previewHour;
        float nextRender, nextLighting;
        public float Daylight { get; private set; }
        public Vector3 SunDirection { get; private set; }
        public Vector3 MoonDirection => -SunDirection;
        public Light KeyLight => key;
        public ParticleSystem Clouds => clouds;
        public GardenStreetLighting StreetLighting { get; private set; }
        public CapstoneDesign.Prototype.PrototypeGardenPreviewLayout PreviewLayout { get; set; }

        void OnEnable()
        {
            GardenVisualClock.Changed += RefreshClock;
            RenderPipelineManager.beginCameraRendering -= PrepareSkyProjection;
            RenderPipelineManager.beginCameraRendering += PrepareSkyProjection;
            RefreshClock();
        }
        void OnDisable()
        {
            GardenVisualClock.Changed -= RefreshClock;
            RenderPipelineManager.beginCameraRendering -= PrepareSkyProjection;
        }
        void PrepareSkyProjection(ScriptableRenderContext context, Camera camera)
        {
            if (camera != view || sky == null) return;
            // Unity's orthographic sky pass uses its own square projection and
            // replaces built-in aspect data. Preserve the final RawImage aspect,
            // including cards that stretch a shared RT, before every render.
            // Material data only: never render or mutate targets in this callback.
            sky.SetFloat("_SkyAspect", camera.aspect);
        }
        void RefreshClock()
        {
            if (key == null) return;
            ApplyTime(previewHour ?? GardenVisualClock.CurrentHour);
            UpdateClouds(Time.unscaledTime);
            // Never render from a UI/Canvas callback. LateUpdate presents the changed
            // light and sky on this frame without re-entering the SRP camera pass.
            nextRender = 0;
            nextLighting = 0;
        }

        public static GardenSkyEnvironment Ensure(Camera camera, Transform visualRoot)
        {
            if (camera == null || visualRoot == null) return null;
            var environment = visualRoot.GetComponent<GardenSkyEnvironment>();
            if (environment == null) environment = visualRoot.gameObject.AddComponent<GardenSkyEnvironment>();
            environment.Initialize(camera);
            return environment;
        }

        void Initialize(Camera camera)
        {
            view = camera;
            GardenPostProcessing.Ensure(camera, transform);
            // Ensure is also used by editor captures, where this runtime-only
            // component has not received OnEnable. Keep registration idempotent.
            RenderPipelineManager.beginCameraRendering -= PrepareSkyProjection;
            RenderPipelineManager.beginCameraRendering += PrepareSkyProjection;
            if (key != null) return;
            foreach (var light in transform.GetComponentsInChildren<Light>(true)) light.enabled = false;
            var node = new GameObject("Sun and moon directional light");
            node.transform.SetParent(transform, false);
            key = node.AddComponent<Light>();
            key.type = LightType.Directional;
            key.cullingMask = camera.cullingMask;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .82f;
            key.shadowBias = .003f;
            key.shadowNormalBias = .01f;
            key.shadowNearPlane = .05f;
            node.AddComponent<UniversalAdditionalLightData>().usePipelineSettings = false;
            var shader = Resources.Load<Shader>("GardenAtmosphere/Sky");
            if (shader != null) sky = new Material(shader) { name = "Garden local-time sky" };
            var skybox = view.GetComponent<Skybox>();
            if (skybox == null) skybox = view.gameObject.AddComponent<Skybox>();
            skybox.material = sky;
            view.clearFlags = CameraClearFlags.Skybox;
            StreetLighting = GardenStreetLighting.Ensure(camera, transform);
            CreateClouds();
            ApplyTime(previewHour ?? GardenVisualClock.CurrentHour);
        }

        public void SetTimeForPreview(double? localHour)
        {
            previewHour = localHour;
            ApplyTime(localHour ?? GardenVisualClock.CurrentHour);
            UpdateClouds(0);
            nextRender = 0;
        }

        public static Vector3 SolarDirection(double hour)
        {
            float angle = (float)((hour % 24 - 6) / 24 * Math.PI * 2);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .35f * Mathf.Sin(angle)).normalized;
        }

        void ApplyTime(double hour)
        {
            if (key == null) return;
            SunDirection = SolarDirection(hour);
            float elevation = SunDirection.y;
            Daylight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.14f, .24f, elevation));
            float twilight = (1 - Mathf.Clamp01(Mathf.Abs(elevation) / .32f)) * Daylight;
            Vector3 direction = elevation >= 0 ? SunDirection : MoonDirection;
            // The sky disc and shadow light use exactly the same world direction.
            key.transform.rotation = Quaternion.LookRotation(-direction.normalized, Vector3.up);
            key.color = Color.Lerp(new Color(.55f,.66f,.91f), Color.Lerp(new Color(1,.96f,.86f),
                new Color(1,.61f,.34f), twilight), Daylight);
            key.intensity = Mathf.Lerp(.30f, 1.22f, Daylight);
            RenderSettings.sun = key;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.07f,.095f,.15f), new Color(.47f,.59f,.71f), Daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.045f,.06f,.095f), new Color(.25f,.31f,.33f), Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.02f,.028f,.045f), new Color(.14f,.16f,.15f), Daylight);
            RenderSettings.reflectionIntensity = Mathf.Lerp(.06f,.25f,Daylight);
            if (sky != null)
            {
                sky.SetVector("_SunDirection", SunDirection);
                sky.SetFloat("_Daylight", Daylight);
                sky.SetFloat("_Twilight", twilight);
            }
            if (cloudMaterial != null)
            {
                cloudMaterial.SetFloat("_Daylight", Daylight);
                cloudMaterial.SetFloat("_Twilight", twilight);
            }
            StreetLighting?.ApplyTime(hour);
            StreetLighting?.ApplyToMist(cloudMaterial);
            view.backgroundColor = Color.Lerp(new Color(.009f,.018f,.043f), new Color(.46f,.70f,.88f), Daylight);
        }

        void CreateClouds()
        {
            var shader = Resources.Load<Shader>("GardenAtmosphere/Cloud");
            if (shader == null) return;
            cloudMaterial = new Material(shader) { name = "World-space drifting mist" };
            // A small periodic volume is generated once. Three density samples per
            // fragment keep the soft particle volume bounded without ray marching.
            cloudNoise = GardenCloudNoise.Create();
            cloudMaterial.SetTexture("_Noise", cloudNoise);
            var node = new GameObject("World-space mist particles");
            node.transform.SetParent(transform, false);
            node.layer = 30;
            clouds = node.AddComponent<ParticleSystem>();
            clouds.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = clouds.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = particles.Length;
            main.startSpeed = 0; main.startLifetime = 100000; main.startSize3D = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = clouds.emission; emission.enabled = false;
            var shape = clouds.shape; shape.enabled = false;
            var renderer = clouds.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = cloudMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            clouds.Play();
            UpdateClouds(0);
        }

        public void UpdateClouds(float seconds)
        {
            if (view == null || clouds == null) return;
            var light = SunDirection.y >= 0 ? SunDirection : MoonDirection;
            cloudMaterial.SetVector("_LightDirection", light);
            cloudMaterial.SetVector("_IslandOrigin", transform.position);
            cloudMaterial.SetFloat("_CloudSeconds", seconds);
            StreetLighting?.ApplyToMist(cloudMaterial);
            for (int i = 0; i < particles.Length; i++)
            {
                float seed = Mathf.Repeat(i * .61803399f + .17f, 1);
                float variant = Mathf.Repeat(i * .41421356f + .31f, 1);
                float depth = Mathf.Repeat(i * .73205081f + .53f, 1);
                float angle = seed * Mathf.PI * 2;
                float radius = 2.05f + variant * 1.9f;
                var anchor = new Vector3(Mathf.Cos(angle) * radius, -1.0f - depth * 1.65f, Mathf.Sin(angle) * radius);
                // Bounded curl of a smooth vector potential; no wrap or reset jumps.
                float a = anchor.y + anchor.z + seconds * (.009f + seed * .007f);
                float b = anchor.z + anchor.x + seconds * (.013f + variant * .005f);
                float c = anchor.x + anchor.y + seconds * (.011f + depth * .006f);
                var drift = new Vector3(Mathf.Cos(c) - Mathf.Cos(b), Mathf.Cos(a) - Mathf.Cos(c),
                    Mathf.Cos(b) - Mathf.Cos(a)) * .22f;
                float width = 2.1f + variant * 1.35f;
                float height = width * (.66f + depth * .28f);
                particles[i].position = transform.position + anchor + drift;
                particles[i].startSize3D = new Vector3(width, height, 1);
                particles[i].rotation = seed * 160 - 80;
                // Seeds shift density, never position particles relative to the camera.
                particles[i].startColor = new Color(seed, variant, depth, .46f + seed * .16f);
                particles[i].remainingLifetime = particles[i].startLifetime = 100000;
                particles[i].randomSeed = (uint)(i + 1);
            }
            clouds.SetParticles(particles, particles.Length);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || view == null || !view.gameObject.activeInHierarchy) return;
            float now = Time.unscaledTime;
            if (now >= nextLighting) { nextLighting = now + 15; ApplyTime(previewHour ?? GardenVisualClock.CurrentHour); }
            if (now < nextRender) return;
            nextRender = now + .1f;
            UpdateClouds(now);
            if (!view.enabled && view.targetTexture != null) view.Render();
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= PrepareSkyProjection;
            if (RenderSettings.sun == key) RenderSettings.sun = null;
            if (sky != null) { if (Application.isPlaying) Destroy(sky); else DestroyImmediate(sky); }
            if (cloudMaterial != null) { if (Application.isPlaying) Destroy(cloudMaterial); else DestroyImmediate(cloudMaterial); }
            if (cloudNoise != null) { if (Application.isPlaying) Destroy(cloudNoise); else DestroyImmediate(cloudNoise); }
        }
    }
}
