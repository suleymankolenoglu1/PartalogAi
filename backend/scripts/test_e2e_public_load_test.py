from __future__ import annotations

import argparse
import asyncio
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from e2e_public_load_test import (
    Fixture,
    ScenarioOutcome,
    check_thresholds,
    extract_fallback_reasons,
    percentile,
    run_chat_stream,
    scenario_latency_limits,
    summarize_scenario,
    write_json_report,
)


class FakeStreamResponse:
    status_code = 200

    def __init__(self, lines: list[str]):
        self.lines = lines

    async def __aenter__(self) -> FakeStreamResponse:
        return self

    async def __aexit__(self, *_args: object) -> None:
        return None

    async def aiter_lines(self):
        for line in self.lines:
            yield line


class FakeStreamClient:
    def __init__(self, lines: list[str]):
        self.response = FakeStreamResponse(lines)

    def stream(self, *_args: object, **_kwargs: object) -> FakeStreamResponse:
        return self.response


class PublicLoadTestHelpersTests(unittest.TestCase):
    fixture = Fixture("public-token", "", "catalog-id", "", "", 1.0)

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
            ],
            elapsed_seconds=10.0,
        )

        self.assertEqual(summary["total"], 3)
        self.assertEqual(summary["ok_count"], 2)
        self.assertEqual(summary["failed_count"], 1)
        self.assertAlmostEqual(summary["success_rate"], 2.0 / 3.0)
        self.assertAlmostEqual(summary["error_rate"], 1.0 / 3.0)
        self.assertEqual(summary["throughput_rps"], 0.3)
        self.assertEqual(summary["successful_throughput_rps"], 0.2)
        self.assertEqual(summary["event_count_avg"], 0.0)
        self.assertEqual(summary["fallback_case_count"], 0)
        self.assertEqual(summary["fallback_rate"], 0.0)
        self.assertEqual(summary["degraded_fallback_case_count"], 0)
        self.assertEqual(summary["degraded_fallback_rate"], 0.0)
        self.assertEqual(summary["fallback_reason_counts"], {})
        self.assertEqual(summary["status_counts"], {200: 2, 0: 1})
        self.assertEqual(summary["top_errors"], [("stream completed without done event", 1)])
        self.assertAlmostEqual(summary["latency_p95_ms"], 470.0)

    def test_summarize_scenario_tracks_stream_events_and_fallbacks(self) -> None:
        summary = summarize_scenario(
            [
                ScenarioOutcome(True, 200, 100.0, event_count=4, first_token_latency_ms=80.0),
                ScenarioOutcome(
                    False,
                    0,
                    500.0,
                    "chat stream reason=upstream_timeout",
                    event_count=2,
                    fallback_reasons=("upstream_timeout", "upstream_timeout"),
                    first_token_latency_ms=300.0,
                ),
                ScenarioOutcome(
                    False,
                    0,
                    600.0,
                    "chat stream reason=upstream_timeout",
                    event_count=2,
                    fallback_reasons=("upstream_timeout", "upstream_non_success"),
                ),
            ]
        )

        self.assertEqual(summary["ok_count"], 1)
        self.assertEqual(summary["failed_count"], 2)
        self.assertAlmostEqual(summary["event_count_avg"], 8.0 / 3.0)
        self.assertEqual(summary["first_token_sample_count"], 2)
        self.assertEqual(summary["first_token_latency_avg_ms"], 190.0)
        self.assertEqual(summary["first_token_latency_p95_ms"], 289.0)
        self.assertEqual(summary["fallback_case_count"], 2)
        self.assertAlmostEqual(summary["fallback_rate"], 2.0 / 3.0)
        self.assertEqual(summary["degraded_fallback_case_count"], 2)
        self.assertAlmostEqual(summary["degraded_fallback_rate"], 2.0 / 3.0)
        self.assertEqual(
            summary["fallback_reason_counts"],
            {"upstream_timeout": 2, "upstream_non_success": 1},
        )

    def test_run_chat_stream_records_first_non_empty_token(self) -> None:
        client = FakeStreamClient(
            [
                'data: {"type":"sources","sources":[]}',
                'data: {"type":"token","token":""}',
                'data: {"type":"token","token":"yanit"}',
                'data: {"type":"done","completion":{"status":"completed"}}',
            ]
        )

        outcome = asyncio.run(
            run_chat_stream(client, "https://example.test", self.fixture, "soru", {})  # type: ignore[arg-type]
        )

        self.assertTrue(outcome.ok)
        self.assertEqual(outcome.event_count, 4)
        self.assertIsNotNone(outcome.first_token_latency_ms)
        self.assertLessEqual(outcome.first_token_latency_ms or 0.0, outcome.latency_ms)

    def test_run_chat_stream_rejects_done_without_token(self) -> None:
        client = FakeStreamClient(
            [
                'data: {"type":"sources","sources":[]}',
                'data: {"type":"done","completion":{"status":"completed"}}',
            ]
        )

        outcome = asyncio.run(
            run_chat_stream(client, "https://example.test", self.fixture, "soru", {})  # type: ignore[arg-type]
        )

        self.assertFalse(outcome.ok)
        self.assertEqual(outcome.error, "stream completed without token event")
        self.assertIsNone(outcome.first_token_latency_ms)

    def test_summarize_scenario_does_not_mark_search_fallback_as_degraded(self) -> None:
        summary = summarize_scenario(
            [
                ScenarioOutcome(
                    True,
                    200,
                    100.0,
                    event_count=4,
                    fallback_reasons=("text_embedding_fallback",),
                )
            ]
        )

        self.assertEqual(summary["fallback_case_count"], 1)
        self.assertEqual(summary["fallback_rate"], 1.0)
        self.assertEqual(summary["degraded_fallback_case_count"], 0)
        self.assertEqual(summary["degraded_fallback_rate"], 0.0)

    def test_extract_fallback_reasons_supports_contract_and_legacy_reasons(self) -> None:
        self.assertEqual(
            extract_fallback_reasons(
                {
                    "type": "sources",
                    "fallback": {"used": True, "reason": "text_embedding_fallback"},
                    "reason": "upstream_timeout",
                }
            ),
            ["text_embedding_fallback", "upstream_timeout"],
        )

    def test_check_thresholds_skips_disabled_scenarios(self) -> None:
        args = argparse.Namespace(
            browse_weight=1,
            chat_weight=1,
            stream_weight=0,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=1000.0,
            min_samples_per_scenario=1,
            max_stream_degraded_rate=0.10,
        )
        summaries = {
            "browse": {"total": 1, "success_rate": 1.0, "latency_p95_ms": 100.0},
            "chat": {"total": 2, "success_rate": 0.50, "latency_p95_ms": 2000.0},
            "stream": {
                "total": 0,
                "success_rate": 0.0,
                "latency_p95_ms": 0.0,
                "degraded_fallback_rate": 0.0,
            },
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            [
                "chat success_rate 0.500 < 0.900",
                "chat latency_p95_ms 2000.0 > 1000.0",
            ],
        )

    def test_check_thresholds_gates_stream_degraded_fallback_rate(self) -> None:
        args = argparse.Namespace(
            browse_weight=0,
            chat_weight=0,
            stream_weight=1,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=1000.0,
            min_samples_per_scenario=1,
            max_stream_degraded_rate=0.10,
        )
        summaries = {
            "browse": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "chat": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "stream": {
                "total": 4,
                "success_rate": 1.0,
                "latency_p95_ms": 500.0,
                "degraded_fallback_rate": 0.25,
            },
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            ["stream degraded_fallback_rate 0.250 > 0.100"],
        )

    def test_check_thresholds_requires_minimum_samples_per_enabled_scenario(self) -> None:
        args = argparse.Namespace(
            browse_weight=0,
            chat_weight=0,
            stream_weight=1,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=1000.0,
            min_samples_per_scenario=5,
            max_stream_degraded_rate=0.10,
        )
        summaries = {
            "browse": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "chat": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "stream": {
                "total": 2,
                "success_rate": 0.50,
                "latency_p95_ms": 2000.0,
                "degraded_fallback_rate": 0.50,
            },
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            ["stream sample_count 2 < 5"],
        )

    def test_check_thresholds_uses_scenario_latency_overrides(self) -> None:
        args = argparse.Namespace(
            browse_weight=1,
            chat_weight=1,
            stream_weight=0,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=1000.0,
            max_browse_latency_p95_ms=200.0,
            max_chat_latency_p95_ms=2000.0,
            min_samples_per_scenario=1,
            max_stream_degraded_rate=0.10,
        )
        summaries = {
            "browse": {"total": 5, "success_rate": 1.0, "latency_p95_ms": 250.0},
            "chat": {"total": 5, "success_rate": 1.0, "latency_p95_ms": 1500.0},
            "stream": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            ["browse latency_p95_ms 250.0 > 200.0"],
        )
        self.assertEqual(
            scenario_latency_limits(args),
            {"browse": 200.0, "chat": 2000.0, "stream": 1000.0, "checkout": 1000.0},
        )

    def test_check_thresholds_gates_stream_first_token_latency(self) -> None:
        args = argparse.Namespace(
            browse_weight=0,
            chat_weight=0,
            stream_weight=1,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=5000.0,
            min_samples_per_scenario=5,
            max_stream_degraded_rate=0.10,
            max_stream_first_token_p95_ms=1000.0,
        )
        summaries = {
            "browse": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "chat": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "stream": {
                "total": 5,
                "success_rate": 1.0,
                "latency_p95_ms": 2000.0,
                "degraded_fallback_rate": 0.0,
                "first_token_sample_count": 5,
                "first_token_latency_p95_ms": 1200.0,
            },
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            ["stream first_token_latency_p95_ms 1200.0 > 1000.0"],
        )

    def test_check_thresholds_requires_enough_first_token_samples(self) -> None:
        args = argparse.Namespace(
            browse_weight=0,
            chat_weight=0,
            stream_weight=1,
            checkout_weight=0,
            min_success_rate=0.90,
            max_latency_p95_ms=5000.0,
            min_samples_per_scenario=5,
            max_stream_degraded_rate=0.10,
            max_stream_first_token_p95_ms=1000.0,
        )
        summaries = {
            "browse": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "chat": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
            "stream": {
                "total": 5,
                "success_rate": 0.80,
                "latency_p95_ms": 2000.0,
                "degraded_fallback_rate": 0.0,
                "first_token_sample_count": 4,
                "first_token_latency_p95_ms": 900.0,
            },
            "checkout": {"total": 0, "success_rate": 0.0, "latency_p95_ms": 0.0},
        }

        self.assertEqual(
            check_thresholds(args, summaries),
            [
                "stream success_rate 0.800 < 0.900",
                "stream first_token_sample_count 4 < 5",
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
