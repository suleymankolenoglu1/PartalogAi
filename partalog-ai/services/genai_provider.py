import asyncio
import time
from typing import Optional
from copy import deepcopy

from loguru import logger

from config import settings

_CLOUD_PLATFORM_SCOPE = "https://www.googleapis.com/auth/cloud-platform"


def _clean_secret(value: str) -> str:
    return value.strip().strip('"').strip("'").strip() if value else ""


def _mask_secret(value: str) -> str:
    if not value:
        return "<empty>"
    if len(value) <= 8:
        return f"{value[:2]}...{value[-2:]}"
    return f"{value[:4]}...{value[-4:]}"


class GenAiProvider:
    def __init__(self) -> None:
        self._token: str = ""
        self._token_expires_at: float = 0.0
        self._token_lock = asyncio.Lock()
        self._auth_logged = False

    @property
    def use_vertex(self) -> bool:
        return settings.GENAI_PROVIDER == "vertex"

    @property
    def has_legacy_key(self) -> bool:
        return bool(_clean_secret(settings.GEMINI_API_KEY))

    @property
    def has_vertex_api_key(self) -> bool:
        return bool(_clean_secret(settings.VERTEX_API_KEY))

    def has_credentials(self) -> bool:
        if self.use_vertex:
            return bool(settings.GOOGLE_CLOUD_PROJECT)
        return self.has_legacy_key

    def _log_auth_once(self, mode: str, detail: str) -> None:
        if self._auth_logged:
            return
        logger.info("🔐 GenAI auth mode={} detail={}", mode, detail)
        self._auth_logged = True

    async def build_headers(self) -> dict[str, str]:
        if self.use_vertex:
            if self.has_vertex_api_key:
                key = _clean_secret(settings.VERTEX_API_KEY)
                self._log_auth_once("vertex-api-key", _mask_secret(key))
                return {
                    "Content-Type": "application/json",
                    "x-goog-api-key": key,
                }

            token = await self._get_adc_access_token()
            self._log_auth_once("vertex-adc", "service-account/adc")
            return {
                "Content-Type": "application/json",
                "Authorization": f"Bearer {token}",
            }

        key = _clean_secret(settings.GEMINI_API_KEY)
        self._log_auth_once("legacy-api-key", _mask_secret(key))
        return {
            "Content-Type": "application/json",
            "x-goog-api-key": key,
        }

    async def _get_adc_access_token(self) -> str:
        async with self._token_lock:
            now = time.time()
            if self._token and now < self._token_expires_at - 60:
                return self._token

            token, expires_at = await asyncio.to_thread(self._refresh_adc_access_token)
            self._token = token
            self._token_expires_at = expires_at
            return token

    @staticmethod
    def _refresh_adc_access_token() -> tuple[str, float]:
        try:
            import google.auth
            import google.auth.transport.requests
        except ImportError as exc:
            raise RuntimeError(
                "Vertex ADC authentication için google-auth paketi gerekli."
            ) from exc

        credentials, _ = google.auth.default(scopes=[_CLOUD_PLATFORM_SCOPE])
        request = google.auth.transport.requests.Request()
        credentials.refresh(request)
        expiry = getattr(credentials, "expiry", None)
        expires_at = expiry.timestamp() if expiry else time.time() + 3000
        if not credentials.token:
            raise RuntimeError("ADC access token alinamadi.")
        return credentials.token, expires_at

    def generate_content_url(self, model_name: str, stream: bool = False) -> str:
        if self.use_vertex:
            project = settings.GOOGLE_CLOUD_PROJECT
            location = settings.GOOGLE_CLOUD_LOCATION or "global"
            if not project:
                raise RuntimeError("Vertex için GOOGLE_CLOUD_PROJECT zorunlu.")
            method = "streamGenerateContent" if stream else "generateContent"
            suffix = "?alt=sse" if stream else ""
            return (
                "https://aiplatform.googleapis.com/v1/projects/"
                f"{project}/locations/{location}/publishers/google/models/{model_name}:{method}{suffix}"
            )

        api_key = _clean_secret(settings.GEMINI_API_KEY)
        if not api_key:
            return ""
        method = "streamGenerateContent" if stream else "generateContent"
        suffix = "?alt=sse" if stream else ""
        return (
            "https://generativelanguage.googleapis.com/v1beta/models/"
            f"{model_name}:{method}{suffix}"
            if stream
            else "https://generativelanguage.googleapis.com/v1beta/models/"
            f"{model_name}:{method}"
        )

    def embed_content_url(self, model_name: str) -> str:
        if self.use_vertex:
            project = settings.GOOGLE_CLOUD_PROJECT
            # Vertex text embedding models use the Text Embeddings API in a regional
            # endpoint. Generative Gemini calls can use global, but embeddings 404
            # there for gemini-embedding-001.
            location = "us-central1"
            if not project:
                raise RuntimeError("Vertex için GOOGLE_CLOUD_PROJECT zorunlu.")
            return (
                f"https://{location}-aiplatform.googleapis.com/v1/projects/"
                f"{project}/locations/{location}/publishers/google/models/{model_name}:predict"
            )

        api_key = _clean_secret(settings.GEMINI_API_KEY)
        if not api_key:
            return ""
        return (
            "https://generativelanguage.googleapis.com/v1beta/models/"
            f"{model_name}:embedContent"
        )

    def normalize_generate_payload(self, payload: dict) -> dict:
        if not self.use_vertex:
            return payload

        normalized = deepcopy(payload)

        contents = normalized.get("contents")
        if isinstance(contents, list):
            fixed_contents = []
            for content in contents:
                if not isinstance(content, dict):
                    fixed_contents.append(content)
                    continue

                fixed_content = dict(content)
                fixed_content["role"] = fixed_content.get("role") or "user"

                parts = fixed_content.get("parts")
                if isinstance(parts, list):
                    fixed_parts = []
                    for part in parts:
                        if not isinstance(part, dict):
                            fixed_parts.append(part)
                            continue

                        fixed_part = dict(part)
                        inline_data = fixed_part.pop("inline_data", None)
                        if isinstance(inline_data, dict):
                            fixed_part["inlineData"] = {
                                "mimeType": inline_data.get("mime_type"),
                                "data": inline_data.get("data"),
                            }
                        fixed_parts.append(fixed_part)
                    fixed_content["parts"] = fixed_parts

                fixed_contents.append(fixed_content)

            normalized["contents"] = fixed_contents

        generation_config = normalized.get("generationConfig")
        if isinstance(generation_config, dict):
            if "response_mime_type" in generation_config:
                generation_config["responseMimeType"] = generation_config.pop("response_mime_type")

        return normalized


provider = GenAiProvider()
