from __future__ import annotations

import json
from decimal import Decimal
from typing import Any

STREAM_SCHEMA_VERSION = 1
STREAM_COMPLETION_STATUS = "completed"


def build_stream_fallback(*, used: bool = False, reason: str | None = None) -> dict[str, Any]:
    return {
        "used": used,
        "reason": reason,
    }


def build_sources_event(
    sources: list[Any] | None,
    *,
    debug_intent: Any | None = None,
    search_trace: Any | None = None,
    fallback_used: bool = False,
    fallback_reason: str | None = None,
) -> dict[str, Any]:
    event: dict[str, Any] = {
        "schemaVersion": STREAM_SCHEMA_VERSION,
        "type": "sources",
        "sources": sources or [],
        "fallback": build_stream_fallback(used=fallback_used, reason=fallback_reason),
    }
    if debug_intent is not None:
        event["debugIntent"] = debug_intent
    if search_trace is not None:
        event["searchTrace"] = search_trace
    return event


def build_token_event(
    token: str,
    *,
    fallback_used: bool = False,
    fallback_reason: str | None = None,
) -> dict[str, Any]:
    return {
        "schemaVersion": STREAM_SCHEMA_VERSION,
        "type": "token",
        "token": token,
        "fallback": build_stream_fallback(used=fallback_used, reason=fallback_reason),
    }


def build_done_event(
    *,
    fallback_used: bool = False,
    fallback_reason: str | None = None,
    status: str = STREAM_COMPLETION_STATUS,
) -> dict[str, Any]:
    return {
        "schemaVersion": STREAM_SCHEMA_VERSION,
        "type": "done",
        "completion": {"status": status},
        "fallback": build_stream_fallback(used=fallback_used, reason=fallback_reason),
    }


def validate_stream_event(event: dict[str, Any]) -> None:
    if event.get("schemaVersion") != STREAM_SCHEMA_VERSION:
        raise ValueError("Invalid stream schemaVersion.")

    event_type = event.get("type")
    if event_type not in {"sources", "token", "done"}:
        raise ValueError("Invalid stream type.")

    fallback = event.get("fallback")
    if not isinstance(fallback, dict) or not isinstance(fallback.get("used"), bool):
        raise ValueError("Missing fallback contract.")

    reason = fallback.get("reason")
    if reason is not None and not isinstance(reason, str):
        raise ValueError("Fallback reason must be a string or null.")

    if event_type == "sources":
        if not isinstance(event.get("sources"), list):
            raise ValueError("sources event must include an array.")
        return

    if event_type == "token":
        token = event.get("token")
        if not isinstance(token, str) or token == "":
            raise ValueError("token event must include token text.")
        return

    completion = event.get("completion")
    if not isinstance(completion, dict):
        raise ValueError("done event must include completion.")
    status = completion.get("status")
    if not isinstance(status, str) or status == "":
        raise ValueError("completion.status must be a non-empty string.")


def serialize_sse_event(event: dict[str, Any]) -> str:
    validate_stream_event(event)
    return f"data: {json.dumps(event, ensure_ascii=False, default=_json_default)}\n\n"


def _json_default(value: Any) -> Any:
    if isinstance(value, Decimal):
        return float(value)
    raise TypeError(f"Object of type {type(value).__name__} is not JSON serializable")
