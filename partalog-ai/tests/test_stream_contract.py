from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from api.stream_contract import (  # noqa: E402
    STREAM_SCHEMA_VERSION,
    build_done_event,
    build_sources_event,
    build_token_event,
    serialize_sse_event,
)


class StreamContractTests(unittest.TestCase):
    def parse_sse_payload(self, event_text: str) -> dict:
        self.assertTrue(event_text.startswith("data: "))
        self.assertTrue(event_text.endswith("\n\n"))
        return json.loads(event_text[6:].strip())

    def test_token_event_uses_versioned_schema(self) -> None:
        payload = self.parse_sse_payload(serialize_sse_event(build_token_event("Merhaba ustam.")))

        self.assertEqual(payload["schemaVersion"], STREAM_SCHEMA_VERSION)
        self.assertEqual(payload["type"], "token")
        self.assertEqual(payload["token"], "Merhaba ustam.")
        self.assertEqual(payload["fallback"], {"used": False, "reason": None})

    def test_done_event_includes_completion_contract(self) -> None:
        payload = self.parse_sse_payload(serialize_sse_event(build_done_event()))

        self.assertEqual(payload["schemaVersion"], STREAM_SCHEMA_VERSION)
        self.assertEqual(payload["type"], "done")
        self.assertEqual(payload["completion"], {"status": "completed"})
        self.assertEqual(payload["fallback"], {"used": False, "reason": None})

    def test_fallback_token_event_exposes_reason(self) -> None:
        payload = self.parse_sse_payload(
            serialize_sse_event(
                build_token_event(
                    "Kaynaklardan derlenen yedek yanit.",
                    fallback_used=True,
                    fallback_reason="zero_tokens",
                )
            )
        )

        self.assertEqual(payload["type"], "token")
        self.assertEqual(payload["fallback"], {"used": True, "reason": "zero_tokens"})

    def test_sources_event_can_include_search_trace(self) -> None:
        payload = self.parse_sse_payload(
            serialize_sse_event(
                build_sources_event(
                    [],
                    search_trace={
                        "original_query": "vida kodu",
                        "rewritten_query": {"text": "vida kodu", "source": "fallback"},
                        "resolved_scope": {"catalog_id": "c1", "catalog_ids": ["c1"], "scope_source": "frontend_catalog_ids"},
                        "retrieved_candidates_count": 2,
                        "compatibility_gate_filtered_count": 1,
                        "final_decision": {"decision": "EXACT_MATCH", "candidate_scores": []},
                    },
                )
            )
        )

        self.assertEqual(payload["type"], "sources")
        self.assertEqual(payload["searchTrace"]["original_query"], "vida kodu")
        self.assertEqual(payload["searchTrace"]["final_decision"]["decision"], "EXACT_MATCH")


if __name__ == "__main__":
    unittest.main()
