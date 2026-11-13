# Garden status and memory access

## Current scope after selective garden rollback

The user subsequently preferred the garden immediately before work groups 4–5.
Restore only the home presentation to the verified final groups 1–3 baseline:
the 160-unit card, four appearance icons, and garden-decoration action. Remove
the taller detailed-status card and its two extra home buttons, along with its
unused `GardenPlantStatus` projection. Growth/completion/archive policies and
saved data are unchanged.

Keep the brighter memory leaves, assisted picking, latest-memory page selection,
full record list, and transactional edit/delete/return behavior. The existing
Records → Plant album → Memory leaves route still opens the improved list;
garden and album 3D leaves still open their corresponding records directly.

The restored home baseline has SHA256
`F914CED4FD9593818F148D0D1A72D92EB7E9E6E19E38FF96172478BE01E52670`,
matching the `garden-refresh-20261109/view03` source manifest. The restored home
file differs from that baseline only by the retained memory-screen-picking
binding, cleanup, and helper. Fresh rollback validation is recorded separately
below; the original work-group results remain historical evidence.

## Original work groups 4–5 contract (home portion superseded)

Implement design work groups 4–5 after the garden composition refresh: clear
growth/completion/automatic collection status, and discoverable memory leaves
with a direct record-list entry. Keep the existing growth, reward, completion,
next-seed, identity and persistence policies. Do not publish without user request.

- `GardenPlantStatus` projects the committed individual, using its own
  `GrowthStep/MaxGrowthSteps` (including older 12-step recipes). An early completed
  plant keeps its actual attained level. Automatic collection is shown only when
  the completed individual's saved photo ID and source identity both match.
- The home replaces the four coarse appearance icons with an actual level and
  collection message. Separate buttons open all linked memory records and the
  current saved photo (or album list when no current photo exists). Garden
  decoration remains available. These callbacks read state; they never grow,
  complete, archive or create a plant.
- Memory count excludes disconnected or missing records. A newly appended
  memory brings its 3D page into view without storing view state. Every linked
  record remains available in the full chronological reading list.
- The list uses dated, full-row buttons with mood and small basic/additional
  mission names. It scrolls independently of the fixed return/navigation controls.
  Editor/delete actions reuse existing transactions and return to their original
  garden/list/photo context and list scroll position. Disconnected organ IDs are
  rejected before legacy fallback lookup.
- Linked leaves have a brighter colour and narrow vein; base plant vertices,
  normals, colours, recipes and memory identifiers/positions remain unchanged.
  Deletion keeps the memory geometry while changing colour/selectability.
- Picking prioritizes exact hits, then allows a 16-design-pixel neighbourhood
  (bounded to 12–24 actual pixels). Candidates must be visibly hit by the original
  triangle/occluder test. Nonmemory foreground geometry and similarly near distinct
  leaves are rejected. Expensive assisted probes are capped at four per leaf.
  RawImage rectangle/UV/mask and tap-vs-drag checks still apply. Photo views use
  their own immutable render snapshot, even after the shared studio is reused.

## Files and validation

Main implementation: `GardenPlantStatus.cs`, `GardenHomePresenter.cs`,
`GardenHomePresenter.WeeklyPlants.cs`, `PrototypeGardenPreviewLayout.cs`,
`GardenMemoryPicker.cs`, `GardenPlantPhotoView.cs`, `PlantMemoryGeometry.cs`, and
the `WeekOneQuestDemo.PlantMemories/PlantMemo/PlantAlbum` partials. Existing emotion
and completion copy now uses the memory-leaf/automatic-collection terminology.

The synthetic GPU harness adds actual 21-step/legacy 12-step/early-completion
status cases, null/archived/mismatched-photo read-only projection cases, home
buttons, 46-record scrolling and transactional edit/delete/retry/return cases,
and precise/nearby/blocked/ambiguous/deleted picking contracts. No invalid schema-3
empty garden is persisted just to create a test case.

Evidence lives under ignored `artifacts/garden-records-20261109/`. `view01` is a
retained failed diagnostic run: its legacy fixture omitted baseline category
credits, and its synthetic blocker was incorrectly parented under the plant.
Both fixtures were corrected without weakening product rules or assertions.
`view02` retained an incorrect legacy fixture expectation (.1 growth is the old
3-step value, not 4); the intended 4/12 test now uses the historical .2 value.

All UI validation runs use a separate synthetic product identity and memory store.
The original Editor processes, physical mouse, user saves and connected-device
data are left untouched. Android device performance and the old standalone
overlay/lighting capture sweeps are outside the executed checks for this change.

## Original work groups 4–5 results

- `garden-records-20261109/view03`: Unity 6000.3.24f1 compilation and actual GPU
  scene rendering passed, 19/19 groups, 569,752 assertions (mostly mesh vertices),
  76 UI clicks, 292 raycasts and zero captured runtime errors. All 1,563 copied
  source files matched. Existing Editor processes remained alive; the synthetic
  product identity was restored after execution.
- Visually inspected the final seed/mature homes, legacy 4/12 and 12/12 states,
  2/21 completed state, long record lists and record popup at phone sizes.
  New controls and state details remain in their card without clipping/overlap.
- `garden-records-20261109/ui01`: mission regression 17/17 groups, 3,569 assertions,
  including release script compilation; 556 copied sources matched. Subsequent
  home text-contrast and picker-input validation edits are covered by `view03`.
- `test-memory-leaf-affordance.ps1`: 3,888 pure geometry/identity assertions passed;
  evidence at `artifacts/memory-affordance-20261109/unit-02/affordance.log`.
- Existing `test-plant-memory-presentation.ps1`: 36 passed.
- `garden-records-20261109/legacy-compile-01`: updated older overlay/lighting
  validators compiled successfully; their separate capture sweeps were not run.
- `git diff --check` passed. The prior local work was preserved; no commit/push.

## Selective garden rollback results

- Fresh `garden-home-rollback-20261109/view01`: Unity 6000.3.24f1 compilation
  and GPU rendering passed, 19/19 groups, 571,084 assertions, 94 UI clicks,
  288 raycasts, zero captured runtime errors. All 1,561 copied source files
  matched; the unused status projection was also removed from the reused cache.
- The restored 160-unit home card and four icons passed containment/interaction
  checks. Memory-list tests enter through the actual Records → Plant album →
  Memory leaves route and retain the 46-record scroll, edit/delete failure/retry,
  return navigation, picking, legacy growth and early-completion checks.
- Visually inspected the new mature home at 390×844, seed home at 360×640 and
  long memory list at 390×844. The compact card is restored and the improved
  memory list remains available. Captures use only synthetic state.
- Updated standalone overlay/lighting validators compiled successfully in
  `garden-home-restore-20261109/legacy-compile-01`; their separate capture sweeps
  were not run. Android device/performance checks were not run for this rollback.
- The isolated Unity process exited with code 0. All original Editors remained
  running and the synthetic project identity was restored. No mouse input, user
  save writes, commit or push was performed. `git diff --check` passed.
