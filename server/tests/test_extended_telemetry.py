import copy
import json
import unittest
import uuid
from server.tests import test_measurements as fixture
event, NOW, DAY = fixture.event, fixture.NOW, fixture.DAY
from server.measurements import validate
from server.analyze_measurements import analyze
from server.service import ApiError

def metadata(qa=False):
    return dict(version=2,classified=True,qa=qa,debug=qa,visit_id='a'*32,source_notification_id='',build_id='synthetic-build',os_api=33)

def activity(qa=False):
    e=event('activity_completed',NOW-1000)
    e.update(notification_id='',session_key=uuid.uuid4().hex,elapsed_ms=-1,boot_count=-1,scheduled_utc_ms=0,
             policy_id='none',preferred_minute=-1,effective_minute=-1,learned_samples=0,activity_id='course:P01',telemetry=metadata(qa),
             details=dict(content_version=5,role='basic',entry_source='course',active_seconds=17,recommended_seconds=60,completion_mode='too_long',end_reason='too_long',step_index=-1))
    return e

class ExtendedTelemetryTests(unittest.TestCase):
    setUp=fixture.MeasurementTests.setUp
    body=fixture.MeasurementTests.body
    post=fixture.MeasurementTests.post
    def test_metadata_retry_is_immutable_and_received_time_is_first_receipt(self):
        e=activity(True);body=dict(self.body([e]),schema_version=2)
        self.post(body)
        self.service.clock=lambda:(NOW+5000)/1000
        self.post(body)
        with self.service.connect() as db:
            self.assertEqual(db.execute('SELECT received FROM measurement_receipts').fetchone()[0],NOW)
        changed=copy.deepcopy(e);changed['telemetry']['qa']=False
        with self.assertRaises(ApiError):self.post(self.body([changed]))
    def test_cohorts_and_duration_do_not_use_wall_time(self):
        qa=activity(True);participant=activity();legacy=event()
        records=[(self.subject,self.collection,e) for e in (qa,participant,legacy)]
        report,_=analyze(records,{},NOW,'synthetic',cohort='participant')
        self.assertEqual(report['available_cohort_events'],dict(qa=1,participant=1,legacy=1))
        self.assertEqual(report['practice_details']['completed_sessions'],1)
        self.assertEqual(report['practice_details']['active_seconds'],[17])
        self.assertEqual(report['practice_details']['shortened_sessions'],1)
    def test_diagnostics_only_accept_qa_and_never_arbitrary_text(self):
        e=event('ui_layout',NOW-1000)
        e.update(notification_id='',session_key=uuid.uuid4().hex,activity_id='',scheduled_utc_ms=0,policy_id='none',
                 preferred_minute=-1,effective_minute=-1,learned_samples=0,telemetry=metadata(True),
                 details=dict(screen_id='focus-practice',width=360,height=640,safe_area=[0,0,360,600],scale_milli=1000,
                              controls=[dict(id='Focus.TooLong',bounds=[20,530,300,48],visibility='partial')]))
        self.assertTrue(validate(e,NOW));self.post(self.body([e]))
        e['telemetry']['qa']=False;self.assertFalse(validate(e,NOW))
        e['telemetry']['qa']=True;e['details']['note']='synthetic forbidden text';self.assertFalse(validate(e,NOW))
    def test_transport_and_receipts_are_removed_by_withdrawal(self):
        transport=dict(pending_count=1,oldest_pending_ms=1000,retry_count=2,last_error='network',last_success_utc_ms=NOW-1000)
        self.post(dict(self.body([activity()]),schema_version=2,transport=transport))
        self.service.handle('DELETE','/participant',self.auth,None)
        with self.service.connect() as db:
            self.assertEqual(db.execute('SELECT count(*) FROM measurement_receipts').fetchone()[0],0)
            self.assertEqual(db.execute('SELECT count(*) FROM measurement_transport').fetchone()[0],0)
    def test_batch_rejects_bad_extension_atomically(self):
        good=activity();bad=activity();bad['details']['active_seconds']=-1
        with self.assertRaises(ApiError):self.post(self.body([good,bad]))
        with self.service.connect() as db:self.assertEqual(db.execute('SELECT count(*) FROM measurements').fetchone()[0],0)

    def test_mixed_qa_activity_is_excluded_from_participant_funnel(self):
        completed=activity(False);started=copy.deepcopy(completed)
        started.update(kind='activity_started',event_id=uuid.uuid4().hex,telemetry=metadata(True))
        report,_=analyze([(self.subject,self.collection,e) for e in (started,completed)],{},NOW,'synthetic',cohort='participant')
        self.assertEqual(report['excluded_mixed_cohort_flows'],1)
        self.assertEqual(report['practice_details']['completed_sessions'],0)

    def test_legacy_replay_remains_identical_after_v2_upload(self):
        legacy=event();self.post(self.body([legacy]))
        self.post(dict(self.body([activity(True)]),schema_version=2))
        self.post(self.body([legacy]))
        with self.service.connect() as db:
            self.assertEqual(db.execute('SELECT count(*) FROM measurements').fetchone()[0],2)

    def test_pre_migration_receipt_time_is_not_invented_by_replay(self):
        e=event();self.post(self.body([e]))
        with self.service.connect() as db:db.execute('DELETE FROM measurement_receipts')
        self.post(self.body([e]))
        with self.service.connect() as db:self.assertEqual(db.execute('SELECT count(*) FROM measurement_receipts').fetchone()[0],0)
