"""Minimal distribution-key gate for persistent, anonymous install registration.

The shared key is not device attestation. Only a SHA-256 hash of each app-created
token is stored; its random subject survives retries, process restarts and
withdrawal. Existing manual registrations keep their original subject.
"""
from __future__ import annotations

import hashlib
import hmac
import os
from pathlib import Path
import re
import stat
import uuid

MAX_ENROLLMENT_BODY = 1024
HOURLY_NEW_LIMIT = 240
DAILY_NEW_LIMIT = 1000
HOUR_MS = 3600000
DAY_MS = 24 * HOUR_MS


def load_key(path):
    """An absent setting disables the route; a configured bad file fails closed."""
    if not path:
        return None
    with Path(path).open("rb") as stream:
        metadata = os.fstat(stream.fileno())
        if not stat.S_ISREG(metadata.st_mode) or metadata.st_mode & 0o077:
            raise ValueError("Automatic enrollment key must be an owner-private regular file")
        raw = stream.read(257)
        if len(raw) > 256:
            raise ValueError("Automatic enrollment key file is too large")
        key = raw.decode("ascii").rstrip("\r\n")
    if not re.fullmatch(r"[a-f0-9]{64}", key):
        raise ValueError("Automatic enrollment key has invalid format")
    return key


class AutomaticEnrollments:
    def __init__(self, key, error, limits=(HOURLY_NEW_LIMIT, DAILY_NEW_LIMIT)):
        if key is not None and (not isinstance(key, str) or not re.fullmatch(r"[a-f0-9]{64}", key)):
            raise ValueError("Automatic enrollment key has invalid format")
        if (not isinstance(limits, tuple) or len(limits) != 2
                or any(type(value) is not int or value < 1 for value in limits)):
            raise ValueError("Positive automatic enrollment limits required")
        self.key_digest = hashlib.sha256(key.encode("ascii")).digest() if key is not None else None
        self.error, self.limits = error, limits

    @staticmethod
    def initialize(db, manual):
        db.executescript("""
            CREATE TABLE IF NOT EXISTS automatic_enrollments (
                token_sha256 TEXT PRIMARY KEY, subject TEXT NOT NULL UNIQUE,
                created_utc_ms INTEGER NOT NULL, app_version TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS automatic_enrollments_created
                ON automatic_enrollments(created_utc_ms);
            CREATE TABLE IF NOT EXISTS revoked_enrollment_tokens (
                token_sha256 TEXT PRIMARY KEY, subject TEXT NOT NULL);
        """)
        # A later manual mapping must not silently reassign an existing install.
        for digest, subject in manual:
            row = db.execute("SELECT subject FROM automatic_enrollments WHERE token_sha256=?", (digest.hex(),)).fetchone()
            if row and row[0] != subject:
                raise ValueError("Manual and automatic enrollment identities conflict")
            revoked = db.execute("SELECT subject FROM revoked_enrollment_tokens WHERE token_sha256=?", (digest.hex(),)).fetchone()
            if revoked and revoked[0] != subject:
                raise ValueError("A revoked enrollment token cannot be reassigned")
            if db.execute("SELECT 1 FROM revoked WHERE subject=?", (subject,)).fetchone():
                AutomaticEnrollments.revoke(db, digest, subject)

    def authorize(self, supplied):
        if self.key_digest is None:
            raise self.error(404, "not_found")
        if not isinstance(supplied, str):
            raise self.error(401, "enrollment_authentication_required")
        digest = hashlib.sha256(supplied.encode("utf-8")).digest()
        if not hmac.compare_digest(digest, self.key_digest):
            raise self.error(401, "enrollment_authentication_required")

    @staticmethod
    def subject(db, digest):
        row = db.execute("SELECT subject FROM automatic_enrollments WHERE token_sha256=?", (digest.hex(),)).fetchone()
        if row is None:
            row = db.execute("SELECT subject FROM revoked_enrollment_tokens WHERE token_sha256=?", (digest.hex(),)).fetchone()
        return row[0] if row else None

    @staticmethod
    def revoke(db, digest, subject):
        db.execute("INSERT OR IGNORE INTO revoked_enrollment_tokens VALUES (?, ?)", (digest.hex(), subject))

    def register(self, db, body, manual, now_ms):
        if (not isinstance(body, dict) or set(body) != {"token", "app_version"}
                or not isinstance(body["token"], str) or not re.fullmatch(r"[a-f0-9]{64}", body["token"])
                or not isinstance(body["app_version"], str)
                or not re.fullmatch(r"[A-Za-z0-9._-]{1,48}", body["app_version"])):
            raise self.error(400, "enrollment_fields")
        digest = hashlib.sha256(body["token"].encode("ascii")).digest()
        # Serialize lookup/rate-limit/insert across processes, not only threads.
        db.execute("BEGIN IMMEDIATE")
        if db.execute("SELECT 1 FROM revoked_enrollment_tokens WHERE token_sha256=?", (digest.hex(),)).fetchone():
            raise self.error(403, "enrollment_revoked")
        subject = next((subject for expected, subject in manual if hmac.compare_digest(digest, expected)), None)
        subject = subject or self.subject(db, digest)
        if subject:
            if db.execute("SELECT 1 FROM revoked WHERE subject=?", (subject,)).fetchone():
                raise self.error(403, "enrollment_revoked")
            # Never rate-limit idempotent recovery or count it as a new install.
            return {"registered": True}
        self._allocate(db, digest, body["app_version"], manual, now_ms)
        return {"registered": True}

    def pending_withdrawal_subject(self, db, token, manual, now_ms):
        """Caller must write the withdrawal in this same transaction before commit."""
        if not isinstance(token, str) or not re.fullmatch(r"[a-f0-9]{64}", token):
            raise self.error(400, "enrollment_token")
        digest = hashlib.sha256(token.encode("ascii")).digest()
        db.execute("BEGIN IMMEDIATE")
        # Another worker may have registered/revoked it since the HTTP precheck.
        subject = next((subject for expected, subject in manual if hmac.compare_digest(digest, expected)), None)
        subject = subject or self.subject(db, digest)
        return subject or self._allocate(db, digest, "withdraw-before-registration", manual, now_ms)

    def _allocate(self, db, digest, app_version, manual, now_ms):
        # Global budgets avoid treating a school/classroom NAT as one user.
        # Unregistered withdrawals use the same budget and persist the same
        # identity hash, so they cannot become an unbounded tombstone endpoint.
        for window, limit in ((HOUR_MS, self.limits[0]), (DAY_MS, self.limits[1])):
            count = db.execute("SELECT COUNT(*) FROM automatic_enrollments WHERE created_utc_ms >= ?", (now_ms - window,)).fetchone()[0]
            if count >= limit:
                raise self.error(429, "enrollment_rate_limited", retry_after=60)
        manual_subjects = {subject for _, subject in manual}
        while True:
            subject = uuid.uuid4().hex
            if (subject not in manual_subjects and not db.execute("SELECT 1 FROM revoked WHERE subject=?", (subject,)).fetchone()
                    and not db.execute("SELECT 1 FROM automatic_enrollments WHERE subject=?", (subject,)).fetchone()):
                break
        db.execute("INSERT INTO automatic_enrollments VALUES (?, ?, ?, ?)",
                   (digest.hex(), subject, now_ms, app_version))
        return subject
