#!/usr/bin/env python3
"""Validate an approved public load baseline file before it is merged."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


DEFAULT_BASELINE = "backend/load-baselines/public-e2e-load-baseline.json"
REQUIRED_CONFIG_FIELDS = (
    "duration_seconds",
    "concurrency",
    "timeout_seconds",
    "weights",
    "chat_queries",
)


def load_json_object(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError(f"JSON root must be an object: {path}")
    return payload


def number(value: Any, default: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def validate_baseline(payload: dict[str, Any]) -> list[str]:
    failures: list[str] = []
    config = payload.get("config", {})

    if payload.get("schema_version") != 1:
        failures.append("schema_version must be 1")
    if payload.get("status") != "passed":
        failures.append("status must be passed")
    if payload.get("threshold_failures"):
        failures.append("threshold_failures must be empty")

    metadata = payload.get("baseline_metadata")
    if not isinstance(metadata, dict):
        failures.append("baseline_metadata must be an object")
    elif metadata.get("source") not in {"report", "saturation"}:
        failures.append("baseline_metadata.source must be report or saturation")

    if not isinstance(config, dict):
        failures.append("config must be an object")
        config = {}
    for field in REQUIRED_CONFIG_FIELDS:
        if field not in config:
            failures.append(f"config.{field} is required")

    if "base_url" in config:
        failures.append("config.base_url must not be stored in approved baseline")
    if "elapsed_seconds" in config:
        failures.append("config.elapsed_seconds must not be stored in approved baseline")
    if int(config.get("duration_seconds") or 0) <= 0:
        failures.append("config.duration_seconds must be > 0")
    if int(config.get("concurrency") or 0) <= 0:
        failures.append("config.concurrency must be > 0")
    if number(config.get("timeout_seconds")) <= 0:
        failures.append("config.timeout_seconds must be > 0")

    weights = config.get("weights")
    enabled_scenarios: list[str] = []
    if not isinstance(weights, dict):
        failures.append("config.weights must be an object")
    else:
        for scenario, weight in weights.items():
            if not isinstance(scenario, str) or not scenario:
                failures.append("config.weights keys must be non-empty strings")
                continue
            weight_value = number(weight, -1.0)
            if weight_value < 0:
                failures.append(f"config.weights.{scenario} must be >= 0")
            elif weight_value > 0:
                enabled_scenarios.append(scenario)
        if not enabled_scenarios:
            failures.append("config.weights must enable at least one scenario")

    chat_queries = config.get("chat_queries")
    if not isinstance(chat_queries, list) or not chat_queries:
        failures.append("config.chat_queries must be a non-empty array")
    elif any(not isinstance(query, str) or not query.strip() for query in chat_queries):
        failures.append("config.chat_queries must contain non-empty strings")

    thresholds = config.get("thresholds", {})
    if thresholds is not None and not isinstance(thresholds, dict):
        failures.append("config.thresholds must be an object when present")

    overall_rps = number(payload.get("overall", {}).get("successful_throughput_rps"))
    if overall_rps <= 0:
        failures.append("overall.successful_throughput_rps must be > 0")

    scenarios = payload.get("scenarios")
    if not isinstance(scenarios, dict):
        failures.append("scenarios must be an object")
        scenarios = {}
    for scenario in enabled_scenarios:
        scenario_rps = number(scenarios.get(scenario, {}).get("successful_throughput_rps"))
        if scenario_rps <= 0:
            failures.append(f"scenarios.{scenario}.successful_throughput_rps must be > 0")

    comparison = payload.get("source_baseline_comparison")
    if comparison is not None:
        if not isinstance(comparison, dict):
            failures.append("source_baseline_comparison must be an object when present")
        elif comparison.get("status") not in {"passed", "skipped"}:
            failures.append("source_baseline_comparison.status must be passed or skipped")

    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate approved public load baseline JSON")
    parser.add_argument("--baseline-json", default=DEFAULT_BASELINE)
    args = parser.parse_args()

    path = Path(args.baseline_json)
    try:
        payload = load_json_object(path)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Baseline validation failed: {exc}")
        return 2

    failures = validate_baseline(payload)
    if failures:
        print("--- Baseline validation failed ---")
        for failure in failures:
            print(f"- {failure}")
        return 2

    print(f"Baseline validation passed: {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
