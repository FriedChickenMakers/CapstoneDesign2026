"""Actual local HTTP + subprocess fixtures test orchestration, not Unity code."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("personalization_play_runner", ROOT / "scripts/test-personalization-play.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)

FAKE_EDITOR = r'''
import json, os, sys, time, urllib.request, uuid
from pathlib import Path
root = Path(os.environ["CAPSTONE_PERSONALIZATION_TEST_ROOT"])
control = Path(os.environ["CAPSTONE_PERSONALIZATION_TEST_CONTROL"])
enrollment = json.loads((root / "personalization-enrollment.json").read_text())
mode = sys.argv[1]
def request(method, path, data=None):
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(enrollment["endpoint"] + path, body, method=method,
        headers={"Authorization": "Bearer " + enrollment["token"], "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=2) as response:
        return json.load(response)
def marker(name, value):
    temporary = control / (name + ".pending")
    temporary.write_text(json.dumps(value))
    temporary.replace(control / name)
if mode == "hang":
    time.sleep(20)
if mode == "exit-failure":
    raise SystemExit(7)
assert request("GET", "/policy/current")["model_type"] == "rules"
if mode != "no-upload":
    event = {"event_id": uuid.uuid4().hex, "decision_id": uuid.uuid4().hex,
        "kind": "recommendation_shown", "occurred_utc_ms": int(time.time() * 1000),
        "utc_offset_minutes": 0, "activity_id": "course:MBCT2W01", "policy_id": "local-rules-v1",
        "app_version": "synthetic-test", "content_version": "courses-v1", "features": [1, .5, .25, 1]}
    assert request("POST", "/events/batch", {"schema_version": 1, "consent_version": 1,
        "events": [event]})["accepted_event_ids"] == [event["event_id"]]
marker("uploaded.json", {"status": "uploaded"})
deadline = time.monotonic() + 3
while not (control / "server-upload-confirmed.json").exists():
    if time.monotonic() > deadline:
        raise SystemExit(8)
    time.sleep(.01)
if mode != "no-delete":
    assert request("DELETE", "/participant")["enrollment_revoked"]
if mode != "no-result":
    checks = json.loads(os.environ["SYNTHETIC_CLIENT_CHECKS"])
    if mode == "incomplete-result":
        checks = checks[:1]
    if mode == "missing-storage-recovery-result":
        checks = [check for check in checks if check not in {
            "activation-write-failure-keeps-collection-disabled", "activation-retries-after-storage-recovers",
            "measurement-write-failure-keeps-measurements-disabled",
            "measurement-activation-retries-preserving-original-boundary"}]
    if mode == "legacy-consent-result":
        checks = ["configured-with-explicit-test-root", "no-recording-before-consent", "consent-opt-in",
            "synthetic-event-queued", "upload-acknowledged", "server-upload-confirmed",
            "withdrawal-immediate-local-clear", "server-deletion-acknowledged",
            "revoked-enrollment-cannot-reconsent", "revoked-enrollment-cannot-record"]
    marker("client-result.json", {"status": "PASS", "checks": checks})
'''


class PersonalizationPlayRunnerTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="synthetic-network-runner-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.fake = self.root / "fake_editor.py"
        self.fake.write_text(FAKE_EDITOR)
        self.output = self.root / "output"

    def run_fixture(self, mode, timeout=5):
        report = runner.run_validation(self.output, [sys.executable, str(self.fake), mode], timeout,
            {"SYNTHETIC_CLIENT_CHECKS": json.dumps(sorted(runner.CLIENT_CHECKS))})
        self.assertTrue(report["cleanupComplete"])
        self.assertFalse(list(self.output.glob("isolated-*")))
        self.assertEqual(json.loads((self.output / "result.json").read_text()), report)
        for path in self.output.iterdir():
            self.assertEqual(path.stat().st_mode & 0o077, 0)
        return report

    def test_real_api_upload_observed_then_erased_but_scope_is_fixture(self):
        report = self.run_fixture("success")
        self.assertEqual(report["status"], "PASS")
        self.assertEqual(report["scope"], "runner subprocess fixture")
        self.assertEqual(len(report["checks"]), 2)
        self.assertEqual(set(report["clientChecks"]), runner.CLIENT_CHECKS)

    def test_unsubstantiated_upload_marker_cannot_pass(self):
        report = self.run_fixture("no-upload")
        self.assertEqual(report["status"], "FAIL")
        self.assertIn("stored event", report["error"])

    def test_client_pass_cannot_mask_remaining_server_records(self):
        report = self.run_fixture("no-delete")
        self.assertEqual(report["status"], "FAIL")
        self.assertIn("erase server events", report["error"])

    def test_successful_process_with_missing_result_cannot_pass(self):
        self.assertEqual(self.run_fixture("no-result")["status"], "FAIL")

    def test_successful_process_with_incomplete_assertions_cannot_pass(self):
        self.assertEqual(self.run_fixture("incomplete-result")["status"], "FAIL")

    def test_legacy_manual_consent_result_cannot_prove_automatic_registration(self):
        report = self.run_fixture("legacy-consent-result")
        self.assertEqual(report["status"], "FAIL")
        self.assertIn("incomplete", report["error"])

    def test_activation_without_storage_recovery_evidence_cannot_pass(self):
        report = self.run_fixture("missing-storage-recovery-result")
        self.assertEqual(report["status"], "FAIL")
        self.assertIn("incomplete", report["error"])

    def test_process_failure_preserved_and_enrollment_removed(self):
        report = self.run_fixture("exit-failure")
        self.assertEqual(report["status"], "FAIL")
        self.assertIn("process returned", report["error"])

    def test_timeout_terminates_child_and_removes_private_credentials(self):
        report = self.run_fixture("hang", timeout=.15)
        self.assertEqual(report["status"], "FAIL")
        self.assertIn("deadline", report["error"])
        self.assertLess(report["seconds"], 3)

    def test_stale_results_are_rejected_before_running_any_process(self):
        self.output.mkdir()
        (self.output / "stale.json").write_text("{}")
        with self.assertRaises(ValueError):
            runner.run_validation(self.output, ["must-not-start"])
        self.assertTrue((self.output / "stale.json").exists())


if __name__ == "__main__":
    unittest.main()
