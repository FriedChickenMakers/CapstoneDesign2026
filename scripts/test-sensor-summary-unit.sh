#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${CAPSTONE_ARTIFACTS:-${root}/artifacts/sensor-summary-tests}"
mkdir -p "$output"
run_args=()
for arg in "$@"; do run_args+=("--run-arg=$arg"); done
python3 "$root/scripts/run-kotlin-test.py" --output "$output/tests.jar" \
  --main com.capstonedesign2026.platform.SensorWindowAccumulatorTestKt "${run_args[@]}" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/SensorWindowAccumulator.kt" \
  "$root/tests/native/SensorWindowAccumulatorTest.kt" | tee "$output/results.txt"
