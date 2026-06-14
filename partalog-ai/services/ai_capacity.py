"""In-process capacity guard for expensive chat generation work."""

from __future__ import annotations

import asyncio
import hashlib
import time
import socket
import uuid
from contextlib import asynccontextmanager
from dataclasses import dataclass
from typing import Any, AsyncIterator, Awaitable, Callable

import asyncpg
from loguru import logger
try:
    import redis.asyncio as redis_async
    from redis.exceptions import RedisError
except ImportError:  # pragma: no cover - dependency is present in deployed env via requirements.txt
    redis_async = None
    RedisError = RuntimeError

from config import settings


@dataclass(frozen=True)
class AiCapacitySnapshot:
    active_chats: int
    global_concurrent_chats: int
    saturated: bool
    mode: str = "in-memory"
    distributed: bool = False


@dataclass(frozen=True)
class AiCapacityHealth:
    ready: bool
    mode: str
    provider: str
    latency_ms: float | None = None
    error: str | None = None


class AiCapacityLimitExceeded(RuntimeError):
    """Raised when the service is already handling too many chat requests."""


class AiCapacityLease:
    def __init__(self, release: Callable[[], Awaitable[None]]):
        self._release = release
        self._released = False

    async def release(self) -> None:
        if self._released:
            return
        self._released = True
        await self._release()

    async def __aenter__(self) -> "AiCapacityLease":
        return self

    async def __aexit__(self, exc_type, exc, tb) -> None:
        await self.release()


class AiCapacityGuard:
    _REDIS_ACQUIRE_SCRIPT = """
local global_key = KEYS[1]
local partition_key = KEYS[2]
local now_ms = tonumber(ARGV[1])
local expires_ms = tonumber(ARGV[2])
local global_limit = tonumber(ARGV[3])
local partition_limit = tonumber(ARGV[4])
local lease_id = ARGV[5]
local ttl_ms = tonumber(ARGV[6])

redis.call('ZREMRANGEBYSCORE', global_key, '-inf', now_ms)
redis.call('ZREMRANGEBYSCORE', partition_key, '-inf', now_ms)

local global_count = redis.call('ZCARD', global_key)
if global_count >= global_limit then
    return {0, global_count}
end

local partition_count = redis.call('ZCARD', partition_key)
if partition_count >= partition_limit then
    return {0, global_count}
end

redis.call('ZADD', global_key, expires_ms, lease_id)
redis.call('ZADD', partition_key, expires_ms, lease_id)
redis.call('PEXPIRE', global_key, ttl_ms * 2)
redis.call('PEXPIRE', partition_key, ttl_ms * 2)

return {1, global_count + 1}
"""
    _REDIS_RELEASE_SCRIPT = """
redis.call('ZREM', KEYS[1], ARGV[1])
redis.call('ZREM', KEYS[2], ARGV[1])
return 1
"""

    def __init__(
        self,
        global_concurrency: int,
        acquire_timeout_seconds: float,
        *,
        provider: str = "inmemory",
        use_distributed_leases: bool = False,
        db_dsn: str = "",
        redis_url: str = "",
        redis_key_prefix: str = "partalog:ai-capacity",
        lease_ttl_seconds: int = 180,
        pool_name: str = "python-chat",
    ):
        self.global_concurrency = max(1, int(global_concurrency or 1))
        self.acquire_timeout_seconds = max(0.0, float(acquire_timeout_seconds or 0.0))
        normalized_provider = (provider or "").strip().lower().replace("-", "")
        if not normalized_provider or normalized_provider == "inmemory":
            normalized_provider = "postgres" if use_distributed_leases else "inmemory"
        self.provider = normalized_provider
        self.use_redis = self.provider == "redis" and bool(redis_url)
        self.use_distributed_leases = self.provider in {"postgres", "postgresdistributed"} and bool(db_dsn)
        self.db_dsn = db_dsn
        self.redis_url = redis_url
        self.redis_key_prefix = (redis_key_prefix or "partalog:ai-capacity").strip().rstrip(":")
        self.lease_ttl_seconds = max(30, int(lease_ttl_seconds or 180))
        self.pool_name = (pool_name or "python-chat").strip() or "python-chat"
        self.instance_id = f"{socket.gethostname()}:{uuid.uuid4().hex}"
        self._semaphore = asyncio.Semaphore(self.global_concurrency)
        self._active = 0
        self._lock = asyncio.Lock()
        self._last_distributed_active = 0
        self._redis_client: Any | None = None

    @asynccontextmanager
    async def lease(self) -> AsyncIterator[None]:
        lease = await self.try_acquire()
        try:
            yield
        finally:
            await lease.release()

    async def try_acquire(self) -> AiCapacityLease:
        if self.use_redis:
            return await self._try_acquire_redis()

        if self.use_distributed_leases:
            return await self._try_acquire_distributed()

        try:
            if self.acquire_timeout_seconds <= 0:
                if self._semaphore.locked():
                    raise asyncio.TimeoutError()
                await self._semaphore.acquire()
            else:
                await asyncio.wait_for(self._semaphore.acquire(), timeout=self.acquire_timeout_seconds)
        except asyncio.TimeoutError as exc:
            logger.warning(
                "AI chat capacity saturated active={} limit={}",
                self._active,
                self.global_concurrency,
            )
            raise AiCapacityLimitExceeded("ai_capacity_limited") from exc

        async with self._lock:
            self._active += 1

        return AiCapacityLease(self._release)

    async def _try_acquire_distributed(self) -> AiCapacityLease:
        timeout = max(0.05, self.acquire_timeout_seconds)
        conn: asyncpg.Connection | None = None
        transaction = None
        try:
            conn = await asyncio.wait_for(
                asyncpg.connect(self.db_dsn, statement_cache_size=settings.DB_STATEMENT_CACHE_SIZE),
                timeout=timeout,
            )
            transaction = conn.transaction()
            await transaction.start()

            await conn.execute(
                "SELECT pg_advisory_xact_lock(hashtext($1));",
                f"partalog_ai_capacity:{self.pool_name}",
            )
            await conn.execute(
                'DELETE FROM "AiCapacityLeases" WHERE "PoolName" = $1 AND "ExpiresAt" <= now();',
                self.pool_name,
            )
            await conn.execute('DELETE FROM "AiCapacityLeases" WHERE "ExpiresAt" <= now();')

            active = await conn.fetchval(
                'SELECT count(*) FROM "AiCapacityLeases" WHERE "PoolName" = $1 AND "ExpiresAt" > now();',
                self.pool_name,
            )
            active_count = int(active or 0)
            if active_count >= self.global_concurrency:
                await transaction.rollback()
                self._last_distributed_active = active_count
                raise AiCapacityLimitExceeded("ai_capacity_limited")

            lease_id = uuid.uuid4()
            await conn.execute(
                """
                INSERT INTO "AiCapacityLeases" ("Id", "PoolName", "PartitionKey", "InstanceId", "CreatedAt", "ExpiresAt")
                VALUES ($1, $2, $3, $4, now(), now() + ($5 * interval '1 second'));
                """,
                lease_id,
                self.pool_name,
                "global",
                self.instance_id,
                self.lease_ttl_seconds,
            )
            await transaction.commit()
            self._last_distributed_active = active_count + 1
            return AiCapacityLease(lambda: self._release_distributed(lease_id))
        except AiCapacityLimitExceeded:
            logger.warning(
                "AI chat distributed capacity saturated active={} limit={} pool={}",
                self._last_distributed_active,
                self.global_concurrency,
                self.pool_name,
            )
            raise
        except (asyncio.TimeoutError, asyncpg.UndefinedTableError) as exc:
            if transaction is not None:
                try:
                    await transaction.rollback()
                except Exception:
                    pass
            logger.warning(
                "AI chat distributed capacity unavailable pool={} error={}",
                self.pool_name,
                type(exc).__name__,
            )
            raise AiCapacityLimitExceeded("ai_capacity_limited") from exc
        except Exception as exc:
            if transaction is not None:
                try:
                    await transaction.rollback()
                except Exception:
                    pass
            logger.exception("AI chat distributed capacity lease failed pool={}", self.pool_name)
            raise AiCapacityLimitExceeded("ai_capacity_limited") from exc
        finally:
            if conn is not None:
                await conn.close()

    async def _try_acquire_redis(self) -> AiCapacityLease:
        timeout = max(0.05, self.acquire_timeout_seconds)
        lease_id = uuid.uuid4().hex
        global_key, partition_key = self._redis_keys("global")
        now_ms = int(time.time() * 1000)
        ttl_ms = self.lease_ttl_seconds * 1000
        expires_ms = now_ms + ttl_ms

        try:
            client = self._get_redis_client()
            result = await asyncio.wait_for(
                client.eval(
                    self._REDIS_ACQUIRE_SCRIPT,
                    2,
                    global_key,
                    partition_key,
                    now_ms,
                    expires_ms,
                    self.global_concurrency,
                    self.global_concurrency,
                    lease_id,
                    ttl_ms,
                ),
                timeout=timeout,
            )
            acquired = int(result[0]) == 1
            active_count = int(result[1])
            self._last_distributed_active = active_count
            if not acquired:
                raise AiCapacityLimitExceeded("ai_capacity_limited")

            return AiCapacityLease(lambda: self._release_redis(global_key, partition_key, lease_id))
        except AiCapacityLimitExceeded:
            logger.warning(
                "AI chat Redis capacity saturated active={} limit={} pool={}",
                self._last_distributed_active,
                self.global_concurrency,
                self.pool_name,
            )
            raise
        except (asyncio.TimeoutError, RedisError) as exc:
            logger.warning(
                "AI chat Redis capacity unavailable pool={} error={}",
                self.pool_name,
                type(exc).__name__,
            )
            raise AiCapacityLimitExceeded("ai_capacity_limited") from exc
        except Exception as exc:
            logger.exception("AI chat Redis capacity lease failed pool={}", self.pool_name)
            raise AiCapacityLimitExceeded("ai_capacity_limited") from exc

    async def _release_redis(self, global_key: str, partition_key: str, lease_id: str) -> None:
        try:
            client = self._get_redis_client()
            await client.eval(self._REDIS_RELEASE_SCRIPT, 2, global_key, partition_key, lease_id)
            self._last_distributed_active = max(0, self._last_distributed_active - 1)
        except Exception:
            logger.warning(
                "AI chat Redis capacity lease release failed; TTL cleanup will recover pool={}",
                self.pool_name,
            )

    async def _release_distributed(self, lease_id: uuid.UUID) -> None:
        try:
            conn = await asyncpg.connect(self.db_dsn, statement_cache_size=settings.DB_STATEMENT_CACHE_SIZE)
            try:
                await conn.execute(
                    'DELETE FROM "AiCapacityLeases" WHERE "PoolName" = $1 AND "Id" = $2;',
                    self.pool_name,
                    lease_id,
                )
                self._last_distributed_active = max(0, self._last_distributed_active - 1)
            finally:
                await conn.close()
        except Exception:
            logger.warning(
                "AI chat distributed capacity lease release failed; TTL cleanup will recover pool={}",
                self.pool_name,
            )

    async def _release(self) -> None:
        async with self._lock:
            self._active = max(0, self._active - 1)
        self._semaphore.release()

    async def check_health(self) -> AiCapacityHealth:
        started_at = time.monotonic()
        try:
            if self.use_redis:
                client = self._get_redis_client()
                await asyncio.wait_for(client.ping(), timeout=max(0.05, self.acquire_timeout_seconds))
            elif self.use_distributed_leases:
                conn = await asyncio.wait_for(
                    asyncpg.connect(self.db_dsn, statement_cache_size=settings.DB_STATEMENT_CACHE_SIZE),
                    timeout=max(0.05, self.acquire_timeout_seconds),
                )
                try:
                    await conn.fetchval("SELECT 1;")
                finally:
                    await conn.close()

            return AiCapacityHealth(
                ready=True,
                mode=self.snapshot().mode,
                provider=self.provider,
                latency_ms=(time.monotonic() - started_at) * 1000,
            )
        except Exception as exc:
            return AiCapacityHealth(
                ready=False,
                mode=self.snapshot().mode,
                provider=self.provider,
                latency_ms=(time.monotonic() - started_at) * 1000,
                error=str(exc),
            )

    def snapshot(self) -> AiCapacitySnapshot:
        if self.use_redis:
            active = max(0, self._last_distributed_active)
            return AiCapacitySnapshot(
                active_chats=active,
                global_concurrent_chats=self.global_concurrency,
                saturated=active >= self.global_concurrency,
                mode="redis-distributed",
                distributed=True,
            )

        if self.use_distributed_leases:
            active = max(0, self._last_distributed_active)
            return AiCapacitySnapshot(
                active_chats=active,
                global_concurrent_chats=self.global_concurrency,
                saturated=active >= self.global_concurrency,
                mode="postgres-distributed",
                distributed=True,
            )

        active = max(0, self._active)
        return AiCapacitySnapshot(
            active_chats=active,
            global_concurrent_chats=self.global_concurrency,
            saturated=active >= self.global_concurrency,
        )

    def _get_redis_client(self) -> Any:
        if redis_async is None:
            raise AiCapacityLimitExceeded("ai_capacity_limited")

        if self._redis_client is None:
            self._redis_client = redis_async.from_url(
                self.redis_url,
                encoding="utf-8",
                decode_responses=False,
            )
        return self._redis_client

    def _redis_keys(self, partition_key: str) -> tuple[str, str]:
        global_key = f"{self.redis_key_prefix}:{self.pool_name}:global"
        partition_hash = hashlib.sha256(partition_key.encode("utf-8")).hexdigest()
        return global_key, f"{self.redis_key_prefix}:{self.pool_name}:partition:{partition_hash}"


chat_capacity_guard = AiCapacityGuard(
    global_concurrency=settings.AI_CHAT_GLOBAL_CONCURRENCY,
    acquire_timeout_seconds=settings.AI_CHAT_ACQUIRE_TIMEOUT_SECONDS,
    provider=settings.AI_CHAT_CAPACITY_PROVIDER,
    use_distributed_leases=settings.AI_CHAT_USE_DISTRIBUTED_LEASES,
    db_dsn=settings.db_dsn,
    redis_url=settings.AI_CHAT_REDIS_URL,
    redis_key_prefix=settings.AI_CHAT_REDIS_KEY_PREFIX,
    lease_ttl_seconds=settings.AI_CHAT_DISTRIBUTED_LEASE_TTL_SECONDS,
    pool_name=settings.AI_CHAT_DISTRIBUTED_POOL_NAME,
)


def get_ai_capacity_snapshot() -> dict:
    snapshot = chat_capacity_guard.snapshot()
    return {
        "active_chats": snapshot.active_chats,
        "global_concurrent_chats": snapshot.global_concurrent_chats,
        "saturated": snapshot.saturated,
        "mode": snapshot.mode,
        "distributed": snapshot.distributed,
    }


async def check_ai_capacity_health() -> dict:
    health = await chat_capacity_guard.check_health()
    return {
        "ready": health.ready,
        "mode": health.mode,
        "provider": health.provider,
        "latency_ms": health.latency_ms,
        "error": health.error,
    }
