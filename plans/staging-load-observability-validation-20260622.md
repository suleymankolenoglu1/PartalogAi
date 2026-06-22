# Staging Load and Observability Validation — 2026-06-22

## Sonuç

- Public browse saturation baseline: `passed`
- Public non-stream exact-code chat load gate: `passed`
- Public SSE exact-code chat warm load gate: `passed`
- Cold-start SSE gate: `failed`, ardından sıcak tekrar ile kök neden ayrıştırıldı
- Cloud Monitoring uptime checks: `2 active`
- Cloud Monitoring alert policies: `2 active / 5 conditions`
- Cloud Billing budget: `active`

## Public Browse Saturation

Profil yalnızca okuma yapan public katalog gezinme senaryosunu kullandı. Checkout
kapalıydı; chat ve stream ise public `20 request / 60 seconds` limitini yapay
olarak tüketmemek için ayrı kapılarda ölçüldü.

| concurrency | requests | success | successful rps | p95 latency |
|---:|---:|---:|---:|---:|
| 1 | 38 | 100% | 2.53 | 557.5 ms |
| 2 | 79 | 100% | 5.15 | 570.0 ms |
| 4 | 142 | 100% | 9.26 | 926.8 ms |

- Saturation analysis status: `scaling`
- Recommended measured concurrency: `4`
- First saturation point: `not observed`
- Bottleneck scenario: `none`
- Threshold failure: `0`
- Approved bounded baseline:
  `backend/load-baselines/staging-public-browse-baseline.json`

Bu sonuç concurrency 4 üzerinde kapasite garantisi vermez; yalnızca ölçülen aralıkta
doygunluk veya regresyon görülmediğini kanıtlar.

## Public Chat Load Gates

Test aracı toplam istek sayısını tüm worker'lar arasında atomik olarak sınırlayan
`--max-requests` seçeneğini kazandı. Böylece kontrollü chat kapıları duration
boyunca sınırsız istek üretmiyor.

| lane | requests | concurrency | success | p95 | first token p95 | fallback |
|---|---:|---:|---:|---:|---:|---:|
| non-stream exact-code | 8 | 2 | 100% | 633.3 ms | - | 0% |
| SSE exact-code, cold sample | 8 | 2 | 100% | 9201.9 ms | 9187.0 ms | 0% |
| SSE exact-code, warm repeat | 8 | 2 | 100% | 1445.1 ms | 1444.0 ms | 0% |

Cold sample `%100` başarılı olsa da 5 saniye completion ve 2 saniye first-token
hedeflerini geçemedi. Aynı revision sıcak halde tekrarlandığında iki hedef de
geçti. Bu nedenle Cloud Run min-instance kullanılmayan staging profilinde
cold-start latency bilinen risk olarak korunuyor; başarısız ilk ölçüm silinmedi.

Ham JSON raporları `backend/reports/` altında git-ignore kapsamındadır.

## Google Cloud Observability

### Uptime checks

- `Partalog staging API readiness`
  - `/health/ready`, HTTP 200 ve response içinde `ready`
  - every 60 seconds
  - Europe, Iowa, Asia Pacific
- `Partalog staging web availability`
  - `/`, HTTP 200
  - every 60 seconds
  - Europe, Iowa, Asia Pacific

### Alert policies

- `Partalog staging public availability`
  - API veya web uptime iki dakika boyunca %100 altına düşerse incident
- `Partalog staging Cloud Run reliability`
  - API 5xx rate `> 0.01/s`
  - AI service 5xx rate `> 0.01/s`
  - API p95 request latency iki dakika boyunca `> 5000 ms`

Policy tanımları `deploy/google-cloud/monitoring/` altında sürüm kontrollüdür.
Henüz doğrulanmış bir Monitoring notification channel seçilmedi. Incident'lar
Cloud Monitoring'de oluşur; production go-live öncesinde ekip/on-call kanalı
bağlanmalıdır.

## Google Cloud Billing Budget

- Name: `Partalog project monthly guardrail`
- Scope: project number `851093992319`
- Currency: `TRY`
- Amount: previous calendar period
- Current spend thresholds: `50%`, `80%`, `100%`
- Forecasted spend threshold: `100%`
- Default IAM recipients remain enabled

Cloud Billing Budget API bu adımda etkinleştirildi.

## Doğrulama

- Load and baseline helper suites: `45/45 passed` after the request-cap change
- Python compile: `passed`
- Approved browse baseline validation: `passed`
- Google Cloud resource read-back: `2 uptime checks`, `2 policies`, `1 budget`

## Kalan Release Riskleri

- Production on-call notification channel seçilmeli ve doğrulanmalı.
- Cold-start SSE hedefi için production'da min-instance veya kabul edilen daha
  yüksek cold-path SLO kararı verilmeli.
- Vertex embedding quota artırımı veya cache/rate-limit stratejisi netleşmeli.
- Production rollout/canary ve rollback tatbikatı henüz yapılmadı.
