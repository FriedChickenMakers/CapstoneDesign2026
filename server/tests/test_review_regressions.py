"""Synthetic regressions independently reproduced from the Argon review."""
import unittest

from server.analyze_measurements import analyze
from server.tests.test_focus_measurements import funnel
from server.tests.test_measurements import DAY, NOW, event, pair
from server.tests import test_service as service_tests
from server.service import ApiError


class AnalysisReviewTests(unittest.TestCase):
    def analyze(self, events, watermark=NOW, window=DAY):
        return analyze([("synthetic-subject", "synthetic-collection", e) for e in events],
                       {("synthetic-subject", "synthetic-collection"): {"complete_through": watermark}},
                       NOW, "synthetic", window)[0]

    def test_clicked_and_unclicked_posts_both_need_complete_upload(self):
        post, click = pair()
        for watermark in (0, post["occurred_utc_ms"] + 60000):
            with self.subTest(watermark=watermark):
                report = self.analyze([post, click, event()], watermark)
                policy = report["policies"]["daily-adaptive-v2"]
                self.assertEqual(policy["closed_complete_posts"], 0)
                self.assertIsNone(policy["click_rate_within_window"])
                self.assertEqual(policy["clicked_latency_n"], 0)

    def test_focus_only_records_do_not_invent_activity_participants(self):
        report = self.analyze(funnel())
        self.assertEqual(report["activity_participants"], 0)
        self.assertEqual(report["focus_recommendation_participants"], 1)
        self.assertEqual(report["participants"], 1)
        self.assertTrue(all(n == 0 for n in report["activity_sessions"].values()))

    def test_late_selection_remains_in_denominator_for_shorter_analysis_window(self):
        window = 3600000  # CLI supports 1 hour; client exposures can last 24 hours.
        for actual in ("free-mission:M17", "free-mission:M02"):
            for delay in (window, window + 1000):
                with self.subTest(actual=actual, delay=delay):
                    events = funnel(actual)
                    for i, e in enumerate(events[1:]):
                        e["occurred_utc_ms"] = events[0]["occurred_utc_ms"] + delay + i * 1000
                    focus = self.analyze(events, window=window)["focus_recommendations"]
                    policy = focus["policies"]["focus-additional-history-preference-v2"]
                    self.assertTrue(focus["decisions"][0]["eligible"])
                    self.assertEqual(focus["decisions"][0]["outcome"], "late_selection")
                    self.assertEqual(policy["closed_complete_exposures"], 1)
                    for field in ("recommended_selected", "recommended_started", "recommended_completed", "other_activity_selected"):
                        self.assertEqual(policy[field], 0)
                    self.assertEqual(policy["selection_rate"], 0)


class RetentionReviewTests(unittest.TestCase):
    setUp = service_tests.ServiceTests.setUp
    post = service_tests.ServiceTests.post
    count = service_tests.ServiceTests.count

    def test_rejected_request_does_not_roll_back_expiration(self):
        self.post([service_tests.event()])
        self.service.clock = lambda: (NOW + 91 * DAY) / 1000
        with self.assertRaises(ApiError) as caught:
            self.post([])
        self.assertEqual(caught.exception.status, 400)
        self.assertEqual(self.count(), 0)


if __name__ == "__main__":
    unittest.main()
