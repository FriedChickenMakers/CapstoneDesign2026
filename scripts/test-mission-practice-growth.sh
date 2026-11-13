#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/garden-domain-sources.sh"
garden_test_modes=(release development)
garden_run_test MissionPracticeGrowthTests
