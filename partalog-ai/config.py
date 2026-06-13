"""
Partalog AI Service - Configuration (Final v2.1)
"""

from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field, AliasChoices
from pathlib import Path
import os
from dotenv import load_dotenv
import logging

# 1. .env dosyasının KESİN yolunu bul
BASE_DIR = Path(__file__).resolve().parent
ENV_PATH = BASE_DIR / ".env"

# 2. .env içeriğini yükle ama gerçek runtime env'lerini ezme.
# Cloud Run/Secret Manager değerleri local .env'nin üstünde kalmalı.
load_dotenv(ENV_PATH, override=False)

def _clean_env(value: str) -> str:
    return value.strip().strip('"').strip("'").strip()

class Settings(BaseSettings):
    # GENEL
    APP_NAME: str = "Partalog AI Service"
    APP_VERSION: str = "2.1.0"
    DEBUG: bool = Field(default=True)
    
    # SUNUCU
    HOST: str = Field(default="0.0.0.0")
    PORT: int = Field(default=8000)
    STARTUP_SKIP_MODEL_LOADING: bool = Field(default=False)
    FAIL_STARTUP_ON_UNREADY: bool = Field(default=False)
    ENABLE_HOTSPOT_ENDPOINTS: bool = Field(default=True)
    ENABLE_CATALOG_PROCESSING_ENDPOINTS: bool = Field(default=True)
    
    # YOLLAR
    BASE_DIR: Path = BASE_DIR
    MODELS_DIR: Path = Field(default=Path("models"))
    
    # --- GENAI PROVIDER ---
    GENAI_PROVIDER: str = Field(default="legacy")
    GOOGLE_CLOUD_PROJECT: str = Field(default="")
    GOOGLE_CLOUD_LOCATION: str = Field(default="global")
    VERTEX_API_KEY: str = Field(default="")
    GEMINI_CHAT_MODEL: str = Field(default="gemini-2.5-flash")
    GEMINI_STREAM_MODEL: str = Field(default="gemini-2.5-flash")
    GEMINI_TABLE_MODEL: str = Field(default="gemini-2.5-flash")
    GEMINI_ANALYSIS_MODEL: str = Field(default="gemini-2.5-flash")
    GEMINI_EMBEDDING_MODEL: str = Field(default="gemini-embedding-001")

    # --- GEMINI AI ---
    # Legacy key yolu local/dev fallback olarak tutuluyor.
    GEMINI_API_KEY: str = Field(
        default="",
        validation_alias=AliasChoices("GEMINI_API_KEY", "GOOGLE_API_KEY")
    )
    GEMINI_VISUAL_MODEL: str = Field(default="gemini-3-pro-preview")

    # --- VERİTABANI (YENİ EKLENDİ) ---
    # train_dictionary.py artık şifreyi buradan okuyacak.
    # Varsayılan değer boş, .env dosyasından gelmeli.
    DB_CONNECTION_STRING: str = Field(
        default="",
        validation_alias=AliasChoices("DB_CONNECTION_STRING", "DATABASE_URL")
    )
    DB_POOL_MIN_SIZE: int = Field(default=2)
    DB_POOL_MAX_SIZE: int = Field(default=10)
    DB_POOL_COMMAND_TIMEOUT_SECONDS: float = Field(default=15.0)
    DB_POOL_HEALTHCHECK_TIMEOUT_SECONDS: float = Field(default=3.0)
    DB_POOL_MAX_INACTIVE_CONNECTION_LIFETIME_SECONDS: float = Field(default=300.0)
    DB_STATEMENT_CACHE_SIZE: int = Field(default=0)
    DB_ALLOW_EPHEMERAL_FALLBACK: bool = Field(default=True)
    HEALTH_READY_DB_PING_SLO_MS: float = Field(default=250.0)

    @property
    def db_dsn(self) -> str:
        """asyncpg için temiz DSN döner. postgresql+asyncpg:// → postgresql:// dönüşümü yapar."""
        raw = self.DB_CONNECTION_STRING
        if not raw:
            return ""
        # SQLAlchemy prefix'ini asyncpg için temizle
        raw = raw.replace("postgresql+asyncpg://", "postgresql://")
        raw = raw.replace("postgres://", "postgresql://")
        return raw.strip().strip('"').strip("'")

    # YOLO
    YOLO_MODEL_PATH: str = Field(default="models/best.pt")
    YOLO_CONFIDENCE: float = Field(default=0.25)
    YOLO_IMG_SIZE: int = Field(default=1280)
    
    # OCR (EasyOCR - Hotspot için)
    OCR_USE_GPU: bool = Field(default=False)
    
    # PADDLEOCR (Yedek)
    PADDLE_USE_GPU: bool = Field(default=False)
    PADDLE_LANG: str = Field(default="en")
    PADDLE_TABLE_MAX_LEN: int = Field(default=800)
    PADDLE_SHOW_LOG: bool = Field(default=False)

    # ==========================================
    # 🗂️ STORAGE AYARLARI (LOCAL / S3 COMPAT)
    # ==========================================
    STORAGE_PROVIDER: str = Field(default="local")  # local | s3
    STORAGE_BUCKET: str = Field(default="partalog-visuals")
    STORAGE_BASE_URL: str = Field(default="https://storage.googleapis.com/partalog-visuals")
    STORAGE_LOCAL_DIR: str = Field(default="static/visual-parts")

    # S3 / GCS Interoperability
    STORAGE_S3_ENDPOINT: str = Field(default="https://storage.googleapis.com")
    STORAGE_ACCESS_KEY: str = Field(default="")
    STORAGE_SECRET_KEY: str = Field(default="")
    STORAGE_REGION: str = Field(default="auto")

    # RATE LIMIT / OPERATIONAL TESTING
    RATE_LIMIT_HEADERS_ENABLED: bool = Field(default=True)
    RATE_LIMIT_PROBE_LIMIT: str = Field(default="5/second")
    RATE_LIMIT_PROBE_WINDOW_SECONDS: float = Field(default=1.0)
    CHAT_HTTP_RATE_LIMIT_ENABLED: bool = Field(default=False)
    DEV_AI_QUOTA_BYPASS: bool = Field(default=False)
    AI_CHAT_CAPACITY_PROVIDER: str = Field(default="inmemory")
    AI_CHAT_GLOBAL_CONCURRENCY: int = Field(default=80)
    AI_CHAT_ACQUIRE_TIMEOUT_SECONDS: float = Field(default=0.15)
    AI_CHAT_USE_DISTRIBUTED_LEASES: bool = Field(default=False)
    AI_CHAT_DISTRIBUTED_LEASE_TTL_SECONDS: int = Field(default=180)
    AI_CHAT_DISTRIBUTED_POOL_NAME: str = Field(default="python-chat")
    AI_CHAT_REDIS_URL: str = Field(default="redis://localhost:6379/0")
    AI_CHAT_REDIS_KEY_PREFIX: str = Field(default="partalog:ai-capacity")
    AI_CHAT_BUSY_MESSAGE: str = Field(default="AI kapasitesi şu an dolu. Lütfen birkaç saniye sonra tekrar deneyin.")
    GEMINI_CHAT_TIMEOUT_SECONDS: float = Field(default=35.0)
    GEMINI_STREAM_TIMEOUT_SECONDS: float = Field(default=75.0)
    GEMINI_STREAM_SOCK_READ_TIMEOUT_SECONDS: float = Field(default=30.0)

    # Config Ayarları
    model_config = SettingsConfigDict(
        env_file=str(ENV_PATH), # Sadece ".env" yerine tam yolu veriyoruz
        env_file_encoding="utf-8",
        extra="ignore" # Bilinmeyen değişkenleri hata vermeden geç
    )

    def ensure_directories(self):
        self.MODELS_DIR.mkdir(parents=True, exist_ok=True)

    # 🔒 TIRNAK TEMİZLEME
    def clean_env_values(self):
        # API ve model
        if self.GEMINI_API_KEY:
            self.GEMINI_API_KEY = _clean_env(self.GEMINI_API_KEY)
        if self.GENAI_PROVIDER:
            self.GENAI_PROVIDER = _clean_env(self.GENAI_PROVIDER).lower()
        if self.GOOGLE_CLOUD_PROJECT:
            self.GOOGLE_CLOUD_PROJECT = _clean_env(self.GOOGLE_CLOUD_PROJECT)
        if self.GOOGLE_CLOUD_LOCATION:
            self.GOOGLE_CLOUD_LOCATION = _clean_env(self.GOOGLE_CLOUD_LOCATION)
        if self.VERTEX_API_KEY:
            self.VERTEX_API_KEY = _clean_env(self.VERTEX_API_KEY)
        if self.GEMINI_CHAT_MODEL:
            self.GEMINI_CHAT_MODEL = _clean_env(self.GEMINI_CHAT_MODEL)
        if self.GEMINI_STREAM_MODEL:
            self.GEMINI_STREAM_MODEL = _clean_env(self.GEMINI_STREAM_MODEL)
        if self.GEMINI_TABLE_MODEL:
            self.GEMINI_TABLE_MODEL = _clean_env(self.GEMINI_TABLE_MODEL)
        if self.GEMINI_ANALYSIS_MODEL:
            self.GEMINI_ANALYSIS_MODEL = _clean_env(self.GEMINI_ANALYSIS_MODEL)
        if self.GEMINI_EMBEDDING_MODEL:
            self.GEMINI_EMBEDDING_MODEL = _clean_env(self.GEMINI_EMBEDDING_MODEL)
        if self.GEMINI_VISUAL_MODEL:
            self.GEMINI_VISUAL_MODEL = _clean_env(self.GEMINI_VISUAL_MODEL)

        # DB
        if self.DB_CONNECTION_STRING:
            self.DB_CONNECTION_STRING = _clean_env(self.DB_CONNECTION_STRING)

        # Storage
        if self.STORAGE_PROVIDER:
            self.STORAGE_PROVIDER = _clean_env(self.STORAGE_PROVIDER)
        if self.STORAGE_BUCKET:
            self.STORAGE_BUCKET = _clean_env(self.STORAGE_BUCKET)
        if self.STORAGE_BASE_URL:
            self.STORAGE_BASE_URL = _clean_env(self.STORAGE_BASE_URL)
        if self.STORAGE_LOCAL_DIR:
            self.STORAGE_LOCAL_DIR = _clean_env(self.STORAGE_LOCAL_DIR)
        if self.STORAGE_S3_ENDPOINT:
            self.STORAGE_S3_ENDPOINT = _clean_env(self.STORAGE_S3_ENDPOINT)
        if self.STORAGE_ACCESS_KEY:
            self.STORAGE_ACCESS_KEY = _clean_env(self.STORAGE_ACCESS_KEY)
        if self.STORAGE_SECRET_KEY:
            self.STORAGE_SECRET_KEY = _clean_env(self.STORAGE_SECRET_KEY)
        if self.STORAGE_REGION:
            self.STORAGE_REGION = _clean_env(self.STORAGE_REGION)
        if self.RATE_LIMIT_PROBE_LIMIT:
            self.RATE_LIMIT_PROBE_LIMIT = _clean_env(self.RATE_LIMIT_PROBE_LIMIT)
        if self.AI_CHAT_CAPACITY_PROVIDER:
            self.AI_CHAT_CAPACITY_PROVIDER = _clean_env(self.AI_CHAT_CAPACITY_PROVIDER).lower()
        if self.AI_CHAT_REDIS_URL:
            self.AI_CHAT_REDIS_URL = _clean_env(self.AI_CHAT_REDIS_URL)
        if self.AI_CHAT_REDIS_KEY_PREFIX:
            self.AI_CHAT_REDIS_KEY_PREFIX = _clean_env(self.AI_CHAT_REDIS_KEY_PREFIX)
        if self.AI_CHAT_BUSY_MESSAGE:
            self.AI_CHAT_BUSY_MESSAGE = _clean_env(self.AI_CHAT_BUSY_MESSAGE)


settings = Settings()
settings.ensure_directories()
settings.clean_env_values()

logger = logging.getLogger("partalog.config")
def _mask_key(value: str) -> str:
    if not value:
        return "<empty>"
    if len(value) <= 8:
        return f"{value[:2]}...{value[-2:]}"
    return f"{value[:4]}...{value[-4:]}"

if settings.GEMINI_API_KEY:
    logger.info(f"🔐 GEMINI_API_KEY loaded: {_mask_key(settings.GEMINI_API_KEY)} (len={len(settings.GEMINI_API_KEY)})")
elif settings.GENAI_PROVIDER != "vertex":
    logger.warning("⚠️ GEMINI_API_KEY not loaded (empty). Legacy provider won't work without a key.")

logger.info(
    "🧭 GENAI provider={} project={} location={}",
    settings.GENAI_PROVIDER,
    settings.GOOGLE_CLOUD_PROJECT or "<empty>",
    settings.GOOGLE_CLOUD_LOCATION or "<empty>",
)
