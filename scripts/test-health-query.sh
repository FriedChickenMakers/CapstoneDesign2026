#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Reuses the installed Unity Android Gradle Kotlin compiler; no Unity Editor process.
gradle_lib="${KOTLIN_GRADLE_LIB:-/opt/unity/editors/6000.3.24f1/Editor/Data/PlaybackEngines/AndroidPlayer/Tools/gradle/lib}"
java_bin="${JAVA_BIN:-/opt/android-jdk/bin/java}"
output="${HEALTH_TEST_OUTPUT:-${repo_root}/artifacts/health-query-tests}"
mkdir -p "$output"
compiler_cp="$(find "$gradle_lib" -maxdepth 1 -name '*.jar' -printf '%p:' )"
stdlib="$(find "$gradle_lib" -maxdepth 1 -name 'kotlin-stdlib-*.jar' | head -1)"
coroutines="$(find "$gradle_lib" -maxdepth 1 -name 'kotlinx-coroutines-core-jvm-*.jar' | head -1)"
"$java_bin" -cp "$compiler_cp" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -jvm-target 17 -classpath "$stdlib:$coroutines" -d "$output/tests.jar" \
 "$repo_root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/HealthQueryLogic.kt" "$repo_root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/HealthSourcePolicy.kt" "$repo_root/Assets/Plugins/Android/CapstonePlatform.androidlib/src/main/kotlin/com/capstonedesign2026/platform/BoundedSensorLog.kt" "$repo_root/tests/native/HealthQueryLogicTest.kt"
"$java_bin" -cp "$output/tests.jar:$stdlib:$coroutines" com.capstonedesign2026.platform.HealthQueryLogicTestKt | tee "$output/results.txt"
python3 - "$output/results.txt" "$output/results.json" <<'PYJSON'
import json, pathlib, sys
lines = pathlib.Path(sys.argv[1]).read_text().splitlines()
cases = [dict(name=line[5:], status="PASS") for line in lines if line.startswith("PASS ")]
pathlib.Path(sys.argv[2]).write_text(json.dumps(dict(environment="synthetic JVM", passed=len(cases), failed=0, cases=cases), indent=2) + "\n")
PYJSON
