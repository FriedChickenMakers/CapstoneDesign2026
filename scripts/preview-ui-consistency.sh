#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
capture_root="${CAPSTONE_ARTIFACTS:-$project_dir/artifacts/ui-consistency}"
mkdir -p "$capture_root"
capture_root="$(cd "$capture_root" && pwd)"
export DISPLAY="${DISPLAY:-:99}"

for suite in local-loop mbct expanded; do
  case "$suite" in
    local-loop) method=LocalLoopPreview ;;
    mbct) method=MbctPreview ;;
    expanded) method=UiConsistencyBaselineCapture ;;
  esac
  CAPSTONE_ARTIFACTS="$capture_root/$suite" \
    "$project_dir/scripts/unity-run.sh" -batchmode -force-vulkan -quit \
    -projectPath "$project_dir" \
    -executeMethod "CapstoneDesign.EditorTools.$method.Capture" \
    -logFile "$capture_root/$suite.log"
done
