# Prod Chat Readiness Runbook

Bu runbook, AI/chat fazini staging veya production ortaminda acmadan once izlenecek kontrol listesidir. Bu dokuman deploy komutu calistirmaz; amaci deploy gunu sirasini, gerekli secret/env degerlerini, smoke testleri ve rollback yolunu netlestirmektir.

Catalog-only ilk faz dokumani ayri kalir:
- `deploy/google-cloud/catalog-only-cloud-run.md`

## Hedef

Chat fazi acildiginda sistemin:

- yanlis local config ile prod'a cikmamasini,
- Redis tabanli rate limit/capacity state kullanmasini,
- DataProtection key-ring'i shared ve sifreli tutmasini,
- AI servisi yavasladiginda timeout/fallback ile kontrollu davranmasini,
- loglardan request id, latency, fallback reason ve token/source sayilarini izlenebilir hale getirmesini,
- smoke testlerden gecmeden kullanici trafigine acilmamasini

saglamak.

## Ortamlar

### Staging

Production'a cok benzemeli ama gercek musteri trafigi almamali.

- Ayri API service
- Ayri AI service
- Ayri DB veya prod snapshot'inin maskeleme yapilmis kopyasi
- Ayri Redis
- Ayri Secret Manager secret'lari
- Ayri public token ve test catalog id'leri
- Feature flag'ler kontrollu acik

### Production

Sadece staging smoke ve readiness gecerse ilerlenir.

- Secret'lar Secret Manager'dan gelmeli.
- `localhost`, `127.0.0.1`, local/smoke placeholder secret kalmamali.
- Chat acilisi feature flag ile kontrollu yapilmali.
- Rollback icin onceki Cloud Run revision hazir tutulmali.

## Zorunlu Secret'lar

Secret degerlerini repoya, image icine veya terminal gecmisine acik sekilde koyma. Deger uretildikten sonra Secret Manager'a yaz.

```bash
JWT_SECRET="$(openssl rand -base64 32)"
PUBLIC_LINK_SECRET="$(openssl rand -base64 32)"
EMBED_SECRET="$(openssl rand -base64 32)"
DATA_PROTECTION_KEY_ENCRYPTION_KEY="$(openssl rand -base64 32)"
DB_PASSWORD="$(openssl rand -base64 24)"
```

Google Cloud Secret Manager ornegi:

```bash
printf "%s" "$JWT_SECRET" | gcloud secrets create katalogcu-jwt-secret --data-file=-
printf "%s" "$PUBLIC_LINK_SECRET" | gcloud secrets create katalogcu-public-link-secret --data-file=-
printf "%s" "$EMBED_SECRET" | gcloud secrets create katalogcu-embed-secret --data-file=-
printf "%s" "$DATA_PROTECTION_KEY_ENCRYPTION_KEY" | gcloud secrets create katalogcu-data-protection-key-encryption-key --data-file=-
printf "%s" "$DB_PASSWORD" | gcloud secrets create katalogcu-db-password --data-file=-
```

AI service icin:

```bash
GEMINI_API_KEY="<provider-key>"
printf "%s" "$GEMINI_API_KEY" | gcloud secrets create partalog-gemini-api-key --data-file=-
```

Var olan secret'i yeni version olarak guncellemek icin:

```bash
printf "%s" "$JWT_SECRET" | gcloud secrets versions add katalogcu-jwt-secret --data-file=-
```

## API Env Checklist

Production API service icin beklenen ana env/config degerleri:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

ConnectionStrings__DefaultConnection=<prod-db-connection>
Frontend__BaseUrl=https://<domain>
Cors__AllowedOrigins__0=https://<domain>

AiService__BaseUrl=https://<partalog-ai-service-url>
AiService__ChatTimeoutSeconds=45
AiService__StreamTimeoutSeconds=90
AiService__LongRunningTimeoutSeconds=300

AiCapacity__Provider=Redis
AiCapacity__RedisConnectionString=<redis-host>:6379,abortConnect=false
AiCapacity__DistributedPoolName=api-chat

DistributedRateLimits__RedisPublicChatEnabled=true
DistributedRateLimits__RedisConnectionString=<redis-host>:6379,abortConnect=false
DistributedRateLimits__FailOpen=false

DataProtection__Provider=Redis
DataProtection__RedisConnectionString=<redis-host>:6379,abortConnect=false
DataProtection__RedisKey=partalog:data-protection:keys

ProductFeatures__EnableChatbot=true
ProductFeatures__EnableCatalogAnalysis=false
ProductFeatures__EnableEcommerce=false
ProductFeatures__EnableUpgradePrompts=false
ProductFeatures__EnablePlanManagement=false
```

Secret Manager bindings:

```text
JwtSettings__SecretKey=katalogcu-jwt-secret:latest
PublicLink__SecretKey=katalogcu-public-link-secret:latest
EmbedAccessToken__SecretKey=katalogcu-embed-secret:latest
DataProtection__KeyEncryptionKey=katalogcu-data-protection-key-encryption-key:latest
```

## AI Service Env Checklist

Production AI service icin beklenen ana env/config degerleri:

```text
GEMINI_API_KEY=<secret-manager>
GEMINI_CHAT_MODEL=gemini-2.5-flash
GEMINI_STREAM_MODEL=gemini-2.5-flash
GEMINI_EMBEDDING_MODEL=gemini-embedding-001

AI_CHAT_CAPACITY_PROVIDER=redis
AI_CHAT_REDIS_URL=redis://<redis-host>:6379/0
AI_CHAT_DISTRIBUTED_POOL_NAME=python-chat
AI_CHAT_GLOBAL_CONCURRENCY=80
AI_CHAT_ACQUIRE_TIMEOUT_SECONDS=0.15
AI_CHAT_DISTRIBUTED_LEASE_TTL_SECONDS=180

GEMINI_CHAT_TIMEOUT_SECONDS=35
GEMINI_STREAM_TIMEOUT_SECONDS=75
GEMINI_STREAM_SOCK_READ_TIMEOUT_SECONDS=30
```

## Pre-Deploy Checks

Kod seviyesi:

```bash
dotnet build backend/Katalogcu.API/Katalogcu.API.csproj
dotnet test backend/Katalogcu.API.Tests/Katalogcu.API.Tests.csproj
python partalog-ai/tests/test_stream_contract.py
bash -n backend/scripts/smoke_chat_prod_readiness.sh
```

Migration/model:

```bash
cd backend/Katalogcu.API
dotnet ef migrations list
```

Beklenti:

- pending migration yok veya deploy planinda acikca var
- EF model snapshot drift yok
- DB backup/snapshot alindi
- rollback edilecek onceki image/revision biliniyor

## Staging Smoke

Staging ortam degiskenleri:

```bash
export API_BASE_URL="https://<staging-api-url>"
export AI_BASE_URL="https://<staging-ai-url>"
export PARTALOG_PUBLIC_TOKEN="<staging-public-token>"
export PARTALOG_ADMIN_TOKEN="<staging-admin-jwt>"
export PARTALOG_CATALOG_IDS='["<catalog-guid>"]'
```

Smoke:

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_BASE_URL" \
  --ai-base-url "$AI_BASE_URL" \
  --public-token "$PARTALOG_PUBLIC_TOKEN" \
  --admin-bearer-token "$PARTALOG_ADMIN_TOKEN" \
  --catalog-ids "$PARTALOG_CATALOG_IDS" \
  --rate-limit-check
```

Ek manuel kontroller:

```bash
curl -fsS "$API_BASE_URL/health/live"
curl -fsS "$API_BASE_URL/health/ready"
curl -fsS "$API_BASE_URL/health/migrations"
curl -fsS "$AI_BASE_URL/health/ready"
curl -fsS -H "Authorization: Bearer $PARTALOG_ADMIN_TOKEN" \
  "$API_BASE_URL/api/system/production-readiness"
```

Beklenti:

- `production-readiness.status` `ready` veya bilincli kabul edilmis `ready_with_warnings`
- `ai_capacity` ready
- `ai_service` ready
- `data_protection_key_ring` pass
- `production_required_secrets` pass
- `ai_service_endpoint_config` pass
- chat response 200 ve `replySuggestion` iceriyor
- rate limit smoke en az bir 429 yakaliyor

## Production Go/No-Go

Production'a gecmeden once asagidaki maddeler tamamlanmali:

- Staging smoke yesil.
- Admin JWT, public token ve catalog id degerleri hazir.
- Secret Manager degerleri placeholder/local degil.
- API `AiService__BaseUrl` production AI service URL'ini hedefliyor.
- API ve AI ayni Redis veya beklenen Redis cluster'i kullaniyor.
- `DistributedRateLimits__FailOpen=false`.
- DB backup/snapshot alindi.
- Onceki Cloud Run revision rollback icin duruyor.
- Feature flag acilisi planli: once staging, sonra production.
- Log/monitor ekraninda `Chat ask completed`, `Chat stream completed`, `Chat stream proxy completed`, `chat_stream_completed` loglari aranabiliyor.

No-go nedenleri:

- `/health/ready` 200 degil.
- `/health/migrations` 200 degil.
- `/api/system/production-readiness` `blocked`.
- AI `/health/ready` ready degil.
- `AiService__BaseUrl` localhost/127.0.0.1.
- DataProtection key-ring veya encryption key fail.
- Redis rate limit/capacity dependency fail.
- Real chat smoke 200 donmuyor.

## Production Smoke

Production deploy veya feature flag acilisindan hemen sonra:

```bash
export API_BASE_URL="https://<prod-api-url>"
export AI_BASE_URL="https://<prod-ai-url>"
export PARTALOG_PUBLIC_TOKEN="<prod-public-token>"
export PARTALOG_ADMIN_TOKEN="<prod-admin-jwt>"
export PARTALOG_CATALOG_IDS='["<catalog-guid>"]'
```

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_BASE_URL" \
  --ai-base-url "$AI_BASE_URL" \
  --public-token "$PARTALOG_PUBLIC_TOKEN" \
  --admin-bearer-token "$PARTALOG_ADMIN_TOKEN" \
  --catalog-ids "$PARTALOG_CATALOG_IDS"
```

Rate-limit check'i production'da sadece dusuk trafikli pencerede calistir:

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_BASE_URL" \
  --ai-base-url "$AI_BASE_URL" \
  --public-token "$PARTALOG_PUBLIC_TOKEN" \
  --admin-bearer-token "$PARTALOG_ADMIN_TOKEN" \
  --catalog-ids "$PARTALOG_CATALOG_IDS" \
  --rate-limit-check
```

## Monitoring

Canli acilistan sonraki ilk 30-60 dakika su sinyaller izlenmeli:

- `Chat ask completed`
- `Chat stream completed`
- `Chat stream proxy completed`
- `chat_completed`
- `chat_stream_completed`
- fallback reason dagilimi: `ai_timeout`, `ai_upstream_error`, `ai_capacity_limited`, `zero_tokens`
- API 5xx orani
- 429 orani
- p95/p99 chat latency
- Redis connection error
- DB timeout/pool exhaustion
- Gemini non-200 veya timeout loglari

Alarm esikleri ilk gun icin elle izlenebilir; daha sonra dashboard/alert haline getirilmeli.

## Rollback

Chat davranisi bozulursa en hizli azaltma:

```text
ProductFeatures__EnableChatbot=false
```

Bu, chat'i kapatir ama catalog-only akislarin calismaya devam etmesini hedefler.

API revision rollback:

```bash
gcloud run revisions list --service <api-service> --region <region>
gcloud run services update-traffic <api-service> \
  --region <region> \
  --to-revisions <previous-revision>=100
```

AI service rollback:

```bash
gcloud run revisions list --service <ai-service> --region <region>
gcloud run services update-traffic <ai-service> \
  --region <region> \
  --to-revisions <previous-revision>=100
```

Rollback sonrasi tekrar:

```bash
curl -fsS "$API_BASE_URL/health/ready"
curl -fsS "$API_BASE_URL/health/migrations"
curl -fsS "$AI_BASE_URL/health/ready"
```

## Kayit Altina Alinacaklar

Deploy/staging smoke sonunda su bilgiler release notuna eklenmeli:

- API image tag
- AI image tag
- API revision
- AI revision
- DB migration durumu
- `/api/system/production-readiness` sonucu
- smoke komutu ve sonucu
- rollback revision
- bilinen warning'ler ve kabul nedeni
