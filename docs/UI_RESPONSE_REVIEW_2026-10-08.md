# UI response and navigation review — 2026-10-08

The Home, Activities and Settings tabs now retain one Home-owned TextMeshPro
brand in a fixed header. Activity pages retain their scrollable content below
that header. Vector icons fit a centered square inside their cells, so the tall
roadmap rows no longer stretch the lock, checkmark or arrow.

Android Back follows the activity page history, then the previously visited tab,
then Home. Back at the Home root backgrounds the task. Returning through a page
restores its main scroll position; completed forms cannot be reopened as editable
history entries. Practice drafts are saved before leaving, and a failed save
keeps the user on the form. Purchase previews are canceled without a transaction.

## Method

Only the Galaxy Tab A7 was connected for this review. Existing private records,
preferences and external garden saves were backed up before the in-place update.
The app's data and logcat were not cleared. Raw captures, profiler exports and
restoration journals are retained only under ignored `artifacts/`.

The development APK supports an explicit `capstoneProfile=true` launch extra.
`UiPerformanceProbe` records up to 120 seconds of frame intervals, available
Unity counters and named operation durations. It is not instantiated on normal
launches or in non-development players. No sensor values, notes or device
identifiers are included in the probe report. Counters are sampled for completed
frames and unavailable metrics are explicit; nested operation durations must not
be added together. Main Thread and PlayerLoop include waits, so these are not
pure CPU busy time.

A repeated read-only sequence opened Home, Activities, the roadmap, free
activities and Settings, scrolled Settings and used Android Back to return to
the launcher. There were three additional Home/Activities tab cycles. Frame interval
analysis excludes the first three seconds and intervals of at least one second.
Operation statistics include all captured scope calls, including startup calls.
This is an instrumented single-device comparison, not a touch-to-photon or
battery-life benchmark. APK installation, startup and automation wait time are
not counted as screen construction time.

## Initial measurements

The pre-change APK's SurfaceFlinger presentation intervals had a median of
33.33 ms on all three tabs. The instrumented 30 fps build captured 1,986 frames
and 222 operations, including 442 post-startup Settings frames.

| Operation | Samples | Median | Maximum |
| --- | ---: | ---: | ---: |
| Tab switch (`Navigation.SetActivePanel`) | 10 | 43.18 ms | 79.78 ms |
| Roadmap construction | 1 | 61.37 ms | 61.37 ms |
| Settings refresh | 30 | 11.68 ms | 20.07 ms |
| Android snapshot retrieval | 30 | 7.51 ms | 15.77 ms |
| Detached garden state snapshot | 116 | 1.62 ms | 3.56 ms |
| Daily-cycle refresh | 17 | 3.65 ms | 5.78 ms |

The idle display cadence was capped at 30 fps. Screen creation and activation
also caused isolated longer frames. Settings rebuilt hidden diagnostics every
half second, including detached garden state copies, even while diagnostics
were collapsed. These are separate contributors; raising the frame cap alone
does not remove screen-construction stalls.

The UI.Layout and Canvas.SendWillRenderCanvases counters were unavailable in
this player. Rendering/layout subphase attribution cannot be inferred from
those missing counters. The available BehaviourUpdate counter and explicit
operation timings establish synchronous UI work but not exact GPU cost.

## Changes and verification

The foreground target is now 60 fps in both the runtime default and serialized
scene. The Android sensor service remains independent of the foreground frame
rate. Collapsed diagnostics skip their text/debug controls and state copies;
visible Settings summaries retain their refresh cadence, and expanding
Diagnostics refreshes its content immediately.

On A7, Vision review and pixel measurements found the brand identical across
nine app screenshots, including the scrolled Settings page and Back navigation
states: the ink bounds were 188 × 42 px at the same location and the header
region hashes matched exactly. The first roadmap lock changed from 37 × 104 px
to 37 × 52 px, retaining its center and width. Device screenshots verified
roadmap/free activities → Activities, Settings → Activities, Activities → Home,
and Home → launcher.

The Android keyboard dismissal path also preserves the final native text before
UGUI's cancellation rollback. The first Back can close the keyboard without
leaving the form; navigation remains a subsequent action. Read-only fields and
deliberate desktop Escape cancellation retain their existing behavior.

Automated checks passed: 26 Unity integration scenarios, 129 EditMode tests,
324 local-state assertions and 683 content assertions. Play-mode checks passed
both the fresh and process-restart phases using an isolated synthetic save.
A three-size capture sweep generated 114 images with zero detected text
overflows. The sizes cover tall phones, shorter phones and a landscape tablet.

An intermediate 60 fps capture isolated an additional rendering problem:
Home reached a 16.67 ms median interval, but Activities and Settings remained
near 33.3 ms and their presentation-wait counter increased. Inspection found
that leaving Home re-enabled the legacy scene camera and light behind opaque
UI. The integrated UI now keeps both disabled on every tab; its garden and
purchase previews use their existing on-demand garden camera. The standalone
legacy fallback remains available. This finding is supported by the subsequent
same-sequence measurement, rather than interpreting presentation wait as pure
GPU execution time.

On the physical A7 keyboard, a synthetic practice draft survived the first
Android Back, which closed only the keyboard. The second Back returned to
Activities. The final draft was also present in the mock save. This check used a
new, isolated mock directory: it was archived and removed afterward, and the
LIVE save was byte-identical before and after the check.

## Final measured result and remaining cost

The final capture contains 3,277 frames and 162 operations, with no dropped
operations. Medians use the mean of the two central samples for even counts.
The table below uses frame intervals after the startup exclusion; the Activity
root is the probe's `home` route and Home/Garden is its `garden` route.

| Screen | Initial median / mean | Final median / mean | Final p95 |
| --- | ---: | ---: | ---: |
| Home / Garden | 33.38 / 33.84 ms | 16.68 / 17.27 ms | 17.07 ms |
| Activities root | 33.36 / 34.05 ms | 16.72 / 22.78 ms | 33.52 ms |
| Roadmap | 33.35 / 34.27 ms | 16.73 / 22.57 ms | 33.54 ms |
| Settings | 33.34 / 33.60 ms | 16.70 / 22.48 ms | 33.47 ms |

Settings refresh median fell from **11.676 to 8.062 ms (31%)**, with 30 samples
in each capture. State-copy calls across the same sequence fell from 116 to 56.
The native snapshot portion remained about 8 ms; this change removes hidden
UI work and does not make the Android health/status query itself faster.

Tab-switch operation median remained **43.18 → 43.79 ms** (10 samples each).
Roadmap construction took 57.44 ms in the final single observation. Rebuilding
and activating UI synchronously still exceeds a 16.7 ms frame budget. About a
third of Activities/Settings intervals exceeded 20 ms, and the final Settings
maximum was 166 ms. These results do **not** establish sustained 60 fps or a
reduction in every navigation delay. Page construction/activation and the native
snapshot query remain the measured targets for further performance work.

A separate normal launch, with the probe disabled, confirmed actual
SurfaceFlinger presentation intervals (125 intervals per tab): Home median
16.68 ms / p95 17.15 ms, Activities 16.75 / 33.60 ms, Settings 16.70 / 33.43 ms.
The installed APK hash matched the final local build. Higher foreground frame
rate can change power consumption; long-duration thermal and battery behavior
were not benchmarked in this review.

The final device check also rendered the garden placement preview correctly
with the legacy camera disabled. Back canceled the preview, with no change to
the LIVE save. The app was returned to its normal LIVE Home screen with profiling
disabled. Original screen timeout, brightness and brightness mode were restored
and individually verified against their durable restoration journals.
