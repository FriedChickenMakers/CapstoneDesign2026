"""No hardware: a subprocess fake ADB exercises failure and restoration paths."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("device_session", ROOT / "scripts/android_device_session.py")
device = importlib.util.module_from_spec(spec)
spec.loader.exec_module(device)
collector_spec = importlib.util.spec_from_file_location("collector_lifecycle", ROOT / "scripts/test-daily-collector-lifecycle.py")
collector = importlib.util.module_from_spec(collector_spec)
sys.modules["android_device_session"] = device
collector_spec.loader.exec_module(collector)

FAKE_ADB = r'''#!/usr/bin/env python3
import hashlib, io, json, os, pathlib, shlex, subprocess, sys, tarfile, tempfile
p=pathlib.Path(os.environ['FAKE_ADB_STATE'])
s=json.loads(p.read_text())
args=sys.argv[1:]
assert args[:2]==['-s','emulator-5554'], 'explicit synthetic serial must be preserved'
args=args[2:]
s['calls'].append(args)
def save(): p.write_text(json.dumps(s))
save()
if s.get('offline'): sys.exit(1)
if args[0]=='shell':
 a=shlex.split(args[1])
 if a[:3]==['settings','get','system']: print(s['settings'].get(a[3],'null'))
 elif a[:2] in (['settings','put'],['settings','delete']):
  key=a[3]
  if a[1]=='put': s['settings'][key]=a[4]
  else: s['settings'].pop(key,None)
  save()
 elif a[:2]==['pm','path']: print('package:/data/app/synthetic/base.apk' if s.get('installed',True) else '')
 elif a[:1]==['sha256sum']: print(s.get('apkHash','mismatch')+'  /data/app/synthetic/base.apk')
 elif a[:1]==['run-as'] and a[2:] == ['pwd']: print('/data/user/0/synthetic')
 elif a[:1]==['run-as'] and a[2:4]==['sh','-c']: print('present')
 elif a[:2]==['am','start']: print('Status: ok')
 elif a[:1]==['pidof']: print('123')
 else: raise RuntimeError('unexpected synthetic shell command')
elif args[:2]==['install','-r']:
 s['apkHash']=hashlib.sha256(pathlib.Path(args[2]).read_bytes()).hexdigest()
 s['installed']=True
 save();print('Success')
elif args[:2]==['exec-out','run-as']:
 if s.get('failBackup'): sys.stdout.buffer.write(b'not a tar archive');sys.exit(0)
 if args[3:5]==['sh','-c']:
  # exec-out escapes argv itself. Execute the actual shell program against a
  # synthetic directory so double-quoting cannot be hidden by a canned tar.
  with tempfile.TemporaryDirectory(prefix='synthetic-remote-') as root:
   root=pathlib.Path(root)
   for name in ['files','shared_prefs']:
    (root/name).mkdir();(root/name/'synthetic.txt').write_text('synthetic preserved record')
   result=subprocess.run(['sh','-c',args[5]],cwd=root,capture_output=True)
   sys.stdout.buffer.write(result.stdout+result.stderr)
   # adb exec-out may return zero even when the remote program fails.
   sys.exit(0)
 data=b'synthetic save or preference fixture'
 stream=io.BytesIO()
 with tarfile.open(fileobj=stream,mode='w') as tar:
  entry=tarfile.TarInfo('synthetic-fixture.txt');entry.size=len(data);tar.addfile(entry,io.BytesIO(data))
 sys.stdout.buffer.write(stream.getvalue())
elif args[:2]==['exec-out','screencap']: sys.stdout.buffer.write(b'synthetic screenshot')
elif args[:2]==['logcat','-d']: print('synthetic preserved log')
else: raise RuntimeError('unexpected synthetic adb command')
'''


class AndroidDeviceSessionTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="synthetic-device-session-")
        self.addCleanup(temporary.cleanup)
        self.base = Path(temporary.name)
        self.fake = self.base / "adb"
        self.fake.write_text(FAKE_ADB)
        self.fake.chmod(0o700)
        self.state_path = self.base / "fake-state.json"
        self.originals = {"screen_off_timeout": "45000", "screen_brightness": "110", "screen_brightness_mode": "1"}
        self.write_state({"settings": dict(self.originals), "calls": []})
        env = patch.dict(os.environ, {"FAKE_ADB_STATE": str(self.state_path), "ADB_BIN": str(self.fake)})
        env.start()
        self.addCleanup(env.stop)

    def state(self):
        return json.loads(self.state_path.read_text())

    def write_state(self, state):
        self.state_path.write_text(json.dumps(state))

    def session(self, output="evidence"):
        return device.DeviceSession("emulator-5554", self.base / output, journal_root=self.base / "journals", adb_bin=str(self.fake))

    def test_originals_recorded_before_work_and_restored_on_exception(self):
        session = self.session()
        with self.assertRaisesRegex(RuntimeError, "synthetic failure"):
            with session:
                journal = json.loads(session.journal_path.read_text())
                self.assertEqual(journal["originals"], self.originals)
                self.assertFalse(journal["restored"])
                self.assertEqual(self.state()["settings"], {"screen_off_timeout": "1800000", "screen_brightness": "20", "screen_brightness_mode": "0"})
                self.assertEqual(self.state()["calls"][0], ["shell", "settings get system screen_off_timeout"])
                raise RuntimeError("synthetic failure")
        self.assertEqual(self.state()["settings"], self.originals)
        self.assertTrue(json.loads(session.journal_path.read_text())["restored"])
        self.assertEqual(session.journal_path.stat().st_mode & 0o077, 0)

    def test_missing_original_settings_are_deleted(self):
        state = self.state(); state["settings"] = {}; self.write_state(state)
        with self.session():
            pass
        self.assertEqual(self.state()["settings"], {})
        deletes = [call for call in self.state()["calls"] if call[0] == "shell" and call[1].startswith("settings delete")]
        self.assertEqual(len(deletes), 3)

    def test_disconnect_preserves_pending_originals_then_reconnect_restores_first(self):
        first = self.session()
        with self.assertRaisesRegex(device.DeviceError, "PENDING"):
            with first:
                state = self.state(); state["offline"] = True; self.write_state(state)
        pending = json.loads(first.journal_path.read_text())
        self.assertFalse(pending["restored"])
        self.assertEqual(pending["originals"], self.originals)
        state = self.state(); state["offline"] = False; state["calls"] = []; self.write_state(state)
        with self.session("reconnect") as second:
            self.assertEqual(second.journal["originals"], self.originals)
            self.assertEqual(self.state()["calls"][0], ["shell", "settings put system screen_off_timeout 45000"])
        self.assertEqual(self.state()["settings"], self.originals)

    def test_failed_backup_blocks_install_and_launch(self):
        state = self.state(); state["failBackup"] = True; self.write_state(state)
        apk = self.base / "synthetic.apk"; apk.write_bytes(b"synthetic APK")
        with self.assertRaisesRegex(device.DeviceError, "tar archive"):
            with self.session() as session:
                session.install(apk)
        self.assertFalse(any(call[0] == "install" for call in self.state()["calls"]))
        self.assertFalse(any(call[0] == "shell" and "am start" in call[1] for call in self.state()["calls"]))
        self.assertEqual(self.state()["settings"], self.originals)
        failed = list((self.base / "evidence").glob("*/internal.tar"))
        self.assertEqual(len(failed), 1)
        self.assertEqual(failed[0].read_bytes(), b"not a tar archive")
        self.assertEqual(failed[0].stat().st_mode & 0o077, 0)
        self.assertFalse(list((self.base / "evidence").glob("*/manifest.json")))

    def test_private_archives_precede_install_and_launch_and_logcat_is_preserved(self):
        apk = self.base / "synthetic.apk"; apk.write_bytes(b"synthetic APK")
        with self.session() as session:
            session.install(apk)
            session.launch()
        calls = self.state()["calls"]
        install = next(i for i, call in enumerate(calls) if call[0] == "install")
        launch = next(i for i, call in enumerate(calls) if call[0] == "shell" and call[1].startswith("am start"))
        backups = [i for i, call in enumerate(calls) if call[:2] == ["exec-out", "run-as"]]
        self.assertEqual(len(backups), 4)
        self.assertTrue(backups[1] < install < backups[2] < backups[3] < launch)
        self.assertFalse(any(call[:2] == ["logcat", "-c"] for call in calls))
        manifests = list(session.output.glob("*/manifest.json"))
        self.assertEqual(len(manifests), 2)
        self.assertTrue(all(json.loads(path.read_text())["status"] == "COMPLETE" for path in manifests))
        self.assertEqual(json.loads((session.output / "install-verification.json").read_text())["apkSha256"], hashlib.sha256(apk.read_bytes()).hexdigest())

    def test_second_session_cannot_mutate_same_device(self):
        with self.session():
            with self.assertRaisesRegex(device.DeviceError, "already owns"):
                with self.session("other"):
                    self.fail("must not enter")

    def test_invalid_or_absent_target_is_rejected_before_adb(self):
        for target in (None, "", "-e", "device with spaces"):
            with self.subTest(target=target), self.assertRaises(device.DeviceError):
                device.DeviceSession(target, self.base / "invalid")
        self.assertEqual(self.state()["calls"], [])

    def test_sigterm_restores_settings_via_finally(self):
        ready = self.base / "ready"
        child = [sys.executable, "-c", "from pathlib import Path; import time; Path(" + repr(str(ready)) + ").touch(); time.sleep(60)"]
        command = [sys.executable, str(ROOT / "scripts/android_device_session.py"), "run", "--serial", "emulator-5554",
                   "--journal-root", str(self.base / "journals"), "--output", str(self.base / "signal"), "--read-only", "--", *child]
        process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
        try:
            deadline = time.monotonic() + 8
            while not ready.exists() and time.monotonic() < deadline and process.poll() is None:
                time.sleep(.05)
            self.assertTrue(ready.exists(), "fake child did not start")
            os.killpg(process.pid, signal.SIGTERM)
            process.communicate(timeout=8)
            self.assertNotEqual(process.returncode, 0)
            self.assertEqual(self.state()["settings"], self.originals)
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.communicate()


class CollectorObservationTests(unittest.TestCase):
    def test_disabled_before_test_skips_but_interruption_during_test_fails(self):
        class StoppedSession:
            package = "com.example.synthetic"
            def shell(self, *args):
                return '{"status":"STOPPED"}'
        with self.assertRaises(device.DeviceSkip):
            collector.snapshot(StoppedSession(), allow_disabled=True)
        with self.assertRaises(device.DeviceError) as caught:
            collector.snapshot(StoppedSession())
        self.assertNotIsInstance(caught.exception, device.DeviceSkip)

    def test_sample_and_disk_write_must_both_advance(self):
        before = {"lastSampleEpochMs": 100, "lastWriteEpochMs": 100}
        self.assertFalse(collector.advanced(before, {"lastSampleEpochMs": 101, "lastWriteEpochMs": 100}))
        self.assertFalse(collector.advanced(before, {"lastSampleEpochMs": 100, "lastWriteEpochMs": 101}))
        self.assertTrue(collector.advanced(before, {"lastSampleEpochMs": 101, "lastWriteEpochMs": 101}))


if __name__ == "__main__":
    unittest.main()
