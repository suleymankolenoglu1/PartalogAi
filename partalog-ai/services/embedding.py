import aiohttp
import asyncio
import os
import time
from loguru import logger
from config import settings

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

    # 1. API Key Alma
    raw_api_key = getattr(settings, "GOOGLE_API_KEY", None) or \
                  os.getenv("GOOGLE_API_KEY") or \
                  getattr(settings, "GEMINI_API_KEY", None) or \
                  os.getenv("GEMINI_API_KEY")
    
    if not raw_api_key:
        logger.error("API Key bulunamadı!")
        return None

    api_key = raw_api_key.replace('"', '').replace("'", '').strip()

    # Model Adı
    model_name = "models/gemini-embedding-001"
    
    url = f"https://generativelanguage.googleapis.com/v1beta/{model_name}:embedContent?key={api_key}"
    
    # 2. PAYLOAD
    payload = {
        "model": model_name,
        "content": {"parts": [{"text": text}]}
    }
    
    try:
        async with aiohttp.ClientSession() as session:
            async with session.post(url, json=payload, headers={"Content-Type": "application/json"}) as response:
                if response.status != 200:
                    logger.error(f"Gemini Embedding API Hatası: {await response.text()}")
                    return None

                data = await response.json()
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
