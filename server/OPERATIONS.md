# Isolated QA telemetry service operations

## Extended telemetry deployed on 2026-11-14 KST

The final source release `qa-debug-20261114-v3` accepts both legacy measurement
batches and immutable QA/context extensions. API and maintenance working directories
were switched through dedicated `zz-debug-telemetry.conf` drop-ins. Existing proxy,
firewall, registration keys, manual mappings and participant rows were preserved.
An online SQLite backup, integrity/count evidence and original drop-ins were saved
privately before switching. Rollback removes only this operation's drop-ins,
reloads systemd and restarts the API; never overwrite the current database with the
old snapshot.

The final ARM64/Python 3.8 source passed **97 server tests**. External HTTPS passed
**14 synthetic checks** covering v1/v2 acceptance, retry identity, frozen QA,
unchanged first receipt, transport storage and withdrawal cleanup. Synthetic
measurements and receipt/transport rows were removed afterward. API/proxy and the
maintenance timer remained active/enabled; database integrity was `ok`. Existing
measurements and behavior rows were unchanged by the smoke test. These checks are
server evidence; Android installation and real-device collection are separate.

Historic observations without a stored receipt time remain unknown, including on
replay. New observations receive an immutable first server receipt timestamp.
See [debug and measurement behavior](../docs/DEBUG_TELEMETRY_20261114.md).

## Automatic registration deployed on 2026-11-10

The frozen source release `qa-auto-20261110-7249cb5ad7c1` now serves
`POST /enrollments` and persistent hashed-token registration. Its identifier is
a source manifest, not a Git commit. Existing manual S24/A7 mappings are
byte-for-byte unchanged. See [the API contract](README.md#automatic-install-registration)
for fields, limits, idempotence and withdrawal behavior.

`CAPSTONE_ENROLLMENT_KEY_FILE` points to a versioned owner-private 0600 text file
in the existing secrets directory. The key was transferred through SSH stdin,
never command arguments or logs. Only the exact `/enrollments` proxy route was
added; existing routes and logging restrictions were retained. The API admits
only POST on this route and caps its body at 1,024 bytes. The candidate proxy
configuration passed validation before restarting the dedicated proxy. API and
maintenance use separate dedicated drop-ins for the new working directory.
Minecraft, host services, manual enrollment files and participant identities
were not changed.

The migration adds `automatic_enrollments` (token hash → random subject) and
`revoked_enrollment_tokens` (minimal withdrawal hash tombstones). Keep existing
manual mappings during migration so earlier manual withdrawals can acquire
their token-hash tombstone. Back up the live database online before switching
source. A rollback to older source would not authenticate new automatic
registrations; leave their queues pending and restore the compatible release
rather than creating new participant identities. Do not remove the new tables
or revocation records to make a rollback appear successful.

Local and actual remote ARM64/Python 3.8 validation pass **89 server tests**, including 21 automatic
registration cases: same-token retry/restart/concurrency, global new-registration
budgets, manual identity preservation, withdrawn-token resurrection prevention,
offline-created event replay, key-file privacy and HTTP validation. An
offline-first installation can finish withdrawal before enrollment through
the gated DELETE contract without uploading data; failure injection verifies
identity and tombstone rollback together, and concurrent enrollment/withdrawal
ends with one revoked identity. Existing registered-token deletion remains
independent of distribution-key rotation. External HTTPS synthetic verification
then passed **31 checks**: new/duplicate registration, historical event replay,
offline pre-registration withdrawal, late registration rejection, and stable
identity/metadata after a dedicated API restart. Existing A7/S24 bearer policy
requests still returned 200. Synthetic test registrations were withdrawn and
their data erased. No device-probe token was generated or pre-registered by the
server operator.

A final read-only check passed **17 checks**: API/proxy/timer active and enabled,
startup links present, maintenance's last run successful, current source paths,
live database integrity, three private valid managed backups, unchanged manual
mapping hash and an API listener restricted to `127.0.0.1:8091`. Host reboot was
not performed. These server checks do not establish a new Android APK result.
Private logs/manifests are under `artifacts/qa-server-autoenrollment-20261110/`
and `artifacts/qa-server-autoenrollment-deploy-20261110/`.

Before the switch, an online SQLite backup and current proxy/unit snapshots
were stored privately. `deployment-result-private.json` identifies that remote
backup directory and its `rollback-private.json`. Rollback must restore only
this operation's API/maintenance drop-ins and proxy config after checking they
have not changed again; it must **not overwrite the current database** with the
pre-deployment snapshot. The old source cannot serve newly registered automatic
tokens, so prefer a compatible forward correction while their client queues are
preserved. No rollback was needed or exercised during this successful rollout.

## Earlier reliability baseline evidence

The earlier 2026-11-10 reliability update was deployed as the immutable source release
`qa-20261110-7ee71ee847f6`. This name identifies a SHA-256 manifest of the tested
working-tree Python files; it is **not a Git commit**. Current changes remain
local until publication is explicitly requested. Existing enrollment mappings,
database rows, proxy configuration and unrelated host services were preserved.

| Earlier baseline check | Result and boundary |
| --- | --- |
| Local Python and actual remote ARM64 Python 3.8 | 68 tests PASS on the final source: schema/idempotence, connection limits, stalled headers, app visits, incomplete follow-up, snapshot retention and withdrawal scrub |
| External HTTPS and operational recovery | 27 checks PASS on the first reliability release: invalid/missing tokens, timestamp/session upload, duplicate retry, outage/no false ACK, unchanged replay, unexpected API process death/automatic recovery, persistent DB and backup scrub |
| Final release after review corrections | 9 checks PASS: HTTPS auth, fresh A7 registration, exact old-batch replay, withdrawal, row counts, active/enabled services and final source identity |
| Enrollment | New random QA participant ID/token for the fresh A7 installation; old registrations preserved. Those manual-build APKs contained no enrollment credential |
| Scheduled maintenance | Hourly timer active and enabled; one private online SQLite snapshot per UTC day, seven daily snapshots, withdrawal/90-day-expiry scrub on every run |
| Host reboot / real participant results | Host reboot not performed on the shared machine. Enabled startup and dedicated API crash recovery were verified; no participant or clinical outcome is claimed |

The final review fixed a capacity-test race and added a bounded half-close/drain
after an overload 503. A client must retain every unacknowledged event even if a
connection reset or proxy 502 hides the original 503. Oversized HTTPS bodies may
receive HTTP 413 or an early TLS close while the client is still writing; neither
path acknowledges events.

Private manifests, credentials and detailed evidence are in
`artifacts/qa-server-20261110/`. The private `a7_status.py` operator utility prints
only counts/kinds/quality; it stores event IDs and timestamps in a private JSON
file for comparison with the device outbox. Do not publish that directory.

The earlier 2026-11-09 deployment used server source from commit
`17c19788d7d82281e5cdffef886c353245b81beb`. It uses synthetic QA registrations;
no real participant was enrolled during deployment verification.

| Check | Result and boundary |
| --- | --- |
| Python 3.8.10 / SQLite 3.31.1 on the remote ARM64 host | 58 server tests PASS on the committed `server/` source |
| Actual external HTTPS | 18 checks PASS: certificate/hostname validation, authentication, batch ACK, duplicate retry, invalid inputs and withdrawal |
| Database, dedicated API restart and online backup | 10 checks PASS: intended row counts, deletion/tombstone preservation, private file modes, backup integrity and no registered bearer tokens in service logs |
| External HTTPS after API restart | 4 checks PASS: duplicate ACK, withdrawn registration rejection and a separate active device QA registration |
| Actual A7/API29 app → external HTTPS | Bootstrap904: two UI events survived Wi-Fi outage and process restart unchanged, then uploaded once and drained from the outbox. Non-debug905 and906: same enrollment preserved; 906 produced three new selected/started/ended events received by the server. Controlled QA identity only; UI release status is separate |
| Real participants / randomized A/B | Not performed |

Private evidence is under
`artifacts/qa-release-20261109/server-preflight/`.
It contains deployment locations, exact file inventories and operator credentials;
do not publish or attach that directory wholesale. The tests use different units
and must not be added together as a participant count or an effect estimate.

## Layout and isolation

Use placeholders below for the operator's private deployment values:

```text
<PROJECT_ROOT>/
  releases/<RELEASE_SHA>/server/       immutable application source and tests
  bin/caddy-<VERSION>                 verified ARM64 executable
  licenses/                          retained third-party license
  config/Caddyfile                    one exact HTTPS host; no bearer values
  config/systemd/                     source copies of the two dedicated units
  secrets/enrollments.json            operator-managed mapping, mode 0600
  secrets/enrollment-key-<RELEASE>.txt shared registration gate, mode 0600
  state/api/events.sqlite            live database, mode 0600
  state/api/backups/                  private SQLite snapshots
  state/proxy/                        Caddy certificates and ACME state
  state/proxy-config/                 private proxy state
  logs/                              private verification artifacts
  metadata/                          source and download verification manifests
  backups/firewall-before-https/      original firewall evidence
```

The project directory and private subdirectories use mode 0700. Server source,
data and credentials are separate. Server credentials and individual participant
bearers are never bundled into an APK. The new automatic-registration build does
contain a shared distribution key, which provides a minimal enrollment gate and
can be extracted from the APK; it is not a participant bearer or strong device
authentication. Secrets are not committed, supplied as command-line arguments,
or printed to logs. Manual app enrollment contains `endpoint` and `token`;
automatic enrollment also retains its distribution key privately for registration
retries. `endpoint` is the HTTPS origin, without a path, query, fragment or embedded
credentials. Existing synthetic device registrations remain active and were not
used in deletion tests.

| Dedicated service | Exposure and limits |
| --- | --- |
| `capstone-telemetry-api.service` | Python standard-library adapter on `127.0.0.1:8091`; CPUQuota 25%, MemoryMax 128 MiB, TasksMax 64 |
| `capstone-telemetry-proxy.service` | Caddy on TCP 80/443; admin API disabled; HTTP/1.1 and HTTP/2; CPUQuota 25%, MemoryMax 192 MiB, TasksMax 64 |
| `capstone-telemetry-maintenance.timer` / `.service` | Hourly persistent timer, at most two minutes randomized delay; isolated Python online backup/scrub, no network access required; CPUQuota 25%, MemoryMax 128 MiB, TasksMax 8 |

Both units are enabled, run as the designated non-root server account, use
UMask 0077, `NoNewPrivileges`, a private temporary directory and filesystem
write restrictions. The proxy receives only `CAP_NET_BIND_SERVICE`; the API
has no capabilities. Only project state directories are writable by the units.
These are conservative configured limits, not measured production capacity.

No global packages were installed. Existing services and the shared Minecraft
configuration were preserved. Lack of an observed game process is not permission
to change or remove its configuration.

## Proxy, TLS and request logging

The deployment uses [Caddy v2.11.9](https://github.com/caddyserver/caddy/releases/tag/v2.11.9),
released on 2026-11-05, under its
[Apache-2.0 license](https://github.com/caddyserver/caddy/blob/v2.11.9/LICENSE).
The official ARM64 archive matched both the release checksum file's SHA-512 and
the GitHub release asset's SHA-256. The transferred executable also matched its
recorded binary hash. This is checksum verification, not a claim that a detached
signature was independently verified.

The executable stays inside the project; dedicated units adapt the
[official systemd guidance](https://caddyserver.com/docs/running#linux-service).
Public certificate issuance and renewal follow the
[automatic HTTPS requirements](https://caddyserver.com/docs/automatic-https),
including reachable DNS and ports. Certificate/ACME files remain private and
persistent. No private key is copied into the repository or operator reports.

Only the exact API paths are proxied to the loopback listener: `POST /events/batch`,
`POST /measurements/batch`, `GET /policy/current`, `DELETE /participant`, and the
new gated `POST /enrollments`.
Their contracts are documented in [README.md](README.md#api-contract) and its
measurement section. The proxy limits
request bodies to 131,072 bytes and applies header/body/response timeouts.
The API authenticates requests and independently validates its schema and size.
It admits at most 16 concurrent request handlers, applies a ten-second socket
timeout before reading headers, and rejects excess requests with a retryable 503.
The proxy's body/header limits remain an additional boundary. These limits
reduce resource exhaustion; they are not a distributed denial-of-service service.
Never expose port 8091 publicly or disable certificate validation to make a test
pass. This remains a limited QA deployment of the standard-library adapter;
large-scale load capacity has not been established.

API request logging is suppressed. Caddy access logging is not enabled; its
warning/error encoder removes the request object. Bearer headers and behavioral
bodies must not be added to logs during troubleshooting. Deployment checks found
no registered bearer values in the two services' journal output. This check does
not authorize sharing unreviewed logs.

## Firewall change and precise rollback

The host already used `netfilter-persistent` and `/etc/iptables/rules.v4`.
Complete original runtime rules and the original persistent file were privately
backed up. Exactly one IPv4 INPUT rule was added immediately before the existing
REJECT, with the same one-line addition in the persistent file:

```text
-A INPUT -p tcp -m multiport --dports 80,443 -m comment --comment capstone-telemetry-https -j ACCEPT
```

Before/after comparisons confirmed that SSH, Minecraft, NAT, existing policies
and all other rules were preserved. IPv6 and other tables were not modified.
No firewall flush, whole-file restore, global save, service reload or host reboot
was used. Public HTTPS subsequently succeeded; no cloud firewall change was made.

Rollback must take a fresh backup, delete only that exact commented runtime rule
and remove only its exact line from the current persistent file. Compare both
diffs before accepting the result. Preserve any other changes made since
deployment. Do not replace the current rules with the old full backup, flush
INPUT, disable a firewall, or modify the Minecraft rules.

## Status, updates and restart

Inspect only the dedicated services:

```sh
sudo systemctl status capstone-telemetry-api.service capstone-telemetry-proxy.service
sudo journalctl -u capstone-telemetry-api.service -u capstone-telemetry-proxy.service --since '-15 minutes'
```

Keep journal output private. An unauthenticated HTTPS request to
`<HTTPS_ORIGIN>/policy/current` should return 401 with a valid certificate. This
proves reachability and authentication enforcement, not successful collection.
For upload checks, load an authorized synthetic token from a private file inside
the test process; never place it in shell arguments or print it.

Deploy new application code into a new `releases/<RELEASE_SHA>` directory and run
the server tests there before changing the dedicated API unit's working
directory. Preserve the previous release and database. Do not overwrite a shared
checkout or run host-wide package upgrades as part of a release.

An enrollment-file change requires an operator-reviewed atomic replacement that
preserves existing mappings, mode 0600 and owner, followed by restarting only
`capstone-telemetry-api.service`; mappings are read at process startup. The
deployment verified this API's restart with the same persistent database.
Proxy configuration changes require validation before restarting only the
dedicated proxy; its admin API is deliberately disabled. Automatic service
startup is configured, but a host reboot was not performed as a test.

`Restart=on-failure` recovers an unexpected API/proxy process exit after five
seconds. An intentional `systemctl stop` does **not** automatically restart a
unit. For a controlled outage, arm a separate server-side restore timer before
stopping only the API; the operator's SSH session must not be the sole restore
mechanism. The private QA helper `api_outage.py` prepares this path without an
outage, and its explicit `start` action schedules restoration after 45 seconds
with a one-second timer accuracy before stopping the API. Its `status`/`restore`
actions keep a durable private journal. This is an OS timer, not a hard real-time
guarantee when the host itself is frozen or unavailable. No host reboot or
Minecraft interruption is needed for this test.

Boot startup was checked through enabled unit configuration, persistent storage
paths and current service readiness. Only dedicated-process crash recovery has
been exercised. Network loss, a cloud instance stop/reclamation, storage failure
or expired/invalid credentials can still prevent ingestion; unit enablement is
not a guarantee of uninterrupted availability.

To disable this deployment, stop/disable only the API/proxy and the dedicated
maintenance timer/service. Keep its data,
credentials, certificates and backups pending the agreed retention/deletion
procedure. Do not recursively delete the project or restart unrelated services.

## Backup, recovery and withdrawal

Use SQLite's online backup API while the service is running; copying a live
database file directly can omit committed journal/WAL state. Run as the service
account, use a new destination, and keep the result private. For example:

```sh
umask 077
python3 - '<PROJECT_ROOT>/state/api/events.sqlite' '<NEW_PRIVATE_BACKUP>.sqlite' <<'PY'
from pathlib import Path
import sqlite3
import sys

source, destination = map(Path, sys.argv[1:])
destination.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
destination.touch(mode=0o600, exist_ok=False)
with sqlite3.connect(source.resolve().as_uri() + '?mode=ro', uri=True) as live:
    with sqlite3.connect(str(destination)) as backup:
        live.backup(backup)
        assert backup.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
PY
```

The managed backup job uses the same SQLite online backup API. The hourly
`capstone-telemetry-maintenance.timer` is persistent across missed runs and calls
`python3 -B -m server.maintenance` using private `CAPSTONE_EVENTS_DB` and
`CAPSTONE_BACKUP_DIR` configuration. It keeps one snapshot per UTC day for seven
days. Each run scrubs current revocations and expired rows from **all managed
snapshots**, preserving a union of known revocation tombstones even after an
accidental rollback of the live database. Run the maintenance service immediately
after a withdrawal to verify backup deletion, rather than waiting up to the next
hourly run. A stopped host catches up when the persistent timer resumes.

Only exact `qa-daily-YYYYMMDD.sqlite` files in the designated managed directory
are touched. Earlier operator snapshots, exports and enrollment backups still
need explicit operator reconciliation. The synthetic test verified an online
snapshot containing a subject, withdrawal, and subsequent removal from every
managed snapshot with integrity and mode-0600 checks. A full disaster-recovery
restore drill and off-host backup destination are not established by these tests.
Back up enrollment mappings separately with restricted access; do not mix them
into a source archive.

For recovery, stop only the API, preserve the current database, validate the
selected backup and reconcile current enrollments plus all subsequent
revocations before resuming. **Restoring an old snapshot must not recreate
withdrawn records or reactivate a revoked registration.** If the current
revocation history is unavailable, do not resume collection from an older
snapshot until the operator resolves it. The API restart test is not a full
disaster-recovery test.

`DELETE /participant` erases that subject's event/measurement records and keeps
a revocation tombstone that rejects late uploads. The deployed synthetic test
checked both behaviors and their survival across API restart. Device collection,
exports and backups require separate shutdown/deletion handling; a server DELETE
does not stop the device or rewrite older snapshots. Follow the written consent
and withdrawal procedure, retain the required revocation state, and do not add
an in-app consent screen as an operational workaround.

## Anonymous registrations and basic ingestion protection

Manual QA uses an operator-generated random UUID subject plus a separate
high-entropy bearer token for each installation/QA participant. Neither is a hardware ID. Provision
the private enrollment into the development bootstrap, then update in place to
the signed non-debuggable final APK. Application restarts and in-place updates
retain the registration; a fresh install needs a newly provisioned registration.
An APK without automatic-registration configuration still requires this operator
step. Automatic QA builds create their install token locally and use the new
registration gate before the receiver accepts queued events.
The server groups records by the token's random subject, not the phone's serial.

HTTPS and registered per-participant tokens are the simple ingestion gate. No RSA
private key or participant bearer token is hardcoded in the application. New
automatic QA builds contain a separate shared distribution key generated from
private build configuration; it is extractable and is not device attestation.
A shared APK private key would likewise not distinguish installations;
operator registration also permits revoking one participant without replacing
everyone's app. This does not prove an enrolled client is untampered or establish
strong device attestation.

## Distribution-key rotation and availability

The shared APK key is a minimal gate for the current controlled QA distribution.
Someone who extracts it can spend the global 240/hour or 1,000/day new-identity
budget, delaying legitimate first registration or first pre-registration
withdrawal. Existing bearer uploads and same-token registration/deletion retries
do not consume that budget. No IP-based participant identity or per-school-NAT
bucket is used, and no strong attestation or attack-proof availability is claimed.

To rotate, generate a new key privately, update the private build configuration,
write a new versioned 0600 server key file and point only the dedicated API's
`CAPSTONE_ENROLLMENT_KEY_FILE` to it. Restart that API and verify both existing
bearers and a new synthetic registration over HTTPS. Preserve the old secret
privately only as needed for controlled rollback; never log or publish either
key. Do not erase identities, events or withdrawal tombstones as a rotation step.

Already registered tokens remain valid. An installation that is still offline
before first registration may have the old distribution key in its private
enrollment file. The current client intentionally preserves existing enrollment
files during APK updates; replacing the APK's configuration alone does not
migrate that stored key. Before disabling the old gate, identify these pending
QA installations and plan an authorized update of only their distribution key,
preserving endpoint, token, collection and queues, or implement/test an explicit
client migration. Clearing app data, silently generating a new token or claiming
that a simple APK update repairs every pending install is not a valid substitute.

The public proxy currently has `read_header 10s`, `read_body 30s`, `write 30s`
and `idle 2m`; [Caddy documents `read_body` as a whole-upload limit](https://caddyserver.com/docs/caddyfile/options#timeouts).
The Python socket's ten-second per-receive timeout is not itself an absolute
request deadline. The checked public deployment also has the proxy deadlines
and an API listener on loopback only. Direct connections by another process on
the shared host remain a separate trust boundary. No new watchdog/deadline
patch or unrelated service change was applied for the review observation.

## Oracle idle-instance constraint

Oracle's [Always Free idle-instance documentation](https://docs.oracle.com/en-us/iaas/Content/FreeTier/freetier_topic-Always_Free_Resources.htm#compute__idleinstances)
describes a seven-day test: CPU 95th-percentile utilization below 20%, network
utilization below 20%, and, for A1 shapes, memory utilization below 20%. It does
not recommend filling 50% of RAM or guarantee retention because an application
allocates that amount. A single OS memory sample is not the provider's seven-day
metric or proof of the account's billing/shape status.

The shared host was lightly loaded at inspection. No dummy memory allocation,
host-wide package change, cloud billing change, Minecraft change or host reboot
was performed. The server owner should review the actual OCI tenancy/shape,
utilization and idle notices; service auto-restart cannot recover a cloud instance
that the provider has stopped or reclaimed. Local device queues cover a temporary
receiver outage only within their documented retention/capacity limits.

## Remaining measurement boundaries

The **2026-11-09 builds 904–906** passed the A7/API29 bootstrap-to-final path with
a controlled QA identity: two events survived an actual Wi-Fi outage and process
restart, then uploaded without duplication; those non-debug final builds retained
registration and sent new events. This is historical evidence for those builds,
**not a pass for the current 2026-11-10 APK**. Current installation, lifecycle,
offline/Doze and server-outage results are recorded separately in
[the current QA release report](../docs/QA_RELEASE_20261110.md).
Do not replace a pending current-device result with an older APK or host test.
The historical test verifies the exercised path, not all offline durations or
participant conditions. No real-participant enrollment, longitudinal collection,
causal notification-optimization result or randomized A/B result is claimed here.
An accepted notification post is not proof that a person saw it.

The native outbox has a 1,024-event cap, 8,192 receipts/90-day limit and 100-event
batch size. Foreground upload normally checks every 60 seconds. The behavior
outbox has 256 events and lacks the native completeness/loss-counter contract.
Clock skew exceeding the server allowance can still reject batches. Queue limits,
clock quality, incomplete uploads and known gaps must accompany any presentation
of response-time or engagement data; a successful synthetic batch does not prove
loss-free collection during prolonged offline use.
