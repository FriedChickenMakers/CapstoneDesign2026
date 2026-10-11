#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
gradle_lib="${KOTLIN_GRADLE_LIB:-/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib}"
java_bin="${JAVA_BIN:-/opt/android-jdk/bin/java}"
output="${NOTIFICATION_TEST_OUTPUT:-${repo_root}/artifacts/notification-policy-tests}"
mkdir -p "$output"
compiler_cp="$(find "$gradle_lib" -maxdepth 1 -name '*.jar' -printf '%p:' )"
stdlib="$(find "$gradle_lib" -maxdepth 1 -name 'kotlin-stdlib-*.jar' | head -1)"
"$java_bin" -cp "$compiler_cp" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -jvm-target 17 -classpath "$stdlib" -d "$output/tests.jar" \
 "$repo_root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/ActivityReminderPolicy.kt" \
 "$repo_root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/WalkingAchievementPolicy.kt" \
 "$repo_root/tests/native/NotificationPolicyTest.kt"
"$java_bin" -cp "$output/tests.jar:$stdlib" com.capstonedesign2026.platform.NotificationPolicyTestKt | tee "$output/results.txt"
