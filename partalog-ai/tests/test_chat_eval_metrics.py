from __future__ import annotations

import argparse
import sys
import unittest
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from eval.chat_eval import (  # noqa: E402
    best_rank,
    choose_better_result,
    classify_quality_issues,
    evaluate_thresholds,
    extract_codes_from_response,
    load_cases,
    parse_category_threshold,
    summarize,
    validate_cases,
    validate_category_threshold_cases,
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

    def test_failed_expected_case_stays_in_retrieval_metric_denominator(self) -> None:
        base = {
            "status": 200,
            "error": None,
            "logical_error": False,
            "latency_ms": 100.0,
            "source_count": 1,
            "reply_len": 20,
            "expect_no_codes": False,
            "no_code_ok": None,
            "required_ok": True,
            "forbidden_ok": True,
            "mentioned_codes": [],
            "hallucinated_codes": [],
        }
        summary = summarize(
            [
                {
                    **base,
                    "ok": True,
                    "rank": 1,
                    "expected_codes": ["160000"],
                    "hit_at_1": True,
                    "hit_at_3": True,
                    "hit_at_5": True,
                    "mrr": 1.0,
                },
                {
                    **base,
                    "ok": False,
                    "status": 503,
                    "error": "service unavailable",
                    "rank": None,
                    "expected_codes": ["120016"],
                    "hit_at_1": False,
                    "hit_at_3": False,
                    "hit_at_5": False,
                    "mrr": 0.0,
                },
            ]
        )

        self.assertEqual(summary["expected_case_count"], 2)
        self.assertEqual(summary["hit_at_1"], 0.5)
        self.assertEqual(summary["hit_at_3"], 0.5)
        self.assertEqual(summary["hit_at_5"], 0.5)
        self.assertEqual(summary["mrr"], 0.5)

    def test_failed_no_code_case_does_not_count_as_passed(self) -> None:
        base = {
            "error": None,
            "logical_error": False,
            "latency_ms": 100.0,
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
        }
        summary = summarize(
            [
                {**base, "ok": True, "status": 200},
                {**base, "ok": False, "status": 503, "error": "service unavailable"},
            ]
        )

        self.assertEqual(summary["no_code_case_count"], 2)
        self.assertEqual(summary["no_code_pass_rate"], 0.5)

    def test_choose_better_result_prefers_clean_retry_result(self) -> None:
        failed = {
            "ok": True,
            "rank": None,
            "expected_codes": ["160000"],
            "expect_no_codes": False,
            "no_code_ok": None,
            "required_ok": True,
            "forbidden_ok": True,
            "hallucinated_codes": ["999999"],
            "mrr": 0.0,
            "source_count": 0,
        }
        clean = {
            **failed,
            "rank": 1,
            "hit_at_1": True,
            "hit_at_3": True,
            "hit_at_5": True,
            "mrr": 1.0,
            "source_count": 1,
            "hallucinated_codes": [],
        }

        self.assertIs(choose_better_result(failed, clean), clean)

    def test_summarize_reports_retrieval_metrics_by_category(self) -> None:
        base = {
            "error": None,
            "logical_error": False,
            "latency_ms": 100.0,
            "source_count": 1,
            "reply_len": 20,
            "expect_no_codes": False,
            "no_code_ok": None,
            "required_ok": True,
            "forbidden_ok": True,
            "mentioned_codes": [],
            "hallucinated_codes": [],
        }
        summary = summarize(
            [
                {
                    **base,
                    "ok": True,
                    "status": 200,
                    "rank": 1,
                    "expected_codes": ["160000"],
                    "hit_at_1": True,
                    "hit_at_3": True,
                    "hit_at_5": True,
                    "mrr": 1.0,
                    "case": {"category": "specification"},
                },
                {
                    **base,
                    "ok": False,
                    "status": 503,
                    "error": "service unavailable",
                    "rank": None,
                    "expected_codes": ["120016"],
                    "hit_at_1": False,
                    "hit_at_3": False,
                    "hit_at_5": False,
                    "mrr": 0.0,
                    "case": {"category": "specification"},
                },
                {
                    **base,
                    "ok": True,
                    "status": 200,
                    "source_count": 0,
                    "expected_codes": [],
                    "expect_no_codes": True,
                    "no_code_ok": True,
                    "rank": None,
                    "hit_at_1": False,
                    "hit_at_3": False,
                    "hit_at_5": False,
                    "mrr": 0.0,
                    "case": {"category": "negative"},
                },
            ]
        )

        specification = summary["category_metrics"]["specification"]
        self.assertEqual(specification["case_count"], 2)
        self.assertEqual(specification["success_rate"], 0.5)
        self.assertEqual(specification["hit_at_1"], 0.5)
        self.assertEqual(specification["mrr"], 0.5)
        self.assertEqual(specification["quality_issue_case_count"], 1)
        self.assertEqual(summary["category_metrics"]["negative"]["no_code_pass_rate"], 1.0)

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

    def test_thresholds_can_gate_hit_at_3_hit_at_5_and_mrr(self) -> None:
        class Args:
            min_success_rate = None
            min_hit_at_1 = None
            min_hit_at_3 = 0.90
            min_hit_at_5 = 0.95
            min_mrr = 0.85
            max_latency_p95_ms = None
            max_hallucination_rate = None
            min_no_code_pass_rate = None
            min_required_term_pass_rate = None
            min_forbidden_term_pass_rate = None

        failures = evaluate_thresholds(
            {
                "success_rate": 1.0,
                "hit_at_1": 0.80,
                "hit_at_3": 0.85,
                "hit_at_5": 0.90,
                "mrr": 0.82,
                "latency_ms_p95": 1.0,
                "hallucination_rate": 0.0,
                "no_code_case_count": 1,
                "no_code_pass_rate": 1.0,
                "required_term_pass_rate": 1.0,
                "forbidden_term_pass_rate": 1.0,
            },
            Args(),
        )

        self.assertEqual(
            failures,
            [
                "hit_at_3 0.850 < min_hit_at_3 0.900",
                "hit_at_5 0.900 < min_hit_at_5 0.950",
                "mrr 0.820 < min_mrr 0.850",
            ],
        )

    def test_thresholds_can_gate_category_metrics(self) -> None:
        class Args:
            min_success_rate = None
            min_hit_at_1 = None
            min_hit_at_3 = None
            min_hit_at_5 = None
            min_mrr = None
            max_latency_p95_ms = None
            max_hallucination_rate = None
            min_no_code_pass_rate = None
            min_required_term_pass_rate = None
            min_forbidden_term_pass_rate = None
            min_category_success_rate = []
            min_category_hit_at_1 = [("exact_code", 1.0)]
            min_category_hit_at_3 = []
            min_category_hit_at_5 = []
            min_category_mrr = [("model_typo", 0.75)]
            min_category_no_code_pass_rate = [("negative", 1.0)]

        failures = evaluate_thresholds(
            {
                "success_rate": 1.0,
                "hit_at_1": 0.80,
                "hit_at_3": 0.90,
                "hit_at_5": 1.0,
                "mrr": 0.85,
                "latency_ms_p95": 1.0,
                "hallucination_rate": 0.0,
                "no_code_case_count": 3,
                "no_code_pass_rate": 0.67,
                "required_term_pass_rate": 1.0,
                "forbidden_term_pass_rate": 1.0,
                "category_metrics": {
                    "exact_code": {
                        "case_count": 5,
                        "success_rate": 1.0,
                        "expected_case_count": 5,
                        "hit_at_1": 0.8,
                        "hit_at_3": 1.0,
                        "hit_at_5": 1.0,
                        "mrr": 0.9,
                        "no_code_case_count": 0,
                        "no_code_pass_rate": 0.0,
                    },
                    "model_typo": {
                        "case_count": 3,
                        "success_rate": 1.0,
                        "expected_case_count": 3,
                        "hit_at_1": 0.33,
                        "hit_at_3": 0.67,
                        "hit_at_5": 1.0,
                        "mrr": 0.61,
                        "no_code_case_count": 0,
                        "no_code_pass_rate": 0.0,
                    },
                    "negative": {
                        "case_count": 3,
                        "success_rate": 1.0,
                        "expected_case_count": 0,
                        "hit_at_1": 0.0,
                        "hit_at_3": 0.0,
                        "hit_at_5": 0.0,
                        "mrr": 0.0,
                        "no_code_case_count": 3,
                        "no_code_pass_rate": 0.67,
                    },
                },
            },
            Args(),
        )

        self.assertEqual(
            failures,
            [
                "exact_code.hit_at_1 0.800 < min_category_hit_at_1 1.000",
                "model_typo.mrr 0.610 < min_category_mrr 0.750",
                "negative.no_code_pass_rate 0.670 < min_category_no_code_pass_rate 1.000",
            ],
        )

    def test_parse_category_threshold_validates_format(self) -> None:
        self.assertEqual(parse_category_threshold("exact_code=0.8"), ("exact_code", 0.8))

        with self.assertRaises(argparse.ArgumentTypeError):
            parse_category_threshold("exact_code")
        with self.assertRaises(argparse.ArgumentTypeError):
            parse_category_threshold("=0.8")
        with self.assertRaises(argparse.ArgumentTypeError):
            parse_category_threshold("exact_code=1.1")

    def test_category_threshold_contract_validates_case_coverage(self) -> None:
        class Args:
            min_category_success_rate = [("exact_code", 1.0)]
            min_category_hit_at_1 = [("missing", 0.8)]
            min_category_hit_at_3 = []
            min_category_hit_at_5 = []
            min_category_mrr = []
            min_category_no_code_pass_rate = [("exact_code", 1.0)]

        failures = validate_category_threshold_cases(
            [
                {
                    "id": "EXACT",
                    "category": "exact_code",
                    "text": "160000",
                    "expected_codes": ["160000"],
                }
            ],
            Args(),
        )

        self.assertEqual(
            failures,
            [
                "min_category_hit_at_1 set for unknown category 'missing'",
                "min_category_no_code_pass_rate set for category 'exact_code' but no eligible cases exist",
            ],
        )

    def test_validate_cases_accepts_relevance_corpus_contract(self) -> None:
        cases_path = Path(__file__).resolve().parents[1] / "eval" / "queries.relevance.jsonl"
        cases = load_cases(str(cases_path))

        failures = validate_cases(cases)

        self.assertEqual(len(cases), 25)
        self.assertEqual(
            Counter(case["category"] for case in cases),
            Counter(
                {
                    "specification": 6,
                    "lexical": 8,
                    "exact_code": 5,
                    "model_typo": 3,
                    "negative": 3,
                }
            ),
        )
        self.assertEqual(failures, [])

    def test_validate_cases_rejects_empty_category(self) -> None:
        failures = validate_cases(
            [
                {
                    "id": "BAD-CATEGORY",
                    "category": " ",
                    "text": "160000 kodlu parca",
                    "expected_codes": ["160000"],
                }
            ]
        )

        self.assertEqual(
            failures,
            ["BAD-CATEGORY: category must be a non-empty string when present"],
        )

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
