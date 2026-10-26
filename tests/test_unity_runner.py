"""Wrapper process/lock contracts with a fake Editor and an isolated Library."""
import fcntl
import ctypes
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[1]
FAKE_EDITOR = '''
import json, os, pathlib, signal, sys, time
state = {"pid": os.getpid(), "parent": os.getppid(), "arguments": sys.argv[1:]}
hang = sys.argv[1] == "hang" or os.environ.get("FAKE_EDITOR_HANG") == "1"
if hang:
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    try:
        os.setsid()
        state["detached"] = True
    except PermissionError:
        state["detached"] = False
path = pathlib.Path(os.environ["FAKE_EDITOR_STATE"])
pending = path.with_suffix(".pending")
pending.write_text(json.dumps(state))
pending.replace(path)
if hang:
    while True:
        time.sleep(1)
raise SystemExit(int(sys.argv[2]))
'''


class UnityRunnerTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="synthetic-unity-wrapper-")
        self.addCleanup(temporary.cleanup)
        self.project = Path(temporary.name)
        scripts = self.project / "scripts"
        scripts.mkdir()
        self.wrapper = scripts / "unity-run.sh"
        shutil.copyfile(ROOT / "scripts/unity-run.sh", self.wrapper)
        self.fake = self.project / "fake_editor"
        self.fake.write_text("#!" + sys.executable + "\n" + FAKE_EDITOR)
        self.fake.chmod(0o700)
        self.state = self.project / "state.json"
        self.environment = {**os.environ, "UNITY_BIN": str(self.fake), "FAKE_EDITOR_STATE": str(self.state)}

    def assert_lock_available(self):
        with (self.project / "Library/capstone-editor.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)

    def test_editor_replaces_wrapper_preserving_pid_arguments_and_exit_status(self):
        arguments = ["exit", "19", "argument with spaces", "literal;$value"]
        process = subprocess.Popen(["bash", str(self.wrapper), *arguments], env=self.environment)
        self.assertEqual(process.wait(timeout=5), 19)
        state = json.loads(self.state.read_text())
        self.assertEqual(state["pid"], process.pid)
        self.assertEqual(state["arguments"], arguments)
        self.assert_lock_available()

    def test_timeout_kills_detached_term_ignoring_editor_and_releases_lock(self):
        # Foreground keeps timeout alive to reap its child after SIGKILL. The
        # child deliberately enters a different session, so direct PID tracking
        # is required; signalling only the original process group cannot work.
        process = subprocess.Popen(["timeout", "--foreground", "--signal=TERM", "--kill-after=0.2s",
                                    "0.5s", "bash", str(self.wrapper), "hang"], env=self.environment,
                                   start_new_session=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            self.assertEqual(process.wait(timeout=5), 137)
            state = json.loads(self.state.read_text())
            self.assertTrue(state["detached"], "fixture must exercise an Editor-owned session")
            with self.assertRaises(ProcessLookupError):
                os.kill(state["pid"], 0)
            self.assert_lock_available()
        finally:
            # Also clean up the fake if a future regression restores the forked
            # flock parent; never leave the failing fixture as a real orphan.
            if self.state.exists():
                try:
                    os.kill(json.loads(self.state.read_text())["pid"], signal.SIGKILL)
                except ProcessLookupError:
                    pass
            if process.poll() is None:
                process.kill()
            process.wait(timeout=5)

    def test_outer_qa_cancellation_reaches_detached_editor_and_releases_lock(self):
        # Adopt the monitor if cancellation ends its shell first, so even a
        # failing regression is fully reaped instead of leaving PID 1 a zombie.
        libc = ctypes.CDLL(None, use_errno=True)
        previous = ctypes.c_int()
        self.assertEqual(libc.prctl(37, ctypes.byref(previous), 0, 0, 0), 0)
        self.assertEqual(libc.prctl(36, 1, 0, 0, 0), 0)
        self.addCleanup(lambda: libc.prctl(36, previous.value, 0, 0, 0))
        play = self.project / "scripts/test-local-loop-play.sh"
        # Exercise the actual script and flags; scale only its two deadlines.
        source = (ROOT / "scripts/test-local-loop-play.sh").read_text()
        self.assertIn("--kill-after=15s 150s", source)
        play.write_text(source.replace("--kill-after=15s 150s", "--kill-after=0.2s 30s"))
        self.wrapper.chmod(0o700)
        driver = self.project / "qa_fixture.py"
        driver.write_text("import sys\n"
                          "sys.path.insert(0, sys.argv.pop(1))\n"
                          "import qa\n"
                          "play = sys.argv.pop(1)\n"
                          "qa.catalog = lambda _: [qa.Check('play-fixture', 'unity', ['bash', play])]\n"
                          "raise SystemExit(qa.run_cli())\n")
        output = self.project / "qa-report"
        process = subprocess.Popen([sys.executable, str(driver), str(ROOT / "scripts"), str(play),
                                    "--tier", "unity", "--output", str(output)],
                                   env={**self.environment, "FAKE_EDITOR_HANG": "1"},
                                   start_new_session=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        adopted = []
        try:
            deadline = time.monotonic() + 5
            while not self.state.exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(.025)
            self.assertTrue(self.state.exists(), "synthetic Editor did not start")
            state = json.loads(self.state.read_text())
            adopted = [state["pid"], state["parent"]]
            self.assertTrue(state["detached"])
            process.send_signal(signal.SIGTERM)  # Only QA; it must cancel its active child group.
            diagnostics = process.communicate(timeout=5)[0].decode()
            self.assertNotEqual(process.returncode, 0, diagnostics)
            report = json.loads((output / "results.json").read_text())
            self.assertTrue(report["interrupted"])
            self.assertEqual(report["summary"]["status"], "FAIL")
            deadline = time.monotonic() + 3
            while time.monotonic() < deadline:
                try:
                    os.kill(state["pid"], 0)
                except ProcessLookupError:
                    break
                time.sleep(.025)
            with self.assertRaises(ProcessLookupError):
                os.kill(state["pid"], 0)
            self.assert_lock_available()
        finally:
            for pid in adopted:
                try:
                    os.kill(pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            if process.poll() is None:
                process.terminate()
            process.communicate(timeout=5)
            for pid in adopted:
                try:
                    os.waitpid(pid, 0)
                except ChildProcessError:
                    pass


if __name__ == "__main__":
    unittest.main()
