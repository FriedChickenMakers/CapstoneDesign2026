# Prototype UI readability and polish — 2026-09-27

## Goal and contract

The player can comfortably read and operate the existing portrait prototype, with clearer visual hierarchy and unmistakable selection feedback. The maintainer can edit all visuals in Unity.

- Increase typography on the existing 720-wide design: titles 42–46, main buttons 34, body/input/record notes 30–32, supporting labels 26–28. Emotion labels fit five columns at 26. Do not silently shrink critical copy to old sizes.
- Keep the warm paper/forest-green palette; deepen secondary text contrast, unify corners/borders and button feedback, add restrained native vector navigation/selection icons. No raster assets or external dependencies.
- Preserve the same four pages, mission → emotion/note/choice → growth → home, two species/four stages, dark mission state, home orbit, and session-only history. No persistence, new gameplay, forms, services or privacy changes.
- Keep one explanatory line per screen. Empty, selected, disabled, completed, long-note and modal states must remain legible.
- Stable header/navigation/action anchors on tab switches; sufficient touch targets; text within bounds at 720×960, 720×1280 and 720×1600. Long history text wraps and determines row height. Phone keyboard/accessibility scaling are separate future validation.

## Design / use cases

Use cases: start/finish the daily mission, select a mood and branch, type a note, confirm growth, read/scroll history, inspect the island, use demo modals. Domain events and state guarantees are unchanged.

An idempotent editor polish pass modifies only the existing UI hierarchy and serializes editable styles. The scene builder calls the same pass for future rebuilds. A small selection presentation component renders nav/mood/choice emphasis from controller state, without storing gameplay state or polling input. Record row layout remains responsible for wrapped-note height.

Acceptance checks extend the existing scene validator and PlayMode smoke: minimum font sizes, selection feedback, complete button bounds, non-blocking decorative graphics, record text fit, tab stability, mission contrast and unchanged journey/orbit behavior. EditMode covers selection rendering and dynamic row layout. Save a scene snapshot before applying; do not replace garden models or unrelated dirty files.

## Verification

- Applied and saved `Assets/Scenes/Prototype.unity` with its original GUID. No new dependencies, persistence, gameplay changes, or model regeneration. Other pre-existing dirty materials, MockupMain, packages and project settings were preserved.
- Edit Mode: **99 passed / 0 failed / 0 skipped**, final run 2026-09-27 19:09 KST. Thirteen added cases cover page/mood/branch selection presentation and larger Korean note wrapping/card height.
- Play smoke: **4,332 assertions passed at each of 720×960, 720×1280 and 720×1600**, zero failures. Includes one-line labels, minimum type sizes/no autosizing, full hit bounds, selected markers, non-intercepting decorations, long-note wrapping/scrolling, repeated tab stability, mission background/CTA contrast, both plant routes and InputSystem mouse/touch orbit integration. These are repeated assertions across the existing journey, not 4,332 distinct test cases.
- Scene validation passed: 21 bound buttons, four pages, two species, 8,728 plant triangles. Native icon renderers, selection references, minimum fonts and scene components are valid.
- Visually inspected all three GameView proportions, emotion form, 200-character history cards, demo/reset dialogs and growth popup. The first test exposed insufficient padding width for the larger demo label; corrected to 12 units per side before passing all proportions.
- Actual Windows mouse clicks in a fresh 720×960 Play session verified home → history → home → mission → completion → mood/branch selection → growth popup. The saved scene is stopped with GameView restored to 720×1600; reapplied the polish pass to refresh the stopped editor preview after resizing.

### Evidence / remaining risks

- `artifacts/prototype/editmode-results.xml`, `daily-play-smoke-{720x960,720x1280,720x1600}.txt`, `daily-mood-and-branch-*.png`, `daily-history-three-*.png`, `daily-mission-active-home-*.png`, `polish-demo-settings-*.png`, `polish-reset-confirmation-*.png`.
- The editor-only orbit smoke temporarily isolates virtual devices from live OS input and restores its filter in a `finally` block. After the automated journey a native click did not respond in that same Play session; stopping and restarting Play restored normal clicks, and the fresh-session journey above passed. The exact harness/editor cause remains unisolated; do not treat synthetic input checks as physical-device validation. Restart Play after this smoke test before interactive review.
- Physical phone touch, virtual keyboard/IME, OS accessibility font scaling and Android build were not tested. No clinical-effect claim or permanent mood storage was added.

## Editing / rollback

- `PrototypeUiPolishUpgrade.Configure` centralizes sizes, palette, spacing, card/button styles and serialized bindings; the scene builder also uses it. Reapply via **Capstone Prototype → Apply Readable UI Polish** with the scene saved and Play stopped. The pass reuses named decoration objects rather than duplicating them.
- `PrototypeUiSelectionView` reads existing controller state solely for visual selection. `PrototypeUiIcon` supplies editable leaf/journal/check/arrow vector graphics. `PrototypeUiSurface` adds absolute corner radius and optional border while retaining legacy roundness behavior when radius is zero.
- `PrototypeMissionPresentation` changes primary button contrast alongside the existing mission background. `PrototypeMoodHistoryRow` exposes note inset/minimum-height parameters and measures the larger note text; domain/session/data contracts remain unchanged.
- Pre-change scene snapshot: `artifacts/prototype/Prototype-before-ui-polish-20260927.unity`. A complete rollback must restore compatible UI code along with that snapshot; it is not an independent runnable project backup. No commits or pushes were made.
