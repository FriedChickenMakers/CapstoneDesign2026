#!/usr/bin/env python3
"""Check publishable files without printing matched values.

Default: tracked working files and nonignored untracked files. --staged: the
entire index, including the staged version of partially staged files. Requires
gitleaks v8 with the `dir` command (PATH or GITLEAKS_BIN). Exit 1 means findings;
exit 2 means the check could not finish. This does not scan Git history, ignored
local evidence, or understand whether arbitrary prose/measurements are personal.
"""
import argparse
import ipaddress
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile


VENDOR_LICENSES = {
    "Assets/Art/External/KenneyNatureKit/LICENSE.txt",
    "Assets/Art/Prototype/Fonts/OFL.txt",
    "Assets/Resources/Fonts/OFL.txt",
    "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt",
}
EMAIL = re.compile(r"(?<![\w.+-])[\w.!#$%&'*+/=?^`{|}~-]+@(?:[\w-]+\.)+[A-Za-z]{2,}")
IPV4 = re.compile(r"(?<![\w.])(?:\d{1,3}\.){3}\d{1,3}(?![\w.])")
PERSONAL_PATH = re.compile(r"/(?:Users|home)/([^/\s\"'<>${}]+)")
SERIAL = re.compile(
    r"\b(?:adb_serial|android_serial|device_serial|device_id|serial)[\"']?\s*[:=]\s*[\"']?"
    r"([A-Za-z0-9][A-Za-z0-9_.:-]{5,})|\badb\s+-s\s+[\"']?([A-Za-z0-9]{6,})",
    re.IGNORECASE,
)
PRIVATE_DOC = re.compile(r"https?://app\.notion\.com/p/[a-f0-9]{32}(?![a-f0-9])", re.IGNORECASE)
GENERIC_USERS = {"codex", "user", "username", "developer", "runner", "ubuntu", "vscode", "example"}


class ScanError(Exception):
    """Messages must never contain file contents or subprocess diagnostics."""


def git(root, *args):
    result = subprocess.run(
        ["git", "-c", "safe.directory=" + str(root), "-C", str(root), *args],
        capture_output=True,
    )
    if result.returncode:
        raise ScanError("Git could not read the repository/index.")
    return result.stdout


def filename_rule(name):
    path = PurePosixPath(name)
    parts = {part.lower() for part in path.parts}
    base = path.name.lower()
    if "artifacts" in parts and name != "artifacts/.gitkeep":
        return "private-artifact-path"
    if (base == ".env" or base.startswith(".env.")) and base != ".env.example":
        return "environment-file"
    if path.suffix.lower() in {".pem", ".key", ".p12", ".pfx", ".keystore", ".jks", ".mobileprovision"}:
        return "private-key-container"
    if base in {"id_rsa", "id_ed25519", ".netrc", ".npmrc.local", "local.properties",
                "google-services.json", "googleservice-info.plist"}:
        return "local-credential-file"
    if re.fullmatch(r"\.?((?:service[-_])?accounts?|credentials?|local[-_]settings)(?:[._-].*)?", base):
        return "local-account-or-credential-file"
    if (parts & {"shared_prefs", "daily_accel", "daily_acceleration", "sensor_records", "sensor-records", "device-backup", "device-backups"}
            or (base == "state.xml" and any(part.startswith("garden-") for part in parts))):
        return "private-device-records"
    if (base in {"sensor_samples.jsonl", "platform_snapshots.jsonl", "screen-timeout-journal.json", "brightness-journal.json"}
            or re.fullmatch(r"(?:local-)?garden-state.*\.json(?:\.(?:bak|tmp))?", base)
            or re.fullmatch(r"logcat.*\.txt|.*\.logcat\.txt", base)
            or re.fullmatch(r"(?:sensor|accelerometer|acceleration)[_-].*\.(?:csv|jsonl|ndjson)", base)
            or re.fullmatch(r".*(?:backup|journal)\.(?:json|tar|zip|tgz|gz|ab)", base)
            or base.startswith("mono_crash.")
            or base.startswith(("performancetestruninfo.json", "performancetestrunsettings.json"))):
        return "private-device-or-session-data"
    return None


def content_findings(name, data):
    # Binary assets are still passed to gitleaks; avoid treating random font/image
    # bytes as personal text. UTF-16 text is common in exported Windows records.
    try:
        if data.startswith((b"\xff\xfe", b"\xfe\xff")):
            text = data.decode("utf-16")
        elif b"\x00" in data:
            return []
        else:
            text = data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return []
    findings = set()
    for number, line in enumerate(text.splitlines(), 1):
        for match in IPV4.finditer(line):
            try:
                octets = ipaddress.IPv4Address(match.group()).packed
            except ipaddress.AddressValueError:
                continue
            if octets[0] == 10 or (octets[0] == 172 and 16 <= octets[1] <= 31) or (octets[0] == 192 and octets[1] == 168):
                findings.add((name, number, "private-lan-address"))
        if name not in VENDOR_LICENSES:
            for match in EMAIL.finditer(line):
                domain = match.group().rsplit("@", 1)[1].lower()
                if domain not in {"example.com", "example.org", "example.net"} and not domain.endswith((".invalid", ".test", ".example")):
                    findings.add((name, number, "personal-or-unreviewed-email"))
        for match in PERSONAL_PATH.finditer(line):
            if match.group(1).lower() not in GENERIC_USERS:
                findings.add((name, number, "personal-home-path"))
        for match in SERIAL.finditer(line):
            value = match.group(1) or match.group(2)
            if re.fullmatch(r"[A-Za-z0-9]{6,}", value) and any(c.isdigit() for c in value):
                findings.add((name, number, "literal-device-serial"))
        if PRIVATE_DOC.search(line):
            findings.add((name, number, "private-document-link"))
    return sorted(findings)


def export_files(root, destination, staged):
    """Export bytes, never following symlinks or accepting conflicted entries."""
    entries = {}
    for entry in git(root, "ls-files", "--stage", "-z").split(b"\0"):
        if not entry:
            continue
        metadata, raw_name = entry.split(b"\t", 1)
        mode, oid, stage = metadata.decode("ascii").split()
        if stage != "0":
            raise ScanError("Resolve index conflicts before scanning.")
        entries[os.fsdecode(raw_name)] = (mode, oid)
    if not staged:
        for raw_name in git(root, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0"):
            if raw_name:
                entries[os.fsdecode(raw_name)] = (None, None)
    findings = []
    count = 0
    for name, (mode, oid) in sorted(entries.items()):
        path = PurePosixPath(name)
        if path.is_absolute() or ".." in path.parts:
            raise ScanError("Unsafe repository path.")
        if mode not in {None, "100644", "100755"}:
            findings.append((name, 1, "unsupported-symlink-or-gitlink"))
            continue
        if staged:
            data = git(root, "cat-file", "blob", oid)
        else:
            source = root / name
            if any(parent.is_symlink() for parent in (source, *source.parents) if parent != root and root in parent.parents):
                findings.append((name, 1, "unsupported-symlink-or-gitlink"))
                continue
            try:
                if not stat.S_ISREG(source.stat().st_mode):
                    findings.append((name, 1, "unsupported-nonregular-file"))
                    continue
                data = source.read_bytes()
            except FileNotFoundError:
                # Tracked deletion is absent from the default worktree scan.
                continue
            except OSError:
                raise ScanError("A repository file could not be read.") from None
        count += 1
        rule = filename_rule(name)
        if rule:
            findings.append((name, 1, rule))
        if name == "artifacts/.gitkeep" and data:
            findings.append((name, 1, "nonempty-artifact-sentinel"))
        findings.extend(content_findings(name, data))
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return findings, count


def gitleaks_findings(executable, source, temporary):
    report = temporary / "report.json"
    config = temporary / "defaults.toml"
    config.write_text("[extend]\nuseDefault = true\n")
    ignore = temporary / "empty.ignore"
    ignore.touch()
    command = [executable, "dir", "--no-banner", "--no-color", "--redact",
               "--config", str(config), "--ignore-gitleaks-allow",
               "--gitleaks-ignore-path", str(ignore), "--report-format", "json",
               "--report-path", str(report), str(source)]
    environment = {k: v for k, v in os.environ.items() if not k.startswith("GITLEAKS_CONFIG")}
    try:
        result = subprocess.run(command, cwd=temporary, env=environment, capture_output=True, timeout=300)
    except (OSError, subprocess.TimeoutExpired):
        raise ScanError("Gitleaks could not finish; check installation and retry.") from None
    # Never relay tool stdout/stderr: even diagnostic messages may contain secrets.
    if result.returncode not in {0, 1}:
        raise ScanError("Gitleaks failed; no successful privacy check was recorded.")
    try:
        payload = json.loads(report.read_text())
        if not isinstance(payload, list) or (result.returncode == 1 and not payload):
            raise ValueError
        findings = []
        for item in payload:
            path = Path(item["File"])
            if path.is_absolute():
                path = path.relative_to(source)
            if ".." in path.parts or not (source / path).is_file():
                raise ValueError
            line, rule = item["StartLine"], item["RuleID"]
            if not isinstance(line, int) or line < 1 or not isinstance(rule, str) or not re.fullmatch(r"[a-zA-Z0-9_.-]+", rule):
                raise ValueError
            findings.append((path.as_posix(), line, "gitleaks:" + rule))
        return findings
    except (OSError, ValueError, KeyError, TypeError):
        raise ScanError("Gitleaks returned an invalid or missing report.") from None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--staged", action="store_true", help="scan the exact complete Git index")
    args = parser.parse_args()
    try:
        root = Path(os.fsdecode(git(Path.cwd(), "rev-parse", "--show-toplevel")).strip()).resolve()
        configured = os.environ.get("GITLEAKS_BIN", "gitleaks")
        executable = shutil.which(configured)
        if not executable:
            raise ScanError("Gitleaks is required; install it or set GITLEAKS_BIN.")
        executable = str(Path(executable).resolve())
        # TemporaryDirectory is mode 0700. Exports and reports are removed even
        # when findings, parser errors, or subprocess failures stop the scan.
        with tempfile.TemporaryDirectory(prefix="repo-privacy-") as folder:
            temporary = Path(folder)
            source = temporary / "source"
            source.mkdir()
            findings, count = export_files(root, source, args.staged)
            findings.extend(gitleaks_findings(executable, source, temporary))
        for name, line, rule in sorted(set(findings)):
            print(json.dumps({"path": name, "line": line, "rule": rule}, ensure_ascii=True))
        if findings:
            print("Privacy check failed: review the listed locations.", file=sys.stderr)
            return 1
        print(f"Privacy check passed: {count} files ({'index' if args.staged else 'worktree'}).")
        return 0
    except (ScanError, OSError) as error:
        message = str(error) if isinstance(error, ScanError) else "Privacy check could not read required local files."
        print(message, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
