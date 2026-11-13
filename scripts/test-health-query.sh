#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${HEALTH_TEST_OUTPUT:-${root}/artifacts/health-query-tests}"
mkdir -p "$output"
run_args=()
for arg in "$@"; do run_args+=("--run-arg=$arg"); done
python3 "$root/scripts/run-kotlin-test.py" --output "$output/tests.jar" \
  --main com.capstonedesign2026.platform.HealthQueryLogicTestKt "${run_args[@]}" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/HealthQueryLogic.kt" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/HealthPersonalizationLogic.kt" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/HealthSourcePolicy.kt" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/BoundedSensorLog.kt" \
  "$root/tests/native/HealthQueryLogicTest.kt" | tee "$output/results.txt"
python3 - "$output/results.txt" "$output/results.json" <<'PYJSON'
import json, pathlib, sys
lines = pathlib.Path(sys.argv[1]).read_text().splitlines()
cases = [dict(name=line[5:], status="PASS") for line in lines if line.startswith("PASS ")]
pathlib.Path(sys.argv[2]).write_text(json.dumps(dict(environment="synthetic JVM", passed=len(cases), failed=0, cases=cases), indent=2) + "\n")
PYJSON
