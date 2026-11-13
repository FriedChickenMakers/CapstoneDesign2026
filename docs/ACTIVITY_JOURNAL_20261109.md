# Activity journal calendar — 2026-11-09

The Records tab's former date-based emotion history is now **활동 일지**. The entry method (`ShowDailyEmotionHistory`), page key (`daily-emotion-history`), and existing detail pages remain compatible. Course-specific history and the plant album remain available.

## Read-only grouping

- Include every existing mission session, including basic/additional, older course/free missions, review, unfinished, paused, ended and completed records.
- Include every daily emotion record, including saved drafts and expired drafts. Display its real status.
- Group by the saved `ActivitySession.InstanceId` / `DailyEmotionRecord.CycleId` when it is a valid calendar day. For older records without a day, convert an available UTC timestamp using the service time zone and the existing 04:00 day boundary. Records with neither remain reachable under “날짜 정보 없는 이전 기록”.
- Link by existing IDs; an emotion's basic/additional references do not create extra mission rows. Plant/photo copies and reward ledgers are not record-list sources, so deleted content is not reconstructed from a snapshot.
- No schema, migration, date assignment, rewards, growth or save API changes. The index stores only strings identifying existing records.

## UI contract

First entry shows the current app day/month. Previous/next month and Today controls update the calendar. A reused garden sprout icon marks each day containing records. Selecting a date updates one date card below the calendar; its emotion and mission rows open the existing detail pages. Empty dates have explicit guidance. Returning from a detail retains the chosen month/day and scroll position. Each rebuild reads a fresh snapshot.

Stable nodes: `ActivityJournal.PreviousMonth`, `NextMonth`, `Today`, `Day.<yyyy-MM-dd>`, `Sprout.<date>`, `SelectedDate`, `DateCard`, `Empty`, `Mission.<sessionId>`, `Scroll`, `Undated`, `Back`; emotion rows retain `DailyEmotion.HistoryRow.<recordId>`.

## Mission entry routes

- From a mission screen, the bottom **기록** tab opens the activity journal directly.
- The mission completion action is now **기록으로 이동** (`Focus.ViewRecord`). It opens the journal with that session's saved activity date and month selected, even after the app day has changed. It uses the same date resolver as the calendar index; undated historical sessions open the undated group.
- Ordinary journal entry still selects the current app day. Opening a mission or emotion detail from a date card and returning restores that date and scroll position.
- The Records hub, course history, plant album and explicit individual-record actions remain available. The journal's **기록으로 돌아가기** action and its active Records tab reach the hub.
- These routes only select presentation state. Existing draft-save guards and debug isolation remain in force; no schema, reward or record mutation is introduced.

Changed route owners: `WeekOneQuestDemo.FigmaShell.cs` (mission tab), `WeekOneQuestDemo.FocusPractice.cs` (completion action), and `WeekOneQuestDemo.ActivityJournal.cs` (shared date-aware entry). The existing pure `ActivityJournal` index remains responsible for date grouping.

## Quieter date card presentation

The emotion entry now has one **감정 기록 · 상태** heading and its summary on the date card's paper, with no nested green box. A small chevron indicates that the full entry still opens its detail. Mission names, basic/additional labels and completion states remain in rounded cards, using low-saturation sage (`#E8EDE5`). The separate **미션 기록** section heading is removed.

Only the journal row factory opts out of the shared selected-card decoration. Calendar selection, outer date cards and the rest of the garden theme are unchanged. Existing button IDs, hit areas, keyboard feedback, callbacks and record data remain intact. This is a presentation-only change in `WeekOneQuestDemo.ActivityJournal.cs`; validation reuses the existing two-resolution click/navigation suite and fresh GPU captures.

Fresh evidence: `artifacts/activity-journal-style-20261109/ui01` passed all 23 existing UI groups (4,087 assertions, 391 clicks, 926 raycasts), including release compilation and journal detail/back navigation. Zero Unity errors; all 558 copied sources matched; all pre-existing Editors remained running. Visually inspected the date card at 360×640 and 390×844: one emotion heading, no emotion box, muted mission cards and no mission section heading. `git diff --check` passed. No new test suite was added for this local styling change. Android hardware was not used; saves and mouse input remained isolated as in the earlier runs.

## Validation

`scripts/test-activity-journal.ps1` compiles only pure C# and synthetic fixtures. Twenty assertions cover record inclusion/deduplication, non-mutation, 04:00/month/year/leap-day boundaries, saved-day precedence, unknown dates, fresh deletion results and 12,000 records without truncation. Pure tests passed on 2026-11-09. Actual Unity compilation and the two-resolution calendar click/detail-return checks are tracked separately by the isolated UI validation run; this document does not claim them passed before that run finishes.

## Final validation results

- `artifacts/activity-journal-20261109/ui01` is retained as a failed diagnostic
  run. The new test clicked the completed-emotion page's explicit “미션으로” action
  while expecting a list return. Correct the test to use “기록 목록”; preserve
  the existing mission-navigation action. Review also found and corrected legacy
  mission-list return routing, stale detail history keys that could remove the
  calendar on re-entry, and incorrect normalized icon sizing that hid the sprout.
- `ui02`: Unity 6000.3.24f1 GPU UI validation and release script compilation
  passed, 23/23 groups, 3,907 assertions, 363 clicks, 898 raycasts, zero errors.
  All 558 copied source files matched. Existing icon-gesture and daily-mission
  regressions remain in this run.
- Calendar checks at 360×640 and 390×844 cover date grouping, month navigation,
  empty days, current day, emotion/modern/legacy mission detail returns, OS Back,
  selected date and scroll retention, and unchanged full state XML/save counts.
  Sprouts are checked for positive bounds, containment, and actual GPU pixels
  using visible/hidden icon comparison rather than component presence alone.
- Visually inspected fresh current-month, selected-day, and detail-reentry
  captures. Pure date/grouping tests remain 20/20 passed. `git diff --check` passed.
- Runs use only synthetic state in a separate hidden Unity project. Original
  Editors and real user/device data are untouched; no physical mouse input,
  commit or push was used. Android device validation was not run.

## Mission route validation — 2026-11-09

- Fresh pure date/grouping run: 20/20 assertions passed.
- `artifacts/mission-journal-route-20261109/ui01` passed 23 groups but is not the final source-matched evidence: a final test-only hub-return assertion was added after copying its sources. It is retained for diagnostics.
- Final `ui02`: 23/23 groups passed, 4,087 assertions, 391 clicks and 926 raycasts; zero Unity errors. Release script compilation passed. All 558 copied files matched the final source; Unity exited 0 and all pre-existing Editors remained running.
- At both 360×640 and 390×844, real pointer clicks verify the mission bottom Records tab, additional/basic carry-over completion actions, an October completion opened in November, the selected date's mission row, record-list and system Back returns, and the existing course/photo hub route. Exact serialized state and save counts remain unchanged throughout navigation.
- Fresh GPU captures include `journal-from-mission-record-tab`, `journal-completion-previous-month`, `journal-basic-carry-completion`, `journal-additional-carry-completion`, selected-day grouped records and hub return. Visually inspected calendar and grouped record captures. `git diff --check` passed.
- Validation uses the same isolated, synthetic, hidden Unity setup; no actual user saves, connected devices or physical mouse input. Android device validation was not run. Changes remain local and uncommitted.
