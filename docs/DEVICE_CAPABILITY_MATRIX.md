# Device Capability Matrix

Only observed results belong in this table. Build-time assumptions are not
recorded as device capabilities.

S24 observations below are from 2026-09-26 after `adb install -r` of the
permission-diagnostics APK. The Watch was not inspected. The A7 rows describe
the earlier APK with its previous wake-lock behavior.

| Capability | Galaxy S24 + Watch | Galaxy Tab A7 |
| --- | --- | --- |
| ADB | Connected; `SM-S921N`, Android 16 / API 36 | Authorized; `SM-T500`, Android 10 / API 29 |
| Accelerometer | `AVAILABLE` in two-second raw capture | Available; TDK-InvenSense `icm4x6xx` |
| Gyroscope | `AVAILABLE` in two-second raw capture | Available; TDK-InvenSense `icm4x6xx` |
| Gravity | `AVAILABLE` in two-second raw capture | Available; Qualcomm virtual sensor |
| Linear acceleration | `AVAILABLE` in two-second raw capture | Available; Qualcomm virtual sensor |
| Magnetic field | Not measured | Available; Senodia `st480ms` |
| Light | Not measured | Available; Sensortek `stk3329b` |
| Pressure | `AVAILABLE` in two-second raw capture | `UNSUPPORTED` |
| Step counter | `AVAILABLE` with Activity Recognition granted | Available after Activity Recognition grant |
| Activity Recognition flow | Granted before update; denied/request UI not tested | System Allow/Deny dialog opened; denied state stayed nonfatal |
| Health Connect availability | `AVAILABLE`; four of four read permissions granted, queries completed | `SERVICE_UNAVAILABLE` |
| Samsung Health source data | No Samsung-origin record returned; all-source steps present | `SERVICE_UNAVAILABLE` |
| Watch-origin data | Not tested | N/A by test role |
| Foreground sensor collection | Raw burst returned values; daily acceleration service active after app sent Home | Passed; seven requested sensors registered |
| Background timestamp advance | Preserved prior daily summaries; post-update advance not tested | Passed (timestamps advanced during the observation) |
| Screen-off timestamp advance | Not tested after update | Passed (timestamps advanced during the observation) |
| Service stop / release | Not tested | Passed; service removed and samples stopped advancing |

## How to update

1. Approve the computer RSA prompt on the device and confirm `adb devices -l`
   reports `device`, not `unauthorized`.
2. Build/deploy the APK and use the in-app debug panel for capability statuses.
3. Run `scripts/test-platform-background.sh` with the exact `ADB_SERIAL`.
4. Copy observed sensor names and capability/status conclusions here. Keep actual
   Health Connect values, personal timestamps, device identifiers and screenshots
   in ignored private artifacts. Do not infer a capability from the model name.

For the S24, verify at least two real Health Connect categories and preserve the
reported `sourcePackage`. A value labelled Samsung Health is evidence that the
record came from Samsung Health; it does not by itself prove a live watch
connection at query time.

## Overnight validation scope

Pure-JVM Health query and bounded sensor-file fixtures: UNIT_TESTED.
S24 preflight/checklist: `docs/S24_WATCH_TEST_CHECKLIST.md`.
Screen-off 5 min / 30 min / long-duration: NOT_TESTED on both devices.
Watch → Samsung Health → Health Connect sync: NOT_TESTED. S24 screenshots and
aggregate data inspection are kept in the ignored private
`artifacts/s24-permission-20260926/` directory.
