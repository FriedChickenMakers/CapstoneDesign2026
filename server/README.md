# Optional recommendation research service

This directory contains the authenticated event API and an offline response-model
evaluation command. The Android app remains usable without either service. A
missing, invalid or expired model falls back to the local recommendation rules;
the required course order, rewards and garden state stay local.

An isolated external HTTPS QA service has now been deployed and verified with
synthetic registrations. Deployment does **not** provision real participants,
enable collection without registration, or demonstrate that a learned policy
outperforms the baseline. See [OPERATIONS.md](OPERATIONS.md) for the source
baseline, test boundaries, deployment isolation and operator procedures. The
actual A7/API29 bootstrap-to-final path and external HTTPS upload have
also passed with a controlled QA identity; prospective participant evaluation
remains separate. APK/UI release approval is recorded in
[QA release readiness](../docs/QA_RELEASE_READINESS_20261109.md).

## Run locally

Use Python 3 with the standard library, from the repository root:

```sh
umask 077
export CAPSTONE_ENROLLMENTS_FILE=/private/path/enrollments.json
export CAPSTONE_EVENTS_DB=/private/path/events.sqlite
export CAPSTONE_API_PORT=8091
python3 -m server.service
```

The server binds to loopback only. The enrollment file is an operator-provisioned
JSON mapping from a randomly generated bearer token to a distinct pseudonymous
participant ID. Tokens must be at least 32 characters; IDs must be lowercase
32-character UUID hex strings. Generate values with `secrets.token_urlsafe(32)`
and `uuid.uuid4().hex`; do not put real values in source, command history, CI
configuration examples or screenshots. Store the enrollment file with mode 0600
inside a private directory. Do not use device identifiers or contact details as
participant IDs.

For the manual workflow (including the verified S24 build 1010), app enrollment
is provisioned separately as `personalization-enrollment.json` in
the Android application's private files directory. Its only fields are
`endpoint` and `token`. The endpoint is an HTTPS origin without a path, query,
fragment or embedded credentials. Debug builds permit HTTP only for loopback
development. The operator provisions this existing file only after collecting written consent
outside the app; measurement is a condition of this QA/beta participation. The
app does not inspect a signed form and no new marker or enrollment schema is added.
A valid registered LIVE app activates recommendation and measurement collection
from the initial garden screen, without visiting missions or settings and without
an in-app consent card, toggle or extra confirmation. Unregistered apps
remain local and do not export these records. A participant's bearer token is
never bundled in the APK. The optional automatic-registration contract below
uses a separate shared distribution key; it does not replace existing manual
registrations or claim strong device authentication.

Existing authorized queues and collection-start boundaries are preserved. A newly
activated measurement scope starts at activation time and excludes earlier selected
sessions. Once enrollment is bound, transient activation-state writes retry every
three seconds without moving an established collection boundary. Failed initial
enrollment reads or queue binding also retry in the same running client; collection
is suspended while the existing native outbox remains intact. Explicit withdrawal
still clears the authorized queues. Delete-pending and revoked registrations must
not auto-enable.

Before an operator-managed QA install, run `scripts/check-qa-apk.py` to inspect
package identity, version, signing certificate, debuggability and bundled private
material without connecting to a device. `--distribution bootstrap-final` requires
both `--previous-apk` pointing to the debuggable enrollment bootstrap and
`--verifier-apk` pointing to the fixed-target QA registration verifier. It verifies
a same-package, same-signer, higher-version-code non-debuggable final APK and a
verifier signed by the same certificate for the fixed QA target package. The
operator must provision and verify the app-private registration before the in-place
update; this offline gate does not perform enrollment or establish server connectivity.
The separate device verifier checks the registration file hash, queue enrollment
fingerprint and consent boundary without launching the app or changing its data.
A verifier pass is not evidence of HTTPS upload, server ACK or withdrawal success.
Its `expectedSha256` is the SHA-256 of the exact registration-file bytes, not the
APK hash; `expectedMeasurementConsentUtcMs` is the bootstrap collection-activation
boundary, not the time a participant signed a form. The verifier is active-only
(`expectedEnabled=true`) and cannot validate withdrawal. Re-enrolling after a
non-debuggable final install requires a newer bootstrap followed by a still newer
same-package, same-signer final APK; do not downgrade or clear participant data.
Server `DELETE /participant` revokes server access but does not itself stop device
collection. The operator must separately confirm local collection shutdown and
local/export/backup cleanup; this does not add an in-app consent screen.
The final APK requires HTTPS, including during QA: debug-only HTTP loopback
registrations are rejected after switching to a non-development build. Final
non-debuggable apps also reject `run-as`, so take the authorized private backup
before replacement; the existing development deployment helper's post-install
backup/launch sequence is not a non-debuggable deployment workflow.

For any remote deployment, terminate TLS in a reviewed service configuration,
keep the Python listener private, and configure storage and backup access before
enrolling participants. The standard-library HTTP adapter is a development
server. It intentionally suppresses request logging. Reverse proxies must also
avoid logging bearer headers or behavioral request bodies.

The Python API runs separately from the app. The isolated external QA service
passed 18 actual HTTPS checks plus 10 database/restart/backup checks with synthetic
data; four further HTTPS checks passed after the dedicated API restart. A separate
synthetic device registration remains active. These checks do not establish an
Android app upload, real-participant collection, or an A/B effect; the device
bootstrap-to-final flow was verified on A7/API29, including an actual Wi-Fi
outage, process restart and exact event replay over HTTPS in the bootstrap,
then a new event from the non-debuggable final. These controlled QA identities
are excluded from participant results. UI distribution approval is tracked
separately in the [release record](../docs/QA_RELEASE_READINESS_20261109.md).
See [OPERATIONS.md](OPERATIONS.md)
for details. The analysis PNG is generated outside the app, not added as a new
in-app chart.

## Automatic install registration

**Implementation status:** source release `qa-auto-20261110-7249cb5ad7c1` is
deployed with the key file and exact proxy route. Local/remote server tests and
synthetic HTTPS protocol checks passed; see [operations](OPERATIONS.md).
Existing manual S24/A7 registrations remain valid. New APK/device results must
be established separately from these server checks.

An authorized QA build creates a random per-install token locally before it has
network access. Its events and timestamps can enter the existing persistent
outbox. On an upload's 401, the native client may register that same token and
retry the same batch once; it must not replace the token or recreate events.
A manual registration has no automatic-registration key and retains its operator
workflow. Client implementation/real-device validation are separate from server
unit tests.

Configure the server with `CAPSTONE_ENROLLMENT_KEY_FILE=/private/path/enrollment-key`.
The owner-private regular file contains exactly 64 lowercase hex characters
(32 random bytes), optionally followed by a newline. Use mode 0600. The API
reads it at startup; an unset path closes registration with 404, while a
configured unreadable/malformed/non-private file fails startup safely. Never
put the actual key in Git, command arguments, logs or documentation. The key
from private build configuration must match this server file. A key rotation
does not invalidate already registered bearer tokens; it affects new registration.

| Request/response | Contract |
| --- | --- |
| Request | `POST /enrollments`, `Content-Type: application/json`, `X-Enrollment-Key: <distribution key>` |
| Body | Exactly `token` (64 lowercase hex characters) and `app_version` (`[A-Za-z0-9._-]{1,48}`); maximum 1,024 bytes |
| Success | HTTP 200 with exactly `{"registered":true}`; no participant identity or token returned |
| Closed/invalid key | 404 when unconfigured; 401 for a missing or invalid shared key |
| Invalid/revoked | 400 for invalid fields/duplicate JSON fields; 413 for excess body size; 403 for a withdrawn token |
| New-registration budget | Global 240/hour and 1,000/day; 429 with `Retry-After: 60`. No per-IP identity or IP rate bucket, so one school's NAT is not treated as one participant |

An installation can withdraw before its first successful registration/upload.
For **only this unknown-token deletion**, `DELETE /participant` may include both
`Authorization: Bearer <local 64-lowercase-hex token>` and the valid
`X-Enrollment-Key`. The server atomically creates a hashed identity and revokes
it, returning the existing `deleted: true, enrollment_revoked: true` response
without accepting any activity or measurement. Later registration of that token
returns 403. A missing/invalid distribution key returns 401 for this unknown
token; malformed tokens with a valid gate return 400.

First-time pre-registration withdrawals share the new-registration budget. Their
identity metadata uses the internal marker `withdraw-before-registration`, not
an observed app version. Repeated deletion is idempotent and does not consume
that budget. Already registered or revoked tokens retain normal deletion without
the distribution key, even if it has rotated or automatic registration is closed.
The shared key never grants an unknown token access to measurement/policy routes.

`automatic_enrollments` stores only SHA-256(token), a random UUID subject, first
registration time and app version. A SQLite immediate transaction makes lookup,
new-registration limits and insertion atomic across threads/processes. Existing
tokens return the same subject on retries/restarts and do not consume or fail the
new-registration budget. A matching manual token preserves its existing subject.
The distribution key comparison uses constant-time digest comparison.

Withdrawal retains subject and token-hash tombstones. A withdrawn manual token
cannot regain access by being removed from the operator mapping and submitted
as a new automatic token. Those minimal tombstones/identity mappings remain
after event retention; deleting them would permit identity reset or resurrection.
Managed backups carry the token-hash tombstones as well as subject revocations.
Previously removed manual mappings cannot be reconstructed if no token mapping
was available during migration; retain current manual mappings during upgrade.

The shared APK key can be extracted. It is a basic unwanted-ingestion gate,
not device attestation or proof that the client is genuine. Existing body,
connection and new-registration limits still apply. A newly generated token
after an intentional reinstall is a new anonymous installation, not a permanent
hardware identity.

An actor holding the shared key can exhaust the global new-identity budget and
delay legitimate new registration or a first pre-registration withdrawal. This
is an accepted availability limitation of the minimal gate for a controlled QA
distribution, not a claim of abuse-proof public enrollment. Existing registered
uploads and idempotent registration/deletion retries are outside that budget.
For key rotation and its effect on not-yet-registered offline installations,
see [the operational procedure](OPERATIONS.md#distribution-key-rotation-and-availability).

## API contract

Data/policy routes require `Authorization: Bearer <registered token>`. Deletion
also supports the narrowly gated pre-registration case above. Only the optional
`/enrollments` route uses `X-Enrollment-Key` instead of a bearer token.
Responses use JSON and `Cache-Control: no-store`. Redirects are not part of the
protocol. The client does not forward bearer credentials to redirected hosts.

| Method and path | Request | Successful response |
| --- | --- | --- |
| `POST /events/batch` | JSON `schema_version: 1`, `consent_version: 1`, and 1–100 events | `accepted_event_ids`, including idempotent retries |
| `GET /policy/current` | No body | Validated rules or logistic policy; rules when no policy file is configured |
| `DELETE /participant` | No body | `deleted: true`, `enrollment_revoked: true` |

An event has exactly `event_id`, `decision_id`, `kind`, `occurred_utc_ms`,
`utc_offset_minutes`, `activity_id`, `policy_id`, `app_version`,
`content_version`, and `features`. Decision IDs are random UUID hex strings;
event IDs use that 32-hex format and recovered lifecycle events derive a stable
ID from their session and kind. Activity IDs refer to catalog entries. Features are exactly four finite
numbers in [0, 1], ordered as `preference`, `completion_rate`, `days_since`, and
`duration_fit`. Journal text, symptoms, raw health measurements, contact details,
and device IDs are outside this contract and are rejected as extra fields.

The schema recognizes recommendation exposure and activity lifecycle events, plus
separately named notification eligibility, schedule, post acceptance, blocked,
failed and opened events. A schema allowing a kind does not establish that all
platform producers are wired: check the app integration and QA coverage before
claiming notification telemetry completeness. In particular, an accepted Android
notification post is not proof that the person heard or saw it.

Batches are atomic. Retrying an identical event ID/payload is safe. Reusing an ID
with different content returns 409 and writes none of that batch. Invalid shapes
return 400; oversized bodies return 413; non-JSON POSTs return 415. Authentication
fails with 401; a withdrawn enrollment returns 403. A malformed, expired or
unready configured model returns 503, so clients retain local fallback behavior.
The maximum HTTP body is 128 KiB.

Both native measurement and C# behavior outboxes preserve future-dated rows when
the device clock is corrected, defer those rows and send other valid events. They
never rewrite observed timestamps to obtain acceptance. If the device clock is
still more than five minutes ahead of the server, the existing atomic API can
reject the whole batch; local filtering cannot establish trusted server time.
No server-Date synchronization or automatic clock correction has been implemented.
Native measurement completeness remains 0 while any row is deferred. The separate
behavior API has no completeness watermark or queue-loss counter: its 256-event
queue and 8,192 receipts are bounded, and a full queue rejects new events. Do not
treat absent behavior outcomes during clock/overflow incidents as verified
nonresponses or personalization improvement. Resolve time settings and verify
queue drainage before including such observations; unverified windows remain
excluded from presentation claims. The behavior evaluator does not perform this
operational exclusion automatically.

Deletion removes that participant's event rows and keeps a minimal revocation
tombstone. This prevents a delayed upload from recreating withdrawn records.
Withdrawal is requested through the operator. The internal `SetConsent(false)`
control/test path stops device collection and records pending deletion; there is
no participant-facing withdrawal button or new administrator portal. The operator
must verify that device collection/outboxes are stopped, call/confirm
`DELETE /participant`, then reconcile local enrollment, exports and backups.
Removing only the device enrollment file does not complete server deletion.

Repeated deletion is idempotent. A new enrollment requires a new operator-issued
identity/token; withdrawal is not silently reversed by an upload retry.

Event retention is 90 days based on occurrence time. Expired rows are removed on
startup, authenticated requests and an idle-server check at least hourly while
the service runs. A storage error causes a retry after one minute. When the
service is stopped or storage is unavailable, deletion cannot run; startup
performs catch-up before serving. SQLite secure deletion is enabled, but this
does not remove separately retained filesystem snapshots, exports or backups.
Those require an operator-managed retention/deletion process. Revocation
tombstones remain to prevent old credentials from restoring deleted events.
The deployed hourly maintenance job now manages a private set of seven daily
SQLite online snapshots and scrubs revoked/expired rows from them. It does not
touch earlier operator archives or exports. Run and verify maintenance as part
of withdrawal; see [operations](OPERATIONS.md#backup-recovery-and-withdrawal).

## Evaluate without deploying

```sh
umask 077
python3 -m server.evaluate \
  --database /private/path/events.sqlite \
  --output-dir /private/path/evaluation \
  --as-of-ms <explicit-UTC-epoch-milliseconds>
```

The database is opened read-only. The command writes `evaluation.json` and
`policy-candidate.json`; it does not upload observations or change the serving
policy. To serve a separately reviewed candidate, an operator explicitly sets
`CAPSTONE_POLICY_FILE` to the approved private file.

The target is whether the displayed activity was started within 24 hours of its
recommendation exposure. Features are frozen at exposure time. Later events
provide labels only. Windows still open at the specified evaluation time and
ambiguous duplicate exposures are excluded.

Evaluation uses two separate splits:

- **Same-user future:** one global calendar cutoff, with training outcomes closed
  before it. A 24-hour purge prevents unfinished training labels from crossing
  into evaluation. Test participants must already appear in training. Each
  participant needs at least six observations to enter this split.
- **Unseen users:** deterministic participant-disjoint training and evaluation,
  requiring at least four participants.

Each fit requires at least 20 training rows and both outcome classes; each test
requires at least 10 rows and both classes. Brier score and log loss compare the
logistic candidate with the fixed rule baseline. Passing both offline comparisons
can produce a candidate; `deployment_approved` remains false. Insufficient data
produces a rules policy. Candidates expire after 30 days.

These are engineering checks, not evidence of clinical benefit or the causal
effect of a new recommendation policy. They describe responses to previously
observed recommendations. Repeated users, missing or delayed uploads, selection
bias, queue capacity, small sample sizes, and feature quality can affect the
estimate. A closed wall-clock window alone does not establish that every device
has synchronized its outcome. Review upload completeness and collection health
before accepting negative labels; run a prospective controlled comparison before
claiming policy improvement. The current offline gate does not estimate
confidence intervals, optimize notification timing, or evaluate depression
outcomes.

## Validation

```sh
python3 -m unittest discover -s server/tests -v
```

Tests use generated credentials, pseudonyms and synthetic observations only.
They cover HTTP behavior, malformed bodies, atomic/idempotent ingestion,
isolation, deletion versus late retries, startup/read/idle retention, temporal
and participant split separation, frozen features, closed response windows,
insufficient-data fallback and deterministic candidate output. They do not
replace real-device lifecycle, TLS deployment, operational backup or participant
usability tests.

## Additional QA/beta measurements (written consent; wire version 2)

`POST /measurements/batch` uses the existing authenticated enrollment, schema 1
and consent 2. It is separate from the four-feature recommendation contract.
The exact field allowlist is in `measurements.py`; batches contain 0–100 events,
a collection ID, dropped-attempt/expired-event counters, a storage-error flag and
`complete_through_utc_ms`. The latter is the native snapshot time only when the
batch includes **every** event then pending; partial batches send 0. A successfully
accepted full snapshot advances the collection's completion watermark. The
analyzer requires this watermark to cover the response window before treating an
unobserved click as an eligible nonresponse; server receipt time alone is insufficient.
Legacy clients may omit the field, and older databases migrate it to 0 rather than
inventing a completion boundary.
Empty batches report synchronization/quality without inventing a behavioral event.
`measurements` and `measurement_quality` tables are created on service startup;
participant deletion and 90-day retention include both tables. Deploy this server
change before the app; a prior server's 404 leaves the native outbox pending.

Notification rows record due/planned time, accepted-post time, click time, policy
and clock metadata. Activity rows contain catalog ID, pseudonymous session key
and selected/started/completed/ended timestamps. Raw notes, health values and
unrecognized fields are rejected. The operator must confirm the written scope before provisioning registration.
`app_session_started` adds a random foreground-session key and native timestamp,
elapsed clock and boot count. Its activity/notification IDs are empty and its
policy/reason are `none`; it contains no hardware identifier, screen text or
health value. A session starts at process launch or return after at least 30
seconds in the background. This is an observed app visit, not proof of attention.
The Android event is subject to the same enrollment, outbox, expiry and quality
contract as the other measurements.
The valid registered LIVE app activates this producer without additional UI; it
preserves any existing measurement boundary and starts new measurements at first
activation. The retained `consent_version` values describe existing wire contracts,
not a new in-app procedure.

Read [implementation, evaluation rules and remaining beta operating steps](../docs/QA_BETA_MEASUREMENT_PLAN.md).
Run `python3 -m server.analyze_measurements --help` for the read-only export tool.
The report's `app_sessions` section deduplicates foreground-session keys and
reports D7/D14 return in elapsed 24-hour windows from the **first observed** visit.
It excludes unfinished, incomplete and unconfirmed follow-up from rate
denominators, and keeps synthetic/live datasets separate. These are not install
cohorts or clinical-effect estimates. The first-day launch-to-activity interval
is a wall-clock temporal association; without an explicit task/session join and
clock validation it must not be labeled an exact usability task duration.
Real notification delivery, Unity upload and deletion were validated on a device
for the prior in-app-consent baseline. The written-consent activation change passed its current-source Editor network
checks (18 client and 2 server checks with cleanup), plus unregistered/registered
settings validation. A newly built Android product APK then passed 12 device
checks: unregistered collection/export off, provisioned activation without UI,
actual Unity HTTP quality heartbeat and selected-activity delivery, preserved
activation boundary/collection after restart, operator API deletion/revocation,
and original-state/display restoration. The local QA server was stopped and
reverse forwarding removed. These are temporary QA registrations, not a beta
cohort or evidence of clinical or personalization benefit.
See the linked plan for the factual written-notice draft, including recommendation
features, local-only health/free-text data, retention limits and operator fields
that have not yet been assigned.
