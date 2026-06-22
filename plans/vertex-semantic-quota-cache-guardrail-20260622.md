# Vertex Semantic Quota / Cache Guardrail — 2026-06-22

## Sonuç

- Status: `local_passed`
- Kapsam: `partalog-ai/services/embedding.py`
- Amaç: staging semantic eval sırasında görülen Vertex `429 RESOURCE_EXHAUSTED`
  titreşimini kod seviyesinde yumuşatmak
- Lokal doğrulama:
  - Python compile: `passed`
  - Embedding retry/cache tests: `3/3 passed`
  - Chat/vector regression paketi: `112/112 passed`

## Kök Neden

Chat generation çağrılarında retry/backoff zaten vardı; ancak semantic retrieval
için kullanılan embedding çağrısı tek denemeyle çalışıyordu. Vertex embedding
endpoint'i `429` veya geçici `5xx` döndürdüğünde semantic lane hemen boş kalıyor,
eval tarafında retry gerekiyordu.

Mevcut process-local cache de kısa ve küçüktü:

- TTL: `300s`
- max item: `200`
- eşzamanlı aynı sorgularda singleflight yoktu

Bu, tekrar eden public semantic sorgularda gereksiz upstream embedding çağrısı
üretebiliyordu.

## Düzeltmeler

- Embedding çağrılarına bounded retry eklendi:
  - retryable status: `408`, `429`, `500`, `502`, `503`, `504`
  - `GENAI_RETRY_ATTEMPTS`
  - `GENAI_RETRY_BASE_DELAY_SECONDS`
  - `GENAI_RETRY_MAX_DELAY_SECONDS`
  - `Retry-After` header desteği
- Embedding cache ayarları config'e taşındı:
  - `GENAI_EMBEDDING_CACHE_TTL_SECONDS`
  - `GENAI_EMBEDDING_CACHE_MAX_ITEMS`
- Default cache profili güçlendirildi:
  - TTL: `900s`
  - max item: `1000`
- Cache key normalize edildi:
  - whitespace sadeleştirme
  - casefold
  - Unicode combining mark temizliği
  - Türkçe `İ/i` varyantları için kararlı key
- Singleflight eklendi:
  - aynı anda gelen aynı embedding sorguları tek upstream çağrı paylaşır
- Cache state helper eklendi:
  - `get_embedding_cache_state`
  - test/ops introspection için cache size, TTL, max item, inflight bilgisi

## Testler

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m py_compile \
  partalog-ai/services/embedding.py \
  partalog-ai/config.py \
  partalog-ai/services/chat_retrieval.py \
  partalog-ai/api/chat.py
```

Sonuç: `passed`

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m unittest \
  partalog-ai/tests/test_embedding_cache_retry.py \
  partalog-ai/tests/test_chat_runtime_fast_path.py \
  partalog-ai/tests/test_chat_eval_metrics.py
```

Sonuç: `23/23 passed`

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m unittest \
  partalog-ai/tests/test_vector_db_lifecycle.py \
  partalog-ai/tests/test_chat_behavior.py \
  partalog-ai/tests/test_stream_contract.py \
  partalog-ai/tests/test_chat_runtime_fast_path.py \
  partalog-ai/tests/test_chat_eval_metrics.py \
  partalog-ai/tests/test_embedding_cache_retry.py
```

Sonuç: `112/112 passed`

## Release Notu

Bu guardrail Vertex kota artışının yerine geçmez; ancak aynı sorguların tekrar
embedding üretmesini ve anlık `429` yanıtlarının semantic lane'i hemen düşürmesini
azaltır.

Production öncesi hâlâ önerilen karar:

- Vertex embedding/generation quota artırımı istenmeli veya quota limiti net
  dokümante edilmeli.
- Staging deploy sonrası semantic gate tekrar çalıştırılmalı.
