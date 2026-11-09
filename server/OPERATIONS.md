# Isolated QA telemetry service operations

## Verified deployment scope

An isolated QA deployment now runs the server source from commit
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
  state/api/events.sqlite            live database, mode 0600
  state/api/backups/                  private SQLite snapshots
  state/proxy/                        Caddy certificates and ACME state
  state/proxy-config/                 private proxy state
  logs/                              private verification artifacts
  metadata/                          source and download verification manifests
  backups/firewall-before-https/      original firewall evidence
```

The project directory and private subdirectories use mode 0700. Server source,
data and credentials are separate. Credentials are never bundled into an APK,
committed, supplied as command-line arguments, or printed to logs. App enrollment
contains only `endpoint` and `token`; `endpoint` is the HTTPS origin, without a
path, query, fragment or embedded credentials. A separate synthetic device
registration is kept active and was not used in deletion tests.

| Dedicated service | Exposure and limits |
| --- | --- |
| `capstone-telemetry-api.service` | Python standard-library adapter on `127.0.0.1:8091`; CPUQuota 25%, MemoryMax 128 MiB, TasksMax 64 |
| `capstone-telemetry-proxy.service` | Caddy on TCP 80/443; admin API disabled; HTTP/1.1 and HTTP/2; CPUQuota 25%, MemoryMax 192 MiB, TasksMax 64 |

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

Only these four API routes are proxied to the loopback listener: `POST /events/batch`,
`POST /measurements/batch`, `GET /policy/current`, and `DELETE /participant`.
Their contracts are documented in [README.md](README.md#api-contract) and its
measurement section. The proxy limits
request bodies to 131,072 bytes and applies header/body/response timeouts.
The API authenticates requests and independently validates its schema and size.
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

To disable this deployment, stop/disable only these two units. Keep its data,
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

A synthetic online backup passed integrity verification. Recurring scheduling,
backup retention and a full disaster-recovery restore drill are not established
by that result. Back up the enrollment mapping separately with restricted
access; do not mix it into a source archive.

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

## Remaining measurement boundaries

The A7/API29 bootstrap-to-final path has passed with a controlled QA identity:
two events survived an actual Wi-Fi outage and process restart, then uploaded
without duplication; the non-debug final app retained registration and sent a
new event. This verifies that exercised path, not all offline durations or
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
