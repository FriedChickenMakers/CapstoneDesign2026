using System;
using UnityEngine;
using UnityEngine.UI;
using CapstoneDesign.Runtime.LocalState;

namespace CapstoneDesign.Runtime
{
    public sealed partial class WeekOneQuestDemo
    {
        readonly HeartRangeRefresh recordHeartRefresh = new HeartRangeRefresh();
        GameObject recordHeartView;
        Transform recordHeartPage;
        Text recordHeartStatus;
        Button recordHeartButton;
        string recordHeartSession, recordHeartStart, recordHeartEnd;
        float nextRecordHeartPoll;
        int recordHeartRevision;

        AndroidPlatformSnapshot ReadRecordHealth() => preview && PreviewHealth != null
            ? PreviewHealth : AndroidPlatformBridge.GetSnapshot();
        string CurrentHeartMode => preview ? "MOCK" : AndroidPlatformBridge.InputMode.ToString().ToUpperInvariant();

        void BuildRecordHeart(Transform parent, ActivitySession session)
        {
            recordHeartPage = parent; recordHeartSession = session.SessionId;
            recordHeartStart = session.StartedUtc; recordHeartEnd = session.EndedUtc;
            RenderRecordHeart(ReadRecordHealth());
            recordHeartButton = GardenUi.Button(parent,"건강 기록 다시 조회",.07f,.15f,.86f,.075f,StartRecordHeartRefresh);
            recordHeartButton.interactable = CurrentHeartMode == "LIVE";
        }

        void RenderRecordHeart(AndroidPlatformSnapshot snapshot)
        {
            if (recordHeartView != null)
            {
                recordHeartView.SetActive(false);
                if (Application.isPlaying) Destroy(recordHeartView); else DestroyImmediate(recordHeartView);
            }
            recordHeartView = GardenUi.Box(recordHeartPage,"Activity heart history",0,0,1,1);
            // Keep the graph behind the persistent action controls when replacing it.
            recordHeartView.transform.SetAsFirstSibling();
            recordHeartStatus = HeartHistoryView.Build(recordHeartView.transform,
                snapshot ?? new AndroidPlatformSnapshot(),recordHeartStart,recordHeartEnd);
        }

        void StartRecordHeartRefresh()
        {
            if (screen != "record" || recordHeartPage == null || recordHeartRefresh.IsPending) return;
            long start = DateTimeOffset.TryParse(recordHeartStart,out var a) ? a.ToUnixTimeMilliseconds() : 0;
            long end = DateTimeOffset.TryParse(recordHeartEnd,out var b) ? b.ToUnixTimeMilliseconds() : 0;
            recordHeartRefresh.Start(CurrentHeartMode,start,end,ReadRecordHealth(),Time.realtimeSinceStartup,
                AndroidPlatformBridge.RefreshHeartRange);
            nextRecordHeartPoll = Time.realtimeSinceStartup + .35f;
            ApplyRecordHeartRefresh();
        }

        void TickRecordHeartRefresh()
        {
            if (!recordHeartRefresh.IsPending || Time.realtimeSinceStartup < nextRecordHeartPoll) return;
            if (screen != "record" || recordHeartPage == null || string.IsNullOrEmpty(recordHeartSession))
            { ClearRecordHeartRefresh(); return; }
            nextRecordHeartPoll = Time.realtimeSinceStartup + .35f;
            recordHeartRefresh.Tick(CurrentHeartMode,ReadRecordHealth(),Time.realtimeSinceStartup,
                AndroidPlatformBridge.RefreshHeartRange);
            ApplyRecordHeartRefresh();
        }

        void ApplyRecordHeartRefresh()
        {
            if (recordHeartRevision == recordHeartRefresh.Revision || recordHeartPage == null) return;
            recordHeartRevision = recordHeartRefresh.Revision;
            if (recordHeartRefresh.Result != null) RenderRecordHeart(recordHeartRefresh.Result);
            else if (recordHeartStatus != null && !string.IsNullOrEmpty(recordHeartRefresh.Message))
            {
                recordHeartStatus.text = recordHeartRefresh.Message;
                recordHeartStatus.color = recordHeartRefresh.Failed ? GardenUi.Warning : GardenUi.Muted;
            }
            if (recordHeartButton != null)
                recordHeartButton.interactable = !recordHeartRefresh.IsPending && CurrentHeartMode == "LIVE";
        }

        void ClearRecordHeartRefresh()
        {
            recordHeartRefresh.Cancel(); recordHeartSession = null;
            recordHeartView = null; recordHeartPage = null; recordHeartStatus = null; recordHeartButton = null;
        }

        void OnDisable()
        {
            if (!recordHeartRefresh.IsPending) return;
            recordHeartRefresh.Cancel();
            if (recordHeartStatus != null) recordHeartStatus.text = "조회 화면을 나갔어요. 다시 조회할 수 있어요.";
            if (recordHeartButton != null) recordHeartButton.interactable = CurrentHeartMode == "LIVE";
        }
    }
}
