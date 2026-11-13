import unittest
import uuid

from server.evaluate import WINDOW_MS, evaluate, examples, split_temporal, split_users
from server.tests.test_service import event, NOW


class EvaluationTests(unittest.TestCase):
    def test_only_closed_windows_and_matching_activity_create_labels(self):
        shown = event()
        later = dict(shown, kind="activity_started", event_id=uuid.uuid4().hex,
                     occurred_utc_ms=shown["occurred_utc_ms"] + 5000, features=[0, 0, 0, 0])
        self.assertEqual(examples([("synthetic", shown)], NOW * 1000 + 1000), [])
        rows = examples([("synthetic", shown), ("synthetic", later)], NOW * 1000 + WINDOW_MS)
        self.assertEqual(rows[0]["y"], 1)
        self.assertEqual(rows[0]["x"], shown["features"])
        later["activity_id"] = "course:MBCT02"
        self.assertEqual(examples([("synthetic", shown), ("synthetic", later)], NOW * 1000 + WINDOW_MS)[0]["y"], 0)

    def test_duplicate_decision_exposure_is_excluded(self):
        shown = event()
        self.assertEqual(examples([("synthetic", shown), ("synthetic", dict(shown, event_id=uuid.uuid4().hex))], NOW * 1000 + WINDOW_MS), [])

    def test_temporal_split_purges_unfinished_training_outcomes(self):
        rows = [{"subject": "synthetic", "time": i * WINDOW_MS // 2, "x": [0] * 4, "y": i % 2} for i in range(12)]
        train, test = split_temporal(rows)
        self.assertLessEqual(max(r["time"] for r in train) + WINDOW_MS, min(r["time"] for r in test))

    def test_staggered_participants_share_one_calendar_cutoff(self):
        rows = [{"subject": user, "time": (offset + i) * WINDOW_MS, "x": [0] * 4, "y": i % 2}
                for user, offset in [("synthetic-early", 0), ("synthetic-late", 100)] for i in range(12)]
        train, test = split_temporal(rows)
        self.assertTrue(train and test)
        self.assertLessEqual(max(r["time"] for r in train) + WINDOW_MS, min(r["time"] for r in test))
        self.assertTrue({r["subject"] for r in test} <= {r["subject"] for r in train})
        self.assertEqual((train, test), split_temporal(list(reversed(rows))))

    def test_same_user_future_excludes_participants_first_seen_after_cutoff(self):
        rows = [{"subject": "synthetic-established", "time": i * WINDOW_MS, "x": [0] * 4, "y": i % 2}
                for i in range(30)]
        rows += [{"subject": "synthetic-new", "time": (100 + i) * WINDOW_MS, "x": [0] * 4, "y": i % 2}
                 for i in range(6)]
        train, test = split_temporal(rows)
        self.assertTrue(test)
        self.assertNotIn("synthetic-new", {r["subject"] for r in test})

    def test_grouped_split_has_no_participant_overlap(self):
        rows = [{"subject": "synthetic-" + str(i), "time": j, "x": [0] * 4, "y": j % 2} for i in range(8) for j in range(10)]
        train, test = split_users(rows)
        self.assertFalse({r["subject"] for r in train} & {r["subject"] for r in test})
        self.assertEqual((train, test), split_users(list(rows)))

    def test_sparse_data_exports_rules_and_never_claims_effect(self):
        report, policy = evaluate([], NOW * 1000)
        self.assertEqual(policy["model_type"], "rules")
        self.assertFalse(report["candidate_passes_offline_gate"])
        self.assertFalse(report["causal_or_clinical_evidence"])
        self.assertFalse(report["deployment_approved"])

    def test_synthetic_fitting_is_reproducible_and_explicitly_not_deployed(self):
        events = []
        for user in range(8):
            for i in range(40):
                shown = event()
                shown["decision_id"] = uuid.UUID(int=user * 100 + i + 1).hex
                shown["occurred_utc_ms"] = NOW * 1000 + i * WINDOW_MS
                shown["features"] = [i % 2, 0.5, 0.5, 0.5]
                events.append(("synthetic-" + str(user), shown))
                if i % 2:
                    events.append(("synthetic-" + str(user), dict(shown, kind="activity_started", event_id=uuid.uuid4().hex,
                                                                occurred_utc_ms=shown["occurred_utc_ms"] + 1000)))
        first = evaluate(events, NOW * 1000 + 41 * WINDOW_MS)
        self.assertEqual(first, evaluate(events, NOW * 1000 + 41 * WINDOW_MS))
        report, policy = first
        self.assertEqual(report["observations"], 320)
        self.assertEqual(policy["model_type"], "logistic")
        self.assertTrue(report["candidate_passes_offline_gate"])
        self.assertFalse(report["deployment_approved"])


if __name__ == "__main__":
    unittest.main()
