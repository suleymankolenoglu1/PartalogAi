from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from eval.build_cases_from_feedback import build_case  # noqa: E402


class BuildCasesFromFeedbackTests(unittest.TestCase):
    def test_negative_behavior_case_forbids_returned_source_codes(self) -> None:
        case = build_case(
            {
                "Id": "abc123",
                "CreatedAt": "2026-05-27T12:00:00Z",
                "Helpful": False,
                "UserQuery": "yanlis luper kodu verdi",
                "SourceCodes": ["70003363", "70003363", " ps0150042k0 "],
                "Reason": "Yanlış parça ailesi",
            },
            1,
            "",
            "behavior",
            [],
            False,
        )

        self.assertIsNotNone(case)
        self.assertEqual(case["id"], "FB_BEHAVIOR_20260527_ABC123")
        self.assertEqual(case["catalog_ids"], ["<CATALOG_GUID>"])
        self.assertEqual(case["public_token"], "<PUBLIC_TOKEN>")
        self.assertEqual(case["forbidden_terms"], ["70003363", "PS0150042K0"])
        self.assertEqual(case["feedback_reason"], "Yanlış parça ailesi")

    def test_context_case_uses_stored_context_part_code_as_expected_code(self) -> None:
        case = build_case(
            {
                "id": "ctx-row",
                "createdAt": "2026-05-27T12:00:00Z",
                "helpful": False,
                "userQuery": "bunun kodu ne",
                "catalogIds": ["catalog-1"],
                "contextJson": '{"catalogId":"catalog-1","pageNumber":4,"partCode":"70003363","refNo":"32"}',
            },
            1,
            "pk_test",
            "context",
            [],
            False,
        )

        self.assertIsNotNone(case)
        self.assertEqual(case["id"], "FB_CTX_20260527_CTXROW")
        self.assertEqual(case["public_token"], "pk_test")
        self.assertEqual(case["catalog_ids"], ["catalog-1"])
        self.assertEqual(case["expected_codes"], ["70003363"])
        self.assertEqual(case["required_terms"], ["70003363"])
        self.assertEqual(case["context_json"]["partCode"], "70003363")


if __name__ == "__main__":
    unittest.main()
