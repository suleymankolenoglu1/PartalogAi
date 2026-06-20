from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from eval.chat_eval import (  # noqa: E402
    best_rank,
    classify_quality_issues,
    evaluate_thresholds,
    extract_codes_from_response,
    load_cases,
    summarize,
    validate_cases,
)


class ChatEvalMetricsTests(unittest.TestCase):
    def test_best_rank_returns_first_expected_code_position(self) -> None:
        self.assertEqual(
            best_rank(["ABC-001", "160000", "120016"], ["120016", "160000"]),
            2,
        )
        self.assertIsNone(best_rank(["ABC-001"], ["160000"]))

    def test_extract_codes_from_response_collects_and_deduplicates_sources(self) -> None:
        response = {
            "products": [
                {"code": "160000"},
                {"Code": "120016"},
            ],
            "compareGroups": [
                {
                    "results": [
                        {"code": "160000"},
                        {"Code": "005075"},
                    ]
                }
            ],
        }

        self.assertEqual(extract_codes_from_response(response), ["160000", "120016", "005075"])

    def test_summarize_tracks_retrieval_and_no_code_rates(self) -> None:
        summary = summarize(
            [
                {
                    "ok": True,
                    "status": 200,
                    "error": None,
                    "logical_error": False,
                    "latency_ms": 100.0,
                    "source_count": 2,
                    "reply_len": 40,
                    "rank": 1,
                    "expected_codes": ["160000", "120016"],
                    "expect_no_codes": False,
                    "no_code_ok": None,
                    "hit_at_1": True,
                    "hit_at_3": True,
                    "hit_at_5": True,
                    "mrr": 1.0,
                    "required_ok": True,
                    "forbidden_ok": True,
                    "mentioned_codes": ["160000"],
                    "hallucinated_codes": [],
                },
                {
                    "ok": True,
                    "status": 200,
                    "error": None,
                    "logical_error": False,
                    "latency_ms": 200.0,
                    "source_count": 0,
                    "reply_len": 20,
                    "rank": None,
                    "expected_codes": [],
                    "expect_no_codes": True,
                    "no_code_ok": True,
                    "hit_at_1": False,
                    "hit_at_3": False,
                    "hit_at_5": False,
                    "mrr": 0.0,
                    "required_ok": True,
                    "forbidden_ok": True,
                    "mentioned_codes": [],
                    "hallucinated_codes": [],
                },
            ]
        )

        self.assertEqual(summary["expected_case_count"], 1)
        self.assertAlmostEqual(summary["hit_at_1"], 1.0)
        self.assertAlmostEqual(summary["no_code_pass_rate"], 1.0)
        self.assertEqual(summary["quality_issue_case_count"], 0)
        self.assertEqual(summary["quality_issue_counts"], {})

    def test_summarize_counts_quality_issue_reasons(self) -> None:
        summary = summarize(
            [
                {
                    "ok": True,
                    "status": 200,
                    "error": None,
                    "logical_error": False,
                    "latency_ms": 100.0,
                    "source_count": 2,
                    "reply_len": 40,
                    "rank": 2,
                    "expected_codes": ["160000"],
                    "expect_no_codes": False,
                    "no_code_ok": None,
                    "hit_at_1": False,
                    "hit_at_3": True,
                    "hit_at_5": True,
                    "mrr": 0.5,
                    "required_ok": False,
                    "forbidden_ok": True,
                    "mentioned_codes": ["160000"],
                    "hallucinated_codes": [],
                },
                {
                    "ok": False,
                    "status": 200,
                    "error": None,
                    "logical_error": True,
                    "latency_ms": 100.0,
                    "source_count": 0,
                    "reply_len": 20,
                    "rank": None,
                    "expected_codes": ["120016"],
                    "expect_no_codes": False,
                    "no_code_ok": None,
                    "hit_at_1": False,
                    "hit_at_3": False,
                    "hit_at_5": False,
                    "mrr": 0.0,
                    "required_ok": True,
                    "forbidden_ok": False,
                    "mentioned_codes": ["999999"],
                    "hallucinated_codes": ["999999"],
                },
            ]
        )

        self.assertEqual(summary["quality_issue_case_count"], 2)
        self.assertEqual(
            summary["quality_issue_counts"],
            {
                "expected_code_not_rank1": 1,
                "required_term_missing": 1,
                "logical_error": 1,
                "expected_code_missing": 1,
                "forbidden_term_present": 1,
                "hallucinated_code": 1,
            },
        )

    def test_classify_quality_issues_names_contract_failures(self) -> None:
        self.assertEqual(
            classify_quality_issues(
                {
                    "ok": False,
                    "status": 503,
                    "error": "service unavailable",
                    "logical_error": False,
                    "expected_codes": [],
                    "expect_no_codes": True,
                    "no_code_ok": False,
                    "required_ok": False,
                    "forbidden_ok": False,
                    "hallucinated_codes": ["999999"],
                }
            ),
            [
                "http_error",
                "unexpected_code_returned",
                "required_term_missing",
                "forbidden_term_present",
                "hallucinated_code",
            ],
        )

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

    def test_validate_cases_accepts_relevance_corpus_contract(self) -> None:
        cases_path = Path(__file__).resolve().parents[1] / "eval" / "queries.relevance.jsonl"

        failures = validate_cases(load_cases(str(cases_path)))

        self.assertEqual(failures, [])

    def test_validate_cases_rejects_ambiguous_expectations(self) -> None:
        failures = validate_cases(
            [
                {
                    "id": "BAD1",
                    "text": "160000 kodlu parça",
                    "expected_codes": ["160000"],
                    "expect_no_codes": True,
                },
                {
                    "id": "BAD1",
                    "text": "ikinci case",
                },
            ]
        )

        self.assertEqual(
            failures,
            [
                "BAD1: expected_codes and expect_no_codes cannot both be set",
                "BAD1: duplicate id",
                "BAD1: expected_codes or expect_no_codes is required",
            ],
        )


if __name__ == "__main__":
    unittest.main()
