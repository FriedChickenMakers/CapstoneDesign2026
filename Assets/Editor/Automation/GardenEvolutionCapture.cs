using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CapstoneDesign.EditorTools
{
    public static class GardenEvolutionCapture
    {
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
        public static void Capture()
        {
            string output = Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ?? "artifacts/evolution-20261012/garden";
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            AndroidPlatformBridge.UseMockProvider();
            var home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            var loop = home.activity;
            using var service = new GardenStateService(new Store(),new Clock(),new DemoBalanceConfig { VisitorChancePercent=0 },new LegacySnapshot { Nutrient=100 });
            loop.Initialize(service,true);
            var state = service.Snapshot;
            state.LegacyGrowth = 2f;
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId="environment:E01",Slot="left" });
            state.Environments.Add(new EnvironmentPlacement { EnvironmentId="environment:E02",Slot="right" });
            state.DailyDecisions.Last().VisitorId="animal:A07";
            home.navigation.ShowIsland();
            home.RefreshFromState(state);
            var sky = GardenSkyEnvironment.Ensure(home.gardenCamera,home.gardenVisuals.transform);
            var camera = home.gardenCamera;
            camera.aspect=1;camera.orthographicSize=1.9f;
            camera.transform.SetPositionAndRotation(new Vector3(0,.08f,0)+Quaternion.Euler(28,-25,0)*Vector3.back*5.8f,Quaternion.Euler(28,-25,0));
            var report = new List<string>();
            foreach (var item in new[] { ("morning",9.0), ("day",12.0), ("afternoon",15.0), ("sunset",17.6), ("night",23.0) })
            {
                sky.SetTimeForPreview(item.Item2);
                Render(camera,Path.Combine(output,item.Item1+".png"),1200,1200);
                if(sky.KeyLight.shadows==LightShadows.None)throw new Exception("Directional shadows disabled");
                report.Add(item.Item1+" hour="+item.Item2+" daylight="+sky.Daylight+" direction="+sky.SunDirection);
            }
            var morning=GardenSkyEnvironment.SolarDirection(9);var noon=GardenSkyEnvironment.SolarDirection(12);
            var afternoon=GardenSkyEnvironment.SolarDirection(15);var night=GardenSkyEnvironment.SolarDirection(23);
            if(morning.x<=0 || afternoon.x>=0 || noon.y<=morning.y || night.y>=0 || Vector3.Angle(morning,afternoon)<60)
                throw new Exception("Local clock sun arc does not travel from morning to afternoon/night.");
            sky.SetTimeForPreview(23);
            if(sky.Daylight>.01f || Vector3.Dot(-sky.KeyLight.transform.forward,sky.MoonDirection)<.99f)
                throw new Exception("Night lighting is not aligned with the moon.");
            report.Add("Local clock directions: morning/noon/afternoon/night PASS");
            sky.SetTimeForPreview(12);
            var cloudStart=new ParticleSystem.Particle[24];var cloudEnd=new ParticleSystem.Particle[24];
            sky.UpdateClouds(0);int startCount=sky.Clouds.GetParticles(cloudStart);
            Render(camera,Path.Combine(output,"clouds-0s.png"),1000,1000);
            sky.UpdateClouds(60);int endCount=sky.Clouds.GetParticles(cloudEnd);
            Render(camera,Path.Combine(output,"clouds-60s.png"),1000,1000,60);
            int moved=0;for(int i=0;i<Math.Min(startCount,endCount);i++)if(Vector3.Distance(cloudStart[i].position,cloudEnd[i].position)>.05f)moved++;
            if(startCount!=24 || endCount!=24 || moved<12)throw new Exception("Cloud particles did not move over sixty seconds.");
            report.Add("Cloud movement: "+moved+" / "+startCount+" particles moved after 60 s");
            sky.UpdateClouds(0);
            var shadowed=Render(camera,Path.Combine(output,"shadows-on.png"),1000,1000);
            sky.KeyLight.shadows=LightShadows.None;
            var unshadowed=Render(camera,Path.Combine(output,"shadows-off.png"),1000,1000);
            sky.KeyLight.shadows=LightShadows.Soft;
            int darker=0;long delta=0;
            for(int y=150;y<650;y++)for(int x=150;x<850;x++)
            {
                int i=y*1000+x;int difference=unshadowed[i].r+unshadowed[i].g+unshadowed[i].b-shadowed[i].r-shadowed[i].g-shadowed[i].b;
                if(difference>12)darker++;delta+=difference;
            }
            if(darker<500 || delta<20000)throw new Exception("Real shadows did not darken rendered garden pixels: "+darker+" / "+delta);
            report.Add("Rendered shadow comparison: "+darker+" visibly darkened garden pixels; brightness delta="+delta);
            for(int i=1;i<=12;i++)
            {
                state.DailyDecisions.Last().VisitorId="animal:A"+i.ToString("00");home.RefreshFromState(state);
                camera.aspect=1;camera.orthographicSize=1.9f;
                Render(camera,Path.Combine(output,"visitor-"+i.ToString("00")+".png"),1000,1000);
                ValidatePlacement(home,report);
            }
            for(int stage=0;stage<4;stage++)
            {
                state.LegacyGrowth=stage*.1f;home.RefreshFromState(state);
                camera.aspect=1;camera.orthographicSize=1.9f;
                Render(camera,Path.Combine(output,"growth-"+stage+".png"),1000,1000);
            }
            sky.SetTimeForPreview(12);
            foreach(var angles in new[]{new Vector2(0,28),new Vector2(90,28),new Vector2(180,28),new Vector2(270,28),new Vector2(-25,75),new Vector2(-25,-60)})
            {
                var rotation=Quaternion.Euler(angles.y,angles.x,0);
                camera.transform.SetPositionAndRotation(new Vector3(0,.08f,0)+rotation*Vector3.back*5.8f,rotation);
                sky.UpdateClouds(20);camera.orthographicSize=1.9f;
                Render(camera,Path.Combine(output,"orbit-"+angles.x+"-"+angles.y+".png"),1000,1000);
            }
            foreach(var celestial in new[]{("sun",12.0),("moon",23.0)})
            {
                sky.SetTimeForPreview(celestial.Item2);
                var direction=celestial.Item1=="sun"?sky.SunDirection:sky.MoonDirection;
                camera.transform.SetPositionAndRotation(new Vector3(0,2,0),Quaternion.LookRotation(direction,Vector3.forward));
                sky.UpdateClouds(20);
                Render(camera,Path.Combine(output,"celestial-"+celestial.Item1+".png"),1000,1000);
            }
            sky.SetTimeForPreview(12);
            home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true)?.ResetView();
            home.RefreshFromState(state);
            CaptureHome(home,sky,new Vector2Int(900,1950),Path.Combine(output,"home-phone.png"));
            CaptureHome(home,sky,new Vector2Int(1200,800),Path.Combine(output,"home-landscape.png"));
            File.WriteAllLines(Path.Combine(output,"validation.txt"),report);
            Debug.Log("GARDEN_EVOLUTION_CAPTURE_PASS "+report.Count);
        }
        static void CaptureHome(GardenHomePresenter home,GardenSkyEnvironment sky,Vector2Int size,string path)
        {
            // The first UI pass changes the camera aspect via dynamic card layout.
            // Settle those canvases before taking the actual garden/UI frame.
            LocalLoopPreview.Render(size,path);
            Canvas.ForceUpdateCanvases();
            foreach(var layout in home.homeCanvas.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true))layout.Apply();
            sky.UpdateClouds(0);home.gardenCamera.Render();
            LocalLoopPreview.Render(size,path);
        }

        static void ValidatePlacement(GardenHomePresenter home,List<string> report)
        {
            var additions=home.gardenRoot.Find("Saved garden additions");
            var footprints=new List<Bounds>();
            foreach(Transform item in additions)
            {
                GardenSceneModels.TryBounds(item,home.gardenRoot,out var b);
                float y=GardenSceneModels.GroundHeight(home.gardenRoot,new Vector2(b.center.x,b.center.z));
                if(Mathf.Abs(b.min.y-y-.002f)>.008f)throw new Exception("Floating model: "+item.name);
                foreach(float x in new[]{b.min.x,b.max.x})foreach(float z in new[]{b.min.z,b.max.z})
                    if(Mathf.Abs(GardenSceneModels.GroundHeight(home.gardenRoot,new Vector2(x,z))-y)>.01f)
                        throw new Exception("Decoration straddles an unsupported ledge: "+item.name);
                foreach(var other in footprints)
                    if(b.min.x<other.max.x && b.max.x>other.min.x && b.min.z<other.max.z && b.max.z>other.min.z)
                        throw new Exception("Overlapping decoration: "+item.name);
                GardenSceneModels.TryBounds(home.plant.transform,home.gardenRoot,out var plantBounds);
                if(b.min.x<plantBounds.max.x && b.max.x>plantBounds.min.x && b.min.z<plantBounds.max.z && b.max.z>plantBounds.min.z)
                    throw new Exception("Decoration intersects the fully grown plant footprint: "+item.name+" plant="+plantBounds);
                foreach(var renderer in item.GetComponentsInChildren<Renderer>())
                    if(renderer.shadowCastingMode==ShadowCastingMode.Off || !renderer.receiveShadows || renderer.sharedMaterials.Any(m=>m.shader.name.Contains("Unlit")))
                        throw new Exception("Unlit/no-shadow garden model: "+item.name);
                if(item.name.StartsWith("Visitor",StringComparison.Ordinal))
                {
                    var feet=item.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("leg-",StringComparison.Ordinal)).ToArray();
                    float footHeight=feet.Min(f=>{GardenSceneModels.TryBounds(f.transform,home.gardenRoot,out var foot);return foot.min.y;});
                    if(Mathf.Abs(footHeight-y-.002f)>.008f)throw new Exception("Actual paws float above ground: "+item.name+" "+footHeight);
                }
                footprints.Add(b);report.Add(item.name+" grounded="+b.min.y+" footprint="+b.size);
            }
        }
        static Color32[] Render(Camera camera,string path,int width,int height,float cloudSeconds=0)
        {
            var previous=camera.targetTexture;
            var target=new RenderTexture(width,height,24){antiAliasing=4};
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            camera.targetTexture=target;camera.aspect=(float)width/height;
            camera.GetComponentInParent<GardenSkyEnvironment>()?.UpdateClouds(cloudSeconds);
            camera.Render();camera.Render();
            var active=RenderTexture.active;RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            var pixels=texture.GetPixels32();
            RenderTexture.active=active;camera.targetTexture=previous;target.Release();
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
            return pixels;
        }
    }
}
