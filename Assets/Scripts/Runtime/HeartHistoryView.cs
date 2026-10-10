using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Runtime
{
    public static class HeartHistoryView
    {
        public static Text Build(Transform parent, AndroidPlatformSnapshot snapshot, string startIso, string endIso)
        {
            var metric=snapshot.healthConnect?.heartRate ?? new HealthMetricSnapshot();
            long start=DateTimeOffset.TryParse(startIso,out var a)?a.ToUnixTimeMilliseconds():0;
            long end=DateTimeOffset.TryParse(endIso,out var b)?b.ToUnixTimeMilliseconds():0;
            bool exact=metric.queryStartEpochMs==start && metric.queryEndEpochMs==end && start>0 && end>start;
            var samples=exact?(metric.heartRateSamples ?? Array.Empty<HeartRateSampleSnapshot>()).Where(s=>s!=null && s.measuredAtEpochMs>=start && s.measuredAtEpochMs<end).ToArray():Array.Empty<HeartRateSampleSnapshot>();
            string status=!exact?"이 활동 구간은 아직 조회하지 않았어요":samples.Length==0?(metric.queryComplete?"측정 기록 없음":"조회 미완료 · 기록 유무를 확인할 수 없어요"):"심박 "+metric.sampleCount+"개 · 표시 "+samples.Length+"개";
            string mode=snapshot.inputMode=="LIVE"?"":"["+snapshot.inputMode+"] ";
            var statusLabel=GardenUi.Label(parent,mode+status+"\n"+(exact?StatusGuidance(metric.status):"아래 ‘건강 기록 다시 조회’를 눌러 주세요."),.07f,.48f,.86f,.08f,23);
            var chart=GardenUi.Card(parent,"Heart samples",.07f,.34f,.86f,.14f);
            if(samples.Length>0)
            {
                var graph=GardenUi.Box(chart.transform,"Sample graph",0,0,1,1).AddComponent<HeartSampleGraph>();graph.Samples=samples;graph.WindowStart=start;graph.WindowEnd=end;graph.raycastTarget=false;graph.SetAllDirty();
                var latest=samples.OrderBy(s=>s.measuredAtEpochMs).Last();
                GardenUi.Label(parent,latest.beatsPerMinute+" BPM · "+DateTimeOffset.FromUnixTimeMilliseconds(latest.measuredAtEpochMs).ToLocalTime().ToString("MM/dd HH:mm:ss")+"\n"+string.Join(" / ",samples.Select(x=>x.sourcePackage).Distinct())+" · 출처별 분리, 빈 구간은 비워둡니다",.07f,.24f,.86f,.09f,21);
            }
            else GardenUi.Label(chart.transform,"측정값이 있을 때만 표시합니다",.04f,.2f,.92f,.6f,25);
            return statusLabel;
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
