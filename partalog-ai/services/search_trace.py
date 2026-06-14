from __future__ import annotations

from typing import Any, Literal

from pydantic import BaseModel, Field


class RewrittenQueryTrace(BaseModel):
    text: str
    source: Literal["gemini_query_rewriter", "fallback"] = "fallback"


class ResolvedScopeTrace(BaseModel):
    catalog_id: str | None = None
    catalog_ids: list[str] = Field(default_factory=list)
    brand: str | None = None
    machine_model: str | None = None
    scope_source: str | None = None


class CandidateScoreTrace(BaseModel):
    code: str | None = None
    name: str | None = None
    score: float | None = None


class FinalDecisionTrace(BaseModel):
    decision: Literal["EXACT_MATCH", "AMBIGUITY", "LOW_CONFIDENCE"]
    candidate_scores: list[CandidateScoreTrace] = Field(default_factory=list)


class SearchTraceLog(BaseModel):
    original_query: str
    rewritten_query: RewrittenQueryTrace
    resolved_scope: ResolvedScopeTrace
    retrieved_candidates_count: int = 0
    compatibility_gate_filtered_count: int = 0
    final_decision: FinalDecisionTrace
    applied_high_threshold: float | None = None
    applied_low_threshold: float | None = None
    applied_ambiguity_delta: float | None = None
    threshold_source: str | None = None


def model_to_dict(model: BaseModel) -> dict[str, Any]:
    if hasattr(model, "model_dump"):
        return model.model_dump()
    return model.dict()


def build_search_trace(
    *,
    original_query: str,
    analysis: dict | None = None,
    search_scope: dict | None = None,
    sources: list[dict] | None = None,
    retrieved_candidates_count: int = 0,
    compatibility_gate_filtered_count: int = 0,
    query_rewrite_debug: dict | None = None,
    extracted_code: str | None = None,
) -> dict[str, Any]:
    analysis = analysis or {}
    search_scope = search_scope or {}
    sources = sources or []

    rewritten_text = str((query_rewrite_debug or {}).get("rewritten") or original_query or "")
    rewrite_source = (query_rewrite_debug or {}).get("source")
    if rewrite_source != "gemini_query_rewriter":
        rewrite_source = "fallback"

    catalog_ids = [
        str(item)
        for item in (search_scope.get("resolved_catalog_ids") or search_scope.get("catalog_ids") or [])
        if str(item).strip()
    ]

    candidate_scores = [
        CandidateScoreTrace(
            code=source.get("code") or source.get("PartCode"),
            name=source.get("name") or source.get("PartName"),
            score=_source_score(source),
        )
        for source in sources[:5]
    ]

    confidence_gate = str(analysis.get("confidence_gate") or "").lower()
    if confidence_gate == "ambiguous":
        decision = "AMBIGUITY"
    elif confidence_gate == "low" or not sources:
        decision = "LOW_CONFIDENCE"
    elif extracted_code or analysis.get("part_code") or confidence_gate in {"high", "medium_clear", "medium_single"}:
        decision = "EXACT_MATCH"
    else:
        decision = "EXACT_MATCH"

    trace = SearchTraceLog(
        original_query=original_query,
        rewritten_query=RewrittenQueryTrace(text=rewritten_text, source=rewrite_source),
        resolved_scope=ResolvedScopeTrace(
            catalog_id=catalog_ids[0] if catalog_ids else None,
            catalog_ids=catalog_ids,
            brand=search_scope.get("resolved_brand") or analysis.get("resolved_brand") or analysis.get("brand"),
            machine_model=(
                search_scope.get("resolved_machine_model")
                or analysis.get("resolved_machine_model")
                or analysis.get("machine_model")
            ),
            scope_source=search_scope.get("scope_source") or analysis.get("scope_source"),
        ),
        retrieved_candidates_count=max(int(retrieved_candidates_count or 0), len(sources)),
        compatibility_gate_filtered_count=int(compatibility_gate_filtered_count or 0),
        final_decision=FinalDecisionTrace(decision=decision, candidate_scores=candidate_scores),
        applied_high_threshold=_threshold_value(analysis, "applied_high_threshold", "high_confidence"),
        applied_low_threshold=_threshold_value(analysis, "applied_low_threshold", "low_confidence"),
        applied_ambiguity_delta=_threshold_value(analysis, "applied_ambiguity_delta", "ambiguity_score_delta"),
        threshold_source=_threshold_source(analysis),
    )
    return model_to_dict(trace)


def _source_score(source: dict) -> float | None:
    for key in ("similarity", "visual_similarity", "visualSimilarity"):
        value = source.get(key)
        if isinstance(value, (int, float)):
            return round(float(value), 4)
    return None


def _threshold_value(analysis: dict, flat_key: str, nested_key: str) -> float | None:
    value = analysis.get(flat_key)
    if not isinstance(value, (int, float)):
        thresholds = analysis.get("applied_confidence_thresholds")
        value = thresholds.get(nested_key) if isinstance(thresholds, dict) else None
    if isinstance(value, (int, float)):
        return float(value)
    return None


def _threshold_source(analysis: dict) -> str | None:
    value = analysis.get("threshold_source")
    if not value:
        thresholds = analysis.get("applied_confidence_thresholds")
        value = thresholds.get("source") if isinstance(thresholds, dict) else None
    if value:
        return str(value)
    return None
