"""
Partalog AI - Vector Database Service (Async/Pgvector/3072 + Exact Match)
---------------------------------------------------------
Görevi: C# tarafından oluşturulan 3072'lik vektörleri aramak ve 
Parça Kodu (PartCode) ile birebir eşleşme (Hard-Boost) yapmak.
"""

import asyncpg
import json
from loguru import logger
from config import settings

async def get_db_connection():
    """
    Asenkron veritabanı bağlantısı (asyncpg).
    Config dosyasındaki farklı isimlendirmeleri (DB_CONNECTION_STRING veya DATABASE_URL) yönetir.
    """
    try:
        # 1. Önce senin muhtemel ayar ismini deneriz
        dsn = getattr(settings, "DB_CONNECTION_STRING", None)
        
        # 2. Bulamazsa standart ismi deneriz
        if not dsn:
            dsn = getattr(settings, "DATABASE_URL", None)
            
        if not dsn:
            logger.critical("❌ HATA: Config dosyasında Veritabanı Bağlantı Linki bulunamadı!")
            return None

        # Bağlantıyı kur
        return await asyncpg.connect(dsn)

    except Exception as e:
        logger.error(f"❌ Veritabanı Bağlantı Hatası: {e}")
        return None

async def exact_match_search(part_code: str, brand_filter: str = None, catalog_ids: list = None, limit: int = 5):
    """
    Vektör (Semantic) arama YAPMADAN, parça koduna (PartCode) göre birebir/yakın eşleşme arar.
    Chatbot'ta kod belirtilmişse ilk olarak "Hard-Boost" için kullanılır.
    """
    conn = await get_db_connection()
    if not conn:
        return []

    try:
        # Puanı 1.0 (Tam eşleşme) olarak döndürüyoruz ki frontend veya chat formatı bozulmasın.
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
        
        # ILIKE kullanarak büyük/küçük harf duyarlılığını aşıyoruz.
        # '%kod%' formatı, kodun içinde geçmesi durumunda da (örn: B2424 aranınca B2424-354) bulmasını sağlar.
        params = [f"%{part_code}%"] 
        param_idx = 2

        # Kullanıcıya ait katalog filtresi
        if catalog_ids:
            sql += f" AND \"CatalogId\" = ANY(${param_idx})"
            params.append(catalog_ids)
            param_idx += 1

        # Marka Filtresi
        if brand_filter:
            sql += f" AND \"MachineBrand\" ILIKE ${param_idx}"
            params.append(f"%{brand_filter}%")
            param_idx += 1
            
        # Limit
        sql += f" LIMIT ${param_idx}"
        params.append(limit)

        results = await conn.fetch(sql, *params)
        
        # Dict listesine çevir
        return [dict(row) for row in results]

    except Exception as e:
        logger.error(f"❌ Exact Match Arama Hatası: {e}")
        return []
    finally:
        if conn:
            await conn.close()


async def search_vector_db(query_vector: list, brand_filter: str = None, limit: int = 5, catalog_ids: list = None):
    """
    Vektörel benzerlik (Semantic) araması yapar.
    
    Args:
        query_vector (list): 3072 boyutlu float listesi.
        brand_filter (str): Marka filtresi (Opsiyonel).
        limit (int): Sonuç sayısı.
        catalog_ids (list): Kullanıcıya ait katalog ID listesi (Opsiyonel).
    """
    conn = await get_db_connection()
    if not conn:
        return []

    try:
        # 1. Boyut Güvenlik Kontrolü (3072)
        if len(query_vector) != 3072:
            logger.warning(f"⚠️ Vektör boyutu 3072 değil! Gelen: {len(query_vector)}")

        # 2. SQL Sorgusu (Cosine Similarity: <=>)
        # asyncpg'de parametreler $1, $2 diye gider.
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
        
        # pgvector için vektörü string formatında gönderiyoruz '[0.1, 0.2...]'
        params = [str(query_vector)]
        param_idx = 2

        # ✅ Catalog filtresi (kullanıcıya ait kataloglar)
        if catalog_ids:
            sql += f" AND \"CatalogId\" = ANY(${param_idx})"
            params.append(catalog_ids)
            param_idx += 1

        # 3. Marka Filtresi (Varsa)
        if brand_filter:
            sql += f" AND \"MachineBrand\" ILIKE ${param_idx}"
            params.append(f"%{brand_filter}%")
            param_idx += 1
            
        # 4. Sıralama ve Limit
        sql += f" ORDER BY similarity DESC LIMIT ${param_idx}"
        params.append(limit)

        # 5. Çalıştır
        results = await conn.fetch(sql, *params)
        
        # Sonuçları Dictionary listesine çevir
        return [dict(row) for row in results]

    except Exception as e:
        logger.error(f"❌ Vektör Arama Hatası: {e}")
        return []
    finally:
        if conn:
            await conn.close()


async def search_visual_vector_db(query_vector: list, brand_filter: str = None, limit: int = 5, catalog_ids: list = None, min_similarity: float = 0.75):
    """
    VisualEmbedding sütunu üzerinden görsel benzerlik araması yapar.
    Yalnızca VisualEmbedding dolu olan kayıtlarda arar.
    Foto→foto eşleşmesi için kullanılır.
    """
    conn = await get_db_connection()
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

        # min_similarity filtresi
        rows = [r for r in rows if (r.get("visual_similarity") or 0) >= min_similarity]
        return rows

    except Exception as e:
        logger.error(f"❌ Visual Vektör Arama Hatası: {e}")
        return []
    finally:
        if conn:
            await conn.close()


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
    part_code ile eşleşen TÜM CatalogItem'ları günceller.
    """
    conn = await get_db_connection()
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

        # asyncpg "UPDATE N" string döndürür
        updated_count = int(result.split()[-1]) if result else 0
        logger.info(f"VisualEmbedding UPDATE: {updated_count} satır güncellendi (part_code={part_code})")
        return updated_count > 0

    except Exception as e:
        logger.error(f"❌ VisualEmbedding DB Güncelleme Hatası: {e}")
        return False
    finally:
        if conn:
            await conn.close()