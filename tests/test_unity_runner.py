"""Wrapper process/lock contracts with a fake Editor and an isolated Library."""
import fcntl
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
FAKE_EDITOR = '''
import json, os, pathlib, signal, sys, time
state = {"pid": os.getpid(), "arguments": sys.argv[1:]}
if sys.argv[1] == "hang":
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    try:
        os.setsid()
        state["detached"] = True
    except PermissionError:
        state["detached"] = False
pathlib.Path(os.environ["FAKE_EDITOR_STATE"]).write_text(json.dumps(state))
if sys.argv[1] == "hang":
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


if __name__ == "__main__":
    unittest.main()
