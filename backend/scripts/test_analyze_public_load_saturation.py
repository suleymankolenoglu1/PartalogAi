from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from analyze_public_load_saturation import analyze_saturation


def report(
    concurrency: int,
    successful_rps: float,
    duration: int = 30,
    status: str = "passed",
) -> dict:
    return {
        "schema_version": 1,
        "status": status,
        "threshold_failures": [],
        "config": {
            "duration_seconds": duration,
            "concurrency": concurrency,
            "timeout_seconds": 30.0,
            "weights": {"browse": 4, "chat": 3, "stream": 2, "checkout": 0},
            "chat_queries": ["conta", "160000"],
        },
        "overall": {
            "successful_throughput_rps": successful_rps,
            "success_rate": 1.0,
            "latency_p95_ms": 1000.0,
        },
    }


class SaturationAnalysisTests(unittest.TestCase):
    def test_reports_scaling_when_each_step_keeps_growing(self) -> None:
        summary, failures = analyze_saturation(
            [report(4, 10.0), report(8, 18.0), report(16, 30.0)],
            [4, 8, 16],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(failures, [])
        self.assertEqual(summary["status"], "scaling")
        self.assertEqual(summary["recommended_concurrency"], 16)
        self.assertEqual(
            [item["classification"] for item in summary["transitions"]],
            ["scaling", "scaling"],
        )

    def test_reports_saturation_without_failing(self) -> None:
        summary, failures = analyze_saturation(
            [report(4, 10.0), report(8, 18.0), report(16, 19.0)],
            [4, 8, 16],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(failures, [])
        self.assertEqual(summary["status"], "saturated")
        self.assertEqual(summary["first_saturation_concurrency"], 16)
        self.assertEqual(summary["recommended_concurrency"], 8)

    def test_fails_when_throughput_regresses(self) -> None:
        summary, failures = analyze_saturation(
            [report(4, 10.0), report(8, 18.0), report(16, 14.0)],
            [4, 8, 16],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(summary["status"], "failed")
        self.assertEqual(summary["recommended_concurrency"], 8)
        self.assertEqual(failures, ["concurrency 16 throughput drop 22.2% > 10.0%"])

    def test_fails_for_missing_concurrency_report(self) -> None:
        summary, failures = analyze_saturation(
            [report(4, 10.0), report(8, 18.0)],
            [4, 8, 16],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(summary["status"], "failed")
        self.assertIn("missing concurrency reports: 16", failures)

    def test_fails_for_profile_mismatch(self) -> None:
        summary, failures = analyze_saturation(
            [report(4, 10.0), report(8, 18.0, duration=60)],
            [4, 8],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(summary["status"], "failed")
        self.assertIn("concurrency 8 profile mismatch: duration_seconds", failures)

    def test_carries_load_gate_failures_into_summary(self) -> None:
        failed_report = report(8, 18.0, status="failed")
        failed_report["threshold_failures"] = ["stream success_rate 0.800 < 0.900"]

        summary, failures = analyze_saturation(
            [report(4, 10.0), failed_report],
            [4, 8],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(summary["status"], "failed")
        self.assertIn(
            "concurrency 8 load gates failed: stream success_rate 0.800 < 0.900",
            failures,
        )


if __name__ == "__main__":
    unittest.main()
