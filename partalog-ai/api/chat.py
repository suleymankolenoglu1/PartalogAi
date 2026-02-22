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
import re
import urllib.parse
import uuid
from PIL import Image
from datetime import datetime, timezone
from pathlib import Path
from fastapi import APIRouter, Form, File, UploadFile
from loguru import logger
from config import settings

# ✅ Gerekli Servisler (exact_match_search eklendi)
from services.embedding import get_text_embedding 
from services.vector_db import search_vector_db, exact_match_search, search_visual_vector_db, update_visual_embedding_in_db

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
                analyze_image_with_gemini(image_bytes, user_query),
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
                return {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis}

            return {
                "answer": "Aleykümselam ustam. Hangi parçayı arıyorsun? Marka, kod veya parça adı söyle, hemen depoya bakayım.",
                "reply": "Buyur ustam?",
                "sources": [],
                "debug_intent": analysis
            }

        # ✅ Multi-part & Hybrid Search
        all_sources = []

        # --- YENİ: VISUAL SEARCH (VisualEmbedding dolu parçalarda önce ara) ---
        visual_sources = []
        if file is not None and image_analysis:
            embedding_text_for_search = image_analysis.get("embedding_text")
            visible_codes_from_img = image_analysis.get("visible_codes")

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
                        )
                        logger.info(f"🖼️ Visual Search (≥0.60): {len(visual_results)} sonuç")

                    # ADIM 3: Hâlâ yok ise normal Embedding araması yap
                    if not visual_results:
                        logger.info("🖼️ Visual Search tamamen başarısız. Normal Embedding aramasına fallback...")
                        text_fallback_vector = await get_text_embedding(embedding_text_for_search)
                        if text_fallback_vector:
                            text_fallback_results = await search_vector_db(
                                query_vector=text_fallback_vector,
                                brand_filter=extracted_brand,
                                limit=5,
                                catalog_ids=catalog_ids_list,
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
                                "visual_similarity": vr.get("visual_similarity"),
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
            
            # HİBRİT ADIM 1: EXACT MATCH (TAM EŞLEŞME)
            if p_code:
                logger.info(f"🔍 Kod tespit edildi ({p_code}). Exact Match aranıyor...")
                part_results = await exact_match_search(p_code, extracted_brand, catalog_ids_list)
            
            # HİBRİT ADIM 2: VECTOR SEARCH (Eğer kod yoksa veya kodla bulunamadıysa)
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

        # 5. FİNAL CEVAP
        final_prompt = f"""
Sen sanayi yedek parça uzmanısın (Partalog AI). Kısa, samimi, usta ağzıyla konuş.

{"SOHBET GEÇMİŞİ (bağlam için kullan):" + chr(10) + history_text + chr(10) if history_text else ""}
ŞİMDİKİ KULLANICI SORUSU: "{user_query}"

DEPODAN BULDUĞUN PARÇALAR:
{context_text}

GÖREV:
1. Bulduğun parçaları özetle. Marka, model, ölçü uyumuna dikkat çek.
2. Sohbet geçmişindeki bağlamı kullan — kullanıcı "bu parçanın fiyatı ne?" derse hangi parçadan bahsettiğini geçmişten anla.
3. Link verme, sistem zaten gösterecek.
4. Maksimum 3-4 cümle yaz.
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