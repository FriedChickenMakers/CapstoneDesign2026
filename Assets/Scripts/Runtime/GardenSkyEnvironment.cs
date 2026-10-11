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
            ApplyTime(DateTime.Now.TimeOfDay.TotalHours);
        }

        public void SetTimeForPreview(double? localHour)
        {
            previewHour = localHour;
            ApplyTime(localHour ?? DateTime.Now.TimeOfDay.TotalHours);
            UpdateClouds(0);
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
            key.color = Color.Lerp(new Color(.63f,.73f,1), Color.Lerp(new Color(1,.96f,.86f),
                new Color(1,.61f,.34f), twilight), Daylight);
            key.intensity = Mathf.Lerp(.58f, 1.22f, Daylight);
            RenderSettings.sun = key;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.14f,.19f,.32f), new Color(.47f,.59f,.71f), Daylight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.10f,.13f,.21f), new Color(.25f,.31f,.33f), Daylight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.055f,.065f,.105f), new Color(.14f,.16f,.15f), Daylight);
            RenderSettings.reflectionIntensity = .25f;
            if (sky != null)
            {
                sky.SetVector("_SunDirection", SunDirection);
                sky.SetFloat("_Daylight", Daylight);
                sky.SetFloat("_Twilight", twilight);
                sky.SetFloat("_ClockHour", (float)((hour % 24 + 24) % 24));
            }
            view.backgroundColor = Color.Lerp(new Color(.055f,.08f,.16f), new Color(.46f,.70f,.88f), Daylight);
        }

        void CreateClouds()
        {
            var shader = Resources.Load<Shader>("GardenAtmosphere/Cloud");
            if (shader == null) return;
            cloudMaterial = new Material(shader) { name = "Soft particle cloud wisps" };
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
            // Camera-relative peripheral bands keep clouds behind/below the island from every orbit.
            var right = view.transform.right; var up = view.transform.up; var forward = view.transform.forward;
            var focus = transform.position + Vector3.up * .4f;
            float halfWidth = Mathf.Max(2, view.orthographicSize * view.aspect);
            for (int i = 0; i < particles.Length; i++)
            {
                int group = i / 4, puff = i % 4;
                float phase = group * 1.0472f + seconds * .012f;
                float x = Mathf.Cos(phase) * (halfWidth + .4f) + (puff - 1.5f) * .3f;
                float y = group % 2 == 0 ? -1.65f : 1.95f;
                // Behind the garden; upper clouds stay outside the central plant silhouette.
                var at = focus + forward * 3.2f + right * x + up * (y + Mathf.Sin(phase * 1.3f) * .18f);
                particles[i].position = at;
                particles[i].startSize3D = new Vector3(1.3f + puff * .12f, .46f + puff % 2 * .12f, 1);
                particles[i].startColor = Color.Lerp(new Color(.48f,.57f,.76f,.20f),new Color(1,1,1,.32f),Daylight);
                particles[i].remainingLifetime = particles[i].startLifetime = 100000;
                particles[i].randomSeed = (uint)(i + 1);
            }
            clouds.SetParticles(particles, particles.Length);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || view == null || !view.gameObject.activeInHierarchy) return;
            float now = Time.unscaledTime;
            if (now >= nextLighting) { nextLighting = now + 15; ApplyTime(previewHour ?? DateTime.Now.TimeOfDay.TotalHours); }
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
        }
    }
}
