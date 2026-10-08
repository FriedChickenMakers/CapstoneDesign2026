#!/usr/bin/env bash
set -euo pipefail

artifact_root="${CAPSTONE_ARTIFACTS:-/artifacts}"
output_root="${artifact_root}/a7"
package_name="${ANDROID_PACKAGE:-com.capstonedesign2026.mockup}"
mkdir -p "${output_root}"

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

  echo "A7 is offline; artifact collection skipped"
  exit 0
fi

if [[ "$(adb -s "${serial}" get-state 2>/dev/null || true)" != "device" ]]; then
  echo "Selected ADB target is not ready" >&2
  exit 2
fi

timestamp="$(date +%Y%m%d-%H%M%S)"
screenshot_path="${output_root}/screenshot-${timestamp}.png"
log_path="${output_root}/logcat-${timestamp}.txt"
metadata_path="${output_root}/metadata-${timestamp}.json"

adb -s "${serial}" exec-out screencap -p > "${screenshot_path}"
adb -s "${serial}" logcat -d > "${log_path}"

if [[ "${A7_SCREENRECORD:-0}" == "1" ]]; then
  remote_video="/sdcard/capstone-mockup-${timestamp}.mp4"
  video_path="${output_root}/screenrecord-${timestamp}.mp4"
  adb -s "${serial}" shell screenrecord --time-limit "${A7_RECORD_SECONDS:-15}" "${remote_video}"
  adb -s "${serial}" pull -q "${remote_video}" "${video_path}"
  adb -s "${serial}" shell rm "${remote_video}" >/dev/null 2>&1 || true
fi

android_version="$(adb -s "${serial}" shell getprop ro.build.version.release | tr -d '\r' | head -n 1)"
sdk_version="$(adb -s "${serial}" shell getprop ro.build.version.sdk | tr -d '\r' | head -n 1)"
resolution="$(adb -s "${serial}" shell wm size | tr -d '\r' | awk -F': ' '/Physical size/ { print $2; exit }')"
unity_version="6000.3.24f1"
commit="$(git -C "${artifact_root}/../workspace/CapstoneDesign2026" rev-parse HEAD 2>/dev/null || printf 'unknown')"

jq -n \
  --arg commit "${commit}" \
  --arg device "Galaxy Tab A7" \
  --arg serial "redacted" \
  --arg androidVersion "${android_version:-unknown}" \
  --arg androidApi "${sdk_version:-unknown}" \
  --arg resolution "${resolution:-unknown}" \
  --arg unityVersion "${unity_version}" \
  --arg timestamp "${timestamp}" \
  --arg package "${package_name}" \
  '{commit:$commit,device:$device,serial:$serial,androidVersion:$androidVersion,androidApi:$androidApi,resolution:$resolution,unityVersion:$unityVersion,timestamp:$timestamp,package:$package}' \
  > "${metadata_path}"

echo "A7 artifacts written under ${output_root}"
