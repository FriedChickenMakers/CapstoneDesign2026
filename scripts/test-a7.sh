#!/usr/bin/env bash
# Scope: install hash, activity launch, process presence, private screenshots.
# UI correctness is a separate assertion/vision gate; screenshots alone are not E2E.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${ADB_SERIAL:?Explicit ADB_SERIAL is required; an absent device is not a pass}"
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  "$root/scripts/build-android.sh"
fi
args=(smoke --serial "$ADB_SERIAL")
if [[ "${SKIP_INSTALL:-0}" != "1" ]]; then
  apk="${APK_PATH:-${CAPSTONE_ARTIFACTS:-$root/artifacts}/build/capstone-mockup.apk}"
  args+=(--apk "$apk")
fi
exec python3 "$root/scripts/android_device_session.py" "${args[@]}" "$@"
