from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.chat_terms import (  # noqa: E402
    expand_part_search_text,
    extract_overlap_tokens,
    has_domain_part_keyword,
    normalize_for_overlap,
)


class ChatTermsAdversarialTests(unittest.TestCase):
    def test_normalize_for_overlap_handles_turkish_diacritics(self) -> None:
        text = normalize_for_overlap("İĞNE PLAKASI, RÜZGÂR KILAVUZU, ÖLÇÜ")

        self.assertEqual(text, "igne plakasi, ruzgar kilavuzu, olcu")

    def test_extract_overlap_tokens_preserves_codes_and_drops_fillers(self) -> None:
        tokens = extract_overlap_tokens("Bu 0037164 kodlu parça var mı, vida M3-0.5X3 lazım")

        self.assertIn("0037164", tokens)
        self.assertIn("kodlu", tokens)
        self.assertIn("parca", tokens)
        self.assertIn("vida", tokens)
        self.assertIn("m3", tokens)
        self.assertIn("5x3", tokens)
        self.assertNotIn("bu", tokens)
        self.assertNotIn("var", tokens)
        self.assertNotIn("mi", tokens)
        self.assertNotIn("lazim", tokens)

    def test_extract_overlap_tokens_deduplicates_diacritic_variants(self) -> None:
        tokens = extract_overlap_tokens("vida VIDA vıda vida")

        self.assertEqual(tokens, ["vida"])

    def test_domain_keyword_detection_rejects_unrelated_consumer_query(self) -> None:
        self.assertTrue(has_domain_part_keyword("iğne plakası lazım"))
        self.assertTrue(has_domain_part_keyword("M3 vida arıyorum"))
        self.assertFalse(has_domain_part_keyword("telefon şarj kablosu arıyorum"))

    def test_expand_part_search_text_adds_bilingual_plate_terms(self) -> None:
        expanded = expand_part_search_text("iğne plakası")

        self.assertIsNotNone(expanded)
        terms = set(str(expanded).split(" | "))
        self.assertIn("iğne plakası", terms)
        self.assertIn("igne plakasi", terms)
        self.assertIn("needle plate", terms)
        self.assertIn("throat plate", terms)


if __name__ == "__main__":
    unittest.main()
