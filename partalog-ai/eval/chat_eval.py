import argparse
import asyncio
import json
import os
import re
import statistics
import time
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple
from datetime import datetime, timezone

import aiohttp


CODE_PATTERN = re.compile(r"\b[A-Z0-9][A-Z0-9\-]{4,}\b")
GENERIC_ERROR_PATTERNS = (
    "sistem hatası oluştu",
    "ai servisine şu an ulaşılamıyor",
    "hata",
)


def load_cases(path: str) -> List[Dict[str, Any]]:
    p = Path(path)
    if not p.exists():
        raise FileNotFoundError(f"Cases file not found: {path}")

    if p.suffix.lower() == ".json":
        payload = json.loads(p.read_text(encoding="utf-8"))
        if not isinstance(payload, list):
            raise ValueError("JSON case file must be a list.")
        return payload

    cases: List[Dict[str, Any]] = []
    with p.open("r", encoding="utf-8") as f:
        for idx, line in enumerate(f, start=1):
            raw = line.strip()
            if not raw or raw.startswith("#"):
                continue
            try:
                cases.append(json.loads(raw))
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSONL at line {idx}: {exc}") from exc
    return cases


def resolve_case_placeholders(cases: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    env_token = (os.getenv("PARTALOG_PUBLIC_TOKEN") or "").strip()
    env_catalog_ids = [
        x.strip()
        for x in (os.getenv("PARTALOG_CATALOG_IDS") or "").split(",")
        if x.strip()
    ]

    resolved: List[Dict[str, Any]] = []
    for case in cases:
        c = dict(case)
        token = str(c.get("public_token") or "").strip()
        if token == "<PUBLIC_TOKEN>":
            if env_token:
                c["public_token"] = env_token
            else:
                raise ValueError(
                    f"Case '{c.get('id', '?')}' uses <PUBLIC_TOKEN>. "
                    "Set PARTALOG_PUBLIC_TOKEN or update case file."
                )

        raw_catalog_ids = c.get("catalog_ids") or []
        if isinstance(raw_catalog_ids, list):
            if any(str(x).strip() == "<CATALOG_GUID>" for x in raw_catalog_ids):
                if env_catalog_ids:
                    c["catalog_ids"] = env_catalog_ids
                else:
                    # Placeholder bırakıldıysa filtreyi tamamen kaldır.
                    c["catalog_ids"] = []

        resolved.append(c)
    return resolved


def extract_codes_from_response(resp: Dict[str, Any]) -> List[str]:
    codes: List[str] = []

    products = resp.get("products") or []
    for p in products:
        code = p.get("code") or p.get("Code")
        if code:
            codes.append(str(code).strip().upper())

    compare_groups = resp.get("compareGroups") or []
    for group in compare_groups:
        results = group.get("results") or []
        for p in results:
            code = p.get("code") or p.get("Code")
            if code:
                codes.append(str(code).strip().upper())

    # stable dedup
    out: List[str] = []
    seen = set()
    for c in codes:
        if not c or c in seen:
            continue
        seen.add(c)
        out.append(c)
    return out


def extract_allowed_identifiers_from_response(resp: Dict[str, Any]) -> List[str]:
    identifiers: List[str] = []

    def collect_identifiers_from_text(value: Any) -> None:
        if not value:
            return
        for tok in extract_codes_from_text(str(value)):
            identifiers.append(tok)

    def collect_from_product(p: Dict[str, Any]) -> None:
        code = p.get("code") or p.get("Code")
        if code:
            identifiers.append(str(code).strip().upper())

        model = p.get("model") or p.get("Model")
        if model:
            identifiers.append(str(model).strip().upper())

        collect_identifiers_from_text(p.get("name") or p.get("Name"))
        collect_identifiers_from_text(p.get("description") or p.get("Description"))

    for p in (resp.get("products") or []):
        collect_from_product(p)

    for group in (resp.get("compareGroups") or []):
        for p in (group.get("results") or []):
            collect_from_product(p)

    out: List[str] = []
    seen = set()
    for val in identifiers:
        if not val or val in seen:
            continue
        seen.add(val)
        out.append(val)
    return out


def best_rank(codes: List[str], expected: List[str]) -> Optional[int]:
    if not expected:
        return None
    expected_set = {e.strip().upper() for e in expected if e and e.strip()}
    for idx, c in enumerate(codes, start=1):
        if c in expected_set:
            return idx
    return None


def precision_at_k(codes: List[str], expected: List[str], k: int) -> Optional[float]:
    if not expected:
        return None
    expected_set = {e.strip().upper() for e in expected if e and e.strip()}
    if not expected_set:
        return None
    top_k = [c.strip().upper() for c in codes[:k] if c and c.strip()]
    if not top_k:
        return 0.0
    hits = sum(1 for c in top_k if c in expected_set)
    return hits / min(k, len(top_k))


def case_age_days(case: Dict[str, Any]) -> Optional[int]:
    raw = str(case.get("last_verified_at") or case.get("updated_at") or "").strip()
    if not raw:
        return None

    try:
        verified_at = datetime.fromisoformat(raw.replace("Z", "+00:00"))
    except ValueError:
        return None

    if verified_at.tzinfo is None:
        verified_at = verified_at.replace(tzinfo=timezone.utc)

    return max(0, (datetime.now(timezone.utc) - verified_at.astimezone(timezone.utc)).days)


def percentile(values: List[float], p: float) -> float:
    if not values:
        return 0.0
    sorted_vals = sorted(values)
    if len(sorted_vals) == 1:
        return sorted_vals[0]
    rank = (len(sorted_vals) - 1) * p
    low = int(rank)
    high = min(low + 1, len(sorted_vals) - 1)
    frac = rank - low
    return sorted_vals[low] * (1 - frac) + sorted_vals[high] * frac


def extract_codes_from_text(text: str) -> List[str]:
    if not text:
        return []
    out = set()
    for m in CODE_PATTERN.finditer(text):
        tok = m.group(0).strip().upper()
        # Marka gibi düz kelimeleri (örn: YAMATO) hariç tut, sadece sayısal kimlikleri yakala.
        if any(ch.isdigit() for ch in tok):
            out.add(tok)
    return list(out)


def check_required_forbidden(
    text: str,
    required_terms: List[str],
    forbidden_terms: List[str],
) -> Tuple[bool, bool]:
    norm = (text or "").lower()
    req_ok = all(term.lower() in norm for term in required_terms) if required_terms else True
    forb_ok = all(term.lower() not in norm for term in forbidden_terms) if forbidden_terms else True
    return req_ok, forb_ok


def _is_identifier_covered(token: str, allowed_identifiers: List[str]) -> bool:
    t = token.strip().upper()
    if not t:
        return False
    allowed = [a.strip().upper() for a in allowed_identifiers if a and a.strip()]
    if t in allowed:
        return True
    for a in allowed:
        # Örn: VG2500 -> VG2500-8F kabul edilsin.
        if a.startswith(t + "-") or a.startswith(t + "/") or a.startswith(t + "_"):
            return True
    return False


async def run_case(
    session: aiohttp.ClientSession,
    base_url: str,
    endpoint: str,
    case: Dict[str, Any],
) -> Dict[str, Any]:
    url = f"{base_url.rstrip('/')}{endpoint}"
    form = aiohttp.FormData()

    text = case.get("text")
    message = case.get("message")
    history = case.get("history") or []
    catalog_ids = case.get("catalog_ids") or []
    public_token = case.get("public_token")
    image_path = case.get("image_path")

    if text:
        form.add_field("text", str(text))
    if message:
        form.add_field("message", str(message))
    form.add_field("history", json.dumps(history, ensure_ascii=False))
    form.add_field("catalog_ids", json.dumps(catalog_ids))
    if public_token:
        form.add_field("publicToken", str(public_token))

    started = time.perf_counter()
    status = 0
    payload: Dict[str, Any] = {}
    error: Optional[str] = None

    try:
        if image_path:
            img = Path(image_path)
            if not img.exists():
                raise FileNotFoundError(f"image_path not found: {image_path}")
            with img.open("rb") as f:
                form.add_field("image", f, filename=img.name, content_type="application/octet-stream")
                async with session.post(url, data=form) as resp:
                    status = resp.status
                    if resp.status == 200:
                        payload = await resp.json()
                    else:
                        error = await resp.text()
        else:
            async with session.post(url, data=form) as resp:
                status = resp.status
                if resp.status == 200:
                    payload = await resp.json()
                else:
                    error = await resp.text()
    except Exception as exc:
        error = f"{type(exc).__name__}: {exc}"

    latency_ms = (time.perf_counter() - started) * 1000.0

    reply_text = (
        payload.get("replySuggestion")
        or payload.get("answer")
        or payload.get("reply")
        or ""
    )
    codes = extract_codes_from_response(payload) if payload else []
    allowed_identifiers = extract_allowed_identifiers_from_response(payload) if payload else []
    mentioned_codes = extract_codes_from_text(reply_text)
    user_text_identifiers = extract_codes_from_text(str(text or message or ""))
    expected_codes = [str(x).strip().upper() for x in (case.get("expected_codes") or []) if str(x).strip()]
    expect_no_codes = bool(case.get("expect_no_codes", False))
    rank = best_rank(codes, expected_codes)
    precision3 = precision_at_k(codes, expected_codes, 3)
    req_ok, forb_ok = check_required_forbidden(
        reply_text,
        case.get("required_terms") or [],
        case.get("forbidden_terms") or [],
    )
    age_days = case_age_days(case)

    hallucinated_codes = sorted(
        [
            tok
            for tok in set(mentioned_codes)
            if tok not in set(user_text_identifiers) and not _is_identifier_covered(tok, allowed_identifiers)
        ]
    )

    reply_norm = (reply_text or "").strip().lower()
    logical_error = any(pat in reply_norm for pat in GENERIC_ERROR_PATTERNS)

    return {
        "id": case.get("id"),
        "status": status,
        "ok": status == 200 and error is None and not logical_error,
        "error": error,
        "logical_error": logical_error,
        "latency_ms": latency_ms,
        "reply_text": reply_text,
        "reply_len": len(reply_text),
        "codes": codes,
        "allowed_identifiers": allowed_identifiers,
        "source_count": len(codes),
        "expected_codes": expected_codes,
        "expect_no_codes": expect_no_codes,
        "no_code_ok": (len(codes) == 0) if expect_no_codes else None,
        "rank": rank,
        "hit_at_1": rank is not None and rank <= 1,
        "hit_at_3": rank is not None and rank <= 3,
        "hit_at_5": rank is not None and rank <= 5,
        "precision_at_3": precision3,
        "mrr": (1.0 / rank) if rank else 0.0,
        "required_ok": req_ok,
        "forbidden_ok": forb_ok,
        "case_age_days": age_days,
        "case_has_freshness_metadata": age_days is not None,
        "mentioned_codes": mentioned_codes,
        "hallucinated_codes": hallucinated_codes,
        "case": case,
    }


def summarize(results: List[Dict[str, Any]]) -> Dict[str, Any]:
    total = len(results)
    ok_results = [r for r in results if r["ok"]]
    error_results = [r for r in results if not r["ok"]]

    latencies = [r["latency_ms"] for r in ok_results]
    source_counts = [r["source_count"] for r in ok_results]
    reply_lens = [r["reply_len"] for r in ok_results]

    expected_cases = [r for r in ok_results if r["expected_codes"]]
    no_code_cases = [r for r in ok_results if r.get("expect_no_codes")]
    precision3_values = [r["precision_at_3"] for r in expected_cases if r.get("precision_at_3") is not None]
    stale_case_results = [r for r in results if r.get("case_age_days") is not None]

    hallucination_cases = [r for r in ok_results if r["mentioned_codes"]]
    hallucination_hits = [r for r in hallucination_cases if r["hallucinated_codes"]]

    return {
        "total": total,
        "ok": len(ok_results),
        "errors": len(error_results),
        "success_rate": (len(ok_results) / total) if total else 0.0,
        "latency_ms_avg": statistics.mean(latencies) if latencies else 0.0,
        "latency_ms_p95": percentile(latencies, 0.95) if latencies else 0.0,
        "source_count_avg": statistics.mean(source_counts) if source_counts else 0.0,
        "reply_len_avg": statistics.mean(reply_lens) if reply_lens else 0.0,
        "expected_case_count": len(expected_cases),
        "hit_at_1": (sum(1 for r in expected_cases if r["hit_at_1"]) / len(expected_cases)) if expected_cases else 0.0,
        "hit_at_3": (sum(1 for r in expected_cases if r["hit_at_3"]) / len(expected_cases)) if expected_cases else 0.0,
        "hit_at_5": (sum(1 for r in expected_cases if r["hit_at_5"]) / len(expected_cases)) if expected_cases else 0.0,
        "precision_at_3": statistics.mean(precision3_values) if precision3_values else 0.0,
        "mrr": (sum(r["mrr"] for r in expected_cases) / len(expected_cases)) if expected_cases else 0.0,
        "no_code_case_count": len(no_code_cases),
        "no_code_pass_rate": (sum(1 for r in no_code_cases if r.get("no_code_ok")) / len(no_code_cases)) if no_code_cases else 0.0,
        "required_term_pass_rate": (sum(1 for r in ok_results if r["required_ok"]) / len(ok_results)) if ok_results else 0.0,
        "forbidden_term_pass_rate": (sum(1 for r in ok_results if r["forbidden_ok"]) / len(ok_results)) if ok_results else 0.0,
        "hallucination_rate": (len(hallucination_hits) / len(hallucination_cases)) if hallucination_cases else 0.0,
        "freshness_metadata_case_count": len(stale_case_results),
        "max_case_age_days": max((r["case_age_days"] or 0) for r in stale_case_results) if stale_case_results else 0,
    }


def print_summary(summary: Dict[str, Any]) -> None:
    print("\n--- Summary ---")
    print(f"Total: {summary['total']}")
    print(f"Success: {summary['ok']} | Errors: {summary['errors']} | SuccessRate: {summary['success_rate']:.2%}")
    print(f"Latency avg/p95 (ms): {summary['latency_ms_avg']:.1f} / {summary['latency_ms_p95']:.1f}")
    print(f"Avg source count: {summary['source_count_avg']:.2f}")
    print(f"Avg reply length: {summary['reply_len_avg']:.1f}")
    print(f"Expected-case count: {summary['expected_case_count']}")
    print(f"Hit@1: {summary['hit_at_1']:.2%}")
    print(f"Hit@3: {summary['hit_at_3']:.2%}")
    print(f"Hit@5: {summary['hit_at_5']:.2%}")
    print(f"Precision@3: {summary['precision_at_3']:.2%}")
    print(f"MRR: {summary['mrr']:.3f}")
    print(f"No-code pass: {summary['no_code_pass_rate']:.2%} (cases={summary['no_code_case_count']})")
    print(f"Required-term pass: {summary['required_term_pass_rate']:.2%}")
    print(f"Forbidden-term pass: {summary['forbidden_term_pass_rate']:.2%}")
    print(f"Hallucination rate: {summary['hallucination_rate']:.2%}")
    print(
        "Freshness metadata: "
        f"cases={summary['freshness_metadata_case_count']} "
        f"max_age_days={summary['max_case_age_days']}"
    )


def write_markdown(path: str, summary: Dict[str, Any], results: List[Dict[str, Any]]) -> None:
    lines: List[str] = []
    lines.append("# Chat Eval Report")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Total: `{summary['total']}`")
    lines.append(f"- Success: `{summary['ok']}`")
    lines.append(f"- Errors: `{summary['errors']}`")
    lines.append(f"- SuccessRate: `{summary['success_rate']:.2%}`")
    lines.append(f"- Latency avg/p95 (ms): `{summary['latency_ms_avg']:.1f}` / `{summary['latency_ms_p95']:.1f}`")
    lines.append(f"- Hit@1 / Hit@3 / Hit@5: `{summary['hit_at_1']:.2%}` / `{summary['hit_at_3']:.2%}` / `{summary['hit_at_5']:.2%}`")
    lines.append(f"- Precision@3: `{summary['precision_at_3']:.2%}`")
    lines.append(f"- MRR: `{summary['mrr']:.3f}`")
    lines.append(f"- No-code pass rate: `{summary['no_code_pass_rate']:.2%}` (cases={summary['no_code_case_count']})")
    lines.append(f"- Hallucination rate: `{summary['hallucination_rate']:.2%}`")
    lines.append(f"- Freshness metadata cases: `{summary['freshness_metadata_case_count']}`")
    lines.append(f"- Max case age days: `{summary['max_case_age_days']}`")
    lines.append("")
    lines.append("## Cases")
    lines.append("")
    lines.append("| id | ok | status | latency_ms | rank | source_count | no_code_ok | error |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---|")
    for r in results:
        no_code_ok = r.get("no_code_ok")
        if no_code_ok is True:
            no_code_cell = "Y"
        elif no_code_ok is False:
            no_code_cell = "N"
        else:
            no_code_cell = "-"
        lines.append(
            f"| {r.get('id') or '-'} | "
            f"{'Y' if r['ok'] else 'N'} | "
            f"{r['status']} | "
            f"{r['latency_ms']:.1f} | "
            f"{r['rank'] if r['rank'] is not None else '-'} | "
            f"{r['source_count']} | "
            f"{no_code_cell} | "
            f"{(r['error'] or '').replace('|', '/')} |"
        )
    Path(path).write_text("\n".join(lines), encoding="utf-8")


def evaluate_thresholds(summary: Dict[str, Any], args: argparse.Namespace) -> List[str]:
    failures: List[str] = []

    if args.min_success_rate is not None and summary["success_rate"] < args.min_success_rate:
        failures.append(
            f"success_rate {summary['success_rate']:.3f} < min_success_rate {args.min_success_rate:.3f}"
        )

    if args.min_hit_at_1 is not None and summary["hit_at_1"] < args.min_hit_at_1:
        failures.append(f"hit_at_1 {summary['hit_at_1']:.3f} < min_hit_at_1 {args.min_hit_at_1:.3f}")

    if args.min_precision_at_3 is not None and summary["precision_at_3"] < args.min_precision_at_3:
        failures.append(
            f"precision_at_3 {summary['precision_at_3']:.3f} < "
            f"min_precision_at_3 {args.min_precision_at_3:.3f}"
        )

    if args.max_latency_p95_ms is not None and summary["latency_ms_p95"] > args.max_latency_p95_ms:
        failures.append(
            f"latency_ms_p95 {summary['latency_ms_p95']:.1f} > max_latency_p95_ms {args.max_latency_p95_ms:.1f}"
        )

    if args.max_hallucination_rate is not None and summary["hallucination_rate"] > args.max_hallucination_rate:
        failures.append(
            f"hallucination_rate {summary['hallucination_rate']:.3f} > "
            f"max_hallucination_rate {args.max_hallucination_rate:.3f}"
        )

    if args.min_no_code_pass_rate is not None:
        if summary["no_code_case_count"] == 0:
            failures.append("min_no_code_pass_rate set but no expect_no_codes case exists")
        elif summary["no_code_pass_rate"] < args.min_no_code_pass_rate:
            failures.append(
                f"no_code_pass_rate {summary['no_code_pass_rate']:.3f} < "
                f"min_no_code_pass_rate {args.min_no_code_pass_rate:.3f}"
            )

    if (
        args.min_required_term_pass_rate is not None
        and summary["required_term_pass_rate"] < args.min_required_term_pass_rate
    ):
        failures.append(
            f"required_term_pass_rate {summary['required_term_pass_rate']:.3f} < "
            f"min_required_term_pass_rate {args.min_required_term_pass_rate:.3f}"
        )

    if (
        args.min_forbidden_term_pass_rate is not None
        and summary["forbidden_term_pass_rate"] < args.min_forbidden_term_pass_rate
    ):
        failures.append(
            f"forbidden_term_pass_rate {summary['forbidden_term_pass_rate']:.3f} < "
            f"min_forbidden_term_pass_rate {args.min_forbidden_term_pass_rate:.3f}"
        )

    if args.max_case_age_days is not None:
        if summary["freshness_metadata_case_count"] == 0:
            failures.append("max_case_age_days set but no case has last_verified_at/updated_at metadata")
        elif summary["max_case_age_days"] > args.max_case_age_days:
            failures.append(
                f"max_case_age_days {summary['max_case_age_days']} > "
                f"max_case_age_days {args.max_case_age_days}"
            )

    return failures


async def main() -> int:
    parser = argparse.ArgumentParser(description="Partalog Chat Eval")
    parser.add_argument("--base-url", default=os.getenv("PARTALOG_BASE_URL", "http://localhost:5159"))
    parser.add_argument("--endpoint", default="/api/chat/ask")
    parser.add_argument("--cases", default="eval/queries.sample.jsonl")
    parser.add_argument("--timeout-seconds", type=float, default=60.0)
    parser.add_argument("--output-json", default="")
    parser.add_argument("--output-md", default="")
    parser.add_argument("--fail-on-error", action="store_true")
    parser.add_argument("--min-success-rate", type=float, default=None)
    parser.add_argument("--min-hit-at-1", type=float, default=None)
    parser.add_argument("--min-precision-at-3", type=float, default=None)
    parser.add_argument("--max-latency-p95-ms", type=float, default=None)
    parser.add_argument("--max-hallucination-rate", type=float, default=None)
    parser.add_argument("--min-no-code-pass-rate", type=float, default=None)
    parser.add_argument("--min-required-term-pass-rate", type=float, default=None)
    parser.add_argument("--min-forbidden-term-pass-rate", type=float, default=None)
    parser.add_argument("--max-case-age-days", type=int, default=None)
    args = parser.parse_args()

    cases = resolve_case_placeholders(load_cases(args.cases))
    if not cases:
        print("No cases found.")
        return 1

    timeout = aiohttp.ClientTimeout(total=args.timeout_seconds)
    connector = aiohttp.TCPConnector(limit=10)

    results: List[Dict[str, Any]] = []
    async with aiohttp.ClientSession(timeout=timeout, connector=connector) as session:
        for idx, case in enumerate(cases, start=1):
            case_id = case.get("id", f"case-{idx}")
            result = await run_case(session, args.base_url, args.endpoint, case)
            results.append(result)
            print(
                f"[{case_id}] ok={result['ok']} status={result['status']} "
                f"lat={result['latency_ms']:.1f}ms rank={result['rank']} "
                f"codes={result['codes'][:5]}"
            )
            if result["error"]:
                print(f"  error: {result['error']}")
            if result.get("logical_error"):
                print("  logical_error: generic fallback/error reply detected")
            if result["hallucinated_codes"]:
                print(f"  hallucinated_codes: {result['hallucinated_codes']}")

    summary = summarize(results)
    print_summary(summary)

    if args.output_json:
        payload = {"summary": summary, "results": results}
        Path(args.output_json).write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"\nJSON report written: {args.output_json}")

    if args.output_md:
        write_markdown(args.output_md, summary, results)
        print(f"Markdown report written: {args.output_md}")

    if args.fail_on_error and summary["errors"] > 0:
        return 2

    threshold_failures = evaluate_thresholds(summary, args)
    if threshold_failures:
        print("\n--- Threshold check failed ---")
        for fail in threshold_failures:
            print(f"- {fail}")
        return 3

    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
