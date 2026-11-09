#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/garden-domain-sources.sh"
# Current purpose/allowance contracts and immutable historical version regressions.
garden_run_test FocusMissionContractTests
