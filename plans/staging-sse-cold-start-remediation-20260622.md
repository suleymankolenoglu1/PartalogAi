# Staging SSE Cold-Start Remediation — 2026-06-22

## Sonuç

- Status: `passed`
- Active AI revision: `partalog-ai-chat-staging-00010-6rh`
- Active image: `ai-staging-20260622-stream-fastpath-min1-v2`
- Cloud Build: `93b6abcc-2715-4e5e-aa45-615b24202586` — `SUCCESS`
- Image digest: `sha256:03284890b7d133867e135c64224f42c0d49d3ec659171d212d79e26b23182b65`
- AI scaling: `min=1`, `max=1`
- Exact-code SSE: `8/8`, fallback `0%`
- Exact-code SSE first-token p95: `905.6 ms`
- Exact-code SSE completion p95: `907.1 ms`
- Exact-code non-stream: `8/8`, p95 `369.7 ms`
- Semantic SSE contract smoke: `1/1`, fallback `0%`, first token `2632.1 ms`

## Kök Neden

Cloud Run request logları ilk iki SSE isteğinin API tarafında `8.94s` ve `8.72s`
sürdüğünü gösterdi. Aynı anda private AI servisinde `AUTOSCALING` nedeniyle yeni
instance başladı; application startup yaklaşık `6.3s` sonra tamamlandı. Public
API uptime check ile sıcak tutulurken private AI servisine doğrudan uptime
trafiği gelmediği için asıl cold-start kaynağı AI revision'ıydı.

Log incelemesinde ikinci bir hata bulundu: exact-code kaynaklarındaki PostgreSQL
`Decimal` değerleri legacy SSE `json.dumps` çağrısında serialize edilemiyordu.
Legacy stream hata cevabını versioned fallback contract'ı olmadan gönderdiği için
load aracı HTTP 200 ve token görüp isteği başarılı saymıştı.

## Düzeltmeler

- Tüm SSE event'leri `api.stream_contract` üzerinden üretilecek hale getirildi.
- `Decimal` kaynak değerleri güvenli biçimde JSON number'a çevriliyor.
- Sources/token/done event'leri versioned schema, completion ve fallback alanları
  taşıyor.
- Upstream/config/zero-token/unexpected fallback nedenleri görünür hale geldi.
- Açık parça kodları lokal olarak algılanıyor; intent GenAI çağrısı atlanıyor.
- Exact-code sonucu katalog kaynaklarından deterministik yanıt üretiyor; gereksiz
  Vertex generation çağrısı yapılmıyor.
- Private AI staging servisi `min-instances=1` profiline geçirildi.

## Ara Gate Bulgusu

Contract düzeltmeli fakat exact-code fast-path öncesindeki revision
`partalog-ai-chat-staging-00008-xww` ile gate doğru biçimde başarısız oldu:

- requests: `8`
- HTTP success: `8/8`
- degraded fallback: `37.5%`
- fallback reason: `upstream_non_success`
- Cloud Logging kök nedeni: Vertex `429 RESOURCE_EXHAUSTED`

Bu ara sonuç, yeni contract'ın gizli upstream bozulmalarını artık release gate'e
yansıttığını kanıtlıyor.

## Final Doğrulama

| lane | requests | success | fallback | p95 | first-token p95 |
|---|---:|---:|---:|---:|---:|
| exact-code SSE | 8 | 100% | 0% | 907.1 ms | 905.6 ms |
| exact-code non-stream | 8 | 100% | 0% | 369.7 ms | - |
| semantic SSE contract smoke | 1 | 100% | 0% | 3306.1 ms | 2632.1 ms |

Final revision log doğrulaması:

- local exact-code fast-path records: `8`
- semantic Gemini stream 200 records: `1`
- `Decimal is not JSON serializable`: `0`
- `RESOURCE_EXHAUSTED`: `0`
- API `/health/ready`: `ready`

Lokal doğrulama:

- Stream contract + fast-path + eval metric tests: `25/25 passed`
- Load/baseline helper tests: `45/45 passed`
- Relevant targeted total: `70/70 passed`
- Python compile: `passed`
- Staging bootstrap shell syntax: `passed`

The modular `test_chat_behavior.py` discovery blocker that existed at this
point was repaired in `modular-chat-vector-db-contract-remediation-20260622.md`.
The follow-up contract remediation restored the missing `services.vector_db`
hybrid/lexical/visual APIs and passed the local modular chat regression gates.

## Maliyet ve Release Kararı

Minimum instance yalnızca cold-start üreten private AI servisine uygulandı. API
ve web minimum instance değeri `0` kalıyor. Aylık proje bütçe guardrail'i zaten
aktif olduğundan bu karar maliyet alarmı altında izlenebilir.

Production chat first-token hedefi korunacaksa private AI servisinde de
`min-instances=1` kullanılmalı. Daha düşük maliyet tercih edilirse cold-path için
ayrı ve daha yüksek bir SLO açıkça kabul edilmelidir.
