# Modular Chat Vector DB Contract Remediation — 2026-06-22

## Sonuç

- Status: `passed`
- Kapsam: `services.chat_retrieval` ↔ `services.vector_db` API drift'i
- Ana etki: modular chat behavior suite import/runtime blokajı kapandı.
- Staging AI deployment: `passed`
- Active staging AI revision: `partalog-ai-chat-staging-00011-9q9`
- Active staging AI image: `ai-staging-a12bd1c-vector-contract`
- Lokal doğrulama:
  - Python compile: `passed`
  - Vector lifecycle + modular chat behavior: `84/84 passed`
  - Stream contract + fast path + eval metrics: `25/25 passed`
  - Chat discovery paketi: `109/109 passed`
- Staging semantic doğrulama:
  - `8/8 passed`
  - Hit@1/Hit@3/Hit@5: `100% / 100% / 100%`
  - MRR: `1.000`
  - hallucination rate: `0%`
  - latency avg/p95: `3666.3 ms / 5572.4 ms`

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

## Staging Deployment ve Semantic Gate

AI staging servisi API/Web servislerine dokunmadan sadece image update ile
güncellendi.

- Cloud Build: `6a94ba22-332f-4b95-a008-93430f03bcea` — `SUCCESS`
- Image: `europe-west1-docker.pkg.dev/partalog/partalog/partalog-ai-chat-staging:ai-staging-a12bd1c-vector-contract`
- Image digest: `sha256:d36f8ded27fa2abc0b35d505418a951958e137f10d8b2481851e32916a798ec3`
- Cloud Run revision: `partalog-ai-chat-staging-00011-9q9`
- Traffic: `100%`
- Scaling korundu: service min/max `1/1`, revision max `1`
- Private AI `/health/ready`:
  - DB ready: `true`
  - DB mode: `configured`
  - Redis capacity ready: `true`
  - capacity mode: `redis-distributed`

Staging semantic gate gerçek public token ve gerçek Cloud SQL verisiyle tekrar
çalıştırıldı.

```bash
python eval/chat_eval.py \
  --base-url "$API_URL" \
  --cases eval/queries.staging_semantic.jsonl \
  --timeout-seconds 45 \
  --case-delay-seconds 15 \
  --retry-quality-issues 2 \
  --retry-delay-seconds 65 \
  --min-success-rate 1.0 \
  --min-hit-at-1 1.0 \
  --min-hit-at-3 1.0 \
  --min-hit-at-5 1.0 \
  --min-mrr 1.0 \
  --max-latency-p95-ms 10000 \
  --max-hallucination-rate 0.0
```

Sonuç:

- total: `8`
- success: `8/8`
- Hit@1/Hit@3/Hit@5: `100% / 100% / 100%`
- MRR: `1.000`
- required/forbidden term pass: `100% / 100%`
- hallucination rate: `0%`
- quality issue cases: `0`
- latency avg/p95: `3666.3 ms / 5572.4 ms`

Bir case ilk denemede Vertex quota kaynaklı kalite retry'ına girdi ve ikinci
denemede temiz geçti. Cloud Logging'de aynı koşuya ait tek `429
RESOURCE_EXHAUSTED` kaydı görüldü. Bu gate sonucunu bozmadı, ancak semantic
Vertex quota/caching maddesinin production öncesi hâlâ açık kalması gerektiğini
tekrar doğruladı.

## Release Notu

Bu düzeltme lokal contract/regression seviyesinde ve staging semantic gate ile
tamamlandı. Production canary öncesi önerilen sıradaki kapı:

1. production notification channel'ı bağlamak,
2. Vertex semantic quota/caching kararını netleştirmek,
3. canary/rollback tatbikatına geçmek.

Staging semantic gate, geri getirilen hybrid/lexical/vector DB contract'ının
canlı staging verisinde beklendiği gibi çalıştığını gösterdi.
