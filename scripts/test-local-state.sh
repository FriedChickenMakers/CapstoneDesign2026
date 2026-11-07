#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
mono_root="${UNITY_MONO_ROOT:-/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge}"
test_dir="$(mktemp -d)"
trap 'rm -rf "$test_dir"' EXIT
garden_plant_sources=("$project_dir"/Assets/Scripts/ProceduralPlants/Plant{Catalog,Math,Recipe,Generator,Geometry}.cs)
for mode in release development; do
    defines=()
    if [[ "$mode" == development ]]; then defines=(-define:DEVELOPMENT_BUILD); fi
    "$mono_root/bin/mono" "$mono_root/lib/mono/4.5/mcs.exe" -langversion:latest "${defines[@]}" -r:System.Xml.dll -r:System.Core.dll -out:"$test_dir/local-state-tests-$mode.exe" "$project_dir/Assets/Scripts/Runtime/LocalGardenState.cs" "$project_dir/Assets/Scripts/Runtime/MindfulnessContent.cs" "$project_dir/Assets/Scripts/Runtime/MbctContent.cs" "$project_dir/Assets/Scripts/Runtime/FocusMissionCatalog.cs" "$project_dir/Assets/Scripts/Runtime/FocusDrawingData.cs" "$project_dir/Assets/Scripts/Runtime/GardenPlantAppearance.cs" "${garden_plant_sources[@]}" "$project_dir/tests/LocalGardenStateTests.cs"
    "$mono_root/bin/mono" "$test_dir/local-state-tests-$mode.exe"
done
