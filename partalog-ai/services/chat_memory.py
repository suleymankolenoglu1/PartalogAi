"""Conversation memory helpers for chat follow-up references."""

import re

from services.chat_terms import normalize_for_overlap


KNOWN_BRANDS = [
    "JUKI",
    "YAMATO",
    "PEGASUS",
    "BROTHER",
    "TYPICAL",
    "SIRUBA",
    "KANSAI",
    "JACK",
]

MACHINE_GROUP_ALIASES: list[tuple[str, str]] = [
    ("overlok", "Overlok"),
    ("surfile", "Overlok"),
    ("recme", "Reçme"),
    ("recmeci", "Reçme"),
    ("coverstitch", "Reçme"),
    ("duz", "Düz"),
    ("duz dikis", "Düz"),
    ("lockstitch", "Düz"),
]

BRAND_PATTERN = re.compile(r"\b(" + "|".join(KNOWN_BRANDS) + r")\b", re.IGNORECASE)
MODEL_AFTER_BRAND_PATTERN = re.compile(
    r"\b(" + "|".join(KNOWN_BRANDS) + r")\b[\s:/-]*([A-Z]{1,4}[- ]?\d[A-Z0-9-]*)",
    re.IGNORECASE,
)
MODEL_ONLY_PATTERN = re.compile(r"\b([A-Z]{1,4}[- ]?\d[A-Z0-9-]{1,})\b", re.IGNORECASE)


def normalize_model_token(model: str | None) -> str | None:
    if not model:
        return None
    m = re.sub(r"\s+", "-", model.strip().upper())
    m = re.sub(r"-{2,}", "-", m).strip("-")
    if re.match(r"^(REF|SF|SAYFA|PAGE)-?\d+$", m):
        return None
    return m or None


def detect_brand_from_text(text: str) -> str | None:
    if not text:
        return None
    m = BRAND_PATTERN.search(text.upper())
    return m.group(1).upper() if m else None


def detect_machine_group_from_text(text: str) -> str | None:
    norm = normalize_for_overlap(text or "")
    for needle, canonical in MACHINE_GROUP_ALIASES:
        normalized_needle = normalize_for_overlap(needle)
        if re.search(rf"(?<![a-z0-9]){re.escape(normalized_needle)}(?![a-z0-9])", norm):
            return canonical
    return None


def detect_machine_model_from_text(text: str) -> tuple[str | None, str | None]:
    if not text:
        return None, None

    up = (text or "").upper()
    m = MODEL_AFTER_BRAND_PATTERN.search(up)
    if m:
        brand = m.group(1).upper()
        model = normalize_model_token(m.group(2))
        return model, brand

    m2 = MODEL_ONLY_PATTERN.search(up)
    if m2:
        model = normalize_model_token(m2.group(1))
        if model and any(ch.isdigit() for ch in model):
            return model, None

    return None, None


def model_token_matches(requested_model: str | None, source_model: str | None) -> bool:
    if not requested_model or not source_model:
        return False
    requested_compact = re.sub(r"[^A-Z0-9]", "", str(requested_model).upper())
    source_compact = re.sub(r"[^A-Z0-9]", "", str(source_model).upper())
    return bool(requested_compact and requested_compact in source_compact)


def extract_sticky_context_from_history(history_list: list | None) -> dict:
    sticky_brand = None
    sticky_machine_group = None
    sticky_machine_model = None
    sticky_part_code = None
    sticky_part_name = None
    sticky_page_number = None
    sticky_ref_no = None
    sticky_part_candidates: list[dict] = []

    messages = history_list or []
    for msg in reversed(messages):
        text = str((msg or {}).get("text") or "")
        if not text:
            continue

        if not sticky_brand:
            sticky_brand = detect_brand_from_text(text)

        if not sticky_machine_group:
            sticky_machine_group = detect_machine_group_from_text(text)

        if not sticky_machine_model:
            model, model_brand = detect_machine_model_from_text(text)
            if model:
                sticky_machine_model = model
            if not sticky_brand and model_brand:
                sticky_brand = model_brand

        if not sticky_part_candidates:
            sticky_part_candidates = extract_sticky_part_candidates_from_text(text)

        if not sticky_part_code:
            part_code, part_name = extract_sticky_part_from_text(text)
            if not part_code and sticky_part_candidates:
                part_code = sticky_part_candidates[0].get("code")
                part_name = sticky_part_candidates[0].get("name")
            if part_code:
                sticky_part_code = part_code
            if part_name:
                sticky_part_name = part_name

        if not sticky_page_number or not sticky_ref_no:
            page_number, ref_no = extract_sticky_part_location_from_text(text)
            if page_number and not sticky_page_number:
                sticky_page_number = page_number
            if ref_no and not sticky_ref_no:
                sticky_ref_no = ref_no

        if sticky_brand and sticky_machine_group and sticky_machine_model and sticky_part_code:
            break

    return {
        "brand": sticky_brand,
        "machine_group": sticky_machine_group,
        "machine_model": sticky_machine_model,
        "part_code": sticky_part_code,
        "part_name": sticky_part_name,
        "page_number": sticky_page_number,
        "ref_no": sticky_ref_no,
        "part_candidates": sticky_part_candidates,
    }


def extract_sticky_part_from_text(text: str | None) -> tuple[str | None, str | None]:
    raw = str(text or "").strip()
    if not raw:
        return None, None

    explicit_patterns = (
        r"\b(?:kod|parça kodu|parca kodu)\s*[:：]\s*([A-Z0-9-]{5,})\b",
        r"\b([A-Z]{1,4}\d[A-Z0-9-]{3,}|\d{6,}[A-Z0-9-]*)\s+kodlu\b",
    )
    for pattern in explicit_patterns:
        match = re.search(pattern, raw, flags=re.IGNORECASE)
        if match:
            code = match.group(1).strip().upper()
            return code, extract_sticky_part_name_near_code(raw, code)

    line_pattern = re.compile(
        r"\b([A-Z]{1,4}\d[A-Z0-9-]{3,}|\d{6,}[A-Z0-9-]*)\s*[-–]\s*([A-ZÂÇĞİÖŞÜ0-9_ /.-]{3,})",
        flags=re.IGNORECASE,
    )
    for match in line_pattern.finditer(raw):
        code = match.group(1).strip().upper()
        name = clean_sticky_part_name(match.group(2))
        if name:
            return code, name

    return None, None


def extract_sticky_part_candidates_from_text(text: str | None) -> list[dict]:
    raw = str(text or "").strip()
    if not raw:
        return []

    candidate_area = raw
    marker = re.search(r"aday parça(?:\(lar\))?\s*[:：]\s*", raw, flags=re.IGNORECASE)
    if marker:
        candidate_area = raw[marker.end():]

    candidates: list[dict] = []
    seen_codes: set[str] = set()
    for segment in re.split(r"\s*;\s*", candidate_area):
        match = re.search(
            r"\b([A-Z]{1,4}\d[A-Z0-9-]{3,}|\d{6,}[A-Z0-9-]*)\s*[-–]\s*([A-ZÂÇĞİÖŞÜ0-9_ /.-]{3,})",
            segment,
            flags=re.IGNORECASE,
        )
        if not match:
            continue
        code = match.group(1).strip().upper()
        if code in seen_codes:
            continue
        name = clean_sticky_part_name(match.group(2))
        if not name:
            continue
        page_number, ref_no = extract_sticky_part_location_from_text(segment)
        candidates.append(
            {
                "code": code,
                "name": name,
                "pageNumber": page_number or "",
                "refNo": ref_no or "",
                "source": "sticky_history",
            }
        )
        seen_codes.add(code)
        if len(candidates) >= 6:
            break

    return candidates


def select_sticky_candidate_from_query(user_query: str | None, candidates: list[dict] | None) -> dict | None:
    if not candidates:
        return None

    normalized_query = normalize_for_overlap(user_query or "")
    if not normalized_query.strip():
        return None

    has_candidate_context = any(
        token in normalized_query
        for token in ("parca", "aday", "kod", "ref", "sayfa", "sf", "listedeki", "sonuc")
    )
    has_last_reference = re.search(r"\bson(?:uncu\w*)?\b", normalized_query) is not None
    if not has_candidate_context and not has_last_reference:
        return None

    ordinal_patterns: list[tuple[int, tuple[str, ...]]] = [
        (0, (r"(?<!\d)1\.(?!\d)", r"\bilk(?:i|ini|inci)?\b", r"\bbirinci\w*\b")),
        (1, (r"(?<!\d)2\.(?!\d)", r"\bikinci\w*\b")),
        (2, (r"(?<!\d)3\.(?!\d)", r"\bucuncu\w*\b")),
        (3, (r"(?<!\d)4\.(?!\d)", r"\bdorduncu\w*\b")),
        (4, (r"(?<!\d)5\.(?!\d)", r"\bbesinci\w*\b")),
        (5, (r"(?<!\d)6\.(?!\d)", r"\baltinci\w*\b")),
    ]

    if has_last_reference:
        return candidates[-1]

    for index, patterns in ordinal_patterns:
        if any(re.search(pattern, normalized_query) for pattern in patterns):
            return candidates[index] if index < len(candidates) else None

    return None


def extract_sticky_part_name_near_code(text: str, code: str) -> str | None:
    part_label = re.search(r"\b(?:parça|parca)\s*[:：]\s*([A-ZÂÇĞİÖŞÜ0-9_ /.-]{3,})", text, flags=re.IGNORECASE)
    if part_label:
        return clean_sticky_part_name(part_label.group(1))

    paren = re.search(rf"{re.escape(code)}\s*\(([^,\n)]+)", text, flags=re.IGNORECASE)
    if paren:
        return clean_sticky_part_name(paren.group(1))

    return None


def extract_sticky_part_location_from_text(text: str | None) -> tuple[str | None, str | None]:
    raw = str(text or "")
    if not raw.strip():
        return None, None

    page_match = re.search(r"\b(?:sf|sayfa|page)\.?\s*[:：]?\s*(\d{1,4})\b", raw, flags=re.IGNORECASE)
    ref_match = re.search(r"\b(?:ref|ref no|ref\. no)\.?\s*[:：]?\s*([A-Z0-9.-]{1,12})\b", raw, flags=re.IGNORECASE)
    page_number = page_match.group(1).strip() if page_match else None
    ref_no = ref_match.group(1).strip().upper() if ref_match else None
    return page_number, ref_no


def clean_sticky_part_name(value: str | None) -> str | None:
    text = str(value or "").strip()
    if not text:
        return None
    text = re.split(r"\s*(?:\(|,|\.| Kaynak:| Sf | Ref )", text, maxsplit=1)[0].strip()
    text = re.sub(r"\s+", " ", text)
    return text.upper() if text else None


def format_source_identity(source: dict) -> str:
    code = str(source.get("code") or "-").strip()
    name = str(source.get("name") or "Parça").strip()
    ref_no = str(source.get("refNo") or "").strip()
    page = str(source.get("pageNumber") or "").strip()
    location_bits = []
    if page:
        location_bits.append(f"Sf {page}")
    if ref_no:
        location_bits.append(f"Ref {ref_no}")
    location_text = f" ({', '.join(location_bits)})" if location_bits else ""
    return f"{code} - {name}{location_text}"


def active_candidate_sources_from_analysis(analysis: dict | None) -> list[dict]:
    analysis = analysis or {}
    current_context = analysis.get("current_context") if isinstance(analysis.get("current_context"), dict) else {}
    raw_candidates = current_context.get("candidateSources") or analysis.get("candidate_sources") or []
    candidates: list[dict] = []
    seen_codes: set[str] = set()

    if isinstance(raw_candidates, list):
        for item in raw_candidates:
            if not isinstance(item, dict):
                continue
            code = str(item.get("code") or item.get("partCode") or "").strip()
            name = str(item.get("name") or item.get("partName") or "Seçili parça").strip()
            if not code and not name:
                continue
            dedupe_key = code.upper() or name.upper()
            if dedupe_key in seen_codes:
                continue
            candidates.append(
                {
                    "code": code,
                    "name": name,
                    "brand": str(item.get("brand") or analysis.get("brand") or current_context.get("machineBrand") or "").strip(),
                    "pageNumber": str(item.get("pageNumber") or item.get("page") or "").strip(),
                    "refNo": str(item.get("refNo") or item.get("ref") or "").strip(),
                    "catalogId": str(item.get("catalogId") or current_context.get("catalogId") or "").strip(),
                    "source": str(item.get("source") or "active_context"),
                }
            )
            seen_codes.add(dedupe_key)

    code = str(analysis.get("part_code") or current_context.get("partCode") or "").strip()
    name = str(analysis.get("part_name") or current_context.get("partName") or "").strip()
    if code or name:
        dedupe_key = code.upper() or name.upper()
        if dedupe_key not in seen_codes:
            candidates.append(
                {
                    "code": code,
                    "name": name or "Seçili parça",
                    "brand": str(analysis.get("brand") or current_context.get("machineBrand") or "").strip(),
                    "pageNumber": str(current_context.get("pageNumber") or "").strip(),
                    "refNo": str(current_context.get("refNo") or "").strip(),
                    "catalogId": str(current_context.get("catalogId") or "").strip(),
                    "source": "active_context",
                }
            )

    return candidates


def format_active_part_context(analysis: dict | None) -> str | None:
    candidates = active_candidate_sources_from_analysis(analysis)
    if candidates:
        return "; ".join(format_source_identity(source) for source in candidates[:4])

    analysis = analysis or {}
    current_context = analysis.get("current_context") if isinstance(analysis.get("current_context"), dict) else {}
    code = str(analysis.get("part_code") or current_context.get("partCode") or "").strip()
    name = str(analysis.get("part_name") or current_context.get("partName") or "").strip()
    page = str(current_context.get("pageNumber") or "").strip()
    ref_no = str(current_context.get("refNo") or "").strip()

    if not code and not name:
        return None

    identity = " - ".join([item for item in (code, name) if item])
    location_bits = []
    if page:
        location_bits.append(f"Sf {page}")
    if ref_no:
        location_bits.append(f"Ref {ref_no}")
    if location_bits:
        identity += f" ({', '.join(location_bits)})"
    return identity


def build_active_context_source(analysis: dict | None) -> dict | None:
    sources = build_active_context_sources(analysis)
    return sources[0] if sources else None


def build_active_context_sources(analysis: dict | None) -> list[dict]:
    candidates = active_candidate_sources_from_analysis(analysis)
    if candidates:
        return candidates

    analysis = analysis or {}
    current_context = analysis.get("current_context") if isinstance(analysis.get("current_context"), dict) else {}
    code = str(analysis.get("part_code") or current_context.get("partCode") or "").strip()
    if not code:
        return []

    return [{
        "code": code,
        "name": str(analysis.get("part_name") or current_context.get("partName") or "Seçili parça").strip(),
        "brand": str(analysis.get("brand") or current_context.get("machineBrand") or "").strip(),
        "pageNumber": str(current_context.get("pageNumber") or "").strip(),
        "refNo": str(current_context.get("refNo") or "").strip(),
        "catalogId": str(current_context.get("catalogId") or "").strip(),
        "source": "active_context",
    }]
