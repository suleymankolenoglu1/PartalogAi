"""Decision helpers for chat routing and early exits."""

import json
import os
from collections.abc import Callable
from dataclasses import dataclass, replace
from typing import Any

from loguru import logger


@dataclass(frozen=True)
class ConfidenceThresholds:
    high_confidence: float = 0.85
    low_confidence: float = 0.55
    ambiguity_score_delta: float = 0.10
    source: str = "default"


DEFAULT_CONFIDENCE_THRESHOLDS = ConfidenceThresholds()


def _load_configured_threshold_overrides() -> dict[str, dict[str, dict[str, float]]]:
    """Load optional threshold overrides from the runtime config layer.

    Expected shape:
    {
        "brands": {"juki": {"high_confidence": 0.80}},
        "catalogs": {"catalog-guid": {"low_confidence": 0.50}}
    }
    """
    raw_value = os.getenv("PARTALOG_CONFIDENCE_THRESHOLD_OVERRIDES_JSON")
    if not raw_value:
        return {"global": {}, "brands": {}, "catalogs": {}}
    try:
        loaded = json.loads(raw_value)
    except json.JSONDecodeError as exc:
        logger.warning("Invalid PARTALOG_CONFIDENCE_THRESHOLD_OVERRIDES_JSON: {}", exc)
        return {"global": {}, "brands": {}, "catalogs": {}}
    if not isinstance(loaded, dict):
        return {"global": {}, "brands": {}, "catalogs": {}}
    return {
        "global": loaded.get("global") if isinstance(loaded.get("global"), dict) else {},
        "brands": loaded.get("brands") if isinstance(loaded.get("brands"), dict) else {},
        "catalogs": loaded.get("catalogs") if isinstance(loaded.get("catalogs"), dict) else {},
    }


CONFIDENCE_THRESHOLD_OVERRIDES = _load_configured_threshold_overrides()

HIGH_CONFIDENCE = DEFAULT_CONFIDENCE_THRESHOLDS.high_confidence
LOW_CONFIDENCE = DEFAULT_CONFIDENCE_THRESHOLDS.low_confidence
AMBIGUITY_SCORE_DELTA = DEFAULT_CONFIDENCE_THRESHOLDS.ambiguity_score_delta


def _as_catalog_ids(search_scope: dict | None, catalog_id: str | None = None) -> list[str]:
    values: list[Any] = []
    if catalog_id:
        values.append(catalog_id)
    if search_scope:
        values.extend(search_scope.get("resolved_catalog_ids") or [])
        values.extend(search_scope.get("catalog_ids") or [])
    catalog_ids: list[str] = []
    for value in values:
        item = str(value or "").strip()
        if item and item not in catalog_ids:
            catalog_ids.append(item)
    return catalog_ids


def _clean_brand(value: Any) -> str | None:
    brand = str(value or "").strip().casefold()
    return brand or None


def _coerce_threshold_overrides(overrides: dict | None) -> dict[str, float]:
    if not isinstance(overrides, dict):
        return {}
    allowed = {"high_confidence", "low_confidence", "ambiguity_score_delta"}
    coerced: dict[str, float] = {}
    for key in allowed:
        value = overrides.get(key)
        if isinstance(value, (int, float)):
            coerced[key] = float(value)
    return coerced


def _merge_thresholds(
    base: ConfidenceThresholds,
    overrides: dict | None,
    *,
    source: str,
) -> ConfidenceThresholds:
    coerced = _coerce_threshold_overrides(overrides)
    if not coerced:
        return base
    override_source = overrides.get("source") if isinstance(overrides, dict) else None
    if isinstance(override_source, str) and override_source.strip():
        source = override_source.strip()
    return replace(base, **coerced, source=source)


def resolve_confidence_thresholds(
    search_scope: dict | None = None,
    *,
    catalog_id: str | None = None,
    brand: str | None = None,
    overrides: dict | None = None,
) -> ConfidenceThresholds:
    active = DEFAULT_CONFIDENCE_THRESHOLDS
    configured_overrides = overrides if overrides is not None else CONFIDENCE_THRESHOLD_OVERRIDES

    active = _merge_thresholds(
        active,
        (configured_overrides or {}).get("global"),
        source="global",
    )

    brand_key = _clean_brand(brand or (search_scope or {}).get("resolved_brand"))
    if brand_key:
        brand_overrides = (configured_overrides or {}).get("brands") or {}
        active = _merge_thresholds(
            active,
            brand_overrides.get(brand_key),
            source=f"brand:{brand_key}",
        )

    catalog_overrides = (configured_overrides or {}).get("catalogs") or {}
    for resolved_catalog_id in _as_catalog_ids(search_scope, catalog_id):
        if resolved_catalog_id in catalog_overrides:
            active = _merge_thresholds(
                active,
                catalog_overrides.get(resolved_catalog_id),
                source=f"catalog:{resolved_catalog_id}",
            )
            break

    return active


async def resolve_confidence_thresholds_for_scope(search_scope: dict | None = None) -> ConfidenceThresholds:
    try:
        from services.policy_thresholds import load_policy_threshold_overrides

        db_overrides = await load_policy_threshold_overrides(search_scope)
    except Exception as exc:
        logger.warning("Policy threshold DB resolution failed; falling back to configured defaults: {}", exc)
        db_overrides = None

    if db_overrides:
        return resolve_confidence_thresholds(search_scope, overrides=db_overrides)
    return resolve_confidence_thresholds(search_scope)


def _normalize_thresholds(value: ConfidenceThresholds | dict | None) -> ConfidenceThresholds:
    if isinstance(value, ConfidenceThresholds):
        return value
    return _merge_thresholds(DEFAULT_CONFIDENCE_THRESHOLDS, value, source="inline")


def confidence_threshold_trace_metadata(thresholds: ConfidenceThresholds) -> dict[str, float | str]:
    return {
        "applied_high_threshold": thresholds.high_confidence,
        "applied_low_threshold": thresholds.low_confidence,
        "applied_ambiguity_delta": thresholds.ambiguity_score_delta,
        "threshold_source": thresholds.source,
    }


def _record_active_thresholds(analysis: dict, thresholds: ConfidenceThresholds) -> None:
    metadata = confidence_threshold_trace_metadata(thresholds)
    analysis.update(metadata)
    analysis["applied_confidence_thresholds"] = {
        "high_confidence": thresholds.high_confidence,
        "low_confidence": thresholds.low_confidence,
        "ambiguity_score_delta": thresholds.ambiguity_score_delta,
        "source": thresholds.source,
    }


def early_response(message: str, analysis: dict, sources: list[dict] | None = None) -> dict:
    return {
        "early": True,
        "response": {
            "answer": message,
            "reply": message,
            "sources": sources or [],
            "debug_intent": analysis,
        },
    }


def _source_score(source: dict) -> float | None:
    for key in ("similarity", "visual_similarity"):
        value = source.get(key)
        if isinstance(value, (int, float)):
            return float(value)
    return None


def _ranked_sources_with_scores(sources: list[dict]) -> list[tuple[dict, float]]:
    ranked: list[tuple[dict, float]] = []
    for source in sources or []:
        score = _source_score(source)
        if score is not None:
            ranked.append((source, score))
    ranked.sort(key=lambda item: item[1], reverse=True)
    return ranked


def _format_candidate_label(source: dict) -> str:
    name = str(source.get("name") or "Aday parça").strip()
    code = str(source.get("code") or "").strip()
    return f"{name} ({code})" if code else name


def ambiguity_guard_response(
    *,
    all_sources: list[dict],
    raw_user_query: str,
    analysis: dict,
    extracted_code: str | None,
    build_no_result_guidance: Callable[[str, dict, str], str],
    confidence_thresholds: ConfidenceThresholds | dict | None = None,
) -> dict | None:
    if extracted_code:
        return None

    active_thresholds = _normalize_thresholds(confidence_thresholds)
    _record_active_thresholds(analysis, active_thresholds)

    ranked = _ranked_sources_with_scores(all_sources)
    if not ranked:
        return None

    top_source, top_score = ranked[0]
    analysis["retrieval_confidence"] = round(top_score, 4)

    if top_score >= active_thresholds.high_confidence:
        analysis["confidence_gate"] = "high"
        return None

    if top_score < active_thresholds.low_confidence:
        analysis["confidence_gate"] = "low"
        logger.info(
            "🧯 Retrieval confidence gate: low score={:.3f} query={}",
            top_score,
            raw_user_query,
        )
        message = build_no_result_guidance(raw_user_query, analysis, reason="retrieval_low_confidence")
        return early_response(message, analysis)

    if len(ranked) < 2:
        analysis["confidence_gate"] = "medium_single"
        return None

    second_source, second_score = ranked[1]
    score_delta = top_score - second_score
    if (
        second_score >= active_thresholds.low_confidence
        and score_delta < active_thresholds.ambiguity_score_delta
    ):
        candidates = [top_source, second_source]
        analysis["confidence_gate"] = "ambiguous"
        analysis["ambiguity_candidates"] = [
            {
                "code": source.get("code"),
                "name": source.get("name"),
                "score": round(score, 4),
            }
            for source, score in ranked[:2]
        ]
        logger.info(
            "🧭 Ambiguity guard: top={:.3f} second={:.3f} delta={:.3f} query={}",
            top_score,
            second_score,
            score_delta,
            raw_user_query,
        )
        message = (
            "Ustam, iki yakın parça arasında kaldım:\n"
            f"1. {_format_candidate_label(top_source)}\n"
            f"2. {_format_candidate_label(second_source)}\n"
            "Hangisini kastetmiştiniz?"
        )
        return early_response(message, analysis, sources=candidates)

    analysis["confidence_gate"] = "medium_clear"
    return None


def apply_unavailable_feature_policy(
    *,
    intent: str,
    analysis: dict,
    unavailable_feature_label: Callable[[str | None], str | None],
    context_code: str | None,
    context_part_name: str | None,
    extracted_part: str | None,
    extracted_code: str | None,
    extracted_brand: str | None,
) -> tuple[str, dict | None]:
    unavailable_feature = unavailable_feature_label(intent)
    if not unavailable_feature:
        return intent, None

    analysis["unsupported_feature"] = unavailable_feature
    analysis["original_intent"] = intent
    intent = "SEARCH"
    analysis["intent"] = "SEARCH"

    has_context_signal = bool(context_code or context_part_name)
    has_query_signal = bool(extracted_part or extracted_code or extracted_brand or analysis.get("parts"))
    if has_context_signal or has_query_signal:
        return intent, None

    message = (
        f"Ustam, {unavailable_feature} bilgisi bu ekranda henüz aktif değil. "
        "İstersen parça kodu, ref no, katalog sayfası veya parça adını yaz; katalogda hangi parçaya denk geldiğini bulayım."
    )
    return intent, early_response(message, analysis)


def low_confidence_response(
    *,
    confidence: float | None,
    intent: str,
    raw_user_query: str,
    analysis: dict,
    extracted_part: str | None,
    extracted_code: str | None,
    extracted_brand: str | None,
    extracted_parts,
    build_no_result_guidance: Callable[[str, dict, str], str],
) -> dict | None:
    has_concrete_signal = bool(extracted_part or extracted_code or extracted_brand or extracted_parts)
    if confidence is None or confidence >= 0.60 or has_concrete_signal:
        return None

    logger.info(
        f"⚠️ Low intent confidence early-exit: confidence={confidence:.2f} intent={intent} query={raw_user_query}"
    )
    message = build_no_result_guidance(raw_user_query, analysis, reason="low_confidence")
    return early_response(message, analysis)


def route_without_search(
    *,
    intent: str,
    extracted_part: str | None,
    extracted_code: str | None,
    parts: list[dict],
    image_analysis: dict,
    analysis: dict,
    is_general_knowledge_intent: Callable[[str | None], bool],
) -> tuple[bool, bool, dict | None]:
    is_diagnose_or_advice = intent in {"DIAGNOSE", "ADVICE", "EXPLAIN_PART", "PHOTO_GUIDANCE"}
    if intent == "CHAT" or ((not extracted_part and not extracted_code and not parts) and not is_diagnose_or_advice):
        if image_analysis:
            candidate = image_analysis.get("candidate_part_name") or "parça adı çıkarılamadı"
            brand_hint = image_analysis.get("detected_brand_text") or "marka okunamadı"
            questions = image_analysis.get("questions_for_user") or []
            question_text = " ".join([f"- {question}" for question in questions[:3]]) if questions else "- Makine türü nedir?\n- Marka/model nedir?"
            message = (
                f"Fotoğraftan tahminim: parça '{candidate}', görünen marka: '{brand_hint}'. "
                f"Doğru parçayı bulmam için şu bilgileri yaz ustam:\n{question_text}"
            )
            return False, False, early_response(message, analysis)

        logger.info("💬 CHAT intent — routing to Gemini for dynamic conversational response")
        return True, False, None

    if is_general_knowledge_intent(intent) and not parts and not extracted_code:
        logger.info(f"🧠 {intent} intent — no catalog search terms, routing to Gemini general knowledge mode")
        return False, True, None

    return False, False, None


def source_quality_early_response(
    *,
    all_sources: list[dict],
    intent: str,
    is_chat_only: bool,
    is_general_knowledge_only: bool,
    raw_user_query: str,
    user_query: str,
    analysis: dict,
    extracted_code: str | None,
    weak_match_min_similarity: float,
    build_no_result_guidance: Callable[[str, dict, str], str],
    best_similarity: Callable[[list[dict]], float | None],
    has_lexical_overlap: Callable[[str, list[dict]], bool],
    has_domain_part_keyword: Callable[[str], bool],
    is_general_knowledge_intent: Callable[[str | None], bool],
    search_scope: dict | None = None,
    confidence_thresholds: ConfidenceThresholds | dict | None = None,
) -> dict | None:
    active_thresholds = (
        _normalize_thresholds(confidence_thresholds)
        if confidence_thresholds is not None
        else resolve_confidence_thresholds(search_scope)
    )
    _record_active_thresholds(analysis, active_thresholds)

    if not all_sources:
        if is_chat_only or is_general_knowledge_only or is_general_knowledge_intent(intent):
            logger.info(f"💬 {intent} intent — no sources, building guarded prompt for Gemini")
            return None
        message = build_no_result_guidance(raw_user_query, analysis, reason="no_result")
        return early_response(message, analysis)

    best_sim = best_similarity(all_sources)
    has_overlap = has_lexical_overlap(user_query, all_sources)
    has_domain_keyword = has_domain_part_keyword(user_query)
    any_high_confidence = any(
        source.get("_semantic_primary")
        or (
            isinstance(source.get("similarity"), (int, float))
            and float(source.get("similarity", 0)) >= active_thresholds.high_confidence
        )
        for source in all_sources
    )
    logger.info(
        f"📊 En iyi benzerlik skoru: {best_sim if best_sim is not None else 'N/A'} | "
        f"lexical_overlap={has_overlap} | domain_keyword={has_domain_keyword} | "
        f"high_confidence={any_high_confidence}"
    )

    is_guarded_mode = is_chat_only or is_general_knowledge_only or is_general_knowledge_intent(intent)
    if not is_guarded_mode:
        confidence_gate_response = ambiguity_guard_response(
            all_sources=all_sources,
            raw_user_query=raw_user_query,
            analysis=analysis,
            extracted_code=extracted_code,
            build_no_result_guidance=build_no_result_guidance,
            confidence_thresholds=active_thresholds,
        )
        if confidence_gate_response:
            return confidence_gate_response

    if not is_guarded_mode and not any_high_confidence and (
        best_sim is not None
        and best_sim < 0.70
        and not has_overlap
        and not has_domain_keyword
        and not extracted_code
    ):
        message = build_no_result_guidance(raw_user_query, analysis, reason="out_of_domain")
        return early_response(message, analysis)

    if not is_guarded_mode and best_sim is not None and best_sim < weak_match_min_similarity:
        message = build_no_result_guidance(raw_user_query, analysis, reason="weak_match")
        return early_response(message, analysis)

    return None
