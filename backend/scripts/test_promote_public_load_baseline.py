from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from promote_public_load_baseline import (  # noqa: E402
    baseline_payload,
    baseline_review_markdown,
    select_report_from_saturation,
    validate_promotable_report,
    write_json,
)


def report(concurrency: int = 8, status: str = "passed", base_url: str = "https://staging.example.test") -> dict:
    weights = {"browse": 4, "chat": 3, "stream": 2, "checkout": 0}
    return {
        "schema_version": 1,
        "status": status,
        "threshold_failures": [] if status == "passed" else ["stream success_rate 0.80 < 0.90"],
        "baseline_comparison": {"status": "skipped", "reason": "baseline_not_found"},
        "config": {
            "base_url": base_url,
            "duration_seconds": 60,
            "elapsed_seconds": 61.2,
            "concurrency": concurrency,
            "timeout_seconds": 30.0,
            "weights": weights,
            "chat_queries": ["conta", "160000"],
            "thresholds": {
                "min_success_rate": 0.9,
                "max_stream_first_token_p95_ms": 5000.0,
            },
        },
        "overall": {
            "successful_throughput_rps": 12.4,
            "success_rate": 1.0,
            "latency_p95_ms": 1200.0,
        },
        "scenarios": {
            "browse": {"successful_throughput_rps": 6.0},
            "chat": {"successful_throughput_rps": 4.0},
            "stream": {"successful_throughput_rps": 2.4},
            "checkout": {"successful_throughput_rps": 0.0},
        },
    }


class BaselinePromotionTests(unittest.TestCase):
    def test_validates_passed_staging_report(self) -> None:
        failures = validate_promotable_report(report(), require_base_url_contains="staging")

        self.assertEqual(failures, [])

    def test_rejects_failed_report_and_non_staging_url(self) -> None:
        failures = validate_promotable_report(
            report(status="failed", base_url="https://api.example.test"),
            require_base_url_contains="staging",
        )

        self.assertEqual(
            failures,
            [
                "report status must be passed",
                "threshold_failures must be empty",
                "config.base_url must contain 'staging' for baseline promotion",
            ],
        )

    def test_builds_stable_baseline_payload_without_runtime_fixture_ids(self) -> None:
        payload = baseline_payload(report(), {"source": "report", "report_json": "report.json"})

        self.assertEqual(payload["schema_version"], 1)
        self.assertEqual(payload["config"]["concurrency"], 8)
        self.assertNotIn("elapsed_seconds", payload["config"])
        self.assertNotIn("base_url", payload["config"])
        self.assertEqual(payload["baseline_metadata"]["source"], "report")

    def test_writes_human_review_markdown(self) -> None:
        payload = baseline_payload(
            report(),
            {
                "source": "saturation",
                "recommended_concurrency": 8,
                "saturation_status": "saturated",
                "bottleneck_scenario": "stream",
            },
        )

        markdown = baseline_review_markdown(payload)

        self.assertIn("# Public E2E Load Baseline Candidate", markdown)
        self.assertIn("## Review Decision", markdown)
        self.assertIn("- [ ] Candidate came from the intended staging environment.", markdown)
        self.assertIn("- Recommended concurrency: `8`", markdown)
        self.assertIn("- Bottleneck scenario: `stream`", markdown)
        self.assertIn('"min_success_rate": 0.9', markdown)
        self.assertIn("- Successful throughput: `12.40 req/s`", markdown)
        self.assertIn("| stream | 2.40 | - | - | - | - |", markdown)

    def test_selects_recommended_concurrency_from_saturation_summary(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            reports_dir = Path(tmp)
            write_json(reports_dir / "public-e2e-saturation-4.json", report(concurrency=4))
            write_json(reports_dir / "public-e2e-saturation-8.json", report(concurrency=8))

            selected = select_report_from_saturation(
                {
                    "status": "saturated",
                    "recommended_concurrency": 8,
                    "failures": [],
                },
                reports_dir,
            )

        self.assertEqual(selected["config"]["concurrency"], 8)

    def test_rejects_failed_saturation_summary(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaisesRegex(ValueError, "saturation summary status is failed"):
                select_report_from_saturation(
                    {
                        "status": "failed",
                        "recommended_concurrency": 8,
                        "failures": ["concurrency 16 throughput drop"],
                    },
                    Path(tmp),
                )


if __name__ == "__main__":
    unittest.main()
