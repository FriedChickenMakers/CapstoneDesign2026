#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/garden-domain-sources.sh"
if [[ -n "${CAPSTONE_ARTIFACTS:-}" ]]; then
    fixture_dir="$CAPSTONE_ARTIFACTS/guided-v4-fixtures"
else
    fixture_dir="$(mktemp -d -t capstone-guided-v4-XXXXXXXX)"
    trap 'rm -rf "$fixture_dir"' EXIT
fi
python3 "$garden_project_dir/scripts/create-guided-v4-fixtures.py" --output "$fixture_dir"
garden_test_args=("$fixture_dir")
garden_run_test GuidedV4CompatibilityTests
