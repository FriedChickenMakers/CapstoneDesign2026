# Mockup scripts

Before committing, pushing, or creating a source archive, follow
[repository privacy checks](../docs/REPOSITORY_PRIVACY.md). With Gitleaks installed
or `GITLEAKS_BIN` set, run `python3 scripts/check-repo-privacy.py`; after staging,
also run `python3 scripts/check-repo-privacy.py --staged`.

All scripts resolve the repository root from their own path and use the
worker's `unity-editor` wrapper by default. Set `UNITY_BIN` when testing on a
different machine.

```bash
./scripts/bootstrap-project.sh
./scripts/test.sh
./scripts/visual-preview.sh
./scripts/build-android.sh
```

The A7 scripts select the authenticated wireless device from `adb devices` or
use `ADB_SERIAL` explicitly. A missing device is a clean skip unless
`A7_REQUIRED=1` is set. Set `ADB_SERIAL` in your local shell to the current
authenticated endpoint shown on the device; never commit that value. Screen
recording is opt-in:

```bash
ADB_SERIAL="${ADB_SERIAL:?Set the target endpoint locally}" A7_SCREENRECORD=1 ./scripts/test-a7.sh
```

Provider mode can be selected with `PLATFORM_INPUT_MODE=LIVE|MOCK|REPLAY`.
Replay also needs `PLATFORM_REPLAY_PATH` pointing to an app-readable snapshot
JSONL file.

After authorizing ADB, verify foreground, Home/background, and screen-off
sensor persistence with:

```bash
ADB_SERIAL="${ADB_SERIAL:?Set the target endpoint locally}" \
CAPSTONE_ARTIFACTS=/tmp/capstone-platform-build \
./scripts/test-platform-background.sh
```

Set `APK_PATH` to install before the test. Start-button coordinates and wait
times are configurable through the `PLATFORM_*` variables in the script. The
test grants its runtime permissions through ADB by default; set
`PLATFORM_AUTO_GRANT_PERMISSIONS=0` when validating the manual permission UI.
It also taps Stop and verifies that the service disappears and sample
timestamps remain stable.

## Local loop verification (synthetic data)

```bash
export CAPSTONE_ARTIFACTS="$PWD/artifacts/manual-local-loop"
./scripts/test-local-state.sh
./scripts/test-health-query.sh
./scripts/test-heart-range-refresh.sh
./scripts/test-local-loop-unity.sh
./scripts/preview-local-loop.sh
./scripts/build-android.sh
```

`test-heart-range-refresh.sh` runs isolated synthetic C# checks for activity-heart query state, stale response handling and busy timeouts. It does not start Unity or connect to a device.

Unity entry scripts share `Library/capstone-editor.lock`; run editors through
`scripts/unity-run.sh` to serialize this project's Library/Temp usage. Preview
renders phone 900x1600 and landscape tablet 1200x800 using injected time and
MOCK state. It never initializes the normal game save or changes system time.

S24 preparation is read-only and requires explicit selection:

```bash
./scripts/test-s24-health.sh --serial "$ADB_SERIAL"
./scripts/test-platform-background.sh --serial "$ADB_SERIAL" --screen-off-seconds 300
```

The second command is an interactive-device lifecycle test; inspect its flags
and the checklist first. Device tests are not included in the synthetic suite.

For actual Editor Play-mode initialization and process-restart persistence,
use a new synthetic directory (the script runs fresh and restore in separate
Editor processes):

```bash
CAPSTONE_SMOKE_SAVE_ROOT="$CAPSTONE_ARTIFACTS/new-synthetic-save" ./scripts/test-local-loop-play.sh
```

To preserve source hashes, final APK hash and a source-only archive after checks:
`./scripts/archive-local-loop.sh` with `CAPSTONE_ARTIFACTS` set to that run.
