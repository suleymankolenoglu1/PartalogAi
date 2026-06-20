from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from eval.chat_eval import (  # noqa: E402
    evaluate_thresholds,
    expand_combined_identifier_variants,
    extract_fallback_reasons_from_response,
    precision_at_k,
    summarize,
)


class ChatEvalMetricsTests(unittest.TestCase):
    def test_expand_combined_identifier_variants_splits_model_suffixes(self) -> None:
        self.assertEqual(
            expand_combined_identifier_variants("MF-7900-E22/E23"),
            ["MF-7900-E22", "MF-7900-E23"],
        )

    def test_precision_at_k_uses_fixed_k_denominator(self) -> None:
        codes = ["160000", "120016", "XYZ"]
        expected = ["160000", "120016", "005075"]

        self.assertEqual(precision_at_k(codes, expected, 1), 1.0)
        self.assertAlmostEqual(precision_at_k(codes, expected, 3), 2.0 / 3.0)
        self.assertAlmostEqual(precision_at_k(codes, expected, 5), 2.0 / 5.0)

    def test_extract_fallback_reasons_collects_unique_reasons(self) -> None:
        response = {
            "products": [
                {"code": "160000", "fallback": True, "fallback_reason": "brand_removed"},
                {"code": "120016", "fallback": True, "fallbackReason": "brand_removed"},
            ],
            "compareGroups": [
                {
                    "results": [
                        {"code": "005075", "fallback": True, "fallback_reason": "all_filters_removed"}
                    ]
                }
            ],
        }

        self.assertEqual(
            extract_fallback_reasons_from_response(response),
            ["brand_removed", "all_filters_removed"],
        )

    def test_summarize_tracks_precision_and_fallback_trigger_rate(self) -> None:
        summary = summarize(
            [
                {
                    "ok": True,
                    "latency_ms": 100.0,
                    "source_count": 2,
                    "reply_len": 40,
                    "expected_codes": ["160000", "120016"],
                    "expect_no_codes": False,
                    "hit_at_1": True,
                    "hit_at_3": True,
                    "hit_at_5": True,
                    "precision_at_1": 1.0,
                    "precision_at_3": 2.0 / 3.0,
                    "precision_at_5": 2.0 / 5.0,
                    "mrr": 1.0,
                    "fallback_triggered": True,
                    "fallback_reasons": ["brand_removed"],
                    "required_ok": True,
                    "forbidden_ok": True,
                    "mentioned_codes": ["160000"],
                    "hallucinated_codes": [],
                },
                {
                    "ok": True,
                    "latency_ms": 200.0,
                    "source_count": 0,
                    "reply_len": 20,
                    "expected_codes": [],
                    "expect_no_codes": True,
                    "no_code_ok": True,
                    "hit_at_1": False,
                    "hit_at_3": False,
                    "hit_at_5": False,
                    "precision_at_1": 0.0,
                    "precision_at_3": 0.0,
                    "precision_at_5": 0.0,
                    "mrr": 0.0,
                    "fallback_triggered": False,
                    "fallback_reasons": [],
                    "required_ok": True,
                    "forbidden_ok": True,
                    "mentioned_codes": [],
                    "hallucinated_codes": [],
                },
            ]
        )

        self.assertEqual(summary["expected_case_count"], 1)
        self.assertAlmostEqual(summary["precision_at_3"], 2.0 / 3.0)
        self.assertAlmostEqual(summary["fallback_trigger_rate"], 0.5)
        self.assertEqual(summary["fallback_reason_counts"], {"brand_removed": 1})

    def test_thresholds_can_gate_required_and_forbidden_term_pass_rates(self) -> None:
        class Args:
            min_success_rate = None
            min_hit_at_1 = None
            min_precision_at_3 = None
            max_latency_p95_ms = None
            max_hallucination_rate = None
            min_no_code_pass_rate = None
            max_fallback_trigger_rate = None
            min_required_term_pass_rate = 1.0
            min_forbidden_term_pass_rate = 1.0

        failures = evaluate_thresholds(
            {
                "success_rate": 1.0,
                "hit_at_1": 1.0,
                "precision_at_3": 1.0,
                "latency_ms_p95": 1.0,
                "hallucination_rate": 0.0,
                "no_code_case_count": 1,
                "no_code_pass_rate": 1.0,
                "fallback_trigger_rate": 0.0,
                "required_term_pass_rate": 0.75,
                "forbidden_term_pass_rate": 0.5,
            },
            Args(),
        )

        self.assertEqual(
            failures,
            [
                "required_term_pass_rate 0.750 < min_required_term_pass_rate 1.000",
                "forbidden_term_pass_rate 0.500 < min_forbidden_term_pass_rate 1.000",
            ],
        )


if __name__ == "__main__":
    unittest.main()
