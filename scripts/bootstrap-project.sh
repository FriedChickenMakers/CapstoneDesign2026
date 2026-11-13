#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_bin="${UNITY_BIN:-unity-editor}"

echo "Generating Unity mockup in ${repo_root}"
exec "${repo_root}/scripts/unity-run.sh" \
  -batchmode \
  -quit \
  -projectPath "${repo_root}" \
  -executeMethod CapstoneDesign.EditorTools.MockupProjectBuilder.BuildMockup \
  -logFile -
