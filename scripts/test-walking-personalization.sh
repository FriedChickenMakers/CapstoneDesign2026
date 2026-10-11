#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
mono_root="${UNITY_MONO_ROOT:-/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge}"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/mcs.exe" -langversion:latest -r:System.Xml.dll -r:System.Core.dll -out:"$test_dir/walking-tests.exe" \
  "$project_dir/Assets/Scripts/Runtime/LocalGardenState.cs" "$project_dir/Assets/Scripts/Runtime/AndroidPlatformModels.cs" \
  "$project_dir/Assets/Scripts/Runtime/WalkingPersonalization.cs" "$project_dir/tests/WalkingPersonalizationTests.cs"
"$mono_root/bin/mono" "$test_dir/walking-tests.exe"
