# Plan / implementation gaps — 2026-09-23 overnight

This run prioritizes complete, testable P0/P1 slices. Statuses refer to this run,
not earlier A7 measurements. No S24/Watch or A7 is connected in this run.

| Area | Delivered | Remaining / evidence boundary |
|---|---|---|
| P0 persistence | Versioned checksum state, single writer, atomic replace, retained backup, migration, receipts, injected clock | Android filesystem power-loss and IL2CPP device runtime NOT_TESTED; corruption deliberately fails closed, manual recovery required |
| Daily cycles | Local 04:00 DEV_DEFAULT, monotonic date high-water, one current decision, no catchup | No server clock/anti-cheat; moving clock far forward can defer cycles until date catches up |
| Course | All source-backed P01–P28 plus M01–M17; separate stable typed IDs and explicit completion | Text alternatives are locally authored; audio metadata only, playback unavailable pending rights/content review |
| Sessions | Select/start/pause/resume/end/optional reflection/atomic reward, process restoration | Wall-clock activity range includes pauses, no clinical interpretation; history UI currently latest five entries, no full-history paging or weekly summary |
| Garden | Nutrient spending, growth preview, flowerbed/shelter fixed slots, cancel/confirm, visible local changes | DEV balance only; existing generic tree is the replaceable visual for plant:P06 (maple); procedural environment/rabbit placeholders, no free-placement editor |
| Animals | At most one persisted visitor, none allowed, discoveries retained | Rendered/demo candidate pool restricted to source-backed rabbit A07 for E01/E02; other catalog animals not yet instantiated. Candidate OR policy and 50% rate are DEV_DEFAULT, not finalized planning |
| HC | Paginated reads, partial flags, aggregate all/Samsung Steps, sample times/sources/gaps, session range refresh | SDK/provider dedup and permissions need device validation; read-only JVM seams do not emulate Health Connect. UI downsampling can omit extrema |
| Sensor service | No blanket wake lock; bounded async disk writer, bounded logs, reporting metadata, late callback guard | Screen-off 5m/30m/long tests prepared but NOT_TESTED. In-app deletion and persisted explicit opt-in remain pending |
| P2 notifications | Not implemented | User-enabled permissions, quiet hours, suppression, dedupe, cancellation, deep-link validation require a separate tested slice |
| P2 HC worker | Not implemented | Feature/background permission checks, opt-in, unique WorkManager work, cache lifecycle require a separate tested slice |
| Privacy | Game saves partitioned LIVE/MOCK/REPLAY; no health samples written into game state; fixtures synthetic | At-rest encryption, user-facing export/delete and retention policy remain product decisions |
| Device kit | Explicit serial read-only S24 preflight, manual permission/sync checklist, long screen-off script | Watch → Samsung Health → HC remains NOT_TESTED; no direct Watch connection/control claim |

Do not interpret a successful APK build as DEVICE_TESTED. The retained old A7
result was only a 10-second screen-off observation of the previous build.

## DEV_DEFAULT decisions

- Day reset: local 04:00. Date keys never move backward; timezone is recorded.
- Reward: 10 nutrient per typed mission per daily instance; repeated session
  submission cannot repay. Ending participation pays zero pending product policy.
- Growth: cost 10, +0.1 growth. Environment: cost 10, fixed distinct slots.
- Visitor: 50% of eligible cycles; no eligible candidate means none. Only rabbit
  placeholder is enabled; environment changes affect the next cycle.
- First launch grants no invented starting nutrient. Old nutrient, XP, growth,
  last unlock and known Week 1 seed are retained without conversion.
- HC budgets/gap thresholds are engineering limits documented separately.

## Known execution limitations

The repository's `.git/objects` contains root-owned non-writable subdirectories.
Running Git as the repository owner allowed status and branch creation but
checkpoint commit staging failed with `insufficient permission for adding an
object to repository database`. No permission/security changes were made; index
remained unstaged. A private independent `review-repository` clone under this
run's ignored artifacts uses its own copied Git objects; it preserves baseline
and final implementation commits without changing the original object store.
The original working tree remains the build workspace. No remote push occurred.
