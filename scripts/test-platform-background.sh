#!/usr/bin/env bash
set -euo pipefail

package_name="${ANDROID_PACKAGE:-com.capstonedesign2026.mockup}"
activity_name="${UNITY_ACTIVITY:-com.unity3d.player.UnityPlayerGameActivity}"
serial="${ADB_SERIAL:-}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifact_root="${CAPSTONE_ARTIFACTS:-${repo_root}/artifacts}/platform"
while (($#)); do
  case "$1" in
    --serial) serial="${2:?Missing serial}"; shift 2 ;;
    --screen-off-seconds) export PLATFORM_SCREEN_OFF_SECONDS="${2:?Missing duration}"; shift 2 ;;
    *) echo 'Usage: test-platform-background.sh --serial SERIAL [--screen-off-seconds 300|1800|SECONDS]' >&2; exit 2 ;;
  esac
done
sleep_chunks() {
  local remaining="$1"
  [[ "$remaining" =~ ^[0-9]+$ ]] || { echo 'Duration must be integer seconds' >&2; return 2; }
  while ((remaining > 0)); do
    local chunk=$((remaining > 30 ? 30 : remaining))
    sleep "$chunk"
    remaining=$((remaining - chunk))
  done
}

if [[ -z "${serial}" || "$(adb -s "${serial}" get-state 2>/dev/null || true)" != "device" ]]; then
  echo "No authorized Android device is available; set ADB_SERIAL after approving RSA debugging" >&2
  exit 2
fi

mkdir -p "${artifact_root}"
chmod 700 "${artifact_root}"
timestamp="$(date +%Y%m%d-%H%M%S)"
report_path="${artifact_root}/background-sensor-${timestamp}.json"
log_path="${artifact_root}/background-sensor-${timestamp}.logcat.txt"

if [[ -n "${APK_PATH:-}" ]]; then
  adb -s "${serial}" install -r "${APK_PATH}"
fi

# Permission changes are opt-in; complete normal app consent before lifecycle testing.
if [[ "${PLATFORM_AUTO_GRANT_PERMISSIONS:-0}" == "1" ]]; then
  adb -s "${serial}" shell pm grant "${package_name}" \
    android.permission.ACTIVITY_RECOGNITION >/dev/null 2>&1 || true
  adb -s "${serial}" shell pm grant "${package_name}" \
    android.permission.POST_NOTIFICATIONS >/dev/null 2>&1 || true
fi

read_last_sample_time() {
  adb -s "${serial}" exec-out run-as "${package_name}" \
    cat files/platform/sensor_samples.jsonl 2>/dev/null \
    | tail -n 1 \
    | jq -r '.receivedAtEpochMs // 0' 2>/dev/null \
    || printf '0\n'
}

adb -s "${serial}" shell am force-stop "${package_name}"
adb -s "${serial}" shell am start \
  -n "${package_name}/${activity_name}" \
  --es platformInputMode LIVE >/dev/null
sleep_chunks "${PLATFORM_LAUNCH_SETTLE_SECONDS:-5}"

# Defaults target the generated 1200x2000 portrait debug panel. Override for
# another resolution or aspect ratio.
adb -s "${serial}" shell input tap \
  "${PLATFORM_START_X:-100}" "${PLATFORM_START_Y:-940}"
sleep_chunks "${PLATFORM_FOREGROUND_SECONDS:-8}"
foreground_time="$(read_last_sample_time)"

adb -s "${serial}" shell input keyevent KEYCODE_HOME
sleep_chunks "${PLATFORM_BACKGROUND_SECONDS:-10}"
background_time="$(read_last_sample_time)"

screen_off_seconds="${PLATFORM_SCREEN_OFF_SECONDS:-10}"
if [[ "${screen_off_seconds}" != "0" ]]; then
  adb -s "${serial}" shell input keyevent KEYCODE_SLEEP
  sleep_chunks "${screen_off_seconds}"
  screen_off_time="$(read_last_sample_time)"
  adb -s "${serial}" shell input keyevent KEYCODE_WAKEUP
else
  screen_off_time="${background_time}"
fi

adb -s "${serial}" shell am start \
  -n "${package_name}/${activity_name}" \
  --es platformInputMode LIVE >/dev/null
sleep_chunks "${PLATFORM_RETURN_SETTLE_SECONDS:-3}"

service_dump="$(adb -s "${serial}" shell dumpsys activity services "${package_name}" | tr -d '\r')"
service_running=false
if grep -q 'SensorForegroundService' <<<"${service_dump}"; then
  service_running=true
fi

# Case 5: stopping the service must unregister listeners and stop timestamp
# advancement. Allow one in-flight event before measuring the stable value.
adb -s "${serial}" shell input tap \
  "${PLATFORM_STOP_X:-205}" "${PLATFORM_STOP_Y:-940}"
sleep_chunks "${PLATFORM_STOP_SETTLE_SECONDS:-2}"
stopped_time="$(read_last_sample_time)"
sleep_chunks "${PLATFORM_STOP_VERIFY_SECONDS:-3}"
stopped_verify_time="$(read_last_sample_time)"
service_stopped=true
if adb -s "${serial}" shell dumpsys activity services "${package_name}" \
  | grep -q 'SensorForegroundService'; then
  service_stopped=false
fi
samples_stable_after_stop=false
if [[ "${stopped_time:-0}" == "${stopped_verify_time:-0}" ]]; then
  samples_stable_after_stop=true
fi

if [[ "${PLATFORM_CAPTURE_LOGS:-0}" == "1" ]]; then
  adb -s "${serial}" logcat -d -s CapstoneSensorService:I CapstoneSensors:I AndroidRuntime:E > "${log_path}"
else
  printf 'Log capture disabled by default; opt in with PLATFORM_CAPTURE_LOGS=1.\n' > "${log_path}"
fi

jq -n \
  --arg serial "explicit-device-redacted" \
  --arg package "${package_name}" \
  --arg timestamp "${timestamp}" \
  --argjson screenOffSeconds "${screen_off_seconds}" \
  --argjson foreground "${foreground_time:-0}" \
  --argjson background "${background_time:-0}" \
  --argjson screenOff "${screen_off_time:-0}" \
  --argjson serviceRunning "${service_running}" \
  --argjson serviceStopped "${service_stopped}" \
  --argjson stopped "${stopped_time:-0}" \
  --argjson stoppedVerify "${stopped_verify_time:-0}" \
  --argjson samplesStableAfterStop "${samples_stable_after_stop}" \
  '{screenOffSeconds:$screenOffSeconds,serial:$serial,package:$package,timestamp:$timestamp,serviceRunning:$serviceRunning,foregroundLastSampleMs:$foreground,backgroundLastSampleMs:$background,screenOffLastSampleMs:$screenOff,backgroundAdvanced:($background>$foreground),screenOffAdvanced:($screenOff>$background),serviceStopped:$serviceStopped,stoppedLastSampleMs:$stopped,stoppedVerifyLastSampleMs:$stoppedVerify,samplesStableAfterStop:$samplesStableAfterStop}' \
  > "${report_path}"

cat "${report_path}"
if [[ "${foreground_time:-0}" == "0" || "${background_time:-0}" -le "${foreground_time:-0}" ]]; then
  echo "Background sensor timestamps did not advance; inspect ${log_path}" >&2
  exit 1
fi
if [[ "${service_running}" != "true" || "${service_stopped}" != "true" ||
      "${samples_stable_after_stop}" != "true" ]]; then
  echo "Foreground service lifecycle verification failed; inspect ${report_path} and ${log_path}" >&2
  exit 1
fi

echo "Foreground/background lifecycle checks passed; inspect screen-off observation separately (not a lossless guarantee). Report: ${report_path}"
