#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${CAPSTONE_ARTIFACTS:-${root}/artifacts/measurement-buffer-tests}"
python3 "$root/scripts/run-kotlin-test.py" --output "$output/tests.jar" \
 --main com.capstonedesign2026.platform.MeasurementBufferTestKt \
 "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/MeasurementBuffer.kt" \
 "$root/tests/native/MeasurementBufferTest.kt"
