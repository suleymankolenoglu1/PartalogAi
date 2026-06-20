#!/usr/bin/env python3
"""Validate Public E2E Saturation Smoke workflow inputs."""

from __future__ import annotations

import argparse
import json
from typing import Any


def parse_concurrency_levels(raw: str) -> list[int]:
    try:
        payload: Any = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise ValueError(f"concurrency_levels must be a JSON array: {exc}") from exc

    if not isinstance(payload, list):
        raise ValueError("concurrency_levels must be a JSON array")
    if len(payload) < 2:
        raise ValueError("concurrency_levels must contain at least two levels")

    levels: list[int] = []
    for index, item in enumerate(payload):
        if not isinstance(item, int):
            raise ValueError(f"concurrency_levels[{index}] must be an integer")
        if item <= 0:
            raise ValueError(f"concurrency_levels[{index}] must be > 0")
        levels.append(item)

    if len(set(levels)) != len(levels):
        raise ValueError("concurrency_levels must not contain duplicates")
    if levels != sorted(levels):
        raise ValueError("concurrency_levels must be sorted ascending")

    return levels


def validate_rate(name: str, value: float) -> None:
    if not 0 <= value < 1:
        raise ValueError(f"{name} must be >= 0 and < 1")


def validate_duration(seconds: int) -> None:
    if seconds <= 0:
        raise ValueError("duration_seconds must be > 0")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate public load saturation workflow inputs")
    parser.add_argument("--concurrency-levels", required=True)
    parser.add_argument("--duration-seconds", type=int, required=True)
    parser.add_argument("--min-throughput-gain-rate", type=float, required=True)
    parser.add_argument("--max-throughput-drop-rate", type=float, required=True)
    args = parser.parse_args()

    try:
        levels = parse_concurrency_levels(args.concurrency_levels)
        validate_duration(args.duration_seconds)
        validate_rate("min_throughput_gain_rate", args.min_throughput_gain_rate)
        validate_rate("max_throughput_drop_rate", args.max_throughput_drop_rate)
    except ValueError as exc:
        print(f"Saturation input validation failed: {exc}")
        return 2

    print(f"Saturation inputs valid: concurrency_levels={levels}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
