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


def format_percent(value: Any) -> str:
    try:
        return f"{float(value):.2%}"
    except (TypeError, ValueError):
        return "-"


def format_float(value: Any, suffix: str = "") -> str:
    try:
        return f"{float(value):.2f}{suffix}"
    except (TypeError, ValueError):
        return "-"


def baseline_review_markdown(payload: dict[str, Any]) -> str:
    config = payload.get("config", {})
    overall = payload.get("overall", {})
    metadata = payload.get("baseline_metadata", {})
    thresholds = config.get("thresholds", {})
    lines = [
        "# Public E2E Load Baseline Candidate",
        "",
        "## Review Decision",
        "",
        "- [ ] Candidate came from the intended staging environment.",
        "- [ ] Overall and enabled-scenario successful throughput look stable.",
        "- [ ] p95 latency is comfortably below the configured thresholds.",
        "- [ ] Stream degraded fallback and first-token latency are acceptable.",
        "- [ ] No bottleneck scenario blocks the intended rollout profile.",
        "",
        "## Source",
        "",
        f"- Source: `{metadata.get('source', '-')}`",
        f"- Recommended concurrency: `{metadata.get('recommended_concurrency', config.get('concurrency', '-'))}`",
        f"- Saturation status: `{metadata.get('saturation_status', '-')}`",
        f"- Bottleneck scenario: `{metadata.get('bottleneck_scenario') or '-'}`",
        "",
        "## Profile",
        "",
        f"- Duration: `{config.get('duration_seconds', '-')}s`",
        f"- Concurrency: `{config.get('concurrency', '-')}`",
        f"- Timeout: `{config.get('timeout_seconds', '-')}s`",
        f"- Chat queries: `{', '.join(str(q) for q in config.get('chat_queries', [])) or '-'}`",
        f"- Weights: `{json.dumps(config.get('weights', {}), ensure_ascii=False, sort_keys=True)}`",
        f"- Thresholds: `{json.dumps(thresholds, ensure_ascii=False, sort_keys=True)}`",
        "",
        "## Overall",
        "",
        f"- Successful throughput: `{format_float(overall.get('successful_throughput_rps'), ' req/s')}`",
        f"- Success rate: `{format_percent(overall.get('success_rate'))}`",
        f"- p95 latency: `{format_float(overall.get('latency_p95_ms'), ' ms')}`",
        "",
        "## Scenarios",
        "",
        "| scenario | successful rps | success | p95 ms | degraded fallback | first token p95 |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for name, summary in sorted((payload.get("scenarios") or {}).items()):
        lines.append(
            f"| {name} | "
            f"{format_float(summary.get('successful_throughput_rps'))} | "
            f"{format_percent(summary.get('success_rate'))} | "
            f"{format_float(summary.get('latency_p95_ms'))} | "
            f"{format_percent(summary.get('degraded_fallback_rate'))} | "
            f"{format_float(summary.get('first_token_latency_p95_ms'))} |"
        )

    comparison = payload.get("source_baseline_comparison") or {}
    if comparison:
        lines.extend(
            [
                "",
                "## Previous Baseline Comparison",
                "",
                f"- Status: `{comparison.get('status', '-')}`",
                f"- Max regression rate: `{format_percent(comparison.get('max_regression_rate'))}`",
            ]
        )
        metrics = comparison.get("metrics") or {}
        if metrics:
            lines.extend(
                [
                    "",
                    "| metric | previous rps | candidate rps | regression | result |",
                    "|---|---:|---:|---:|---|",
                ]
            )
            for name, metric in sorted(metrics.items()):
                result = "pass" if metric.get("passed") else "fail"
                lines.append(
                    f"| {name} | "
                    f"{format_float(metric.get('baseline_rps'))} | "
                    f"{format_float(metric.get('current_rps'))} | "
                    f"{format_percent(metric.get('regression_rate'))} | "
                    f"{result} |"
                )

    lines.append("")
    return "\n".join(lines)


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Promote a staging public load report to baseline JSON")
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--report-json", default="", help="Passed public load report to promote")
    source.add_argument("--saturation-summary-json", default="", help="Saturation summary used to select a report")
    parser.add_argument("--reports-dir", default="", help="Directory containing saturation step reports")
    parser.add_argument("--output-json", default=DEFAULT_OUTPUT)
    parser.add_argument("--output-md", default="", help="Optional markdown review output path")
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
            if args.output_md:
                write_text(Path(args.output_md), baseline_review_markdown(payload))
                print(f"Baseline review written: {args.output_md}")
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Baseline promotion failed: {exc}")
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
