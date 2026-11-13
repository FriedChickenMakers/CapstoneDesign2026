#!/usr/bin/env bash
set -euo pipefail
serial="${1:?authorized A7 serial required}"
apk="${2:?APK path required}"
output="${3:?artifact output required}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ "${CAPSTONE_DEVICE_SESSION_ACTIVE:-0}" != "1" ]]; then
  exec python3 "$root/scripts/android_device_session.py" run --serial "$serial" --output "$output/session" -- bash "$0" "$@"
fi
mkdir -p "$output"
original="$(adb -s "$serial" shell settings get global stay_on_while_plugged_in | tr -d '\r')"
case "$original" in ''|null) echo 'Cannot safely preserve stay-awake setting' >&2; exit 1;; esac
printf '%s\n' "$original" >"$output/stay-awake-original.txt"
restore() {
  adb -s "$serial" shell settings put global stay_on_while_plugged_in "$original"
  [[ "$(adb -s "$serial" shell settings get global stay_on_while_plugged_in | tr -d '\r')" == "$original" ]] || { echo "Stay-awake restoration pending; preserve stay-awake-original.txt" >&2; return 1; }
}
trap restore EXIT
adb -s "$serial" shell settings put global stay_on_while_plugged_in 0
python3 -u scripts/test-a7-recents-collector.py --serial "$serial" --apk "$apk" --output "$output" --skip-install
