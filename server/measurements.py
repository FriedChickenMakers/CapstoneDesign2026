"""Version-2 opt-in measurements, separate from recommendation model features."""
import json
import re

NOTIFICATION_KINDS = {"notification_due", "notification_post_accepted", "notification_post_recovered",
                      "notification_blocked", "notification_failed", "notification_opened"}
ACTIVITY_KINDS = {"activity_selected", "activity_started", "activity_completed", "activity_ended"}
FIELDS = {"event_id", "notification_id", "session_key", "kind", "occurred_utc_ms", "elapsed_ms", "boot_count",
          "utc_offset_minutes", "scheduled_utc_ms", "policy_id", "preferred_minute", "effective_minute",
          "learned_samples", "reason", "activity_id", "app_version", "dataset"}
REASONS = {"none", "permission_required", "permission_denied", "channel_disabled", "notify_exception", "active_receipt"}


def identifier(value):
    return isinstance(value, str) and re.fullmatch(r"[a-f0-9]{32}", value) is not None


def integer(value, low, high):
    return type(value) is int and low <= value <= high


def validate(event, now):
    if not isinstance(event, dict) or set(event) != FIELDS:
        return False
    if not identifier(event["event_id"]) or not isinstance(event["kind"], str) or event["kind"] not in NOTIFICATION_KINDS | ACTIVITY_KINDS:
        return False
    for field, low, high in (("occurred_utc_ms", now - 90 * 86400000, now + 300000),
                             ("scheduled_utc_ms", 0, now + 86400000), ("elapsed_ms", -1, 2**63-1),
                             ("boot_count", -1, 2**31-1), ("utc_offset_minutes", -840, 840),
                             ("preferred_minute", -1, 1439), ("effective_minute", -1, 1439), ("learned_samples", 0, 1000000)):
        if not integer(event[field], low, high):
            return False
    if any(not isinstance(event[f], str) or not re.fullmatch(r"[A-Za-z0-9._-]{1,48}", event[f])
           for f in ("app_version", "policy_id")):
        return False
    if not isinstance(event["dataset"], str) or not isinstance(event["reason"], str) or event["dataset"] not in ("live", "synthetic") or event["reason"] not in REASONS:
        return False
    if event["kind"] in NOTIFICATION_KINDS:
        return identifier(event["notification_id"]) and event["session_key"] == event["activity_id"] == ""
    return (identifier(event["session_key"]) and event["notification_id"] == ""
            and isinstance(event["activity_id"], str)
            and re.fullmatch(r"(?:course:MBCT(?:2W|4W)?[0-9]{2}|free-mission:M[0-9]{2})", event["activity_id"]) is not None
            and event["elapsed_ms"] == event["boot_count"] == -1)


def accept(db, subject, body, now, api_error):
    required = {"schema_version", "consent_version", "collection_id", "dropped_attempts", "expired_events", "storage_error", "events"}
    if (not isinstance(body, dict) or set(body) != required or type(body["schema_version"]) is not int
            or body["schema_version"] != 1 or type(body["consent_version"]) is not int or body["consent_version"] != 2
            or not identifier(body["collection_id"]) or not integer(body["dropped_attempts"], 0, 2**63-1)
            or not integer(body["expired_events"], 0, 2**63-1) or type(body["storage_error"]) is not bool
            or not isinstance(body["events"], list) or not 0 <= len(body["events"]) <= 100):
        raise api_error(400, "measurement_batch")
    if any(not validate(e, now) for e in body["events"]):
        raise api_error(400, "measurement_event")
    ids = []
    for event in body["events"]:
        payload = json.dumps(event, sort_keys=True, separators=(",", ":"), allow_nan=False)
        old = db.execute("SELECT collection_id,payload FROM measurements WHERE subject=? AND event_id=?", (subject, event["event_id"])).fetchone()
        if old and old != (body["collection_id"], payload):
            raise api_error(409, "measurement_identity_conflict")
        db.execute("INSERT OR IGNORE INTO measurements VALUES (?, ?, ?, ?, ?, ?)",
                   (subject, event["event_id"], body["collection_id"], event["notification_id"], event["occurred_utc_ms"], payload))
        ids.append(event["event_id"])
    db.execute("""INSERT INTO measurement_quality VALUES (?, ?, ?, ?, ?, ?)
        ON CONFLICT(subject, collection_id) DO UPDATE SET
        dropped=MAX(dropped, excluded.dropped), expired=MAX(expired, excluded.expired),
        storage_error=MAX(storage_error, excluded.storage_error), updated=excluded.updated""",
               (subject, body["collection_id"], body["dropped_attempts"], body["expired_events"], int(body["storage_error"]), now))
    return {"accepted_event_ids": ids}
