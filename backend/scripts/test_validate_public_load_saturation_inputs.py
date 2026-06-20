from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from validate_public_load_saturation_inputs import (  # noqa: E402
    parse_concurrency_levels,
    validate_duration,
    validate_rate,
)


class SaturationInputValidationTests(unittest.TestCase):
    def test_parses_valid_concurrency_levels(self) -> None:
        self.assertEqual(parse_concurrency_levels("[4,8,16]"), [4, 8, 16])

    def test_rejects_invalid_concurrency_levels(self) -> None:
        cases = [
            ("{}", "must be a JSON array"),
            ("[4]", "at least two levels"),
            ("[4, 4]", "must not contain duplicates"),
            ("[8, 4]", "sorted ascending"),
            ("[0, 4]", "must be > 0"),
            ("[4, 8.5]", "must be an integer"),
        ]
        for raw, expected in cases:
            with self.subTest(raw=raw):
                with self.assertRaisesRegex(ValueError, expected):
                    parse_concurrency_levels(raw)

    def test_validates_duration(self) -> None:
        validate_duration(1)
        with self.assertRaisesRegex(ValueError, "duration_seconds must be > 0"):
            validate_duration(0)

    def test_validates_rates(self) -> None:
        validate_rate("min_throughput_gain_rate", 0.0)
        validate_rate("min_throughput_gain_rate", 0.99)

        with self.assertRaisesRegex(ValueError, "must be >= 0 and < 1"):
            validate_rate("min_throughput_gain_rate", 1.0)
        with self.assertRaisesRegex(ValueError, "must be >= 0 and < 1"):
            validate_rate("max_throughput_drop_rate", -0.1)


if __name__ == "__main__":
    unittest.main()
