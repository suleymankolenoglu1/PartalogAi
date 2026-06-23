# Google Cloud Catalog + Chat Staging Kurulumu

Bu doküman Catalog + Grounded Chat MVP için production'dan izole staging ortamını kurar.

Amaç:

- Production kaynaklarını ezmeden gerçek Cloud Run / Cloud SQL / Redis / Vertex akışını doğrulamak.
- Chat'i private AI Cloud Run servisi üzerinden Google OIDC identity token ile çalıştırmak.
- Eval, smoke, load ve rollback provaları için production-benzeri ama küçük kapasiteli ortam yaratmak.

## 0. Ön Koşul

Yerel makinede şu an `gcloud` CLI yoksa iki yol var:

1. Google Cloud SDK kurup bu komutları lokalden çalıştır.
2. Google Cloud Console içindeki Cloud Shell'i açıp repo erişimiyle komutları oradan çalıştır.

Bu dosyadaki komutlar kaynak oluşturur ve maliyet başlatabilir. Staging için küçük kapasite seçildi ama Cloud SQL, Memorystore ve Cloud Run kullanımı ücret doğurur.

Ön koşulları hızlı kontrol:

```bash
./deploy/google-cloud/check-staging-prereqs.sh
```

Bu repo için daha önce hazırlanmış `partalog` Google Cloud project'i kullanılacaksa en pratik otomatik yol:

```bash
./deploy/google-cloud/bootstrap-existing-partalog-staging.sh
```

Bu script mevcut `partalog` Artifact Registry, bucket, service account ve secret'larını kullanır; eski `partalog-api`, `partalog-web`, `partalog-ai` servislerini ezmez. Yeni servis adları:

- `partalog-api-staging`
- `partalog-web-staging`
- `partalog-ai-chat-staging`

API ve AI staging servisleri çalışan `partalog-db-connection` Secret Manager secret'ını paylaşır. Python config, Npgsql/.NET connection string formatını asyncpg URI formatına güvenli şekilde normalize eder.

Eksikse `katalogcu-redis-staging` kaynağını oluşturur ve Cloud Run servislerini Direct VPC egress ile bağlar. Mevcut `katalogcu-db` instance'ı STOPPED ise başlatır.

Not: 2026-06-21 tarihinde Serverless VPC Access connector iki farklı CIDR ile iki kez Google internal error verdi. Sorun connector kullanmadan Cloud Run Direct VPC egress ile çözüldü. Bootstrap artık varsayılan olarak Redis'i açar:

```bash
./deploy/google-cloud/bootstrap-existing-partalog-staging.sh
```

Yalnızca Redis'siz acil fallback gerekirse `USE_REDIS=false` kullanılabilir; bu mod distributed public chat rate-limit'i devre dışı bırakır ve production-benzeri kabul edilmez.

## 1. Staging Değişkenleri

```bash
PROJECT_ID="$(gcloud config get-value project)"
REGION="europe-west1"
REPOSITORY="partalog"
TAG="$(git rev-parse --short HEAD)-staging"

API_SERVICE="katalogcu-api-staging"
WEB_SERVICE="katalogcu-web-staging"
AI_SERVICE="partalog-ai-chat-staging"
SQL_INSTANCE="katalogcu-db-staging"
DB_NAME="KatalogcuDb"
DB_USER="katalogcu_app"
REDIS_INSTANCE="katalogcu-redis-staging"
NETWORK="default"
SUBNET="default"
ASSETS_BUCKET="$PROJECT_ID-katalogcu-staging-assets"
STAGING_PORTAL_HOST="staging.example.com"
STAGING_PANEL_HOST="panel.staging.example.com"

API_SA="katalogcu-api-staging-run@$PROJECT_ID.iam.gserviceaccount.com"
AI_SA="partalog-ai-chat-staging-run@$PROJECT_ID.iam.gserviceaccount.com"
```

Gerçek secret değerleri shell history'ye düşürmemek için mümkünse Cloud Shell secret prompt veya geçici dosya kullan.

```bash
DB_PASSWORD="CHANGE_ME"
JWT_SECRET="CHANGE_ME_MIN_32_CHARS"
PUBLIC_LINK_SECRET="CHANGE_ME_MIN_32_CHARS"
DATA_PROTECTION_KEY_ENCRYPTION_KEY="CHANGE_ME_BASE64_32_BYTE_KEY"
```

## 2. API'leri Aç

```bash
gcloud services enable \
  run.googleapis.com \
  sqladmin.googleapis.com \
  artifactregistry.googleapis.com \
  secretmanager.googleapis.com \
  cloudbuild.googleapis.com \
  storage.googleapis.com \
  redis.googleapis.com \
  vpcaccess.googleapis.com \
  aiplatform.googleapis.com
```

## 3. Artifact Registry

Eğer repo yoksa oluştur:

```bash
gcloud artifacts repositories describe "$REPOSITORY" \
  --location="$REGION" >/dev/null 2>&1 \
  || gcloud artifacts repositories create "$REPOSITORY" \
    --repository-format=docker \
    --location="$REGION"
```

## 4. Service Account'lar

```bash
gcloud iam service-accounts create katalogcu-api-staging-run \
  --display-name="Katalogcu API Staging Cloud Run" || true

gcloud iam service-accounts create partalog-ai-chat-staging-run \
  --display-name="Partalog AI Chat Staging Cloud Run" || true
```

AI servisinin Vertex kullanabilmesi için:

```bash
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:$AI_SA" \
  --role="roles/aiplatform.user"
```

API ve AI servislerinin Cloud SQL'e bağlanması için:

```bash
gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:$API_SA" \
  --role="roles/cloudsql.client"

gcloud projects add-iam-policy-binding "$PROJECT_ID" \
  --member="serviceAccount:$AI_SA" \
  --role="roles/cloudsql.client"
```

## 5. Cloud SQL PostgreSQL

Staging için küçük instance yeterli. İlk deploy sırasında API mevcut EF migration'ları otomatik uygular.

```bash
gcloud sql instances create "$SQL_INSTANCE" \
  --database-version=POSTGRES_16 \
  --region="$REGION" \
  --tier=db-custom-1-3840 \
  --storage-size=20GB \
  --storage-type=SSD \
  --backup-start-time=03:30
```

```bash
gcloud sql databases create "$DB_NAME" --instance="$SQL_INSTANCE"
gcloud sql users create "$DB_USER" --instance="$SQL_INSTANCE" --password="$DB_PASSWORD"
```

## 6. Cloud Storage

```bash
gcloud storage buckets create "gs://$ASSETS_BUCKET" \
  --location="$REGION" \
  --uniform-bucket-level-access
```

```bash
gcloud storage buckets add-iam-policy-binding "gs://$ASSETS_BUCKET" \
  --member="serviceAccount:$API_SA" \
  --role="roles/storage.objectAdmin"

gcloud storage buckets add-iam-policy-binding "gs://$ASSETS_BUCKET" \
  --member="serviceAccount:$AI_SA" \
  --role="roles/storage.objectAdmin"
```

Bucket private kalmalı; public-read vermiyoruz.

## 7. Redis / Memorystore + Direct VPC Egress

Cloud Run'ın Memorystore private IP'ye erişmesi için connector yerine Direct VPC egress kullanılır. Servis deploy komutları `--network=default --subnet=default --vpc-egress=private-ranges-only` taşır.

```bash
gcloud redis instances create "$REDIS_INSTANCE" \
  --region="$REGION" \
  --tier=basic \
  --size=1 \
  --redis-version=redis_7_0 \
  --network=default
```

```bash
REDIS_HOST="$(gcloud redis instances describe "$REDIS_INSTANCE" \
  --region="$REGION" \
  --format='value(host)')"
```

## 8. Secret Manager

```bash
INSTANCE_CONNECTION_NAME="$PROJECT_ID:$REGION:$SQL_INSTANCE"
API_DB_CONNECTION="Host=/cloudsql/$INSTANCE_CONNECTION_NAME;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"
AI_DB_CONNECTION="postgresql://$DB_USER:$DB_PASSWORD@/$DB_NAME?host=/cloudsql/$INSTANCE_CONNECTION_NAME"
```

```bash
printf "%s" "$API_DB_CONNECTION" | gcloud secrets create staging-katalogcu-api-db-connection --data-file=-
printf "%s" "$AI_DB_CONNECTION" | gcloud secrets create staging-partalog-ai-chat-db-connection --data-file=-
printf "%s" "$JWT_SECRET" | gcloud secrets create staging-katalogcu-jwt-secret --data-file=-
printf "%s" "$PUBLIC_LINK_SECRET" | gcloud secrets create staging-katalogcu-public-link-secret --data-file=-
printf "%s" "$DATA_PROTECTION_KEY_ENCRYPTION_KEY" | gcloud secrets create staging-katalogcu-data-protection-key-encryption-key --data-file=-
```

Cloud Run service account'larına secret okuma izni:

```bash
for SECRET in \
  staging-katalogcu-api-db-connection \
  staging-katalogcu-jwt-secret \
  staging-katalogcu-public-link-secret \
  staging-katalogcu-data-protection-key-encryption-key; do
  gcloud secrets add-iam-policy-binding "$SECRET" \
    --member="serviceAccount:$API_SA" \
    --role="roles/secretmanager.secretAccessor"
done

gcloud secrets add-iam-policy-binding staging-partalog-ai-chat-db-connection \
  --member="serviceAccount:$AI_SA" \
  --role="roles/secretmanager.secretAccessor"
```

## 9. Image Build

```bash
gcloud builds submit backend \
  --config=backend/cloudbuild.api.yaml \
  --substitutions=_REGION="$REGION",_REPOSITORY="$REPOSITORY",_IMAGE_NAME=api-staging,_TAG="$TAG"
```

```bash
gcloud builds submit frontend/katalogcu-frontend \
  --config=frontend/katalogcu-frontend/cloudbuild.web.yaml \
  --substitutions=_REGION="$REGION",_REPOSITORY="$REPOSITORY",_IMAGE_NAME=web-staging,_TAG="$TAG"
```

```bash
gcloud builds submit partalog-ai \
  --config=partalog-ai/cloudbuild.chat.yaml \
  --substitutions=_REGION="$REGION",_REPOSITORY="$REPOSITORY",_IMAGE_NAME=partalog-ai-chat-staging,_TAG="$TAG"
```

```bash
API_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/$REPOSITORY/api-staging:$TAG"
WEB_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/$REPOSITORY/web-staging:$TAG"
AI_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/$REPOSITORY/partalog-ai-chat-staging:$TAG"
```

## 10. Private AI Chat Deploy

```bash
gcloud run deploy "$AI_SERVICE" \
  --image="$AI_IMAGE" \
  --region="$REGION" \
  --service-account="$AI_SA" \
  --no-allow-unauthenticated \
  --add-cloudsql-instances="$INSTANCE_CONNECTION_NAME" \
  --network="$NETWORK" \
  --subnet="$SUBNET" \
  --vpc-egress=private-ranges-only \
  --set-env-vars="DEBUG=false,STARTUP_SKIP_MODEL_LOADING=true,ENABLE_HOTSPOT_ENDPOINTS=false,ENABLE_CATALOG_PROCESSING_ENDPOINTS=false,GENAI_PROVIDER=vertex,GOOGLE_CLOUD_PROJECT=$PROJECT_ID,GOOGLE_CLOUD_LOCATION=global,GEMINI_CHAT_MODEL=gemini-2.5-flash-lite,GEMINI_ANALYSIS_MODEL=gemini-2.5-flash-lite,GENAI_REQUEST_TIMEOUT_SECONDS=30,GENAI_STREAM_TIMEOUT_SECONDS=90,GENAI_RETRY_ATTEMPTS=2,AI_CHAT_CAPACITY_PROVIDER=redis,AI_CHAT_USE_DISTRIBUTED_LEASES=true,AI_CHAT_REDIS_URL=redis://$REDIS_HOST:6379/0,AI_CHAT_REDIS_KEY_PREFIX=partalog:staging:ai-capacity,AI_CHAT_GLOBAL_CONCURRENCY=25,AI_CHAT_ACQUIRE_TIMEOUT_SECONDS=0.5,AI_CHAT_DISTRIBUTED_POOL_NAME=python-chat-staging,STORAGE_BUCKET=$ASSETS_BUCKET,STORAGE_BASE_URL=https://storage.googleapis.com/$ASSETS_BUCKET" \
  --set-secrets="DB_CONNECTION_STRING=staging-partalog-ai-chat-db-connection:latest" \
  --memory=1Gi \
  --cpu=1 \
  --min-instances=1 \
  --max-instances=3
```

```bash
AI_URL="$(gcloud run services describe "$AI_SERVICE" --region="$REGION" --format='value(status.url)')"
```

AI servisinin sadece API service account tarafından çağrılması:

```bash
gcloud run services add-iam-policy-binding "$AI_SERVICE" \
  --region="$REGION" \
  --member="serviceAccount:$API_SA" \
  --role="roles/run.invoker"
```

Staging chat release gate'i first-token p95 `< 2000 ms` hedeflediği için private
AI servisinde bir minimum instance tutulur. API ve web servisleri public uptime
check trafiğiyle zaten periyodik olarak uyandırılır; bu nedenle onların minimum
instance değeri `0` kalır.

## 11. API Deploy

İlk deploy'da `Frontend__BaseUrl` geçici Cloud Run web URL'i henüz bilinmediği için API URL'iyle başlatılabilir; web deploy sonrası güncellenir.
Custom staging domainleri kullanılıyorsa `STAGING_PORTAL_HOST` ve `STAGING_PANEL_HOST` değerlerini gerçek hostlara çek.

```bash
TEMP_FRONTEND_ORIGIN="https://placeholder-staging.local"
REDIS_CONNECTION="$REDIS_HOST:6379,abortConnect=false"
```

```bash
gcloud run deploy "$API_SERVICE" \
  --image="$API_IMAGE" \
  --region="$REGION" \
  --allow-unauthenticated \
  --service-account="$API_SA" \
  --add-cloudsql-instances="$INSTANCE_CONNECTION_NAME" \
  --network="$NETWORK" \
  --subnet="$SUBNET" \
  --vpc-egress=private-ranges-only \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,ASPNETCORE_URLS=http://+:8080,Frontend__BaseUrl=$TEMP_FRONTEND_ORIGIN,Cors__AllowedOrigins__0=$TEMP_FRONTEND_ORIGIN,Cors__AllowedOrigins__1=https://$STAGING_PANEL_HOST,RequestLimits__DefaultMaxBodySizeMb=50,AiService__BaseUrl=$AI_URL,AiService__UseCloudRunIdentityToken=true,AiService__CloudRunAudience=$AI_URL,AiService__ChatTimeoutSeconds=45,AiService__StreamTimeoutSeconds=90,AiService__LongRunningTimeoutSeconds=300,AiService__EnableItemEmbeddings=true,AiService__EmbeddingTimeoutSeconds=20,ProductFeatures__EnableChatbot=true,ProductFeatures__EnableCatalogAnalysis=true,ProductFeatures__EnableEcommerce=false,ProductFeatures__EnableUpgradePrompts=false,ProductFeatures__EnablePlanManagement=false,DistributedRateLimits__RedisPublicChatEnabled=true,DistributedRateLimits__RedisConnectionString=$REDIS_CONNECTION,DistributedRateLimits__RedisKeyPrefix=partalog:staging:rate-limit,DistributedRateLimits__PublicChatPermitLimit=20,DistributedRateLimits__PublicChatWindowSeconds=60,DistributedRateLimits__FailOpen=false,AiCapacity__Provider=Redis,AiCapacity__GlobalConcurrentChats=25,AiCapacity__PerUserConcurrentChats=3,AiCapacity__AcquireTimeoutMs=500,AiCapacity__RedisConnectionString=$REDIS_CONNECTION,AiCapacity__RedisKeyPrefix=partalog:staging:ai-capacity,AiCapacity__DistributedPoolName=api-chat-staging,DataProtection__Provider=Redis,DataProtection__RedisConnectionString=$REDIS_CONNECTION,DataProtection__RedisKey=partalog:staging:data-protection:keys,FileStorage__Provider=GoogleCloudStorage,FileStorage__BucketName=$ASSETS_BUCKET,FileStorage__PublicBaseUrl=https://storage.googleapis.com/$ASSETS_BUCKET,CatalogAiProcessing__MaxAttempts=4,CatalogAiProcessing__BaseRetryDelaySeconds=15,CatalogAiProcessing__HangfireWorkerCount=1,BackgroundProcessing__EnableCatalogAiServer=true,BackgroundProcessing__EnableExternalSiteCrawlServer=false,BackgroundProcessing__EnableDefaultServer=false,BackgroundProcessing__EnableRecurringJobs=false" \
  --set-secrets="ConnectionStrings__DefaultConnection=staging-katalogcu-api-db-connection:latest,JwtSettings__SecretKey=staging-katalogcu-jwt-secret:latest,PublicLink__SecretKey=staging-katalogcu-public-link-secret:latest,DataProtection__KeyEncryptionKey=staging-katalogcu-data-protection-key-encryption-key:latest" \
  --memory=1Gi \
  --cpu=1 \
  --min-instances=0 \
  --max-instances=3
```

```bash
API_URL="$(gcloud run services describe "$API_SERVICE" --region="$REGION" --format='value(status.url)')"
```

## 12. Web Deploy ve API CORS Güncellemesi

```bash
gcloud run deploy "$WEB_SERVICE" \
  --image="$WEB_IMAGE" \
  --region="$REGION" \
  --allow-unauthenticated \
  --set-env-vars="API_PROXY_URL=$API_URL" \
  --memory=512Mi \
  --cpu=1 \
  --min-instances=0 \
  --max-instances=3
```

```bash
WEB_URL="$(gcloud run services describe "$WEB_SERVICE" --region="$REGION" --format='value(status.url)')"
```

API CORS ve frontend base URL'i staging web URL'ine çek:

```bash
gcloud run services update "$API_SERVICE" \
  --region="$REGION" \
  --update-env-vars="Frontend__BaseUrl=$WEB_URL,Cors__AllowedOrigins__0=$WEB_URL,Cors__AllowedOrigins__1=https://$STAGING_PANEL_HOST"
```

Custom staging domainleri map edildikten sonra web host yönlendirmelerini de güncelle:

```bash
gcloud run services update "$WEB_SERVICE" \
  --region="$REGION" \
  --update-env-vars="PORTAL_HOST=$STAGING_PORTAL_HOST,PANEL_HOST=$STAGING_PANEL_HOST"
```

## 13. Smoke

```bash
curl -fsS "$API_URL/health/live"
curl -fsS "$API_URL/health/ready"
curl -fsS "$API_URL/health/migrations"
curl -fsS "$WEB_URL/api/system/features"
```

Private AI URL doğrudan anonymous çağrıda 401/403 dönmelidir:

```bash
curl -sS -o /dev/null -w "%{http_code}\n" "$AI_URL/health/ready"
```

Private AI health smoke için user identity token:

```bash
AI_IDENTITY_TOKEN="$(gcloud auth print-identity-token)"
```

API üzerinden readiness:

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_URL" \
  --ai-base-url "$AI_URL" \
  --ai-identity-token "$AI_IDENTITY_TOKEN" \
  --rate-limit-check
```

Gerçek public token üretildikten sonra:

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_URL" \
  --ai-base-url "$AI_URL" \
  --ai-identity-token "$AI_IDENTITY_TOKEN" \
  --public-token "$PARTALOG_PUBLIC_TOKEN" \
  --catalog-ids "$PARTALOG_CATALOG_IDS" \
  --rate-limit-check
```

## 14. Eval

Staging katalog verisi hazır olunca `queries.relevance.jsonl` güncellenmeli ve yeniden baseline alınmalı:

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

## 15. Staging Rollback

Chat'i kapat:

```bash
gcloud run services update "$API_SERVICE" \
  --region="$REGION" \
  --update-env-vars="ProductFeatures__EnableChatbot=false"
```

Önceki image'e dön:

```bash
gcloud run services update "$API_SERVICE" --region="$REGION" --image="$PREVIOUS_API_IMAGE"
gcloud run services update "$WEB_SERVICE" --region="$REGION" --image="$PREVIOUS_WEB_IMAGE"
gcloud run services update "$AI_SERVICE" --region="$REGION" --image="$PREVIOUS_AI_IMAGE"
```

## 16. Staging GO Kriteri

- API, web ve AI staging servisleri ayrı isimlerle çalışıyor.
- AI service anonymous erişime kapalı.
- API service account, AI service üzerinde `roles/run.invoker` sahibi.
- API `/health/live`, `/health/ready`, `/health/migrations` başarılı.
- Web `/api/system/features` chat ve katalog analiz flag'lerini doğru gösteriyor.
- Invalid token ve rate-limit smoke başarılı.
- Gerçek public token ile chat smoke başarılı.
- Güncel eval baseline eşikleri geçiyor.
- Load smoke production hedeflerini en az küçük ölçekte doğruluyor.
