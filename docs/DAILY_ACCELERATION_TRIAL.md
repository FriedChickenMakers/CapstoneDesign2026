# Daily phone acceleration trial

The LIVE Android app starts the collector only when **Settings → 가속도 수집 켜기** is pressed. Opening the app or the heart chart does not enable it. **가속도 수집 끄기** and the ongoing notification's **수집 중지** action end the session. The collector runs in its own foreground service process; removing the Unity task does not intentionally stop it.

The collector registers the phone accelerometer for 5 seconds per minute. During this explicitly enabled session it holds a partial CPU wake lock, renewed with a 120-second timeout each minute, to keep the non-wakeup accelerometer and Handler schedule from sleeping with the display. This increases battery use compared with the old opportunistic collector. It releases the lock on every normal stop/destruction. It stops after 48 hours, when unplugged below 20%, when sensor registration fails, or when a record write fails. Charging does not bypass the 48-hour limit. A stopped session requires another explicit start; it does not automatically restart on reboot or force-stop. Android/OEM restrictions may still interrupt service execution.

The service checkpoints each completed five-second burst and flushes the unfinished minute on a normal stop. App-private `files/platform/daily_acceleration/daily-YYYYMMDD.jsonl` contains UTC-day files with one aggregate per minute: sample count, gravity-inclusive mean acceleration magnitude, observation status, and a `partial` flag. Atomic daily-file replacement prevents a killed append from corrupting the next row. Checkpoints/restarts in the same minute merge sample-weighted means into one row. Completed empty windows use `NO_SAMPLES` and a null mean, never zero acceleration. A process kill can still lose the currently uncheckpointed burst (up to five seconds); a partial minute is not a full minute of continuous sensing.

Retention is seven days (pruned at session start), with a 512 KiB cap per daily file. Existing schema-1 data remains readable. The bounded `lifecycle.jsonl` / `lifecycle.previous.jsonl` journal records explicit starts, process restarts and stop reasons. `status.json` records the last sample/write and heartbeat, read directly from disk across the separate UI/service processes. Settings distinguishes running, stopped, missing legacy status, and a heartbeat older than two minutes. A stale heartbeat indicates an interruption, not a proven cause.

**Settings → 심박 차트** displays Health Connect heart-rate data; it is separate from this phone acceleration collector. The two-second raw sensor debug preview is also separate. MOCK and REPLAY do not automatically start the daily collector.

Validation: `bash scripts/test-sensor-summary-unit.sh`, `bash scripts/test-daily-acceleration-unit.sh`, `python3 scripts/test-daily-acceleration-device.py SERIAL` against a matching installed development APK, Android build and S24 device checks. Device storage tests use an isolated cache directory and do not inject fabricated samples into actual collector files. The original September verification below describes the historical auto-start/no-wake-lock version, not the current behavior.

Android references: [sensor suspend behavior](https://source.android.com/docs/core/interaction/sensors/suspend-mode), [wake locks](https://developer.android.com/develop/background-work/background-tasks/awake/wakelock), [atomic file replacement](https://developer.android.com/reference/android/util/AtomicFile).

## A7 verification, 2026-09-26

- SM-T500, Android 10/API 29: installed the development APK with `adb install -r` and opened normal `GardenPreview` mode. The `:daily_sensor` foreground service started.
- The first and subsequent persisted minute summaries contained samples, consistent with the sensor only being registered for short bursts. Measured acceleration values remain in ignored private artifacts.
- Opened **Settings → Charts** through the UI. The final APK screenshot at `artifacts/daily-acceleration-20260926/a7-charts-final-apk.png` shows one observed hourly bucket and six observed minute buckets; blank intervals remain blank.
- Removed only this app card from Recents, then turned the display off. `dumpsys power` reported `Dozing`; the service stayed alive and another observed minute row was appended while the app task was absent. Relaunching the app showed the saved rows in the charts. The app's **Stop** button ended the A7 service and set its active flag to false.
- A7 was plugged in at 100%, so this verifies behavior, not daily battery drain. The S921N daily carry test is needed to judge actual power impact.

APK SHA-256: `067f69c0982c8fdc330f3eb9f28074f311402638df243d65cc1cd28db0700e13`.

## S921N initial launch, 2026-09-26

- SM-S921N, Android 16/API 36: installed this APK with `adb install -r`, retaining app data. The normal app launch started `DailyAccelerationService` as a foreground health service in the `:daily_sensor` process.
- Removed only this app's card from Recents. The task disappeared, while the collector remained foreground and its app-private minute-summary file gained rows. The app was not reopened after dismissal.
- Android sensor-service diagnostics showed 5-second accelerometer registration bursts about one minute apart, with unregistration between bursts. This checks the intended duty cycle on the actual phone; full-day battery impact remains for the user's daily carry test.
- After the phone screen turned off and wireless ADB disconnected, a later reconnection showed the app task still absent, the foreground collector still running, and the minute-summary file grown from two to five observed rows. Sensor-service history included successive registration/unregistration pairs about one minute apart, each with an approximately five-second capture span. The app was not reopened for this check.
