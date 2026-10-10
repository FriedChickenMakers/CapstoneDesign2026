using System;

namespace CapstoneDesign.Runtime
{
    // Health range queries share one native slot. Correlate the returned interval
    // and its query timestamp; an old cached graph must not complete a new request.
    public sealed class HeartRangeRefresh
    {
        public const float TimeoutSeconds = 45;
        public bool IsPending { get; private set; }
        public bool Failed { get; private set; }
        public string Message { get; private set; } = "";
        public AndroidPlatformSnapshot Result { get; private set; }
        public int Revision { get; private set; }
        long start, end, previousMetricUpdate;
        float deadline;
        bool waitingForSlot;
        string requestMode;

        public bool Start(string mode, long startEpochMs, long endEpochMs, AndroidPlatformSnapshot before,
            float now, Func<long, long, PlatformActionResult> request)
        {
            if (IsPending) return false;
            Cancel();
            if (mode != "LIVE") { Fail("예시·재생 모드에서는 준비된 측정 기록을 보여줘요."); return false; }
            if (startEpochMs <= 0 || endEpochMs <= startEpochMs || endEpochMs - startEpochMs > 7L * 86400000)
            { Fail("조회할 활동 시간이 없거나 7일을 넘었어요."); return false; }
            start = startEpochMs; end = endEpochMs; requestMode = mode;
            deadline = now + TimeoutSeconds; IsPending = true;
            Submit(before, request);
            return IsPending;
        }

        public void Tick(string mode, AndroidPlatformSnapshot snapshot, float now,
            Func<long, long, PlatformActionResult> request)
        {
            if (!IsPending) return;
            if (mode != requestMode || (snapshot != null && snapshot.inputMode != requestMode))
            { Fail("데이터 모드가 바뀌었어요. 기록을 다시 열어 주세요."); return; }
            if (now >= deadline) { Fail("조회가 오래 걸리고 있어요. 잠시 후 다시 조회해 주세요."); return; }
            var health = snapshot?.healthConnect;
            if (health == null) { Fail("건강 기록을 확인하지 못했어요. 다시 조회해 주세요."); return; }
            if (health.refreshing) return;
            if (waitingForSlot) { Submit(snapshot, request); return; }
            var metric = health.heartRate;
            string failure = FailureMessage(metric?.status) ?? FailureMessage(health.status);
            if (failure != null) { Fail(failure); return; }
            bool fresh = metric != null && metric.lastUpdatedEpochMs > previousMetricUpdate;
            if (!fresh) return;
            if (metric.queryStartEpochMs != start || metric.queryEndEpochMs != end)
            { Fail("다른 구간의 조회 결과가 도착했어요. 다시 조회해 주세요."); return; }
            Result = snapshot; IsPending = false; Message = ""; Revision++;
        }

        void Submit(AndroidPlatformSnapshot before, Func<long, long, PlatformActionResult> request)
        {
            previousMetricUpdate = before?.healthConnect?.heartRate?.lastUpdatedEpochMs ?? 0;
            PlatformActionResult action;
            try { action = request(start, end); }
            catch { Fail("건강 기록 조회를 시작하지 못했어요. 다시 시도해 주세요."); return; }
            if (action?.errorCode == "REFRESH_BUSY")
            {
                waitingForSlot = true;
                SetMessage("다른 건강 기록 조회를 기다리는 중이에요.");
            }
            else if (action?.ParsedStatus == PlatformStatus.Available)
            {
                waitingForSlot = false;
                SetMessage("이 활동의 심박 기록을 조회하고 있어요.");
            }
            else Fail(FailureMessage(action?.status) ?? "건강 기록 조회를 시작하지 못했어요. 다시 시도해 주세요.");
        }

        public void Cancel()
        {
            IsPending = false; Failed = false; Result = null; waitingForSlot = false;
            Message = ""; Revision++;
        }

        void Fail(string message) { IsPending = false; Failed = true; Result = null; SetMessage(message); }
        void SetMessage(string value) { if (Message == value) return; Message = value; Revision++; }
        static string FailureMessage(string status)
        {
            switch (status)
            {
                case "PERMISSION_REQUIRED": case "PERMISSION_DENIED": return "설정에서 건강 데이터 권한을 확인해 주세요.";
                case "UNSUPPORTED": return "이 기기에서는 건강 기록 연결을 지원하지 않아요.";
                case "DISCONNECTED": case "SERVICE_UNAVAILABLE": return "건강 기록 연결을 확인한 뒤 다시 조회해 주세요.";
                case "ERROR": return "건강 기록을 조회하지 못했어요. 잠시 후 다시 시도해 주세요.";
                default: return null;
            }
        }
    }
}
