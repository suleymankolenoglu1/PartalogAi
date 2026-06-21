#!/usr/bin/env python3
"""Audit chat eval JSON reports for release-readiness signals.

This script is intentionally dependency-free so it can run in a bare Python
environment before the full AI runtime is installed.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def pct(value: Any) -> str:
    if isinstance(value, (int, float)):
        return f"{value:.2%}"
    return "-"


def ms(value: Any) -> str:
    if isinstance(value, (int, float)):
        return f"{value:.1f}ms"
    return "-"


def load_report(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError(f"{path}: report must be a JSON object")
    return payload


def infer_findings(summary: dict[str, Any], cases: list[dict[str, Any]]) -> list[str]:
    findings: list[str] = []

    total = summary.get("total") or len(cases)
    success_rate = summary.get("success_rate")
    hit_at_1 = summary.get("hit_at_1")
    hallucination_rate = summary.get("hallucination_rate")

    statuses: dict[str, int] = {}
    for case in cases:
        status = str(case.get("status") or "")
        if status:
            statuses[status] = statuses.get(status, 0) + 1

    if total and success_rate == 1.0 and hit_at_1 == 0.0 and hallucination_rate == 0.0:
        findings.append(
            "stale_expected_codes_suspected: requests succeeded without hallucination but retrieval metrics are zero"
        )

    if statuses.get("403", 0) or statuses.get("429", 0):
        findings.append(
            f"quota_or_rate_limit_pollution: 403={statuses.get('403', 0)} 429={statuses.get('429', 0)}"
        )

    if isinstance(summary.get("latency_ms_p95"), (int, float)) and summary["latency_ms_p95"] > 8000:
        findings.append("latency_gate_risk: p95 exceeds 8000ms")

    if not findings:
        findings.append("no_obvious_release_blocker_in_report_summary")

    return findings


def summarize(path: Path) -> tuple[str, list[str]]:
    report = load_report(path)
    summary = report.get("summary") or {}
    cases = report.get("results") or report.get("cases") or []
    if not isinstance(cases, list):
        cases = []

    line = (
        f"| {path.name} | {summary.get('total', len(cases))} | "
        f"{pct(summary.get('success_rate'))} | {pct(summary.get('hit_at_1'))} | "
        f"{pct(summary.get('hit_at_3'))} | {summary.get('mrr', '-') if summary.get('mrr') is not None else '-'} | "
        f"{ms(summary.get('latency_ms_p95'))} | {pct(summary.get('hallucination_rate'))} |"
    )
    return line, infer_findings(summary, cases)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("reports", nargs="+", help="Eval report JSON files")
    parser.add_argument("--output-md", help="Optional markdown output path")
    args = parser.parse_args()

    lines = [
        "# Chat Eval Report Audit",
        "",
        "| report | total | success | Hit@1 | Hit@3 | MRR | p95 | hallucination |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    finding_lines = ["", "## Findings", ""]

    exit_code = 0
    for raw in args.reports:
        path = Path(raw)
        row, findings = summarize(path)
        lines.append(row)
        finding_lines.append(f"### {path.name}")
        for finding in findings:
            finding_lines.append(f"- {finding}")
            if not finding.startswith("no_obvious"):
                exit_code = 2
        finding_lines.append("")

    output = "\n".join(lines + finding_lines)
    if args.output_md:
        Path(args.output_md).write_text(output, encoding="utf-8")
    print(output)
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
