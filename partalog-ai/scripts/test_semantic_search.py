"""
Partalog — Semantic Search Integration Test

Tests that vector search now works end-to-end after populating embeddings.

Usage:
  cd partalog-ai
  GENAI_PROVIDER=vertex DB_CONNECTION_STRING="postgresql://postgres:CHANGE_ME@127.0.0.1:5432/KatalogcuDb" \
    python scripts/test_semantic_search.py

Tests:
  1. "kumaşı alttan çeken parça" → should find "dişli" / feed-dog (purely semantic)
  2. "iğne plaka" → should find needle-plate related items
  3. "makaranın altındaki yay" → should find bobbin case spring
"""

import asyncio
import os
import sys
import json

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from loguru import logger
from config import settings
from services.embedding import get_text_embedding
from services.vector_db import init_db_pool, close_db_pool, search_vector_db


TEST_QUERIES = [
    {
        "query": "kumaşı alttan çeken parça",
        "description": "Semantic: fabric-pulling part → should find feed dog / dişli",
        "expected_keywords": ["DİŞLİ", "dişli"],
    },
    {
        "query": "iğne plaka",
        "description": "Semantic: needle plate → should find needle plate items",
        "expected_keywords": [],
    },
    {
        "query": "makaranın altındaki yay",
        "description": "Semantic: bobbin spring → should find spring parts",
        "expected_keywords": [],
    },
    {
        "query": "iplik geçirme mekanizması",
        "description": "Semantic: thread take-up mechanism",
        "expected_keywords": [],
    },
]

# Also run a pure keyword fallback to prove that keyword search WOULD fail for query 1
KEYWORD_FALLBACK_QUERY = "kumaşı alttan çeken parça"


async def test_keyword_fallback():
    """Show that keyword search cannot find anything for the semantic query."""
    import asyncpg

    dsn = settings.db_dsn
    if not dsn:
        logger.error("No DB DSN")
        return

    conn = await asyncpg.connect(dsn)
    try:
        # Try keyword search for each individual word
        words = KEYWORD_FALLBACK_QUERY.lower().split()
        total = 0
        for w in words:
            pattern = f"%{w}%"
            rows = await conn.fetch(
                """
                SELECT "PartCode", "PartName", "Description"
                FROM "CatalogItems"
                WHERE "PartName" ILIKE $1 OR "Description" ILIKE $1
                LIMIT 5
                """,
                pattern,
            )
            if rows:
                total += len(rows)
                logger.info(f"   Word '{w}' found {len(rows)} results")
                for r in rows:
                    logger.info(f"     {r['PartCode']}: {r['PartName']}")
        if total == 0:
            logger.info(f"✅ Keyword search returns ZERO results for '{KEYWORD_FALLBACK_QUERY}' — proving semantic search is needed")
        else:
            logger.info(f"⚠️ Keyword search found {total} results (some words match individually)")
    finally:
        await conn.close()


async def test_semantic_search():
    """Test that vector search returns semantically relevant results."""
    logger.info(f"{'='*60}")
    logger.info(f"🧪 SEMANTIC SEARCH INTEGRATION TEST")
    logger.info(f"{'='*60}")

    # Initialize the DB pool
    pool_result = await init_db_pool()
    logger.info(f"DB pool init: ready={pool_result.get('ready')} mode={pool_result.get('mode')}")

    if not pool_result.get("ready"):
        logger.error("DB pool not ready — cannot test")
        return

    try:
        for test in TEST_QUERIES:
            query = test["query"]
            desc = test["description"]
            expected = test["expected_keywords"]

            logger.info(f"\n{'─'*60}")
            logger.info(f"🔍 Query: \"{query}\"")
            logger.info(f"   {desc}")

            # Get the embedding
            vector = await get_text_embedding(query)
            if not vector:
                logger.error(f"❌ Embedding API returned None for '{query}'")
                continue

            logger.info(f"   ✅ Embedding: {len(vector)}-dim vector generated")

            # Search vector DB
            results = await search_vector_db(
                query_vector=vector,
                limit=8,
                min_similarity=0.2,  # low threshold to catch anything relevant
            )

            if not results:
                logger.warning(f"   ⚠️ No vector search results (below similarity threshold)")
                continue

            logger.info(f"   📊 Top {len(results)} results (by cosine similarity):")

            for i, r in enumerate(results, 1):
                sim = r.get("similarity", 0)
                code = r.get("PartCode", "?")
                name = r.get("PartName", "?")
                desc_text = (r.get("Description") or "")[:80]
                brand = r.get("MachineBrand") or ""
                model = r.get("MachineModel") or ""
                logger.info(
                    f"   [{i}] sim={sim:.4f} | {code} | {name}"
                    + (f" | {desc_text}" if desc_text else "")
                    + (f" | {brand} {model}" if brand else "")
                )

            # Check for expected keywords
            if expected:
                all_text = " ".join(
                    str(r.get("PartName", "")) + " " + str(r.get("Description", "") or "")
                    for r in results
                ).upper()
                found = [kw for kw in expected if kw.upper() in all_text]
                if found:
                    logger.info(f"   ✅ Found expected keyword(s): {found}")
                else:
                    logger.warning(f"   ⚠️ Expected keywords {expected} not found in results")

    finally:
        await close_db_pool()


async def main():
    logger.info(f"🧪 STEP 1: Verify keyword search FAILS for semantic query")
    await test_keyword_fallback()

    logger.info(f"\n🧪 STEP 2: Run semantic search tests")
    await test_semantic_search()

    logger.info(f"\n{'='*60}")
    logger.info(f"🏁 TEST COMPLETE")


if __name__ == "__main__":
    asyncio.run(main())
