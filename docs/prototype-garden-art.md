# Prototype garden art and interaction — 2026-09-27

## Contract

Player goal: see a recognisable miniature plant grow and inspect the island from any side on the home screen.

- Exactly four visible stages **including seed**: seed → sprout → buds → bloom. Three daily completed-and-recorded missions advance the plant; two final species remain chamomile/hydrangea. Existing four question variants remain authored data; this short run uses the first three. Majority of those three answers determines the flower.
- Mission → emotion/note/branch record → growth popup → home stays unchanged. No persistence, growth animation, currency, new mission type or dependency.
- Original low-poly geometry inspired by clustered foliage, warm earth and faceted miniature scenery in the [official Solar Forest screenshot](https://www.hanwha.com/newsroom/news/press-releases/hanwha-group-launches-ninth-hanwha-solar-forest.do). No source-game assets downloaded or copied.
- Home's plant preview accepts mouse/one-finger drag: 360° yaw and ±85° pitch (top and underside; no inverted pole crossing). Release stops. UI/records do not rotate the island; navigation cancels the gesture, mission/popups use default view, returning home restores its viewing angle. Angles are session-only.
- Different screen sizes use relative preview-local motion, not fixed pixel sensitivity. The latest correction (2026-09-28) enlarges the actual UI preview rather than only widening the camera. Responsive projection preserves full-island visibility at every orbit angle, with at least 1.5% margin on all preview edges.

## Design and verification

Session remains the growth/vote information expert. PlantView switches four authored silhouettes. Editor garden art builder creates original geometry and material-batched meshes. IslandDrag owns camera presentation and pointer lifecycle, independent of mission conditions.

Apply Garden Art updates only the Prototype garden/preview wiring and stage definition; it does not regenerate unrelated UI. Scene/asset GUIDs are retained. Generated meshes can be rebuilt from the editor builder; replace the stage roots with artist assets later. No network/privacy/persistence API changes.

Acceptance: eight 3-answer vote paths; exactly one visible stage; visibly distinct species; bounded low-poly triangles/renderers; normalized drag, pitch bounds, pointer isolation/cancellation; camera framing front/back/top/bottom; default modal view and home restore; existing responsive/mission/history regression. Editor rendering and mouse checks are not physical-device touch/GPU performance validation.

## Verification results

- Unity 6000.3.24f1 compiled successfully; Apply Garden Art saved the existing Prototype scene with its original GUID. Reference/scene validation passed (21 buttons, four pages, two species).
- All authored plant stages/species together total **8,728 triangles**, material-batched rather than individual petal renderers. Each active garden is checked against 12k triangles and 32 renderers, and all plant variants against 18k triangles/40 renderers.
- EditMode: **86 passed, 0 failed, 0 skipped**. Eight three-vote routes, existing mission/reflection/daily rollover/history contracts, 18 responsive calculations and 15 orbit cases. Lifecycle callback unit tests explicitly invoke callbacks because these runtime components do not execute in EditMode; PlayMode checks verify real activation/deactivation.
- 720×960, 720×1280 and 720×1600 PlayMode: **3,008 checks passed per resolution, zero failures** in the final runs (18:38–18:40 KST). This includes InputSystem virtual mouse and touchscreen → UI input bindings → raycast → drag (press/move/release across frames), not only direct handler calls. Temporary virtual devices are removed in finally blocks, including on test cancellation. Mesh projection framing was checked for seed/sprout/buds/both flowers and front/three quarter/back/top/underside camera positions.
- An earlier 720×960 run checked touch release too early and failed. The editor test runner now waits for two actual PlayerLoop frames per queued input state, with a five-second timeout, instead of assuming each EditorApplication.update is a game frame. All three final runs passed without changing runtime drag behavior. Physical-device validation remains necessary.
- Visually reviewed the actual seed, sprout, bud, chamomile and hydrangea renders, including an underside capture. Existing mission-darkening, note/history scrolling, repeated page switches, default growth-popup view and restored home orbit passed regression checks.
- Native automation successfully clicked UI buttons, but its drag delivered down/up at the same endpoint in one update (no drag event). This automation limitation is distinct from the passing frame-separated InputSystem mouse/touch integration checks. Temporary diagnostic logging was removed. Physical human drag/mobile touch, phone GPU performance and Android build are still unverified.
- Evidence: `artifacts/prototype/editmode-results.xml`, `daily-play-smoke-*.txt`, `daily-input-{mouse,touch}-orbit-*.png`, `daily-growth-*-QTQ-*.png`, `daily-final-{chamomile,hydrangea}-*.png`, `daily-orbit-*.png`.
- Pre-replacement scene snapshot: `artifacts/prototype/Prototype-before-garden-art-20260927.unity`. Old art assets remain untouched; only owned scene garden roots were replaced. A rollback of the complete feature also needs compatible prior scripts/definition, not just this scene snapshot.
- Final editor handoff: saved Prototype scene, Play Mode stopped, Game view restored to 720×1600. No unrelated existing tracked edits were reverted. `git diff --check` passed; no commit or push performed.

## Handoff / editing

- Growth stages: `PrototypeDefinition.StageCount` (4 visible states), `maximumStage` (3 successful recordings). The first progress mark is filled at seed; after 3 daily actions the result is 4/4. There is no fifth visual state.
- Visual authoring: `PrototypeGardenArt.cs` and `Assets/Materials/Prototype/Refined_*.mat`. Run **Capstone Prototype → Apply Garden Art** in a saved, stopped Prototype scene to regenerate model meshes only. Material edits are preserved. `01_Seed`, `02_Sprout`, `03_Buds`, `04_Bloom/{Chamomile,Hydrangea}` are separate replacement points for later art assets.
- Drag: `MainPlant` → `PrototypeIslandDrag`. Camera distance/focus/default angles are editable; pitch is clamped ±85° to avoid an upside-down pole. UI raycast catches only home preview. No save system or animation was added.
- Closer framing (2026-09-27): orbit distance is now 5.8 and Garden Camera's Orthographic Size is 1.7 (previously 7 / 1.92). The smaller orthographic size makes the island/plant about 12.9% larger; distance alone does not magnify an orthographic view. `Apply Closer Garden View` updates the existing saved scene without rebuilding art/UI. Focus (0, 0.58, 0), default yaw/pitch (-25, 28), and drag direction/limits remain unchanged. All-stage sphere bounds protect every orbit angle from clipping.
- Superseded close-up (2026-09-27): Size 1.1333333 enlarged the plant but cropped the island. The subsequent Size 1.7 camera-only fix prevented clipping but made the plant smaller; it is superseded by the responsive UI below.
- **Current UI framing (2026-09-28):** `MainPlant`, `MissionPlant` and `GrownPlantPreview` each have `PrototypeGardenPreviewLayout`. Home uses the full space between the guide and compact bottom growth row: **704×880 at 720×1600**, versus the former 600×540. Measured apparent plant size is **1.454×** the preceding full-island view. At 720×1280 the region is 704×560; shorter screens scale the existing portrait column to fit.
- The active preview owns camera aspect/orthographic size; changing Size alone in the Inspector is now overridden. Edit `horizontalPadding`, `boundaryGap`, `edgeMargin`, and the upper/lower boundary RectTransforms to adjust composition. All authored stages/species contribute to cached XZ/full-radius bounds, so rotating or growing does not cause zoom breathing. After replacing geometry in the Editor, re-enable the layout component or run **Capstone Prototype → Expand Plant UI Area** to remeasure. Art rebuilding also rebinds and remeasures automatically.
- The shared 1000×900 RenderTexture is retained; projection matches the final UI aspect to avoid distortion without per-frame texture allocations. Growth popup height now follows the safe area, with title/guide at the top and stage/summary/return button at the bottom. Camera distance/focus/rotation, mission rules and hidden decorative ovals are unchanged.
