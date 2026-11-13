#!/usr/bin/env python3
"""Regression tests use synthetic, reserved-domain data and a fake gitleaks."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


SCANNER = Path(__file__).resolve().parents[1] / "scripts/check-repo-privacy.py"
FAKE_GITLEAKS = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
assert args[0] == 'dir'
assert '--redact' in args and '--ignore-gitleaks-allow' in args
assert '--config' in args and '--gitleaks-ignore-path' in args
report = pathlib.Path(args[args.index('--report-path') + 1])
source = pathlib.Path(args[-1])
assert source.parent.stat().st_mode & 0o077 == 0
mode = os.environ.get('FAKE_GITLEAKS_MODE', '')
if mode == 'fail':
    print('unredacted-tool-diagnostic', file=sys.stderr)
    sys.exit(2)
if mode == 'missing-report':
    sys.exit(0)
if mode == 'malformed-report':
    report.write_text('{invalid-secret-report')
    sys.exit(0)
findings = []
for path in source.rglob('*'):
    if path.is_file() and b'fixture_' + b'secret' in path.read_bytes():
        findings.append({'File': str(path), 'StartLine': 1, 'RuleID': 'fixture',
                         'Secret': 'raw-secret-must-not-appear', 'Match': 'raw-match-must-not-appear'})
report.write_text(json.dumps(findings))
print('unredacted-tool-diagnostic')
sys.exit(1 if findings else 0)
'''


class RepoPrivacyTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="privacy-tests-")
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.repo = self.base / "repo"
        self.repo.mkdir()
        self.fake = self.base / "gitleaks"
        self.fake.write_text(FAKE_GITLEAKS)
        self.fake.chmod(0o700)
        self.git("init", "-q")
        self.git("config", "user.name", "Privacy Tests")
        self.git("config", "user.email", "privacy@example.invalid")

    def git(self, *args):
        return subprocess.run(["git", "-C", str(self.repo), *args], check=True, capture_output=True)

    def write(self, name, data):
        path = self.repo / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data if isinstance(data, bytes) else data.encode())
        return path

    def scan(self, *args, mode="", executable=None):
        result = subprocess.run(
            [sys.executable, str(SCANNER), *args], cwd=self.repo, capture_output=True, text=True,
            env={**os.environ, "GITLEAKS_BIN": str(executable or self.fake), "FAKE_GITLEAKS_MODE": mode},
        )
        output = result.stdout + result.stderr
        self.assertNotIn("unredacted-tool-diagnostic", output)
        self.assertNotIn("raw-secret-must-not-appear", output)
        self.assertNotIn("raw-match-must-not-appear", output)
        return result

    def rules(self, result):
        return {json.loads(line)["rule"] for line in result.stdout.splitlines() if line.startswith("{")}

    def test_private_identifiers_redacted(self):
        ip = ".".join(map(str, (192, 168, 7, 24)))
        email = "real.person" + "@" + "private-mail.invalidx"
        serial = "R58" + "N1234567"
        home = "/home/" + "private-person" + "/work"
        self.write("notes.txt", f"{ip}\n{email}\nserial={serial}\n{home}\n")
        result = self.scan()
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertEqual(self.rules(result), {"private-lan-address", "personal-or-unreviewed-email", "literal-device-serial", "personal-home-path"})
        for value in (ip, email, serial, home):
            self.assertNotIn(value, result.stdout + result.stderr)

    def test_rfc1918_ranges_and_reserved_examples(self):
        for first, second in ((10, 0), (172, 16), (172, 31), (192, 168)):
            with self.subTest(first=first, second=second):
                self.write("network.txt", ".".join(map(str, (first, second, 1, 9))))
                self.assertIn("private-lan-address", self.rules(self.scan()))
        self.write("network.txt", "192.0.2.9\n198.51.100.5\n203.0.113.7\n172.15.1.9\n172.32.1.9")
        self.assertEqual(self.scan().returncode, 0)

    def test_placeholders_and_known_public_license(self):
        self.write("README.md", "hello@example.com\nhello@example.invalid\n/home/codex/work\n/Users/user/work\nADB_SERIAL=${ADB_SERIAL}\nserial=sys.argv[1]\nserial=<serial>\nadb -s emulator-5554\n")
        self.write(".env.example", "TOKEN=<your-token>\n")
        self.write("Assets/Art/Prototype/Fonts/OFL.txt", "fonts" + "@" + "vendor.invalidx")
        self.assertEqual(self.scan().returncode, 0)

    def test_license_exception_does_not_cover_other_files_or_secrets(self):
        self.write("LICENSE.txt", "person" + "@" + "private.invalidx")
        self.assertIn("personal-or-unreviewed-email", self.rules(self.scan()))
        self.write("LICENSE.txt", "safe")
        self.write("Assets/Art/Prototype/Fonts/OFL.txt", "fixture_" + "secret")
        self.assertIn("gitleaks:fixture", self.rules(self.scan()))

    def test_private_document_link(self):
        self.write("link.md", "https://app.notion.com/p/" + "a" * 32)
        self.assertIn("private-document-link", self.rules(self.scan()))
        self.write("link.md", "https://www.notion.com/help/guides")
        self.assertEqual(self.scan().returncode, 0)

    def test_staged_scan_reads_index_not_worktree(self):
        path = self.write("config.txt", "fixture_" + "secret")
        self.git("add", ".")
        path.write_text("safe working copy")
        self.assertEqual(self.scan().returncode, 0)
        staged = self.scan("--staged")
        self.assertEqual(staged.returncode, 1)
        self.assertIn("gitleaks:fixture", self.rules(staged))
        self.git("add", ".")
        path.write_text("fixture_" + "secret")
        self.assertEqual(self.scan("--staged").returncode, 0)
        self.assertEqual(self.scan().returncode, 1)

    def test_entire_index_includes_unchanged_files(self):
        self.write("existing.txt", "fixture_" + "secret")
        self.git("add", ".")
        self.git("commit", "-qm", "fixture")
        self.assertEqual(self.scan("--staged").returncode, 1)

    def test_ignored_artifacts_excluded_but_force_added_rejected(self):
        self.write(".gitignore", "artifacts/\n.env\n")
        self.write("artifacts/private.txt", "fixture_" + "secret")
        self.write(".env", "fixture_" + "secret")
        self.assertEqual(self.scan().returncode, 0)
        self.git("add", "-f", "artifacts/private.txt", ".env")
        rules = self.rules(self.scan("--staged"))
        self.assertTrue({"private-artifact-path", "environment-file", "gitleaks:fixture"} <= rules)

    def test_artifact_sentinel_must_be_empty(self):
        self.write("artifacts/.gitkeep", "")
        self.assertEqual(self.scan().returncode, 0)
        self.write("artifacts/.gitkeep", "some data")
        self.assertIn("nonempty-artifact-sentinel", self.rules(self.scan()))

    def test_forbidden_local_files(self):
        names = ["device.p12", "release.keystore", "secret.key", "credentials.json", "account.yaml", "local-settings.json", "daily_acceleration/day.jsonl", "sensor_samples.jsonl", "platform_snapshots.jsonl", "garden-live/state.xml", "garden-state.json", "backup.tar", "brightness-journal.json"]
        for name in names:
            self.write(name, "data")
        result = self.scan()
        self.assertEqual(result.returncode, 1)
        paths = {json.loads(line)["path"] for line in result.stdout.splitlines()}
        self.assertEqual(paths, set(names))

    def test_deleted_files(self):
        path = self.write("removed.txt", "fixture_" + "secret")
        self.git("add", ".")
        path.unlink()
        self.assertEqual(self.scan().returncode, 0)
        self.assertEqual(self.scan("--staged").returncode, 1)
        self.git("rm", "--cached", "removed.txt")
        self.assertEqual(self.scan("--staged").returncode, 0)

    def test_symlinks_rejected_without_following_target(self):
        target = self.base / "outside.txt"
        target.write_text("fixture_" + "secret")
        (self.repo / "link.txt").symlink_to(target)
        self.assertEqual(self.rules(self.scan()), {"unsupported-symlink-or-gitlink"})
        self.git("add", ".")
        self.assertEqual(self.rules(self.scan("--staged")), {"unsupported-symlink-or-gitlink"})

    def test_binary_reaches_gitleaks_and_utf16_has_privacy_check(self):
        self.write("binary.bin", b"\x00\xfffixture_" + b"secret")
        self.assertIn("gitleaks:fixture", self.rules(self.scan()))
        self.write("binary.bin", b"\x00\xffsafe")
        self.write("text.txt", ("/Users/" + "private-person" + "/work").encode("utf-16"))
        self.assertIn("personal-home-path", self.rules(self.scan()))

    def test_scanner_fails_closed(self):
        self.write("safe.txt", "safe")
        for mode in ("fail", "missing-report", "malformed-report"):
            with self.subTest(mode=mode):
                self.assertEqual(self.scan(mode=mode).returncode, 2)
        self.assertEqual(self.scan(executable=self.base / "missing").returncode, 2)

    def test_not_a_git_repository_fails_closed(self):
        result = subprocess.run([sys.executable, str(SCANNER)], cwd=self.base, capture_output=True, text=True)
        self.assertEqual(result.returncode, 2)


if __name__ == "__main__":
    unittest.main()
