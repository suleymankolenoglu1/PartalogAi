import aiohttp
import base64
import json
import io
import asyncio
from PIL import Image
from fastapi import APIRouter, UploadFile, File, HTTPException
from pydantic import BaseModel
from loguru import logger
from config import settings
from services.genai_provider import provider

router = APIRouter()

# 🚀 HIZ AYARI
CONCURRENCY_LIMIT = asyncio.Semaphore(10)

# ✅ GÜVENLİK: Yanıt Şeması
class PageAnalysisResponse(BaseModel):
    is_technical_drawing: bool
    is_parts_list: bool
    title: str

@router.post("/analyze-page-title", response_model=PageAnalysisResponse)
async def analyze_page_title(file: UploadFile = File(...)):
    async with CONCURRENCY_LIMIT:
        try:
            image_bytes = await file.read()
            image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
            
            # Analiz için 1024px yeterli
            image.thumbnail((1024, 1024)) 
            buffered = io.BytesIO()
            image.save(buffered, format="JPEG", quality=85) 
            base64_image = base64.b64encode(buffered.getvalue()).decode("utf-8")

            # 🧠 HASSAS PROMPT
            prompt_text = """
            You are a spare parts catalog analyzer. Look at this page image carefully.

            TASK 1: CLASSIFY (True/False)
            - "is_technical_drawing": MUST be True ONLY if the page contains a schematic, exploded view, or diagram with numbered parts. If it is just a text list, this MUST be False.
            - "is_parts_list": MUST be True if the page contains a data table (Ref, Code, Qty).

            TASK 2: EXTRACT TITLE (Crucial)
            - Find the specific component group name (e.g., "NEEDLE BAR COMPONENTS", "MAIN SHAFT", "FRAME ASSEMBLY").
            - TRANSLATE it into TURKISH UPPERCASE (e.g., "İĞNE MİLİ BİLEŞENLERİ").
            - RULE: Do NOT return generic titles like "Teknik Resim", "Figure", or "Table". Return the specific name of the mechanism shown.
            - If no title is found on the page, return "GENEL PARÇALAR".

            OUTPUT JSON:
            {
              "is_technical_drawing": boolean,
              "is_parts_list": boolean,
              "title": "TURKISH_TITLE_HERE"
            }
            """

            payload = provider.normalize_generate_payload({
                "contents": [{
                    "parts": [
                        {"text": prompt_text},
                        {"inline_data": {"mime_type": "image/jpeg", "data": base64_image}}
                    ]
                }],
                "generationConfig": { "response_mime_type": "application/json" }
            })

            url = provider.generate_content_url(settings.GEMINI_ANALYSIS_MODEL)
            headers = await provider.build_headers()
            async with aiohttp.ClientSession(headers=headers) as session:
                async with session.post(url, json=payload) as response:
                    if response.status != 200:
                        error_body = await response.text()
                        logger.error(f"AI API Hatası ({response.status}): {error_body}")
                        raise HTTPException(
                            status_code=502,
                            detail=f"AI page analysis upstream failed: {response.status}"
                        )

                    result_json = await response.json()
                    
                    if "candidates" not in result_json or not result_json["candidates"]:
                        logger.error(f"AI page analysis aday döndürmedi: {result_json}")
                        raise HTTPException(
                            status_code=502,
                            detail="AI page analysis returned no candidates"
                        )

                    raw_text = result_json["candidates"][0]["content"]["parts"][0]["text"]
                    clean_text = raw_text.replace("```json", "").replace("```", "").strip()
                    
                    # 🔥 GÜVENLİ JSON PARSE İŞLEMİ 🔥
                    try:
                        data = json.loads(clean_text)
                        
                        # Eğer AI liste döndürürse ([{...}]), ilk elemanı al
                        if isinstance(data, list):
                            if len(data) > 0:
                                data = data[0]
                            else:
                                data = {} # Boş liste gelirse
                    except json.JSONDecodeError:
                        logger.error(f"JSON Parse Hatası: {clean_text}")
                        data = {}
                    
                    # Pydantic ile doğrulayıp dönüyoruz
                    return PageAnalysisResponse(
                        is_technical_drawing=data.get("is_technical_drawing", False),
                        is_parts_list=data.get("is_parts_list", False),
                        title=data.get("title", "GENEL GÖRÜNÜM")
                    )

        except HTTPException:
            raise
        except Exception as e:
            logger.error(f"Sistem Hatası: {e}")
            raise HTTPException(status_code=500, detail="AI page analysis failed")
