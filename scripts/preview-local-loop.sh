#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DISPLAY="${DISPLAY:-:99}"
exec "$project_dir/scripts/unity-run.sh" -batchmode -force-vulkan -quit -projectPath "$project_dir" -executeMethod CapstoneDesign.EditorTools.LocalLoopPreview.Capture -logFile -
