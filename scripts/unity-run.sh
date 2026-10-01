#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$project_dir/Library"
# All project editor entry points share one lock; never share Library concurrently.
exec flock "$project_dir/Library/capstone-editor.lock" "${UNITY_BIN:-unity-editor}" "$@"
