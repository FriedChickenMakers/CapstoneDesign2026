using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.EditorTools
{
    public static class KenneyGardenBuilder
    {
        const string Source = "Assets/Art/External/KenneyGarden/";
        const string Output = "Assets/Resources/GardenModels/";
        const string Materials = "Assets/Materials/KenneyGarden/";
        static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();

        public static void Build()
        {
            Directory.CreateDirectory(Output); Directory.CreateDirectory(Materials);
            Directory.CreateDirectory("Assets/Art/KenneyGardenDerived");
            AssetDatabase.Refresh(); Palette.Clear();
            foreach (var path in Directory.GetFiles(Source, "*.obj", SearchOption.AllDirectories))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = true; importer.importNormals = ModelImporterNormals.Import;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            BuildBase(); BuildEnvironments(); BuildVisitors();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            InstallInScene("Assets/Scenes/MockupMain.unity");
            Debug.Log("KENNEY_GARDEN_BUILD_PASS");
        }

        // Update only the island asset. Scene instances retain their references,
        // plant state, placements and camera settings through the existing GUIDs.
        public static void BuildIslandGeometry()
        {
            string path = Output + "GardenBase.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BuildSolidSurface(root.transform.Find("platform_grass").gameObject);
                foreach (string name in new[] { "rock_largeA", "rock_largeB" })
                {
                    var detachedUnderside = root.transform.Find(name);
                    if (detachedUnderside != null) UnityEngine.Object.DestroyImmediate(detachedUnderside.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("KENNEY_SOLID_ISLAND_BUILD_PASS");
        }

        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }
        static Material Mat(string name, Color color, Texture texture = null)
        {
            if (Palette.TryGetValue(name, out var result)) return result;
            string path = Materials + name + ".mat";
            result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.SetColor("_BaseColor", color); result.SetFloat("_Smoothness", .06f); result.SetTexture("_BaseMap", texture);
            result.SetFloat("_Cull", 0); result.doubleSidedGI = true;
            EditorUtility.SetDirty(result); Palette[name] = result; return result;
        }

        static Color ColorFor(string material)
        {
            if (material.Contains("grass")) return Hex("70955E");
            if (material.Contains("dirt")) return Hex("956C50");
            if (material.Contains("wood")) return Hex("AE8056");
            if (material.Contains("stone")) return Hex("A3AA95");
            if (material.Contains("colorWhite")) return Hex("FFF4D7");
            if (material.Contains("colorYellow")) return Hex("E5AE48");
            if (material.Contains("colorPurple")) return Hex("AA94C4");
            if (material.Contains("colorRed")) return Hex("DC967F");
            return Hex("C1B291");
        }

        static GameObject Model(string pack, string name, Transform parent, Vector3 at, float size, float yaw = 0, string variation = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + pack + "/" + name + ".obj");
            if (source == null) throw new InvalidOperationException("Missing Kenney source: " + name);
            var go = UnityEngine.Object.Instantiate(source, parent, false); go.name = name;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            GardenSceneModels.TryBounds(go.transform, parent, out var bounds);
            float span = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            go.transform.localScale *= size / span;
            GardenSceneModels.TryBounds(go.transform, parent, out bounds);
            go.transform.localPosition = at - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    string key = m.name.Replace(" (Instance)", "");
                    if (pack == "pets")
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "pets/Textures/colormap.png");
                        return Mat("PetsPalette", Color.white, texture);
                    }
                    Color color = ColorFor(key);
                    if (variation == "bud" && key.Contains("color")) color = Hex("94AD6F");
                    if (variation == "chamomile" && key.Contains("colorYellow")) color = Hex("FFF4D7");
                    if (variation == "chamomile" && key.Contains("colorWhite")) color = Hex("E5AE48");
                    if (variation == "underside") color = Hex(key.Contains("grass") ? "997455" : "805D47");
                    return Mat((variation ?? "Natural") + "_" + key, color);
                }).ToArray();
            }
            if (variation == "chamomile") RefineChamomile(go);
            GardenSceneModels.ConfigureRenderers(go); return go;
        }
        static Transform Root(string name, Transform parent = null)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static void Save(Transform root, string name)
        {
            GardenSceneModels.ConfigureRenderers(root.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Output + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        static void BuildBase()
        {
            var root = Root("Kenney floating garden");
            var ground = Model("nature", "platform_grass", root, Vector3.zero, 3.3f);
            BuildSolidSurface(ground);
            GardenSceneModels.TryBounds(ground.transform, root, out var groundBounds);
            ground.transform.localPosition += Vector3.up * (.08f - groundBounds.max.y);
            var surface = root.gameObject.AddComponent<GardenSurface>(); surface.surface = ground.GetComponentInChildren<MeshFilter>();
            // Low perimeter planting leaves the three purchased/visitor footprints
            // and the central growth area open; no invisible overlapping primitives.
            foreach (var at in new[] { new Vector2(-.97f,.65f), new Vector2(.91f,.74f), new Vector2(-1.12f,-.47f), new Vector2(.98f,-.58f) })
            {
                float y = surface.HeightInSpace(root, at);
                Model("nature", "grass_leafs", root, new Vector3(at.x,y,at.y), .27f, at.x * 97);
                Model("nature", "rock_smallA", root, new Vector3(at.x+.1f,y-.025f,at.y+.04f), .18f, at.y*55);
            }
            var plant = Root("Plant_GrowthParts", root).gameObject.AddComponent<PrototypePlantView>();
            plant.seed = Root("01_Seed", plant.transform).gameObject;
            plant.sprout = Root("02_Sprout", plant.transform).gameObject;
            plant.bud = Root("03_Buds", plant.transform);
            plant.blossomAnchor = Root("04_Bloom", plant.transform);
            plant.chamomileFlower = Root("Chamomile", plant.blossomAnchor).gameObject;
            plant.hydrangeaFlower = Root("Hydrangea", plant.blossomAnchor).gameObject;
            Model("nature", "crops_dirtSingle", plant.seed.transform, new Vector3(0,.065f,.12f), .34f);
            Model("nature", "crops_leafsStageA", plant.sprout.transform, new Vector3(0,.065f,.12f), .44f);
            Model("nature", "flower_yellowC", plant.bud, new Vector3(0,.065f,.12f), .57f, 10, "bud");
            // An asymmetric three-stem patch, using Kenney's daisy petal topology.
            foreach (var stem in new[] { new Vector4(-.18f,.10f,.62f,18), new Vector4(.16f,.23f,.78f,-26), new Vector4(.04f,-.08f,.57f,76) })
                Model("nature", "flower_yellowC", plant.chamomileFlower.transform, new Vector3(stem.x,.065f,stem.y), stem.z, stem.w, "chamomile");
            Model("nature", "flower_purpleC", plant.hydrangeaFlower.transform, new Vector3(0,.065f,.12f), .78f);
            plant.RenderVisual(3, FlowerShape.Chamomile);
            Save(root,"GardenBase");
        }

        static void BuildEnvironments()
        {
            var bed = Root("Flower bed");
            Model("nature", "platform_grass", bed, Vector3.zero, .60f);
            for (int i = 0; i < 3; i++)
            {
                Model("nature", i == 1 ? "flower_purpleC" : "flower_redC", bed, new Vector3((i-1)*.16f,.055f,(i%2)*.09f), .22f, i*32);
            }
            Model("nature", "fence_simpleLow", bed, new Vector3(0,0,.18f), .54f);
            Save(bed,"FlowerBed");
            var rest = Root("Rest corner");
            Model("furniture", "bench", rest, new Vector3(.02f,0,.10f), .56f, 180);
            Model("furniture", "tableCoffeeSquare", rest, new Vector3(-.02f,0,-.18f), .24f);
            Save(rest,"RestCorner");
        }

        static void BuildVisitors()
        {
            string[] models = { "cat", "hog", "fox", "koala", "chick", "fox", "bunny", "beaver", "hog", "beaver", "beaver", "chick" };
            for (int i = 0; i < models.Length; i++)
            {
                var visitor = Root("Visitor" + (i+1).ToString("00"));
                var body = Model("pets", "animal-" + models[i], visitor, Vector3.zero, .41f);
                // Variants retain the authored eyes, face and paws. Species that
                // are not in the Kenney pack receive explicit mesh/palette edits.
                AdaptVisitor(body, i+1);
                Save(visitor, "Visitor" + (i+1).ToString("00"));
            }
        }

        static void RefineChamomile(GameObject flower)
        {
            foreach(var filter in flower.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
                var materials=filter.GetComponent<Renderer>().sharedMaterials;
                var vertices=mesh.vertices.ToList();var triangles=new int[mesh.subMeshCount][];
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    triangles[sub]=mesh.GetTriangles(sub);
                    if(!materials[sub].name.Contains("colorYellow"))continue;
                    var original=triangles[sub].Distinct().Select(i=>vertices[i]).ToArray();
                    float minX=original.Min(v=>v.x),maxX=original.Max(v=>v.x),minZ=original.Min(v=>v.z),maxZ=original.Max(v=>v.z);
                    float y=original.Min(v=>v.y)+.020f;
                    var center=new Vector3((minX+maxX)*.5f,y,(minZ+maxZ)*.5f);
                    float radius=Mathf.Max(maxX-minX,maxZ-minZ)*.5f;
                    var petals=new List<int>();
                    // Open the source's cup into six faceted daisy rays. The Kenney
                    // stem, leaves and center remain, with the same flat normals.
                    for(int petal=0;petal<6;petal++)
                    {
                        float a=petal*Mathf.PI/3;
                        var along=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                        var across=new Vector3(-along.z,0,along.x);
                        var points=new[]{center+along*radius*.19f,center+along*radius*.59f+across*radius*.25f,
                            center+along*radius,center+along*radius*.59f-across*radius*.25f};
                        var crown=center+along*radius*.56f+Vector3.up*.005f;
                        for(int side=0;side<4;side++)
                        {
                            int start=vertices.Count;vertices.Add(crown);vertices.Add(points[side]);vertices.Add(points[(side+1)%4]);
                            petals.Add(start);petals.Add(start+1);petals.Add(start+2);
                        }
                    }
                    triangles[sub]=petals.ToArray();
                }
                mesh.Clear();mesh.SetVertices(vertices);mesh.subMeshCount=triangles.Length;
                for(int sub=0;sub<triangles.Length;sub++)mesh.SetTriangles(triangles[sub],sub);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                filter.sharedMesh=SaveMesh(mesh,"Chamomile-open-petals");
            }
        }

        static void BuildSolidSurface(GameObject ground)
        {
            // Keep the entire existing meadow and sloped rim: some furniture
            // footprints touch that rim. Move only the bottom cap, then bridge
            // its boundary to the original rim to form one closed soil volume.
            // Always derive from Kenney's source, never a previous generated mesh.
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "nature/platform_grass.obj");
            var sourceMesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            foreach(var filter in ground.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=UnityEngine.Object.Instantiate(sourceMesh);
                var authored=mesh.vertices;var vertices=authored.ToList();
                float top=mesh.bounds.max.y,bottom=mesh.bounds.min.y;
                var center=mesh.bounds.center;
                float verticalScale=ground.transform.parent.InverseTransformVector(filter.transform.TransformVector(Vector3.up)).magnitude;
                float baseY=top-.98f/verticalScale;
                for(int i=0;i<vertices.Count;i++)
                {
                    var p=vertices[i];if(p.y>bottom+.001f)p.y=top;vertices[i]=p;
                }
                var triangles=new List<int>[mesh.subMeshCount];
                var cornerIds=new Dictionary<Vector3,int>();var corners=new List<Vector3>();
                var capEdges=new Dictionary<(int,int),(int a,int b,int material,int count)>();
                Vector3 Lower(Vector3 p) => new Vector3(center.x+(p.x-center.x)*.42f,baseY,center.z+(p.z-center.z)*.42f);
                int Corner(Vector3 p)
                {
                    if(!cornerIds.TryGetValue(p,out int id)){id=corners.Count;corners.Add(p);cornerIds[p]=id;}
                    return id;
                }
                void Edge(Vector3 from,Vector3 to,int material)
                {
                    int a=Corner(from),b=Corner(to);var key=(Math.Min(a,b),Math.Max(a,b));
                    if(capEdges.TryGetValue(key,out var edge))capEdges[key]=(edge.a,edge.b,edge.material,edge.count+1);
                    else capEdges[key]=(a,b,material,1);
                }
                void Triangle(int material,Vector3 a,Vector3 b,Vector3 c)
                {
                    int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                    triangles[material].AddRange(new[]{start,start+1,start+2});
                }
                for(int sub=0;sub<triangles.Length;sub++)
                {
                    triangles[sub]=new List<int>();var sourceTriangles=mesh.GetTriangles(sub);
                    for(int i=0;i<sourceTriangles.Length;i+=3)
                    {
                        var a=authored[sourceTriangles[i]];var b=authored[sourceTriangles[i+1]];var c=authored[sourceTriangles[i+2]];
                        if(a.y<=bottom+.001f && b.y<=bottom+.001f && c.y<=bottom+.001f)
                        {
                            Triangle(sub,Lower(a),Lower(b),Lower(c));Edge(a,b,sub);Edge(b,c,sub);Edge(c,a,sub);
                        }
                        else triangles[sub].AddRange(new[]{sourceTriangles[i],sourceTriangles[i+1],sourceTriangles[i+2]});
                    }
                }
                foreach(var edge in capEdges.Values.Where(e=>e.count==1))
                {
                    var a=corners[edge.a];var b=corners[edge.b];
                    Triangle(edge.material,a,b,Lower(b));Triangle(edge.material,a,Lower(b),Lower(a));
                }
                mesh.Clear();mesh.SetVertices(vertices);mesh.subMeshCount=triangles.Length;
                for(int sub=0;sub<triangles.Length;sub++)mesh.SetTriangles(triangles[sub],sub);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                filter.sharedMesh=SaveMesh(mesh,"Island-level-meadow");
            }
        }

        static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path="Assets/Art/KenneyGardenDerived/"+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);saved.name=name;
            // CopySerialized preserves the asset GUID but does not invalidate an
            // existing graphics buffer. Same-process previews need the new data;
            // retain the CPU copy for GardenSurface's placement intersections.
            saved.UploadMeshData(false);
            EditorUtility.SetDirty(saved);UnityEngine.Object.DestroyImmediate(mesh);return saved;
        }

        static void AdaptVisitor(GameObject body, int species)
        {
            // Geometry and palette adaptations keep the saved species IDs useful,
            // while sharing the original Kenney face/eye/paw vocabulary.
            var texturePath=Source+"pets/Textures/colormap.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Vector2 PaletteUv(Color target)
            {
                var pixels=texture.GetPixels();float best=float.MaxValue;int index=0;
                for(int i=0;i<pixels.Length;i++)
                {
                    var d=(Vector3)new Vector4(pixels[i].r-target.r,pixels[i].g-target.g,pixels[i].b-target.b,0);
                    if(d.sqrMagnitude<best){best=d.sqrMagnitude;index=i;}
                }
                return new Vector2((index%texture.width+.5f)/texture.width,(index/texture.width+.5f)/texture.height);
            }
            var ivory=PaletteUv(Hex("EEE9DA"));var sand=PaletteUv(Hex("D9AD7E"));var rust=PaletteUv(Hex("B85C3D"));var brown=PaletteUv(Hex("8B6553"));
            foreach(var filter in body.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;var uv=mesh.uv;
                for(int i=0;i<vertices.Length;i++)
                {
                    var c=texture.GetPixelBilinear(uv[i].x,uv[i].y);Color.RGBToHSV(c,out float h,out float saturation,out float value);
                    if(species==3 && h<.12f && saturation>.3f)uv[i]=sand;
                    if((species==5 || species==12) && h>.09f && h<.22f && saturation>.3f)uv[i]=ivory;
                    if(species==6 && ((value>.55f && saturation<.22f && vertices[i].z<.42f) || (h<.12f && saturation>.3f)))uv[i]=rust;
                    if(species==6 && vertices[i].y>1.25f && filter.name=="body")vertices[i].y=1.25f+(vertices[i].y-1.25f)*.52f;
                    if(species==6 && filter.name=="tail" && ((int)Mathf.Floor(vertices[i].z*7)&1)==0)uv[i]=brown;
                    if((species==2 || species==9) && saturation>.18f && h<.15f)uv[i]=brown;
                    // Wide, shallow duck bill; compact bird keeps the original beak.
                    if(species==12 && vertices[i].z>.625f && vertices[i].y<1.1f)vertices[i].x*=1.5f;
                    // The large incisors belong to the source beaver, not capybara/otter.
                    if((species==10 || species==11) && vertices[i].z>.66f && vertices[i].y<.7f && saturation<.1f && value>.8f)
                    {vertices[i].z=.64f;uv[i]=brown;}
                }
                mesh.vertices=vertices;mesh.uv=uv;mesh.RecalculateBounds();
                filter.sharedMesh=SaveMesh(mesh,"Visitor"+species.ToString("00")+"-"+filter.name);
            }
            if (species == 4) body.transform.localScale = Vector3.Scale(body.transform.localScale,new Vector3(.85f,1.15f,1));
            if (species == 10) body.transform.localScale = Vector3.Scale(body.transform.localScale,new Vector3(1.1f,.85f,1.2f));
            if (species == 11) body.transform.localScale = Vector3.Scale(body.transform.localScale,new Vector3(.85f,.82f,1.3f));
            var tail = body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "tail");
            if (tail != null && species == 10) UnityEngine.Object.DestroyImmediate(tail.gameObject);
            if (tail != null && species == 8) tail.localScale = new Vector3(.75f,1,1.12f);
            if (tail != null && species == 11) tail.localScale = new Vector3(.42f,1,1.35f);
            if(species==2 || species==9)
            {
                var shell=Model("nature","rock_smallA",body.transform.parent,new Vector3(0,.18f,-.045f),.28f,10,"underside");
                shell.name=species==2?"Armadillo shell":"Hedgehog back";
                shell.transform.localScale=Vector3.Scale(shell.transform.localScale,new Vector3(1,.48f,1.3f));
                if(species==9)
                    for(int i=0;i<3;i++)Model("nature","grass_leafs",body.transform.parent,new Vector3((i-1)*.07f,.21f,-.08f),.13f,i*55,"underside");
            }
        }

        static void InstallInScene(string scene)
        {
            EditorSceneManager.OpenScene(scene);
            var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            if (home == null) throw new InvalidOperationException("Integrated home missing.");
            var previous = home.gardenRoot;
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "GardenBase.prefab"));
            replacement.transform.SetParent(previous.parent, false);
            replacement.transform.SetPositionAndRotation(previous.position, previous.rotation);
            home.gardenRoot = replacement.transform;
            home.plant = replacement.GetComponentInChildren<PrototypePlantView>(true);
            foreach (var layout in home.homeCanvas.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true)) layout.gardenRoot = replacement.transform;
            foreach (var orbit in home.homeCanvas.GetComponentsInChildren<PrototypeIslandDrag>(true))
            { orbit.focus = new Vector3(0,.08f,0); orbit.ResetView(); }
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
            EditorUtility.SetDirty(home);
            EditorSceneManager.MarkSceneDirty(home.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
