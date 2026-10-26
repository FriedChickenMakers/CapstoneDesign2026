#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "$project_dir/Library"
# All project editor entry points share one lock; never share Library concurrently.
# Replace flock with the Editor so timeout/signal supervisors retain its PID,
# even if the Editor creates its own session. A forked flock parent can exit
# while a detached Editor keeps running and holds the inherited lock.
exec flock --no-fork "$project_dir/Library/capstone-editor.lock" "${UNITY_BIN:-unity-editor}" "$@"
