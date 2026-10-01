#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="${CAPSTONE_ARTIFACTS:-/artifacts}"
apk_path="${APK_PATH:-${artifact_root}/build/capstone-mockup.apk}"
package_name="${ANDROID_PACKAGE:-com.capstonedesign2026.mockup}"
activity_name="${UNITY_ACTIVITY:-com.unity3d.player.UnityPlayerGameActivity}"

choose_serial() {
  if [[ -n "${ADB_SERIAL:-}" ]]; then
    printf '%s\n' "${ADB_SERIAL}"
    return 0
  fi

  adb devices | awk '$2 == "device" && $1 ~ /:/ { print $1; exit }'
}

serial="$(choose_serial)"
if [[ -z "${serial}" ]]; then
  if [[ "${A7_REQUIRED:-0}" == "1" ]]; then
    echo "A7 is offline and A7_REQUIRED=1" >&2
    exit 2
  fi

  echo "A7 is offline; hardware deployment skipped"
  exit 0
fi

if [[ ! -f "${apk_path}" ]]; then
  echo "APK not found: ${apk_path}" >&2
  echo "Run scripts/build-android.sh first" >&2
  exit 1
fi

state="$(adb -s "${serial}" get-state 2>/dev/null || true)"
if [[ "${state}" != "device" ]]; then
  echo "ADB target ${serial} is not ready: ${state:-offline}" >&2
  exit 2
fi

echo "Installing ${apk_path} on ${serial}"
adb -s "${serial}" install -r "${apk_path}"
adb -s "${serial}" shell am force-stop "${package_name}"
if [[ -n "${A7_TEST_MODE:-}" || -n "${PLATFORM_INPUT_MODE:-}" ]]; then
  launch_args=(-n "${package_name}/${activity_name}")
  if [[ -n "${A7_TEST_MODE:-}" ]]; then
    launch_args+=(--es testScene "${A7_TEST_MODE}")
  fi
  if [[ -n "${PLATFORM_INPUT_MODE:-}" ]]; then
    launch_args+=(--es platformInputMode "${PLATFORM_INPUT_MODE}")
  fi
  if [[ -n "${PLATFORM_REPLAY_PATH:-}" ]]; then
    launch_args+=(--es platformReplayPath "${PLATFORM_REPLAY_PATH}")
  fi
  adb -s "${serial}" shell am start "${launch_args[@]}"
else
  adb -s "${serial}" shell monkey -p "${package_name}" 1 >/dev/null
fi
echo "A7 mockup launched (${package_name})"
