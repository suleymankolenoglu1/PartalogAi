from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import main  # noqa: E402
from config import settings  # noqa: E402


class MainHealthTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self._skip_models = settings.STARTUP_SKIP_MODEL_LOADING
        settings.STARTUP_SKIP_MODEL_LOADING = True

    async def asyncTearDown(self):
        settings.STARTUP_SKIP_MODEL_LOADING = self._skip_models

    async def test_readiness_returns_200_when_db_is_ready(self):
        ready_state = {
            "ready": True,
            "mode": "pool",
            "last_error": None,
            "last_healthcheck_latency_ms": 12.5,
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
                    response = await client.get("/health/ready")

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertTrue(payload["ready"])
        self.assertEqual(payload["status"], "ready")

    async def test_readiness_returns_503_when_db_is_not_ready(self):
        startup_state = {
            "ready": False,
            "mode": "init_failed",
            "last_error": "db unavailable",
            "last_healthcheck_latency_ms": None,
            "size": 0,
            "idle": 0,
            "min_size": 2,
            "max_size": 10,
            "ephemeral_fallback_enabled": True,
            "dsn_configured": True,
        }

        with patch.object(main, "init_db_pool", AsyncMock(return_value=startup_state)), \
             patch.object(main, "check_db_pool_health", AsyncMock(return_value=startup_state)), \
             patch.object(main, "close_db_pool", AsyncMock()):
            transport = httpx.ASGITransport(app=main.app)
            async with main.app.router.lifespan_context(main.app):
                async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
                    response = await client.get("/health/ready")

        self.assertEqual(response.status_code, 503)
        payload = response.json()
        self.assertFalse(payload["ready"])
        self.assertEqual(payload["components"]["db"]["mode"], "init_failed")


if __name__ == "__main__":
    unittest.main()
