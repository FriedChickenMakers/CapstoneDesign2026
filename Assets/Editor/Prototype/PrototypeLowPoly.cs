using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Original, flat-shaded geometry inspired by miniature low-poly gardens.
    // No imported Solar Forest artwork or licensed game assets.
    public static class PrototypeLowPoly
    {
        public static Mesh Gem => Make("FacetGem", 8, true, false);
        public static Mesh Tile => Make("HexTile", 6, false, true);
        public static Mesh Stem => Make("StemPrism", 6, false, false);
        public static Mesh Leaf => Make("FacetLeaf", 4, true, false);

        private static Mesh Make(string name, int sides, bool gem, bool taper)
        {
            string path = "Assets/Art/Prototype/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory("Assets/Art/Prototype/Meshes");
            var points = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 Point(int ring, int i)
            {
                float angle = (i % sides) * Mathf.PI * 2 / sides;
                float y = ring == 0 ? -.5f : .5f;
                float radius = .5f;
                if (gem) { y *= .5f; radius = .43f; }
                if (taper && ring == 0) radius = .36f;
                return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3) < 0) { var t = b; b = c; c = t; }
                int start = points.Count;
                points.Add(a); points.Add(b); points.Add(c);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            for (int i = 0; i < sides; i++)
            {
                Tri(Point(0, i), Point(0, i + 1), Point(1, i));
                Tri(Point(0, i + 1), Point(1, i + 1), Point(1, i));
                Tri(new Vector3(0, gem ? -.5f : -.5f, 0), Point(0, i), Point(0, i + 1));
                Tri(new Vector3(0, .5f, 0), Point(1, i + 1), Point(1, i));
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
