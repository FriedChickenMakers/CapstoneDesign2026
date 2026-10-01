#!/usr/bin/env bash
set -euo pipefail

script_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  "${script_root}/build-android.sh"
fi

if [[ -z "${ADB_SERIAL:-}" ]]; then
  echo "ADB_SERIAL is unset; an already authenticated A7 device will be selected"
fi

"${script_root}/deploy-a7.sh"

serial="${ADB_SERIAL:-}"
if [[ -z "${serial}" ]]; then
  serial="$(adb devices | awk '$2 == "device" && $1 ~ /:/ { print $1; exit }')"
fi

if [[ -z "${serial}" ]]; then
  echo "A7 is offline; hardware smoke test skipped"
  exit 0
fi

adb -s "${serial}" logcat -c
adb -s "${serial}" shell am force-stop "${ANDROID_PACKAGE:-com.capstonedesign2026.mockup}"
if [[ -n "${A7_TEST_MODE:-}" || -n "${PLATFORM_INPUT_MODE:-}" ]]; then
  launch_args=(-n "${ANDROID_PACKAGE:-com.capstonedesign2026.mockup}/${UNITY_ACTIVITY:-com.unity3d.player.UnityPlayerGameActivity}")
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
  adb -s "${serial}" shell monkey -p "${ANDROID_PACKAGE:-com.capstonedesign2026.mockup}" 1 >/dev/null
fi
sleep "${A7_SETTLE_SECONDS:-4}"

# The dedicated A7 target is 1200x2000 in portrait. Exercise the three main
# navigation states with configurable coordinates and keep state-specific
# screenshots alongside the normal artifact bundle.
if [[ "${A7_NAV_SMOKE:-1}" == "1" ]]; then
  artifact_root="${CAPSTONE_ARTIFACTS:-/artifacts}"
  output_root="${artifact_root}/a7"
  nav_timestamp="$(date +%Y%m%d-%H%M%S)"
  nav_y="${A7_NAV_Y:-1900}"
  mkdir -p "${output_root}"

  adb -s "${serial}" exec-out screencap -p \
    > "${output_root}/nav-island-${nav_timestamp}.png"
  adb -s "${serial}" shell input tap "${A7_ACTIVITY_X:-190}" "${nav_y}"
  sleep "${A7_NAV_SETTLE_SECONDS:-2}"
  adb -s "${serial}" exec-out screencap -p \
    > "${output_root}/nav-activities-${nav_timestamp}.png"
  adb -s "${serial}" shell input tap "${A7_SETTINGS_X:-1010}" "${nav_y}"
  sleep "${A7_NAV_SETTLE_SECONDS:-2}"
  adb -s "${serial}" exec-out screencap -p \
    > "${output_root}/nav-settings-${nav_timestamp}.png"
  adb -s "${serial}" shell input tap "${A7_ISLAND_X:-600}" "${nav_y}"
  sleep 1
fi

ADB_SERIAL="${serial}" "${script_root}/collect-a7-artifacts.sh"
