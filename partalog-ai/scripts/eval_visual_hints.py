"""
Evaluate structured visual-hint retrieval from saved visual feedback samples.

Usage:
  cd partalog-ai
  DB_CONNECTION_STRING="postgresql://..." python scripts/eval_visual_hints.py

The script reads static/user-generated-parts/index.jsonl, loads each sibling
*.analysis.json file, runs search_by_visual_hints(), and reports Hit@k when the
feedback record has a part_code.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from loguru import logger

from services.vector_db import close_db_pool, init_db_pool, search_by_visual_hints


DEFAULT_INDEX = Path("static/user-generated-parts/index.jsonl")


def _normalize_code(value: str | None) -> str:
    return "".join(ch for ch in str(value or "").upper() if ch.isalnum())


def _load_jsonl(path: Path) -> list[dict]:
    if not path.exists():
        return []

    records: list[dict] = []
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                parsed = json.loads(line)
                if isinstance(parsed, dict):
                    records.append(parsed)
            except json.JSONDecodeError:
                continue
    return records


def _analysis_path_for(record: dict) -> Path | None:
    explicit = record.get("visual_analysis_path")
    if explicit:
        return Path(str(explicit))

    image_path = record.get("image_path")
    if not image_path:
        return None
    return Path(str(image_path)).with_suffix(".analysis.json")


def _load_visual_analysis(record: dict) -> dict | None:
    path = _analysis_path_for(record)
    if not path or not path.exists():
        return None

    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None

    visual_analysis = payload.get("visual_analysis")
    return visual_analysis if isinstance(visual_analysis, dict) else None


async def main() -> int:
    parser = argparse.ArgumentParser(description="Evaluate visual structured-hint retrieval")
    parser.add_argument("--index", type=Path, default=DEFAULT_INDEX)
    parser.add_argument("--limit", type=int, default=8)
    parser.add_argument("--catalog-id", action="append", default=[])
    parser.add_argument("--show", type=int, default=5, help="Show top N rows per evaluated sample")
    args = parser.parse_args()

    records = _load_jsonl(args.index)
    if not records:
        logger.error(f"No feedback records found: {args.index}")
        return 1

    state = await init_db_pool()
    if not state.get("ready"):
        logger.error(f"DB pool is not ready: {state}")
        return 2

    evaluated = 0
    skipped = 0
    hit1 = 0
    hit3 = 0
    hit5 = 0

    try:
        for record in records:
            expected = _normalize_code(record.get("part_code"))
            visual_analysis = _load_visual_analysis(record)
            if not expected or not visual_analysis:
                skipped += 1
                continue

            results = await search_by_visual_hints(
                visual_analysis,
                brand_filter=record.get("machine_brand"),
                machine_group_filter=record.get("machine_type"),
                catalog_ids=args.catalog_id,
                limit=args.limit,
            )
            codes = [_normalize_code(r.get("PartCode")) for r in results]

            evaluated += 1
            if codes[:1] and expected in codes[:1]:
                hit1 += 1
            if expected in codes[:3]:
                hit3 += 1
            if expected in codes[:5]:
                hit5 += 1

            logger.info(
                "[{}] expected={} hit_rank={} terms={} top={}",
                record.get("id"),
                record.get("part_code"),
                (codes.index(expected) + 1) if expected in codes else None,
                {
                    "part_family": visual_analysis.get("part_family"),
                    "shape_traits": visual_analysis.get("shape_traits") or visual_analysis.get("shape_tags"),
                    "assembly_hint": visual_analysis.get("assembly_hint"),
                },
                [r.get("PartCode") for r in results[: args.show]],
            )

    finally:
        await close_db_pool()

    def pct(count: int) -> str:
        return f"{(count / evaluated * 100):.1f}%" if evaluated else "0.0%"

    logger.info("========== Visual Hint Eval ==========")
    logger.info(f"records={len(records)} evaluated={evaluated} skipped={skipped}")
    logger.info(f"Hit@1={hit1}/{evaluated} ({pct(hit1)})")
    logger.info(f"Hit@3={hit3}/{evaluated} ({pct(hit3)})")
    logger.info(f"Hit@5={hit5}/{evaluated} ({pct(hit5)})")
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
