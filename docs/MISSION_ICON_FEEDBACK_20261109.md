# Mission icon pointer feedback

## Bug and contract

Dragging the central illustration in the water mission leaves an opaque green
rectangle over the artwork. All mission icons using the shared button feedback
must remain visible during press, pointer exit, cancellation and release. Normal
clicks still advance once; keyboard focus remains visible without filling the
icon. Preserve the existing centre-pivot press animation and mission state rules.
This is a presentation/input bug fix; no persistence or reward changes are needed.

## Cause and design

`FocusControlFeedback.OnSelect` adds Unity's `Outline` to the button's target
Image for both pointer and keyboard selection. Central icon hit targets are large
transparent rounded Images. `Outline` duplicates their triangles four times;
`useGraphicAlpha=false` discards the original transparent alpha and produces
opaque green geometry. The selected object remains selected after pointer exit,
so clearing the image's press tint does not remove this geometry.

Use an independent border-only graphic for keyboard focus, with no triangles in
its centre and no raycast/layout participation. Pointer selection hides keyboard
focus feedback, including when clicking an already keyboard-selected control.
Deselection and disable clear the border and restore any pressed scale. Existing
icon press tint and mission click actions remain unchanged.

## Verification plan

- Reproduce the water mission pointer selection and retained green overlay in an
  isolated synthetic Unity project, with real GPU captures before the product fix.
- Check press, drag outside, release/cancel, keyboard focus and ordinary clicks at
  phone sizes, using the actual mission page factories and pointer handlers.
- Run the related daily mission UI/release compilation regression and centre-pivot
  checks. Keep actual user saves, physical mouse and original Editors untouched.
- Inspect fresh rendered output and record results separately from earlier tasks.

## Evidence so far

- `artifacts/mission-icon-feedback-20261109/before01` is a retained fixture-entry
  failure: the new fixture had not selected its course. It is not reproduction
  evidence. The missing synthetic course setup was added.
- `before02` reproduced the reported opaque rectangle in the actual water
  practice at 360×640 and 390×844. Pointer exit left 48,847 / 60,473 newly dark-green
  interior pixels respectively; both new gesture checks failed while all 17
  existing daily UI checks passed. GPU captures match the reported failure.
- `pivots01` ran the existing `UiPivotValidation.RunBatch` in the isolated cache
  after the product fix: 8/8 groups, 1,790 assertions, zero errors. The run checked
  centre-pivot press/release/disable, transformed layouts and existing theme tests.
  Four explicitly copied source files matched their manifest; the temporary
  synthetic product identity was restored after exit.
- `after01` exposed a missing `CanvasRenderer` dependency on the new keyboard
  border. Add explicit required components before rerunning; the failed report
  and its captures remain diagnostic evidence only.
- `after02` passed all pointer checks and both basic-mission checks. One water
  keyboard comparison incorrectly compared images before/after the existing
  small-screen keyboard-reveal scroll. The fixture now captures its comparison
  baseline after that legitimate layout change; its empty-centre threshold is
  unchanged.
- Final `after03`: Unity 6000.3.24f1 GPU validation and release script compilation
  passed, 21/21 groups, 3,675 assertions, 331 clicks, 866 raycasts, zero errors.
  All 554 copied source files matched. The actual M03 and P01 factories were
  checked at 360×640 and 390×844. Pointer/drag/cancel added zero dark-green interior
  pixels; keyboard focus kept the illustration interior unchanged and drew only
  its border. Normal clicks still advance once and cancelled gestures do not save.
- Inspected fresh M03 cancelled and P01 dragging captures. Original Editors stayed
  running; the physical mouse and real user/device state were not used. Android
  device validation was not run. Changes remain local without commit or push.

## Follow-up: larger illustrations and centred instruction copy

The user requested larger mission icons and centred headings/instructions.
Guided/basic P slides now use a 224-point target instead of 180, scaling the
artwork proportionally inside a centred wrapper. The layout keeps the copy clear
and reserves answer space; constrained views with a storage notice may reduce
the target toward 180 points. The separate wrapper preserves the button's
existing centre-pivot press animation.

Mission names, headings and instruction bodies explicitly use horizontal centre
alignment. The shared label factory, navigation controls and record/input text
keep their existing alignment. Additional-mission artwork sizes are adjusted per
layout to preserve nearby options, input fields, counters and audio controls.
Water practice uses a 300-point picture/target, with footer spacing adjusted for
small phones. No growth, rewards, saved data or mission progression rules change.

`mission-icon-layout-20261109/daily01` passed all 23 existing GPU/UI groups,
including the prior gesture regression, with 3,907 assertions and release script
compilation. Fresh 360×640 and 390×844 P01 and M03 captures were inspected. A later
2-point input-illustration position correction keeps it on the illustration side
of the existing layout's classification boundary.

The initial `guided01`/`basic01` full-slide runs are retained as failures: their
older fixtures skipped the current optional-photo choice, assumed all growth
stayed on one individual, and treated an original schema-1 guided-v3 draft as a
modern save. The validators now use the real photo Skip action, verify the
identified growth receipt and archived/new-individual transition, and exercise
the actual original-save migration. Product policy code was not changed.

Final full-slide checks under `mission-icon-layout-20261109/`:

- `guided02`: 5/5 groups, 5,359 assertions, 346 clicks, zero errors. Covers P01–P07
  at both phone sizes, including choice/help/reflection and storage failures.
- `basic02`: 6/6 groups, 9,629 assertions, 694 clicks, zero errors. Covers all
  P08–P28 steps at both phone sizes plus the actual M01–M17 page/action sequence.
- Both final runs compiled in Unity 6000.3.24f1; all 1,559 copied sources matched
  each manifest. The source identity and original Editor processes were preserved.
- Inspected fresh overview, instruction, answer, review and save-failure captures
  at 360×640 and 390×844. `git diff --check` passed. No physical mouse input, real
  save writes, Android device run, commit or push was performed.
