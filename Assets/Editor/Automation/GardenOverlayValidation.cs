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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Read the scene asset into a disposable preview scene; never initialize a file-backed app service.
    [InitializeOnLoad]
    public static class GardenOverlayValidation
    {
        [Serializable] sealed class Request { public string runId; public bool capture = true; }
        [Serializable] sealed class CheckResult { public string name, status, detail; }
        [Serializable] sealed class Report
        {
            public string runId, utc, unityVersion;
            public string environment="Isolated scene-asset preview / synthetic memory state / no Play mode or user saves";
            public int passed, failed;
            public List<CheckResult> checks=new List<CheckResult>();
            public List<string> captures=new List<string>();
        }
        static bool running;
        static double nextPoll;
        static string outputOverride,reportOverride;
        static string OutputRoot=>outputOverride??Environment.GetEnvironmentVariable("CAPSTONE_GARDEN_OVERLAY_OUTPUT")
            ??Path.Combine(Directory.GetCurrentDirectory(),"artifacts","garden-overlay-validation");
        static string RequestPath=>Path.Combine(OutputRoot,"request.json");

        static GardenOverlayValidation(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(running||EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+1;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(RequestPath))return;
            Request request;
            try{request=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath))??new Request();}
            catch{File.Delete(RequestPath);return;}
            File.Delete(RequestPath);Execute(request);
        }
        [MenuItem("Capstone Mockup/Validate Garden Overlay (Synthetic)")]
        public static void Run()=>Execute(new Request{runId="manual-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")});
        public static void RunBatch()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-gardenOverlayReport");
            if(!Application.isBatchMode||Application.companyName!="CapstoneSyntheticValidation")
                throw new InvalidOperationException("Use a separately copied CapstoneSyntheticValidation batch project.");
            if(at>=0&&at+1<args.Length)
            {reportOverride=Path.GetFullPath(args[at+1]);outputOverride=Path.GetDirectoryName(reportOverride);}
            else if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CAPSTONE_GARDEN_OVERLAY_OUTPUT")))
                throw new InvalidOperationException("Supply -gardenOverlayReport or CAPSTONE_GARDEN_OVERLAY_OUTPUT for isolated evidence.");
            int failed=Execute(new Request{runId="batch-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"),capture=!args.Contains("-gardenOverlayNoCapture")});
            EditorApplication.Exit(failed==0?0:1);
        }
        static int Execute(Request request)
        {
            if(running||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return 1;
            running=true;Directory.CreateDirectory(OutputRoot);
            var report=new Report{runId=request.runId??"requested",utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            try
            {
                if(GraphicsSettings.currentRenderPipeline!=null&&RenderPipelineManager.currentPipeline==null)Blitter.Cleanup();
                foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(390,844),new Vector2Int(1200,2000)})
                using(var fixture=new Fixture(size))
                {
                    Check(report,"Top outline panel and centered bottom shop "+size,()=>Surfaces(fixture));
                    Check(report,"Coin icon stays close to short and large balances "+size,()=>CoinBalances(fixture));
                    Check(report,"Garden-only panel and shop visibility "+size,()=>Visibility(fixture));
                    Check(report,"Night/day overlay ink and effective date "+size,()=>DayAndNight(fixture,report,request.capture));
                    Check(report,"Wheel/pinch preserve state and orbit transition "+size,()=>Zoom(fixture,report,request.capture));
                    Check(report,"Bottom tabs, shop and panel intercept garden input "+size,()=>UiInterception(fixture));
                    Check(report,"Settings has neutral tabs and a hittable home return "+size,()=>SettingsNavigation(fixture));
                }
            }
            catch(Exception error){report.failed++;report.checks.Add(new CheckResult{name="Isolated preview fixture",status="fail",detail=error.ToString()});}
            finally
            {
                running=false;string path=reportOverride??Path.Combine(OutputRoot,"report.json");File.WriteAllText(path,JsonUtility.ToJson(report,true));
                Debug.Log("GARDEN_OVERLAY_VALIDATION passed="+report.passed+" failed="+report.failed+" report="+path);
            }
            return report.failed;
        }
        static void Check(Report report,string name,Action check)
        {
            try{check();report.passed++;report.checks.Add(new CheckResult{name=name,status="pass",detail=""});}
            catch(Exception error){report.failed++;report.checks.Add(new CheckResult{name=name,status="fail",detail=error.ToString()});}
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static Rect RectIn(RectTransform owner,RectTransform child)
        {
            var corners=new Vector3[4];child.GetWorldCorners(corners);
            var a=owner.InverseTransformPoint(corners[0]);var b=owner.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        static bool Contains(Rect outer,Rect inner)=>inner.xMin>=outer.xMin-.5f&&inner.xMax<=outer.xMax+.5f
            &&inner.yMin>=outer.yMin-.5f&&inner.yMax<=outer.yMax+.5f;
        static bool Overlap(Rect a,Rect b)=>Mathf.Min(a.xMax,b.xMax)>Mathf.Max(a.xMin,b.xMin)+.5f
            &&Mathf.Min(a.yMax,b.yMax)>Mathf.Max(a.yMin,b.yMin)+.5f;

        static void Surfaces(Fixture f)
        {
            var home=f.Home;var content=home.homeCanvas.transform.Find("SafePortraitFrame/MainContent");
            foreach(var name in new[]{"Brand","Home overview surface"})
            {var node=content.Find(name);Require(node==null||!node.gameObject.activeInHierarchy,"Removed upper element remains visible: "+name);}
            foreach(var name in new[]{"TodayMissionCard","TodayMissionCard_Edge","DailyStatus","MissionName","MissionEmblem","Garden view controls"})
            {var node=content.Find("GardenAndGrowth/"+name);Require(node==null||!node.gameObject.activeInHierarchy,"Removed lower element remains visible: "+name);}
            var growth=content.Find("GardenAndGrowth/Growth surface") as RectTransform;
            Require(growth!=null&&growth.gameObject.activeInHierarchy,"The upper plant information panel is missing.");
            var growthImage=growth.GetComponent<Image>();
            Require(growthImage!=null&&growthImage.color.a<.01f,"The upper panel must have a transparent fill.");
            var theme=growth.Find("Garden theme surface");
            Require(theme==null||!theme.gameObject.activeInHierarchy,"An opaque theme decoration remains behind the upper panel.");
            Require(growth.anchorMin==new Vector2(0,1)&&growth.anchorMax==new Vector2(0,1)
                &&Vector2.Distance(growth.anchoredPosition,new Vector2(24,-60))<.5f
                &&Vector2.Distance(growth.rect.size,new Vector2(672,244))<.5f,"The enlarged information panel must occupy the upper garden area.");
            Require(home.title!=null&&home.title.transform.parent==growth&&home.stage!=null
                &&home.stage.gameObject.activeInHierarchy&&home.stage.transform.parent==growth,"Plant name and growth text must share the top panel.");
            HollowOutline(growth);
            Require(home.progressDots!=null&&home.progressDots.Length==4,"The four original appearance markers are missing.");
            Require(home.shopButton!=null&&home.shopButton.IsActive()&&home.shopButton.IsInteractable()
                &&home.shopButton.transform.parent==growth.parent,"The garden shop must be a separate bottom action.");
            var nameBounds=RectIn(growth,home.title.rectTransform);var stage=RectIn(growth,home.stage.rectTransform);
            Require(Contains(growth.rect,nameBounds)&&Contains(growth.rect,stage)&&!Overlap(nameBounds,stage),"Plant name and growth caption must fit on separate rows in the top panel.");
            Require(Mathf.Abs(nameBounds.center.x-growth.rect.center.x)<.5f&&Mathf.Abs(stage.center.x-growth.rect.center.x)<.5f,
                "Plant name and growth caption must be centered in their panel.");
            var occupied=new List<Rect>{nameBounds,stage};
            foreach(var dot in home.progressDots)
            {
                Require(dot!=null&&dot.gameObject.activeInHierarchy&&dot.transform.parent==growth&&dot.GetComponentInChildren<PrototypeUiIcon>()!=null,"Original appearance marker is not visible inside the card.");
                var bounds=RectIn(growth,(RectTransform)dot.transform);
                Require(Contains(growth.rect,bounds)&&occupied.All(other=>!Overlap(other,bounds)),"Appearance markers must fit beneath the caption without overlapping other information.");occupied.Add(bounds);
                var marker=dot.GetComponentInChildren<PrototypeUiIcon>().rectTransform;
                Require(Vector2.Distance(marker.rect.size,new Vector2(40,40))<.5f,"The enlarged panel must retain readable appearance markers.");
            }
            Require(!growth.GetComponentsInChildren<Transform>(false).Any(node=>node.name=="GardenHome.PlantStatusDetail"||node.name=="GardenHome.MemoryRecords"||node.name=="GardenHome.Album"),"Home-only record actions and detailed status were not removed.");
            var shopRect=(RectTransform)home.shopButton.transform;
            Require(f.Layout.upperBoundary==growth&&home.OverlayTopBoundary==growth&&f.Layout.lowerBoundary==shopRect,
                "Garden framing must use the top information panel and separate bottom shop.");
            var growthBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(content,growth);
            Require(growthBounds.center.y>((RectTransform)content).rect.center.y,"The plant information panel is not in the upper garden area.");
            var owner=(RectTransform)content;var panelBounds=RectIn(owner,growth);var shop=RectIn(owner,shopRect);
            var navigation=RectIn(owner,(RectTransform)home.NavigationBar.transform);
            Require(Contains(owner.rect,panelBounds)&&Contains(owner.rect,shop)&&!Overlap(panelBounds,shop)
                &&!Overlap(shop,navigation)&&shop.yMin>=navigation.yMax-.5f,"Panel and bottom shop must fit without covering the shared tabs.");
            Require(Mathf.Abs(shop.center.x-owner.rect.center.x)<.5f&&shopRect.anchorMin==new Vector2(.5f,0)
                &&shopRect.anchorMax==new Vector2(.5f,0)&&shopRect.pivot==new Vector2(.5f,0)
                &&Vector2.Distance(shopRect.rect.size,new Vector2(300,96))<.5f,"The bottom shop must be centered with its original usable touch height.");
            Require(shop.yMax<panelBounds.yMin&&f.Layout.VisibleAreaInViewport.height>0,"Top and bottom boundaries leave a usable garden composition area.");
            Require(!home.homeCanvas.GetComponentsInChildren<Button>(false).Any(b=>b.GetComponentsInChildren<Text>(false).Any(t=>t.text=="보기 +"||t.text=="보기 −"||t.text=="가까이 보기"||t.text=="정원 전체")),"Retired view controls remain visible.");
            foreach(var label in new[]{home.title,home.date,home.guide,home.stage})
            {
                Require(label!=null&&label.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(label.text),"Plant information is missing.");
                Require(!label.raycastTarget,"Plant information blocks garden gestures.");
                for(var parent=label.transform.parent;parent!=null&&parent!=home.homeCanvas.transform;parent=parent.parent)
                {
                    var image=parent.GetComponent<Image>();
                    Require(image==null||!image.enabled||image.color.a<.01f,"Plant information has an opaque image backdrop.");
                    var surface=parent.GetComponent<PrototypeUiSurface>();
                    Require(surface==null||!surface.enabled||(surface.topColor.a<.01f&&surface.bottomColor.a<.01f),"Plant information has an opaque surface backdrop.");
                }
            }
            Require(home.title.text==GardenHomePresenter.CurrentPlantName(home.RenderedRecipe)&&!home.title.text.StartsWith("오늘의",StringComparison.Ordinal),"Selected plant name is not shown without the retired heading prefix.");
            var locked=PlantCodec.Clone(home.RenderedRecipe);
            while(locked.SpeciesId==null)locked=PlantGenerator.Advance(locked);
            Require(GardenHomePresenter.CurrentPlantName(locked)==PlantCatalog.Get(locked.SpeciesId).Name,"A known selected species does not display its actual name.");
            string expectedDate=TimeZoneInfo.ConvertTime(f.Service.CurrentUtc,f.Service.CurrentTimeZone).ToString("M월 d일",System.Globalization.CultureInfo.InvariantCulture);
            Require(home.date.text==expectedDate,"The restored date does not match the app's effective local date.");
            Require(home.title.fontSize>home.date.fontSize&&home.title.rectTransform.parent==growth,"The plant name is not the primary panel label.");
            Require(home.title.fontSize>=72&&home.date.fontSize>=40&&home.stage.fontSize>=36,
                "The upper plant name, date and growth caption must retain their enlarged sizes.");
            var settingsButton=home.TemporarySettingsButton;
            Require(settingsButton!=null,"The garden settings entry disappeared.");
            var settingsLabels=settingsButton.GetComponentsInChildren<Text>(false)
                .Where(label=>label.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(label.text)).ToArray();
            Require(settingsLabels.Length==0,"The garden settings entry must not retain a visible settings label.");
            var settingsIcon=settingsButton.transform.Find("Settings gear icon")?.GetComponent<RawImage>();
            Require(settingsIcon!=null&&settingsIcon.isActiveAndEnabled&&settingsIcon.texture!=null&&!settingsIcon.raycastTarget
                &&!settingsButton.GetComponentsInChildren<PrototypeUiIcon>(false).Any(icon=>icon.symbol==PrototypeUiIcon.Symbol.Gear),
                "The settings entry must show its wood texture without retaining the old vector gear.");
            Require(Vector4.Distance(settingsIcon.color,Color.white)<.01f,"The wood texture must retain its original colors.");
            var settingsRect=(RectTransform)settingsButton.transform;var gearRect=settingsIcon.rectTransform;
            var gearBounds=RectIn(settingsRect,gearRect);
            Require(Vector2.Distance(settingsRect.rect.size,new Vector2(184,104))<.5f
                &&Vector2.Distance(gearRect.rect.size,new Vector2(88,88))<.5f
                &&Contains(settingsRect.rect,gearBounds)
                &&Mathf.Abs(gearBounds.center.x-(settingsRect.rect.xMin+136))<.5f
                &&Mathf.Abs(gearBounds.center.y-settingsRect.rect.center.y)<.5f,
                "The wood gear must occupy the former settings label position inside the preserved touch target.");
            var widestName=PlantCatalog.All.Select(species=>species.Name).Concat(new[]{"새 식물을 기다려요","저장 상태 확인 필요"})
                .OrderByDescending(value=>home.title.GetPreferredValues(value).x).First();
            SingleLineFits(home.title,widestName);
            SingleLineFits(home.stage,"다음 실습을 기다려요");
            SingleLineFits(home.date,home.date.text);
            var coinIcon=content.GetComponentsInChildren<PrototypeUiIcon>(true).Single(icon=>icon.name=="Garden coin balance icon");
            Require(home.guide.text=="37"&&coinIcon.symbol==PrototypeUiIcon.Symbol.Coin&&coinIcon.gameObject.activeSelf,
                "The preserved balance must show the coin icon followed by its number without a currency label.");
            Require(home.guide.fontSize<home.title.fontSize,"The coin count is not visually secondary.");
            Require(Mathf.Abs(home.guide.fontSize-40)<.1f&&Mathf.Abs(home.date.fontSize-home.guide.fontSize)<.1f
                &&coinIcon.rectTransform.rect.width>=54&&coinIcon.rectTransform.rect.height>=54,
                "The date and normal coin count must share the requested text size beside the enlarged coin icon.");
            var dateBounds=RectIn(owner,home.date.rectTransform);var coinBounds=RectIn(owner,home.guide.rectTransform);
            var coinIconBounds=RectIn(owner,coinIcon.rectTransform);
            Require(Mathf.Abs(dateBounds.height-64)<.5f&&Mathf.Abs(coinBounds.height-64)<.5f
                &&Mathf.Abs(dateBounds.center.y-coinBounds.center.y)<.5f
                &&Mathf.Abs(coinIconBounds.center.y-dateBounds.center.y)<.5f,
                "Date, coin number and coin icon must share one top-row midline.");
            Require(home.stage.text=="새싹","The restored stage caption no longer matches the synthetic legacy .1 growth state.");
            Require(home.NavigationBar!=null&&home.TemporarySettingsButton!=null&&home.TemporarySettingsButton.gameObject.activeInHierarchy,"Home navigation or temporary settings entry disappeared.");
            Require(home.OverlayTopBoundary!=null,"Transparent camera-framing boundary is missing.");
        }
        static void SingleLineFits(TMP_Text label,string value)
        {
            string original=label.text;
            try
            {
                label.text=value;label.ForceMeshUpdate(false,true);
                Require(!label.isTextOverflowing&&label.textInfo.lineCount==1,"Enlarged upper text must fit on one line: "+value);
            }
            finally{label.text=original;label.ForceMeshUpdate(false,true);}
        }
        static void HollowOutline(RectTransform panel)
        {
            var frame=panel.Find("Garden outline frame")?.GetComponent<MaskableGraphic>();
            Require(frame!=null&&frame.isActiveAndEnabled&&!frame.raycastTarget,"The visible outline must be decorative and leave the panel click target intact.");
            var mesh=new Mesh();
            try
            {
                using(var vertices=new VertexHelper())
                {
                    frame.GetType().GetMethod("OnPopulateMesh",BindingFlags.NonPublic|BindingFlags.Instance,
                        null,new[]{typeof(VertexHelper)},null).Invoke(frame,new object[]{vertices});vertices.FillMesh(mesh);
                }
                var rect=frame.rectTransform.rect;
                foreach(var point in new[]{rect.center,new Vector2(rect.xMin+24,rect.center.y),new Vector2(rect.xMax-24,rect.center.y),
                    new Vector2(rect.center.x,rect.yMin+24),new Vector2(rect.center.x,rect.yMax-24)})
                    Require(!CoveredBy(mesh,point),"The outline emits opaque triangles over the panel's transparent reading area.");
                foreach(var point in new[]{new Vector2(rect.xMin+1,rect.center.y),new Vector2(rect.xMax-1,rect.center.y),
                    new Vector2(rect.center.x,rect.yMin+1),new Vector2(rect.center.x,rect.yMax-1)})
                    Require(CoveredBy(mesh,point),"A visible perimeter is missing from the border-only panel.");
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static bool CoveredBy(Mesh mesh,Vector2 point)
        {
            var vertices=mesh.vertices;var colors=mesh.colors32;var triangles=mesh.triangles;
            float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                if(colors[a].a==0||colors[b].a==0||colors[c].a==0)continue;
                Vector2 va=vertices[a],vb=vertices[b],vc=vertices[c];
                if(Mathf.Abs(Cross(vb-va,vc-va))<.0001f)continue;
                float ab=Cross(vb-va,point-va),bc=Cross(vc-vb,point-vb),ca=Cross(va-vc,point-vc);
                if((ab>=-.001f&&bc>=-.001f&&ca>=-.001f)||(ab<=.001f&&bc<=.001f&&ca<=.001f))return true;
            }
            return false;
        }
        static void CoinBalances(Fixture f)
        {
            var home=f.Home;var content=(RectTransform)home.homeCanvas.transform.Find("SafePortraitFrame/MainContent");
            var icon=content.GetComponentsInChildren<PrototypeUiIcon>(true).Single(item=>item.name=="Garden coin balance icon");
            string before=StateCodec.Encode(f.Service.Snapshot);int writes=f.Store.Writes;
            string widestDate=Enumerable.Range(0,366).Select(day=>new DateTime(2024,1,1).AddDays(day)
                .ToString("M월 d일",System.Globalization.CultureInfo.InvariantCulture))
                .OrderByDescending(value=>home.date.GetPreferredValues(value).x).First();
            try
            {
                foreach(int coins in new[]{0,37,1234,int.MaxValue})
                {
                    var state=StateCodec.Clone(f.Service.Snapshot);state.Coins=coins;home.RefreshFromState(state);
                    home.date.text=widestDate;f.Draw();SingleLineFits(home.date,widestDate);
                    var label=home.guide;label.ForceMeshUpdate(false,true);
                    Require(label.text==coins.ToString("N0")&&!label.isTextOverflowing,"The balance is truncated or replaced: "+coins);
                    Require(label.fontSize>=24&&label.fontSize<=40.01f,"The coin count must stay legible while fitting large balances.");
                    if(coins<=1234)Require(Mathf.Abs(label.fontSize-home.date.fontSize)<.1f&&label.fontSize>=39.9f,
                        "Normal balances must retain the same requested text size as the date.");
                    var number=RectIn(content,label.rectTransform);var iconBounds=RectIn(content,icon.rectTransform);
                    float gap=number.xMin-iconBounds.xMax;
                    Require(gap>=7.5f&&gap<=8.5f,"Coin icon and number must have a tight eight-unit gap: "+gap);
                    var ink=label.textBounds;float inkLeft=content.InverseTransformPoint(label.rectTransform.TransformPoint(ink.min)).x;
                    Require(inkLeft-iconBounds.xMax>=6&&inkLeft-iconBounds.xMax<=18,"Rendered coin digits leave an excessive or overlapping icon gap.");
                    Require(Contains(content.rect,iconBounds)&&Contains(content.rect,number)&&!Overlap(iconBounds,number),"Coin group leaves the safe home frame.");
                    var date=RectIn(content,home.date.rectTransform);var settings=RectIn(content,(RectTransform)home.TemporarySettingsButton.transform);
                    Require(Mathf.Abs(date.center.y-number.center.y)<.5f&&Mathf.Abs(date.center.y-iconBounds.center.y)<.5f,
                        "Changing the balance must not move its number or coin away from the date midline.");
                    Require(!Overlap(date,iconBounds)&&!Overlap(date,number)&&!Overlap(settings,iconBounds)&&!Overlap(settings,number),
                        "A large balance covers the date or settings touch target.");
                }
                home.RefreshFromState(null);f.Draw();
                SingleLineFits(home.title,home.title.text);
                Require(home.guide.transform.parent==home.OverlayTopBoundary&&!home.guide.isTextOverflowing
                    &&Contains(home.OverlayTopBoundary.rect,RectIn(home.OverlayTopBoundary,home.guide.rectTransform)),
                    "Unavailable saved state must display its readable explanation inside the information panel.");
                Require(!icon.gameObject.activeSelf&&home.progressDots.All(dot=>dot==null||!dot.gameObject.activeSelf),
                    "The unavailable-state explanation must not overlap wallet or growth icons.");
            }
            finally{home.RefreshFromState(f.Service.Snapshot);f.Draw();}
            Require(home.guide.transform.parent==content&&icon.gameObject.activeSelf
                &&home.progressDots.All(dot=>dot!=null&&dot.gameObject.activeSelf),"Restoring saved state must restore wallet placement and growth icons.");
            Require(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes,"Rendering synthetic balances wrote wallet or plant state.");
        }
        static void Visibility(Fixture f)
        {
            var home=f.Home;var panel=home.OverlayTopBoundary;var shop=home.shopButton;
            var navigation=home.NavigationBar;string before=StateCodec.Encode(f.Service.Snapshot);int writes=f.Store.Writes;
            var angles=f.Layout.orbit.Angles;float zoom=f.Layout.Zoom;
            try
            {
                home.Show(false);
                Require(!panel.gameObject.activeInHierarchy&&!shop.gameObject.activeInHierarchy,"Garden panel or bottom shop leaks onto another page.");
                Require(home.NavigationBar==navigation,"Hiding the garden replaced the shared navigation.");
            }
            finally{home.Show(true);f.Draw();}
            Require(panel.gameObject.activeInHierarchy&&shop.IsActive()&&home.NavigationBar==navigation,"Returning home did not restore its panel and shop with the shared tabs.");
            Require(f.Layout.orbit.Angles==angles&&Mathf.Approximately(f.Layout.Zoom,zoom),"Leaving and returning home changed the garden view.");
            Require(StateCodec.Encode(f.Service.Snapshot)==before&&f.Store.Writes==writes,"Garden visibility changed saved state.");
        }
        static void DayAndNight(Fixture f,Report report,bool capture)
        {
            var gear=f.Home.TemporarySettingsButton.transform.Find("Settings gear icon").GetComponent<RawImage>();
            foreach(var hour in new[]{0d,12d})
            {
                f.Sky.SetTimeForPreview(hour);f.Home.RefreshOverlayTextColor();
                Color expected=hour==0?Color.white:Color.black;
                foreach(var label in new[]{f.Home.title,f.Home.date,f.Home.guide,f.Home.stage})
                    Require(Vector4.Distance(label.color,expected)<.01f,"Overlay text has the wrong color at hour "+hour);
                Require(Vector4.Distance(gear.color,Color.white)<.01f,
                    "The settings gear must preserve its wood artwork during day/night text recoloring.");
                Require(!f.Home.TemporarySettingsButton.GetComponentsInChildren<Text>(false)
                    .Any(label=>label.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(label.text)),
                    "Day/night recoloring restored the retired settings text.");
                f.Capture(hour==0?"01-garden-night":"02-garden-day",report,capture);
            }
            Require(GardenHomePresenter.OverlayTextColor(.49f)==Color.white&&GardenHomePresenter.OverlayTextColor(.5f)==Color.black,"Day/night overlay threshold is inconsistent.");
            var before=f.Service.CurrentUtc;
            Require(f.Service.AdvancePreviewDay(),"Synthetic mission date advance failed.");
            f.Home.RefreshFromState(f.Service.Snapshot);f.Home.RefreshOverlayTextColor();
            string nextDate=TimeZoneInfo.ConvertTime(before.AddDays(1),f.Service.CurrentTimeZone).ToString("M월 d일",System.Globalization.CultureInfo.InvariantCulture);
            Require(f.Home.date.text==nextDate,"Advancing the app day did not update the restored garden date.");
            f.Capture("05-garden-next-date",report,capture);
        }
        static void Zoom(Fixture f,Report report,bool capture)
        {
            var orbit=f.Layout.orbit;orbit.zoomLayout=f.Layout;
            string stateBefore=StateCodec.Encode(f.Service.Snapshot);
            f.Layout.SetZoom(.85f);f.Layout.Apply();float initial=f.Home.gardenCamera.orthographicSize;var angles=orbit.Angles;
            var middle=new Vector2(f.Size.x*.5f,f.Size.y*.48f);
            orbit.OnScroll(f.Pointer(1,middle,new Vector2(0,1)));
            Require(f.Layout.Zoom>.85f&&f.Home.gardenCamera.orthographicSize<initial,"Mouse wheel up did not visibly zoom in.");
            orbit.OnScroll(f.Pointer(1,middle,new Vector2(0,-1)));
            Require(Mathf.Abs(f.Layout.Zoom-.85f)<.001f,"Opposite wheel movement did not restore zoom.");
            var first=f.Pointer(1,middle+Vector2.left*100);var second=f.Pointer(2,middle+Vector2.right*100);
            orbit.OnPointerDown(first);orbit.OnPointerDown(second);
            Require(orbit.IsPinching,"Two garden touches did not begin a pinch.");
            second.position=middle+Vector2.right*170;orbit.OnDrag(second);
            Require(f.Layout.Zoom>.85f&&f.Home.gardenCamera.orthographicSize<initial,"Spreading two fingers did not zoom in.");
            float spread=f.Layout.Zoom;second.position=middle+Vector2.right*40;orbit.OnDrag(second);
            Require(f.Layout.Zoom<spread,"Closing two fingers did not zoom out.");
            Require(orbit.Angles==angles,"Pinch unexpectedly rotated the garden.");
            orbit.OnPointerUp(second);first.position+=new Vector2(2,1);orbit.OnDrag(first);
            Require(orbit.Angles==angles,"Returning to one finger caused an orbit jump.");
            first.position+=new Vector2(20,4);orbit.OnDrag(first);
            Require((orbit.Angles-angles).sqrMagnitude>.0001f,"Single-finger orbit did not resume after a pinch.");
            orbit.OnPointerUp(first);
            Require(!orbit.IsPinching,"Pinch remained latched after touch release.");
            Require(PrototypeIslandDrag.ZoomFromWheel(1,999)==1&&PrototypeIslandDrag.ZoomFromWheel(0,-999)==0,"Wheel zoom is not bounded.");
            Require(PrototypeIslandDrag.ZoomFromPinch(.5f,0,100)==.5f,"A zero-distance pinch changed zoom.");
            Require(StateCodec.Encode(f.Service.Snapshot)==stateBefore,"View gestures modified saved garden or mission state.");
            f.Layout.SetZoom(0);f.Capture("03-garden-wide",report,capture);
            f.Layout.SetZoom(1);f.Capture("04-garden-close",report,capture);
            f.Layout.SetZoom(.85f);
        }
        static void UiInterception(Fixture f)
        {
            f.Draw();
            foreach(var button in new[]{f.Home.NavigationBar.MissionButton,f.Home.shopButton,f.Home.OverlayTopBoundary.GetComponent<Button>(),f.Home.TemporarySettingsButton})
            {
                var rect=(RectTransform)button.transform;
                Vector2 point=RectTransformUtility.WorldToScreenPoint(f.Camera,rect.TransformPoint(rect.rect.center));
                var pointer=f.Pointer(1,point,new Vector2(0,1));var hits=new List<RaycastResult>();f.Raycaster.Raycast(pointer,hits);
                var hit=hits.OrderByDescending(h=>h.depth).FirstOrDefault();
                Require(hit.gameObject!=null&&hit.gameObject.GetComponentInParent<Button>()==button,
                    "Visible control did not receive the leading UI hit: "+button.name+"; "+RaycastDiagnostics(f,button,point,hits));
                float previous=f.Layout.Zoom;ExecuteEvents.ExecuteHierarchy(hit.gameObject,pointer,ExecuteEvents.scrollHandler);
                Require(f.Layout.Zoom==previous,"Scrolling over a garden UI control zoomed the scene: "+button.name);
            }
            var settingsButton=f.Home.TemporarySettingsButton;var settingsRect=(RectTransform)settingsButton.transform;
            var gearRect=settingsButton.transform.Find("Settings gear icon").GetComponent<RectTransform>();
            var gear=RectIn(settingsRect,gearRect);var bounds=settingsRect.rect;
            var touchPoints=new[]{gear.center,new Vector2(bounds.xMin+52,bounds.center.y),new Vector2(bounds.xMin+3,bounds.center.y),
                new Vector2(bounds.xMax-3,bounds.center.y),new Vector2(bounds.center.x,bounds.yMin+3),new Vector2(bounds.center.x,bounds.yMax-3)};
            string saved=StateCodec.Encode(f.Service.Snapshot);int writes=f.Store.Writes;
            foreach(var localPoint in touchPoints)
            {
                try
                {
                    f.Draw();
                    Vector2 point=RectTransformUtility.WorldToScreenPoint(f.Camera,settingsRect.TransformPoint(localPoint));
                    var pointer=f.Pointer(1,point);var hits=new List<RaycastResult>();f.Raycast(pointer,hits);
                    var hit=hits.FirstOrDefault();
                    Require(hit.gameObject!=null&&hit.gameObject.GetComponentInParent<Button>()==settingsButton,
                        "The moved settings icon, former icon position or padding is not clickable: "+localPoint+"; "+RaycastDiagnostics(f,settingsButton,point,hits));
                    pointer.pointerCurrentRaycast=pointer.pointerPressRaycast=hit;
                    ExecuteEvents.ExecuteHierarchy(hit.gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    Require(f.Home.navigation.settingsPanel.activeSelf,"Clicking the moved settings icon or preserved touch area did not open settings.");
                }
                finally{f.Home.navigation.ShowIsland();f.Draw();}
            }
            Require(StateCodec.Encode(f.Service.Snapshot)==saved&&f.Store.Writes==writes,"Settings hit testing modified stored garden data.");
        }
        static void SettingsNavigation(Fixture f)
        {
            var home=f.Home;var navigation=home.NavigationBar;
            var registered=new List<KeyValuePair<Canvas,Graphic>>();
            string saved=StateCodec.Encode(f.Service.Snapshot);int writes=f.Store.Writes;
            try
            {
                home.TemporarySettingsButton.onClick.Invoke();
                Require(home.navigation.settingsPanel.activeSelf,"The gear button did not open settings.");
                var settings=home.navigation.GetComponentInChildren<SensorRawDisplay>(true);
                Require(settings!=null,"Actual scene has no settings presenter.");
                settings.PrepareView();home.RefreshMissionStatus();f.Draw();
                Require(FigmaMissionUi.Find<Button>(settings.transform,"Settings.Back")==null,
                    "Settings still contains the redundant garden return button.");
                Require(navigation.ActiveTab==null&&!navigation.HomeButton.GetComponentInChildren<GardenThemeSurface>(false).SageAppearance,
                    "Home remains selected while settings is open.");
                foreach(var graphic in home.navigation.GetComponentsInChildren<Graphic>(false))
                {
                    GraphicRegistry.RegisterGraphicForCanvas(graphic.canvas,graphic);
                    registered.Add(new KeyValuePair<Canvas,Graphic>(graphic.canvas,graphic));
                    graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);
                }
                Canvas.ForceUpdateCanvases();f.Camera.Render();
                var rect=(RectTransform)navigation.HomeButton.transform;
                Vector2 point=RectTransformUtility.WorldToScreenPoint(f.Camera,rect.TransformPoint(rect.rect.center));
                var hits=new List<RaycastResult>();
                // MockupMain keeps IntegratedHomeCanvas as a separate scene
                // root from UiCanvas. Both participate in the same UI hit test.
                foreach(var caster in home.navigation.GetComponentsInChildren<GraphicRaycaster>(false)
                    .Concat(home.homeCanvas.GetComponentsInChildren<GraphicRaycaster>(false)).Distinct())
                    if(caster.IsActive())caster.Raycast(f.Pointer(1,point),hits);
                var compare=typeof(EventSystem).GetField("s_RaycastComparer",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) as Comparison<RaycastResult>;
                Require(compare!=null,"The installed uGUI raycast ordering is unavailable.");hits.Sort(compare);
                var hit=hits.FirstOrDefault();
                Require(hit.gameObject!=null&&hit.gameObject.GetComponentInParent<Button>()==navigation.HomeButton,
                    "Actual settings blocks the home tab; "+RaycastDiagnostics(f,navigation.HomeButton,point,hits));
                navigation.HomeButton.onClick.Invoke();f.Draw();
                Require(!home.navigation.settingsPanel.activeSelf&&navigation.ActiveTab=="홈"
                    &&navigation.HomeButton.GetComponentInChildren<GardenThemeSurface>(false).SageAppearance,
                    "Home tab did not restore the garden and its light selected surface.");
                Require(StateCodec.Encode(f.Service.Snapshot)==saved&&f.Store.Writes==writes,
                    "Settings navigation modified stored garden data.");
            }
            finally
            {
                foreach(var entry in registered)GraphicRegistry.UnregisterGraphicForCanvas(entry.Key,entry.Value);
                home.navigation.ShowIsland();f.Draw();
            }
        }
        static string RaycastDiagnostics(Fixture f,Button expected,Vector2 point,List<RaycastResult> hits)
        {
            string PathOf(Transform node)=>node==null?"<none>":string.Join("/",node.GetComponentsInParent<Transform>(true).Reverse().Select(item=>item.name));
            string CameraOf(Camera camera)=>camera==null?"<none>":camera.name+" pixelRect="+camera.pixelRect
                +" target="+(camera.targetTexture==null?"screen":camera.targetTexture.width+"x"+camera.targetTexture.height)
                +" enabled="+camera.enabled+" scene="+camera.scene.name;
            string CanvasOf(Canvas canvas)=>canvas==null?"<none>":PathOf(canvas.transform)+" mode="+canvas.renderMode
                +" enabled="+canvas.enabled+" active="+canvas.gameObject.activeInHierarchy+" order="+canvas.sortingOrder
                +" display="+canvas.targetDisplay+" camera="+CameraOf(canvas.worldCamera);
            var registered=GraphicRegistry.GetRaycastableGraphicsForCanvas(f.Home.homeCanvas);
            string graphics=string.Join(" | ",expected.GetComponentsInChildren<Graphic>(true).Select(graphic=>
                PathOf(graphic.transform)+" active="+graphic.isActiveAndEnabled+" raycast="+graphic.raycastTarget
                +" depth="+graphic.depth+" culled="+graphic.canvasRenderer.cull+" registered="+registered.Contains(graphic)
                +" ownsHomeCanvas="+(graphic.canvas==f.Home.homeCanvas)
                +" contains="+RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform,point,f.Raycaster.eventCamera)
                +" filtersAccept="+graphic.Raycast(point,f.Raycaster.eventCamera)));
            string result=string.Join(" | ",hits.Take(12).Select(hit=>PathOf(hit.gameObject==null?null:hit.gameObject.transform)
                +" depth="+hit.depth+" distance="+hit.distance+" sorting="+hit.sortingOrder
                +" module="+(hit.module==null?"<none>":PathOf(hit.module.transform))));
            return "point="+point+" targetSize="+f.Size+" screen="+Screen.width+"x"+Screen.height
                +" hitCount="+hits.Count+" hits=["+result+"] expectedGraphics=["+graphics+"] registeredCount="+registered.Count
                +" raycaster="+PathOf(f.Raycaster.transform)+" enabled="+f.Raycaster.isActiveAndEnabled
                +" eventCamera="+CameraOf(f.Raycaster.eventCamera)+" homeCanvas="+CanvasOf(f.Home.homeCanvas);
        }

        sealed class Clock:IClock
        {
            public DateTimeOffset UtcNow=>DateTimeOffset.Parse("2026-10-29T03:00:00Z");
            public TimeZoneInfo TimeZone=>TimeZoneInfo.Utc;
        }
        sealed class Store:IStateStore
        {
            GardenState value;
            public int Writes {get;private set;}
            public GardenState Load()=>value==null?null:StateCodec.Clone(value);
            public void Save(GardenState state){value=StateCodec.Clone(state);Writes++;}
        }
        sealed class Fixture:IDisposable
        {
            public readonly GardenHomePresenter Home;
            public readonly GardenStateService Service;
            public readonly Store Store=new Store();
            public readonly GardenState State;
            public readonly GardenSkyEnvironment Sky;
            public readonly PrototypeGardenPreviewLayout Layout;
            public readonly Camera Camera;
            public readonly GraphicRaycaster Raycaster;
            public readonly Vector2Int Size;
            readonly Scene scene;
            readonly Scene originalScene;
            readonly EventSystem pointerContext;
            readonly RenderTexture target;
            readonly Texture2D texture;
            readonly AmbientMode ambientMode;
            readonly Color ambientLight,ambientSky,ambientEquator,ambientGround;
            readonly float reflection;
            readonly Material skybox;
            public Fixture(Vector2Int size)
            {
                Size=size;
                originalScene=SceneManager.GetActiveScene();ambientMode=RenderSettings.ambientMode;ambientLight=RenderSettings.ambientLight;
                ambientSky=RenderSettings.ambientSkyColor;ambientEquator=RenderSettings.ambientEquatorColor;ambientGround=RenderSettings.ambientGroundColor;
                reflection=RenderSettings.reflectionIntensity;skybox=RenderSettings.skybox;
                scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/MockupMain.unity");
                try
                {
                    Home=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GardenHomePresenter>(true)).Single();
                    Require(Home.activity.Service==null,"Preview scene unexpectedly initialized a persistent app service.");
                    Service=new GardenStateService(Store,new Clock(),new DemoBalanceConfig{VisitorChancePercent=0},new LegacySnapshot{Nutrient=37,Growth=.1f});
                    Require(Service.Open(),"Synthetic memory state did not open.");
                    SetField(Home.activity,"<Service>k__BackingField",Service);SetField(Home.activity,"opened",true);SetField(Home.activity,"preview",true);SetField(Home.activity,"mode","MOCK");
                    Camera=Home.oldCamera;Camera.enabled=false;Camera.scene=scene;Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=Color.black;
                    target=new RenderTexture(Size.x,Size.y,24);texture=new Texture2D(Size.x,Size.y,TextureFormat.RGBA32,false);
                    target.Create();Camera.targetTexture=target;Camera.aspect=(float)Size.x/Size.y;
                    var canvas=Home.GetComponent<Canvas>();canvas.renderMode=Home.homeCanvas.renderMode=RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera=Home.homeCanvas.worldCamera=Camera;canvas.planeDistance=1;Home.homeCanvas.planeDistance=.75f;
                    Home.gardenCamera.scene=scene;
                    // Preview scenes retain the serialized root Canvas's zero scale
                    // until a camera rebuild. Runtime buttons preserve their centre
                    // in world space, so create them only after that transform exists.
                    // A later Fit cannot recover coordinates lost under a zero matrix.
                    for(int pass=0;pass<3;pass++)
                    {
                        Canvas.ForceUpdateCanvases();Camera.Render();
                        foreach(var fit in Home.homeCanvas.GetComponentsInChildren<PrototypePortraitLayout>(true))fit.Apply();
                    }
                    var canvasRect=(RectTransform)Home.homeCanvas.transform;
                    Require(canvasRect.rect.width>0&&canvasRect.rect.height>0&&Mathf.Abs(canvasRect.lossyScale.x)>1e-7f
                        &&Mathf.Abs(canvasRect.lossyScale.y)>1e-7f,"Preview canvas must have a usable world transform before runtime controls are constructed.");
                    Home.navigation.ShowIsland();State=Service.Snapshot;Home.RefreshFromState(State);
                    Layout=Home.homeCanvas.GetComponentInChildren<PrototypeGardenPreviewLayout>(true);
                    Require(Layout!=null&&Layout.orbit!=null,"Actual scene is missing its garden input surface.");
                    Sky=GardenSkyEnvironment.Ensure(Home.gardenCamera,Home.gardenVisuals.transform);
                    Raycaster=Home.homeCanvas.GetComponent<GraphicRaycaster>()??Home.homeCanvas.gameObject.AddComponent<GraphicRaycaster>();
                    var pointer=new GameObject("Isolated overlay pointer");pointer.SetActive(false);SceneManager.MoveGameObjectToScene(pointer,scene);
                    pointerContext=pointer.AddComponent<EventSystem>();pointerContext.enabled=false;
                    Draw();
                }
                catch{Dispose();throw;}
            }
            static void SetField(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
            public PointerEventData Pointer(int id,Vector2 point,Vector2 scroll=default)
            {
                return new PointerEventData(pointerContext){pointerId=id,button=PointerEventData.InputButton.Left,position=point,pressPosition=point,scrollDelta=scroll,
                    pointerPressRaycast=new RaycastResult{gameObject=Layout.orbit.gameObject,module=Raycaster,screenPosition=point},
                    pointerCurrentRaycast=new RaycastResult{gameObject=Layout.orbit.gameObject,module=Raycaster,screenPosition=point}};
            }
            public void Raycast(PointerEventData pointer,List<RaycastResult> hits)
            {
                // Preview scenes can skip the normal raycaster OnEnable registration.
                // Register only this fixture's missing casters for the real EventSystem
                // query, then restore the registry exactly to its prior membership.
                var registry=RaycasterManager.GetRaycasters();
                var added=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<GraphicRaycaster>(false))
                    .Where(caster=>caster.IsActive()&&!registry.Contains(caster)).Distinct().ToArray();
                try
                {
                    foreach(var caster in added)registry.Add(caster);
                    pointerContext.RaycastAll(pointer,hits);
                    // Preserve EventSystem sorting while excluding unrelated editor scenes.
                    hits.RemoveAll(hit=>hit.gameObject==null||hit.gameObject.scene!=scene);
                }
                finally{foreach(var caster in added)registry.Remove(caster);}
            }
            public void Draw()
            {
                // Resolve safe-area and Figma frames before rebuilding child meshes,
                // matching the settling passes used by the actual-scene fixture.
                for(int pass=0;pass<3;pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach(var fit in Home.homeCanvas.GetComponentsInChildren<PrototypePortraitLayout>(true))fit.Apply();
                    foreach(var fit in Home.homeCanvas.GetComponentsInChildren<FigmaMissionCanvasFit>(true))fit.Fit();
                    Canvas.ForceUpdateCanvases();
                    foreach(var graphic in Home.homeCanvas.GetComponentsInChildren<Graphic>(false))
                    {
                        GraphicRegistry.RegisterGraphicForCanvas(graphic.canvas,graphic);
                        graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);
                    }
                    foreach(var label in Home.homeCanvas.GetComponentsInChildren<TMP_Text>(false))label.ForceMeshUpdate(false,true);
                    Canvas.ForceUpdateCanvases();
                }
                Layout.Apply();Sky.UpdateClouds(0);Home.gardenCamera.Render();Canvas.ForceUpdateCanvases();Camera.Render();
            }
            public void Capture(string name,Report report,bool enabled)
            {
                if(!enabled)return;
                for(int pass=0;pass<4;pass++)Draw();
                var previous=RenderTexture.active;
                try{RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,Size.x,Size.y),0,0);texture.Apply();}
                finally{RenderTexture.active=previous;}
                var pixels=texture.GetPixels32();int min=765,max=0;
                foreach(var pixel in pixels){int value=pixel.r+pixel.g+pixel.b;min=Math.Min(min,value);max=Math.Max(max,value);}
                Require(max-min>=90,"Actual garden preview is blank or a solid frame: "+name);
                string path=Path.Combine(OutputRoot,name+"-"+Size.x+"x"+Size.y+".png");File.WriteAllBytes(path,texture.EncodeToPNG());report.captures.Add(path);
            }
            public void Dispose()
            {
                if(Home!=null&&Home.homeCanvas!=null)
                    foreach(var graphic in Home.homeCanvas.GetComponentsInChildren<Graphic>(true))GraphicRegistry.UnregisterGraphicForCanvas(graphic.canvas,graphic);
                if(Camera!=null)Camera.targetTexture=null;
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
                Service?.Dispose();
                if(originalScene.IsValid()&&originalScene.isLoaded)SceneManager.SetActiveScene(originalScene);
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambientLight;RenderSettings.ambientSkyColor=ambientSky;
                RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
                RenderSettings.reflectionIntensity=reflection;RenderSettings.skybox=skybox;
            }
        }
    }
}
