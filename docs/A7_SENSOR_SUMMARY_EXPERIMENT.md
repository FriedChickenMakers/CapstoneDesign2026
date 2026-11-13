# A7 interval-summary experiment

Purpose: test background **device** sensor capture, independently of Health
Connect, Samsung Health, Watch data, game rewards or user analysis. Published
results below retain elapsed-time coverage and delivery counts only; exact device
timestamps and measured orientation values stay in ignored private artifacts.

## Implementation

The Kotlin foreground service accumulates fixed elapsed-time windows in O(1)
aggregate memory. It writes one JSONL row per completed window on a single disk
executor. Raw events are not appended to disk; the existing bounded transient
debug history and old raw files are preserved. Each summary file is limited to
1 MiB. This is an experiment, not a production retention policy.

Each row contains:

- Linear-acceleration movement episode count: experimental hysteresis thresholds
  0.8 m/s² enter, 0.3 m/s² release, 1 second refractory interval.
- Mean acceleration magnitude, mean linear-acceleration magnitude, and mean
  angular speed in rad/s.
- Mean azimuth, pitch and roll in degrees, calculated as separate circular means
  from rotation-vector orientation; resultant concentrations accompany them.
  These are device orientation averages, not integrated gyro rotation, a full
  quaternion mean, or screen-relative orientation.
- Counts, first/last event times, maximum inter-event gap, cumulative late-sample
  drops, monotonic and wall-clock window labels, and screen state at flush.

Empty windows retain null means and zero counts. Near-zero motion is a valid
stationary result. Late arrivals for already closed windows are counted and
dropped. This implementation does not claim lossless capture; the 250 ms ticker
may close a window before a delayed sensor event arrives. Wall-clock labels are
anchored once to the monotonic start. No wake lock or device-setting change is
used. The 2-second startup warmup permits HOME before measurement starts.

The debug-only bridge accepts explicit run ID, interval and duration. Normal
service start defaults to 10-minute intervals. Duration-limited tests stop the
service automatically. Android non-wake-up sensors may stall during sleep.

## Verification status

UNIT_TESTED: 11 pure Kotlin assertions cover boundaries, stationary input,
hysteresis, circular averaging, late events, empty windows and simulated
10-minute windows. The simulation is not device evidence.

BUILD_VERIFIED: Unity Android APK built successfully at
`artifacts/a7-summary-20260923/build/capstone-mockup.apk`. The installed APK hash
is recorded in `device-verified/device.json` under that artifact directory.

DEVICE_TESTED_A7: SM-T500, Android 10, install-r preserving app data. The
5-second × 12-window test passed all runner checks: exact windows, sample
presence, at least 80% first-to-last event span in each window, unchanged raw
file signature, no write error, and service termination. The app was sent HOME;
screen was ON at the end. This does not establish screen-off continuity.

Short-run totals: 0 movement episodes; 308 accelerometer, 289 linear acceleration,
300 gyroscope and 277 orientation samples contributed to the summaries. The
maximum recorded inter-event gap was 687 ms. Cumulative late arrivals dropped
were 35, so passing coverage checks must not be interpreted as lossless delivery.
Per-window mean orientation remained nearly constant on the stationary tablet.
The 80% span check is a coarse coverage check, not proof that every point inside
that span was observed.

DEVICE_TESTED_A7: The 10-minute × 6-window test completed and passed all runner
checks over one hour. The service stopped 209 ms after the final window boundary,
without a write error. A subsequent
`dumpsys activity services` for this package also returned no running service.

All six 10-minute windows had orientation samples; the stationary tablet showed
no movement episodes and nearly constant mean orientation.

The six on-device JSONL rows occupy **5,902 bytes** (metadata and termination
files excluded). They aggregate 18,076 accelerometer, 18,070 linear acceleration,
18,076 gyro and 18,064 orientation events. Every window has an observed
first-to-last event span of at least 599.193 seconds out of 600; the maximum
recorded inter-event gap is 598 ms. Cumulative late events dropped: 23.
The raw file signature remained unchanged; the old raw file still had its
2026-09-22 modification date and 1,565,820-byte size after this experiment.

**Screen-off continuity remains NOT_TESTED by this run.** Despite the existing
30-minute timeout setting, every flush reported `screenInteractiveAtFlush=true`
and the end power check was ON. The cause was not investigated by changing
settings. HOME was sent before measurement; this establishes background app
capture under the observed conditions, not sleep/Doze capture. No screen setting
or wake-lock policy was changed. The runner slept on the host with no periodic
ADB polling during capture. Do not infer battery cost or other devices' behavior
from this stationary A7 experiment.

Evidence: `artifacts/a7-summary-20260923/device-verified/` contains metadata,
summary rows, termination markers, result checks and current `status.json`.
An earlier metadata-read attempt failed in the host runner before HOME and is
excluded from background-test evidence; its diagnostic log is preserved.

## Repeat

```bash
./scripts/test-sensor-summary-unit.sh
python3 -u scripts/test-a7-sensor-summary.py \
  --serial AUTHORIZED_A7_SERIAL \
  --apk artifacts/a7-summary-20260923/build/capstone-mockup.apk \
  --output artifacts/a7-summary-NEW_RUN/device
```

The runner gates the hour on a passing minute, requires SM-T500, and never clears
app data. Choose a fresh output directory. Do not run a Unity build concurrently:
the current editor shutdown disrupts the shared ADB server.

## Required screen-off follow-up — 2026-09-24

The user clarified that the intended test is long-duration **screen-off** capture.
The prior screen-on experiment does not satisfy that condition. Run the command
above with `--screen-off` and a fresh output directory. This mode explicitly sends
KEYCODE_SLEEP after HOME and requires OFF before the first measurement window,
OFF at every summary flush, and OFF at the end. It stops an invalid startup rather
than silently proceeding with an ON screen. Existing timeout/charging settings
are not changed. The 1-minute gate is also run screen-off.

Connectivity recovered and the installed APK SHA-256 matched the retained build.
The `--skip-install` option verifies this hash before using the existing install.
The first retry failed during reinstall; a second retry verified OFF 4 ms after
measurement start and was stopped as invalid. Neither is counted as evidence.
Removing a redundant metadata read allowed the third retry to meet the strict
OFF-before-start check without changing the APK or device settings.

**DEVICE_TESTED_A7 — screen-off test completed.** The screen-off 5-second × 12
test passed all checks, including OFF before start, at every flush, and at the
end. The subsequent 10-minute × 6 test completed over exactly 3,600,000 ms.
Screen OFF was verified 442 ms
before the first window; all six summary rows report non-interactive screen
state, and the retrieval-time display remained OFF (`mWakefulness=Dozing`).
No input or periodic ADB polling was sent during the hour.

All six 10-minute windows had orientation samples and reported the screen OFF
at flush. The stationary tablet showed no movement episodes and nearly constant
mean orientation.

The six summary rows occupy **5,895 bytes** on device. They summarize 17,999
accelerometer, 22,585 linear acceleration, 18,000 gyroscope and 22,579 orientation
events. Minimum observed first-to-last span per window: 599.572 seconds.
Maximum recorded inter-event gap: 597 ms; cumulative late arrivals dropped: 12.
All flush timestamps are within 209 ms of their scheduled boundary; termination
was 221 ms after the final boundary. No write error and no remaining app service
were observed. Stationary movement count zero and small angle variation are normal.

The Wi-Fi ADB transport disappeared during the host wait. The runner exited at
result retrieval, before reading device files. A single reconnect succeeded;
**the same run** was recovered without restarting the app, waking the screen or
repeating the experiment. On-device timestamps show that summaries and automatic
termination completed before this reconnect. `hour-result.json` is the separate
post-reconnect audit, not a claim that the initial runner exited successfully.
The runner now performs one bounded reconnect at result retrieval and saves the
starting raw-file signature for future recovery.

The short run's raw-file hash comparison passed. The hour's in-memory starting
hash was lost when the original runner exited, so its result instead records
verification of the historical raw file's unchanged 1,565,820-byte size and
2026-09-22 modification date. Do not describe the hour as a recovered before/after
hash comparison. Existing raw files were preserved; no new raw persistence was
introduced.

Evidence directory: `artifacts/a7-screen-off-20260924/retry-3/`. See
`hour-screen-start.json`, `hour-screen-end.json`, `hour-meta.json`,
`hour-windows.json`, `hour-finished.json`, `hour-result.json`, `hour-audit.json`
and `retrieval-evidence.txt`. The failed host retrieval remains in `retry-3.log`.

This confirms one hour of screen-off capture on this A7 under the observed
conditions. Screen state was sampled before/after and at each flush; this is not
an independently recorded continuous screen-transition trace. It does not prove
lossless delivery, battery performance, Android deep-idle behavior or behavior on
other devices. No wake lock or timeout/charging-setting change was added.

Orientation semantics: [Android position sensors](https://developer.android.com/develop/sensors-and-location/sensors/sensors_position).
Sleep limitations: [Android Sensor reference](https://developer.android.com/reference/android/hardware/Sensor).

## Recent-app dismissal before the fix — 2026-09-24

DEVICE_TESTED_A7, current installed APK. This is different from HOME/background.
Per user clarification, force-stop behavior was not a test scenario. An initial
setup reset preceded the clarification; the actual dismissal tests use only a
swipe on this app's card in Samsung Recents, never Close all.

**Result: current APK did not continue recording after removal from Recents.**
The task's absence was verified using ActivityManager recents. In the first
attempt the process died before the first 5-second row was persisted; no rows
were recovered 65 seconds later. In a second attempt, recording ran for about
30 seconds first. Five rows existed before opening Recents; a sixth completed
during the dismissal procedure. All six remained readable from app-private
storage without relaunching the app, but no completed window started after
dismissal during the subsequent 65-second observation.

ActivityManager logs show the foreground-service process dying at removal and
Android attempting a service restart. The first restarted service was stopped
due to app idle. In the second attempt a foreground service record reappeared,
but there were no new sensor-registration logs or summary rows. A notification,
PID or service record alone must not be treated as active sensor collection.
The manifest already has `stopWithTask=false`; this was insufficient on the
current Unity/service process arrangement. The exact cause of process death has
not been isolated to Unity or the OEM policy by this test.

Previously completed summaries are recoverable. This does **not** demonstrate
intermittent *new* capture after dismissal, nor retroactive recovery of missed
motion/orientation events. The one-hour dismissal run was not started because
the short persistence gate failed. Earlier one-hour HOME/screen-off results do
not establish dismissal behavior.

Evidence: `artifacts/a7-app-closed-20260924/result.json`,
`recents-short-removal.json`, `recents-short-after.json`,
`recents-retained-before.json`, `recents-retained-removal.json`,
`recents-retained-windows.json`, `recents-retained-after.json` and
`lifecycle-log.txt`. The runner scripts in that directory retain the exact
device-specific swipe procedure. Source/APK behavior was not changed for these
tests. A follow-up implementation should separate the collector lifecycle from
Unity and explicitly restore experiment configuration on supported restarts,
then repeat the Recents test. The following section records that follow-up.

## Recents removal fix and retest — 2026-09-25/26

**IMPLEMENTED / DEVICE_TESTED_A7.** `SensorForegroundService` now declares
`android:process=":sensor"` in the library manifest. It remains non-exported,
`stopWithTask=false`, and a foreground service with a visible notification.
This gives the Kotlin collector an app-private process independent of the Unity
Activity process. The exact APK was inspected with `aapt` to verify the merged
manifest, built successfully, and installed with `install -r` (preserving app
data). The installed APK hash was checked again before the final test.

On SM-T500, the runner opened the app once per test phase with the development
summary experiment selected, removed **only our app card** from Samsung Recents,
verified the task was absent, then waited without periodic device queries. The
service was observed in a separate PID immediately before and after task removal.
The 5-second × 1-minute gate passed: 12 rows total, with 11 full windows starting
after removal. Those post-removal rows all contained acceleration, gyro and
orientation samples and were flushed while the screen was OFF.

The 10-minute × 1-hour run also passed. The task was removed 1.650 seconds after
measurement start. Six 10-minute rows
were saved; windows 1–5 began wholly after removal, covering 50 complete
minutes. The remainder of window 0 after removal extends the observed period to
about **59 minutes 58 seconds**. Every window had sensor events spanning almost
the entire interval. The five wholly post-removal windows contained 15,004
accelerometer, 18,765 linear acceleration, 15,004 gyro and 18,760 orientation
samples. Movement episodes were 0 on the stationary tablet, and mean orientation
remained nearly constant across all six rows. The six rows occupied 5,929 bytes
on device.

Each flush reported the screen non-interactive, the final display check was OFF,
and the service terminated automatically without a write error. The raw-sample
file hash was unchanged. Cumulative late arrivals dropped were 21, so this is
continuous observed delivery, not a lossless guarantee. The test did not
exercise a later Android process kill or restart of the sensor process.

An initial 1-hour run confirmed all six rows after Recents removal but failed
the screen-OFF check: the A7 was AC powered and its developer "stay awake while
charging" value was 7, so the display became interactive after the first row.
For the final repeat, a wrapper saved that value, temporarily set it to 0,
and restored **7** at process exit. The restored value was verified with ADB.
No app permission, raw retention policy, or screen timeout was changed.

Evidence: `artifacts/a7-recents-isolation-20260925/build/` (APK),
`device-screen-off/` (metadata, task removal, 12/6 rows, results, audit,
termination markers and raw signatures), and `device-screen-off.log`.
Reproduce on the authorized A7 with:

```bash
scripts/run-a7-recents-screen-off.sh AUTHORIZED_A7_SERIAL \
  artifacts/a7-recents-isolation-20260925/build/capstone-mockup.apk \
  artifacts/a7-recents-isolation-NEW_RUN/device
```

The Unity-side `SensorRepository` is process-local; its transient live status
and latest raw values do not mirror the independent service process. The
app-private summary files are the authoritative result of this experiment.
Cross-process UI status and restart recovery are separate follow-up work.

Android's [service manifest reference](https://developer.android.com/guide/topics/manifest/service-element)
defines the private `:sensor` process and `stopWithTask` behavior; the
[services overview](https://developer.android.com/develop/background-work/services)
explains that services use the app process unless a process is specified.
