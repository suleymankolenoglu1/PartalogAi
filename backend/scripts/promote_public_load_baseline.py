#!/usr/bin/env python3
"""Create an approved public load baseline from a passed staging load report."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


DEFAULT_OUTPUT = "backend/load-baselines/public-e2e-load-baseline.json"
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


def enabled_scenarios(report: dict[str, Any]) -> list[str]:
    weights = report.get("config", {}).get("weights", {})
    if not isinstance(weights, dict):
        return []
    return [name for name, weight in weights.items() if float(weight or 0) > 0]


def validate_promotable_report(
    report: dict[str, Any],
    require_base_url_contains: str = "",
) -> list[str]:
    failures: list[str] = []
    config = report.get("config", {})

    if report.get("schema_version") != 1:
        failures.append("schema_version must be 1")
    if report.get("status") != "passed":
        failures.append("report status must be passed")
    if report.get("threshold_failures"):
        failures.append("threshold_failures must be empty")

    for field in REQUIRED_CONFIG_FIELDS:
        if field not in config:
            failures.append(f"config.{field} is required")

    required_base_url = require_base_url_contains.strip()
    base_url = str(config.get("base_url") or "")
    if required_base_url and required_base_url not in base_url:
        failures.append(
            f"config.base_url must contain '{required_base_url}' for baseline promotion"
        )

    comparison = report.get("baseline_comparison") or {}
    comparison_status = comparison.get("status", "skipped")
    if comparison_status not in {"passed", "skipped"}:
        failures.append(f"baseline_comparison status must be passed or skipped, got {comparison_status}")

    overall_rps = float(report.get("overall", {}).get("successful_throughput_rps", 0.0))
    if overall_rps <= 0:
        failures.append("overall successful_throughput_rps must be > 0")

    scenarios = report.get("scenarios", {})
    if not isinstance(scenarios, dict):
        failures.append("scenarios must be an object")
        scenarios = {}
    for scenario in enabled_scenarios(report):
        scenario_rps = float(scenarios.get(scenario, {}).get("successful_throughput_rps", 0.0))
        if scenario_rps <= 0:
            failures.append(f"{scenario} successful_throughput_rps must be > 0")

    return failures


def select_report_from_saturation(
    saturation_summary: dict[str, Any],
    reports_dir: Path,
) -> dict[str, Any]:
    if saturation_summary.get("status") == "failed":
        raise ValueError("saturation summary status is failed")
    if saturation_summary.get("failures"):
        raise ValueError("saturation summary contains failures")

    recommended = int(saturation_summary.get("recommended_concurrency") or 0)
    if recommended <= 0:
        raise ValueError("saturation summary recommended_concurrency must be > 0")

    for path in sorted(reports_dir.glob("public-e2e-saturation-*.json")):
        report = load_json_object(path)
        if int(report.get("config", {}).get("concurrency", 0)) == recommended:
            return report

    raise ValueError(f"no report found for recommended concurrency {recommended}")


def baseline_payload(report: dict[str, Any], source: dict[str, Any]) -> dict[str, Any]:
    config = report["config"]
    payload = {
        "schema_version": report["schema_version"],
        "status": report["status"],
        "baseline_metadata": source,
        "config": {
            "duration_seconds": config["duration_seconds"],
            "concurrency": config["concurrency"],
            "timeout_seconds": config["timeout_seconds"],
            "weights": config["weights"],
            "chat_queries": config["chat_queries"],
            "thresholds": config.get("thresholds", {}),
        },
        "overall": report.get("overall", {}),
        "scenarios": report.get("scenarios", {}),
        "threshold_failures": report.get("threshold_failures", []),
    }
    comparison = report.get("baseline_comparison")
    if comparison:
        payload["source_baseline_comparison"] = comparison
    return payload


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Promote a staging public load report to baseline JSON")
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--report-json", default="", help="Passed public load report to promote")
    source.add_argument("--saturation-summary-json", default="", help="Saturation summary used to select a report")
    parser.add_argument("--reports-dir", default="", help="Directory containing saturation step reports")
    parser.add_argument("--output-json", default=DEFAULT_OUTPUT)
    parser.add_argument(
        "--require-base-url-contains",
        default="",
        help="Optional substring required in config.base_url, for example staging",
    )
    parser.add_argument("--dry-run", action="store_true", help="Validate and print without writing")
    args = parser.parse_args()

    try:
        source_metadata: dict[str, Any]
        if args.report_json:
            report_path = Path(args.report_json)
            report = load_json_object(report_path)
            source_metadata = {"source": "report", "report_json": str(report_path)}
        else:
            if not args.reports_dir:
                raise ValueError("--reports-dir is required with --saturation-summary-json")
            summary_path = Path(args.saturation_summary_json)
            saturation_summary = load_json_object(summary_path)
            report = select_report_from_saturation(saturation_summary, Path(args.reports_dir))
            source_metadata = {
                "source": "saturation",
                "saturation_summary_json": str(summary_path),
                "recommended_concurrency": saturation_summary.get("recommended_concurrency"),
                "saturation_status": saturation_summary.get("status"),
                "bottleneck_scenario": saturation_summary.get("bottleneck_scenario"),
            }

        failures = validate_promotable_report(report, args.require_base_url_contains)
        if failures:
            print("--- Baseline promotion failed ---")
            for failure in failures:
                print(f"- {failure}")
            return 2

        payload = baseline_payload(report, source_metadata)
        if args.dry_run:
            print(json.dumps(payload, ensure_ascii=False, indent=2))
        else:
            write_json(Path(args.output_json), payload)
            print(f"Baseline written: {args.output_json}")
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Baseline promotion failed: {exc}")
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
