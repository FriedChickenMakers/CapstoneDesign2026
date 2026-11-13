#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_bin="${UNITY_BIN:-unity-editor}"

if [[ -z "${DISPLAY:-}" ]]; then
  export DISPLAY=:99
fi

# Visual rendering must initialize a graphics device. Xvfb supplies the
# window context while MESA_VK_DEVICE_SELECT selects the worker iGPU.
exec "${repo_root}/scripts/unity-run.sh" \
  -batchmode \
  -force-vulkan \
  -quit \
  -projectPath "${repo_root}" \
  -executeMethod CapstoneDesign.EditorTools.VisualTestTools.CaptureMockupPreview \
  -logFile -
