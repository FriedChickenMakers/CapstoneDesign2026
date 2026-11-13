"""Private SQLite snapshots for the isolated QA service, never a raw file copy.

Run hourly: retain at most one snapshot per UTC day for seven days, and scrub
withdrawn/expired records from every managed snapshot. Only this module's exact
filename pattern is managed; existing operator archives are untouched.
"""
from __future__ import annotations

from datetime import datetime, timezone
import fcntl
import json
import os
from pathlib import Path
import re
import sqlite3
import tempfile
import time

DAY = 86400
NAME = re.compile(r"qa-daily-[0-9]{8}\.sqlite")


def revoked_tokens(db):
    if not db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='revoked_enrollment_tokens'").fetchone():
        return set()
    return set(db.execute("SELECT token_sha256,subject FROM revoked_enrollment_tokens"))


def maintain(database, directory, now=None, keep_days=7):
    now = time.time() if now is None else now
    if type(keep_days) is not int or not 1 <= keep_days <= 30:
        raise ValueError("Snapshot retention must be between one and thirty days")
    database, directory = Path(database).resolve(), Path(directory).resolve()
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    directory.chmod(0o700)
    lock_path = directory / ".maintenance.lock"
    with os.fdopen(os.open(str(lock_path), os.O_RDWR | os.O_CREAT, 0o600), "a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        today = datetime.fromtimestamp(now, timezone.utc).strftime("%Y%m%d")
        snapshot = directory / ("qa-daily-" + today + ".sqlite")
        if snapshot.is_symlink():
            raise ValueError("A managed snapshot cannot be a symlink")
        created = False
        with sqlite3.connect(database.as_uri() + "?mode=ro", uri=True, timeout=10) as live:
            revoked = {row[0] for row in live.execute("SELECT subject FROM revoked")}
            token_tombstones = revoked_tokens(live)
            if not snapshot.exists():
                descriptor, pending = tempfile.mkstemp(prefix=".snapshot-", suffix=".pending", dir=directory)
                os.close(descriptor)
                try:
                    with sqlite3.connect(pending) as backup:
                        live.backup(backup)
                        if backup.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                            raise RuntimeError("Snapshot integrity check failed")
                    with open(pending, "rb") as stream:
                        os.fsync(stream.fileno())
                    os.replace(pending, snapshot)
                    created = True
                finally:
                    if os.path.exists(pending):
                        os.unlink(pending)
        snapshots = [path for path in directory.iterdir() if NAME.fullmatch(path.name) and path.is_file() and not path.is_symlink()]
        # Retain revocations learned by earlier maintenance runs even if an
        # operator later restores an older live DB. Never resurrect withdrawn data.
        for path in snapshots:
            with sqlite3.connect(path.as_uri() + "?mode=ro", uri=True) as backup:
                revoked.update(row[0] for row in backup.execute("SELECT subject FROM revoked"))
                token_tombstones.update(revoked_tokens(backup))
        revoked.update(subject for _, subject in token_tombstones)
        scrubbed = removed = 0
        cutoff = int((now - 90 * DAY) * 1000)
        for path in snapshots:
            date = datetime.strptime(path.stem[len("qa-daily-"):], "%Y%m%d").replace(tzinfo=timezone.utc)
            if date.timestamp() < datetime.fromtimestamp(now, timezone.utc).replace(hour=0, minute=0, second=0, microsecond=0).timestamp() - (keep_days - 1) * DAY:
                path.unlink()
                removed += 1
                continue
            path.chmod(0o600)
            with sqlite3.connect(str(path), timeout=10) as backup:
                backup.execute("PRAGMA secure_delete=ON")
                backup.execute("CREATE TABLE IF NOT EXISTS revoked_enrollment_tokens (token_sha256 TEXT PRIMARY KEY, subject TEXT NOT NULL)")
                for digest, subject in token_tombstones:
                    backup.execute("INSERT OR IGNORE INTO revoked_enrollment_tokens VALUES (?, ?)", (digest, subject))
                for subject in revoked:
                    backup.execute("INSERT OR IGNORE INTO revoked VALUES (?)", (subject,))
                    for table in ("events", "measurements", "measurement_quality"):
                        scrubbed += backup.execute("DELETE FROM " + table + " WHERE subject=?", (subject,)).rowcount
                for table, field in (("events", "occurred"), ("measurements", "occurred"), ("measurement_quality", "updated")):
                    scrubbed += backup.execute("DELETE FROM " + table + " WHERE " + field + " < ?", (cutoff,)).rowcount
                if backup.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                    raise RuntimeError("Managed backup integrity check failed")
        descriptor = os.open(str(directory), os.O_RDONLY)
        try:
            os.fsync(descriptor)
        finally:
            os.close(descriptor)
        return {"snapshot_created": created, "managed_snapshots": len(snapshots) - removed,
                "expired_snapshots_removed": removed, "rows_scrubbed": scrubbed,
                "retention_days": keep_days, "integrity": "ok"}


def main():
    # File locations are operator configuration; summaries contain no subjects,
    # event bodies, credentials or addresses and are safe for the service journal.
    result = maintain(os.environ["CAPSTONE_EVENTS_DB"], os.environ["CAPSTONE_BACKUP_DIR"])
    print(json.dumps(result, sort_keys=True), flush=True)


if __name__ == "__main__":
    main()
