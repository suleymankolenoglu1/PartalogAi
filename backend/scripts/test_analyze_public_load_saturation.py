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
    scenario_rps: dict[str, float] | None = None,
) -> dict:
    scenario_rps = scenario_rps or {
        "browse": successful_rps * 0.5,
        "chat": successful_rps * 0.3,
        "stream": successful_rps * 0.2,
    }
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
            "error_kind_counts": {},
        },
        "scenarios": {
            name: {
                "successful_throughput_rps": value,
                "success_rate": 1.0,
                "latency_p95_ms": 1000.0,
                "error_kind_counts": {},
            }
            for name, value in scenario_rps.items()
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
        self.assertIsNone(summary["bottleneck_scenario"])
        self.assertEqual(summary["scenario_curves"]["stream"]["status"], "scaling")

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
        failed_report["overall"]["error_kind_counts"] = {
            "rate_limited": 2,
            "timeout": 1,
        }

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
        self.assertEqual(
            summary["points"][1]["error_kind_counts"],
            {"rate_limited": 2, "timeout": 1},
        )

    def test_identifies_scenario_bottleneck_before_overall_saturation(self) -> None:
        reports = [
            report(4, 10.0, scenario_rps={"browse": 5.0, "chat": 3.0, "stream": 2.0}),
            report(8, 18.0, scenario_rps={"browse": 9.0, "chat": 5.2, "stream": 3.8}),
            report(16, 30.0, scenario_rps={"browse": 15.0, "chat": 11.0, "stream": 4.0}),
        ]

        summary, failures = analyze_saturation(
            reports,
            [4, 8, 16],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(failures, [])
        self.assertEqual(summary["status"], "saturated")
        self.assertEqual(summary["bottleneck_scenario"], "stream")
        self.assertEqual(summary["scenario_curves"]["stream"]["status"], "saturated")
        self.assertEqual(summary["scenario_curves"]["stream"]["recommended_concurrency"], 8)
        self.assertEqual(summary["scenario_curves"]["browse"]["status"], "scaling")

    def test_scenario_regression_is_diagnostic_not_an_independent_gate(self) -> None:
        reports = [
            report(4, 10.0, scenario_rps={"browse": 5.0, "chat": 3.0, "stream": 2.0}),
            report(8, 18.0, scenario_rps={"browse": 9.0, "chat": 5.0, "stream": 4.0}),
            report(16, 30.0, scenario_rps={"browse": 16.0, "chat": 11.0, "stream": 3.0}),
        ]

        summary, failures = analyze_saturation(
            reports,
            [4, 8, 16],
            min_throughput_gain_rate=0.10,
            max_throughput_drop_rate=0.10,
        )

        self.assertEqual(failures, [])
        self.assertEqual(summary["status"], "saturated")
        self.assertEqual(summary["bottleneck_scenario"], "stream")
        self.assertEqual(summary["scenario_curves"]["stream"]["status"], "regressed")


if __name__ == "__main__":
    unittest.main()
