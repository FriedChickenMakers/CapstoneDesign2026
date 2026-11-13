using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Runtime
{
    // The first effect implementation uses a white emissive plant silhouette,
    // three rising ribbons, deterministic motes and a one-shot celebratory burst.
    public sealed class GardenPlantEvolutionEffect : GardenGrowthEffect
    {
        readonly List<Renderer> glowing = new List<Renderer>();
        readonly List<Material[]> originalMaterials = new List<Material[]>();
        readonly LineRenderer[] ribbons = new LineRenderer[3];
        readonly ParticleSystem.Particle[] motes = new ParticleSystem.Particle[56];
        ParticleSystem swirl, burst;
        Material ribbonMaterial, particleMaterial, whiteMaterial;
        Vector3 center;
        float height, radius;
        bool revealed, released;
        public override int ParticleCount => (swirl == null ? 0 : swirl.particleCount) + (burst == null ? 0 : burst.particleCount);

        public override void Begin(Transform plant)
        {
            target = plant;
            released = false;
            var shader = Resources.Load<Shader>("GardenAtmosphere/PlantGrowthParticles");
            particleMaterial = new Material(shader) { name = "Growth particles (owned)", hideFlags = HideFlags.DontSave };
            ribbonMaterial = new Material(shader) { name = "Growth ribbons (owned)", hideFlags = HideFlags.DontSave };
            ribbonMaterial.SetFloat("_Round", 0);
            whiteMaterial = new Material(Resources.Load<Shader>("GardenAtmosphere/PlantGrowthWhite"))
                { name = "Growth white silhouette (owned)", hideFlags = HideFlags.DontSave };
            Measure(plant);
            for (int i = 0; i < ribbons.Length; i++)
            {
                var node = new GameObject("Growth spiral " + i); node.transform.SetParent(transform, false);
                var line = node.AddComponent<LineRenderer>(); ribbons[i] = line;
                line.sharedMaterial = ribbonMaterial; line.useWorldSpace = true; line.positionCount = 60;
                line.widthMultiplier = Mathf.Max(.018f, radius * .12f);
                line.numCornerVertices = 2; line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            }
            swirl = CreateParticles("Growth swirling motes", 56);
            burst = CreateParticles("Growth celebration burst", 96);
            foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = plant.gameObject.layer;
        }

        ParticleSystem CreateParticles(string name, int max)
        {
            var node = new GameObject(name); node.SetActive(false); node.transform.SetParent(transform, false);
            var system = node.AddComponent<ParticleSystem>();
            var main = system.main; main.playOnAwake = false; main.loop = false; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true;
            main.startLifetime = 1.25f; main.startSize = .04f; main.startSpeed = 0;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            system.useAutoRandomSeed = false; system.randomSeed = 17;
            node.SetActive(true); system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        void Measure(Transform plant)
        {
            var bounds = new Bounds(plant.position + Vector3.up * .12f, Vector3.one * .25f);
            foreach (var renderer in plant.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            center = new Vector3(bounds.center.x, plant.position.y, bounds.center.z);
            height = Mathf.Max(.65f, bounds.max.y - center.y + .18f);
            radius = Mathf.Max(.22f, Mathf.Max(bounds.extents.x, bounds.extents.z) + .15f);
        }

        public override void Sample(float progress)
        {
            float strength = revealed ? 1 - Mathf.InverseLerp(.58f, .82f, progress) : Mathf.SmoothStep(0, 1, progress / .25f);
            for (int ribbon = 0; ribbon < ribbons.Length; ribbon++)
            {
                var line = ribbons[ribbon]; if (line == null) continue;
                var color = ribbon == 1 ? new Color(.67f, 1, .88f, strength * .8f) : new Color(1, .88f, .52f, strength * .8f);
                line.startColor = color; line.endColor = new Color(1, 1, 1, 0);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = i / (float)(line.positionCount - 1);
                    float angle = progress * 22 + t * Mathf.PI * 4 + ribbon * Mathf.PI * 2 / 3;
                    float width = radius * (.65f + .65f * Mathf.Sin(t * Mathf.PI));
                    line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * width, t * height, Mathf.Sin(angle) * width));
                }
            }
            for (int i = 0; i < motes.Length; i++)
            {
                float t = Mathf.Repeat(i * .618034f + progress * 1.2f, 1);
                float angle = i * 2.399963f + progress * 25;
                motes[i].position = center + new Vector3(Mathf.Cos(angle) * radius, t * height, Mathf.Sin(angle) * radius);
                motes[i].startColor = new Color(1, .96f, .72f, strength * Mathf.Sin(t * Mathf.PI));
                motes[i].startSize = Mathf.Max(.018f, radius * .065f);
                motes[i].startLifetime = 1; motes[i].remainingLifetime = 1;
            }
            swirl.SetParticles(motes, motes.Length);
            float glow = revealed ? 1 - Mathf.InverseLerp(.58f, .77f, progress) : Mathf.InverseLerp(.20f, .43f, progress);
            whiteMaterial.SetFloat("_Glow", glow);
            if (!revealed && glow > .02f && glowing.Count == 0) Illuminate(target);
            if (revealed && glow <= .01f) RestoreMaterials();
        }
        Transform target;
        void Illuminate(Transform plant)
        {
            if (plant == null) return;
            foreach (var renderer in plant.GetComponentsInChildren<Renderer>())
            {
                glowing.Add(renderer); originalMaterials.Add(renderer.sharedMaterials);
                var white = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < white.Length; i++) white[i] = whiteMaterial;
                renderer.sharedMaterials = white;
            }
        }
        void RestoreMaterials()
        {
            for (int i = 0; i < glowing.Count; i++) if (glowing[i] != null) glowing[i].sharedMaterials = originalMaterials[i];
            glowing.Clear(); originalMaterials.Clear();
        }
        public override void BeforePlantChanges() => RestoreMaterials();
        public override void Reveal(Transform plant)
        {
            revealed = true; target = plant; Measure(plant); Illuminate(plant);
            burst.Play();
            for (int i = 0; i < 88; i++)
            {
                float y = 1 - 2 * (i + .5f) / 88;
                float angle = i * 2.399963f, ring = Mathf.Sqrt(1 - y * y);
                var direction = new Vector3(Mathf.Cos(angle) * ring, y * .65f + .4f, Mathf.Sin(angle) * ring);
                var emit = new ParticleSystem.EmitParams {
                    position = center + Vector3.up * height * .48f,
                    velocity = direction * Mathf.Max(.65f, radius * 3.3f),
                    startLifetime = .8f + (i % 5) * .08f,
                    startSize = Mathf.Max(.026f, radius * .11f),
                    startColor = i % 3 == 0 ? new Color(.64f, 1, .77f) : i % 3 == 1 ? new Color(1, .84f, .38f) : Color.white
                };
                burst.Emit(emit, 1);
            }
        }
        public override void Finish()
        {
            if (released) return; released = true; RestoreMaterials();
            if (swirl != null) swirl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ReleaseMaterial(whiteMaterial); ReleaseMaterial(ribbonMaterial); ReleaseMaterial(particleMaterial);
        }
        static void ReleaseMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
        void OnDestroy() => Finish();
    }
}
