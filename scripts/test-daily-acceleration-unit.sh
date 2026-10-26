#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${CAPSTONE_ARTIFACTS:-${root}/artifacts/daily-acceleration-tests}"
mkdir -p "$output"
run_args=()
for arg in "$@"; do run_args+=("--run-arg=$arg"); done
python3 "$root/scripts/run-kotlin-test.py" --output "$output/tests.jar" \
  --main com.capstonedesign2026.platform.DailyAccelerationBucketsTestKt "${run_args[@]}" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/DailyAccelerationBuckets.kt" \
  "$root/tests/native/DailyAccelerationBucketsTest.kt" | tee "$output/results.txt"
