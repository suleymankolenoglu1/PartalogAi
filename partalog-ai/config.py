"""
Partalog AI Service - Configuration (Final v2.1)
"""

import os
from urllib.parse import quote, urlencode

# Pydantic plugin discovery can be very slow in some local virtualenvs because it
# scans installed package metadata on import. The service does not use pydantic
# plugins, so disable discovery before importing pydantic/pydantic-settings.
os.environ.setdefault("PYDANTIC_DISABLE_PLUGINS", "1")

from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import Field, AliasChoices
from pathlib import Path
from dotenv import load_dotenv
import logging

# 1. .env dosyasının KESİN yolunu bul
BASE_DIR = Path(__file__).resolve().parent
ENV_PATH = BASE_DIR / ".env"

# 2. .env içeriğini yükle ama gerçek environment değerlerini ezme.
# Cloud Run, CI ve batch script'lerinde açıkça verilen env var'lar .env'den
# daha güvenilir olmalı; aksi halde staging/production komutları local .env'ye
# yanlışlıkla bağlanabilir.
load_dotenv(ENV_PATH, override=False)

def _clean_env(value: str) -> str:
    return value.strip().strip('"').strip("'").strip()


def _parse_npgsql_connection(raw_value: str) -> dict[str, str]:
    raw = _clean_env(raw_value)
    if not raw or "://" in raw or "=" not in raw or ";" not in raw:
        return {}

    parts: dict[str, str] = {}
    for segment in raw.split(";"):
        if not segment.strip() or "=" not in segment:
            continue
        key, value = segment.split("=", 1)
        normalized_key = key.strip().lower().replace(" ", "")
        parts[normalized_key] = value.strip()
    return parts


def _normalize_db_dsn(raw_value: str) -> str:
    raw = _clean_env(raw_value)
    if not raw:
        return ""

    raw = raw.replace("postgresql+asyncpg://", "postgresql://")
    raw = raw.replace("postgres://", "postgresql://")
    if "://" in raw:
        return raw

    # Accept the Npgsql/.NET connection string already used by the API so both
    # Cloud Run services can safely reference the same Secret Manager secret.
    if "=" not in raw or ";" not in raw:
        return raw

    parts = _parse_npgsql_connection(raw)

    host = parts.get("host") or parts.get("server") or parts.get("datasource") or ""
    database = parts.get("database") or parts.get("initialcatalog") or ""
    username = parts.get("username") or parts.get("userid") or parts.get("user") or ""
    password = parts.get("password") or ""
    port = parts.get("port") or ""
    if not host or not database or not username:
        return raw

    credentials = f"{quote(username, safe='')}:{quote(password, safe='')}"
    database_path = quote(database, safe="")
    if host.startswith("/"):
        return f"postgresql://{credentials}@/{database_path}?{urlencode({'host': host})}"

    host_part = host if not port else f"{host}:{port}"
    return f"postgresql://{credentials}@{host_part}/{database_path}"


def _asyncpg_connect_kwargs(raw_value: str) -> dict[str, object]:
    parts = _parse_npgsql_connection(raw_value)
    if not parts:
        dsn = _normalize_db_dsn(raw_value)
        return {"dsn": dsn} if dsn else {}

    host = parts.get("host") or parts.get("server") or parts.get("datasource") or ""
    database = parts.get("database") or parts.get("initialcatalog") or ""
    username = parts.get("username") or parts.get("userid") or parts.get("user") or ""
    password = parts.get("password") or ""
    if not host or not database or not username:
        dsn = _normalize_db_dsn(raw_value)
        return {"dsn": dsn} if dsn else {}

    kwargs: dict[str, object] = {
        "host": host,
        "database": database,
        "user": username,
        "password": password,
    }
    port = parts.get("port")
    if port:
        kwargs["port"] = int(port)
    return kwargs

class Settings(BaseSettings):
    # GENEL
    APP_NAME: str = "Partalog AI Service"
    APP_VERSION: str = "2.1.0"
    DEBUG: bool = Field(default=True)
    DEV_AI_QUOTA_BYPASS: bool = Field(default=False)
    
    # SUNUCU
    HOST: str = Field(default="0.0.0.0")
    PORT: int = Field(default=8000)
    STARTUP_SKIP_MODEL_LOADING: bool = Field(default=False)
    ENABLE_HOTSPOT_ENDPOINTS: bool = Field(default=True)
    ENABLE_CATALOG_PROCESSING_ENDPOINTS: bool = Field(default=True)
    
    # YOLLAR
    BASE_DIR: Path = BASE_DIR
    MODELS_DIR: Path = Field(default=Path("models"))
    
    # --- GEMINI AI ---
    # Hem 'GEMINI_API_KEY' hem de 'GOOGLE_API_KEY' olarak gelsen kabul etsin.
    # .env dosyasında hangisi varsa onu alır.
    GEMINI_API_KEY: str = Field(
        default="",
        validation_alias=AliasChoices("GEMINI_API_KEY", "GOOGLE_API_KEY")
    )
    GENAI_PROVIDER: str = Field(default="legacy")  # legacy | vertex
    VERTEX_API_KEY: str = Field(default="")
    GOOGLE_CLOUD_PROJECT: str = Field(default="")
    GOOGLE_CLOUD_LOCATION: str = Field(default="global")
    GEMINI_CHAT_MODEL: str = Field(default="gemini-2.5-flash-lite")
    GEMINI_ANALYSIS_MODEL: str = Field(default="gemini-2.5-flash-lite")
    GEMINI_EMBEDDING_MODEL: str = Field(default="gemini-embedding-001")
    GEMINI_VISUAL_MODEL: str = Field(default="gemini-3-pro-preview")
    GENAI_REQUEST_TIMEOUT_SECONDS: float = Field(default=30.0)
    GENAI_STREAM_TIMEOUT_SECONDS: float = Field(default=90.0)
    GENAI_RETRY_ATTEMPTS: int = Field(default=2)
    GENAI_RETRY_BASE_DELAY_SECONDS: float = Field(default=0.5)
    GENAI_RETRY_MAX_DELAY_SECONDS: float = Field(default=8.0)
    GENAI_EMBEDDING_CACHE_TTL_SECONDS: int = Field(default=900)
    GENAI_EMBEDDING_CACHE_MAX_ITEMS: int = Field(default=1000)

    # --- CHAT CAPACITY / RATE SAFETY ---
    AI_CHAT_GLOBAL_CONCURRENCY: int = Field(default=100)
    AI_CHAT_ACQUIRE_TIMEOUT_SECONDS: float = Field(default=0.5)
    AI_CHAT_CAPACITY_PROVIDER: str = Field(default="inmemory")  # inmemory | redis
    AI_CHAT_USE_DISTRIBUTED_LEASES: bool = Field(default=False)
    AI_CHAT_REDIS_URL: str = Field(default="")
    AI_CHAT_REDIS_KEY_PREFIX: str = Field(default="partalog:ai-capacity")
    AI_CHAT_DISTRIBUTED_LEASE_TTL_SECONDS: int = Field(default=180)
    AI_CHAT_DISTRIBUTED_POOL_NAME: str = Field(default="python-chat")
    DB_STATEMENT_CACHE_SIZE: int = Field(default=0)
    DB_POOL_MIN_SIZE: int = Field(default=2)
    DB_POOL_MAX_SIZE: int = Field(default=10)
    DB_POOL_COMMAND_TIMEOUT_SECONDS: float = Field(default=15.0)
    DB_POOL_HEALTHCHECK_TIMEOUT_SECONDS: float = Field(default=3.0)
    DB_POOL_MAX_INACTIVE_CONNECTION_LIFETIME_SECONDS: float = Field(default=300.0)
    DB_ALLOW_EPHEMERAL_FALLBACK: bool = Field(default=True)

    # --- VERİTABANI (YENİ EKLENDİ) ---
    # train_dictionary.py artık şifreyi buradan okuyacak.
    # Varsayılan değer boş, .env dosyasından gelmeli.
    DB_CONNECTION_STRING: str = Field(
        default="",
        validation_alias=AliasChoices("DB_CONNECTION_STRING", "DATABASE_URL")
    )

    @property
    def db_dsn(self) -> str:
        """asyncpg için URI veya Npgsql connection string'den temiz DSN döner."""
        return _normalize_db_dsn(self.DB_CONNECTION_STRING)

    @property
    def db_connect_kwargs(self) -> dict[str, object]:
        """asyncpg için explicit connection kwargs döner; Cloud SQL socket'i URI parsing'e bırakmaz."""
        return _asyncpg_connect_kwargs(self.DB_CONNECTION_STRING)

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
        if self.VERTEX_API_KEY:
            self.VERTEX_API_KEY = _clean_env(self.VERTEX_API_KEY)
        if self.GOOGLE_CLOUD_PROJECT:
            self.GOOGLE_CLOUD_PROJECT = _clean_env(self.GOOGLE_CLOUD_PROJECT)
        if self.GOOGLE_CLOUD_LOCATION:
            self.GOOGLE_CLOUD_LOCATION = _clean_env(self.GOOGLE_CLOUD_LOCATION)
        if self.GEMINI_CHAT_MODEL:
            self.GEMINI_CHAT_MODEL = _clean_env(self.GEMINI_CHAT_MODEL)
        if self.GEMINI_ANALYSIS_MODEL:
            self.GEMINI_ANALYSIS_MODEL = _clean_env(self.GEMINI_ANALYSIS_MODEL)
        if self.GEMINI_EMBEDDING_MODEL:
            self.GEMINI_EMBEDDING_MODEL = _clean_env(self.GEMINI_EMBEDDING_MODEL)
        if self.GEMINI_VISUAL_MODEL:
            self.GEMINI_VISUAL_MODEL = _clean_env(self.GEMINI_VISUAL_MODEL)
        if self.AI_CHAT_CAPACITY_PROVIDER:
            self.AI_CHAT_CAPACITY_PROVIDER = _clean_env(self.AI_CHAT_CAPACITY_PROVIDER).lower()
        if self.AI_CHAT_REDIS_URL:
            self.AI_CHAT_REDIS_URL = _clean_env(self.AI_CHAT_REDIS_URL)
        if self.AI_CHAT_REDIS_KEY_PREFIX:
            self.AI_CHAT_REDIS_KEY_PREFIX = _clean_env(self.AI_CHAT_REDIS_KEY_PREFIX)
        if self.AI_CHAT_DISTRIBUTED_POOL_NAME:
            self.AI_CHAT_DISTRIBUTED_POOL_NAME = _clean_env(self.AI_CHAT_DISTRIBUTED_POOL_NAME)

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
else:
    logger.warning("⚠️ GEMINI_API_KEY not loaded (empty). Check partalog-ai/.env")
