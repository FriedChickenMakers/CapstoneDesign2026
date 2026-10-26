#!/usr/bin/env python3
"""Explicit-target Android work with private backups and durable display restoration.

No command clears app data or logcat. An interrupted restoration remains on disk
and is completed before a later session can record new original values.
"""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import fcntl
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shlex
import signal
import subprocess
import sys
import tarfile
import time

ROOT = Path(__file__).resolve().parents[1]
SETTINGS = ("screen_off_timeout", "screen_brightness", "screen_brightness_mode")
SKIP = 77


class DeviceError(RuntimeError):
    pass


class DeviceSkip(DeviceError):
    pass


def private_directory(path):
    path = Path(path).resolve()
    if path.is_relative_to(ROOT) and not path.is_relative_to(ROOT / "artifacts"):
        raise DeviceError("Device evidence and restoration journals must stay in ignored artifacts or outside the repository")
    path.mkdir(parents=True, exist_ok=True, mode=0o700)
    path.chmod(0o700)
    return path


def durable_bytes(path, data):
    path = Path(path)
    temporary = path.with_name(path.name + ".pending")
    with temporary.open("wb") as stream:
        os.fchmod(stream.fileno(), 0o600)
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)
    descriptor = os.open(str(path.parent), os.O_RDONLY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def durable_json(path, data):
    durable_bytes(path, (json.dumps(data, indent=2) + "\n").encode())


def stamp():
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "-" + str(time.time_ns())


def protect_script(serial, output, read_only=False):
    """Re-exec a legacy Python entry point inside one safe session, before ADB."""
    if os.environ.get("CAPSTONE_DEVICE_SESSION_ACTIVE") == "1":
        return
    command = [sys.executable, str(Path(__file__).resolve()), "run", "--serial", serial,
               "--output", str(Path(output) / ("session-" + stamp()))]
    if read_only:
        command += ["--read-only"]
    command += ["--", sys.executable, *sys.argv]
    os.execv(sys.executable, command)


class DeviceSession:
    def __init__(self, serial, output, package="com.capstonedesign2026.mockup", journal_root=None, adb_bin=None):
        if not serial or serial.startswith("-") or any(c.isspace() for c in serial):
            raise DeviceError("An explicit ADB_SERIAL or --serial is required")
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+", package):
            raise DeviceError("Invalid Android package name")
        self.serial = serial
        self.package = package
        self.output = private_directory(output)
        self.adb_bin = adb_bin or os.environ.get("ADB_BIN", "/opt/android-sdk/platform-tools/adb")
        device_key = hashlib.sha256(serial.encode()).hexdigest()[:24]
        self.journal_dir = private_directory(Path(journal_root or ROOT / "artifacts/device-sessions") / device_key)
        self.journal_path = self.journal_dir / "display-restoration.json"
        self.journal = None
        self.lock = None

    def adb(self, *args, timeout=90):
        try:
            result = subprocess.run([self.adb_bin, "-s", self.serial, *map(str, args)],
                                    capture_output=True, timeout=timeout, check=False)
        except (OSError, subprocess.TimeoutExpired) as error:
            raise DeviceError("ADB command could not complete; reconnect the explicit target") from error
        if result.returncode:
            # Raw subprocess diagnostics can contain addresses, personal paths or records.
            durable_bytes(self.output / "adb-error-private.txt", result.stdout + result.stderr)
            raise DeviceError("ADB command failed; private diagnostics were preserved")
        return result.stdout

    def shell(self, *args):
        return self.adb("shell", shlex.join(map(str, args))).decode().strip()

    def save_journal(self):
        durable_json(self.journal_path, self.journal)

    def setting(self, name):
        value = self.shell("settings", "get", "system", name)
        if value != "null" and not re.fullmatch(r"-?\d+", value):
            raise DeviceError("Unexpected display setting response; original value was not guessed")
        return value

    def write_setting(self, name, value):
        if value == "null":
            self.shell("settings", "delete", "system", name)
        else:
            self.shell("settings", "put", "system", name, value)
        if self.setting(name) != value:
            raise DeviceError("Display setting verification failed")

    def restore(self):
        if self.journal is None and self.journal_path.exists():
            self.journal = json.loads(self.journal_path.read_text())
        if not self.journal or self.journal.get("restored"):
            return
        originals = self.journal.get("originals", {})
        failures = []
        for name in SETTINGS:
            if name not in originals:
                continue
            try:
                self.write_setting(name, originals[name])
                self.journal.setdefault("verified", {})[name] = True
            except DeviceError:
                failures.append(name)
                self.journal.setdefault("verified", {})[name] = False
            self.save_journal()
        self.journal["restored"] = not failures
        if not failures:
            self.journal["restoredAtUtc"] = datetime.now(timezone.utc).isoformat()
        self.save_journal()
        if failures:
            raise DeviceError("Display restoration is PENDING. Preserve " + str(self.journal_path) + " and run restore after reconnecting")

    def acquire(self):
        self.lock = (self.journal_dir / "session.lock").open("a")
        os.fchmod(self.lock.fileno(), 0o600)
        try:
            fcntl.flock(self.lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            self.lock.close()
            self.lock = None
            raise DeviceError("Another session already owns this target") from error

    def release(self):
        if self.lock:
            self.lock.close()
            self.lock = None

    def __enter__(self):
        self.acquire()
        try:
            if self.journal_path.exists():
                self.restore()  # Never reinterpret a previous temporary value as the original.
                durable_bytes(self.journal_dir / (stamp() + "-restored.json"), self.journal_path.read_bytes())
            self.journal = {"version": 1, "startedAtUtc": datetime.now(timezone.utc).isoformat(),
                            "originals": {}, "verified": {}, "restored": False}
            self.save_journal()
            self.journal["originals"]["screen_off_timeout"] = self.setting("screen_off_timeout")
            self.save_journal()
            self.write_setting("screen_off_timeout", "1800000")
            for name in SETTINGS[1:]:
                self.journal["originals"][name] = self.setting(name)
                self.save_journal()
            self.write_setting("screen_brightness_mode", "0")
            self.write_setting("screen_brightness", "20")
            return self
        except BaseException:
            try:
                self.restore()
            finally:
                self.release()
            raise

    def __exit__(self, kind, value, traceback):
        try:
            self.restore()
        finally:
            self.release()

    def backup(self, label="before-app-work"):
        destination = private_directory(self.output / (label + "-" + stamp()))
        # An installed app that is not debuggable must fail closed; an empty backup is not sufficient.
        installed = self.shell("pm", "path", self.package)
        if not installed:
            durable_json(destination / "manifest.json", {"status": "NOT_INSTALLED", "archives": []})
            return destination
        self.shell("run-as", self.package, "pwd")
        archives = []
        script = 'set --; for p in files shared_prefs databases no_backup; do if [ -e "$p" ]; then set -- "$@" "$p"; fi; done; if [ "$#" -gt 0 ]; then tar -cf - "$@"; else tar -cf - -T /dev/null; fi'
        # adb exec-out escapes each argument itself; adding shell quotes here
        # would make sh -c interpret the entire script as a command filename.
        internal = self.adb("exec-out", "run-as", self.package, "sh", "-c", script)
        archives.append(self.save_archive(destination / "internal.tar", internal))
        external_root = "/sdcard/Android/data/" + self.package + "/files"
        exists = self.shell("run-as", self.package, "sh", "-c", "if [ -d " + shlex.quote(external_root) + " ]; then echo present; else echo absent; fi")
        if exists == "present":
            external = self.adb("exec-out", "run-as", self.package, "tar", "-cf", "-", "-C", external_root, ".")
            archives.append(self.save_archive(destination / "external.tar", external))
        elif exists != "absent":
            raise DeviceError("Could not establish external garden save availability")
        durable_bytes(destination / "logcat-private.txt", self.adb("logcat", "-d", "-t", "10000"))
        durable_json(destination / "manifest.json", {"status": "COMPLETE", "external": exists, "archives": archives,
                     "consistency": "live snapshot; collectors may write during backup"})
        return destination

    @staticmethod
    def save_archive(path, data):
        # Preserve a failed transport response for private diagnosis. Validation
        # below still blocks install/launch; no COMPLETE manifest is written.
        durable_bytes(path, data)
        try:
            with tarfile.open(fileobj=io.BytesIO(data), mode="r:") as archive:
                count = len(archive.getmembers())
        except tarfile.TarError as error:
            raise DeviceError("Private backup is not a readable tar archive; app work stopped") from error
        return {"name": path.name, "entries": count, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}

    def install(self, apk):
        apk = Path(apk).resolve()
        if not apk.is_file():
            raise DeviceError("APK file is missing")
        self.backup("before-install")
        result = self.adb("install", "-r", apk, timeout=240)
        durable_bytes(self.output / "install-private.txt", result)
        if b"Success" not in result:
            raise DeviceError("APK install did not explicitly report Success")
        paths = self.shell("pm", "path", self.package).splitlines()
        if len(paths) != 1 or not paths[0].startswith("package:/"):
            raise DeviceError("Expected one installed base APK for hash verification")
        actual = self.shell("sha256sum", paths[0].removeprefix("package:")).split()[0]
        expected = hashlib.sha256(apk.read_bytes()).hexdigest()
        if actual != expected:
            raise DeviceError("Installed APK hash differs from the requested build")
        durable_json(self.output / "install-verification.json", {"status": "PASS", "apkSha256": expected, "inPlace": True})

    def launch(self):
        self.backup("before-launch")
        arguments = ["am", "start", "-W", "-n", self.package + "/com.unity3d.player.UnityPlayerGameActivity"]
        for variable, key in (("A7_TEST_MODE", "testScene"), ("PLATFORM_INPUT_MODE", "platformInputMode"),
                              ("PLATFORM_REPLAY_PATH", "platformReplayPath")):
            if os.environ.get(variable):
                arguments += ["--es", key, os.environ[variable]]
        output = self.shell(*arguments)
        durable_bytes(self.output / "launch-private.txt", output.encode())
        if "Error:" in output or "Status: ok" not in output:
            raise DeviceError("Android activity did not report a successful launch")

    def capture(self, name):
        durable_bytes(self.output / (name + ".png"), self.adb("exec-out", "screencap", "-p"))


@contextmanager
def restore_on_signal():
    previous = {}
    def interrupt(signum, frame):
        raise KeyboardInterrupt("Device session interrupted")
    for signum in (signal.SIGTERM, signal.SIGINT):
        previous[signum] = signal.signal(signum, interrupt)
    try:
        yield
    finally:
        for signum, handler in previous.items():
            signal.signal(signum, handler)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("deploy", "smoke", "capture", "restore", "run"))
    parser.add_argument("--serial", default=os.environ.get("ADB_SERIAL"))
    parser.add_argument("--output")
    parser.add_argument("--journal-root")
    parser.add_argument("--package", default=os.environ.get("ANDROID_PACKAGE", "com.capstonedesign2026.mockup"))
    parser.add_argument("--apk", default=os.environ.get("APK_PATH"))
    parser.add_argument("--read-only", action="store_true", help="Child command neither updates nor launches the app; omit app backup")
    tokens = sys.argv[1:]
    separator = tokens.index("--") if "--" in tokens else len(tokens)
    args = parser.parse_args(tokens[:separator])
    args.command = tokens[separator + 1:]
    output = args.output or Path(os.environ.get("CAPSTONE_ARTIFACTS", ROOT / "artifacts")) / ("device-" + stamp())
    try:
        session = DeviceSession(args.serial, output, args.package, args.journal_root)
        with restore_on_signal():
            if args.action == "restore":
                session.acquire()
                try:
                    session.restore()
                finally:
                    session.release()
            else:
                with session:
                    if args.action in ("deploy", "smoke"):
                        if args.apk:
                            session.install(args.apk)
                        elif args.action == "deploy":
                            raise DeviceError("--apk or APK_PATH is required for deployment")
                        session.launch()
                        time.sleep(float(os.environ.get("A7_SETTLE_SECONDS", "12")))
                    if args.action == "smoke":
                        session.capture("launch")
                        if not session.shell("pidof", session.package):
                            raise DeviceError("App process is absent after launch")
                        # Coordinates are opt-in, never silently inherited from an old layout.
                        taps = os.environ.get("A7_NAV_TAPS_JSON")
                        if taps:
                            for index, point in enumerate(json.loads(taps)):
                                if len(point) != 2 or not all(isinstance(v, int) and 0 <= v <= 10000 for v in point):
                                    raise DeviceError("Invalid navigation tap coordinates")
                                session.shell("input", "tap", *point)
                                time.sleep(1)
                                session.capture("navigation-" + str(index + 1))
                        durable_json(session.output / "smoke-result.json", {"status": "PASS", "scope": "launch/process/screenshots only", "navigationAssertions": False})
                    if args.action in ("capture", "smoke"):
                        session.capture("current")
                        durable_bytes(session.output / "logcat-private.txt", session.adb("logcat", "-d", "-t", "10000"))
                    if args.action == "run":
                        command = args.command[1:] if args.command[:1] == ["--"] else args.command
                        if not command:
                            raise DeviceError("run requires a child command after --")
                        if not args.read_only:
                            session.backup("before-child")
                        child = subprocess.run(command, env={**os.environ, "ADB_SERIAL": args.serial,
                            "CAPSTONE_DEVICE_SESSION_ACTIVE": "1", "CAPSTONE_DEVICE_OUTPUT": str(session.output)})
                        if child.returncode == SKIP:
                            raise DeviceSkip("Child device check reported SKIP; this is not a pass")
                        if child.returncode:
                            raise DeviceError("Child device check failed")
        print("PASS: requested device scope completed; original display settings verified")
        return 0
    except DeviceSkip as error:
        print("SKIP: " + str(error), file=sys.stderr)
        return SKIP
    except (DeviceError, KeyboardInterrupt, ValueError) as error:
        print("FAIL: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
