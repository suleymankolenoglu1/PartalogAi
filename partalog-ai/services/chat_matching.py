"""Matching, filtering and reranking helpers for chat retrieval."""

from loguru import logger

from domain.assembly_rules import CONTEXT_SOURCE_RULES
from domain.chat_lexicon import (
    DIAGNOSIS_RESULT_SYNONYMS,
    DOMAIN_PART_TERMS,
    STRICT_PART_LOOKUP_TERMS,
)
from services.chat_terms import (
    extract_overlap_tokens,
    normalize_for_overlap,
)


def best_similarity(sources: list) -> float | None:
    sims: list[float] = []
    for source in sources or []:
        for key in ("similarity", "visual_similarity"):
            val = source.get(key)
            if isinstance(val, (int, float)):
                sims.append(float(val))
    return max(sims) if sims else None


def has_lexical_overlap(user_query: str, sources: list[dict]) -> bool:
    query_tokens = extract_overlap_tokens(user_query or "")
    if not query_tokens:
        return False

    haystack_parts: list[str] = []
    for source in sources or []:
        haystack_parts.append(str(source.get("code") or ""))
        haystack_parts.append(str(source.get("name") or ""))
        haystack_parts.append(str(source.get("machine_model") or ""))
        haystack_parts.append(str(source.get("brand") or ""))
        haystack_parts.append(str(source.get("description") or ""))

    haystack = normalize_for_overlap(" ".join(haystack_parts))
    if not haystack.strip():
        return False

    return any(tok in haystack for tok in query_tokens)


def diagnosis_result_matches_term(row: dict, part_name: str | None, search_text: str | None) -> bool:
    haystack = normalize_for_overlap(
        " ".join(
            [
                str(row.get("PartName") or ""),
                str(row.get("Description") or ""),
                str(row.get("PartCode") or ""),
                str(row.get("RefNumber") or ""),
                str(row.get("Mechanism") or ""),
                str(row.get("Dimensions") or ""),
            ]
        )
    )
    if not haystack.strip():
        return False

    candidates: list[str] = []
    for raw in (part_name, search_text):
        if not raw:
            continue
        candidates.extend([part.strip() for part in str(raw).split("|") if part.strip()])

    for candidate in candidates:
        normalized_candidate = normalize_for_overlap(candidate)
        synonyms = DIAGNOSIS_RESULT_SYNONYMS.get(normalized_candidate, ())
        if not synonyms:
            synonyms = tuple(extract_overlap_tokens(candidate))

        if any(normalize_for_overlap(term) in haystack for term in synonyms if term):
            return True

    return False


def filter_diagnosis_results(results: list[dict], part_name: str | None, search_text: str | None) -> list[dict]:
    if not results:
        return []

    filtered: list[dict] = []
    for row in results:
        sim = row.get("similarity")
        if not isinstance(sim, (int, float)):
            sim = row.get("visual_similarity")
        sim_value = float(sim) if isinstance(sim, (int, float)) else 0.0

        if diagnosis_result_matches_term(row, part_name, search_text):
            if sim_value >= 0.68:
                filtered.append(row)
            continue

        if sim_value >= 0.82 and not row.get("fallback"):
            filtered.append(row)

    if len(filtered) != len(results):
        logger.info(
            "🩺 Diagnosis result filter: part='{}' {} -> {}",
            part_name,
            len(results),
            len(filtered),
        )

    return filtered


def is_strict_part_lookup(user_query: str, part_name: str | None, intent: str | None) -> bool:
    normalized_intent = str(intent or "").upper()
    if normalized_intent not in {"SEARCH", "COMPATIBILITY", "COMPARE"}:
        return False

    norm_query = normalize_for_overlap(user_query or "")
    norm_part = normalize_for_overlap(part_name or "")
    asks_for_code = any(token in norm_query for token in ("kodu", "kodunu", "part no", "parca no", "parça no"))
    has_strict_term = any(term in norm_query or term in norm_part for term in STRICT_PART_LOOKUP_TERMS)
    return asks_for_code and has_strict_term


def filter_strict_part_lookup_results(results: list[dict], part_name: str | None, search_text: str | None) -> list[dict]:
    if not results:
        return []

    filtered = [
        row for row in results
        if diagnosis_result_matches_term(row, part_name, search_text)
    ]

    if len(filtered) != len(results):
        logger.info(
            "🎯 Strict part lookup filter: part='{}' {} -> {}",
            part_name,
            len(results),
            len(filtered),
        )

    return filtered


def limit_context_related_sources(sources: list[dict], related_terms: list[str] | None) -> list[dict]:
    if not sources or not related_terms:
        return sources

    norm_terms = [normalize_for_overlap(term) for term in related_terms if term]
    matching_rule = next(
        (
            rule
            for term in norm_terms
            for rule in CONTEXT_SOURCE_RULES
            if term in rule.triggers
        ),
        None,
    )
    if not matching_rule:
        return sources

    selected: list[dict] = []
    selected_codes: set[str] = set()

    def source_text(source: dict) -> str:
        return normalize_for_overlap(
            " ".join(
                [
                    str(source.get("name") or "").replace("_", " "),
                    str(source.get("query") or "").replace("_", " "),
                    str(source.get("code") or ""),
                ]
            )
        )

    def source_name_text(source: dict) -> str:
        return normalize_for_overlap(str(source.get("name") or "").replace("_", " "))

    def matches_group(source: dict, rule) -> bool:
        haystack = source_name_text(source) if rule.name_only else source_text(source)
        if rule.exact_name:
            return haystack == normalize_for_overlap(rule.needles[0])
        return any(normalize_for_overlap(needle) in haystack for needle in rule.needles)

    for group_rule in matching_rule.groups:
        matches = [
            source for source in sources
            if source.get("code") not in selected_codes
            and matches_group(source, group_rule)
        ]
        matches.sort(key=lambda source: float(source.get("similarity") or 0), reverse=True)
        if matches:
            selected.append(matches[0])
            selected_codes.add(str(matches[0].get("code") or ""))

    if selected:
        logger.info("🧹 Context source limit: {} {} -> {}", matching_rule.label, len(sources), len(selected))
        return selected

    return sources


def extract_requested_domain_terms(*texts: str) -> list[str]:
    out: list[str] = []
    for text in texts:
        for tok in extract_overlap_tokens(text or ""):
            if tok in DOMAIN_PART_TERMS and tok not in out:
                out.append(tok)
    return out


def brand_matches_available(extracted_brand: str, available_brands: list[str]) -> bool:
    if not extracted_brand:
        return True
    expected = normalize_for_overlap(extracted_brand).strip()
    if not expected:
        return True

    for brand in available_brands:
        normalized = normalize_for_overlap(brand).strip()
        if not normalized:
            continue
        if expected == normalized or expected in normalized or normalized in expected:
            return True
    return False


def filter_results_by_requested_terms(results: list[dict], requested_terms: list[str]) -> list[dict]:
    if not results or not requested_terms:
        return results

    protected_results = [
        row for row in results
        if row.get("_semantic_primary")
        or (isinstance(row.get("similarity"), (int, float)) and float(row.get("similarity", 0)) >= 0.75)
    ]
    remaining = [row for row in results if row not in protected_results]

    if not remaining:
        return results

    name_matched: list[dict] = []
    for row in remaining:
        part_name_norm = normalize_for_overlap(str(row.get("PartName") or ""))
        if any(term in part_name_norm for term in requested_terms):
            name_matched.append(row)

    if name_matched:
        merged = protected_results + name_matched
        logger.info(
            f"🧪 PartName term filtresi (semantic korumalı): terms={requested_terms} | "
            f"{len(results)} -> {len(merged)} ({len(protected_results)} semantic korundu)"
        )
        return merged

    filtered: list[dict] = []
    for row in remaining:
        hay = normalize_for_overlap(
            " ".join(
                [
                    str(row.get("PartName") or ""),
                    str(row.get("Description") or ""),
                    str(row.get("PartCode") or ""),
                    str(row.get("RefNumber") or ""),
                    str(row.get("Dimensions") or ""),
                ]
            )
        )
        if any(term in hay for term in requested_terms):
            filtered.append(row)

    if filtered:
        merged = protected_results + filtered
        logger.info(
            f"🧪 Domain term filtresi (semantic korumalı): terms={requested_terms} | "
            f"{len(results)} -> {len(merged)} ({len(protected_results)} semantic korundu)"
        )
        return merged

    if protected_results:
        logger.info(
            f"🧪 Semantic match korundu: {len(protected_results)} sonuç (requested_terms={requested_terms} "
            f"hiçbir remaining sonuçta bulunamadı ancak semantic skor yeterli)"
        )
        return protected_results

    return results


def rerank_results_by_context_part(results: list[dict], context_part: str | None) -> list[dict]:
    if not results or not context_part:
        return results

    context_tokens = [tok for tok in extract_overlap_tokens(context_part) if len(tok) >= 3]
    if not context_tokens:
        return results

    scored_rows: list[tuple[int, float, dict]] = []
    for row in results:
        hay = normalize_for_overlap(
            " ".join(
                [
                    str(row.get("PartName") or ""),
                    str(row.get("Description") or ""),
                    str(row.get("Mechanism") or ""),
                    str(row.get("Dimensions") or ""),
                    str(row.get("MachineModel") or ""),
                ]
            )
        )
        context_score = sum(1 for tok in context_tokens if tok in hay)
        sim = row.get("similarity")
        if not isinstance(sim, (int, float)):
            sim = row.get("visual_similarity")
        sim_score = float(sim) if isinstance(sim, (int, float)) else 0.0
        scored_rows.append((context_score, sim_score, row))

    max_ctx = max((item[0] for item in scored_rows), default=0)
    if max_ctx <= 0:
        return results

    scored_rows.sort(key=lambda item: (item[0], item[1]), reverse=True)
    reranked = [item[2] for item in scored_rows]
    logger.info(
        f"🧭 Context rerank uygulandı: context='{context_part}' tokens={context_tokens[:6]} "
        f"max_ctx={max_ctx} count={len(results)}"
    )
    return reranked
