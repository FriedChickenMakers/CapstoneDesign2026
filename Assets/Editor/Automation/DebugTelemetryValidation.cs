using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime.Personalization;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace CapstoneDesign.EditorTools
{
    public sealed class DebugValidationScope:IDisposable
    {
        readonly Func<string,int,int> oldRead=DebugAccess.ReadForValidation;
        readonly Action<string,int> oldWrite=DebugAccess.WriteForValidation;
        readonly Func<string,bool> oldVerify=DebugAccess.VerifyForValidation;
        readonly Dictionary<string,int> values=new Dictionary<string,int>();
        public DebugValidationScope(bool authorized=false)
        {
            if(authorized)values["capstone.debug.authorized.v1"]=1;
            DebugAccess.ReadForValidation=(key,fallback)=>values.TryGetValue(key,out var value)?value:fallback;
            DebugAccess.WriteForValidation=(key,value)=>values[key]=value;
            DebugAccess.VerifyForValidation=pin=>pin=="12345678"; // Synthetic gate only, not the product verifier.
        }
        public void Dispose(){DebugAccess.ReadForValidation=oldRead;DebugAccess.WriteForValidation=oldWrite;DebugAccess.VerifyForValidation=oldVerify;}
    }
    public static class DebugTelemetryValidation
    {
        static int checks;
        static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
        static Button Button(SensorRawDisplay settings,string name)=>settings.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        public static void RunBatch()
        {
            var output=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            if(!Application.isBatchMode || string.IsNullOrEmpty(output))throw new InvalidOperationException("Explicit batch artifact directory required.");
            Directory.CreateDirectory(output);checks=0;
            var report=new FocusMissionValidation.Report{runId="debug-telemetry",utc=DateTimeOffset.UtcNow.ToString("O"),environment="Synthetic memory gate and save; no device/network/preferences"};
            using(var gate=new DebugValidationScope())
            {
                foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(390,844)})
                using(var f=new FocusMissionValidation.Fixture(report,true))
                {
                    DebugAccess.Exit();f.Resize(size.x,size.y);
                    var settings=f.OpenMissionSettings();
                    Check(!DebugAccess.Enabled && !DebugAccess.Qa,"Ordinary mode defaults to non-QA.");
                    Check(!settings.AddTestCoinsButton.gameObject.activeInHierarchy,"Tools are initially hidden.");
                    Check(!f.Ui.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Focus.DebugLauncher"),"No unprotected mission launcher.");
                    string saved=StateCodec.Encode(f.Service.Snapshot);
                    f.Ui.StartMissionDebug("P03");Check(!f.Ui.IsMissionDebugActive,"Direct debug call cannot bypass the gate.");
                    f.ClickControl(settings.TestToolsToggleButton);
                    if(!DebugAccess.Authorized)
                    {
                        var input=settings.GetComponentsInChildren<InputField>(false).Single(i=>i.name=="Settings.DebugPassword");
                        Check(input.inputType==InputField.InputType.Password,"Password is masked.");
                        input.SetTextWithoutNotify("00000000");f.ClickControl(Button(settings,"Settings.DebugUnlock"));
                        Check(!DebugAccess.Authorized && !DebugAccess.Enabled,"Wrong password does not authorize.");
                        f.Capture("debug-locked-"+size.x,true);
                        input.SetTextWithoutNotify("12345678");f.ClickControl(Button(settings,"Settings.DebugUnlock"));
                    }
                    Check(DebugAccess.Authorized && DebugAccess.Enabled && DebugAccess.Qa,"Entering enables QA by default.");
                    f.Capture("debug-tools-"+size.x,true);
                    f.ClickControl(Button(settings,"Settings.QaFlag"));
                    Check(DebugAccess.Enabled && !DebugAccess.Qa,"Authorized user may turn QA off while debug stays enabled.");
                    f.ClickControl(Button(settings,"Settings.DebugExit"));
                    Check(DebugAccess.Authorized && !DebugAccess.Enabled && !DebugAccess.Qa,"Exit retains authorization and stops QA.");
                    f.ClickControl(settings.TestToolsToggleButton);
                    Check(DebugAccess.Enabled && DebugAccess.Qa,"Remembered entry needs no password and resets QA ON.");
                    Check(!settings.GetComponentsInChildren<InputField>(false).Any(i=>i.name=="Settings.DebugPassword"),"Remembered entry leaves password input hidden.");
                    f.ClickControl(Button(settings,"Settings.MissionDemo"));f.Click("Focus.Debug.P03");
                    Check(f.Ui.IsMissionDebugActive,"Protected menu opens the existing mission sandbox.");
                    Check(StateCodec.Encode(f.Service.Snapshot)==saved,"Demo cannot mutate the original save.");
                    f.Capture("debug-demo-P03-"+size.x,true);
                    f.Ui.RestoreMissionDebug();Check(ReferenceEquals(f.Ui.Service,f.Service),"Closing demo restores the original service.");
                }
                using(var f=new FocusMissionValidation.Fixture(report))
                {
                    bool qa=true;f.Service.CaptureMeasurementContext=()=>new MeasurementStamp{Metadata=qa?"QA":"PARTICIPANT",RecordedUtcMs=1};
                    Check(f.Service.SelectMbctProgram("8w") && f.Service.BeginFocusSession("synthetic-frozen","P01",1),f.Service.LastError);
                    qa=false;Check(f.Service.StartGuidedFocusSession("synthetic-frozen",""),f.Service.LastError);
                    var session=f.Service.Snapshot.Sessions.Single(s=>s.SessionId=="synthetic-frozen");
                    Check(session.SelectedMeasurement.Metadata=="QA" && session.StartedMeasurement.Metadata=="PARTICIPANT","Event classification freezes at each durable transition.");
                    Check(f.Service.StartFocusPracticeStep(session.SessionId,0),f.Service.LastError);
                    Check(f.Service.ShortenGuidedFocusPractice(session.SessionId,"","",17),f.Service.LastError);
                    Check(f.Service.CompleteFocusSession(session.SessionId),f.Service.LastError);
                    session=f.Service.Snapshot.Sessions.Single(s=>s.SessionId==session.SessionId);
                    var d=TelemetryMeasurementContext.Details(session,session.EndedMeasurement,"activity_completed");
                    Check(d.active_seconds==17 && d.recommended_seconds==60 && d.completion_mode=="too_long","Stored summary uses actual timer seconds and shortened reason.");
                    var clone=StateCodec.Clone(f.Service.Snapshot).Sessions.Single(s=>s.SessionId==session.SessionId);
                    Check(clone.SelectedMeasurement.Metadata=="QA" && clone.EndedMeasurement.Metadata=="PARTICIPANT","Save roundtrip preserves historical classification.");
                }
            }
            var modelStore=new FileEventQueueStore(Path.Combine(output,"synthetic-model-queue.xml"));
            var modelQueue=new EventQueue(modelStore);modelQueue.Bind("synthetic-frozen-model");modelQueue.SetConsent(true,DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds());
            var holder=new GameObject("Synthetic model stamp validation");
            try
            {
                var client=holder.AddComponent<PersonalizationClient>();
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(PersonalizationClient).GetField("queue",flags).SetValue(client,modelQueue);
                var at=DateTimeOffset.UtcNow;
                var session=new ActivitySession{SessionId="synthetic-model-transition",MissionId="free-mission:M03",
                    Recommendation=new RecommendationContext{DecisionId=Guid.NewGuid().ToString("N"),PolicyId="local-rules-v1",AppVersion="synthetic",Features=new[]{1f,.5f,.5f,1f},MeasurementClassified=true,MeasurementQa=true,MeasurementDebug=true},
                    StartedMeasurement=new MeasurementStamp{Metadata=JsonUtility.ToJson(new TelemetryMeasurementContext.Context{classified=true,qa=false,debug=true}),RecordedUtcMs=at.ToUnixTimeMilliseconds()}};
                typeof(PersonalizationClient).GetMethod("RecordSessionEvent",flags).Invoke(client,new object[]{session,"activity_started",at});
                Check(modelStore.Load().Events.Single().classification.classified && !modelStore.Load().Events.Single().classification.qa,"Model event uses its own durable transition flag, not earlier recommendation QA.");
                session.StartedMeasurement.Metadata=JsonUtility.ToJson(new TelemetryMeasurementContext.Context{classified=true,qa=true,debug=true});
                typeof(PersonalizationClient).GetMethod("RecordSessionEvent",flags).Invoke(client,new object[]{session,"activity_started",at});
                Check(modelStore.Load().Events.Count==1 && !modelStore.Load().Events.Single().classification.qa,"Model replay cannot change an already queued flag.");
            }
            finally{UnityEngine.Object.DestroyImmediate(holder);}
            report.passed=checks;File.WriteAllText(Path.Combine(output,"debug-telemetry.json"),JsonUtility.ToJson(report,true));
            Debug.Log("DEBUG_TELEMETRY_VALIDATION PASS checks="+checks);
        }
    }
}
