#!/usr/bin/env bash
# In-place update with pre-launch backups and durable display-setting recovery.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${ADB_SERIAL:?Explicit ADB_SERIAL is required; automatic device selection is disabled}"
apk="${APK_PATH:-${CAPSTONE_ARTIFACTS:-$root/artifacts}/build/capstone-mockup.apk}"
exec python3 "$root/scripts/android_device_session.py" deploy --serial "$ADB_SERIAL" --apk "$apk" "$@"
