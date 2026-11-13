#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/garden-domain-sources.sh"
if [[ -n "${CAPSTONE_ARTIFACTS:-}" ]]; then
    fixture_dir="$CAPSTONE_ARTIFACTS/branch-fixtures"
else
    fixture_dir="$(mktemp -d -t capstone-branch-fixtures-XXXXXXXX)"
    trap 'rm -rf "$fixture_dir"' EXIT
fi
python3 "$garden_project_dir/scripts/create-combined-branch-fixtures.py" --output "$fixture_dir"
garden_test_args=("$fixture_dir/previous/state.xml" "$fixture_dir/latest/state.xml")
garden_run_test CombinedBranchFixtureTests
