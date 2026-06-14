"""Terminology helpers used by chat retrieval and routing."""

import re

from domain.chat_lexicon import (
    CONTEXT_RELATED_PART_TERMS,
    DIAGNOSIS_SEARCH_TERMS,
    DOMAIN_PART_TERMS,
    GENERAL_KNOWLEDGE_INTENTS,
    TERM_EXPANSIONS,
)


def normalize_for_overlap(text: str) -> str:
    text = (text or "").replace("İ", "i").lower()
    text = (
        text.replace("ı", "i")
        .replace("ş", "s")
        .replace("ğ", "g")
        .replace("â", "a")
        .replace("ü", "u")
        .replace("ö", "o")
        .replace("ç", "c")
    )
    return text


def extract_overlap_tokens(text: str) -> list[str]:
    norm = normalize_for_overlap(text)
    raw_tokens = re.findall(r"[a-z0-9]+", norm)
    stop = {
        "ve", "ile", "icin", "mi", "mu", "mü", "mı", "var", "yok",
        "arayan", "ariyorum", "ariyorum", "lazim", "tam", "olarak",
        "bir", "bu", "su", "de", "da", "ki", "ya", "ama", "gibi",
    }
    out: list[str] = []
    for token in raw_tokens:
        if token in stop:
            continue
        if len(token) >= 3 or any(ch.isdigit() for ch in token):
            out.append(token)

    seen = set()
    unique_tokens = []
    for token in out:
        if token in seen:
            continue
        seen.add(token)
        unique_tokens.append(token)
    return unique_tokens


def has_domain_part_keyword(text: str) -> bool:
    tokens = set(extract_overlap_tokens(text))
    return any(token in DOMAIN_PART_TERMS for token in tokens)


def is_general_knowledge_intent(intent: str | None) -> bool:
    return str(intent or "").upper() in GENERAL_KNOWLEDGE_INTENTS


def diagnosis_terms_for_query(text: str) -> list[str]:
    norm = normalize_for_overlap(text or "")
    terms: list[str] = []
    for needles, mapped_terms in DIAGNOSIS_SEARCH_TERMS:
        if any(normalize_for_overlap(needle) in norm for needle in needles):
            for term in mapped_terms:
                if term not in terms:
                    terms.append(term)
    return terms


def related_part_terms_for_context(text: str | None) -> list[str]:
    norm = normalize_for_overlap(text or "")
    terms: list[str] = []
    if not norm:
        return terms
    for needles, mapped_terms in CONTEXT_RELATED_PART_TERMS:
        if any(normalize_for_overlap(needle) in norm for needle in needles):
            for term in mapped_terms:
                if term not in terms:
                    terms.append(term)
    wants_family = any(hint in norm for hint in ("hangi parcalar", "hangi parçalar", "hangi parcalari", "hangi parçaları"))
    if wants_family:
        family_orders = (
            ("kayar kapak", ("kayar kapak", "kapak destek", "kapak pimi", "vida")),
            ("aşağı açılır kapak", ("aşağı açılır kapak", "kapak pimi", "menteşe", "vida", "rondela")),
            ("kumaş plaka", ("kumaş plaka", "arka plaka", "plaka blok", "vida")),
        )
        for family_term, ordered_terms in family_orders:
            if family_term in terms:
                reordered = [term for term in ordered_terms if term in terms]
                reordered.extend(term for term in terms if term not in reordered)
                return reordered
    return terms


def split_expanded_search_terms(search_text: str | None) -> list[str]:
    terms: list[str] = []
    for item in str(search_text or "").split("|"):
        cleaned = item.strip()
        if cleaned and cleaned not in terms:
            terms.append(cleaned)
    return terms


def expand_part_search_text(part_name: str | None) -> str | None:
    if not part_name:
        return None

    base = str(part_name).strip()
    if not base:
        return None

    norm = normalize_for_overlap(base)
    expansions: list[str] = []
    for key, values in TERM_EXPANSIONS.items():
        if normalize_for_overlap(key) in norm:
            expansions.extend(values)

    if not expansions:
        return base

    merged = [base]
    for item in expansions:
        if item and item not in merged:
            merged.append(item)
    return " | ".join(merged)
