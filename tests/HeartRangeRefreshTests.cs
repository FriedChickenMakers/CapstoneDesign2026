using System;
using CapstoneDesign.Runtime;

static class HeartRangeRefreshTests
{
    static int passed;
    static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); passed++; }
    static PlatformActionResult Accepted(long start, long end) => new PlatformActionResult { status = "AVAILABLE" };
    static AndroidPlatformSnapshot Snapshot(long revision, long start = 1000, long end = 2000, string status = "AVAILABLE", bool refreshing = false)
    {
        return new AndroidPlatformSnapshot { inputMode = "LIVE", healthConnect = new HealthConnectSnapshot {
            status = status, lastRefreshEpochMs = revision, refreshing = refreshing,
            heartRate = new HealthMetricSnapshot { status = status, lastUpdatedEpochMs = revision,
                queryStartEpochMs = start, queryEndEpochMs = end, queryComplete = status != "PARTIAL" }
        }};
    }
    public static void Main()
    {
        var query = new HeartRangeRefresh(); int requests = 0;
        Func<long,long,PlatformActionResult> request = (start,end) => { requests++; return Accepted(start,end); };
        Assert(query.Start("LIVE",1000,2000,Snapshot(10),0,request),"Request not started.");
        Assert(!query.Start("LIVE",1000,2000,Snapshot(10),1,request) && requests==1,"Repeated tap submitted duplicate request.");
        query.Tick("LIVE",Snapshot(10),1,request);
        Assert(query.IsPending && query.Result==null,"Cached matching interval falsely completed refresh.");
        var otherMetricUpdated=Snapshot(10); otherMetricUpdated.healthConnect.lastRefreshEpochMs=20;
        query.Tick("LIVE",otherMetricUpdated,1.5f,request);
        Assert(query.IsPending && query.Result==null,"Another health metric's update completed the heart query.");
        query.Tick("LIVE",Snapshot(20,refreshing:true),2,request);
        Assert(query.IsPending,"Busy snapshot completed prematurely.");
        var result = Snapshot(20); query.Tick("LIVE",result,3,request);
        Assert(!query.IsPending && ReferenceEquals(query.Result,result),"New exact result not delivered.");
        int revision = query.Revision; query.Tick("LIVE",Snapshot(30),4,request);
        Assert(query.Revision==revision && ReferenceEquals(query.Result,result),"Completed request delivered repeatedly.");

        requests=0;
        Func<long,long,PlatformActionResult> busyFirst = (start,end) => ++requests==1
            ? new PlatformActionResult { status="ERROR", errorCode="REFRESH_BUSY" } : Accepted(start,end);
        query.Start("LIVE",1000,2000,Snapshot(20),0,busyFirst);
        Assert(query.IsPending && query.Message.Contains("기다리는"),"Busy slot treated as failed request.");
        query.Tick("LIVE",Snapshot(20,refreshing:true),1,busyFirst);
        Assert(requests==1,"Retried while other query still running.");
        query.Tick("LIVE",Snapshot(30),2,busyFirst);
        Assert(requests==2 && query.IsPending,"Did not submit after previous query completed.");
        query.Tick("LIVE",Snapshot(30),3,busyFirst);
        Assert(query.IsPending,"Previous query result was mistaken for queued request.");
        query.Tick("LIVE",Snapshot(40),4,busyFirst);
        Assert(!query.IsPending && query.Result!=null && requests==2,"Queued query did not complete exactly once.");

        query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
        query.Tick("LIVE",Snapshot(20,start:3000,end:4000),1,Accepted);
        Assert(query.Failed && query.Result==null,"Different activity's result accepted.");
        query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
        query.Tick("MOCK",Snapshot(20),1,Accepted);
        Assert(query.Failed && query.Result==null,"Mode change accepted live result.");
        query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
        var wrongMode=Snapshot(20); wrongMode.inputMode="REPLAY";
        query.Tick("LIVE",wrongMode,1,Accepted);
        Assert(query.Failed && query.Result==null,"Provider mode mismatch accepted result.");
        query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
        query.Cancel(); query.Tick("LIVE",Snapshot(20),1,Accepted);
        Assert(!query.IsPending && query.Result==null,"Cancelled page received a late result.");

        query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
        query.Tick("LIVE",Snapshot(10,refreshing:true),HeartRangeRefresh.TimeoutSeconds,Accepted);
        Assert(query.Failed && !query.IsPending && query.Message.Contains("오래"),"Stuck query never timed out.");
        query.Tick("LIVE",Snapshot(20),HeartRangeRefresh.TimeoutSeconds+1,Accepted);
        Assert(query.Result==null,"Timed-out query accepted late result.");
        Assert(query.Start("LIVE",1000,2000,Snapshot(20),50,Accepted),"Could not retry after timeout.");

        foreach (string status in new[]{"ERROR","PERMISSION_DENIED","PERMISSION_REQUIRED","SERVICE_UNAVAILABLE","DISCONNECTED","UNSUPPORTED"})
        {
            query.Cancel(); query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
            query.Tick("LIVE",Snapshot(20,status:status),1,Accepted);
            Assert(query.Failed && !query.IsPending && query.Result==null,"Terminal status not surfaced: "+status);
        }
        foreach (string status in new[]{"NO_DATA","PARTIAL","STALE"})
        {
            query.Start("LIVE",1000,2000,Snapshot(10),0,Accepted);
            query.Tick("LIVE",Snapshot(20,status:status),1,Accepted);
            Assert(!query.Failed && !query.IsPending && query.Result!=null,"Valid interval status lost: "+status);
        }
        query.Start("LIVE",1000,2000,Snapshot(10),0,(start,end)=>{ throw new Exception("Synthetic bridge failure"); });
        Assert(query.Failed && !query.IsPending,"Bridge exception left query pending.");
        query.Start("LIVE",1000,2000,Snapshot(10),0,(start,end)=>null);
        Assert(query.Failed && !query.IsPending,"Null action left query pending.");
        requests=0;
        Assert(!query.Start("MOCK",1000,2000,Snapshot(10),0,request) && requests==0,"Mock mode submitted device query.");
        Assert(!query.Start("LIVE",2000,1000,Snapshot(10),0,request) && requests==0,"Invalid range submitted device query.");
        Assert(!query.Start("LIVE",1000,1000+8L*86400000,Snapshot(10),0,request) && requests==0,"Oversized range submitted device query.");
        Console.WriteLine("Heart range refresh: "+passed+" assertions passed.");
    }
}
