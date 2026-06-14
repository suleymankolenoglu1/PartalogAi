"""Visual retrieval orchestration for chat requests."""

from collections.abc import Callable

from loguru import logger

from services.chat_sources import append_unique_sources, build_visual_source
from services.embedding import get_text_embedding
from services.vector_db import (
    exact_match_search,
    search_by_visual_hints,
    search_vector_db,
    search_visual_vector_db,
)


def _empty_debug() -> dict:
    return {
        "used": False,
        "ocr_code_results": 0,
        "visual_embedding_results": 0,
        "structured_hint_results": 0,
        "text_fallback_results": 0,
        "top_reason": None,
    }


async def run_visual_search(
    image_analysis: dict,
    *,
    extracted_brand: str | None,
    catalog_ids_list: list,
    extracted_machine_group: str | None,
    shop_base_url: str,
    build_visual_hint_text: Callable[[dict | None], str],
    as_clean_text_list: Callable[[object], list[str]],
    rerank_results_by_visual_hints: Callable[[list[dict], dict | None], list[dict]],
) -> tuple[list[dict], dict]:
    visual_search_debug = _empty_debug()
    if not image_analysis:
        return [], visual_search_debug

    visual_search_debug["used"] = True
    embedding_text_for_search = image_analysis.get("embedding_text") or build_visual_hint_text(image_analysis)
    visible_codes_from_img = image_analysis.get("visible_codes")
    visible_code_tokens = as_clean_text_list(image_analysis.get("visible_code_tokens") or visible_codes_from_img)
    visual_query_vector = None
    structured_hint_search_attempted = False
    visual_sources: list[dict] = []

    visual_results = []
    for code_token in visible_code_tokens[:4]:
        code_results = await exact_match_search(
            code_token,
            brand_filter=extracted_brand,
            catalog_ids=catalog_ids_list,
            limit=5,
            machine_group_filter=extracted_machine_group,
        )
        for result in code_results:
            result["visual_similarity"] = 1.0
            result["visual_match"] = True
            result["visual_match_reason"] = "ocr_code"
        visual_results.extend(code_results)

    if visual_results:
        visual_search_debug["ocr_code_results"] = len(visual_results)
        visual_search_debug["top_reason"] = "ocr_code"
        logger.info(f"🔍 Görsel OCR/kod araması: {len(visual_results)} sonuç")

    if embedding_text_for_search:
        visual_query_vector = await get_text_embedding(embedding_text_for_search)

        if visual_query_vector:
            if not visual_results:
                visual_results = await search_visual_vector_db(
                    query_vector=visual_query_vector,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    min_similarity=0.78,
                    machine_group_filter=extracted_machine_group,
                )
                visual_results = rerank_results_by_visual_hints(visual_results, image_analysis)
                visual_search_debug["visual_embedding_results"] = len(visual_results)
                if visual_results:
                    visual_search_debug["top_reason"] = "visual_embedding_high"
                logger.info(f"🖼️ Visual Search (≥0.78): {len(visual_results)} sonuç")

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
                visual_results = rerank_results_by_visual_hints(visual_results, image_analysis)
                visual_search_debug["visual_embedding_results"] = len(visual_results)
                if visual_results:
                    visual_search_debug["top_reason"] = "visual_embedding_low"
                logger.info(f"🖼️ Visual Search (≥0.60): {len(visual_results)} sonuç")

            if not visual_results:
                logger.info("🧩 Structured visual hint search çalışıyor...")
                structured_hint_search_attempted = True
                visual_results = await search_by_visual_hints(
                    image_analysis,
                    brand_filter=extracted_brand,
                    limit=8,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                )
                visual_results = rerank_results_by_visual_hints(visual_results, image_analysis)
                for result in visual_results:
                    score = result.get("visual_hint_score")
                    result["visual_similarity"] = min(0.69, 0.46 + (float(score or 0) * 0.04))
                    result["visual_match"] = False
                    result["visual_match_reason"] = "structured_hints"
                visual_search_debug["structured_hint_results"] = len(visual_results)
                if visual_results:
                    visual_search_debug["top_reason"] = "structured_hints"
                logger.info(f"🧩 Structured visual hint search: {len(visual_results)} sonuç")

            if not visual_results and not structured_hint_search_attempted:
                logger.info("🧩 Structured visual hint search çalışıyor (embedding yok/başarısız)...")
                visual_results = await search_by_visual_hints(
                    image_analysis,
                    brand_filter=extracted_brand,
                    limit=8,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                )
                visual_results = rerank_results_by_visual_hints(visual_results, image_analysis)
                for result in visual_results:
                    score = result.get("visual_hint_score")
                    result["visual_similarity"] = min(0.69, 0.46 + (float(score or 0) * 0.04))
                    result["visual_match"] = False
                    result["visual_match_reason"] = "structured_hints"
                visual_search_debug["structured_hint_results"] = len(visual_results)
                if visual_results:
                    visual_search_debug["top_reason"] = "structured_hints"
                logger.info(f"🧩 Structured visual hint search: {len(visual_results)} sonuç")

            if not visual_results:
                logger.info("🖼️ Visual Search tamamen başarısız. Normal Embedding aramasına fallback...")
                text_fallback_results = await search_vector_db(
                    query_vector=visual_query_vector,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                )
                for result in text_fallback_results:
                    result["visual_similarity"] = result.get("similarity", 0)
                    result["visual_match"] = False
                visual_results = text_fallback_results
                visual_search_debug["text_fallback_results"] = len(visual_results)
                if visual_results:
                    visual_search_debug["top_reason"] = "text_embedding_fallback"
                logger.info(f"📝 Text Embedding fallback: {len(visual_results)} sonuç")

            append_unique_sources(
                visual_sources,
                [
                    build_visual_source(
                        result,
                        query=embedding_text_for_search,
                        shop_base_url=shop_base_url,
                    )
                    for result in visual_results
                ],
            )
            if visual_sources:
                logger.success(f"🖼️ Visual Search toplam {len(visual_sources)} eşleşme bulundu!")

    if not visual_results and not structured_hint_search_attempted:
        logger.info("🧩 Structured visual hint search çalışıyor (embedding üretilemedi ya da atlandı)...")
        visual_results = await search_by_visual_hints(
            image_analysis,
            brand_filter=extracted_brand,
            limit=8,
            catalog_ids=catalog_ids_list,
            machine_group_filter=extracted_machine_group,
        )
        visual_results = rerank_results_by_visual_hints(visual_results, image_analysis)
        for result in visual_results:
            score = result.get("visual_hint_score")
            result["visual_similarity"] = min(0.69, 0.46 + (float(score or 0) * 0.04))
            result["visual_match"] = False
            result["visual_match_reason"] = "structured_hints"
        visual_search_debug["structured_hint_results"] = len(visual_results)
        if visual_results:
            visual_search_debug["top_reason"] = "structured_hints"
        logger.info(f"🧩 Structured visual hint search: {len(visual_results)} sonuç")

    if visual_results and not visual_sources:
        append_unique_sources(
            visual_sources,
            [
                build_visual_source(
                    result,
                    query=embedding_text_for_search,
                    shop_base_url=shop_base_url,
                )
                for result in visual_results
            ],
        )
        if visual_sources:
            logger.success(f"🖼️ Visual Search toplam {len(visual_sources)} eşleşme bulundu!")

    visual_search_debug["source_count"] = len(visual_sources)
    return visual_sources, visual_search_debug
