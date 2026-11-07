"""Private, descriptive QA/beta evidence. No fabricated reduction or causal claim."""
from __future__ import annotations
import argparse
import csv
import json
import math
import os
from pathlib import Path
import sqlite3
import statistics
from urllib.parse import quote

DAY = 86400000
CLOCK_TOLERANCE_MS = 120000


def percentile(values, fraction):
    if not values:
        return None
    values = sorted(values)
    index = (len(values) - 1) * fraction
    low = math.floor(index)
    return values[low] + (values[math.ceil(index)] - values[low]) * (index - low)


def analyze_focus_recommendations(records, quality, as_of, dataset, window=DAY):
    """Observed funnel only. Missing exposure/upload cannot become a rejected suggestion."""
    groups = {}
    for subject, collection, event in records:
        if (event.get("kind", "").startswith("focus_recommendation_") and event["dataset"] == dataset
                and as_of - 90*DAY <= event["occurred_utc_ms"] <= as_of):
            groups.setdefault((subject, collection, event["decision_id"]), []).append(event)
    rows = []
    for (subject, collection, decision), events in sorted(groups.items()):
        ordered = sorted(events, key=lambda e: e["occurred_utc_ms"])
        first = ordered[0]
        shown = [e for e in ordered if e["kind"] == "focus_recommendation_shown"]
        selected = [e for e in ordered if e["kind"] == "focus_recommendation_selected"]
        q = quality.get((subject, collection), {})
        row = dict(subject=subject, decision_id=decision, dataset=dataset, policy_id=first["policy_id"], reason=first["reason"],
                   recommended_activity_id=first["recommended_activity_id"], shown_utc_ms=shown[0]["occurred_utc_ms"] if shown else None,
                   selected_utc_ms=None, started_utc_ms=None, completed_utc_ms=None, ended_utc_ms=None,
                   selected_activity_id=None, selection_matches_recommendation=False,
                   collection_incomplete=bool(q.get("dropped", 0) or q.get("expired", 0) or q.get("storage_error", 0)),
                   complete_through_utc_ms=q.get("complete_through", 0), eligible=False, outcome="missing_exposure")
        if len(shown) > 1 or len(selected) > 1:
            row["outcome"] = "ambiguous_lifecycle"
        elif len({(e["policy_id"], e["reason"], e["recommended_activity_id"]) for e in ordered}) != 1:
            row["outcome"] = "inconsistent_snapshot"
        elif shown:
            at = shown[0]["occurred_utc_ms"]
            end = at + window
            row["outcome"] = "no_observed_selection"
            valid = True
            if selected:
                choice = selected[0]
                row["selected_utc_ms"] = choice["occurred_utc_ms"]
                row["selected_activity_id"] = choice["activity_id"]
                row["selection_matches_recommendation"] = choice["activity_id"] == first["recommended_activity_id"]
                # A valid choice after the response window is a nonresponse within
                # that window, not a reason to discard the original exposure.
                valid = at <= choice["occurred_utc_ms"]
                associated = [e for e in ordered if e["kind"] != "focus_recommendation_shown"]
                valid = valid and all(e["session_key"] == choice["session_key"] and e["activity_id"] == choice["activity_id"] for e in associated)
                for suffix in ("started", "completed", "ended"):
                    items = [e for e in associated if e["kind"] == "focus_recommendation_" + suffix]
                    if len(items) > 1:
                        valid = False
                    if items:
                        row[suffix + "_utc_ms"] = items[0]["occurred_utc_ms"]
                        valid = valid and items[0]["occurred_utc_ms"] >= choice["occurred_utc_ms"]
                if row["completed_utc_ms"] is not None:
                    valid = valid and row["started_utc_ms"] is not None and row["completed_utc_ms"] >= row["started_utc_ms"]
                if row["ended_utc_ms"] is not None and row["started_utc_ms"] is not None:
                    valid = valid and row["ended_utc_ms"] >= row["started_utc_ms"]
                if row["completed_utc_ms"] is not None and row["ended_utc_ms"] is not None:
                    valid = False
                row["outcome"] = ("recommended_selected" if row["selection_matches_recommendation"] else "different_activity_selected")
                if choice["occurred_utc_ms"] >= end:
                    row["outcome"] = "late_selection"
            elif any(e["kind"] != "focus_recommendation_shown" for e in ordered):
                valid = False
            if not valid:
                row["outcome"] = "incomplete_or_invalid_lifecycle"
            elif end > as_of:
                row["outcome"] = "window_open"
            elif row["collection_incomplete"]:
                row["outcome"] = "collection_incomplete"
            elif row["complete_through_utc_ms"] < end:
                row["outcome"] = "awaiting_upload_confirmation"
            else:
                row["eligible"] = True
        rows.append(row)
    policies = {}
    for policy in sorted({r["policy_id"] for r in rows}):
        subset = [r for r in rows if r["policy_id"] == policy]
        eligible = [r for r in subset if r["eligible"]]
        selected_in_window = [r for r in eligible if r["selected_utc_ms"] is not None
                              and r["selected_utc_ms"] < r["shown_utc_ms"] + window]
        chosen = [r for r in selected_in_window if r["selection_matches_recommendation"]]
        completed = [r for r in chosen if r["completed_utc_ms"] is not None and r["completed_utc_ms"] < r["shown_utc_ms"] + window]
        started = [r for r in chosen if r["started_utc_ms"] is not None and r["started_utc_ms"] < r["shown_utc_ms"] + window]
        policies[policy] = dict(observed_decisions=len(subset), closed_complete_exposures=len(eligible),
                               recommended_selected=len(chosen), recommended_started=len(started), recommended_completed=len(completed),
                               other_activity_selected=sum(not r["selection_matches_recommendation"] for r in selected_in_window),
                               selection_rate=len(chosen)/len(eligible) if eligible else None,
                               completion_per_exposure=len(completed)/len(eligible) if eligible else None,
                               outcomes={name: sum(r["outcome"] == name for r in subset) for name in sorted({r["outcome"] for r in subset})})
    return dict(response_window_ms=window, policies=policies, decisions=rows,
                causal_or_clinical_evidence=False,
                limitation="Version groups are descriptive. Missing exposure, partial upload, clock/lifecycle inconsistency and collection loss are excluded from rates; direct starts are unattributed.")


def analyze(records, quality, as_of, dataset, window=DAY):
    records = list(records)
    groups, sessions = {}, {}
    for subject, collection, e in records:
        if e["dataset"] != dataset or not as_of - 90*DAY <= e["occurred_utc_ms"] <= as_of:
            continue
        if e["notification_id"]:
            groups.setdefault((subject, collection, e["notification_id"]), []).append(e)
        elif e["session_key"] and e["kind"] in ("activity_selected", "activity_started", "activity_completed", "activity_ended"):
            sessions.setdefault((subject, e["session_key"]), []).append(e)
    rows = []
    for (subject, collection, nid), values in sorted(groups.items()):
        posts = [e for e in values if e["kind"] == "notification_post_accepted"]
        opened = sorted((e for e in values if e["kind"] == "notification_opened"), key=lambda e:e["occurred_utc_ms"])
        first = min(values, key=lambda e:e["occurred_utc_ms"])
        q = quality.get((subject, collection), {})
        row = {"subject": subject, "notification_id": nid, "dataset": dataset, "policy_id": first["policy_id"],
               "scheduled_utc_ms": first["scheduled_utc_ms"], "posted_utc_ms": None,
               "preferred_minute":first["preferred_minute"], "effective_minute":first["effective_minute"],
               "utc_offset_minutes":first["utc_offset_minutes"],
               "observed_reasons":";".join(sorted({e["reason"] for e in values if e["reason"]!="none"})),
               "opened_utc_ms": opened[0]["occurred_utc_ms"] if opened else None,
               "posting_delay_ms": None, "click_delay_ms": None, "wall_click_delay_ms": None,
               "clock_quality": "unknown", "outcome": "unverified_post", "learned_samples": first["learned_samples"],
               "collection_incomplete": bool(q.get("dropped", 0) or q.get("expired", 0) or q.get("storage_error", 0)),
               "last_upload_utc_ms": q.get("updated", 0), "complete_through_utc_ms":q.get("complete_through", 0),
               "recovered_post": any(e["kind"]=="notification_post_recovered" for e in values)}
        if len(posts) == 1:
            post = posts[0]; at = post["occurred_utc_ms"]; row["posted_utc_ms"] = at
            if post["scheduled_utc_ms"]:
                row["posting_delay_ms"] = at - post["scheduled_utc_ms"]
            if opened:
                click = opened[0]; wall = click["occurred_utc_ms"] - at; row["wall_click_delay_ms"] = wall
                mono = click["elapsed_ms"] - post["elapsed_ms"]
                if (post["boot_count"] >= 0 and post["boot_count"] == click["boot_count"]
                        and min(post["elapsed_ms"], click["elapsed_ms"]) >= 0 and mono >= 0
                        and abs(mono-wall) <= CLOCK_TOLERANCE_MS):
                    row["clock_quality"] = "same_boot_consistent"
                    row["click_delay_ms"] = mono
                    row["outcome"] = "clicked" if mono < window else "late_click"
                else:
                    row["clock_quality"] = "reboot_or_clock_change_or_unknown"
                    row["outcome"] = "clock_unreliable"
            elif at + window > as_of:
                row["outcome"] = "window_open"
            elif row["collection_incomplete"]:
                row["outcome"] = "collection_incomplete"
            elif q.get("complete_through", 0) < at + window:
                row["outcome"] = "awaiting_upload_confirmation"
            else:
                row["outcome"] = "no_observed_click"
        elif len(posts) > 1:
            row["outcome"] = "ambiguous_post"
        elif any(e["kind"] == "notification_failed" for e in values):
            row["outcome"] = "post_failed_or_unknown"
        elif any(e["kind"] == "notification_blocked" for e in values):
            row["outcome"] = "blocked"
        rows.append(row)
    # Ordinal is per participant, not calendar time or a device identifier.
    for subject in {r["subject"] for r in rows}:
        ordered = sorted((r for r in rows if r["subject"] == subject), key=lambda r:r["posted_utc_ms"] or r["scheduled_utc_ms"])
        for i, row in enumerate(ordered, 1):
            row["notification_ordinal"] = i
    policies = {}
    for policy in sorted({r["policy_id"] for r in rows}):
        subset = [r for r in rows if r["policy_id"] == policy]
        eligible = [r for r in subset if r["posted_utc_ms"] is not None and r["posted_utc_ms"]+window<=as_of
                    and r["complete_through_utc_ms"] >= r["posted_utc_ms"] + window
                    and not r["collection_incomplete"] and r["outcome"] in ("clicked", "late_click", "no_observed_click")]
        clicked = [r for r in eligible if r["outcome"] == "clicked"]
        delays = [r["click_delay_ms"]/60000 for r in clicked]
        policies[policy] = {"observed_notifications":len(subset), "closed_complete_posts":len(eligible),
                            "click_rate_within_window":len(clicked)/len(eligible) if eligible else None,
                            "clicked_latency_n":len(delays), "median_minutes":percentile(delays,.5),
                            "p90_minutes":percentile(delays,.9),
                            "outcomes":{name:sum(r["outcome"]==name for r in subset) for name in sorted({r["outcome"] for r in subset})}}
    activity = {kind:sum(any(e["kind"]==kind for e in values) for values in sessions.values())
                for kind in ("activity_selected", "activity_started", "activity_completed", "activity_ended")}
    report = {"dataset":dataset, "as_of_utc_ms":as_of, "response_window_ms":window,
              "clock_tolerance_ms":CLOCK_TOLERANCE_MS, "participants":len({r["subject"] for r in rows} | {subject for subject,_ in sessions}),
              "notification_participants":len({r["subject"] for r in rows}), "activity_participants":len({subject for subject,_ in sessions}),
              "policies":policies, "activity_sessions":activity, "causal_or_clinical_evidence":False,
              "limitations":["Post acceptance is not proof of display, attention or audibility.",
                             "Clicked-only latency is selected; report click rate and censored/unknown outcomes alongside it.",
                             "A fully included outbox snapshot must cover the response window; receipt time or a partial batch cannot confirm missing clicks.",
                             "Loss counters are conservative per collection; incomplete collections are excluded from summary rates/latency.",
                             "Activity counts are observed session counts, not clinical improvement or retention estimates.",
                             "Policy groups are descriptive, not randomized; no reduction is assumed."]}
    report["focus_recommendations"] = analyze_focus_recommendations(records, quality, as_of, dataset, window)
    focus_subjects = {r["subject"] for r in report["focus_recommendations"]["decisions"]}
    report["focus_recommendation_participants"] = len(focus_subjects)
    report["participants"] = len(focus_subjects | {r["subject"] for r in rows} | {subject for subject,_ in sessions})
    return report, rows


def plots(report, rows, output):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, axes = plt.subplots(1, 3, figsize=(16, 5))
    for policy in report["policies"]:
        subset = [r for r in rows if r["policy_id"] == policy]
        clicks = [r for r in subset if r["outcome"]=="clicked" and not r["collection_incomplete"]]
        axes[0].scatter([r["notification_ordinal"] for r in clicks], [r["click_delay_ms"]/60000 for r in clicks], label=policy, alpha=.65)
        posts = [r for r in subset if r["posting_delay_ms"] is not None]
        axes[1].scatter([r["notification_ordinal"] for r in posts], [r["posting_delay_ms"]/60000 for r in posts], label=policy, alpha=.65)
    axes[0].set(title="Accepted post → click (clicked only)", xlabel="Notification ordinal per participant", ylabel="Minutes (same-boot monotonic)")
    axes[1].set(title="Planned time → accepted post", xlabel="Notification ordinal per participant", ylabel="Minutes (wall clock)")
    policies = list(report["policies"]); bottom = [0]*len(policies)
    for outcome in sorted({r["outcome"] for r in rows}):
        counts = [report["policies"][p]["outcomes"].get(outcome,0) for p in policies]
        axes[2].bar(policies, counts, bottom=bottom, label=outcome)
        bottom = [a+b for a,b in zip(bottom,counts)]
    axes[0].set_ylim(bottom=0)
    if all(r["posting_delay_ms"] is None or r["posting_delay_ms"]>=0 for r in rows):axes[1].set_ylim(bottom=0)
    axes[2].set(title="All observed outcomes", ylabel="Notifications")
    axes[2].tick_params(axis="x", labelrotation=20)
    for ax in axes:
        if ax.get_legend_handles_labels()[0]:ax.legend(fontsize=7)
        ax.grid(axis="y", alpha=.2)
    fig.suptitle(f'{report["dataset"].upper()} — descriptive evidence, not proof of policy effect')
    fig.tight_layout();fig.savefig(output/"notification-evidence.png", dpi=170);plt.close(fig)
    focus = report.get("focus_recommendations", {}).get("policies", {})
    if focus:
        fig, axes = plt.subplots(1, 2, figsize=(12, 5))
        names = list(focus)
        stages = ("closed_complete_exposures", "recommended_selected", "recommended_started", "recommended_completed")
        for i, name in enumerate(names):
            axes[0].plot(range(4), [focus[name][stage] for stage in stages], marker="o", label=name)
        axes[0].set_xticks(range(4), ["Eligible exposure", "Selected", "Started", "Completed"])
        axes[0].set(title="Observed recommendation funnel", ylabel="Decisions")
        outcomes = sorted({kind for policy in focus.values() for kind in policy["outcomes"]})
        bottom = [0]*len(names)
        for outcome in outcomes:
            counts = [focus[name]["outcomes"].get(outcome, 0) for name in names]
            axes[1].bar(names, counts, bottom=bottom, label=outcome)
            bottom = [a+b for a,b in zip(bottom,counts)]
        axes[1].set(title="All decisions, including unknown outcomes", ylabel="Decisions")
        axes[1].tick_params(axis="x", labelrotation=15)
        for ax in axes:
            ax.grid(axis="y", alpha=.2); ax.legend(fontsize=7); ax.set_ylim(bottom=0)
        fig.suptitle(f'{report["dataset"].upper()} — descriptive comparison; not randomized policy-effect evidence')
        fig.tight_layout();fig.savefig(output/"focus-recommendation-evidence.png", dpi=170);plt.close(fig)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--database",required=True);parser.add_argument("--output-dir",required=True)
    parser.add_argument("--as-of-ms",type=int,required=True);parser.add_argument("--dataset",choices=("live","synthetic"),required=True)
    parser.add_argument("--window-hours",type=int,default=24);parser.add_argument("--no-plots",action="store_true")
    args=parser.parse_args()
    if not 1<=args.window_hours<=168:parser.error("window-hours must be 1–168")
    os.umask(0o077);out=Path(args.output_dir);out.mkdir(parents=True,exist_ok=True)
    if any(out.iterdir()):parser.error("Use a fresh private output directory")
    with sqlite3.connect("file:"+quote(str(Path(args.database).resolve()))+"?mode=ro",uri=True) as db:
        records=[(s,c,json.loads(p)) for s,c,p in db.execute("SELECT subject,collection_id,payload FROM measurements")]
        has_watermark = "complete_through" in {row[1] for row in db.execute("PRAGMA table_info(measurement_quality)")}
        watermark_column = "complete_through" if has_watermark else "0"
        quality={(s,c):dict(dropped=d,expired=e,storage_error=f,updated=t,complete_through=w)
                 for s,c,d,e,f,t,w in db.execute("SELECT subject,collection_id,dropped,expired,storage_error,updated,"+watermark_column+" FROM measurement_quality")}
    report,rows=analyze(records,quality,args.as_of_ms,args.dataset,args.window_hours*3600000)
    (out/"summary.json").write_text(json.dumps(report,indent=2,ensure_ascii=False)+"\n")
    with (out/"notification-pairs.csv").open("w",newline="") as f:
        if rows:
            writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
    focus_rows = report["focus_recommendations"]["decisions"]
    with (out/"focus-recommendation-decisions.csv").open("w",newline="") as f:
        if focus_rows:
            writer=csv.DictWriter(f,fieldnames=list(focus_rows[0]));writer.writeheader();writer.writerows(focus_rows)
    if not args.no_plots:plots(report,rows,out)
    print("Private measurement analysis written; inspect summary limitations before presentation.")

if __name__=="__main__":main()
