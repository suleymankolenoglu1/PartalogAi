# Staging Semantic Chat Validation — 2026-06-22

## Ortam

- Project: `partalog`
- Region: `europe-west1`
- API: `https://partalog-api-staging-851093992319.europe-west1.run.app`
- AI: `https://partalog-ai-chat-staging-851093992319.europe-west1.run.app`
- Aktif API revision: `partalog-api-staging-00008-84r`
- Aktif AI revision: `partalog-ai-chat-staging-00007-brf`

## Bulgular ve Düzeltmeler

1. Vertex model erişimi doğrulandı.
   - `gemini-2.0-flash` staging projesinde/bölgelerinde erişilebilir değildi.
   - `gemini-2.5-flash-lite` Vertex üzerinden çalıştı.
   - AI staging env ve repo şablonları bu modele geçirildi.

2. Staging katalog semantic altyapısı dolduruldu.
   - Catalog item: `32`
   - Embedding: `32/32`
   - Search text: `32/32`

3. Lokal embedding/test script uyumsuzlukları giderildi.
   - `.env` dosyasının açık staging env değerlerini ezmesi engellendi.
   - `GEMINI_EMBEDDING_MODEL` config alanı eklendi.
   - Semantic search script'i DB pool init contract değişikliğine uyumlu hale getirildi.

4. Public chat semantic fallback kök nedeni bulundu.
   - Python AI servis bazı kaynaklarda `similarity: null` döndürebiliyor.
   - API DTO `double` beklediği için AI cevabı parse edilemiyor ve kullanıcı fallback cevabı görüyordu.
   - `ChatSourceDto.Similarity` `double?` yapıldı ve regression testi eklendi.

## Doğrulama

- Backend tests: `70/70` geçti.
- API health:
  - `/health/live`: `Healthy`
  - `/health/ready`: `ready`
- Ara semantic eval:
  - total: `4`
  - success: `4/4`
  - Hit@1/Hit@3/Hit@5: `75% / 75% / 75%`
  - MRR: `0.750`
  - hallucination rate: `25%`
  - quality issue cases: `1`
  - latency avg/p95: `3141.9 ms / 3979.2 ms`
- Temiz corpus semantic eval:
  - corpus: `partalog-ai/eval/queries.staging_semantic.jsonl`
  - profile: `--case-delay-seconds 15 --retry-quality-issues 2 --retry-delay-seconds 65`
  - total: `8`
  - success: `8/8`
  - Hit@1/Hit@3/Hit@5: `100% / 100% / 100%`
  - MRR: `1.000`
  - hallucination rate: `0%`
  - quality issue cases: `0`
  - latency avg/p95: `4073.5 ms / 5211.1 ms`

## Case Özeti

| Case | Sonuç | Not |
|---|---:|---|
| `staging-semantic-thread-guide` | geçti | `70003363` rank 1; yavaş profilde ilk deneme quota kaynaklı retry istedi. |
| `staging-semantic-rubber-buffer` | geçti | `13302302` rank 1 |
| `staging-semantic-spring-washer` | geçti | `WS0510002KP` rank 1 |
| `staging-semantic-opening-cover` | geçti | `70003402` rank 1 |
| `staging-semantic-front-plate` | geçti | `70003409` rank 1 |
| `staging-semantic-plate-support` | geçti | `70003404` rank 1 |
| `staging-semantic-spring-pin` | geçti | `PS0150042K0` rank 1 |
| `staging-semantic-m5-l8-screw` | geçti | `SM6050800SP` rank 1 |

## Notlar

- Embedding backfill sırasında Vertex embedding quota `429` verdi; düşük batch ve daha uzun sleep ile kalan kayıtlar başarıyla tamamlandı.
- Public semantic eval sırasında da Vertex `gemini-embedding` quota `429` kayıtları görüldü. Bu yüzden staging semantic gate yavaş profil ve kalite-issue retry ile koşulmalı.
- `backend/reports/` git ignore altında olduğu için ham eval JSON/MD artifact'leri repoya alınmadı; bu dosya kalıcı özet rapordur.
- Sıradaki release gate işi: full saturation/load + alert/maliyet budget doğrulaması.
