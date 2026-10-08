#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
mono_root="${UNITY_MONO_ROOT:-/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge}"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/mcs.exe" -langversion:latest -r:System.Xml.dll -r:System.Core.dll -out:"$test_dir/mbct-tests.exe" \
  "$project_dir/Assets/Scripts/Runtime/LocalGardenState.cs" "$project_dir/Assets/Scripts/Runtime/MindfulnessContent.cs" \
  "$project_dir/Assets/Scripts/Runtime/MbctContent.cs" "$project_dir/tests/MbctTests.cs"
"$mono_root/bin/mono" "$test_dir/mbct-tests.exe"
