"""Request normalization helpers for chat endpoints."""

import json
import re
from typing import Any


QUERY_TYPO_RULES: list[tuple[str, str]] = [
    (r"\byamaot\b", "yamato"),
    (r"\byamto\b", "yamato"),
    (r"\bvdia\b", "vida"),
    (r"\bvidaa\b", "vida"),
    (r"\bpercin\b", "perçin"),
]


def resolve_raw_user_query(text: str | None, message: str | None, has_file: bool) -> tuple[str | None, dict | None]:
    raw_user_query = text if text else message
    if not raw_user_query and not has_file:
        return None, {
            "early": True,
            "response": {"answer": "Boş mesaj.", "reply": "Boş mesaj.", "sources": [], "debug_intent": None},
        }
    if not raw_user_query and has_file:
        raw_user_query = "Yüklenen görseldeki parçayı analiz et."
    return raw_user_query, None


def normalize_user_query(text: str) -> str:
    """
    Kullanıcı sorgusunu arama için normalize eder:
    - sık typo düzeltmeleri
    - ölçü formatları (mm, x, kesir, ondalık)
    """
    if not text:
        return ""

    value = text.strip()

    for pattern, replacement in QUERY_TYPO_RULES:
        value = re.sub(pattern, replacement, value, flags=re.IGNORECASE)

    value = re.sub(r"(?<=\d),(?=\d)", ".", value)
    value = re.sub(r"\bm\s+(\d+(?:\.\d+)?)\b", r"m\1", value, flags=re.IGNORECASE)
    value = re.sub(r"(\d+)\s*/\s*(\d+)", r"\1/\2", value)
    value = re.sub(r"(\d+(?:\.\d+)?)\s*[xX]\s*(\d+(?:\.\d+)?)", r"\1x\2", value)
    value = re.sub(
        r"(\d+(?:\.\d+)?)\s*(mm|milimetre|milimetre|milim|milimetrelik)\b",
        r"\1mm",
        value,
        flags=re.IGNORECASE,
    )
    return re.sub(r"\s+", " ", value).strip()


def parse_json_list(value: str | list | None) -> list:
    if isinstance(value, list):
        return value
    try:
        parsed = json.loads(value or "[]")
        return parsed if isinstance(parsed, list) else []
    except Exception:
        return []


def parse_history(value: str | list | None) -> list[dict[str, Any]]:
    parsed = parse_json_list(value)
    return [item for item in parsed if isinstance(item, dict)]
