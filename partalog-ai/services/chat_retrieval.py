"""Hybrid text retrieval orchestration for chat requests."""

import asyncio
from collections.abc import Callable
import re

import aiohttp
from loguru import logger

from config import settings
from services.chat_sources import append_unique_sources, build_catalog_source
from services.embedding import get_text_embedding
from services.genai_provider import provider
from services.vector_db import (
    exact_match_search,
    hybrid_search_vector_db,
    search_by_page_and_part,
    search_by_visual_hints,
    search_vector_db,
    text_term_search,
)


QUERY_REWRITE_TIMEOUT_SECONDS = 2.5
QUERY_REWRITE_MAX_CHARS = 80
QUERY_REWRITE_STOP_WORDS = {
    "böyle",
    "boyle",
    "var",
    "olan",
    "nedir",
    "kodu",
    "kodunu",
    "parça",
    "parca",
    "makine",
    "makinenin",
}

LEXICAL_STOP_WORDS = {
    "acaba",
    "bana",
    "bunun",
    "için",
    "icin",
    "kodu",
    "kodunu",
    "makine",
    "makinenin",
    "nedir",
    "parca",
    "parça",
    "peki",
    "sende",
    "var",
    "varmis",
    "varmış",
}

GENERIC_LEXICAL_PART_TOKENS = {
    "code",
    "kod",
    "kodu",
    "parca",
    "parça",
    "part",
    "plate",
    "plaka",
    "plakasi",
    "plakas",
}

TURKISH_ASCII_TRANSLATION = str.maketrans(
    {
        "ç": "c",
        "Ç": "C",
        "ğ": "g",
        "Ğ": "G",
        "ı": "i",
        "İ": "I",
        "ö": "o",
        "Ö": "O",
        "ş": "s",
        "Ş": "S",
        "ü": "u",
        "Ü": "U",
    }
)


def _sanitize_rewritten_query(value: str | None) -> str | None:
    cleaned = str(value or "").strip().strip("\"'`“”‘’")
    if not cleaned:
        return None

    cleaned = re.split(r"[\r\n]", cleaned, maxsplit=1)[0].strip()
    cleaned = re.sub(r"^\s*[-*•]\s*", "", cleaned).strip()
    cleaned = re.sub(r"\s+", " ", cleaned)
    cleaned = cleaned.strip(" .,:;")

    if not cleaned or len(cleaned) > QUERY_REWRITE_MAX_CHARS:
        return None

    words = cleaned.split()
    if len(words) > 6:
        return None

    normalized = _as_ascii_term(cleaned).lower()
    if not normalized or normalized in QUERY_REWRITE_STOP_WORDS:
        return None

    return cleaned


def _looks_like_direct_code_query(text: str | None) -> bool:
    return bool(re.search(r"\b[A-Z]{1,4}\d[A-Z0-9-]{3,}|\b\d{6,}[A-Z0-9-]*\b", str(text or ""), re.IGNORECASE))


def _should_rewrite_query(user_query: str | None) -> bool:
    text = str(user_query or "").strip()
    if not text:
        return False
    tokens = _tokenize_for_lexical_search(text)
    return len(tokens) >= 2


async def rewrite_user_query(user_query: str, brand_context: str | None = None) -> str:
    """Rewrite noisy natural-language part descriptions into catalog search keywords."""
    original = str(user_query or "").strip()
    if not _should_rewrite_query(original):
        return original

    if not provider.has_credentials():
        logger.debug("Query rewriter atlandı: GenAI credentials/config yok.")
        return original

    brand_line = (
        f"\nBilinen marka bağlamı: {brand_context}. Marka adını arama anahtarına yazma; sadece parça adını dön."
        if brand_context
        else ""
    )
    system_prompt = f"""
Sen bir yedek parça katalog arama motoru asistanısın.
Görevin, kullanıcının doğal dildeki dolaylı parça tarifini katalogda aranabilecek en net teknik parça adına dönüştürmektir.
Dolgu kelimelerini temizle: böyle, var, olan, nedir, kodu, kodunu, parça, makine.
Marka, model ve konuşma üslubunu arama anahtarına ekleme.{brand_line}

Örnekler:
- üstünde marka yazan böyle yamato yazan bir plaka -> model plakası
- makinenin adının yazdığı etiket -> model plakası
- iğnenin altına giren delikli plaka -> iğne plakası
- ipliği yönlendiren tel/kılavuz -> iplik kılavuzu

Sadece temizlenmiş anahtar kelimeyi dön. Açıklama, JSON, tırnak veya alternatif liste yazma.
""".strip()
    payload = provider.normalize_generate_payload(
        {
            "contents": [{"parts": [{"text": f"{system_prompt}\n\nKULLANICI METNİ: {original}"}]}],
            "generationConfig": {"temperature": 0.0, "maxOutputTokens": 24},
        }
    )

    try:
        api_url = provider.generate_content_url(settings.GEMINI_CHAT_MODEL)
        if not api_url:
            return original
        headers = await provider.build_headers()
        timeout = aiohttp.ClientTimeout(total=QUERY_REWRITE_TIMEOUT_SECONDS)
        async with aiohttp.ClientSession(headers=headers, timeout=timeout) as session:
            async with session.post(api_url, json=payload) as response:
                if response.status != 200:
                    logger.warning("Query rewriter non-200 status={}", response.status)
                    return original
                result = await response.json()
        text_response = (
            result.get("candidates", [{}])[0]
            .get("content", {})
            .get("parts", [{}])[0]
            .get("text")
        )
        rewritten = _sanitize_rewritten_query(text_response)
        if not rewritten:
            return original
        if _as_ascii_term(rewritten).lower() == _as_ascii_term(original).lower():
            return original
        logger.info("🧼 Query rewrite: '{}' -> '{}'", original, rewritten)
        return rewritten
    except (asyncio.TimeoutError, aiohttp.ClientError) as exc:
        logger.warning("Query rewriter atlandı: {}", exc)
        return original
    except Exception as exc:
        logger.error("Query rewriter hatası: {}", exc)
        return original


def _as_ascii_term(term: str) -> str:
    return str(term or "").translate(TURKISH_ASCII_TRANSLATION)


def _tokenize_for_lexical_search(text: str | None) -> list[str]:
    tokens: list[str] = []
    current: list[str] = []
    for char in str(text or ""):
        if char.isalnum():
            current.append(char)
        elif current:
            tokens.append("".join(current))
            current = []
    if current:
        tokens.append("".join(current))

    cleaned: list[str] = []
    for token in tokens:
        normalized = _as_ascii_term(token).lower()
        if len(normalized) < 4 or normalized in LEXICAL_STOP_WORDS:
            continue
        cleaned.append(token)
        ascii_token = _as_ascii_term(token)
        if ascii_token != token:
            cleaned.append(ascii_token)
        if len(ascii_token) > 5:
            lower_ascii = ascii_token.lower()
            stems = []
            if lower_ascii.endswith(("si", "su")):
                stems.append(ascii_token[:-2])
            elif lower_ascii.endswith(("i", "u")):
                stems.append(ascii_token[:-1])
            for stem in stems:
                if len(stem) >= 4 and stem.lower() not in LEXICAL_STOP_WORDS:
                    cleaned.append(stem)
    return cleaned


def _build_lexical_terms(
    *,
    part_search_text: str | None,
    part_name: str | None,
    raw_user_query: str | None,
    split_expanded_search_terms: Callable[[str | None], list[str]],
) -> list[str]:
    terms: list[str] = []

    expanded_terms = split_expanded_search_terms(part_search_text or part_name)
    if expanded_terms:
        primary_term = expanded_terms[0]
        terms.append(primary_term)
        ascii_term = _as_ascii_term(primary_term)
        if ascii_term != primary_term:
            terms.append(ascii_term)

    terms.extend(_tokenize_for_lexical_search(part_name))
    terms.extend(_tokenize_for_lexical_search(raw_user_query))

    for term in expanded_terms[1:]:
        terms.append(term)
        ascii_term = _as_ascii_term(term)
        if ascii_term != term:
            terms.append(ascii_term)

    unique_terms: list[str] = []
    seen: set[str] = set()
    for term in terms:
        cleaned = str(term or "").strip()
        key = cleaned.lower()
        if cleaned and key not in seen:
            unique_terms.append(cleaned)
            seen.add(key)
    return unique_terms


def _specific_lexical_tokens(part_name: str | None, raw_user_query: str | None) -> list[str]:
    tokens = _tokenize_for_lexical_search(part_name) + _tokenize_for_lexical_search(raw_user_query)
    specific: list[str] = []
    seen: set[str] = set()
    for token in tokens:
        normalized = _as_ascii_term(token).lower()
        if normalized in GENERIC_LEXICAL_PART_TOKENS or len(normalized) < 4:
            continue
        if normalized not in seen:
            specific.append(normalized)
            seen.add(normalized)
    return specific


def _filter_by_specific_lexical_tokens(
    rows: list[dict],
    *,
    part_name: str | None,
    raw_user_query: str | None,
) -> list[dict]:
    specific_tokens = _specific_lexical_tokens(part_name, raw_user_query)
    if not rows or not specific_tokens:
        return rows

    filtered = []
    for row in rows:
        haystack = _as_ascii_term(
            " ".join(
                str(row.get(field) or "")
                for field in ("PartName", "Description", "Mechanism", "Dimensions")
            )
        ).lower()
        if any(token in haystack for token in specific_tokens):
            filtered.append(row)
    return filtered


async def _safe_text_embedding(text: str) -> list[float] | None:
    try:
        return await get_text_embedding(text)
    except Exception as exc:
        logger.error("Embedding üretilemedi, bu arama adımı atlanıyor: {}", exc)
        return None


async def retrieve_part_sources(
    parts: list[dict],
    *,
    initial_sources: list[dict],
    raw_user_query: str,
    user_query: str,
    intent: str,
    extracted_brand: str | None,
    extracted_machine_group: str | None,
    extracted_context_part: str | None,
    catalog_ids_list: list,
    shop_base_url: str,
    text_vector_min_similarity: float,
    expand_part_search_text: Callable[[str | None], str | None],
    split_expanded_search_terms: Callable[[str | None], list[str]],
    extract_requested_domain_terms: Callable[..., list[str]],
    rerank_results_by_context_part: Callable[[list[dict], str | None], list[dict]],
    filter_results_by_requested_terms: Callable[[list[dict], list[str]], list[dict]],
    filter_diagnosis_results: Callable[[list[dict], str | None, str | None], list[dict]],
    is_strict_part_lookup: Callable[[str, str | None, str | None], bool],
    filter_strict_part_lookup_results: Callable[[list[dict], str | None, str | None], list[dict]],
) -> tuple[list[dict], list[str], dict | None]:
    all_sources = list(initial_sources)
    context_page_hints: list[str] = []
    rewritten_user_query = raw_user_query
    query_rewrite_debug: dict | None = None

    if intent not in {"CHAT", "HELP"} and not any((part or {}).get("part_code") for part in parts if isinstance(part, dict)):
        rewritten_user_query = await rewrite_user_query(raw_user_query, extracted_brand)
        if _as_ascii_term(rewritten_user_query).lower() != _as_ascii_term(raw_user_query).lower():
            query_rewrite_debug = {
                "original": raw_user_query,
                "rewritten": rewritten_user_query,
                "source": "gemini_query_rewriter",
            }

    for part in parts:
        part_code = part.get("part_code")
        part_name = part.get("part_name")
        part_search_text = expand_part_search_text(part_name)
        part_context = part.get("context_part") or extracted_context_part
        requested_terms = extract_requested_domain_terms(part_name or "", raw_user_query or "")

        part_results = []
        is_fallback = False
        fallback_reason = None
        query_vector = None

        if part_code:
            logger.info(f"🔍 Kod tespit edildi ({part_code}). Exact Match aranıyor...")
            part_results = await exact_match_search(
                part_code,
                extracted_brand,
                catalog_ids_list,
                limit=5,
                machine_group_filter=extracted_machine_group,
            )

        if (
            not part_results
            and part_name
            and intent not in {"CHAT", "HELP"}
            and part.get("source") != "diagnosis_symptom_map"
        ):
            lexical_terms = _build_lexical_terms(
                part_search_text=part_search_text,
                part_name=part_name,
                raw_user_query=raw_user_query,
                split_expanded_search_terms=split_expanded_search_terms,
            )
            if lexical_terms:
                lexical_results = await text_term_search(
                    lexical_terms,
                    brand_filter=extracted_brand,
                    catalog_ids=catalog_ids_list,
                    limit=5,
                    machine_group_filter=extracted_machine_group,
                )
                if lexical_results:
                    lexical_results = _filter_by_specific_lexical_tokens(
                        lexical_results,
                        part_name=part_name,
                        raw_user_query=raw_user_query,
                    )
                    if not lexical_results:
                        logger.info(
                            "🔎 Name lexical search: part='{}' terimler bulundu ama özgül token filtresi sonucu boş",
                            part_name,
                        )
                        lexical_results = []
                        part_results = []
                    else:
                        best_similarity = max(float(result.get("similarity") or 0) for result in lexical_results)
                        if best_similarity >= 0.66:
                            lexical_results = [
                                result
                                for result in lexical_results
                                if float(result.get("similarity") or 0) >= best_similarity * 0.80
                            ]
                        for result in lexical_results:
                            original_similarity = float(result.get("similarity") or 0)
                            if original_similarity >= 0.66:
                                result["similarity"] = max(original_similarity, 0.88)
                            result["_lexical_name_match"] = True
                        part_results = lexical_results
                        logger.info(
                            "🔎 Name lexical search: part='{}' terms={} -> {} sonuç",
                            part_name,
                            lexical_terms[:5],
                            len(lexical_results),
                        )

        if (
            not part_results
            and
            part.get("source") == "context_related_part_map"
            and part_name
            and intent not in {"CHAT", "HELP"}
        ):
            lexical_terms = split_expanded_search_terms(part_search_text or part_name)
            if lexical_terms:
                lexical_results = await text_term_search(
                    lexical_terms,
                    brand_filter=extracted_brand,
                    catalog_ids=catalog_ids_list,
                    limit=5,
                    machine_group_filter=extracted_machine_group,
                )
                if not lexical_results:
                    lexical_results = await search_by_visual_hints(
                        {
                            "object_name": part_name,
                            "shape_tags": lexical_terms,
                        },
                        brand_filter=extracted_brand,
                        catalog_ids=catalog_ids_list,
                        limit=5,
                        machine_group_filter=extracted_machine_group,
                    )
                if lexical_results:
                    for result in lexical_results:
                        result["similarity"] = max(
                            float(result.get("visual_hint_score") or 0) / max(len(lexical_terms), 1),
                            0.76,
                        )
                        result["_lexical_context_match"] = True
                    part_results = lexical_results
                    logger.info(
                        "🔎 Context lexical search: part='{}' terms={} -> {} sonuç",
                        part_name,
                        lexical_terms[:5],
                        len(lexical_results),
                    )

        if not part_results and part_name and raw_user_query and intent not in {"CHAT", "HELP"}:
            semantic_query = rewritten_user_query or raw_user_query
            if semantic_query == raw_user_query and part_search_text and part_search_text != part_name:
                semantic_query = f"{raw_user_query} | terim genişletme: {part_search_text}"
            logger.info(f"🧠 Semantic-First: FULL query vector search for '{semantic_query}'")
            full_query_vector = await _safe_text_embedding(semantic_query)
            if full_query_vector:
                semantic_results = await hybrid_search_vector_db(
                    query_vector=full_query_vector,
                    query_text=semantic_query,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                    min_similarity=0.72,
                )
                if semantic_results:
                    for result in semantic_results:
                        result["_semantic_primary"] = True
                    part_results = semantic_results
                    logger.success(f"🧠 Semantic-First: {len(semantic_results)} yüksek güvenilir sonuç (≥0.72)")

        if not part_results and part_context and part_name and not part_code:
            context_query_vector = await _safe_text_embedding(str(part_context))
            context_anchor_results = []
            if context_query_vector:
                context_anchor_results = await hybrid_search_vector_db(
                    query_vector=context_query_vector,
                    query_text=str(part_context),
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                    min_similarity=0.40,
                )
                logger.info(
                    f"🧭 Context anchor search: context_part='{part_context}' -> {len(context_anchor_results)} sonuç"
                )

            if context_anchor_results:
                anchor = context_anchor_results[0]
                anchor_catalog_id = anchor.get("CatalogId")
                anchor_page = str(anchor.get("PageNumber") or "").strip()
                anchor_code = anchor.get("PartCode")
                if anchor_catalog_id and anchor_page:
                    page_scoped_results = await search_by_page_and_part(
                        catalog_ids=[anchor_catalog_id],
                        page_number=anchor_page,
                        part_name=str(part_search_text or part_name),
                        limit=5,
                        brand_filter=extracted_brand,
                        machine_group_filter=extracted_machine_group,
                    )
                    logger.info(
                        f"🧭 Context page search: catalog={anchor_catalog_id} page={anchor_page} "
                        f"part='{part_name}' -> {len(page_scoped_results)} sonuç"
                    )
                    if page_scoped_results:
                        part_results = page_scoped_results
                        is_fallback = True
                        fallback_reason = "context_page_match"
                        sample_codes = [str(result.get("PartCode") or "-") for result in page_scoped_results[:4]]
                        context_page_hints.append(
                            f"'{part_context}' için bulunan ana parça: {anchor_code or '-'} (sayfa {anchor_page}). "
                            f"Aynı sayfadaki '{part_name}' sonuçları: {', '.join(sample_codes)}."
                        )

        if not part_results and part_code and extracted_brand:
            logger.info(f"🔄 Fallback [Adım 2]: Exact Match marka filtresi kaldırılıyor ({extracted_brand})")
            part_results = await exact_match_search(
                part_code,
                None,
                catalog_ids_list,
                limit=5,
                machine_group_filter=None,
            )
            if part_results:
                is_fallback = True
                fallback_reason = "brand_removed"

        if not part_results and part_name and raw_user_query and intent not in {"CHAT", "HELP"}:
            semantic_query = rewritten_user_query or raw_user_query
            if semantic_query == raw_user_query and part_search_text and part_search_text != part_name:
                semantic_query = f"{raw_user_query} | terim genişletme: {part_search_text}"
            logger.info(f"🧠 Kod yok veya bulunamadı. Vektör (Semantic) aranıyor: '{semantic_query[:120]}'")
            query_vector = await _safe_text_embedding(semantic_query)

            if query_vector:
                part_results = await hybrid_search_vector_db(
                    query_vector=query_vector,
                    query_text=semantic_query,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=extracted_machine_group,
                    min_similarity=text_vector_min_similarity,
                )
                logger.info(f"🧠 Vector Search (filtreli): {len(part_results)} sonuç")

        if not part_results and part_name and extracted_machine_group and intent not in {"CHAT", "HELP"}:
            logger.info(
                f"🔄 Fallback [Adım 4]: machine_group filtresi kaldırıldı ({extracted_machine_group}), "
                f"brand={extracted_brand} korundu"
            )
            if query_vector:
                part_results = await hybrid_search_vector_db(
                    query_vector=query_vector,
                    query_text=part_search_text or part_name or raw_user_query,
                    brand_filter=extracted_brand,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=None,
                    min_similarity=text_vector_min_similarity,
                )
                logger.info(f"🧠 Vector Search (machine_group'suz): {len(part_results)} sonuç")
                if part_results:
                    is_fallback = True
                    fallback_reason = "machine_group_removed"

        if not part_results and part_name and extracted_brand and intent not in {"CHAT", "HELP"}:
            logger.info(f"🔄 Fallback [Adım 5]: brand filtresi de kaldırıldı ({extracted_brand}), filtresiz arama yapılıyor")
            if query_vector:
                part_results = await hybrid_search_vector_db(
                    query_vector=query_vector,
                    query_text=part_search_text or part_name or raw_user_query,
                    brand_filter=None,
                    limit=5,
                    catalog_ids=catalog_ids_list,
                    machine_group_filter=None,
                    min_similarity=text_vector_min_similarity,
                )
                logger.info(f"🧠 Vector Search (filtresiz): {len(part_results)} sonuç")
                if part_results:
                    is_fallback = True
                    fallback_reason = "all_filters_removed"

        part_results = rerank_results_by_context_part(part_results, part_context)
        part_results = filter_results_by_requested_terms(part_results, requested_terms)
        if intent == "DIAGNOSE":
            if is_fallback:
                for result in part_results:
                    result["fallback"] = True
                    result["fallback_reason"] = fallback_reason
            part_results = filter_diagnosis_results(part_results, part_name, part_search_text)
        elif is_strict_part_lookup(raw_user_query or user_query, part_name, intent):
            part_results = filter_strict_part_lookup_results(part_results, part_name, part_search_text)

        append_unique_sources(
            all_sources,
            [
                build_catalog_source(
                    result,
                    query=(
                        rewritten_user_query
                        if query_rewrite_debug and (result.get("_semantic_primary") or query_vector)
                        else part_search_text or part_name or part_code
                    ),
                    requested_code=part_code,
                    shop_base_url=shop_base_url,
                    is_fallback=is_fallback,
                    fallback_reason=fallback_reason,
                )
                for result in part_results
            ],
        )

    return all_sources, context_page_hints, query_rewrite_debug
