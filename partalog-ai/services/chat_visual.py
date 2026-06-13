"""Visual analysis helpers for chat and feedback flows."""

import base64
import io
import json
import re

import aiohttp
from PIL import Image
from loguru import logger

from config import settings
from services.chat_terms import extract_overlap_tokens, normalize_for_overlap
from services.genai_provider import provider


def parse_json_from_text(text: str) -> dict:
    if not text:
        return {}
    text = text.strip()
    candidates = [text]
    fence = re.search(r"```(?:json)?\s*(.*?)\s*```", text, re.DOTALL | re.IGNORECASE)
    if fence:
        candidates.append(fence.group(1).strip())
    obj = re.search(r"(\{[\s\S]*\})", text)
    if obj:
        candidates.append(obj.group(1).strip())

    for candidate in candidates:
        try:
            parsed = json.loads(candidate)
            if isinstance(parsed, dict):
                return parsed
        except Exception:
            continue
    return {}


def as_clean_text_list(value) -> list[str]:
    if value is None:
        return []
    raw_values = value if isinstance(value, list) else re.split(r"[,;/|]+", str(value))
    out: list[str] = []
    seen = set()
    for item in raw_values:
        text = str(item or "").strip()
        if not text:
            continue
        key = normalize_for_overlap(text)
        if key in seen:
            continue
        seen.add(key)
        out.append(text)
    return out


def normalize_image_analysis(payload: dict | None) -> dict:
    if not isinstance(payload, dict):
        return {}
    normalized = dict(payload)
    normalized["shape_traits"] = as_clean_text_list(normalized.get("shape_traits") or normalized.get("shape_tags"))
    normalized["shape_tags"] = normalized["shape_traits"]
    normalized["visible_code_tokens"] = as_clean_text_list(normalized.get("visible_code_tokens") or normalized.get("visible_codes"))
    normalized["brand_model_tokens"] = as_clean_text_list(normalized.get("brand_model_tokens") or normalized.get("detected_brand_text"))
    if not normalized.get("part_family") and normalized.get("part_category"):
        normalized["part_family"] = normalized.get("part_category")
    if not normalized.get("material") and normalized.get("material_hint"):
        normalized["material"] = normalized.get("material_hint")
    if not normalized.get("assembly_hint") and normalized.get("machine_type_hint"):
        normalized["assembly_hint"] = normalized.get("machine_type_hint")
    return normalized


def build_visual_hint_text(image_analysis: dict | None, fallback_text: str = "") -> str:
    if not isinstance(image_analysis, dict):
        return fallback_text.strip()
    parts: list[str] = []
    for key in (
        "candidate_part_name",
        "part_family",
        "part_category",
        "material",
        "material_hint",
        "size_hint",
        "assembly_hint",
        "machine_type_hint",
        "detected_brand_text",
        "visual_description",
    ):
        value = image_analysis.get(key)
        if value:
            parts.append(str(value))
    for key in ("shape_traits", "shape_tags", "visible_code_tokens", "brand_model_tokens"):
        parts.extend(as_clean_text_list(image_analysis.get(key)))
    if fallback_text:
        parts.append(fallback_text)
    seen = set()
    cleaned: list[str] = []
    for part in parts:
        text = str(part or "").strip()
        if not text:
            continue
        key = normalize_for_overlap(text)
        if key in seen:
            continue
        seen.add(key)
        cleaned.append(text)
    return " | ".join(cleaned)


def rerank_results_by_visual_hints(results: list[dict], image_analysis: dict | None) -> list[dict]:
    if not results or not isinstance(image_analysis, dict):
        return results
    hint_text = build_visual_hint_text(image_analysis)
    hint_tokens = [token for token in extract_overlap_tokens(hint_text) if len(token) >= 3]
    if not hint_tokens:
        return results
    scored_rows: list[tuple[int, float, dict]] = []
    for row in results:
        haystack = normalize_for_overlap(
            " ".join(
                [
                    str(row.get("PartName") or ""),
                    str(row.get("Description") or ""),
                    str(row.get("PartCode") or ""),
                    str(row.get("RefNumber") or ""),
                    str(row.get("MachineBrand") or ""),
                    str(row.get("MachineModel") or ""),
                    str(row.get("MachineGroup") or ""),
                    str(row.get("Mechanism") or ""),
                    str(row.get("Dimensions") or ""),
                    str(row.get("VisualShapeTags") or ""),
                    str(row.get("VisualOcrText") or ""),
                ]
            )
        )
        hint_score = sum(1 for token in hint_tokens if token in haystack)
        base_score = row.get("visual_hint_score")
        if not isinstance(base_score, (int, float)):
            base_score = row.get("similarity")
        if not isinstance(base_score, (int, float)):
            base_score = row.get("visual_similarity")
        scored_rows.append((hint_score, float(base_score) if isinstance(base_score, (int, float)) else 0.0, row))
    max_hint = max((score[0] for score in scored_rows), default=0)
    if max_hint <= 0:
        return results
    scored_rows.sort(key=lambda item: (item[0], item[1]), reverse=True)
    logger.info(f"🧩 Visual hint rerank uygulandı: tokens={hint_tokens[:10]} max_hint={max_hint} count={len(results)}")
    return [item[2] for item in scored_rows]


async def analyze_image_with_gemini(image_bytes: bytes, user_hint: str = "") -> dict:
    if not image_bytes:
        return {}
    try:
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        image.thumbnail((1024, 1024))
        buffered = io.BytesIO()
        image.save(buffered, format="JPEG", quality=85)
        image_bytes = buffered.getvalue()
    except Exception as exc:
        logger.warning(f"Image resize failed, using original bytes: {exc}")

    prompt = f"""
    Sen bir sanayi yedek parça görsel analiz uzmanısın.
    Yüklenen makine parçası fotoğrafını analiz et ve SADECE JSON döndür:
    {{
      "candidate_part_name": "Parçanın Türkçe adı (tahmin). Örn: 'İğne Barı', 'Baskı Ayağı', 'Vida'",
      "detected_brand_text": "Görselde okunan marka/model yazısı. Örn: 'JUKI', 'TYPICAL', null",
      "visible_codes": "Görselde görünen parça kodu, seri no veya barkod. Örn: 'B2424-354-000', null",
      "visible_code_tokens": ["Kod tam okunmuyorsa görünen parçaları yaz. Örn: 'B2424', '354', '000'"],
      "machine_type_hint": "Makine türü tahmini. Örn: 'Overlok', 'Düz Dikiş', 'Reçme', null",
      "assembly_hint": "Parçanın ait olabileceği bölge/assembly. Örn: 'iğne çevresi', 'baskı ayağı bölgesi', 'alt mekanizma', 'iplik yolu', null",
      "part_family": "Parça ailesi tek kelime. Örn: 'vida', 'yay', 'plaka', 'dişli', 'mil', 'kapak', 'ayak'",
      "part_category": "Geniş kategori tek kelime. Örn: 'vida', 'yay', 'iğne', 'plaka', 'mil', 'dişli', 'baskı_ayağı'",
      "material": "Malzeme. Örn: 'metal', 'plastik', 'kauçuk', null",
      "material_hint": "Malzeme tahmini. Örn: 'metal', 'plastik', 'kauçuk', null",
      "size_hint": "Görsel boyut sınıfı. Örn: 'küçük', 'ince', 'uzun', 'kalın', 'geniş', null",
      "shape_traits": ["Yapısal özellikler. Örn: 'iki_delikli', 'L_tip', 'flanşlı', 'silindirik', 'yay_formu', 'dişli_kenar'"],
      "shape_tags": ["şekil etiketleri listesi. Örn: 'silindirik', 'düz', 'L_şekli', 'flanşlı'"],
      "brand_model_tokens": ["Görselde okunan marka/model parçaları. Örn: 'JUKI', 'MF-7900'"],
      "visual_description": "Parçanın görsel özelliklerini açıklayan 1-2 cümle Türkçe. Renk, şekil, boyut ipuçları, bağlantı noktaları.",
      "embedding_text": "PartName + category + material + shape + brand bilgilerini birleştiren kısa metin. Bu alan VisualEmbedding üretmek için kullanılacak. Örn: 'iğne barı overlok metal silindirik JUKI'",
      "questions_for_user": ["Belirsizlik varsa kullanıcıya sorulacak max 3 Türkçe soru. Belirgin ise boş liste."],
      "confidence": 0.0
    }}

    KURALLAR:
    - Türkçe terminoloji kullan. 'needle bar' değil 'iğne barı' de.
    - Görselde yazı/kod/barkod varsa kesinlikle oku, visible_codes alanına yaz.
    - Kod tam okunmuyorsa okunan parçaları visible_code_tokens listesine koy.
    - part_family, shape_traits, assembly_hint alanları arama/rerank için kritik; boş bırakmamaya çalış.
    - embedding_text alanını MUTLAKA doldur, asla null bırakma — bu alan veritabanında görsel arama için kritik.
    - Emin değilsen confidence'ı düşür ve questions_for_user doldur.
    - shape_traits, shape_tags, visible_code_tokens, brand_model_tokens her zaman liste olsun, boş olsa bile [].
    - User hint: {user_hint or "yok"}
    """
    payload = provider.normalize_generate_payload({
        "contents": [{"parts": [{"text": prompt}, {"inline_data": {"mime_type": "image/jpeg", "data": base64.b64encode(image_bytes).decode("utf-8")}}]}],
        "generationConfig": {"response_mime_type": "application/json"},
    })
    try:
        api_url = provider.generate_content_url(settings.GEMINI_ANALYSIS_MODEL)
        headers = await provider.build_headers()
        async with aiohttp.ClientSession(headers=headers) as session:
            if not provider.has_credentials() or not api_url:
                logger.error("⚠️ GenAI credentials/config eksik.")
                return {}
            async with session.post(api_url, json=payload) as response:
                if response.status != 200:
                    logger.warning(f"Image analyze failed status={response.status}")
                    return {}
                data = await response.json()
                text_response = data["candidates"][0]["content"]["parts"][0]["text"]
                return normalize_image_analysis(parse_json_from_text(text_response))
    except Exception as exc:
        logger.error(f"Image analyze error: {exc}")
        return {}
