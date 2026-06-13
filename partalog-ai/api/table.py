"""
Table API - Gemini 2.0 Flash (Industrial Turkish Translation Mode 🇹🇷)
Görevi: Resmi okur, markayı bulur.
ÖZELLİK: Kaynak dil ne olursa olsun (Çince, Japonca, İngilizce) veriyi SANAYİ TÜRKÇESİNE çevirir.
"""

import aiohttp
import base64
import json
import io
import asyncio
import fitz  # ✅ PDF render
import re
from PIL import Image
from fastapi import APIRouter, UploadFile, File, Query, HTTPException
from pydantic import BaseModel, Field
from typing import List, Optional
from loguru import logger
import time
from config import settings
from services.genai_provider import provider

router = APIRouter()

# --- Modeller ---
class ProductResult(BaseModel):
    ref_number: str = Field(default="0")
    part_code: str
    part_name: str = Field(default="PARÇA")
    description: str = Field(default="")
    quantity: int = Field(default=1)
    dimensions: Optional[str] = None

class TableResult(BaseModel):
    row_count: int
    products: List[ProductResult]

class TableExtractionResponse(BaseModel):
    success: bool
    message: str
    total_products: int
    tables: List[TableResult]
    page_number: int = 0
    processing_time_ms: float = 0

class MetadataResponse(BaseModel):
    machine_model: str
    machine_brand: Optional[str] = None
    machine_group: str = "General"
    catalog_title: str


_FASTENER_KEYWORDS = (
    "VİDA",
    "SOMUN",
    "PUL",
    "CİVATA",
    "CIVATA",
)

_FASTENER_SOURCE_KEYWORDS = _FASTENER_KEYWORDS + (
    "SCREW",
    "BOLT",
    "NUT",
    "WASHER",
)

_DIMENSION_PATTERN = re.compile(
    r"(?i)\b(?:M\d+(?:[.,]\d+)?(?:-\d+(?:[.,]\d+)?)?(?:[xX]\d+(?:[.,]\d+)?)?|\d+(?:[.,]\d+)?(?:[xX]\d+(?:[.,]\d+)?)|\d+/\d+|\d+(?:[.,]\d+)?\s*(?:mm|cm|in|inch|\"|'))\b"
)
_TURKISH_UPPER_MAP = str.maketrans({"i": "İ", "ı": "I"})


def _turkish_upper(text: str) -> str:
    return text.translate(_TURKISH_UPPER_MAP).upper()


def _canonicalize_dimension_token(token: str) -> str:
    cleaned = token.strip(" ,;:()[]{}")
    if not cleaned:
        return ""

    cleaned = re.sub(r"\s+", "", cleaned)
    cleaned = re.sub(r"(?i)^m", "M", cleaned)
    cleaned = re.sub(r"[xX]", "x", cleaned)
    cleaned = re.sub(r"(?i)(mm|cm|in|inch)$", lambda m: m.group(1).lower(), cleaned)
    return cleaned


def _extract_dimension_candidates(text: str) -> List[str]:
    if not text:
        return []

    candidates = []
    for match in _DIMENSION_PATTERN.finditer(text):
        token = _canonicalize_dimension_token(match.group(0))
        if token:
            candidates.append(token)
    return candidates


def _best_dimension_candidate(*texts: Optional[str]) -> Optional[str]:
    seen = set()
    candidates: List[str] = []

    for text in texts:
        for token in _extract_dimension_candidates(text or ""):
            if token not in seen:
                seen.add(token)
                candidates.append(token)

    if not candidates:
        return None

    return max(candidates, key=len)


def _is_fastener_text(*texts: Optional[str]) -> bool:
    haystack = " ".join((text or "").upper() for text in texts if text)
    return any(keyword in haystack for keyword in _FASTENER_SOURCE_KEYWORDS)


def _extract_dimensions_from_name(name: str) -> Optional[str]:
    return _best_dimension_candidate(name)


def _normalize_product_item(item: dict) -> Optional[ProductResult]:
    p_code = str(item.get("part_code") or "0").strip()
    if len(p_code) < 3:
        return None

    raw_name = str(item.get("part_name") or "").strip()
    if not raw_name:
        raw_name = p_code

    source_name = str(item.get("source_name") or item.get("original_name") or "").strip()
    description = str(item.get("remarks") or "").strip()

    dims = _best_dimension_candidate(
        str(item.get("dimensions") or ""),
        raw_name,
        source_name,
        description,
    )

    raw_name_upper = _turkish_upper(raw_name)

    if dims and _is_fastener_text(raw_name_upper, source_name, description):
        if dims.upper() not in raw_name_upper:
            raw_name_upper = f"{raw_name_upper} {dims}".strip()

    return ProductResult(
        ref_number=str(item.get("ref_no") or "0"),
        part_code=p_code,
        part_name=raw_name_upper,
        description=description,
        quantity=1,
        dimensions=dims
    )

# --- Endpoints ---

@router.post("/extract-metadata", response_model=MetadataResponse)
async def extract_metadata(file: UploadFile = File(...)):
    logger.info("🔍 [METADATA] Kapak analizi (Zeka Modu) isteği geldi...")
    try:
        content = await file.read()
        image = Image.open(io.BytesIO(content)).convert("RGB")
        image.thumbnail((1024, 1024))
        buffered = io.BytesIO()
        image.save(buffered, format="JPEG", quality=90)
        base64_image = base64.b64encode(buffered.getvalue()).decode("utf-8")

        prompt = """
        You are an expert industrial sewing machine technician.
        Analyze this catalog cover image.
        
        TASK:
        1. Identify BRAND (JUKI, PEGASUS, YAMATO, TYPICAL, BROTHER, SIRUBA, JACK etc.)
        2. Identify MODEL (e.g. MF-7900, GK335)
        3. Identify MACHINE GROUP (Lockstitch, Overlock, Coverstitch, Chainstitch, Bartack, Buttonhole, General)

        Return JSON:
        { "machine_model": "...", "machine_brand": "...", "machine_group": "...", "catalog_title": "..." }
        """

        payload = provider.normalize_generate_payload({
            "contents": [{"parts": [{"text": prompt}, {"inline_data": {"mime_type": "image/jpeg", "data": base64_image}}]}],
            "generationConfig": {"response_mime_type": "application/json", "temperature": 0.3}
        })

        url = provider.generate_content_url(settings.GEMINI_TABLE_MODEL)
        headers = await provider.build_headers()
        async with aiohttp.ClientSession(headers=headers) as session:
            async with session.post(url, json=payload) as response:
                if response.status == 200:
                    res = await response.json()
                    candidates = res.get("candidates", [])
                    if candidates:
                        txt = candidates[0]["content"]["parts"][0]["text"]
                        clean_txt = txt.replace("```json", "").replace("```", "").strip()
                        data = json.loads(clean_txt)

                        machine_group = data.get("machine_group") or "General"

                        return MetadataResponse(
                            machine_model=data.get("machine_model", "Unknown"),
                            machine_brand=data.get("machine_brand"),
                            machine_group=machine_group,
                            catalog_title=data.get("catalog_title", "Unknown Catalog")
                        )

        return MetadataResponse(machine_model="Unknown", catalog_title="Error")
    except Exception as e:
        logger.error(f"Metadata Error: {e}")
        return MetadataResponse(machine_model="Error", catalog_title="Error")


@router.post("/extract", response_model=TableExtractionResponse)
async def extract_table(
    file: UploadFile = File(...),
    page_number: int = Query(default=1)
):
    start_time = time.time()
    logger.info(f"📄 [GEMINI] Tablo Okunuyor ve Türkçeye Çevriliyor: Sayfa {page_number}")
    
    try:
        content = await file.read()
        image = None

        # ✅ PDF mi?
        if content[:4] == b"%PDF":
            doc = fitz.open(stream=content, filetype="pdf")
            if page_number < 1 or page_number > doc.page_count:
                logger.error("❌ Sayfa numarası geçersiz")
                return _empty_response("Geçersiz sayfa")

            page = doc.load_page(page_number - 1)
            pix = page.get_pixmap(dpi=200)
            image = Image.frombytes("RGB", [pix.width, pix.height], pix.samples)
        else:
            # ✅ Görsel (jpg/png) ise direkt aç
            image = Image.open(io.BytesIO(content)).convert("RGB")

        image.thumbnail((1500, 1500))
        buffered = io.BytesIO()
        image.save(buffered, format="JPEG", quality=95)
        base64_image = base64.b64encode(buffered.getvalue()).decode("utf-8")

    except Exception as e:
        logger.error(f"❌ Resim hatası: {e}")
        return _empty_response()

    prompt_text = """
    You are Sewing Machine expert,Analyze this Sewing Machine Parts Catalog page. Extract the table into JSON.

    ROLE: You are an expert Turkish Industrial Sewing Machine Technician (40 years experience).

    🚨 CRITICAL TRANSLATION RULES (STRICT INDUSTRIAL JARGON):
    1. **TARGET LANGUAGE:** TURKISH (Sanayi Dili).
    2. **NO LITERAL TRANSLATION:** Never use Google Translate style. Use the terms used in a real workshop (Atölye).
       - ❌ WRONG: "Besleme Köpeği" (Feed Dog) -> ✅ RIGHT: "DİŞLİ"
       - ❌ WRONG: "Boğaz Plakası" (Throat Plate) -> ✅ RIGHT: "PLAKA" or "AYNA"
       - ❌ WRONG: "Hareketli Bıçak" (Movable Knife) -> ✅ RIGHT: "HAREKETLİ" (Bıçak zaten anlaşılırsa) or "HAREKETLİ BIÇAK"

    3. **UNIVERSAL INPUT:** If text is Chinese, Japanese, or English: Translate to TURKISH JARGON.
       - If text is already Turkish: Keep it uppercase.

    4. **NEVER RETURN UNKNOWN:** part_name MUST always be filled.
       - If the text is unclear, still infer the most likely Turkish workshop term.
       - Do NOT output "BİLİNMEYEN PARÇA", "UNKNOWN", or empty.

    5. **JARGON MAPPING (MEMORIZE THIS):**
       - "Feed Dog" / "送料牙" -> "DİŞLİ"
       - "Looper" / "弯针" -> "LÜPER"
       - "Needle Clamp" -> "İĞNE BAĞI"
       - "Presser Foot" / "压脚" -> "AYAK"
       - "Thread Take-up" -> "HOROZ"
       - "Tension Assembly" -> "TANSİYON"
       - "Bobbin Case" -> "MEKİK"
       - "Hook" -> "ÇAĞANOZ"
       - "Screw" -> "VİDA"
       - "Nut" -> "SOMUN"
       - "Washer" -> "PUL"
       - "Crank Shaft" -> "KRANK"

    6. **FASTENER SIZES ARE CRITICAL:**
       - If the part name includes size/spec like M3, M3x5, M3-0.5x5, 3/16, 5mm, etc. KEEP IT.
       - Example: "Screw M3-0.5x5" -> "VİDA M3-0.5x5" (do not drop the size).
       - If a row mixes Turkish and English, translate the noun to Turkish jargon but preserve all spec tokens exactly.
       - Example: "vida screw M4x10" -> "VİDA M4x10"

    OUTPUT RULES:
    1. **FORMAT:** JSON List only.
    2. **FIELDS:**
       - "ref_no": Reference number.
       - "part_code": Exact part code (Remove spaces, fix OCR errors).
       - "source_name": Original row text exactly as seen (no translation). This is required for recovery if OCR/translation loses dimensions.
       - "part_name": **THE TRANSLATED TURKISH NAME** (Uppercase).
       - "dimensions": Extract measurements (M4x10, M3-0.5x5, 3/16, 5mm) to this field.
       - "qty": Quantity.

    RETURN JSON LIST ONLY. NO MARKDOWN.
    """

    payload = provider.normalize_generate_payload({
        "contents": [{
            "parts": [
                {"text": prompt_text},
                {"inline_data": {"mime_type": "image/jpeg", "data": base64_image}}
            ]
        }],
        "generationConfig": {"response_mime_type": "application/json", "temperature": 0.1}
    })

    products = []
    
    url = provider.generate_content_url(settings.GEMINI_TABLE_MODEL)
    headers = await provider.build_headers()
    async with aiohttp.ClientSession(headers=headers) as session:
        for attempt in range(3):
            try:
                async with session.post(url, json=payload) as response:
                    if response.status == 200:
                        res = await response.json()
                        if not res.get("candidates"):
                            logger.error(f"❌ [GEMINI] Aday döndürmedi (Sayfa {page_number}): {res}")
                            raise HTTPException(
                                status_code=502,
                                detail="AI table extraction returned no candidates"
                            )
                        
                        txt = res["candidates"][0]["content"]["parts"][0]["text"]
                        clean_txt = txt.replace("```json", "").replace("```", "").strip()
                        if clean_txt.endswith(",]"): clean_txt = clean_txt[:-2] + "]"
                        
                        try:
                            raw_data = json.loads(clean_txt)
                            for item in raw_data:
                                normalized_item = _normalize_product_item(item)
                                if normalized_item is None:
                                    continue
                                products.append(normalized_item)
                            logger.success(f"✅ [GEMINI] {len(products)} parça TÜRKÇELEŞTİRİLDİ (Sayfa {page_number})")
                            break
                        except json.JSONDecodeError as exc:
                            logger.warning(f"⚠️ [GEMINI] JSON parse hatası (Sayfa {page_number}, Deneme {attempt + 1}): {exc}")
                            continue
                    else:
                        error_body = await response.text()
                        logger.error(
                            f"❌ [GEMINI] Tablo okuma upstream hatası "
                            f"(Sayfa {page_number}, Status {response.status}): {error_body}"
                        )
                        if response.status in {401, 403}:
                            raise HTTPException(
                                status_code=502,
                                detail=f"AI table extraction upstream auth failed: {response.status}"
                            )
                        await asyncio.sleep(1)
            except HTTPException:
                raise
            except Exception as exc:
                logger.warning(f"⚠️ [GEMINI] Tablo okuma denemesi başarısız (Sayfa {page_number}, Deneme {attempt + 1}): {exc}")
                await asyncio.sleep(1)

    return TableExtractionResponse(
        success=True,
        message=f"Gemini {len(products)} parçayı Türkçeye çevirip buldu.",
        total_products=len(products),
        tables=[TableResult(row_count=len(products), products=products)],
        page_number=page_number,
        processing_time_ms=round((time.time() - start_time) * 1000, 2)
    )

def _empty_response(msg="Boş"):
    return TableExtractionResponse(
        success=True, message=msg, total_products=0, 
        tables=[TableResult(row_count=0, products=[])], 
        page_number=0, processing_time_ms=0
    )
