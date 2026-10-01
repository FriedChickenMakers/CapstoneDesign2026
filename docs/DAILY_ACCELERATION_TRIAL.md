# Daily phone acceleration trial

The normal LIVE Android app starts a 48-hour accelerometer trial when opened. The collector runs in its own foreground-service process, so removing the app card from Recents does not intentionally stop collection. The notification's **Stop** action ends the trial. A new normal app launch can start another trial.

To reduce battery use, the service listens to the accelerometer for 5 seconds, then unregisters it for 55 seconds. It takes no wake lock and does not schedule wakeups to fill gaps during deep sleep. It automatically stops when unplugged below 20% battery, after 48 hours, or if summary writing fails. Sensor, OS, and battery behavior still need measurement during actual daily carry.

Only one mean acceleration magnitude and sample count per completed minute are stored in app-private `files/platform/daily_acceleration/daily-YYYYMMDD.jsonl`. Individual events are not saved. Files older than seven days are removed when the service starts; each daily file has a 512 KiB cap. The means include gravity, so a stationary device normally reads near 9.8 m/s². Missing minutes remain blank, not zero.

Open **Settings → Charts** to see two temporary GraphView charts: 24 one-hour means and 60 one-minute means. The hour means weight the minute means by their sample counts. **Refresh** reloads stored summaries. The chart uses [GraphView 4.2.2](https://github.com/jjoe64/GraphView) for debug display only; the final in-app chart can be redesigned independently.

The separate `SensorSummaryExperiment` intent still uses the earlier experimental service. MOCK and REPLAY launches do not start the daily collector.

Validation artifact: `artifacts/daily-acceleration-20260926/build/capstone-mockup.apk`. The bucket unit test is `bash scripts/test-daily-acceleration-unit.sh`. Device validation results are recorded separately after A7 and S921N runs.

## A7 verification, 2026-09-26

- SM-T500, Android 10/API 29: installed the development APK with `adb install -r` and opened normal `GardenPreview` mode. The `:daily_sensor` foreground service started.
- The first persisted minute had 249 samples and a 9.8207 m/s² mean. Subsequent minute summaries had 25, 25, and 21 samples, consistent with the sensor only being registered for short bursts.
- Opened **Settings → Charts** through the UI. The final APK screenshot at `artifacts/daily-acceleration-20260926/a7-charts-final-apk.png` shows one observed hourly bucket and six observed minute buckets; blank intervals remain blank.
- Removed only this app card from Recents, then turned the display off. `dumpsys power` reported `Dozing`; the service stayed alive and another observed minute row was appended while the app task was absent. Relaunching the app showed the saved rows in the charts. The app's **Stop** button ended the A7 service and set its active flag to false.
- A7 was plugged in at 100%, so this verifies behavior, not daily battery drain. The S921N daily carry test is needed to judge actual power impact.

APK SHA-256: `067f69c0982c8fdc330f3eb9f28074f311402638df243d65cc1cd28db0700e13`.

## S921N initial launch, 2026-09-26

- SM-S921N, Android 16/API 36: installed this APK with `adb install -r`, retaining app data. The normal app launch started `DailyAccelerationService` as a foreground health service in the `:daily_sensor` process.
- Removed only this app's card from Recents. The task disappeared, while the collector remained foreground and its app-private minute-summary file gained rows. The app was not reopened after dismissal.
- Android sensor-service diagnostics showed 5-second accelerometer registration bursts about one minute apart, with unregistration between bursts. This checks the intended duty cycle on the actual phone; full-day battery impact remains for the user's daily carry test.
- After the phone screen turned off and wireless ADB disconnected, a later reconnection showed the app task still absent, the foreground collector still running, and the minute-summary file grown from two to five observed rows. Sensor-service history included 14:57 and 14:58 registration/unregistration pairs, each about five seconds apart. The app was not reopened for this check.
