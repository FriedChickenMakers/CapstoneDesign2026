# Health Connect query findings — 2026-09-23

Status: IMPLEMENTED / UNIT_TESTED (synthetic JVM); S24 + Galaxy Watch NOT_TESTED.
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

`steps` is `aggregate(StepsRecord.COUNT_TOTAL)` across all sources; `samsungSteps`
is a separate aggregate filtered to `com.sec.android.app.shealth`. Neither is
SensorManager's cumulative counter. Raw overlapping records are never summed.
Null aggregate means NO_DATA/hasValue=false, while numeric zero remains a valid
AVAILABLE/hasValue=true count. Query range, zone, filter and returned origins are
included. Aggregate timestamps are not measurement timestamps: `measuredAtEpochMs`
is zero and the UI must show it as unavailable, not replace it with refresh time.

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
