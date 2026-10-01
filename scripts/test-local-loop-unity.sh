#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec "$project_dir/scripts/unity-run.sh" -batchmode -nographics -quit -projectPath "$project_dir" -executeMethod CapstoneDesign.EditorTools.LocalLoopValidation.Run -logFile -
