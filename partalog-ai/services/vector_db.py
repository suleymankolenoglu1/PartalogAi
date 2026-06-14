"""
Partalog AI - Vector Database Service (Async/Pgvector/3072 + Exact Match + Connection Pool)
---------------------------------------------------------
Görevi: C# tarafından oluşturulan 3072'lik vektörleri aramak ve 
Parça Kodu (PartCode) ile birebir eşleşme (Hard-Boost) yapmak.
v2: asyncpg.Pool ile bağlantı havuzu kullanılıyor.
"""

import asyncio
import asyncpg
import json
import re
import time
from loguru import logger
from config import settings

# =============================================
# 🏊 CONNECTION POOL (uygulama ömrünce yaşar)
# =============================================
_pool: asyncpg.Pool | None = None
_pool_lock = asyncio.Lock()
_pool_state = {
    "ready": False,
    "mode": "uninitialized",
    "last_error": None,
    "last_init_started_at": None,
    "last_init_completed_at": None,
    "last_healthcheck_at": None,
    "last_healthcheck_latency_ms": None,
    "ephemeral_fallback_uses": 0,
}

CATALOG_PART_ROW_FILTER = """
    NOT (
        COALESCE("RefNumber", '') = '0'
        AND regexp_replace(upper(COALESCE("PartName", '')), '[^A-Z0-9]', '', 'g')
            = regexp_replace(upper(COALESCE("PartCode", '')), '[^A-Z0-9]', '', 'g')
    )
"""

_FTS_TOKEN_RE = re.compile(r"[0-9A-Za-zÇĞİÖŞÜçğıöşü]+")
FTS_CANDIDATE_LIMIT = 30
_TURKISH_FTS_STOPWORDS = {
    "acaba",
    "ama",
    "bana",
    "ben",
    "bir",
    "bu",
    "da",
    "de",
    "dedin",
    "diye",
    "hangi",
    "icin",
    "için",
    "ile",
    "ise",
    "katalog",
    "kod",
    "kodu",
    "kullanilan",
    "kullanılan",
    "makine",
    "makinenin",
    "mi",
    "mı",
    "mu",
    "mü",
    "ne",
    "nedir",
    "nerede",
    "olan",
    "olarak",
    "parca",
    "parça",
    "peki",
    "sen",
    "sende",
    "su",
    "şu",
    "var",
    "varmış",
    "ve",
    "ya",
}


def _ascii_fold_turkish(value: str) -> str:
    translation = str.maketrans(
        {
            "ç": "c",
            "Ç": "c",
            "ğ": "g",
            "Ğ": "g",
            "ı": "i",
            "İ": "i",
            "ö": "o",
            "Ö": "o",
            "ş": "s",
            "Ş": "s",
            "ü": "u",
            "Ü": "u",
        }
    )
    return value.translate(translation)


def _turkish_token_variants(token: str) -> list[str]:
    variants = [token]
    suffixes = (
        "sının",
        "sinin",
        "sunun",
        "sünün",
        "ının",
        "inin",
        "unun",
        "ünün",
        "nın",
        "nin",
        "nun",
        "nün",
        "sı",
        "si",
        "su",
        "sü",
    )
    for suffix in suffixes:
        if token.endswith(suffix) and len(token) > len(suffix) + 2:
            variants.append(token[: -len(suffix)])

    folded = _ascii_fold_turkish(token).lower()
    variants.append(folded)
    for suffix in ("sinin", "sunun", "inin", "unun", "nin", "nun", "si", "su"):
        if folded.endswith(suffix) and len(folded) > len(suffix) + 2:
            variants.append(folded[: -len(suffix)])

    return variants


def _catalog_jargon_expansions(query_text: str) -> list[str]:
    normalized = _ascii_fold_turkish(str(query_text or "")).lower()
    normalized = re.sub(r"\s+", " ", normalized)
    expansions: list[str] = []

    identity_plate_phrases = (
        "marka plaka",
        "model plaka",
        "kimlik plaka",
        "isim etiket",
        "name plate",
        "nameplate",
        "rating plate",
    )
    if any(phrase in normalized for phrase in identity_plate_phrases):
        expansions.extend(["model", "kimlik", "etiket"])

    needle_plate_phrases = ("igne plaka", "needle plate", "throat plate")
    if any(phrase in normalized for phrase in needle_plate_phrases):
        expansions.extend(["igne", "needle", "throat"])

    wind_guide_phrases = ("ruzgar kilavuz", "wind guide")
    if any(phrase in normalized for phrase in wind_guide_phrases):
        expansions.extend(["ruzgar", "kilavuz", "wind", "guide"])

    return expansions


def _build_turkish_fts_query(query_text: str, max_terms: int = 12) -> str:
    """
    Build a tolerant PostgreSQL tsquery for Turkish catalog jargon.

    PostgreSQL does not ship a Turkish stemming configuration by default in
    every install, so we use the `simple` dictionary and generate OR-prefixed
    lexemes. This keeps natural phrases like "bu makinenin marka plakası kodu"
    from becoming an over-strict AND query while still letting ts_rank_cd reward
    rows that match more of the meaningful terms.
    """
    terms: list[str] = []
    seen: set[str] = set()

    expanded_text = " ".join([str(query_text or ""), *_catalog_jargon_expansions(query_text)])
    for raw_token in _FTS_TOKEN_RE.findall(expanded_text):
        token = raw_token.lower().strip("_")
        if len(token) < 2 or token in _TURKISH_FTS_STOPWORDS:
            continue

        for variant in _turkish_token_variants(token):
            if len(variant) < 2 or variant in seen or variant in _TURKISH_FTS_STOPWORDS:
                continue
            seen.add(variant)
            terms.append(f"{variant}:*")
            if len(terms) >= max_terms:
                return " | ".join(terms)

    return " | ".join(terms)


def _get_dsn() -> str | None:
    dsn = getattr(settings, "DB_CONNECTION_STRING", None)
    if not dsn:
        dsn = getattr(settings, "DATABASE_URL", None)
    return dsn


def _utc_timestamp() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def _pool_size_snapshot() -> dict:
    global _pool
    if _pool is None:
        return {
            "size": 0,
            "idle": 0,
            "min_size": settings.DB_POOL_MIN_SIZE,
            "max_size": settings.DB_POOL_MAX_SIZE,
        }

    snapshot = {
        "size": 0,
        "idle": 0,
        "min_size": settings.DB_POOL_MIN_SIZE,
        "max_size": settings.DB_POOL_MAX_SIZE,
    }
    for attr_name, key in (
        ("get_size", "size"),
        ("get_idle_size", "idle"),
        ("get_min_size", "min_size"),
        ("get_max_size", "max_size"),
    ):
        getter = getattr(_pool, attr_name, None)
        if callable(getter):
            snapshot[key] = getter()
    return snapshot


def get_db_pool_state() -> dict:
    snapshot = dict(_pool_state)
    snapshot.update(_pool_size_snapshot())
    snapshot["ephemeral_fallback_enabled"] = settings.DB_ALLOW_EPHEMERAL_FALLBACK
    snapshot["dsn_configured"] = bool(_get_dsn())
    return snapshot


async def check_db_pool_health() -> dict:
    global _pool

    started = time.perf_counter()
    _pool_state["last_healthcheck_at"] = _utc_timestamp()

    if _pool is None:
        _pool_state["ready"] = False
        if _pool_state["mode"] == "uninitialized":
            _pool_state["mode"] = "missing_pool"
        _pool_state["last_healthcheck_latency_ms"] = None
        return get_db_pool_state()

    try:
        async def _ping_pool():
            async with _pool.acquire() as conn:
                await conn.fetchval("SELECT 1")

        await asyncio.wait_for(
            _ping_pool(),
            timeout=settings.DB_POOL_HEALTHCHECK_TIMEOUT_SECONDS,
        )
        latency_ms = round((time.perf_counter() - started) * 1000.0, 2)
        _pool_state["ready"] = True
        _pool_state["mode"] = "pool"
        _pool_state["last_error"] = None
        _pool_state["last_healthcheck_latency_ms"] = latency_ms
        return get_db_pool_state()
    except Exception as exc:
        latency_ms = round((time.perf_counter() - started) * 1000.0, 2)
        _pool_state["ready"] = False
        _pool_state["mode"] = "pool_unhealthy"
        _pool_state["last_error"] = str(exc)
        _pool_state["last_healthcheck_latency_ms"] = latency_ms
        logger.error(f"❌ DB pool healthcheck başarısız: {exc}")
        return get_db_pool_state()


async def init_db_pool() -> dict:
    """
    Uygulama startup'ında çağrılmalı (main.py lifespan).
    Pool'u oluşturur.
    """
    global _pool

    async with _pool_lock:
        _pool_state["last_init_started_at"] = _utc_timestamp()

        if _pool is not None:
            return await check_db_pool_health()

        dsn = _get_dsn()
        if not dsn:
            message = "Config dosyasında veritabanı bağlantı bilgisi bulunamadı."
            _pool_state["ready"] = False
            _pool_state["mode"] = "missing_dsn"
            _pool_state["last_error"] = message
            _pool_state["last_init_completed_at"] = _utc_timestamp()
            logger.critical(f"❌ HATA: {message}")
            return get_db_pool_state()

        try:
            _pool = await asyncpg.create_pool(
                dsn,
                min_size=settings.DB_POOL_MIN_SIZE,
                max_size=settings.DB_POOL_MAX_SIZE,
                command_timeout=settings.DB_POOL_COMMAND_TIMEOUT_SECONDS,
                max_inactive_connection_lifetime=settings.DB_POOL_MAX_INACTIVE_CONNECTION_LIFETIME_SECONDS,
                statement_cache_size=settings.DB_STATEMENT_CACHE_SIZE,
            )
            _pool_state["last_init_completed_at"] = _utc_timestamp()
            logger.success("✅ DB Connection Pool oluşturuldu.")
            return await check_db_pool_health()
        except Exception as exc:
            _pool = None
            _pool_state["ready"] = False
            _pool_state["mode"] = "init_failed"
            _pool_state["last_error"] = str(exc)
            _pool_state["last_init_completed_at"] = _utc_timestamp()
            logger.error(f"❌ DB Pool Oluşturma Hatası: {exc}")
            return get_db_pool_state()


async def close_db_pool():
    """
    Uygulama shutdown'ında çağrılmalı (main.py lifespan).
    """
    global _pool

    async with _pool_lock:
        if _pool:
            await _pool.close()
            _pool = None
            logger.info("🔌 DB Connection Pool kapatıldı.")

        _pool_state["ready"] = False
        _pool_state["mode"] = "closed"
        _pool_state["last_healthcheck_latency_ms"] = None


async def _get_conn():
    """
    Pool varsa pool'dan, yoksa tek bağlantı açar (graceful fallback).
    """
    global _pool
    if _pool:
        return await _pool.acquire(), True   # (conn, from_pool)

    if not settings.DB_ALLOW_EPHEMERAL_FALLBACK:
        logger.error("❌ DB pool hazır değil ve ephemeral fallback devre dışı.")
        return None, False

    # Pool henüz başlatılmadıysa tek bağlantı aç
    dsn = _get_dsn()
    if not dsn:
        return None, False
    try:
        conn = await asyncpg.connect(dsn, statement_cache_size=settings.DB_STATEMENT_CACHE_SIZE)
        _pool_state["mode"] = "ephemeral_fallback"
        _pool_state["ephemeral_fallback_uses"] += 1
        _pool_state["last_error"] = None
        return conn, False
    except Exception as e:
        _pool_state["last_error"] = str(e)
        logger.error(f"❌ Veritabanı Bağlantı Hatası: {e}")
        return None, False


async def _release_conn(conn, from_pool: bool):
    """
    Bağlantıyı pool'a iade eder veya kapatır.
    """
    global _pool
    if conn is None:
        return
    try:
        if from_pool and _pool:
            await _pool.release(conn)
        else:
            await conn.close()
    except Exception:
        pass

# =============================================
# 🔍 EXACT MATCH
# =============================================
async def exact_match_search(part_code: str, brand_filter: str = None, catalog_ids: list = None, limit: int = 5, machine_group_filter: str = None, min_similarity: float = 0.0):
    """
    Vektör (Semantic) arama YAPMADAN, parça koduna (PartCode) göre birebir/yakın eşleşme arar.
    Chatbot'ta kod belirtilmişse ilk olarak "Hard-Boost" için kullanılır.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        sql = f"""
            SELECT 
                "Id",
                "CatalogId",
                "PageNumber",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", '')) AS "ViewerPageNumber",
                "RefNumber",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel", 
                "MachineGroup",
                "Mechanism",
                "Description",
                "Dimensions",
                1.0 as similarity 
            FROM "CatalogItems"
            WHERE "PartCode" ILIKE $1
            AND {CATALOG_PART_ROW_FILTER}
        """
        
        params = [f"%{part_code}%"] 
        param_idx = 2

        if catalog_ids:
            sql += f" AND \"CatalogId\" = ANY(${param_idx})"
            params.append(catalog_ids)
            param_idx += 1

        if brand_filter:
            sql += f" AND \"MachineBrand\" ILIKE ${param_idx}"
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            sql += f" AND \"MachineGroup\" ILIKE ${param_idx}"
            params.append(f"%{machine_group_filter}%")
            param_idx += 1
            
        sql += f" LIMIT ${param_idx}"
        params.append(limit)

        results = await conn.fetch(sql, *params)
        rows = [dict(row) for row in results]

        # Minimum similarity filtresi
        rows = [r for r in rows if r.get("similarity", 0) >= min_similarity]
        return rows

    except Exception as e:
        logger.error(f"❌ Exact Match Arama Hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


async def text_term_search(
    terms: list[str],
    brand_filter: str = None,
    catalog_ids: list = None,
    limit: int = 5,
    machine_group_filter: str = None,
):
    """
    Katalog metninde birebir/lexical terim araması yapar.
    Vektör aramadan önce "SG_SLIDE_COVER" gibi katalog terimlerini yakalamak için kullanılır.
    """
    cleaned_terms = [str(term).strip() for term in terms or [] if str(term or "").strip()]
    if not cleaned_terms:
        return []

    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        where_parts = []
        score_parts = []
        params = []
        param_idx = 1

        if catalog_ids:
            where_parts.append(f"\"CatalogId\" = ANY(${param_idx})")
            params.append(catalog_ids)
            param_idx += 1

        if brand_filter:
            where_parts.append(f"\"MachineBrand\" ILIKE ${param_idx}")
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            where_parts.append(f"\"MachineGroup\" ILIKE ${param_idx}")
            params.append(f"%{machine_group_filter}%")
            param_idx += 1

        where_parts.append(CATALOG_PART_ROW_FILTER)

        searchable_fields = """
            COALESCE("SearchText", '') || ' ' ||
            COALESCE("PartName", '') || ' ' ||
            COALESCE("Description", '') || ' ' ||
            COALESCE("PartCode", '') || ' ' ||
            COALESCE("RefNumber", '') || ' ' ||
            COALESCE("Mechanism", '') || ' ' ||
            COALESCE("Dimensions", '')
        """

        term_match_parts = []
        limited_terms = cleaned_terms[:8]
        for term in limited_terms:
            score_parts.append(f"CASE WHEN ({searchable_fields}) ILIKE ${param_idx} THEN 1 ELSE 0 END")
            term_match_parts.append(f"({searchable_fields}) ILIKE ${param_idx}")
            params.append(f"%{term}%")
            param_idx += 1

        where_parts.append("(" + " OR ".join(term_match_parts) + ")")
        where_sql = " AND ".join(where_parts)
        score_sql = " + ".join(score_parts)

        sql = f"""
            SELECT
                "Id",
                "CatalogId",
                "PageNumber",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", '')) AS "ViewerPageNumber",
                "RefNumber",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel",
                "MachineGroup",
                "Mechanism",
                "Description",
                "Dimensions",
                LEAST(({score_sql})::float, 3.0) / LEAST({max(len(limited_terms), 1)}::float, 3.0) AS similarity
            FROM "CatalogItems"
            WHERE {where_sql}
            ORDER BY ({score_sql}) DESC, "PartCode" ASC
            LIMIT ${param_idx}
        """
        params.append(limit)

        rows = await conn.fetch(sql, *params)
        return [dict(row) for row in rows]
    except Exception as e:
        logger.error(f"❌ Metin terimi arama hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


async def find_catalogs_by_machine(
    *,
    brand_filter: str | None = None,
    machine_model: str | None = None,
    catalog_ids: list | None = None,
    limit: int = 5,
):
    """
    Find catalog-level matches for machine/model questions without turning
    catalog title rows into spare-part candidates.
    """
    normalized_model = "".join(ch for ch in str(machine_model or "").lower() if ch.isalnum())
    cleaned_brand = str(brand_filter or "").strip()
    if not normalized_model and not cleaned_brand and not catalog_ids:
        return []

    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        where_parts = []
        params = []
        param_idx = 1

        if catalog_ids:
            where_parts.append(f'c."Id" = ANY(${param_idx})')
            params.append(catalog_ids)
            param_idx += 1

        if normalized_model:
            where_parts.append(
                "("
                "regexp_replace(lower(coalesce(c.\"Name\", '') || ' ' || coalesce(ci.\"MachineModel\", '') || ' ' || coalesce(ci.\"PartCode\", '')), '[^a-z0-9]', '', 'g') "
                f"LIKE '%' || ${param_idx} || '%'"
                ")"
            )
            params.append(normalized_model)
            param_idx += 1

        if cleaned_brand:
            where_parts.append(
                "("
                f'c."Name" ILIKE ${param_idx} OR ci."MachineBrand" ILIKE ${param_idx}'
                ")"
            )
            params.append(f"%{cleaned_brand}%")
            param_idx += 1

        where_sql = " AND ".join(where_parts) if where_parts else "1=1"
        sql = f"""
            SELECT
                c."Id",
                c."Name",
                c."Status",
                COUNT(ci."Id")::int AS "ItemCount",
                STRING_AGG(DISTINCT NULLIF(ci."MachineBrand", ''), ', ') AS "Brands",
                STRING_AGG(DISTINCT NULLIF(ci."MachineModel", ''), ', ') AS "Models",
                STRING_AGG(DISTINCT NULLIF(ci."MachineGroup", ''), ', ') AS "Groups"
            FROM "Catalogs" c
            LEFT JOIN "CatalogItems" ci ON ci."CatalogId" = c."Id"
            WHERE {where_sql}
            GROUP BY c."Id", c."Name", c."Status", c."CreatedDate"
            ORDER BY c."CreatedDate" DESC
            LIMIT ${param_idx}
        """
        params.append(limit)

        rows = await conn.fetch(sql, *params)
        return [dict(row) for row in rows]
    except Exception as e:
        logger.error(f"❌ Makine katalog arama hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


# =============================================
# 🧠 VECTOR SEARCH (Semantic / Embedding)
# =============================================
async def search_vector_db(query_vector: list, brand_filter: str = None, limit: int = 5, catalog_ids: list = None, machine_group_filter: str = None, min_similarity: float = 0.3):
    """
    Vektörel benzerlik (Semantic) araması yapar.
    """
    # Guard: query_vector None ise hiç başlama
    if not query_vector:
        logger.warning("⚠️ search_vector_db çağrıldı ama query_vector None/boş!")
        return []

    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        if len(query_vector) != 3072:
            logger.warning(f"⚠️ Vektör boyutu 3072 değil! Gelen: {len(query_vector)}")

        sql = f"""
            SELECT
                "Id",
                "CatalogId",
                "PageNumber",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", '')) AS "ViewerPageNumber",
                "RefNumber",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel",
                "MachineGroup",
                "Mechanism",
                "Description",
                "Dimensions",
                1 - ("Embedding" <=> $1) as similarity,
                (SELECT "StockQuantity" FROM "Products" WHERE "Code" = "CatalogItems"."PartCode" AND "CatalogId" = "CatalogItems"."CatalogId" LIMIT 1) AS "StockQuantity"
            FROM "CatalogItems"
            WHERE 1=1
            -- Phase 6: Exclude PDF header/metadata rows that are not actual spare parts.
            -- Real part codes always contain at least one digit (e.g. PS0150042K0, 13302302).
            -- PDF headers like 'HOWTOMAKEUSEOFTHISPARTSLIST' or 'MISCELLANEOUSCOVERCOMPONENTS' are all-alpha.
            AND "PartCode" IS NOT NULL AND "PartCode" != '' AND "PartCode" ~ '[0-9]'
            AND {CATALOG_PART_ROW_FILTER}
        """
        
        params = [str(query_vector)]
        param_idx = 2

        if catalog_ids:
            sql += f" AND \"CatalogId\" = ANY(${param_idx})"
            params.append(catalog_ids)
            param_idx += 1

        if brand_filter:
            sql += f" AND \"MachineBrand\" ILIKE ${param_idx}"
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            sql += f" AND \"MachineGroup\" ILIKE ${param_idx}"
            params.append(f"%{machine_group_filter}%")
            param_idx += 1
            
        sql += f" ORDER BY similarity DESC LIMIT ${param_idx}"
        params.append(limit)

        results = await conn.fetch(sql, *params)
        rows = [dict(row) for row in results]

        # Minimum similarity filtresi — alakasız sonuçları eler
        rows = [r for r in rows if r.get("similarity", 0) >= min_similarity]
        return rows

    except Exception as e:
        logger.error(f"❌ Vektör Arama Hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


async def hybrid_search_vector_db(
    query_vector: list,
    query_text: str,
    brand_filter: str = None,
    limit: int = 5,
    catalog_ids: list = None,
    machine_group_filter: str = None,
    min_similarity: float = 0.3,
    vector_weight: float = 0.60,
    lexical_weight: float = 0.40,
    candidate_limit: int = 50,
):
    """
    Context-aware hybrid search over CatalogItems.

    Candidate generation uses pgvector cosine similarity as the broad semantic
    lane and PostgreSQL FTS as a tightly capped lexical candidate generator.
    Final ranking uses weighted score when both channels hit, otherwise the
    available channel score is preserved for exact lexical matches.
    """
    if not query_vector:
        logger.warning("⚠️ hybrid_search_vector_db çağrıldı ama query_vector None/boş!")
        return []

    cleaned_query = str(query_text or "").strip()
    if not cleaned_query:
        return await search_vector_db(
            query_vector,
            brand_filter=brand_filter,
            limit=limit,
            catalog_ids=catalog_ids,
            machine_group_filter=machine_group_filter,
            min_similarity=min_similarity,
        )

    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        if len(query_vector) != 3072:
            logger.warning(f"⚠️ Vektör boyutu 3072 değil! Gelen: {len(query_vector)}")

        fts_query = _build_turkish_fts_query(cleaned_query)
        lexical_candidate_limit = min(int(candidate_limit), FTS_CANDIDATE_LIMIT)
        params = [
            str(query_vector),
            fts_query,
            float(vector_weight),
            float(lexical_weight),
            int(candidate_limit),
            int(limit),
            float(min_similarity),
            lexical_candidate_limit,
        ]
        param_idx = 9

        filters = [
            "ci.\"PartCode\" IS NOT NULL",
            "ci.\"PartCode\" != ''",
            "ci.\"PartCode\" ~ '[0-9]'",
            CATALOG_PART_ROW_FILTER,
        ]

        if catalog_ids:
            filters.append(f"ci.\"CatalogId\" = ANY(${param_idx})")
            params.append(catalog_ids)
            param_idx += 1

        if brand_filter:
            filters.append(f"ci.\"MachineBrand\" ILIKE ${param_idx}")
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            filters.append(f"ci.\"MachineGroup\" ILIKE ${param_idx}")
            params.append(f"%{machine_group_filter}%")
            param_idx += 1

        filter_sql = " AND ".join(filters)

        sql = f"""
            WITH query_input AS (
                SELECT
                    $1::vector AS query_vector,
                    CASE
                        WHEN NULLIF($2, '') IS NULL THEN NULL::tsquery
                        ELSE to_tsquery('simple', $2)
                    END AS ts_query,
                    $3::float AS vector_weight,
                    $4::float AS lexical_weight,
                    $5::int AS candidate_limit,
                    $6::int AS result_limit,
                    $7::float AS min_score,
                    $8::int AS lexical_candidate_limit
            ),
            vector_matches AS (
                SELECT
                    ci."Id",
                    ci."CatalogId",
                    ci."PageNumber",
                    COALESCE(ci."VisualPageNumber"::text, NULLIF(ci."PageNumber", '')) AS "ViewerPageNumber",
                    ci."RefNumber",
                    ci."PartCode",
                    ci."PartName",
                    ci."MachineBrand",
                    ci."MachineModel",
                    ci."MachineGroup",
                    ci."Mechanism",
                    ci."Description",
                    ci."Dimensions",
                    1 - (ci."Embedding" <=> qi.query_vector) AS vector_score,
                    NULL::float AS lexical_score,
                    ROW_NUMBER() OVER (ORDER BY ci."Embedding" <=> qi.query_vector ASC) AS vector_rank,
                    NULL::bigint AS lexical_rank
                FROM "CatalogItems" ci
                CROSS JOIN query_input qi
                WHERE ci."Embedding" IS NOT NULL
                  AND {filter_sql}
                ORDER BY ci."Embedding" <=> qi.query_vector ASC
                LIMIT (SELECT candidate_limit FROM query_input)
            ),
            lexical_matches AS (
                SELECT
                    ci."Id",
                    ci."CatalogId",
                    ci."PageNumber",
                    COALESCE(ci."VisualPageNumber"::text, NULLIF(ci."PageNumber", '')) AS "ViewerPageNumber",
                    ci."RefNumber",
                    ci."PartCode",
                    ci."PartName",
                    ci."MachineBrand",
                    ci."MachineModel",
                    ci."MachineGroup",
                    ci."Mechanism",
                    ci."Description",
                    ci."Dimensions",
                    NULL::float AS vector_score,
                    ts_rank_cd(ci."SearchTextVector", qi.ts_query)::float AS lexical_score,
                    NULL::bigint AS vector_rank,
                    ROW_NUMBER() OVER (
                        ORDER BY ts_rank_cd(ci."SearchTextVector", qi.ts_query) DESC, ci."PartCode" ASC
                    ) AS lexical_rank
                FROM "CatalogItems" ci
                CROSS JOIN query_input qi
                WHERE qi.ts_query IS NOT NULL
                  AND ci."SearchTextVector" @@ qi.ts_query
                  AND {filter_sql}
                ORDER BY ts_rank_cd(ci."SearchTextVector", qi.ts_query) DESC, ci."PartCode" ASC
                LIMIT (SELECT lexical_candidate_limit FROM query_input)
            ),
            combined AS (
                SELECT * FROM vector_matches
                UNION ALL
                SELECT * FROM lexical_matches
            ),
            aggregated AS (
                SELECT
                    "Id",
                    "CatalogId",
                    "PageNumber",
                    "ViewerPageNumber",
                    "RefNumber",
                    "PartCode",
                    "PartName",
                    "MachineBrand",
                    "MachineModel",
                    "MachineGroup",
                    "Mechanism",
                    "Description",
                    "Dimensions",
                    MAX(vector_score) AS vector_score,
                    MAX(lexical_score) AS lexical_score,
                    MIN(vector_rank) AS vector_rank,
                    MIN(lexical_rank) AS lexical_rank
                FROM combined
                GROUP BY
                    "Id",
                    "CatalogId",
                    "PageNumber",
                    "ViewerPageNumber",
                    "RefNumber",
                    "PartCode",
                    "PartName",
                    "MachineBrand",
                    "MachineModel",
                    "MachineGroup",
                    "Mechanism",
                    "Description",
                    "Dimensions"
            ),
            scored AS (
                SELECT
                    a.*,
                    COALESCE(a.vector_score, 0.0) AS vector_similarity,
                    CASE
                        WHEN a.lexical_score IS NULL THEN 0.0
                        ELSE COALESCE(
                            a.lexical_score / NULLIF(MAX(a.lexical_score) OVER (), 0.0),
                            1.0 / (1.0 + ((a.lexical_rank - 1)::float * 0.15))
                        )
                    END AS lexical_similarity,
                    COALESCE(1.0 / (60.0 + a.vector_rank), 0.0)
                        + COALESCE(1.0 / (60.0 + a.lexical_rank), 0.0) AS rrf_score
                FROM aggregated a
            ),
            hybrid AS (
                SELECT
                    s.*,
                    CASE
                        WHEN s.vector_score IS NOT NULL AND s.lexical_score IS NOT NULL THEN
                            (qi.vector_weight * s.vector_similarity) + (qi.lexical_weight * s.lexical_similarity)
                        ELSE GREATEST(s.vector_similarity, s.lexical_similarity)
                    END AS hybrid_score
                FROM scored s
                CROSS JOIN query_input qi
            )
            SELECT
                h."Id",
                h."CatalogId",
                h."PageNumber",
                h."ViewerPageNumber",
                h."RefNumber",
                h."PartCode",
                h."PartName",
                h."MachineBrand",
                h."MachineModel",
                h."MachineGroup",
                h."Mechanism",
                h."Description",
                h."Dimensions",
                h.hybrid_score AS similarity,
                h.vector_similarity,
                h.lexical_similarity,
                h.rrf_score,
                h.vector_rank,
                h.lexical_rank,
                h.hybrid_score,
                (SELECT "StockQuantity"
                 FROM "Products"
                 WHERE "Code" = h."PartCode" AND "CatalogId" = h."CatalogId"
                 LIMIT 1) AS "StockQuantity"
            FROM hybrid h
            CROSS JOIN query_input qi
            WHERE h.hybrid_score >= qi.min_score
            ORDER BY h.hybrid_score DESC, h.rrf_score DESC, h.lexical_rank ASC NULLS LAST, h.vector_rank ASC NULLS LAST
            LIMIT (SELECT result_limit FROM query_input)
        """

        results = await conn.fetch(sql, *params)
        rows = [dict(row) for row in results]
        for row in rows:
            row["_hybrid_search"] = True
        return rows

    except Exception as e:
        logger.error(f"❌ Hibrit Arama Hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


# =============================================
# 🖼️ VISUAL VECTOR SEARCH
# =============================================
async def search_visual_vector_db(query_vector: list, brand_filter: str = None, limit: int = 5, catalog_ids: list = None, min_similarity: float = 0.75, machine_group_filter: str = None):
    """
    VisualEmbedding sütunu üzerinden görsel benzerlik araması yapar.
    Yalnızca VisualEmbedding dolu olan kayıtlarda arar.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        if len(query_vector) != 3072:
            logger.warning(f"⚠️ Visual vektör boyutu 3072 değil! Gelen: {len(query_vector)}")

        sql = """
            SELECT 
                "Id",
                "CatalogId",
                "PageNumber",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", '')) AS "ViewerPageNumber",
                "RefNumber",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel",
                "MachineGroup",
                "Mechanism",
                "Description",
                "Dimensions",
                "VisualImageUrl",
                1 - ("VisualEmbedding" <=> $1) as visual_similarity
            FROM "CatalogItems"
            WHERE "VisualEmbedding" IS NOT NULL
        """

        params = [str(query_vector)]
        param_idx = 2

        if catalog_ids:
            sql += f" AND \"CatalogId\" = ANY(${param_idx})"
            params.append(catalog_ids)
            param_idx += 1

        if brand_filter:
            sql += f" AND \"MachineBrand\" ILIKE ${param_idx}"
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            sql += f" AND \"MachineGroup\" ILIKE ${param_idx}"
            params.append(f"%{machine_group_filter}%")
            param_idx += 1

        sql += f" ORDER BY visual_similarity DESC LIMIT ${param_idx}"
        params.append(limit)

        results = await conn.fetch(sql, *params)
        rows = [dict(row) for row in results]
        rows = [r for r in rows if (r.get("visual_similarity") or 0) >= min_similarity]
        return rows

    except Exception as e:
        logger.error(f"❌ Visual Vektör Arama Hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


def _dedupe_hint_terms(values: list[str]) -> list[str]:
    terms: list[str] = []
    seen = set()
    for value in values:
        text = str(value or "").strip()
        if not text:
            continue
        # Keep short OCR/code-like values, drop noisy natural-language crumbs.
        if len(text) < 3 and not any(ch.isdigit() for ch in text):
            continue
        key = text.casefold()
        if key in seen:
            continue
        seen.add(key)
        terms.append(text)
    return terms


async def search_by_visual_hints(
    visual_hints: dict,
    brand_filter: str = None,
    limit: int = 8,
    catalog_ids: list = None,
    machine_group_filter: str = None,
):
    """
    Görselden çıkarılan yapısal ipuçlarıyla aday getirir.
    Bu arama gerçek image-image retrieval değildir; OCR/kategori/şekil/bağlam sinyallerini
    katalog metadatası üzerinde kullanarak aday kümesini daraltır.
    """
    if not isinstance(visual_hints, dict):
        return []

    raw_terms: list[str] = []
    for key in (
        "candidate_part_name",
        "part_family",
        "part_category",
        "material",
        "material_hint",
        "size_hint",
        "assembly_hint",
        "machine_type_hint",
        "detected_brand_text",
        "visible_codes",
    ):
        value = visual_hints.get(key)
        if isinstance(value, list):
            raw_terms.extend(str(x) for x in value)
        elif value:
            raw_terms.append(str(value))

    for key in ("shape_traits", "shape_tags", "brand_model_tokens", "visible_code_tokens"):
        value = visual_hints.get(key)
        if isinstance(value, list):
            raw_terms.extend(str(x) for x in value)
        elif value:
            raw_terms.append(str(value))

    terms = _dedupe_hint_terms(raw_terms)[:12]
    if not terms:
        return []

    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        select_score_parts: list[str] = []
        where_parts: list[str] = []
        params: list = []
        param_idx = 1

        if catalog_ids:
            where_parts.append(f"\"CatalogId\" = ANY(${param_idx})")
            params.append(catalog_ids)
            param_idx += 1

        if brand_filter:
            where_parts.append(f"\"MachineBrand\" ILIKE ${param_idx}")
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            where_parts.append(f"\"MachineGroup\" ILIKE ${param_idx}")
            params.append(f"%{machine_group_filter}%")
            param_idx += 1

        searchable_fields = """
            COALESCE("PartName", '') || ' ' ||
            COALESCE("Description", '') || ' ' ||
            COALESCE("PartCode", '') || ' ' ||
            COALESCE("RefNumber", '') || ' ' ||
            COALESCE("MachineBrand", '') || ' ' ||
            COALESCE("MachineModel", '') || ' ' ||
            COALESCE("MachineGroup", '') || ' ' ||
            COALESCE("Mechanism", '') || ' ' ||
            COALESCE("Dimensions", '') || ' ' ||
            COALESCE("VisualShapeTags"::text, '') || ' ' ||
            COALESCE("VisualOcrText", '')
        """

        term_match_parts: list[str] = []
        for term in terms:
            select_score_parts.append(f"CASE WHEN ({searchable_fields}) ILIKE ${param_idx} THEN 1 ELSE 0 END")
            term_match_parts.append(f"({searchable_fields}) ILIKE ${param_idx}")
            params.append(f"%{term}%")
            param_idx += 1

        where_parts.append("(" + " OR ".join(term_match_parts) + ")")
        where_parts.append("\"PartCode\" IS NOT NULL AND \"PartCode\" != '' AND \"PartCode\" ~ '[0-9]'")

        visual_hint_score_sql = " + ".join(select_score_parts)
        where_sql = " AND ".join(where_parts)

        sql = f"""
            SELECT
                "Id",
                "CatalogId",
                "PageNumber",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", '')) AS "ViewerPageNumber",
                "RefNumber",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel",
                "MachineGroup",
                "Mechanism",
                "Description",
                "Dimensions",
                "VisualImageUrl",
                "VisualShapeTags",
                "VisualOcrText",
                ({visual_hint_score_sql}) AS visual_hint_score,
                (SELECT "StockQuantity" FROM "Products" WHERE "Code" = "CatalogItems"."PartCode" AND "CatalogId" = "CatalogItems"."CatalogId" LIMIT 1) AS "StockQuantity"
            FROM "CatalogItems"
            WHERE {where_sql}
            ORDER BY visual_hint_score DESC, "PartCode" ASC
            LIMIT ${param_idx}
        """
        params.append(limit)

        rows = await conn.fetch(sql, *params)
        return [dict(row) for row in rows]
    except Exception as e:
        logger.error(f"❌ Görsel ipucu arama hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


async def search_by_page_and_part(
    catalog_ids: list,
    page_number: str,
    part_name: str,
    limit: int = 5,
    brand_filter: str = None,
    machine_group_filter: str = None,
):
    """
    Belirli CatalogId + PageNumber içinde parça adına göre arama yapar.
    context_part tabanlı iki adımlı arama akışında kullanılır.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        normalized_page = str(page_number or "").strip()
        normalized_part = str(part_name or "").strip()
        if not normalized_page or not normalized_part:
            return []

        if not catalog_ids:
            return []

        sql = """
            SELECT
                "Id",
                "CatalogId",
                "PageNumber",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", '')) AS "ViewerPageNumber",
                "RefNumber",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel",
                "MachineGroup",
                "Mechanism",
                "Description",
                "Dimensions",
                "VisualImageUrl"
            FROM "CatalogItems"
            WHERE
                "CatalogId" = ANY($1)
                AND ("PageNumber" = $2 OR COALESCE("VisualPageNumber"::text, '') = $2)
                AND (
                    "PartName" ILIKE $3
                    OR "Description" ILIKE $3
                    OR "PartCode" ILIKE $3
                    OR "RefNumber" ILIKE $3
                )
        """

        params = [catalog_ids, normalized_page, f"%{normalized_part}%"]
        param_idx = 4

        if brand_filter:
            sql += f" AND \"MachineBrand\" ILIKE ${param_idx}"
            params.append(f"%{brand_filter}%")
            param_idx += 1

        if machine_group_filter:
            sql += f" AND \"MachineGroup\" ILIKE ${param_idx}"
            params.append(f"%{machine_group_filter}%")
            param_idx += 1

        sql += (
            f" ORDER BY "
            f"CASE WHEN \"PartName\" ILIKE $3 THEN 0 ELSE 1 END, "
            f"\"PartCode\" ASC "
            f"LIMIT ${param_idx}"
        )
        params.append(limit)

        results = await conn.fetch(sql, *params)
        return [dict(row) for row in results]
    except Exception as e:
        logger.error(f"❌ Sayfa+Parça arama hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


async def get_catalog_brands(catalog_ids: list) -> list[str]:
    """
    Verilen kataloglar içindeki farklı MachineBrand değerlerini döndürür.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        if not catalog_ids:
            return []

        rows = await conn.fetch(
            """
            SELECT DISTINCT "MachineBrand"
            FROM "CatalogItems"
            WHERE "CatalogId" = ANY($1)
              AND "MachineBrand" IS NOT NULL
              AND TRIM("MachineBrand") <> ''
            ORDER BY "MachineBrand" ASC
            """,
            catalog_ids,
        )
        brands: list[str] = []
        for row in rows:
            value = row["MachineBrand"]
            if value is None:
                continue
            text = str(value).strip()
            if text:
                brands.append(text)
        return brands
    except Exception as e:
        logger.error(f"❌ Katalog marka listesi alma hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


# =============================================
# ✏️ VISUAL EMBEDDING GÜNCELLEME
# =============================================
async def update_visual_embedding_in_db(
    part_code: str,
    visual_vector: list,
    visual_image_url: str = None,
    visual_shape_tags: list = None,
    visual_ocr_text: str = None,
) -> bool:
    """
    Feedback onayında VisualEmbedding, VisualImageUrl, VisualShapeTags, VisualOcrText
    alanlarını DB'de günceller.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return False

    try:
        shape_tags_json = json.dumps(visual_shape_tags, ensure_ascii=False) if visual_shape_tags else None

        result = await conn.execute(
            """
            UPDATE "CatalogItems"
            SET
                "VisualEmbedding"  = $1,
                "VisualImageUrl"   = COALESCE($2, "VisualImageUrl"),
                "VisualShapeTags"  = COALESCE($3, "VisualShapeTags"),
                "VisualOcrText"    = COALESCE($4, "VisualOcrText")
            WHERE "PartCode" = $5
            """,
            str(visual_vector),
            visual_image_url,
            shape_tags_json,
            visual_ocr_text,
            part_code,
        )

        updated_count = int(result.split()[-1]) if result else 0
        logger.info(f"VisualEmbedding UPDATE: {updated_count} satır güncellendi (part_code={part_code})")
        return updated_count > 0

    except Exception as e:
        logger.error(f"❌ VisualEmbedding DB Güncelleme Hatası: {e}")
        return False
    finally:
        await _release_conn(conn, from_pool)
