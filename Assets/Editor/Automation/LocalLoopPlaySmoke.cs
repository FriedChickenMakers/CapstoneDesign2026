using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.EditorTools
{
    [InitializeOnLoad]
    public static class LocalLoopPlaySmoke
    {
        const string Key="Capstone.LocalLoopSmoke.Active";
        [Serializable] sealed class Result
        {
            public string status,phase,error;
            public string mode="MOCK",environment="Unity Editor Play mode";
        }
        static int raycastFrame;
        static LocalLoopPlaySmoke()
        {
            if(SessionState.GetBool(Key,false))EditorApplication.update+=WaitForPlay;
        }
        public static void Start()
        {
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT")))throw new Exception("Explicit isolated smoke save directory required");
            EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
            SessionState.SetBool(Key,true);
            SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+60);
            EditorApplication.update-=WaitForPlay;EditorApplication.update+=WaitForPlay;
            EditorApplication.EnterPlaymode();
        }
        static void WaitForPlay()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))
            {
                Finish(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT"),
                    Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_PHASE")??"fresh",new Exception("Play smoke startup timed out"));return;
            }
            if(!EditorApplication.isPlaying)return;
            var loop=UnityEngine.Object.FindFirstObjectByType<WeekOneQuestDemo>(FindObjectsInactive.Include);
            if(loop?.Service==null)return;
            Application.runInBackground=true;
            SessionState.SetFloat(Key+".Deadline",(float)EditorApplication.timeSinceStartup+
                (Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_PHASE")=="chrome"?30:90));
            EditorApplication.update-=WaitForPlay;
            EditorApplication.update-=WatchRun;EditorApplication.update+=WatchRun;
            Run();
        }
        static void WatchRun()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".Deadline",0))
                Finish(Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT"),
                    Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_PHASE")??"fresh",new Exception("Play smoke runtime deadline exceeded"));
        }
        static void Run()
        {
            string root=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT");
            string phase=Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_PHASE") ?? "fresh";
            UnityEngine.Object.FindFirstObjectByType<MockupNavigation>().StartCoroutine(RunGuarded(root,phase));
        }
        static IEnumerator RunGuarded(string root,string phase)
        {
            var steps=RunSteps(root,phase);
            while(true)
            {
                bool moved=false;object current=null;Exception failure=null;
                try { moved=steps.MoveNext();if(moved)current=steps.Current; }
                catch(Exception ex) { failure=ex; }
                if(failure!=null)
                {
                    Finish(root,phase,failure);yield break;
                }
                if(!moved)yield break;
                yield return current;
            }
        }
        static IEnumerator RunSteps(string root,string phase)
        {
            var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>();
            if(nav==null)throw new Exception("Serialized navigation missing in Play mode");
            if(AndroidPlatformBridge.InputMode!=PlatformInputMode.Mock)throw new Exception("Smoke must remain MOCK");
            nav.ShowActivities();
            var ui=nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();
            if(ui?.Service==null)throw new Exception("Awake did not initialize local state");
            var home=nav.home;
            if(home==null || home.gardenCamera==null || home.gardenCamera.enabled)
                throw new Exception("Integrated home or on-demand garden camera missing in Play mode");
            if(phase=="chrome")
            {
                // Batch mode has no normal Game view overlay draw. This dedicated
                // phase must observe the real player frame, without the preview
                // helper's forced mesh submissions hiding the device regression.
                if(Application.isBatchMode)throw new Exception("The chrome phase requires a graphical Editor without -batchmode.");
                var gameView=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
                gameView.Show();gameView.Focus();
                if(!ui.Service.SelectMbctProgram("8w"))throw new Exception(ui.Service.LastError);
                var pages=new Action[]{ui.ShowHome,ui.ShowCourse,ui.ShowFree,ui.ShowProgramSelection,ui.ShowHome};
                var names=new[]{"activities","course","free","programs","activities-return"};
                nav.ShowActivities();
                for(int index=0;index<pages.Length;index++)
                {
                    pages[index]();
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    var graphics=CurrentChromeGraphics(ui);
                    var visible=ScreenCapture.CaptureScreenshotAsTexture();
                    Texture2D hidden=null;
                    try
                    {
                        foreach(var graphic in graphics)graphic.enabled=false;
                        yield return null;
                        yield return new WaitForEndOfFrame();
                        hidden=ScreenCapture.CaptureScreenshotAsTexture();
                        AssertSharedChromePixels(graphics,visible,hidden,Path.Combine(root,"shared-chrome-"+names[index]+".png"));
                    }
                    finally
                    {
                        foreach(var graphic in graphics)if(graphic!=null)graphic.enabled=true;
                        if(visible!=null)UnityEngine.Object.Destroy(visible);
                        if(hidden!=null)UnityEngine.Object.Destroy(hidden);
                    }
                }
            }
            else if(phase=="fresh")
            {
                if(ui.Service.Snapshot.Sessions.Count!=0)throw new Exception("Fresh fixture directory already contains sessions");
                nav.ShowIsland();
                AssertLegacyRendererParked(home);
                if((home.ProceduralPlant==null || home.RenderedRecipe?.GrowthStep!=0) || !home.homeCanvas.gameObject.activeSelf)
                    throw new Exception("Fresh home did not show the saved seed");
                yield return null;
                if(home.ViewControlsExpanded || home.homeCanvas.GetComponentsInChildren<Button>()
                    .Any(button=>button.name=="보기 +" || button.name=="가까이 보기" || button.name=="확대"))
                    throw new Exception("Retired view buttons overlay the gesture-based garden");
                if(!home.IsOverview)throw new Exception("Home did not default to the complete garden");
                var gardenContext=GardenProceduralValidation.CaptureGardenContext(home);
                if(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Any(node=>node.name=="Saved garden additions" && node.parent==null))
                    throw new Exception("Legacy saved scenery leaks into the integrated garden camera");
                string stateBeforeView=StateCodec.Encode(ui.Service.Snapshot);
                float initialZoom=home.Zoom;
                ScrollGarden(home,-1);
                if(home.Zoom>=initialZoom)throw new Exception("Garden wheel gesture did not zoom out");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                ScrollGarden(home,1);
                if(Mathf.Abs(home.Zoom-initialZoom)>.001f || home.RenderedRecipe.GrowthStep!=0)
                    throw new Exception("Opposite garden gestures failed to restore the view or changed plant growth");
                ScrollGarden(home,-1);
                float previousZoom=home.Zoom;
                if(StateCodec.Encode(ui.Service.Snapshot)!=stateBeforeView)
                    throw new Exception("Garden view gestures changed saved mission or garden state");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                AssertRaycastTarget(home.activityTab);
                home.activityTab.onClick.Invoke();
                AssertLegacyRendererParked(home);
                if(!nav.activitiesPanel.activeSelf || home.gardenVisuals.activeSelf)
                    throw new Exception("Activity tab did not park the garden camera");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext,requireActive:false);
                if (ui.CurrentScreen != "focus-programs" || ui.Service.Snapshot.MbctProgramChosen)
                    throw new Exception("Fresh activity tab bypassed course duration selection");
                yield return null;
                var duration = ui.GetComponentsInChildren<Button>().Single(button=>button.name=="Focus.SelectProgram.8w");
                ScrollIntoView(ui.GetComponentInChildren<ScrollRect>(),duration);
                AssertRaycastTarget(duration); duration.onClick.Invoke();
                yield return null;
                var confirmDuration = ui.GetComponentsInChildren<Button>().Single(button=>button.name=="Focus.ConfirmProgram.8w");
                ScrollIntoView(ui.GetComponentInChildren<ScrollRect>(),confirmDuration);
                AssertRaycastTarget(confirmDuration); confirmDuration.onClick.Invoke();
                yield return null;
                if (!ui.Service.Snapshot.MbctProgramChosen || MbctPolicy.ActiveProgram(ui.Service.Snapshot) != "8w")
                    throw new Exception("Course confirmation did not persist the selected duration");
                AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="기본 미션 시작하기"));
                nav.ShowIsland();
                if(!home.IsOverview || Mathf.Abs(home.Zoom-previousZoom)>.001f)
                    throw new Exception("Tab return lost the complete garden or selected gesture zoom");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                yield return null;
                AssertRaycastTarget(home.TemporarySettingsButton);
                home.TemporarySettingsButton.onClick.Invoke();
                AssertLegacyRendererParked(home);
                if(!nav.settingsPanel.activeSelf || home.IsGardenVisible || !home.NavigationBar.gameObject.activeInHierarchy)
                    throw new Exception("Home settings entry did not open settings with shared navigation");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext,requireActive:false);
                var settings = nav.GetComponentInChildren<SensorRawDisplay>(true);
                var heartChart = settings.GetComponentsInChildren<Button>().Single(button=>button.name=="심박 차트 보기");
                // Settings is a scrolling page. The batch Game view can be much
                // shorter than a device, so reveal this control before proving
                // that shared home navigation does not intercept its tap.
                yield return null;
                ScrollIntoView(settings.PrimaryScroll, heartChart);
                yield return null;
                AssertRaycastTarget(heartChart);
                nav.ShowActivities();
                // Five nutrients of synthetic legacy participation fund the ten-nutrient garden check.
                if(!ui.Service.BeginSession("synthetic-starter","free-mission:M01",0) || !ui.Service.StartSession("synthetic-starter") || !ui.Service.CompleteSession("synthetic-starter"))throw new Exception(ui.Service.LastError);
                if(ui.Service.Snapshot.Nutrient!=5)throw new Exception("Default reward must be five");
                ui.SelectMission(MbctContent.Course[0]);
                // New CanvasRenderers acquire raycast depth after a rendered frame.
                yield return null;
                AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="시작"));
                AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="선택 취소"));
                string id=ui.Service.Snapshot.Sessions.Last().SessionId;
                if(!ui.Service.StartSession(id))throw new Exception(ui.Service.LastError);
                ui.ShowSession();
                yield return null;
                var activityScroll=ui.GetComponentsInChildren<ScrollRect>().Single(scroll=>scroll.name=="Local garden flow");
                activityScroll.verticalNormalizedPosition=0;
                AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="다음"));
                AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="잠시 쉬기"));
                activityScroll.verticalNormalizedPosition=1;
                ui.GetComponentsInChildren<InputField>(true).Single(f=>f.gameObject.activeInHierarchy).text="물";
                for(int step=0;step<4;step++)ui.AdvancePractice(false);
                ui.Finish(null,null);
                if(!ui.Service.SaveExperience("play-record","즐거움","바깥 소리","어깨가 내려감","차분해요","소리가 들린다") || !ui.Service.SavePreference("support","치료진"))throw new Exception(ui.Service.LastError);
                if(ui.Service.Snapshot.Nutrient!=10 || ui.Service.Snapshot.MbctNextOrder!=2)throw new Exception("Runtime completion mismatch");
                LocalLoopValidation.ShowLegacy(ui,"Home");
                yield return null;
                AssertPageStartsAtTop(ui,"오늘의 활동");
                ui.ShowGardenJournal();
                yield return null;
                AssertPageStartsAtTop(ui,"참여 기록");
                ui.PreviewEnvironment("environment:E01");nav.ShowIsland();
                if(GameObject.Find("PREVIEW environment:E01")!=null || ui.Service.Snapshot.Nutrient!=10)throw new Exception("Navigation did not cancel unpaid preview");
                home.shopButton.onClick.Invoke();
                if(ui.CurrentScreen!="shop")throw new Exception("Home shop button did not open the existing garden shop");
                ui.PreviewGrowth();nav.ShowIsland();
                if((home.ProceduralPlant==null || home.RenderedRecipe?.GrowthStep!=0) || ui.Service.Snapshot.Nutrient!=10)
                    throw new Exception("Cancelled growth changed the saved home");
                nav.ShowActivities();ui.PreviewGrowth();
                ui.GetComponentsInChildren<Button>(true).Single(b=>b.gameObject.activeInHierarchy && b.gameObject.name=="확정").onClick.Invoke();
                if(ui.Service.Snapshot.Nutrient!=0 || (home.ProceduralPlant==null || home.RenderedRecipe?.GrowthStep!=3))
                    throw new Exception("Growth purchase did not update the saved home");
                yield return null;
                AssertFullScreenGarden(home);
                LocalLoopPreview.Render(new Vector2Int(900,1600),Path.Combine(root,"home-after-growth.png"));
            }
            else
            {
                if(ui.Service.Snapshot.Nutrient!=0 || ui.Service.Snapshot.MbctNextOrder!=2 || ui.Service.Snapshot.Sessions.Count!=2
                    || (home.ProceduralPlant==null || home.RenderedRecipe?.GrowthStep!=3))throw new Exception("Process restart failed to restore committed progress and growth");
                if(ui.Service.Snapshot.Experiences.Single().Emotion!="차분해요" || ui.Service.Snapshot.PracticePreferences.Single(a=>a.Key=="support").Value!="치료진")throw new Exception("Restart lost journal or plan");
                string id=ui.Service.Snapshot.Sessions.Single(s=>MbctPolicy.IsMbct(s.MissionId)).SessionId;
                if(!ui.Service.CompleteSession(id) || ui.Service.Snapshot.Nutrient!=0)throw new Exception("Restart retry duplicated reward");
                nav.ShowIsland();
                if(!home.IsOverview || home.ViewControlsExpanded)throw new Exception("Process restart did not restore the complete garden with folded controls");
                yield return null;
                AssertFullScreenGarden(home);
                LocalLoopPreview.Render(new Vector2Int(900,1600),Path.Combine(root,"home-after-restore.png"));
                GardenProceduralValidation.CaptureGardenContext(home);
                // A step sync may commit rewards while the activity page is hidden.
                // Reopening that existing page must agree with the main garden.
                ui.ShowHome();nav.ShowIsland();
                if(!ui.ToggleDebugWalk())throw new Exception("Could not enable synthetic debug walk");
                for(int i=0;i<10;i++)if(!ui.AddVirtualDebugStep())throw new Exception("Synthetic step failed");
                nav.ShowActivities();LocalLoopValidation.ShowLegacy(ui,"Home");
                if(ui.Service.Snapshot.Nutrient!=1 || !home.guide.text.Contains("영양제 1개")
                    || !ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("영양제 1  ·  8주 코스")))
                    throw new Exception("Activity header retained stale nutrients after a hidden reward update");
            }
            Finish(root,phase,null);
        }

        static void Finish(string root,string phase,Exception failure)
        {
            EditorApplication.update-=WaitForPlay;EditorApplication.update-=WatchRun;
            SessionState.SetBool(Key,false);
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,"play-"+phase+".json"),JsonUtility.ToJson(new Result
                {status=failure==null?"PASS":"FAIL",phase=phase,error=failure?.ToString()},true));
            if(failure!=null)Debug.LogException(failure);else Debug.Log("LOCAL_LOOP_PLAY_SMOKE_PASS "+phase);
            EditorApplication.Exit(failure==null?0:1);
        }

        static void ScrollGarden(GardenHomePresenter home,float direction)
        {
            var layout=home.homeCanvas.GetComponentInChildren<CapstoneDesign.Prototype.PrototypeGardenPreviewLayout>();
            var orbit=layout==null?null:layout.orbit;
            if(orbit==null || EventSystem.current==null)throw new Exception("Gesture smoke needs the actual garden orbit and EventSystem");
            Canvas.ForceUpdateCanvases();
            var point=new Vector2(Screen.width*.5f,Screen.height*.48f);
            var pointer=new PointerEventData(EventSystem.current){position=point,scrollDelta=new Vector2(0,direction)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<CapstoneDesign.Prototype.PrototypeIslandDrag>()!=orbit)
                throw new Exception("Garden gesture intercepted by UI at "+point+": "+string.Join(" > ",hits.Take(5).Select(hit=>hit.gameObject.name)));
            pointer.pointerCurrentRaycast=hits[0];
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.scrollHandler);
        }

        static void AssertLegacyRendererParked(GardenHomePresenter home)
        {
            if(home.oldCamera.enabled || home.oldLight.enabled)
                throw new Exception("Integrated tab rendered the hidden legacy camera or light");
        }

        static Graphic[] CurrentChromeGraphics(WeekOneQuestDemo ui)
        {
            var bar=ui.GetComponentsInChildren<MissionBottomNavigation>().Single();
            var heading=ui.GetComponentsInChildren<Text>().Single(text=>text.fontSize==28 && !text.transform.IsChildOf(bar.transform));
            var graphics=new List<Graphic>{heading};
            foreach(var button in new[]{bar.HomeButton,bar.MissionButton,bar.CourseButton})
            {
                graphics.Add(button.GetComponentsInChildren<Text>().Single(text=>text.gameObject.activeInHierarchy && text!=bar.MissionBadge));
                graphics.Add(button.GetComponentInChildren<RawImage>());
            }
            foreach(var graphic in graphics)
                if(graphic==null || !graphic.isActiveAndEnabled || graphic.canvasRenderer.cull)
                    throw new Exception("Current header/tab Graphic is missing, inactive or culled: "+graphic?.name);
            return graphics.ToArray();
        }

        static void AssertSharedChromePixels(Graphic[] graphics,Texture2D visible,Texture2D hidden,string path)
        {
            if(visible==null || hidden==null || visible.width!=hidden.width || visible.height!=hidden.height)
                throw new Exception("The chrome phase did not receive matching rendered Game view frames.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path,visible.EncodeToPNG());
            var pixels=visible.GetPixels32();var control=hidden.GetPixels32();
            // Compare with a frame where only the target glyphs/icons are hidden.
            // This observes actual rendering without assuming the former green palette.
            foreach(var graphic in graphics)
            {
                var canvas=graphic.canvas.rootCanvas;
                Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                var corners=new Vector3[4];graphic.rectTransform.GetWorldCorners(corners);
                var minimum=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
                var maximum=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                int x0=Mathf.Clamp(Mathf.FloorToInt(minimum.x*visible.width/Screen.width),0,visible.width);
                int x1=Mathf.Clamp(Mathf.CeilToInt(maximum.x*visible.width/Screen.width),0,visible.width);
                int y0=Mathf.Clamp(Mathf.FloorToInt(minimum.y*visible.height/Screen.height),0,visible.height);
                int y1=Mathf.Clamp(Mathf.CeilToInt(maximum.y*visible.height/Screen.height),0,visible.height);
                int ink=0;
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)
                {
                    int offset=y*visible.width+x;var pixel=pixels[offset];var absent=control[offset];
                    if(Mathf.Abs(pixel.r-absent.r)>8 || Mathf.Abs(pixel.g-absent.g)>8 || Mathf.Abs(pixel.b-absent.b)>8)ink++;
                }
                if(ink<8)throw new Exception("Current header/tab has no rendered glyph/icon difference: "+graphic.name+"; pixels="+ink+"; image="+path);
            }
            Debug.Log("LOCAL_LOOP_SHARED_CHROME_PIXELS_PASS "+Path.GetFileName(path));
        }

        static void AssertPageStartsAtTop(WeekOneQuestDemo ui,string title)
        {
            // Observe the actual first rendered frame, without scrolling it into place.
            Canvas.ForceUpdateCanvases();
            var scroll=ui.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Local garden flow");
            if(scroll.content.rect.height>scroll.viewport.rect.height+1 && scroll.verticalNormalizedPosition<.999f)
                throw new Exception("Page did not open at its beginning: "+title+"; scroll="+scroll.verticalNormalizedPosition);
            var heading=ui.GetComponentsInChildren<Text>().Single(t=>t.text==title);
            var focus=heading.GetComponentInParent<GardenActivityLayout>();
            if(focus!=null)
            {
                var frame=(RectTransform)focus.transform;
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(frame,heading.transform);
                var body=RectTransformUtility.CalculateRelativeRectTransformBounds(frame,focus.Body);
                if(!heading.transform.IsChildOf(focus.Header) || bounds.min.y<body.max.y-1 ||
                    bounds.max.y>frame.rect.yMax+1 || bounds.min.x<frame.rect.xMin-1 || bounds.max.x>frame.rect.xMax+1)
                    throw new Exception("Fixed page heading is hidden at first entry: "+title);
            }
            else
            {
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,heading.transform);
                if(bounds.min.y<scroll.viewport.rect.yMin-1 || bounds.max.y>scroll.viewport.rect.yMax-95)
                    throw new Exception("Page heading is hidden at first entry: "+title);
            }
            Debug.Log("LOCAL_LOOP_FIRST_ENTRY_PASS "+title);
        }

        static void AssertFullScreenGarden(GardenHomePresenter home)
        {
            // Observe the player's normal frame before the capture helper applies
            // any layout itself; this catches return-to-home composition regressions.
            var layout=home.homeCanvas.GetComponentInChildren<CapstoneDesign.Prototype.PrototypeGardenPreviewLayout>();
            if(!home.IsGardenVisible || layout==null || !layout.IsFullScreen)
                throw new Exception("Home did not restore its full-screen garden background");
            var canvas=(RectTransform)home.homeCanvas.transform;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(canvas,layout.transform);
            if(bounds.min.x>canvas.rect.xMin+1 || bounds.max.x<canvas.rect.xMax-1 ||
                bounds.min.y>canvas.rect.yMin+1 || bounds.max.y<canvas.rect.yMax-1)
                throw new Exception("Garden background leaves uncovered canvas edges");
            Debug.Log("LOCAL_LOOP_FULL_SCREEN_GARDEN_PASS");
        }

        static void ScrollIntoView(ScrollRect scroll, Button target)
        {
            if(scroll==null || scroll.content==null || scroll.viewport==null || target==null
                || !target.transform.IsChildOf(scroll.content))
                throw new Exception("Scroll smoke requires a button in the actual scrolling content");
            Canvas.ForceUpdateCanvases();Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            var viewport = scroll.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target.transform);
            var content = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, scroll.content);
            float hiddenHeight = content.size.y - viewport.rect.height;
            if(hiddenHeight>0)
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition
                    + (bounds.center.y - viewport.rect.center.y) / hiddenHeight);
            Canvas.ForceUpdateCanvases();Canvas.ForceUpdateCanvases();
            bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target.transform);
            if(bounds.min.y < viewport.rect.yMin - 1 || bounds.max.y > viewport.rect.yMax + 1)
                throw new Exception("Could not reveal the entire scroll target: "+target.name);
        }

        static void AssertRaycastTarget(Button target)
        {
            // Callback-only tests cannot detect an inactive page
            // or decorative Graphic intercepting the current control's tap.
            Canvas.ForceUpdateCanvases();
            // Flow text height depends on the width resolved in the first pass.
            Canvas.ForceUpdateCanvases();
            // Batchmode has no Game view overlay draw after all cameras park.
            // Use the same explicit render texture as the synthetic UI captures;
            // this checks Unity hit routing, not the device's overlay or IME.
            if(target!=null && (target.targetGraphic.depth<0 || target.targetGraphic.canvasRenderer.cull))
                LocalLoopPreview.Render(new Vector2Int(Screen.width,Screen.height),Path.Combine(
                    Environment.GetEnvironmentVariable("CAPSTONE_SMOKE_SAVE_ROOT"),"raycast-frame-"+(raycastFrame++)+".png"));
            var eventSystem=EventSystem.current;
            if(eventSystem==null || target==null || !target.IsActive() || !target.IsInteractable())
                throw new Exception("Raycast smoke requires an active EventSystem and enabled button");
            var rect=(RectTransform)target.transform;
            var canvas=target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
            if(!new Rect(0,0,Screen.width,Screen.height).Contains(point))
                throw new Exception("Raycast target is outside the visible screen: "+target.name+" at "+point);
            var hits=new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem){position=point},hits);
            var reached=hits.Count==0?null:hits[0].gameObject.GetComponentInParent<Button>();
            if(reached!=target)
                throw new Exception("Tap blocked for "+target.name+" at "+point+"; raycast order: "+
                    string.Join(" > ",hits.Take(5).Select(hit=>hit.gameObject.name))+"; graphic depth="+
                    target.targetGraphic.depth+" culled="+target.targetGraphic.canvasRenderer.cull+" rect="+rect.rect);
            Debug.Log("LOCAL_LOOP_RAYCAST_PASS "+target.name+" at "+point);
        }
    }
}
