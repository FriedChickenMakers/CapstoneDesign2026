using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CapstoneDesign.Runtime;
using CapstoneDesign.Runtime.LocalState;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.EditorTools
{
    // Presentation-only checks use the existing disposable memory-state fixture.
    // No real history, Health Connect query, PlayerPrefs or device input is used.
    public static class RemainingHistoryUiValidation
    {
        static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;

        public static void Run(FocusMissionValidation.Report report,Action<bool,string> check,bool capture)
        {
            foreach(var size in new[]{new Vector2Int(320,568),new Vector2Int(390,844),new Vector2Int(1200,2000)})
                ReadOnlyScreens(report,check,capture,size);
            EmptyScreens(report,check,capture);
            HistoricalCollections(report,check,capture);
        }

        public static void RunBatch()
        {
            string requested=Environment.GetEnvironmentVariable("CAPSTONE_HISTORY_VALIDATION");
            if(!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.companyName!="CapstoneSyntheticValidation" || string.IsNullOrWhiteSpace(requested))
                throw new InvalidOperationException("An isolated synthetic batch and CAPSTONE_HISTORY_VALIDATION are required.");
            string output=Path.GetFullPath(requested);
            if(output==Path.GetFullPath(Application.persistentDataPath)
                || Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())
                throw new InvalidOperationException("Use a fresh output directory.");
            Directory.CreateDirectory(output);
            string oldArtifacts=Environment.GetEnvironmentVariable("CAPSTONE_ARTIFACTS");
            Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS",output);
            var report=new FocusMissionValidation.Report{runId="remaining-history-ui",utc=DateTimeOffset.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            int assertions=0;
            try
            {
                Run(report,(value,message)=>{assertions++;if(!value)throw new InvalidOperationException(message);},true);
                report.passed++;
            }
            catch(Exception error){report.failed++;report.checks.Add(new FocusMissionValidation.CheckResult{name="History UI",status="fail",detail=error.ToString()});throw;}
            finally
            {
                report.checks.Add(new FocusMissionValidation.CheckResult{name="assertions",status="info",detail=assertions.ToString()});
                File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
                Environment.SetEnvironmentVariable("CAPSTONE_ARTIFACTS",oldArtifacts);
            }
        }

        static void ReadOnlyScreens(FocusMissionValidation.Report report,Action<bool,string> check,bool capture,Vector2Int size)
        {
            using(var f=new FocusMissionValidation.Fixture(report,true))
            {
                f.Resize(size.x,size.y);
                check(f.Service.BeginSession("synthetic-history-detail","free-mission:M15",0)&&f.Service.StartSession("synthetic-history-detail"),f.Service.LastError);
                f.Clock.UtcNow=f.Clock.UtcNow.AddMinutes(6);
                check(f.Service.EndParticipation("synthetic-history-detail"),f.Service.LastError);
                string longText=string.Join(" ",Enumerable.Repeat("합성 기록의 긴 문장입니다.",18));
                check(f.Service.SaveExperience("synthetic-experience","중립",longText,"합성 몸 감각","합성 감정","합성 생각"),f.Service.LastError);
                check(f.Service.SavePreference("warning-sign",longText),f.Service.LastError);
                var session=f.Service.Snapshot.Sessions.Single(s=>s.SessionId=="synthetic-history-detail");
                long start=DateTimeOffset.Parse(session.StartedUtc).ToUnixTimeMilliseconds(),end=DateTimeOffset.Parse(session.EndedUtc).ToUnixTimeMilliseconds();
                f.Ui.PreviewHealth=Health(start,end);
                string before=StateCodec.Encode(f.Service.Snapshot);
                f.Invoke("ShowRecord",session.SessionId);
                // Retain first-frame evidence even when a layout assertion fails.
                f.Capture("history-health-top-"+size.x,capture);
                FixedFrame(f,check,"health");
                var query=f.Ui.GetComponentsInChildren<Button>(false).Single(b=>b.name=="건강 기록 다시 조회");
                check(f.Ui.CurrentScreen=="record"&&query!=null&&f.Has("기록 목록"),"Health detail lost fixed actions.");
                check(!query.interactable,"Synthetic health preview must not issue native queries.");
                var graph=f.Ui.GetComponentInChildren<HeartSampleGraph>(false);
                check(graph!=null&&graph.Samples.Length==4&&graph.WindowStart==start&&graph.WindowEnd==end,"Exact interval/source samples changed.");
                check(graph.AxisMinimum==65&&graph.AxisMaximum==80,"Graph axis no longer matches rendered data bounds.");
                var chart=(RectTransform)graph.transform.parent;
                var chartLabels=chart.GetComponentsInChildren<Text>(false);
                var unit=OwnBounds(chart,chartLabels.Single(t=>t.text=="BPM").rectTransform);
                var upper=OwnBounds(chart,chartLabels.Single(t=>t.text==graph.AxisMaximum.ToString("0")).rectTransform);
                var lower=OwnBounds(chart,chartLabels.Single(t=>t.text==graph.AxisMinimum.ToString("0")).rectTransform);
                check(unit.min.x>=upper.max.x,"BPM unit overlaps the maximum-axis label.");
                float plotBottom=chart.InverseTransformPoint(graph.rectTransform.TransformPoint(new Vector3(0,graph.rectTransform.rect.yMin+HeartSampleGraph.PlotPadding))).y;
                float plotTop=chart.InverseTransformPoint(graph.rectTransform.TransformPoint(new Vector3(0,graph.rectTransform.rect.yMax-HeartSampleGraph.PlotPadding))).y;
                check(Mathf.Abs(lower.center.y-plotBottom)<.1f&&Mathf.Abs(upper.center.y-plotTop)<.1f,"Axis labels are not aligned with the padded data plot.");
                var sources=f.Ui.GetComponentsInChildren<Text>(true).Single(t=>t.name=="Heart source details");
                check(!sources.gameObject.activeSelf,"Source diagnostics must be folded initially.");
                f.RevealGraphic(graph);f.Capture("history-health-graph-"+size.x,capture);
                f.Click("측정 출처 보기");
                check(sources.gameObject.activeSelf&&sources.text.Contains("synthetic-watch")&&sources.text.Contains("synthetic-phone"),"Source fold loses provenance.");
                f.Click("측정 출처 보기");check(!sources.gameObject.activeSelf,"Source fold failed to close.");
                f.Click("기록 목록");check(f.Ui.CurrentScreen=="records","Health back destination changed.");
                FixedFrame(f,check,"legacy history");f.Capture("history-legacy-list-"+size.x,capture);

                f.Invoke("ShowExperienceHistory");FixedFrame(f,check,"experience list");
                check(f.VisibleText.Contains("…"),"Long event should have a concise list preview.");
                f.Click("중립");
                check(f.VisibleText.Contains(longText)&&f.VisibleText.Contains("합성 몸 감각")&&f.VisibleText.Contains("합성 감정")&&f.VisibleText.Contains("합성 생각"),"Experience detail truncated saved data.");
                check(f.Ui.GetComponentsInChildren<Transform>(false).Count(t=>t.name.StartsWith("History field · "))==4,"Experience fields are not separated.");
                FixedFrame(f,check,"experience detail");f.Capture("history-experience-detail-"+size.x,capture);
                f.Click("경험 목록");f.Click("나의 계획");
                check(f.VisibleText.Contains("…")&&f.VisibleText.Contains("아직 적지 않았어요"),"Plan previews must distinguish saved and empty fields.");
                f.Capture("history-plan-"+size.x,capture);
                f.Click("나의 변화 신호");check(f.Input!=null&&f.Input.text==longText,"Plan editing lost complete saved text.");
                f.SetInput("합성 미저장 초안");f.Click("돌아가기");
                check(f.VisibleText.Contains("작성 중 · 합성 미저장 초안"),"Plan return lost or misrepresented unsaved draft.");
                f.Click("나의 변화 신호");check(f.Input.text=="합성 미저장 초안","Draft not retained on reopen.");
                f.Store.FailNextSaves=1;f.Click("저장");
                check(f.Ui.CurrentScreen=="plan-field"&&f.Input.text=="합성 미저장 초안","Save failure discarded the draft or navigated away.");
                check(f.VisibleText.Contains("저장하지 못했어요"),"Plan save failure must remain visible in the editor.");
                f.Click("돌아가기");
                check(!f.VisibleText.Contains("저장하지 못했어요")&&f.VisibleText.Contains("작성 중 · 합성 미저장 초안"),"Leaving the editor must dismiss its error and retain the draft preview.");
                f.Invoke("ShowRecordSummary");FixedFrame(f,check,"summary");
                check(!f.VisibleText.Contains("저장하지 못했어요"),"Unrelated summary inherited a dismissed plan save error.");
                check(f.Ui.GetComponentsInChildren<Transform>(false).Count(t=>t.name.StartsWith("Record summary · "))==2,"Weekly/all-time summaries are not distinct cards.");
                check(f.VisibleText.Contains("1회")&&f.VisibleText.Contains("1개"),"Summary lost participation or experience counts.");
                f.Capture("history-summary-"+size.x,capture);
                f.Invoke("ShowPersonalPlan");f.Click("나의 변화 신호");
                check(f.Input.text=="합성 미저장 초안","Dismissing the save error discarded the editable draft.");
                check(before==StateCodec.Encode(f.Service.Snapshot),"Read-only navigation or failed save changed persisted history.");
            }
        }

        static void EmptyScreens(FocusMissionValidation.Report report,Action<bool,string> check,bool capture)
        {
            using(var f=new FocusMissionValidation.Fixture(report,true))
            {
                f.Resize(320,568);string before=StateCodec.Encode(f.Service.Snapshot);
                f.Invoke("ShowDiscoveries");FixedFrame(f,check,"empty animals");
                check(f.VisibleText.Contains("아직 만난 동물이 없어요"),"Empty animal state disappeared.");
                f.Capture("history-animals-empty",capture);
                f.Invoke("ShowGrowthHistory");FixedFrame(f,check,"empty growth");
                check(f.VisibleText.Contains("이전에 남긴 성장 기록이 없어요"),"Historical growth empty state disappeared.");
                f.Invoke("ShowExperienceTypes");FixedFrame(f,check,"experience chooser");
                f.Click("중립");check(f.Input!=null,"Experience input disappeared.");
                FixedFrame(f,check,"experience input");f.SetInput("합성 입력 보존");f.Click("다음");f.Click("이전");
                check(f.Input.text=="합성 입력 보존","Experience previous-step draft changed.");
                f.Capture("history-experience-input",capture);f.Click("기록 취소");
                check(before==StateCodec.Encode(f.Service.Snapshot),"Viewing empty states or cancelling experience mutated the save.");
            }
        }

        static void FixedFrame(FocusMissionValidation.Fixture f,Action<bool,string> check,string context)
        {
            typeof(FocusMissionValidation.Fixture).GetMethod("Layout",Hidden).Invoke(f,null);
            var layout=f.Ui.GetComponentInChildren<GardenActivityLayout>(false);
            check(layout!=null&&layout.Header!=null&&layout.Body!=null&&layout.Footer!=null,"Fixed frame missing: "+context);
            layout.RefreshLayout();
            var frame=(RectTransform)layout.transform;
            // CalculateRelativeRectTransformBounds includes every descendant;
            // scroll content intentionally extends outside its masked viewport.
            // Test the actual frame rectangles instead of that content union.
            var head=OwnBounds(frame,layout.Header);
            var body=OwnBounds(frame,layout.Body);
            var foot=OwnBounds(frame,layout.Footer);
            check(layout.Body.GetComponent<RectMask2D>()!=null,"Scrolling history must be clipped by its viewport: "+context);
            check(head.min.y>=body.max.y-1&&foot.max.y<=body.min.y+1&&body.size.y>100,
                "Heading/body/footer overlap or collapse: "+context+"; head="+head+"; body="+body+"; footer="+foot);
            check(head.max.y<=frame.rect.yMax+1&&foot.min.y>=frame.rect.yMin-1,"Fixed controls leave the frame: "+context);
        }

        static Bounds OwnBounds(RectTransform frame,RectTransform target)
        {
            var corners=new Vector3[4];target.GetWorldCorners(corners);
            var bounds=new Bounds(frame.InverseTransformPoint(corners[0]),Vector3.zero);
            for(int i=1;i<corners.Length;i++)bounds.Encapsulate(frame.InverseTransformPoint(corners[i]));
            return bounds;
        }

        static void HistoricalCollections(FocusMissionValidation.Report report,Action<bool,string> check,bool capture)
        {
            var store=new FocusMissionValidation.MemoryStore();var clock=new FocusMissionValidation.SyntheticClock();
            using(var seed=new GardenStateService(store,clock,new DemoBalanceConfig{VisitorChancePercent=0}))
            {
                check(seed.Open()&&seed.SelectMbctProgram("2w"),seed.LastError);
                var saved=seed.Snapshot;
                saved.DiscoveredAnimalIds.AddRange(new[]{"animal:A01","animal:A07"});
                saved.PurchaseReceipts.Add("synthetic-old-growth");
                saved.PurchaseCommands.Add(new PurchaseCommand{RequestId="synthetic-old-growth",Command="growth|plant:P06"});
                saved.GrowthHistory.Add(new GardenGrowthRecord{RequestId="synthetic-old-growth",PlantId="plant:P06",LinkedSessionId="",CreatedUtc=clock.UtcNow.ToString("O"),BeforeGrowth=0,AfterGrowth=.1f});
                StateCodec.Validate(saved);store.Save(saved);
            }
            using(var f=new FocusMissionValidation.Fixture(report,true,false,store))
            {
                f.Resize(390,844);string before=StateCodec.Encode(f.Service.Snapshot);
                f.Invoke("ShowDiscoveries");FixedFrame(f,check,"discovered animals");
                check(f.VisibleText.Contains("고양이")&&f.VisibleText.Contains("토끼"),"Discovered animals disappeared.");
                check(f.Ui.GetComponentsInChildren<Transform>(false).Count(t=>t.name.StartsWith("Discovered animal · "))==2,"Animals are not separate rows.");
                f.Capture("history-animals-found",capture);
                f.Invoke("ShowGrowthHistory");f.Click("씨앗 → 새싹");FixedFrame(f,check,"growth detail");
                check(f.VisibleText.Contains("연결한 활동 기록이 없어요")&&!f.Has("그때의 활동과 감정 보기"),"Unlinked growth invented a source activity.");
                f.Capture("history-growth-detail",capture);
                check(before==StateCodec.Encode(f.Service.Snapshot),"Collection/history viewing changed the saved garden.");
            }
        }

        static AndroidPlatformSnapshot Health(long start,long end)=>new AndroidPlatformSnapshot
        {
            inputMode="MOCK",healthConnect=new HealthConnectSnapshot{heartRate=new HealthMetricSnapshot
            {
                status="AVAILABLE",queryComplete=true,queryStartEpochMs=start,queryEndEpochMs=end,sampleCount=4,
                heartRateSamples=new[]
                {
                    new HeartRateSampleSnapshot{measuredAtEpochMs=start+60000,beatsPerMinute=70,sourcePackage="synthetic-watch",segmentId=1},
                    new HeartRateSampleSnapshot{measuredAtEpochMs=start+120000,beatsPerMinute=74,sourcePackage="synthetic-watch",segmentId=1},
                    new HeartRateSampleSnapshot{measuredAtEpochMs=start+180000,beatsPerMinute=72,sourcePackage="synthetic-phone",segmentId=1},
                    new HeartRateSampleSnapshot{measuredAtEpochMs=start+240000,beatsPerMinute=75,sourcePackage="synthetic-phone",segmentId=1}
                }
            }}
        };
    }
}
