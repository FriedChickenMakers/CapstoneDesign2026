#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="${NOTIFICATION_TEST_OUTPUT:-${root}/artifacts/notification-policy-tests}"
mkdir -p "$output"
run_args=()
for arg in "$@"; do run_args+=("--run-arg=$arg"); done
python3 "$root/scripts/run-kotlin-test.py" --output "$output/tests.jar" \
  --main com.capstonedesign2026.platform.NotificationPolicyTestKt "${run_args[@]}" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/ActivityReminderPolicy.kt" \
  "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/WalkingAchievementPolicy.kt" \
  "$root/tests/native/NotificationPolicyTest.kt" | tee "$output/results.txt"
