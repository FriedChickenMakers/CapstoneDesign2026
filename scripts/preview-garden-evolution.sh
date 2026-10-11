#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
capture_root="${CAPSTONE_ARTIFACTS:-$project_dir/artifacts/garden-evolution}"
mkdir -p "$capture_root"
capture_root="$(cd "$capture_root" && pwd)"
export DISPLAY="${DISPLAY:-:99}"
CAPSTONE_ARTIFACTS="$capture_root" "$project_dir/scripts/unity-run.sh" \
  -batchmode -force-vulkan -quit -projectPath "$project_dir" \
  -executeMethod CapstoneDesign.EditorTools.GardenEvolutionCapture.Capture \
  -logFile "$capture_root/capture.log"
