#!/usr/bin/env bash
# Read-only preflight. Never install, grant permissions, clear data, or choose a device automatically.
set -euo pipefail
serial=""
output=""
capture=0
while (($#)); do
  case "$1" in
    --serial) serial="${2:?Missing serial}"; shift 2 ;;
    --output) output="${2:?Missing output directory}"; shift 2 ;;
    --capture-screen) capture=1; shift ;;
    *) echo 'Usage: test-s24-health.sh --serial SERIAL [--output PRIVATE_DIRECTORY] [--capture-screen]' >&2; exit 2 ;;
  esac
done
[[ -n "$serial" ]] || { echo 'Explicit --serial is required.' >&2; exit 2; }
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${output:-${repo_root}/artifacts/s24-health-$(date -u +%Y%m%dT%H%M%SZ)}"
mkdir -p "$output"
chmod 700 "$output"
command -v adb >/dev/null || { echo 'BLOCKED: adb unavailable' >&2; exit 3; }
[[ "$(adb -s "$serial" get-state 2>/dev/null)" == device ]] || { echo 'NOT_TESTED: specified device is not authorized/connected' >&2; exit 3; }
app_id="${CAPSTONE_APP_ID:-com.capstonedesign2026.mockup}"
# Serial is deliberately omitted from saved output. Package information contains no health records.
{
  echo 'S24/WATCH HEALTH PREFLIGHT — automated metadata only; functional checks NOT_TESTED'
  date -u +%FT%TZ
  for prop in ro.product.manufacturer ro.product.model ro.build.version.release ro.build.version.sdk; do
    printf '%s=' "$prop"; adb -s "$serial" shell getprop "$prop" | tr -d '\r'
  done
  printf 'timezone='; adb -s "$serial" shell getprop persist.sys.timezone | tr -d '\r'
  for package in "$app_id" com.sec.android.app.shealth com.google.android.apps.healthdata com.google.android.healthconnect.controller; do
    printf '\nPackage: %s\n' "$package"
    adb -s "$serial" shell dumpsys package "$package" | sed -n -E '/versionCode=|versionName=|android.permission.health.READ_(STEPS|HEART_RATE|SLEEP|EXERCISE).*granted=/p' || true
  done
} > "$output/preflight.txt"
cat > "$output/manual-results.tsv" <<'FORM'
check	status	observed_at	query_start	query_end	timezone	source_filter	notes
Health Connect available	NOT_TESTED						
Read permissions partial/full/denied	NOT_TESTED						
All-source Steps aggregate	NOT_TESTED					ALL	
Samsung-only Steps aggregate	NOT_TESTED					com.sec.android.app.shealth	
Heart sample timestamp/source/gaps	NOT_TESTED						
Sleep session length	NOT_TESTED						
Watch to Samsung Health sync	NOT_TESTED						
Samsung Health to HC sync	NOT_TESTED						
Session range requery after sync	NOT_TESTED						
Permission revoked during query	NOT_TESTED						
Direct Watch connection	NOT_TESTED						Out of scope
FORM
if ((capture)); then
  adb -s "$serial" exec-out screencap -p > "$output/screen-private.png"
fi
printf 'Metadata saved to %s. Complete docs/S24_WATCH_TEST_CHECKLIST.md manually. No functional result inferred.\n' "$output"
