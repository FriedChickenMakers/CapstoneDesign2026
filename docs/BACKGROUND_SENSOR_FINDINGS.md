# Background Sensor Findings

## Current experiment — 2026-09-23

The interval-summary experiment supersedes the raw-persistence implementation
described below. New raw events stay in bounded transient memory; they are not
appended to `sensor_samples.jsonl`. Existing files are preserved. The current
service registers accelerometer, linear acceleration, gyroscope and rotation
vector, and persists interval aggregates. See
[A7 summary experiment](A7_SENSOR_SUMMARY_EXPERIMENT.md) for current evidence.
The older `test-platform-background.sh` checks raw-file advancement and is not a
valid test of this new summary mode. Earlier results below remain historical.

### Recents removal follow-up — 2026-09-25/26

The foreground sensor service now runs in app-private `:sensor`, separate from
the Unity Activity process. A7 verified that 5-second summaries continued after
our app card was removed from Recents; the 10-minute × 1-hour screen-off repeat
then saved six rows, including five complete windows entirely after removal.
The raw file did not grow and the service stopped at its configured duration.
See the latest section of [A7 summary experiment](A7_SENSOR_SUMMARY_EXPERIMENT.md)
for the exact gate, evidence and limitations.

## Implemented

- Enumerates all `SensorManager.TYPE_ALL` sensors and logs name, vendor, type,
  version, resolution, maximum range, power, and minimum delay.
- Requests Accelerometer, Gyroscope, Gravity, Linear Acceleration, Magnetic
  Field, Light, Pressure, and Step Counter when present.
- Collects from a Kotlin foreground service on a dedicated handler thread.
- Uses a visible low-importance notification and Stop action.
- Persists a rotating application-private JSONL buffer and reloads its tail when
  the Unity process returns.
- Missing hardware and Activity Recognition permission are status values, not
  numeric zero or exceptions.
- Listener and sensor handler are released on stop; queued writes drain asynchronously.

## Build verification

The service, health foreground-service type, required permissions, and Kotlin
classes are present in the built target-36 ARM64 APK. Unity project validation
and the full Gradle build pass.

## Hardware procedure

After ADB authorization and APK installation:

```bash
ADB_SERIAL=<serial> \
CAPSTONE_ARTIFACTS=/tmp/capstone-platform-build \
./scripts/test-platform-background.sh
```

The script launches LIVE mode, taps the generated Start button, records the
latest persisted timestamp in foreground, after Home/background, and after a
screen-off interval, then returns to Unity. It writes a JSON report and logcat.
Button coordinates and wait periods are environment variables for different
screen sizes. Permission grants now default OFF; use the normal app consent flow first.
`PLATFORM_AUTO_GRANT_PERMISSIONS=1` is an explicit test-only opt-in.

## Galaxy Tab A7 result — 2026-09-22

The current APK ran on an authorized SM-T500 (Android 10 / API 29). The service
registered Accelerometer, Gyroscope, Gravity, Linear Acceleration, Magnetic
Field, Light, and Step Counter; Pressure reported `UNSUPPORTED`. Persisted
timestamps advanced in foreground, after Home, and during a 10-second
screen-off interval. Returning to Unity showed the running service, live values,
and 2,851 persisted samples. Stopping removed the service and timestamps then
remained stable. No crash, unhandled exception, persistence failure, or retry
loop appeared in the captured logcat. The in-app permission action also opened
Android's physical-activity Allow/Deny dialog; the debug panel reported the
denied state without interrupting the Garden.

Evidence is stored under `/tmp/capstone-platform-build/platform/` and
`/tmp/capstone-platform-build/a7/` on the worker. S24 behavior is still pending.

## Production follow-up

The original POC held a partial wake lock; the overnight change removes it.
The service still samples every supported sensor at normal delay. A production design should reduce the active sensor set, batch where
supported, define retention/consent policy, measure battery use, and test OEM
background restrictions.

## Overnight hardening — 2026-09-23

IMPLEMENTED / UNIT_TESTED storage seam; new hardware behavior NOT_TESTED.
The earlier A7 observations above are historical, not rerun overnight.

- Removed unconditional wake lock and its manifest permission. Foreground service
  priority does not guarantee delivery of non-wake-up sensors during sleep.
- Repeated Start still only calls listener registration in service `onCreate`.
  STOP marks callbacks inactive before unregistering and quitting the handler;
  late queued callbacks are ignored. Listener lifecycle needs a device test.
- Callback does no file I/O: it copies samples into a 512-entry bounded pending
  buffer; a single asynchronous writer drains it outside the snapshot lock.
  Excess pending samples increment `droppedPendingSamples`. Memory history is
  independently limited to 512. This is code review, not a throughput benchmark.
- Writer closes each append and its idle executor thread expires after 30 seconds.
  Stop schedules remaining writes without blocking the main thread; abrupt process
  death before drain can lose buffered samples. No lossless/crash-durable claim.
- Each current/previous JSONL generation is capped at 4 MiB before appending;
  rotation/delete failures stop persistence with an explicit error and preserve
  the current log. Streaming tail reload tolerates malformed individual JSON
  lines. An unterminated tail gets a newline before new records are appended.
- Samples include `captureSessionId` to distinguish service sessions/reboots;
  legacy records use `legacy-unknown`. Event nanoseconds remain monotonic sensor
  time, separate from receipt wall-clock milliseconds. A UUID identifies capture
  sessions, not an exact boot counter. Snapshot reports sensor reporting mode,
  wake-up capability, observed receipt interval and malformed-tail/drop counts.
- On-change sensors are not marked STALE merely because the value was unchanged
  for 60 seconds. Continuous sensor receipt age still uses that DEV_DEFAULT.
- `persistedSampleCount` now counts successful writes in the current process;
  it is not the number of records still retained after rotation.
- Log read/write errors use generic messages and do not print sample contents.

Four pure-JVM file tests cover rotation/bounds, truncated-tail separation,
rotation failure preservation and oversized-batch rejection, through
`./scripts/test-health-query.sh` (20 total health+storage cases). Queue saturation,
real disk-full handling, listener restart, battery use, service death, current
OEM behavior and settings OFF remain device/integration follow-ups. An in-app
history deletion action is still pending; this change does not claim one.

Long-duration collection commands now require an explicit serial, default to no
ADB permission grants, and disable log capture by default. They never clear global
logcat or save the serial in the report. Inspect coordinates before use:

```bash
./scripts/test-platform-background.sh --serial AUTHORIZED_A7 --screen-off-seconds 300
./scripts/test-platform-background.sh --serial AUTHORIZED_A7 --screen-off-seconds 1800
./scripts/test-platform-background.sh --serial AUTHORIZED_A7 --screen-off-seconds 7200
```

No device was available for these runs. Screen-off timestamp advancement is a
separate observation from foreground/background lifecycle success, not proof of
all-sensor continuity. [Android Sensor reference](https://developer.android.com/reference/android/hardware/Sensor)
describes wake-up and reporting-mode differences.
