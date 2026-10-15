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
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.EditorTools
{
    [InitializeOnLoad]
    public static class LocalLoopPlaySmoke
    {
        const string Key="Capstone.LocalLoopSmoke.Active";
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
                EditorApplication.update-=WaitForPlay;SessionState.SetBool(Key,false);
                Debug.LogError("Play smoke startup timed out");EditorApplication.Exit(1);return;
            }
            if(!EditorApplication.isPlaying)return;
            var loop=UnityEngine.Object.FindFirstObjectByType<WeekOneQuestDemo>(FindObjectsInactive.Include);
            if(loop?.Service==null)return;
            EditorApplication.update-=WaitForPlay;Run();
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
                    Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"play-"+phase+".json"),"{\"status\":\"FAIL\"}");
                    Debug.LogException(failure);SessionState.SetBool(Key,false);EditorApplication.Exit(1);yield break;
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
            if(phase=="fresh")
            {
                if(ui.Service.Snapshot.Sessions.Count!=0)throw new Exception("Fresh fixture directory already contains sessions");
                nav.ShowIsland();
                AssertLegacyRendererParked(home);
                if((home.ProceduralPlant==null || home.RenderedRecipe?.GrowthStep!=0) || !home.homeCanvas.gameObject.activeSelf)
                    throw new Exception("Fresh home did not show the saved seed");
                yield return null;
                if(home.ViewControlsExpanded)throw new Exception("Home view controls must start folded");
                var viewToggle=home.homeCanvas.GetComponentsInChildren<Button>().Single(button=>button.name=="보기 +");
                AssertRaycastTarget(viewToggle);
                viewToggle.onClick.Invoke();
                yield return null;
                if(!home.ViewControlsExpanded)throw new Exception("Home view toggle did not reveal controls");
                var viewButtons = home.homeCanvas.GetComponentsInChildren<Button>();
                var plantView = viewButtons.Single(button=>button.name=="가까이 보기");
                var overview = viewButtons.Single(button=>button.name=="정원 전체");
                var zoomIn = viewButtons.Single(button=>button.name=="확대");
                var zoomOut = viewButtons.Single(button=>button.name=="축소");
                foreach(var button in new[]{plantView,overview,zoomIn,zoomOut}) AssertRaycastTarget(button);
                if(!home.IsOverview)throw new Exception("Home did not default to the complete garden");
                var gardenContext=GardenProceduralValidation.CaptureGardenContext(home);
                if(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Any(node=>node.name=="Saved garden additions" && node.parent==null))
                    throw new Exception("Legacy saved scenery leaks into the integrated garden camera");
                plantView.onClick.Invoke();
                if(home.IsOverview)throw new Exception("Close view did not focus the saved plant");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                overview.onClick.Invoke();
                if(!home.IsOverview)throw new Exception("Overview did not show the complete garden");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                plantView.onClick.Invoke();
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                float previousZoom=home.Zoom;
                zoomOut.onClick.Invoke();
                if(home.Zoom>=previousZoom)throw new Exception("Zoom out did not change the camera view");
                zoomIn.onClick.Invoke();
                if(Mathf.Abs(home.Zoom-previousZoom)>.001f || home.RenderedRecipe.GrowthStep!=0)
                    throw new Exception("Zoom controls changed the plant or failed to restore the view");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                home.activityTab.onClick.Invoke();
                AssertLegacyRendererParked(home);
                if(!nav.activitiesPanel.activeSelf || home.gardenVisuals.activeSelf)
                    throw new Exception("Activity tab did not park the garden camera");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext,requireActive:false);
                AssertRaycastTarget(ui.GetComponentsInChildren<Button>().Single(button=>button.name=="추천 활동 안내"));
                nav.ShowIsland();
                if(home.IsOverview || Mathf.Abs(home.Zoom-previousZoom)>.001f)
                    throw new Exception("Tab return lost the selected close view or zoom");
                GardenProceduralValidation.AssertGardenContext(home,gardenContext);
                home.settingsTab.onClick.Invoke();
                AssertLegacyRendererParked(home);
                if(!nav.settingsPanel.activeSelf || home.IsGardenVisible || !home.settingsTab.gameObject.activeInHierarchy)
                    throw new Exception("Settings tab did not open existing settings");
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
                ui.ShowHome();
                yield return null;
                AssertPageStartsAtTop(ui,"오늘의 활동");
                ui.ShowRecords();
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
                GardenProceduralValidation.CaptureGardenContext(home);
                // A step sync may commit rewards while the activity page is hidden.
                // Reopening that existing page must agree with the main garden.
                ui.ShowHome();nav.ShowIsland();
                if(!ui.ToggleDebugWalk())throw new Exception("Could not enable synthetic debug walk");
                for(int i=0;i<10;i++)if(!ui.AddVirtualDebugStep())throw new Exception("Synthetic step failed");
                nav.ShowActivities();
                if(ui.Service.Snapshot.Nutrient!=1 || !home.guide.text.Contains("영양제 1개")
                    || !ui.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("영양제 1  ·  코스")))
                    throw new Exception("Activity header retained stale nutrients after a hidden reward update");
            }
            File.WriteAllText(Path.Combine(root,"play-"+phase+".json"),"{\"status\":\"PASS\",\"mode\":\"MOCK\",\"environment\":\"Unity Editor Play mode\",\"phase\":\""+phase+"\"}");
            Debug.Log("LOCAL_LOOP_PLAY_SMOKE_PASS "+phase);
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }

        static void AssertLegacyRendererParked(GardenHomePresenter home)
        {
            if(home.oldCamera.enabled || home.oldLight.enabled)
                throw new Exception("Integrated tab rendered the hidden legacy camera or light");
        }

        static void AssertPageStartsAtTop(WeekOneQuestDemo ui,string title)
        {
            // Observe the actual first rendered frame, without scrolling it into place.
            Canvas.ForceUpdateCanvases();
            var scroll=ui.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Local garden flow");
            if(scroll.content.rect.height>scroll.viewport.rect.height+1 && scroll.verticalNormalizedPosition<.999f)
                throw new Exception("Page did not open at its beginning: "+title+"; scroll="+scroll.verticalNormalizedPosition);
            var heading=ui.GetComponentsInChildren<Text>().Single(t=>t.text==title);
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,heading.transform);
            if(bounds.min.y<scroll.viewport.rect.yMin-1 || bounds.max.y>scroll.viewport.rect.yMax-95)
                throw new Exception("Page heading is hidden at first entry: "+title);
            Debug.Log("LOCAL_LOOP_FIRST_ENTRY_PASS "+title);
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
            // The home Canvas stays mounted to share its navigation. Callback-only
            // tests cannot detect a transparent home Graphic intercepting these taps.
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
