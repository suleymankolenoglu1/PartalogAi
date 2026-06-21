import aiohttp
import asyncio
import time
from loguru import logger
from config import settings
from services.genai_provider import provider

# Simple in-memory cache to reduce duplicate embedding calls
_cache: dict[str, tuple[float, list]] = {}
_cache_lock = asyncio.Lock()
_CACHE_TTL_SECONDS = 300
_CACHE_MAX = 200


async def _cache_get(text: str):
    now = time.time()
    async with _cache_lock:
        item = _cache.get(text)
        if not item:
            return None
        ts, vec = item
        if now - ts > _CACHE_TTL_SECONDS:
            _cache.pop(text, None)
            return None
        return vec


async def _cache_set(text: str, vec: list):
    async with _cache_lock:
        if len(_cache) >= _CACHE_MAX:
            oldest_key = min(_cache.items(), key=lambda x: x[1][0])[0]
            _cache.pop(oldest_key, None)
        _cache[text] = (time.time(), vec)

async def get_text_embedding(text: str):
    """
    Verilen metni Google Gemini 'gemini-embedding-001' modelini kullanarak vektöre çevirir.
    Async (aiohttp) versiyonu — FastAPI event loop'unu bloke etmez.
    Veritabanı 3072 boyutuna güncellendiği için veri olduğu gibi (RAW) iletilir.
    """

    # 0. Cache
    if text:
        cached = await _cache_get(text)
        if cached:
            return cached

    if not provider.has_credentials():
        logger.error("GenAI credentials/config eksik: embedding")
        return None

    model_name = settings.GEMINI_EMBEDDING_MODEL

    url = provider.embed_content_url(model_name)
    if not url:
        logger.error("GenAI embedding URL üretilemedi.")
        return None

    # 2. PAYLOAD
    if provider.use_vertex:
        payload = {
            "instances": [{"content": text, "task_type": "RETRIEVAL_QUERY"}],
            "parameters": {"autoTruncate": True, "outputDimensionality": 3072},
        }
    else:
        payload = {
            "model": f"models/{model_name}",
            "content": {"parts": [{"text": text}]},
        }

    try:
        headers = await provider.build_headers()
        timeout = aiohttp.ClientTimeout(total=max(float(settings.GENAI_REQUEST_TIMEOUT_SECONDS), 1.0), connect=5, sock_connect=5)
        async with aiohttp.ClientSession(headers=headers, timeout=timeout) as session:
            async with session.post(url, json=payload) as response:
                if response.status != 200:
                    logger.error(f"Gemini Embedding API Hatası: {await response.text()}")
                    return None

                data = await response.json()
                if provider.use_vertex:
                    vector = (
                        data.get("predictions", [{}])[0]
                        .get("embeddings", {})
                        .get("values")
                    )
                else:
                    vector = data.get("embedding", {}).get("values")

                if not vector:
                    logger.error("API boş vektör döndü.")
                    return None

                # 3. KONTROL MEKANİZMASI (Sadece Loglama)
                vec_len = len(vector)

                if vec_len < 768:
                     logger.warning(f"⚠️ Dikkat: Vektör boyutu beklenenden küçük geldi: {vec_len}")

                # Veritabanı 3072 olduğu için, 3072 gelen veriyi olduğu gibi yolluyoruz.
                await _cache_set(text, vector)
                return vector

    except Exception as e:
        logger.error(f"Embedding Bağlantı Hatası: {str(e)}")
        return None
