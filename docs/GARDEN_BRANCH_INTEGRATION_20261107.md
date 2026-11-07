# Garden integration — 2026-11-07

## Sources and decisions

The working base is `a05634cbf6f33161dfb3a2a12b294b68356cd2ab`
(`codex/integrate-garden-missions-20261029`). Selected changes come from
`texture-rework`, updated with `git pull --ff-only` to
`255fb987b4a4f56768d0d626cefff9262f74d6c9`. The integration worktree is
`codex/combine-garden-20261107`. Work was prepared locally; after the runtime
follow-up below, the user explicitly authorized committing and pushing this
integration branch on 2026-11-08.

The user confirmed automatic collection of completed plants, removal of partial
growth, and wood/leaf textures on backgrounds, headings, and buttons. Everything
outside the selected feature set retains the previous branch's behavior.

## Product behavior

- Retain P01–P28 guided slides and one basic/one additional mission per day.
  Unfinished practices resume without any partial growth or reward.
- Each eligible practice completes atomically with one growth step and 50 coins.
  The separate daily emotion record opens after both practices and grants a
  memory leaf plus 100 coins, without growth.
- New plants require 21 completed growth steps. Completed plants appear in the
  photo album automatically; the next growth-eligible mission starts a new seed.
  Existing plants retain their saved recipe, mesh version, and original target.
  With two growth-eligible practices per day, 21 steps do not imply seven days.
- Keep the previous thumbnail album and individual 3D nature views. Plant dates,
  records, and memory leaves follow the same individual plant, independently of
  which plant is selected for viewing. Record edits/deletions propagate to views.
- Import optional local camera confirmation and its once-per-day bonus of one
  coin into the existing coin balance: practice → optional photo → growth
  cinematic → completion. Skipping the camera still completes the mission.
- Import additional-mission recommendation, recommendation measurement/analysis,
  notification configuration, improved flower/leaf geometry, and text density
  fixes. Recommendations cannot bypass the daily additional-mission limit.

## Design boundaries

`GardenStateService` remains the transaction and persistence boundary. The plant
collection owns plant identities, recipes, dates, growth receipts and memory
links. Compatibility projections support previous album/rendering callers and
are validated against the canonical collection. No second wallet or save store
is introduced. Historical partial-growth receipts remain migration evidence;
they cannot grant new growth after the policy change.

The branches both used Focus content version 3 with different step definitions.
The previous guided definition becomes version 4. Schema-1 guided sessions are
mapped to that definition; texture-branch version-3 sessions keep an explicit
historical definition. Migration occurs before applying current validation.

Camera rendering retains the previous shared camera → RenderTexture → RawImage
structure. Memory selection uses the RawImage viewport and distinguishes taps
from orbit gestures. Screen failure/retry paths must remain navigable without
discarding the save.

## Validation plan

All fixtures use synthetic state or isolated projects. Verify both save families,
repeat migration, failed writes/retries, duplicate commands, daily reward limits,
automatic archive/next seed, immutable appearance, leaf edit/delete, full reset,
camera cancellation and reset generations. Run relevant pure C#/Python/Kotlin
checks, Unity compilation, actual rendered home/mission/record/album views,
font-layout regressions, and responsive input checks. Android results require
fresh isolated evidence; earlier reports are not results of this integration.

Final review must list actual checks and remaining limitations. Do not publish
before the user reviews the completed local result.

## Implemented and verified (2026-11-08)

### Follow-up: actual Editor checkout (2026-11-08)

The user reported that both the garden and tab errors remained. The interactive
Editor was still running `CapstoneDesign2026` on `texture-rework@255fb98`, while
the integration had only been applied to `CapstoneDesign2026-combined`. Previous
isolated test results did not establish that the user's running checkout was
fixed. Its Editor log still contained `FocusMissionEntrance.Awake` failures when
accessing a missing `CanvasGroup` on `Mobile.Content`.

The integration branch `codex/combine-garden-20261107` now belongs to the actual
`CapstoneDesign2026` working directory. The separate worktree is preserved on
`codex/combine-garden-review-copy-20261108`. All 1,894 publishable source files
were compared by SHA-256 after transfer; 244 differing/missing files were copied.
Neither source branch was reset, committed, or pushed. The transfer manifest
and previous file versions are under ignored
`artifacts/runtime-repair-20261108/source-transfer/`.

This restores the existing `RequireComponent(CanvasGroup)` and Unity-aware null
checks to the actual checkout, together with the old-save migration and safe
load retry views. The latest texture branch's missing cloud restart condition is
also restored: returning to the garden restarts its deliberately non-autoplay
particle system. Camera → RenderTexture → RawImage remains unchanged.

Follow-up verification uses the transferred source tree. Private save files are
read only for in-memory compatibility checks; no test writes into those files.
Visible Editor controls, mouse input, and device data remain untouched.

Read-only compatibility evidence now establishes the second failure directly:
both existing primary and backup saves pass their checksums but fail the exact
`texture-rework@255fb98` decoder/service with `FOCUS_MISSION_RECORD_INVALID`.
The transferred integration accepts the same bytes, migrates to schema 3 in an
in-memory store, preserves identity, wallet, receipts, sessions, recipes, memories
and photo archives, and passes repeat-open plus failed-migration/retry checks.
Before/after hashes of both original files are identical. The report contains
only booleans/error categories, not private record values:
`artifacts/runtime-repair-20261108/private-save-check/report.json`.
The original-branch synthetic fixture regression was also rerun from the actual
checkout and passed all 58 checks. Repository privacy scanning passed 1,894 files.

Fresh Unity follow-up evidence:

- `artifacts/runtime-repair-20261108/actual-checkout-01/`: the first actual-scene
  run passed 133 checks and 17 pointer clicks, with no Play runtime errors, but
  the deep test directory caused Windows path-length failures importing optional
  URP package shaders. This is not treated as a clean import result.
- `artifacts/r8/p2/`: reran the same transferred product source through a temporary
  short path. Unity compilation/import and three Play phases passed: 132 checks,
  17 raycast-verified pointer clicks, zero runtime errors, and no package import
  failures. All 1,553 copied source files still match the actual checkout. The
  temporary drive alias was removed and the original Editor processes survived.
- The Play regression exercises real `MockupMain` home → missions → records →
  home navigation before and after restart, exact saved-state preservation,
  `FocusMissionEntrance`/CanvasGroup creation and cleanup, and the same garden
  camera/RT/visible RawImage. It requires normal product rendering to replace a
  deliberately poisoned synthetic RT, and verifies cloud playback and movement
  after returning to the garden, including an explicitly stopped system.
- `artifacts/r8/ui03/`: 14 UI groups, 1,942 assertions and 313 raycast-verified
  pointer clicks passed at 360×640 and 390×844, including mission and records
  screenshots and release script compilation. All 552 copied sources match.
  Fresh garden RT and portrait UI captures were visually inspected.
- Final whitespace and Gitleaks-backed repository privacy checks passed; the
  index remains unstaged. No physical Android check was run in this follow-up.

The already-open interactive Editor was intentionally not focused, driven, or
restarted. It must refresh/recompile these local files and start a new Play run
for the user to review this version. The isolated results above do not claim
that the user's existing interactive Play session has already been replaced.

### Original isolated integration evidence

At the end of the original isolated run, the `texture-rework` checkout was clean
at `255fb98`; its remote head was checked again after integration. Integration
edits were then only in the separate combined worktree. No commit, push,
real-save replacement, physical input, or device deployment was performed.

The implementation preserves previous daily mission/slides/record navigation,
full reset and growth cinematic behavior. It adds canonical plant collections,
automatic completion archives and next seeds, memory-leaf picking/edit/delete,
historical Focus-definition compatibility, optional camera reward, local
recommendations, measurement/analysis, notification options, and the new theme.

Additional defects found by this integration's checks were corrected:

- Different version-3 mission definitions no longer cause old save validation to
  reject the mission and records tabs. Failed loads also expose a safe retry page.
- Previous safe CanvasGroup initialization is retained. The shared garden camera
  and RenderTexture output are preserved.
- A stale completion-retry callback no longer dereferences a destroyed page.
- Preference changes persist their timestamp, so a new explicit preference wins
  over an older mission answer after restart.
- Starting the next plant is allowed through the growth cinematic, which shows
  that new seed rather than morphing the completed previous plant into it.
- Latest text-metric and font-atlas callback fixes are included. Wood headings and
  the garden settings button use readable foreground colors.

Fresh evidence is under ignored `artifacts/`; it is synthetic, not user data:

| Check | Result / evidence |
| --- | --- |
| Latest branch before integration | Home, mission and record images at 390×844 and 900×1600; `integration-latest/run-02` |
| Original-branch save generation and migration | 58 checks pass; actual compiled a056/255 code generated the inputs; `branch-save-fixtures` |
| Combined plant policy and migration | 175 checks pass; `combined-20261107/domain` |
| Camera reward and recommendation integration | 311 checks pass; `combine-garden-20261107/combined-camera-04-report.json` |
| Existing state, Focus, guided mission, daily emotion, full reset, growth and appearance contracts | Final 14/14 runners pass; `combined-regression/pure-final/report.json` (includes all-mission 2,711 and guided 399 checks) |
| Daily mission UI, actual pointer input and non-development player compilation | Final 14 groups pass, 1,942 assertions; `integration-ui/combined-ui-final` |
| Memory/album UI, rendering, shared camera, low angles, paging and resource release | 5 groups pass, including text-density validation; `integration-ui/memory-04/renders/report.json` |
| Theme mesh, opaque tab corners and texture contracts | Unity EditMode NUnit 24/24 pass; `integration-ui/theme-02/results.xml` |
| Actual Play mode, 21-stage completion, next seed, cinematic interruption/restart | 3 phases, 78 assertions, 0 runtime errors; `integration-play/combined-growth-01/play/play-report.json` |
| Native Android library compile and Robolectric | 30/30 integration tests pass; `combine-garden-20261107/platform-05` |
| Native epoch, reminder and measurement checks | 36 epoch / 27 reminder checks plus measurement checks pass; same platform evidence |
| Python server | 52 synthetic tests pass; `combine-garden-20261107/server-01` |
| Working-tree privacy scan | Gitleaks-backed repository scan passed, 1,894 files at that checkpoint |

Android Unity player build and physical-device performance were not run: this
Unity installation has no Android Player module. Native library compilation is
not an APK or a device performance result. Actual camera/model recognition and
device notification delivery also remain unverified. Four server tests that start
HTTP sockets were deliberately excluded from the no-server validation run.

There are no remaining product-policy questions from this merge. Review the
combined worktree before authorizing any commit or push.

## Main review files

| Area | Files |
| --- | --- |
| Atomic save/migration/growth/archive | `Assets/Scripts/Runtime/LocalGardenState.cs`, `CombinedPlantCollection.cs`, `WeeklyPlantState.cs`, `GardenPlantAppearance.cs` |
| Historical mission definitions and recommendations | `Assets/Scripts/Runtime/FocusMissionCatalog.cs`, `FocusMissionCatalog.TextureHistory.cs`, `FocusAdditionalRecommendation.cs` |
| Garden/album/memory UI | `Assets/Scripts/Runtime/GardenHomePresenter.WeeklyPlants.cs`, `GardenMemoryPicker.cs`, `WeekOneQuestDemo.PlantAlbum.cs`, `WeekOneQuestDemo.PlantMemories.cs`, `PlantAlbumHistory.cs` |
| Theme/text | `Assets/Scripts/Prototype/GardenThemeSurface.cs`, `Assets/Scripts/Runtime/ScreenDensityText.cs`, `FigmaMissionUi.cs`, `MissionBottomNavigation.cs` |
| Camera/notification/measurement | `Assets/Scripts/Runtime/CameraMissionPolicy.cs`, `WeekOneQuestDemo.PhotoEvidence.cs`, `PhotoVerificationService.cs`, `ActivityNotificationBridge.cs`, `PersonalizationClient.Measurements.cs`, Android platform sources, `server/measurements.py`, `server/analyze_measurements.py` |
| Regression evidence entry points | `scripts/test-combined-branch-fixtures.ps1`, `test-combined-plants.ps1`, `test-combined-camera-recommendation.ps1`, `test-combined-memory-ui.ps1`, `test-mission-growth-play.ps1`, `test-daily-missions-ui.ps1` |
