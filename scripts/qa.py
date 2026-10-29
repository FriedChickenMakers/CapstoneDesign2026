#!/usr/bin/env python3
"""Explicit QA tiers. A required SKIP is an unsuccessful gate, never PASS.

The default unit tier does not launch Unity or connect to Android. Unity and
device tiers require explicit selection and their own licensed/device setup.
"""
import argparse
from dataclasses import dataclass, field
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


@dataclass
class Check:
    name: str
    tier: str
    command: list
    dependencies: tuple = ()
    required: bool = True
    environment: dict = field(default_factory=dict)


def catalog(output):
    python = sys.executable
    checks = [Check("harness", "python", [python, "-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py", "-v"]),
              Check("server", "python", [python, "-m", "unittest", "discover", "-s", "server/tests", "-v"], ("server",))]
    for name in ("local-state", "mbct", "garden-appearance", "heart-range-refresh", "walking-baseline-refresh",
                 "walking-personalization", "activity-notifications", "personalization", "mission-recommendation", "focus-state", "all-missions"):
        checks.append(Check(name, "unit", ["bash", "scripts/test-" + name + ".sh"], ("mono",)))
    checks.append(Check("procedural-plants", "unit", [python, "scripts/test-procedural-plants.py"], ("plant-compiler",)))
    for name in ("health-query", "notification-policy", "notification-personas", "sensor-summary-unit", "daily-acceleration-unit", "measurement-buffer"):
        checks.append(Check(name, "unit", ["bash", "scripts/test-" + name + ".sh"], ("kotlin",)))
    unity = ["bash", "scripts/unity-run.sh", "-batchmode", "-projectPath", str(ROOT)]
    def editor(name, method, tier="unity", graphics=False, quit=True):
        command = unity + ([] if graphics else ["-nographics"]) + (["-quit"] if quit else [])
        return Check(name, tier, command + ["-executeMethod", "CapstoneDesign.EditorTools." + method, "-logFile", "-"],
                     ("unity", "graphics") if graphics else ("unity",))
    checks += [editor("project-bindings", "BuildTools.ValidateProject"),
               editor("local-loop", "LocalLoopValidation.Run"),
               Check("editmode", "unity", unity + ["-runTests", "-testPlatform", "EditMode", "-testResults", str(output / "editmode.xml"), "-logFile", "-"], ("unity", "graphics")),
               Check("play-save-restore", "unity", ["bash", "scripts/test-local-loop-play.sh"], ("unity", "graphics")),
               Check("personalization-network", "unity", [python, "scripts/test-personalization-play.py"], ("unity", "server")),
               editor("music", "MusicPlayValidation.Start", graphics=True, quit=False)]
    for name, method in (("tabs", "TabConsistencyCapture.Capture"), ("supplemental-ui", "TabConsistencyCapture.CaptureRemaining"),
                         ("guided-ui", "TabConsistencyCapture.CaptureGuidance"), ("demo-polish", "DemoPolishValidation.Capture"),
                         ("sky", "GardenSkyValidation.Capture"), ("lighting", "GardenLightingValidation.Capture"),
                         ("procedural-scene", "GardenProceduralValidation.Capture"),
                         ("focus-missions", "FocusMissionValidation.Run")):
        checks.append(editor(name, method, "visual", graphics=True))
    settings = editor("measurement-settings", "MeasurementSettingsCapture.Start", "visual", graphics=True, quit=False)
    settings.environment["CAPSTONE_PERSONALIZATION_TEST_ROOT"] = str(output / "measurement-settings" / "synthetic-enrollment")
    checks.append(settings)
    checks += [Check("android-launch", "device", ["bash", "scripts/test-a7.sh"], ("adb", "apk"), environment={"SKIP_BUILD": "1", "SKIP_INSTALL": "0"}),
               Check("collector-lifecycle", "device", ["bash", "scripts/test-platform-background.sh"], ("adb",))]
    return checks


def dependency_issue(dependencies, environ):
    mono = Path(environ.get("UNITY_MONO_ROOT", "/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge"))
    for dependency in dependencies:
        if dependency == "mono" and not ((mono / "bin/mono").is_file() and (mono / "lib/mono/4.5/mcs.exe").is_file()):
            return "Mono compiler/runtime missing; set UNITY_MONO_ROOT"
        if dependency == "plant-compiler":
            if not (environ.get("DOTNET_BIN") or shutil.which("dotnet") or (mono / "bin/mono").is_file()):
                return "Plant contracts require .NET or Mono"
        if dependency == "kotlin":
            lib = Path(environ.get("KOTLIN_GRADLE_LIB", "/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib"))
            if not list(lib.glob("kotlin-compiler*.jar")):
                return "Kotlin compiler jars missing; set KOTLIN_GRADLE_LIB"
            java = environ.get("JAVA_BIN") or shutil.which("java") or "/opt/android-jdk/bin/java"
            if not Path(java).is_file() and not shutil.which(java):
                return "Java runtime is missing"
        if dependency == "server" and not (ROOT / "server/tests").is_dir():
            return "Server contract tests are missing"
        if dependency == "unity":
            executable = environ.get("UNITY_BIN", "unity-editor")
            if not shutil.which(executable) and not Path(executable).is_file():
                return "Unity editor is missing; license setup is an explicit external prerequisite"
        if dependency == "graphics" and not environ.get("DISPLAY"):
            return "A graphics display is required for this tier (for example Xvfb)"
        if dependency == "apk" and not Path(environ.get("APK_PATH", "")).is_file():
            return "APK_PATH must identify the exact build for the installation gate"
        if dependency == "adb":
            if not environ.get("ADB_SERIAL"):
                return "An explicit ADB_SERIAL is required; no device is selected automatically"
            executable = environ.get("ADB_BIN", "/opt/android-sdk/platform-tools/adb")
            if not Path(executable).is_file() and not shutil.which(executable):
                return "ADB executable is missing"
    return None


def execute(check, output, environ=None, timeout=900):
    environment = {**(environ if environ is not None else os.environ), **check.environment}
    directory = output / check.name
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    environment.update({"CAPSTONE_ARTIFACTS": str(directory), "CAPSTONE_SMOKE_SAVE_ROOT": str(directory / "synthetic-save"),
                        "HEALTH_TEST_OUTPUT": str(directory), "NOTIFICATION_TEST_OUTPUT": str(directory),
                        "NOTIFICATION_PERSONA_OUTPUT": str(directory)})
    issue = dependency_issue(check.dependencies, environment)
    result = {"name": check.name, "tier": check.tier, "required": check.required, "status": "SKIP", "detail": issue or "", "seconds": 0}
    if issue:
        return result
    started = time.monotonic()
    try:
        with (directory / "output.log").open("wb") as log:
            os.fchmod(log.fileno(), 0o600)
            child = subprocess.Popen(check.command, cwd=ROOT, env=environment, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                exit_code = child.wait(timeout=timeout)
            except (subprocess.TimeoutExpired, KeyboardInterrupt):
                # Let a device harness run its finally restoration before a hard stop.
                try:
                    os.killpg(child.pid, signal.SIGTERM)
                except ProcessLookupError:
                    pass  # The child may finish as interruption arrives.
                try:
                    child.wait(timeout=30)
                except subprocess.TimeoutExpired:
                    try:
                        os.killpg(child.pid, signal.SIGKILL)
                    except ProcessLookupError:
                        pass
                    child.wait()
                raise
        result["exitCode"] = exit_code
        result["status"] = "PASS" if exit_code == 0 else "SKIP" if exit_code == 77 else "FAIL"
        if exit_code:
            result["detail"] = "Subprocess reported SKIP" if exit_code == 77 else "Subprocess failed; inspect its output.log"
        if check.name in ("harness", "server") and result["status"] == "PASS":
            text = (directory / "output.log").read_text(errors="replace")
            count = re.search(r"^Ran (\d+) tests? in ", text, re.MULTILINE)
            if count is None or int(count.group(1)) == 0:
                raise RuntimeError("Python discovery did not execute any tests")
            result["testCases"] = int(count.group(1))
        # Unity Test Runner has historically exited 0 in some incomplete runs.
        if check.name == "editmode" and result["status"] == "PASS":
            report = output / "editmode.xml"
            if not report.exists():
                raise RuntimeError("Unity test result XML was not produced")
            xml = ET.parse(report).getroot()
            if int(xml.attrib.get("testcasecount", xml.attrib.get("total", "0"))) == 0 or xml.attrib.get("result") != "Passed":
                raise RuntimeError("Unity test result did not report nonempty passed tests")
        if check.name == "measurement-settings" and result["status"] == "PASS":
            capture = json.loads((directory / "result.json").read_text())
            images = capture.get("images", [])
            if capture.get("status") != "PASS" or not capture.get("checks") or not images:
                raise RuntimeError("Settings capture must report nonempty passed checks and images")
            if any(Path(name).name != name or not (directory / "visual" / name).is_file() for name in images):
                raise RuntimeError("Settings capture report references missing images")
            result["testCases"] = len(capture["checks"])
            result["images"] = len(images)
    except (OSError, subprocess.TimeoutExpired, RuntimeError, ET.ParseError, json.JSONDecodeError) as error:
        result.update(status="FAIL", detail=type(error).__name__ + ": check could not finish; inspect its private output")
        if check.tier == "device":
            result["detail"] += "; check artifacts/device-sessions for pending display restoration before further device work"
    result["seconds"] = round(time.monotonic() - started, 3)
    return result


def summarize(results):
    counts = {status: sum(row["status"] == status for row in results) for status in ("PASS", "FAIL", "SKIP")}
    required_skips = sum(row["status"] == "SKIP" and row["required"] for row in results)
    status = "FAIL" if counts["FAIL"] or required_skips else "PARTIAL" if counts["SKIP"] else "PASS"
    return {"status": status, "counts": counts, "requiredSkipped": required_skips}, 1 if status == "FAIL" else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tier", choices=("python", "unit", "unity", "visual", "device"), action="append")
    parser.add_argument("--only", action="append", help="Select named checks from the chosen tiers")
    parser.add_argument("--optional", action="append", default=[], help="Explicitly permit a named check to skip; overall result then remains PARTIAL")
    parser.add_argument("--output")
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--timeout", type=int, default=900)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("timeout must be positive")
    tiers = set(args.tier or ["unit"])
    if "unit" in tiers:
        tiers.add("python")
    output = Path(args.output or ROOT / "artifacts" / ("qa-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "-" + str(time.time_ns()))).resolve()
    checks = [check for check in catalog(output) if check.tier in tiers and (not args.only or check.name in args.only)]
    unknown = set((args.only or []) + args.optional) - {check.name for check in checks}
    if unknown or not checks:
        parser.error("No checks selected, or unknown check in this tier: " + ", ".join(sorted(unknown)))
    for check in checks:
        check.required = check.name not in args.optional
    if args.list:
        for check in checks:
            print(check.tier + ": " + check.name + (" (required)" if check.required else " (optional)"))
        return 0
    if output.exists() and any(output.iterdir()):
        parser.error("Use a fresh output directory so prior results cannot be mistaken for this run")
    if output.is_relative_to(ROOT) and not output.is_relative_to(ROOT / "artifacts"):
        parser.error("QA evidence must be under ignored artifacts or outside the repository")
    output.mkdir(parents=True, exist_ok=True, mode=0o700)
    output.chmod(0o700)
    commit = subprocess.run(["git", "-c", "safe.directory=" + str(ROOT), "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True)
    dirty = subprocess.run(["git", "-c", "safe.directory=" + str(ROOT), "status", "--porcelain"], cwd=ROOT, capture_output=True, text=True)
    report = {"schemaVersion": 1, "commit": commit.stdout.strip(), "workingTreeDirty": bool(dirty.stdout.strip()), "startedAtUtc": datetime.now(timezone.utc).isoformat(),
              "scope": sorted(tiers), "deviceEvidencePrivate": "device" in tiers, "results": []}
    for check in checks:
        interrupted = False
        started = time.monotonic()
        try:
            result = execute(check, output, timeout=args.timeout)
        except KeyboardInterrupt:
            # execute has already signalled and waited for the separate child
            # group, including a device harness's restoration finally block.
            interrupted = True
            result = {"name": check.name, "tier": check.tier, "required": check.required,
                      "status": "FAIL", "detail": "QA interrupted; inspect the private child log",
                      "seconds": round(time.monotonic() - started, 3)}
            if check.tier == "device":
                result["detail"] += "; verify display restoration or preserve its pending journal"
        report["results"].append(result)
        report["summary"], code = summarize(report["results"])
        (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
        print(result["status"] + " " + check.name + (": " + result["detail"] if result["detail"] else ""), flush=True)
        if interrupted:
            report["interrupted"] = True
            (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
            break
    suite = ET.Element("testsuite", name="Capstone QA", tests=str(len(report["results"])),
                       failures=str(sum(r["status"] == "FAIL" or (r["status"] == "SKIP" and r["required"]) for r in report["results"])))
    for row in report["results"]:
        case = ET.SubElement(suite, "testcase", name=row["name"], classname=row["tier"], time=str(row["seconds"]))
        if row["status"] == "FAIL" or (row["status"] == "SKIP" and row["required"]):
            ET.SubElement(case, "failure", message=row["status"] + ": " + row["detail"])
        elif row["status"] == "SKIP":
            ET.SubElement(case, "skipped", message=row["detail"])
    ET.ElementTree(suite).write(output / "results.xml", encoding="utf-8", xml_declaration=True)
    print(json.dumps(report["summary"]))
    return code


def run_cli():
    # Children have separate process groups so a timeout can stop their whole
    # tree. Translate termination of this parent into the same cleanup path.
    def interrupt(signum, frame):
        raise KeyboardInterrupt()
    previous = signal.signal(signal.SIGTERM, interrupt)
    try:
        return main()
    except KeyboardInterrupt:
        print("FAIL: QA interrupted", file=sys.stderr)
        return 130
    finally:
        signal.signal(signal.SIGTERM, previous)


if __name__ == "__main__":
    sys.exit(run_cli())
