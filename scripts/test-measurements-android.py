#!/usr/bin/env python3
"""Real native measurements in an isolated test APK, with safe device restoration.

Build using build-notification-test-apk.sh. This is not Unity product E2E or a
real participant experiment. All outputs, including generated timestamps, stay private.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

from android_device_session import DeviceSession, durable_json, durable_bytes, restore_on_signal, stamp

PACKAGE = "com.capstonedesign2026.notificationpreview"
COMPONENT = PACKAGE + "/com.capstonedesign2026.platform.MeasurementValidationInstrumentation"


def verify_isolated_apk(apk, aapt=None):
    """Reject a product/wrong APK before creating a device session or installing."""
    apk = Path(apk).resolve()
    if not apk.is_file():
        raise ValueError("APK file is missing")
    if not aapt:
        sdk = Path(os.environ.get("ANDROID_SDK_ROOT") or os.environ.get("ANDROID_HOME") or "/opt/android-sdk")
        candidates = sorted((sdk / "build-tools").glob("*/aapt"), reverse=True)
        aapt = os.environ.get("AAPT_BIN") or (str(candidates[0]) if candidates else None)
    if not aapt:
        raise ValueError("Android aapt is required for offline APK identity verification; set AAPT_BIN")
    try:
        result = subprocess.run([aapt, "dump", "badging", str(apk)], capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.TimeoutExpired) as failure:
        raise ValueError("Offline APK identity verification could not run") from failure
    package = re.search(r"^package: name='([^']+)'", result.stdout, re.MULTILINE)
    if result.returncode or not package or package.group(1) != PACKAGE:
        raise ValueError("Refusing APK: only the isolated notificationpreview package is permitted")
    return apk


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", default=os.environ.get("ADB_SERIAL"), required=not os.environ.get("ADB_SERIAL"))
    parser.add_argument("--apk", required=True)
    parser.add_argument("--aapt", help="Android build-tools aapt path for offline APK identity verification")
    parser.add_argument("--output", default="artifacts/measurement-android-" + stamp())
    args = parser.parse_args()
    try:
        apk = verify_isolated_apk(args.apk, args.aapt)
    except ValueError as failure:
        parser.error(str(failure))
    output = Path(args.output)
    if output.exists() and any(output.iterdir()):
        parser.error("Use a fresh private output directory")
    report = {"status": "FAIL", "syntheticOnly": True, "scope": "native Android adapter in isolated package", "phases": {}}
    session = DeviceSession(args.serial, output, package=PACKAGE)
    try:
        with restore_on_signal(), session:
            session.install(apk) # Verified isolated package; backup, install -r and exact installed APK hash.
            for phase in ("produce", "restore"):
                # Remove only this synthetic fixture's old report, never product data.
                # A crashed instrumentation launch must not reuse a previous PASS.
                session.shell("run-as", PACKAGE, "rm", "-f", "cache/measurement-validation/" + phase + "-result.json")
                transcript = session.shell("am", "instrument", "-w", "-e", "phase", phase, COMPONENT)
                durable_bytes(session.output / (phase + "-instrumentation-private.txt"), transcript.encode())
                raw = session.adb("exec-out", "run-as", PACKAGE, "cat", "cache/measurement-validation/" + phase + "-result.json")
                durable_bytes(session.output / (phase + "-result.json"), raw)
                result = json.loads(raw)
                if result.get("status") != "PASS" or not result.get("checks") or not isinstance(result.get("processId"), int):
                    raise RuntimeError("Native phase did not provide successful current evidence")
                report["phases"][phase] = result
            if report["phases"]["produce"]["processId"] == report["phases"]["restore"]["processId"]:
                raise RuntimeError("Restore phase did not run in a new process")
            raw = session.adb("exec-out", "run-as", PACKAGE, "cat", "cache/measurement-validation/pending.json")
            durable_bytes(session.output / "synthetic-notification-batch.json", raw)
        report["status"] = "PASS"
    except (Exception, KeyboardInterrupt) as failure:
        report["errorType"] = type(failure).__name__ # Raw diagnostics remain private.
    finally:
        report["displayRestored"] = bool(session.journal and session.journal.get("restored"))
        if not report["displayRestored"]:
            report["status"] = "FAIL"
        durable_json(session.output / "result.json", report)
    print(report["status"] + ": isolated native measurement phases; display restored=" + str(report["displayRestored"]))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
