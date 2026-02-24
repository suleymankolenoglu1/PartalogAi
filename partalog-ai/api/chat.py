"""
Partalog AI - Chat API (Final v4.2 - Turkish Native Mode + Hybrid Search 🇹🇷)
------------------------------------------------
1. NO DICTIONARY: Sözlük iptal. "SCREW" yok, "VİDA" var.
2. HYBRID SEARCH: Parça kodu varsa ÖNCE tam eşleşme, yoksa Vektör aranır.
3. SMART ROUTER: Marka, Parça ismi, KOD ve ÖLÇÜ(Dimensions) ayıklar.
4. MULTI-PART: Birden fazla parça istenirse "parts" listesi döndürür.
"""

import aiohttp
import asyncio
import base64
import io
import json
import os
from pathlib import Path
from dotenv import load_dotenv
import re
import urllib.parse
import uuid
from PIL import Image
from datetime import datetime, timezone
from pathlib import Path
from fastapi import APIRouter, Form, File, UploadFile, Request
from fastapi.responses import StreamingResponse
from loguru import logger
from config import settings

# Ensure local .env is loaded for GOOGLE_API_KEY / GEMINI_API_KEY
_BASE_DIR = Path(__file__).resolve().parents[1]
_ENV_PATH = _BASE_DIR / ".env"
load_dotenv(_ENV_PATH)

def _clean_key(value: str) -> str:
    return value.strip().strip('"').strip("'").strip() if value else ""

def _get_gemini_api_key_with_source() -> tuple[str, str]:
    if settings.GEMINI_API_KEY:
        return _clean_key(settings.GEMINI_API_KEY), "settings.GEMINI_API_KEY"
    env_google = os.getenv("GOOGLE_API_KEY")
    if env_google:
        return _clean_key(env_google), "env:GOOGLE_API_KEY"
    env_gemini = os.getenv("GEMINI_API_KEY")
    if env_gemini:
        return _clean_key(env_gemini), "env:GEMINI_API_KEY"
    return "", "empty"

_gemini_key_logged = False

def _mask_key(value: str) -> str:
    if not value:
        return "<empty>"
    if len(value) <= 8:
        return f"{value[:2]}...{value[-2:]}"
    return f"{value[:4]}...{value[-4:]}"

def _get_gemini_urls() -> tuple[str, str, str]:
    global _gemini_key_logged
    key, source = _get_gemini_api_key_with_source()
    if not _gemini_key_logged:
        logger.info(f"🔐 GEMINI_API_KEY source={source} value={_mask_key(key)} len={len(key)}")
        _gemini_key_logged = True
    api_url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={key}"
    stream_url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent?alt=sse&key={key}"
    return key, api_url, stream_url
from core.rate_limiter import limiter

# Rate limit constants
CHAT_RATE_LIMIT = "10/minute"
VISUAL_FEEDBACK_RATE_LIMIT = "5/minute"
TEXT_VECTOR_MIN_SIMILARITY = 0.50
WEAK_MATCH_MIN_SIMILARITY = 0.52
GEMINI_CHAT_GENERATION_CONFIG = {
    "temperature": 0.3,
    "maxOutputTokens": 220,
}

# ✅ Gerekli Servisler (exact_match_search eklendi)
from services.embedding import get_text_embedding 
from services.vector_db import search_vector_db, exact_match_search, search_visual_vector_db, update_visual_embedding_in_db

router = APIRouter()

# ⚡️ Gemini API (urls resolved per-request)
SHOP_BASE_URL = "https://www.parcagalerisi.com/ara/"
USER_FEEDBACK_DIR = Path("static/user-generated-parts")
USER_FEEDBACK_INDEX = USER_FEEDBACK_DIR / "index.jsonl"


def _safe_slug(text: str) -> str:
    text = (text or "").strip().replace("/", "_")
    text = re.sub(r"[^A-Za-z0-9._-]+", "_", text)
    text = text.strip("._")
    return text or "unknown_part"


def _safe_ext(filename: str | None) -> str:
    if not filename:
        return ".jpg"
    ext = os.path.splitext(filename)[1].lower()
    if ext in {".jpg", ".jpeg", ".png", ".webp"}:
        return ".jpg" if ext == ".jpeg" else ext
    return ".jpg"

def _best_similarity(sources: list) -> float | None:
    sims: list[float] = []
    for s in sources or []:
        for key in ("similarity", "visual_similarity"):
            val = s.get(key)
            if isinstance(val, (int, float)):
                sims.append(float(val))
    return max(sims) if sims else None


def _normalize_for_overlap(text: str) -> str:
    text = (text or "").lower()
    text = (
        text.replace("ı", "i")
        .replace("İ", "i")
        .replace("ş", "s")
        .replace("ğ", "g")
        .replace("ü", "u")
        .replace("ö", "o")
        .replace("ç", "c")
    )
    return text


def _extract_overlap_tokens(text: str) -> list[str]:
    norm = _normalize_for_overlap(text)
    raw_tokens = re.findall(r"[a-z0-9]+", norm)
    stop = {
        "ve", "ile", "icin", "mi", "mu", "mü", "mı", "var", "yok",
        "arayan", "ariyorum", "ariyorum", "lazim", "tam", "olarak",
        "bir", "bu", "su", "de", "da", "ki", "ya", "ama", "gibi",
    }
    out: list[str] = []
    for tok in raw_tokens:
        if tok in stop:
            continue
        if len(tok) >= 3 or any(ch.isdigit() for ch in tok):
            out.append(tok)
    # stable dedup
    seen = set()
    uniq = []
    for t in out:
        if t in seen:
            continue
        seen.add(t)
        uniq.append(t)
    return uniq


def _has_lexical_overlap(user_query: str, sources: list[dict]) -> bool:
    query_tokens = _extract_overlap_tokens(user_query or "")
    if not query_tokens:
        return False

    haystack_parts: list[str] = []
    for s in sources or []:
        haystack_parts.append(str(s.get("code") or ""))
        haystack_parts.append(str(s.get("name") or ""))
        haystack_parts.append(str(s.get("machine_model") or ""))
        haystack_parts.append(str(s.get("brand") or ""))
        haystack_parts.append(str(s.get("description") or ""))

    haystack = _normalize_for_overlap(" ".join(haystack_parts))
    if not haystack.strip():
        return False

    for tok in query_tokens:
        if tok in haystack:
            return True
    return False


def _has_domain_part_keyword(text: str) -> bool:
    # Tek kelimelik ama domain-içi sorgular (örn: "vida var mı") için
    # overlap zorunluluğunu gevşet.
    domain_terms = {
        "vida", "civata", "somun", "pul", "rondela", "conta", "percin",
        "yay", "plaka", "mil", "rulman", "kayis", "kece", "igne", "disli",
        "kapak", "pim", "burc", "kasnak", "kanca", "yayli", "nozul",
    }
    tokens = set(_extract_overlap_tokens(text))
    return any(t in domain_terms for t in tokens)


_QUERY_TYPO_RULES: list[tuple[str, str]] = [
    (r"\byamaot\b", "yamato"),
    (r"\byamto\b", "yamato"),
    (r"\bvdia\b", "vida"),
    (r"\bvidaa\b", "vida"),
    (r"\bpercin\b", "perçin"),
]


def normalize_user_query(text: str) -> str:
    """
    Kullanıcı sorgusunu arama için normalize eder:
    - sık typo düzeltmeleri
    - ölçü formatları (mm, x, kesir, ondalık)
    """
    if not text:
        return ""

    t = text.strip()

    # 1) Harf typo düzeltmeleri
    for pat, repl in _QUERY_TYPO_RULES:
        t = re.sub(pat, repl, t, flags=re.IGNORECASE)

    # 2) Ondalık virgül -> nokta (sadece sayı arasında)
    t = re.sub(r"(?<=\d),(?=\d)", ".", t)

    # 3) "m 3" -> "m3"
    t = re.sub(r"\bm\s+(\d+(?:\.\d+)?)\b", r"m\1", t, flags=re.IGNORECASE)

    # 4) "3 / 8" -> "3/8"
    t = re.sub(r"(\d+)\s*/\s*(\d+)", r"\1/\2", t)

    # 5) "5 x 20" -> "5x20"
    t = re.sub(r"(\d+(?:\.\d+)?)\s*[xX]\s*(\d+(?:\.\d+)?)", r"\1x\2", t)

    # 6) "5 mm", "5milimetre" -> "5mm"
    t = re.sub(
        r"(\d+(?:\.\d+)?)\s*(mm|milimetre|milimetre|milim|milimetrelik)\b",
        r"\1mm",
        t,
        flags=re.IGNORECASE,
    )

    # 7) Gereksiz çoklu boşlukları temizle
    t = re.sub(r"\s+", " ", t).strip()
    return t


def _is_generic_part_name(name: str | None) -> bool:
    if not name:
        return False
    n = (name or "").strip().lower()
    generic = {
        "vida", "civata", "somun", "pul", "conta", "perçin", "percin",
        "yay", "plaka", "mil", "rulman", "kapak", "pim", "burç", "burc",
        "dişli", "disli",
    }
    return n in generic


def build_no_result_guidance(user_query: str, analysis: dict, reason: str) -> str:
    brand = (analysis or {}).get("brand")
    part_code = (analysis or {}).get("part_code")
    part_name = (analysis or {}).get("part_name")
    dimensions = (analysis or {}).get("dimensions")
    machine_group = (analysis or {}).get("machine_group")

    if reason == "out_of_domain":
        intro = "Ustam, bu sorgu katalogdaki parça içeriğiyle net eşleşmedi."
    elif reason == "weak_match":
        intro = "Ustam, eşleşmeler zayıf kaldı; yanlış parça önermemek için durdurdum."
    else:
        intro = "Ustam, veritabanında bu sorguya doğrudan bir sonuç bulamadım."

    questions: list[str] = []

    if not brand:
        questions.append("Makine markası nedir? (örn: Yamato/Juki)")

    if not machine_group:
        questions.append("Makine tipi nedir? (Düz dikiş / Overlok / Reçme)")

    if not part_code:
        if not dimensions and _is_generic_part_name(part_name):
            questions.append("Parçanın ölçüsü nedir? (örn: M3-0.5x3, 3/8-24x8)")
        else:
            questions.append("Parça kodu varsa birebir yazar mısın?")

    if part_code and not dimensions:
        questions.append("Kod doğruysa, ölçü/model bilgisini de paylaşır mısın?")

    if not questions:
        questions.append("Parça kodu veya net ölçü paylaşırsan nokta atışı bulurum.")

    q_text = "\n".join([f"- {q}" for q in questions[:3]])
    return f"{intro}\nDoğru parçayı netleyelim:\n{q_text}"


def build_deterministic_reply_from_sources(user_query: str, sources: list[dict]) -> str:
    """
    Gemini yanıtı üretilemediğinde deterministic fallback metni üretir.
    """
    if not sources:
        return (
            "Ustam, şu an kısa özet modundayım. "
            "Sonuç listesi boş görünüyor; parça kodu veya ölçü paylaşırsan net arama yaparım."
        )

    picks = sources[:3]
    item_chunks = []
    for s in picks:
        code = s.get("code") or "-"
        name = s.get("name") or "Parça"
        brand = s.get("brand") or ""
        if brand:
            item_chunks.append(f"{code} ({name}, {brand})")
        else:
            item_chunks.append(f"{code} ({name})")

    listed = ", ".join(item_chunks)
    extra = len(sources) - len(picks)
    extra_text = f" Ayrıca {extra} sonuç daha var." if extra > 0 else ""

    return (
        f"Ustam, AI açıklaması şu an üretilemedi ama eşleşen parçaları buldum: {listed}.{extra_text} "
        "Listeden uygun kodu seç veya marka/model/ölçü yaz, hemen daraltayım."
    )


def _parse_json_from_text(text: str) -> dict:
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

    for c in candidates:
        try:
            parsed = json.loads(c)
            if isinstance(parsed, dict):
                return parsed
        except Exception:
            continue
    return {}


async def analyze_image_with_gemini(image_bytes: bytes, user_hint: str = "") -> dict:
    if not image_bytes:
        return {}

    # Resize to max 1024x1024 before sending (saves bandwidth & latency)
    try:
        image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        image.thumbnail((1024, 1024))
        buffered = io.BytesIO()
        image.save(buffered, format="JPEG", quality=85)
        image_bytes = buffered.getvalue()
    except Exception as e:
        logger.warning(f"Image resize failed, using original bytes: {e}")

    prompt = f"""
    Sen bir sanayi yedek parça görsel analiz uzmanısın.
    Yüklenen makine parçası fotoğrafını analiz et ve SADECE JSON döndür:
    {{
      "candidate_part_name": "Parçanın Türkçe adı (tahmin). Örn: 'İğne Barı', 'Baskı Ayağı', 'Vida'",
      "detected_brand_text": "Görselde okunan marka/model yazısı. Örn: 'JUKI', 'TYPICAL', null",
      "visible_codes": "Görselde görünen parça kodu, seri no veya barkod. Örn: 'B2424-354-000', null",
      "machine_type_hint": "Makine türü tahmini. Örn: 'Overlok', 'Düz Dikiş', 'Reçme', null",
      "part_category": "Geniş kategori tek kelime. Örn: 'vida', 'yay', 'iğne', 'plaka', 'mil', 'dişli', 'baskı_ayağı'",
      "material_hint": "Malzeme tahmini. Örn: 'metal', 'plastik', 'kauçuk', null",
      "shape_tags": ["şekil etiketleri listesi. Örn: 'silindirik', 'düz', 'L_şekli', 'flanşlı'"],
      "visual_description": "Parçanın görsel özelliklerini açıklayan 1-2 cümle Türkçe. Renk, şekil, boyut ipuçları, bağlantı noktaları.",
      "embedding_text": "PartName + category + material + shape + brand bilgilerini birleştiren kısa metin. Bu alan VisualEmbedding üretmek için kullanılacak. Örn: 'iğne barı overlok metal silindirik JUKI'",
      "questions_for_user": ["Belirsizlik varsa kullanıcıya sorulacak max 3 Türkçe soru. Belirgin ise boş liste."],
      "confidence": 0.0
    }}

    KURALLAR:
    - Türkçe terminoloji kullan. 'needle bar' değil 'iğne barı' de.
    - Görselde yazı/kod/barkod varsa kesinlikle oku, visible_codes alanına yaz.
    - embedding_text alanını MUTLAKA doldur, asla null bırakma — bu alan veritabanında görsel arama için kritik.
    - Emin değilsen confidence'ı düşür ve questions_for_user doldur.
    - shape_tags her zaman liste olsun, boş olsa bile [].
    - User hint: {user_hint or "yok"}
    """

    payload = {
        "contents": [
            {
                "parts": [
                    {"text": prompt},
                    {
                        "inline_data": {
                            "mime_type": "image/jpeg",
                            "data": base64.b64encode(image_bytes).decode("utf-8"),
                        }
                    },
                ]
            }
        ],
        "generationConfig": {"response_mime_type": "application/json"},
    }

    try:
        async with aiohttp.ClientSession() as session:
            key, api_url, _ = _get_gemini_urls()
            if not key:
                logger.error("⚠️ GEMINI_API_KEY empty. Check partalog-ai/.env")
                return {}
            async with session.post(api_url, json=payload) as resp:
                if resp.status != 200:
                    logger.warning(f"Image analyze failed status={resp.status}")
                    return {}
                data = await resp.json()
                text_resp = data["candidates"][0]["content"]["parts"][0]["text"]
                parsed = _parse_json_from_text(text_resp)
                return parsed if isinstance(parsed, dict) else {}
    except Exception as e:
        logger.error(f"Image analyze error: {e}")
        return {}


def save_user_feedback_sample(
    *,
    file_bytes: bytes,
    original_filename: str | None,
    part_name: str | None,
    part_code: str | None,
    machine_brand: str | None,
    machine_type: str | None,
    user_id: str | None,
    note: str | None,
) -> dict:
    USER_FEEDBACK_DIR.mkdir(parents=True, exist_ok=True)

    part_key = _safe_slug(part_code or part_name or "unknown_part")
    part_dir = USER_FEEDBACK_DIR / part_key
    part_dir.mkdir(parents=True, exist_ok=True)

    ext = _safe_ext(original_filename)
    file_id = uuid.uuid4().hex
    file_name = f"{file_id}{ext}"
    file_path = part_dir / file_name

    with open(file_path, "wb") as f:
        f.write(file_bytes)

    rel_path = file_path.as_posix()
    static_path = f"/{rel_path}" if not rel_path.startswith("/") else rel_path

    record = {
        "id": file_id,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "user_id": user_id,
        "part_name": part_name,
        "part_code": part_code,
        "machine_brand": machine_brand,
        "machine_type": machine_type,
        "note": note,
        "image_path": rel_path,
        "image_url": static_path,
        "source": "chat_user_feedback",
    }

    with open(USER_FEEDBACK_INDEX, "a", encoding="utf-8") as f:
        f.write(json.dumps(record, ensure_ascii=False) + "\n")

    return record

# =========================================================
# 🕵️‍♂️ ROUTER: NİYET, KOD VE ÖLÇÜ ANALİZİ (GÜNCELLENDİ)
# =========================================================
async def analyze_intent_with_gemini(text: str, history: list = None) -> dict:
    # Son 4 mesajı bağlam olarak al
    context_block = ""
    if history:
        recent = history[-4:] if len(history) > 4 else history
        lines = [f"{'Kullanıcı' if m.get('role')=='user' else 'Asistan'}: {m.get('text','').strip()}" for m in recent]
        context_block = "\nSon mesajlar (bağlam için):\n" + "\n".join(lines) + "\n"

    system_prompt = f"""
    GÖREV: Bir sanayi yedek parça asistanı olarak kullanıcı mesajını analiz et.
    {context_block}
    ÇIKTI FORMATI (JSON):
    {{
        "intent": "SEARCH" | "CHAT" | "PRICE" | "STOCK" | "COMPATIBILITY" | "HELP" | "COMPARE",
        "brand": "Marka Varsa Buraya (TYPICAL, JUKI, YAMATO, PEGASUS, BROTHER...)",
        "part_name": "Aranan Parçanın SAF TÜRKÇE ADI (Sıfatları ve ölçüleri at, kök ismi bul)",
        "part_code": "Parça kodu BİREBİR geçiyorsa buraya (örn: B2424-354-000, 110-40056)",
        "dimensions": "Sorgudaki ölçü, metrik veya ebat bilgisi (örn: 5mm, 3/16, M3, 10x20)",
        "parts": [
          {{"part_name": "...", "part_code": null, "dimensions": null}},
          {{"part_name": "...", "part_code": null, "dimensions": null}}
        ],
        "machine_group": "Makine Grubu (Reçme, Overlok, Düz...)",
        "confidence": 0.0-1.0
    }}

    KURALLAR:
    1. ASLA İngilizceye çevirme. Kullanıcı "Vida" dediyse "VİDA" al. "SCREW" DEME!
    2. Gereksiz kelimeleri at.
    3. ÖLÇÜ NORMALİZASYONU: "5 mm", "5milimetre" gibi ifadeleri "dimensions" alanına "5mm" olarak temizle.
    4. KOD TESPİTİ: Harf/Rakam karışık spesifik bir şey yazıldıysa (örn: S08084-001) bunu kesinlikle "part_code" alanına al!
    5. Birden fazla parça varsa "parts" listesine hepsini koy.
    6. BAĞLAM KULLANIMI: Kullanıcı "bunun", "bu parçanın", "onun fiyatı" gibi şeyler yazarsa, son mesajlardaki parçaya atıfta bulunduğunu anla ve o parçayı part_name/part_code'a koy.
    """
    payload = {
        "contents": [{"parts": [{"text": system_prompt + f"\n\nKULLANICI MESAJI: {text}"}]}],
        "generationConfig": {"response_mime_type": "application/json"}
    }
    
    try:
        async with aiohttp.ClientSession() as session:
            key, api_url, _ = _get_gemini_urls()
            if not key:
                logger.error("⚠️ GEMINI_API_KEY empty. Check partalog-ai/.env")
                return {"intent": "SEARCH", "brand": None, "part_name": text, "machine_group": None}
            async with session.post(api_url, json=payload) as resp:
                if resp.status == 200:
                    res = await resp.json()
                    text_resp = res["candidates"][0]["content"]["parts"][0]["text"]
                    return json.loads(text_resp)
                else:
                    return {"intent": "SEARCH", "brand": None, "part_name": text, "machine_group": None}
    except Exception as e:
        logger.error(f"Router Hatası: {e}")
        return {"intent": "SEARCH", "brand": None, "part_name": text, "machine_group": None}

def split_terms(text: str):
    if not text:
        return []
    seps = [" ve ", " & ", ",", ";", "/", " ile "]
    parts = [text]
    for sep in seps:
        parts = [p for chunk in parts for p in chunk.split(sep)]
    return [p.strip() for p in parts if p.strip()]

# =========================================================
# 🔧 ORTAK ARAMA YARDIMCISI (send + stream paylaşır)
# =========================================================
async def _prepare_chat_context(
    text: str | None,
    message: str | None,
    history: str,
    catalog_ids: str,
    file: UploadFile | None,
) -> dict:
    """
    Intent analizi ve hibrit aramayı çalıştırır. Döndürür:
    - {"early": True, "response": {...}}   — erken çıkış durumları
    - {"early": False, "user_query": ..., "all_sources": ...,
       "analysis": ..., "final_prompt": ...}  — normal durum
    """
    raw_user_query = text if text else message
    if not raw_user_query and not file:
        return {"early": True, "response": {"answer": "Boş mesaj.", "reply": "Boş mesaj.", "sources": [], "debug_intent": None}}
    if not raw_user_query and file:
        raw_user_query = "Yüklenen görseldeki parçayı analiz et."

    user_query = normalize_user_query(raw_user_query)
    logger.info(f"📨 [GİRİŞ] Mesaj: {raw_user_query}")
    if user_query != raw_user_query:
        logger.info(f"🧹 [NORMALIZE] '{raw_user_query}' -> '{user_query}'")

    try:
        catalog_ids_list = json.loads(catalog_ids) or []
    except Exception:
        catalog_ids_list = []

    # 1. ANALİZ ET (Router) — eğer dosya varsa her iki analizi paralel çalıştır
    try:
        history_list_for_intent = json.loads(history) if isinstance(history, str) else (history or [])
    except Exception:
        history_list_for_intent = []

    image_analysis = {}

    if file is not None:
        image_bytes = await file.read()
        results = await asyncio.gather(
            analyze_intent_with_gemini(user_query, history=history_list_for_intent),
            analyze_image_with_gemini(image_bytes, raw_user_query),
            return_exceptions=True
        )
        analysis = results[0] if isinstance(results[0], dict) else {"intent": "SEARCH", "brand": None, "part_name": user_query, "machine_group": None}
        image_analysis = results[1] if isinstance(results[1], dict) else {}
        analysis["image_analysis"] = image_analysis
    else:
        analysis = await analyze_intent_with_gemini(user_query, history=history_list_for_intent)

    intent = analysis.get("intent", "CHAT")
    extracted_brand = analysis.get("brand")
    extracted_part = analysis.get("part_name")
    extracted_code = analysis.get("part_code")
    extracted_dim = analysis.get("dimensions")
    extracted_machine_group = analysis.get("machine_group")

    if file is not None:
        img_part = image_analysis.get("candidate_part_name")
        img_brand = image_analysis.get("detected_brand_text")
        img_code = image_analysis.get("visible_codes")  # YENİ

        if (not extracted_part) and img_part:
            extracted_part = img_part
            analysis["part_name"] = img_part
            if intent == "CHAT":
                intent = "SEARCH"
                analysis["intent"] = "SEARCH"
        if (not extracted_brand) and img_brand:
            extracted_brand = img_brand
            analysis["brand"] = img_brand
        if (not extracted_code) and img_code:  # YENİ
            extracted_code = img_code
            analysis["part_code"] = img_code
            if intent == "CHAT":
                intent = "SEARCH"
                analysis["intent"] = "SEARCH"

    parts = analysis.get("parts")
    if not parts:
        if extracted_part or extracted_code:
            parts = [{"part_name": extracted_part, "part_code": extracted_code, "dimensions": extracted_dim}]
        else:
            parts = []

    if len(parts) <= 1 and intent == "SEARCH" and not extracted_code:
        fallback_parts = split_terms(user_query)
        if len(fallback_parts) > 1:
            parts = [{"part_name": p, "part_code": None, "dimensions": None} for p in fallback_parts]

    analysis["parts"] = parts

    if intent == "CHAT" or (not extracted_part and not extracted_code and not parts):
        if image_analysis:
            candidate = image_analysis.get("candidate_part_name") or "parça adı çıkarılamadı"
            brand_hint = image_analysis.get("detected_brand_text") or "marka okunamadı"
            questions = image_analysis.get("questions_for_user") or []
            q_text = " ".join([f"- {q}" for q in questions[:3]]) if questions else "- Makine türü nedir?\n- Marka/model nedir?"
            msg = (
                f"Fotoğraftan tahminim: parça '{candidate}', görünen marka: '{brand_hint}'. "
                f"Doğru parçayı bulmam için şu bilgileri yaz ustam:\n{q_text}"
            )
            return {"early": True, "response": {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}}

        return {
            "early": True,
            "response": {
                "answer": "Aleykümselam ustam. Hangi parçayı arıyorsun? Marka, kod veya parça adı söyle, hemen depoya bakayım.",
                "reply": "Buyur ustam?",
                "sources": [],
                "debug_intent": analysis,
            },
        }

    # ✅ Multi-part & Hybrid Search
    all_sources = []

    # --- YENİ: VISUAL SEARCH (VisualEmbedding dolu parçalarda önce ara) ---
    visual_sources = []
    if file is not None and image_analysis:
        embedding_text_for_search = image_analysis.get("embedding_text")
        visible_codes_from_img = image_analysis.get("visible_codes")
        visual_query_vector = None

        if embedding_text_for_search:
            visual_query_vector = await get_text_embedding(embedding_text_for_search)

            if visual_query_vector:
                # ADIM 1: Yüksek eşikle VisualEmbedding araması
                visual_results = await search_visual_vector_db(
                    query_vector=visual_query_vector,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    min_similarity=0.78,
                    machine_group_filter=extracted_machine_group,
                )
                logger.info(f"🖼️ Visual Search (≥0.78): {len(visual_results)} sonuç")

                # ADIM 2: Sonuç yoksa eşiği düşür
                if not visual_results:
                    logger.info("🖼️ Visual Search fallback: eşik 0.60'a düşürülüyor...")
                    visual_results = await search_visual_vector_db(
                        query_vector=visual_query_vector,
                        brand_filter=extracted_brand,
                        limit=5,
                        catalog_ids=catalog_ids_list,
                        min_similarity=0.60,
                        machine_group_filter=extracted_machine_group,
                    )
                    logger.info(f"🖼️ Visual Search (≥0.60): {len(visual_results)} sonuç")

                # ADIM 3: Hâlâ yok ise normal Embedding araması yap
                if not visual_results:
                    logger.info("🖼️ Visual Search tamamen başarısız. Normal Embedding aramasına fallback...")
                    if visual_query_vector:
                        text_fallback_results = await search_vector_db(
                            query_vector=visual_query_vector,
                            brand_filter=extracted_brand,
                            limit=5,
                            catalog_ids=catalog_ids_list,
                            machine_group_filter=extracted_machine_group,
                        )
                        for r in text_fallback_results:
                            r["visual_similarity"] = r.get("similarity", 0)
                            r["visual_match"] = False
                        visual_results = text_fallback_results
                        logger.info(f"📝 Text Embedding fallback: {len(visual_results)} sonuç")

                # ADIM 4: Görselde kod okunmuşsa exact match de dene
                if not visual_results and visible_codes_from_img:
                    logger.info(f"🔍 Görseldeki kod ile exact match: {visible_codes_from_img}")
                    code_results = await exact_match_search(
                        visible_codes_from_img,
                        brand_filter=extracted_brand,
                        catalog_ids=catalog_ids_list,
                        limit=5,
                        machine_group_filter=extracted_machine_group,
                    )
                    for r in code_results:
                        r["visual_similarity"] = 1.0
                        r["visual_match"] = True
                    visual_results = code_results
                    logger.info(f"🔍 Exact match (görselden kod): {len(visual_results)} sonuç")

                # visual_results'ı visual_sources'a ekle
                for vr in visual_results:
                    p_code_db = vr.get("PartCode", "-")
                    p_name_db = vr.get("PartName", "Bilinmeyen")
                    p_brand_db = vr.get("MachineBrand", "-")
                    p_model_db = vr.get("MachineModel", "")
                    p_desc_db = vr.get("Description", "")
                    visual_img_url = vr.get("VisualImageUrl")
                    safe_code = urllib.parse.quote(p_code_db.strip())
                    buy_link = f"{SHOP_BASE_URL}{safe_code}"
                    if not any(s["code"] == p_code_db for s in visual_sources):
                        visual_similarity = vr.get("visual_similarity")
                        if visual_similarity is None and vr.get("visual_match") is True:
                            visual_similarity = 1.0
                        visual_sources.append({
                            "code": p_code_db,
                            "name": p_name_db,
                            "brand": p_brand_db,
                            "buy_url": buy_link,
                            "machine_model": p_model_db,
                            "description": p_desc_db,
                            "query": embedding_text_for_search,
                            "visual_match": vr.get("visual_match", True),
                            "visual_image_url": visual_img_url,
                            "visual_similarity": visual_similarity,
                        })
                if visual_sources:
                    logger.success(f"🖼️ Visual Search toplam {len(visual_sources)} eşleşme bulundu!")

    # Görsel eşleşmeler önce gelir
    all_sources = list(visual_sources)

    # Eğer tek parça varsa ya da liste varsa hepsini dön (Hybrid Mantığı)
    for part in parts:
        p_code = part.get("part_code")
        p_name = part.get("part_name")
        p_dim = part.get("dimensions")

        part_results = []
        is_fallback = False
        fallback_reason = None
        query_vector = None  # Adım 3'te hesaplanır, 4 ve 5'te yeniden kullanılır

        # HİBRİT ADIM 1: EXACT MATCH (marka + machine_group ile)
        if p_code:
            logger.info(f"🔍 Kod tespit edildi ({p_code}). Exact Match aranıyor...")
            part_results = await exact_match_search(p_code, extracted_brand, catalog_ids_list, limit=5, machine_group_filter=extracted_machine_group)

        # HİBRİT ADIM 2: EXACT MATCH RETRY (filtresiz — kod varsa ama marka ile bulunamadıysa)
        if not part_results and p_code and extracted_brand:
            logger.info(f"🔄 Fallback [Adım 2]: Exact Match marka filtresi kaldırılıyor ({extracted_brand})")
            part_results = await exact_match_search(p_code, None, catalog_ids_list, limit=5, machine_group_filter=None)
            if part_results:
                is_fallback = True
                fallback_reason = "brand_removed"

        # HİBRİT ADIM 3: VECTOR SEARCH (marka + machine_group ile)
        if not part_results and p_name:
            logger.info(f"🧠 Kod yok veya bulunamadı. Vektör (Semantic) aranıyor: {p_name}")
            # Vektör gücünü arttırmak için ölçüyü de ekle
            search_query = f"{p_name} {p_dim}" if p_dim else p_name
            query_vector = await get_text_embedding(search_query)

            if query_vector:
                part_results = await search_vector_db(
                    query_vector,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                    min_similarity=TEXT_VECTOR_MIN_SIMILARITY,
                )
                logger.info(f"🧠 Vector Search (filtreli): {len(part_results)} sonuç")

        # HİBRİT ADIM 4: VECTOR SEARCH RETRY (sadece machine_group kaldır, marka tut)
        if not part_results and p_name and extracted_machine_group:
            logger.info(f"🔄 Fallback [Adım 4]: machine_group filtresi kaldırıldı ({extracted_machine_group}), brand={extracted_brand} korundu")
            if query_vector:
                part_results = await search_vector_db(
                    query_vector,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=None,
                    min_similarity=TEXT_VECTOR_MIN_SIMILARITY,
                )
                logger.info(f"🧠 Vector Search (machine_group'suz): {len(part_results)} sonuç")
                if part_results:
                    is_fallback = True
                    fallback_reason = "machine_group_removed"

        # HİBRİT ADIM 5: VECTOR SEARCH RETRY (marka da kaldır, tamamen filtresiz)
        if not part_results and p_name and extracted_brand:
            logger.info(f"🔄 Fallback [Adım 5]: brand filtresi de kaldırıldı ({extracted_brand}), filtresiz arama yapılıyor")
            if query_vector:
                part_results = await search_vector_db(
                    query_vector,
                    brand_filter=None,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=None,
                    min_similarity=TEXT_VECTOR_MIN_SIMILARITY,
                )
                logger.info(f"🧠 Vector Search (filtresiz): {len(part_results)} sonuç")
                if part_results:
                    is_fallback = True
                    fallback_reason = "all_filters_removed"

        # Sonuçları listeye toparla
        for p in part_results:
            p_code_db = p.get('PartCode', '-')
            p_name_db = p.get('PartName', 'Bilinmeyen')
            p_brand_db = p.get('MachineBrand', '-')
            p_model_db = p.get('MachineModel', '')
            p_desc_db = p.get('Description', '')

            safe_code = urllib.parse.quote(p_code_db.strip())
            buy_link = f"{SHOP_BASE_URL}{safe_code}"

            # Mükerrerliği önle
            if not any(s['code'] == p_code_db for s in all_sources):
                source_entry = {
                    "code": p_code_db,
                    "name": p_name_db,
                    "brand": p_brand_db,
                    "buy_url": buy_link,
                    "machine_model": p_model_db,
                    "description": p_desc_db,
                    "query": p_name or p_code,
                    "similarity": p.get("similarity", 1.0 if p_code else None),
                }
                if is_fallback:
                    source_entry["fallback"] = True
                    source_entry["fallback_reason"] = fallback_reason
                all_sources.append(source_entry)

    logger.success(f"📦 Toplam Bulunan Benzersiz Sonuç: {len(all_sources)}")

    if not all_sources:
        msg = build_no_result_guidance(raw_user_query, analysis, reason="no_result")
        return {"early": True, "response": {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}}

    best_sim = _best_similarity(all_sources)
    has_overlap = _has_lexical_overlap(user_query, all_sources)
    has_domain_keyword = _has_domain_part_keyword(user_query)
    logger.info(
        f"📊 En iyi benzerlik skoru: {best_sim if best_sim is not None else 'N/A'} | "
        f"lexical_overlap={has_overlap} | domain_keyword={has_domain_keyword}"
    )
    if (
        best_sim is not None
        and best_sim < 0.70
        and not has_overlap
        and not has_domain_keyword
        and not extracted_code
    ):
        msg = build_no_result_guidance(raw_user_query, analysis, reason="out_of_domain")
        return {"early": True, "response": {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}}

    if best_sim is not None and best_sim < WEAK_MATCH_MIN_SIMILARITY:
        msg = build_no_result_guidance(raw_user_query, analysis, reason="weak_match")
        return {"early": True, "response": {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}}

    # 4. Gemini'ye verilecek Context Metni
    context_lines = []
    for s in all_sources[:6]: # Gemini gecikmesini azaltmak için ilk 6
        sim_val = s.get("similarity") or s.get("visual_similarity")
        sim_text = f" | Benzerlik: {sim_val:.2f}" if isinstance(sim_val, (int, float)) else ""
        desc = (s.get("description") or "").strip()
        if len(desc) > 140:
            desc = desc[:137] + "..."
        line = (
            f"- Marka: {s['brand']} | Model: {s['machine_model']} | "
            f"Parça: {s['name']} (Kod: {s['code']}) | Detay: {desc}{sim_text}"
        )
        context_lines.append(line)

    context_text = "\n".join(context_lines)

    # History'yi parse et
    history_text = ""
    try:
        history_list = json.loads(history) if isinstance(history, str) else (history or [])
        recent = history_list[-6:] if len(history_list) > 6 else history_list
        lines = []
        for msg in recent:
            role_label = "Kullanıcı" if msg.get("role") == "user" else "Sen (Asistan)"
            lines.append(f"{role_label}: {msg.get('text', '').strip()}")
        history_text = "\n".join(lines)
    except Exception:
        history_text = ""

    # 5. FİNAL PROMPT
    final_prompt = f"""
Sen sanayi yedek parça uzmanısın (Partalog AI). Kısa, samimi, usta ağzıyla konuş.

{"SOHBET GEÇMİŞİ (bağlam için kullan):" + chr(10) + history_text + chr(10) if history_text else ""}
ŞİMDİKİ KULLANICI SORUSU: "{raw_user_query}"

DEPODAN BULDUĞUN PARÇALAR:
{context_text}

GÖREV:
1. Bulduğun parçaları özetle. Marka, model, ölçü uyumuna dikkat çek.
2. Sohbet geçmişindeki bağlamı kullan — kullanıcı "bu parçanın fiyatı ne?" derse hangi parçadan bahsettiğini geçmişten anla.
3. Sadece listelenen parçaları referans al; liste dışı marka/model/ürün uydurma.
4. Eğer listede net ve güçlü bir eşleşme yoksa kullanıcıdan net bilgi iste.
5. Link verme, sistem zaten gösterecek.
6. Maksimum 3-4 cümle yaz.
"""

    return {
        "early": False,
        "user_query": user_query,
        "all_sources": all_sources,
        "analysis": analysis,
        "final_prompt": final_prompt,
    }


# =========================================================
# 🧠 ANA CHAT ENDPOINT (HİBRİT ARAMA EKLENDİ)
# =========================================================
@router.post("/send")
@router.post("/expert-chat")
@limiter.limit(CHAT_RATE_LIMIT)
async def chat_endpoint(
    request: Request,
    text: str = Form(None),
    message: str = Form(None),
    history: str = Form("[]"),
    catalog_ids: str = Form("[]"),
    file: UploadFile = File(None),
):
    try:
        ctx = await _prepare_chat_context(text, message, history, catalog_ids, file)
        if ctx["early"]:
            return ctx["response"]

        async with aiohttp.ClientSession() as session:
            payload = {
                "contents": [{"parts": [{"text": ctx["final_prompt"]}]}],
                "generationConfig": GEMINI_CHAT_GENERATION_CONFIG,
            }
            key, api_url, _ = _get_gemini_urls()
            if not key:
                logger.error("⚠️ GEMINI_API_KEY empty. Check partalog-ai/.env")
                fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                return {
                    "answer": fallback,
                    "reply": fallback,
                    "sources": ctx.get("all_sources", []),
                    "debug_intent": ctx.get("analysis"),
                }
            async with session.post(api_url, json=payload) as resp:
                if resp.status == 200:
                    ai_reply = (await resp.json())["candidates"][0]["content"]["parts"][0]["text"]
                else:
                    logger.warning(f"Gemini generateContent non-200: {resp.status}")
                    ai_reply = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))

        return {
            "answer": ai_reply,
            "reply": ai_reply,
            "sources": ctx["all_sources"],
            "debug_intent": ctx["analysis"],
        }

    except Exception as e:
        logger.error(f"Chat Hatası: {e}")
        return {
            "answer": "Sistemsel bir hata oluştu ustam.",
            "reply": "Hata",
            "sources": [],
            "debug_intent": None,
        }


# =========================================================
# 🌊 STREAMING CHAT ENDPOINT (SSE)
# =========================================================
@router.post("/stream")
async def chat_stream_endpoint(
    text: str = Form(None),
    message: str = Form(None),
    history: str = Form("[]"),
    catalog_ids: str = Form("[]"),
    file: UploadFile = File(None),
):
    async def event_generator():
        try:
            ctx = await _prepare_chat_context(text, message, history, catalog_ids, file)
            if ctx["early"]:
                resp = ctx["response"]
                yield f"data: {json.dumps({'type': 'sources', 'sources': resp.get('sources', []), 'debug_intent': resp.get('debug_intent')})}\n\n"
                yield f"data: {json.dumps({'type': 'token', 'token': resp.get('answer', '')})}\n\n"
                yield f"data: {json.dumps({'type': 'done'})}\n\n"
                return

            # Kaynakları stream başında tek seferlik gönder
            yield f"data: {json.dumps({'type': 'sources', 'sources': ctx['all_sources'], 'debug_intent': ctx['analysis']})}\n\n"

            # Gemini streamGenerateContent çağrısı
            payload = {
                "contents": [{"parts": [{"text": ctx["final_prompt"]}]}],
                "generationConfig": GEMINI_CHAT_GENERATION_CONFIG,
            }
            async with aiohttp.ClientSession() as session:
                key, _, stream_url = _get_gemini_urls()
                if not key:
                    logger.error("⚠️ GEMINI_API_KEY empty. Check partalog-ai/.env")
                    fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                    yield f"data: {json.dumps({'type': 'token', 'token': fallback})}\n\n"
                    yield f"data: {json.dumps({'type': 'done'})}\n\n"
                    return
                async with session.post(stream_url, json=payload) as resp:
                    logger.info(f"🤖 [GEMINI-STREAM] status={resp.status} content-type={resp.headers.get('Content-Type')}")
                    if resp.status != 200:
                        try:
                            err_text = await resp.text()
                        except Exception:
                            err_text = "<read-failed>"
                        logger.error(f"🤖 [GEMINI-STREAM] error body: {err_text[:500]}")
                        fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                        yield f"data: {json.dumps({'type': 'token', 'token': fallback})}\n\n"
                        yield f"data: {json.dumps({'type': 'done'})}\n\n"
                        return
                    buffer = ""
                    done = False
                    token_count = 0
                    line_count = 0
                    async for chunk in resp.content.iter_any():
                        buffer += chunk.decode("utf-8")
                        while "\n" in buffer:
                            line, buffer = buffer.split("\n", 1)
                            line = line.strip()
                            if not line:
                                continue
                            if not line.startswith("data:"):
                                continue
                            raw = line[5:].strip()
                            if raw == "[DONE]":
                                done = True
                                break
                            line_count += 1
                            if line_count <= 3:
                                logger.debug(f"🤖 [GEMINI-STREAM] raw line sample: {raw[:200]}")
                            try:
                                chunk_json = json.loads(raw)
                                parts = (
                                    chunk_json.get("candidates", [{}])[0]
                                    .get("content", {})
                                    .get("parts", [])
                                )
                                token = parts[0].get("text") if parts else None
                                if token:
                                    token_count += 1
                                    yield f"data: {json.dumps({'type': 'token', 'token': token})}\n\n"
                            except Exception as parse_err:
                                logger.debug(f"SSE chunk parse atlandı: {parse_err} | raw={raw[:80]}")
                                continue
                        if done:
                            break
                    if token_count == 0:
                        logger.warning("🤖 [GEMINI-STREAM] 0 token üretildi. Yanıt formatı beklenenden farklı olabilir.")
                        fallback = build_deterministic_reply_from_sources(ctx["user_query"], ctx.get("all_sources", []))
                        yield f"data: {json.dumps({'type': 'token', 'token': fallback})}\n\n"

        except Exception as e:
            logger.error(f"Chat Stream Hatası: {e}")
            yield f"data: {json.dumps({'type': 'token', 'token': 'Sistemsel bir hata oluştu ustam.'})}\n\n"

        yield f"data: {json.dumps({'type': 'done'})}\n\n"

    return StreamingResponse(event_generator(), media_type="text/event-stream")


@router.post("/visual-feedback")
@limiter.limit(VISUAL_FEEDBACK_RATE_LIMIT)
async def visual_feedback_endpoint(
    request: Request,
    # ... (Burası senin orijinal dosyanla tamamen aynı) ...
    file: UploadFile = File(...),
    part_name: str = Form(None),
    part_code: str = Form(None),
    machine_brand: str = Form(None),
    machine_type: str = Form(None),
    user_id: str = Form(None),
    note: str = Form(None),
):
    try:
        if file is None:
            return {"success": False, "message": "Fotoğraf zorunlu."}

        image_bytes = await file.read()
        if not image_bytes:
            return {"success": False, "message": "Boş dosya."}

        if not part_name and not part_code:
            return {"success": False, "message": "part_name veya part_code zorunlu."}

        # 1. Dosyayı kaydet (mevcut davranış korunuyor)
        record = save_user_feedback_sample(
            file_bytes=image_bytes,
            original_filename=file.filename,
            part_name=part_name,
            part_code=part_code,
            machine_brand=machine_brand,
            machine_type=machine_type,
            user_id=user_id,
            note=note,
        )

        # 2. YENİ: Görsel analiz yap → embedding_text üret → VisualEmbedding DB'ye yaz
        visual_embedding_saved = False
        try:
            # VLM analizi (embedding_text için)
            vlm_result = await analyze_image_with_gemini(image_bytes, user_hint=part_name or "")
            embedding_text = vlm_result.get("embedding_text")

            # Fallback: embedding_text boşsa part_name + part_code ile oluştur
            if not embedding_text:
                parts_text = " ".join(filter(None, [part_name, part_code, machine_brand, machine_type]))
                embedding_text = parts_text if parts_text else None

            if embedding_text:
                visual_vector = await get_text_embedding(embedding_text)
                if visual_vector and part_code:
                    # VisualEmbedding'i DB'ye yaz
                    saved = await update_visual_embedding_in_db(
                        part_code=part_code,
                        visual_vector=visual_vector,
                        visual_image_url=record.get("image_url"),
                        visual_shape_tags=vlm_result.get("shape_tags"),
                        visual_ocr_text=vlm_result.get("visible_codes"),
                    )
                    visual_embedding_saved = saved
                    if saved:
                        logger.success(f"✅ VisualEmbedding güncellendi: {part_code}")
                    else:
                        logger.warning(f"⚠️ VisualEmbedding yazılamadı (part_code DB'de bulunamadı?): {part_code}")
        except Exception as e:
            logger.error(f"VisualEmbedding güncelleme hatası: {e}")
            # Embedding hatası feedback kaydını engellemez

        return {
            "success": True,
            "message": "Kullanıcı geri bildirimi kaydedildi.",
            "visual_embedding_saved": visual_embedding_saved,
            "record": record,
        }
    except Exception as e:
        logger.error(f"visual-feedback error: {e}")
        return {"success": False, "message": "Geri bildirim kaydedilemedi."}
