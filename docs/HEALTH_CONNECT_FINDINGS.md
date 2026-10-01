# Health Connect query findings — 2026-09-23

Status: IMPLEMENTED / UNIT_TESTED (synthetic JVM); basic S24 reads DEVICE_TESTED_S24
on 2026-10-02. Multiple overlapping providers and Watch provenance remain NOT_TESTED.
Android library remains Health Connect `connect-client:1.1.0`; no SDK upgrade.

## Query contract

A refresh fixes its end Instant and timezone before issuing calls. Steps uses the
local day containing that Instant; Sleep uses 48 hours; Heart/Exercise use 7 days.
Raw reads use 1000-record pages, terminate on null **or empty** tokens, detect
repeated tokens, cap at 100 pages and 30 seconds per type. IllegalStateException
(including quota failures) gets at most two retries, after 250/500 ms. Permission
failures are not retried. External coroutine cancellation propagates. A failed
page preserves earlier data with `status=PARTIAL`, `queryComplete=false` and a
machine-readable `completionReason`; it is not a complete interval. Failure in
one data type does not prevent the following types being read. Permissions are
checked separately per type. `lastSuccessfulRefreshEpochMs` retains the most
recent refresh with at least one complete type; inspect each metric for per-type
success, rather than treating this as an all-permissions success flag.

These limits and the five-minute graph gap threshold are DEV_DEFAULT, not product
policy. The timeout is per data type, not the whole refresh; up to five serial
queries (including two step aggregates) can each consume the budget.

## Steps

As of 2026-10-02, `steps` selects one source from paged raw records using
`HealthSourcePolicy`; daily display, mission ranges and the reward worker share
this path. It prefers explicit wearable metadata, then Samsung records whose
device is unspecified, then phone records, then other sources. Samsung origin
alone does not prove wearable provenance. A source with no positive steps can
fall back to another source. Overlaps within the chosen source contribute only
uncovered duration; clipped records are prorated. Counts from different sources
are never added. `sourceBreakdown` shows their separate candidate totals.

`samsungSteps` remains a separate Samsung-only aggregate for diagnostics.
Neither value adds SensorManager's cumulative counter. Empty data and zero are
distinct, and incomplete reads do not drive rewards. Query range, zone, filter
and selected origin are included. `measuredAtEpochMs` is unavailable for the
step total, rather than being replaced with refresh time. Sleep, heart and
exercise choose sources independently, including their own freshness rules.

Android recommends aggregation for cumulative records to avoid double counting;
its aggregation honors source deduplication rules. See [raw reads](https://developer.android.com/health-and-fitness/health-connect/read-data)
and [aggregate data](https://developer.android.com/health-and-fitness/health-connect/aggregate-data).
Provider deduplication itself is an Android responsibility and is not claimed as
covered by the local pure Kotlin tests.

## Heart series and DTO

Record samples are flattened, filtered by **sample** time in `[start,end)`, sorted,
and deduplicated by record ID + origin + timestamp + BPM. Distinct record IDs
are retained even if they contain equal values/times, preserving provenance.
All returned samples are processed before display sampling. `sampleCount` is the
processed count; `heartRateSamples` is a maximum 512-point view with endpoints;
`displaySampleCount` identifies its size. This view can omit peaks and is not a
clinical signal or an export of the raw history. Raw processing is in memory and
is not persisted. A bounded page count does not imply a fixed sample count per
record; profiling dense histories remains a device follow-up.

Each sample has `recordId`, `sourcePackage`, `measuredAtEpochMs`, `beatsPerMinute`,
and `segmentId`. Graphs connect points only within the same source and segment.
Gaps greater than five minutes break segments. `missingIntervals` includes leading,
internal and trailing gaps per source, or the whole range if no samples exist.
This threshold controls visualization; it makes no inference about user health.
A single sample is a point, not an extended live trace. Latest BPM keeps its
measurement timestamp; older than 24 hours has STALE status (DEV_DEFAULT). No
BPM/HRV success/failure or treatment-effect scoring is introduced.

`refreshHealthRange(startEpochMs,endEpochMs)` requests a dedicated heart interval
(maximum 7 days, past times only). It keeps other metric windows unchanged. A
busy range refresh returns REFRESH_BUSY, so callers must retry after it finishes;
ordinary refresh requests coalesce into one follow-up query. Match returned
`queryStartEpochMs/queryEndEpochMs` before attaching to a session.
A downsampled seven-day snapshot must never be treated as a complete short-session
history. Partial results cannot prove there were no measurements.

## Sleep, provenance, and Watch

Sleep is latest **session length** (end minus start), not actual asleep time or
stage analysis. No deep-sleep estimates are generated.

Samsung origin detection scans all returned record origins, not just the latest
metric. `watchData` is a legacy DTO name only; its message identifies Samsung
Health origin and `directConnectionStatus=NOT_CHECKED`. It is not connection
telemetry. [Samsung's FAQ](https://developer.samsung.com/health/health-connect-faq.html)
describes Watch → Samsung Health → Health Connect and separate sync schedules.
Watch origin, live BPM, direct connection and haptics remain NOT_TESTED.

The debug panel now captures raw sensors only for two seconds when Settings opens;
this is separate from the persistent daily acceleration collector. On opening
Settings, missing Android runtime and Health Connect permissions launch the system
requests. If a prior request was denied, a dialog offers the appropriate app or
Health Connect settings page. Returning to the app refreshes the permission and
health snapshot, including permissions granted manually while the app was running.
`Samsung Health app` reports installation/availability only;
`Samsung-origin data in Health Connect` reports whether queried records came from
Samsung Health. The latter can remain NO_DATA after this app's permissions are
granted until Samsung Health sharing and synchronization provide records. This
behavior was observed on S24 with all four read permissions granted; the
first-request and previously-denied native permission dialogs still require
device validation.

## Actual validation and remaining work

Run `./scripts/test-health-query.sh`; output lives under
`artifacts/health-query-tests/results.txt`. Twenty synthetic cases pass:
multiple/empty-token pages, repeated tokens, page limit, revoked permission,
quota retry cap, timeout, external/explicit cancellation and parent timeout, empty query, sample bounds,
sorting/duplicates, separate sources/gaps, empty interval, display sampling and
DST midnight. They run production HealthQueryLogic on JVM with fake page readers,
not a model reimplementation. On S24, the debug UI displayed Health Connect
availability, four granted read permissions, an all-source steps aggregate,
and completed empty Samsung-only/sleep/heart/exercise queries. This validates
the basic Android query-to-Unity display path for that device, not the Android
permission dialog flow, Samsung Health synchronization, aggregate behavior
across multiple providers, or Watch provenance. APK compile evidence belongs
to the root execution report.

P2 background HC Worker and notification delivery remain pending. Bounded sensor
persistence hardening adds four JVM storage cases (20 total); see
BACKGROUND_SENSOR_FINDINGS.md. Long-duration sensor behavior remains NOT_TESTED.

## 2026-10-02 S24 follow-up

The latest APK was installed with `adb install --no-streaming -r`, preserving
existing game data and all four granted read permissions. On SM-S921N / Android
16, settings displayed complete Samsung-origin step, sleep and heart results;
exercise was NO_DATA. The daily step value matched its sole source candidate
and the Samsung-only diagnostic. Device metadata was unspecified, so this run
does not establish Watch provenance or verify overlapping-provider behavior.
Raw sensor capture, refresh and chart navigation also worked. Personal values
and screenshots remain in ignored private artifacts, not in this document.

Synthetic source-policy tests cover overlapping origins, watch and phone records
from the same app, interval overlap/clipping and independent metric fallback.
See `artifacts/health-source-policy-tests/results.txt` (27 cases). The earlier
validation sections describe historical builds; worker/reminder implementation
has since been added, but background delivery is not validated by this S24 run.

## Hourly heart chart — 2026-10-02

The Settings chart now reads heart-rate records for a fixed rolling 24-hour
window and displays 24 one-hour means in BPM. The x-axis labels mark each
bucket's end in local time. The query uses only heart-rate read permission and
the existing preferred-source policy; it does not modify the shared session
snapshot or start a sensor service. Queries run off the UI thread, occur only
on opening/manual refresh, and are cancelled when the activity is destroyed.

Means use the complete, timestamp-filtered and deduplicated sample series before
display downsampling. Missing hours remain blank and line segments stop at
missing buckets. The UI distinguishes missing permission, unsupported provider,
empty results, partial reads and errors, and displays the source, query interval
and last measurement time. Partial averages are explicitly provisional.
Existing acceleration data and manual Trial On/Off controls are retained;
this chart no longer displays acceleration history.

Native synthetic tests now pass 29 cases, including hourly boundaries,
duplicates, empty intervals and all 1,000 samples beyond the 512-point display
limit. Output: `artifacts/hourly-heart-tests/results.txt`.

DEVICE_TESTED_S24: installed with data-preserving `--no-streaming -r`; actual
Samsung-origin samples rendered as hourly means with missing hours left blank.
Manual refresh updated the interval and chart successfully. No matched runtime
exceptions were found in the current app's error log. Screen-off timeout was
restored to its original 30,000 ms and read back after testing. Personal chart
screenshots remain under ignored `artifacts/s24-hourly-heart-test/`.
APK: `artifacts/s24-hourly-heart-build/build/capstone-mockup.apk`;
SHA256: `cd234a7280067921ef137d1935d4009c9c182c19932e98899ad3c0bccf3ae173`.
