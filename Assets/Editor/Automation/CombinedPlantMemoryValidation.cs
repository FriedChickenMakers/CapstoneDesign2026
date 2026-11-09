using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Run only in a separately copied, synthetic batch project. This fixture never
    // creates a file-backed state store, reads preferences, or sends physical input.
    public static class CombinedPlantMemoryValidation
    {
        [Serializable] sealed class Check { public string name, status, detail; }
        [Serializable] sealed class CompositionSample
        {
            public string name;
            public int width,height,growthStep,maxGrowthSteps,plantPixels,islandVertices;
            public Vector4 visibleBounds,plantBounds,islandBounds;
        }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion, scope;
            public int passed, failed, assertions, clicks, raycasts;
            public List<Check> checks = new List<Check>();
            public List<string> captures = new List<string>(), errors = new List<string>();
            public List<CompositionSample> compositions = new List<CompositionSample>();
        }
        static Report report;
        static string output;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void RunBatch()
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-combinedMemoryReport");
            if (!Application.isBatchMode || Application.companyName != "CapstoneSyntheticValidation"
                || !Application.productName.StartsWith("CombinedMemory-", StringComparison.Ordinal) || at < 0 || at + 1 >= args.Length)
                throw new InvalidOperationException("Use a copied CombinedMemory-* synthetic batch project and -combinedMemoryReport path.");
            string path = Path.GetFullPath(args[at + 1]); output = Path.GetDirectoryName(path); Directory.CreateDirectory(output);
            report = new Report { utc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                scope = "Copied MockupMain; synthetic in-memory saves; real RT rendering and EventSystem handlers; no original Editor, user save, device or OS input" };
            Application.logMessageReceived += Observe; GardenVisualClock.SetOverrideMinutes(720);
            try
            {
                Run("Presentation-only island geometry, source preservation and lamp lifecycle", SolidIslandValidation.ValidatePresentationOnly);
                foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(390,844)})
                    foreach(bool mature in new[]{false,true})
                        foreach(int minute in new[]{720,1260})
                            Run("Garden composition "+(mature?"mature":"seed")+" "+(minute==720?"day":"night")+" "+size,
                                ()=>HomeComposition(size.x,size.y,mature,minute));
                if(!args.Contains("-gardenCompositionOnly"))
                {
                Run("Restored home layout and preserved individual growth 360x640",()=>HomeStatusFlow(360,640));
                Run("Restored home layout and preserved individual growth 390x844",()=>HomeStatusFlow(390,844));
                Run("Existing album route, latest-first record list and transactional actions 360x640",()=>HomeRecordsFlow(360,640));
                Run("Existing album route, latest-first record list and transactional actions 390x844",()=>HomeRecordsFlow(390,844));
                Run("Precise, nearby, blocked and ambiguous memory picking",MemoryPickingContract);
                Run("Scaled text layout, atlas callbacks and input metrics", ScreenDensityTextValidation.Run);
                Run("Garden/album leaf input and atomic record lifecycle 360x640", () => MobileFlow(360, 640));
                Run("Garden/album leaf input and atomic record lifecycle 390x844", () => MobileFlow(390, 844));
                Run("21-stage v6 species bounds, 60-leaf paging, deterministic nature and low angles", StressRender);
                }
                Run("All shared studio resources released", () => {
                    Assert(!GardenMemoPopup.IsOpen, "Popup owner survived fixture disposal.");
                    Assert(GardenPlantPhotoView.ActiveViewCount == 0 && GardenPlantPhotoView.SharedCameraCount == 0
                        && GardenPlantPhotoView.OwnedTextureCount == 0 && GardenPlantPhotoView.OwnedEnvironmentMeshCount == 0,
                        "Photo textures, camera or nature meshes survived their last view.");
                });
            }
            finally
            {
                GardenMemoPopup.TryCloseOpen(); GardenVisualClock.UseSystemTime();
                Application.logMessageReceived -= Observe;
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            EditorApplication.Exit(report.failed == 0 && report.errors.Count == 0 ? 0 : 1);
        }
        static void Observe(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(type + ": " + text + "\n" + stack); }
        static void Assert(bool value, string message) { report.assertions++; if (!value) throw new InvalidOperationException(message); }
        static void Run(string name, Action action)
        {
            try { action(); report.passed++; report.checks.Add(new Check { name = name, status = "pass" }); }
            catch (Exception error) { report.failed++; report.checks.Add(new Check { name = name, status = "fail", detail = error.ToString() }); GardenMemoPopup.TryCloseOpen(); }
        }
        static T Find<T>(string name) where T : Component => Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(item => item != null && item.name == name && item.gameObject.activeInHierarchy);
        static string Screen(WeekOneQuestDemo ui) => (string)typeof(WeekOneQuestDemo).GetField("screen", Private).GetValue(ui);
        static void Complete(GardenStateService service, string id, string mission)
        {
            Assert(service.RefreshCycle(null) && service.BeginSession(id, mission, mission.StartsWith("course:") ? 1 : 0), service.LastError);
            Assert(service.StartSession(id) && service.CompleteSession(id, null, null), service.LastError);
        }
        sealed class Clock : IClock
        {
            public DateTimeOffset Now = DateTimeOffset.Parse("2026-11-09T10:00:00Z");
            public DateTimeOffset UtcNow => Now;
            public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        }
        sealed class Store : IStateStore
        {
            string value; public bool Fail; public int Writes;
            public GardenState Load() => value == null ? null : StateCodec.Decode(value);
            public void Save(GardenState state)
            { if (Fail) throw new IOException("Synthetic write failure"); value = StateCodec.Encode(state); Writes++; }
        }

        static bool Contains(Rect outer,Rect inner,float tolerance=.75f) => inner.xMin>=outer.xMin-tolerance
            &&inner.xMax<=outer.xMax+tolerance&&inner.yMin>=outer.yMin-tolerance&&inner.yMax<=outer.yMax+tolerance;
        static bool Overlap(Rect a,Rect b) => Mathf.Min(a.xMax,b.xMax)>Mathf.Max(a.xMin,b.xMin)+.5f
            &&Mathf.Min(a.yMax,b.yMax)>Mathf.Max(a.yMin,b.yMin)+.5f;
        static Vector4 Coordinates(Rect rect)=>new Vector4(rect.xMin,rect.yMin,rect.xMax,rect.yMax);
        static void HomeComposition(int width,int height,bool mature,int minute)
        {
            GardenVisualClock.SetOverrideMinutes(minute);
            using(var f=new Fixture(width,height,mature?2:1))
            {
                string name="garden-composition-"+(mature?"mature":"seed")+"-"+(minute==720?"day":"night");
                f.Render();f.Capture(name); // Preserve visual evidence even if a subsequent geometry assertion fails.
                var home=f.Home;var state=f.Service.Snapshot;var active=WeeklyPlants.Active(state);
                string saved=StateCodec.Encode(state);int writes=f.Store.Writes;
                Assert(home.IsGardenVisible&&home.IsOverview&&Mathf.Approximately(home.Zoom,.85f),"Default garden composition keeps the overview and existing zoom rule.");
                Assert(home.RenderedRecipe.InstanceId==active.Id&&home.RenderedRecipe.GrowthStep==(mature?active.Recipe.MaxGrowthSteps:0),
                    "Composition renders the actual seed or fully grown individual's stored recipe.");
                var image=f.Preview.GetComponent<RawImage>();var camera=home.gardenCamera;var target=camera.targetTexture;
                Assert(target!=null&&target.IsCreated()&&image.texture==target,"Home uses its existing garden camera through the live RawImage target.");
                int cameras=Resources.FindObjectsOfTypeAll<Camera>().Length;
                var screen=new Rect(0,0,width,height);var display=f.Bounds((RectTransform)image.transform);
                var upper=f.Bounds(f.Preview.upperBoundary);var lower=f.Bounds(f.Preview.lowerBoundary);
                var visible=Rect.MinMaxRect(display.xMin,lower.yMax,display.xMax,upper.yMin);
                Assert(visible.height>0&&Contains(screen,visible),"Composition area stays between the actual information and footer bounds.");
                var plantPoints=home.ProceduralPlant.Geometry.Vertices.Select(vertex=>home.ProceduralPlant.transform.TransformPoint(
                    new Vector3((float)vertex.X,(float)vertex.Y,(float)vertex.Z))).ToArray();
                var plant=f.ProjectBounds(image,camera,plantPoints);
                var islandPoints=new List<Vector3>();
                foreach(var filter in home.gardenRoot.GetComponentsInChildren<MeshFilter>(false))
                {
                    if(filter.sharedMesh==null||filter.transform.IsChildOf(home.ProceduralPlant.transform))continue;
                    if(f.Preview.excludedBoundsRoot!=null&&filter.transform.IsChildOf(f.Preview.excludedBoundsRoot))continue;
                    var renderer=filter.GetComponent<Renderer>();if(renderer==null||!renderer.enabled)continue;
                    islandPoints.AddRange(filter.sharedMesh.vertices.Select(filter.transform.TransformPoint));
                }
                Assert(islandPoints.Count>0,"The visible island contains actual scene mesh geometry.");
                var island=f.ProjectBounds(image,camera,islandPoints);
                var sample=new CompositionSample{name=name,width=width,height=height,growthStep=active.Recipe.GrowthStep,
                    maxGrowthSteps=active.Recipe.MaxGrowthSteps,visibleBounds=Coordinates(visible),plantBounds=Coordinates(plant),
                    islandBounds=Coordinates(island),islandVertices=islandPoints.Count};report.compositions.Add(sample);
                Assert(Contains(visible,plant),"Plant geometry stays inside the visible composition area: "+plant+" / "+visible);
                Assert(Contains(visible,island),"The island, pot and visible decorations fit above the footer: "+island+" / "+visible);
                var labels=new[]{home.title,home.date,home.guide,home.stage};
                var nav=f.Bounds((RectTransform)home.NavigationBar.transform);var footer=f.Bounds(f.Preview.lowerBoundary);
                Assert(Contains(screen,nav)&&Contains(screen,footer)&&!Overlap(nav,footer),"Information footer and bottom tabs fit and do not overlap.");
                for(int i=0;i<labels.Length;i++)
                {
                    var label=labels[i];Assert(label!=null&&label.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(label.text),"Each home information label is visible.");
                    label.ForceMeshUpdate(false,true);var bounds=f.Bounds(label.rectTransform);
                    Assert(Contains(screen,bounds)&&!label.isTextOverflowing,"Home information is complete and inside the screen: "+label.name);
                    Assert(!Overlap(bounds,plant)&&!Overlap(bounds,nav),"Information avoids plant geometry and bottom tabs: "+label.name);
                    for(int j=0;j<i;j++)Assert(!Overlap(bounds,f.Bounds(labels[j].rectTransform)),"Information labels do not cover each other.");
                }
                var shop=f.Bounds((RectTransform)home.shopButton.transform);
                Assert(Contains(footer,shop)&&!Overlap(shop,f.Bounds(home.stage.rectTransform)),"Garden action fits inside its footer beside the growth status.");
                AssertHomeInformation(f);
                var buttons=new[]{home.TemporarySettingsButton,home.shopButton,home.NavigationBar.HomeButton,home.NavigationBar.MissionButton,home.NavigationBar.CourseButton};
                foreach(var button in buttons)
                {
                    Assert(button!=null&&button.IsActive()&&button.IsInteractable(),"Home action is enabled.");
                    var bounds=f.Bounds((RectTransform)button.transform);Assert(Contains(screen,bounds),"Home action is wholly visible: "+button.name);
                    f.AssertButtonHit(button);
                    foreach(var label in labels)Assert(!Overlap(bounds,f.Bounds(label.rectTransform)),"Button does not cover home information: "+button.name+" / "+label.name);
                }
                sample.plantPixels=f.VisiblePlantPixels();
                Assert(sample.plantPixels>=4,"The actual plant contributes visible GPU pixels in "+(minute==720?"daylight":"night lighting")+"; changed="+sample.plantPixels);
                if(minute==720)
                {
                    f.ClickButton(home.TemporarySettingsButton);Assert(home.navigation.settingsPanel.activeInHierarchy,"Settings button reaches the actual settings panel.");
                    f.ClickButton(home.NavigationBar.HomeButton);f.Render();f.ClickButton(home.shopButton);
                    Assert(Screen(f.Ui)=="shop","Footer garden action reaches the real garden shop.");
                    f.Click("돌아가기");
                    f.ClickButton(f.Ui.GetComponentsInChildren<MissionBottomNavigation>(false).Single().HomeButton);f.Render();
                    Assert(home.IsGardenVisible,"The bottom home tab returns from garden actions.");
                }
                Assert(camera==home.gardenCamera&&target==camera.targetTexture&&Resources.FindObjectsOfTypeAll<Camera>().Length==cameras,
                    "Composition, theme and home actions retain one existing garden camera and its render target.");
                Assert(StateCodec.Encode(f.Service.Snapshot)==saved&&f.Store.Writes==writes,"Framing, time-of-day, information and navigation do not alter saved plants or rewards.");
            }
        }

        static void AssertHomeInformation(Fixture f)
        {
            var home=f.Home;var plant=WeeklyPlants.Active(f.Service.Snapshot);
            Assert(plant.Completed?home.stage.text=="완성 · 사진첩 보관":new[]{"작은 씨앗","새싹","봉오리","꽃이 피었어요"}.Contains(home.stage.text),
                "Restored home shows its original compact stage caption.");
            Assert(Find<Text>("GardenHome.PlantStatusDetail")==null&&Find<Button>("GardenHome.MemoryRecords")==null&&Find<Button>("GardenHome.Album")==null,
                "Home-only status detail and added record/album actions have been removed.");
            Assert(Mathf.Abs(f.Preview.lowerBoundary.rect.height-160)<.5f,"Restored home uses the original 160-unit growth card.");
            var surface=f.Bounds(f.Preview.lowerBoundary);
            var stage=f.Bounds(home.stage.rectTransform);var shop=f.Bounds((RectTransform)home.shopButton.transform);
            Assert(Contains(surface,stage)&&Contains(surface,shop)&&!Overlap(stage,shop),"Original stage caption and garden action fit without overlap.");
            Assert(home.progressDots!=null&&home.progressDots.Length==4,"Four original growth icons remain available.");
            var occupied=new List<Rect>{stage,shop};
            foreach(var dot in home.progressDots)
            {
                Assert(dot!=null&&dot.gameObject.activeInHierarchy&&dot.GetComponentInChildren<PrototypeUiIcon>()!=null,"Original stage icon is visibly mounted.");
                var bounds=f.Bounds((RectTransform)dot.transform);
                Assert(Contains(surface,bounds)&&occupied.All(other=>!Overlap(bounds,other)),"Original icons occupy their own row without covering the caption or garden action.");occupied.Add(bounds);
            }
            Assert(home.shopButton.IsInteractable(),"Original garden action remains enabled.");f.AssertButtonHit(home.shopButton);
        }
        static void OpenAlbumFromHome(Fixture f)
        {
            f.ClickButton(f.Home.NavigationBar.CourseButton);Assert(Screen(f.Ui)=="course","Home record tab opens the existing records screen.");
            f.Click("PlantAlbum.Open");Assert(Screen(f.Ui)=="plant-album","Records screen opens the existing plant album.");
        }
        static GardenState LegacyHomeState(float growth)
        {
            var state=new GardenState{MigrationCompleted=true,InstallationId="synthetic-home-legacy-owner",Nutrient=137,LegacyGrowth=growth};
            var recipe=PlantGenerator.Create(state.InstallationId,"synthetic-home-legacy");
            recipe.Counts=new[]{4,0,0,0,0,2}; // Historical adapter's explicit baseline, before any practice credit.
            state.PlantAppearance=new GardenPlantAppearance{Recipe=recipe};
            GardenPlantVisuals.Sync(state);StateCodec.Validate(state);return state;
        }
        static void HomeStatusFlow(int width,int height)
        {
            foreach(float growth in new[]{.2f,.3f})
                using(var f=new Fixture(width,height,1,LegacyHomeState(growth)))
                {
                    var active=WeeklyPlants.Active(f.Service.Snapshot);
                    Assert(active.Recipe.MaxGrowthSteps==12&&active.Recipe.GrowthStep==(growth<.3f?4:12),"Real schema-1 migration retains the original twelve-stage recipe.");
                    Assert(active.Completed==(growth>=.3f)&&f.Service.Snapshot.PlantPhotos.Any(photo=>photo.Id==active.PhotoId&&photo.SourcePlantInstanceId==active.Id)==(growth>=.3f),
                        "Restoring home presentation preserves actual completion and automatic collection.");
                    AssertHomeInformation(f);f.Capture("home-status-legacy-"+active.Recipe.GrowthStep);
                    string before=StateCodec.Encode(f.Service.Snapshot);int writes=f.Store.Writes;
                    OpenAlbumFromHome(f);
                    if(active.Completed){f.Click("PlantAlbum.Photo."+active.PhotoId);Assert(Screen(f.Ui)=="plant-photo:"+active.PhotoId,"Existing album tile opens the correct collected individual.");f.Ui.ShowPlantAlbum();f.Render();}
                    f.Click("PlantAlbum.CurrentMemories");
                    Assert(Screen(f.Ui)=="plant-memories:"+active.Id&&Find<Text>("PlantMemory.Empty")!=null,"Record-free migrated plant opens useful empty list guidance.");
                    Assert(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes,"Opening migration records and album never adds photos or rewards.");
                }
            using(var f=new Fixture(width,height))
            {
                var state=f.Service.Snapshot;var plant=WeeklyPlants.Active(state);
                Assert(plant.Completed&&plant.Recipe.GrowthStep==2&&plant.Recipe.MaxGrowthSteps==21
                    &&state.PlantPhotos.Any(photo=>photo.Id==plant.PhotoId&&photo.SourcePlantInstanceId==plant.Id),"Restored home preserves explicit low-step completion without changing the recipe to maximum growth.");
                AssertHomeInformation(f);f.Capture("home-status-early-completed");
                string before=StateCodec.Encode(state);int writes=f.Store.Writes;
                for(int repeat=0;repeat<2;repeat++)
                {
                    OpenAlbumFromHome(f);f.Click("PlantAlbum.Photo."+plant.PhotoId);Assert(Screen(f.Ui)=="plant-photo:"+plant.PhotoId,"Existing album route opens the completed individual's correct photo.");
                    f.Home.navigation.ShowIsland();f.Render();
                }
                Assert(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes,"Repeated completed-plant viewing is entirely read-only.");
            }
        }
        static void HomeRecordsFlow(int width,int height)
        {
            using(var f=new Fixture(width,height))
            {
                var initial=WeeklyPlants.Active(f.Service.Snapshot);string plantId=initial.Id,photoId=initial.PhotoId;
                string dailyOrgan=initial.Memories.Single().OrganId;
                for(int i=0;i<45;i++)
                {
                    f.Clock.Now=f.Clock.Now.AddHours(6);
                    Assert(f.Service.SaveExperience("synthetic-list-"+i,"즐거움","합성 경험 "+i,"차분한 느낌","편안함","합성 생각"),f.Service.LastError);
                }
                Assert(f.Service.RefreshCycle(null),f.Service.LastError);f.Home.RefreshFromState();f.Render();
                var state=f.Service.Snapshot;var memories=WeeklyPlants.Active(state).Memories.OrderByDescending(item=>item.CreatedUtc,StringComparer.Ordinal).ToArray();
                string before=StateCodec.Encode(state);int writes=f.Store.Writes;
                Assert(memories.Length==46&&memories.All(memory=>PlantAlbumHistory.MemoryRecord(state,plantId,memory.OrganId)!=null),"All forty-six linked records remain available beyond the first rendered leaf page.");
                Assert(f.Home.ProceduralPlant.Geometry.Organs.Any(organ=>organ.OrganId==memories[0].OrganId&&organ.Selectable),"New latest memory automatically appears on the current garden leaf page.");
                f.Home.RefreshFromState();f.Render();
                Assert(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes
                    &&f.Home.ProceduralPlant.Geometry.Organs.Any(organ=>organ.OrganId==memories[0].OrganId&&organ.Selectable),"Unchanged reentry retains the latest memory page without writing or paying rewards.");
                OpenAlbumFromHome(f);f.Click("PlantAlbum.CurrentMemories");Assert(Screen(f.Ui)=="plant-memories:"+plantId,"Existing album entry reaches that individual's complete record list.");
                var rows=f.Ui.GetComponentsInChildren<Button>(false).Where(button=>button.name.StartsWith("PlantMemory.Record.",StringComparison.Ordinal)).OrderBy(button=>button.transform.GetSiblingIndex()).ToArray();
                Assert(rows.Length==46&&rows[0].name=="PlantMemory.Record."+memories[0].OrganId,"All linked records appear, with the latest first.");
                Assert(Find<Button>("PlantMemory.Previous")==null&&Find<Button>("PlantMemory.Next")==null,"Record list does not hide later dates behind the 3D leaf-page limit.");
                f.Capture("home-records-many-latest");var scroll=Find<ScrollRect>("PlantMemory.Scroll");
                Assert(scroll!=null&&scroll.vertical&&scroll.content.rect.height>scroll.viewport.rect.height,"Many records have a working independent vertical list.");
                scroll.verticalNormalizedPosition=0;f.Layout();f.Capture("home-records-many-last");
                f.Click("PlantMemory.Record."+dailyOrgan);
                Assert(GardenMemoPopup.IsOpen&&Find<Text>("GardenMemoPopup.Note").text=="합성 기억","Dated record button opens its own saved text.");
                f.Capture("home-records-selected-popup");f.Click("GardenMemoPopup.Edit");
                Find<InputField>("PlantMemory.EditField.0").text="고마움";Find<InputField>("PlantMemory.EditField.1").text="목록에서 수정한 합성 기억";
                f.Store.Fail=true;f.Click("PlantMemory.Save");f.Store.Fail=false;
                Assert(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes&&Find<InputField>("PlantMemory.EditField.1").text=="목록에서 수정한 합성 기억","List edit failure preserves both durable data and unsaved input.");
                f.Click("PlantMemory.Save");Assert(Screen(f.Ui)=="plant-memories:"+plantId,"Edit retry returns to the originating list.");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot,plantId,dailyOrgan).Note=="목록에서 수정한 합성 기억","Retried list edit updates the selected record only.");
                scroll=Find<ScrollRect>("PlantMemory.Scroll");Assert(scroll.verticalNormalizedPosition<.15f,"Edit return restores the scrolled part of the list.");
                before=StateCodec.Encode(f.Service.Snapshot);writes=f.Store.Writes;
                f.Click("PlantMemory.Record."+dailyOrgan);f.Click("GardenMemoPopup.Delete");
                f.Store.Fail=true;f.Click("PlantMemory.DeleteConfirm");f.Store.Fail=false;
                Assert(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes,"Delete failure preserves the linked record and reward state.");
                f.Click("PlantMemory.DeleteConfirm");f.Render();
                Assert(Screen(f.Ui)=="plant-memories:"+plantId&&Find<Button>("PlantMemory.Record."+dailyOrgan)==null,"Successful deletion returns to the same list without the deleted row.");
                Assert(!f.Ui.OpenPlantMemo(plantId,dailyOrgan,photoId,f.Ui.transform),"Deleted record cannot be reopened by a retained organ identity.");
                Assert(f.Service.Snapshot.Coins==state.Coins&&f.Service.Snapshot.PlantPhotos.Count==state.PlantPhotos.Count
                    &&PlantCodec.Encode(WeeklyPlants.Active(f.Service.Snapshot).Recipe)==PlantCodec.Encode(initial.Recipe),"List edits and deletion do not award coins, grow the plant or create photos.");
                f.Click("PlantMemory.ViewPlant");f.Render();Assert(f.Home.IsGardenVisible,"Record-list return reaches the actual garden.");
                Assert(!f.Home.ProceduralPlant.Geometry.Organs.Any(organ=>organ.OrganId==dailyOrgan&&organ.Selectable),"Deleted memory is not selectable in the restored garden.");
                f.Capture("home-records-deleted-return");
            }
        }

        static void MemoryPickingContract()
        {
            var root=new GameObject("Synthetic nearby-memory geometry");
            var plantNode=new GameObject("Synthetic linked plant");plantNode.transform.SetParent(root.transform,false);
            var view=plantNode.AddComponent<ProceduralPlantView>();Material opaque=null;
            bool RayAt(Vector2 point,out Ray ray){ray=new Ray(new Vector3(point.x,point.y,-10),Vector3.forward);return true;}
            bool Project(Vector3 point,out Vector2 screen){screen=new Vector2(point.x,point.y);return true;}
            PlantMeshData Data(bool second=false,bool stem=false)
            {
                var data=new PlantMeshData();
                void Quad(float left,float right,float bottom,float top,float z,string id)
                {
                    int start=data.TriangleCount;var color=new PlantColor(.2,.7,.3);
                    data.Triangle(new PlantVector(left,bottom,z),new PlantVector(right,bottom,z),new PlantVector(right,top,z),color);
                    data.Triangle(new PlantVector(left,bottom,z),new PlantVector(right,top,z),new PlantVector(left,top,z),color);
                    if(id!=null)data.Organs.Add(new PlantOrganGeometry{OrganId=id,FirstTriangle=start,TriangleCount=2,Selectable=true,Center=new PlantVector((left+right)/2,(bottom+top)/2,z)});
                }
                Quad(0,10,0,10,0,"leaf-a");if(second)Quad(20,30,0,10,0,"leaf-b");if(stem)Quad(17,19,4,6,-1,null);return data;
            }
            GardenMemoryPicker.Snapshot Capture(PlantMeshData data)
            {typeof(ProceduralPlantView).GetProperty("Geometry").SetValue(view,data);return GardenMemoryPicker.Capture(view,root.transform);}
            try
            {
                var one=Capture(Data());RayAt(new Vector2(5,5),out var exact);
                Assert(one.TryPick(exact,out var id,out _)&&id=="leaf-a","Controlled linked triangle accepts its exact ray.");
                Assert(one.TryPickNearby(new Vector2(5,5),RayAt,Project,16,out id,out _)&&id=="leaf-a","Exact memory wins before nearby resolution.");
                Assert(one.TryPickNearby(new Vector2(18,5),RayAt,Project,16,out id,out _)&&id=="leaf-a","Eight-pixel miss resolves the sole visible linked leaf.");
                Assert(!one.TryPickNearby(new Vector2(27,5),RayAt,Project,16,out _,out _),"Seventeen-pixel miss remains outside the sixteen-pixel allowance.");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Synthetic opaque pot blocker";blocker.transform.SetParent(root.transform,false);
                blocker.transform.localPosition=new Vector3(5,5,-1);blocker.transform.localScale=new Vector3(12,12,.5f);
                opaque=new Material(Shader.Find("Universal Render Pipeline/Lit")){renderQueue=2000};blocker.GetComponent<Renderer>().sharedMaterial=opaque;
                var blocked=Capture(Data());
                Assert(!blocked.TryPickNearby(new Vector2(5,5),RayAt,Project,16,out _,out _)
                    &&!blocked.TryPickNearby(new Vector2(18,5),RayAt,Project,16,out _,out _),"Exact and nearby picking cannot open an opaque-occluded memory.");
                UnityEngine.Object.DestroyImmediate(blocker);
                var stem=Capture(Data(stem:true));
                Assert(!stem.TryPickNearby(new Vector2(18,5),RayAt,Project,16,out _,out _),"A direct hit on non-memory plant geometry is not redirected to a nearby leaf.");
                var two=Capture(Data(second:true));
                Assert(!two.TryPickNearby(new Vector2(15,5),RayAt,Project,16,out _,out _),"Equidistant linked leaves are ambiguous and do not open either record.");
                Assert(two.TryPickNearby(new Vector2(17,5),RayAt,Project,16,out id,out _)&&id=="leaf-b","A clearly nearer linked leaf resolves to its own identity.");
                Assert(two.TryPickNearby(new Vector2(5,5),RayAt,Project,16,out id,out _)&&id=="leaf-a","An exact hit is preserved even when a second candidate is within tolerance.");
                var removed=Data(second:true);removed.Organs[1].Selectable=false;var deleted=Capture(removed);
                Assert(!deleted.TryPickNearby(new Vector2(25,5),RayAt,Project,16,out _,out _),"A retained but unlinked leaf cannot be selected.");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);if(opaque!=null)UnityEngine.Object.DestroyImmediate(opaque);}
        }

        static void MobileFlow(int width, int height)
        {
            using (var f = new Fixture(width, height))
            {
                var state = f.Service.Snapshot; var original = WeeklyPlants.Active(state); string plantId = original.Id;
                var memory = original.Memories.Single(); string organId = memory.OrganId, memoId = memory.RecordId;
                string recipe = PlantCodec.Encode(original.Recipe), photoId = original.PhotoId;
                Assert(original.Completed && state.PlantPhotos.Count == 1 && !state.PlantAppearance.IsArchived,
                    "Completed plant must be auto-collected while remaining in its garden.");
                Assert(f.Home.ShownPlantId == plantId && f.Home.ProceduralPlant.Geometry.Organs.Any(o => o.OrganId == organId && o.Selectable),
                    "Garden did not bind the active individual's linked leaf.");
                int writes = f.Store.Writes; string before = StateCodec.Encode(state);
                f.Capture("garden-memory");
                var homeImage = f.Preview.GetComponent<RawImage>();
                Assert(!f.Preview.TryPickMemo(f.Bounds((RectTransform)homeImage.transform).max+Vector2.one*8,f.Camera,out _),"Home picking rejects points outside the actual RawImage.");
                Vector2 homePoint = f.LeafPoint(f.Home.ProceduralPlant, f.Home.gardenCamera, homeImage,
                    point => f.Preview.TryPickMemo(point, f.Camera, out var id) && id == organId);
                f.Tap(homeImage, homePoint);
                Assert(GardenMemoPopup.IsOpen && Find<Text>("GardenMemoPopup.Note").text == "합성 기억", "Garden tap opened the wrong record.");
                f.Capture("garden-memory-popup"); f.Click("GardenMemoPopup.Close");
                f.Drag(homeImage, homePoint); Assert(!GardenMemoPopup.IsOpen, "Orbit drag was interpreted as a memory tap.");
                f.Home.ResetView(); f.Render();
                Assert(StateCodec.Encode(f.Service.Snapshot) == before && f.Store.Writes == writes, "Reading/orbiting changed saved state.");

                // Exercise the actual popup -> editor -> failed write -> retry buttons.
                homePoint = f.LeafPoint(f.Home.ProceduralPlant, f.Home.gardenCamera, homeImage,
                    point => f.Preview.TryPickMemo(point, f.Camera, out var id) && id == organId);
                f.Tap(homeImage, homePoint); f.Click("GardenMemoPopup.Edit");
                Assert(Screen(f.Ui) == "plant-memory-edit:" + organId, "Edit action did not open this leaf's editor.");
                Find<InputField>("PlantMemory.EditField.0").text = "고마움";
                Find<InputField>("PlantMemory.EditField.1").text = "수정한 합성 기억";
                var staleSave = Find<Button>("PlantMemory.Save").onClick;
                f.Store.Fail = true; f.Click("PlantMemory.Save");
                Assert(Find<InputField>("PlantMemory.EditField.1").text == "수정한 합성 기억"
                    && !string.IsNullOrEmpty(Find<Text>("PlantMemory.Error").text), "Failed save discarded the edit or gave no retry feedback.");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId).Note == "합성 기억", "Failed edit escaped its transaction.");
                f.Store.Fail = false; f.Click("PlantMemory.Save");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId).Note == "수정한 합성 기억", "Retry did not commit the edit.");
                writes = f.Store.Writes; staleSave.Invoke();
                Assert(f.Store.Writes == writes, "A stale editor callback wrote again after navigation.");

                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photoId); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                var image = view.GetComponent<RawImage>();
                Assert(!view.TryPickMemo(f.Bounds((RectTransform)image.transform).max+Vector2.one*8,f.Camera,out _),"Photo picking rejects points outside the actual RawImage.");
                Assert(view.RenderedMemoIds.Contains(organId) && view.MemoRepresentationKey == GardenMemoryPicker.RepresentationKey,
                    "Album photo did not render canonical memory geometry.");
                Assert(Find<Text>("PlantAlbum.DetailPeriod").text == PlantAlbumHistory.Period(f.Service.Snapshot.PlantPhotos.Single(), f.Service.CurrentTimeZone),
                    "The photo period differs from its stored business dates.");
                var savedMemo = PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId);
                Assert(savedMemo.BasicMissionId == "course:P01" && savedMemo.AdditionalMissionId == "free-mission:M17"
                    && string.IsNullOrEmpty(savedMemo.BasicMissionTitle) && string.IsNullOrEmpty(savedMemo.AdditionalMissionTitle)
                    && Find<Text>("PlantAlbum.RecordBasic." + memoId).text == "기본 · 미션명 기록 없음"
                    && Find<Text>("PlantAlbum.RecordAdditional." + memoId).text == "추가 · 미션명 기록 없음",
                    "Legacy ID-only memories must keep their saved IDs while showing code-free missing-title labels.");
                var camera = Studio<Camera>("camera"); var plant = Studio<ProceduralPlantView>("plant");
                Vector2 pointOnPhoto = f.LeafPoint(plant, camera, image, point => view.TryPickMemo(point, f.Camera, out var id) && id == organId);
                // A second thumbnail replaces the shared studio. The first view must
                // retain its own immutable ray basis and geometry for input.
                var node = new GameObject("Synthetic other thumbnail", typeof(RectTransform), typeof(RawImage));
                node.transform.SetParent(image.transform.parent, false);
                FigmaMissionUi.SetRect((RectTransform)node.transform, -2000, 0, 80, 80);
                var other = node.AddComponent<GardenPlantPhotoView>();
                var alternate = PlantGenerator.Create("synthetic", "other-photo", 21, PlantGrowthMode.MissionCompletions);
                alternate.MeshVersion = PlantGeometry.CurrentMeshVersion;
                other.Initialize(alternate, 1.9f, false); Assert(other.RenderNow(), "Second photo did not render.");
                Assert(view.TryPickMemo(pointOnPhoto, f.Camera, out var held) && held == organId, "Shared studio reuse changed the first photo's picker.");
                UnityEngine.Object.DestroyImmediate(node);
                f.Tap(image, pointOnPhoto); Assert(GardenMemoPopup.IsOpen && Find<Text>("GardenMemoPopup.Note").text == "수정한 합성 기억",
                    "Photo leaf tap did not open the edited record.");
                f.Click("GardenMemoPopup.Close"); f.Drag(image, pointOnPhoto);
                Assert(!GardenMemoPopup.IsOpen, "Photo orbit drag opened a record.");
                view.ResetView(); f.Render(); f.Capture("album-memory-records");
                var previewRect = f.Bounds((RectTransform)view.transform);
                var records = Find<ScrollRect>("PlantAlbum.RecordsViewport"); records.verticalNormalizedPosition = 0; f.Layout();
                Assert(f.Bounds((RectTransform)view.transform) == previewRect, "Record scrolling moved the 3D preview.");

                f.Clock.Now = f.Clock.Now.AddDays(1); Complete(f.Service, "synthetic-next", "free-mission:M17");
                state = f.Service.Snapshot; string nextId = WeeklyPlants.Active(state).Id; int coins = state.Coins;
                Assert(nextId != plantId && WeeklyPlants.Active(state).Memories.Count == 0, "Next growth did not create an independent plant.");
                f.Home.navigation.ShowIsland(); f.Home.RefreshFromState(); f.Render();
                Assert(f.Home.ShownPlantId == nextId && !f.Home.ProceduralPlant.Geometry.Organs.Any(o => o.Selectable), "Archived leaves leaked into the next garden plant.");
                f.Capture("next-individual");
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(photoId); f.Render();
                f.Click("PlantAlbum.Record." + memoId); f.Click("GardenMemoPopup.Delete");
                f.Store.Fail = true; f.Click("PlantMemory.DeleteConfirm");
                Assert(PlantAlbumHistory.MemoryRecord(f.Service.Snapshot, plantId, organId) != null, "Failed delete lost the record.");
                f.Store.Fail = false; f.Click("PlantMemory.DeleteConfirm"); f.Render();
                state = f.Service.Snapshot;
                Assert(PlantAlbumHistory.MemoryRecord(state, plantId, organId) == null
                    && PlantAlbumHistory.Records(state, state.PlantPhotos.Single()).Length == 0, "Deleted text reappeared through album fallback.");
                var retainedLeaf = PlantAlbumHistory.Leaves(state, plantId).Single();
                Assert(retainedLeaf.OrganId == organId && retainedLeaf.Slot == 0 && !retainedLeaf.Linked, "Deletion removed or moved the retained leaf geometry.");
                Assert(PlantCodec.Encode(state.PlantCollection.Plants.Single(p => p.Id == plantId).Recipe) == recipe
                    && WeeklyPlants.Active(state).Id == nextId && state.Coins == coins, "Record deletion changed appearance, active growth target or coins.");
                Assert(!Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview").RenderedMemoIds.Contains(organId), "Deleted leaf remained clickable.");
                f.Capture("album-deleted-memory");
                using (var reopened = new GardenStateService(f.Store, f.Clock, new DemoBalanceConfig()))
                { Assert(reopened.Open(), reopened.LastError); Assert(StateCodec.Encode(reopened.Snapshot) == StateCodec.Encode(state), "Reopening changed the committed plant collection."); }
            }
        }

        static T Studio<T>(string name) where T : class
        {
            var studio = typeof(GardenPlantPhotoView).GetField("studio", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return (T)studio.GetType().GetField(name, Private).GetValue(studio);
        }
        static void SetPitch(GardenPlantPhotoView view, float angle)
        { typeof(GardenPlantPhotoView).GetField("pitch", Private).SetValue(view, angle); Assert(view.RenderNow(), "Render changed elevation."); }
        static PlantRecipe Mature(string identity, int primary, int secondary)
        {
            var recipe = PlantGenerator.Create("synthetic-memory-bounds", identity, 21, PlantGrowthMode.MissionCompletions);
            recipe.MeshVersion = PlantGeometry.CurrentMeshVersion; recipe.Counts[primary] = 14; recipe.Counts[secondary] += 7;
            for (int i = 0; i < 21; i++) recipe = PlantGenerator.Advance(recipe);
            return recipe;
        }
        static void StressRender()
        {
            using (var f = new Fixture(390, 844))
            {
                f.Home.navigation.ShowCourse(); f.Ui.ShowPlantPhoto(f.Service.Snapshot.PlantPhotos.Single().Id); f.Render();
                var view = Find<GardenPlantPhotoView>("PlantAlbum.DetailPreview");
                var leaves = Enumerable.Range(0, 60).Select(i => new PlantMemoryLeaf { OrganId = "stress/memory/" + i, Slot = i, Linked = i % 11 != 0 }).ToArray();
                int speciesIndex = 0;
                foreach (var species in PlantCatalog.All)
                {
                    var recipe = Mature("mature-" + species.Id, species.Primary, species.Secondary);
                    Assert(recipe.Mature && recipe.GrowthStep == 21 && recipe.MeshVersion == 6, "Stress fixture must use new 21-step v6 geometry.");
                    for (int page = 0; page < 3; page++)
                    {
                        view.Initialize(recipe, 1.9f, true, null, leaves, page); Assert(view.RenderNow(), "Render mature memory page.");
                        var geometry = Studio<ProceduralPlantView>("plant").Geometry;
                        Assert(geometry.TriangleCount <= 8000 && geometry.Organs.Count == (page < 2 ? 21 : 18), "A memory page exceeded mesh budget or lost its final slots.");
                        Assert(view.RenderedMemoIds.SequenceEqual(leaves.Where(l => l.Slot / 21 == page && l.Linked).Select(l => l.OrganId)), "Page picked IDs differ from displayed linked leaves.");
                        AssertFrame(Studio<ProceduralPlantView>("plant"), Studio<Camera>("camera"));
                        if (speciesIndex == 0) f.Capture("mature-page-" + page);
                    }
                    speciesIndex++;
                }
                Assert(speciesIndex >= 6, "Species matrix was unexpectedly empty.");
                view.SetMemoryPage(99); Assert(view.MemoryPage == 2, "Upper page bound lost its last page.");
                view.SetMemoryPage(-1); Assert(view.MemoryPage == 0, "Lower page bound was not clamped.");
                var sample = Mature("nature-repeatable", 0, 1);
                view.Initialize(sample, 1.9f, true, null, leaves); view.RenderNow();
                string nature = view.EnvironmentKey; byte[] pixels = f.PhotoBytes(view);
                view.Rotate(87); view.SetZoom(.75f); view.RenderNow();
                Assert(view.EnvironmentKey == nature, "Orbit/zoom changed the saved environment identity.");
                view.ResetView(); view.RenderNow();
                Assert(pixels.SequenceEqual(f.PhotoBytes(view)), "Returning to the same recipe, time and angles changed the actual rendered photo.");
                foreach (float angle in new[] { 0f, -15f, -30f, -85f })
                {
                    SetPitch(view, angle); Assert(Mathf.Abs(view.LastRenderedEnvironmentOpacity - GardenPlantPhotoView.EnvironmentOpacity(angle)) < .00001f,
                        "Low-angle environment did not apply its uniform fade.");
                    AssertFrame(Studio<ProceduralPlantView>("plant"), Studio<Camera>("camera"));
                    f.Capture("memory-low-angle-" + Math.Abs(angle));
                }
                Assert(GardenPlantPhotoView.SharedCameraCount == 1 && GardenPlantPhotoView.OwnedEnvironmentMeshCount <= 4,
                    "Memory pages created extra cameras or unbounded environment meshes.");
            }
        }
        static void AssertFrame(ProceduralPlantView plant, Camera camera)
        {
            foreach (var vertex in plant.Geometry.Vertices)
            {
                var viewport = camera.WorldToViewportPoint(plant.transform.TransformPoint(new Vector3((float)vertex.X, (float)vertex.Y, (float)vertex.Z)));
                Assert(viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1,
                    "Plant or memory vertex is clipped at the default photo zoom: " + viewport);
            }
        }

        sealed class Fixture : IDisposable
        {
            public readonly GardenHomePresenter Home; public readonly WeekOneQuestDemo Ui;
            public readonly GardenStateService Service; public readonly Store Store = new Store(); public readonly Clock Clock = new Clock();
            public readonly Camera Camera; public readonly PrototypeGardenPreviewLayout Preview;
            readonly EventSystem events; readonly RenderTexture target; readonly int width, height;
            public Fixture(int width, int height, int compositionStage = 0, GardenState initialState = null)
            {
                this.width = width; this.height = height;
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity", OpenSceneMode.Single); AndroidPlatformBridge.UseMockProvider();
                if(initialState!=null)Store.Save(initialState);
                Service = new GardenStateService(Store, Clock, new DemoBalanceConfig { VisitorChancePercent = 0 }); Assert(Service.Open(), Service.LastError);
                if(compositionStage!=1)
                {
                    Complete(Service, "synthetic-basic", "course:P01"); Complete(Service, "synthetic-additional", "free-mission:M17");
                    Assert(Service.BeginDailyEmotion(Service.Snapshot.LastCycleId), Service.LastError);
                    Assert(Service.CompleteDailyEmotion(DailyEmotionPolicy.RecordId(Service.Snapshot.LastCycleId), "편안함", "합성 기억"), Service.LastError);
                    if(compositionStage==2)
                    {
                        int remaining=WeeklyPlants.Active(Service.Snapshot).Recipe.MaxGrowthSteps-WeeklyPlants.Active(Service.Snapshot).Recipe.GrowthStep;
                        for(int step=0;step<remaining;step++){Clock.Now=Clock.Now.AddDays(1);Complete(Service,"composition-growth-"+step,"free-mission:M17");}
                    }
                    else Assert(Service.SetPlantCompleted(WeeklyPlants.Active(Service.Snapshot).Id, true), Service.LastError);
                }
                Home = UnityEngine.Object.FindFirstObjectByType<GardenHomePresenter>(FindObjectsInactive.Include); Ui = Home.activity;
                Ui.Initialize(Service, true);
                // GardenHomePresenter is not ExecuteAlways: opening this scene
                // in edit mode never wires its real Awake-owned button listeners.
                // Refuse an already-awakened instance, then invoke that lifecycle
                // once; do not fabricate listeners or invoke it from Render.
                var didAwake=typeof(MonoBehaviour).GetProperty("didAwake",BindingFlags.Instance|BindingFlags.Public);
                Assert(!Application.isPlaying&&didAwake!=null&&!(bool)didAwake.GetValue(Home),
                    "Copied edit-mode presenter has not already received Unity Awake.");
                typeof(GardenHomePresenter).GetMethod("Awake",Private).Invoke(Home,null);
                Home.navigation.ShowIsland(); Home.RefreshFromState(); Home.ResetView();
                Camera = GameObject.Find("MockupCamera").GetComponent<Camera>(); Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>().Where(c => c.gameObject.scene.IsValid() && c.isRootCanvas))
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = canvas == Home.homeCanvas ? .75f : 1; }
                target = new RenderTexture(width, height, 24); target.Create(); Camera.targetTexture = target; Camera.aspect = width / (float)height;
                Preview = Home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                events = UnityEngine.Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (events == null) events = new GameObject("Synthetic memory event system", typeof(EventSystem)).GetComponent<EventSystem>();
                events.gameObject.SetActive(true);
                if (EventSystem.current == null) typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null);
                EventSystem.current = events; Render();
            }
            public void Layout()
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    foreach (var caster in Resources.FindObjectsOfTypeAll<GraphicRaycaster>().Where(c => c.isActiveAndEnabled))
                        if (!RaycasterManager.GetRaycasters().Contains(caster)) typeof(BaseRaycaster).GetMethod("OnEnable", Private).Invoke(caster, null);
                    foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(t => t.gameObject.activeInHierarchy)) text.ForceMeshUpdate(false, true);
                    Canvas.ForceUpdateCanvases();
                    foreach (var fit in Resources.FindObjectsOfTypeAll<FigmaMissionCanvasFit>().Where(x => x.isActiveAndEnabled)) fit.Fit();
                    foreach (var page in Resources.FindObjectsOfTypeAll<FocusMobilePageLayout>().Where(x => x.isActiveAndEnabled)) page.Refresh();
                    foreach (var detail in Resources.FindObjectsOfTypeAll<GardenPlantAlbumDetailLayout>().Where(x => x.isActiveAndEnabled)) detail.Refresh();
                    foreach (var popup in Resources.FindObjectsOfTypeAll<GardenMemoPopup>().Where(x => x.isActiveAndEnabled)) popup.Layout();
                    Canvas.ForceUpdateCanvases();
                }
                Camera.Render();
            }
            public void Render()
            {
                Layout(); Preview.Apply(); Home.gardenCamera.Render();
                foreach (var view in Resources.FindObjectsOfTypeAll<GardenPlantPhotoView>().Where(v => v.isActiveAndEnabled)) Assert(view.RenderNow(), "Visible photo render failed.");
                Layout();
            }
            public Rect Bounds(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(Camera, corners[0]), b = RectTransformUtility.WorldToScreenPoint(Camera, corners[2]);
                return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            }
            Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(rect.rect.center));
            public Rect ProjectBounds(RawImage image,Camera view,IEnumerable<Vector3> vertices)
            {
                var rect=(RectTransform)image.transform;var uv=image.uvRect;
                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);int count=0;
                foreach(var world in vertices)
                {
                    var viewport=view.WorldToViewportPoint(world);Assert(viewport.z>0,"Visible mesh is in front of the garden camera.");
                    var local=rect.rect.min+Vector2.Scale(new Vector2((viewport.x-uv.x)/uv.width,(viewport.y-uv.y)/uv.height),rect.rect.size);
                    var point=RectTransformUtility.WorldToScreenPoint(Camera,rect.TransformPoint(local));min=Vector2.Min(min,point);max=Vector2.Max(max,point);count++;
                }
                Assert(count>0,"Projected geometry has vertices.");return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
            public void AssertButtonHit(Button button)
            {
                var data=Pointer(Center((RectTransform)button.transform));
                Assert(data.pointerPressRaycast.gameObject.GetComponentInParent<Button>()==button,"Actual home pointer target is unobscured: "+button.name);
            }
            public void ClickButton(Button button)
            {
                Assert(button!=null&&button.IsActive()&&button.IsInteractable(),"Actual home button is enabled.");Layout();
                var data=Pointer(Center((RectTransform)button.transform));
                Assert(data.pointerPressRaycast.gameObject.GetComponentInParent<Button>()==button,"Actual home click is unobscured: "+button.name);
                ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);report.clicks++;Layout();
            }
            static Color32[] ReadPixels(RenderTexture source)
            {
                var prior=RenderTexture.active;var image=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
                try{RenderTexture.active=source;image.ReadPixels(new Rect(0,0,source.width,source.height),0,0);image.Apply();return image.GetPixels32();}
                finally{RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(image);}
            }
            public int VisiblePlantPixels()
            {
                var target=Home.gardenCamera.targetTexture;var renderers=Home.ProceduralPlant.GetComponentsInChildren<Renderer>(false).Where(item=>item.enabled).ToArray();
                Assert(renderers.Length>0,"Actual plant has an enabled renderer.");var withPlant=ReadPixels(target);Color32[] withoutPlant;
                try{foreach(var item in renderers)item.enabled=false;Home.gardenCamera.Render();withoutPlant=ReadPixels(target);}
                finally{foreach(var item in renderers)item.enabled=true;Home.gardenCamera.Render();}
                int changed=0;
                for(int i=0;i<withPlant.Length;i++)if(Math.Abs(withPlant[i].r-withoutPlant[i].r)>8||Math.Abs(withPlant[i].g-withoutPlant[i].g)>8||Math.Abs(withPlant[i].b-withoutPlant[i].b)>8)changed++;
                return changed;
            }
            public Vector2 LeafPoint(ProceduralPlantView plant, Camera camera, RawImage image, Func<Vector2, bool> picker)
            {
                var data = plant.Geometry; var rect = (RectTransform)image.transform; var uv = image.uvRect;
                foreach (var organ in data.Organs.Where(o => o.Selectable))
                    for (int triangle = organ.FirstTriangle; triangle < organ.FirstTriangle + organ.TriangleCount; triangle++)
                    {
                        var a = data.Vertices[triangle * 3]; var b = data.Vertices[triangle * 3 + 1]; var c = data.Vertices[triangle * 3 + 2];
                        var localCenter = new Vector3((float)(a.X + b.X + c.X) / 3, (float)(a.Y + b.Y + c.Y) / 3, (float)(a.Z + b.Z + c.Z) / 3);
                        var viewport = camera.WorldToViewportPoint(plant.transform.TransformPoint(localCenter));
                        if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                        var local = rect.rect.min + Vector2.Scale(new Vector2((viewport.x - uv.x) / uv.width, (viewport.y - uv.y) / uv.height), rect.rect.size);
                        Vector2 point = RectTransformUtility.WorldToScreenPoint(Camera, rect.TransformPoint(local));
                        if (!picker(point)) continue;
                        var hits = Hits(point); if (hits.Count > 0 && hits[0].gameObject == image.gameObject) return point;
                    }
                throw new InvalidOperationException("No actually visible linked leaf point reaches the RawImage and picker: " + image.name);
            }
            List<RaycastResult> Hits(Vector2 point)
            { var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = point }, hits); report.raycasts++; return hits; }
            PointerEventData Pointer(Vector2 point)
            {
                var hits = Hits(point); Assert(hits.Count > 0, "No actual canvas raycast at " + point);
                return new PointerEventData(events) { pointerId = -1, position = point, pressPosition = point,
                    button = PointerEventData.InputButton.Left, pointerPressRaycast = hits[0], pointerCurrentRaycast = hits[0], eligibleForClick = true, useDragThreshold = true };
            }
            public void Tap(RawImage image, Vector2 point)
            {
                var data = Pointer(point); Assert(data.pointerPressRaycast.gameObject == image.gameObject, "Memory surface is not the top UI hit.");
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler);
                report.clicks++; Layout();
            }
            public void Drag(RawImage image, Vector2 point)
            {
                var data = Pointer(point); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerDownHandler);
                data.dragging = true; data.eligibleForClick = false; ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.beginDragHandler);
                data.position = point + new Vector2(34, 13); data.delta = new Vector2(34, 13); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.dragHandler);
                data.position = point; data.delta = new Vector2(-34, -13); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(image.gameObject, data, ExecuteEvents.endDragHandler); Layout();
            }
            public void Click(string name)
            {
                Layout(); var button = Find<Button>(name); Assert(button != null && button.IsInteractable(), "Missing interactive button: " + name);
                var scroll = button.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    // Reveal through the actual scroll viewport, without moving controls.
                    for (int i = 0; i <= 20; i++)
                    {
                        scroll.verticalNormalizedPosition = 1 - i / 20f; Layout();
                        var hits = Hits(Center((RectTransform)button.transform));
                        if (hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform))) break;
                    }
                }
                var data = Pointer(Center((RectTransform)button.transform));
                Assert(data.pointerPressRaycast.gameObject == button.gameObject || data.pointerPressRaycast.gameObject.transform.IsChildOf(button.transform), "Button is obscured: " + name);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); report.clicks++; Layout();
            }
            public byte[] PhotoBytes(GardenPlantPhotoView view) => Pixels((RenderTexture)view.GetComponent<RawImage>().texture);
            static byte[] Pixels(RenderTexture source)
            {
                var prior = RenderTexture.active; var image = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                try { RenderTexture.active = source; image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply(); return image.EncodeToPNG(); }
                finally { RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
            }
            public void Capture(string name)
            { Layout(); string file = name + "-" + width + "x" + height + ".png"; File.WriteAllBytes(Path.Combine(output, file), Pixels(target)); report.captures.Add(file); }
            public void Dispose()
            {
                GardenMemoPopup.TryCloseOpen(); Camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                // Runtime-only components do not receive OnDestroy in EditMode; this
                // is fixture cleanup, not a substitute for the photo lifecycle test.
                if (Home.ProceduralPlant != null) typeof(ProceduralPlantView).GetMethod("OnDestroy", Private).Invoke(Home.ProceduralPlant, null);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Service.Dispose();
            }
        }
    }
}
