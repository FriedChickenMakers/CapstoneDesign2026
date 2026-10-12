using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Synthetic garden only: no saved user state or device captures are read.
    public static class SolidIslandValidation
    {
        const string PrefabPath = "Assets/Resources/GardenModels/GardenBase.prefab";
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-12T12:00:00+09:00");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Preview-KST",TimeSpan.FromHours(9),"KST","KST");
        }
        sealed class Store : IStateStore
        {
            GardenState state;
            public GardenState Load() => state == null ? null : StateCodec.Clone(state);
            public void Save(GardenState value) => state = StateCodec.Clone(value);
        }
        static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException(message); }

        public static void BuildAndCapture() => Run(true);
        public static void CaptureExisting() => Run(false);
        static void Run(bool rebuild)
        {
            string output=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ?? "artifacts/solid-island";
            Directory.CreateDirectory(output);
            var before=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var placements=before.GetComponentsInChildren<Transform>(true)
                .Where(t=>!AnimationUtility.CalculateTransformPath(t,before.transform).StartsWith("rock_large",StringComparison.Ordinal))
                .ToLookup(t=>AnimationUtility.CalculateTransformPath(t,before.transform),t=>(t.localPosition,t.localRotation,t.localScale));
            if(rebuild)KenneyGardenBuilder.BuildIslandGeometry();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach(var item in placements)
            {
                var retained=prefab.GetComponentsInChildren<Transform>(true)
                    .Where(t=>AnimationUtility.CalculateTransformPath(t,prefab.transform)==item.Key)
                    .Select(t=>(t.localPosition,t.localRotation,t.localScale));
                Require(item.SequenceEqual(retained),
                    "Existing plant/perimeter transform changed: "+item.Key);
            }
            var report=new List<string> { "All retained prefab transforms are unchanged: "+placements.Sum(group=>group.Count()) };
            ValidateSolid(prefab,report);

            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            AndroidPlatformBridge.UseMockProvider();
            var home=UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            Require(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(home.gardenRoot.gameObject)==PrefabPath,
                "Actual home is not linked to the updated GardenBase prefab.");
            Require(home.gardenRoot.Find("rock_largeA")==null && home.gardenRoot.Find("rock_largeB")==null,
                "Detached underside survived in the actual scene.");
            Require(home.gardenRoot.GetComponent<GardenSurface>().surface.sharedMesh==prefab.GetComponent<GardenSurface>().surface.sharedMesh,
                "Actual scene overrides the derived island mesh.");
            report.Add("Actual MockupMain home uses updated prefab/mesh, no independent underside or mesh override: PASS");
            using var service=new GardenStateService(new Store(),new Clock(),new DemoBalanceConfig { VisitorChancePercent=0 },new LegacySnapshot { Nutrient=100 });
            home.activity.Initialize(service,true);
            var state=service.Snapshot;state.LegacyGrowth=2f;
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId="environment:E01",Slot="left" });
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId="environment:E02",Slot="right" });
            home.navigation.ShowIsland();
            var placementFailures=new List<string>();
            for(int visitor=1;visitor<=12;visitor++)
            {
                state.DailyDecisions.Last().VisitorId="animal:A"+visitor.ToString("00");
                home.RefreshFromState(state);
                try { ValidatePlacements(home); }
                catch(Exception error) { placementFailures.Add("Visitor "+visitor+": "+error.Message); }
            }
            report.AddRange(placementFailures);
            if(placementFailures.Count==0)report.Add("Both purchased environments and all 12 visitors: grounded at unchanged meadow, supported corners, no footprint collision: PASS");
            state.DailyDecisions.Last().VisitorId="animal:A07";home.RefreshFromState(state);
            var camera=home.gardenCamera;
            var sky=GardenSkyEnvironment.Ensure(camera,home.gardenVisuals.transform);sky.SetTimeForPreview(12);
            int captures=0;
            foreach(float pitch in new[]{-85f,-45f,0f,28f,85f})
                foreach(float yaw in new[]{0f,90f,180f,270f})
                {
                    var rotation=Quaternion.Euler(pitch,yaw,0);
                    camera.transform.SetPositionAndRotation(new Vector3(0,-.15f,0)+rotation*Vector3.back*5.8f,rotation);
                    camera.orthographicSize=1.95f;camera.aspect=1;camera.ResetProjectionMatrix();
                    Render(camera,Path.Combine(output,"island-yaw"+yaw+"-pitch"+pitch+".png"));captures++;
                }
            report.Add("Rendered full yaw quadrants at lower limit -85, -45, horizon, normal 28, upper limit 85: "+captures);
            File.WriteAllLines(Path.Combine(output,"validation.txt"),report);
            Require(placementFailures.Count==0,"Placement validation failed: "+string.Join("; ",placementFailures));
            Debug.Log("SOLID_ISLAND_VALIDATION_PASS "+string.Join("; ",report));
        }

        static void ValidateSolid(GameObject prefab,List<string> report)
        {
            var surface=prefab.GetComponent<GardenSurface>();var filter=surface.surface;var mesh=filter.sharedMesh;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/External/KenneyGarden/nature/platform_grass.obj").GetComponentInChildren<MeshFilter>().sharedMesh;
            var authored=source.vertices;var vertices=mesh.vertices;
            Require(authored.Length<vertices.Length,"Connected lower side faces were not generated.");
            int preserved=0;
            for(int i=0;i<authored.Length;i++) if(authored[i].y>source.bounds.min.y+.001f)
            {
                Require(vertices[i].x==authored[i].x && vertices[i].z==authored[i].z,"Meadow footprint moved.");
                float y=prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i])).y;
                Require(Mathf.Abs(y-.08f)<.00001f,"Meadow height moved.");preserved++;
            }
            else Require(vertices[i]==authored[i],"Original supporting rim moved.");
            var sourceMaterials=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/External/KenneyGarden/nature/platform_grass.obj").GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
            Require(source.subMeshCount==mesh.subMeshCount && materials.Length==sourceMaterials.Length,"Island material partitions changed.");
            int retainedFaces=0;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                Require(materials[sub].name=="Natural_"+sourceMaterials[sub].name.Replace(" (Instance)",""),"Grass/dirt material mapping changed.");
                var current=mesh.GetTriangles(sub);var original=source.GetTriangles(sub);
                var faces=new HashSet<(int,int,int)>();
                for(int i=0;i<current.Length;i+=3)faces.Add((current[i],current[i+1],current[i+2]));
                for(int i=0;i<original.Length;i+=3)
                {
                    int a=original[i],b=original[i+1],c=original[i+2];
                    if(authored[a].y<=source.bounds.min.y+.001f && authored[b].y<=source.bounds.min.y+.001f && authored[c].y<=source.bounds.min.y+.001f)continue;
                    Require(faces.Contains((a,b,c)),"Original meadow/rim face changed.");retainedFaces++;
                }
                if(sourceMaterials[sub].name.Contains("grass"))Require(original.SequenceEqual(current),"Grass triangle indices changed.");
            }
            var welded=new Dictionary<Vector3,int>();var indices=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                if(!welded.TryGetValue(vertices[i],out int id)){id=welded.Count;welded[vertices[i]]=id;}
                indices[i]=id;
            }
            var tris=mesh.triangles;var edges=new Dictionary<(int,int),List<(int,int)>>();
            for(int i=0;i<tris.Length;i+=3) for(int side=0;side<3;side++)
            {
                int a=indices[tris[i+side]],b=indices[tris[i+(side+1)%3]];
                var key=(Math.Min(a,b),Math.Max(a,b));
                if(!edges.TryGetValue(key,out var faces))edges[key]=faces=new List<(int,int)>();
                faces.Add((i/3,a<b ? 1 : -1));
            }
            Require(edges.Values.All(e=>e.Count==2 && e[0].Item2+e[1].Item2==0),"Island has an open or inconsistently wound edge.");
            var seen=new HashSet<int>{0};var pending=new Queue<int>();pending.Enqueue(0);
            var neighbors=new List<int>[tris.Length/3];for(int i=0;i<neighbors.Length;i++)neighbors[i]=new List<int>();
            foreach(var pair in edges.Values){neighbors[pair[0].Item1].Add(pair[1].Item1);neighbors[pair[1].Item1].Add(pair[0].Item1);}
            while(pending.Count>0) foreach(int next in neighbors[pending.Dequeue()]) if(seen.Add(next))pending.Enqueue(next);
            Require(seen.Count==tris.Length/3,"Island contains disconnected shells.");
            Require(welded.Count-edges.Count+tris.Length/3==2,"Island is not a single closed genus-zero shell.");
            for(int i=0;i<tris.Length;i+=3) for(int j=0;j<i;j+=3) for(int side=0;side<3;side++)
            {
                Require(!Crosses(vertices[tris[i+side]],vertices[tris[i+(side+1)%3]],vertices[tris[j]],vertices[tris[j+1]],vertices[tris[j+2]]) &&
                    !Crosses(vertices[tris[j+side]],vertices[tris[j+(side+1)%3]],vertices[tris[i]],vertices[tris[i+1]],vertices[tris[i+2]]),"Island side faces intersect.");
            }
            report.Add("Closed connected oriented shell: "+welded.Count+" positions, "+edges.Count+" edges, "+tris.Length/3+" triangles, Euler 2; no crossing faces");
            report.Add("Kenney meadow/rim positions and "+retainedFaces+" faces preserved; "+preserved+" upper vertices at y=0.08; grass partition/material unchanged");
        }

        static bool Crosses(Vector3 p,Vector3 q,Vector3 a,Vector3 b,Vector3 c)
        {
            var direction=q-p;var e=b-a;var f=c-a;var h=Vector3.Cross(direction,f);float determinant=Vector3.Dot(e,h);
            if(Mathf.Abs(determinant)<.00000001f)return false;
            var s=p-a;float u=Vector3.Dot(s,h)/determinant;
            if(u<=.00001f || u>=.99999f)return false;
            var k=Vector3.Cross(s,e);float v=Vector3.Dot(direction,k)/determinant;
            if(v<=.00001f || u+v>=.99999f)return false;
            float t=Vector3.Dot(f,k)/determinant;return t>.00001f && t<.99999f;
        }

        static void ValidatePlacements(GardenHomePresenter home)
        {
            var footprints=new List<Bounds>();
            GardenSceneModels.TryBounds(home.plant.transform,home.gardenRoot,out var plant);
            foreach(Transform item in home.gardenRoot.Find("Saved garden additions"))
            {
                GardenSceneModels.TryBounds(item,home.gardenRoot,out var b);
                float y=GardenSceneModels.GroundHeight(home.gardenRoot,new Vector2(b.center.x,b.center.z));
                Require(Mathf.Abs(y-.08f)<.00001f && Mathf.Abs(b.min.y-y-.002f)<.008f,"Decoration no longer grounded: "+item.name);
                foreach(float x in new[]{b.min.x,b.max.x})foreach(float z in new[]{b.min.z,b.max.z})
                {
                    try { Require(Mathf.Abs(GardenSceneModels.GroundHeight(home.gardenRoot,new Vector2(x,z))-y)<.01f,"Unsupported decoration corner"); }
                    catch(Exception error) { throw new InvalidOperationException(item.name+" corner "+x+","+z+": "+error.Message+"; original surface="+OriginalHeight(home,new Vector2(x,z))); }
                }
                foreach(var other in footprints.Append(plant))
                    Require(!(b.min.x<other.max.x && b.max.x>other.min.x && b.min.z<other.max.z && b.max.z>other.min.z),"Decoration footprint collision: "+item.name);
                footprints.Add(b);
            }
        }

        static string OriginalHeight(GardenHomePresenter home,Vector2 point)
        {
            var filter=home.gardenRoot.GetComponent<GardenSurface>().surface;var current=filter.sharedMesh;
            var original=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/External/KenneyGarden/nature/platform_grass.obj").GetComponentInChildren<MeshFilter>().sharedMesh);
            var vertices=original.vertices;float top=original.bounds.max.y,bottom=original.bounds.min.y;
            for(int i=0;i<vertices.Length;i++)if(vertices[i].y>bottom+.001f)vertices[i].y=top;
            original.vertices=vertices;filter.sharedMesh=original;
            try { return GardenSceneModels.GroundHeight(home.gardenRoot,point).ToString("F6"); }
            catch(InvalidOperationException) { return "outside"; }
            finally { filter.sharedMesh=current;UnityEngine.Object.DestroyImmediate(original); }
        }

        static void Render(Camera camera,string path)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(800,800,24){antiAliasing=4};var image=new Texture2D(800,800,TextureFormat.RGBA32,false);
            camera.targetTexture=target;camera.Render();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,800,800),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=active;camera.targetTexture=previous;target.Release();
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
