from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from api.table import _normalize_product_item  # noqa: E402


class TableTranslationNormalizationTests(unittest.TestCase):
    def test_fastener_dimensions_survive_translation_when_source_name_has_spec(self) -> None:
        product = _normalize_product_item(
            {
                "ref_no": "12",
                "part_code": "SM4041055SP",
                "part_name": "vida",
                "source_name": "screw M3-0.5x5",
                "dimensions": "",
            }
        )

        self.assertIsNotNone(product)
        self.assertEqual(product.part_name, "VİDA M3-0.5x5")
        self.assertEqual(product.dimensions, "M3-0.5x5")

    def test_bolt_specs_are_preserved_from_dimensions_field(self) -> None:
        product = _normalize_product_item(
            {
                "ref_no": "4",
                "part_code": "BOLT001",
                "part_name": "cıvata",
                "source_name": "bolt assy",
                "dimensions": "m8-1.25X20",
            }
        )

        self.assertIsNotNone(product)
        self.assertEqual(product.part_name, "CIVATA M8-1.25x20")
        self.assertEqual(product.dimensions, "M8-1.25x20")

    def test_mixed_turkish_english_rows_keep_identifying_dimensions(self) -> None:
        product = _normalize_product_item(
            {
                "ref_no": "7",
                "part_code": "SM6050800SP",
                "part_name": "vida",
                "source_name": "vida screw M4x10",
                "remarks": "mixed row from OCR",
            }
        )

        self.assertIsNotNone(product)
        self.assertEqual(product.part_name, "VİDA M4x10")
        self.assertEqual(product.dimensions, "M4x10")

    def test_non_fasteners_do_not_get_dimension_suffixes(self) -> None:
        product = _normalize_product_item(
            {
                "ref_no": "9",
                "part_code": "PLATE001",
                "part_name": "plaka",
                "source_name": "throat plate 5mm",
            }
        )

        self.assertIsNotNone(product)
        self.assertEqual(product.part_name, "PLAKA")
        self.assertEqual(product.dimensions, "5mm")


if __name__ == "__main__":
    unittest.main()
