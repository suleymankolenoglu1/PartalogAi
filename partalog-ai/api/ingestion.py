from __future__ import annotations

from fastapi import APIRouter, HTTPException
from loguru import logger
from pydantic import BaseModel

from services.search_text_builder import build_catalog_item_search_texts

router = APIRouter()


class IngestionSearchTextRow(BaseModel):
    part_name: str | None = None
    machine_brand_model: str | None = None
    machine_brand: str | None = None
    machine_model: str | None = None
    machine_group: str | None = None
    category: str | None = None
    description: str | None = None
    part_code: str | None = None
    ref_no: str | None = None
    dimensions: str | None = None
    mechanism: str | None = None


@router.post("/build-search-texts", response_model=list[str])
async def build_search_texts_endpoint(rows: list[IngestionSearchTextRow]) -> list[str]:
    try:
        payload = [
            row.model_dump() if hasattr(row, "model_dump") else row.dict()
            for row in rows
        ]
        return build_catalog_item_search_texts(payload)
    except Exception as exc:
        logger.error("Ingestion search text build failed: {}", exc)
        raise HTTPException(status_code=500, detail="Search text üretilemedi.") from exc
