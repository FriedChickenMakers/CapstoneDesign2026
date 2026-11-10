"""Synthetic snapshots: online backup, bounded retention and withdrawn records."""
from datetime import datetime, timezone
from pathlib import Path
import secrets
import hashlib
import sqlite3
import tempfile
import unittest
import uuid

from server.maintenance import DAY, maintain
from server.service import EventService

NOW = datetime(2026, 11, 10, 12, tzinfo=timezone.utc).timestamp()


class MaintenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.db = Path(self.temp.name) / "synthetic.sqlite"
        self.directory = Path(self.temp.name) / "backups"
        self.subject = uuid.uuid4().hex
        self.token = secrets.token_urlsafe(32)
        self.service = EventService(self.db, {self.token: self.subject}, clock=lambda: NOW)
        with self.service.connect() as db:
            db.execute("INSERT INTO events VALUES (?, ?, ?, ?, ?)",
                       (self.subject, uuid.uuid4().hex, uuid.uuid4().hex, int(NOW * 1000), "{}"))

    def snapshot(self):
        return next(self.directory.glob("qa-daily-*.sqlite"))

    def test_online_backup_is_private_valid_and_not_recreated_same_day(self):
        result = maintain(self.db, self.directory, NOW)
        self.assertTrue(result["snapshot_created"])
        self.assertEqual(self.directory.stat().st_mode & 0o777, 0o700)
        self.assertEqual(self.snapshot().stat().st_mode & 0o777, 0o600)
        with sqlite3.connect(self.snapshot()) as db:
            self.assertEqual(db.execute("PRAGMA integrity_check").fetchone()[0], "ok")
            self.assertEqual(db.execute("SELECT COUNT(*) FROM events").fetchone()[0], 1)
        self.assertFalse(maintain(self.db, self.directory, NOW + 60)["snapshot_created"])

    def test_withdrawal_scrubs_every_managed_backup_and_preserves_tombstone(self):
        maintain(self.db, self.directory, NOW)
        self.service.handle("DELETE", "/participant", "Bearer " + self.token)
        result = maintain(self.db, self.directory, NOW + DAY)
        self.assertEqual(result["rows_scrubbed"], 1)
        for path in self.directory.glob("qa-daily-*.sqlite"):
            with sqlite3.connect(path) as db:
                self.assertEqual(db.execute("SELECT COUNT(*) FROM events").fetchone()[0], 0)
                self.assertEqual(db.execute("SELECT subject FROM revoked").fetchone()[0], self.subject)
                self.assertEqual(db.execute("SELECT token_sha256,subject FROM revoked_enrollment_tokens").fetchone(),
                                 (hashlib.sha256(self.token.encode()).hexdigest(), self.subject))
        # Even a mistaken rollback of the live DB cannot remove already backed-up revocations.
        with self.service.connect() as db:
            db.execute("DELETE FROM revoked")
            db.execute("DELETE FROM revoked_enrollment_tokens")
            db.execute("INSERT INTO events VALUES (?, ?, ?, ?, ?)",
                       (self.subject, uuid.uuid4().hex, uuid.uuid4().hex, int(NOW * 1000), "{}"))
        maintain(self.db, self.directory, NOW + 2 * DAY)
        newest = sorted(self.directory.glob("qa-daily-*.sqlite"))[-1]
        with sqlite3.connect(newest) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM events").fetchone()[0], 0)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked").fetchone()[0], 1)
            self.assertEqual(db.execute("SELECT COUNT(*) FROM revoked_enrollment_tokens").fetchone()[0], 1)

    def test_expiry_bounds_seven_daily_snapshots_and_preserves_operator_files(self):
        for day in range(9):
            maintain(self.db, self.directory, NOW + day * DAY)
        self.assertEqual(len(list(self.directory.glob("qa-daily-*.sqlite"))), 7)
        operator = self.directory / "operator-preserved.sqlite"
        operator.write_bytes(b"synthetic operator archive")
        result = maintain(self.db, self.directory, NOW + 91 * DAY)
        self.assertEqual(result["managed_snapshots"], 1)
        self.assertEqual(operator.read_bytes(), b"synthetic operator archive")
        with sqlite3.connect(self.snapshot()) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM events").fetchone()[0], 0)

    def test_symlink_is_not_written_through_and_missing_live_db_does_not_create_it(self):
        missing = Path(self.temp.name) / "missing.sqlite"
        with self.assertRaises(sqlite3.OperationalError):
            maintain(missing, self.directory, NOW)
        self.assertFalse(missing.exists())
        target = Path(self.temp.name) / "outside.txt"
        target.write_text("synthetic outside file")
        (self.directory / "qa-daily-20261110.sqlite").symlink_to(target)
        with self.assertRaises(ValueError):
            maintain(self.db, self.directory, NOW)
        self.assertEqual(target.read_text(), "synthetic outside file")


if __name__ == "__main__":
    unittest.main()
