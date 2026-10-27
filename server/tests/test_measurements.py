"""Synthetic fixtures only: consent schema, upload, deletion and honest analysis."""
import copy
import json
from pathlib import Path
import secrets
import sqlite3
import tempfile
import threading
from urllib.request import Request, urlopen
import unittest
import uuid

from server.service import EventService, ApiError, make_server
from server.analyze_measurements import analyze, DAY

NOW=1800000000000


def event(kind="notification_post_accepted", at=NOW-2*DAY, notification=None):
    return dict(event_id=uuid.uuid4().hex, notification_id=notification or uuid.uuid4().hex, session_key="", kind=kind,
                occurred_utc_ms=at, elapsed_ms=100000, boot_count=2, utc_offset_minutes=540,
                scheduled_utc_ms=at-60000, policy_id="daily-adaptive-v2", preferred_minute=1200,
                effective_minute=1190, learned_samples=6, reason="none", activity_id="", app_version="synthetic-test", dataset="synthetic")


def pair(delay=300000):
    post=event(); click=dict(post,event_id=uuid.uuid4().hex,kind="notification_opened",occurred_utc_ms=post["occurred_utc_ms"]+delay,elapsed_ms=post["elapsed_ms"]+delay)
    return post,click


class MeasurementTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.token=secrets.token_urlsafe(32);self.subject=uuid.uuid4().hex;self.collection=uuid.uuid4().hex
        self.service=EventService(Path(self.temp.name)/"synthetic.sqlite",{self.token:self.subject},clock=lambda:NOW/1000)
        self.auth="Bearer "+self.token
    def body(self,events):
        return dict(schema_version=1,consent_version=2,collection_id=self.collection,dropped_attempts=0,expired_events=0,storage_error=False,
                    complete_through_utc_ms=NOW,events=events)
    def post(self,body):return self.service.handle("POST","/measurements/batch",self.auth,body)
    def test_retry_and_conflict_are_atomic(self):
        post,click=pair();body=self.body([post,click]);self.assertEqual(self.post(body),self.post(body))
        with self.service.connect() as db:self.assertEqual(db.execute("SELECT COUNT(*) FROM measurements").fetchone()[0],2)
        with self.assertRaises(ApiError):self.post(self.body([event(),dict(post,elapsed_ms=8)]))
        with self.service.connect() as db:self.assertEqual(db.execute("SELECT COUNT(*) FROM measurements").fetchone()[0],2)
    def test_event_cannot_move_to_another_consent_collection(self):
        item=event();self.post(self.body([item]))
        with self.assertRaises(ApiError) as caught:
            self.post(dict(self.body([item]),collection_id=uuid.uuid4().hex))
        self.assertEqual(caught.exception.status,409)
    def test_extra_fields_types_and_old_consent_rejected(self):
        for key,value in [("note","synthetic text"),("occurred_utc_ms",True),("elapsed_ms",-2),("kind",[]),("reason",{}),
                          ("dataset",[]),("notification_id","invalid"),("activity_id","private note"),("utc_offset_minutes",999)]:
            with self.subTest(key=key),self.assertRaises(ApiError):self.post(self.body([dict(event(),**{key:value})]))
        for key,value in [("consent_version",1),("collection_id","invalid"),("dropped_attempts",-1),("storage_error",1),
                          ("complete_through_utc_ms",True),("complete_through_utc_ms",-1),("complete_through_utc_ms",NOW+300001)]:
            with self.subTest(key=key),self.assertRaises(ApiError):self.post(dict(self.body([]),**{key:value}))
    def test_quality_survives_empty_batch_and_reordered_upload(self):
        self.post(dict(self.body([]),dropped_attempts=3,storage_error=True));self.post(self.body([]))
        self.post(dict(self.body([]),complete_through_utc_ms=0))
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT dropped,storage_error,complete_through FROM measurement_quality").fetchone(),(3,1,NOW))
    def test_partial_backlog_never_confirms_pending_click_as_missing(self):
        post,click=pair();unclicked=event()
        first=[post,unclicked]+[event() for _ in range(98)]
        self.post(dict(self.body(first),complete_through_utc_ms=0))
        def result():
            with self.service.connect() as db:
                records=[(s,c,json.loads(p)) for s,c,p in db.execute("SELECT subject,collection_id,payload FROM measurements")]
                quality={(s,c):dict(updated=t,complete_through=w) for s,c,t,w in db.execute(
                    "SELECT subject,collection_id,updated,complete_through FROM measurement_quality")}
            return analyze(records,quality,NOW,"synthetic")
        report,rows=result()
        self.assertEqual({r["outcome"] for r in rows},{"awaiting_upload_confirmation"})
        self.assertEqual(report["policies"]["daily-adaptive-v2"]["closed_complete_posts"],0)
        # The final batch includes every remaining pending event at this boundary.
        self.post(self.body([click]))
        report,rows=result()
        self.assertEqual(next(r for r in rows if r["notification_id"]==post["notification_id"])["outcome"],"clicked")
        self.assertEqual(next(r for r in rows if r["notification_id"]==unclicked["notification_id"])["outcome"],"no_observed_click")
        self.assertEqual(report["policies"]["daily-adaptive-v2"]["click_rate_within_window"],.01)
    def test_legacy_client_receipt_is_not_a_completion_watermark(self):
        body=self.body([event()]);del body["complete_through_utc_ms"]
        self.post(body)
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT updated,complete_through FROM measurement_quality").fetchone(),(NOW,0))
    def test_old_database_migration_preserves_quality_without_inventing_completion(self):
        database=Path(self.temp.name)/"old-schema.sqlite"
        with sqlite3.connect(database) as db:
            db.execute("CREATE TABLE measurement_quality (subject TEXT, collection_id TEXT, dropped INTEGER, expired INTEGER, storage_error INTEGER, updated INTEGER, PRIMARY KEY(subject,collection_id))")
            db.execute("INSERT INTO measurement_quality VALUES (?,?,?,?,?,?)",(self.subject,self.collection,3,0,1,NOW))
        migrated=EventService(database,{self.token:self.subject},clock=lambda:NOW/1000)
        with migrated.connect() as db:
            self.assertEqual(db.execute("SELECT dropped,storage_error,updated,complete_through FROM measurement_quality").fetchone(),(3,1,NOW,0))
    def test_activity_shape_and_deletion_cover_both_tables(self):
        e=dict(event("activity_started"),notification_id="",session_key=uuid.uuid4().hex,activity_id="course:MBCT2W01",elapsed_ms=-1,boot_count=-1)
        self.post(self.body([e]));self.service.handle("DELETE","/participant",self.auth)
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM measurements").fetchone()[0],0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM measurement_quality").fetchone()[0],0)
        with self.assertRaises(ApiError) as caught:self.post(self.body([e]))
        self.assertEqual(caught.exception.status,403)
    def test_real_http_measurement_route(self):
        server=make_server(self.service);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        try:
            body=self.body(list(pair()))
            request=Request("http://127.0.0.1:"+str(server.server_address[1])+"/measurements/batch",
                            data=json.dumps(body).encode(),headers={"Authorization":self.auth,"Content-Type":"application/json"})
            with urlopen(request,timeout=5) as response:
                self.assertEqual(json.load(response)["accepted_event_ids"],[e["event_id"] for e in body["events"]])
        finally:
            server.shutdown();server.server_close();thread.join(timeout=5)
    def test_retention_covers_measurements(self):
        self.post(self.body([event()]));self.service.clock=lambda:(NOW+91*DAY)/1000;self.service.purge_expired()
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM measurements").fetchone()[0],0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM measurement_quality").fetchone()[0],0)


class AnalysisTests(unittest.TestCase):
    def analyze(self,events,quality=None,dataset="synthetic"):
        return analyze([("synthetic-subject","synthetic-collection",e) for e in events],
                       {("synthetic-subject","synthetic-collection"):quality or dict(updated=NOW,complete_through=NOW)},NOW,dataset)
    def test_actual_pairs_and_unclicked_denominator(self):
        post,click=pair();no_click=event()
        report,rows=self.analyze([click,no_click,post])
        policy=report["policies"]["daily-adaptive-v2"]
        self.assertEqual(policy["closed_complete_posts"],2);self.assertEqual(policy["click_rate_within_window"],.5)
        self.assertEqual(policy["median_minutes"],5);self.assertFalse(report["causal_or_clinical_evidence"])
    def test_open_windows_late_clicks_and_missing_upload(self):
        post,click=pair(25*3600000)
        _,rows=self.analyze([post,click,event(at=NOW-1000)])
        self.assertEqual({r["outcome"] for r in rows},{"late_click","window_open"})
        _,rows=self.analyze([post],dict(updated=post["occurred_utc_ms"]))
        self.assertEqual(rows[0]["outcome"],"awaiting_upload_confirmation")
        _,rows=self.analyze([post],dict(updated=NOW))
        self.assertEqual(rows[0]["outcome"],"awaiting_upload_confirmation")
    def test_clock_change_and_reboot_do_not_claim_latency(self):
        post,click=pair()
        for change in (dict(boot_count=3),dict(occurred_utc_ms=click["occurred_utc_ms"]+3600000),dict(elapsed_ms=-1)):
            _,rows=self.analyze([post,dict(click,**change)])
            self.assertIsNone(rows[0]["click_delay_ms"]);self.assertEqual(rows[0]["outcome"],"clock_unreliable")
    def test_loss_excludes_summary_even_when_click_observed(self):
        report,rows=self.analyze(pair(),dict(updated=NOW,dropped=1))
        self.assertEqual(report["policies"]["daily-adaptive-v2"]["closed_complete_posts"],0)
        self.assertTrue(rows[0]["collection_incomplete"])
    def test_mixed_synthetic_live_data_never_combines(self):
        post,click=pair();report,rows=self.analyze([post,click],dataset="live")
        self.assertEqual(rows,[]);self.assertEqual(report["participants"],0)
    def test_recovered_delivery_does_not_invent_post_time(self):
        recovered=event("notification_post_recovered");_,rows=self.analyze([recovered])
        self.assertIsNone(rows[0]["posted_utc_ms"]);self.assertEqual(rows[0]["outcome"],"unverified_post")
    def test_activity_counts_are_separate_from_notification_response(self):
        e=dict(event("activity_started"),notification_id="",session_key=uuid.uuid4().hex,activity_id="course:MBCT01",elapsed_ms=-1,boot_count=-1)
        report,rows=self.analyze([e]);self.assertEqual(rows,[]);self.assertEqual(report["activity_sessions"]["activity_started"],1)

if __name__=="__main__":unittest.main()
