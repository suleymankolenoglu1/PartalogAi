# Modular Chat Vector DB Contract Remediation — 2026-06-22

## Sonuç

- Status: `passed`
- Kapsam: `services.chat_retrieval` ↔ `services.vector_db` API drift'i
- Ana etki: modular chat behavior suite import/runtime blokajı kapandı.
- Lokal doğrulama:
  - Python compile: `passed`
  - Vector lifecycle + modular chat behavior: `84/84 passed`
  - Stream contract + fast path + eval metrics: `25/25 passed`
  - Chat discovery paketi: `109/109 passed`

## Kök Neden

Staging çalışmaları sırasında `services.vector_db` daha küçük Cloud SQL uyumlu
bir sürüme sadeleşmişti. Buna karşılık modular chat katmanı hâlâ daha zengin
retrieval contract'ını bekliyordu:

- `hybrid_search_vector_db`
- `text_term_search`
- `search_by_visual_hints`
- `find_catalogs_by_machine`
- DB pool state/health helpers

Bu yüzden deployed `api.chat` yolu staging entegrasyon kapılarından geçse de
modular chat testleri import aşamasında bloklanıyordu. Production canary öncesi
bu iki katmanın aynı contract'a dönmesi gerekiyordu.

## Düzeltmeler

- `vector_db` contract'ı tekrar genişletildi:
  - exact part lookup
  - Turkish-tolerant lexical FTS query builder
  - capped lexical candidate lane
  - vector + lexical hybrid search
  - visual hint search
  - machine/catalog lookup helpers
  - pool state and health reporting
- Geri getirilen sorgu katmanı mevcut Google Cloud bağlantı modeline uyarlandı:
  - raw DSN yerine `settings.db_connect_kwargs`
  - Cloud SQL socket/path değerlerini asyncpg URI parsing'e bırakmayan kwargs akışı
  - pool ve ephemeral fallback bağlantılarında aynı connect contract
- Config tarafında runtime tuning alanları eklendi:
  - `DB_POOL_MIN_SIZE`
  - `DB_POOL_MAX_SIZE`
  - `DB_POOL_COMMAND_TIMEOUT_SECONDS`
  - `DB_POOL_HEALTHCHECK_TIMEOUT_SECONDS`
  - `DB_POOL_MAX_INACTIVE_CONNECTION_LIFETIME_SECONDS`
  - `DB_ALLOW_EPHEMERAL_FALLBACK`
- Dev/test ergonomisi için `DEV_AI_QUOTA_BYPASS` eklendi:
  - default `False`
  - yalnızca `settings.DEBUG` açıkken plan limit gate'ini bypass eder
  - production davranışı `DEBUG=False` iken değişmez

## Doğrulama

Çalıştırılan lokal kapılar:

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m py_compile \
  partalog-ai/services/vector_db.py \
  partalog-ai/config.py \
  partalog-ai/services/chat_retrieval.py \
  partalog-ai/services/chat_context.py
```

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m unittest \
  partalog-ai/tests/test_vector_db_lifecycle.py \
  partalog-ai/tests/test_chat_behavior.py
```

Sonuç: `84/84 passed`

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m unittest \
  partalog-ai/tests/test_stream_contract.py \
  partalog-ai/tests/test_chat_runtime_fast_path.py \
  partalog-ai/tests/test_chat_eval_metrics.py
```

Sonuç: `25/25 passed`

```bash
PYTHONPATH=partalog-ai partalog-ai/venv/bin/python -m unittest discover \
  -s partalog-ai/tests -p 'test_chat*.py'
```

Sonuç: `109/109 passed`

## Release Notu

Bu düzeltme lokal contract ve regression seviyesinde tamamlandı. Production
canary öncesi önerilen sıradaki kapı:

1. staging semantic smoke'u canlı Cloud SQL verisi üstünde tekrar çalıştırmak,
2. production notification channel'ı bağlamak,
3. canary/rollback tatbikatına geçmek.

Bu adımlardan özellikle semantic smoke, geri getirilen hybrid/lexical/vector DB
contract'ının staging verisinde de beklendiği gibi davrandığını gösterecek.
