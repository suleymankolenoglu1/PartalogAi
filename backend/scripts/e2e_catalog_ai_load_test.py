#!/usr/bin/env python3
"""
End-to-end privileged catalog upload + AI analysis load test.

Flow per workflow:
1. Upload a PDF via /api/files/upload
2. Create a catalog with the uploaded PDF URL
3. Verify the catalog is readable and has generated pages
4. Trigger /api/catalogs/{id}/start-ai-process
5. Poll /api/catalogs/ai-jobs until the catalog job completes or fails
6. Optionally delete the created catalog

This script is intended for local/staging owner flows and requires a privileged
bearer token. Public browse/chat is covered by e2e_public_load_test.py.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import statistics
import time
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import httpx


ROOT_DIR = Path(__file__).resolve().parents[2]


@dataclass
class WorkflowResult:
    ok: bool
    status_code: int
    total_ms: float
    catalog_id: str = ""
    ai_status: str = ""
    page_count: int = 0
    error: str | None = None
    phases_ms: dict[str, float] = field(default_factory=dict)


def trim_slash(value: str) -> str:
    return value[:-1] if value.endswith("/") else value


def percentile(values: list[float], p: float) -> float:
    if not values:
        return 0.0
    if len(values) == 1:
        return values[0]
    sorted_vals = sorted(values)
    rank = (len(sorted_vals) - 1) * p
    low = int(rank)
    high = min(low + 1, len(sorted_vals) - 1)
    frac = rank - low
    return sorted_vals[low] * (1 - frac) + sorted_vals[high] * frac


def parse_json_response(response: httpx.Response) -> Any:
    if not response.content:
        return {}
    content_type = response.headers.get("content-type", "")
    if "application/json" in content_type.lower():
        return response.json()
    text = response.text
    try:
        return json.loads(text)
    except json.JSONDecodeError:
        return {"raw": text}


def resolve_pdf_path(path_arg: str) -> Path:
    if path_arg:
        candidate = Path(path_arg).expanduser().resolve()
        if not candidate.exists():
            raise FileNotFoundError(f"PDF bulunamadı: {candidate}")
        return candidate

    candidates = [
        *sorted((ROOT_DIR / "publish/wwwroot/uploads").glob("*1642-00*.pdf")),
        *sorted((ROOT_DIR / "publish/wwwroot/uploads").glob("*typical*.pdf")),
        *sorted((ROOT_DIR / "publish/wwwroot/uploads").glob("*deneme-katalog*.pdf")),
        ROOT_DIR / "output/pdf/katalogcu-ozet.pdf",
        *sorted((ROOT_DIR / "publish/wwwroot/uploads").glob("*.pdf")),
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate

    raise FileNotFoundError(
        "Varsayılan test PDF bulunamadı. --pdf-path ile yerel bir PDF verin."
    )


async def upload_pdf(
    client: httpx.AsyncClient,
    base_url: str,
    pdf_name: str,
    pdf_bytes: bytes,
    headers: dict[str, str],
) -> tuple[str, float]:
    started = time.perf_counter()
    response = await client.post(
        f"{base_url}/api/files/upload",
        files={"file": (pdf_name, pdf_bytes, "application/pdf")},
        headers=headers,
    )
    body = parse_json_response(response)
    if response.status_code != 200:
        raise RuntimeError(f"upload status={response.status_code} body={body}")
    url = body.get("url") if isinstance(body, dict) else None
    if not url:
        raise RuntimeError(f"upload url missing body={body}")
    return str(url), (time.perf_counter() - started) * 1000.0


async def create_catalog(
    client: httpx.AsyncClient,
    base_url: str,
    headers: dict[str, str],
    *,
    sequence: int,
    pdf_url: str,
) -> tuple[str, float]:
    started = time.perf_counter()
    suffix = f"{int(time.time())}-{sequence:05d}"
    payload = {
        "name": f"AI Load Catalog {suffix}",
        "description": "Synthetic catalog for upload + AI load test",
        "imageUrl": None,
        "pdfUrl": pdf_url,
        "folderId": None,
    }
    response = await client.post(f"{base_url}/api/catalogs", json=payload, headers=headers)
    body = parse_json_response(response)
    if response.status_code not in (200, 201):
        raise RuntimeError(f"catalog create status={response.status_code} body={body}")
    catalog_id = body.get("id") if isinstance(body, dict) else None
    if not catalog_id:
        raise RuntimeError(f"catalog id missing body={body}")
    return str(catalog_id), (time.perf_counter() - started) * 1000.0


async def get_catalog_details(
    client: httpx.AsyncClient,
    base_url: str,
    headers: dict[str, str],
    catalog_id: str,
) -> tuple[dict[str, Any], float]:
    started = time.perf_counter()
    response = await client.get(f"{base_url}/api/catalogs/{catalog_id}", headers=headers)
    body = parse_json_response(response)
    if response.status_code != 200 or not isinstance(body, dict):
        raise RuntimeError(f"catalog get status={response.status_code} body={body}")
    return body, (time.perf_counter() - started) * 1000.0


async def start_ai_process(
    client: httpx.AsyncClient,
    base_url: str,
    headers: dict[str, str],
    catalog_id: str,
) -> float:
    started = time.perf_counter()
    response = await client.post(f"{base_url}/api/catalogs/{catalog_id}/start-ai-process", headers=headers)
    body = parse_json_response(response)
    if response.status_code not in (200, 202):
        raise RuntimeError(f"start-ai status={response.status_code} body={body}")
    return (time.perf_counter() - started) * 1000.0


async def wait_for_ai_completion(
    client: httpx.AsyncClient,
    base_url: str,
    headers: dict[str, str],
    catalog_id: str,
    *,
    poll_interval_seconds: float,
    timeout_seconds: int,
) -> tuple[str, float]:
    started = time.perf_counter()
    deadline = started + timeout_seconds
    last_status = ""

    while time.perf_counter() < deadline:
        response = await client.get(f"{base_url}/api/catalogs/ai-jobs", params={"take": 100}, headers=headers)
        body = parse_json_response(response)
        if response.status_code != 200 or not isinstance(body, dict):
            raise RuntimeError(f"ai-jobs status={response.status_code} body={body}")

        jobs = body.get("jobs") if isinstance(body, dict) else None
        if isinstance(jobs, list):
            current = next((job for job in jobs if str(job.get("catalogId")) == catalog_id), None)
            if current is not None:
                last_status = str(current.get("status") or "")
                if last_status == "Completed":
                    return last_status, (time.perf_counter() - started) * 1000.0
                if last_status == "Failed":
                    last_error = current.get("lastError")
                    raise RuntimeError(f"ai job failed status=Failed error={last_error}")

        await asyncio.sleep(poll_interval_seconds)

    raise RuntimeError(f"ai job timeout last_status={last_status or 'missing'}")


async def delete_catalog(
    client: httpx.AsyncClient,
    base_url: str,
    headers: dict[str, str],
    catalog_id: str,
) -> float:
    started = time.perf_counter()
    response = await client.delete(f"{base_url}/api/catalogs/{catalog_id}", headers=headers)
    body = parse_json_response(response)
    if response.status_code not in (200, 204):
        raise RuntimeError(f"catalog delete status={response.status_code} body={body}")
    return (time.perf_counter() - started) * 1000.0


async def run_workflow(
    *,
    client: httpx.AsyncClient,
    base_url: str,
    headers: dict[str, str],
    pdf_name: str,
    pdf_bytes: bytes,
    sequence: int,
    poll_interval_seconds: float,
    ai_timeout_seconds: int,
    keep_catalogs: bool,
    cleanup_failed_catalogs: bool,
) -> WorkflowResult:
    workflow_started = time.perf_counter()
    phases: dict[str, float] = {}
    catalog_id = ""
    page_count = 0
    ai_status = ""

    try:
        pdf_url, phases["upload_pdf"] = await upload_pdf(client, base_url, pdf_name, pdf_bytes, headers)
        catalog_id, phases["create_catalog"] = await create_catalog(
            client,
            base_url,
            headers,
            sequence=sequence,
            pdf_url=pdf_url,
        )

        catalog_body, phases["verify_upload"] = await get_catalog_details(client, base_url, headers, catalog_id)
        pages = catalog_body.get("pages")
        if not isinstance(pages, list) or not pages:
            raise RuntimeError("catalog pages missing after upload")
        page_count = len(pages)

        phases["start_ai"] = await start_ai_process(client, base_url, headers, catalog_id)
        ai_status, phases["wait_ai"] = await wait_for_ai_completion(
            client,
            base_url,
            headers,
            catalog_id,
            poll_interval_seconds=poll_interval_seconds,
            timeout_seconds=ai_timeout_seconds,
        )

        final_catalog, phases["verify_catalog"] = await get_catalog_details(client, base_url, headers, catalog_id)
        final_pages = final_catalog.get("pages")
        if isinstance(final_pages, list):
            page_count = len(final_pages)

        if not keep_catalogs:
            phases["cleanup"] = await delete_catalog(client, base_url, headers, catalog_id)

        return WorkflowResult(
            ok=True,
            status_code=200,
            total_ms=(time.perf_counter() - workflow_started) * 1000.0,
            catalog_id=catalog_id,
            ai_status=ai_status,
            page_count=page_count,
            phases_ms=phases,
        )
    except Exception as exc:
        if catalog_id and not keep_catalogs and cleanup_failed_catalogs:
            try:
                phases["cleanup"] = await delete_catalog(client, base_url, headers, catalog_id)
            except Exception:
                pass

        return WorkflowResult(
            ok=False,
            status_code=0,
            total_ms=(time.perf_counter() - workflow_started) * 1000.0,
            catalog_id=catalog_id,
            ai_status=ai_status,
            page_count=page_count,
            error=str(exc),
            phases_ms=phases,
        )


def build_summary(results: list[WorkflowResult]) -> dict[str, Any]:
    total = len(results)
    success_count = sum(1 for result in results if result.ok)
    latencies = [result.total_ms for result in results]
    status_counts = Counter(result.status_code for result in results)
    error_counts = Counter(result.error for result in results if result.error)

    phase_values: dict[str, list[float]] = defaultdict(list)
    for result in results:
        for phase_name, phase_ms in result.phases_ms.items():
            phase_values[phase_name].append(phase_ms)

    phases = {
        phase_name: {
            "count": len(values),
            "latency_avg_ms": statistics.fmean(values) if values else 0.0,
            "latency_p95_ms": percentile(values, 0.95),
        }
        for phase_name, values in sorted(phase_values.items())
    }

    return {
        "overall": {
            "total": total,
            "success_rate": success_count / total if total else 0.0,
            "error_rate": (total - success_count) / total if total else 0.0,
            "latency_avg_ms": statistics.fmean(latencies) if latencies else 0.0,
            "latency_p95_ms": percentile(latencies, 0.95),
            "status_counts": {str(key): value for key, value in sorted(status_counts.items())},
            "top_errors": error_counts.most_common(10),
        },
        "phases": phases,
    }


async def execute_load(args: argparse.Namespace) -> dict[str, Any]:
    base_url = trim_slash(args.base_url)
    pdf_path = resolve_pdf_path(args.pdf_path)
    pdf_bytes = pdf_path.read_bytes()
    headers = {
        "Authorization": f"Bearer {args.admin_token}",
        "Accept": "application/json",
    }

    timeout = httpx.Timeout(args.request_timeout_seconds, connect=10.0)
    limits = httpx.Limits(max_connections=max(args.concurrency * 2, 10), max_keepalive_connections=max(args.concurrency, 5))

    queue: asyncio.Queue[int] = asyncio.Queue()
    for sequence in range(1, args.iterations + 1):
        queue.put_nowait(sequence)

    results: list[WorkflowResult] = []
    results_lock = asyncio.Lock()

    async with httpx.AsyncClient(timeout=timeout, limits=limits) as client:
        async def worker() -> None:
            while True:
                try:
                    sequence = queue.get_nowait()
                except asyncio.QueueEmpty:
                    return

                result = await run_workflow(
                    client=client,
                    base_url=base_url,
                    headers=headers,
                    pdf_name=pdf_path.name,
                    pdf_bytes=pdf_bytes,
                    sequence=sequence,
                    poll_interval_seconds=args.poll_interval_seconds,
                    ai_timeout_seconds=args.ai_timeout_seconds,
                    keep_catalogs=args.keep_catalogs,
                    cleanup_failed_catalogs=args.cleanup_failed_catalogs,
                )
                async with results_lock:
                    results.append(result)
                queue.task_done()

        workers = [asyncio.create_task(worker()) for _ in range(args.concurrency)]
        await asyncio.gather(*workers)

    summary = build_summary(results)
    report = {
        "config": {
            "base_url": base_url,
            "iterations": args.iterations,
            "concurrency": args.concurrency,
            "pdf_path": str(pdf_path),
            "pdf_size_bytes": len(pdf_bytes),
            "ai_timeout_seconds": args.ai_timeout_seconds,
            "poll_interval_seconds": args.poll_interval_seconds,
            "keep_catalogs": args.keep_catalogs,
        },
        **summary,
        "workflows": [
            {
                "ok": result.ok,
                "status_code": result.status_code,
                "catalog_id": result.catalog_id,
                "ai_status": result.ai_status,
                "page_count": result.page_count,
                "total_ms": result.total_ms,
                "error": result.error,
                "phases_ms": result.phases_ms,
            }
            for result in results
        ],
    }
    return report


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Catalog upload + AI analysis end-to-end load test")
    parser.add_argument("--base-url", default="http://localhost:5159", help="API base URL")
    parser.add_argument("--admin-token", default="", help="Privileged owner/admin bearer token")
    parser.add_argument("--iterations", type=int, default=4, help="Total workflow count")
    parser.add_argument("--concurrency", type=int, default=2, help="Concurrent workflow count")
    parser.add_argument("--pdf-path", default="", help="Optional local PDF path")
    parser.add_argument("--ai-timeout-seconds", type=int, default=600, help="Max wait time per AI job")
    parser.add_argument("--poll-interval-seconds", type=float, default=2.0, help="AI job poll interval")
    parser.add_argument("--request-timeout-seconds", type=float, default=60.0, help="Per-request timeout")
    parser.add_argument("--success-threshold", type=float, default=0.9, help="Minimum success rate")
    parser.add_argument("--keep-catalogs", action="store_true", help="Do not delete created catalogs")
    parser.add_argument("--cleanup-failed-catalogs", action="store_true", help="Delete failed catalogs too")
    parser.add_argument("--output-json", default="", help="Optional JSON output path")
    args = parser.parse_args()

    if not args.admin_token:
        parser.error("--admin-token zorunlu. Bu test privileged katalog akışı çalıştırır.")
    if args.iterations <= 0:
        parser.error("--iterations pozitif olmalı.")
    if args.concurrency <= 0:
        parser.error("--concurrency pozitif olmalı.")
    if args.poll_interval_seconds <= 0:
        parser.error("--poll-interval-seconds pozitif olmalı.")
    if args.ai_timeout_seconds <= 0:
        parser.error("--ai-timeout-seconds pozitif olmalı.")

    return args


async def main() -> int:
    args = parse_args()
    report = await execute_load(args)

    output = json.dumps(report, ensure_ascii=False, indent=2)
    print(
        "Running catalog upload + AI load test: "
        f"iterations={report['config']['iterations']} "
        f"concurrency={report['config']['concurrency']} "
        f"pdf={report['config']['pdf_path']}"
    )
    print(output)

    if args.output_json:
        Path(args.output_json).write_text(output + "\n", encoding="utf-8")

    failures: list[str] = []
    success_rate = float(report["overall"]["success_rate"])
    if success_rate < args.success_threshold:
        failures.append(f"success_rate {success_rate:.3f} < {args.success_threshold:.3f}")

    if failures:
        print("Threshold failures:")
        for failure in failures:
            print(f"- {failure}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
