using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CapstoneDesign.Plants;
using CapstoneDesign.Prototype;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Invoked by the isolated remaining-UI runner. All content is synthetic and
    // every mutation uses a memory store; camera output is a generated test JPG.
    public static class RemainingFormsUiValidation
    {
        static bool captureFailures;
        public static void Run(FocusMissionValidation.Report report, Action<bool,string> check, bool capture)
        {
            if(!Application.isBatchMode || Application.companyName!="CapstoneSyntheticValidation")
                throw new InvalidOperationException("A synthetic isolated batch is required.");
            captureFailures=capture;
            var failures=new List<string>();
            void Case(string name,Action test)
            {
                try{test();}
                catch(Exception error){failures.Add(name+"\n"+error);}
            }
            foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(390,844),new Vector2Int(1200,2000)})
            {
                Case("Daily "+size,()=>Daily(report,check,capture,size));
                Case("Memory "+size,()=>Memory(report,check,capture,size));
                Case("Photo "+size,()=>Photo(report,check,capture,size));
            }
            if(failures.Count>0)throw new InvalidOperationException(string.Join("\n\n",failures));
        }

        static T Find<T>(FocusMissionValidation.Fixture f,string name) where T:Component =>
            f.Ui.transform.root.GetComponentsInChildren<T>(false).SingleOrDefault(item=>item.name==name);
        static string State(FocusMissionValidation.Fixture f)=>StateCodec.Encode(f.Service.Snapshot);
        static void Settle(FocusMissionValidation.Fixture f)
        {
            for(int pass=0;pass<3;pass++)
            {
                Canvas.ForceUpdateCanvases();
                foreach(var fit in f.Ui.GetComponentsInChildren<FigmaMissionCanvasFit>(false))fit.Fit();
                foreach(var page in f.Ui.GetComponentsInChildren<GardenPageLayout>(false))page.Refresh();
                Canvas.ForceUpdateCanvases();
            }
        }
        static Rect In(RectTransform parent,RectTransform node)
        {
            // A viewport's descendant content can be thousands of units tall.
            // Compare its own four corners, never the aggregate child bounds.
            var corners=new Vector3[4];node.GetWorldCorners(corners);
            var points=corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point=>point.x),points.Min(point=>point.y),points.Max(point=>point.x),points.Max(point=>point.y));
        }
        static void Layout(FocusMissionValidation.Fixture f,Action<bool,string> check,params string[] actions)
        {
            Settle(f);var layout=f.Ui.GetComponentInChildren<GardenPageLayout>(false);
            check(layout!=null&&layout.GetComponent<FocusMobilePageLayout>().FixedContent,"Shared fixed form layout has a single geometry owner.");
            var root=(RectTransform)layout.transform;
            var viewport=In(root,layout.Viewport);var footer=In(root,layout.Footer);
            bool separated=viewport.height>80&&viewport.yMin>=footer.yMax-.5f;
            if(!separated)f.Capture("forms-layout-failed-"+f.Ui.CurrentScreen.Replace(':','-')+"-"+Mathf.RoundToInt(root.rect.height),captureFailures);
            check(separated,"Only the content scrolls above the fixed actions; viewport="+viewport+", footer="+footer+", frame height="+root.rect.height);
            foreach(string name in actions)
            {
                var button=Find<Button>(f,name);var bounds=In(root,(RectTransform)button.transform);
                check(button!=null&&button.GetComponentInParent<ScrollRect>()==null&&bounds.yMin>=root.rect.yMin-.5f
                    &&bounds.yMax<=root.rect.yMax+.5f,"Fixed action remains on screen without scrolling: "+name);
            }
            if(layout.Notice!=null&&layout.Notice.gameObject.activeSelf)
            {
                var notice=In(root,layout.Notice.rectTransform);
                check(viewport.yMin>=notice.yMax-.5f&&notice.yMin>=footer.yMax-.5f,"Save failure notice occupies its own fixed region.");
            }
            if(layout.Navigation!=null)
                check(footer.yMin>=In(root,layout.Navigation).yMax-.5f,"Form actions remain above the bottom navigation.");
            var status=Find<Text>(f,"DailyEmotion.Status");
            if(status!=null)check(status.preferredHeight<=status.rectTransform.rect.height+.5f,
                "Daily guidance fits its reserved status region, including selected, completed and expired states.");
        }
        static void Ready(GardenStateService service,string id,string code,int order,Action<bool,string> check,bool complete)
        {
            check(service.BeginFocusSession(id,code,order),service.LastError);
            check(service.StartSession(id),service.LastError);
            var definition=FocusMissionCatalog.FindForSession(service.Snapshot.Sessions.Single(item=>item.SessionId==id));
            for(int i=0;i<definition.Steps.Length;i++)
            {
                var step=definition.Steps[i];string value=step.RequiredAnswer?"합성 응답":null;
                if(step.Kind=="draw")value="DRAW1|AA_B";
                check(service.ConfirmFocusStep(id,i,value,value==null),service.LastError);
            }
            if(!complete)return;
            if(CameraMissionPolicy.Ready(service.Snapshot.Sessions.Single(item=>item.SessionId==id)))check(service.SkipPhoto(id),service.LastError);
            check(service.CompletePracticeSession(id),service.LastError);
        }

        static void Daily(FocusMissionValidation.Report report,Action<bool,string> check,bool capture,Vector2Int size)
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                f.Resize(size.x,size.y);check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Ready(f.Service,"forms-p","P01",1,check,true);Ready(f.Service,"forms-m","M03",0,check,true);
                f.Ui.ShowDailyEmotion();Settle(f);
                string id=DailyEmotionPolicy.RecordId(f.Service.CurrentCycleId);
                Layout(f,check,"DailyEmotion.Back","DailyEmotion.Save");
                f.Capture("forms-emotion-entry-"+size,capture);
                f.Click("DailyEmotion.MoreEmotions");
                check(f.Ui.GetComponentsInChildren<Button>(false).Count(b=>b.name.StartsWith("DailyEmotion.Emotion.",StringComparison.Ordinal))==25,
                    "Expanded emotion choices retain every existing emotion and unknown option.");
                f.Click("DailyEmotion.Emotion.편안함");
                var selected=Find<Button>(f,"DailyEmotion.Emotion.편안함");
                var unselected=Find<Button>(f,"DailyEmotion.Emotion.기쁨");
                check(selected.GetComponentInChildren<GardenThemeSurface>().style==GardenThemeSurface.Style.Selected
                    &&selected.GetComponentInChildren<Text>().color==GardenSagePalette.Primary
                    &&unselected.GetComponentInChildren<GardenThemeSurface>().style==GardenThemeSurface.Style.Wood
                    &&unselected.GetComponentInChildren<Text>().color==GardenSagePalette.Ink,
                    "The actual themed choice face and readable text both follow the selected emotion.");
                Layout(f,check,"DailyEmotion.Back","DailyEmotion.Save");f.Capture("forms-emotion-selected-"+size,capture);
                var input=Find<InputField>(f,"DailyEmotion.Note");input.text="다시 돌아와 이어 쓸 합성 메모";input.onEndEdit.Invoke(input.text);
                var scroll=Find<ScrollRect>(f,"DailyEmotion.Scroll");scroll.content.anchoredPosition=new Vector2(0,170);Settle(f);
                float position=scroll.content.anchoredPosition.y;
                f.Click("DailyEmotion.Back");
                float remembered=(float)typeof(WeekOneQuestDemo).GetField("dailyEmotionScroll",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(f.Ui);
                f.Ui.ShowDailyEmotion(id);Settle(f);
                var restored=Find<ScrollRect>(f,"DailyEmotion.Scroll");
                string restoredText=Find<InputField>(f,"DailyEmotion.Note").text;
                int choices=f.Ui.GetComponentsInChildren<Button>(false).Count(b=>b.name.StartsWith("DailyEmotion.Emotion.",StringComparison.Ordinal));
                f.Capture("forms-emotion-draft-reopened-"+size,capture);
                check(restoredText=="다시 돌아와 이어 쓸 합성 메모"&&choices==25
                    &&Mathf.Abs(restored.content.anchoredPosition.y-position)<.6f,
                    "Reopening the same daily draft restores text, expanded choices and exact clamped reading position; text="+restoredText
                    +", choices="+choices+", expected="+position+", remembered="+remembered+", actual="+restored.content.anchoredPosition.y
                    +", content="+restored.content.rect.height+", viewport="+restored.viewport.rect.height);
                string before=State(f);f.Store.FailNextSaves=1;f.Click("DailyEmotion.Save");
                check(State(f)==before&&f.Ui.CurrentScreen=="daily-emotion","A failed daily completion preserves the committed state.");
                check(Find<InputField>(f,"DailyEmotion.Note").text=="다시 돌아와 이어 쓸 합성 메모","Failed daily completion preserves unsaved input.");
                Layout(f,check,"DailyEmotion.Back","DailyEmotion.Save");f.Capture("forms-emotion-save-failed-"+size,capture);
                var stale=Find<Button>(f,"DailyEmotion.Save").onClick;f.Click("DailyEmotion.Save");
                check(f.Service.GetDailyEmotion(id).IsCompleted,"Daily completion retry succeeds.");
                before=State(f);stale.Invoke();check(State(f)==before,"A retained completion callback never duplicates rewards.");
                f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);f.Store.FailNextSaves=1;f.Ui.ShowDailyEmotion(id);Settle(f);
                check(State(f)==before&&f.Store.FailNextSaves==1&&Find<InputField>(f,"DailyEmotion.Note")==null,
                    "Reading yesterday's completed emotion does not refresh/save a new cycle or bind an editable draft.");
                f.Store.FailNextSaves=0;Layout(f,check,"DailyEmotion.Back","DailyEmotion.History");
                f.Capture("forms-emotion-readonly-"+size,capture);
            }
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                f.Resize(size.x,size.y);check(f.Service.SelectMbctProgram("8w"),f.Service.LastError);
                Ready(f.Service,"expiry-p","P01",1,check,true);Ready(f.Service,"expiry-m","M03",0,check,true);
                f.Ui.ShowDailyEmotion();Settle(f);var input=Find<InputField>(f,"DailyEmotion.Note");input.text="저장하지 않은 만료 초안";
                string before=State(f);f.Clock.UtcNow=f.Clock.UtcNow.AddDays(1);f.Ui.RefreshDailyEmotionUiDate();Settle(f);
                check(!Find<Button>(f,"DailyEmotion.Save").interactable&&input.text=="저장하지 않은 만료 초안"
                    &&State(f)==before,"Crossing the deadline disables completion and keeps the expired local draft without writing.");
                Layout(f,check,"DailyEmotion.Back","DailyEmotion.Save");
                f.Capture("forms-emotion-expired-"+size,capture);f.Click("DailyEmotion.Back");
                check(f.Ui.CurrentScreen=="home","An expired draft does not trap the back action.");
            }
        }

        static void Memory(FocusMissionValidation.Report report,Action<bool,string> check,bool capture,Vector2Int size)
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                f.Resize(size.x,size.y);
                for(int i=0;i<12;i++)check(f.Service.SaveExperience("forms-memory-"+i,"즐거움","합성 경험 "+i,"편안한 몸","고마움","차분한 생각"),f.Service.LastError);
                var plant=WeeklyPlants.Active(f.Service.Snapshot);string plantId=plant.Id;
                var memory=plant.Memories.First(item=>item.Kind==MemoryRecordKind.Experience);
                string recipe=PlantCodec.Encode(plant.Recipe);int coins=f.Service.CoinBalance;
                f.Ui.ShowPlantMemories(plantId);Settle(f);Layout(f,check,"PlantMemory.Back");
                check(Find<Button>(f,"PlantMemory.ViewPlant").GetComponentInParent<ScrollRect>()==null,
                    "The current-plant return action stays outside its complete scrollable memory list.");
                f.Capture("forms-memory-list-"+size,capture);
                f.Click("PlantMemory.Record."+memory.OrganId);
                f.ClickControl(Find<Button>(f,"GardenMemoPopup.Edit"));Settle(f);
                check(f.Ui.GetComponentsInChildren<FocusMissionKeyboardAvoidance>(false).Length==4,
                    "Every experience input has the existing keyboard-avoidance presentation.");
                Layout(f,check,"PlantMemory.Cancel","PlantMemory.Save");f.Capture("forms-memory-edit-"+size,capture);
                Find<InputField>(f,"PlantMemory.EditField.0").text="수정할 합성 경험";
                string before=State(f);f.Store.FailNextSaves=1;f.Click("PlantMemory.Save");
                check(State(f)==before&&Find<InputField>(f,"PlantMemory.EditField.0").text=="수정할 합성 경험",
                    "Failed memory save preserves the input and all stored data.");
                Layout(f,check,"PlantMemory.Cancel","PlantMemory.Save");f.Capture("forms-memory-edit-failed-"+size,capture);
                f.Click("PlantMemory.Cancel");check(State(f)==before&&f.Ui.CurrentScreen=="plant-memories:"+plantId,"Edit cancellation restores the source without saving.");
                f.Click("PlantMemory.Record."+memory.OrganId);f.ClickControl(Find<Button>(f,"GardenMemoPopup.Edit"));
                Find<InputField>(f,"PlantMemory.EditField.0").text="저장한 합성 경험";
                var stale=Find<Button>(f,"PlantMemory.Save").onClick;f.Click("PlantMemory.Save");before=State(f);stale.Invoke();
                check(State(f)==before&&f.Service.Snapshot.Experiences.Single(item=>item.Id==memory.RecordId).Event=="저장한 합성 경험",
                    "Memory retry changes only its own record and rejects a stale save callback.");
                f.Click("PlantMemory.Record."+memory.OrganId);f.ClickControl(Find<Button>(f,"GardenMemoPopup.Delete"));
                Layout(f,check,"PlantMemory.Cancel","PlantMemory.DeleteConfirm");f.Capture("forms-memory-delete-"+size,capture);
                f.Click("PlantMemory.Cancel");check(State(f)==before,"Delete cancellation leaves the record intact.");
                f.Click("PlantMemory.Record."+memory.OrganId);f.ClickControl(Find<Button>(f,"GardenMemoPopup.Delete"));
                f.Store.FailNextSaves=1;f.Click("PlantMemory.DeleteConfirm");check(State(f)==before,"Failed deletion preserves the record and leaf association.");
                Layout(f,check,"PlantMemory.Cancel","PlantMemory.DeleteConfirm");f.Capture("forms-memory-delete-failed-"+size,capture);
                stale=Find<Button>(f,"PlantMemory.DeleteConfirm").onClick;f.Click("PlantMemory.DeleteConfirm");before=State(f);stale.Invoke();
                check(State(f)==before&&PlantAlbumHistory.MemoryRecord(f.Service.Snapshot,plantId,memory.OrganId)==null
                    &&f.Service.CoinBalance==coins&&PlantCodec.Encode(WeeklyPlants.Active(f.Service.Snapshot).Recipe)==recipe,
                    "Deleting the selected memory retains the plant appearance, growth, wallet and other records; stale retry is inert.");
            }
        }

        sealed class Photos : IMissionPhotoProvider
        {
            public string DirectoryPath {get;}
            public PhotoVerificationResult Result;
            readonly string time;
            public Photos(string path,string time){DirectoryPath=path;this.time=time;Directory.CreateDirectory(path);}
            public PhotoVerificationResult Start(string id,string target)
            {
                Result=PhotoVerificationService.Unavailable(id,target);Result.status="processing";Result.capturedUtc=time;
                var image=new Texture2D(48,48,TextureFormat.RGB24,false);
                try
                {
                    image.SetPixels(Enumerable.Repeat(new Color(.3f,.6f,.4f),48*48).ToArray());image.Apply();
                    File.WriteAllBytes(Path.Combine(DirectoryPath,id+".jpg"),image.EncodeToJPG());
                }
                finally{UnityEngine.Object.DestroyImmediate(image);}
                return Result;
            }
            public PhotoVerificationResult Poll(string id)=>Result;
            public void Delete(string id){if(CameraMissionPolicy.ValidId(id)){string path=Path.Combine(DirectoryPath,id+".jpg");if(File.Exists(path))File.Delete(path);}}
            public void Prune(string[] keep){}
        }
        static void Photo(FocusMissionValidation.Report report,Action<bool,string> check,bool capture,Vector2Int size)
        {
            using(var f=new FocusMissionValidation.Fixture(report))
            {
                f.Resize(size.x,size.y);
                var provider=new Photos(Path.Combine(Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS"),"synthetic-form-photos",size.ToString()),f.Clock.UtcNow.ToString("O"));
                f.Ui.SetPhotoProviderForValidation(provider);
                Ready(f.Service,"forms-photo","M03",0,check,false);f.Ui.SelectFocusSession("forms-photo");Settle(f);
                check(f.Ui.CurrentScreen=="focus-photo","Ready camera mission enters its optional photo page.");
                Layout(f,check,"Photo.Capture","Photo.Skip");f.Capture("forms-photo-choice-"+size,capture);
                var staleCapture=Find<Button>(f,"Photo.Capture").onClick;f.Click("Photo.Capture");
                var scroll=Find<ScrollRect>(f,"Photo.Scroll");scroll.verticalNormalizedPosition=0;Settle(f);
                float position=scroll.content.anchoredPosition.y;
                provider.Result.status="not_found";f.Ui.TickMissionPhoto();Settle(f);
                scroll=Find<ScrollRect>(f,"Photo.Scroll");
                check(Mathf.Abs(scroll.content.anchoredPosition.y-Mathf.Min(position,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)))<.6f,
                    "Asynchronous photo status changes retain the exact clamped reading position.");
                check(Find<Text>(f,"Photo.Status").text.Contains("이대로 등록할까요?")&&Find<Text>(f,"Photo.Privacy").text.Contains("기기에만"),
                    "Manual confirmation and local-only photo privacy remain explicit.");
                Layout(f,check,"Photo.Register","Photo.Retake","Photo.Skip");f.Capture("forms-photo-confirm-"+size,capture);
                string before=State(f);staleCapture.Invoke();check(State(f)==before,"A stale capture action cannot replace the current pending photo.");
                f.Store.FailNextSaves=1;f.Click("Photo.Register");check(State(f)==before&&Find<RawImage>(f,"Photo.Preview")!=null,
                    "Failed photo registration keeps both pending photo and its visible preview.");
                Layout(f,check,"Photo.Register","Photo.Retake","Photo.Skip");f.Capture("forms-photo-save-failed-"+size,capture);
                var stale=Find<Button>(f,"Photo.Register").onClick;f.Click("Photo.Register");before=State(f);stale.Invoke();
                check(State(f)==before&&f.Service.Snapshot.Sessions.Single(item=>item.SessionId=="forms-photo").Photo?.UserConfirmed==true,
                    "Photo retry stores direct confirmation once and a stale callback never pays again.");
            }
        }
    }
}
