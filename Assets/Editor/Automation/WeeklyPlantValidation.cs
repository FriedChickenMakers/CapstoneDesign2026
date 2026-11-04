using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Plants;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using CapstoneDesign.Prototype;
using FlowerShape = CapstoneDesign.Plants.FlowerShape;

namespace CapstoneDesign.EditorTools
{
    // Explicit synthetic previews; never reads the player's garden or touches a device.
    public static class WeeklyPlantValidation
    {
        static readonly string Output=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS")??"artifacts/weekly-plants-20261104/validation";
        public static void PlantVolume()
        {
            Directory.CreateDirectory(Output);
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Graphics required.");
            var report=new List<string>{"Synthetic volume comparison: same seed, recipe, lighting and camera; left mesh5 / right mesh6."};
            var species=new[]{PlantCatalog.Get("species:sunflower"),PlantCatalog.Get("species:cosmos"),PlantCatalog.Get("species:chamomile")};
            Gallery("volume-before-after",2,3,(col,row)=>Recipe(species[row],21,col+5),(_,__)=>0,report,cellWidth:400);
            Gallery("volume-multiview",4,3,(col,row)=>Recipe(species[row],21,6),(_,__)=>0,report,(col,row)=>-25+90*col,400);
            Gallery("volume-all-species",4,3,(col,row)=>Recipe(PlantCatalog.All[row*4+col],21,6),(_,__)=>21,report,cellWidth:400);
            int[] steps={0,1,3,6,7,11,15,18,21};
            Gallery("volume-growth",steps.Length,1,(col,row)=>Recipe(species[0],steps[col],6),(_,__)=>0,report,cellWidth:400);
            ValidateHome(report);
            File.WriteAllLines(Path.Combine(Output,"unity-report.txt"),report);
            Debug.Log("PLANT_VOLUME_VALIDATION_PASS "+report.Count+" entries");
        }
        public static void FlowerSpacing()
        {
            Directory.CreateDirectory(Output);
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Graphics required.");
            var report=new List<string>{"Synthetic flower-spacing comparison: same seed, recipe, lighting and camera; left mesh4 / right mesh5."};
            var species=new[]{PlantCatalog.Get("species:chamomile"),PlantCatalog.Get("species:cosmos"),PlantCatalog.Get("species:campanula")};
            Gallery("spacing-before-after",2,3,(col,row)=>Recipe(species[row],21,col+4),(_,__)=>0,report,cellWidth:400);
            Gallery("spacing-multiview",4,3,(col,row)=>Recipe(species[row],21,5),(_,__)=>0,report,(col,row)=>-25+90*col,400);
            Gallery("spacing-all-species",4,3,(col,row)=>Recipe(PlantCatalog.All[row*4+col],21,5),(_,__)=>21,report,cellWidth:400);
            int[] steps={0,1,3,6,7,11,15,18,21};
            Gallery("spacing-growth",steps.Length,1,(col,row)=>Recipe(species[0],steps[col],5),(_,__)=>0,report,cellWidth:400);
            ValidateHome(report);
            File.WriteAllLines(Path.Combine(Output,"unity-report.txt"),report);
            Debug.Log("FLOWER_SPACING_VALIDATION_PASS "+report.Count+" entries");
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Graphics are required for actual rendered evidence.");
            var report=new List<string>{"Unity "+Application.unityVersion,"Synthetic data; Editor rendering, no device or user study.","Style columns: legacy mesh 2 / broad facets 3 / compact facets 4."};
            var species=new[]{PlantCatalog.All.First(s=>s.Id=="species:chamomile"),PlantCatalog.All.First(s=>s.Flower==FlowerShape.Lavender),PlantCatalog.All.First(s=>s.Id.Contains("campanula"))};
            Gallery("style-comparison",3,3,(col,row)=>Recipe(species[row],21,col+2),(_,__)=>0,report);
            int[] stages={0,1,3,6,7,11,15,18,21};
            Gallery("growth-21",stages.Length,1,(col,row)=>Recipe(species[0],stages[col],4),(_,__)=>0,report);
            Gallery("memory-leaves",3,3,(col,row)=>Recipe(species[row],21,4),(col,row)=>new[]{0,3,21}[col],report);
            Gallery("all-species",4,3,(col,row)=>Recipe(PlantCatalog.All[row*4+col],21,4),(_,__)=>21,report);
            ValidateUi(report);
            ValidateHome(report);
            File.WriteAllLines(Path.Combine(Output,"report.txt"),report);
            Debug.Log("WEEKLY_PLANTS_VALIDATION_PASS "+report.Count+" evidence entries");
        }
        static PlantRecipe Recipe(SpeciesDefinition species,int step,int style)
        {
            var p=PlantGenerator.Create("synthetic-weekly-art",species.Id,21,PlantGrowthMode.MissionCompletions);
            // Art isolation: force a species profile; transaction tests separately
            // verify real completion counts. Never use this fixture in player state.
            p.Counts=species.ExampleCounts();p.MeshVersion=style;
            while(p.GrowthStep<step)p=PlantGenerator.Advance(p);return p;
        }
        static void Gallery(string name,int columns,int rows,Func<int,int,PlantRecipe> recipe,Func<int,int,int> memories,List<string> report,Func<int,int,float> yaw=null,int cellWidth=300)
        {
            int width=cellWidth;const int height=400;
            var sheet=new Texture2D(columns*width,rows*height,TextureFormat.RGB24,false);
            try
            {
                for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
                {
                    var p=recipe(col,row);var go=new GameObject("Synthetic weekly plant preview");var preview=new PreviewRenderUtility();
                    try
                    {
                        var leaves=Enumerable.Range(0,memories(col,row)).Select(i=>new PlantMemoryLeaf{Slot=i,OrganId="synthetic-memory-"+i,Linked=true}).ToArray();
                        var view=go.AddComponent<ProceduralPlantView>();view.Render(p,leaves);
                        preview.AddSingleGO(go);preview.ambientColor=new Color(.42f,.45f,.39f);
                        var camera=preview.camera;camera.orthographic=true;camera.orthographicSize=.66f;camera.nearClipPlane=.01f;camera.farClipPlane=10;
                        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.95f,.93f,.87f);
                        var rotation=Quaternion.Euler(27,yaw==null?-25:yaw(col,row),0);camera.transform.SetPositionAndRotation(new Vector3(0,.49f,0)+rotation*(Vector3.back*3),rotation);
                        preview.lights[0].intensity=1.15f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-35,0);preview.lights[1].intensity=.4f;
                        preview.BeginStaticPreview(new Rect(0,0,width,height));preview.Render(true);var texture=preview.EndStaticPreview();
                        try
                        {
                            var pixels=texture.GetPixels();var bg=pixels[0];
                            if(pixels.Count(c=>Mathf.Abs(c.r-bg.r)+Mathf.Abs(c.g-bg.g)+Mathf.Abs(c.b-bg.b)>.10f)<15)throw new Exception("Empty plant render");
                            if(pixels.Count(c=>c.r>.9f&&c.b>.9f&&c.g<.1f)>10)throw new Exception("Unsupported plant shader");
                            sheet.SetPixels(col*width,(rows-row-1)*height,width,height,pixels);
                        }
                        finally{UnityEngine.Object.DestroyImmediate(texture);}
                        if(leaves.Length>0)
                        {
                            var hit=typeof(GardenMemoryPicker).GetMethod("Hit",BindingFlags.Static|BindingFlags.NonPublic);
                            int visible=0;
                            foreach(var organ in view.Geometry.Organs)
                            {
                                var c=organ.Center;var center=go.transform.TransformPoint(new Vector3((float)c.X,(float)c.Y,(float)c.Z));
                                int triangle=(int)hit.Invoke(null,new object[]{view,camera.ViewportPointToRay(camera.WorldToViewportPoint(center))});
                                if(triangle>=organ.FirstTriangle&&triangle<organ.FirstTriangle+organ.TriangleCount)visible++;
                            }
                            if(visible==0)throw new Exception("No memory leaf is directly reachable from camera.");
                            report.Add(name+" "+row+","+col+" visible leaf centers="+visible+"/"+leaves.Length);
                        }
                        report.Add(name+" "+row+","+col+" "+p.SpeciesId+" step="+p.GrowthStep+" mesh="+p.MeshVersion+" triangles="+view.TriangleCount);
                    }
                    finally{preview.Cleanup();UnityEngine.Object.DestroyImmediate(go);}
                }
                sheet.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),sheet.EncodeToPNG());
            }
            finally{UnityEngine.Object.DestroyImmediate(sheet);}
        }
        static void ValidateUi(List<string> report)
        {
            var uiReport=new FocusMissionValidation.Report{runId="weekly-plants"};
            using(var f=new FocusMissionValidation.Fixture(uiReport))
            {
                if(!f.Service.RefreshCycle(null)||!f.Service.BeginSession("synthetic-memory-session","free-mission:M17",0)||!f.Service.StartSession("synthetic-memory-session")
                    ||!f.Service.CompleteSession("synthetic-memory-session","중립","합성 화면 검증 기록"))throw new Exception(f.Service.LastError);
                var state=f.Service.Snapshot;var plant=WeeklyPlants.Active(state);var memory=plant.Memories.Single();
                f.Ui.ShowPlantCollection();f.Capture("weekly-plant-collection",true);
                f.Ui.ShowPlantMemories(plant.Id,0);f.Capture("weekly-memory-list",true);
                f.Ui.ShowMemoryChoices(plant.Id,new[]{memory.OrganId});f.Capture("weekly-memory-detail",true);
                if(!f.VisibleText.Contains("합성 화면 검증 기록"))throw new Exception("Memory opens wrong source.");
                f.Click("감정 기록 수정");f.Capture("weekly-memory-edit",true);
                f.Click("취소");
                f.Ui.ShowMemoryChoices(plant.Id,new[]{memory.OrganId});f.Click("이 기록 삭제");f.Capture("weekly-memory-delete",true);
                f.Click("기록 삭제");
                if(WeeklyPlants.Active(f.Service.Snapshot).Memories.Single().Kind!=MemoryRecordKind.None)throw new Exception("Delete failed to detach organ.");
                report.Add("Memory detail/edit/cancel/delete callbacks passed; visible raycast button clicks="+uiReport.buttonClicks);
            }
        }
        static void ValidateHome(List<string> report)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            AndroidPlatformBridge.UseMockProvider();
            var home=UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include);
            if(home==null)throw new Exception("Integrated garden presenter missing.");
            var clock=new FocusMissionValidation.SyntheticClock();var store=new FocusMissionValidation.MemoryStore();
            using(var service=new GardenStateService(store,clock,new DemoBalanceConfig()))
            {
                home.activity.Initialize(service,true);service.SelectMbctProgram("2w");service.RefreshCycle(null);
                for(int i=0;i<21;i++)
                {
                    if(i>0&&i%3==0){clock.UtcNow=clock.UtcNow.AddDays(1);service.RefreshCycle(null);}
                    string id="synthetic-home-"+i;
                    if(!service.BeginSession(id,"free-mission:M17",0)||!service.StartSession(id)||!service.CompleteSession(id,i<3?"중립":null,i<3?"합성 정원 기록":null))throw new Exception(service.LastError);
                }
                home.navigation.ShowIsland();home.RefreshFromState();home.SetOverview(false);home.SetZoom(.9f);
                var sky=GardenSkyEnvironment.Ensure(home.gardenCamera,home.gardenVisuals.transform);
                double? oldTime=GardenVisualClock.OverrideHour;
                try
                {
                    GardenVisualClock.SetOverrideMinutes(12*60);
                    // Ensure installs post processing, then disable random dither
                    // only for pixel-stable evidence of the final scene.
                    home.gardenCamera.GetUniversalAdditionalCameraData().dithering=false;
                    using(var capture=new GardenLightingValidation.HomeCapture(home,sky,new Vector2Int(900,1600)))
                    {
                        capture.Render(Path.Combine(Output,"weekly-integrated-home.png"),report);
                        var layout=home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                        var picker=layout.GetComponent<GardenMemoryPicker>();var raw=layout.GetComponent<RawImage>();var rect=(RectTransform)raw.transform;
                        var orbit=layout.orbit;orbit.RestoreAngles(new Vector2(-31,24));
                        capture.Render(Path.Combine(Output,"weekly-integrated-orbit.png"),report);
                        var angle=orbit.Angles;float zoom=home.Zoom;
                        var view=home.ProceduralPlant;var hit=typeof(GardenMemoryPicker).GetMethod("Hit",BindingFlags.Static|BindingFlags.NonPublic);
                        PlantOrganGeometry selected=null;
                        foreach(var organ in view.Geometry.Organs)
                        {
                            var c=organ.Center;var center=view.transform.TransformPoint(new Vector3((float)c.X,(float)c.Y,(float)c.Z));
                            int triangle=(int)hit.Invoke(null,new object[]{view,home.gardenCamera.ViewportPointToRay(home.gardenCamera.WorldToViewportPoint(center))});
                            if(triangle>=organ.FirstTriangle&&triangle<organ.FirstTriangle+organ.TriangleCount){selected=organ;break;}
                        }
                        if(selected==null)throw new Exception("No selectable memory in integrated garden.");
                        var anchor=selected.Center;var uv=home.gardenCamera.WorldToViewportPoint(view.transform.TransformPoint(new Vector3((float)anchor.X,(float)anchor.Y,(float)anchor.Z)));
                        var local=new Vector3(rect.rect.xMin+(uv.x-raw.uvRect.x)/raw.uvRect.width*rect.rect.width,rect.rect.yMin+(uv.y-raw.uvRect.y)/raw.uvRect.height*rect.rect.height);
                        var position=RectTransformUtility.WorldToScreenPoint(home.homeCanvas.worldCamera,rect.TransformPoint(local));
                        var events=UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                        var data=new PointerEventData(events){position=position,pressPosition=position,button=PointerEventData.InputButton.Left,
                            pointerPressRaycast=new RaycastResult{gameObject=raw.gameObject,module=home.homeCanvas.GetComponent<GraphicRaycaster>()}};
                        picker.OnPointerDown(data);picker.OnPointerUp(data);picker.OnPointerClick(data);
                        if(home.activity.CurrentScreen!="memory")throw new Exception("RenderTexture leaf tap did not open its record: "+home.activity.CurrentScreen);
                        var back=home.activity.GetComponentsInChildren<Button>(true).First(b=>b.name=="식물로 돌아가기");back.onClick.Invoke();
                        if(Vector2.Distance(orbit.Angles,angle)>.001f || Math.Abs(home.Zoom-zoom)>.001f)throw new Exception("Memory return changed orbit or zoom.");
                        home.gardenCamera.GetUniversalAdditionalCameraData().dithering=false;
                        capture.Render(Path.Combine(Output,"weekly-integrated-return.png"),report);
                        report.Add("Integrated RenderTexture leaf tap -> record -> return preserves orbit/zoom; actual callbacks passed.");
                        string archivedId=WeeklyPlants.Active(service.Snapshot).Id;
                        clock.UtcNow=clock.UtcNow.AddDays(1);
                        if(!service.BeginSession("synthetic-next-plant","free-mission:M17",0)||!service.StartSession("synthetic-next-plant")||!service.CompleteSession("synthetic-next-plant"))throw new Exception(service.LastError);
                        home.SelectPlant(null);home.navigation.ShowActivities();
                        home.activity.ShowPlantMemories(archivedId,0);
                        home.activity.ShowMemoryChoices(archivedId,new[]{selected.OrganId});
                        home.activity.GetComponentsInChildren<Button>(true).First(b=>b.name=="식물로 돌아가기").onClick.Invoke();
                        if(home.SelectedPlantId!=archivedId || home.RenderedRecipe.InstanceId!=archivedId)throw new Exception("Archive list returned to the active plant instead of its own plant.");
                        report.Add("Archived memory opened from list returns to its own completed plant after the next plant starts.");
                    }
                }
                finally{if(oldTime.HasValue)GardenVisualClock.SetOverrideMinutes((int)(oldTime.Value*60));else GardenVisualClock.UseSystemTime();}
            }
        }
    }
}
