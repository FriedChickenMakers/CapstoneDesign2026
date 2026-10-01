#!/usr/bin/env bash
set -euo pipefail
serial="${1:?authorized A7 serial required}"
apk="${2:?APK path required}"
output="${3:?artifact output required}"
mkdir -p "$output"
original="$(adb -s "$serial" shell settings get global stay_on_while_plugged_in | tr -d '\r')"
case "$original" in ''|null) echo 'Cannot safely preserve stay-awake setting' >&2; exit 1;; esac
printf '%s\n' "$original" >"$output/stay-awake-original.txt"
restore() {
  adb connect "$serial" >/dev/null 2>&1 || true
  adb -s "$serial" shell settings put global stay_on_while_plugged_in "$original"
}
trap restore EXIT
adb -s "$serial" shell settings put global stay_on_while_plugged_in 0
python3 -u scripts/test-a7-recents-collector.py --serial "$serial" --apk "$apk" --output "$output" --skip-install
