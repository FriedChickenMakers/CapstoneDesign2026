using System;
using System.Collections.Generic;
using UnityEngine;

namespace CapstoneDesign.Runtime
{
    public enum PlantAlbumEnvironmentKind { SunlitMeadow, ForestEdge, RockyMeadow, AutumnGrove }

    // Presentation only: no saved state, global Random, prefabs, materials or cameras.
    // Callers may cache one fixed-seed mesh per kind; each returned mesh is caller-owned.
    public static class PlantAlbumEnvironment
    {
        public const float ClearRadius = 2f;

        public static PlantAlbumEnvironmentKind Select(string instanceId, string baseSeed)
        {
            // Length-prefixed UTF-16 bytes keep this FNV-1a selection
            // stable across platforms, process restarts and string hash versions.
            uint hash = 2166136261u;
            unchecked
            {
                foreach (string value in new[] { instanceId ?? "", baseSeed ?? "" })
                {
                    uint length = (uint)value.Length;
                    for (int i = 0; i < 4; i++) { hash = (hash ^ (byte)length) * 16777619u; length >>= 8; }
                    foreach (char character in value)
                    {
                        hash = (hash ^ (byte)character) * 16777619u;
                        hash = (hash ^ (byte)(character >> 8)) * 16777619u;
                    }
                }
            }
            return (PlantAlbumEnvironmentKind)(hash % 4);
        }

        public static string Label(PlantAlbumEnvironmentKind kind)
        {
            switch (kind)
            {
                case PlantAlbumEnvironmentKind.SunlitMeadow: return "햇살 머무는 초원";
                case PlantAlbumEnvironmentKind.ForestEdge: return "고요한 숲 가장자리";
                case PlantAlbumEnvironmentKind.RockyMeadow: return "바위 곁의 풀밭";
                case PlantAlbumEnvironmentKind.AutumnGrove: return "가을빛 작은 숲";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // Exactly the former album MeadowHeight; memo feet and the plant's root
        // retain their original contact with the ground in every theme.
        public static float Height(Vector2 point)
            => Mathf.SmoothStep(0, 1, point.magnitude / 2) * (.035f + .035f * Mathf.Sin(point.x * .7f) * Mathf.Cos(point.y * .9f));

        public static Mesh Build(PlantAlbumEnvironmentKind kind, int seed)
        {
            if (kind < PlantAlbumEnvironmentKind.SunlitMeadow || kind > PlantAlbumEnvironmentKind.AutumnGrove)
                throw new ArgumentOutOfRangeException(nameof(kind));
            return new Builder(kind, seed).Build();
        }

        struct Palette
        {
            public Color Ground, GroundLight, Grass, GrassLight, Foliage, FoliageLight, Rock, RockLight, Bark, Flower;
        }

        sealed class Builder
        {
            const float Tau = Mathf.PI * 2;
            readonly PlantAlbumEnvironmentKind kind;
            readonly Palette palette;
            readonly List<Vector3> vertices = new List<Vector3>(45000);
            readonly List<Vector3> normals = new List<Vector3>(45000);
            readonly List<Color> colors = new List<Color>(45000);
            readonly List<int> indices = new List<int>(45000);
            readonly List<Vector3> obstacles = new List<Vector3>(); // x,z plus footprint radius in y
            uint random;

            public Builder(PlantAlbumEnvironmentKind kind, int seed)
            {
                this.kind = kind;
                unchecked { random = (uint)seed ^ (0x9e3779b9u * ((uint)kind + 1)) ^ 0xa341316cu; }
                if (random == 0) random = 0x6d2b79f5u;
                palette = Colors(kind);
            }

            public Mesh Build()
            {
                Ground();
                Rocks();
                Trees();
                Shrubs();
                Grass();
                Pebbles();
                FlowersAndLeaves();
                // Flat-shaded triangles share one draw call and remain inside the
                // 16-bit index range. No runtime object besides this mesh is owned.
                if (vertices.Count >= 60000) throw new InvalidOperationException("Album environment exceeded its geometry budget.");
                var mesh = new Mesh { name = "Plant album nature " + kind + " (owned)", hideFlags = HideFlags.HideAndDontSave };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
                mesh.SetTriangles(indices, 0, false); mesh.RecalculateBounds();
                return mesh;
            }

            float Next()
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                return (random >> 8) * (1f / 16777216f);
            }
            float Range(float from, float to) => Mathf.Lerp(from, to, Next());
            Vector2 Point(float radius, float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector3 OnGround(Vector2 point) => new Vector3(point.x, Height(point), point.y);
            Color Shade(Color dark, Color light) => Color.Lerp(dark, light, Next());

            // Alpha records geometry category, NOT opacity: ground/grass/flowers
            // use 0 and rocks/shrubs/trees use 1. The shader ignores this category
            // for visibility and fades the entire environment with one angle alpha.
            // All RGB values are converted to linear space exactly once here.
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color, float decoration)
                => Triangle(a, b, c, color, color, color, decoration);
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc, float decoration)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                ca = ca.linear; cb = cb.linear; cc = cc.linear;
                ca.a = cb.a = cc.a = decoration;
                colors.Add(ca); colors.Add(cb); colors.Add(cc);
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            }

            void Ground()
            {
                const int sides = 72;
                var rings = new[] { 0f, .65f, 1.6f, 2.3f, 3.5f, 5.2f, 7.5f, 10.5f };
                for (int ring = 1; ring < rings.Length; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Tau / sides, b = (i + 1) * Tau / sides;
                    var p = GroundPoint(rings[ring - 1], a); var q = GroundPoint(rings[ring - 1], b);
                    var r = GroundPoint(rings[ring], a); var s = GroundPoint(rings[ring], b);
                    GroundTriangle(p, s, r);
                    if (ring > 1) GroundTriangle(p, q, s);
                }
            }
            Vector3 GroundPoint(float radius, float angle)
            {
                if (radius > 8) radius *= 1 + .025f * Mathf.Sin(angle * 5) + .018f * Mathf.Cos(angle * 9);
                return OnGround(Point(radius, angle));
            }
            Color GroundColor(Vector3 point)
            {
                // Continuous colors avoid a spoke pattern through the central plant.
                float blend = .5f + .22f * Mathf.Sin(point.x * .61f) * Mathf.Cos(point.z * .73f)
                    + .13f * Mathf.Sin(point.x * 1.7f + point.z * .8f);
                return Color.Lerp(palette.Ground, palette.GroundLight, blend);
            }
            void GroundTriangle(Vector3 a, Vector3 b, Vector3 c)
                => Triangle(a, b, c, GroundColor(a), GroundColor(b), GroundColor(c), 0);

            bool Place(float inner, float outer, float radius, out Vector2 point)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    point = Point(Mathf.Sqrt(Range(inner * inner, outer * outer)), Range(0, Tau));
                    bool clear = true;
                    foreach (var obstacle in obstacles)
                        if ((point - new Vector2(obstacle.x, obstacle.z)).sqrMagnitude < (radius + obstacle.y) * (radius + obstacle.y))
                        { clear = false; break; }
                    if (clear) { obstacles.Add(new Vector3(point.x, radius, point.y)); return true; }
                }
                point = default; return false;
            }

            void Rocks()
            {
                int count = kind == PlantAlbumEnvironmentKind.RockyMeadow ? 30 : kind == PlantAlbumEnvironmentKind.ForestEdge ? 12 : 17;
                for (int i = 0; i < count; i++)
                {
                    float radius = Range(.14f, kind == PlantAlbumEnvironmentKind.RockyMeadow ? .48f : .32f);
                    if (!Place(3, 6.9f, radius + .15f, out var point)) continue;
                    var center = OnGround(point) + Vector3.up * radius * .27f;
                    Blob(center, new Vector3(radius * 1.2f, radius * .65f, radius), Shade(palette.Rock, palette.RockLight), 7, .17f);
                    // A smaller companion breaks the isolated repeating-boulder look.
                    if (i % 3 == 0)
                    {
                        var companion = point + Point(radius * 1.2f, Range(0, Tau));
                        Blob(OnGround(companion) + Vector3.up * radius * .12f,
                            new Vector3(radius * .48f, radius * .3f, radius * .44f), palette.RockLight, 6, .12f);
                    }
                }
            }

            void Trees()
            {
                int count = kind == PlantAlbumEnvironmentKind.SunlitMeadow ? 7 : kind == PlantAlbumEnvironmentKind.RockyMeadow ? 9 : 18;
                float phase = Range(0, Tau);
                for (int i = 0; i < count; i++)
                {
                    float angle = phase + (i + Range(-.2f, .2f)) * Tau / count;
                    var point = Point(Range(4.8f, 7), angle);
                    float height = Range(.9f, 1.4f), crown = Range(.3f, .48f);
                    var origin = OnGround(point);
                    Tube(origin, origin + new Vector3(.025f, height * .7f, .02f), .035f, .022f, palette.Bark);
                    if (kind == PlantAlbumEnvironmentKind.ForestEdge && i % 3 != 0)
                    {
                        // Layered conifers distinguish the cool forest silhouette.
                        for (int tier = 0; tier < 3; tier++)
                        {
                            float bottom = height * (.28f + tier * .21f);
                            Cone(origin + Vector3.up * bottom, crown * (1 - tier * .22f), height * .43f,
                                Shade(palette.Foliage, palette.FoliageLight));
                        }
                    }
                    else
                    {
                        Color leaves = Shade(palette.Foliage, palette.FoliageLight);
                        if (kind == PlantAlbumEnvironmentKind.AutumnGrove && i % 3 == 0) leaves = new Color(.72f, .29f, .14f);
                        float spread = kind == PlantAlbumEnvironmentKind.SunlitMeadow ? 1.25f : 1;
                        Blob(origin + Vector3.up * height * .76f, new Vector3(crown * spread, height * .27f, crown), leaves, 8, .06f);
                        Blob(origin + new Vector3(crown * .51f, height * .65f, crown * .12f),
                            new Vector3(crown * .67f, height * .22f, crown * .75f), Color.Lerp(leaves, palette.FoliageLight, .3f), 7, .06f);
                        Blob(origin + new Vector3(-crown * .38f, height * .68f, -crown * .22f),
                            new Vector3(crown * .7f, height * .21f, crown * .66f), leaves, 7, .06f);
                    }
                    obstacles.Add(new Vector3(point.x, .18f, point.y));
                }
            }

            void Shrubs()
            {
                int count = kind == PlantAlbumEnvironmentKind.ForestEdge ? 28 : 19;
                for (int i = 0; i < count; i++)
                {
                    float radius = Range(.14f, .28f);
                    if (!Place(2.85f, 6.4f, radius + .12f, out var point)) continue;
                    var tint = Shade(palette.Foliage, palette.FoliageLight);
                    Blob(OnGround(point) + Vector3.up * radius * .43f,
                        new Vector3(radius * 1.25f, radius * .62f, radius), tint, 7, .1f);
                }
            }

            void Grass()
            {
                int count = kind == PlantAlbumEnvironmentKind.ForestEdge ? 800 : kind == PlantAlbumEnvironmentKind.RockyMeadow ? 650 : 760;
                var patches = new Vector2[18];
                for (int i = 0; i < patches.Length; i++) patches[i] = Point(Range(2.7f, 7.3f), i * Tau / patches.Length + Range(-.15f, .15f));
                for (int i = 0; i < count; i++)
                {
                    var point = patches[i % patches.Length] + Point(Range(.03f, 1.15f), Range(0, Tau));
                    if (point.magnitude < 2.25f || point.magnitude > 8.2f) continue;
                    bool blocked = false;
                    foreach (var obstacle in obstacles)
                        if ((point - new Vector2(obstacle.x, obstacle.z)).sqrMagnitude < obstacle.y * obstacle.y * .64f)
                        { blocked = true; break; }
                    if (blocked) continue;
                    var origin = OnGround(point);
                    float height = Range(.075f, kind == PlantAlbumEnvironmentKind.ForestEdge ? .24f : .19f);
                    Color tint = Shade(palette.Grass, palette.GrassLight);
                    float facing = Range(0, Tau);
                    for (int blade = 0; blade < 3; blade++) Blade(origin, height * Range(.7f, 1.1f), facing + blade * 2.09f, tint);
                }
            }
            void Blade(Vector3 origin, float height, float angle, Color tint)
            {
                var side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Range(.012f, .026f);
                var bend = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * height * .35f;
                var mid = origin + Vector3.up * height * .6f + bend * .35f;
                var tip = origin + Vector3.up * height + bend;
                Triangle(origin - side, origin + side, mid + side * .45f, tint, 0);
                Triangle(origin - side, mid + side * .45f, mid - side * .45f, tint, 0);
                Triangle(mid - side * .45f, mid + side * .45f, tip, Color.Lerp(tint, palette.GrassLight, .45f), 0);
            }

            void Pebbles()
            {
                int count = kind == PlantAlbumEnvironmentKind.RockyMeadow ? 150 : 70;
                float phase = Range(0, Tau);
                for (int i = 0; i < count; i++)
                {
                    // Small winding gravel pockets; no path cuts through the plant.
                    float angle = phase + i * 2.399963f;
                    var point = Point(Range(2.5f, 5.9f), angle);
                    float size = Range(.025f, .065f);
                    var center = OnGround(point) + Vector3.up * size * .18f;
                    Octahedron(center, new Vector3(size * 1.3f, size * .35f, size), Shade(palette.Rock, palette.RockLight));
                }
            }

            void FlowersAndLeaves()
            {
                bool autumn = kind == PlantAlbumEnvironmentKind.AutumnGrove;
                int count = autumn ? 140 : kind == PlantAlbumEnvironmentKind.SunlitMeadow ? 85 : 30;
                for (int i = 0; i < count; i++)
                {
                    var point = Point(Range(2.35f, 6.8f), Range(0, Tau));
                    var ground = OnGround(point);
                    var origin = ground + Vector3.up * (autumn ? .006f : Range(.08f, .17f));
                    float angle = Range(0, Tau), size = Range(.024f, .047f);
                    var along = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * size;
                    var across = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * size * .55f;
                    Color tint = autumn ? Shade(palette.Foliage, palette.FoliageLight) : palette.Flower;
                    if (autumn)
                    {
                        Triangle(origin - along, origin + Vector3.up * .012f + across, origin + along, tint, 0);
                        Triangle(origin - along, origin + along, origin - across, tint, 0);
                    }
                    else
                    {
                        Triangle(origin - along, origin + across, origin + along, tint, 0);
                        Triangle(origin - along, origin + along, origin - across, tint, 0);
                        Triangle(origin - across, origin + Vector3.up * .017f, origin + across, new Color(.91f, .71f, .27f), 0);
                        var stem = ground;
                        Triangle(stem - along * .06f, stem + along * .06f, origin, palette.Grass, 0);
                    }
                }
            }

            void Blob(Vector3 center, Vector3 scale, Color tint, int sides, float irregularity)
            {
                var lower = new Vector3[sides]; var upper = new Vector3[sides];
                float phase = Range(0, Tau);
                for (int i = 0; i < sides; i++)
                {
                    float angle = phase + i * Tau / sides, shape = Range(1 - irregularity, 1 + irregularity);
                    var outward = new Vector3(Mathf.Cos(angle) * scale.x, 0, Mathf.Sin(angle) * scale.z) * shape;
                    lower[i] = center + outward * .85f - Vector3.up * scale.y * .4f;
                    upper[i] = center + outward * .73f + Vector3.up * scale.y * .55f;
                }
                var top = center + Vector3.up * scale.y; var bottom = center - Vector3.up * scale.y * .72f;
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    var face = Color.Lerp(tint * .86f, tint, .5f + .5f * Mathf.Sin(phase + i * 1.8f));
                    Triangle(bottom, lower[next], lower[i], face, 1);
                    Triangle(lower[i], lower[next], upper[next], face, 1);
                    Triangle(lower[i], upper[next], upper[i], face, 1);
                    Triangle(upper[i], upper[next], top, Color.Lerp(face, tint, .5f), 1);
                }
            }
            void Cone(Vector3 origin, float radius, float height, Color tint)
            {
                const int sides = 10;
                for (int i = 0; i < sides; i++)
                {
                    var a = Point(radius, i * Tau / sides); var b = Point(radius, (i + 1) * Tau / sides);
                    Triangle(origin + new Vector3(a.x, 0, a.y), origin + new Vector3(b.x, 0, b.y), origin + Vector3.up * height,
                        Color.Lerp(tint * .85f, tint, i % 3 * .5f), 1);
                }
            }
            void Tube(Vector3 bottom, Vector3 top, float bottomRadius, float topRadius, Color tint)
            {
                const int sides = 6;
                for (int i = 0; i < sides; i++)
                {
                    var a = Point(1, i * Tau / sides); var b = Point(1, (i + 1) * Tau / sides);
                    var va = new Vector3(a.x, 0, a.y); var vb = new Vector3(b.x, 0, b.y);
                    Triangle(bottom + va * bottomRadius, bottom + vb * bottomRadius, top + vb * topRadius, tint, 1);
                    Triangle(bottom + va * bottomRadius, top + vb * topRadius, top + va * topRadius, tint, 1);
                }
            }
            void Octahedron(Vector3 center, Vector3 size, Color tint)
            {
                var rim = new[] { center + Vector3.right * size.x, center + Vector3.forward * size.z,
                    center - Vector3.right * size.x, center - Vector3.forward * size.z };
                for (int i = 0; i < 4; i++)
                {
                    Triangle(rim[i], rim[(i + 1) % 4], center + Vector3.up * size.y, tint, 1);
                    Triangle(rim[(i + 1) % 4], rim[i], center - Vector3.up * size.y * .5f, tint, 1);
                }
            }
        }

        static Palette Colors(PlantAlbumEnvironmentKind kind)
        {
            switch (kind)
            {
                case PlantAlbumEnvironmentKind.SunlitMeadow:
                    return new Palette { Ground = new Color(.42f,.55f,.27f), GroundLight = new Color(.57f,.66f,.36f),
                        Grass = new Color(.31f,.49f,.18f), GrassLight = new Color(.69f,.74f,.37f), Foliage = new Color(.31f,.49f,.25f), FoliageLight = new Color(.56f,.65f,.29f),
                        Rock = new Color(.43f,.46f,.39f), RockLight = new Color(.67f,.66f,.54f), Bark = new Color(.43f,.34f,.23f), Flower = new Color(.98f,.94f,.71f) };
                case PlantAlbumEnvironmentKind.ForestEdge:
                    return new Palette { Ground = new Color(.26f,.40f,.31f), GroundLight = new Color(.43f,.55f,.36f),
                        Grass = new Color(.23f,.42f,.25f), GrassLight = new Color(.49f,.66f,.39f), Foliage = new Color(.16f,.34f,.29f), FoliageLight = new Color(.36f,.52f,.32f),
                        Rock = new Color(.35f,.42f,.40f), RockLight = new Color(.55f,.59f,.50f), Bark = new Color(.34f,.28f,.23f), Flower = new Color(.74f,.81f,.91f) };
                case PlantAlbumEnvironmentKind.RockyMeadow:
                    return new Palette { Ground = new Color(.43f,.49f,.35f), GroundLight = new Color(.61f,.64f,.46f),
                        Grass = new Color(.33f,.47f,.31f), GrassLight = new Color(.59f,.67f,.46f), Foliage = new Color(.28f,.40f,.33f), FoliageLight = new Color(.48f,.56f,.36f),
                        Rock = new Color(.39f,.44f,.49f), RockLight = new Color(.69f,.71f,.68f), Bark = new Color(.40f,.35f,.28f), Flower = new Color(.83f,.78f,.94f) };
                case PlantAlbumEnvironmentKind.AutumnGrove:
                    return new Palette { Ground = new Color(.51f,.48f,.27f), GroundLight = new Color(.69f,.62f,.36f),
                        Grass = new Color(.46f,.49f,.23f), GrassLight = new Color(.78f,.70f,.39f), Foliage = new Color(.69f,.36f,.15f), FoliageLight = new Color(.92f,.67f,.22f),
                        Rock = new Color(.47f,.44f,.37f), RockLight = new Color(.68f,.64f,.52f), Bark = new Color(.78f,.73f,.61f), Flower = new Color(.86f,.64f,.31f) };
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
