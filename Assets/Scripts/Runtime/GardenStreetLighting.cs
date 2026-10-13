using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Runtime
{
    // Local garden lighting only; the visual clock never changes saved activities.
    public sealed class GardenStreetLighting : MonoBehaviour
    {
        public static readonly Color BulbColor = new Color(1f, .61f, .28f);
        public Light[] Lights { get; private set; } = Array.Empty<Light>();
        public bool IsLit { get; private set; }
        readonly List<(Renderer renderer, int slot)> glass = new List<(Renderer, int)>();
        MaterialPropertyBlock properties;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly int Lamp0 = Shader.PropertyToID("_GardenLamp0");
        static readonly int Lamp1 = Shader.PropertyToID("_GardenLamp1");
        static readonly int LampColor = Shader.PropertyToID("_GardenLampColor");

        public static bool IsNightHour(double hour)
        {
            hour = (hour % 24 + 24) % 24;
            return hour >= 18 || hour < 6;
        }

        public static GardenStreetLighting Ensure(Camera camera, Transform visualRoot)
        {
            var lighting = visualRoot.GetComponent<GardenStreetLighting>();
            if (lighting == null) lighting = visualRoot.gameObject.AddComponent<GardenStreetLighting>();
            lighting.Initialize(camera);
            return lighting;
        }

        void Initialize(Camera camera)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            if (Lights.Length > 0) return;
            var surface = GetComponentInChildren<GardenSurface>(true);
            // Sky-only preview cameras have no island to furnish.
            if (surface == null) return;
            var source = Resources.Load<GameObject>("GardenModels/StreetLamp");
            if (source == null) throw new InvalidOperationException("Missing imported garden streetlamp");
            var positions = new[] { new Vector2(-.75f, .78f), new Vector2(.80f, -.42f) };
            Lights = new Light[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var lamp = Instantiate(source, surface.transform, false);
                lamp.name = "Garden streetlamp " + (i + 1);
                GardenSceneModels.ConfigureRenderers(lamp);
                GardenSceneModels.Place(lamp.transform, positions[i], i == 0 ? 18 : -16, surface.transform);
                foreach (var renderer in lamp.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++)
                        if (materials[m] != null && materials[m].name.StartsWith("StreetLamp_Glass", StringComparison.Ordinal))
                            glass.Add((renderer, m));
                }
                var bulb = lamp.transform.Find("Bulb");
                if (bulb == null) throw new InvalidOperationException("Streetlamp is missing its bulb anchor");
                var light = bulb.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = BulbColor;
                light.intensity = 2.4f;
                light.range = 3.4f;
                light.cullingMask = camera.cullingMask;
                light.renderMode = LightRenderMode.ForcePixel;
                // Two short-range lights, without twelve mobile cubemap shadow passes.
                light.shadows = LightShadows.None;
                light.enabled = false;
                Lights[i] = light;
            }
        }

        public void ApplyTime(double hour)
        {
            IsLit = IsNightHour(hour);
            foreach (var light in Lights) light.enabled = IsLit;
            foreach (var item in glass)
            {
                item.renderer.GetPropertyBlock(properties, item.slot);
                properties.SetColor(Emission, IsLit ? BulbColor * 4.5f : Color.black);
                item.renderer.SetPropertyBlock(properties, item.slot);
            }
        }

        public void ApplyToMist(Material material)
        {
            if (material == null) return;
            Vector4 Position(int i)
            {
                if (i >= Lights.Length || !Lights[i].enabled) return Vector4.zero;
                var light = Lights[i]; var p = light.transform.position;
                return new Vector4(p.x, p.y, p.z, light.range);
            }
            material.SetVector(Lamp0, Position(0));
            material.SetVector(Lamp1, Position(1));
            material.SetColor(LampColor, BulbColor * (Lights.Length > 0 ? Lights[0].intensity : 0));
        }
    }
}
