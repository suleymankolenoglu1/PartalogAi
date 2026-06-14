from __future__ import annotations

import argparse
import json
from datetime import datetime
from pathlib import Path
from typing import Any


ASSERTION_FIELDS = (
    "expected_codes",
    "expect_no_codes",
    "required_terms",
    "forbidden_terms",
)


def load_jsonl(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        raise FileNotFoundError(f"Missing eval case file: {path}")

    cases: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            raw = line.strip()
            if not raw or raw.startswith("#"):
                continue
            try:
                payload = json.loads(raw)
            except json.JSONDecodeError as exc:
                raise ValueError(f"{path}:{line_number}: invalid JSONL: {exc}") from exc
            if not isinstance(payload, dict):
                raise ValueError(f"{path}:{line_number}: case must be a JSON object")
            payload["_line_number"] = line_number
            cases.append(payload)
    return cases


def has_assertion(case: dict[str, Any]) -> bool:
    if bool(case.get("expect_no_codes")):
        return True
    for field in ASSERTION_FIELDS:
        value = case.get(field)
        if isinstance(value, list) and len(value) > 0:
            return True
    return False


def validate_file(path: Path, *, allow_empty: bool, require_freshness: bool) -> list[str]:
    errors: list[str] = []
    try:
        cases = load_jsonl(path)
    except (FileNotFoundError, ValueError) as exc:
        return [str(exc)]

    if not cases and not allow_empty:
        errors.append(f"{path}: no executable cases found")

    seen_ids: set[str] = set()
    for case in cases:
        line = case.get("_line_number", "?")
        case_id = str(case.get("id") or "").strip()
        if not case_id:
            errors.append(f"{path}:{line}: missing id")
        elif case_id in seen_ids:
            errors.append(f"{path}:{line}: duplicate id '{case_id}'")
        else:
            seen_ids.add(case_id)

        text = str(case.get("text") or case.get("message") or "").strip()
        if not text:
            errors.append(f"{path}:{line}: missing text/message")

        if not has_assertion(case):
            errors.append(
                f"{path}:{line}: case must define expected_codes, expect_no_codes, "
                "required_terms, or forbidden_terms"
            )

        if require_freshness:
            source = str(case.get("source") or "").strip()
            verified_at = str(case.get("last_verified_at") or case.get("updated_at") or "").strip()
            if not source:
                errors.append(f"{path}:{line}: feedback case must include source")
            if not verified_at:
                errors.append(f"{path}:{line}: feedback case must include last_verified_at")
            else:
                try:
                    datetime.fromisoformat(verified_at.replace("Z", "+00:00"))
                except ValueError:
                    errors.append(f"{path}:{line}: last_verified_at must be an ISO date/time")

    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Partalog chat eval JSONL case files")
    parser.add_argument("paths", nargs="+")
    parser.add_argument("--allow-empty", action="append", default=[])
    parser.add_argument("--require-freshness", action="append", default=[])
    args = parser.parse_args()

    allow_empty = {Path(value).as_posix() for value in args.allow_empty}
    require_freshness = {Path(value).as_posix() for value in args.require_freshness}
    errors: list[str] = []

    for raw_path in args.paths:
        path = Path(raw_path)
        normalized = path.as_posix()
        errors.extend(
            validate_file(
                path,
                allow_empty=normalized in allow_empty,
                require_freshness=normalized in require_freshness,
            )
        )

    if errors:
        print("Eval case validation failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print(f"Eval case validation passed for {len(args.paths)} file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
