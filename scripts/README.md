# Build and verification scripts

The combined test entry point is `python3 scripts/qa.py`. See the
[QA tiers and feature coverage](../docs/QA_AUTOMATION_AND_COVERAGE.md) for exact
scope, remaining manual gates, and the distinction between available tests and
current run evidence. Required SKIP results fail a gate; optional skips remain
PARTIAL. Scripts resolve their own repository root.

```bash
python3 scripts/qa.py --tier unit --list
python3 scripts/qa.py --tier unit --output artifacts/qa-unit
python3 scripts/qa.py --tier unity --output artifacts/qa-unity
python3 scripts/qa.py --tier visual --output artifacts/qa-visual
```

`unit` includes Python infrastructure/server contracts, pure C# domain tests and
pure Kotlin policies. It never starts Unity or contacts a device. Mono is selected
by `UNITY_MONO_ROOT`; `/usr` works for a standalone Linux Mono installation.
`KOTLIN_GRADLE_LIB` accepts either Unity's Gradle compiler jars or a Kotlin SDK
`lib` directory. `JAVA_BIN` selects Java 17+. The CI workflow installs standalone
pinned toolchains and runs unit/privacy checks on push and pull request.

Unity tiers need a licensed Unity editor (`UNITY_BIN`, default `unity-editor`)
and a graphics display for Play/rendering. Entry points use
`scripts/unity-run.sh` and its shared `Library/capstone-editor.lock`. Do not start
parallel editors against this project's Library. `scripts/test.sh` only validates
project wiring; it is not the full suite. Isolated synthetic saves are created by
the combined runner, which never changes the operating-system clock.

Run Unity as the OS user whose Unity license is already configured, with that
user's actual `HOME`. On the shared worker this is `codex` with
`HOME="/home/codex"`; a root shell must switch users before launching the Editor.
An access-token warning alone does not establish license expiry: check whether
entitlements resolve and the requested validation actually runs. Do not copy or
edit license files to work around an account mismatch. Before switching users,
check existing Editor processes and project locks; repair only specifically
identified generated files that the wrong user left unwritable, not the entire
checkout. Keep license logs and generated captures in ignored artifacts.

```bash
./scripts/bootstrap-project.sh
./scripts/test.sh
./scripts/visual-preview.sh
./scripts/build-android.sh
```

## Device work

Always set an explicit `ADB_SERIAL` locally; do not commit it. Use
`ADB_BIN=/opt/android-sdk/platform-tools/adb` if needed. `deploy-a7.sh`,
`test-a7.sh` and `collect-a7-artifacts.sh` use the shared safe session: durable
original display settings, temporary 30-minute timeout/manual brightness 20,
private data/preferences/garden backups before update or launch, in-place update,
APK hash verification, finally restoration and readback. No app data or logcat
is cleared. Failed restoration leaves a pending private journal for reconnection.

```bash
ADB_SERIAL="${ADB_SERIAL:?Select the target locally}" \
APK_PATH="${APK_PATH:?Select the built APK locally}" ./scripts/deploy-a7.sh

ADB_SERIAL="${ADB_SERIAL:?Select the target locally}" \
APK_PATH="${APK_PATH:?Select the built APK locally}" SKIP_BUILD=1 ./scripts/test-a7.sh

python3 scripts/android_device_session.py restore --serial "$ADB_SERIAL"
```

Launch smoke verifies installation/process presence and saves screenshots. It
is not an assertion that every UI flow works. Optional `A7_NAV_TAPS_JSON` contains
explicit `[x,y]` points for the current screen; no obsolete coordinates are
silently reused. Evidence is private under ignored `artifacts/` or a private
external directory. Do not upload real device reports with CI artifacts.

After the user normally enables daily collection, observe the current minute
summaries/status without enabling or stopping collection:

```bash
ADB_SERIAL="${ADB_SERIAL:?Select the target locally}" \
  ./scripts/test-platform-background.sh --screen-off-seconds 300
```

The old raw-file timestamp test has been replaced. Each observation phase must
span at least 70 seconds for the current one-minute cadence. Missing/disabled
collection yields SKIP (77), not a successful test. Start/Stop UI, power behavior,
force-stop/reboot and battery drain remain separate gates.

`test-s24-health.sh --serial "$ADB_SERIAL"` collects metadata and creates a manual
checklist. It returns SKIP (77): metadata or an unsupported-device screen does not
prove actual Health Connect functionality. Complete
[the supported-device checklist](../docs/S24_WATCH_TEST_CHECKLIST.md) separately.
`test-daily-acceleration-device.py` checks Android storage using isolated cache
files and requires a matching built/installed debug APK. Historical summary/Recents
experiment scripts remain distinct from the current daily collector test.

## Specific synthetic and rendering checks

Individual domain scripts remain available, e.g. `test-local-state.sh`,
`test-mission-recommendation.sh` (pure optional review selection/UI contract),
`test-mbct.sh`, `test-health-query.sh`, `test-heart-range-refresh.sh`,
`test-walking-personalization.sh`, `test-notification-policy.sh`,
`test-notification-personas.sh`, `test-garden-appearance.sh` and
`test-procedural-plants.py`. Their inputs are synthetic and their assertions do
not establish clinical efficacy or real-user preference.

The `unity` tier includes Play initialization/persistence and music transitions.
The `visual` tier includes multi-size activity/settings/guided captures,
`GardenLightingValidation`, `GardenSkyValidation`, and
`GardenProceduralValidation`. Vision review and final-device verification remain
necessary. Lighting checks hold bulb emission constant when measuring actual
point-light contribution, and check fog lighting, bloom and overlay preservation.
The procedural scene checks growth stages and species in actual home rendering.

Imported lamp provenance is in
`Assets/Art/External/KenneyStreetLamp/README.md`; procedural integration and
rollback details are in [the integration document](../docs/PROCEDURAL_GARDEN_INTEGRATION.md).

Before committing/pushing or creating a publishable source archive, follow
[repository privacy checks](../docs/REPOSITORY_PRIVACY.md): run the full working
check with real Gitleaks, then the exact `--staged` check. Missing or failed
scanners are not passes. Device backups, journals and screenshots stay private;
never remove a pending restoration record during cleanup.

## QA/beta measurement exports

See [measurement scope and operating checklist](../docs/QA_BETA_MEASUREMENT_PLAN.md).
`test-measurement-buffer.sh` runs the required pure Kotlin outbox contracts.
`server.analyze_measurements` exports private notification pairs, descriptive
statistics and optional Matplotlib charts; select `live` or `synthetic` explicitly.
Install `scripts/requirements-measurement-plots.txt` in a separate Python environment
for charts, or pass `--no-plots` for standard-library CSV/JSON analysis.
