#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
phases=(fresh restore)
if (( $# > 0 )); then
 if [[ $# -ne 1 || "$1" != --chrome ]]; then
  echo "Usage: $0 [--chrome]" >&2
  exit 2
 fi
 phases=(chrome)
fi
: "${CAPSTONE_SMOKE_SAVE_ROOT:?Set an isolated synthetic save directory; default fresh/restore requires a new directory}"
export DISPLAY="${DISPLAY:-:99}"
for phase in "${phases[@]}"; do
 export CAPSTONE_SMOKE_PHASE="$phase"
 # A real Game view needs presentation; Xvfb supports GLX but not Vulkan DRI3.
 args=(-force-glcore -projectPath "$project_dir" -executeMethod CapstoneDesign.EditorTools.LocalLoopPlaySmoke.Start -logFile -)
 if [[ "$phase" != chrome ]]; then args=(-batchmode "${args[@]}"); fi
 result="$CAPSTONE_SMOKE_SAVE_ROOT/play-$phase.json"
 rm -f -- "$result"
 # Keep timeout in the QA process group so cancellation reaches its signal
 # handler, which also supervises an Editor that creates a separate session.
 if timeout --foreground --signal=TERM --kill-after=15s 150s "$project_dir/scripts/unity-run.sh" "${args[@]}"; then
  :
 else
  unity_status=$?
  python3 - "$result" "$phase" "$unity_status" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
if not path.exists():
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"status": "FAIL", "phase": sys.argv[2],
                                "error": f"Unity exited before reporting; exit status {sys.argv[3]}"}, indent=2))
PY
  exit "$unity_status"
 fi
 python3 - "$result" "$phase" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
report = json.loads(path.read_text())
if report.get("status") != "PASS" or report.get("phase") != sys.argv[2]:
    raise SystemExit(f"Play smoke result is not a verified PASS: {path}")
PY
done
