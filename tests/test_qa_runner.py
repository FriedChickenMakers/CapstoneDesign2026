"""Synthetic execution tests: skip is never mistaken for successful coverage."""
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

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("capstone_qa", ROOT / "scripts/qa.py")
qa = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = qa
spec.loader.exec_module(qa)


class QaRunnerTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="synthetic-qa-")
        self.addCleanup(temporary.cleanup)
        self.output = Path(temporary.name)

    def execute(self, body, **options):
        check = qa.Check("fixture", "unit", [sys.executable, "-c", body], **options)
        return qa.execute(check, self.output, environ={}, timeout=2)

    def test_real_exit_status_and_private_log_are_preserved(self):
        result = self.execute("print('synthetic assertion'); raise SystemExit(3)")
        self.assertEqual(result["status"], "FAIL")
        self.assertEqual(result["exitCode"], 3)
        self.assertIn("synthetic assertion", (self.output / "fixture/output.log").read_text())
        self.assertEqual((self.output / "fixture/output.log").stat().st_mode & 0o077, 0)

    def test_required_skip_fails_gate(self):
        result = self.execute("raise SystemExit(77)")
        self.assertEqual(result["status"], "SKIP")
        summary, code = qa.summarize([result])
        self.assertEqual((summary["status"], summary["requiredSkipped"], code), ("FAIL", 1, 1))

    def test_explicit_optional_skip_is_partial_not_pass(self):
        result = self.execute("raise SystemExit(77)", required=False)
        summary, code = qa.summarize([result])
        self.assertEqual((summary["status"], code), ("PARTIAL", 0))

    def test_missing_device_never_runs_command(self):
        result = self.execute("raise RuntimeError('must not run')", dependencies=("adb",))
        self.assertEqual(result["status"], "SKIP")
        self.assertFalse((self.output / "fixture/output.log").exists())
        self.assertEqual(qa.summarize([result])[1], 1)

    def test_timeout_is_failure(self):
        check = qa.Check("timeout", "unit", [sys.executable, "-c", "import time; time.sleep(5)"])
        result = qa.execute(check, self.output, environ={}, timeout=.05)
        self.assertEqual(result["status"], "FAIL")

    def test_unit_catalog_cannot_connect_to_device_or_start_editor(self):
        selected = [check for check in qa.catalog(self.output) if check.tier in ("unit", "python")]
        self.assertGreaterEqual(len(selected), 15)
        self.assertTrue(all(not {"adb", "unity", "graphics"}.intersection(check.dependencies) for check in selected))

    def test_empty_unity_result_cannot_pass(self):
        (self.output / "editmode.xml").write_text('<test-run result="Passed" testcasecount="0"/>')
        result = qa.execute(qa.Check("editmode", "unity", [sys.executable, "-c", "pass"]), self.output, environ={})
        self.assertEqual(result["status"], "FAIL")

    def test_settings_capture_requires_nonempty_report_and_real_images(self):
        directory = self.output / "measurement-settings"
        directory.mkdir()
        report = directory / "result.json"
        check = qa.Check("measurement-settings", "visual", [sys.executable, "-c", "pass"])
        for payload in ({"status": "PASS", "checks": [], "images": []},
                        {"status": "PASS", "checks": ["synthetic-check"], "images": ["missing.png"]}):
            report.write_text(json.dumps(payload))
            self.assertEqual(qa.execute(check, self.output, environ={})["status"], "FAIL")

    def test_empty_python_discovery_cannot_pass(self):
        check = qa.Check("server", "python", [sys.executable, "-c", "print('Ran 0 tests in 0.000s\\nOK')"])
        self.assertEqual(qa.execute(check, self.output, environ={})["status"], "FAIL")

    def test_parent_sigterm_waits_for_child_cleanup_and_records_failure(self):
        ready, cleaned = self.output / "ready", self.output / "cleaned"
        child = self.output / "child.py"
        child.write_text("import pathlib, signal, sys, time\n"
                         "def stop(*_): raise KeyboardInterrupt()\n"
                         "signal.signal(signal.SIGTERM, stop)\n"
                         "try:\n"
                         " pathlib.Path(sys.argv[1]).write_text('ready')\n"
                         " time.sleep(20)\n"
                         "finally:\n"
                         " pathlib.Path(sys.argv[2]).write_text('cleanup complete')\n")
        wrapper = self.output / "qa_fixture.py"
        wrapper.write_text("import pathlib, sys\n"
                           "sys.path.insert(0, sys.argv.pop(1))\n"
                           "import qa\n"
                           "child, ready, cleaned = sys.argv[1:4]\n"
                           "del sys.argv[1:4]\n"
                           "qa.catalog = lambda _: [qa.Check('synthetic-child', 'unit', "
                           "[sys.executable, child, ready, cleaned])]\n"
                           "raise SystemExit(qa.run_cli())\n")
        output = self.output / "report"
        process = subprocess.Popen([sys.executable, str(wrapper), str(ROOT / "scripts"),
                                    str(child), str(ready), str(cleaned), "--output", str(output)],
                                   stdout=subprocess.PIPE, stderr=subprocess.STDOUT, start_new_session=True)
        try:
            deadline = time.monotonic() + 5
            while not ready.exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(.025)
            self.assertTrue(ready.exists(), "synthetic child did not start")
            process.send_signal(signal.SIGTERM)  # Parent only; child is a separate process group.
            diagnostics = process.communicate(timeout=8)[0].decode()
            self.assertNotEqual(process.returncode, 0, diagnostics)
            self.assertTrue(cleaned.exists(), diagnostics)
            report = json.loads((output / "results.json").read_text())
            self.assertTrue(report["interrupted"])
            self.assertEqual(report["summary"]["status"], "FAIL")
            self.assertEqual(report["results"][0]["name"], "synthetic-child")
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.communicate()


if __name__ == "__main__":
    unittest.main()
