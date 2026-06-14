"""Source normalization helpers for chat retrieval results."""

import urllib.parse


_FALLBACK_REASON_LABELS = {
    "context_page_match": "aktif sayfa/ref bağlamından aday oldu",
    "brand_removed": "marka filtresi kaldırılınca aday oldu; uyum teyidi gerekir",
    "machine_group_removed": "makine tipi filtresi kaldırılınca aday oldu; uyum teyidi gerekir",
    "all_filters_removed": "filtreler gevşetilince aday oldu; mutlaka model/kod teyidi gerekir",
}

_VISUAL_REASON_LABELS = {
    "ocr_code": "görselde okunan kodla eşleşti",
    "structured_hints": "görsel şekil/konum ipuçlarıyla aday oldu",
    "text_embedding_fallback": "görsel açıklamasından metin aramasıyla aday oldu",
}


def normalize_location_fields(catalog_id_value, page_number_value) -> tuple[str | None, str]:
    catalog_id = None
    if catalog_id_value is not None:
        raw = str(catalog_id_value).strip()
        if raw:
            catalog_id = raw

    page_number = str(page_number_value).strip() if page_number_value is not None else ""
    if not page_number:
        page_number = "1"

    return catalog_id, page_number


def _clean_query_label(query: str | None) -> str | None:
    value = str(query or "").strip()
    if not value:
        return None
    value = value.split("|")[0].strip()
    if len(value) > 42:
        value = value[:39].rstrip() + "..."
    return value or None


def build_source_match_reason(source: dict) -> str:
    reasons: list[str] = []

    query_label = _clean_query_label(source.get("query"))
    page = str(source.get("pageNumber") or "").strip()
    ref_no = str(source.get("refNo") or "").strip()

    visual_reason = source.get("visual_match_reason")
    if visual_reason:
        reasons.append(_VISUAL_REASON_LABELS.get(str(visual_reason), f"görsel sinyal: {visual_reason}"))

    if source.get("fallback"):
        fallback_reason = source.get("fallback_reason")
        reasons.append(_FALLBACK_REASON_LABELS.get(str(fallback_reason), "genişletilmiş aramada aday oldu"))

    similarity = source.get("similarity")
    visual_similarity = source.get("visual_similarity")
    score = similarity if isinstance(similarity, (int, float)) else visual_similarity
    if query_label and isinstance(score, (int, float)) and score >= 0.72:
        reasons.append(f"{query_label} araması bu parçayla eşleşti")
    elif query_label:
        reasons.append(f"{query_label} aramasından aday oldu")
    elif isinstance(score, (int, float)):
        if score >= 0.99:
            reasons.append("kod/kayıt eşleşmesi güçlü görünüyor")
        elif score >= 0.72:
            reasons.append("katalogdaki parça adıyla güçlü eşleşti")
        elif score >= 0.60:
            reasons.append("katalogda aday parça olarak öne çıktı")

    if page and ref_no:
        reasons.append(f"katalogda Sf {page}, Ref {ref_no} olarak görünüyor")
    elif page:
        reasons.append(f"katalogda Sf {page} üzerinde görünüyor")

    deduped: list[str] = []
    for reason in reasons:
        if reason and reason not in deduped:
            deduped.append(reason)

    return "; ".join(deduped[:3]) or "katalog kaydında aday olarak bulundu"


def build_source_confidence(source: dict) -> tuple[str, bool]:
    if source.get("fallback"):
        return "Teyit gerekli", True

    visual_reason = str(source.get("visual_match_reason") or "")
    if visual_reason == "ocr_code":
        return "Yüksek güven", False

    similarity = source.get("similarity")
    visual_similarity = source.get("visual_similarity")
    score = similarity if isinstance(similarity, (int, float)) else visual_similarity
    if isinstance(score, (int, float)):
        if score >= 0.99:
            return "Yüksek güven", False
        if score >= 0.72:
            return "Yüksek aday", False
        if score >= 0.60:
            return "Orta aday", True

    return "Aday", True


def enrich_source_explanation(source: dict) -> dict:
    if not source.get("matchReason"):
        source["matchReason"] = build_source_match_reason(source)
    if not source.get("confidenceLabel"):
        confidence_label, requires_verification = build_source_confidence(source)
        source["confidenceLabel"] = confidence_label
        source["requiresVerification"] = requires_verification
    return source


def build_visual_source(row: dict, *, query: str | None, shop_base_url: str) -> dict:
    part_code = row.get("PartCode", "-")
    catalog_id, page_number = normalize_location_fields(
        row.get("CatalogId"),
        row.get("ViewerPageNumber") or row.get("PageNumber"),
    )
    visual_similarity = row.get("visual_similarity")
    if visual_similarity is None and row.get("visual_match") is True:
        visual_similarity = 1.0

    source_entry = {
        "code": part_code,
        "name": row.get("PartName", "Bilinmeyen"),
        "brand": row.get("MachineBrand", "-"),
        "buy_url": f"{shop_base_url}{urllib.parse.quote(part_code.strip())}",
        "catalogId": catalog_id,
        "pageNumber": page_number,
        "refNo": row.get("RefNumber", ""),
        "machine_model": row.get("MachineModel", ""),
        "description": row.get("Description", ""),
        "query": query,
        "visual_match": row.get("visual_match", True),
        "visual_match_reason": row.get("visual_match_reason"),
        "visual_hint_score": row.get("visual_hint_score"),
        "visual_image_url": row.get("VisualImageUrl"),
        "visual_similarity": visual_similarity,
    }
    return enrich_source_explanation(source_entry)


def build_catalog_source(
    row: dict,
    *,
    query: str | None,
    requested_code: str | None,
    shop_base_url: str,
    is_fallback: bool,
    fallback_reason: str | None,
) -> dict:
    part_code = row.get("PartCode", "-")
    catalog_id, page_number = normalize_location_fields(
        row.get("CatalogId"),
        row.get("ViewerPageNumber") or row.get("PageNumber"),
    )
    quantity = row.get("StockQuantity") or row.get("quantity") or 0
    try:
        quantity = int(quantity)
    except (ValueError, TypeError):
        quantity = 0

    source_entry = {
        "code": part_code,
        "name": row.get("PartName", "Bilinmeyen"),
        "brand": row.get("MachineBrand", "-"),
        "buy_url": f"{shop_base_url}{urllib.parse.quote(part_code.strip())}",
        "catalogId": catalog_id,
        "pageNumber": page_number,
        "refNo": row.get("RefNumber", ""),
        "machine_model": row.get("MachineModel", ""),
        "description": row.get("Description", ""),
        "query": query,
        "similarity": row.get("similarity", 1.0 if requested_code else None),
        "quantity": quantity,
    }
    if is_fallback:
        source_entry["fallback"] = True
        source_entry["fallback_reason"] = fallback_reason
    return enrich_source_explanation(source_entry)


def append_unique_sources(target: list[dict], sources: list[dict]) -> None:
    existing_codes = {source.get("code") for source in target}
    for source in sources:
        code = source.get("code")
        if code not in existing_codes:
            target.append(enrich_source_explanation(source))
            existing_codes.add(code)


def collect_source_model_labels(sources: list[dict], *, limit: int = 6) -> list[str]:
    labels: list[str] = []
    for source in sources[:limit]:
        model_label = str(source.get("machine_model") or "").strip()
        if model_label and model_label not in labels:
            labels.append(model_label)
    return labels
