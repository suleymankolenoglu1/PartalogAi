"""Persistence helpers for user visual feedback samples."""

import json
import os
import re
import uuid
from datetime import datetime, timezone
from pathlib import Path

from loguru import logger

from services.chat_visual import analyze_image_with_gemini, build_visual_hint_text
from services.embedding import get_text_embedding
from services.vector_db import update_visual_embedding_in_db


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
    file_path = part_dir / f"{file_id}{ext}"
    with open(file_path, "wb") as handle:
        handle.write(file_bytes)
    rel_path = file_path.as_posix()
    static_path = f"/{rel_path}" if not rel_path.startswith("/") else rel_path
    return {
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


def append_user_feedback_index(record: dict) -> None:
    USER_FEEDBACK_INDEX.parent.mkdir(parents=True, exist_ok=True)
    with open(USER_FEEDBACK_INDEX, "a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, ensure_ascii=False) + "\n")


def save_user_feedback_analysis(
    *,
    record: dict,
    visual_analysis: dict,
    embedding_text: str | None,
) -> dict | None:
    image_path = record.get("image_path")
    if not image_path or not isinstance(visual_analysis, dict):
        return None
    analysis_path = Path(image_path).with_suffix(".analysis.json")
    payload = {
        "feedback_id": record.get("id"),
        "created_at": datetime.now(timezone.utc).isoformat(),
        "part_name": record.get("part_name"),
        "part_code": record.get("part_code"),
        "embedding_text": embedding_text,
        "visual_analysis": visual_analysis,
        "source": "chat_user_feedback_analysis",
    }
    try:
        analysis_path.parent.mkdir(parents=True, exist_ok=True)
        with open(analysis_path, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=2)
        record["visual_analysis_path"] = analysis_path.as_posix()
        record["visual_hints"] = {
            "part_family": visual_analysis.get("part_family"),
            "shape_traits": visual_analysis.get("shape_traits") or visual_analysis.get("shape_tags") or [],
            "assembly_hint": visual_analysis.get("assembly_hint"),
            "visible_code_tokens": visual_analysis.get("visible_code_tokens") or [],
            "brand_model_tokens": visual_analysis.get("brand_model_tokens") or [],
        }
        return payload
    except Exception as exc:
        logger.warning(f"Visual feedback analysis kaydedilemedi: {exc}")
        return None


async def process_visual_feedback(
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
    record = save_user_feedback_sample(
        file_bytes=file_bytes,
        original_filename=original_filename,
        part_name=part_name,
        part_code=part_code,
        machine_brand=machine_brand,
        machine_type=machine_type,
        user_id=user_id,
        note=note,
    )

    visual_embedding_saved = False
    try:
        vlm_result = await analyze_image_with_gemini(file_bytes, user_hint=part_name or "")
        embedding_text = vlm_result.get("embedding_text") or build_visual_hint_text(vlm_result)
        save_user_feedback_analysis(
            record=record,
            visual_analysis=vlm_result,
            embedding_text=embedding_text,
        )

        if not embedding_text:
            parts_text = " ".join(filter(None, [part_name, part_code, machine_brand, machine_type]))
            embedding_text = parts_text if parts_text else None

        if embedding_text:
            visual_vector = await get_text_embedding(embedding_text)
            if visual_vector and part_code:
                saved = await update_visual_embedding_in_db(
                    part_code=part_code,
                    visual_vector=visual_vector,
                    visual_image_url=record.get("image_url"),
                    visual_shape_tags=vlm_result.get("shape_traits") or vlm_result.get("shape_tags"),
                    visual_ocr_text=vlm_result.get("visible_codes"),
                )
                visual_embedding_saved = saved
                if saved:
                    logger.success(f"✅ VisualEmbedding güncellendi: {part_code}")
                else:
                    logger.warning(f"⚠️ VisualEmbedding yazılamadı (part_code DB'de bulunamadı?): {part_code}")
    except Exception as exc:
        logger.error(f"VisualEmbedding güncelleme hatası: {exc}")

    append_user_feedback_index(record)
    return {
        "success": True,
        "message": "Kullanıcı geri bildirimi kaydedildi.",
        "visual_embedding_saved": visual_embedding_saved,
        "record": record,
    }
