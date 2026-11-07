"""Synthetic catalog-only recommendation funnels; never real participant records."""
import uuid
import unittest
from server.tests import test_measurements as base
from server.tests.test_measurements import event, NOW, DAY
from server.service import ApiError


def focus(kind="shown", decision=None, session=None, actual="free-mission:M17", at=NOW-2*DAY):
    return dict(event("focus_recommendation_"+kind, at), notification_id="", session_key="" if kind=="shown" else session or uuid.uuid4().hex,
                decision_id=decision or uuid.uuid4().hex, recommended_activity_id="free-mission:M17", activity_id=actual,
                elapsed_ms=-1, boot_count=-1, scheduled_utc_ms=0, preferred_minute=-1, effective_minute=-1,
                learned_samples=0, policy_id="focus-additional-history-preference-v2", reason="focus-method-preference")


def funnel(actual="free-mission:M17"):
    shown=focus(); session=uuid.uuid4().hex
    return [shown]+[focus(kind,shown["decision_id"],session,actual,shown["occurred_utc_ms"]+i*1000)
                    for i,kind in enumerate(("selected","started","completed"),1)]


class FocusMeasurementApiTests(unittest.TestCase):
    setUp = base.MeasurementTests.setUp
    body = base.MeasurementTests.body
    post = base.MeasurementTests.post
    def test_focus_retry_idempotency_and_static_contract(self):
        rows=funnel(); body=self.body(rows)
        self.assertEqual(self.post(body),self.post(body))
        for key,value in (("reason","private free text"),("recommended_activity_id","course:P01"),("decision_id","private-id"),
                          ("policy_id","unknown-user-policy"),("session_key",uuid.uuid4().hex),("elapsed_ms",1)):
            with self.subTest(key=key),self.assertRaises(ApiError):
                self.post(self.body([dict(focus(),**{key:value})]))
    def test_focus_catalog_ids_and_other_choice_are_valid(self):
        self.post(self.body(funnel("free-mission:M02")))
        for mission in ("course:P01","course:P28","free-mission:M17"):
            row=dict(event("activity_started"),notification_id="",session_key=uuid.uuid4().hex,activity_id=mission,elapsed_ms=-1,boot_count=-1)
            self.post(self.body([row]))
    def test_recommendation_shape_never_leaks_into_notification(self):
        with self.assertRaises(ApiError):self.post(self.body([dict(event(),decision_id=uuid.uuid4().hex,recommended_activity_id="free-mission:M17")]))
        with self.assertRaises(ApiError):self.post(self.body([dict(event(),reason="focus-method-preference")]))
    def test_focus_snapshot_fields_cannot_be_arbitrary_extra_fields(self):
        with self.assertRaises(ApiError):self.post(self.body([dict(focus(),note="synthetic free text")]))
        bad=focus();del bad["decision_id"]
        with self.assertRaises(ApiError):self.post(self.body([bad]))


class FocusAnalysisTests(unittest.TestCase):
    analyze = base.AnalysisTests.analyze
    def summary(self,events,quality=None,dataset="synthetic"):
        return self.analyze(events,quality,dataset)[0]["focus_recommendations"]
    def test_complete_funnel_and_other_choice_remain_distinct(self):
        report=self.summary(funnel()+funnel("free-mission:M02")+[focus()])
        policy=report["policies"]["focus-additional-history-preference-v2"]
        self.assertEqual(policy["closed_complete_exposures"],3)
        self.assertEqual(policy["recommended_completed"],1)
        self.assertEqual(policy["other_activity_selected"],1)
        self.assertAlmostEqual(policy["selection_rate"],1/3)
    def test_missing_exposure_is_not_fabricated_from_selection(self):
        report=self.summary(funnel()[1:])
        self.assertEqual(report["decisions"][0]["outcome"],"missing_exposure")
        self.assertIsNone(report["policies"]["focus-additional-history-preference-v2"]["selection_rate"])
    def test_direct_activity_has_no_recommendation(self):
        row=dict(event("activity_started"),notification_id="",session_key=uuid.uuid4().hex,activity_id="free-mission:M03",elapsed_ms=-1,boot_count=-1)
        self.assertEqual(self.summary([row])["decisions"],[])
    def test_partial_upload_loss_and_open_window_excluded(self):
        for quality,outcome in ((dict(complete_through=0),"awaiting_upload_confirmation"),
                                (dict(complete_through=NOW,dropped=1),"collection_incomplete")):
            row=self.summary(funnel(),quality)["decisions"][0]
            self.assertFalse(row["eligible"]);self.assertEqual(row["outcome"],outcome)
        self.assertEqual(self.summary([focus(at=NOW-1000)])["decisions"][0]["outcome"],"window_open")
    def test_duplicate_render_identity_has_no_invented_extra_denominator(self):
        rows=funnel(); rows.append(dict(rows[0],event_id=uuid.uuid4().hex))
        self.assertEqual(self.summary(rows)["decisions"][0]["outcome"],"ambiguous_lifecycle")
    def test_mismatched_snapshot_or_missing_step_is_excluded(self):
        rows=funnel();rows[-1]["reason"]="participation-history"
        self.assertEqual(self.summary(rows)["decisions"][0]["outcome"],"inconsistent_snapshot")
        rows=funnel();del rows[1]
        self.assertEqual(self.summary(rows)["decisions"][0]["outcome"],"incomplete_or_invalid_lifecycle")
        rows=funnel();del rows[2]
        self.assertEqual(self.summary(rows)["decisions"][0]["outcome"],"incomplete_or_invalid_lifecycle")
    def test_synthetic_and_live_never_combine(self):
        self.assertEqual(self.summary(funnel(),dataset="live")["decisions"],[])
    def test_late_completion_is_not_credited_inside_window(self):
        rows=funnel();rows[-1]["occurred_utc_ms"]+=DAY
        policy=self.summary(rows)["policies"]["focus-additional-history-preference-v2"]
        self.assertEqual(policy["recommended_selected"],1);self.assertEqual(policy["recommended_completed"],0)

    def test_end_cannot_precede_start_but_before_start_stop_is_valid(self):
        rows=funnel()[:3]
        ended=focus("ended",rows[0]["decision_id"],rows[1]["session_key"],at=rows[1]["occurred_utc_ms"])
        invalid=self.summary(rows+[ended])["decisions"][0]
        self.assertFalse(invalid["eligible"]);self.assertEqual(invalid["outcome"],"incomplete_or_invalid_lifecycle")
        before_start=self.summary(rows[:2]+[ended])["decisions"][0]
        self.assertTrue(before_start["eligible"]);self.assertIsNone(before_start["started_utc_ms"])
