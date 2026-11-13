using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Focused synthetic regression for world-fixed sky directions and world-space mist.
    public static class GardenAtmosphereOrbitCapture
    {
        sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-12T12:00:00+09:00");
            public TimeZoneInfo TimeZone => TimeZoneInfo.CreateCustomTimeZone("Synthetic-KST",TimeSpan.FromHours(9),"KST","KST");
        }
        sealed class Store : IStateStore
        {
            GardenState state;
            public GardenState Load() => state == null ? null : StateCodec.Clone(state);
            public void Save(GardenState value) => state = StateCodec.Clone(value);
        }
        readonly struct Disc
        {
            public readonly int Count;
            public readonly Vector2 Center;
            public Disc(Color32[] pixels,int width,bool moon)
            {
                int count=0;float x=0,y=0;
                for(int i=0;i<pixels.Length;i++)
                {
                    var p=pixels[i];
                    bool match=moon?p.r>180&&p.g>190&&p.b>200:p.r>220&&p.g>180&&p.r>p.b+25;
                    if(!match)continue;count++;x+=i%width;y+=i/width;
                }
                Count=count;Center=count==0?new Vector2(-1,-1):new Vector2(x/count/width,y/count/(pixels.Length/width));
            }
            public override string ToString()=>Count+" pixels @ "+Center;
        }
        public static void Capture() => Capture(false);
        public static void CaptureFog() => Capture(true);
        static void Capture(bool fogOnly)
        {
            string output=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if(string.IsNullOrEmpty(output))throw new Exception("Explicit synthetic artifact directory required");
            Directory.CreateDirectory(output);
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            AndroidPlatformBridge.UseMockProvider();
            var home=UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            using var service=new GardenStateService(new Store(),new Clock(),new DemoBalanceConfig{VisitorChancePercent=0},new LegacySnapshot{Nutrient=100});
            home.activity.Initialize(service,true);
            var state=service.Snapshot;state.LegacyGrowth=2;
            state.Environments.Add(new EnvironmentPlacement{EnvironmentId="environment:E01",Slot="left"});
            state.Environments.Add(new EnvironmentPlacement{EnvironmentId="environment:E02",Slot="right"});
            state.DailyDecisions.Last().VisitorId="animal:A07";
            home.navigation.ShowIsland();home.RefreshFromState(state);
            var camera=home.gardenCamera;
            var sky=GardenSkyEnvironment.Ensure(camera,home.gardenVisuals.transform);
            var report=new List<string>();
            var errors=new List<string>();
            Application.LogCallback listener=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
            Application.logMessageReceived+=listener;
            try
            {
                camera.ResetProjectionMatrix();camera.aspect=1;camera.orthographicSize=2.6f;
                int mask=camera.cullingMask;
                if(!fogOnly)
                {
                    camera.cullingMask=0;
                    sky.SetTimeForPreview(9);
                    float pitch=-Mathf.Asin(sky.SunDirection.y)*Mathf.Rad2Deg;
                    float yaw=Mathf.Atan2(sky.SunDirection.x,sky.SunDirection.z)*Mathf.Rad2Deg;
                    var discs=new Dictionary<int,Disc>();
                    Color32[] first=null,last=null;
                    foreach(int offset in new[]{-180,-90,-20,0,20,90,180})
                    {
                        camera.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(pitch,yaw+offset,0));
                        sky.UpdateClouds(0);
                        var pixels=Render(camera,Path.Combine(output,"sun-yaw-"+offset+".png"),640,640);
                        discs[offset]=new Disc(pixels,640,false);
                        report.Add("sun yaw "+offset+": "+discs[offset]);
                        if(offset==-180)first=pixels;if(offset==180)last=pixels;
                    }
                    Require(discs[0].Count>50,"Looking toward the world sun did not show the sun");
                    Require(discs[-20].Count>10&&discs[20].Count>10,"Sun did not cross both sides of the viewport");
                    Require(discs[-20].Center.x>discs[0].Center.x&&discs[0].Center.x>discs[20].Center.x,"World sun moved backward relative to camera yaw");
                    Require(discs[-90].Count<5&&discs[90].Count<5&&discs[-180].Count<5&&discs[180].Count<5,"Sun remained pinned in view when looking away");
                    double returnError=first.Zip(last,(a,b)=>(Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b))/3d).Average();
                    Require(returnError<.1,"Sky did not return to the same view after 360 degrees");
                    report.Add("World-direction sun yaw/full 360 return: PASS, pixel error="+returnError);
                    camera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
                    camera.projectionMatrix=Matrix4x4.Ortho(-2.2f,3f,-2.1f,3.1f,camera.nearClipPlane,camera.farClipPlane);
                    var shifted=new Disc(Render(camera,Path.Combine(output,"sun-off-axis.png"),640,640),640,false);
                    Require(shifted.Count>20,"Off-axis orthographic projection lost sky disc while looking toward it");
                    report.Add("Off-axis orthographic sky: "+shifted);
                    camera.ResetProjectionMatrix();
                    sky.SetTimeForPreview(23);
                    camera.transform.rotation=Quaternion.LookRotation(sky.MoonDirection,Vector3.up);
                    var moon=new Disc(Render(camera,Path.Combine(output,"moon-look.png"),640,640),640,true);
                    camera.transform.rotation=Quaternion.LookRotation(-sky.MoonDirection,Vector3.up);
                    var away=new Disc(Render(camera,Path.Combine(output,"moon-away.png"),640,640),640,true);
                    Require(moon.Count>50&&away.Count<moon.Count*.2,"Moon did not leave the frame when looking away");
                    report.Add("Moon world direction look/away: "+moon+" / "+away);
                    foreach(double hour in new[]{6.01,9,12,17.99,23})
                    {
                        sky.SetTimeForPreview(hour);
                        var light=sky.SunDirection.y>=0?sky.SunDirection:sky.MoonDirection;
                        Require(Vector3.Dot(-sky.KeyLight.transform.forward,light)>.99999f,"Directional light disagrees with world sky at "+hour);
                    }
                    report.Add("Disc/light world directions including low sun: PASS");
                }
                camera.cullingMask=mask;
                var density=sky.Clouds.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetTexture("_Noise") as Texture3D;
                Require(density!=null&&!UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(density.graphicsFormat),"Fog density must be linear scalar data");
                var legacy=new Texture3D(1,1,1,TextureFormat.RGBA32,false);
                report.Add("Density volume format="+density.graphicsFormat+", default color format="+legacy.graphicsFormat);
                UnityEngine.Object.DestroyImmediate(legacy);
                var initial=new ParticleSystem.Particle[24];var rotated=new ParticleSystem.Particle[24];var moved=new ParticleSystem.Particle[24];
                sky.UpdateClouds(0);int count=sky.Clouds.GetParticles(initial);
                var angles=fogOnly?new[]{new Vector2(0,28),new Vector2(90,28),new Vector2(-25,-75)}:
                    new[]{new Vector2(0,28),new Vector2(90,28),new Vector2(180,28),new Vector2(270,28),new Vector2(-25,-75),new Vector2(-25,75)};
                foreach(var angle in angles)
                {
                    var rotation=Quaternion.Euler(angle.y,angle.x,0);
                    camera.transform.SetPositionAndRotation(new Vector3(0,.58f,0)+rotation*Vector3.back*5.8f,rotation);
                    sky.SetTimeForPreview(12);sky.UpdateClouds(0);sky.Clouds.GetParticles(rotated);
                    for(int i=0;i<count;i++)Require(Vector3.Distance(initial[i].position,rotated[i].position)<.00001f,"Mist repositioned with camera orbit");
                    Render(camera,Path.Combine(output,"garden-orbit-"+angle.x+"-"+angle.y+".png"),900,900);
                }
                sky.UpdateClouds(60);int endCount=sky.Clouds.GetParticles(moved);
                int moving=0,vertical=0;
                for(int i=0;i<count;i++)
                {
                    if(Vector3.Distance(initial[i].position,moved[i].position)>.025f)moving++;
                    if(Mathf.Abs(initial[i].position.y-moved[i].position.y)>.015f)vertical++;
                }
                Require(count==24&&endCount==24&&moving>=12&&vertical>=6,"Mist did not drift through its bounded 3D volume");
                report.Add("Camera-invariant world particles: PASS; 60s movement="+moving+", vertical="+vertical);
                // Test actual rendered output, not just GetParticles counts.
                var fogRenderer=sky.Clouds.GetComponent<ParticleSystemRenderer>();
                var front=Quaternion.Euler(28,-25,0);
                camera.transform.SetPositionAndRotation(new Vector3(0,.58f,0)+front*Vector3.back*5.8f,front);
                camera.ResetProjectionMatrix();camera.orthographicSize=3.6f;
                foreach(var light in new[]{("day",12d),("night",23d)})
                {
                    sky.SetTimeForPreview(light.Item2);sky.UpdateClouds(0);
                    fogRenderer.enabled=true;
                    var on=Render(camera,Path.Combine(output,"fog-"+light.Item1+"-on.png"),900,900);
                    fogRenderer.enabled=false;
                    var off=Render(camera,Path.Combine(output,"fog-"+light.Item1+"-off.png"),900,900);
                    fogRenderer.enabled=true;
                    int changed=0,noticeable=0,peak=0;long total=0;
                    for(int i=0;i<on.Length;i++)
                    {
                        int delta=Math.Max(Math.Abs(on[i].r-off[i].r),Math.Max(Math.Abs(on[i].g-off[i].g),Math.Abs(on[i].b-off[i].b)));
                        if(delta>0)changed++;if(delta>=3)noticeable++;peak=Math.Max(peak,delta);total+=delta;
                    }
                    report.Add("Fog "+light.Item1+" output: changed="+changed+", >=3 levels="+noticeable+", peak="+peak+", mean="+(double)total/on.Length+", bounds="+fogRenderer.bounds);
                    Require(peak>=12&&noticeable>=on.Length/10,"Mist rendered too faintly against the "+light.Item1+" sky");
                }
                home.homeCanvas.GetComponentInChildren<PrototypeIslandDrag>(true)?.ResetView();home.RefreshFromState(state);
                foreach(var item in new[]{("day",12d),("sunset",17.6d),("night",23d)})
                {
                    sky.SetTimeForPreview(item.Item2);CaptureHome(home,sky,Path.Combine(output,"home-"+item.Item1+".png"),0);
                }
                sky.SetTimeForPreview(12);CaptureHome(home,sky,Path.Combine(output,"home-fog-60s.png"),60);
                Require(errors.Count==0,"Render errors: "+string.Join(";",errors));
                report.Add("All captured RT pixels remain opaque under translucent mist: PASS");
                report.Add(fogOnly?"GARDEN_ATMOSPHERE_FOG_PASS":"GARDEN_ATMOSPHERE_ORBIT_PASS");
                Debug.Log(report.Last());
            }
            finally
            {
                Application.logMessageReceived-=listener;
                File.WriteAllLines(Path.Combine(output,"validation.txt"),report);
            }
        }
        static void Require(bool pass,string message){if(!pass)throw new Exception(message);}
        static void CaptureHome(GardenHomePresenter home,GardenSkyEnvironment sky,string path,float seconds)
        {
            var size=new Vector2Int(900,1950);LocalLoopPreview.Render(size,path);Canvas.ForceUpdateCanvases();
            foreach(var layout in home.homeCanvas.GetComponentsInChildren<PrototypeGardenPreviewLayout>(true))layout.Apply();
            sky.UpdateClouds(seconds);home.gardenCamera.Render();LocalLoopPreview.Render(size,path);
        }
        static Color32[] Render(Camera camera,string path,int width,int height)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=new RenderTexture(width,height,24){antiAliasing=4};
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;camera.aspect=(float)width/height;camera.Render();camera.Render();
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                var pixels=texture.GetPixels32();File.WriteAllBytes(path,texture.EncodeToPNG());
                Require(pixels.All(p=>p.a>=250),"Transparent fog leaked into RT alpha");return pixels;
            }
            finally
            {
                RenderTexture.active=active;camera.targetTexture=previous;target.Release();
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
