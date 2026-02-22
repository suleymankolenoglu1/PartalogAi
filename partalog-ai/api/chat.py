"""
Partalog AI - Chat API (Final v4.2 - Turkish Native Mode + Hybrid Search 🇹🇷)
------------------------------------------------
1. NO DICTIONARY: Sözlük iptal. "SCREW" yok, "VİDA" var.
2. HYBRID SEARCH: Parça kodu varsa ÖNCE tam eşleşme, yoksa Vektör aranır.
3. SMART ROUTER: Marka, Parça ismi, KOD ve ÖLÇÜ(Dimensions) ayıklar.
4. MULTI-PART: Birden fazla parça istenirse "parts" listesi döndürür.
"""

import aiohttp
import base64
import json
import os
import re
import urllib.parse
import uuid
from datetime import datetime, timezone
from pathlib import Path
from fastapi import APIRouter, Form, File, UploadFile
from loguru import logger
from config import settings

# ✅ Gerekli Servisler (exact_match_search eklendi)
from services.embedding import get_text_embedding 
from services.vector_db import search_vector_db, exact_match_search

router = APIRouter()

# ⚡️ Gemini API
GEMINI_API_URL = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={settings.GEMINI_API_KEY}"
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

    prompt = f"""
    You are a spare-parts visual analyst for industrial machines.
    Analyze the uploaded part photo and return JSON ONLY:
    {{
      "candidate_part_name": "string or null",
      "detected_brand_text": "string or null",
      "machine_type_hint": "string or null",
      "questions_for_user": ["question1", "question2", "question3"],
      "confidence": 0.0
    }}
    Rules:
    - Read any visible text/brand from the image.
    - If uncertain, keep confidence low.
    - Ask practical follow-up questions to identify exact part.
    User hint: {user_hint or "none"}
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
            async with session.post(GEMINI_API_URL, json=payload) as resp:
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
async def analyze_intent_with_gemini(text: str) -> dict:
    system_prompt = """
    GÖREV: Bir sanayi yedek parça asistanı olarak kullanıcı mesajını analiz et.
    
    ÇIKTI FORMATI (JSON):
    {
        "intent": "SEARCH" | "CHAT" | "PRICE" | "STOCK" | "COMPATIBILITY" | "HELP" | "COMPARE",
        "brand": "Marka Varsa Buraya (TYPICAL, JUKI, YAMATO, PEGASUS, BROTHER...)",
        "part_name": "Aranan Parçanın SAF TÜRKÇE ADI (Sıfatları ve ölçüleri at, kök ismi bul)",
        "part_code": "Parça kodu BİREBİR geçiyorsa buraya (örn: B2424-354-000, 110-40056)",
        "dimensions": "Sorgudaki ölçü, metrik veya ebat bilgisi (örn: 5mm, 3/16, M3, 10x20)",
        "parts": [
          {"part_name": "...", "part_code": null, "dimensions": null},
          {"part_name": "...", "part_code": null, "dimensions": null}
        ],
        "machine_group": "Makine Grubu (Reçme, Overlok, Düz...)",
        "confidence": 0.0-1.0
    }

    KURALLAR:
    1. ASLA İngilizceye çevirme. Kullanıcı "Vida" dediyse "VİDA" al. "SCREW" DEME!
    2. Gereksiz kelimeleri at.
    3. ÖLÇÜ NORMALİZASYONU: "5 mm", "5milimetre" gibi ifadeleri "dimensions" alanına "5mm" olarak temizle.
    4. KOD TESPİTİ: Harf/Rakam karışık spesifik bir şey yazıldıysa (örn: S08084-001) bunu kesinlikle "part_code" alanına al!
    5. Birden fazla parça varsa "parts" listesine hepsini koy.
    """
    payload = {
        "contents": [{"parts": [{"text": system_prompt + f"\n\nKULLANICI MESAJI: {text}"}]}],
        "generationConfig": {"response_mime_type": "application/json"}
    }
    
    try:
        async with aiohttp.ClientSession() as session:
            async with session.post(GEMINI_API_URL, json=payload) as resp:
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
# 🧠 ANA CHAT ENDPOINT (HİBRİT ARAMA EKLENDİ)
# =========================================================
@router.post("/send")
@router.post("/expert-chat")
async def chat_endpoint(
    text: str = Form(None),   
    message: str = Form(None),
    history: str = Form("[]"),
    catalog_ids: str = Form("[]"),
    file: UploadFile = File(None),
):
    try:
        user_query = text if text else message
        if not user_query and not file:
            return {"answer": "Boş mesaj.", "reply": "Boş mesaj.", "sources": [], "debug_intent": None}
        if not user_query and file:
            user_query = "Yüklenen görseldeki parçayı analiz et."

        logger.info(f"📨 [GİRİŞ] Mesaj: {user_query}")

        try:
            catalog_ids_list = json.loads(catalog_ids) or []
        except Exception:
            catalog_ids_list = []

        # 1. ANALİZ ET (Router)
        analysis = await analyze_intent_with_gemini(user_query)
        
        intent = analysis.get("intent", "CHAT")
        extracted_brand = analysis.get("brand")
        extracted_part = analysis.get("part_name")
        extracted_code = analysis.get("part_code")
        extracted_dim = analysis.get("dimensions")
        
        image_analysis = {}

        if file is not None:
            image_bytes = await file.read()
            image_analysis = await analyze_image_with_gemini(image_bytes, user_query)
            analysis["image_analysis"] = image_analysis

            img_part = image_analysis.get("candidate_part_name")
            img_brand = image_analysis.get("detected_brand_text")
            if (not extracted_part) and img_part:
                extracted_part = img_part
                analysis["part_name"] = img_part
                if intent == "CHAT":
                    intent = "SEARCH"
                    analysis["intent"] = "SEARCH"
            if (not extracted_brand) and img_brand:
                extracted_brand = img_brand
                analysis["brand"] = img_brand

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
                return {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}

            return {
                "answer": "Aleykümselam ustam. Hangi parçayı arıyorsun? Marka, kod veya parça adı söyle, hemen depoya bakayım.",
                "reply": "Buyur ustam?",
                "sources": [],
                "debug_intent": analysis
            }

        # ✅ Multi-part & Hybrid Search
        all_sources = []
        
        # Eğer tek parça varsa ya da liste varsa hepsini dön (Hybrid Mantığı)
        for part in parts:
            p_code = part.get("part_code")
            p_name = part.get("part_name")
            p_dim = part.get("dimensions")
            
            part_results = []
            
            # HİBRİT ADIM 1: EXACT MATCH (TAM EŞLEŞME)
            if p_code:
                logger.info(f"🔍 Kod tespit edildi ({p_code}). Exact Match aranıyor...")
                part_results = await exact_match_search(p_code, extracted_brand, catalog_ids_list)
            
            # HİBRİT ADIM 2: VECTOR SEARCH (Eğer kod yoksa veya kodla bulunamadıysa)
            if not part_results and p_name:
                logger.info(f"🧠 Kod yok veya bulunamadı. Vektör (Semantic) aranıyor: {p_name}")
                # Vektör gücünü arttırmak için ölçüyü de ekle
                search_query = f"{p_name} {p_dim}" if p_dim else p_name
                query_vector = get_text_embedding(search_query)
                
                if query_vector:
                    part_results = await search_vector_db(
                        query_vector, 
                        brand_filter=extracted_brand, 
                        limit=5,
                        catalog_ids=catalog_ids_list
                    )

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
                    all_sources.append({
                        "code": p_code_db,
                        "name": p_name_db,
                        "brand": p_brand_db,
                        "buy_url": buy_link,
                        "machine_model": p_model_db,
                        "description": p_desc_db,
                        "query": p_name or p_code
                    })

        logger.success(f"📦 Toplam Bulunan Benzersiz Sonuç: {len(all_sources)}")

        if not all_sources:
            msg = "Ustam, veritabanında bu parçaya uygun bir sonuç bulamadım. Marka veya kod doğru mu?"
            return {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}

        # 4. Gemini'ye verilecek Context Metni
        context_lines = []
        for s in all_sources[:10]: # Gemini'ye çok yüklenmemek için ilk 10
            line = f"- Marka: {s['brand']} | Model: {s['machine_model']} | Parça: {s['name']} (Kod: {s['code']}) | Detay: {s['description']}"
            context_lines.append(line)
            
        context_text = "\n".join(context_lines)

        # 5. FİNAL CEVAP
        final_prompt = f"""
        Sen sanayi yedek parça uzmanısın (Partalog AI).
        
        KULLANICI SORUSU: "{user_query}"
        
        DEPODAN BULDUĞUN PARÇALAR:
        {context_text}
        
        GÖREV:
        1. Kullanıcıya bulduğun parçaları listele.
        2. Marka, Model ve özellikle ölçü (varsa) uyumuna dikkat çek.
        3. Samimi, kısa ve öz, usta ağzıyla konuş.
        4. Link verme, zaten sistem gösterecek.
        """

        async with aiohttp.ClientSession() as session:
            payload = {"contents": [{"parts": [{"text": final_prompt}]}]}
            async with session.post(GEMINI_API_URL, json=payload) as resp:
                if resp.status == 200:
                    ai_reply = (await resp.json())["candidates"][0]["content"]["parts"][0]["text"]
                else:
                    ai_reply = "Sonuçlar yukarıda listelendi ustam."

        return {
            "answer": ai_reply,
            "reply": ai_reply,
            "sources": all_sources,
            "debug_intent": analysis
        }

    except Exception as e:
        logger.error(f"Chat Hatası: {e}")
        return {
            "answer": "Sistemsel bir hata oluştu ustam.",
            "reply": "Hata",
            "sources": [],
            "debug_intent": None
        }


@router.post("/visual-feedback")
async def visual_feedback_endpoint(
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

        return {
            "success": True,
            "message": "Kullanıcı geri bildirimi kaydedildi.",
            "record": record,
        }
    except Exception as e:
        logger.error(f"visual-feedback error: {e}")
        return {"success": False, "message": "Geri bildirim kaydedilemedi."}