#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
lib=/opt/unity/current/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib
out="${CAPSTONE_ARTIFACTS:-$root/artifacts/a7-summary-20260923}"
mkdir -p "$out"
cp="$(find "$lib" -maxdepth 1 -name '*.jar' -printf '%p:')"
stdlib="$(find "$lib" -maxdepth 1 -name 'kotlin-stdlib-*.jar' | head -1)"
/opt/android-jdk/bin/java -cp "$cp" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -jvm-target 17 -classpath "$stdlib" -d "$out/summary-tests.jar" "$root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/SensorWindowAccumulator.kt" "$root/tests/native/SensorWindowAccumulatorTest.kt"
/opt/android-jdk/bin/java -cp "$out/summary-tests.jar:$stdlib" com.capstonedesign2026.platform.SensorWindowAccumulatorTestKt
