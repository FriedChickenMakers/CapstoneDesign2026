"""Synthetic automatic enrollment: no production distribution/install secrets."""
import concurrent.futures
import hashlib
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

from server.enrollments import load_key
from server.service import ApiError, EventService, make_server

NOW = 1800000000


class EnrollmentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "synthetic.sqlite"
        self.key, self.token = secrets.token_hex(32), secrets.token_hex(32)
        self.manual_token, self.manual_subject = secrets.token_hex(32), uuid.uuid4().hex
        self.manual = {self.manual_token: self.manual_subject}
        self.service = self.new_service()

    def new_service(self, key=True, limits=(240, 1000), manual=None):
        return EventService(self.path, self.manual if manual is None else manual, clock=lambda: NOW,
                            enrollment_key=self.key if key else None, enrollment_limits=limits)

    def register(self, token=None, service=None, key=None, body=None):
        return (service or self.service).handle("POST", "/enrollments", None,
                                               body if body is not None else {"token": token or self.token, "app_version": "synthetic-test"},
                                               enrollment_key=self.key if key is None else key)

    def row(self, token=None):
        digest = hashlib.sha256((token or self.token).encode()).hexdigest()
        with sqlite3.connect(self.path) as db:
            return db.execute("SELECT token_sha256,subject,created_utc_ms,app_version FROM automatic_enrollments WHERE token_sha256=?", (digest,)).fetchone()

    def test_first_registration_stores_only_hash_and_returns_no_identity_or_secret(self):
        self.assertEqual(self.register(), {"registered": True})
        row = self.row()
        self.assertEqual(row[0], hashlib.sha256(self.token.encode()).hexdigest())
        self.assertEqual(uuid.UUID(hex=row[1]).hex, row[1])
        self.assertNotEqual(row[1], self.manual_subject)
        self.assertEqual(row[2:], (NOW * 1000, "synthetic-test"))
        self.assertNotIn(self.token.encode(), self.path.read_bytes())
        self.assertNotIn(self.key.encode(), self.path.read_bytes())
        self.assertEqual(self.service.authenticate("Bearer " + self.token), row[1])
        self.assertEqual(self.service.handle("GET", "/policy/current", "Bearer " + self.token)["model_type"], "rules")

    def test_retry_and_restart_keep_subject_and_original_metadata(self):
        self.register()
        original = self.row()
        self.register(body={"token": self.token, "app_version": "synthetic-update"})
        self.assertEqual(self.row(), original)
        restarted = self.new_service()
        self.assertEqual(self.register(service=restarted), {"registered": True})
        self.assertEqual(restarted.authenticate("Bearer " + self.token), original[1])
        self.assertEqual(self.row(), original)

    def test_concurrent_duplicate_across_service_instances_is_one_registration(self):
        services = [self.new_service() for _ in range(6)]
        barrier = threading.Barrier(len(services))
        def run(service):
            barrier.wait(timeout=5)
            return self.register(service=service)
        with concurrent.futures.ThreadPoolExecutor(max_workers=len(services)) as pool:
            results = list(pool.map(run, services))
        self.assertEqual(results, [{"registered": True}] * len(services))
        with sqlite3.connect(self.path) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM automatic_enrollments").fetchone()[0], 1)
        self.assertEqual(len({service.authenticate("Bearer " + self.token) for service in services}), 1)

    def test_manual_same_token_keeps_existing_subject_and_does_not_consume_new_budget(self):
        self.assertEqual(self.register(token=self.manual_token), {"registered": True})
        self.assertEqual(self.service.authenticate("Bearer " + self.manual_token), self.manual_subject)
        self.assertIsNone(self.row(self.manual_token))

    def test_new_token_rate_limit_is_global_but_retries_manual_and_auth_are_unaffected(self):
        limited = self.new_service(limits=(2, 3))
        tokens = [secrets.token_hex(32) for _ in range(4)]
        self.register(tokens[0], limited)
        self.register(tokens[1], limited)
        with self.assertRaises(ApiError) as caught:
            self.register(tokens[2], limited)
        self.assertEqual((caught.exception.status, caught.exception.code, caught.exception.retry_after), (429, "enrollment_rate_limited", 60))
        self.assertIsNone(self.row(tokens[2]))
        self.assertEqual(self.register(tokens[0], limited), {"registered": True})
        self.assertEqual(self.register(self.manual_token, limited), {"registered": True})
        restarted = self.new_service(limits=(2, 3))
        with self.assertRaises(ApiError):
            self.register(tokens[2], restarted)
        restarted.clock = lambda: NOW + 3601
        self.register(tokens[2], restarted)
        with self.assertRaises(ApiError) as caught:
            self.register(tokens[3], restarted)
        self.assertEqual(caught.exception.status, 429, "Daily budget survives hourly window rollover")
        restarted.clock = lambda: NOW + 86401
        self.register(tokens[3], restarted)

    def test_concurrent_distinct_tokens_cannot_exceed_new_limit(self):
        services = [self.new_service(limits=(2, 2)) for _ in range(6)]
        barrier = threading.Barrier(len(services))
        def run(service):
            barrier.wait(timeout=5)
            try:
                self.register(secrets.token_hex(32), service)
                return 200
            except ApiError as error:
                return error.status
        with concurrent.futures.ThreadPoolExecutor(max_workers=len(services)) as pool:
            results = list(pool.map(run, services))
        self.assertEqual(sorted(results), [200, 200, 429, 429, 429, 429])

    def test_withdrawn_auto_token_cannot_be_reenrolled_after_restart(self):
        self.register()
        original = self.row()
        authorization = "Bearer " + self.token
        self.service.handle("DELETE", "/participant", authorization)
        restarted = self.new_service()
        for call in (lambda: self.register(service=restarted), lambda: restarted.handle("GET", "/policy/current", authorization)):
            with self.assertRaises(ApiError) as caught:
                call()
            self.assertEqual(caught.exception.status, 403)
        self.assertEqual(self.row(), original)
        self.assertEqual(restarted.handle("DELETE", "/participant", authorization), {"deleted": True, "enrollment_revoked": True})

    def test_withdrawn_manual_token_cannot_move_to_new_automatic_subject(self):
        self.service.handle("DELETE", "/participant", "Bearer " + self.manual_token)
        with self.assertRaises(ApiError) as caught:
            self.register(self.manual_token, self.new_service())
        self.assertEqual(caught.exception.status, 403)
        self.assertIsNone(self.row(self.manual_token))

    def test_withdrawn_manual_token_stays_revoked_after_mapping_removal(self):
        self.service.handle("DELETE", "/participant", "Bearer " + self.manual_token)
        restarted = self.new_service(manual={})
        with self.assertRaises(ApiError) as caught:
            self.register(self.manual_token, restarted)
        self.assertEqual(caught.exception.status, 403)
        with self.assertRaises(ApiError) as caught:
            restarted.handle("GET", "/policy/current", "Bearer " + self.manual_token)
        self.assertEqual(caught.exception.status, 403)
        self.assertNotIn(self.manual_token.encode(), self.path.read_bytes())
        self.assertEqual(restarted.handle("DELETE", "/participant", "Bearer " + self.manual_token),
                         {"deleted": True, "enrollment_revoked": True})

    def test_preexisting_manual_withdrawal_migrates_token_tombstone_on_startup(self):
        with self.service.connect() as db:
            db.execute("INSERT INTO revoked VALUES (?)", (self.manual_subject,))
        self.new_service()  # Upgrade while the old manual mapping is present.
        restarted = self.new_service(manual={})
        with self.assertRaises(ApiError) as caught:
            self.register(self.manual_token, restarted)
        self.assertEqual(caught.exception.status, 403)

    def test_offline_measurement_created_before_registration_replays_unchanged(self):
        at = (NOW - 86400) * 1000
        event = dict(event_id=uuid.uuid4().hex, notification_id="", session_key=uuid.uuid4().hex,
                     kind="app_session_started", occurred_utc_ms=at, elapsed_ms=123456, boot_count=2,
                     utc_offset_minutes=540, scheduled_utc_ms=0, policy_id="none", preferred_minute=-1,
                     effective_minute=-1, learned_samples=0, reason="none", activity_id="",
                     app_version="synthetic-test", dataset="synthetic")
        body = dict(schema_version=1, consent_version=2, collection_id=uuid.uuid4().hex,
                    dropped_attempts=0, expired_events=0, storage_error=False, complete_through_utc_ms=NOW * 1000,
                    events=[event])
        with self.assertRaises(ApiError) as caught:
            self.service.handle("POST", "/measurements/batch", "Bearer " + self.token, body)
        self.assertEqual(caught.exception.status, 401)
        self.register()
        for _ in range(2):
            self.assertEqual(self.service.handle("POST", "/measurements/batch", "Bearer " + self.token, body),
                             {"accepted_event_ids": [event["event_id"]]})
        with self.service.connect() as db:
            rows = db.execute("SELECT payload FROM measurements").fetchall()
        self.assertEqual([json.loads(row[0]) for row in rows], [event])

    def test_disabling_key_closes_registration_but_preserves_existing_auto_and_manual_auth(self):
        self.register()
        disabled = self.new_service(key=False)
        with self.assertRaises(ApiError) as caught:
            self.register(service=disabled)
        self.assertEqual(caught.exception.status, 404)
        self.assertEqual(disabled.authenticate("Bearer " + self.token), self.row()[1])
        self.assertEqual(disabled.authenticate("Bearer " + self.manual_token), self.manual_subject)

    def test_invalid_keys_fields_and_versions_do_not_create_rows(self):
        for supplied in ("", "synthetic-wrong-key", self.key[:-1], None):
            with self.subTest(key_type=type(supplied).__name__), self.assertRaises(ApiError) as caught:
                self.service.handle("POST", "/enrollments", None, {"token": self.token, "app_version": "synthetic-test"}, enrollment_key=supplied)
            self.assertEqual(caught.exception.status, 401)
        for body in ({}, [], {"token": self.token}, {"token": self.token, "app_version": "synthetic-test", "subject": self.manual_subject},
                     {"token": "A" + self.token[1:], "app_version": "synthetic-test"}, {"token": self.token + "\n", "app_version": "synthetic-test"},
                     {"token": 64, "app_version": "synthetic-test"}, {"token": self.token, "app_version": "unapproved free text"},
                     {"token": self.token, "app_version": "a" * 49}, {"token": self.token, "app_version": True}):
            with self.subTest(body_type=type(body).__name__), self.assertRaises(ApiError) as caught:
                self.register(body=body)
            self.assertEqual(caught.exception.status, 400)
        with sqlite3.connect(self.path) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM automatic_enrollments").fetchone()[0], 0)

    def test_conflicting_future_manual_mapping_fails_closed(self):
        self.register()
        with self.assertRaises(ValueError):
            self.new_service(manual={self.token: uuid.uuid4().hex})
        self.assertEqual(self.service.authenticate("Bearer " + self.token), self.row()[1])

    def test_http_gate_size_duplicate_fields_retry_after_and_bearer_flow(self):
        server = make_server(self.new_service(limits=(1, 1)))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.shutdown)
        base = "http://127.0.0.1:" + str(server.server_port)
        def call(body, key=True, content="application/json"):
            headers = {"Content-Type": content}
            if key:headers["X-Enrollment-Key"] = self.key
            request = Request(base + "/enrollments", body, headers)
            try:
                with urlopen(request, timeout=2) as response:
                    return response.status, json.load(response), response.headers
            except HTTPError as response:
                return response.code, json.load(response), response.headers
        body = json.dumps({"token": self.token, "app_version": "synthetic-test"}).encode()
        self.assertEqual(call(body, key=False)[0], 401)
        self.assertEqual(call(b"x" * 1025)[0], 413)
        self.assertEqual(call(body, content="text/plain")[0], 415)
        duplicate = ('{"token":"' + self.token + '","token":"' + self.token + '","app_version":"synthetic-test"}').encode()
        self.assertEqual(call(duplicate)[0], 400)
        self.assertEqual(call(body)[:2], (200, {"registered": True}))
        second = json.dumps({"token": secrets.token_hex(32), "app_version": "synthetic-test"}).encode()
        status, data, headers = call(second)
        self.assertEqual((status, data, headers["Retry-After"]), (429, {"error": "enrollment_rate_limited"}, "60"))
        self.assertEqual(call(body)[:2], (200, {"registered": True}))
        with urlopen(Request(base + "/policy/current", headers={"Authorization": "Bearer " + self.token}), timeout=2) as response:
            self.assertEqual(response.status, 200)

    def test_private_key_file_contract(self):
        self.assertIsNone(load_key(None))
        keyfile = Path(self.temp.name) / "synthetic-key"
        keyfile.write_text(self.key + "\n")
        keyfile.chmod(0o600)
        self.assertEqual(load_key(keyfile), self.key)
        keyfile.chmod(0o644)
        with self.assertRaises(ValueError):load_key(keyfile)
        keyfile.chmod(0o600)
        for value in ("A" + self.key[1:], self.key + " extra", self.key + "\n" * 300, "not-a-real-key"):
            keyfile.write_text(value)
            with self.assertRaises(ValueError):load_key(keyfile)

    def test_http_withdrawal_before_first_registration_and_future_registration_rejected(self):
        server = make_server(self.service)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 2)
        self.addCleanup(server.shutdown)
        base = "http://127.0.0.1:" + str(server.server_port)
        def call(method, path, token=None, key=True, payload=None):
            headers = {"Authorization": "Bearer " + (self.token if token is None else token)}
            if key:headers["X-Enrollment-Key"] = self.key if key is True else key
            if payload is not None:headers["Content-Type"] = "application/json"
            request = Request(base + path, json.dumps(payload).encode() if payload is not None else None, headers, method=method)
            try:
                with urlopen(request, timeout=2) as response:return response.status, json.load(response)
            except HTTPError as response:return response.code, json.load(response)
        self.assertEqual(call("DELETE", "/participant", key=False)[0], 401)
        self.assertEqual(call("DELETE", "/participant", key="synthetic-wrong-key")[0], 401)
        self.assertEqual(call("DELETE", "/participant", token="not-an-install-token")[0], 400)
        # The distribution key never grants data/policy access to an unknown token.
        self.assertEqual(call("GET", "/policy/current")[0], 401)
        self.assertEqual(call("POST", "/measurements/batch", payload={})[0], 401)
        self.assertEqual(call("DELETE", "/participant"), (200, {"deleted": True, "enrollment_revoked": True}))
        original = self.row()
        self.assertEqual(original[3], "withdraw-before-registration")
        self.assertEqual(call("POST", "/enrollments", payload={"token": self.token, "app_version": "synthetic-test"})[0], 403)
        self.assertEqual(call("GET", "/policy/current")[0], 403)
        # Retry no longer requires the shared key, including after its rotation.
        self.assertEqual(call("DELETE", "/participant", key=False)[0], 200)
        self.assertEqual(call("DELETE", "/participant", key="rotated-away")[0], 200)
        restarted = self.new_service(key=False)
        self.assertEqual(restarted.handle("DELETE", "/participant", "Bearer " + self.token),
                         {"deleted": True, "enrollment_revoked": True})
        self.assertEqual(self.row(), original)
        with self.service.connect() as db:
            for table in ("events", "measurements", "measurement_quality"):
                self.assertEqual(db.execute("SELECT COUNT(*) FROM " + table).fetchone()[0], 0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked").fetchone()[0], 1)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked_enrollment_tokens").fetchone()[0], 1)
        self.assertNotIn(self.token.encode(), self.path.read_bytes())

    def test_unregistered_withdrawal_and_registration_share_budget_but_retries_do_not(self):
        limited = self.new_service(limits=(1, 1))
        auth = "Bearer " + self.token
        limited.handle("DELETE", "/participant", auth, enrollment_key=self.key)
        self.assertEqual(limited.handle("DELETE", "/participant", auth), {"deleted": True, "enrollment_revoked": True})
        self.assertEqual(limited.handle("DELETE", "/participant", "Bearer " + self.manual_token), {"deleted": True, "enrollment_revoked": True})
        second = secrets.token_hex(32)
        with self.assertRaises(ApiError) as caught:
            limited.handle("DELETE", "/participant", "Bearer " + second, enrollment_key=self.key)
        self.assertEqual(caught.exception.status, 429)
        self.assertIsNone(self.row(second))
        with self.assertRaises(ApiError) as caught:
            self.register(second, limited)
        self.assertEqual(caught.exception.status, 429)

    def test_unregistered_withdrawal_failure_rolls_back_identity_and_tombstone_together(self):
        with self.service.connect() as db:
            db.execute("CREATE TRIGGER synthetic_revoke_failure BEFORE INSERT ON revoked BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END")
        with self.assertRaises(sqlite3.IntegrityError):
            self.service.handle("DELETE", "/participant", "Bearer " + self.token, enrollment_key=self.key)
        self.assertIsNone(self.row())
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked_enrollment_tokens").fetchone()[0], 0)
        with self.assertRaises(ApiError) as caught:self.service.authenticate("Bearer " + self.token)
        self.assertEqual(caught.exception.status, 401)

    def test_concurrent_first_registration_and_withdrawal_end_with_one_revoked_identity(self):
        services = [self.new_service() for _ in range(6)]
        barrier = threading.Barrier(len(services))
        def run(item):
            index, service = item
            barrier.wait(timeout=5)
            if index % 2:
                return service.handle("DELETE", "/participant", "Bearer " + self.token, enrollment_key=self.key)
            try:
                return self.register(service=service)
            except ApiError as error:
                self.assertEqual(error.status, 403)
                return {"revoked": True}
        with concurrent.futures.ThreadPoolExecutor(max_workers=len(services)) as pool:
            results = list(pool.map(run, enumerate(services)))
        self.assertEqual(sum(result.get("deleted", False) for result in results), 3)
        with self.service.connect() as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM automatic_enrollments").fetchone()[0], 1)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked").fetchone()[0], 1)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked_enrollment_tokens").fetchone()[0], 1)
        with self.assertRaises(ApiError) as caught:self.register()
        self.assertEqual(caught.exception.status, 403)

    def test_registered_manual_and_auto_withdrawal_ignore_shared_key_rotation(self):
        self.register()
        rotated = EventService(self.path, self.manual, clock=lambda: NOW, enrollment_key=secrets.token_hex(32))
        for token in (self.manual_token, self.token):
            self.assertEqual(rotated.handle("DELETE", "/participant", "Bearer " + token, enrollment_key=self.key),
                             {"deleted": True, "enrollment_revoked": True})
        closed = self.new_service(key=False)
        with self.assertRaises(ApiError) as caught:
            closed.handle("DELETE", "/participant", "Bearer " + secrets.token_hex(32), enrollment_key=self.key)
        self.assertEqual(caught.exception.status, 401)


if __name__ == "__main__":
    unittest.main()
