"""
Partalog AI Service - Ana Uygulama (Final v3.0 - Turkish Native & 3072 Vector)
Görevi: C# Backend için Zeka Servislerini (YOLO, OCR, Gemini, Embedding) sunmak.
"""

# --- 1. Standart Kütüphaneler ---
from fastapi import FastAPI, HTTPException, Request, Response
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
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
from services.ai_capacity import check_ai_capacity_health, get_ai_capacity_snapshot
from services.vector_db import check_db_pool_health, close_db_pool, get_db_pool_state, init_db_pool

# --- 4. API Routerları (Uç Noktalar) ---
# Chat runtime keeps catalog-processing modules optional so OCR/YOLO dependencies
# are not imported unless that profile is explicitly enabled.
from api.chat import router as chat_router         # Chatbot (Türkçe & 3072 Uyumlu)

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


def _build_service_state() -> dict:
    return {
        "status": "starting",
        "started_at": None,
        "startup_completed_at": None,
        "ready": False,
        "components": {
            "yolo": {"status": "pending"},
            "ocr": {"status": "pending"},
            "db": get_db_pool_state(),
            "capacity": {"status": "pending"},
        },
    }


def _set_component_status(app: FastAPI, name: str, status: str, details: dict | None = None) -> None:
    component = {"status": status}
    if details:
        component.update(details)
    app.state.service_state["components"][name] = component


async def _refresh_service_readiness(app: FastAPI) -> dict:
    db_state = await check_db_pool_health()
    app.state.service_state["components"]["db"] = db_state
    capacity_health = await check_ai_capacity_health()
    app.state.service_state["components"]["capacity"] = {
        "status": "ready" if capacity_health.get("ready") else "failed",
        **capacity_health,
    }
    db_ready = bool(db_state.get("ready"))
    capacity_ready = bool(capacity_health.get("ready"))
    app.state.service_state["ready"] = db_ready and capacity_ready
    app.state.service_state["status"] = "ready" if db_ready and capacity_ready else "degraded"
    return app.state.service_state

@asynccontextmanager
async def lifespan(app: FastAPI):
    app.state.service_state = _build_service_state()
    app.state.service_state["started_at"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    logger.info("=" * 60)
    logger.info(f"🚀 {settings.APP_NAME} v{settings.APP_VERSION} (Service Mode) BAŞLATILIYOR...")
    logger.info("=" * 60)
    
    # A. YOLO Hotspot Detector Yükle (Varsa)
    if settings.STARTUP_SKIP_MODEL_LOADING:
        _set_component_status(app, "yolo", "skipped", {"reason": "startup_skip_model_loading"})
        _set_component_status(app, "ocr", "skipped", {"reason": "startup_skip_model_loading"})
    elif os.path.exists(settings.YOLO_MODEL_PATH):
        try:
            from core.detector import HotspotDetector
            models["yolo"] = HotspotDetector(
                settings.YOLO_MODEL_PATH, 
                settings.YOLO_CONFIDENCE, 
                settings.YOLO_IMG_SIZE
            )
            logger.success(f"✅ YOLO Modeli Yüklendi: {settings.YOLO_MODEL_PATH}")
            _set_component_status(app, "yolo", "ready", {"path": settings.YOLO_MODEL_PATH})
        except Exception as e:
            logger.error(f"❌ YOLO Başlatılamadı: {e}")
            _set_component_status(app, "yolo", "failed", {"error": str(e)})
    else:
        logger.warning(f"⚠️ Model dosyası yok: {settings.YOLO_MODEL_PATH}")
        _set_component_status(app, "yolo", "missing", {"path": settings.YOLO_MODEL_PATH})
    
    # B. EasyOCR Yükle
    if not settings.STARTUP_SKIP_MODEL_LOADING:
        try:
            from core.ocr import HotspotOCR
            models["ocr"] = HotspotOCR(use_gpu=settings.OCR_USE_GPU)
            logger.success("✅ EasyOCR Motoru Hazır.")
            _set_component_status(app, "ocr", "ready", {"use_gpu": settings.OCR_USE_GPU})
        except Exception as e:
            logger.error(f"❌ EasyOCR Hatası: {e}")
            _set_component_status(app, "ocr", "failed", {"error": str(e)})
    
    logger.info(f"📍 Servis Yayında: http://{settings.HOST}:{settings.PORT}")
    
    # C. DB Connection Pool Başlat
    db_state = await init_db_pool()
    app.state.service_state["components"]["db"] = db_state
    await _refresh_service_readiness(app)
    app.state.service_state["startup_completed_at"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())

    if settings.FAIL_STARTUP_ON_UNREADY and not app.state.service_state["ready"]:
        raise RuntimeError(f"Service readiness failed during startup: {app.state.service_state['components']['db']}")
    
    yield
    # Kapanış
    logger.info("👋 Servis durduruluyor, modeller temizleniyor...")
    models.clear()
    await close_db_pool()
    app.state.service_state["ready"] = False
    app.state.service_state["status"] = "stopped"
    app.state.service_state["components"]["db"] = get_db_pool_state()

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
app.state.service_state = _build_service_state()

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
if settings.ENABLE_CATALOG_PROCESSING_ENDPOINTS:
    from api.analysis import router as analysis_router # Sayfa Sınıflandırma
    from api.table import router as table_router       # Gemini Tablo Okuma
    from api.ingestion import router as ingestion_router # Canonical ingestion contracts

    app.include_router(analysis_router, prefix="/api/analysis", tags=["1. Analiz"])
    app.include_router(table_router, prefix="/api/table", tags=["3. Tablo (Gemini Türkçe)"])
    app.include_router(ingestion_router, prefix="/api/v1/ingestion", tags=["5. Ingestion"])

if settings.ENABLE_HOTSPOT_ENDPOINTS:
    from api.hotspot import router as hotspot_router   # YOLO & OCR

    app.include_router(hotspot_router, prefix="/api/hotspot", tags=["2. Hotspot (YOLO)"])

app.include_router(chat_router, prefix="/api/chat", tags=["4. Chatbot"])

# =================================================================
# 👇 C# İÇİN YARDIMCI ENDPOINTLER
# =================================================================

class EmbeddingRequest(BaseModel):
    text: str

@app.post("/api/embed", tags=["6. Semantic Search (C# Helper)"])
@limiter.limit("30/minute")
async def generate_embedding_endpoint(request: Request, response: Response, req: EmbeddingRequest):
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
        "status": app.state.service_state.get("status", "unknown"),
        "ready": app.state.service_state.get("ready", False),
        "health": {
            "live": "/health/live",
            "ready": "/health/ready",
        },
    }


@app.get("/health/live", tags=["Health"])
async def liveness():
    return {
        "service": settings.APP_NAME,
        "version": settings.APP_VERSION,
        "status": "live",
        "uptime_state": app.state.service_state.get("status", "unknown"),
    }


@app.get("/health/ready", tags=["Health"])
async def readiness():
    state = await _refresh_service_readiness(app)
    payload = {
        "service": settings.APP_NAME,
        "version": settings.APP_VERSION,
        "status": state["status"],
        "ready": state["ready"],
        "started_at": state["started_at"],
        "startup_completed_at": state["startup_completed_at"],
        "components": state["components"],
        "capacity": get_ai_capacity_snapshot(),
        "slos": {
            "db_ping_ms_max": settings.HEALTH_READY_DB_PING_SLO_MS,
        },
    }
    status_code = 200 if state["ready"] else 503
    return JSONResponse(status_code=status_code, content=payload)


@app.get("/health/rate-limit-probe", tags=["Health"])
@limiter.limit(settings.RATE_LIMIT_PROBE_LIMIT)
async def rate_limit_probe(request: Request, response: Response):
    return {
        "ok": True,
        "limit": settings.RATE_LIMIT_PROBE_LIMIT,
        "window_seconds": settings.RATE_LIMIT_PROBE_WINDOW_SECONDS,
        "timestamp": time.time(),
    }

if __name__ == "__main__":
    uvicorn.run("main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
