from __future__ import annotations

from decimal import Decimal
from typing import Any

import asyncpg
from loguru import logger

from services import vector_db


def _catalog_ids_from_scope(search_scope: dict | None) -> list[str]:
    values: list[Any] = []
    if search_scope:
        values.extend(search_scope.get("resolved_catalog_ids") or [])
        values.extend(search_scope.get("catalog_ids") or [])

    catalog_ids: list[str] = []
    for value in values:
        item = str(value or "").strip()
        if item and item not in catalog_ids:
            catalog_ids.append(item)
    return catalog_ids


def _brand_from_scope(search_scope: dict | None) -> str | None:
    brand = str((search_scope or {}).get("resolved_brand") or "").strip().casefold()
    return brand or None


def _to_float(value: Any) -> float | None:
    if isinstance(value, Decimal):
        return float(value)
    if isinstance(value, (int, float)):
        return float(value)
    return None


def _threshold_payload(row: dict[str, Any], source: str) -> dict[str, float | str]:
    payload: dict[str, float | str] = {"source": source}
    for column_name, key in (
        ("HighConfidence", "high_confidence"),
        ("LowConfidence", "low_confidence"),
        ("AmbiguityScoreDelta", "ambiguity_score_delta"),
    ):
        value = _to_float(row.get(column_name))
        if value is not None:
            payload[key] = value
    return payload


async def load_policy_threshold_overrides(search_scope: dict | None) -> dict | None:
    catalog_ids = _catalog_ids_from_scope(search_scope)
    brand_key = _brand_from_scope(search_scope)

    conn, from_pool = await vector_db._get_conn()
    if not conn:
        return None

    try:
        db_rows = await conn.fetch(
            """
            SELECT
                "ScopeType",
                "ScopeKey",
                "HighConfidence",
                "LowConfidence",
                "AmbiguityScoreDelta",
                "Version",
                "UpdatedDate",
                "CreatedDate"
            FROM "PolicyThresholds"
            WHERE "IsActive" = TRUE
              AND (
                    "ScopeType" = 'Global'
                    OR ("ScopeType" = 'Brand' AND lower("ScopeKey") = $1)
                    OR ("ScopeType" = 'Catalog' AND "ScopeKey" = ANY($2::text[]))
              )
            ORDER BY
                CASE "ScopeType"
                    WHEN 'Global' THEN 1
                    WHEN 'Brand' THEN 2
                    WHEN 'Catalog' THEN 3
                    ELSE 0
                END,
                "Version" ASC,
                "UpdatedDate" ASC NULLS FIRST,
                "CreatedDate" ASC
            """,
            brand_key,
            catalog_ids,
        )
    except asyncpg.UndefinedTableError:
        logger.warning("PolicyThresholds tablosu bulunamadı; default confidence policy kullanılacak.")
        return None
    except Exception as exc:
        logger.warning("PolicyThresholds okunamadı; default confidence policy kullanılacak: {}", exc)
        return None
    finally:
        await vector_db._release_conn(conn, from_pool)

    if not db_rows:
        return None

    rows = [dict(row) for row in db_rows]
    overrides = {"global": {}, "brands": {}, "catalogs": {}}
    for row in rows:
        scope_type = str(row.get("ScopeType") or "")
        scope_key = str(row.get("ScopeKey") or "").strip()
        if scope_type == "Global":
            overrides["global"] = _threshold_payload(row, "db:global")
        elif scope_type == "Brand" and scope_key:
            brand_scope_key = scope_key.casefold()
            overrides["brands"][brand_scope_key] = _threshold_payload(row, f"db:brand:{brand_scope_key}")
        elif scope_type == "Catalog" and scope_key:
            overrides["catalogs"][scope_key] = _threshold_payload(row, f"db:catalog:{scope_key}")

    return overrides
