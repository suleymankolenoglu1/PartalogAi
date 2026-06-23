# Google Cloud Catalog + Chat Deploy

Bu dokuman Catalog + Grounded Chat MVP icin Google Cloud deploy akisini tarif eder.

Catalog-only planindan farklari:

- `partalog-ai-chat` adinda ayri, hafif bir Cloud Run servisi deploy edilir.
- AI servisi `--no-allow-unauthenticated` ile private kalir.
- `katalogcu-api` service account'una AI servisinde yalnizca `roles/run.invoker` verilir.
- Backend, AI servisine Google imzali OIDC identity token ile gider.
- E-ticaret, plan yonetimi ve upgrade promptlari kapali kalir.

## Servisler

| Servis | Gorev | Public |
|---|---|---|
| `katalogcu-web` | Angular frontend | Evet |
| `katalogcu-api` | .NET API | Evet veya sadece web proxy arkasinda |
| `partalog-ai-chat` | Python chat/search/embedding | Hayir |
| Cloud SQL PostgreSQL | Katalog ve chat verisi | Hayir |
| Redis | Rate limit, capacity, DataProtection | Hayir |
| Cloud Storage | Katalog dosyalari/gorseller | Hayir |

## Build

```bash
PROJECT_ID="$(gcloud config get-value project)"
REGION="europe-west1"
COMMIT_SHA="$(git rev-parse --short HEAD)"
```

```bash
gcloud builds submit partalog-ai \
  --config=partalog-ai/cloudbuild.chat.yaml \
  --substitutions=_REGION="$REGION",_REPOSITORY=partalog,_IMAGE_NAME=partalog-ai-chat,_TAG="$COMMIT_SHA"
```

```bash
AI_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/partalog/partalog-ai-chat:$COMMIT_SHA"
```

## Service Accounts

```bash
gcloud iam service-accounts create katalogcu-api-run \
  --display-name="Katalogcu API Cloud Run"

gcloud iam service-accounts create partalog-ai-chat-run \
  --display-name="Partalog AI Chat Cloud Run"
```

AI servisinin Vertex AI ve veritabanina erisebilmesi icin gerekli roller ortama gore verilir:

```bash
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:partalog-ai-chat-run@$PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/aiplatform.user"
```

Storage gerekiyorsa:

```bash
gcloud storage buckets add-iam-policy-binding "gs://$ASSETS_BUCKET" \
  --member="serviceAccount:partalog-ai-chat-run@$PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/storage.objectAdmin"
```

## Private AI Chat Deploy

```bash
INSTANCE_CONNECTION_NAME="$PROJECT_ID:$REGION:katalogcu-db"
DB_CONNECTION="postgresql://katalogcu_app:$DB_PASSWORD@/KatalogcuDb?host=/cloudsql/$INSTANCE_CONNECTION_NAME"
REDIS_URL="redis://CHANGE_ME_REDIS_HOST:6379/0"
```

```bash
gcloud run deploy partalog-ai-chat \
  --image="$AI_IMAGE" \
  --region="$REGION" \
  --service-account="partalog-ai-chat-run@$PROJECT_ID.iam.gserviceaccount.com" \
  --no-allow-unauthenticated \
  --add-cloudsql-instances="$INSTANCE_CONNECTION_NAME" \
  --set-env-vars="DEBUG=false,STARTUP_SKIP_MODEL_LOADING=true,ENABLE_HOTSPOT_ENDPOINTS=false,ENABLE_CATALOG_PROCESSING_ENDPOINTS=false,GENAI_PROVIDER=vertex,GOOGLE_CLOUD_PROJECT=$PROJECT_ID,GOOGLE_CLOUD_LOCATION=global,GEMINI_CHAT_MODEL=gemini-2.5-flash-lite,GEMINI_ANALYSIS_MODEL=gemini-2.5-flash-lite,GENAI_REQUEST_TIMEOUT_SECONDS=30,GENAI_STREAM_TIMEOUT_SECONDS=90,GENAI_RETRY_ATTEMPTS=2,DB_CONNECTION_STRING=$DB_CONNECTION,AI_CHAT_CAPACITY_PROVIDER=redis,AI_CHAT_USE_DISTRIBUTED_LEASES=true,AI_CHAT_REDIS_URL=$REDIS_URL,AI_CHAT_GLOBAL_CONCURRENCY=100,AI_CHAT_ACQUIRE_TIMEOUT_SECONDS=0.5" \
  --memory=1Gi \
  --cpu=1 \
  --min-instances=1 \
  --max-instances=5
```

```bash
AI_URL="$(gcloud run services describe partalog-ai-chat --region="$REGION" --format='value(status.url)')"
```

API service account'una AI servisini cagirma yetkisi ver:

```bash
gcloud run services add-iam-policy-binding partalog-ai-chat \
  --region="$REGION" \
  --member="serviceAccount:katalogcu-api-run@$PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/run.invoker"
```

Public chat first-token SLO'su cold-start sırasında da korunacaksa private AI
servisinde en az bir instance tutulur. `min-instances=0` yalnızca daha yüksek bir
cold-path SLO açıkça kabul edildiğinde kullanılmalıdır.

## API Deploy Flag Profili

API deploy'unda AI servis URL ve identity-token ayarlarini ekle:

```text
Frontend__BaseUrl=https://$DOMAIN
Cors__AllowedOrigins__0=https://$DOMAIN
Cors__AllowedOrigins__1=https://$PANEL_DOMAIN
AiService__BaseUrl=$AI_URL
AiService__UseCloudRunIdentityToken=true
AiService__CloudRunAudience=$AI_URL
ProductFeatures__EnableChatbot=true
ProductFeatures__EnableCatalogAnalysis=true
ProductFeatures__EnableEcommerce=false
ProductFeatures__EnableUpgradePrompts=false
ProductFeatures__EnablePlanManagement=false
DistributedRateLimits__RedisPublicChatEnabled=true
DistributedRateLimits__FailOpen=false
AiCapacity__Provider=Redis
```

API Cloud Run servisinin service account'u:

```bash
gcloud run services update katalogcu-api \
  --region="$REGION" \
  --service-account="katalogcu-api-run@$PROJECT_ID.iam.gserviceaccount.com"
```

Frontend Cloud Run servisinde ayni Angular build'i iki host icin kullan:

```bash
DOMAIN="partalog.com"
PANEL_DOMAIN="panel.$DOMAIN"

gcloud run services update katalogcu-web \
  --region="$REGION" \
  --update-env-vars="PORTAL_HOST=$DOMAIN,PANEL_HOST=$PANEL_DOMAIN"
```

Iki custom domain'i de `katalogcu-web` servisine map et. Ana domain musteri portali
(`/p/:token`, katalog ve chat), panel subdomain'i ise `/login`, `/dashboard` ve
`/platform` icin kullanilir. Nginx Cloud Run template'i yanlis host/path
kombinasyonlarini 308 redirect ile kanonik domaine tasir.

## Smoke ve Eval

API ve AI URL hazir olduktan sonra:

```bash
./backend/scripts/postdeploy_portal_panel_check.sh \
  --portal-url "https://$DOMAIN" \
  --panel-url "https://$PANEL_DOMAIN" \
  --public-token "$PARTALOG_PUBLIC_TOKEN"
```

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_URL" \
  --ai-base-url "$AI_URL" \
  --public-token "$PARTALOG_PUBLIC_TOKEN" \
  --catalog-ids "$PARTALOG_CATALOG_IDS" \
  --rate-limit-check
```

Guncel relevance corpus ile:

```bash
cd partalog-ai
python eval/chat_eval.py \
  --base-url "$API_URL" \
  --cases eval/queries.relevance.jsonl \
  --timeout-seconds 30 \
  --output-json eval/report.relevance.staging.json \
  --output-md eval/report.relevance.staging.md \
  --min-success-rate 1.0 \
  --min-hit-at-1 0.80 \
  --min-hit-at-3 0.90 \
  --min-hit-at-5 0.95 \
  --min-mrr 0.85 \
  --max-latency-p95-ms 8000 \
  --max-hallucination-rate 0.05 \
  --min-no-code-pass-rate 0.9
```

## Rollback

Chat'i kapat:

```bash
gcloud run services update katalogcu-api \
  --region="$REGION" \
  --update-env-vars="ProductFeatures__EnableChatbot=false"
```

AI servisini onceki image'e al:

```bash
gcloud run services update partalog-ai-chat \
  --region="$REGION" \
  --image="$PREVIOUS_AI_IMAGE"
```

## Go / No-Go

GO icin:

- AI servisi private ve sadece API service account'u `roles/run.invoker` ile cagirabiliyor.
- `/health/ready` API ve AI tarafinda ready.
- `smoke_chat_prod_readiness.sh --rate-limit-check` basarili.
- Relevance eval thresholdlari basarili.
- Public load smoke onayli baseline'a gore gerileme gostermiyor.
- Vertex/Cloud Run maliyet alarmi aktif.

NO-GO icin:

- AI servisi anonymous erisime acik.
- Backend identity token olmadan `.run.app` AI URL'ine gidiyor.
- Redis rate limit/capacity production'da kapali veya fail-open.
- Eval corpus stale oldugu icin kalite kaniti uretilemiyor.
- Fallback/degraded oranlari esigi asiyor.
