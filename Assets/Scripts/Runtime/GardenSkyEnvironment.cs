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
        Texture2D cloudNoise;
        ParticleSystem clouds;
        readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[24];
        double? previewHour;
        float nextRender, nextLighting;
        public float Daylight { get; private set; }
        public Vector3 SunDirection { get; private set; }
        public Vector3 MoonDirection => -SunDirection;
        public Light KeyLight => key;
        public ParticleSystem Clouds => clouds;
        public CapstoneDesign.Prototype.PrototypeGardenPreviewLayout PreviewLayout { get; set; }

        void OnEnable()
        {
            GardenVisualClock.Changed += RefreshClock;
            RefreshClock();
        }
        void OnDisable() => GardenVisualClock.Changed -= RefreshClock;
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
            // A low sun still casts shadows, instead of becoming a second ambient fill.
            direction.y = Mathf.Max(.12f, direction.y);
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
                sky.SetFloat("_ClockHour", (float)((hour % 24 + 24) % 24));
            }
            if (cloudMaterial != null)
            {
                cloudMaterial.SetFloat("_Daylight", Daylight);
                cloudMaterial.SetFloat("_Twilight", twilight);
            }
            view.backgroundColor = Color.Lerp(new Color(.009f,.018f,.043f), new Color(.46f,.70f,.88f), Daylight);
        }

        void CreateClouds()
        {
            var shader = Resources.Load<Shader>("GardenAtmosphere/Cloud");
            if (shader == null) return;
            cloudMaterial = new Material(shader) { name = "Eroded cumulus and thin cloud layers" };
            // A tiny, deterministic tiling density field; generated once, never per frame.
            // The fragment shader uses three bounded texture reads rather than ray marching.
            cloudNoise = GardenCloudNoise.Create();
            cloudMaterial.SetTexture("_Noise", cloudNoise);
            var node = new GameObject("Cloud particles");
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
            if (view == null) return;
            if (sky != null)
            {
                var celestial = SunDirection.y >= 0 ? SunDirection : MoonDirection;
                // A stylized upper-sky arc, with left/right tied to the actual light
                // in the current orbit so the visible disc and cast shadow agree.
                var area = PreviewLayout != null && PreviewLayout.isActiveAndEnabled && PreviewLayout.IsFullScreen
                    ? PreviewLayout.VisibleAreaInViewport : new Rect(0, 0, 1, 1);
                sky.SetVector("_CelestialUv", new Vector4(
                    Mathf.Lerp(area.xMin, area.xMax, .5f + .36f * Vector3.Dot(view.transform.right, celestial)),
                    Mathf.Lerp(area.yMin, area.yMax, .78f + .12f * Mathf.Clamp01(celestial.y)), 0, 0));
                sky.SetFloat("_ViewAspect", view.aspect);
            }
            if (clouds == null) return;
            // Independent wind lanes, not four identical overlapping puffs in a circular orbit.
            // Every lane is behind the island; opaque garden depth protects its silhouette.
            var right = view.transform.right; var up = view.transform.up; var forward = view.transform.forward;
            var focus = transform.position + Vector3.up * .4f;
            float halfWidth = Mathf.Max(2, view.orthographicSize * view.aspect);
            var light = SunDirection.y >= 0 ? SunDirection : MoonDirection;
            cloudMaterial.SetVector("_LightDirection", new Vector4(Vector3.Dot(right, light),
                Vector3.Dot(up, light), Vector3.Dot(-forward, light), 0));
            cloudMaterial.SetFloat("_CloudSeconds", seconds);
            float span = halfWidth * 2 + 4;
            for (int i = 0; i < particles.Length; i++)
            {
                float seed = Mathf.Repeat(i * .61803399f + .17f, 1);
                float variant = Mathf.Repeat(i * .41421356f + .31f, 1);
                int lane = i % 3;
                float speed = .025f + lane * .007f + seed * .013f;
                float x = Mathf.Repeat(seed * span + seconds * speed, span) - span * .5f;
                float y = lane == 0 ? 1.95f + variant * .7f : lane == 1 ? -1.9f - variant * .65f : 2.95f + variant * .6f;
                y += Mathf.Sin(seconds * (.008f + seed * .006f) + i * 2.4f) * .07f;
                float width = lane == 2 ? 1.25f + variant * .7f : .95f + variant * 1.0f;
                float height = width * (lane == 2 ? .30f : .57f + seed * .13f);
                var at = focus + forward * (3.5f + lane * .65f) + right * x + up * y;
                particles[i].position = at;
                particles[i].startSize3D = new Vector3(width, height, 1);
                // RGB supplies stable per-cloud shape seeds; lighting is shared by the shader.
                particles[i].startColor = new Color(seed, variant, lane / 2f, lane == 2 ? .35f : .76f);
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
            if (RenderSettings.sun == key) RenderSettings.sun = null;
            if (sky != null) { if (Application.isPlaying) Destroy(sky); else DestroyImmediate(sky); }
            if (cloudMaterial != null) { if (Application.isPlaying) Destroy(cloudMaterial); else DestroyImmediate(cloudMaterial); }
            if (cloudNoise != null) { if (Application.isPlaying) Destroy(cloudNoise); else DestroyImmediate(cloudNoise); }
        }
    }
}
