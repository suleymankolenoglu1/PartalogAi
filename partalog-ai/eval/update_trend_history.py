from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ALERT_LOOKBACK = 7


def load_summary(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8")).get("summary", {})


def load_history(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        return []

    rows: list[dict[str, Any]] = []
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line:
            continue
        rows.append(json.loads(line))
    return rows


def append_snapshot(
    history: list[dict[str, Any]],
    *,
    nightly: dict[str, Any],
    behavior: dict[str, Any],
    run_at: str,
    keep_last: int,
) -> list[dict[str, Any]]:
    rows = [
        *history,
        {
            "run_at": run_at,
            "retrieval": nightly,
            "behavior": behavior,
        },
    ]
    return rows[-keep_last:] if keep_last > 0 else rows


def write_history(path: Path, rows: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = "\n".join(json.dumps(row, ensure_ascii=False, separators=(",", ":")) for row in rows)
    path.write_text(payload + ("\n" if payload else ""), encoding="utf-8")


def build_trend_markdown(rows: list[dict[str, Any]], *, limit: int = 7) -> str:
    recent = rows[-limit:]
    lines = [
        "# Nightly Chat Eval Trend",
        "",
        "| run_at | retrieval_success | retrieval_hit@1 | behavior_success | behavior_required | behavior_forbidden | hallucination |",
        "|---|---:|---:|---:|---:|---:|---:|",
    ]
    for row in recent:
        retrieval = row.get("retrieval", {})
        behavior = row.get("behavior", {})
        lines.append(
            "| {run_at} | {retrieval_success:.2%} | {retrieval_hit:.2%} | "
            "{behavior_success:.2%} | {behavior_required:.2%} | "
            "{behavior_forbidden:.2%} | {hallucination:.2%} |".format(
                run_at=row.get("run_at", "-"),
                retrieval_success=float(retrieval.get("success_rate", 0.0)),
                retrieval_hit=float(retrieval.get("hit_at_1", 0.0)),
                behavior_success=float(behavior.get("success_rate", 0.0)),
                behavior_required=float(behavior.get("required_term_pass_rate", 0.0)),
                behavior_forbidden=float(behavior.get("forbidden_term_pass_rate", 0.0)),
                hallucination=float(behavior.get("hallucination_rate", 0.0)),
            )
        )
    return "\n".join(lines)


def _average(rows: list[dict[str, Any]], group: str, metric: str) -> float:
    values = [
        float(row.get(group, {}).get(metric, 0.0))
        for row in rows
        if metric in row.get(group, {})
    ]
    return sum(values) / len(values) if values else 0.0


def build_alerts(rows: list[dict[str, Any]], *, lookback: int = ALERT_LOOKBACK) -> list[str]:
    if len(rows) < 2:
        return ["Yeterli geçmiş yok; alarm baz çizgisi oluşması için en az 2 koşu gerekli."]

    current = rows[-1]
    baseline = rows[max(0, len(rows) - lookback - 1):-1]
    alerts: list[str] = []

    retrieval = current.get("retrieval", {})
    behavior = current.get("behavior", {})
    retrieval_hit = float(retrieval.get("hit_at_1", 0.0))
    retrieval_hit_baseline = _average(baseline, "retrieval", "hit_at_1")
    if retrieval_hit_baseline - retrieval_hit >= 0.05:
        alerts.append(
            f"Retrieval Hit@1 son koşuda {retrieval_hit:.2%}; önceki ortalamanın "
            f"{retrieval_hit_baseline - retrieval_hit:.2%} altında."
        )

    current_latency = float(retrieval.get("latency_ms_p95", 0.0))
    baseline_latency = _average(baseline, "retrieval", "latency_ms_p95")
    if baseline_latency > 0 and current_latency >= baseline_latency * 1.25:
        alerts.append(
            f"Retrieval p95 latency {current_latency:.1f} ms; önceki ortalama "
            f"{baseline_latency:.1f} ms seviyesinden en az %25 yüksek."
        )

    required_pass = float(behavior.get("required_term_pass_rate", 0.0))
    required_baseline = _average(baseline, "behavior", "required_term_pass_rate")
    if required_baseline - required_pass >= 0.05:
        alerts.append(
            f"Behavior required-term pass son koşuda {required_pass:.2%}; önceki ortalamanın "
            f"{required_baseline - required_pass:.2%} altında."
        )

    forbidden_pass = float(behavior.get("forbidden_term_pass_rate", 0.0))
    forbidden_baseline = _average(baseline, "behavior", "forbidden_term_pass_rate")
    if forbidden_baseline - forbidden_pass >= 0.05:
        alerts.append(
            f"Behavior forbidden-term pass son koşuda {forbidden_pass:.2%}; önceki ortalamanın "
            f"{forbidden_baseline - forbidden_pass:.2%} altında."
        )

    hallucination = float(behavior.get("hallucination_rate", 0.0))
    hallucination_baseline = _average(baseline, "behavior", "hallucination_rate")
    if hallucination > 0 and hallucination > hallucination_baseline:
        alerts.append(
            f"Behavior hallucination oranı {hallucination:.2%}; önceki ortalama "
            f"{hallucination_baseline:.2%} seviyesinin üstüne çıktı."
        )

    return alerts or ["Anlamlı bozulma algılanmadı."]


def build_alert_markdown(rows: list[dict[str, Any]]) -> str:
    lines = ["# Nightly Chat Eval Alerts", ""]
    lines.extend(f"- {alert}" for alert in build_alerts(rows))
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description="Append nightly chat eval summaries to trend history.")
    parser.add_argument("--nightly-report", default="eval/report.nightly.json")
    parser.add_argument("--behavior-report", default="eval/report.behavior_smoke.json")
    parser.add_argument("--history-jsonl", default="eval/history/chat_eval_trend.jsonl")
    parser.add_argument("--output-md", default="eval/report.nightly.trend.md")
    parser.add_argument("--alerts-md", default="eval/report.nightly.alerts.md")
    parser.add_argument("--keep-last", type=int, default=90)
    parser.add_argument("--run-at", default="")
    args = parser.parse_args()

    nightly = load_summary(Path(args.nightly_report))
    behavior = load_summary(Path(args.behavior_report))
    history_path = Path(args.history_jsonl)
    rows = load_history(history_path)
    run_at = args.run_at or datetime.now(timezone.utc).isoformat()
    rows = append_snapshot(
        rows,
        nightly=nightly,
        behavior=behavior,
        run_at=run_at,
        keep_last=args.keep_last,
    )

    write_history(history_path, rows)
    markdown = build_trend_markdown(rows)
    Path(args.output_md).write_text(markdown, encoding="utf-8")
    alerts_markdown = build_alert_markdown(rows)
    Path(args.alerts_md).write_text(alerts_markdown, encoding="utf-8")
    print(markdown)
    print()
    print(alerts_markdown)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
