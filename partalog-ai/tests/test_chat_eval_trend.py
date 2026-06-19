from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from eval.update_trend_history import (  # noqa: E402
    append_snapshot,
    build_alert_markdown,
    build_alerts,
    build_trend_markdown,
)


class ChatEvalTrendTests(unittest.TestCase):
    def test_append_snapshot_keeps_recent_rows(self) -> None:
        rows = append_snapshot(
            [
                {"run_at": "old-1"},
                {"run_at": "old-2"},
            ],
            nightly={"success_rate": 1.0},
            behavior={"success_rate": 1.0},
            run_at="new",
            keep_last=2,
        )

        self.assertEqual([row["run_at"] for row in rows], ["old-2", "new"])

    def test_build_trend_markdown_renders_recent_metrics(self) -> None:
        markdown = build_trend_markdown(
            [
                {
                    "run_at": "2026-05-17T00:00:00+00:00",
                    "retrieval": {"success_rate": 0.95, "hit_at_1": 0.90},
                    "behavior": {
                        "success_rate": 1.0,
                        "required_term_pass_rate": 1.0,
                        "forbidden_term_pass_rate": 0.875,
                        "hallucination_rate": 0.0,
                    },
                }
            ]
        )

        self.assertIn("Nightly Chat Eval Trend", markdown)
        self.assertIn("95.00%", markdown)
        self.assertIn("87.50%", markdown)

    def test_build_alerts_reports_regressions_against_recent_baseline(self) -> None:
        rows = [
            {
                "run_at": "old-1",
                "retrieval": {"hit_at_1": 1.0, "latency_ms_p95": 1000.0},
                "behavior": {
                    "required_term_pass_rate": 1.0,
                    "forbidden_term_pass_rate": 1.0,
                    "hallucination_rate": 0.0,
                },
            },
            {
                "run_at": "old-2",
                "retrieval": {"hit_at_1": 1.0, "latency_ms_p95": 1000.0},
                "behavior": {
                    "required_term_pass_rate": 1.0,
                    "forbidden_term_pass_rate": 1.0,
                    "hallucination_rate": 0.0,
                },
            },
            {
                "run_at": "new",
                "retrieval": {"hit_at_1": 0.9, "latency_ms_p95": 1300.0},
                "behavior": {
                    "required_term_pass_rate": 0.9,
                    "forbidden_term_pass_rate": 0.9,
                    "hallucination_rate": 0.1,
                },
            },
        ]

        alerts = build_alerts(rows)

        self.assertEqual(len(alerts), 5)
        self.assertIn("Retrieval Hit@1", alerts[0])
        self.assertIn("latency", alerts[1])
        self.assertIn("required-term", alerts[2])
        self.assertIn("forbidden-term", alerts[3])
        self.assertIn("hallucination", alerts[4])

    def test_build_alert_markdown_explains_missing_baseline(self) -> None:
        markdown = build_alert_markdown(
            [
                {
                    "run_at": "only-run",
                    "retrieval": {},
                    "behavior": {},
                }
            ]
        )

        self.assertIn("Yeterli geçmiş yok", markdown)


if __name__ == "__main__":
    unittest.main()
