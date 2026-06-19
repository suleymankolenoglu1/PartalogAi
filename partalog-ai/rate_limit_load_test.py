from __future__ import annotations

import argparse
import asyncio
import statistics
import time
from typing import List, Tuple

import httpx


def percentile(values: List[float], p: float) -> float:
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


async def request_probe(client: httpx.AsyncClient, url: str, client_id: str) -> Tuple[int, float]:
    started = time.perf_counter()
    response = await client.get(url, headers={"x-client-id": client_id})
    latency_ms = (time.perf_counter() - started) * 1000.0
    return response.status_code, latency_ms


def summarize(results: List[Tuple[int, float]]) -> dict:
    statuses = [status for status, _ in results]
    latencies = [latency for _, latency in results]
    total = len(results) or 1
    return {
        "total": len(results),
        "success_rate": sum(1 for status in statuses if status == 200) / total,
        "rate_limited_rate": sum(1 for status in statuses if status == 429) / total,
        "server_error_rate": sum(1 for status in statuses if status >= 500) / total,
        "latency_avg_ms": statistics.mean(latencies) if latencies else 0.0,
        "latency_p95_ms": percentile(latencies, 0.95) if latencies else 0.0,
    }


async def run_burst(client: httpx.AsyncClient, url: str, client_id: str, request_count: int) -> List[Tuple[int, float]]:
    return await asyncio.gather(*[request_probe(client, url, client_id) for _ in range(request_count)])


async def run_sustained(
    client: httpx.AsyncClient,
    url: str,
    client_id: str,
    request_count: int,
    interval_seconds: float,
) -> List[Tuple[int, float]]:
    results: List[Tuple[int, float]] = []
    for _ in range(request_count):
        results.append(await request_probe(client, url, client_id))
        await asyncio.sleep(interval_seconds)
    return results


def check_thresholds(name: str, summary: dict, args: argparse.Namespace) -> List[str]:
    failures: List[str] = []
    if name == "burst":
        if summary["rate_limited_rate"] < args.burst_min_429_rate:
            failures.append(
                f"burst rate_limited_rate {summary['rate_limited_rate']:.3f} < {args.burst_min_429_rate:.3f}"
            )
        if summary["server_error_rate"] > args.burst_max_5xx_rate:
            failures.append(
                f"burst server_error_rate {summary['server_error_rate']:.3f} > {args.burst_max_5xx_rate:.3f}"
            )
    else:
        if summary["success_rate"] < args.sustained_min_success_rate:
            failures.append(
                f"sustained success_rate {summary['success_rate']:.3f} < {args.sustained_min_success_rate:.3f}"
            )
        if summary["rate_limited_rate"] > args.sustained_max_429_rate:
            failures.append(
                f"sustained rate_limited_rate {summary['rate_limited_rate']:.3f} > {args.sustained_max_429_rate:.3f}"
            )

    if summary["latency_p95_ms"] > args.max_latency_p95_ms:
        failures.append(
            f"{name} latency_p95_ms {summary['latency_p95_ms']:.1f} > {args.max_latency_p95_ms:.1f}"
        )

    return failures


async def main() -> int:
    parser = argparse.ArgumentParser(description="Partalog AI rate-limit load test")
    parser.add_argument("--base-url", default="http://localhost:8000")
    parser.add_argument("--endpoint", default="/health/rate-limit-probe")
    parser.add_argument("--timeout-seconds", type=float, default=10.0)
    parser.add_argument("--burst-requests", type=int, default=12)
    parser.add_argument("--burst-min-429-rate", type=float, default=0.50)
    parser.add_argument("--burst-max-5xx-rate", type=float, default=0.0)
    parser.add_argument("--sustained-requests", type=int, default=8)
    parser.add_argument("--sustained-interval-seconds", type=float, default=0.25)
    parser.add_argument("--sustained-min-success-rate", type=float, default=0.875)
    parser.add_argument("--sustained-max-429-rate", type=float, default=0.125)
    parser.add_argument("--max-latency-p95-ms", type=float, default=250.0)
    args = parser.parse_args()

    url = f"{args.base_url.rstrip('/')}{args.endpoint}"
    timeout = httpx.Timeout(args.timeout_seconds)

    async with httpx.AsyncClient(timeout=timeout) as client:
        burst_results = await run_burst(client, url, "burst-client", args.burst_requests)
        await asyncio.sleep(1.1)
        sustained_results = await run_sustained(
            client,
            url,
            "sustained-client",
            args.sustained_requests,
            args.sustained_interval_seconds,
        )

    burst_summary = summarize(burst_results)
    sustained_summary = summarize(sustained_results)

    print("Burst:", burst_summary)
    print("Sustained:", sustained_summary)

    failures = check_thresholds("burst", burst_summary, args)
    failures.extend(check_thresholds("sustained", sustained_summary, args))
    if failures:
        print("Threshold failures:")
        for failure in failures:
            print("-", failure)
        return 2

    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
