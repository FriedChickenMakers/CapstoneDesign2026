# Android Platform Architecture

## Scope

The Android platform layer keeps Unity gameplay independent from Android and
Health Connect details:

```text
Unity UI / gameplay
  -> AndroidPlatformBridge (C# facade)
  -> IPlatformDataProvider (LIVE, MOCK, REPLAY)
  -> Kotlin AndroidPlatformBridge
     -> SensorForegroundService -> SensorManager -> JSONL buffer
     -> HealthRepository -> HealthConnectClient
```

Unity does not call `SensorManager`, Android permissions, foreground services,
or Health Connect directly. Native failures are converted to JSON results and
status-bearing C# models.

## Status model

The shared statuses are:

- `AVAILABLE`
- `UNSUPPORTED`
- `PERMISSION_REQUIRED`
- `PERMISSION_DENIED`
- `NO_DATA`
- `SERVICE_UNAVAILABLE`
- `DISCONNECTED`
- `STALE`
- `ERROR`

Every health value also carries `hasValue`. A missing value is never encoded as
an available numeric zero. Persisted sensor values older than 60 seconds and
selected old Health Connect measurements are marked `STALE`.

## Native plugin

The plugin is an Android library project at
`Assets/Plugins/Android/CapstonePlatform.androidlib`.

- `AndroidPlatformBridge.kt`: JNI-safe static facade and runtime permissions.
- `SensorForegroundService.kt`: health-type foreground service, notification,
  wake lock, handler thread, and listener cleanup.
- `SensorRepository.kt`: capability enumeration, metadata logging, ring buffer,
  latest values, and rotating JSONL persistence.
- `HealthRepository.kt`: availability, permissions, and steps/sleep/heart-rate/
  exercise reads, plus explicit Samsung Health package availability.
- `HealthPermissionActivity.kt`: Health Connect permission contract and privacy
  rationale activities.

The custom Gradle properties template enables AGP 9 built-in Kotlin. The module
uses Health Connect client `1.1.0` and Kotlin coroutines `1.10.2`.

## Sensor storage

Raw samples are stored in the application-private path
`files/platform/sensor_samples.jsonl`. Each line includes the sensor type,
values, event monotonic timestamp, receive wall-clock timestamp, accuracy, and
source. The in-memory ring holds 512 samples. The current file rotates at 4 MiB.

## Record and replay

The Unity facade supports three providers:

- `LivePlatformDataProvider`: Kotlin/Android data.
- `MockPlatformDataProvider`: deterministic hardware-free data.
- `ReplayPlatformDataProvider`: full platform snapshots from JSONL.

`StartSnapshotRecording()` decorates the current provider and appends snapshots
to `Application.persistentDataPath/platform_snapshots.jsonl` by default. That
file can later be supplied to `UseReplayProvider(path)`. On Android, automation
can select providers with Intent extras:

```text
platformInputMode = LIVE | MOCK | REPLAY
platformReplayPath = /absolute/app-readable/path/file.jsonl
```

The raw native sensor JSONL and the Unity snapshot JSONL serve different uses:
the first proves background collection; the second reproduces complete Unity
inputs including health/status state.

## Foreground service lifecycle

The service registers only sensors present on the device. Step Counter is
skipped until Activity Recognition permission is available; other supported
sensors continue to work. `Stop` unregisters listeners, stops the handler
thread, flushes pending samples, and releases the wake lock. The service uses
`START_STICKY` and `stopWithTask=false` for the background POC.

The wake lock is intentionally broad for the POC and should be revisited before
production for battery policy, sampling windows, and batching.

## Build verification

Verified in the container on 2026-09-22:

- Unity `6000.3.24f1` project validation succeeds.
- Kotlin/Health Connect compilation succeeds under AGP 9.
- Development ARM64 APK builds with min SDK 29 and target SDK 36.
- Merged APK contains the health foreground service, permission rationale
  activities, Health Connect read permissions, and Kotlin bridge classes.

Runtime hardware findings are deliberately kept in the separate device matrix.
