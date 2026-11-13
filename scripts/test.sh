#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_bin="${UNITY_BIN:-unity-editor}"

exec "${repo_root}/scripts/unity-run.sh" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "${repo_root}" \
  -executeMethod CapstoneDesign.EditorTools.BuildTools.ValidateProject \
  -logFile -
