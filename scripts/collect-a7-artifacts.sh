#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${ADB_SERIAL:?Explicit ADB_SERIAL is required}"
exec python3 "$root/scripts/android_device_session.py" capture --serial "$ADB_SERIAL" "$@"
