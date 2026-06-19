from __future__ import annotations

import asyncio
import sys
import time
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import main  # noqa: E402
from config import settings  # noqa: E402

BURST_REQUEST_COUNT = 12
BURST_MIN_429_RATE = 0.50
BURST_MAX_5XX_RATE = 0.0
SUSTAINED_REQUEST_COUNT = 8
SUSTAINED_INTERVAL_SECONDS = 0.25
SUSTAINED_MIN_SUCCESS_RATE = 0.875
SUSTAINED_MAX_429_RATE = 0.125
LATENCY_P95_SLO_MS = 250.0


def _p95(values: list[float]) -> float:
    if not values:
        return 0.0
    if len(values) == 1:
        return values[0]
    sorted_values = sorted(values)
    rank = (len(sorted_values) - 1) * 0.95
    lower = int(rank)
    upper = min(lower + 1, len(sorted_values) - 1)
    fraction = rank - lower
    return sorted_values[lower] * (1 - fraction) + sorted_values[upper] * fraction


class RateLimitLoadTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self._skip_models = settings.STARTUP_SKIP_MODEL_LOADING
        settings.STARTUP_SKIP_MODEL_LOADING = True

    async def asyncTearDown(self):
        settings.STARTUP_SKIP_MODEL_LOADING = self._skip_models
        main.app.state.limiter._storage.reset()

    async def test_rate_limit_probe_meets_burst_and_sustained_slos(self):
        ready_state = {
            "ready": True,
            "mode": "pool",
            "last_error": None,
            "last_healthcheck_latency_ms": 10.0,
            "size": 2,
            "idle": 1,
            "min_size": 2,
            "max_size": 10,
            "ephemeral_fallback_enabled": True,
            "dsn_configured": True,
        }

        with patch.object(main, "init_db_pool", AsyncMock(return_value=ready_state)), \
             patch.object(main, "check_db_pool_health", AsyncMock(return_value=ready_state)), \
             patch.object(main, "close_db_pool", AsyncMock()):
            transport = httpx.ASGITransport(app=main.app)
            async with main.app.router.lifespan_context(main.app):
                async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
                    main.app.state.limiter._storage.reset()
                    burst_results = await asyncio.gather(
                        *[self._request_probe(client, "burst-client") for _ in range(BURST_REQUEST_COUNT)]
                    )

                    main.app.state.limiter._storage.reset()
                    sustained_results = []
                    for _ in range(SUSTAINED_REQUEST_COUNT):
                        sustained_results.append(await self._request_probe(client, "sustained-client"))
                        await asyncio.sleep(SUSTAINED_INTERVAL_SECONDS)

        self._assert_burst_slo(burst_results)
        self._assert_sustained_slo(sustained_results)

    async def _request_probe(self, client: httpx.AsyncClient, client_id: str) -> tuple[int, float]:
        started = time.perf_counter()
        response = await client.get("/health/rate-limit-probe", headers={"x-client-id": client_id})
        latency_ms = (time.perf_counter() - started) * 1000.0
        return response.status_code, latency_ms

    def _assert_burst_slo(self, results: list[tuple[int, float]]) -> None:
        statuses = [status for status, _ in results]
        latencies = [latency for _, latency in results]

        rate_limited = sum(1 for status in statuses if status == 429) / len(statuses)
        server_error_rate = sum(1 for status in statuses if status >= 500) / len(statuses)
        self.assertGreaterEqual(rate_limited, BURST_MIN_429_RATE)
        self.assertLessEqual(server_error_rate, BURST_MAX_5XX_RATE)
        self.assertLessEqual(_p95(latencies), LATENCY_P95_SLO_MS)

    def _assert_sustained_slo(self, results: list[tuple[int, float]]) -> None:
        statuses = [status for status, _ in results]
        latencies = [latency for _, latency in results]

        success_rate = sum(1 for status in statuses if status == 200) / len(statuses)
        rate_limited = sum(1 for status in statuses if status == 429) / len(statuses)

        self.assertGreaterEqual(success_rate, SUSTAINED_MIN_SUCCESS_RATE)
        self.assertLessEqual(rate_limited, SUSTAINED_MAX_429_RATE)
        self.assertLessEqual(_p95(latencies), LATENCY_P95_SLO_MS)


if __name__ == "__main__":
    unittest.main()
