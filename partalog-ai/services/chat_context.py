"""Chat context preparation: intent analysis, retrieval routing, and prompt assembly."""

import asyncio
import json

from fastapi import UploadFile
from loguru import logger

from services.chat_intent import (
    analyze_intent_with_gemini,
    extract_confidence_value,
    get_intent_mode_block,
    has_current_context_reference,
    normalize_intent_payload,
    parse_chat_context,
    unavailable_feature_label,
)
from services.chat_matching import (
    best_similarity,
    brand_matches_available,
    extract_requested_domain_terms,
    filter_diagnosis_results,
    filter_results_by_requested_terms,
    filter_strict_part_lookup_results,
    has_lexical_overlap,
    is_strict_part_lookup,
    limit_context_related_sources,
    rerank_results_by_context_part,
)
from services.chat_memory import (
    build_active_context_sources,
    detect_brand_from_text,
    detect_machine_group_from_text,
    detect_machine_model_from_text,
    extract_sticky_context_from_history,
    model_token_matches,
    select_sticky_candidate_from_query,
)
from services.chat_parts import build_search_parts
from services.chat_policy import (
    apply_unavailable_feature_policy,
    low_confidence_response,
    resolve_confidence_thresholds_for_scope,
    route_without_search,
    source_quality_early_response,
)
from services.chat_prompt import (
    build_active_context_line,
    build_context_page_hint_block,
    build_context_text,
    build_final_prompt,
    build_history_text,
    build_machine_context_line,
    build_model_safety_line,
    build_relation_context_line,
    build_response_mode_instruction,
)
from services.chat_request import (
    normalize_user_query,
    parse_history,
    parse_json_list,
    resolve_raw_user_query,
)
from services.chat_responses import (
    build_deterministic_reply_from_sources,
    build_exact_code_reply_from_sources,
    build_no_result_guidance,
    classify_response_mode,
    should_answer_with_local_guidance,
)
from services.chat_retrieval import retrieve_part_sources
from services.chat_sources import collect_source_model_labels
from services.search_trace import build_search_trace
from services.chat_terms import (
    diagnosis_terms_for_query,
    expand_part_search_text,
    has_domain_part_keyword,
    is_general_knowledge_intent,
    normalize_for_overlap,
    related_part_terms_for_context,
    split_expanded_search_terms,
)
from services.chat_visual import (
    analyze_image_with_gemini,
    as_clean_text_list,
    build_visual_hint_text,
    rerank_results_by_visual_hints,
)
from services.chat_visual_search import run_visual_search
from services.vector_db import find_catalogs_by_machine, get_catalog_brands

SHOP_BASE_URL = "https://www.parcagalerisi.com/ara/"
TEXT_VECTOR_MIN_SIMILARITY = 0.50
WEAK_MATCH_MIN_SIMILARITY = 0.52


def looks_like_machine_catalog_question(user_query: str, intent: str | None) -> bool:
    normalized = normalize_for_overlap(user_query or "")
    if str(intent or "").upper() not in {"CHAT", "HELP", "ADVICE", "EXPLAIN_PART", "SEARCH"}:
        return False
    if has_domain_part_keyword(normalized):
        return False
    machine_hints = (
        "makine",
        "model",
        "ozellik",
        "özellik",
        "var mi",
        "var mı",
        "sende",
        "katalog",
    )
    part_lookup_hints = (
        "kodu",
        "kodunu",
        "part no",
        "parca no",
        "parça no",
        "fiyat",
        "stok",
        "plaka",
        "plakasi",
        "plakası",
        "kilavuz",
        "kılavuz",
    )
    return any(hint in normalized for hint in machine_hints) and not any(
        hint in normalized for hint in part_lookup_hints
    )


def describe_catalog_machine_type(catalog_name: str) -> str | None:
    normalized = normalize_for_overlap(catalog_name or "")
    if "chainstitch" in normalized or "zincir" in normalized:
        return "yüksek hızlı silindir yatak zincir dikiş makinesi"
    if "coverstitch" in normalized or "recme" in normalized:
        return "reçme/coverstitch grubu"
    if "lockstitch" in normalized:
        return "lockstitch/düz dikiş grubu"
    if "buttonhol" in normalized:
        return "ilik/düğme deliği grubu"
    return None


def build_machine_catalog_answer(
    *,
    brand: str | None,
    machine_model: str | None,
    catalog_rows: list[dict],
) -> str:
    if not catalog_rows:
        label = " ".join(x for x in [brand, machine_model] if x).strip() or "bu model"
        return (
            f"Ustam, {label} için erişilebilir kataloglarda net kayıt bulamadım. "
            "Tam model etiketini veya katalog kapağındaki modeli paylaşırsan tekrar daraltayım."
        )

    primary = catalog_rows[0]
    catalog_name = str(primary.get("Name") or "").strip()
    item_count = int(primary.get("ItemCount") or 0)
    brands = str(primary.get("Brands") or brand or "").strip()
    models = str(primary.get("Models") or machine_model or "").strip()
    machine_type = describe_catalog_machine_type(catalog_name)
    type_text = (
        f"Katalog başlığına göre makine tipi: {machine_type}."
        if machine_type
        else "Bu katalog bir parça listesi; tam teknik özellik tablosu görünmüyor."
    )
    brand_model_text = " ".join(x for x in [brands, models] if x).strip()
    extra_count = len(catalog_rows) - 1
    extra_text = f" Ayrıca {extra_count} ilgili katalog kaydı daha var." if extra_count > 0 else ""

    return (
        f"Var ustam: erişilebilir kataloglarda {brand_model_text or machine_model or catalog_name} görünüyor. "
        f"Katalog adı: {catalog_name}. {type_text} "
        f"Şu an bu katalogdan sistemde {item_count} parça satırı okunmuş.{extra_text} "
        "Katalog parça listesi olduğu için motor gücü/devir gibi katalogda görünmeyen teknik değerleri uydurmam; "
        "parça adı, ref no veya sayfa söylersen katalogdan net kodla devam ederim."
    )


def looks_like_machine_model_code(part_code: str | None, machine_model: str | None) -> bool:
    if not part_code or not machine_model:
        return False
    return model_token_matches(part_code, machine_model) or model_token_matches(machine_model, part_code)


def clear_machine_model_from_part_code(analysis: dict, machine_model: str | None) -> None:
    part_code = analysis.get("part_code")
    if looks_like_machine_model_code(part_code, machine_model):
        logger.info("🧹 Makine modeli parça kodu gibi geldi, temizlendi: {}", part_code)
        analysis["part_code"] = None

    cleaned_parts = []
    for part in analysis.get("parts") or []:
        if not isinstance(part, dict):
            cleaned_parts.append(part)
            continue
        cleaned = dict(part)
        if looks_like_machine_model_code(cleaned.get("part_code"), machine_model):
            cleaned["part_code"] = None
        cleaned_parts.append(cleaned)
    if cleaned_parts:
        analysis["parts"] = cleaned_parts


def clear_unmentioned_brand_guess(analysis: dict, raw_user_query: str | None, extracted_brand: str | None) -> str | None:
    if not extracted_brand:
        return extracted_brand

    explicit_brand = detect_brand_from_text(raw_user_query or "")
    if explicit_brand:
        return extracted_brand

    logger.info("🧹 Router marka tahmini kullanıcı metninde geçmediği için temizlendi: {}", extracted_brand)
    analysis["brand"] = None
    return None


def _as_str_list(values) -> list[str]:
    result: list[str] = []
    for value in values or []:
        text = str(value or "").strip()
        if text and text not in result:
            result.append(text)
    return result


def _first_csv_value(value: object | None) -> str | None:
    text = str(value or "").strip()
    if not text:
        return None
    first = text.split(",", 1)[0].strip()
    return first or None


def _catalog_ids_overlap(left: list[str], right: list[str]) -> bool:
    left_set = {str(item).strip() for item in left or [] if str(item).strip()}
    right_set = {str(item).strip() for item in right or [] if str(item).strip()}
    return bool(left_set and right_set and left_set.intersection(right_set))


async def resolve_search_scope(user_query: str, frontend_catalog_ids, chat_history) -> dict:
    """
    Resolve the catalog/brand scope before retrieval.

    If the user explicitly mentions a machine model, the database model/catalog
    relationship is treated as the source of truth. This prevents stale frontend
    catalog ids or sticky conversation state from leaking JUKI context into a
    Yamato VG2500 question.
    """
    frontend_ids = _as_str_list(frontend_catalog_ids)
    history_items = parse_history(chat_history) if isinstance(chat_history, str) else (chat_history or [])
    sticky_context = extract_sticky_context_from_history(history_items)
    previous_model = sticky_context.get("machine_model")

    detected_model, detected_model_brand = detect_machine_model_from_text(user_query or "")
    explicit_brand = detect_brand_from_text(user_query or "")

    scope = {
        "catalog_ids": frontend_ids,
        "resolved_catalog_ids": frontend_ids,
        "resolved_brand": explicit_brand or None,
        "resolved_machine_model": detected_model or None,
        "scope_source": "frontend_catalog_ids" if frontend_ids else "none",
        "reset_context": False,
        "model_changed": False,
        "frontend_scope_mismatch": False,
    }

    if not detected_model:
        return scope

    scope["scope_source"] = "user_model_unresolved"
    catalog_rows = await find_catalogs_by_machine(
        brand_filter=explicit_brand or detected_model_brand,
        machine_model=detected_model,
        catalog_ids=None,
        limit=5,
    )
    if not catalog_rows and explicit_brand:
        catalog_rows = await find_catalogs_by_machine(
            brand_filter=None,
            machine_model=detected_model,
            catalog_ids=None,
            limit=5,
        )

    if not catalog_rows:
        return scope

    resolved_catalog_ids = _as_str_list(row.get("Id") for row in catalog_rows)
    primary = catalog_rows[0]
    resolved_brand = _first_csv_value(primary.get("Brands")) or explicit_brand or detected_model_brand
    resolved_models = _first_csv_value(primary.get("Models")) or detected_model
    frontend_mismatch = bool(frontend_ids) and not _catalog_ids_overlap(frontend_ids, resolved_catalog_ids)
    model_changed = bool(previous_model) and not model_token_matches(detected_model, previous_model)

    scope.update(
        {
            "catalog_ids": resolved_catalog_ids,
            "resolved_catalog_ids": resolved_catalog_ids,
            "resolved_brand": resolved_brand,
            "resolved_machine_model": detected_model,
            "resolved_machine_model_label": resolved_models,
            "scope_source": "user_model_db_override" if (frontend_mismatch or model_changed) else "user_model_db",
            "reset_context": frontend_mismatch or model_changed,
            "model_changed": model_changed,
            "frontend_scope_mismatch": frontend_mismatch,
        }
    )
    return scope


def source_matches_resolved_scope(source: dict, search_scope: dict) -> bool:
    resolved_model = (search_scope or {}).get("resolved_machine_model")
    resolved_catalog_ids = _as_str_list((search_scope or {}).get("resolved_catalog_ids"))
    if not resolved_model or not resolved_catalog_ids:
        return True

    source_catalog_id = str(source.get("catalogId") or source.get("CatalogId") or "").strip()
    if source_catalog_id:
        return source_catalog_id in resolved_catalog_ids

    source_model = source.get("machine_model") or source.get("model") or source.get("MachineModel")
    return model_token_matches(resolved_model, str(source_model or ""))


def apply_model_compatibility_gate(sources: list[dict], search_scope: dict) -> tuple[list[dict], list[dict]]:
    if not (search_scope or {}).get("resolved_machine_model"):
        return sources, []

    accepted: list[dict] = []
    rejected: list[dict] = []
    for source in sources or []:
        if source_matches_resolved_scope(source, search_scope):
            accepted.append(source)
        else:
            rejected.append(source)
    return accepted, rejected


def attach_search_trace(
    response: dict,
    *,
    original_query: str,
    analysis: dict | None,
    search_scope: dict | None,
    sources: list[dict] | None = None,
    retrieved_candidates_count: int = 0,
    compatibility_gate_filtered_count: int = 0,
    query_rewrite_debug: dict | None = None,
    extracted_code: str | None = None,
) -> dict:
    trace = build_search_trace(
        original_query=original_query,
        analysis=analysis,
        search_scope=search_scope,
        sources=sources or response.get("sources") or [],
        retrieved_candidates_count=retrieved_candidates_count,
        compatibility_gate_filtered_count=compatibility_gate_filtered_count,
        query_rewrite_debug=query_rewrite_debug,
        extracted_code=extracted_code,
    )
    response["search_trace"] = trace
    if analysis is not None:
        analysis["search_trace"] = trace
    return response


def should_hold_generic_diagnosis_catalog_search(
    *,
    intent: str | None,
    extracted_machine_model: str | None,
    extracted_code: str | None,
    parts: list[dict] | None,
    analysis: dict,
) -> bool:
    """Avoid turning generic symptoms into weak product cards before the model is known."""
    if str(intent or "").upper() != "DIAGNOSE":
        return False
    if extracted_machine_model or extracted_code:
        return False
    if (analysis or {}).get("context_related_terms"):
        return False

    search_parts = [part for part in (parts or []) if isinstance(part, dict)]
    if not search_parts:
        return True
    return all(part.get("source") == "diagnosis_symptom_map" for part in search_parts)


def is_generic_symptom_query_without_context(
    user_query: str,
    *,
    extracted_machine_model: str | None,
    extracted_code: str | None,
) -> bool:
    if extracted_machine_model or extracted_code:
        return False
    if not diagnosis_terms_for_query(user_query):
        return False
    if related_part_terms_for_context(user_query):
        return False
    normalized_query = normalize_for_overlap(user_query or "")
    if any(token in normalized_query for token in ("kodu", "kodunu", "part no", "parca no", "parça no")):
        return False
    return True


async def prepare_chat_context(
    text: str | None,
    message: str | None,
    history: str,
    catalog_ids: str,
    file: UploadFile | None,
    context_json: str | None = None,
    policy_threshold_override: str | dict | None = None,
) -> dict:
    """
    Run intent analysis and hybrid retrieval for send + stream endpoints.

    Returns either:
    - {"early": True, "response": {...}}
    - {"early": False, "user_query": ..., "all_sources": ..., "analysis": ..., "final_prompt": ...}
    """
    raw_user_query, early_request = resolve_raw_user_query(text, message, has_file=file is not None)
    if early_request:
        return early_request

    user_query = normalize_user_query(raw_user_query)
    logger.info(f"📨 [GİRİŞ] Mesaj: {raw_user_query}")
    if user_query != raw_user_query:
        logger.info(f"🧹 [NORMALIZE] '{raw_user_query}' -> '{user_query}'")

    catalog_ids_list = parse_json_list(catalog_ids)
    chat_context = parse_chat_context(context_json)
    history_list_for_intent = parse_history(history)
    sticky_context = extract_sticky_context_from_history(history_list_for_intent)
    search_scope = await resolve_search_scope(user_query, catalog_ids_list, history_list_for_intent)
    catalog_ids_list = search_scope.get("catalog_ids") or catalog_ids_list
    if search_scope.get("reset_context"):
        logger.info(
            "🧭 Search scope override: model={} catalogs={} source={}",
            search_scope.get("resolved_machine_model"),
            search_scope.get("resolved_catalog_ids"),
            search_scope.get("scope_source"),
        )
        sticky_context = {}
        chat_context = {}

    image_analysis = {}
    if file is not None:
        image_bytes = await file.read()
        results = await asyncio.gather(
            analyze_intent_with_gemini(user_query, history=history_list_for_intent),
            analyze_image_with_gemini(image_bytes, raw_user_query),
            return_exceptions=True,
        )
        analysis = normalize_intent_payload(results[0] if isinstance(results[0], dict) else None, user_query)
        image_analysis = results[1] if isinstance(results[1], dict) else {}
        analysis["image_analysis"] = image_analysis
    else:
        analysis = normalize_intent_payload(
            await analyze_intent_with_gemini(user_query, history=history_list_for_intent),
            user_query,
        )

    intent = analysis.get("intent", "CHAT")
    extracted_brand = analysis.get("brand")
    extracted_part = analysis.get("part_name")
    extracted_context_part = analysis.get("context_part")
    extracted_code = analysis.get("part_code")
    extracted_dim = analysis.get("dimensions")
    extracted_machine_group = analysis.get("machine_group")
    extracted_machine_model = analysis.get("machine_model")

    extracted_brand = clear_unmentioned_brand_guess(analysis, raw_user_query, extracted_brand)

    if search_scope.get("resolved_machine_model"):
        extracted_machine_model = search_scope.get("resolved_machine_model")
        analysis["machine_model"] = extracted_machine_model
    if search_scope.get("resolved_brand"):
        extracted_brand = search_scope.get("resolved_brand")
        analysis["brand"] = extracted_brand
    analysis["resolved_catalog_ids"] = search_scope.get("resolved_catalog_ids") or []
    analysis["resolved_brand"] = search_scope.get("resolved_brand")
    analysis["resolved_machine_model"] = search_scope.get("resolved_machine_model")
    analysis["scope_source"] = search_scope.get("scope_source")

    if not extracted_machine_model:
        detected_model, detected_model_brand = detect_machine_model_from_text(raw_user_query)
        if detected_model:
            extracted_machine_model = detected_model
            analysis["machine_model"] = detected_model
        if not extracted_brand and detected_model_brand:
            extracted_brand = detected_model_brand
            analysis["brand"] = detected_model_brand

    if not extracted_brand:
        detected_brand = detect_brand_from_text(raw_user_query)
        if detected_brand:
            extracted_brand = detected_brand
            analysis["brand"] = detected_brand

    if extracted_machine_model:
        clear_machine_model_from_part_code(analysis, extracted_machine_model)
        extracted_code = analysis.get("part_code")

    if file is not None:
        img_part = image_analysis.get("candidate_part_name")
        img_brand = image_analysis.get("detected_brand_text")
        img_code = image_analysis.get("visible_codes")
        img_machine_group = image_analysis.get("machine_type_hint")
        img_assembly_hint = image_analysis.get("assembly_hint")

        if (not extracted_part) and img_part:
            extracted_part = img_part
            analysis["part_name"] = img_part
            if intent == "CHAT":
                intent = "SEARCH"
                analysis["intent"] = "SEARCH"
        if (not extracted_brand) and img_brand:
            extracted_brand = img_brand
            analysis["brand"] = img_brand
        if (not extracted_code) and img_code:
            extracted_code = img_code
            analysis["part_code"] = img_code
            if intent == "CHAT":
                intent = "SEARCH"
                analysis["intent"] = "SEARCH"
        if (not extracted_machine_group) and img_machine_group:
            extracted_machine_group = img_machine_group
            analysis["machine_group"] = img_machine_group
        if (not extracted_context_part) and img_assembly_hint:
            extracted_context_part = img_assembly_hint
            analysis["context_part"] = img_assembly_hint

    if (not extracted_brand) and sticky_context.get("brand"):
        extracted_brand = sticky_context["brand"]
        analysis["brand"] = extracted_brand

    if (not extracted_machine_group) and sticky_context.get("machine_group"):
        extracted_machine_group = sticky_context["machine_group"]
        analysis["machine_group"] = extracted_machine_group

    explicit_machine_group = detect_machine_group_from_text(raw_user_query)
    if extracted_machine_group and not explicit_machine_group and not sticky_context.get("machine_group"):
        logger.info(
            "🧭 Machine group tahmini temizlendi: '{}' için kullanıcı açık tip söylemedi",
            extracted_machine_group,
        )
        extracted_machine_group = None
        analysis["machine_group"] = None

    if (not extracted_machine_model) and sticky_context.get("machine_model"):
        extracted_machine_model = sticky_context["machine_model"]
        analysis["machine_model"] = extracted_machine_model

    context_machine_brand = str(chat_context.get("machineBrand") or "").strip()
    context_machine_model = str(chat_context.get("machineModel") or "").strip()
    context_machine_group = str(chat_context.get("machineGroup") or "").strip()
    if not extracted_brand and context_machine_brand:
        extracted_brand = context_machine_brand
        analysis["brand"] = extracted_brand
    if not extracted_machine_model and context_machine_model:
        extracted_machine_model = context_machine_model
        analysis["machine_model"] = extracted_machine_model
    if not extracted_machine_group and context_machine_group:
        extracted_machine_group = context_machine_group
        analysis["machine_group"] = extracted_machine_group

    if extracted_machine_model:
        clear_machine_model_from_part_code(analysis, extracted_machine_model)
        extracted_code = analysis.get("part_code")

    context_part_code = str(chat_context.get("partCode") or "").strip()
    context_ref_no = str(chat_context.get("refNo") or "").strip()
    context_part_name = str(chat_context.get("partName") or "").strip()
    context_page_number = str(chat_context.get("pageNumber") or "").strip()
    sticky_part_candidates = sticky_context.get("part_candidates") or []
    selected_sticky_candidate = select_sticky_candidate_from_query(raw_user_query, sticky_part_candidates)
    active_sticky_candidates = [selected_sticky_candidate] if selected_sticky_candidate else sticky_part_candidates
    if extracted_machine_model and active_sticky_candidates:
        active_sticky_candidates = [
            candidate for candidate in active_sticky_candidates
            if not looks_like_machine_model_code(candidate.get("code"), extracted_machine_model)
        ]
    if active_sticky_candidates:
        analysis["candidate_sources"] = active_sticky_candidates
        if not chat_context.get("candidateSources"):
            chat_context["candidateSources"] = active_sticky_candidates
    if (not context_part_code) and active_sticky_candidates:
        first_candidate = active_sticky_candidates[0]
        context_part_code = str(first_candidate.get("code") or "").strip()
        context_part_name = context_part_name or str(first_candidate.get("name") or "").strip()
        context_page_number = context_page_number or str(first_candidate.get("pageNumber") or "").strip()
        context_ref_no = context_ref_no or str(first_candidate.get("refNo") or "").strip()
    if (
        not context_part_code
        and sticky_context.get("part_code")
        and not looks_like_machine_model_code(sticky_context.get("part_code"), extracted_machine_model)
    ):
        context_part_code = sticky_context["part_code"]
    if (
        not context_part_name
        and sticky_context.get("part_name")
        and not looks_like_machine_model_code(sticky_context.get("part_code"), extracted_machine_model)
    ):
        context_part_name = sticky_context["part_name"]
    if not context_page_number and sticky_context.get("page_number"):
        context_page_number = sticky_context["page_number"]
    if not context_ref_no and sticky_context.get("ref_no"):
        context_ref_no = sticky_context["ref_no"]

    if context_part_code and not chat_context.get("partCode"):
        chat_context["partCode"] = context_part_code
    if context_part_name and not chat_context.get("partName"):
        chat_context["partName"] = context_part_name
    if context_page_number and not chat_context.get("pageNumber"):
        chat_context["pageNumber"] = context_page_number
    if context_ref_no and not chat_context.get("refNo"):
        chat_context["refNo"] = context_ref_no

    context_code = context_part_code or context_ref_no
    references_current_context = has_current_context_reference(user_query) or bool(active_sticky_candidates)
    if context_code and not extracted_code and (
        references_current_context or intent in {"PRICE", "STOCK", "COMPATIBILITY"}
    ):
        extracted_code = context_code
        analysis["part_code"] = context_code
        if intent in {"CHAT", "HELP"}:
            intent = "SEARCH"
            analysis["intent"] = "SEARCH"

    if extracted_machine_model and looks_like_machine_model_code(extracted_code, extracted_machine_model):
        logger.info("🧹 Bağlamdan gelen makine modeli parça kodu gibi geldi, temizlendi: {}", extracted_code)
        extracted_code = None
        analysis["part_code"] = None
        clear_machine_model_from_part_code(analysis, extracted_machine_model)
        if looks_like_machine_model_code(context_part_code, extracted_machine_model):
            context_part_code = ""
            chat_context.pop("partCode", None)
        context_code = context_part_code or context_ref_no

    if context_part_name and not extracted_part and references_current_context:
        extracted_part = context_part_name
        analysis["part_name"] = context_part_name
        if intent in {"CHAT", "HELP"}:
            intent = "SEARCH"
            analysis["intent"] = "SEARCH"

    if chat_context:
        analysis["current_context"] = chat_context

    def trace_early_response(payload: dict, *, sources: list[dict] | None = None) -> dict:
        if payload.get("response"):
            attach_search_trace(
                payload["response"],
                original_query=raw_user_query,
                analysis=analysis,
                search_scope=search_scope,
                sources=sources or payload["response"].get("sources") or [],
                extracted_code=extracted_code,
            )
        return payload

    if should_answer_with_local_guidance(raw_user_query):
        message = build_no_result_guidance(raw_user_query, analysis, reason="local_guidance")
        active_sources = build_active_context_sources(analysis) if references_current_context else []
        return trace_early_response({
            "early": True,
            "response": {"answer": message, "reply": message, "sources": active_sources, "debug_intent": analysis},
        }, sources=active_sources)

    intent, unavailable_feature_response = apply_unavailable_feature_policy(
        intent=intent,
        analysis=analysis,
        unavailable_feature_label=unavailable_feature_label,
        context_code=context_code,
        context_part_name=context_part_name,
        extracted_part=extracted_part,
        extracted_code=extracted_code,
        extracted_brand=extracted_brand,
    )
    if unavailable_feature_response:
        return trace_early_response(unavailable_feature_response)

    confidence = extract_confidence_value(analysis)
    extracted_parts = analysis.get("parts")
    low_confidence = low_confidence_response(
        confidence=confidence,
        intent=intent,
        raw_user_query=raw_user_query,
        analysis=analysis,
        extracted_part=extracted_part,
        extracted_code=extracted_code,
        extracted_brand=extracted_brand,
        extracted_parts=extracted_parts,
        build_no_result_guidance=build_no_result_guidance,
    )
    if low_confidence:
        return trace_early_response(low_confidence)

    if is_generic_symptom_query_without_context(
        raw_user_query,
        extracted_machine_model=extracted_machine_model,
        extracted_code=extracted_code,
    ):
        analysis["intent"] = "DIAGNOSE"
        message = build_no_result_guidance(raw_user_query, analysis, reason="generic_symptom_needs_model")
        return trace_early_response({
            "early": True,
            "response": {"answer": message, "reply": message, "sources": [], "debug_intent": analysis},
        })

    parts = build_search_parts(
        analysis,
        intent=intent,
        extracted_part=extracted_part,
        extracted_code=extracted_code,
        extracted_dimensions=extracted_dim,
        extracted_context_part=extracted_context_part,
        raw_user_query=raw_user_query,
        user_query=user_query,
    )

    if should_hold_generic_diagnosis_catalog_search(
        intent=intent,
        extracted_machine_model=extracted_machine_model,
        extracted_code=extracted_code,
        parts=parts,
        analysis=analysis,
    ):
        message = build_no_result_guidance(raw_user_query, analysis, reason="generic_diagnosis_needs_model")
        return trace_early_response({
            "early": True,
            "response": {"answer": message, "reply": message, "sources": [], "debug_intent": analysis},
        })

    if extracted_brand:
        available_brands = await get_catalog_brands(catalog_ids_list)
        if available_brands and not brand_matches_available(str(extracted_brand), available_brands):
            brands_text = ", ".join(available_brands[:10]) if available_brands else "tespit edilemedi"
            msg = (
                f"Ustam, depoda '{extracted_brand}' markalı katalog bulunmuyor. "
                f"Mevcut markalar: {brands_text}."
            )
            return trace_early_response({
                "early": True,
                "response": {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis},
            })

    if extracted_machine_model and looks_like_machine_catalog_question(raw_user_query, intent):
        catalog_rows = await find_catalogs_by_machine(
            brand_filter=extracted_brand,
            machine_model=extracted_machine_model,
            catalog_ids=catalog_ids_list,
            limit=5,
        )
        msg = build_machine_catalog_answer(
            brand=extracted_brand,
            machine_model=extracted_machine_model,
            catalog_rows=catalog_rows,
        )
        analysis["machine_catalog_matches"] = [
            {
                "catalogId": str(row.get("Id") or ""),
                "name": row.get("Name"),
                "itemCount": row.get("ItemCount"),
                "models": row.get("Models"),
            }
            for row in catalog_rows[:5]
        ]
        return trace_early_response({
            "early": True,
            "response": {"answer": msg, "reply": msg, "sources": [], "debug_intent": analysis},
        })

    is_chat_only, is_general_knowledge_only, route_response = route_without_search(
        intent=intent,
        extracted_part=extracted_part,
        extracted_code=extracted_code,
        parts=parts,
        image_analysis=image_analysis,
        analysis=analysis,
        is_general_knowledge_intent=is_general_knowledge_intent,
    )
    if route_response:
        return trace_early_response(route_response)

    all_sources = []
    context_page_hints: list[str] = []
    visual_sources = []
    visual_search_debug = {"used": False}
    if file is not None and image_analysis:
        visual_sources, visual_search_debug = await run_visual_search(
            image_analysis,
            extracted_brand=extracted_brand,
            catalog_ids_list=catalog_ids_list,
            extracted_machine_group=extracted_machine_group,
            shop_base_url=SHOP_BASE_URL,
            build_visual_hint_text=build_visual_hint_text,
            as_clean_text_list=as_clean_text_list,
            rerank_results_by_visual_hints=rerank_results_by_visual_hints,
        )

    all_sources = list(visual_sources)
    if visual_search_debug["used"]:
        analysis["visual_search"] = visual_search_debug

    all_sources, context_page_hints, query_rewrite_debug = await retrieve_part_sources(
        parts,
        initial_sources=all_sources,
        raw_user_query=raw_user_query,
        user_query=user_query,
        intent=intent,
        extracted_brand=extracted_brand,
        extracted_machine_group=extracted_machine_group,
        extracted_context_part=extracted_context_part,
        catalog_ids_list=catalog_ids_list,
        shop_base_url=SHOP_BASE_URL,
        text_vector_min_similarity=TEXT_VECTOR_MIN_SIMILARITY,
        expand_part_search_text=expand_part_search_text,
        split_expanded_search_terms=split_expanded_search_terms,
        extract_requested_domain_terms=extract_requested_domain_terms,
        rerank_results_by_context_part=rerank_results_by_context_part,
        filter_results_by_requested_terms=filter_results_by_requested_terms,
        filter_diagnosis_results=filter_diagnosis_results,
        is_strict_part_lookup=is_strict_part_lookup,
        filter_strict_part_lookup_results=filter_strict_part_lookup_results,
    )
    if query_rewrite_debug:
        analysis["query_rewrite"] = query_rewrite_debug

    all_sources = limit_context_related_sources(all_sources, analysis.get("context_related_terms"))
    retrieved_candidates_count = len(all_sources)
    all_sources, rejected_by_model_gate = apply_model_compatibility_gate(all_sources, search_scope)
    if rejected_by_model_gate:
        analysis["model_compatibility_rejected"] = [
            {
                "code": source.get("code"),
                "name": source.get("name"),
                "catalogId": source.get("catalogId"),
                "model": source.get("machine_model") or source.get("model"),
            }
            for source in rejected_by_model_gate[:5]
        ]
        logger.info(
            "🧯 Model Compatibility Gate: {} aday reddedildi | target_model={} | catalogs={}",
            len(rejected_by_model_gate),
            search_scope.get("resolved_machine_model"),
            search_scope.get("resolved_catalog_ids"),
        )

    logger.success(f"📦 Toplam Bulunan Benzersiz Sonuç: {len(all_sources)}")
    logger.info(
        "📍 Source preview: {}",
        [
            {
                "code": s.get("code"),
                "refNo": s.get("refNo"),
                "pageNumber": s.get("pageNumber"),
                "catalogId": (s.get("catalogId") or "")[:8],
            }
            for s in all_sources[:5]
        ],
    )

    active_confidence_thresholds = _parse_policy_threshold_override(policy_threshold_override)
    if active_confidence_thresholds is None:
        active_confidence_thresholds = await resolve_confidence_thresholds_for_scope(search_scope)
    quality_response = source_quality_early_response(
        all_sources=all_sources,
        intent=intent,
        is_chat_only=is_chat_only,
        is_general_knowledge_only=is_general_knowledge_only,
        raw_user_query=raw_user_query,
        user_query=user_query,
        analysis=analysis,
        extracted_code=extracted_code,
        weak_match_min_similarity=WEAK_MATCH_MIN_SIMILARITY,
        build_no_result_guidance=build_no_result_guidance,
        best_similarity=best_similarity,
        has_lexical_overlap=has_lexical_overlap,
        has_domain_part_keyword=has_domain_part_keyword,
        is_general_knowledge_intent=is_general_knowledge_intent,
        search_scope=search_scope,
        confidence_thresholds=active_confidence_thresholds,
    )
    if quality_response:
        attach_search_trace(
            quality_response["response"],
            original_query=raw_user_query,
            analysis=analysis,
            search_scope=search_scope,
            sources=quality_response["response"].get("sources") or all_sources,
            retrieved_candidates_count=retrieved_candidates_count,
            compatibility_gate_filtered_count=len(rejected_by_model_gate),
            query_rewrite_debug=query_rewrite_debug,
            extracted_code=extracted_code,
        )
        return quality_response

    response_mode = classify_response_mode(raw_user_query, intent, analysis)
    analysis["response_mode"] = response_mode
    if response_mode == "SERVICE_ACTION" and all_sources:
        service_reply = build_deterministic_reply_from_sources(raw_user_query, all_sources)
        response = {"answer": service_reply, "reply": service_reply, "sources": all_sources, "debug_intent": analysis}
        attach_search_trace(
            response,
            original_query=raw_user_query,
            analysis=analysis,
            search_scope=search_scope,
            sources=all_sources,
            retrieved_candidates_count=retrieved_candidates_count,
            compatibility_gate_filtered_count=len(rejected_by_model_gate),
            query_rewrite_debug=query_rewrite_debug,
            extracted_code=extracted_code,
        )
        return {
            "early": True,
            "response": response,
        }
    if response_mode == "PRICE_STOCK":
        exact_reply = build_exact_code_reply_from_sources(raw_user_query, analysis, all_sources)
        if exact_reply:
            response = {"answer": exact_reply, "reply": exact_reply, "sources": all_sources, "debug_intent": analysis}
            attach_search_trace(
                response,
                original_query=raw_user_query,
                analysis=analysis,
                search_scope=search_scope,
                sources=all_sources,
                retrieved_candidates_count=retrieved_candidates_count,
                compatibility_gate_filtered_count=len(rejected_by_model_gate),
                query_rewrite_debug=query_rewrite_debug,
                extracted_code=extracted_code,
            )
            return {
                "early": True,
                "response": response,
            }

    context_text = build_context_text(all_sources)
    history_text = build_history_text(parse_history(history))
    machine_context_line = build_machine_context_line(
        extracted_brand,
        extracted_machine_model,
        extracted_machine_group,
    )
    active_context_line = build_active_context_line(chat_context, context_code, context_part_name)
    relation_context_line = build_relation_context_line(extracted_context_part, parts)
    source_model_labels = collect_source_model_labels(all_sources)
    model_safety_line = build_model_safety_line(
        extracted_machine_model,
        source_model_labels,
        model_token_matches,
    )
    context_page_hint_block = build_context_page_hint_block(context_page_hints)
    intent_mode_block = get_intent_mode_block(intent)
    response_mode_instruction = build_response_mode_instruction(response_mode)
    general_knowledge_line = (
        "GENEL USTA BİLGİSİ MODU: Katalogda kesin kaynak yoksa genel teknik bilgiyi öneri olarak ver; "
        "bunu kesin katalog/uyumluluk bilgisi gibi sunma.\n"
        if is_general_knowledge_intent(intent)
        else ""
    )

    final_prompt = build_final_prompt(
        history_text=history_text,
        raw_user_query=raw_user_query,
        machine_context_line=machine_context_line,
        model_safety_line=model_safety_line,
        active_context_line=active_context_line,
        relation_context_line=relation_context_line,
        context_page_hint_block=context_page_hint_block,
        context_text=context_text,
        intent=intent,
        general_knowledge_line=general_knowledge_line,
        intent_mode_block=intent_mode_block,
        response_mode_instruction=response_mode_instruction,
    )

    search_trace = build_search_trace(
        original_query=raw_user_query,
        analysis=analysis,
        search_scope=search_scope,
        sources=all_sources,
        retrieved_candidates_count=retrieved_candidates_count,
        compatibility_gate_filtered_count=len(rejected_by_model_gate),
        query_rewrite_debug=query_rewrite_debug,
        extracted_code=extracted_code,
    )
    analysis["search_trace"] = search_trace

    return {
        "early": False,
        "user_query": user_query,
        "all_sources": all_sources,
        "analysis": analysis,
        "search_trace": search_trace,
        "final_prompt": final_prompt,
    }


def _parse_policy_threshold_override(value: str | dict | None) -> dict | None:
    if not value:
        return None
    if isinstance(value, dict):
        payload = value
    else:
        try:
            payload = json.loads(value)
        except json.JSONDecodeError:
            logger.warning("Policy threshold override JSON parse edilemedi.")
            return None
    if not isinstance(payload, dict):
        return None
    allowed = {"high_confidence", "low_confidence", "ambiguity_score_delta", "source"}
    return {key: payload[key] for key in allowed if key in payload}
