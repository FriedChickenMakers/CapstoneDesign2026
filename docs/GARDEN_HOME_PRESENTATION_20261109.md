# Garden home presentation refresh

## Scope

Apply the first three approved design work groups: plant-centred composition,
compact information/action layout, and consistent wood/leaf/sky colour. Keep the
complete island, existing RenderTexture camera, orbit/zoom and saved plant identity.
Growth/completion, rewards, memory data, purchases and persistence are unchanged.

## Presentation changes

- `GardenHomePresenter` selects a slightly higher default view and groups growth
  information and the smaller garden action in one card above navigation. Plant
  name/date and the smaller settings/balance area have separate bounds. Settings
  and garden action use centre pivots. Remove the old navigation initializer's
  garden-action position override so either initialization order retains the card.
- `PrototypeGardenPreviewLayout` still fits the conservative whole-island radius.
  Cached mesh-box corners supply a projected vertical envelope, allowing spare
  sky to sit above the subject without cropping it or scanning vertices per frame.
- `GardenIslandPresentation` owns one transient clone of the known derived island.
  Only the lower cap is compressed; the meadow, rim, topology, materials and
  placement heights remain exact. Disable/destruction restores the source and
  releases the clone; re-entry reapplies it.
- Shorter rear lamps keep the plant silhouette clear. Night ambient/moon light
  supports the plant while reduced lamp intensity keeps the ground pools secondary.
- Existing wood/leaf assets gain muted warm-wood/sage tints and low-opacity colour
  layers, retaining active-tab contrast and opaque square navigation corners.
  The shared sky uses a softer blue; garden and album retain the same local clock.

## Validation approach

Use a hidden synthetic Unity project with a separate product identity and in-memory
states. No original Editor input, real save writes or device data are required.

`scripts/test-combined-memory-ui.ps1` runs the real MockupMain scene through its
RenderTextures at 360x640 and 390x844 with true seed and 21-stage mature states,
each at noon and night. It checks full mesh visibility, actual plant GPU pixels,
information/control containment, pointer routing, navigation, unchanged state and
camera/texture reuse. Existing memory edit/delete/failure/retry/photo checks remain.
`-GardenCompositionOnly` limits visual iterations to the eight compositions.

The same full run includes `SolidIslandValidation.ValidatePresentationOnly` for
102 exact ground-height samples, topology/material preservation, lamp grounding,
day/night switching and owned-mesh release across repeated disable/enable/destroy.
Older overlay/lighting assertions now express the two-row card's containment and
non-overlap contract instead of requiring the previous 64-unit strip.

Evidence is kept in ignored `artifacts/garden-refresh-20261109/`. Initial `view01`
exposed the old button-position override; `view02` exposed a fixture lifecycle
omission (the edit-mode fixture had not called the runtime presenter's `Awake`).
Those failed reports are retained and must not be presented as successful runs.

## Results

- `ui01`: daily-mission UI regression, 17 groups / 3,569 assertions / 319 clicks
  passed; release script compilation passed. Source verification: 554 files,
  zero mismatches. The subsequent home-only framing/lighting edits are covered
  by `view03` below; the mission UI was unchanged after this run.
- `view03`: Unity 6000.3.24f1 GPU scene validation, 14 groups / 502,728 assertions
  (mostly mesh vertices) / 44 clicks / 110 raycasts, zero failures and zero
  captured runtime errors. Source verification: 1,561 files, zero mismatches.
  All eight seed/mature/day/night/size combinations produced visible plant pixels
  without clipping or UI overlap. Day/night captures were visually inspected.
- Island ground/topology/lifecycle checks passed, including 102 unchanged sampled
  placement heights and no retained owned meshes after scene disposal.
- Existing Unity processes remained alive; synthetic product identity was restored
  after each run. `git diff --check` passed. Changes remain local and unstaged.
- `legacy-compile-01`: updated overlay/lighting validators compiled against the
  installed Unity Editor references, exit 0 with no compiler errors or warnings.
- Not run: Android device performance/build and the separate older overlay/lighting
  capture sweeps. Those fixtures' layout expectations were updated; the fresh
  combined GPU run exercises the actual new home UI and day/night rendering.
