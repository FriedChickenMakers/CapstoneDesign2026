using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    public static class MbctPreview
    {
        sealed class Clock:IClock
        {
            public DateTimeOffset Time=DateTimeOffset.Parse("2026-10-05T12:00:00+09:00");
            public DateTimeOffset UtcNow=>Time;
            public TimeZoneInfo TimeZone=>TimeZoneInfo.CreateCustomTimeZone("Preview-KST",TimeSpan.FromHours(9),"KST","KST");
        }
        sealed class Store:IStateStore
        {string data;public GardenState Load()=>data==null?null:StateCodec.Decode(data);public void Save(GardenState s)=>data=StateCodec.Encode(s);}
        public static void Capture()
        {
            AndroidPlatformBridge.UseMockProvider();
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new Exception("Graphics rendering required");
            string output=Path.Combine(Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS")??"artifacts/mbct-localization","visual");Directory.CreateDirectory(output);
            string requested=Environment.GetEnvironmentVariable("CAPSTONE_MBCT_SCREEN");
            int captured=0;
            foreach(var size in new[]{new Vector2Int(900,1600),new Vector2Int(900,1950),new Vector2Int(1200,800)})
            foreach(var name in new[]{"today","course","free","records","activity-record","eating","body","routine","breathing-space","sound","thought","thought-personal","self-care","journal","journal-emotion","journal-record","plan","plan-list","reflection","complete","placement","growth"})
            {
                if(!string.IsNullOrEmpty(requested) && requested!=name)continue;
                Debug.Log("MBCT_CAPTURE_BEGIN "+name+" "+size);
                EditorSceneManager.OpenScene("Assets/Scenes/MockupMain.unity");
                var nav=UnityEngine.Object.FindFirstObjectByType<MockupNavigation>(FindObjectsInactive.Include);
                var loop=nav.activitiesPanel.GetComponent<WeekOneQuestDemo>();var clock=new Clock();var store=new Store();
                var balance=new DemoBalanceConfig{VisitorChancePercent=0};var service=new GardenStateService(store,clock,balance);
                loop.Balance=balance;loop.Initialize(service,true);nav.ShowActivities();
                int target=name=="free"||name=="activity-record"?2:name=="records"?5:name=="thought-personal"?32:name=="body"?2:name=="routine"?4:name=="breathing-space"?14:name=="sound"?16:name=="thought"?21:name=="self-care"?28:1;
                for(int i=1;i<target;i++)
                {
                    while(MbctPolicy.Availability(service.Snapshot)!=null){clock.Time=clock.Time.AddDays(1);service.RefreshCycle(new string[0]);}
                    if(!service.BeginSession("seed-"+i,MbctPolicy.MissionId(i),i)||!service.StartSession("seed-"+i)||!service.CompleteSession("seed-"+i))throw new Exception(service.LastError);
                    clock.Time=clock.Time.AddDays(1);service.RefreshCycle(new string[0]);
                }
                while(MbctPolicy.Availability(service.Snapshot)!=null){clock.Time=clock.Time.AddDays(1);service.RefreshCycle(new string[0]);}
                service.SavePreference("routine","양치하기");
                if(name=="placement")loop.PreviewEnvironment("environment:E01");
                else if(name=="growth")loop.PreviewGrowth();
                else if(name=="today")loop.ShowHome();
                else if(name=="course")loop.ShowCourse();
                else if(name=="free")loop.ShowFree();
                else if(name=="records")loop.ShowRecords();
                else if(name=="activity-record")loop.ShowRecord("seed-1");
                else if(name=="plan-list")loop.ShowPersonalPlan();
                else if(name=="journal-emotion")
                {
                    loop.ShowExperienceTypes();loop.GetComponentsInChildren<Button>().Single(b=>b.name=="불편함").onClick.Invoke();
                    loop.GetComponentsInChildren<Button>().Single(b=>b.name=="다음").onClick.Invoke();loop.GetComponentsInChildren<Button>().Single(b=>b.name=="다음").onClick.Invoke();
                }
                else if(name=="journal")loop.ShowExperienceTypes();
                else if(name=="journal-record")
                {
                    string longText=string.Join("\n",Enumerable.Repeat("합성 미리보기 기록 · 몸과 감정, 생각을 살펴보았어요.",8));
                    service.SaveExperience("preview-journal","불편함",longText,"가슴이 답답했어요","울적해요","답장이 늦어서 걱정하는 생각이 떠올랐어요");loop.ShowExperienceRecord("preview-journal");
                }
                else if(name=="plan") {service.SavePreference("warning-sign","잠을 잘 못 잠");loop.ShowPlanField("warning-sign");}
                else
                {
                    loop.SelectMission(MbctContent.Course[target-1]);var id=service.Snapshot.Sessions.Last().SessionId;service.StartSession(id);
                    if(name=="body")service.SavePracticeStep(id,2);
                    if(name=="thought-personal")service.SavePracticeStep(id,5);
                    if(name=="reflection")loop.ShowReflection();
                    else if(name=="complete")loop.Finish(null,null);
                    else loop.ShowSession();
                }
                Canvas.ForceUpdateCanvases();
                LocalLoopPreview.Render(size,Path.Combine(output,name+"-"+size.x+"x"+size.y+".png"));captured++;service.Dispose();
            }
            File.WriteAllText(Path.Combine(output,"manifest.json"),"{\"environment\":\"Unity Editor synthetic UI preview\",\"images\":"+captured+",\"renderer\":\""+SystemInfo.graphicsDeviceName+"\",\"deviceTested\":false}");
            Debug.Log("MBCT preview: "+captured+" synthetic screens saved to "+output);
        }
    }
}
