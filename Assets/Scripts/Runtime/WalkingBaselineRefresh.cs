using System;

namespace CapstoneDesign.Runtime
{
    // Entry/focus events may repeat. Share an existing native refresh and bound both waiting
    // and retries; a failed refresh must leave the default walking goal available.
    public sealed class WalkingBaselineRefresh
    {
        public const float TimeoutSeconds=15, RetryDelaySeconds=120;
        public bool IsPending { get; private set; }
        public bool Failed { get; private set; }
        float deadline, retryAfter;
        long previousRefresh, previousBaseline;
        string previousStatus;
        bool submitted;

        public void Request(string mode,HealthConnectSnapshot health,DateTimeOffset now,TimeZoneInfo zone,
            float elapsed,Func<PlatformActionResult> refresh)
        {
            if(mode!="LIVE") { IsPending=false;return; }
            if(WalkingPersonalization.IsFresh(health?.personalization,now,zone))
            { IsPending=false;Failed=false;retryAfter=0;return; }
            if(IsPending || elapsed<retryAfter)return;
            IsPending=true;Failed=false;submitted=false;deadline=elapsed+TimeoutSeconds;
            Remember(health);
            // Avoid queueing another whole Health Connect read behind initialization/settings.
            if(health?.refreshing!=true)Submit(health,elapsed,refresh);
        }

        public void Tick(string mode,HealthConnectSnapshot health,DateTimeOffset now,TimeZoneInfo zone,
            float elapsed,Func<PlatformActionResult> refresh)
        {
            if(!IsPending)return;
            if(mode!="LIVE") { IsPending=false;return; }
            if(WalkingPersonalization.IsFresh(health?.personalization,now,zone))
            { IsPending=false;Failed=false;retryAfter=0;return; }
            if(elapsed>=deadline) { Fail(elapsed);return; }
            if(health?.refreshing==true)return;
            if(!submitted)
            {
                if(Changed(health))Fail(elapsed);else Submit(health,elapsed,refresh);
                return;
            }
            // Permission denial and no-data results still complete the attempt. A cached
            // snapshot from before submission cannot release the Start button as a new result.
            if(Changed(health))Fail(elapsed);
        }

        void Submit(HealthConnectSnapshot before,float elapsed,Func<PlatformActionResult> refresh)
        {
            submitted=true;Remember(before);
            try { if(refresh()?.status!="AVAILABLE")Fail(elapsed); }
            catch { Fail(elapsed); }
        }
        void Remember(HealthConnectSnapshot before)
        {
            previousRefresh=before?.lastRefreshEpochMs??0;
            previousBaseline=before?.personalization?.lastUpdatedEpochMs??0;
            previousStatus=before?.status;
        }
        bool Changed(HealthConnectSnapshot health) => health==null || health.lastRefreshEpochMs!=previousRefresh ||
            (health.personalization?.lastUpdatedEpochMs??0)!=previousBaseline || health.status!=previousStatus;
        void Fail(float elapsed) { IsPending=false;Failed=true;retryAfter=elapsed+RetryDelaySeconds; }
    }
}
