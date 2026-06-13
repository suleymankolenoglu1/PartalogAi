"""Search-part construction for the chat pipeline."""

from services.chat_terms import (
    diagnosis_terms_for_query,
    normalize_for_overlap,
    related_part_terms_for_context,
)


def split_terms(text: str) -> list[str]:
    if not text:
        return []
    separators = [" ve ", " & ", ",", ";", "/", " ile "]
    parts = [text]
    for separator in separators:
        parts = [part for chunk in parts for part in chunk.split(separator)]
    return [part.strip() for part in parts if part.strip()]


def infer_part_name_from_query(text: str | None) -> str | None:
    normalized = normalize_for_overlap(text or "")
    if not normalized:
        return None

    rules = (
        (("model plaka", "marka plaka", "kimlik plaka", "isim etiket", "name plate", "nameplate", "rating plate"), "model plakası"),
        (("ruzgar kilavuz", "wind guide"), "rüzgar kılavuz plakası"),
        (("igne plaka", "needle plate", "throat plate"), "iğne plakası"),
        (("seri numara plaka", "serial plate"), "seri numara plakası"),
    )
    for needles, part_name in rules:
        if any(needle in normalized for needle in needles):
            return part_name

    if "plaka" in normalized and any(hint in normalized for hint in ("kod", "parca no", "part no")):
        return "plaka"
    return None


def build_search_parts(
    analysis: dict,
    *,
    intent: str,
    extracted_part: str | None,
    extracted_code: str | None,
    extracted_dimensions: str | None,
    extracted_context_part: str | None,
    raw_user_query: str,
    user_query: str,
) -> list[dict]:
    parts = analysis.get("parts")
    if not parts:
        if extracted_part or extracted_code:
            parts = [
                {
                    "part_name": extracted_part,
                    "part_code": extracted_code,
                    "dimensions": extracted_dimensions,
                    "context_part": extracted_context_part,
                }
            ]
        else:
            parts = []

    if len(parts) <= 1 and intent == "SEARCH" and not extracted_code:
        inferred_part = infer_part_name_from_query(raw_user_query or user_query)
        if inferred_part and not any(
            normalize_for_overlap(str((part or {}).get("part_name") or "")) == normalize_for_overlap(inferred_part)
            for part in parts
            if isinstance(part, dict)
        ):
            parts = [
                {
                    "part_name": inferred_part,
                    "part_code": None,
                    "dimensions": extracted_dimensions,
                    "context_part": extracted_context_part,
                    "source": "deterministic_part_phrase",
                }
            ]
            analysis["part_name"] = inferred_part
            analysis["deterministic_part_name"] = inferred_part

    if len(parts) <= 1 and intent == "SEARCH" and not extracted_code:
        fallback_parts = split_terms(user_query)
        if len(fallback_parts) > 1:
            parts = [
                {
                    "part_name": part,
                    "part_code": None,
                    "dimensions": None,
                    "context_part": extracted_context_part,
                }
                for part in fallback_parts
            ]

    if intent == "DIAGNOSE" and not parts:
        diagnosis_search_terms = diagnosis_terms_for_query(raw_user_query or user_query)
        if diagnosis_search_terms:
            parts = [
                {
                    "part_name": term,
                    "part_code": None,
                    "dimensions": None,
                    "context_part": extracted_context_part,
                    "source": "diagnosis_symptom_map",
                }
                for term in diagnosis_search_terms[:5]
            ]
            analysis["diagnosis_search_terms"] = diagnosis_search_terms

    if parts:
        normalized_parts = []
        for part in parts:
            if isinstance(part, dict):
                normalized_part = dict(part)
                normalized_part.setdefault("context_part", extracted_context_part)
                normalized_parts.append(normalized_part)
        parts = normalized_parts if normalized_parts else parts

    related_context_terms = related_part_terms_for_context(
        " ".join([raw_user_query or user_query, extracted_context_part or ""])
    )
    if related_context_terms:
        existing_terms = {
            normalize_for_overlap(str((part or {}).get("part_name") or ""))
            for part in parts
            if isinstance(part, dict)
        }
        for term in related_context_terms:
            normalized_term = normalize_for_overlap(term)
            if normalized_term and normalized_term not in existing_terms:
                parts.append(
                    {
                        "part_name": term,
                        "part_code": None,
                        "dimensions": None,
                        "context_part": extracted_context_part,
                        "source": "context_related_part_map",
                    }
                )
                existing_terms.add(normalized_term)
        analysis["context_related_terms"] = related_context_terms

    analysis["parts"] = parts
    return parts
