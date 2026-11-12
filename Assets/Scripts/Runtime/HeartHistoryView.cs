using System;
using System.Linq;
using CapstoneDesign.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public static class HeartHistoryView
    {
        public static Text Build(Transform parent, AndroidPlatformSnapshot snapshot, string startIso, string endIso)
        {
            var flow=parent.gameObject.AddComponent<VerticalLayoutGroup>();
            flow.spacing=18;flow.childControlWidth=flow.childControlHeight=true;
            flow.childForceExpandWidth=true;flow.childForceExpandHeight=false;
            var metric=snapshot.healthConnect?.heartRate ?? new HealthMetricSnapshot();
            long start=DateTimeOffset.TryParse(startIso,out var a)?a.ToUnixTimeMilliseconds():0;
            long end=DateTimeOffset.TryParse(endIso,out var b)?b.ToUnixTimeMilliseconds():0;
            bool exact=metric.queryStartEpochMs==start && metric.queryEndEpochMs==end && start>0 && end>start;
            var samples=exact?(metric.heartRateSamples ?? Array.Empty<HeartRateSampleSnapshot>()).Where(s=>s!=null && s.measuredAtEpochMs>=start && s.measuredAtEpochMs<end).ToArray():Array.Empty<HeartRateSampleSnapshot>();
            string status=!exact?"이 활동 구간은 아직 조회하지 않았어요":samples.Length==0?(metric.queryComplete?"측정 기록 없음":"조회 미완료 · 기록 유무를 확인할 수 없어요"):"심박 "+metric.sampleCount+"개 · 표시 "+samples.Length+"개";
            string mode=snapshot.inputMode=="LIVE"?"":"["+snapshot.inputMode+"] ";
            GardenTypography.SetWeight(GardenUi.FlowLabel(parent,"활동 중 심박",GardenUi.HeadingSize),true);
            var statusLabel=GardenUi.FlowLabel(parent,mode+status+"\n"+(exact?StatusGuidance(metric.status):"아래 ‘건강 기록 다시 조회’를 눌러 주세요."),GardenUi.BodySize,GardenUi.Muted);
            if(start>0 && end>start)
                GardenUi.FlowLabel(parent,"조회 구간  "+LocalTime(start)+" ~ "+LocalTime(end),GardenUi.CaptionSize,GardenUi.Muted);
            var chart=GardenUi.Card(parent,"Heart samples",0,0,1,0);
            chart.AddComponent<LayoutElement>().preferredHeight=304;
            if(samples.Length>0)
            {
                var graph=GardenUi.Box(chart.transform,"Sample graph",0,0,1,1).AddComponent<HeartSampleGraph>();
                graph.rectTransform.offsetMin=new Vector2(96,54);graph.rectTransform.offsetMax=new Vector2(-24,-42);
                graph.Samples=samples;graph.WindowStart=start;graph.WindowEnd=end;graph.raycastTarget=false;graph.SetAllDirty();
                var unit=GardenUi.Label(chart.transform,"BPM",0,1,0,0,GardenUi.CaptionSize);
                unit.rectTransform.pivot=new Vector2(0,1);unit.rectTransform.anchoredPosition=new Vector2(96,-10);unit.rectTransform.sizeDelta=new Vector2(100,38);
                AxisLabel(chart.transform,graph.AxisMaximum.ToString("0"),1);
                AxisLabel(chart.transform,((graph.AxisMaximum+graph.AxisMinimum)/2).ToString("0.#"),.5f);
                AxisLabel(chart.transform,graph.AxisMinimum.ToString("0"),0);
                var first=GardenUi.Label(chart.transform,LocalTime(start),0,0,0,0,GardenUi.CaptionSize);
                first.rectTransform.pivot=Vector2.zero;first.rectTransform.anchoredPosition=new Vector2(96,6);first.rectTransform.sizeDelta=new Vector2(150,38);
                var last=GardenUi.Label(chart.transform,LocalTime(end),1,0,0,0,GardenUi.CaptionSize);
                last.rectTransform.pivot=new Vector2(1,0);last.rectTransform.anchoredPosition=new Vector2(-24,6);last.rectTransform.sizeDelta=new Vector2(150,38);last.alignment=TextAnchor.MiddleRight;
                var latest=samples.OrderBy(s=>s.measuredAtEpochMs).Last();
                GardenTypography.SetWeight(GardenUi.FlowLabel(parent,"마지막 측정  "+latest.beatsPerMinute+" BPM",GardenUi.HeadingSize),true);
                GardenUi.FlowLabel(parent,DateTimeOffset.FromUnixTimeMilliseconds(latest.measuredAtEpochMs).ToLocalTime().ToString("MM/dd HH:mm:ss"),GardenUi.CaptionSize,GardenUi.Muted);
                var sources=GardenUi.FlowLabel(parent,string.Join("\n",samples.Select(x=>string.IsNullOrWhiteSpace(x.sourcePackage)?"출처 정보 없음":x.sourcePackage).Distinct())+"\n\n출처별로 분리해 표시하며, 측정이 없는 구간은 연결하지 않아요.",GardenUi.CaptionSize,GardenUi.Muted);
                sources.gameObject.name="Heart source details";sources.gameObject.SetActive(false);
                var disclosure=GardenUi.Button(parent,"측정 출처 보기",0,0,1,0,()=>{});
                disclosure.gameObject.AddComponent<GardenFlowButton>();
                disclosure.onClick.AddListener(()=>
                {
                    bool expanded=!sources.gameObject.activeSelf;sources.gameObject.SetActive(expanded);
                    disclosure.GetComponentInChildren<Text>().text=expanded?"측정 출처 닫기":"측정 출처 보기";
                    LayoutRebuilder.MarkLayoutForRebuild((RectTransform)parent);
                });
                disclosure.transform.SetSiblingIndex(sources.transform.GetSiblingIndex());
            }
            else GardenUi.Label(chart.transform,"측정값이 있을 때만 표시합니다",.08f,.2f,.84f,.6f,GardenUi.BodySize);
            return statusLabel;
        }

        static string LocalTime(long epoch)=>DateTimeOffset.FromUnixTimeMilliseconds(epoch).ToLocalTime().ToString("HH:mm");
        static void AxisLabel(Transform chart,string text,float height)
        {
            // Align to the padded data points, not the graph's outer rectangle.
            float bottom=54+HeartSampleGraph.PlotPadding,top=304-42-HeartSampleGraph.PlotPadding;
            float y=Mathf.Lerp(bottom,top,height);
            var label=GardenUi.Label(chart,text,0,0,0,0,GardenUi.CaptionSize);
            label.rectTransform.pivot=new Vector2(0,.5f);label.rectTransform.anchoredPosition=new Vector2(16,y);
            label.rectTransform.sizeDelta=new Vector2(68,38);label.alignment=TextAnchor.MiddleRight;label.color=GardenUi.Muted;
            var line=GardenUi.Box(chart,"Heart graph guide",0,0,1,0,GardenUi.Border);
            var rect=(RectTransform)line.transform;rect.offsetMin=new Vector2(96+HeartSampleGraph.PlotPadding,y);rect.offsetMax=new Vector2(-24-HeartSampleGraph.PlotPadding,y+1);
            line.GetComponent<Image>().raycastTarget=false;line.transform.SetAsFirstSibling();
        }

        static string StatusGuidance(string status)
        {
            switch(status)
            {
                case "AVAILABLE":return "동기화된 심박 기록이에요.";
                case "UNSUPPORTED":return "이 기기에서는 건강 기록 연결을 지원하지 않아요.";
                case "PERMISSION_REQUIRED":
                case "PERMISSION_DENIED":return "설정에서 건강 데이터 권한을 확인해 주세요.";
                case "NO_DATA":return "동기화된 기록이 없어요. 나중에 다시 조회할 수 있어요.";
                case "DISCONNECTED":return "건강 기록 연결을 확인한 뒤 다시 조회해 주세요.";
                case "SERVICE_UNAVAILABLE":return "건강 기록 서비스를 지금 사용할 수 없어요.";
                case "STALE":return "이전 조회 결과예요. 새 기록을 다시 조회해 주세요.";
                case "PARTIAL":return "일부 기록만 조회했어요. 나중에 다시 조회해 주세요.";
                default:return "조회하지 못했어요. 잠시 후 다시 시도해 주세요.";
            }
        }
    }
}
