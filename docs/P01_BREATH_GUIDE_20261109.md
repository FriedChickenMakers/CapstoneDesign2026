# P01 breathing guide presentation

## Scope and behavior

User request: make each P mission's guidance visually meaningful, starting with P01. Preserve the current mission name, central click action, practice pages, optional choices/reflection, session versions, 1-minute recommendation, completion, growth and coin rules. The subsequent help revision consolidates its three pages into one. This change only covers the current guided P01 presentation. Older versions and P02–P28 continue to use their existing presentation.

The user can read a short instruction, see the corresponding focus on a calm person illustration, and advance at their own pace. There is no paced breathing animation or sensor measurement. The seated figure is an example; preparation explicitly permits sitting or lying down and eyes open or closed.

## Page storyboard

| Existing page | Instruction purpose | Visual state | User action |
|---|---|---|---|
| Overview | Briefly explain attending to one place where breathing is felt | Large, softly shaded person and light contextual marks | Central illustration opens preparation |
| Preparation | Find a comfortable position; eyes may be open or closed | Person, cushion and posture/contact cues | Central illustration opens the existing choice |
| Choice | Choose nose, chest or abdomen; choice remains optional | Three subtle location markers | Choose an existing button or continue without choosing |
| Guidance 1 | Notice temperature/contact at the nose, or movement at chest/abdomen | The saved choice controls the highlighted body location | Central illustration advances |
| Guidance 2 | Leave breathing depth and speed natural | Same focus location and quiet observation marks | Central illustration advances |
| Guidance 3 | Notice wandering attention and return without judging | Small thought cloud and return line towards the chosen focus | Central illustration starts practice through the existing durable command |
| Practice | Stay for a comfortable duration; unclear sensation is acceptable | Fewer decorations, selected focus remains visible | Existing pause, optional reflection and completion actions |
| Optional reflection | Notice what was felt; writing/choosing is optional | Small note card beside the same person | Existing reflection choices/input and completion |
| Optional help | Natural pace; another clear body area; rest if uncomfortable, summarized in three short lines | One illustration with linked focus markers | Central click or Previous returns immediately to the prior page |
| Completion | Show the completed practice using the same visual identity | Person with note motif and saved focus | Existing mission, garden and journal actions |

## Presentation contracts

- Overview title 30, instruction 22 and mission context 16 in the existing 390-unit design canvas. The subsequent concise-copy revision removes the title and repeated mission context from all later guidance pages, leaving only the centered instruction; see `P01_P02_CONCISE_GUIDANCE_20261109.md`. Text bounds use their actual wrapped height; small screens and save notices reserve the existing answer area.
- Central hit target stays centered and square (up to 280 after the emphasis adjustment below; down to 160 on constrained screens). Illustration and effects never intercept pointer input. Drag cancellation and keyboard feedback retain the existing behavior.
- Theme moves subtly from warm sage preparation to cool sage observation and warm reflection/help. Top progress marks match the adjacent instruction number (1/3, 2/3, 3/3). Single-page help has no page counter.
- Vector paths are authored in the repository and rendered by a single non-raycast `MaskableGraphic`. No downloaded assets, cameras, textures, perpetual animation loop or separate save state. The optional one-shot entrance fade reuses the existing reduced-motion-aware implementation.
- `P01BreathGuide` owns display copy; the presenter maps its existing slide/choice to `P01BreathIllustration.Stage`; the illustration only paints geometry. Domain policies remain untouched.

## Verification plan

Use the isolated synthetic Unity project with no physical mouse or device/user save access. Guided UI validation covers all P01 stages at 320×568, 360×640 and 390×844, all three focus choices, actual visible geometry differences, body/illustration/controls separation, pointer cancellation, reduced motion, save failure/retry, optional reflection, resume and reward idempotency. Retain the existing P02–P07 and legacy checks, then run the existing daily suite for mission completion/journal routing and release compilation. Visually inspect fresh GPU captures before reporting success.

No commit or push is authorized. Android hardware verification is outside this run.

## Fresh verification results

- Final `artifacts/p01-breath-20261109/ui03/renders/report.json`: 8/8 guided groups, 7,600 assertions, 415 clicks, 421 raycasts and zero Unity errors. All 1,563 copied files matched; isolated Unity exited 0. Includes P01 at 320×568, 360×640 and 390×844, P02–P07 regression and old-session compatibility.
- Final `artifacts/p01-breath-20261109/daily02/report.json`: 23/23 daily groups, 4,087 assertions, 391 clicks, 926 raycasts and zero Unity errors. Release script compilation passed. All 562 copied files matched; isolated Unity exited 0. Completion, reward idempotency, activity-journal routing and previous icon-drag regressions remain covered.
- Earlier successful `ui01`, `ui02` and `daily01` runs are retained. Review prompted two small presentation refinements before the final runs: an unchosen focus now shows the three possible locations without directing the return line to the chest, and progress marks match their adjacent numbered guide/help label. Added actual no-choice navigation and GPU comparison to the existing P01 checks.
- Inspected fresh GPU captures for overview, nose selection, chosen/unchosen return guidance, actual practice and 320×568 choice-save failure. Text, fixed-center illustration, action hint, controls and error notice remain separate. Nose/chest/abdomen differences are verified using actual rendered pixels, not only component configuration. `git diff --check` passed.
- Original Unity Editors remained running; only synthetic state and separate validation preferences were used. No actual user save, connected device or physical mouse input was used. No Android performance claim is made. Changes remain local and uncommitted.

Main implementation: `WeekOneQuestDemo.P01Breath.cs`, `P01BreathIllustration.cs`, P01-only integration in `WeekOneQuestDemo.GuidedFocusPractice.cs` and `WeekOneQuestDemo.FocusPractice.cs`, optional measured text layout in `FocusMobileLayout.cs`. Existing guided verification is extended in `GuidedFocusValidation.cs`.

## Larger illustration and clearer cues

Follow-up request: the illustration and in-between cue changes were still too subtle.

- P01's maximum illustration target increases from 224 to 280 design units. The larger-reading layout starts at least 254 units below the top, while retaining 18 units below the measured body and 86 units for answers. At 360×640 the normal target is approximately 241; save notices still reserve their own space. Other P missions retain their existing sizes.
- Trim empty vector margins from a 256-unit view to 232 units. Together with the larger target, the person is approximately 38% larger on 390×844. All paths and their antialias fringes stay inside the hit target.
- Add a clear green outline around the selected body area; retain a smaller radius around the nose to preserve the face. Enlarge/darken the thought cloud and make the return line 3.2 units thick with an explicit arrowhead. Strengthen posture marks, airflow lines, location guides and reflection-card strokes.
- Practice remains visually quieter and motion remains unpaced. Skipping a choice still leaves the focus unset and does not draw a directed return arrow. No instruction, choice, reward or persistence changes.
- Reuse the existing verification suite, adapting only P01's responsive size expectation. Fresh results and captures are in `artifacts/p01-emphasis-20261109`.
- `ui01`: 8/8 guided groups, 7,600 assertions, 415 clicks and 421 raycasts; zero errors and 1,563 matching sources. `daily01`: 23/23 groups, 4,087 assertions, 391 clicks and 926 raycasts; zero errors, release compilation passed and 562 matching sources. Both isolated processes exited 0 and preserved the original Editors.
- Visually checked the stronger return cue at 390×844, nose cue at 360×640 and choice-save failure at 320×568. No clipping or overlap was observed. `git diff --check` passed. No physical mouse, actual user save or Android device was used; no commit or push.

## Lower placement, linked markers and single-page help

The user clarified that **도움말** and its action should remain; the proposed **처음으로** rename is cancelled. P01 help now contains just three short lines: natural breathing, observing another clearer place, and opening the eyes or resting if uncomfortable. One central click returns to the prior page. Other missions' help remains unchanged.

The P01 illustration is placed lower with a 14–32 design-unit responsive offset. The measured text gap and existing answer/error space still constrain its size on short screens. The larger-art treatment remains.

Body dots now belong to actual leader/return lines. Choice/help leader lines show their endpoints; selected observation and return-arrow pages show the relevant point. Overview, preparation, unchosen notice/return, ordinary observation, practice and reflection do not show floating dots. The softer focus ring remains independent. This changes no saved selection, elapsed time or rewards.

Fresh verification for this revision (not the earlier emphasis run):

- `artifacts/p01-help-layout-20261109/ui01/renders/report.json`: 8/8 guided groups, 7,519 assertions, 405 clicks, 411 raycasts and zero Unity errors. All 1,563 copied sources matched. Checks cover single-page help returning to the same practice, paused elapsed time and unchanged serialized state/rewards; rendered endpoint pixels across stages; responsive placement; save failure/retry; and P02–P07 regression.
- `artifacts/p01-help-layout-20261109/daily01/report.json`: 23/23 daily groups, 4,087 assertions, 391 clicks, 926 raycasts and zero Unity errors. Release script compilation passed and all 562 copied sources matched. Both isolated Unity processes exited 0 and preserved the user's Editors.
- Visually inspected fresh 390×844 overview and single-page help captures, plus the 360×640 help save-failure capture. Text, illustration, actions and error notice remain separate; the overview has no body endpoint dots. Automated geometry/render checks also cover 320×568.
- `git diff --check` passed. Synthetic saves only; no physical mouse, device data, Android hardware test, commit or push.

## Back navigation focus decoration regression

Reported issue: after choosing a body area, returning to earlier pages leaves some focus decoration on the person. The renderer clears its mesh, but `DrawAttention` previously drew the saved selection's halo and ring regardless of the current page. Consequently, returning to preparation or overview drew them again.

The saved focus choice must survive back/forward navigation, while overview and preparation must look identical to their original unselected rendering. Limit focus decoration at the illustration layer; keep the presenter's choice, draft persistence, timing and rewards unchanged. The regression test follows actual previous/next controls for nose, chest and abdomen, comparing the rendered central illustration to the first visit and checking that advancing restores the chosen guidance. Existing per-stage endpoint and help/save-failure tests remain in place. Use the isolated synthetic Unity fixture; no user save or physical pointer input.

- Reproduced before the fix in `artifacts/p01-back-navigation-20261109/red01/renders/report.json`: all three P01 screen-size groups failed the new check. The returned preparation and overview pictures contained 1,019–3,749 changed pixels from the inappropriate focus ring/halo. The other five guided groups passed; the preceding cue-to-cue comparisons had zero changed pixels.
- Fixed `P01BreathIllustration.DrawAttention` to skip focus decoration on overview and preparation without clearing the saved choice. `green01/renders/report.json` then passed 8/8 groups, 10,339 assertions, 513 clicks and 519 raycasts with zero Unity errors. All nine combinations of body choice and screen size returned to preparation/overview with **zero changed illustration pixels**. Draft state and save count stayed unchanged, and advancing restored the chosen cue exactly. All 1,563 source files matched; Unity exited 0.
- Visually checked fresh returned preparation at 390×844 and overview at 320×568. The unintended ring/halo is absent; stage-specific posture/breath cues remain. No physical mouse or actual user save was used.
- Fresh `daily01/report.json`: 23/23 groups, 4,087 assertions, 391 clicks and 926 raycasts; zero errors, release script compilation passed and all 562 sources matched. Original Unity Editors remained running. `git diff --check` passed. Android hardware was not tested; no commit or push.
