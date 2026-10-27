using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace CapstoneDesign.Runtime.Personalization
{
    public static class RecommendationFeatures
    {
        public static float Preference(int family,string title,string preferred)
        {
            if(string.IsNullOrWhiteSpace(preferred))return 0;
            preferred=preferred.Trim();
            if(preferred==title)return 1;
            return family==2 && preferred=="몸 살펴보기" || family==4 && preferred=="호흡 관찰"
                || family==3 && preferred=="일상 행동" || family==8 && preferred=="잠시 멈추기" ? 1 : 0;
        }
        public static float DurationFit(int suggestedSeconds,int availableMinutes)
        {
            if(suggestedSeconds<=0)return .5f;
            availableMinutes=Math.Max(1,Math.Min(30,availableMinutes));
            return Math.Min(1,availableMinutes*60f/suggestedSeconds);
        }
    }
    // Fixed, bounded numeric features only. No free text, raw health values or
    // device identifiers belong in this contract.
    [Serializable] public sealed class BehaviorEvent
    {
        public string event_id, decision_id, kind, activity_id, policy_id, app_version, content_version;
        public long occurred_utc_ms;
        public int utc_offset_minutes;
        public float[] features;
    }
    [Serializable] public sealed class BehaviorBatch
    {
        public int schema_version = 1, consent_version = 1;
        public BehaviorEvent[] events;
    }
    [Serializable] public sealed class BehaviorAcknowledgement { public string[] accepted_event_ids; }
    [Serializable] public sealed class DeletionAcknowledgement { public bool deleted, enrollment_revoked; }
    [Serializable] public sealed class ResponsePolicy
    {
        public int schema_version = 1, training_rows;
        public string policy_id = "local-rules-v1", model_type = "rules";
        public string[] feature_names = { "preference", "completion_rate", "days_since", "duration_fit" };
        public float[] weights = new float[4];
        public float intercept;
        public long expires_utc_ms;
        public bool Valid(long now)
        {
            return schema_version == 1 && policy_id != null && System.Text.RegularExpressions.Regex.IsMatch(policy_id,@"\A[A-Za-z0-9._-]{1,48}\z")
                && feature_names != null && feature_names.SequenceEqual(new[] { "preference", "completion_rate", "days_since", "duration_fit" })
                && weights != null && weights.Length == 4 && weights.All(w => Finite(w) && Math.Abs(w) <= 20)
                && Finite(intercept) && Math.Abs(intercept) <= 20 && training_rows >= 0
                && (model_type == "rules" || model_type == "logistic" && training_rows >= 20 && expires_utc_ms > now);
        }
        internal static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        public static bool ValidFeatures(float[] values) => values != null && values.Length == 4 && values.All(v => Finite(v) && v >= 0 && v <= 1);
        public double Score(float[] values, long now)
        {
            if (!ValidFeatures(values)) throw new ArgumentException("Four bounded decision-time features required.");
            if (!Valid(now) || model_type == "rules") return Math.Min(.99, .15 + .30 * values[0] + .20 * values[1] + .15 * values[2] + .20 * values[3]);
            double sum = intercept;
            for (int i = 0; i < 4; i++) sum += weights[i] * values[i];
            return 1 / (1 + Math.Exp(-Math.Max(-40, Math.Min(40, sum))));
        }
    }
    public sealed class EventQueueState
    {
        public int SchemaVersion = 1;
        public bool Enabled, DeletePending, EnrollmentRevoked;
        public string EnrollmentFingerprint = "";
        public long ConsentStartedUtcMs, MeasurementConsentUtcMs;
        public List<BehaviorEvent> Events = new List<BehaviorEvent>();
        public List<EventReceipt> Recorded = new List<EventReceipt>();
    }
    public sealed class EventReceipt { public string Id; public long OccurredUtcMs; }
    public interface IEventQueueStore { EventQueueState Load(); void Save(EventQueueState state); }
    public sealed class FileEventQueueStore : IEventQueueStore
    {
        readonly string path;
        public FileEventQueueStore(string path) { this.path = path; }
        public EventQueueState Load()
        {
            if (!File.Exists(path)) return new EventQueueState();
            using (var reader = System.Xml.XmlReader.Create(path, new System.Xml.XmlReaderSettings
            { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
                return (EventQueueState)new XmlSerializer(typeof(EventQueueState)).Deserialize(reader);
        }
        public void Save(EventQueueState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new XmlSerializer(typeof(EventQueueState)).Serialize(stream, state);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
    public sealed class EventQueue
    {
        public const int Capacity = 256;
        readonly IEventQueueStore store;
        EventQueueState state;
        public bool Enabled => state.Enabled;
        public bool DeletePending => state.DeletePending;
        public bool EnrollmentRevoked => state.EnrollmentRevoked;
        public int Count => state.Events.Count;
        public long ConsentStartedUtcMs => state.ConsentStartedUtcMs;
        public long MeasurementConsentUtcMs => state.MeasurementConsentUtcMs;
        public EventQueue(IEventQueueStore store)
        {
            this.store = store;
            state = store.Load() ?? throw new InvalidDataException("Event queue missing.");
            if (state.SchemaVersion != 1 || state.Events == null || state.Events.Count > Capacity || state.Recorded == null
                || state.Recorded.Count>8192 || state.Recorded.Any(r=>r==null || !Identifier(r.Id) || r.OccurredUtcMs<0)
                || state.Recorded.Select(r=>r.Id).Distinct().Count()!=state.Recorded.Count
                || state.Events.Any(e => !ValidEvent(e)) || state.Events.Select(e => e.event_id).Distinct().Count() != state.Events.Count
                || state.MeasurementConsentUtcMs < 0 || state.MeasurementConsentUtcMs > 0 && (!state.Enabled || state.MeasurementConsentUtcMs < state.ConsentStartedUtcMs)
                || state.ConsentStartedUtcMs < 0
                || state.Enabled && (state.ConsentStartedUtcMs == 0 || string.IsNullOrEmpty(state.EnrollmentFingerprint)
                    || state.Events.Any(e=>e.occurred_utc_ms<state.ConsentStartedUtcMs)
                    || state.Recorded.Any(r=>r.OccurredUtcMs<state.ConsentStartedUtcMs))
                || !state.Enabled && (state.Events.Count>0 || state.Recorded.Count>0)
                || (state.DeletePending || state.EnrollmentRevoked) && state.Enabled
                || state.DeletePending && state.EnrollmentRevoked)
                throw new InvalidDataException("Event queue invalid; preserved without upload.");
        }
        static BehaviorEvent Clone(BehaviorEvent e) => new BehaviorEvent { event_id=e.event_id, decision_id=e.decision_id,
            kind=e.kind, activity_id=e.activity_id, policy_id=e.policy_id, app_version=e.app_version, content_version=e.content_version,
            occurred_utc_ms=e.occurred_utc_ms, utc_offset_minutes=e.utc_offset_minutes, features=(float[])e.features.Clone() };
        EventQueueState Copy() => new EventQueueState { Enabled=state.Enabled, DeletePending=state.DeletePending,
            EnrollmentRevoked=state.EnrollmentRevoked, EnrollmentFingerprint=state.EnrollmentFingerprint, ConsentStartedUtcMs=state.ConsentStartedUtcMs, MeasurementConsentUtcMs=state.MeasurementConsentUtcMs,
            Events=state.Events.Select(Clone).ToList(),Recorded=state.Recorded.Select(r=>new EventReceipt {Id=r.Id,OccurredUtcMs=r.OccurredUtcMs}).ToList() };
        void Commit(EventQueueState next) { store.Save(next); state = next; }
        public bool BoundTo(string fingerprint) => !string.IsNullOrEmpty(fingerprint) && state.EnrollmentFingerprint == fingerprint;
        public void Bind(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint)) throw new ArgumentException("Enrollment fingerprint required.");
            if (BoundTo(fingerprint)) return;
            if (state.Enabled || state.DeletePending || state.Events.Count > 0) throw new InvalidOperationException("Finish withdrawal before replacing enrollment.");
            var next = Copy(); next.EnrollmentFingerprint = fingerprint; next.EnrollmentRevoked = false; Commit(next);
        }
        public void SetConsent(bool enabled,long now=0)
        {
            if (enabled == state.Enabled) return;
            if (enabled && (state.DeletePending || state.EnrollmentRevoked || string.IsNullOrEmpty(state.EnrollmentFingerprint)))
                throw new InvalidOperationException("An active enrollment is required.");
            var next = Copy(); next.Enabled = enabled;
            if (enabled) next.ConsentStartedUtcMs=now>0?now:DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!enabled) { next.MeasurementConsentUtcMs=0; next.Events.Clear(); next.Recorded.Clear(); next.DeletePending = true; }
            Commit(next);
        }
        public void EnableMeasurements(long now)
        {
            if (!state.Enabled || now < state.ConsentStartedUtcMs) throw new InvalidOperationException("Active explicit consent required.");
            if (state.MeasurementConsentUtcMs > 0) return;
            var next=Copy(); next.MeasurementConsentUtcMs=now; Commit(next);
        }
        public void CompleteDeletion()
        {
            if (!state.DeletePending) return;
            var next = Copy(); next.DeletePending = false; next.EnrollmentRevoked = true; Commit(next);
        }
        public bool Add(BehaviorEvent item)
        {
            if (!state.Enabled || !ValidEvent(item) || item.occurred_utc_ms<state.ConsentStartedUtcMs) return false;
            if (state.Events.Any(e => e.event_id == item.event_id) || state.Recorded.Any(r=>r.Id==item.event_id)) return false;
            // A full queue drops the new event rather than silently removing an
            // exposure whose outcome may already be queued. No outcome inference
            // is made for an exposure never uploaded.
            if (state.Events.Count >= Capacity || state.Recorded.Count>=8192) return false;
            var next = Copy(); next.Events.Add(Clone(item)); next.Recorded.Add(new EventReceipt {Id=item.event_id,OccurredUtcMs=item.occurred_utc_ms});Commit(next); return true;
        }
        public BehaviorEvent[] Batch(long now)
        {
            if (!state.Enabled) return Array.Empty<BehaviorEvent>();
            var next = Copy(); next.Events.RemoveAll(e => e.occurred_utc_ms < now - 90L*86400000 || e.occurred_utc_ms > now + 300000);
            next.Recorded.RemoveAll(r=>r.OccurredUtcMs<now-90L*86400000);
            if (next.Events.Count != state.Events.Count || next.Recorded.Count!=state.Recorded.Count) Commit(next);
            return state.Events.Take(100).Select(Clone).ToArray();
        }
        public void Acknowledge(IEnumerable<string> ids, IEnumerable<string> sent)
        {
            if (!state.Enabled) return;
            var allowed = new HashSet<string>(sent); var accepted = new HashSet<string>(ids ?? Array.Empty<string>());
            if (!accepted.IsSubsetOf(allowed)) throw new InvalidDataException("Acknowledgement includes unsent events.");
            var next = Copy(); next.Events.RemoveAll(e => accepted.Contains(e.event_id)); Commit(next);
        }
        public static string Fingerprint(string endpoint, string token)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(endpoint + "\n" + token))).Replace("-", "").ToLowerInvariant();
        }
        static readonly HashSet<string> Kinds = new HashSet<string> { "recommendation_shown", "activity_started", "activity_completed", "activity_ended",
            "notification_eligible", "notification_scheduled", "notification_post_accepted", "notification_blocked", "notification_failed", "notification_opened" };
        static bool Version(string s) => s != null && System.Text.RegularExpressions.Regex.IsMatch(s, @"\A[A-Za-z0-9._-]{1,48}\z");
        static bool Identifier(string value)
        {
            Guid parsed;
            return Guid.TryParseExact(value,"N",out parsed) && value==parsed.ToString("N");
        }
        public static bool ValidEvent(BehaviorEvent e)
        {
            return e != null && Identifier(e.event_id) && Identifier(e.decision_id)
                && Kinds.Contains(e.kind ?? "") && e.activity_id != null
                && System.Text.RegularExpressions.Regex.IsMatch(e.activity_id, @"\A(course:MBCT(2W|4W)?[0-9]{2}|free-mission:M[0-9]{2}|notification)\z")
                && Version(e.policy_id) && Version(e.app_version) && Version(e.content_version)
                && e.occurred_utc_ms >= 0 && e.utc_offset_minutes >= -840 && e.utc_offset_minutes <= 840 && ResponsePolicy.ValidFeatures(e.features);
        }
    }
}
