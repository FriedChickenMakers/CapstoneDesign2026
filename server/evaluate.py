"""Reproducible response-model evaluation, never a clinical outcome estimator.

Labels use a closed 24-hour response window. Features come exclusively from the
recommendation event. Later events provide labels, not updated feature values.
This estimates response among observed choices; it cannot establish the effect
of a different recommendation policy without a prospective controlled study.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sqlite3

from server.service import BASELINE, FEATURES, canonical

WINDOW_MS = 24 * 60 * 60 * 1000


def examples(events, as_of_ms):
    grouped = {}
    for subject, event in events:
        if event["occurred_utc_ms"] <= as_of_ms:
            grouped.setdefault((subject, event["decision_id"]), []).append(event)
    rows = []
    for (subject, _decision), values in sorted(grouped.items()):
        shown = [e for e in values if e["kind"] == "recommendation_shown"]
        # Ambiguous decision identities are excluded, not silently relabeled.
        if len(shown) != 1:
            continue
        first = shown[0]
        when = first["occurred_utc_ms"]
        if when + WINDOW_MS > as_of_ms:
            continue
        selected = any(e["kind"] == "activity_started" and e["activity_id"] == first["activity_id"]
                       and when <= e["occurred_utc_ms"] < when + WINDOW_MS for e in values)
        rows.append({"subject": subject, "time": when, "x": list(first["features"]), "y": int(selected)})
    return rows


def split_temporal(rows):
    groups = {}
    for row in rows:
        groups.setdefault(row["subject"], []).append(row)
    eligible = sorted((row for values in groups.values() if len(values) >= 6 for row in values),
                      key=lambda r: (r["time"], r["subject"]))
    if len(eligible) < 2:
        return [], []
    # A single calendar cutoff prevents later participants' future outcomes
    # from training a model used to predict an earlier participant's past.
    cutoff = eligible[max(1, int(len(eligible) * .75))]["time"]
    train = [r for r in eligible if r["time"] + WINDOW_MS <= cutoff]
    known_subjects = {r["subject"] for r in train}
    # This split tests returning participants; unseen-user generalization has
    # its own participant-disjoint split below.
    test = [r for r in eligible if r["time"] >= cutoff and r["subject"] in known_subjects]
    return train, test


def split_users(rows):
    subjects = sorted({r["subject"] for r in rows}, key=lambda s: hashlib.sha256(s.encode()).hexdigest())
    if len(subjects) < 4:
        return [], []
    held_out = set(subjects[:max(1, len(subjects) // 4)])
    return [r for r in rows if r["subject"] not in held_out], [r for r in rows if r["subject"] in held_out]


def sigmoid(value):
    return 1 / (1 + math.exp(-max(-40, min(40, value))))


def predict(model, features):
    return sigmoid(model[0] + sum(a * b for a, b in zip(model[1:], features)))


def baseline(features):
    return max(.01, min(.99, .15 + .30 * features[0] + .20 * features[1] + .15 * features[2] + .20 * features[3]))


def fit(rows):
    if len(rows) < 20 or {r["y"] for r in rows} != {0, 1}:
        raise ValueError("Insufficient closed observations and both response classes required")
    model = [0.0] * 5
    for _ in range(1200):
        gradient = [0.0] * 5
        for row in rows:
            error = predict(model, row["x"]) - row["y"]
            for i, value in enumerate([1.0] + row["x"]):
                gradient[i] += error * value / len(rows)
        model = [max(-20, min(20, weight - .25 * (gradient[i] + (.03 * weight if i else 0))))
                 for i, weight in enumerate(model)]
    return model


def metrics(rows, predictor):
    probabilities = [predictor(row["x"]) for row in rows]
    n = len(rows)
    return {"rows": n, "start_rate": sum(r["y"] for r in rows) / n,
            "brier": sum((p - r["y"]) ** 2 for p, r in zip(probabilities, rows)) / n,
            "log_loss": -sum(r["y"] * math.log(max(1e-8, p)) + (1 - r["y"]) * math.log(max(1e-8, 1 - p))
                             for p, r in zip(probabilities, rows)) / n}


def evaluate(events, as_of_ms):
    rows = examples(events, as_of_ms)
    report = {"schema_version": 1, "target": "start_within_24h_of_observed_recommendation",
              "as_of_utc_ms": as_of_ms, "observations": len(rows), "participants": len({r["subject"] for r in rows}),
              "feature_names": list(FEATURES), "response_window_ms": WINDOW_MS, "evaluations": {},
              "causal_or_clinical_evidence": False, "deployment_approved": False}
    approved = True
    for name, split in (("same_user_future", split_temporal), ("unseen_users", split_users)):
        train, test = split(rows)
        try:
            model = fit(train)
            if len(test) < 10 or {r["y"] for r in test} != {0, 1}:
                raise ValueError("Insufficient evaluation outcomes")
            base, candidate = metrics(test, baseline), metrics(test, lambda x: predict(model, x))
            report["evaluations"][name] = {"status": "evaluated", "train_rows": len(train), "baseline": base, "candidate": candidate}
            approved = approved and candidate["brier"] < base["brier"] and candidate["log_loss"] < base["log_loss"]
        except ValueError:
            approved = False
            report["evaluations"][name] = {"status": "insufficient_data", "train_rows": len(train), "test_rows": len(test)}
    policy = dict(BASELINE)
    if approved:
        model = fit(rows)
        digest = hashlib.sha256(canonical(sorted(rows, key=lambda r: (r["subject"], r["time"]))).encode()).hexdigest()[:12]
        policy.update(policy_id="response-v1-" + digest, model_type="logistic", weights=model[1:], intercept=model[0],
                      expires_utc_ms=as_of_ms + 30 * WINDOW_MS, training_rows=len(rows))
    report["candidate_passes_offline_gate"] = approved
    # Publishing the candidate requires separate review; this command never
    # overwrites the API's policy path and never transmits observations.
    return report, policy


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--as-of-ms", required=True, type=int)
    args = parser.parse_args()
    with sqlite3.connect(args.database.resolve().as_uri() + "?mode=ro", uri=True) as db:
        events = [(subject, json.loads(payload)) for subject, payload in db.execute("SELECT subject,payload FROM events")]
    report, policy = evaluate(events, args.as_of_ms)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / "evaluation.json").write_text(json.dumps(report, indent=2) + "\n")
    (args.output_dir / "policy-candidate.json").write_text(json.dumps(policy, indent=2) + "\n")
    print("Evaluation written. Candidate is not automatically deployed.")


if __name__ == "__main__":
    main()
