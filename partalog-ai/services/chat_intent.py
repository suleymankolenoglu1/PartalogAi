"""Intent analysis and context helpers for chat requests."""

import json
import re

import aiohttp
from loguru import logger

from config import settings
from services.chat_terms import normalize_for_overlap
from services.genai_provider import provider


ALLOWED_INTENTS = {
    "SEARCH", "CHAT", "PRICE", "STOCK", "COMPATIBILITY", "HELP", "COMPARE",
    "DIAGNOSE", "ADVICE", "EXPLAIN_PART", "PHOTO_GUIDANCE",
}

PRICE_HINTS = (
    "fiyat",
    "kaç para",
    "kac para",
    "ücret",
    "ucret",
)
STOCK_HINTS = (
    "stok",
    "stokta",
)
CODE_TOKEN_PATTERN = re.compile(r"\b[A-Z]{1,4}\d[A-Z0-9-]{3,}|\b\d{6,}[A-Z0-9-]*\b", re.IGNORECASE)


async def analyze_intent_with_gemini(text: str, history: list | None = None) -> dict:
    context_block = ""
    if history:
        recent = history[-4:] if len(history) > 4 else history
        lines = [f"{'Kullanıcı' if message.get('role') == 'user' else 'Asistan'}: {message.get('text', '').strip()}" for message in recent]
        context_block = "\nSon mesajlar (bağlam için):\n" + "\n".join(lines) + "\n"

    system_prompt = f"""
    GÖREV: Bir sanayi yedek parça asistanı olarak kullanıcı mesajını analiz et.
    {context_block}
    ÇIKTI FORMATI (JSON):
    {{
        "intent": "SEARCH" | "CHAT" | "PRICE" | "STOCK" | "COMPATIBILITY" | "HELP" | "COMPARE" | "DIAGNOSE" | "ADVICE" | "EXPLAIN_PART" | "PHOTO_GUIDANCE",
        "brand": "Marka Varsa Buraya (TYPICAL, JUKI, YAMATO, PEGASUS, BROTHER...)",
        "part_name": "Aranan Parçanın SAF TÜRKÇE ADI (Sıfatları ve ölçüleri at, kök ismi bul)",
        "context_part": "Parçanın bağlamı (örn: vida neyi tutuyor/sabitleyor) varsa buraya",
        "part_code": "Parça kodu BİREBİR geçiyorsa buraya (örn: B2424-354-000, 110-40056)",
        "dimensions": "Sorgudaki ölçü, metrik veya ebat bilgisi (örn: 5mm, 3/16, M3, 10x20)",
        "machine_model": "Makine modeli varsa buraya (örn: VG-2500, VG2500-8F, MF-7900)",
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
    7. BAĞLAÇ ANALİZİ: Kullanıcı "X'i sabitleyen/tutan/bağlayan/içindeki/üzerindeki Y" derse aranan parça Y'dir, X değil.
       X sadece bağlamdır ve "context_part" alanına yazılır.
       Örn: "ön kumaş plakasını sabitleyen vida" -> part_name="vida", context_part="ön kumaş plakası".
    8. BAĞLAMSAL PARÇA: "sabitleyen", "tutan", "bağlayan", "içindeki", "altındaki", "üzerindeki", "montajı için"
       gibi ilişki fiilleri varsa, fiilin bağladığı hedef isim aranan parçadır.
    9. DIAGNOSE: Kullanıcı arıza/belirti anlatıyorsa seç. Örnek: "Makinam atlıyor", "ses yapıyor", "iplik kopuyor".
    10. ADVICE: Kullanıcı öneri/tavsiye istiyorsa seç. Örnek: "Hangi iğneyi kullanmalıyım?", "ne önerirsin?".
    11. EXPLAIN_PART: Kullanıcı bir parçanın ne işe yaradığını, nerede kullanıldığını veya farkını soruyorsa seç.
    12. PHOTO_GUIDANCE: Fotoğrafın yetersizliği, nasıl fotoğraf çekileceği veya görselden parça bulmayı iyileştirme sorularında seç.
    13. BAĞLAM KORUMA: Kullanıcı arıza konuşmasından sonra "müşteriye mesaj yaz", "kontrol listesi yap",
        "WhatsApp'tan göndereceğim metin yaz" derse bunu CHAT yapma; önceki arıza bağlamını koru ve DIAGNOSE seç.
    14. KOD ARAMA: "kodunu getir", "kodu nedir", "parça no bul", "part no getir" gibi istekler SEARCH'tür.
        Bunları PRICE veya STOCK yapma; kullanıcı fiyat/sipariş istemiyorsa sadece parça kodu arıyordur.
    """
    payload = provider.normalize_generate_payload({
        "contents": [{"parts": [{"text": system_prompt + f"\n\nKULLANICI MESAJI: {text}"}]}],
        "generationConfig": {"response_mime_type": "application/json"},
    })
    try:
        api_url = provider.generate_content_url(settings.GEMINI_CHAT_MODEL)
        headers = await provider.build_headers()
        async with aiohttp.ClientSession(headers=headers) as session:
            if not provider.has_credentials() or not api_url:
                logger.error("⚠️ GenAI credentials/config eksik.")
                return normalize_intent_payload(None, text)
            async with session.post(api_url, json=payload) as response:
                if response.status == 200:
                    result = await response.json()
                    text_response = result["candidates"][0]["content"]["parts"][0]["text"]
                    return normalize_intent_payload(json.loads(text_response), text)
                return normalize_intent_payload(None, text)
    except Exception as exc:
        logger.error(f"Router Hatası: {exc}")
        return normalize_intent_payload(None, text)


def unavailable_feature_label(intent: str | None) -> str | None:
    normalized = str(intent or "").upper()
    if normalized == "PRICE":
        return "fiyat"
    if normalized == "STOCK":
        return "stok"
    return None


def unavailable_feature_notice(analysis: dict | None) -> str:
    label = (analysis or {}).get("unsupported_feature")
    return f" Not: {label} bilgisi bu ekranda henüz aktif değil; şu an katalogdaki parça eşleşmesini gösteriyorum." if label else ""


def ensure_unavailable_feature_notice(reply: str | None, analysis: dict | None) -> str:
    text = (reply or "").strip()
    notice = unavailable_feature_notice(analysis).strip()
    if not notice:
        return text
    normalized = normalize_for_overlap(text)
    if "henuz aktif degil" in normalized and any(label in normalized for label in ("fiyat bilgisi", "stok bilgisi")):
        return text
    return f"{text}\n\n{notice}" if text else notice


def normalize_intent_payload(payload: dict | None, fallback_text: str) -> dict:
    base = {"intent": "SEARCH", "brand": None, "part_name": fallback_text, "machine_group": None}
    fallback_code = extract_code_from_text(fallback_text)
    if not isinstance(payload, dict):
        base["intent"] = infer_unavailable_feature_intent(fallback_text) or base["intent"]
        if fallback_code:
            base["part_code"] = fallback_code
            base["part_name"] = None
        return base
    normalized = dict(payload)
    intent = str(normalized.get("intent", "SEARCH")).strip().upper()
    normalized["intent"] = intent if intent in ALLOWED_INTENTS else "SEARCH"
    unavailable_feature_intent = infer_unavailable_feature_intent(fallback_text)
    if unavailable_feature_intent and normalized["intent"] in {"SEARCH", "CHAT", "HELP"}:
        normalized["intent"] = unavailable_feature_intent
    normalized.setdefault("brand", None)
    normalized.setdefault("part_name", fallback_text)
    normalized.setdefault("context_part", None)
    normalized.setdefault("machine_model", None)
    normalized.setdefault("machine_group", None)
    if fallback_code and not normalized.get("part_code"):
        normalized["part_code"] = fallback_code
    return normalized


def extract_code_from_text(text: str | None) -> str | None:
    match = CODE_TOKEN_PATTERN.search(str(text or "").upper())
    return match.group(0).strip() if match else None


def build_explicit_code_analysis(text: str | None) -> dict | None:
    code = extract_code_from_text(text)
    if not code:
        return None
    return {
        "intent": "SEARCH",
        "part_code": code,
        "part_name": None,
        "parts": [
            {
                "part_name": None,
                "part_code": code,
                "dimensions": None,
            }
        ],
    }


def infer_unavailable_feature_intent(text: str | None) -> str | None:
    normalized = normalize_for_overlap(text or "")
    if any(hint in normalized for hint in PRICE_HINTS):
        return "PRICE"
    if any(hint in normalized for hint in STOCK_HINTS):
        return "STOCK"
    return None


def get_intent_mode_block(intent: str | None) -> str:
    normalized = str(intent or "").upper()
    if normalized == "DIAGNOSE":
        return (
            "AKTİF NİYET: DIAGNOSE\n"
            "- Teşhis modunda cevap ver.\n"
            "- Genel usta bilgisiyle olası 2-3 teknik nedeni kısa yaz.\n"
            "- Katalog sonucu varsa 'katalogda kontrol edilebilecek parçalar' olarak ayır.\n"
            "- Sonunda teşhisi netleştirecek 1-2 soru sor.\n"
        )
    if normalized == "ADVICE":
        return (
            "AKTİF NİYET: ADVICE\n"
            "- Öneri modunda cevap ver.\n"
            "- Genel öneriyi katalog sonucundan ayrı tut.\n"
            "- 2-3 uygulanabilir öneri sun ve hangi durumda hangisinin uygun olduğunu belirt.\n"
            "- Gerekiyorsa model/ölçü netleştirme sorusu sor.\n"
        )
    if normalized == "EXPLAIN_PART":
        return (
            "AKTİF NİYET: EXPLAIN_PART\n"
            "- Parçanın ne işe yaradığını, makinedeki tipik görevini ve karıştırılabileceği parçaları açıkla.\n"
            "- Katalogdaki kaynak varsa kod/ref/sayfa bilgisini ayrıca belirt.\n"
            "- Kesin uyumluluk iddiası verme; uyumluluk için katalog/model verisine ihtiyaç olduğunu söyle.\n"
        )
    if normalized == "PHOTO_GUIDANCE":
        return (
            "AKTİF NİYET: PHOTO_GUIDANCE\n"
            "- Kullanıcıya daha iyi parça fotoğrafı çekmesi için kısa, pratik yönlendirme ver.\n"
            "- Kod/marka yazısını, yan profili, bağlantı noktalarını ve makine üzerindeki konumu istemeyi unutma.\n"
        )
    return ""


def extract_confidence_value(analysis: dict) -> float | None:
    value = (analysis or {}).get("confidence")
    if isinstance(value, (int, float)):
        confidence = float(value)
        if 0.0 <= confidence <= 1.0:
            return confidence
    return None


def parse_chat_context(context_json: str | None) -> dict:
    if not context_json:
        return {}
    try:
        parsed = json.loads(context_json)
        return parsed if isinstance(parsed, dict) else {}
    except Exception:
        return {}


def has_current_context_reference(text: str) -> bool:
    norm = normalize_for_overlap(text or "")
    hints = [
        "bu", "bunu", "bunun", "su", "sunu", "sununkini",
        "bunlar", "bunlari", "bunlarin", "bunlardan",
        "adaylar", "adaylari", "hepsi",
        "sectigim", "secilen", "tikladigim", "tiklanan", "listedeki",
    ]
    return any(re.search(rf"\b{re.escape(hint)}\b", norm) for hint in hints)
