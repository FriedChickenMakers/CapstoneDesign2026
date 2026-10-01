#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${CAPSTONE_SMOKE_SAVE_ROOT:?Set a new isolated synthetic save directory}"
export DISPLAY="${DISPLAY:-:99}"
for phase in fresh restore; do
 export CAPSTONE_SMOKE_PHASE="$phase"
 "$project_dir/scripts/unity-run.sh" -batchmode -force-vulkan -projectPath "$project_dir" -executeMethod CapstoneDesign.EditorTools.LocalLoopPlaySmoke.Start -logFile -
done
