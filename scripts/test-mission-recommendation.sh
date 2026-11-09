#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/garden-domain-sources.sh"
garden_extra_sources=(Assets/Scripts/Runtime/PersonalizationCore.cs Assets/Scripts/Runtime/OptionalActivityRecommendation.cs)
garden_run_test MissionRecommendationTests
