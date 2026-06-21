"""
Partalog AI Service - Ana Uygulama (Final v3.0 - Turkish Native & 3072 Vector)
Görevi: C# Backend için Zeka Servislerini (YOLO, OCR, Gemini, Embedding) sunmak.
"""

# --- 1. Standart Kütüphaneler ---
from fastapi import FastAPI, HTTPException, Request, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from contextlib import asynccontextmanager
from loguru import logger
from pydantic import BaseModel
from slowapi import _rate_limit_exceeded_handler
from slowapi.errors import RateLimitExceeded
import sys
import os
import uvicorn
import time

# --- 2. Ayarlar ---
from config import settings

# --- 3. Rate Limiter ---
from core.rate_limiter import limiter

# --- 3. Servisler ---
# services/embedding.py -> Senin sisteminde 3072 boyutlu vektör üretiyor.
from services.embedding import get_text_embedding 
from services.vector_db import init_db_pool, close_db_pool
from services.ai_capacity import check_ai_capacity_health, get_ai_capacity_snapshot

# --- 4. API Routerları (Uç Noktalar) ---
# Chat image profilinde ağır YOLO/OCR ve katalog-processing modülleri import edilmez.
from api.chat import router as chat_router         # Chatbot (Türkçe & 3072 Uyumlu)

hotspot_router = None
table_router = None
analysis_router = None

if settings.ENABLE_HOTSPOT_ENDPOINTS:
    from api.hotspot import router as hotspot_router   # YOLO & OCR

if settings.ENABLE_CATALOG_PROCESSING_ENDPOINTS:
    from api.table import router as table_router       # Gemini Tablo Okuma
    from api.analysis import router as analysis_router # Sayfa Sınıflandırma

# --- 5. Gelişmiş Loglama Ayarı ---
logger.remove()
logger.add(
    sys.stdout,
    format="<green>{time:YYYY-MM-DD HH:mm:ss}</green> | <level>{level: <8}</level> | <cyan>{name}</cyan>:<cyan>{function}</cyan> - <level>{message}</level>",
    level="DEBUG" if settings.DEBUG else "INFO",
    colorize=True
)

# --- 6. Model Başlatma (Lifespan) ---
models = {}

@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("=" * 60)
    logger.info(f"🚀 {settings.APP_NAME} v{settings.APP_VERSION} (Service Mode) BAŞLATILIYOR...")
    logger.info("=" * 60)
    
    if settings.STARTUP_SKIP_MODEL_LOADING or not settings.ENABLE_HOTSPOT_ENDPOINTS:
        logger.info("⏭️ YOLO/OCR model loading skipped for this runtime profile.")
    else:
        # A. YOLO Hotspot Detector Yükle (Varsa)
        if os.path.exists(settings.YOLO_MODEL_PATH):
            try:
                from core.detector import HotspotDetector
                models["yolo"] = HotspotDetector(
                    settings.YOLO_MODEL_PATH,
                    settings.YOLO_CONFIDENCE,
                    settings.YOLO_IMG_SIZE
                )
                logger.success(f"✅ YOLO Modeli Yüklendi: {settings.YOLO_MODEL_PATH}")
            except Exception as e:
                logger.error(f"❌ YOLO Başlatılamadı: {e}")
        else:
            logger.warning(f"⚠️ Model dosyası yok: {settings.YOLO_MODEL_PATH}")

        # B. EasyOCR Yükle
        try:
            from core.ocr import HotspotOCR
            models["ocr"] = HotspotOCR(use_gpu=settings.OCR_USE_GPU)
            logger.success("✅ EasyOCR Motoru Hazır.")
        except Exception as e:
            logger.error(f"❌ EasyOCR Hatası: {e}")
    
    logger.info(f"📍 Servis Yayında: http://{settings.HOST}:{settings.PORT}")
    
    # C. DB Connection Pool Başlat
    await init_db_pool()
    
    yield
    # Kapanış
    logger.info("👋 Servis durduruluyor, modeller temizleniyor...")
    models.clear()
    await close_db_pool()

# --- 7. Uygulama Tanımı ---
app = FastAPI(
    title=settings.APP_NAME,
    version=settings.APP_VERSION,
    description="C# Backend için Yardımcı Zeka Servisi (3072 Vector Edition)",
    lifespan=lifespan
)

# --- 8. Rate Limiter ---
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)

# --- 9. CORS (Güvenlik İzinleri) ---
allowed_origins_raw = os.getenv("CORS_ALLOWED_ORIGINS", "http://localhost:4200")
allowed_origins = [o.strip() for o in allowed_origins_raw.split(",") if o.strip()]
if not allowed_origins:
    allowed_origins = ["http://localhost:4200"]
allow_credentials = "*" not in allowed_origins

app.add_middleware(
    CORSMiddleware,
    allow_origins=allowed_origins,
    allow_credentials=allow_credentials,
    allow_methods=["*"],
    allow_headers=["*"],
)

# --- 9. Statik Dosyalar ---
if os.path.exists("static"):
    app.mount("/static", StaticFiles(directory="static"), name="static")

# --- 10. Router Bağlantıları ---
if analysis_router is not None:
    app.include_router(analysis_router, prefix="/api/analysis", tags=["1. Analiz"])
if hotspot_router is not None:
    app.include_router(hotspot_router, prefix="/api/hotspot", tags=["2. Hotspot (YOLO)"])
if table_router is not None:
    app.include_router(table_router, prefix="/api/table", tags=["3. Tablo (Gemini Türkçe)"])
app.include_router(chat_router, prefix="/api/chat", tags=["4. Chatbot"])


def _db_readiness_snapshot() -> dict:
    dsn_configured = bool(settings.db_dsn)
    return {
        "ready": dsn_configured,
        "mode": "configured" if dsn_configured else "missing_dsn",
        "dsn_configured": dsn_configured,
    }


async def check_db_pool_health() -> dict:
    # Lightweight readiness surface. Real query failures are still surfaced by
    # vector_db search calls and by startup pool init logs. Tests can patch this.
    return _db_readiness_snapshot()


@app.get("/health/live", tags=["Health"])
async def health_live():
    return {"status": "live", "service": settings.APP_NAME}


@app.get("/health/ready", tags=["Health"])
async def health_ready():
    db = await check_db_pool_health()
    capacity = await check_ai_capacity_health()
    ready = bool(db.get("ready")) and bool(capacity.get("ready"))
    payload = {
        "status": "ready" if ready else "not_ready",
        "ready": ready,
        "components": {
            "db": db,
            "capacity": capacity,
        },
        "capacity": get_ai_capacity_snapshot(),
        "runtime": {
            "hotspotEndpointsEnabled": settings.ENABLE_HOTSPOT_ENDPOINTS,
            "catalogProcessingEndpointsEnabled": settings.ENABLE_CATALOG_PROCESSING_ENDPOINTS,
            "startupSkipModelLoading": settings.STARTUP_SKIP_MODEL_LOADING,
        },
    }
    return payload if ready else fastapi_json_response(payload, status.HTTP_503_SERVICE_UNAVAILABLE)


def fastapi_json_response(payload: dict, status_code: int):
    from fastapi.responses import JSONResponse

    return JSONResponse(status_code=status_code, content=payload)

# =================================================================
# 👇 C# İÇİN YARDIMCI ENDPOINTLER
# =================================================================

class EmbeddingRequest(BaseModel):
    text: str

@app.post("/api/embed", tags=["6. Semantic Search (C# Helper)"])
@limiter.limit("30/minute")
async def generate_embedding_endpoint(request: Request, req: EmbeddingRequest):
    """
    C# Backend bu endpoint'e metin gönderir.
    Python, Google API ile vektör döner.
    DİKKAT: Senin sisteminde bu model 3072 boyutlu çıktı veriyor.
    """
    start_time = time.time()
    if not req.text or len(req.text.strip()) < 2:
         raise HTTPException(status_code=400, detail="Metin çok kısa veya boş.")

    try:
        # services/embedding.py içindeki fonksiyonu çağır
        vector = await get_text_embedding(req.text)
        
        if not vector:
             raise HTTPException(status_code=500, detail="Vektör oluşturulamadı (Google API hatası).")

        process_time = round((time.time() - start_time) * 1000, 2)
        
        # Logda boyutu görelim ki için rahat etsin (3072 bekliyoruz)
        logger.info(f"🧠 Vektör oluşturuldu ({process_time}ms) Boyut: {len(vector)}")
        
        return {"embedding": vector}

    except Exception as e:
         logger.error(f"❌ Embedding Hatası: {e}")
         raise HTTPException(status_code=500, detail=str(e))

@app.get("/", tags=["Health"])
async def root():
    return {
        "service": settings.APP_NAME,
        "mode": "Service Mode (Native Turkish & 3072 Vector)",
        "status": "Active"
    }

if __name__ == "__main__":
    uvicorn.run("main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
