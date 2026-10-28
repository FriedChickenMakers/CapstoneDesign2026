#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
mono_root="${UNITY_MONO_ROOT:-/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge}"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
garden_plant_sources=("$project_dir"/Assets/Scripts/ProceduralPlants/Plant{Catalog,Math,Recipe,Generator,Geometry}.cs)
"$mono_root/bin/mono" "$mono_root/lib/mono/4.5/mcs.exe" -langversion:latest -r:System.Xml.dll -r:System.Core.dll -out:"$test_dir/baseline-refresh-tests.exe" \
 "$project_dir/Assets/Scripts/Runtime/LocalGardenState.cs" "$project_dir/Assets/Scripts/Runtime/FocusMissionCatalog.cs" "$project_dir/Assets/Scripts/Runtime/FocusDrawingData.cs" "$project_dir/Assets/Scripts/Runtime/GardenPlantAppearance.cs" "${garden_plant_sources[@]}" "$project_dir/Assets/Scripts/Runtime/AndroidPlatformModels.cs" \
 "$project_dir/Assets/Scripts/Runtime/WalkingPersonalization.cs" "$project_dir/Assets/Scripts/Runtime/WalkingBaselineRefresh.cs" \
 "$project_dir/tests/WalkingBaselineRefreshTests.cs"
"$mono_root/bin/mono" "$test_dir/baseline-refresh-tests.exe"
