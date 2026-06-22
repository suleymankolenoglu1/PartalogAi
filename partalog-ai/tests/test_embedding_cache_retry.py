from __future__ import annotations

import asyncio
import sys
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from config import settings  # noqa: E402
from services import embedding  # noqa: E402


class _FakeResponse:
    def __init__(self, status: int, payload: dict | None = None, text: str = "", headers: dict | None = None):
        self.status = status
        self._payload = payload or {}
        self._text = text
        self.headers = headers or {}

    async def json(self):
        return self._payload

    async def text(self):
        return self._text


class _FakePostContext:
    def __init__(self, response: _FakeResponse):
        self._response = response

    async def __aenter__(self):
        return self._response

    async def __aexit__(self, exc_type, exc, tb):
        return False


class _FakeSession:
    def __init__(self, responses: list[_FakeResponse], calls: list[dict]):
        self._responses = responses
        self._calls = calls

    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc, tb):
        return False

    def post(self, url: str, json: dict):
        self._calls.append({"url": url, "json": json})
        if not self._responses:
            raise AssertionError("unexpected extra embedding request")
        return _FakePostContext(self._responses.pop(0))


def _vertex_payload(vector: list[float]) -> dict:
    return {"predictions": [{"embeddings": {"values": vector}}]}


class EmbeddingCacheRetryTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self._original_provider = settings.GENAI_PROVIDER
        self._original_attempts = settings.GENAI_RETRY_ATTEMPTS
        self._original_base_delay = settings.GENAI_RETRY_BASE_DELAY_SECONDS
        self._original_max_delay = settings.GENAI_RETRY_MAX_DELAY_SECONDS
        self._original_cache_ttl = settings.GENAI_EMBEDDING_CACHE_TTL_SECONDS
        self._original_cache_max = settings.GENAI_EMBEDDING_CACHE_MAX_ITEMS

        settings.GENAI_PROVIDER = "vertex"
        settings.GENAI_RETRY_ATTEMPTS = 2
        settings.GENAI_RETRY_BASE_DELAY_SECONDS = 0
        settings.GENAI_RETRY_MAX_DELAY_SECONDS = 0
        settings.GENAI_EMBEDDING_CACHE_TTL_SECONDS = 900
        settings.GENAI_EMBEDDING_CACHE_MAX_ITEMS = 100
        await embedding.clear_embedding_cache()

    async def asyncTearDown(self):
        settings.GENAI_PROVIDER = self._original_provider
        settings.GENAI_RETRY_ATTEMPTS = self._original_attempts
        settings.GENAI_RETRY_BASE_DELAY_SECONDS = self._original_base_delay
        settings.GENAI_RETRY_MAX_DELAY_SECONDS = self._original_max_delay
        settings.GENAI_EMBEDDING_CACHE_TTL_SECONDS = self._original_cache_ttl
        settings.GENAI_EMBEDDING_CACHE_MAX_ITEMS = self._original_cache_max
        await embedding.clear_embedding_cache()

    async def test_retries_retryable_embedding_429_then_caches_success(self):
        vector = [0.1] * 3072
        responses = [
            _FakeResponse(429, text="quota", headers={"Retry-After": "0"}),
            _FakeResponse(200, payload=_vertex_payload(vector)),
        ]
        calls: list[dict] = []

        def fake_session(*_args, **_kwargs):
            return _FakeSession(responses, calls)

        with (
            patch("services.embedding.provider.has_credentials", return_value=True),
            patch("services.embedding.provider.embed_content_url", return_value="https://example.test/embed"),
            patch("services.embedding.provider.build_headers", new=AsyncMock(return_value={})),
            patch("services.embedding.aiohttp.ClientSession", side_effect=fake_session),
        ):
            result = await embedding.get_text_embedding("İplik kılavuzu")

        self.assertEqual(result, vector)
        self.assertEqual(len(calls), 2)

        state = await embedding.get_embedding_cache_state()
        self.assertEqual(state["cache_size"], 1)

    async def test_cache_normalizes_case_and_whitespace(self):
        vector = [0.2] * 3072
        responses = [_FakeResponse(200, payload=_vertex_payload(vector))]
        calls: list[dict] = []

        def fake_session(*_args, **_kwargs):
            return _FakeSession(responses, calls)

        with (
            patch("services.embedding.provider.has_credentials", return_value=True),
            patch("services.embedding.provider.embed_content_url", return_value="https://example.test/embed"),
            patch("services.embedding.provider.build_headers", new=AsyncMock(return_value={})),
            patch("services.embedding.aiohttp.ClientSession", side_effect=fake_session),
        ):
            first = await embedding.get_text_embedding("İplik   Kılavuzu")
            second = await embedding.get_text_embedding("iplik kılavuzu")

        self.assertEqual(first, vector)
        self.assertEqual(second, vector)
        self.assertEqual(len(calls), 1)

    async def test_concurrent_identical_embeddings_share_one_upstream_request(self):
        vector = [0.3] * 3072
        responses = [_FakeResponse(200, payload=_vertex_payload(vector))]
        calls: list[dict] = []

        def fake_session(*_args, **_kwargs):
            return _FakeSession(responses, calls)

        with (
            patch("services.embedding.provider.has_credentials", return_value=True),
            patch("services.embedding.provider.embed_content_url", return_value="https://example.test/embed"),
            patch("services.embedding.provider.build_headers", new=AsyncMock(return_value={})),
            patch("services.embedding.aiohttp.ClientSession", side_effect=fake_session),
        ):
            first, second = await asyncio.gather(
                embedding.get_text_embedding("M5 L=8 vida"),
                embedding.get_text_embedding("m5  l=8   vida"),
            )

        self.assertEqual(first, vector)
        self.assertEqual(second, vector)
        self.assertEqual(len(calls), 1)


if __name__ == "__main__":
    unittest.main()
