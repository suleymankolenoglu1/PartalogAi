#!/usr/bin/env python3
"""
Lightweight CQRS architecture guard:
- Finds MediatR IRequest declarations in Application layer
- Finds IRequestHandler implementations
- Fails if any request has zero or multiple handlers
"""

from __future__ import annotations

import re
import sys
from collections import Counter
from pathlib import Path


REQUEST_DECL_RE = re.compile(
    r"\b(?:record|class)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
    re.MULTILINE,
)

REQUEST_MARKER_RE = re.compile(r":\s*IRequest\s*<", re.MULTILINE)

HANDLER_RE = re.compile(
    r"IRequestHandler\s*<\s*(?:global::)?(?:[A-Za-z_][A-Za-z0-9_]*\.)*([A-Za-z_][A-Za-z0-9_]*)\s*,",
    re.MULTILINE,
)

SKIP_DIRS = {"bin", "obj"}


def iter_cs_files(root: Path) -> list[Path]:
    files: list[Path] = []
    for path in root.rglob("*.cs"):
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        files.append(path)
    return files


def collect_requests(files: list[Path]) -> set[str]:
    requests: set[str] = set()
    for file in files:
        text = file.read_text(encoding="utf-8", errors="ignore")
        for match in REQUEST_DECL_RE.finditer(text):
            name = match.group(1)
            if name.endswith("Handler"):
                continue
            snippet = text[match.start() : match.start() + 500]
            if REQUEST_MARKER_RE.search(snippet):
                requests.add(name)
    return requests


def collect_handler_counts(files: list[Path]) -> Counter[str]:
    counts: Counter[str] = Counter()
    for file in files:
        text = file.read_text(encoding="utf-8", errors="ignore")
        for match in HANDLER_RE.finditer(text):
            counts[match.group(1)] += 1
    return counts


def main() -> int:
    repo_root = Path(__file__).resolve().parents[2]
    app_root = repo_root / "backend" / "Katalogcu.Application"

    if not app_root.exists():
        print(f"[cqrs-check] Application path not found: {app_root}", file=sys.stderr)
        return 2

    files = iter_cs_files(app_root)
    requests = collect_requests(files)
    handler_counts = collect_handler_counts(files)

    missing = sorted(r for r in requests if handler_counts[r] == 0)
    duplicates = sorted((r, c) for r, c in handler_counts.items() if r in requests and c > 1)

    print(f"[cqrs-check] IRequest count: {len(requests)}")
    print(f"[cqrs-check] IRequestHandler coverage: {sum(1 for r in requests if handler_counts[r] >= 1)}")

    if missing:
        print("\n[cqrs-check] Missing handlers:")
        for req in missing:
            print(f"  - {req}")

    if duplicates:
        print("\n[cqrs-check] Duplicate handlers:")
        for req, count in duplicates:
            print(f"  - {req}: {count}")

    if missing or duplicates:
        print("\n[cqrs-check] FAILED", file=sys.stderr)
        return 1

    print("[cqrs-check] PASSED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
