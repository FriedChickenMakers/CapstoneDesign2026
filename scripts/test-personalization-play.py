#!/usr/bin/env python3
"""Real Editor UnityWebRequest -> loopback API operator-registration lifecycle gate.

Only synthetic behavioral events are used. Enrollment, queue and SQLite data
are isolated and removed in finally; reports and logs stay in private artifacts.
The subprocess-fake unit tests validate this runner, not the Unity adapter.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import secrets
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "scripts"))
from android_device_session import durable_json, private_directory
from server.service import EventService, make_server

CLIENT_CHECKS = frozenset({"unregistered-install-does-not-collect", "unregistered-install-cannot-record",
                           "configured-with-explicit-test-root", "registered-start-enables-collection-without-ui",
                           "activation-write-failure-keeps-collection-disabled", "activation-retries-after-storage-recovers",
                           "measurement-write-failure-keeps-measurements-disabled",
                           "measurement-activation-retries-preserving-original-boundary",
                           "first-activation-boundaries-persisted", "client-recreation-preserves-activation-boundaries",
                           "synthetic-event-queued", "upload-acknowledged", "server-upload-confirmed",
                           "operator-withdrawal-immediate-local-clear", "pending-deletion-reload-cannot-reactivate",
                           "server-deletion-acknowledged", "revoked-enrollment-reload-cannot-reactivate",
                           "revoked-enrollment-cannot-record"})


class ValidationFailure(RuntimeError):
    """Only fixed, credential-free validation reasons belong in this exception."""


def database_counts(service):
    with service.lock, service.connect() as db:
        return (db.execute("SELECT COUNT(*) FROM events").fetchone()[0],
                db.execute("SELECT COUNT(*) FROM revoked").fetchone()[0])


def run_validation(output, command=None, timeout=360, environment=None):
    """Run a single isolated gate; command injection is for synthetic unit tests."""
    output = private_directory(output)
    if any(output.iterdir()):
        raise ValueError("A fresh output directory is required")
    work = Path(tempfile.mkdtemp(prefix="isolated-", dir=output))
    client_root = private_directory(work / "client")
    control = private_directory(work / "control")
    report = {"status": "FAIL", "scope": "UnityWebRequest operator-registration lifecycle" if command is None else "runner subprocess fixture",
              "checks": [], "clientChecks": [], "cleanupComplete": False}
    started = time.monotonic()
    child = server = thread = None
    try:
        enrollment_token, subject = secrets.token_urlsafe(48), uuid.uuid4().hex
        service = EventService(work / "events.sqlite", {enrollment_token: subject})
        server = make_server(service)
        thread = threading.Thread(target=server.serve_forever, kwargs={"poll_interval": .05}, daemon=True)
        thread.start()
        durable_json(client_root / "personalization-enrollment.json",
                     {"endpoint": "http://127.0.0.1:" + str(server.server_address[1]), "token": enrollment_token})
        env = dict(os.environ if environment is None else environment)
        env.update({"CAPSTONE_PERSONALIZATION_TEST_ROOT": str(client_root),
                    "CAPSTONE_PERSONALIZATION_TEST_CONTROL": str(control),
                    "CAPSTONE_SMOKE_SAVE_ROOT": str(work / "synthetic-save")})
        args = command if command is not None else ["bash", "scripts/unity-run.sh", "-batchmode", "-nographics",
                "-projectPath", str(ROOT), "-executeMethod", "CapstoneDesign.EditorTools.PersonalizationPlayValidation.Start",
                "-logFile", str(output / "unity.log")]
        confirmed = False
        with (output / "process.log").open("wb") as log:
            os.fchmod(log.fileno(), 0o600)
            child = subprocess.Popen(args, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            while True:
                if time.monotonic() - started > timeout:
                    raise ValidationFailure("Editor/network deadline exceeded")
                if not confirmed and (control / "uploaded.json").exists():
                    if database_counts(service) != (1, 0):
                        raise ValidationFailure("Upload marker did not match exactly one stored event")
                    report["checks"].append("server-stored-one-event-before-withdrawal")
                    durable_json(control / "server-upload-confirmed.json", {"status": "confirmed"})
                    confirmed = True
                if child.poll() is not None:
                    break
                time.sleep(.025)
            if child.returncode != 0:
                raise ValidationFailure("Editor process returned a failure")
        if not confirmed:
            raise ValidationFailure("No independently verified upload")
        result_path = control / "client-result.json"
        if not result_path.is_file():
            raise ValidationFailure("Editor did not produce a client result")
        result = json.loads(result_path.read_text())
        if result.get("status") != "PASS" or not CLIENT_CHECKS.issubset(set(result.get("checks", []))):
            raise ValidationFailure("Editor client assertions were incomplete or failed")
        report["clientChecks"] = sorted(CLIENT_CHECKS)
        if database_counts(service) != (0, 1):
            raise ValidationFailure("Withdrawal did not erase server events and revoke enrollment")
        report["checks"].append("server-zero-events-and-revoked-enrollment-after-withdrawal")
        report["status"] = "PASS"
    except (Exception, KeyboardInterrupt) as error:
        report["error"] = str(error) if isinstance(error, ValidationFailure) else type(error).__name__
    finally:
        if child is not None and child.poll() is None:
            os.killpg(child.pid, signal.SIGTERM)
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait(timeout=5)
        if server is not None:
            if thread is not None:
                server.shutdown()
            server.server_close()
        if thread is not None:
            thread.join(timeout=5)
        shutil.rmtree(work)
        report["cleanupComplete"] = not work.exists()
        report["seconds"] = round(time.monotonic() - started, 3)
        for retained in output.iterdir():
            if retained.is_file():
                retained.chmod(0o600)
        durable_json(output / "result.json", report)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output")
    parser.add_argument("--timeout", type=float, default=360)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("timeout must be positive")
    base = private_directory(os.environ.get("CAPSTONE_ARTIFACTS", str(ROOT / "artifacts")))
    output = Path(args.output) if args.output else Path(tempfile.mkdtemp(prefix="personalization-network-", dir=base))
    previous = signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    try:
        report = run_validation(output, timeout=args.timeout)
    finally:
        signal.signal(signal.SIGTERM, previous)
    print(report["status"] + ": actual UnityWebRequest operator-registration lifecycle; private report " + str(output / "result.json"))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
