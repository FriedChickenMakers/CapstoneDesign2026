"""Synthetic app visit evidence; incomplete follow-up is never a non-return."""
import copy
from pathlib import Path
import secrets
import tempfile
import unittest
import uuid

from server.analyze_measurements import analyze, DAY
from server.service import ApiError, EventService

NOW = 1800000000000


def visit(at):
    return dict(event_id=uuid.uuid4().hex, notification_id="", session_key=uuid.uuid4().hex,
                kind="app_session_started", occurred_utc_ms=at, elapsed_ms=100000, boot_count=2,
                utc_offset_minutes=540, scheduled_utc_ms=0, policy_id="none", preferred_minute=-1,
                effective_minute=-1, learned_samples=0, reason="none", activity_id="",
                app_version="synthetic-test", dataset="synthetic")


class AppSessionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.token, self.subject, self.collection = secrets.token_urlsafe(32), uuid.uuid4().hex, uuid.uuid4().hex
        self.service = EventService(Path(self.temp.name) / "synthetic.sqlite", {self.token: self.subject}, clock=lambda: NOW / 1000)

    def body(self, events):
        return dict(schema_version=1, consent_version=2, collection_id=self.collection,
                    dropped_attempts=0, expired_events=0, storage_error=False, complete_through_utc_ms=NOW, events=events)

    def test_round_trip_and_duplicate_ack_without_hardware_identifier(self):
        item = visit(NOW - 1000)
        body = self.body([item])
        for _ in range(2):
            self.assertEqual(self.service.handle("POST", "/measurements/batch", "Bearer " + self.token, body),
                             {"accepted_event_ids": [item["event_id"]]})
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM measurements").fetchone()[0], 1)

    def test_invalid_session_fields_are_rejected_atomically(self):
        for field, value in (("activity_id", "free-mission:M01"), ("session_key", ""), ("elapsed_ms", -1),
                             ("notification_id", uuid.uuid4().hex), ("policy_id", "daily-adaptive-v2"),
                             ("preferred_minute", 720), ("scheduled_utc_ms", NOW), ("learned_samples", 1),
                             ("reason", "permission_denied"), ("device_id", "synthetic-forbidden")):
            with self.subTest(field=field), self.assertRaises(ApiError):
                self.service.handle("POST", "/measurements/batch", "Bearer " + self.token,
                                    self.body([visit(NOW - 1000), dict(visit(NOW - 1000), **{field: value})]))
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM measurements").fetchone()[0], 0)

    def test_d7_d14_exclude_unconfirmed_and_incomplete_followup(self):
        first = NOW - 16 * DAY
        events = [visit(first), visit(first + 7 * DAY), visit(first + 14 * DAY)]
        records = [(self.subject, self.collection, e) for e in events]
        quality = {(self.subject, self.collection): dict(complete_through=NOW)}
        report, _ = analyze(records, quality, NOW, "synthetic")
        self.assertEqual(report["participants"], 1)
        self.assertEqual(report["app_sessions"]["foreground_sessions"], 3)
        for day in ("d7", "d14"):
            self.assertEqual(report["app_sessions"]["returns"][day]["return_rate"], 1)
        for change in ({"complete_through": 0}, {"complete_through": NOW, "dropped": 1}):
            report, _ = analyze(records, {(self.subject, self.collection): change}, NOW, "synthetic")
            self.assertIsNone(report["app_sessions"]["returns"]["d7"]["return_rate"])
            self.assertTrue(report["app_sessions"]["cohorts"][0]["d7"]["observed_return"])

    def test_day_windows_are_half_open_and_dataset_separated(self):
        first = NOW - 16 * DAY
        events = [visit(first), visit(first + 8 * DAY), visit(first + 15 * DAY), dict(visit(first + 7 * DAY), dataset="live")]
        report, _ = analyze([(self.subject, self.collection, e) for e in events],
                            {(self.subject, self.collection): dict(complete_through=NOW)}, NOW, "synthetic")
        self.assertEqual(report["app_sessions"]["foreground_sessions"], 3)
        for day in ("d7", "d14"):
            self.assertEqual(report["app_sessions"]["returns"][day]["return_rate"], 0)


if __name__ == "__main__":
    unittest.main()
