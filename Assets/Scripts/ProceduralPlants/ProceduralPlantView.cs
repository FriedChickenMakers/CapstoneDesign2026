using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.Plants
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class ProceduralPlantView : MonoBehaviour
    {
        Mesh ownedMesh;
        Material ownedMaterial;
        public int TriangleCount { get; private set; }
        // Call only after committing growth or restoring a save; no Update or per-frame RNG.
        public void Render(PlantRecipe recipe)
        {
            var data=PlantGeometry.Build(recipe);
            var vertices=new Vector3[data.Vertices.Count];var normals=new Vector3[vertices.Length];
            var colors=new Color[vertices.Length];var indices=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var v=data.Vertices[i];var n=data.Normals[i];var c=data.Colors[i];
                vertices[i]=new Vector3((float)v.X,(float)v.Y,(float)v.Z);
                normals[i]=new Vector3((float)n.X,(float)n.Y,(float)n.Z);
                colors[i]=new Color((float)c.R,(float)c.G,(float)c.B,1);indices[i]=i;
            }
            if(ownedMaterial==null)
            {
                var shader=Resources.Load<Shader>("ProceduralPlants/PlantVertexColor");
                if(shader==null) throw new InvalidOperationException("Procedural plant vertex color shader is missing.");
                ownedMaterial=new Material(shader) { name="Procedural plant (owned)",hideFlags=HideFlags.DontSave };
            }
            var next=new Mesh { name="ProceduralPlant_"+recipe.GrowthStep,indexFormat=IndexFormat.UInt16,hideFlags=HideFlags.DontSave };
            next.vertices=vertices;next.normals=normals;next.colors=colors;next.triangles=indices;next.RecalculateBounds();
            var old=ownedMesh;ownedMesh=next;
            GetComponent<MeshFilter>().sharedMesh=next;
            var renderer=GetComponent<MeshRenderer>();renderer.sharedMaterial=ownedMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            renderer.receiveShadows=true;
            TriangleCount=data.TriangleCount;Release(old);
        }
        static void Release(UnityEngine.Object resource)
        { if(resource!=null) { if(Application.isPlaying) Destroy(resource);else DestroyImmediate(resource); } }
        void OnDestroy() { Release(ownedMesh);Release(ownedMaterial); }
    }
}
