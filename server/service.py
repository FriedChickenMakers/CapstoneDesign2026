"""Authenticated event/policy API. Run behind TLS for any non-local deployment.

Manual registrations come from CAPSTONE_ENROLLMENTS_FILE. Optional automatic
registration accepts an app-generated install token using a private distribution
key file; it stores only token hashes. No credentials belong in the source tree.
The standard-library HTTP adapter is a development server, not a TLS endpoint.
"""
from __future__ import annotations

import hashlib
import hmac
import json
import math
import os
from pathlib import Path
import re
import socket
import sqlite3
import threading
import time
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from uuid import UUID

from server.enrollments import AutomaticEnrollments, MAX_ENROLLMENT_BODY, load_key

KINDS = frozenset({"recommendation_shown", "activity_started", "activity_completed",
                   "activity_ended", "notification_eligible", "notification_scheduled",
                   "notification_post_accepted", "notification_blocked",
                   "notification_failed", "notification_opened"})
FEATURES = ("preference", "completion_rate", "days_since", "duration_fit")
BASELINE = {"schema_version": 1, "policy_id": "local-rules-v1", "model_type": "rules",
            "feature_names": list(FEATURES), "weights": [0.0] * 4, "intercept": 0.0,
            "expires_utc_ms": 0, "training_rows": 0}
MAX_BODY = 128 * 1024
MAX_BATCH = 100
MAX_CONNECTIONS = 16
REQUEST_TIMEOUT_SECONDS = 10


class ApiError(Exception):
    def __init__(self, status: int, code: str, retry_after=None):
        self.status, self.code, self.retry_after = status, code, retry_after


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False)


def identifier(value):
    if not isinstance(value, str) or len(value) != 32:
        return False
    try:
        return UUID(hex=value).hex == value
    except ValueError:
        return False


def finite_number(value, minimum, maximum):
    return type(value) in (int, float) and minimum <= value <= maximum and math.isfinite(value)


def validate_event(event, now_ms):
    required = {"event_id", "decision_id", "kind", "occurred_utc_ms", "utc_offset_minutes",
                "activity_id", "policy_id", "app_version", "content_version", "features"}
    if not isinstance(event, dict) or set(event) not in (required,required | {"classification"}):
        raise ApiError(400, "event_fields")
    classification=event.get("classification")
    if classification is not None and (not isinstance(classification,dict) or set(classification)!={"classified","qa","debug"}
        or any(type(classification[k]) is not bool for k in classification)
        or not classification["classified"] and (classification["qa"] or classification["debug"])):
        raise ApiError(400,"event_classification")
    if not identifier(event["event_id"]) or not identifier(event["decision_id"]):
        raise ApiError(400, "event_identity")
    if not isinstance(event["kind"], str) or event["kind"] not in KINDS:
        raise ApiError(400, "event_kind")
    timestamp = event["occurred_utc_ms"]
    if type(timestamp) is not int or not now_ms - 90 * 86400000 <= timestamp <= now_ms + 300000:
        raise ApiError(400, "event_timestamp")
    offset = event["utc_offset_minutes"]
    if type(offset) is not int or not -840 <= offset <= 840:
        raise ApiError(400, "event_timezone")
    # IDs are catalog/version identifiers, never arbitrary labels or journal text.
    if not isinstance(event["activity_id"], str) or not re.fullmatch(r"(?:course:MBCT(?:2W|4W)?[0-9]{2}|free-mission:M[0-9]{2}|notification)", event["activity_id"]):
        raise ApiError(400, "activity_id")
    for field in ("policy_id", "app_version", "content_version"):
        if not isinstance(event[field], str) or not re.fullmatch(r"[A-Za-z0-9._-]{1,48}", event[field]):
            raise ApiError(400, "version_id")
    features = event["features"]
    if not isinstance(features, list) or len(features) != 4 or any(not finite_number(x, 0, 1) for x in features):
        raise ApiError(400, "features")


def validate_policy(policy, now_ms):
    if not isinstance(policy, dict) or set(policy) != set(BASELINE):
        raise ApiError(503, "policy_fields")
    if type(policy["schema_version"]) is not int or policy["schema_version"] != 1 or policy["feature_names"] != list(FEATURES):
        raise ApiError(503, "policy_schema")
    if not isinstance(policy["policy_id"], str) or not re.fullmatch(r"[A-Za-z0-9._-]{1,48}", policy["policy_id"]):
        raise ApiError(503, "policy_id")
    if policy["model_type"] not in ("rules", "logistic") or not isinstance(policy["weights"], list) or len(policy["weights"]) != 4:
        raise ApiError(503, "policy_model")
    if not finite_number(policy["intercept"], -20, 20) or any(not finite_number(x, -20, 20) for x in policy["weights"]):
        raise ApiError(503, "policy_coefficients")
    if type(policy["training_rows"]) is not int or policy["training_rows"] < 0:
        raise ApiError(503, "policy_training")
    if type(policy["expires_utc_ms"]) is not int:
        raise ApiError(503, "policy_expiry")
    if policy["model_type"] == "logistic" and (policy["expires_utc_ms"] <= now_ms or policy["training_rows"] < 20):
        raise ApiError(503, "policy_not_ready")
    return policy


class EventService:
    def __init__(self, database, enrollments, policy_file=None, clock=time.time,
                 enrollment_key=None, enrollment_limits=(240, 1000)):
        self.database, self.clock, self.policy_file = str(database), clock, policy_file
        self.lock = threading.RLock()
        # Mapping is token -> a pseudonymous study identity. No contact details.
        if not isinstance(enrollments, dict) or any(not identifier(subject) or not isinstance(token, str) or len(token) < 32
                                  for token, subject in enrollments.items()):
            raise ValueError("Valid external enrollment configuration required")
        if len(set(enrollments.values())) != len(enrollments):
            raise ValueError("Each enrollment must have a unique subject")
        self.enrollments = [(hashlib.sha256(token.encode()).digest(), subject)
                            for token, subject in enrollments.items()]
        self.automatic_enrollments = AutomaticEnrollments(enrollment_key, ApiError, enrollment_limits)
        Path(self.database).parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as db:
            db.executescript("""
                CREATE TABLE IF NOT EXISTS events (
                    subject TEXT NOT NULL, event_id TEXT NOT NULL, decision_id TEXT NOT NULL,
                    occurred INTEGER NOT NULL, payload TEXT NOT NULL,
                    PRIMARY KEY(subject, event_id));
                CREATE INDEX IF NOT EXISTS events_decision ON events(subject, decision_id);
                CREATE INDEX IF NOT EXISTS events_occurred ON events(occurred);
                CREATE TABLE IF NOT EXISTS revoked (subject TEXT PRIMARY KEY);
                CREATE TABLE IF NOT EXISTS measurements (
                    subject TEXT NOT NULL, event_id TEXT NOT NULL, collection_id TEXT NOT NULL,
                    notification_id TEXT NOT NULL, occurred INTEGER NOT NULL, payload TEXT NOT NULL,
                    PRIMARY KEY(subject, event_id));
                CREATE INDEX IF NOT EXISTS measurements_occurred ON measurements(occurred);
                CREATE TABLE IF NOT EXISTS measurement_receipts (
                    subject TEXT NOT NULL, event_id TEXT NOT NULL, received INTEGER NOT NULL, PRIMARY KEY(subject,event_id));
                CREATE TABLE IF NOT EXISTS measurement_transport (
                    subject TEXT NOT NULL, collection_id TEXT NOT NULL, updated INTEGER NOT NULL, payload TEXT NOT NULL, PRIMARY KEY(subject,collection_id));
                CREATE TABLE IF NOT EXISTS measurement_quality (
                    subject TEXT NOT NULL, collection_id TEXT NOT NULL, dropped INTEGER NOT NULL,
                    expired INTEGER NOT NULL, storage_error INTEGER NOT NULL, updated INTEGER NOT NULL,
                    complete_through INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY(subject, collection_id));
            """)
            # Earlier measurement clients supplied only receipt time. It cannot prove
            # that a backlog was drained, so migrated collections remain unconfirmed.
            if "complete_through" not in {row[1] for row in db.execute("PRAGMA table_info(measurement_quality)")}:
                db.execute("ALTER TABLE measurement_quality ADD COLUMN complete_through INTEGER NOT NULL DEFAULT 0")
            self.automatic_enrollments.initialize(db, self.enrollments)
            self._purge_expired(db, int(self.clock() * 1000))
        os.chmod(self.database, 0o600)

    @contextmanager
    def connect(self):
        db = sqlite3.connect(self.database, timeout=10)
        try:
            db.execute("PRAGMA secure_delete=ON")
            with db:
                yield db
        finally:
            db.close()

    @staticmethod
    def _purge_expired(db, now_ms):
        removed = db.execute("DELETE FROM events WHERE occurred < ?", (now_ms - 90 * 86400000,)).rowcount
        removed += db.execute("DELETE FROM measurements WHERE occurred < ?", (now_ms - 90 * 86400000,)).rowcount
        db.execute("DELETE FROM measurement_quality WHERE updated < ?", (now_ms - 90 * 86400000,))
        db.execute("DELETE FROM measurement_transport WHERE updated < ?", (now_ms - 90 * 86400000,))
        db.execute("DELETE FROM measurement_receipts WHERE NOT EXISTS (SELECT 1 FROM measurements m WHERE m.subject=measurement_receipts.subject AND m.event_id=measurement_receipts.event_id)")
        return removed

    def purge_expired(self):
        """Purge on startup, authenticated traffic and the running server's timer."""
        with self.lock, self.connect() as db:
            return self._purge_expired(db, int(self.clock() * 1000))

    def authenticate(self, authorization):
        if not isinstance(authorization, str) or not authorization.startswith("Bearer "):
            raise ApiError(401, "authentication_required")
        digest = hashlib.sha256(authorization[7:].encode()).digest()
        for expected, subject in self.enrollments:
            if hmac.compare_digest(digest, expected):
                return subject
        with self.lock, self.connect() as db:
            subject = self.automatic_enrollments.subject(db, digest)
        if subject:
            return subject
        raise ApiError(401, "authentication_required")

    def authorize_withdrawal(self, authorization, enrollment_key):
        try:
            # Already registered/revoked tokens do not depend on the current
            # distribution key and retain the old idempotent deletion contract.
            return self.authenticate(authorization)
        except ApiError as error:
            if error.status != 401:
                raise
        try:
            self.automatic_enrollments.authorize(enrollment_key)
        except ApiError:
            raise ApiError(401, "authentication_required") from None
        if (not isinstance(authorization, str)
                or not re.fullmatch(r"Bearer [a-f0-9]{64}", authorization)):
            raise ApiError(400, "enrollment_token")
        return None  # Valid gate, but no identity is created during HTTP precheck.

    def handle(self, method, path, authorization, body=None, enrollment_key=None):
        if path == "/enrollments":
            if method != "POST":
                raise ApiError(404, "not_found")
            self.automatic_enrollments.authorize(enrollment_key)
            with self.lock, self.connect() as db:
                return self.automatic_enrollments.register(db, body, self.enrollments, int(self.clock() * 1000))
        withdrawal = method == "DELETE" and path == "/participant"
        subject = (self.authorize_withdrawal(authorization, enrollment_key) if withdrawal
                   else self.authenticate(authorization))
        now_ms = int(self.clock() * 1000)
        with self.lock, self.connect() as db:
            self._purge_expired(db, now_ms)
            # Retention is independent of the request's transaction. A rejected
            # batch must roll back its inserts without undoing expired-row removal.
            db.commit()
            if withdrawal:
                if subject is None:
                    # A local install may withdraw before its first successful
                    # upload/registration. Allocate and revoke atomically; no
                    # active interval or event upload is needed to finish it.
                    subject = self.automatic_enrollments.pending_withdrawal_subject(
                        db, authorization[7:], self.enrollments, now_ms)
                db.execute("DELETE FROM events WHERE subject=?", (subject,))
                db.execute("DELETE FROM measurements WHERE subject=?", (subject,))
                db.execute("DELETE FROM measurement_quality WHERE subject=?", (subject,))
                db.execute("DELETE FROM measurement_receipts WHERE subject=?", (subject,))
                db.execute("DELETE FROM measurement_transport WHERE subject=?", (subject,))
                db.execute("INSERT OR IGNORE INTO revoked VALUES (?)", (subject,))
                self.automatic_enrollments.revoke(db, hashlib.sha256(authorization[7:].encode()).digest(), subject)
                # Tombstone prevents late retries from recreating erased records.
                return {"deleted": True, "enrollment_revoked": True}
            if db.execute("SELECT 1 FROM revoked WHERE subject=?", (subject,)).fetchone():
                raise ApiError(403, "enrollment_revoked")
            if method == "POST" and path == "/measurements/batch":
                from server.measurements import accept
                return accept(db, subject, body, now_ms, ApiError)
            if method == "GET" and path == "/policy/current":
                if self.policy_file is None:
                    return dict(BASELINE)
                try:
                    policy = json.loads(Path(self.policy_file).read_text())
                    return validate_policy(policy, now_ms)
                except (ValueError, OSError, TypeError, KeyError):
                    raise ApiError(503, "policy_unavailable") from None
            if method != "POST" or path != "/events/batch":
                raise ApiError(404, "not_found")
            if not isinstance(body, dict) or set(body) != {"schema_version", "consent_version", "events"} or type(body["schema_version"]) is not int or body["schema_version"] != 1 or type(body["consent_version"]) is not int or body["consent_version"] != 1:
                raise ApiError(400, "batch_schema")
            events = body["events"]
            if not isinstance(events, list) or not 1 <= len(events) <= MAX_BATCH:
                raise ApiError(400, "batch_size")
            # Validate the whole batch before writing: partial success is ambiguous.
            for event in events:
                validate_event(event, now_ms)
            accepted = []
            for event in events:
                normalized=dict(event)
                if not (event.get("classification") or {}).get("classified"):normalized.pop("classification",None)
                encoded = canonical(normalized)
                previous = db.execute("SELECT payload FROM events WHERE subject=? AND event_id=?",
                                      (subject, event["event_id"])).fetchone()
                if previous and previous[0] != encoded:
                    raise ApiError(409, "event_identity_conflict")
                db.execute("INSERT OR IGNORE INTO events VALUES (?, ?, ?, ?, ?)",
                           (subject, event["event_id"], event["decision_id"], event["occurred_utc_ms"], encoded))
                accepted.append(event["event_id"])
            return {"accepted_event_ids": accepted}


def make_server(service, address=("127.0.0.1", 0), max_connections=MAX_CONNECTIONS,
                request_timeout=REQUEST_TIMEOUT_SECONDS):
    class Handler(BaseHTTPRequestHandler):
        server_version = "GardenPersonalization"

        def setup(self):
            # Limit a stalled peer before BaseHTTPRequestHandler reads headers,
            # not only after it has parsed a complete request.
            self.request.settimeout(request_timeout)
            super().setup()

        def log_message(self, *_args):
            pass  # Paths, bearer headers and behavioral bodies must not enter logs.

        def do_GET(self):
            self.respond("GET")

        def do_POST(self):
            self.respond("POST")

        def do_DELETE(self):
            self.respond("DELETE")

        def respond(self, method):
            retry_after = None
            try:
                if self.headers.get("Transfer-Encoding"):
                    raise ApiError(400, "unsupported_transfer")
                length = int(self.headers.get("Content-Length", "0"))
                enrollment_route = self.path == "/enrollments"
                if length < 0 or length > (MAX_ENROLLMENT_BODY if enrollment_route else MAX_BODY):
                    raise ApiError(413, "body_too_large")
                if method == "POST" and self.headers.get_content_type() != "application/json":
                    raise ApiError(415, "content_type")
                # Authenticate before reading an attacker-controlled body.
                authorization = self.headers.get("Authorization")
                enrollment_key = None
                if enrollment_route:
                    if method != "POST":
                        raise ApiError(404, "not_found")
                    keys = self.headers.get_all("X-Enrollment-Key", [])
                    enrollment_key = keys[0] if len(keys) == 1 else None
                    service.automatic_enrollments.authorize(enrollment_key)
                elif method == "DELETE" and self.path == "/participant":
                    keys = self.headers.get_all("X-Enrollment-Key", [])
                    enrollment_key = keys[0] if len(keys) == 1 else None
                    service.authorize_withdrawal(authorization, enrollment_key)
                else:
                    service.authenticate(authorization)
                raw = self.rfile.read(length) if length else b""
                def unique_fields(pairs):
                    result = dict(pairs)
                    if len(result) != len(pairs):
                        raise ValueError("Duplicate enrollment field")
                    return result
                body = json.loads(raw, object_pairs_hook=unique_fields) if raw and enrollment_route else json.loads(raw) if raw else None
                value = service.handle(method, self.path, authorization, body, enrollment_key=enrollment_key)
                status = 200
            except ApiError as exc:
                value, status = {"error": exc.code}, exc.status
                retry_after = exc.retry_after
            except (ValueError, UnicodeError, RecursionError):
                value, status = {"error": "invalid_json"}, 400
            except (OSError, sqlite3.Error):
                value, status = {"error": "service_unavailable"}, 503
            encoded = canonical(value).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(encoded)))
            if retry_after is not None:
                self.send_header("Retry-After", str(retry_after))
            self.end_headers()
            self.wfile.write(encoded)

    class RetentionServer(ThreadingHTTPServer):
        def __init__(self, *args, **kwargs):
            if type(max_connections) is not int or max_connections < 1:
                raise ValueError("Positive maximum connection count required")
            self.connection_slots = threading.BoundedSemaphore(max_connections)
            super().__init__(*args, **kwargs)
            self.next_retention_check = 0.0

        def process_request(self, request, client_address):
            # The shared host's systemd TasksMax is a last resort. Reject excess
            # work promptly instead of spawning enough threads to reach it.
            if not self.connection_slots.acquire(blocking=False):
                try:
                    request.settimeout(1)
                    request.sendall(b"HTTP/1.1 503 Service Unavailable\r\n"
                                    b"Connection: close\r\nRetry-After: 5\r\n"
                                    b"Cache-Control: no-store\r\nContent-Length: 0\r\n\r\n")
                    # Closing with unread request bytes can replace the 503 with
                    # a TCP reset. Half-close writes first, then briefly drain
                    # in-flight input. Both work and time remain bounded; slow
                    # or oversized peers must not block the accept loop.
                    request.shutdown(socket.SHUT_WR)
                    request.settimeout(.05)
                    deadline = time.monotonic() + .05
                    remaining = MAX_BODY + 16384
                    while remaining > 0 and time.monotonic() < deadline:
                        chunk = request.recv(min(remaining, 8192))
                        if not chunk:
                            break
                        remaining -= len(chunk)
                except OSError:
                    pass
                finally:
                    self.shutdown_request(request)
                return
            try:
                super().process_request(request, client_address)
            except BaseException:
                self.connection_slots.release()
                raise

        def process_request_thread(self, request, client_address):
            try:
                super().process_request_thread(request, client_address)
            finally:
                self.connection_slots.release()

        def handle_error(self, request, client_address):
            # Transport failures can include peer addresses in stdlib tracebacks.
            # Clients keep unacknowledged batches and retry; log no request data.
            pass

        def service_actions(self):
            # serve_forever calls this hook even when there are no requests.
            if time.monotonic() < self.next_retention_check:
                return
            try:
                service.purge_expired()
                self.next_retention_check = time.monotonic() + 3600
            except (OSError, sqlite3.Error):
                # A temporary disk/lock error must not kill HTTP serving. Retry
                # the same deletion without logging behavioral contents.
                self.next_retention_check = time.monotonic() + 60

    return RetentionServer(address, Handler)


def main():
    enrollment_path = os.environ.get("CAPSTONE_ENROLLMENTS_FILE")
    if not enrollment_path:
        raise SystemExit("Set CAPSTONE_ENROLLMENTS_FILE to a private enrollment mapping.")
    try:
        enrollment_key = load_key(os.environ.get("CAPSTONE_ENROLLMENT_KEY_FILE"))
    except (OSError, ValueError, UnicodeError):
        raise SystemExit("Invalid private automatic enrollment key configuration.") from None
    service = EventService(os.environ.get("CAPSTONE_EVENTS_DB", "artifacts/personalization/events.sqlite"),
                           json.loads(Path(enrollment_path).read_text()), os.environ.get("CAPSTONE_POLICY_FILE"),
                           enrollment_key=enrollment_key)
    address = ("127.0.0.1", int(os.environ.get("CAPSTONE_API_PORT", "8091")))
    with make_server(service, address) as server:
        print("Development personalization API listening on loopback; authentication required.", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
