#!/usr/bin/env python3
"""Validate Unity GUID metadata before compiling or copying the project.

Run ``python scripts/check-unity-meta.py`` for this repository, or pass
``--root PATH`` to check a frozen project copy. ``--self-test`` checks isolated
temporary fixtures without touching project Assets. Exit 0 means success,
1 means metadata findings, and 2 means the scan could not finish.

Checks existing Assets/**/*.meta files only; assets without metadata are not
reported. Each metadata file must have one top-level GUID of exactly 32 hex
characters, unique across Assets. UTF-8 metadata may include a BOM.
"""
import argparse
from pathlib import Path
import re
import sys
import tempfile


GUID_LINE = re.compile(r"^guid:[ \t]*(.*)$", re.MULTILINE)
VALID_GUID = re.compile(r"[0-9a-fA-F]{32}")


def check_metadata(root):
    assets = root / "Assets"
    if not assets.is_dir():
        raise OSError("Project root must contain an Assets directory.")
    paths = sorted(assets.rglob("*.meta"))
    findings = []
    seen = {}
    for path in paths:
        name = path.relative_to(root).as_posix()
        try:
            content = path.read_text(encoding="utf-8-sig")
        except UnicodeError:
            findings.append(f"{name}: metadata is not valid UTF-8.")
            continue
        except OSError as error:
            raise OSError(f"{name}: metadata could not be read.") from error
        matches = list(GUID_LINE.finditer(content))
        if not matches:
            findings.append(f"{name}: missing top-level guid field.")
            continue
        if len(matches) != 1:
            findings.append(f"{name}: expected one top-level guid field; found {len(matches)}.")
            continue
        match = matches[0]
        line = content.count("\n", 0, match.start()) + 1
        location = f"{name}:{line}"
        guid = match.group(1).strip()
        if not VALID_GUID.fullmatch(guid):
            findings.append(f"{location}: GUID must contain exactly 32 hexadecimal characters.")
            continue
        key = guid.lower()
        if key in seen:
            findings.append(f"{location}: duplicate GUID; first declared at {seen[key]}.")
        else:
            seen[key] = location
    return len(paths), findings


def self_test():
    guid = "0123456789abcdef0123456789abcdef"
    valid = f"fileFormatVersion: 2\nguid: {guid}\n".encode()
    cases = [
        ("valid", {"Source.cs.meta": valid, "Source.cs": b"class Source {}"}, None),
        ("bom", {"Source.cs.meta": b"\xef\xbb\xbf" + valid}, None),
        ("no-missing-meta-policy", {"Source.cs": b"class Source {}"}, None),
        ("33-characters", {"Source.cs.meta": valid.replace(guid.encode(), (guid + "2").encode())}, "exactly 32"),
        ("non-hex", {"Source.cs.meta": valid.replace(guid.encode(), b"z" * 32)}, "hexadecimal"),
        ("missing", {"Source.cs.meta": b"fileFormatVersion: 2\n"}, "missing top-level guid"),
        ("empty", {"Source.cs.meta": b"guid:\n"}, "exactly 32"),
        ("duplicate", {"First.cs.meta": valid, "Nested/Second.cs.meta": valid.replace(guid.encode(), guid.upper().encode())}, "duplicate GUID"),
        ("repeated-field", {"Source.cs.meta": valid + f"guid: {guid}\n".encode()}, "found 2"),
        ("invalid-encoding", {"Source.cs.meta": b"\xff"}, "not valid UTF-8"),
    ]
    with tempfile.TemporaryDirectory(prefix="unity-meta-self-test-") as temporary:
        for case, files, expected in cases:
            root = Path(temporary) / case
            (root / "Assets").mkdir(parents=True)
            for name, content in files.items():
                path = root / "Assets" / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
            count, findings = check_metadata(root)
            assert count == sum(name.endswith(".meta") for name in files), case
            if expected is None:
                assert not findings, (case, findings)
            else:
                assert len(findings) == 1 and expected in findings[0], (case, findings)
                assert findings[0].startswith("Assets/"), (case, findings)
    print(f"Unity metadata self-test passed: {len(cases)} isolated fixtures.")
    return 0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1],
                        help="Unity project root (default: this repository)")
    parser.add_argument("--self-test", action="store_true", help="run isolated temporary fixtures")
    args = parser.parse_args(argv)
    if args.self_test:
        return self_test()
    try:
        count, findings = check_metadata(args.root.resolve())
    except OSError as error:
        print(f"Unity metadata scan failed: {error}", file=sys.stderr)
        return 2
    if findings:
        for finding in findings:
            print(finding, file=sys.stderr)
        print(f"Unity metadata check failed: {len(findings)} finding(s) in {count} files.", file=sys.stderr)
        return 1
    print(f"Unity metadata check passed: {count} files, valid and unique GUIDs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
