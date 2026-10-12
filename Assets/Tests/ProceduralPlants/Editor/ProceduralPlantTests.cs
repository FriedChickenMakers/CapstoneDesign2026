using CapstoneDesign.Plants;
using NUnit.Framework;
using UnityEngine;

namespace CapstoneDesign.PlantTests
{
    public sealed class ProceduralPlantTests
    {
        [Test] public void AllPlantContracts() { Debug.Log(PlantContractChecks.Run()); }
        [Test] public void MeshReplacementAndResources()
        {
            var go=new GameObject("Synthetic renderer test");
            try
            {
                var view=go.AddComponent<ProceduralPlantView>();var p=PlantGenerator.Create("synthetic","view");
                view.Render(p);var old=go.GetComponent<MeshFilter>().sharedMesh;
                view.Render(PlantGenerator.Advance(p));
                Assert.IsTrue(old==null,"Editor mesh replacement releases old mesh");
                Assert.AreEqual(view.TriangleCount,go.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                Assert.IsTrue(go.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
