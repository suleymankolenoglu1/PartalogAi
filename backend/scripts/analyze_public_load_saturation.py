#!/usr/bin/env python3
"""Analyze sequential public load reports for saturation and regression."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


PROFILE_FIELDS = ("duration_seconds", "timeout_seconds", "weights", "chat_queries")


def throughput_point(concurrency: int, summary: dict[str, Any]) -> dict[str, Any]:
    return {
        "concurrency": concurrency,
        "successful_throughput_rps": float(summary.get("successful_throughput_rps", 0.0)),
        "success_rate": float(summary.get("success_rate", 0.0)),
        "latency_p95_ms": float(summary.get("latency_p95_ms", 0.0)),
        "error_kind_counts": dict(summary.get("error_kind_counts", {})),
    }


def analyze_curve(
    points: list[dict[str, Any]],
    min_throughput_gain_rate: float,
    max_throughput_drop_rate: float,
) -> dict[str, Any]:
    transitions: list[dict[str, Any]] = []
    first_saturation_concurrency: int | None = None
    recommended_concurrency = points[0]["concurrency"] if points else 0
    if points and points[0]["successful_throughput_rps"] > 0:
        base_concurrency = points[0]["concurrency"]
        base_rps = points[0]["successful_throughput_rps"]
        for previous, current in zip(points, points[1:]):
            previous_rps = previous["successful_throughput_rps"]
            current_rps = current["successful_throughput_rps"]
            gain_rate = (current_rps / previous_rps) - 1.0 if previous_rps > 0 else -1.0
            scaling_efficiency = (
                (current_rps / base_rps) / (current["concurrency"] / base_concurrency)
                if base_rps > 0
                else 0.0
            )
            if gain_rate < -max_throughput_drop_rate:
                classification = "regressed"
            elif gain_rate < min_throughput_gain_rate:
                classification = "saturated"
            else:
                classification = "scaling"

            if classification in {"saturated", "regressed"} and first_saturation_concurrency is None:
                first_saturation_concurrency = current["concurrency"]
                recommended_concurrency = previous["concurrency"]
            elif first_saturation_concurrency is None:
                recommended_concurrency = current["concurrency"]

            transitions.append(
                {
                    "from_concurrency": previous["concurrency"],
                    "to_concurrency": current["concurrency"],
                    "throughput_gain_rate": gain_rate,
                    "scaling_efficiency": scaling_efficiency,
                    "classification": classification,
                }
            )

    classifications = {item["classification"] for item in transitions}
    if "regressed" in classifications:
        status = "regressed"
    elif "saturated" in classifications:
        status = "saturated"
    else:
        status = "scaling"
    return {
        "status": status,
        "recommended_concurrency": recommended_concurrency,
        "first_saturation_concurrency": first_saturation_concurrency,
        "max_observed": max(
            points,
            key=lambda item: item["successful_throughput_rps"],
            default=None,
        ),
        "points": points,
        "transitions": transitions,
    }


def analyze_saturation(
    reports: list[dict[str, Any]],
    expected_concurrencies: list[int],
    min_throughput_gain_rate: float,
    max_throughput_drop_rate: float,
) -> tuple[dict[str, Any], list[str]]:
    failures: list[str] = []
    indexed: dict[int, dict[str, Any]] = {}
    for report in reports:
        config = report.get("config", {})
        concurrency = int(config.get("concurrency", 0))
        if concurrency <= 0:
            failures.append("report concurrency must be > 0")
            continue
        if concurrency in indexed:
            failures.append(f"duplicate concurrency report: {concurrency}")
            continue
        indexed[concurrency] = report

    missing = sorted(set(expected_concurrencies) - set(indexed))
    if missing:
        failures.append(f"missing concurrency reports: {', '.join(map(str, missing))}")

    ordered = [indexed[key] for key in sorted(indexed)]
    if len(ordered) < 2:
        failures.append("at least two concurrency reports are required")

    if ordered:
        baseline_report = ordered[0]
        baseline_config = baseline_report.get("config", {})
        baseline_schema = baseline_report.get("schema_version")
        for report in ordered[1:]:
            concurrency = report.get("config", {}).get("concurrency", 0)
            mismatches = [
                field
                for field in PROFILE_FIELDS
                if report.get("config", {}).get(field) != baseline_config.get(field)
            ]
            if report.get("schema_version") != baseline_schema:
                mismatches.insert(0, "schema_version")
            if mismatches:
                failures.append(
                    f"concurrency {concurrency} profile mismatch: {', '.join(mismatches)}"
                )

    points: list[dict[str, Any]] = []
    for report in ordered:
        concurrency = int(report["config"]["concurrency"])
        overall = report.get("overall", {})
        successful_rps = float(overall.get("successful_throughput_rps", 0.0))
        if report.get("status") == "failed":
            reasons = report.get("threshold_failures") or ["unknown load gate failure"]
            failures.append(
                f"concurrency {concurrency} load gates failed: {'; '.join(reasons)}"
            )
        if successful_rps <= 0:
            failures.append(f"concurrency {concurrency} successful_throughput_rps must be > 0")
        points.append(throughput_point(concurrency, overall))

    overall_curve = analyze_curve(
        points,
        min_throughput_gain_rate,
        max_throughput_drop_rate,
    )
    for transition in overall_curve["transitions"]:
        if transition["classification"] == "regressed":
            failures.append(
                f"concurrency {transition['to_concurrency']} throughput drop "
                f"{-transition['throughput_gain_rate']:.1%} > {max_throughput_drop_rate:.1%}"
            )

    scenario_curves: dict[str, dict[str, Any]] = {}
    weights = ordered[0].get("config", {}).get("weights", {}) if ordered else {}
    for name, weight in weights.items():
        if weight <= 0:
            continue
        scenario_points = [
            throughput_point(
                int(report["config"]["concurrency"]),
                report.get("scenarios", {}).get(name, {}),
            )
            for report in ordered
        ]
        scenario_curves[name] = analyze_curve(
            scenario_points,
            min_throughput_gain_rate,
            max_throughput_drop_rate,
        )

    bottleneck_candidates = [
        (
            curve["first_saturation_concurrency"],
            0 if curve["status"] == "regressed" else 1,
            name,
        )
        for name, curve in scenario_curves.items()
        if curve["first_saturation_concurrency"] is not None
    ]
    bottleneck_scenario = min(bottleneck_candidates)[2] if bottleneck_candidates else None
    diagnostic_saturation = overall_curve["status"] != "scaling" or bool(bottleneck_candidates)
    status = "failed" if failures else "saturated" if diagnostic_saturation else "scaling"
    return {
        "schema_version": 1,
        "status": status,
        "min_throughput_gain_rate": min_throughput_gain_rate,
        "max_throughput_drop_rate": max_throughput_drop_rate,
        "recommended_concurrency": overall_curve["recommended_concurrency"],
        "first_saturation_concurrency": overall_curve["first_saturation_concurrency"],
        "max_observed": overall_curve["max_observed"],
        "points": overall_curve["points"],
        "transitions": overall_curve["transitions"],
        "scenario_curves": scenario_curves,
        "bottleneck_scenario": bottleneck_scenario,
        "failures": failures,
    }, failures


def load_reports(path: Path) -> list[dict[str, Any]]:
    reports: list[dict[str, Any]] = []
    for report_path in sorted(path.glob("*.json")):
        payload = json.loads(report_path.read_text(encoding="utf-8"))
        if not isinstance(payload, dict):
            raise ValueError(f"report root must be an object: {report_path}")
        reports.append(payload)
    return reports


def main() -> int:
    parser = argparse.ArgumentParser(description="Analyze public load saturation reports")
    parser.add_argument("--reports-dir", required=True)
    parser.add_argument("--expected-concurrency", action="append", type=int, default=[])
    parser.add_argument("--min-throughput-gain-rate", type=float, default=0.10)
    parser.add_argument("--max-throughput-drop-rate", type=float, default=0.10)
    parser.add_argument("--output-json", default="")
    args = parser.parse_args()

    if not 0 <= args.min_throughput_gain_rate < 1:
        raise SystemExit("min-throughput-gain-rate must be >= 0 and < 1")
    if not 0 <= args.max_throughput_drop_rate < 1:
        raise SystemExit("max-throughput-drop-rate must be >= 0 and < 1")

    try:
        reports = load_reports(Path(args.reports_dir))
        summary, failures = analyze_saturation(
            reports,
            args.expected_concurrency,
            args.min_throughput_gain_rate,
            args.max_throughput_drop_rate,
        )
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Saturation analysis failed: {exc}")
        return 2

    print(json.dumps(summary, ensure_ascii=False, indent=2))
    if args.output_json:
        output = Path(args.output_json)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    return 2 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
