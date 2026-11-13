#!/usr/bin/env python3
"""Offline APK gate for operator-managed QA. No ADB, enrollment or network actions."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import urlsplit
import zipfile

from android_device_session import durable_json, private_directory, stamp


class PreflightError(RuntimeError):
    pass


AUTOMATIC_CONFIG_ASSET = "assets/capstone-enrollment-config.json"
AUTOMATIC_CONFIG_LIMIT = 4096


def automatic_configuration(archive):
    """Return only fixed error codes and booleans, never endpoint/key/source bytes."""
    entries = [entry for entry in archive.infolist()
               if entry.filename.replace("\\", "/").rstrip("/").rsplit("/", 1)[-1].lower()
               == "capstone-enrollment-config.json"]
    result = {"present": bool(entries), "valid": False, "errors": []}
    if not entries:
        return result
    if len(entries) != 1:
        result["errors"].append("automatic-enrollment-config-duplicate")
        return result
    entry = entries[0]
    if entry.filename != AUTOMATIC_CONFIG_ASSET or entry.is_dir():
        result["errors"].append("automatic-enrollment-config-unexpected-path")
        return result
    if entry.file_size > AUTOMATIC_CONFIG_LIMIT:
        result["errors"].append("automatic-enrollment-config-too-large")
        return result
    try:
        def unique_object(pairs):
            value = {}
            for key, item in pairs:
                if key in value:
                    raise ValueError("Duplicate configuration field")
                value[key] = item
            return value
        with archive.open(entry) as stream:
            raw = stream.read(AUTOMATIC_CONFIG_LIMIT + 1)
        if len(raw) > AUTOMATIC_CONFIG_LIMIT:
            result["errors"].append("automatic-enrollment-config-too-large")
            return result
        config = json.loads(raw.decode("utf-8"), object_pairs_hook=unique_object)
        if not isinstance(config, dict) or set(config) != {"endpoint", "automaticEnrollmentKey"}:
            result["errors"].append("automatic-enrollment-config-fields-invalid")
            return result
        endpoint, key = config["endpoint"], config["automaticEnrollmentKey"]
        if not isinstance(key, str) or re.fullmatch(r"[a-f0-9]{64}", key) is None:
            result["errors"].append("automatic-enrollment-config-key-invalid")
        valid_origin = isinstance(endpoint, str) and 0 < len(endpoint) <= 2048
        if valid_origin:
            valid_origin = not any(character.isspace() or ord(character) < 33 or ord(character) == 127
                                   for character in endpoint) and not any(c in endpoint for c in "\\?#")
        if valid_origin:
            uri = urlsplit(endpoint)
            valid_origin = (uri.scheme == "https" and bool(uri.hostname) and uri.username is None
                            and uri.password is None and uri.path in ("", "/")
                            and not uri.query and not uri.fragment and not uri.netloc.endswith(":"))
            # Accessing .port also rejects nonnumeric/out-of-range ports.
            valid_origin = valid_origin and (uri.port is None or 1 <= uri.port <= 65535)
        if not valid_origin:
            result["errors"].append("automatic-enrollment-config-origin-invalid")
        result["valid"] = not result["errors"]
    except (ValueError, OSError, RuntimeError, zipfile.BadZipFile, NotImplementedError):
        # JSON, URL and ZIP exceptions can contain source content. Never report them.
        result["errors"].append("automatic-enrollment-config-unreadable")
    return result


def command(tool, *args):
    try:
        result = subprocess.run([str(tool), *map(str, args)], capture_output=True, text=True, timeout=120)
    except (OSError, subprocess.TimeoutExpired):
        raise PreflightError("APK inspection tool could not complete") from None
    if result.returncode:
        # Do not print certificate subjects, arbitrary APK strings or local paths.
        raise PreflightError("APK inspection or signature verification failed")
    return result.stdout


def parse_metadata(badging, certificates):
    package = re.search(r"^package: name='([^']+)' versionCode='([0-9]+)' versionName='([^']*)'", badging, re.M)
    digests = re.findall(r"^Signer #[0-9]+ certificate SHA-256 digest: ([a-fA-F0-9]{64})$", certificates, re.M)
    sdk = re.search(r"^sdkVersion:'([0-9]+)'", badging, re.M)
    target = re.search(r"^targetSdkVersion:'([0-9]+)'", badging, re.M)
    if not package or not digests or not sdk or not target:
        raise PreflightError("APK identity, SDK or signer metadata was incomplete")
    return dict(package=package[1], versionCode=int(package[2]), versionName=package[3],
                debuggable=bool(re.search(r"^application-debuggable\s*$", badging, re.M)),
                minSdk=int(sdk[1]), targetSdk=int(target[1]),
                certificateSha256=sorted(set(x.lower() for x in digests)))


def inspect(apk, aapt, apksigner):
    apk = Path(apk)
    if not apk.is_file():
        raise PreflightError("APK file is missing")
    info = parse_metadata(command(aapt, "dump", "badging", apk), command(apksigner, "verify", "--print-certs", apk))
    with apk.open("rb") as stream:
        info["sha256"] = hashlib.file_digest(stream, "sha256").hexdigest()
    info["bytes"] = apk.stat().st_size
    # Enrollment and private signing material must never be distributed in assets.
    with zipfile.ZipFile(apk) as archive:
        info["forbiddenBundledFiles"] = sorted(name for name in archive.namelist()
            if Path(name).name.lower() in ("personalization-enrollment.json", "enrollments.json")
            or name.lower().endswith((".keystore", ".jks", ".p12", ".pfx")))
        info["automaticEnrollment"] = automatic_configuration(archive)
    return info


def evaluate(info, expected_package, expected_version, distribution, previous=None, require_automatic_enrollment=False):
    failures = []
    automatic = info.get("automaticEnrollment", {"present": False, "valid": False, "errors": []})
    failures.extend(automatic["errors"])
    if require_automatic_enrollment and not automatic["present"]:
        failures.append("automatic-enrollment-config-required")
    if info["package"] != expected_package:
        failures.append("package-mismatch")
    if info["versionName"] != expected_version:
        failures.append("version-name-mismatch")
    if not re.fullmatch(r"[A-Za-z0-9._-]{1,48}", info["versionName"]):
        failures.append("version-name-not-compatible-with-measurement-schema")
    if info["versionName"] == "0.1.0-mockup":
        failures.append("qa-build-needs-distinct-version-name")
    if not 1 <= info["versionCode"] <= 2147483647:
        failures.append("invalid-version-code")
    if info["forbiddenBundledFiles"]:
        failures.append("enrollment-or-private-key-bundled")
    if distribution == "managed-qa":
        if not info["debuggable"]:
            failures.append("operator-run-as-enrollment-unavailable")
    elif distribution == "bootstrap-final":
        if info["debuggable"]:
            failures.append("final-qa-is-debuggable")
        if previous is None:
            failures.append("bootstrap-apk-required")
        elif not previous["debuggable"]:
            failures.append("previous-apk-is-not-debuggable-bootstrap")
        if previous is not None and previous["forbiddenBundledFiles"]:
            failures.append("bootstrap-bundles-enrollment-or-private-key")
    else:
        if info["debuggable"]:
            failures.append("release-is-debuggable")
        # Preserve the legacy release gate unless automatic provisioning is
        # explicitly requested and its bundled build configuration is validated.
        if not (require_automatic_enrollment and automatic["valid"]):
            failures.append("nondebug-enrollment-provisioning-not-implemented")
    if previous is not None:
        if info["package"] != previous["package"]:
            failures.append("update-package-changed")
        if info["certificateSha256"] != previous["certificateSha256"]:
            failures.append("update-signing-certificate-changed")
        if info["versionCode"] < previous["versionCode"]:
            failures.append("update-version-downgrade")
        if info["sha256"] != previous["sha256"] and info["versionCode"] <= previous["versionCode"]:
            failures.append("changed-build-needs-incremented-version-code")
    return failures


def evaluate_verifier(candidate, verifier):
    if verifier is None:
        return ["qa-verifier-apk-required"]
    failures = []
    if candidate["package"] != "com.capstonedesign2026.qa" or verifier["package"] != "com.capstonedesign2026.qa.verifier":
        failures.append("verifier-fixed-target-mismatch")
    if verifier["certificateSha256"] != candidate["certificateSha256"]:
        failures.append("verifier-signing-certificate-mismatch")
    if verifier["forbiddenBundledFiles"]:
        failures.append("verifier-bundles-enrollment-or-private-key")
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apk", required=True)
    parser.add_argument("--expected-package", required=True)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--distribution", choices=("managed-qa", "bootstrap-final", "release"), required=True)
    parser.add_argument("--previous-apk")
    parser.add_argument("--verifier-apk", help="Required for bootstrap-final: signed fixed-target registration verifier")
    parser.add_argument("--require-automatic-enrollment", action="store_true",
                        help="Require a valid bundled automatic-enrollment build configuration; contains no participant token")
    parser.add_argument("--aapt", default=os.environ.get("AAPT_BIN", "/opt/android-sdk/build-tools/36.0.0/aapt"))
    parser.add_argument("--apksigner", default=os.environ.get("APKSIGNER_BIN", "/opt/android-sdk/build-tools/36.0.0/apksigner"))
    parser.add_argument("--output")
    args = parser.parse_args()
    output = private_directory(args.output or Path(__file__).resolve().parents[1] / "artifacts" / ("qa-apk-preflight-" + stamp()))
    if any(output.iterdir()):
        parser.error("A fresh private output directory is required")
    report = {"status": "ERROR", "scope": "offline APK identity/signature/update gate; no device or server validation", "distribution": args.distribution}
    try:
        info = inspect(args.apk, args.aapt, args.apksigner)
        previous = inspect(args.previous_apk, args.aapt, args.apksigner) if args.previous_apk else None
        failures = evaluate(info, args.expected_package, args.expected_version, args.distribution, previous,
                            require_automatic_enrollment=args.require_automatic_enrollment)
        verifier = inspect(args.verifier_apk, args.aapt, args.apksigner) if args.verifier_apk else None
        if args.distribution == "bootstrap-final":
            failures.extend(evaluate_verifier(info, verifier))
        report.update(apk=info, previous=previous, verifier=verifier, failures=failures, status="BLOCKED" if failures else "PASS")
        registration_check = ("automatic first-LIVE registration after written consent; unique private token, offline replay and revoked-token rejection"
                              if args.require_automatic_enrollment else
                              "private individual registration after written consent; bootstrap provisioning before final in-place update")
        report["automaticEnrollmentRequired"] = args.require_automatic_enrollment
        report["operationalChecksStillRequired"] = [registration_check, "HTTPS real-device upload and retry; non-debuggable final rejects HTTP loopback", "notification permission and real posting/click", "server deletion and local/export backup reconciliation"]
    except (PreflightError, OSError, ValueError, zipfile.BadZipFile) as error:
        report["error"] = str(error) if isinstance(error, PreflightError) else type(error).__name__
    durable_json(output / "result.json", report)
    print(json.dumps({"status": report["status"], "report": str(output / "result.json"), "failures": report.get("failures", [])}))
    return 0 if report["status"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
