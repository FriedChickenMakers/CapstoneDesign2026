#!/usr/bin/env bash
# Current daily-aggregate collector. Old raw-file lifecycle checks are retired.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -n "${APK_PATH:-}" ]]; then
  echo 'Install with deploy-a7.sh first, then enable the collector normally; this observation never installs or enables it.' >&2
fi
exec python3 "$root/scripts/test-daily-collector-lifecycle.py" "$@"
