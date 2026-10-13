using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.PlantTests
{
    public sealed class ProceduralPlantVisualTests
    {
        const string Output="artifacts/plant-mesh";

        [Test] public void GeneratedPlantRespondsToSceneAndWarmPointLights()
        {
            Directory.CreateDirectory(Output);
            var root=new GameObject("Synthetic plant lighting test");
            var preview=new PreviewRenderUtility();
            try
            {
                var recipe=PlantGenerator.Create("synthetic-lighting","plant");
                recipe.Counts=PlantCatalog.All[0].ExampleCounts();
                while(!recipe.Mature) recipe=PlantGenerator.Advance(recipe);
                root.AddComponent<ProceduralPlantView>().Render(recipe);
                preview.AddSingleGO(root);
                preview.ambientColor=Color.black;
                var camera=preview.camera;
                camera.orthographic=true;camera.orthographicSize=.77f;
                camera.nearClipPlane=.01f;camera.farClipPlane=10;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                var rotation=Quaternion.Euler(32,-25,0);
                camera.transform.SetPositionAndRotation(new Vector3(0,.59f,0)+rotation*(Vector3.back*3),rotation);
                var key=preview.lights[0];key.type=LightType.Directional;
                key.transform.rotation=Quaternion.Euler(45,-35,0);
                key.color=Color.white;key.intensity=1.2f;
                var point=preview.lights[1];point.type=LightType.Point;
                point.transform.position=new Vector3(-.35f,1.0f,-.65f);
                point.color=new Color(1,.61f,.28f);point.range=3.4f;point.intensity=0;
                var day=CaptureLighting(preview,"scene-day");
                key.color=new Color(.55f,.66f,.91f);key.intensity=.08f;
                var night=CaptureLighting(preview,"scene-night");
                point.intensity=2.4f;
                var warm=CaptureLighting(preview,"scene-night-warm-point");

                double dayLuminance=0,nightLuminance=0,redContribution=0,blueContribution=0;
                int changedByPoint=0;
                for(int i=0;i<day.Length;i++)
                {
                    dayLuminance+=day[i].grayscale;nightLuminance+=night[i].grayscale;
                    redContribution+=warm[i].r-night[i].r;blueContribution+=warm[i].b-night[i].b;
                    if(Mathf.Abs(warm[i].r-night[i].r)+Mathf.Abs(warm[i].g-night[i].g)+Mathf.Abs(warm[i].b-night[i].b)>.03f)
                        changedByPoint++;
                }
                Assert.Greater(dayLuminance,nightLuminance*1.8,"The plant must dim when actual scene lighting dims");
                Assert.Greater(changedByPoint,100,"An additional point light must illuminate the generated mesh");
                Assert.Greater(redContribution,blueContribution*1.2,"The incandescent point light must give a warm contribution");
            }
            finally { preview.Cleanup();Object.DestroyImmediate(root); }
        }

        static Color[] CaptureLighting(PreviewRenderUtility preview,string name)
        {
            preview.BeginStaticPreview(new Rect(0,0,400,500));preview.Render(true);
            var texture=preview.EndStaticPreview();
            try
            {
                var pixels=texture.GetPixels();
                Assert.Less(pixels.Count(c=>c.r>.9f&&c.b>.9f&&c.g<.1f),10,"No unsupported-shader magenta");
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
                return pixels;
            }
            finally { Object.DestroyImmediate(texture); }
        }

        // Real URP previews with synthetic recipes. Images stay in ignored artifacts.
        [Test] public void RenderSpeciesAndReferenceComparison()
        {
            Directory.CreateDirectory(Output);
            var gallery=new Texture2D(4*360,3*440,TextureFormat.RGB24,false);
            var comparison=new Texture2D(3*500,600,TextureFormat.RGB24,false);
            try
            {
                for(int i=0;i<PlantCatalog.All.Count;i++)
                {
                    var species=PlantCatalog.All[i];
                    var p=PlantGenerator.Create("synthetic-visual",species.Id);
                    p.Counts=species.ExampleCounts();
                    while(!p.Mature) p=PlantGenerator.Advance(p);
                    var tile=RenderRecipe(p,360,440,32);
                    gallery.SetPixels((i%4)*360,(2-i/4)*440,360,440,tile.GetPixels());
                    Object.DestroyImmediate(tile);
                    if(i!=0) continue;
                    var old=PlantCodec.Clone(p);old.MeshVersion=1;
                    var before=RenderRecipe(old,500,600,40);
                    var after=RenderRecipe(p,500,600,40);
                    var reference=RenderReference(500,600);
                    comparison.SetPixels(0,0,500,600,before.GetPixels());
                    comparison.SetPixels(500,0,500,600,after.GetPixels());
                    comparison.SetPixels(1000,0,500,600,reference.GetPixels());
                    Object.DestroyImmediate(before);Object.DestroyImmediate(after);Object.DestroyImmediate(reference);
                    var closeup=new Texture2D(1000,500,TextureFormat.RGB24,false);
                    try
                    {
                        for(int style=0;style<2;style++)
                        {
                            var go=new GameObject("Synthetic flower detail");
                            try
                            {
                                go.AddComponent<ProceduralPlantView>().Render(style==0?old:p);
                                var head=p.Heads[0];var tip=PlantGenerator.HeadTip(p,head);
                                var focus=new Bounds(new Vector3((float)tip.X,(float)tip.Y,(float)tip.Z),Vector3.one);
                                var detail=Capture(go,focus,500,500,55,(float)(head.Diameter*head.Scale*.67));
                                closeup.SetPixels(style*500,0,500,500,detail.GetPixels());Object.DestroyImmediate(detail);
                            }
                            finally { Object.DestroyImmediate(go); }
                        }
                        closeup.Apply();File.WriteAllBytes(Output+"/chamomile-detail.png",closeup.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(closeup); }
                    foreach(int step in new[]{4,6,9,12})
                    {
                        var stage=PlantGenerator.Create("synthetic-visual",species.Id);
                        stage.Counts=species.ExampleCounts();
                        while(stage.GrowthStep<step) stage=PlantGenerator.Advance(stage);
                        var frame=RenderRecipe(stage,500,600,25);
                        File.WriteAllBytes(Output+"/chamomile-step-"+step+".png",frame.EncodeToPNG());
                        Object.DestroyImmediate(frame);
                    }
                }
                gallery.Apply();comparison.Apply();
                File.WriteAllBytes(Output+"/species-gallery.png",gallery.EncodeToPNG());
                File.WriteAllBytes(Output+"/chamomile-comparison.png",comparison.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(gallery);Object.DestroyImmediate(comparison); }
        }

        static Texture2D RenderRecipe(PlantRecipe p,int width,int height,float elevation)
        {
            var go=new GameObject("Synthetic plant render");
            try
            {
                go.AddComponent<ProceduralPlantView>().Render(p);
                Assert.IsTrue(go.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported);
                // Fixed framing across growth stages makes elongation and bloom visible.
                return Capture(go,new Bounds(new Vector3(0,.59f,0),new Vector3(1.1f,1.3f,1.1f)),width,height,elevation);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static Texture2D RenderReference(int width,int height)
        {
            var root=new GameObject("Chamomile refined reference");
            try
            {
                foreach(string palette in new[]{"Stem","Leaf","LeafLight","LeafDark","Ivory","PetalShade","Pollen","SeedLight"})
                {
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Prototype/RefinedMeshes/Chamomile_Refined_"+palette+".asset");
                    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototype/Refined_"+palette+".mat");
                    Assert.IsNotNull(mesh);Assert.IsNotNull(material);
                    var child=new GameObject(palette);child.transform.SetParent(root.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterial=material;
                }
                root.transform.localScale=Vector3.one*.55f;
                return Capture(root,new Bounds(new Vector3(0,.59f,0),new Vector3(1.1f,1.3f,1.1f)),width,height,40);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Texture2D Capture(GameObject root,Bounds bounds,int width,int height,float elevation,float size=.77f)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                preview.AddSingleGO(root);
                var camera=preview.camera;camera.orthographic=true;camera.orthographicSize=size;
                camera.nearClipPlane=.01f;camera.farClipPlane=10;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.95f,.93f,.86f);
                var rotation=Quaternion.Euler(elevation,-25,0);
                camera.transform.SetPositionAndRotation(bounds.center+rotation*(Vector3.back*3),rotation);
                preview.lights[0].intensity=1.15f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);
                preview.lights[1].intensity=.5f;
                preview.BeginStaticPreview(new Rect(0,0,width,height));preview.Render(true);
                var texture=preview.EndStaticPreview();
                var pixels=texture.GetPixels();var background=pixels[0];
                Assert.Greater(pixels.Count(c=>Mathf.Abs(c.r-background.r)+Mathf.Abs(c.g-background.g)+Mathf.Abs(c.b-background.b)>.15f),100,"Plant must actually appear in the URP render");
                Assert.Less(pixels.Count(c=>c.r>.9f&&c.b>.9f&&c.g<.1f),10,"No unsupported-shader magenta");
                return texture;
            }
            finally { preview.Cleanup(); }
        }
    }
}
