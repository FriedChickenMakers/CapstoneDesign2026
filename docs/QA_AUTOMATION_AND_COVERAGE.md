# QA automation and feature coverage

This is an execution map, not a claim of complete test coverage. A test existing
in source, a historical successful run, a current successful run, and a CI job
being configured are different facts. Assertion counts are not feature or code
coverage percentages.

## Required tiers

Run from the repository root. Each execution needs a fresh ignored output
directory. `results.json` and JUnit `results.xml` identify the source commit,
selected scope, each check, elapsed time, and PASS/FAIL/SKIP. The process returns
failure if a required check fails **or skips**. `--optional NAME` must be explicit;
an optional skip produces an overall PARTIAL result, never PASS.
SIGINT/SIGTERM interrupt the active child process group and wait for its cleanup;
the interrupted check is recorded as FAIL. Device logs and any pending display
restoration journal still require verification after interruption.

```bash
python3 scripts/qa.py --tier unit --list
python3 scripts/qa.py --tier unit --output artifacts/qa-unit
python3 scripts/qa.py --tier unity --output artifacts/qa-unity
python3 scripts/qa.py --tier visual --output artifacts/qa-visual
ADB_SERIAL="${ADB_SERIAL:?Select a consented test target locally}" \
APK_PATH="${APK_PATH:?Select the exact APK locally}" \
  python3 scripts/qa.py --tier device --output artifacts/qa-device
```

| Tier | Included checks | Prerequisites and meaning |
| --- | --- | --- |
| `python` | Privacy scanner regressions, runner/device-session failure paths using a subprocess fake ADB, server API/evaluation contracts | Python; does not launch Unity or contact a physical device |
| `unit` | `python` plus C# activity/state/MBCT/garden/walking/personalization contracts, procedural generation contracts, Kotlin health/notification/persona/sensor contracts | Mono or .NET as appropriate, Kotlin compiler jars, Java 17; all data are synthetic |
| `unity` | Project wiring, LocalLoop integration, all EditMode tests, real Editor Play fresh/restore, music Play validation, actual UnityWebRequest consent lifecycle against a local API | Licensed Unity 6000.3.24f1; graphics display for rendering/audio Play. A missing result XML or empty test run cannot pass |
| `visual` | Main/supplemental/guided UI captures, demo interactions, sky/lighting/procedural scene assertions | Licensed Unity plus graphics. Geometry/pixel assertions require complementary vision review |
| `device` | Exact APK installation/hash/launch smoke; current daily collector foreground/Home/screen-off observation | Explicit connected target, debuggable APK, existing collection consent. A disabled collector is SKIP and fails the required gate. This tier does not claim every Android flow is automated |

The GitHub workflow `.github/workflows/qa.yml` runs the required **unit tier and
privacy checks** on pushes, pull requests, and manual dispatch. It installs
standalone Mono/Kotlin/Java; it does not need a Unity license. Tool archives have
pinned versions and SHA-256 checks. Unity, visual, and physical-device gates are
explicit local/licensed-runner work, not hidden successful CI skips. Adding the
workflow is not evidence that the remote workflow has already completed.

The existing `scripts/test.sh` remains a project-wiring smoke check; use
`scripts/qa.py` for the combined suites. Never describe `test.sh` alone as a full
test run. `--only NAME` is useful during development but reports only that scope.

### Optional graphical shared-header/navigation check

`scripts/test-local-loop-play.sh` runs the required batch-mode `fresh` and
`restore` phases by default, using a new isolated synthetic save directory.
`scripts/test-local-loop-play.sh --chrome` runs only the optional graphical
phase and may reuse that synthetic save directory. It observes normal rendered
Game view pixels after page transitions for the shared brand, all three tab
labels and their vector icons, without the preview helper's forced mesh rebuilds.

The optional phase requires a graphical Editor without `-batchmode`, a working
display and license, and any Editor Software Terms already accepted by the user.
Automation does not accept legal terms. An attempted graphical run on
2026-10-27 was blocked before the fixture started by the Editor terms dialog;
its timeout/failure evidence is retained privately. That attempt is not a
successful graphical validation and does not invalidate separate fresh/restore
PASS results. The runner preserves per-phase failure reports and has an external
deadline even when the Editor cannot reach the fixture.

The suspected Android shared-header/navigation regression was a visual-reading
false positive. Direct raw-PNG pixel comparisons found the brand, settings icon
and label unchanged across the original A7 captures, including the pages initially
reported as missing them. Temporary refresh/diagnostic code was removed; no
graphics-backend workaround was applied. Retain pixel-level evidence when an
image preview appears to disagree with the source capture. The optional fixture
is a future regression check, not evidence of a confirmed device defect.

## Safe physical-device entry points

`android_device_session.py` is shared by deployment, launch smoke, capture,
collector observation, and the retained historical experiment wrappers.

- A serial must be provided explicitly. It is never discovered by choosing the
  first connected phone. No actual address or identifier belongs in source.
- Before other device work the original timeout is durably recorded and changed
  to 30 minutes; original brightness/mode are recorded, then manual brightness 20
  is used. Every original is saved before its setting is modified.
- A per-target file lock prevents concurrent sessions. An unrestored journal is
  restored and verified before a new session records original values. Absent
  original settings are restored with `settings delete`.
- Normal completion, errors, SIGINT and SIGTERM run restoration. If disconnection
  prevents it, the command fails and prints the private pending-journal path.
  SIGKILL/power failure cannot execute a finally block; the durable journal
  remains the recovery mechanism. Do not remove it during artifact cleanup.
- Before install or launch, archive app-private files/preferences/databases and
  external garden files, and preserve bounded logcat. An unreadable private
  directory or invalid archive blocks installation/launch. Backups are live
  snapshots, so an independently running collector can write while they are
  copied; they are not falsely labelled atomic application snapshots.
- Updates use `install -r`, compare the installed APK SHA-256 with the input,
  and never clear app data or logcat. Files, logs and images remain private under
  ignored `artifacts/` or an explicitly private directory outside the repository.
- `test-a7.sh` verifies installation and launch/process presence. Optional
  `A7_NAV_TAPS_JSON` captures explicit coordinate taps but does not assert that
  the expected page opened. Screenshots alone are not a navigation test.

```bash
# Reconnect to the same explicit target before restoring a pending session.
python3 scripts/android_device_session.py restore --serial "$ADB_SERIAL"
```

`test-platform-background.sh` now observes `daily_acceleration/status.json`
sample/write timestamps at the current one-minute cadence. It does not use the
retired `sensor_samples.jsonl` stream, grant permissions, enable collection, or
stop the user's existing collection session. Each phase needs at least 70
seconds; default foreground/Home/screen-off phases are 120 seconds each. Status
advancement is a continuity observation, not lossless sensing or battery proof.

`test-s24-health.sh` is metadata preparation only. It intentionally returns
SKIP (77), even after successfully saving package/permission metadata, because
the manual Health Connect checklist remains unexecuted. Unsupported/unconnected
Health Connect must never be inferred to be a functional PASS.

## Feature-by-feature map

“Automatic” below describes available checks. The evidence section records which
versions were actually run. Real-device success cannot be inferred from MOCK,
synthetic LIVE DTOs, Editor pictures, or a separate notification test package.

| Feature | Automated checks | Remaining release gate / limitation |
| --- | --- | --- |
| 2/4/8-week course selection, sequencing, per-program progress, saved-state migration | `test-mbct.sh`, `test-local-state.sh`; LocalLoop and course/gallery fixtures | Final APK selection/return/restart flow; content review is separate from software assertions |
| Guided activities, optional answers, pause/resume, drafts, fixed actions, completion and reward | MBCT/state contracts; LocalLoop; Editor Play fresh/restore | Full physical touch/keyboard/Back sequence, interruption during active practice |
| Daily boundaries, missing days, duplicate rewards, save failures/retries | State/MBCT/activity-notification contracts and LocalLoop | Physical storage-pressure/process-death scenarios are not fully automated |
| Experience journal, plan, record viewing, growth-to-record links | State/MBCT contracts and LocalLoop; guided/supplemental captures | Physical keyboard/accessibility paths; existing private records are not overwritten to exercise tests |
| Plant generation, growth receipts, migration, reconstruction | `test-procedural-plants.py`, garden-appearance/state tests, EditMode plant contracts/rendering | Botanical plausibility, audience preference and diversity comparison are evaluation work, not covered by geometry assertions |
| Garden purchase/placement preview, cancel, insufficient balance, visitor decisions | State/garden contracts; LocalLoop/Play/demo visual checks | Actual mobile interactions and all asset/device combinations |
| Day/night sky, Milky Way, camera orbit, lamps, fog, bloom | Sky/lighting/procedural-scene pixel and geometry checks; multi-size captures | Human/vision quality review, mobile GPU differences, long-duration performance |
| Music choice, 06:00/18:00 crossfade, mute, pause/focus, tab continuity | `MusicPlayValidation.Start` | Physical night-track/crossfade playback, speaker listening, device-specific audio/IME events |
| Walking goal and baseline refresh, exact-range heart refresh | Walking-personalization/baseline/heart-range contracts; LocalLoop | Actual wearable synchronization, permission changes, stale/provider-specific native data |
| Adaptive reminder timing and walking achievement | Kotlin policy/personas; C# integration; separate Android notification instrumentation source | Real Android scheduling delays, permissions/channels, sound/vibration/DND, actual product click routing, reboot/long-term delivery |
| Health Connect read/query/source policy and native charts | JVM health contracts; Unity synthetic heart graphs/captures | Supported phone+watch path, denied/partial/revoked consent, populated native chart. A7 unsupported screen is only that state |
| Daily phone acceleration storage/aggregation | Kotlin accumulator/buckets; isolated Android storage script; current collector lifecycle script | Consent Start/Stop UI, write/registration failure, 48-hour/low-battery stops, OEM power behavior and measured battery drain |
| Settings, time dialog, all tabs, typography and layout | UI captures/overlap/overflow checks; time-dial EditMode tests | Native confirm→schedule end-to-end, large system fonts, TalkBack, physical landscape/keyboard variants |
| Optional research event queue/API/model policy/fallback | Personalization C# contracts; `server/tests` HTTP/schema/idempotency/deletion/evaluation contracts; `personalization-network` Editor Play gate uses actual UnityWebRequest and local API | Deployed TLS/Android round-trip and actual consented cohort results remain separate; synthetic evaluation is not real-user model improvement |
| Repository privacy, device preservation/restoration | Real Gitleaks + repository scanner; scanner regressions; fake ADB session/timeout/skip failure tests | Human review of prose/art/assets/licenses and real-device restoration readback |

## Evidence and acceptance

Before this automation change, the 2026-10-25 UI release had 216 synthetic
captures with no reported text overflows, LocalLoop 34/34, Play fresh/restore,
and scoped A7 portrait/native-dialog checks. See
[the exact scope](UI_SERVICE_POLISH_2026-10-25.md). That evidence does not certify
the newly changed course, server or automation paths.

The retained notification evaluation used a separate synthetic Android package:
200 real NotificationManager/PendingIntent executions and 465 repeated checks.
It did not accelerate OS scheduling or demonstrate real-user CTR, speaker output,
or long-term delivery. See [notification limits](ACTIVITY_NOTIFICATIONS.md).
Music's historical 49-check Editor Play result likewise does not establish the
new night track's device playback; see [audio scope](BACKGROUND_MUSIC.md).

The first implementation run under ignored `artifacts/qa-automation-20261026/`
had 15/16 pure checks pass. Its garden-appearance fixture still treated new
course-selection fields as old schema fields. The corrected migration regression
passed separately; a later complete run at
`artifacts/qa-implementation-20261026/unit-final/results.json` passed 16/16.
These are distinct execution records, including the initial failure.

Current 2026-10-27 local evidence, before the final source commit:

| Evidence under ignored `artifacts/qa-final-20261027/` | Verified scope |
| --- | --- |
| `standalone-unit/results.json` | 16/16 required pure suites PASS, no skips; 42 Python harness tests and 22 server/evaluation tests. Ubuntu Mono 6.8, standalone Kotlin 2.2.0 and Java 17 exercise the workflow's compiler paths without starting Unity or ADB. Both pinned Kotlin and Gitleaks archive checksums were verified locally. This row records local workflow compatibility. The separate GitHub Actions run for `c57b6eb` later passed all 16 required suites; its downloaded result is `artifacts/qa-final-20261027/remote-ci-c57b6eb/results.json`. |
| `personalization-network/result.json` | Actual Unity Editor Play and UnityWebRequest PASS: 10 client assertions plus independent database checks showing one stored synthetic event before withdrawal, then zero events and revoked enrollment. `cleanupComplete` is true. This is an Editor/loopback result, not a deployed Android/TLS round-trip. |
| `play-synthetic/play-fresh.json`, `play-synthetic/play-restore.json`, `play.log` | Both synthetic Editor Play phases PASS, with matching phase-specific log markers. Unity Search indexing exceptions and headless audio warnings remain in the log; this is a functional fixture result, not an error-free Editor or audible-audio claim. |
| `editor/local-loop-validation/results.json` | LocalLoop integration 39/39 PASS. |
| `preferences-gallery/` | 48 PNGs with zero reported text overflows; vision review passed for 12 selected images. The selected visual sample does not certify every screen or physical device layout. |

The Python network-runner regressions use a fake Editor subprocess making real
loopback HTTP requests. They validate orchestration, database checks, timeouts
and credential cleanup; the separate real Editor result above establishes the
UnityWebRequest path. The fixture isolates the app save and personal event queue,
uses no actual participant enrollment, and removes generated enrollment, queue
and database files in `finally`. Only private reports/logs remain.

The device-session regression cases use a subprocess fake ADB. They establish
failure handling and restoration orchestration, not physical Android behavior.
Separately, the historical physical-device session preserved internal and
external archives whose sizes and hashes match their manifest. Its restoration
journal records successful readback of all three display settings. The private
review remains under ignored
`artifacts/qa-implementation-20261026/device-before-verified/analysis/`; no device
identifiers, saved preferences, health measurements or personal records belong
in this coverage document. That backup/restoration evidence does not establish
installation of the current APK, current collector continuity or audible alerts.
The current-APK physical-device gate remains NOT_TESTED here until its exact APK
hash and scoped installation/launch results are recorded.
Before a release, link the exact final commit/APK and current results for the
required Unity/visual/device/manual gates. Missing gates remain NOT_TESTED or
SKIP; historical PASS is not copied into them.

QA can begin with a declared feature freeze and known limitations. A claim that
**all functionality is automatically tested** is still inaccurate: native E2E,
accessibility, performance/battery, content validity and real-user evaluation
have explicit remaining work above.
