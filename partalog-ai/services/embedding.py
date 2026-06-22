import aiohttp
import asyncio
import email.utils
import re
import time
import unicodedata
from loguru import logger
from config import settings
from services.genai_provider import provider


_cache: dict[str, tuple[float, list]] = {}
_cache_lock = asyncio.Lock()
_inflight: dict[str, asyncio.Task] = {}
_inflight_lock = asyncio.Lock()
_WHITESPACE_RE = re.compile(r"\s+")
_RETRYABLE_STATUSES = {408, 429, 500, 502, 503, 504}


def _cache_ttl_seconds() -> int:
    return max(0, int(settings.GENAI_EMBEDDING_CACHE_TTL_SECONDS or 0))


def _cache_max_items() -> int:
    return max(0, int(settings.GENAI_EMBEDDING_CACHE_MAX_ITEMS or 0))


def _cache_key(text: str) -> str:
    normalized = _WHITESPACE_RE.sub(" ", str(text or "").strip()).casefold()
    normalized = unicodedata.normalize("NFKD", normalized)
    normalized = "".join(ch for ch in normalized if not unicodedata.combining(ch))
    return unicodedata.normalize("NFKC", normalized)


async def _cache_get(key: str):
    if not key or _cache_ttl_seconds() <= 0:
        return None

    now = time.time()
    async with _cache_lock:
        item = _cache.get(key)
        if not item:
            return None
        ts, vec = item
        if now - ts > _cache_ttl_seconds():
            _cache.pop(key, None)
            return None
        return vec


async def _cache_set(key: str, vec: list):
    max_items = _cache_max_items()
    if not key or max_items <= 0:
        return

    async with _cache_lock:
        while len(_cache) >= max_items:
            oldest_key = min(_cache.items(), key=lambda x: x[1][0])[0]
            _cache.pop(oldest_key, None)
        _cache[key] = (time.time(), vec)


async def clear_embedding_cache() -> None:
    async with _cache_lock:
        _cache.clear()


async def get_embedding_cache_state() -> dict:
    async with _cache_lock:
        cache_size = len(_cache)
    async with _inflight_lock:
        inflight_size = len(_inflight)
    return {
        "cache_size": cache_size,
        "cache_ttl_seconds": _cache_ttl_seconds(),
        "cache_max_items": _cache_max_items(),
        "inflight": inflight_size,
    }


def _retry_attempts() -> int:
    return max(1, int(settings.GENAI_RETRY_ATTEMPTS or 1))


def _retry_base_delay_seconds() -> float:
    return max(0.0, float(settings.GENAI_RETRY_BASE_DELAY_SECONDS or 0.0))


def _retry_max_delay_seconds() -> float:
    return max(0.0, float(settings.GENAI_RETRY_MAX_DELAY_SECONDS or 0.0))


def _parse_retry_after(value: str | None) -> float | None:
    if not value:
        return None
    value = str(value).strip()
    if not value:
        return None
    try:
        return max(0.0, float(value))
    except ValueError:
        try:
            parsed = email.utils.parsedate_to_datetime(value)
        except (TypeError, ValueError):
            return None
        if not parsed:
            return None
        return max(0.0, parsed.timestamp() - time.time())


def _retry_delay(response: aiohttp.ClientResponse | None, attempt: int) -> float:
    retry_after = _parse_retry_after(response.headers.get("Retry-After") if response else None)
    fallback = _retry_base_delay_seconds() * attempt
    delay = retry_after if retry_after is not None else fallback
    max_delay = _retry_max_delay_seconds()
    return min(delay, max_delay) if max_delay > 0 else delay


def _build_payload(text: str, model_name: str) -> dict:
    if provider.use_vertex:
        return {
            "instances": [{"content": text, "task_type": "RETRIEVAL_QUERY"}],
            "parameters": {"autoTruncate": True, "outputDimensionality": 3072},
        }
    return {
        "model": f"models/{model_name}",
        "content": {"parts": [{"text": text}]},
    }


def _extract_vector(data: dict) -> list | None:
    if provider.use_vertex:
        return (
            data.get("predictions", [{}])[0]
            .get("embeddings", {})
            .get("values")
        )
    return data.get("embedding", {}).get("values")


async def _request_embedding(text: str, key: str) -> list | None:
    if not provider.has_credentials():
        logger.error("GenAI credentials/config eksik: embedding")
        return None

    model_name = settings.GEMINI_EMBEDDING_MODEL
    url = provider.embed_content_url(model_name)
    if not url:
        logger.error("GenAI embedding URL üretilemedi.")
        return None

    payload = _build_payload(text, model_name)
    attempts = _retry_attempts()

    try:
        headers = await provider.build_headers()
        timeout = aiohttp.ClientTimeout(
            total=max(float(settings.GENAI_REQUEST_TIMEOUT_SECONDS), 1.0),
            connect=5,
            sock_connect=5,
        )
        async with aiohttp.ClientSession(headers=headers, timeout=timeout) as session:
            for attempt in range(1, attempts + 1):
                async with session.post(url, json=payload) as response:
                    body_text = ""
                    if response.status == 200:
                        data = await response.json()
                        vector = _extract_vector(data)
                        if not vector:
                            logger.error("API boş vektör döndü.")
                            return None

                        vec_len = len(vector)
                        if vec_len < 768:
                            logger.warning(
                                "⚠️ Dikkat: Vektör boyutu beklenenden küçük geldi: {}",
                                vec_len,
                            )

                        await _cache_set(key, vector)
                        return vector

                    body_text = await response.text()
                    if response.status not in _RETRYABLE_STATUSES or attempt >= attempts:
                        logger.error("Gemini Embedding API Hatası: {}", body_text)
                        return None

                    delay = _retry_delay(response, attempt)
                    logger.warning(
                        "Gemini Embedding API retryable status={} attempt={}/{} delay={}s",
                        response.status,
                        attempt,
                        attempts,
                        round(delay, 3),
                    )

                if delay > 0:
                    await asyncio.sleep(delay)

    except Exception as e:
        logger.error("Embedding Bağlantı Hatası: {}", str(e))
        return None

    return None


async def _get_or_start_inflight(text: str, key: str) -> tuple[asyncio.Task, bool]:
    async with _inflight_lock:
        existing = _inflight.get(key)
        if existing and not existing.done():
            return existing, False

        task = asyncio.create_task(_request_embedding(text, key))
        _inflight[key] = task
        return task, True


async def _clear_inflight_if_current(key: str, task: asyncio.Task) -> None:
    async with _inflight_lock:
        if _inflight.get(key) is task:
            _inflight.pop(key, None)


async def get_text_embedding(text: str):
    """
    Verilen metni Google Gemini embedding modelini kullanarak vektöre çevirir.

    - Aynı query tekrarlarında kısa süreli process-local cache kullanır.
    - Aynı anda gelen aynı query'leri singleflight ile tek upstream çağrıya indirir.
    - Vertex/Gemini 429/5xx gibi geçici hatalarda bounded retry yapar.
    """
    cleaned_text = str(text or "").strip()
    if not cleaned_text:
        return None

    key = _cache_key(cleaned_text)
    cached = await _cache_get(key)
    if cached:
        return cached

    task, owner = await _get_or_start_inflight(cleaned_text, key)
    try:
        return await task
    finally:
        if owner:
            await _clear_inflight_if_current(key, task)
