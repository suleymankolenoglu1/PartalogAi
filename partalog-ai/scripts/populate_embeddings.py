"""
Partalog — CatalogItem Embedding Batch Populator

Fetches CatalogItems that need context-aware search text or embeddings,
builds a deterministic context-aware search text from catalog fields,
calls the Vertex AI embedding API (gemini-embedding-001, 3072-dim),
and stores both the raw search text and vector back to the DB.

Features:
  - Exponential backoff on 429 quota errors (up to 3 retries)
  - Configurable batch size and inter-batch sleep
  - Dry-run mode for auditing

Usage:
  cd partalog-ai
  GENAI_PROVIDER=vertex DB_CONNECTION_STRING="postgresql://postgres:CHANGE_ME@127.0.0.1:5432/KatalogcuDb" \
    python scripts/populate_embeddings.py [--dry-run] [--batch-size 3] [--sleep 2.0]

Requires:
  - Vertex AI ADC (google.auth.default()) already configured
  - asyncpg installed
  - GENAI_PROVIDER=vertex env var
"""

import argparse
import asyncio
import os
import sys
import time

# ── Ensure we can import from the parent partalog-ai package ──
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import asyncpg
from loguru import logger
from config import settings
from services.genai_provider import provider
from services.embedding import get_text_embedding
from services.search_text_builder import (
    build_catalog_item_search_text,
    build_legacy_catalog_item_search_text,
)


async def ensure_search_text_column(conn) -> None:
    await conn.execute(
        """
        ALTER TABLE "CatalogItems"
        ADD COLUMN IF NOT EXISTS "SearchText" text
        """
    )


async def fetch_items_for_embedding_refresh(
    conn,
    catalog_id: str | None = None,
    *,
    force: bool = False,
) -> list[dict]:
    rows = await conn.fetch(
        f"""
        SELECT
            "Id",
            "PartCode",
            "PartName",
            "Description",
            "CatalogId",
            "RefNumber",
            "Dimensions",
            "Mechanism",
            "MachineBrand",
            "MachineModel",
            "MachineGroup",
            "SearchText"
        FROM "CatalogItems"
        WHERE ($2::boolean OR "Embedding" IS NULL OR "SearchText" IS NULL)
          AND ($1::uuid IS NULL OR "CatalogId" = $1::uuid)
        ORDER BY "PartCode" ASC
        """,
        catalog_id,
        force,
    )
    return [dict(r) for r in rows]


async def update_search_text_and_embedding(conn, item_id: str, search_text: str, vector: list) -> bool:
    result = await conn.execute(
        """
        UPDATE "CatalogItems"
        SET
            "SearchText" = $1,
            "Embedding" = $2
        WHERE "Id" = $3::uuid
        """,
        search_text,
        str(vector),
        item_id,
    )
    count = int(result.split()[-1]) if result else 0
    return count > 0


async def update_search_text_only(conn, item_id: str, search_text: str) -> bool:
    result = await conn.execute(
        """
        UPDATE "CatalogItems"
        SET "SearchText" = $1
        WHERE "Id" = $2::uuid
        """,
        search_text,
        item_id,
    )
    count = int(result.split()[-1]) if result else 0
    return count > 0


async def get_embedding_with_retry(text: str, max_retries: int = 3) -> list | None:
    """
    Calls get_text_embedding with exponential backoff on 429 errors.
    The embedding service already logs the API error; we just retry.
    """
    for attempt in range(1, max_retries + 1):
        vector = await get_text_embedding(text)
        if vector is not None:
            return vector
        # If it returned None, could be 429 quota or other error.
        # Wait and retry with exponential backoff.
        if attempt < max_retries:
            wait = 2.0 ** attempt  # 2, 4, 8 seconds
            logger.warning(f"⏳ Retry {attempt}/{max_retries} after {wait:.0f}s...")
            await asyncio.sleep(wait)
    return None


async def main():
    parser = argparse.ArgumentParser(description="Populate missing CatalogItem embeddings")
    parser.add_argument("--dry-run", action="store_true", help="Only list items, do not update")
    parser.add_argument("--batch-size", type=int, default=3, help="API calls before a short sleep (default: 3)")
    parser.add_argument("--sleep", type=float, default=2.0, help="Seconds to sleep between batches (default: 2.0)")
    parser.add_argument("--max-retries", type=int, default=3, help="Max retries on 429 quota errors (default: 3)")
    parser.add_argument("--catalog-id", help="Only process one catalog id")
    parser.add_argument(
        "--force",
        action="store_true",
        help="Refresh SearchText and Embedding for matching rows even when Embedding is already present",
    )
    parser.add_argument(
        "--search-text-only",
        action="store_true",
        help="Backfill only SearchText; do not call the embedding API",
    )
    parser.add_argument(
        "--show-text",
        action="store_true",
        help="In dry-run mode, print old/new search text samples",
    )
    args = parser.parse_args()

    # ── Validate provider ──
    if not args.search_text_only and not args.dry_run and not provider.has_credentials():
        logger.error("❌ GenAI provider has no credentials. Set GENAI_PROVIDER=vertex and ensure ADC is configured.")
        sys.exit(1)

    if not args.search_text_only:
        logger.info(f"🔐 GenAI provider mode: {'Vertex AI' if provider.use_vertex else 'Legacy API key'}")
        logger.info(f"📐 Embedding model: {settings.GEMINI_EMBEDDING_MODEL}")

    # ── Connect to DB ──
    dsn = settings.db_dsn
    if not dsn:
        logger.error("❌ DB_CONNECTION_STRING not set!")
        sys.exit(1)

    logger.info(f"🗄️  Connecting to DB...")
    conn = await asyncpg.connect(dsn)
    try:
        await ensure_search_text_column(conn)
        items = await fetch_items_for_embedding_refresh(conn, args.catalog_id, force=args.force)
        logger.info(f"📦 Found {len(items)} items needing SearchText/Embedding refresh")

        if not items:
            logger.info("✅ All matching items already have SearchText and embeddings. Nothing to do.")
            return

        if args.dry_run:
            logger.info(f"🧪 DRY RUN — would process {len(items)} items:")
            for it in items:
                text = build_catalog_item_search_text(it)
                logger.info(f"   [{it['PartCode']}] \"{it['PartName']}\" → text({len(text)} chars)")
                if args.show_text:
                    logger.info(f"      legacy: {build_legacy_catalog_item_search_text(it)}")
                    logger.info(f"      v3:     {text}")
            return

        # ── Process in batches with retry ──
        success = 0
        fail = 0
        total = len(items)

        for idx, item in enumerate(items, start=1):
            item_id = item["Id"]
            search_text = build_catalog_item_search_text(item)

            if not search_text:
                logger.warning(f"⚠️  [{idx}/{total}] Skipping {item['PartCode']}: empty text input")
                fail += 1
                continue

            if args.search_text_only:
                try:
                    ok = await update_search_text_only(conn, item_id, search_text)
                    if ok:
                        success += 1
                        logger.info(f"✅ [{idx}/{total}] {item['PartCode']} → SearchText stored ({len(search_text)} chars)")
                    else:
                        logger.error(f"❌ [{idx}/{total}] SearchText UPDATE returned 0 rows for {item['PartCode']}")
                        fail += 1
                except Exception as e:
                    logger.error(f"❌ [{idx}/{total}] Error updating SearchText for {item['PartCode']}: {e}")
                    fail += 1
                continue

            logger.info(f"🔮 [{idx}/{total}] Embedding {item['PartCode']} ({len(search_text)} chars)...")
            try:
                vector = await get_embedding_with_retry(search_text, max_retries=args.max_retries)
                if not vector:
                    logger.error(f"❌ [{idx}/{total}] All retries exhausted for {item['PartCode']}")
                    fail += 1
                    continue

                ok = await update_search_text_and_embedding(conn, item_id, search_text, vector)
                if ok:
                    success += 1
                    logger.info(
                        f"✅ [{idx}/{total}] {item['PartCode']} → SearchText + {len(vector)}-dim vector stored"
                    )
                else:
                    logger.error(f"❌ [{idx}/{total}] DB UPDATE returned 0 rows for {item['PartCode']}")
                    fail += 1
            except Exception as e:
                logger.error(f"❌ [{idx}/{total}] Error processing {item['PartCode']}: {e}")
                fail += 1

            # Rate-limit: sleep between batches
            if idx % args.batch_size == 0 and idx < total:
                logger.info(f"💤 Batch pause {args.sleep}s...")
                await asyncio.sleep(args.sleep)

        logger.info(f"\n{'='*50}")
        logger.info(f"📊 RESULTS: {success} succeeded, {fail} failed, {total} total")
        logger.info(f"{'='*50}")

        # ── Verify ──
        if args.search_text_only:
            remaining = await conn.fetchval(
                """
                SELECT COUNT(*)
                FROM "CatalogItems"
                WHERE "SearchText" IS NULL
                  AND ($1::uuid IS NULL OR "CatalogId" = $1::uuid)
                """,
                args.catalog_id,
            )
            logger.info(f"📦 Remaining rows with NULL SearchText: {remaining}")
        else:
            remaining = await fetch_items_for_embedding_refresh(conn, args.catalog_id, force=False)
            logger.info(f"📦 Remaining rows with NULL SearchText or NULL Embedding: {len(remaining)}")

    finally:
        await conn.close()


if __name__ == "__main__":
    asyncio.run(main())
