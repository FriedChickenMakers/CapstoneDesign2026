# S24 + Galaxy Watch test kit

Status: NOT_TESTED. This kit prepares a manual run; it does not certify a device.

## Read-only preflight

```bash
./scripts/test-s24-health.sh --serial YOUR_EXPLICIT_SERIAL
# Optional private screen capture; omit while sensitive personal information is visible:
./scripts/test-s24-health.sh --serial YOUR_EXPLICIT_SERIAL --capture-screen
```

The script selects no default device, installs nothing, changes no permissions,
and does not clear app data/logcat. Package defaults to
`com.capstonedesign2026.mockup`; override `CAPSTONE_APP_ID` if necessary.
Metadata and a blank manual-results.tsv are written to ignored
`artifacts/s24-health-<UTC>/` with directory mode 700. Screenshots are opt-in;
serials are not saved. Keep personal health values, screenshots and logs private.
No blanket logcat dump is collected. Record Watch model/OS, Samsung Health
phone/watch versions manually. OS package metadata alone cannot establish HC
availability or actual grants: confirm in the app and settings below.

## Manual sequence

1. Open the app in LIVE mode. Confirm HC availability, app version, queried
   timezone and permission count. Reject permissions first: verify gameplay still
   works and the UI describes denial. Then grant one type, then all required types
   using the app's request action and Android's own consent UI.
2. In Samsung Health settings → Health Connect → App permissions → Samsung Health,
   review desired sharing. If permissions were changed in system settings, reopen
   Samsung Health. Manually sync using Samsung Health's controls if desired.
3. Refresh Steps. Record window start/end, timezone, ALL aggregate and Samsung-only
   aggregate separately. Compare Samsung Health's matching interval and source
   priority, not its counter at a different query time. Zero and no data must have
   distinct states. Phone SensorManager step counter is a third, unrelated value.
4. Refresh Heart. Record actual sample measurement time, origin and query time.
   A lone sample is a point, old samples remain historical, gaps stay blank and
   distinct sources are not joined. Do not change real system time to test this.
5. Refresh Sleep. Compare latest session start/end duration, not sleep-stage totals.
6. Start and complete a short activity without mandatory sensor permission. Query
   its exact time interval. If no samples have synced, leave it unmeasured. Requery
   after sync; only samples in that interval may appear. Session/reward must not
   change as health data arrives. Check returned interval matches requested one.
7. Test partial permission and revoke one type during a query. That type should
   show partial/error/denial while other permitted types remain usable. Large
   histories may hit page/time limits: PARTIAL must not appear as a full week.
8. Record timestamps separately for Watch measurement, appearance in Samsung
   Health, appearance in HC, and successful app refresh. Record delay rather than
   treating asynchronous arrival as failure. Samsung origin alone proves neither
   a current Watch connection nor that a Watch created the record.

Use `manual-results.tsv` for PASS / FAIL / NOT_TESTED and observations. PASS needs
observed matching behavior with a recorded interval/source; FAIL needs a concrete
mismatch/reproduction; absent data or unexecuted cases remain NOT_TESTED. Review
interval, timezone, source filter, source priority and sync time before attributing
a mismatch to either implementation. Direct Watch connection/haptics are outside
this route and remain NOT_TESTED.

## Background follow-up (separate test)

The existing `test-platform-background.sh` has configurable screen-off durations:
300 seconds, 1800 seconds and a separately chosen long interval. It is an A7
sensor-service script, not an HC background test, and its
force-stop and coordinate taps are unsuitable to run blindly on a personal S24.
Permission grants are now OFF by default.
First inspect it, set an explicitly authorized serial, disable auto-grants, verify
coordinates and collect only private artifacts. This overnight work did **not**
execute those durations; the earlier 10-second A7 result is not a long-run result.
Non-wake-up sensor delivery can be delayed while sleeping; interpret reporting
mode and timestamps before asserting lossless collection.

Reference: [Samsung Health/HC FAQ](https://developer.samsung.com/health/health-connect-faq.html).
