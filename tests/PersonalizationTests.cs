using System;
using System.IO;
using System.Linq;
using CapstoneDesign.Runtime.Personalization;

static class PersonalizationTests
{
    sealed class Store : IEventQueueStore
    {
        public bool Fail;
        public EventQueueState Value = new EventQueueState();
        public EventQueueState Load() => Value;
        public void Save(EventQueueState value) { if(Fail)throw new IOException("Synthetic persistence failure"); Value=value; }
    }
    static int assertions;
    static void Check(bool condition,string message) { assertions++;if(!condition)throw new Exception(message); }
    static void Throws(Action action,string message) { bool threw=false;try{action();}catch(Exception){threw=true;}Check(threw,message); }
    static BehaviorEvent Event(long now) => new BehaviorEvent { event_id=Guid.NewGuid().ToString("N"),decision_id=Guid.NewGuid().ToString("N"),
        kind="recommendation_shown",activity_id="course:MBCT2W01",policy_id="local-rules-v1",app_version="synthetic-test",content_version="courses-v1",
        occurred_utc_ms=now,utc_offset_minutes=540,features=new float[]{1,.5f,.25f,1} };
    static void Main()
    {
        long now=1800000000000;
        var store=new Store();var queue=new EventQueue(store);
        Check(!queue.Enabled && !queue.Add(Event(now)),"Default consent must be off");
        Throws(()=>queue.SetConsent(true),"Cannot consent without enrollment");
        queue.Bind("synthetic-enrollment");store.Fail=true;
        Throws(()=>queue.SetConsent(true),"Consent save failure visible");
        Check(!queue.Enabled,"Failed consent save cannot enable collection");store.Fail=false;
        queue.SetConsent(true);var item=Event(now);Check(queue.Add(item),"Consented event persisted");
        item.features[0]=0;Check(queue.Batch(now)[0].features[0]==1,"External arrays cannot mutate queue");
        Check(!queue.Add(item),"Duplicate identity rejected");
        var sent=queue.Batch(now);sent[0].features[0]=0;
        Check(queue.Batch(now)[0].features[0]==1,"Batch is a defensive snapshot");
        Throws(()=>queue.Acknowledge(new[]{Guid.NewGuid().ToString("N")},sent.Select(e=>e.event_id)),"Unsent ack cannot erase queue");
        store.Fail=true;Throws(()=>queue.Acknowledge(sent.Select(e=>e.event_id),sent.Select(e=>e.event_id)),"Failed ack persistence reported");
        Check(queue.Count==1,"Failed ack retained for retry");store.Fail=false;
        queue.Acknowledge(sent.Select(e=>e.event_id),sent.Select(e=>e.event_id));Check(queue.Count==0,"Only confirmed events removed");
        Check(!queue.Add(item),"Acknowledged event is not re-enqueued after recovery");
        var resumed=new EventQueue(store);Check(!resumed.Add(item),"Recorded identities survive process restart");
        queue.Add(Event(now));Throws(()=>queue.Bind("different-synthetic-enrollment"),"Identity cannot switch while active");
        queue.SetConsent(false);Check(queue.Count==0 && !queue.Enabled && queue.DeletePending,"Withdrawal clears local queue and schedules server deletion");
        queue.Acknowledge(sent.Select(e=>e.event_id),sent.Select(e=>e.event_id));Check(queue.Count==0,"Late post ack cannot recreate withdrawn data");
        Throws(()=>queue.SetConsent(true),"Deletion cannot be bypassed by consent toggle");
        var reloaded=new EventQueue(store);Check(reloaded.DeletePending,"Deletion survives process restart");
        reloaded.CompleteDeletion();Check(!reloaded.DeletePending && reloaded.EnrollmentRevoked,"Deletion revokes old enrollment");
        Throws(()=>reloaded.SetConsent(true),"Revoked token needs fresh enrollment");
        reloaded.Bind("new-synthetic-enrollment");reloaded.SetConsent(true);Check(reloaded.Enabled,"Fresh enrollment can opt in");
        Check(!reloaded.Add(Event(reloaded.ConsentStartedUtcMs-1)),"Consent does not upload earlier history");
        for(int i=0;i<EventQueue.Capacity;i++)Check(reloaded.Add(Event(now)),"Bounded queue add");
        Check(!reloaded.Add(Event(now)) && reloaded.Count==EventQueue.Capacity,"Full queue does not erase pending exposure");
        Check(reloaded.Batch(now).Length==100,"Batch limit matches API");
        Check(reloaded.Batch(now+91L*86400000).Length==0,"Expired events removed before upload");
        var invalid=Event(now);invalid.features[0]=float.NaN;Check(!reloaded.Add(invalid),"NaN rejected");
        invalid=Event(now);invalid.activity_id="synthetic free text";Check(!reloaded.Add(invalid),"Free-text catalog identifiers rejected");
        var policy=new ResponsePolicy();var features=new float[]{1,.5f,.25f,1};
        Check(Math.Abs(policy.Score(features,now)-.7875)<.00001,"Rule score matches server baseline");
        policy.model_type="logistic";policy.training_rows=40;policy.expires_utc_ms=now+1000;policy.weights=new float[]{1,2,3,4};
        Check(policy.Valid(now),"Versioned policy accepted");
        Check(Math.Abs(policy.Score(features,now)-1/(1+Math.Exp(-6.75)))<.000001,"Logistic inference parity");
        Check(Math.Abs(policy.Score(features,now+1001)-.7875)<.00001,"Expired policy falls back locally");
        policy.feature_names[0]="unexpected";Check(!policy.Valid(now),"Feature order/schema enforced");
        policy.feature_names[0]="preference";policy.policy_id="invalid free text";Check(!policy.Valid(now),"Policy identifier contract rejects arbitrary text");
        string directory=Path.Combine(Path.GetTempPath(),"garden-synthetic-"+Guid.NewGuid().ToString("N"));
        try
        {
            var file=new FileEventQueueStore(Path.Combine(directory,"events.xml"));var persistent=new EventQueue(file);
            persistent.Bind("file-synthetic-enrollment");persistent.SetConsent(true);persistent.Add(Event(now));
            Check(new EventQueue(file).Count==1,"Atomic file round trip");persistent.SetConsent(false);
            Check(new EventQueue(file).DeletePending,"Atomic withdrawal survives reload");
            File.WriteAllText(Path.Combine(directory,"events.xml"),"not XML");
            Throws(()=>new EventQueue(file),"Corruption must fail closed");
            Check(File.ReadAllText(Path.Combine(directory,"events.xml"))=="not XML","Corrupt data preserved for recovery");
        }
        finally { if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("Personalization contracts: "+assertions+" assertions PASS");
    }
}
