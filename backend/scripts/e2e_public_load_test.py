#!/usr/bin/env python3
"""
End-to-end public flow load test.

Scenarios:
1. Public catalog browse
2. Public non-stream chat ask
3. Public SSE chat ask-stream
4. Public checkout/order create

The script can bootstrap a temporary public catalog fixture when no public token
is provided, reusing the existing smoke bootstrap helpers.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import random
import statistics
import time
import uuid
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import urlencode

import httpx


SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in __import__("sys").path:
    __import__("sys").path.insert(0, str(SCRIPT_DIR))

from smoke_public_checkout import _bootstrap_public_fixture, _request_json  # noqa: E402


DEFAULT_CHAT_QUERIES = [
    "Yamato VG2500-8F için yağ deposu contası var mı?",
    "160000 parça kodu hangi makinelerde kullanılıyor?",
    "Bu makine için uygun conta kodunu söyler misin?",
]

STREAM_FAILURE_REASONS = {
    "ai_capacity_limited",
    "ai_timeout",
    "ai_upstream_error",
    "ai_exception",
    "upstream_connection_failure",
    "upstream_non_success",
    "upstream_timeout",
    "upstream_unexpected_error",
}


@dataclass
class Fixture:
    public_token: str
    admin_token: str
    catalog_id: str
    product_id: str
    product_code: str
    product_price: float


@dataclass
class ScenarioOutcome:
    ok: bool
    status_code: int
    latency_ms: float
    error: str | None = None
    event_count: int = 0
    fallback_reasons: tuple[str, ...] = ()
    first_token_latency_ms: float | None = None


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


def trim_slash(value: str) -> str:
    return value[:-1] if value.endswith("/") else value


def resolve_fixture(args: argparse.Namespace) -> Fixture:
    base = trim_slash(args.base_url)
    api = f"{base}/api"

    public_token = args.public_token
    admin_token = args.admin_token
    catalog_id = args.catalog_id
    product_id = args.product_id

    if not public_token:
        bootstrap_email = args.bootstrap_admin_email or f"load.admin.{int(time.time())}@example.com"
        bootstrap = _bootstrap_public_fixture(
            api,
            timeout=int(args.timeout_seconds),
            admin_email=bootstrap_email,
            admin_password=args.bootstrap_admin_password,
            admin_name=args.bootstrap_admin_name,
            company_name=args.bootstrap_company_name,
        )
        public_token = bootstrap.public_token
        admin_token = admin_token or bootstrap.admin_token
        catalog_id = catalog_id or bootstrap.catalog_id
        product_id = product_id or bootstrap.product_id

    catalogs_resp = _request_json(
        "GET",
        f"{api}/catalogs/public-by-token?{urlencode({'token': public_token})}",
        timeout=int(args.timeout_seconds),
    )
    if catalogs_resp.status != 200:
        raise RuntimeError(f"public catalog resolve failed: {catalogs_resp.status} {catalogs_resp.body}")
    catalogs = catalogs_resp.body if isinstance(catalogs_resp.body, list) else []
    if not catalogs:
        raise RuntimeError("no public catalogs found for load test fixture")

    if not catalog_id:
        catalog_id = str(catalogs[0].get("id") or "")
    if not catalog_id:
        raise RuntimeError("catalog id could not be resolved")

    products_resp = _request_json(
        "GET",
        f"{api}/products/catalog/{catalog_id}?{urlencode({'token': public_token})}",
        timeout=int(args.timeout_seconds),
    )
    if products_resp.status != 200:
        products = []
    else:
        products = products_resp.body if isinstance(products_resp.body, list) else []

    selected = None
    if product_id and products:
        selected = next((p for p in products if str(p.get("id")) == str(product_id)), None)
    if selected is None and products:
        selected = products[0]
    if selected is None:
        product_id = ""
        product_code = ""
        product_price = 1.0
    else:
        product_id = str(selected.get("id") or "")
        product_code = str(selected.get("code") or selected.get("partCode") or "LOAD-SMOKE")
        raw_price = selected.get("price")
        product_price = float(raw_price) if isinstance(raw_price, (int, float)) else 1.0

    return Fixture(
        public_token=public_token,
        admin_token=admin_token,
        catalog_id=catalog_id,
        product_id=product_id,
        product_code=product_code,
        product_price=product_price,
    )


async def request_json(
    client: httpx.AsyncClient,
    method: str,
    url: str,
    *,
    json_body: dict[str, Any] | None = None,
    headers: dict[str, str] | None = None,
) -> httpx.Response:
    return await client.request(method.upper(), url, json=json_body, headers=headers)


def build_chat_form(fixture: Fixture, query: str) -> list[tuple[str, tuple[None, str]]]:
    return [
        ("text", (None, query)),
        ("publicToken", (None, fixture.public_token)),
        ("catalog_ids", (None, json.dumps([fixture.catalog_id]))),
        ("history", (None, "[]")),
    ]


def extract_fallback_reasons(event: dict[str, Any]) -> list[str]:
    reasons: list[str] = []
    fallback = event.get("fallback")
    if isinstance(fallback, dict):
        reason = fallback.get("reason")
        if fallback.get("used") and reason:
            reasons.append(str(reason))

    reason = event.get("reason")
    if reason and str(reason) in STREAM_FAILURE_REASONS:
        reasons.append(str(reason))

    return reasons


async def run_catalog_browse(
    client: httpx.AsyncClient,
    base_url: str,
    fixture: Fixture,
    headers: dict[str, str],
) -> ScenarioOutcome:
    started = time.perf_counter()
    try:
        catalogs = await client.get(
            f"{base_url}/api/catalogs/public-by-token",
            params={"token": fixture.public_token},
            headers=headers,
        )
        if catalogs.status_code != 200:
            raise RuntimeError(f"catalogs status={catalogs.status_code}")
        catalog_list = catalogs.json()
        if not isinstance(catalog_list, list) or not catalog_list:
            raise RuntimeError("catalog list empty")

        products = await client.get(
            f"{base_url}/api/products/catalog/{fixture.catalog_id}",
            params={"token": fixture.public_token},
            headers=headers,
        )
        if products.status_code == 403:
            storefront = await client.get(
                f"{base_url}/api/catalogs/public-storefront",
                params={"token": fixture.public_token},
                headers=headers,
            )
            if storefront.status_code != 200:
                raise RuntimeError(f"storefront status={storefront.status_code}")
        else:
            if products.status_code != 200:
                raise RuntimeError(f"products status={products.status_code}")
            product_list = products.json()
            if not isinstance(product_list, list):
                raise RuntimeError("product list invalid")

        return ScenarioOutcome(True, 200, (time.perf_counter() - started) * 1000.0)
    except Exception as exc:
        return ScenarioOutcome(False, 0, (time.perf_counter() - started) * 1000.0, str(exc))


async def run_chat_ask(
    client: httpx.AsyncClient,
    base_url: str,
    fixture: Fixture,
    query: str,
    headers: dict[str, str],
) -> ScenarioOutcome:
    started = time.perf_counter()
    try:
        response = await client.post(f"{base_url}/api/chat/ask", files=build_chat_form(fixture, query), headers=headers)
        if response.status_code != 200:
            raise RuntimeError(f"chat ask status={response.status_code}")
        body = response.json()
        reply = body.get("replySuggestion") if isinstance(body, dict) else None
        if not reply:
            raise RuntimeError("replySuggestion missing")
        return ScenarioOutcome(True, response.status_code, (time.perf_counter() - started) * 1000.0)
    except Exception as exc:
        return ScenarioOutcome(False, 0, (time.perf_counter() - started) * 1000.0, str(exc))


async def run_chat_stream(
    client: httpx.AsyncClient,
    base_url: str,
    fixture: Fixture,
    query: str,
    headers: dict[str, str],
) -> ScenarioOutcome:
    started = time.perf_counter()
    event_count = 0
    fallback_reasons: list[str] = []
    first_token_latency_ms: float | None = None
    try:
        got_event = False
        got_done = False
        stream_error: str | None = None
        async with client.stream(
            "POST",
            f"{base_url}/api/chat/ask-stream",
            files=build_chat_form(fixture, query),
            headers=headers,
        ) as response:
            if response.status_code != 200:
                raise RuntimeError(f"chat stream status={response.status_code}")
            async for line in response.aiter_lines():
                if not line.startswith("data:"):
                    continue
                payload = line[5:].strip()
                if not payload:
                    continue
                got_event = True
                event_count += 1
                try:
                    data = json.loads(payload)
                except json.JSONDecodeError:
                    continue
                reason = str(data.get("reason") or "")
                fallback_reasons.extend(extract_fallback_reasons(data))
                if (
                    first_token_latency_ms is None
                    and data.get("type") == "token"
                    and str(data.get("token") or "")
                ):
                    first_token_latency_ms = (time.perf_counter() - started) * 1000.0
                if reason in STREAM_FAILURE_REASONS:
                    stream_error = reason
                if data.get("type") == "done" or "completion" in data:
                    got_done = True
            if not got_event:
                raise RuntimeError("no SSE data received")
            if stream_error:
                raise RuntimeError(f"chat stream reason={stream_error}")
            if first_token_latency_ms is None:
                raise RuntimeError("stream completed without token event")
            if not got_done:
                raise RuntimeError("stream completed without done event")
        return ScenarioOutcome(
            True,
            200,
            (time.perf_counter() - started) * 1000.0,
            event_count=event_count,
            fallback_reasons=tuple(fallback_reasons),
            first_token_latency_ms=first_token_latency_ms,
        )
    except Exception as exc:
        return ScenarioOutcome(
            False,
            0,
            (time.perf_counter() - started) * 1000.0,
            str(exc),
            event_count=event_count,
            fallback_reasons=tuple(fallback_reasons),
            first_token_latency_ms=first_token_latency_ms,
        )


async def run_checkout(
    client: httpx.AsyncClient,
    base_url: str,
    fixture: Fixture,
    sequence: int,
    headers: dict[str, str],
) -> ScenarioOutcome:
    started = time.perf_counter()
    try:
        if not fixture.product_id:
            raise RuntimeError("checkout scenario disabled: no public product available")
        suffix = f"{int(time.time())}{sequence:06d}"
        phone = f"90555{suffix[-7:]}"
        email = f"load+{suffix}@example.com"
        password = "LoadP@ssw0rd!"

        register_payload = {
            "publicToken": fixture.public_token,
            "name": "Load Customer",
            "phone": phone,
            "email": email,
            "password": password,
        }
        register = await request_json(
            client,
            "POST",
            f"{base_url}/api/customers/public-auth/register",
            json_body=register_payload,
            headers=headers,
        )
        if register.status_code not in (200, 409):
            raise RuntimeError(f"register status={register.status_code}")

        body = register.json() if register.content else {}
        session_token = body.get("sessionToken", "") if isinstance(body, dict) else ""
        if not session_token:
            login = await request_json(
                client,
                "POST",
                f"{base_url}/api/customers/public-auth/login",
                json_body={
                    "publicToken": fixture.public_token,
                    "phone": phone,
                    "email": email,
                    "password": password,
                },
                headers=headers,
            )
            if login.status_code != 200:
                raise RuntimeError(f"login status={login.status_code}")
            login_body = login.json()
            session_token = login_body.get("sessionToken", "") if isinstance(login_body, dict) else ""
        if not session_token:
            raise RuntimeError("session token missing")

        idempotency_key = str(uuid.uuid4())
        order = await request_json(
            client,
            "POST",
            f"{base_url}/api/orders",
            headers={"Idempotency-Key": idempotency_key, **headers},
            json_body={
                "customerName": "Load Customer",
                "customerEmail": email,
                "customerPhone": phone,
                "deliveryAddress": "Load Test Mah. No:1",
                "deliveryCity": "Istanbul",
                "deliveryDistrict": "Kadikoy",
                "deliveryNote": "load-test",
                "paymentMethod": "KapidaOdeme",
                "publicToken": fixture.public_token,
                "publicSessionToken": session_token,
                "idempotencyKey": idempotency_key,
                "items": [
                    {
                        "productId": fixture.product_id,
                        "partCode": fixture.product_code,
                        "quantity": 1,
                        "price": fixture.product_price,
                    }
                ],
            },
        )
        if order.status_code != 200:
            raise RuntimeError(f"order status={order.status_code}")
        order_body = order.json()
        if not isinstance(order_body, dict) or not order_body.get("orderId"):
            raise RuntimeError("orderId missing")
        return ScenarioOutcome(True, 200, (time.perf_counter() - started) * 1000.0)
    except Exception as exc:
        return ScenarioOutcome(False, 0, (time.perf_counter() - started) * 1000.0, str(exc))


def summarize_scenario(
    outcomes: list[ScenarioOutcome],
    elapsed_seconds: float = 0.0,
) -> dict[str, Any]:
    latencies = [item.latency_ms for item in outcomes]
    total = len(outcomes)
    ok = sum(1 for item in outcomes if item.ok)
    statuses = Counter(item.status_code for item in outcomes)
    errors = Counter(item.error or "" for item in outcomes if item.error)
    fallback_reasons = Counter(reason for item in outcomes for reason in set(item.fallback_reasons))
    fallback_case_count = sum(1 for item in outcomes if item.fallback_reasons)
    degraded_fallback_case_count = sum(
        1
        for item in outcomes
        if any(reason in STREAM_FAILURE_REASONS for reason in item.fallback_reasons)
    )
    event_counts = [item.event_count for item in outcomes]
    first_token_latencies = [
        item.first_token_latency_ms
        for item in outcomes
        if item.first_token_latency_ms is not None
    ]
    return {
        "total": total,
        "ok_count": ok,
        "failed_count": total - ok,
        "success_rate": ok / total if total else 0.0,
        "error_rate": (total - ok) / total if total else 0.0,
        "throughput_rps": total / elapsed_seconds if elapsed_seconds > 0 else 0.0,
        "successful_throughput_rps": ok / elapsed_seconds if elapsed_seconds > 0 else 0.0,
        "latency_avg_ms": statistics.mean(latencies) if latencies else 0.0,
        "latency_p95_ms": percentile(latencies, 0.95) if latencies else 0.0,
        "event_count_avg": statistics.mean(event_counts) if event_counts else 0.0,
        "first_token_sample_count": len(first_token_latencies),
        "first_token_latency_avg_ms": (
            statistics.mean(first_token_latencies) if first_token_latencies else 0.0
        ),
        "first_token_latency_p95_ms": (
            percentile(first_token_latencies, 0.95) if first_token_latencies else 0.0
        ),
        "status_counts": dict(statuses),
        "fallback_case_count": fallback_case_count,
        "fallback_rate": fallback_case_count / total if total else 0.0,
        "degraded_fallback_case_count": degraded_fallback_case_count,
        "degraded_fallback_rate": degraded_fallback_case_count / total if total else 0.0,
        "fallback_reason_counts": dict(fallback_reasons),
        "top_errors": errors.most_common(5),
    }


async def worker(
    worker_id: int,
    client: httpx.AsyncClient,
    base_url: str,
    fixture: Fixture,
    queries: list[str],
    end_time: float,
    sequence: list[int],
    results: dict[str, list[ScenarioOutcome]],
    weights: list[tuple[str, int]],
) -> None:
    effective_weights = []
    for name, weight in weights:
        if name == "checkout" and not fixture.product_id:
            continue
        effective_weights.append((name, weight))
    population = [name for name, weight in effective_weights for _ in range(max(weight, 0))]
    if not population:
        raise RuntimeError("no enabled scenarios to run")
    client_ip = f"198.51.100.{10 + (worker_id % 200)}"
    request_headers = {"X-Forwarded-For": client_ip}
    while time.monotonic() < end_time:
        scenario = random.choice(population)
        query = random.choice(queries)
        if scenario == "browse":
            outcome = await run_catalog_browse(client, base_url, fixture, request_headers)
        elif scenario == "chat":
            outcome = await run_chat_ask(client, base_url, fixture, query, request_headers)
        elif scenario == "stream":
            outcome = await run_chat_stream(client, base_url, fixture, query, request_headers)
        else:
            sequence[0] += 1
            outcome = await run_checkout(client, base_url, fixture, sequence[0] + worker_id * 100000, request_headers)
        results[scenario].append(outcome)


def scenario_latency_limits(args: argparse.Namespace) -> dict[str, float]:
    limits: dict[str, float] = {}
    for name in ("browse", "chat", "stream", "checkout"):
        override = getattr(args, f"max_{name}_latency_p95_ms", None)
        limits[name] = override if override is not None else args.max_latency_p95_ms
    return limits


def check_thresholds(args: argparse.Namespace, scenario_summaries: dict[str, dict[str, Any]]) -> list[str]:
    failures: list[str] = []
    min_samples_per_scenario = getattr(args, "min_samples_per_scenario", 1)
    latency_limits = scenario_latency_limits(args)
    configured_weights = {
        "browse": args.browse_weight,
        "chat": args.chat_weight,
        "stream": args.stream_weight,
        "checkout": args.checkout_weight,
    }
    for name, summary in scenario_summaries.items():
        if configured_weights.get(name, 0) <= 0:
            continue
        if summary["total"] == 0:
            failures.append(f"{name} did not run")
            continue
        if summary["total"] < min_samples_per_scenario:
            failures.append(
                f"{name} sample_count {summary['total']} < {min_samples_per_scenario}"
            )
            continue
        if summary["success_rate"] < args.min_success_rate:
            failures.append(
                f"{name} success_rate {summary['success_rate']:.3f} < {args.min_success_rate:.3f}"
            )
        latency_limit = latency_limits[name]
        if summary["latency_p95_ms"] > latency_limit:
            failures.append(
                f"{name} latency_p95_ms {summary['latency_p95_ms']:.1f} > {latency_limit:.1f}"
            )

    stream_summary = scenario_summaries.get("stream")
    max_stream_degraded_rate = getattr(args, "max_stream_degraded_rate", None)
    if (
        args.stream_weight > 0
        and stream_summary
        and stream_summary["total"] >= min_samples_per_scenario
        and max_stream_degraded_rate is not None
        and stream_summary.get("degraded_fallback_rate", 0.0) > max_stream_degraded_rate
    ):
        failures.append(
            f"stream degraded_fallback_rate {stream_summary['degraded_fallback_rate']:.3f} "
            f"> {max_stream_degraded_rate:.3f}"
        )
    max_stream_first_token_p95_ms = getattr(args, "max_stream_first_token_p95_ms", None)
    if (
        args.stream_weight > 0
        and stream_summary
        and stream_summary["total"] >= min_samples_per_scenario
        and max_stream_first_token_p95_ms is not None
    ):
        first_token_sample_count = stream_summary.get("first_token_sample_count", 0)
        if first_token_sample_count < min_samples_per_scenario:
            failures.append(
                f"stream first_token_sample_count {first_token_sample_count} "
                f"< {min_samples_per_scenario}"
            )
        elif stream_summary.get("first_token_latency_p95_ms", 0.0) > max_stream_first_token_p95_ms:
            failures.append(
                f"stream first_token_latency_p95_ms {stream_summary['first_token_latency_p95_ms']:.1f} "
                f"> {max_stream_first_token_p95_ms:.1f}"
            )
    return failures


def write_json_report(path: str, report: dict[str, Any]) -> None:
    output_path = Path(path)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")


async def main() -> int:
    parser = argparse.ArgumentParser(description="Katalogcu end-to-end public flow load test")
    parser.add_argument("--base-url", default="http://localhost:5159", help="API base URL")
    parser.add_argument("--public-token", default="", help="Existing public token")
    parser.add_argument("--admin-token", default="", help="Optional admin token")
    parser.add_argument("--catalog-id", default="", help="Optional fixed catalog ID")
    parser.add_argument("--product-id", default="", help="Optional fixed product ID")
    parser.add_argument("--timeout-seconds", type=float, default=30.0, help="HTTP timeout per request")
    parser.add_argument("--duration-seconds", type=int, default=60, help="Test duration")
    parser.add_argument("--concurrency", type=int, default=8, help="Number of concurrent workers")
    parser.add_argument("--min-success-rate", type=float, default=0.90, help="Per-scenario minimum success rate")
    parser.add_argument("--max-latency-p95-ms", type=float, default=8000.0, help="Per-scenario max p95 latency")
    for scenario in ("browse", "chat", "stream", "checkout"):
        parser.add_argument(
            f"--max-{scenario}-latency-p95-ms",
            type=float,
            default=None,
            help=f"Optional {scenario} p95 latency override",
        )
    parser.add_argument(
        "--min-samples-per-scenario",
        type=int,
        default=5,
        help="Minimum completed requests required for each enabled scenario",
    )
    parser.add_argument(
        "--max-stream-degraded-rate",
        type=float,
        default=0.05,
        help="Maximum share of stream requests using an upstream failure fallback",
    )
    parser.add_argument(
        "--max-stream-first-token-p95-ms",
        type=float,
        default=5000.0,
        help="Maximum p95 time to the first non-empty SSE token",
    )
    parser.add_argument("--browse-weight", type=int, default=4, help="Browse scenario weight")
    parser.add_argument("--chat-weight", type=int, default=3, help="Non-stream chat scenario weight")
    parser.add_argument("--stream-weight", type=int, default=2, help="SSE chat scenario weight")
    parser.add_argument("--checkout-weight", type=int, default=1, help="Checkout scenario weight")
    parser.add_argument("--chat-query", action="append", dest="chat_queries", default=[], help="Custom chat query")
    parser.add_argument("--output-json", default="", help="Optional JSON report output path")
    parser.add_argument("--bootstrap-admin-email", default="", help="Bootstrap admin email when token missing")
    parser.add_argument("--bootstrap-admin-password", default="LoadAdm1nP@ss!", help="Bootstrap admin password")
    parser.add_argument("--bootstrap-admin-name", default="Load Admin", help="Bootstrap admin full name")
    parser.add_argument("--bootstrap-company-name", default="Load Company", help="Bootstrap company/storefront name")
    args = parser.parse_args()

    if args.concurrency <= 0:
        raise SystemExit("concurrency must be > 0")
    if args.duration_seconds <= 0:
        raise SystemExit("duration-seconds must be > 0")
    if args.min_samples_per_scenario <= 0:
        raise SystemExit("min-samples-per-scenario must be > 0")
    if args.max_latency_p95_ms <= 0 or any(
        limit is not None and limit <= 0
        for limit in (
            args.max_browse_latency_p95_ms,
            args.max_chat_latency_p95_ms,
            args.max_stream_latency_p95_ms,
            args.max_checkout_latency_p95_ms,
        )
    ):
        raise SystemExit("latency thresholds must be > 0")
    if args.max_stream_first_token_p95_ms <= 0:
        raise SystemExit("max-stream-first-token-p95-ms must be > 0")

    base_url = trim_slash(args.base_url)
    fixture = resolve_fixture(args)
    queries = args.chat_queries or DEFAULT_CHAT_QUERIES

    timeout = httpx.Timeout(args.timeout_seconds)
    limits = httpx.Limits(max_connections=max(args.concurrency * 2, 20), max_keepalive_connections=max(args.concurrency, 8))
    results: dict[str, list[ScenarioOutcome]] = defaultdict(list)
    sequence = [0]
    weights = [
        ("browse", args.browse_weight),
        ("chat", args.chat_weight),
        ("stream", args.stream_weight),
        ("checkout", args.checkout_weight),
    ]

    if not fixture.product_id and args.checkout_weight > 0:
        print("Warning: no public product available for checkout scenario; checkout load will be skipped.")

    print(
        f"Running e2e public load test: duration={args.duration_seconds}s concurrency={args.concurrency} "
        f"catalogId={fixture.catalog_id} productId={fixture.product_id or 'none'}"
    )

    load_started = time.monotonic()
    end_time = load_started + args.duration_seconds
    async with httpx.AsyncClient(timeout=timeout, limits=limits) as client:
        await asyncio.gather(*[
            worker(worker_id, client, base_url, fixture, queries, end_time, sequence, results, weights)
            for worker_id in range(args.concurrency)
        ])
    elapsed_seconds = time.monotonic() - load_started

    scenario_summaries = {
        name: summarize_scenario(results.get(name, []), elapsed_seconds)
        for name in ("browse", "chat", "stream", "checkout")
    }
    overall = summarize_scenario(
        [item for items in results.values() for item in items],
        elapsed_seconds,
    )

    report = {
        "schema_version": 1,
        "config": {
            "base_url": base_url,
            "duration_seconds": args.duration_seconds,
            "elapsed_seconds": elapsed_seconds,
            "concurrency": args.concurrency,
            "timeout_seconds": args.timeout_seconds,
            "catalog_id": fixture.catalog_id,
            "product_id": fixture.product_id,
            "chat_queries": queries,
            "weights": {name: weight for name, weight in weights},
            "thresholds": {
                "min_success_rate": args.min_success_rate,
                "max_latency_p95_ms": args.max_latency_p95_ms,
                "max_latency_p95_ms_by_scenario": scenario_latency_limits(args),
                "min_samples_per_scenario": args.min_samples_per_scenario,
                "max_stream_degraded_rate": args.max_stream_degraded_rate,
                "max_stream_first_token_p95_ms": args.max_stream_first_token_p95_ms,
            },
        },
        "overall": overall,
        "scenarios": scenario_summaries,
    }

    print(json.dumps(report, ensure_ascii=False, indent=2))

    if args.output_json:
        write_json_report(args.output_json, report)

    failures = check_thresholds(args, scenario_summaries)
    if failures:
        print("Threshold failures:")
        for failure in failures:
            print("-", failure)
        return 2

    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
