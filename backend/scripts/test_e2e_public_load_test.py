from __future__ import annotations

import argparse
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from e2e_public_load_test import (
    ScenarioOutcome,
    check_thresholds,
    percentile,
    summarize_scenario,
    write_json_report,
)


class PublicLoadTestHelpersTests(unittest.TestCase):
    def test_percentile_interpolates_rank(self) -> None:
        self.assertEqual(percentile([], 0.95), 0.0)
        self.assertEqual(percentile([42.0], 0.95), 42.0)
        self.assertAlmostEqual(percentile([100.0, 200.0, 300.0], 0.95), 290.0)

    def test_summarize_scenario_tracks_success_latency_and_errors(self) -> None:
        summary = summarize_scenario(
            [
                ScenarioOutcome(True, 200, 100.0),
                ScenarioOutcome(True, 200, 200.0),
                ScenarioOutcome(False, 0, 500.0, "stream completed without done event"),
            ]
        )

        self.assertEqual(summary["total"], 3)
        self.assertAlmostEqual(summary["success_rate"], 2.0 / 3.0)
        self.assertAlmostEqual(summary["error_rate"], 1.0 / 3.0)
        self.assertEqual(summary["status_counts"], {200: 2, 0: 1})
        self.assertEqual(summary["top_errors"], [("stream completed without done event", 1)])
        self.assertAlmostEqual(summary["latency_p95_ms"], 470.0)

    def test_check_thresholds_skips_disabled_scenarios(self) -> None:
        args = argparse.Namespace(
            browse_weight=1,
            chat_weight=1,
            stream_weight=0,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=1000.0,
        )
        summaries = {
            "browse": {"total": 1, "success_rate": 1.0, "latency_p95_ms": 100.0},
            "chat": {"total": 2, "success_rate": 0.50, "latency_p95_ms": 2000.0},
            "stream": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            [
                "chat success_rate 0.500 < 0.900",
                "chat latency_p95_ms 2000.0 > 1000.0",
            ],
        )

    def test_write_json_report_creates_parent_directory(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / "nested" / "load-report.json"

            write_json_report(str(output), {"overall": {"success_rate": 1.0}})

            self.assertEqual(
                json.loads(output.read_text(encoding="utf-8")),
                {"overall": {"success_rate": 1.0}},
            )


if __name__ == "__main__":
    unittest.main()
