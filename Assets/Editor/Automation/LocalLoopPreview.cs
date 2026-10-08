using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.EditorTools
{
    public static class LocalLoopPreview
    {
        sealed class Clock : IClock { public DateTimeOffset Now=DateTimeOffset.Parse("2026-09-23T10:00:00+09:00"); public DateTimeOffset UtcNow=>Now; public TimeZoneInfo TimeZone=>TimeZoneInfo.CreateCustomTimeZone("Preview-Seoul",TimeSpan.FromHours(9),"Preview Seoul","Preview Seoul"); }
        sealed class Store : IStateStore { GardenState state;public bool Fail;public GardenState Load()=>state==null?null:StateCodec.Clone(state);public void Save(GardenState candidate){if(Fail)throw new IOException("MOCK injected storage failure");state=StateCodec.Clone(candidate);} }
        public static void Capture()
        {
            AndroidPlatformBridge.UseMockProvider();
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null || SystemInfo.graphicsDeviceName.ToLowerInvariant().Contains("llvmpipe"))throw new Exception("Hardware rendering required");
            string root=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS") ?? Path.Combine(Directory.GetCurrentDirectory(),"artifacts","local-loop");
            string output=Path.Combine(root,"visual");Directory.CreateDirectory(output);
            var names=new[]{"first-launch","today","settings","reflection","growth-preview","growth-sprout","growth-bud","growth-bloom","growth-later","course","activity","paused","completed","m15-debug","environment-preview","environment-saved","visitor","no-visitor","records-heart","unsupported","permission-denied","query-error","save-error"};
            foreach(var size in new[]{new Vector2Int(900,1600),new Vector2Int(900,1950),new Vector2Int(1200,800)})
            foreach(var name in names)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var loop=nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
                var clock=new Clock();var store=new Store();var config=new DemoBalanceConfig{VisitorChancePercent=name=="visitor"?100:0};
                var service=new GardenStateService(store,clock,config,new LegacySnapshot { Nutrient=name.StartsWith("growth-")?50:0 });
                loop.Balance=config;loop.Initialize(service,true);
                nav.ShowActivities();
                if(name=="first-launch")nav.ShowIsland();
                else if(name=="today")loop.ShowHome();
                else if(name=="settings")
                {
                    nav.GetComponentInChildren<SensorRawDisplay>(true).PrepareView();nav.ShowSettings();
                }
                else if(name.StartsWith("growth-") && name!="growth-preview")
                {
                    int count=name=="growth-sprout"?1:name=="growth-bud"?2:name=="growth-bloom"?3:4;
                    for(int i=0;i<count;i++)
                        if(!service.GrowPlant("preview-growth-"+i,"plant:P06"))throw new Exception("Growth preview purchase failed");
                    loop.UpdateGarden();nav.ShowIsland();
                }
                else if(name=="course")loop.ShowCourse();
                else if(name=="m15-debug")
                {
                    loop.ToggleDebugWalk();
                    loop.SelectMission(MindfulnessContent.FreeMissions.First(m=>m.id=="free-mission:M15"));
                    var id=service.Snapshot.Sessions.Last().SessionId;
                    service.StartSession(id);
                    for(int i=0;i<7;i++)service.AddVirtualStep();
                    loop.ShowSession();
                }
                else
                {
                    loop.SelectMission(MindfulnessContent.Course[0]);var id=service.Snapshot.Sessions.Last().SessionId;
                    service.StartSession(id);clock.Now=clock.Now.AddMinutes(4);
                    if(name=="activity")loop.ShowSession();
                    else if(name=="reflection")loop.ShowReflection();
                    else if(name=="paused"){service.PauseSession(id);loop.ShowSession();}
                    else if(name=="save-error"){store.Fail=true;loop.Finish(null,null);}
                    else
                    {
                        loop.Finish("잘 모르겠어요",null);
                        if(name=="completed"){}
                        else if(name=="growth-preview")loop.PreviewGrowth();
                        else if(name=="environment-preview")loop.PreviewEnvironment("environment:E01");
                        else if(name=="environment-saved" || name=="visitor" || name=="no-visitor")
                        {
                            service.PurchaseEnvironment("preview-buy","environment:E01","left");
                            if(name!="environment-saved"){clock.Now=clock.Now.AddDays(1);loop.RefreshCycle();}
                            loop.UpdateGarden();nav.ShowIsland();
                        }
                        else
                        {
                            var session=service.Snapshot.Sessions.Last();
                            long start=DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds();long end=DateTimeOffset.Parse(session.EndedUtc).ToUnixTimeMilliseconds();
                            var metric=new HealthMetricSnapshot{status="AVAILABLE",queryComplete=true,queryStartEpochMs=start,queryEndEpochMs=end,sampleCount=4,displaySampleCount=4};
                            metric.heartRateSamples=new[]{new HeartRateSampleSnapshot{measuredAtEpochMs=start+1000,beatsPerMinute=73,sourcePackage="MOCK source A",segmentId=0},new HeartRateSampleSnapshot{measuredAtEpochMs=start+40000,beatsPerMinute=76,sourcePackage="MOCK source A",segmentId=0},new HeartRateSampleSnapshot{measuredAtEpochMs=start+150000,beatsPerMinute=75,sourcePackage="MOCK source B",segmentId=1},new HeartRateSampleSnapshot{measuredAtEpochMs=start+230000,beatsPerMinute=72,sourcePackage="MOCK source B",segmentId=1}};
                            if(name!="records-heart"){metric.status=name=="unsupported"?"UNSUPPORTED":name=="permission-denied"?"PERMISSION_DENIED":"ERROR";metric.queryComplete=false;metric.heartRateSamples=Array.Empty<HeartRateSampleSnapshot>();metric.sampleCount=0;}
                            loop.PreviewHealth=new AndroidPlatformSnapshot{inputMode="MOCK",healthConnect=new HealthConnectSnapshot{heartRate=metric}};
                            loop.ShowRecord(id);
                        }
                    }
                }
                Render(size,Path.Combine(output,name+"-"+size.x+"x"+size.y+".png"));
                service.Dispose();
            }
            File.WriteAllText(Path.Combine(output,"manifest.json"),"{\"environment\":\"Linux Unity Editor hardware preview\",\"inputMode\":\"MOCK\",\"deviceTested\":false,\"images\":"+(names.Length*3)+",\"renderer\":\""+SystemInfo.graphicsDeviceName+"\"}");
            Debug.Log("LOCAL_LOOP_PREVIEW_PASS "+(names.Length*3)+" MOCK images at "+output);
        }
        internal static void Render(Vector2Int size,string path)
        {
            var camera=GameObject.Find("MockupCamera").GetComponent<Camera>();
            var canvas=GameObject.Find("UiCanvas").GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var home=canvas.GetComponent<GardenHomePresenter>();
            if(home!=null && home.homeCanvas.gameObject.activeSelf)
            {
                home.homeCanvas.renderMode=RenderMode.ScreenSpaceCamera;
                home.homeCanvas.worldCamera=camera;
                home.homeCanvas.planeDistance=.75f;
                if(home.gardenVisuals.activeInHierarchy)home.gardenVisuals.GetComponentInChildren<Camera>(true)?.Render();
            }
            var rt=new RenderTexture(size.x,size.y,24);var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;
            var texts=UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
            foreach(var text in texts)text.font?.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
            foreach(var text in texts){text.cachedTextGenerator.Invalidate();text.SetAllDirty();}
            Canvas.ForceUpdateCanvases();
            foreach(var graph in UnityEngine.Object.FindObjectsByType<HeartSampleGraph>(FindObjectsSortMode.None))
            {
                graph.SetAllDirty();graph.Rebuild(CanvasUpdate.PreRender);
                var mesh=graph.canvasRenderer.GetMesh();
                Debug.Log("HEART_PREVIEW_MESH vertices="+mesh.vertexCount+" active="+graph.IsActive()+" culled="+graph.canvasRenderer.cull+" rect="+graph.rectTransform.rect);
                if(mesh.vertexCount==0 && graph.Samples.Length>0)throw new Exception("Heart preview generated no canvas geometry");
            }
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
