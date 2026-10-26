"""All events and identities here are generated synthetic test fixtures."""
import copy
import json
from pathlib import Path
import secrets
import sqlite3
import tempfile
import threading
import unittest
from urllib.error import HTTPError
from urllib.request import Request, urlopen
import uuid

from server.service import ApiError, BASELINE, EventService, make_server

NOW = 1800000000


def event(kind="recommendation_shown"):
    return {"event_id": uuid.uuid4().hex, "decision_id": uuid.uuid4().hex, "kind": kind,
            "occurred_utc_ms": NOW * 1000, "utc_offset_minutes": 540,
            "activity_id": "course:MBCT2W01", "policy_id": "local-rules-v1",
            "app_version": "synthetic-test", "content_version": "courses-v1",
            "features": [1.0, 0.5, 0.25, 1.0]}


class ServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.token, self.other_token = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
        self.subject, self.other = uuid.uuid4().hex, uuid.uuid4().hex
        self.db = Path(self.temp.name) / "synthetic.sqlite"
        self.service = EventService(self.db, {self.token: self.subject, self.other_token: self.other}, clock=lambda: NOW)
        self.authorization = "Bearer " + self.token

    def post(self, events, token=None):
        return self.service.handle("POST", "/events/batch", token or self.authorization,
                                   {"schema_version": 1, "consent_version": 1, "events": events})

    def count(self):
        with sqlite3.connect(self.db) as db:
            return db.execute("SELECT COUNT(*) FROM events").fetchone()[0]

    def test_retry_is_idempotent_and_isolated(self):
        item = event()
        self.assertEqual(self.post([item]), self.post([item]))
        self.assertEqual(self.count(), 1)
        self.post([item], "Bearer " + self.other_token)
        self.assertEqual(self.count(), 2)

    def test_conflicting_identity_rolls_back_entire_batch(self):
        item = event()
        self.post([item])
        changed = dict(item, kind="activity_started")
        with self.assertRaises(ApiError) as caught:
            self.post([event(), changed])
        self.assertEqual(caught.exception.status, 409)
        self.assertEqual(self.count(), 1)

    def test_free_text_and_unbounded_features_are_rejected(self):
        for field, value in [("journal", "synthetic text"), ("features", [1, 2, 0, 0]),
                             ("features", [float("nan"), 0, 0, 0]), ("occurred_utc_ms", True),
                             ("activity_id", "arbitrary free text"), ("utc_offset_minutes", 900)]:
            bad = dict(event(), **{field: value})
            with self.subTest(field=field, value_type=type(value).__name__), self.assertRaises(ApiError):
                self.post([event(), bad])
            self.assertEqual(self.count(), 0)

    def test_deletion_prevents_late_retry_but_not_other_participant(self):
        item = event()
        self.post([item])
        self.post([event()], "Bearer " + self.other_token)
        self.service.handle("DELETE", "/participant", self.authorization)
        self.service.handle("DELETE", "/participant", self.authorization)
        self.assertEqual(self.count(), 1)
        with self.assertRaises(ApiError) as caught:
            self.post([item])
        self.assertEqual(caught.exception.status, 403)

    def test_authentication_and_fallback_policy(self):
        with self.assertRaises(ApiError):
            self.service.handle("GET", "/policy/current", "")
        self.assertEqual(self.service.handle("GET", "/policy/current", self.authorization)["model_type"], "rules")

    def test_stale_and_future_events_rejected(self):
        for timestamp in ((NOW - 91 * 86400) * 1000, (NOW + 301) * 1000):
            with self.assertRaises(ApiError):
                self.post([dict(event(), occurred_utc_ms=timestamp)])

    def test_malformed_event_types_are_validation_errors_and_do_not_write(self):
        for field, value in [("kind", []), ("kind", {}), ("kind", None),
                             ("features", [10 ** 400, 0, 0, 0]),
                             ("features", [True, 0, 0, 0]), ("features", "0000"),
                             ("event_id", []), ("policy_id", {}), ("utc_offset_minutes", True)]:
            with self.subTest(field=field, value_type=type(value).__name__), self.assertRaises(ApiError) as caught:
                self.post([event(), dict(event(), **{field: value})])
            self.assertEqual(caught.exception.status, 400)
            self.assertEqual(self.count(), 0)

    def test_schema_and_consent_versions_require_integers(self):
        for field in ("schema_version", "consent_version"):
            for value in (True, 1.0, "1", None):
                body = {"schema_version": 1, "consent_version": 1, "events": [event()], field: value}
                with self.subTest(field=field, value_type=type(value).__name__), self.assertRaises(ApiError) as caught:
                    self.service.handle("POST", "/events/batch", self.authorization, body)
                self.assertEqual(caught.exception.status, 400)
        self.assertEqual(self.count(), 0)

    def test_malformed_policy_is_unavailable_without_leaking_file_contents(self):
        path = Path(self.temp.name) / "synthetic-policy.json"
        self.service.policy_file = path
        for field, value in [("schema_version", True), ("weights", None), ("weights", 42),
                             ("weights", [10 ** 400, 0, 0, 0]), ("weights", "0000"),
                             ("intercept", []), ("training_rows", True), ("model_type", {})]:
            path.write_text(json.dumps(dict(BASELINE, **{field: value})))
            with self.subTest(field=field, value_type=type(value).__name__), self.assertRaises(ApiError) as caught:
                self.service.handle("GET", "/policy/current", self.authorization)
            self.assertEqual(caught.exception.status, 503)

    def test_http_malformed_shapes_receive_json_errors(self):
        server = make_server(self.service)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.shutdown)
        url = "http://127.0.0.1:" + str(server.server_port) + "/events/batch"
        bodies = [json.dumps({"schema_version": 1, "consent_version": 1, "events": [dict(event(), kind=[])]}).encode(),
                  json.dumps({"schema_version": 1, "consent_version": 1, "events": [dict(event(), features=[10 ** 400, 0, 0, 0])]}).encode(),
                  b"[" * 2000 + b"0" + b"]" * 2000]
        for body in bodies:
            with self.assertRaises(HTTPError) as caught:
                urlopen(Request(url, body, {"Authorization": self.authorization, "Content-Type": "application/json"}), timeout=2)
            self.assertEqual(caught.exception.code, 400)
            self.assertEqual(set(json.load(caught.exception)), {"error"})
        self.assertEqual(self.count(), 0)

    def test_retention_runs_on_startup_read_and_idle_server_tick(self):
        self.post([event()])
        future = NOW + 91 * 86400
        self.service.clock = lambda: future
        self.service.handle("GET", "/policy/current", self.authorization)
        self.assertEqual(self.count(), 0, "Read-only traffic must also expire old observations")

        self.service.clock = lambda: NOW
        self.post([event()])
        restarted = EventService(self.db, {self.token: self.subject}, clock=lambda: future)
        self.assertEqual(self.count(), 0, "Startup must expire records retained while offline")

        self.post([event()])
        self.service.clock = lambda: future
        server = make_server(self.service)
        self.addCleanup(server.server_close)
        server.service_actions()
        self.assertEqual(self.count(), 0, "Idle serving must expire records without a new upload")
        self.assertGreater(server.next_retention_check, 0)

    def test_retention_preserves_boundary_and_revocation_tombstone(self):
        self.post([event()])
        self.service.clock = lambda: NOW + 90 * 86400
        self.assertEqual(self.service.purge_expired(), 0)
        self.assertEqual(self.count(), 1)
        self.service.handle("DELETE", "/participant", self.authorization)
        self.service.clock = lambda: NOW + 180 * 86400
        self.service.purge_expired()
        with self.assertRaises(ApiError) as caught:
            self.service.handle("GET", "/policy/current", self.authorization)
        self.assertEqual(caught.exception.status, 403)

    def test_http_round_trip_and_private_error(self):
        server = make_server(self.service)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.shutdown)
        url = "http://127.0.0.1:" + str(server.server_port)
        payload = json.dumps({"schema_version": 1, "consent_version": 1, "events": [event()]}).encode()
        request = Request(url + "/events/batch", payload, {"Authorization": self.authorization, "Content-Type": "application/json"})
        with urlopen(request, timeout=2) as response:
            self.assertEqual(len(json.load(response)["accepted_event_ids"]), 1)
        with self.assertRaises(HTTPError) as caught:
            urlopen(Request(url + "/events/batch", b"bad", {"Content-Type": "application/json"}), timeout=2)
        self.assertEqual(caught.exception.code, 401)
        self.assertEqual(json.load(caught.exception), {"error": "authentication_required"})


if __name__ == "__main__":
    unittest.main()
