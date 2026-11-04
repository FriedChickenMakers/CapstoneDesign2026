using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;

namespace CapstoneDesign.EditorTools
{
    // Synthetic previews exercise actual Unity buttons; they are not model-accuracy evidence.
    public static class CameraMissionValidation
    {
        public static void RunAll(){Run();FocusMissionValidation.Run();}
        public static void RunAndBuild(){Run();BuildTools.BuildAndroid();}
        static int assertions;
        static void Check(bool ok,string message){assertions++;if(!ok)throw new InvalidOperationException(message);}
        sealed class Provider:IMissionPhotoProvider
        {
            public string DirectoryPath { get; }
            public string Status="matched";
            public string CapturedUtc;
            public PhotoVerificationResult Result;
            public Provider(string path){DirectoryPath=path;Directory.CreateDirectory(path);}
            public PhotoVerificationResult Start(string id,string target)
            {
                Result=PhotoVerificationService.Unavailable(id,target);Result.status=Status;
                if(CapturedUtc!=null)Result.capturedUtc=CapturedUtc;
                Result.label=Status=="matched"?(target=="book"?"book":"cup"):"";Result.score=Status=="matched"?.88:0;
                if(Status!="cancelled"){
                    var texture=new Texture2D(320,240,TextureFormat.RGB24,false);
                    var colors=new Color32[320*240];
                    for(int y=0;y<240;y++)for(int x=0;x<320;x++)colors[y*320+x]=x>90&&x<230&&y>35&&y<205?new Color32(80,140,120,255):new Color32(235,238,231,255);
                    texture.SetPixels32(colors);texture.Apply();File.WriteAllBytes(Path.Combine(DirectoryPath,id+".jpg"),texture.EncodeToJPG(80));UnityEngine.Object.DestroyImmediate(texture);
                }
                return Result;
            }
            public PhotoVerificationResult Poll(string id)=>Result;
            public void Delete(string id){var path=Path.Combine(DirectoryPath,id+".jpg");if(File.Exists(path))File.Delete(path);}
            public void Prune(string[] keep){}
        }
        [MenuItem("Capstone Mockup/Validate Camera Missions (Synthetic)")]
        public static void Run()
        {
            string root=Path.Combine(Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS")??Path.Combine(Directory.GetCurrentDirectory(),"artifacts"),"camera-validation");
            Directory.CreateDirectory(root);
            var report=new FocusMissionValidation.Report{runId="camera-"+DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"),utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            try {
                foreach(var code in new[]{"M03","M14","P04"})
                foreach(var status in new[]{"matched","not_found","unavailable","cancelled","skip"})
                {
                    using(var f=new FocusMissionValidation.Fixture(report))
                    {
                        if(code=="M14")f.Resize(720,1280);
                        if(code=="P04")f.Resize(1200,2000);
                        var provider=new Provider(Path.Combine(root,code+"-"+status)){Status=status,CapturedUtc=f.Clock.UtcNow.ToString("O")};f.Ui.SetPhotoProviderForValidation(provider);
                        if(code=="P04")f.Click("기본 미션 시작하기");else f.Invoke("StartFocusAdditionalMission",FocusMissionCatalog.Find(code).Id);
                        f.Click("시작");f.CompletePractice(FocusMissionCatalog.Find(code),false,keepPhotoChoice:true);
                        Check(f.Ui.CurrentScreen=="focus-photo",code+" photo gate");
                        if(status=="matched")f.Capture("camera-"+code+"-01-photo-choice",true);
                        if(status=="skip")f.Click("Photo.Skip");
                        else {
                            f.Click("Photo.Capture");f.AssertVisibleAssets();
                            if(code=="M03")f.Capture("camera-"+status,true);
                            if(status=="matched"){
                                if(code=="M14")f.RevealGraphic(f.Ui.GetComponentsInChildren<UnityEngine.UI.Text>(false).Single(t=>t.name=="Photo.Status"));
                                f.Capture("camera-"+code+"-02-confirmed",true);
                            }
                            if(status=="cancelled")f.Click("Photo.Skip");
                            else {
                                if(status=="not_found")Check(f.VisibleText.Contains("이대로 등록할까요?"),"gentle unconfirmed message");
                                if(code=="M03"&&status=="matched"){
                                    var old=provider.Result.id;f.Click("Photo.Retake");Check(provider.Result.id!=old,"new capture id");
                                    f.Store.FailNextSaves=1;f.Click("Photo.Register");Check(f.Ui.CurrentScreen=="focus-photo"&&f.Latest.Photo==null,"registration failure keeps preview");
                                }
                                f.Click("Photo.Register");Check(f.Latest.Photo!=null,"registered");Check(f.Latest.Photo.UserConfirmed==(status!="matched"),"automatic and manual recorded separately");
                            }
                        }
                        Check(f.Ui.CurrentScreen=="focus-emotion","photo choice joins original reflection flow");
                        f.Click("Focus.SkipReflection");
                        int bonus=status=="skip"||status=="cancelled"?0:1;
                        Check(f.Latest.RewardAmount==5&&f.Latest.PhotoBonusAmount==bonus,"base and bonus amounts");
                        Check(f.Ui.CurrentScreen=="focus-complete","optional emotion completion");
                        if(status=="matched")f.Capture("camera-"+code+"-03-completion-bonus",true);
                        if(bonus==1){
                            Check(f.VisibleText.Contains("사진 확인 보너스"),"bonus separately visible");f.Click("Focus.ViewRecord");f.AssertVisibleAssets();
                            if(code=="M03"&&status=="matched")f.Capture("camera-record",true);
                            if(status=="matched")f.Capture("camera-"+code+"-04-saved-photo",true);
                            string photoId=f.Latest.Photo.Id;int nutrients=f.Service.Snapshot.Nutrient;
                            f.Click("Photo.Delete");Check(f.Latest.Photo==null&&!File.Exists(Path.Combine(provider.DirectoryPath,photoId+".jpg")),"photo deletion removes file and binding");
                            Check(f.Service.Snapshot.Nutrient==nutrients&&f.Latest.PhotoBonusAmount==1,"deletion retains reward");
                        }
                        report.passed++;
                    }
                }
                using(var f=new FocusMissionValidation.Fixture(report))
                {
                    var provider=new Provider(Path.Combine(root,"protocol")){Status="processing"};var service=new PhotoVerificationService(provider);
                    string id=Guid.NewGuid().ToString("N");service.Start(id,"container");Check(service.Poll(id,"container",0).status=="processing","worker pending");
                    Check(service.Poll(id,"container",5).status=="unavailable"&&service.ReadPhoto(id)!=null,"five second fallback retains photo");
                    provider.Result.status="matched";provider.Result.label="cup";provider.Result.score=.88;Check(service.Poll(id,"container",6).status=="unavailable","late result cannot overwrite timeout choice");
                    Check(service.Poll(Guid.NewGuid().ToString("N"),"container",7).status=="unavailable","stale result ignored");Check(service.ReadPhoto("../escape")==null,"unsafe path rejected");
                    provider.Status="matched";service.Start(id,"container");provider.Result.score=double.NaN;Check(service.Poll(id,"container",8).status=="unavailable","malformed result fallback");report.passed++;
                }
            }catch(Exception ex){report.failed++;report.checks.Add(new FocusMissionValidation.CheckResult{name="Camera UI integration",status="fail",detail=ex.ToString()});throw;}
            finally{report.checks.Add(new FocusMissionValidation.CheckResult{name="Assertions",status=report.failed==0?"pass":"fail",detail=assertions.ToString()});File.WriteAllText(Path.Combine(root,"report.json"),JsonUtility.ToJson(report,true));}
            Debug.Log("CAMERA_VALIDATION passed="+report.passed+" assertions="+assertions+" clicks="+report.buttonClicks+" raycasts="+report.raycastChecks);
        }
    }
}
