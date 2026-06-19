from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from config import settings  # noqa: E402
from services import vector_db  # noqa: E402


class _FakeConn:
    async def fetchval(self, query: str):
        return 1


class _FakeHybridConn:
    def __init__(self):
        self.sql = ""
        self.args = ()

    async def fetch(self, query: str, *args):
        self.sql = query
        self.args = args
        return []


class _FakeAcquireContext:
    def __init__(self, conn: _FakeConn):
        self._conn = conn

    async def __aenter__(self):
        return self._conn

    async def __aexit__(self, exc_type, exc, tb):
        return False


class _FakePool:
    def __init__(self):
        self.conn = _FakeConn()
        self.closed = False

    def acquire(self):
        return _FakeAcquireContext(self.conn)

    async def close(self):
        self.closed = True

    def get_size(self):
        return 2

    def get_idle_size(self):
        return 1

    def get_min_size(self):
        return 2

    def get_max_size(self):
        return 10


class VectorDbLifecycleTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self._original_dsn = settings.DB_CONNECTION_STRING
        await vector_db.close_db_pool()
        vector_db._pool_state.update(
            {
                "ready": False,
                "mode": "uninitialized",
                "last_error": None,
                "last_init_started_at": None,
                "last_init_completed_at": None,
                "last_healthcheck_at": None,
                "last_healthcheck_latency_ms": None,
                "ephemeral_fallback_uses": 0,
            }
        )

    async def asyncTearDown(self):
        settings.DB_CONNECTION_STRING = self._original_dsn
        await vector_db.close_db_pool()

    async def test_init_db_pool_reports_missing_dsn(self):
        settings.DB_CONNECTION_STRING = ""

        state = await vector_db.init_db_pool()

        self.assertFalse(state["ready"])
        self.assertEqual(state["mode"], "missing_dsn")
        self.assertIn("bağlantı", state["last_error"].lower())

    async def test_init_db_pool_is_idempotent_and_healthchecked(self):
        settings.DB_CONNECTION_STRING = "postgresql://user:pass@localhost/db"
        fake_pool = _FakePool()
        call_count = 0

        async def fake_create_pool(*args, **kwargs):
            nonlocal call_count
            call_count += 1
            return fake_pool

        with patch("services.vector_db.asyncpg.create_pool", side_effect=fake_create_pool):
            first = await vector_db.init_db_pool()
            second = await vector_db.init_db_pool()

        self.assertEqual(call_count, 1)
        self.assertTrue(first["ready"])
        self.assertEqual(first["mode"], "pool")
        self.assertTrue(second["ready"])
        self.assertEqual(second["size"], 2)
        self.assertEqual(second["idle"], 1)

    async def test_close_db_pool_marks_state_closed(self):
        settings.DB_CONNECTION_STRING = "postgresql://user:pass@localhost/db"

        async def fake_create_pool(*args, **kwargs):
            return _FakePool()

        with patch("services.vector_db.asyncpg.create_pool", side_effect=fake_create_pool):
            await vector_db.init_db_pool()

        await vector_db.close_db_pool()
        state = vector_db.get_db_pool_state()
        self.assertFalse(state["ready"])
        self.assertEqual(state["mode"], "closed")

    async def test_hybrid_search_caps_fts_candidate_lane(self):
        conn = _FakeHybridConn()

        async def fake_get_conn():
            return conn, False

        async def fake_release_conn(_conn, _from_pool):
            return None

        with (
            patch("services.vector_db._get_conn", side_effect=fake_get_conn),
            patch("services.vector_db._release_conn", side_effect=fake_release_conn),
        ):
            rows = await vector_db.hybrid_search_vector_db(
                query_vector=[0.0] * 3072,
                query_text="vida m5 kayar kapak",
                candidate_limit=200,
            )

        self.assertEqual(rows, [])
        self.assertEqual(conn.args[4], 200)
        self.assertEqual(conn.args[7], vector_db.FTS_CANDIDATE_LIMIT)

        vector_lane = conn.sql.index("vector_matches AS")
        lexical_lane = conn.sql.index("lexical_matches AS")
        vector_limit = conn.sql.index("LIMIT (SELECT candidate_limit FROM query_input)", vector_lane)
        lexical_limit = conn.sql.index("LIMIT (SELECT lexical_candidate_limit FROM query_input)", lexical_lane)

        self.assertLess(vector_limit, lexical_lane)
        self.assertGreater(lexical_limit, lexical_lane)


if __name__ == "__main__":
    unittest.main()
