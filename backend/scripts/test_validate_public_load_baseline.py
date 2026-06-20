from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from validate_public_load_baseline import validate_baseline  # noqa: E402


def baseline() -> dict:
    return {
        "schema_version": 1,
        "status": "passed",
        "baseline_metadata": {
            "source": "saturation",
            "recommended_concurrency": 8,
            "saturation_status": "saturated",
        },
        "config": {
            "duration_seconds": 60,
            "concurrency": 8,
            "timeout_seconds": 30.0,
            "weights": {"browse": 4, "chat": 3, "stream": 2, "checkout": 0},
            "chat_queries": ["conta", "160000"],
            "thresholds": {"min_success_rate": 0.9},
        },
        "overall": {"successful_throughput_rps": 12.4},
        "scenarios": {
            "browse": {"successful_throughput_rps": 6.0},
            "chat": {"successful_throughput_rps": 4.0},
            "stream": {"successful_throughput_rps": 2.4},
            "checkout": {"successful_throughput_rps": 0.0},
        },
        "threshold_failures": [],
        "source_baseline_comparison": {"status": "skipped", "reason": "baseline_not_found"},
    }


class PublicLoadBaselineValidationTests(unittest.TestCase):
    def test_accepts_promoted_baseline(self) -> None:
        self.assertEqual(validate_baseline(baseline()), [])

    def test_rejects_runtime_fields_and_failed_status(self) -> None:
        payload = baseline()
        payload["status"] = "failed"
        payload["threshold_failures"] = ["stream success_rate 0.80 < 0.90"]
        payload["config"]["base_url"] = "https://staging.example.test"
        payload["config"]["elapsed_seconds"] = 61.2

        self.assertEqual(
            validate_baseline(payload),
            [
                "status must be passed",
                "threshold_failures must be empty",
                "config.base_url must not be stored in approved baseline",
                "config.elapsed_seconds must not be stored in approved baseline",
            ],
        )

    def test_rejects_missing_enabled_scenario_throughput(self) -> None:
        payload = baseline()
        payload["scenarios"]["stream"]["successful_throughput_rps"] = 0.0

        self.assertEqual(
            validate_baseline(payload),
            ["scenarios.stream.successful_throughput_rps must be > 0"],
        )

    def test_rejects_invalid_profile(self) -> None:
        payload = baseline()
        payload["baseline_metadata"]["source"] = "manual"
        payload["config"]["duration_seconds"] = 0
        payload["config"]["weights"] = {"browse": 0}
        payload["config"]["chat_queries"] = [""]
        payload["overall"]["successful_throughput_rps"] = 0

        self.assertEqual(
            validate_baseline(payload),
            [
                "baseline_metadata.source must be report or saturation",
                "config.duration_seconds must be > 0",
                "config.weights must enable at least one scenario",
                "config.chat_queries must contain non-empty strings",
                "overall.successful_throughput_rps must be > 0",
            ],
        )


if __name__ == "__main__":
    unittest.main()
