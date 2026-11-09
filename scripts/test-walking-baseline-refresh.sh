#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/garden-domain-sources.sh"
garden_extra_sources=(Assets/Scripts/Runtime/AndroidPlatformModels.cs Assets/Scripts/Runtime/WalkingPersonalization.cs Assets/Scripts/Runtime/WalkingBaselineRefresh.cs)
garden_run_test WalkingBaselineRefreshTests
