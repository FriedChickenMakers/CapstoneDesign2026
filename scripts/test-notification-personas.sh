#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${NOTIFICATION_PERSONA_OUTPUT:-${root}/artifacts/notification-personas}"
mkdir -p "$output"
run_args=()
for arg in "$@"; do run_args+=("--run-arg=$arg"); done
export NOTIFICATION_PERSONA_OUTPUT="$output"
python3 "$root/scripts/run-kotlin-test.py" --output "$output/tests.jar" \
  --main com.capstonedesign2026.platform.NotificationPersonaEvaluationKt "${run_args[@]}" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/ActivityReminderPolicy.kt" \
  "$root/tests/native/NotificationPersonaEvaluation.kt" | tee "$output/results.txt"
