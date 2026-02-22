"""
Partalog AI - Vector Database Service (Async/Pgvector/3072 + Exact Match + Connection Pool)
---------------------------------------------------------
Görevi: C# tarafından oluşturulan 3072'lik vektörleri aramak ve 
Parça Kodu (PartCode) ile birebir eşleşme (Hard-Boost) yapmak.
v2: asyncpg.Pool ile bağlantı havuzu kullanılıyor.
"""

import asyncpg
import json
from loguru import logger
from config import settings

# =============================================
# 🏊 CONNECTION POOL (uygulama ömrünce yaşar)
# =============================================
_pool: asyncpg.Pool | None = None


def _get_dsn() -> str | None:
    dsn = getattr(settings, "DB_CONNECTION_STRING", None)
    if not dsn:
        dsn = getattr(settings, "DATABASE_URL", None)
    return dsn


async def init_db_pool():
    """
    Uygulama startup'ında çağrılmalı (main.py lifespan).
    Pool'u oluşturur.
    """
    global _pool
    dsn = _get_dsn()
    if not dsn:
        logger.critical("❌ HATA: Config dosyasında Veritabanı Bağlantı Linki bulunamadı!")
        return
    try:
        _pool = await asyncpg.create_pool(dsn, min_size=2, max_size=10)
        logger.success("✅ DB Connection Pool oluşturuldu.")
    except Exception as e:
        logger.error(f"❌ DB Pool Oluşturma Hatası: {e}")


async def close_db_pool():
    """
    Uygulama shutdown'ında çağrılmalı (main.py lifespan).
    """
    global _pool
    if _pool:
        await _pool.close()
        _pool = None
        logger.info("🔌 DB Connection Pool kapatıldı.")


async def _get_conn():
    """
    Pool varsa pool'dan, yoksa tek bağlantı açar (graceful fallback).
    """
    global _pool
    if _pool:
        return await _pool.acquire(), True   # (conn, from_pool)
    # Pool henüz başlatılmadıysa tek bağlantı aç
    dsn = _get_dsn()
    if not dsn:
        return None, False
    try:
        conn = await asyncpg.connect(dsn)
        return conn, False
    except Exception as e:
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
async def exact_match_search(part_code: str, brand_filter: str = None, catalog_ids: list = None, limit: int = 5):
    """
    Vektör (Semantic) arama YAPMADAN, parça koduna (PartCode) göre birebir/yakın eşleşme arar.
    Chatbot'ta kod belirtilmişse ilk olarak "Hard-Boost" için kullanılır.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        sql = """
            SELECT 
                "Id",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel", 
                "MachineGroup",
                "Description",
                "Dimensions",
                1.0 as similarity 
            FROM "CatalogItems"
            WHERE "PartCode" ILIKE $1
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
            
        sql += f" LIMIT ${param_idx}"
        params.append(limit)

        results = await conn.fetch(sql, *params)
        return [dict(row) for row in results]

    except Exception as e:
        logger.error(f"❌ Exact Match Arama Hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


# =============================================
# 🧠 VECTOR SEARCH (Semantic / Embedding)
# =============================================
async def search_vector_db(query_vector: list, brand_filter: str = None, limit: int = 5, catalog_ids: list = None):
    """
    Vektörel benzerlik (Semantic) araması yapar.
    """
    conn, from_pool = await _get_conn()
    if not conn:
        return []

    try:
        if len(query_vector) != 3072:
            logger.warning(f"⚠️ Vektör boyutu 3072 değil! Gelen: {len(query_vector)}")

        sql = """
            SELECT 
                "Id",
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel", 
                "MachineGroup",
                "Description",
                "Dimensions",
                1 - ("Embedding" <=> $1) as similarity
            FROM "CatalogItems"
            WHERE 1=1
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
            
        sql += f" ORDER BY similarity DESC LIMIT ${param_idx}"
        params.append(limit)

        results = await conn.fetch(sql, *params)
        return [dict(row) for row in results]

    except Exception as e:
        logger.error(f"❌ Vektör Arama Hatası: {e}")
        return []
    finally:
        await _release_conn(conn, from_pool)


# =============================================
# 🖼️ VISUAL VECTOR SEARCH
# =============================================
async def search_visual_vector_db(query_vector: list, brand_filter: str = None, limit: int = 5, catalog_ids: list = None, min_similarity: float = 0.75):
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
                "PartCode",
                "PartName",
                "MachineBrand",
                "MachineModel",
                "MachineGroup",
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
            WHERE "PartCode" ILIKE $5
            """,
            str(visual_vector),
            visual_image_url,
            shape_tags_json,
            visual_ocr_text,
            f"%{part_code}%",
        )

        updated_count = int(result.split()[-1]) if result else 0
        logger.info(f"VisualEmbedding UPDATE: {updated_count} satır güncellendi (part_code={part_code})")
        return updated_count > 0

    except Exception as e:
        logger.error(f"❌ VisualEmbedding DB Güncelleme Hatası: {e}")
        return False
    finally:
        await _release_conn(conn, from_pool)