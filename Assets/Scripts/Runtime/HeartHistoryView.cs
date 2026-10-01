using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public static class HeartHistoryView
    {
        public static void Build(Transform parent, AndroidPlatformSnapshot snapshot, string startIso, string endIso)
        {
            var metric=snapshot.healthConnect?.heartRate ?? new HealthMetricSnapshot();
            long start=DateTimeOffset.TryParse(startIso,out var a)?a.ToUnixTimeMilliseconds():0;
            long end=DateTimeOffset.TryParse(endIso,out var b)?b.ToUnixTimeMilliseconds():0;
            bool exact=metric.queryStartEpochMs==start && metric.queryEndEpochMs==end && start>0 && end>start;
            var samples=exact?(metric.heartRateSamples ?? Array.Empty<HeartRateSampleSnapshot>()).Where(s=>s!=null && s.measuredAtEpochMs>=start && s.measuredAtEpochMs<end).ToArray():Array.Empty<HeartRateSampleSnapshot>();
            string status=!exact?"이 활동 구간은 아직 조회하지 않았어요":samples.Length==0?(metric.queryComplete?"측정 기록 없음":"조회 미완료 · 기록 유무를 확인할 수 없어요"):"심박 "+metric.sampleCount+"개 · 표시 "+samples.Length+"개";
            GardenUi.Label(parent,"["+snapshot.inputMode+"] "+status+"\n"+(metric.status+(exact?"":" · 해당 구간 조회 필요")),.07f,.48f,.86f,.08f,23);
            var chart=GardenUi.Box(parent,"Heart samples",.07f,.34f,.86f,.14f,new Color(.065f,.12f,.16f));
            if(samples.Length>0)
            {
                var graph=GardenUi.Box(chart.transform,"Sample graph",0,0,1,1).AddComponent<HeartSampleGraph>();graph.Samples=samples;graph.WindowStart=start;graph.WindowEnd=end;graph.raycastTarget=false;graph.SetAllDirty();
                var latest=samples.OrderBy(s=>s.measuredAtEpochMs).Last();
                GardenUi.Label(parent,latest.beatsPerMinute+" BPM · "+DateTimeOffset.FromUnixTimeMilliseconds(latest.measuredAtEpochMs).ToLocalTime().ToString("MM/dd HH:mm:ss")+"\n"+string.Join(" / ",samples.Select(x=>x.sourcePackage).Distinct())+" · 출처별 분리, 빈 구간은 비워둡니다",.07f,.24f,.86f,.09f,21);
            }
            else GardenUi.Label(chart.transform,"측정값이 있을 때만 표시합니다",.04f,.2f,.92f,.6f,25);
        }
    }
}
