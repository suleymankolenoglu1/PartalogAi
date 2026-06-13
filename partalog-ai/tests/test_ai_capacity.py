import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.ai_capacity import AiCapacityGuard, AiCapacityLimitExceeded


class AiCapacityGuardTests(unittest.IsolatedAsyncioTestCase):
    async def test_try_acquire_enforces_global_limit(self):
        guard = AiCapacityGuard(global_concurrency=1, acquire_timeout_seconds=0)

        lease = await guard.try_acquire()
        with self.assertRaises(AiCapacityLimitExceeded):
            await guard.try_acquire()

        self.assertEqual(guard.snapshot().active_chats, 1)
        await lease.release()

    async def test_release_frees_capacity(self):
        guard = AiCapacityGuard(global_concurrency=1, acquire_timeout_seconds=0)

        lease = await guard.try_acquire()
        await lease.release()
        second = await guard.try_acquire()

        self.assertEqual(guard.snapshot().active_chats, 1)
        await second.release()
        self.assertEqual(guard.snapshot().active_chats, 0)

    async def test_lease_context_manager_releases_on_exit(self):
        guard = AiCapacityGuard(global_concurrency=1, acquire_timeout_seconds=0)

        async with guard.lease():
            self.assertTrue(guard.snapshot().saturated)

        self.assertFalse(guard.snapshot().saturated)

    async def test_snapshot_reports_redis_mode_when_provider_is_redis(self):
        guard = AiCapacityGuard(
            global_concurrency=2,
            acquire_timeout_seconds=0,
            provider="redis",
            redis_url="redis://localhost:6379/0",
        )

        snapshot = guard.snapshot()

        self.assertEqual(snapshot.mode, "redis-distributed")
        self.assertTrue(snapshot.distributed)

    async def test_in_memory_health_is_ready(self):
        guard = AiCapacityGuard(global_concurrency=2, acquire_timeout_seconds=0)

        health = await guard.check_health()

        self.assertTrue(health.ready)
        self.assertEqual(health.mode, "in-memory")
        self.assertIsNone(health.error)


if __name__ == "__main__":
    unittest.main()
