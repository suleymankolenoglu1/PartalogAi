import argparse
import json
import re
from pathlib import Path
from typing import Any


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    if not path.exists():
        raise FileNotFoundError(path)

    with path.open("r", encoding="utf-8") as handle:
        for line_no, line in enumerate(handle, start=1):
            raw = line.strip()
            if not raw:
                continue
            try:
                parsed = json.loads(raw)
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSONL at {path}:{line_no}: {exc}") from exc
            if isinstance(parsed, dict):
                rows.append(parsed)
    return rows


def get_value(row: dict[str, Any], *names: str) -> Any:
    for name in names:
        if name in row:
            return row[name]
        pascal = name[:1].upper() + name[1:]
        if pascal in row:
            return row[pascal]
    return None


def normalize_codes(value: Any) -> list[str]:
    if not isinstance(value, list):
        return []
    out: list[str] = []
    for item in value:
        code = str(item or "").strip().upper()
        if code and code not in out:
            out.append(code)
    return out


def normalize_csv(value: str | None) -> list[str]:
    if not value:
        return []
    return [item.strip() for item in value.split(",") if item.strip()]


def parse_context(value: Any) -> dict[str, Any] | None:
    if isinstance(value, dict):
        return value
    if isinstance(value, str) and value.strip():
        try:
            parsed = json.loads(value)
        except json.JSONDecodeError:
            return None
        return parsed if isinstance(parsed, dict) else None
    return None


def get_context_string(context: dict[str, Any], *names: str) -> str:
    for name in names:
        value = context.get(name)
        if value is None:
            continue
        text = str(value).strip()
        if text:
            return text
    return ""


def trim_for_note(value: str, limit: int = 240) -> str:
    normalized = re.sub(r"\s+", " ", value.strip())
    return normalized if len(normalized) <= limit else normalized[:limit] + "..."


def build_case_id(row: dict[str, Any], index: int, target_set: str) -> str:
    raw_id = str(get_value(row, "id") or "").strip()
    clean_id = re.sub(r"[^A-Za-z0-9]", "", raw_id)[:8] or f"{index:04d}"
    created_at = str(get_value(row, "createdAt") or "").strip()[:10].replace("-", "")
    if not created_at:
        created_at = "DRAFT"
    prefix = "FB_CTX" if target_set == "context" else "FB_BEHAVIOR"
    return f"{prefix}_{created_at}_{clean_id}".upper()


def resolve_catalog_ids(row: dict[str, Any], override_catalog_ids: list[str]) -> list[str]:
    if override_catalog_ids:
        return override_catalog_ids
    row_catalog_ids = normalize_csv(str(get_value(row, "catalogIds") or "")) if isinstance(get_value(row, "catalogIds"), str) else []
    if not row_catalog_ids:
        raw_catalog_ids = get_value(row, "catalogIds")
        if isinstance(raw_catalog_ids, list):
            row_catalog_ids = [str(x).strip() for x in raw_catalog_ids if str(x).strip()]
    return row_catalog_ids or ["<CATALOG_GUID>"]


def resolve_public_token(row: dict[str, Any], public_token: str, use_stored_token: bool) -> str:
    if public_token:
        return public_token
    if use_stored_token:
        stored = str(get_value(row, "publicToken") or "").strip()
        if stored:
            return stored
    return "<PUBLIC_TOKEN>"


def build_case(
    row: dict[str, Any],
    index: int,
    public_token: str,
    target_set: str,
    catalog_ids: list[str],
    use_stored_token: bool,
) -> dict[str, Any] | None:
    user_query = str(get_value(row, "userQuery") or "").strip()
    if not user_query:
        return None

    helpful = bool(get_value(row, "helpful"))
    source_codes = normalize_codes(get_value(row, "sourceCodes"))
    reason = str(get_value(row, "reason") or "").strip()
    reply = str(get_value(row, "replySuggestion") or "").strip()
    context = parse_context(get_value(row, "contextJson"))

    case: dict[str, Any] = {
        "id": build_case_id(row, index, target_set),
        "text": user_query,
        "public_token": resolve_public_token(row, public_token, use_stored_token),
        "catalog_ids": resolve_catalog_ids(row, catalog_ids),
    }

    feedback_id = str(get_value(row, "id") or "").strip()
    if feedback_id:
        case["feedback_id"] = feedback_id

    if target_set == "context" and context:
        case["context_json"] = context
        part_code = get_context_string(context, "partCode", "part_code")
        if part_code:
            normalized_code = part_code.upper()
            case["expected_codes"] = [normalized_code]
            case["required_terms"] = [normalized_code]
    elif helpful and source_codes:
        case["expected_codes"] = source_codes[:5]
    elif source_codes:
        case["forbidden_terms"] = source_codes[:5]
    elif not helpful:
        case["expect_no_codes"] = True

    if reason:
        case["feedback_reason"] = reason
    if reply:
        case["feedback_reply_excerpt"] = trim_for_note(reply)

    return case


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Build chat eval case drafts from backend App_Data/chat-feedback/index.jsonl"
    )
    parser.add_argument("--input", required=True, help="Path to chat-feedback index.jsonl")
    parser.add_argument("--output", required=True, help="Output JSONL path")
    parser.add_argument("--public-token", default="")
    parser.add_argument("--catalog-ids", default="", help="Comma-separated catalog IDs to force into every case")
    parser.add_argument("--target-set", choices=["behavior", "context"], default="behavior")
    parser.add_argument("--ids", default="", help="Comma-separated feedback IDs to export")
    parser.add_argument("--use-stored-token", action="store_true", help="Use stored feedback publicToken when --public-token is empty")
    parser.add_argument("--only-negative", action="store_true")
    parser.add_argument("--append", action="store_true")
    parser.add_argument("--limit", type=int, default=0)
    args = parser.parse_args()

    rows = load_jsonl(Path(args.input))
    cases: list[dict[str, Any]] = []
    selected_ids = {x.lower() for x in normalize_csv(args.ids)}
    catalog_ids = normalize_csv(args.catalog_ids)

    for row in rows:
        feedback_id = str(get_value(row, "id") or "").strip()
        if selected_ids and feedback_id.lower() not in selected_ids:
            continue

        helpful = bool(get_value(row, "helpful"))
        if args.only_negative and helpful:
            continue

        case = build_case(
            row,
            len(cases) + 1,
            args.public_token,
            args.target_set,
            catalog_ids,
            args.use_stored_token,
        )
        if case:
            cases.append(case)

        if args.limit > 0 and len(cases) >= args.limit:
            break

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    mode = "a" if args.append else "w"
    with output.open(mode, encoding="utf-8") as handle:
        for case in cases:
            handle.write(json.dumps(case, ensure_ascii=False) + "\n")

    print(f"Wrote {len(cases)} cases to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
