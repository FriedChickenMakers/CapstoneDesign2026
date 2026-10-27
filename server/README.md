# Optional recommendation research service

This directory contains the authenticated event API and an offline response-model
evaluation command. The Android app remains usable without either service. A
missing, invalid or expired model falls back to the local recommendation rules;
the required course order, rewards and garden state stay local.

The repository does **not** deploy a public service, provision participant
credentials, enable collection by default, or demonstrate that a learned policy
outperforms the baseline with real participants. Synthetic tests verify the
implementation. A real deployment and prospective evaluation are separate work.

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

App enrollment is provisioned separately as `personalization-enrollment.json` in
the Android application's private files directory. Its only fields are
`endpoint` and `token`. The endpoint is an HTTPS origin without a path, query,
fragment or embedded credentials. Debug builds permit HTTP only for loopback
development. The app's explicit participation consent remains necessary after
provisioning. A token is never bundled in the APK.

For any remote deployment, terminate TLS in a reviewed service configuration,
keep the Python listener private, and configure storage and backup access before
enrolling participants. The standard-library HTTP adapter is a development
server. It intentionally suppresses request logging. Reverse proxies must also
avoid logging bearer headers or behavioral request bodies.

## API contract

All routes require `Authorization: Bearer <operator-provisioned token>`.
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

Deletion removes that participant's event rows and keeps a minimal revocation
tombstone. This prevents a delayed upload from recreating withdrawn records.
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

## Additional QA/beta measurements (explicit consent 2)

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
unrecognized fields are rejected. Existing consent does not auto-enable the new
producer; the app asks existing participants to explicitly accept the extra scope.

Read [implementation, evaluation rules and remaining beta operating steps](../docs/QA_BETA_MEASUREMENT_PLAN.md).
Run `python3 -m server.analyze_measurements --help` for the read-only export tool.
Native notification delivery and clicks still require real-device validation;
server tests, a compiled APK and synthetic plots do not establish that gate.
