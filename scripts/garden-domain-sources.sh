#!/usr/bin/env bash
# Source from pure-C# test wrappers; the same manifest is used by PowerShell and Python.
# Only Mono is invoked, never the Unity Editor or an Android device.
garden_project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
garden_domain_sources=()
while IFS= read -r garden_relative || [[ -n "$garden_relative" ]]; do
    [[ -n "$garden_relative" ]] || continue
    if [[ ! -f "$garden_project_dir/$garden_relative" ]]; then
        printf 'Missing domain source: %s\n' "$garden_relative" >&2
        return 1
    fi
    garden_domain_sources+=("$garden_project_dir/$garden_relative")
done < "$garden_project_dir/scripts/garden-domain-sources.txt"
unset garden_relative
garden_extra_sources=()
garden_test_modes=(release)
garden_test_args=()

garden_run_test() (
    set -euo pipefail
    test_name="$1"
    [[ "$test_name" =~ ^[A-Za-z][A-Za-z0-9]*$ ]] || { printf 'Invalid test class name\n' >&2; exit 2; }
    mono_root="${UNITY_MONO_ROOT:-/opt/unity/editors/6000.3.24f1/Editor/Data/MonoBleedingEdge}"
    test_dir="$(mktemp -d -t capstone-domain-test-XXXXXXXX)"
    trap 'rm -rf "$test_dir"' EXIT
    sources=("${garden_domain_sources[@]}")
    for relative in "${garden_extra_sources[@]}"; do sources+=("$garden_project_dir/$relative"); done
    for mode in "${garden_test_modes[@]}"; do
        defines=()
        case "$mode" in
            release) ;;
            development) defines=(-define:DEVELOPMENT_BUILD) ;;
            *) printf 'Unknown test mode: %s\n' "$mode" >&2; exit 2 ;;
        esac
        binary="$test_dir/$test_name-$mode.exe"
        printf 'Pure C# %s (%s)\n' "$test_name" "$mode"
        "$mono_root/bin/mono" "$mono_root/lib/mono/4.5/mcs.exe" -langversion:latest \
            -r:System.Xml.dll -r:System.Core.dll "${defines[@]}" -out:"$binary" \
            "${sources[@]}" "$garden_project_dir/tests/$test_name.cs"
        "$mono_root/bin/mono" "$binary" "${garden_test_args[@]}"
    done
)
