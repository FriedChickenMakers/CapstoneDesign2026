#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
mono_root="${UNITY_MONO_ROOT:-/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge}"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/mcs.exe" -langversion:latest -r:System.Runtime.Serialization.dll \
  -out:"$test_dir/automatic-enrollment-tests.exe" "$project_dir/Assets/Scripts/Runtime/AutomaticEnrollment.cs" "$project_dir/tests/AutomaticEnrollmentTests.cs"
"$mono_root/bin/mono" "$test_dir/automatic-enrollment-tests.exe"
