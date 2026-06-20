import argparse
import asyncio
import json
import os
from pathlib import Path
from typing import Any

import asyncpg


DEFAULT_TEXTS = [
    "bu parça hangi sayfada",
    "bunun kodu ne",
    "bu parça ne işe yarar",
    "seçtiğim parçayı katalogda açmak istiyorum",
]


def get_dsn() -> str:
    dsn = (os.getenv("DB_CONNECTION_STRING") or os.getenv("DATABASE_URL") or "").strip()
    if not dsn:
        raise RuntimeError("DB_CONNECTION_STRING veya DATABASE_URL gerekli.")
    return dsn


def split_csv(value: str | None) -> list[str]:
    return [item.strip() for item in (value or "").split(",") if item.strip()]


async def fetch_seed_items(
    dsn: str,
    catalog_ids: list[str],
    limit: int,
) -> list[asyncpg.Record]:
    conn = await asyncpg.connect(dsn)
    try:
        where = """
            WHERE "PartCode" IS NOT NULL
              AND TRIM("PartCode") <> ''
              AND "PartName" IS NOT NULL
              AND TRIM("PartName") <> ''
        """
        params: list[Any] = []
        if catalog_ids:
            where += " AND \"CatalogId\" = ANY($1)"
            params.append(catalog_ids)

        limit_param = len(params) + 1
        params.append(limit)
        return await conn.fetch(
            f"""
            SELECT
                "CatalogId",
                COALESCE("VisualPageNumber"::text, NULLIF("PageNumber", ''), '1') AS "PageNumber",
                "RefNumber",
                "PartCode",
                "PartName"
            FROM "CatalogItems"
            {where}
            ORDER BY
                CASE WHEN "PartName" = 'Unknown Part' THEN 1 ELSE 0 END,
                "UpdatedDate" DESC NULLS LAST,
                "CreatedDate" DESC NULLS LAST
            LIMIT ${limit_param}
            """,
            *params,
        )
    finally:
        await conn.close()


def build_cases(rows: list[asyncpg.Record], public_token: str) -> list[dict[str, Any]]:
    cases: list[dict[str, Any]] = []
    for row_index, row in enumerate(rows, start=1):
        code = str(row["PartCode"] or "").strip()
        if not code:
            continue

        context = {
            "catalogId": str(row["CatalogId"]),
            "pageNumber": str(row["PageNumber"] or "1"),
            "partCode": code,
            "refNo": str(row["RefNumber"] or "").strip(),
            "partName": str(row["PartName"] or "").strip(),
        }

        for text_index, text in enumerate(DEFAULT_TEXTS, start=1):
            cases.append(
                {
                    "id": f"CTXDB{row_index:03d}-{text_index}",
                    "text": text,
                    "public_token": public_token,
                    "catalog_ids": [context["catalogId"]],
                    "context_json": context,
                    "expected_codes": [code],
                    "required_terms": [code] if "kodu" in text or "sayfada" in text else [],
                }
            )
    return cases


async def main_async() -> int:
    parser = argparse.ArgumentParser(description="Build context-aware chat eval cases from CatalogItems.")
    parser.add_argument("--output", default="eval/queries.context.generated.jsonl")
    parser.add_argument("--public-token", default=os.getenv("PARTALOG_PUBLIC_TOKEN", "<PUBLIC_TOKEN>"))
    parser.add_argument("--catalog-ids", default=os.getenv("PARTALOG_CATALOG_IDS", ""))
    parser.add_argument("--limit", type=int, default=10)
    args = parser.parse_args()

    rows = await fetch_seed_items(get_dsn(), split_csv(args.catalog_ids), args.limit)
    cases = build_cases(rows, args.public_token)

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8") as handle:
        for case in cases:
            handle.write(json.dumps(case, ensure_ascii=False) + "\n")

    print(f"Wrote {len(cases)} cases from {len(rows)} seed items to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main_async()))
