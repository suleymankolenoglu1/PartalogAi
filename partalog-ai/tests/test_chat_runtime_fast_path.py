from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.chat_intent import build_explicit_code_analysis  # noqa: E402
from services.chat_responses import build_exact_code_reply_from_sources  # noqa: E402


class ChatRuntimeFastPathTests(unittest.TestCase):
    def test_builds_local_analysis_for_explicit_part_code(self) -> None:
        analysis = build_explicit_code_analysis("70003363")

        self.assertIsNotNone(analysis)
        self.assertEqual(analysis["intent"], "SEARCH")
        self.assertEqual(analysis["part_code"], "70003363")
        self.assertEqual(analysis["parts"][0]["part_code"], "70003363")

    def test_skips_fast_path_for_natural_language_query(self) -> None:
        self.assertIsNone(build_explicit_code_analysis("iplik kılavuzu arıyorum"))

    def test_builds_deterministic_exact_code_reply(self) -> None:
        analysis = build_explicit_code_analysis("70003363") or {}
        reply = build_exact_code_reply_from_sources(
            "70003363",
            analysis,
            [
                {
                    "code": "70003363",
                    "name": "İplik Kılavuzu",
                    "pageNumber": "4",
                    "refNo": "32",
                }
            ],
        )

        self.assertIsNotNone(reply)
        self.assertIn("70003363", reply or "")
        self.assertIn("İplik Kılavuzu", reply or "")


if __name__ == "__main__":
    unittest.main()
