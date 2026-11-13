"""Version-2 opt-in measurements, separate from recommendation model features."""
import json
import re

NOTIFICATION_KINDS = {"notification_due", "notification_post_accepted", "notification_post_recovered",
                      "notification_blocked", "notification_failed", "notification_opened"}
ACTIVITY_KINDS = {"activity_selected", "activity_started", "activity_completed", "activity_ended"}
SESSION_KINDS = {"app_session_started"}
FOCUS_KINDS = {"focus_recommendation_shown", "focus_recommendation_selected", "focus_recommendation_started",
               "focus_recommendation_completed", "focus_recommendation_ended"}
FOCUS_FIELDS = {"decision_id", "recommended_activity_id"}
FOCUS_POLICIES = {"focus-additional-history-v1", "focus-additional-history-preference-v2"}
FOCUS_REASONS = {"saved-preference", "participation-history", "catalog-default-no-history", "resume-active-activity",
                 "focus-method-preference", "focus-frequency-more", "focus-frequency-less"}
FOCUS_ACTIVITY = r"free-mission:M(?:0[1-9]|1[0-7])"
FIELDS = {"event_id", "notification_id", "session_key", "kind", "occurred_utc_ms", "elapsed_ms", "boot_count",
          "utc_offset_minutes", "scheduled_utc_ms", "policy_id", "preferred_minute", "effective_minute",
          "learned_samples", "reason", "activity_id", "app_version", "dataset"}
REASONS = {"none", "permission_required", "permission_denied", "channel_disabled", "notify_exception", "active_receipt"}


def identifier(value):
    return isinstance(value, str) and re.fullmatch(r"[a-f0-9]{32}", value) is not None


def integer(value, low, high):
    return type(value) is int and low <= value <= high


def validate_base(event, now):
    if not isinstance(event, dict) or set(event) not in (FIELDS, FIELDS | FOCUS_FIELDS):
        return False
    if not identifier(event["event_id"]) or not isinstance(event["kind"], str) or event["kind"] not in NOTIFICATION_KINDS | ACTIVITY_KINDS | FOCUS_KINDS | SESSION_KINDS:
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
    if not isinstance(event["dataset"], str) or not isinstance(event["reason"], str) or event["dataset"] not in ("live", "synthetic") or event["reason"] not in (REASONS | FOCUS_REASONS):
        return False
    if event["kind"] in FOCUS_KINDS:
        if (set(event) != FIELDS | FOCUS_FIELDS or not identifier(event["decision_id"])
                or event["notification_id"] != "" or event["policy_id"] not in FOCUS_POLICIES
                or event["reason"] not in FOCUS_REASONS or event["elapsed_ms"] != -1 or event["boot_count"] != -1
                or event["scheduled_utc_ms"] != 0 or event["preferred_minute"] != -1
                or event["effective_minute"] != -1 or event["learned_samples"] != 0
                or any(not isinstance(event[k], str) or not re.fullmatch(FOCUS_ACTIVITY, event[k])
                       for k in ("recommended_activity_id", "activity_id"))):
            return False
        return (event["session_key"] == "" and event["activity_id"] == event["recommended_activity_id"]
                if event["kind"] == "focus_recommendation_shown" else identifier(event["session_key"]))
    if set(event) != FIELDS or event["reason"] not in REASONS:
        return False
    if event["kind"] in SESSION_KINDS:
        return (identifier(event["session_key"]) and event["notification_id"] == event["activity_id"] == ""
                and event["policy_id"] == event["reason"] == "none" and event["scheduled_utc_ms"] == 0
                and event["preferred_minute"] == event["effective_minute"] == -1
                and event["learned_samples"] == 0 and event["elapsed_ms"] >= 0)
    if event["kind"] in NOTIFICATION_KINDS:
        return identifier(event["notification_id"]) and event["session_key"] == event["activity_id"] == ""
    return (identifier(event["session_key"]) and event["notification_id"] == ""
            and isinstance(event["activity_id"], str)
            and re.fullmatch(r"(?:course:MBCT(?:2W|4W)?[0-9]{2}|course:P(?:0[1-9]|1[0-9]|2[0-8])|free-mission:M(?:0[1-9]|1[0-7]))", event["activity_id"]) is not None
            and event["elapsed_ms"] == event["boot_count"] == -1)


TELEMETRY_FIELDS = {"version", "classified", "qa", "debug", "visit_id", "source_notification_id", "build_id", "os_api"}
ACTIVITY_DETAILS = {"content_version", "role", "entry_source", "active_seconds", "recommended_seconds", "completion_mode", "end_reason", "step_index"}

def valid_telemetry(t):
    return (isinstance(t, dict) and set(t) == TELEMETRY_FIELDS and type(t["version"]) is int and t["version"] == 2
            and all(type(t[k]) is bool for k in ("classified", "qa", "debug"))
            and all(t[k] == "" or identifier(t[k]) for k in ("visit_id", "source_notification_id"))
            and isinstance(t["build_id"], str) and re.fullmatch(r"[a-zA-Z0-9._-]{1,80}", t["build_id"]) is not None
            and integer(t["os_api"], 0, 200))

def valid_details(kind, d, t):
    if kind in ACTIVITY_KINDS | {"practice_step"}:
        return (isinstance(d, dict) and set(d) == ACTIVITY_DETAILS
                and integer(d["content_version"], 0, 1000) and d["role"] in ("basic", "additional", "legacy")
                and d["entry_source"] in ("course", "recommendation", "direct", "review", "alternative", "demo", "legacy")
                and integer(d["active_seconds"], 0, 86400 * 64) and integer(d["recommended_seconds"], 0, 86400 * 64)
                and integer(d["step_index"], -1, 63)
                and d["completion_mode"] in ("none", "normal", "too_long", "ended")
                and d["end_reason"] in ("none", "too_long", "deferred", "not_applicable", "unknown"))
    if kind == "ui_layout":
        if not (t["classified"] and t["qa"] and isinstance(d, dict)
                and set(d) == {"screen_id", "width", "height", "safe_area", "scale_milli", "controls"}
                and isinstance(d["screen_id"], str) and re.fullmatch(r"[A-Za-z0-9._-]{1,64}", d["screen_id"])
                and integer(d["width"], 1, 16384) and integer(d["height"], 1, 16384)
                and integer(d["scale_milli"], 1, 100000) and isinstance(d["safe_area"], list) and len(d["safe_area"]) == 4
                and all(integer(v, 0, 16384) for v in d["safe_area"])
                and isinstance(d["controls"], list) and len(d["controls"]) <= 16): return False
        return all(isinstance(c, dict) and set(c) == {"id", "bounds", "visibility"}
                   and isinstance(c["id"], str) and re.fullmatch(r"[A-Za-z0-9._-]{1,64}", c["id"])
                   and c["visibility"] in ("visible", "partial", "hidden")
                   and isinstance(c["bounds"], list) and len(c["bounds"]) == 4
                   and all(integer(v, -32768, 32768) for v in c["bounds"]) for c in d["controls"])
    return d is None

def validate(event, now):
    if not isinstance(event, dict) or not isinstance(event.get("kind"),str): return False
    extra = set(event) & {"telemetry", "details"}
    if not extra: return validate_base(event, now)
    if "telemetry" not in extra or not valid_telemetry(event["telemetry"]): return False
    base = {k:v for k,v in event.items() if k not in extra}
    kind = event.get("kind")
    if not valid_details(kind, event.get("details"), event["telemetry"]): return False
    if kind == "practice_step": base["kind"] = "activity_started"
    if kind == "ui_layout": base["kind"] = "app_session_started"
    return validate_base(base, now)

def valid_transport(value):
    return (isinstance(value, dict) and set(value) == {"pending_count", "oldest_pending_ms", "retry_count", "last_error", "last_success_utc_ms"}
            and integer(value["pending_count"], 0, 1024) and integer(value["oldest_pending_ms"], 0, 2**63-1)
            and integer(value["retry_count"], 0, 2**63-1) and integer(value["last_success_utc_ms"], 0, 2**63-1)
            and value["last_error"] in ("none", "network", "auth", "server", "client", "ack", "storage"))


def accept(db, subject, body, now, api_error):
    required = {"schema_version", "consent_version", "collection_id", "dropped_attempts", "expired_events", "storage_error", "events"}
    if (not isinstance(body, dict) or set(body) not in (required, required | {"complete_through_utc_ms"}, required | {"complete_through_utc_ms", "transport"}) or type(body["schema_version"]) is not int
            or body["schema_version"] not in (1, 2) or type(body["consent_version"]) is not int or body["consent_version"] != 2
            or not identifier(body["collection_id"]) or not integer(body["dropped_attempts"], 0, 2**63-1)
            or not integer(body["expired_events"], 0, 2**63-1) or type(body["storage_error"]) is not bool
            or not integer(body.get("complete_through_utc_ms", 0), 0, now + 300000)
            or not isinstance(body["events"], list) or not 0 <= len(body["events"]) <= 100):
        raise api_error(400, "measurement_batch")
    if "transport" in body and not valid_transport(body["transport"]):
        raise api_error(400, "measurement_transport")
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
        if not old:
            db.execute("INSERT OR IGNORE INTO measurement_receipts(subject,event_id,received) VALUES (?,?,?)", (subject, event["event_id"], now))
        ids.append(event["event_id"])
    db.execute("""INSERT INTO measurement_quality
        (subject, collection_id, dropped, expired, storage_error, updated, complete_through)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(subject, collection_id) DO UPDATE SET
        dropped=MAX(dropped, excluded.dropped), expired=MAX(expired, excluded.expired),
        storage_error=MAX(storage_error, excluded.storage_error), updated=excluded.updated,
        complete_through=MAX(complete_through, excluded.complete_through)""",
               (subject, body["collection_id"], body["dropped_attempts"], body["expired_events"], int(body["storage_error"]), now,
                min(now, body.get("complete_through_utc_ms", 0))))
    if "transport" in body:
        db.execute("INSERT INTO measurement_transport(subject,collection_id,updated,payload) VALUES (?,?,?,?) ON CONFLICT(subject,collection_id) DO UPDATE SET updated=excluded.updated,payload=excluded.payload",
                   (subject,body["collection_id"],now,json.dumps(body["transport"],sort_keys=True)))
    return {"accepted_event_ids": ids}
