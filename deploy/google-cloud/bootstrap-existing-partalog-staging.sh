#!/usr/bin/env bash
set -euo pipefail

PROJECT_ID="${PROJECT_ID:-partalog}"
REGION="${REGION:-europe-west1}"
REPOSITORY="${REPOSITORY:-partalog}"
TAG="${TAG:-$(git rev-parse --short HEAD)-staging}"

# Existing Google Cloud resources from the previous deployment.
SQL_INSTANCE="${SQL_INSTANCE:-katalogcu-db}"
ASSETS_BUCKET="${ASSETS_BUCKET:-partalog-assets}"
API_SA="${API_SA:-partalog-api-sa@$PROJECT_ID.iam.gserviceaccount.com}"
AI_SA="${AI_SA:-partalog-ai-sa@$PROJECT_ID.iam.gserviceaccount.com}"
API_DB_SECRET="${API_DB_SECRET:-partalog-db-connection}"
AI_DB_SECRET="${AI_DB_SECRET:-partalog-db-connection}"
JWT_SECRET_NAME="${JWT_SECRET_NAME:-partalog-jwt-secret}"
PUBLIC_LINK_SECRET_NAME="${PUBLIC_LINK_SECRET_NAME:-partalog-public-link-secret}"
DATA_PROTECTION_SECRET_NAME="${DATA_PROTECTION_SECRET_NAME:-partalog-data-protection-key-encryption-key}"

# New staging resources. These do not overwrite the existing partalog-api/web/ai services.
API_SERVICE="${API_SERVICE:-partalog-api-staging}"
WEB_SERVICE="${WEB_SERVICE:-partalog-web-staging}"
AI_SERVICE="${AI_SERVICE:-partalog-ai-chat-staging}"
REDIS_INSTANCE="${REDIS_INSTANCE:-katalogcu-redis-staging}"
NETWORK="${NETWORK:-default}"
SUBNET="${SUBNET:-default}"
USE_REDIS="${USE_REDIS:-true}"

GCLOUD="${GCLOUD:-.tools/google-cloud-sdk/bin/gcloud}"

log() {
  printf "\n[%s] %s\n" "$(date +%H:%M:%S)" "$*"
}

exists_service_account() {
  "$GCLOUD" iam service-accounts describe "$1" --project="$PROJECT_ID" >/dev/null 2>&1
}

require_service_account() {
  local email="$1"
  if ! exists_service_account "$email"; then
    echo "Missing service account: $email" >&2
    exit 1
  fi
}

secret_exists() {
  "$GCLOUD" secrets describe "$1" --project="$PROJECT_ID" >/dev/null 2>&1
}

require_secret() {
  local secret="$1"
  if ! secret_exists "$secret"; then
    echo "Missing Secret Manager secret: $secret" >&2
    exit 1
  fi
}

ensure_project() {
  log "Using project ${PROJECT_ID}, region ${REGION}"
  "$GCLOUD" config set project "$PROJECT_ID" >/dev/null
  "$GCLOUD" config set run/region "$REGION" >/dev/null
}

ensure_required_apis() {
  log "Ensuring required Google Cloud APIs"
  "$GCLOUD" services enable \
    run.googleapis.com \
    sqladmin.googleapis.com \
    artifactregistry.googleapis.com \
    secretmanager.googleapis.com \
    cloudbuild.googleapis.com \
    storage.googleapis.com \
    redis.googleapis.com \
    vpcaccess.googleapis.com \
    aiplatform.googleapis.com
}

ensure_existing_basics() {
  log "Checking existing resources"
  "$GCLOUD" artifacts repositories describe "$REPOSITORY" --location="$REGION" >/dev/null
  "$GCLOUD" sql instances describe "$SQL_INSTANCE" >/dev/null
  "$GCLOUD" storage buckets describe "gs://$ASSETS_BUCKET" >/dev/null
  require_service_account "$API_SA"
  require_service_account "$AI_SA"
  require_secret "$API_DB_SECRET"
  require_secret "$AI_DB_SECRET"
  require_secret "$JWT_SECRET_NAME"
  require_secret "$PUBLIC_LINK_SECRET_NAME"
  require_secret "$DATA_PROTECTION_SECRET_NAME"
}

ensure_sql_running() {
  local state
  state="$("$GCLOUD" sql instances describe "$SQL_INSTANCE" --format='value(state)')"
  if [[ "$state" == "RUNNABLE" ]]; then
    log "Cloud SQL already RUNNABLE: $SQL_INSTANCE"
    return
  fi

  log "Starting Cloud SQL instance ${SQL_INSTANCE} (current state: ${state})"
  "$GCLOUD" sql instances patch "$SQL_INSTANCE" --activation-policy=ALWAYS --quiet
}

ensure_vpc_connector() {
  if [[ "$USE_REDIS" != "true" ]]; then
    log "Skipping Direct VPC configuration because USE_REDIS=false"
    return
  fi
  log "Using Cloud Run Direct VPC egress: network=$NETWORK subnet=$SUBNET"
}

ensure_redis() {
  if [[ "$USE_REDIS" != "true" ]]; then
    REDIS_HOST=""
    export REDIS_HOST
    log "Skipping Redis because USE_REDIS=false"
    return
  fi

  if "$GCLOUD" redis instances describe "$REDIS_INSTANCE" --region="$REGION" >/dev/null 2>&1; then
    log "Redis exists: $REDIS_INSTANCE"
  else
    log "Creating Redis / Memorystore: $REDIS_INSTANCE"
    "$GCLOUD" redis instances create "$REDIS_INSTANCE" \
      --region="$REGION" \
      --tier=basic \
      --size=1 \
      --redis-version=redis_7_0 \
      --network="$NETWORK"
  fi

  REDIS_HOST="$("$GCLOUD" redis instances describe "$REDIS_INSTANCE" \
    --region="$REGION" \
    --format='value(host)')"
  export REDIS_HOST
  log "Redis host: $REDIS_HOST"
}

ensure_iam() {
  log "Ensuring IAM bindings"
  "$GCLOUD" projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:$AI_SA" \
    --role="roles/aiplatform.user" \
    --quiet >/dev/null

  "$GCLOUD" projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:$API_SA" \
    --role="roles/cloudsql.client" \
    --quiet >/dev/null

  "$GCLOUD" projects add-iam-policy-binding "$PROJECT_ID" \
    --member="serviceAccount:$AI_SA" \
    --role="roles/cloudsql.client" \
    --quiet >/dev/null

  "$GCLOUD" storage buckets add-iam-policy-binding "gs://$ASSETS_BUCKET" \
    --member="serviceAccount:$API_SA" \
    --role="roles/storage.objectAdmin" \
    --quiet >/dev/null

  "$GCLOUD" storage buckets add-iam-policy-binding "gs://$ASSETS_BUCKET" \
    --member="serviceAccount:$AI_SA" \
    --role="roles/storage.objectAdmin" \
    --quiet >/dev/null

  for secret in "$API_DB_SECRET" "$JWT_SECRET_NAME" "$PUBLIC_LINK_SECRET_NAME" "$DATA_PROTECTION_SECRET_NAME"; do
    "$GCLOUD" secrets add-iam-policy-binding "$secret" \
      --member="serviceAccount:$API_SA" \
      --role="roles/secretmanager.secretAccessor" \
      --quiet >/dev/null
  done

  "$GCLOUD" secrets add-iam-policy-binding "$AI_DB_SECRET" \
    --member="serviceAccount:$AI_SA" \
    --role="roles/secretmanager.secretAccessor" \
    --quiet >/dev/null
}

build_images() {
  log "Building API image"
  "$GCLOUD" builds submit backend \
    --config=backend/cloudbuild.api.yaml \
    --substitutions=_REGION="$REGION",_REPOSITORY="$REPOSITORY",_IMAGE_NAME=api-staging,_TAG="$TAG"

  log "Building Web image"
  "$GCLOUD" builds submit frontend/katalogcu-frontend \
    --config=frontend/katalogcu-frontend/cloudbuild.web.yaml \
    --substitutions=_REGION="$REGION",_REPOSITORY="$REPOSITORY",_IMAGE_NAME=web-staging,_TAG="$TAG"

  log "Building AI chat image"
  "$GCLOUD" builds submit partalog-ai \
    --config=partalog-ai/cloudbuild.chat.yaml \
    --substitutions=_REGION="$REGION",_REPOSITORY="$REPOSITORY",_IMAGE_NAME=partalog-ai-chat-staging,_TAG="$TAG"

  API_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/$REPOSITORY/api-staging:$TAG"
  WEB_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/$REPOSITORY/web-staging:$TAG"
  AI_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/$REPOSITORY/partalog-ai-chat-staging:$TAG"
  export API_IMAGE WEB_IMAGE AI_IMAGE
}

deploy_ai() {
  log "Deploying private AI chat service: $AI_SERVICE"
  if [[ "$USE_REDIS" == "true" ]]; then
    "$GCLOUD" run deploy "$AI_SERVICE" \
      --image="$AI_IMAGE" \
      --region="$REGION" \
      --service-account="$AI_SA" \
      --no-allow-unauthenticated \
      --port=8000 \
      --add-cloudsql-instances="$PROJECT_ID:$REGION:$SQL_INSTANCE" \
      --network="$NETWORK" \
      --subnet="$SUBNET" \
      --vpc-egress=private-ranges-only \
      --set-env-vars="DEBUG=false,STARTUP_SKIP_MODEL_LOADING=true,ENABLE_HOTSPOT_ENDPOINTS=false,ENABLE_CATALOG_PROCESSING_ENDPOINTS=false,GENAI_PROVIDER=vertex,GOOGLE_CLOUD_PROJECT=$PROJECT_ID,GOOGLE_CLOUD_LOCATION=global,GEMINI_CHAT_MODEL=gemini-2.5-flash-lite,GEMINI_ANALYSIS_MODEL=gemini-2.5-flash-lite,GENAI_REQUEST_TIMEOUT_SECONDS=30,GENAI_STREAM_TIMEOUT_SECONDS=90,GENAI_RETRY_ATTEMPTS=2,AI_CHAT_CAPACITY_PROVIDER=redis,AI_CHAT_USE_DISTRIBUTED_LEASES=true,AI_CHAT_REDIS_URL=redis://$REDIS_HOST:6379/0,AI_CHAT_REDIS_KEY_PREFIX=partalog:staging:ai-capacity,AI_CHAT_GLOBAL_CONCURRENCY=25,AI_CHAT_ACQUIRE_TIMEOUT_SECONDS=0.5,AI_CHAT_DISTRIBUTED_POOL_NAME=python-chat-staging,STORAGE_BUCKET=$ASSETS_BUCKET,STORAGE_BASE_URL=https://storage.googleapis.com/$ASSETS_BUCKET" \
      --set-secrets="DB_CONNECTION_STRING=$AI_DB_SECRET:latest" \
      --memory=1Gi \
      --cpu=1 \
      --min-instances=1 \
      --max-instances=3
  else
    "$GCLOUD" run deploy "$AI_SERVICE" \
      --image="$AI_IMAGE" \
      --region="$REGION" \
      --service-account="$AI_SA" \
      --no-allow-unauthenticated \
      --port=8000 \
      --add-cloudsql-instances="$PROJECT_ID:$REGION:$SQL_INSTANCE" \
      --set-env-vars="DEBUG=false,STARTUP_SKIP_MODEL_LOADING=true,ENABLE_HOTSPOT_ENDPOINTS=false,ENABLE_CATALOG_PROCESSING_ENDPOINTS=false,GENAI_PROVIDER=vertex,GOOGLE_CLOUD_PROJECT=$PROJECT_ID,GOOGLE_CLOUD_LOCATION=global,GEMINI_CHAT_MODEL=gemini-2.5-flash-lite,GEMINI_ANALYSIS_MODEL=gemini-2.5-flash-lite,GENAI_REQUEST_TIMEOUT_SECONDS=30,GENAI_STREAM_TIMEOUT_SECONDS=90,GENAI_RETRY_ATTEMPTS=2,AI_CHAT_CAPACITY_PROVIDER=postgres,AI_CHAT_USE_DISTRIBUTED_LEASES=true,AI_CHAT_GLOBAL_CONCURRENCY=10,AI_CHAT_ACQUIRE_TIMEOUT_SECONDS=0.5,AI_CHAT_DISTRIBUTED_POOL_NAME=python-chat-staging,STORAGE_BUCKET=$ASSETS_BUCKET,STORAGE_BASE_URL=https://storage.googleapis.com/$ASSETS_BUCKET" \
      --set-secrets="DB_CONNECTION_STRING=$AI_DB_SECRET:latest" \
      --memory=1Gi \
      --cpu=1 \
      --min-instances=1 \
      --max-instances=1
  fi

  AI_URL="$("$GCLOUD" run services describe "$AI_SERVICE" --region="$REGION" --format='value(status.url)')"
  export AI_URL

  "$GCLOUD" run services add-iam-policy-binding "$AI_SERVICE" \
    --region="$REGION" \
    --member="serviceAccount:$API_SA" \
    --role="roles/run.invoker" \
    --quiet >/dev/null
}

deploy_api() {
  log "Deploying API service: $API_SERVICE"
  if [[ "$USE_REDIS" == "true" ]]; then
    local redis_connection="$REDIS_HOST:6379,abortConnect=false"

    "$GCLOUD" run deploy "$API_SERVICE" \
      --image="$API_IMAGE" \
      --region="$REGION" \
      --allow-unauthenticated \
      --service-account="$API_SA" \
      --add-cloudsql-instances="$PROJECT_ID:$REGION:$SQL_INSTANCE" \
      --network="$NETWORK" \
      --subnet="$SUBNET" \
      --vpc-egress=private-ranges-only \
      --set-env-vars="^|^ASPNETCORE_ENVIRONMENT=Production|ASPNETCORE_URLS=http://+:8080|Frontend__BaseUrl=https://placeholder-staging.local|Cors__AllowedOrigins__0=https://placeholder-staging.local|RequestLimits__DefaultMaxBodySizeMb=50|AiService__BaseUrl=$AI_URL|AiService__UseCloudRunIdentityToken=true|AiService__CloudRunAudience=$AI_URL|AiService__ChatTimeoutSeconds=45|AiService__StreamTimeoutSeconds=90|AiService__LongRunningTimeoutSeconds=300|AiService__EnableItemEmbeddings=true|AiService__EmbeddingTimeoutSeconds=20|ProductFeatures__EnableChatbot=true|ProductFeatures__EnableCatalogAnalysis=true|ProductFeatures__EnableEcommerce=false|ProductFeatures__EnableUpgradePrompts=false|ProductFeatures__EnablePlanManagement=false|DistributedRateLimits__RedisPublicChatEnabled=true|DistributedRateLimits__RedisConnectionString=$redis_connection|DistributedRateLimits__RedisKeyPrefix=partalog:staging:rate-limit|DistributedRateLimits__PublicChatPermitLimit=20|DistributedRateLimits__PublicChatWindowSeconds=60|DistributedRateLimits__FailOpen=false|AiCapacity__Provider=Redis|AiCapacity__GlobalConcurrentChats=25|AiCapacity__PerUserConcurrentChats=3|AiCapacity__AcquireTimeoutMs=500|AiCapacity__RedisConnectionString=$redis_connection|AiCapacity__RedisKeyPrefix=partalog:staging:ai-capacity|AiCapacity__DistributedPoolName=api-chat-staging|DataProtection__Provider=Redis|DataProtection__RedisConnectionString=$redis_connection|DataProtection__RedisKey=partalog:staging:data-protection:keys|FileStorage__Provider=GoogleCloudStorage|FileStorage__BucketName=$ASSETS_BUCKET|FileStorage__PublicBaseUrl=https://storage.googleapis.com/$ASSETS_BUCKET|CatalogAiProcessing__MaxAttempts=4|CatalogAiProcessing__BaseRetryDelaySeconds=15|CatalogAiProcessing__HangfireWorkerCount=1|BackgroundProcessing__EnableCatalogAiServer=true|BackgroundProcessing__EnableExternalSiteCrawlServer=false|BackgroundProcessing__EnableDefaultServer=false|BackgroundProcessing__EnableRecurringJobs=false" \
      --set-secrets="ConnectionStrings__DefaultConnection=$API_DB_SECRET:latest,JwtSettings__SecretKey=$JWT_SECRET_NAME:latest,PublicLink__SecretKey=$PUBLIC_LINK_SECRET_NAME:latest,DataProtection__KeyEncryptionKey=$DATA_PROTECTION_SECRET_NAME:latest" \
      --memory=1Gi \
      --cpu=1 \
      --min-instances=0 \
      --max-instances=3
  else
    "$GCLOUD" run deploy "$API_SERVICE" \
      --image="$API_IMAGE" \
      --region="$REGION" \
      --allow-unauthenticated \
      --service-account="$API_SA" \
      --add-cloudsql-instances="$PROJECT_ID:$REGION:$SQL_INSTANCE" \
      --set-env-vars="^|^ASPNETCORE_ENVIRONMENT=Production|ASPNETCORE_URLS=http://+:8080|Frontend__BaseUrl=https://placeholder-staging.local|Cors__AllowedOrigins__0=https://placeholder-staging.local|RequestLimits__DefaultMaxBodySizeMb=50|AiService__BaseUrl=$AI_URL|AiService__UseCloudRunIdentityToken=true|AiService__CloudRunAudience=$AI_URL|AiService__ChatTimeoutSeconds=45|AiService__StreamTimeoutSeconds=90|AiService__LongRunningTimeoutSeconds=300|AiService__EnableItemEmbeddings=true|AiService__EmbeddingTimeoutSeconds=20|ProductFeatures__EnableChatbot=true|ProductFeatures__EnableCatalogAnalysis=true|ProductFeatures__EnableEcommerce=false|ProductFeatures__EnableUpgradePrompts=false|ProductFeatures__EnablePlanManagement=false|DistributedRateLimits__RedisPublicChatEnabled=false|DistributedRateLimits__FailOpen=false|AiCapacity__Provider=Postgres|AiCapacity__UseDistributedLeases=true|AiCapacity__GlobalConcurrentChats=10|AiCapacity__PerUserConcurrentChats=3|AiCapacity__AcquireTimeoutMs=500|AiCapacity__DistributedPoolName=api-chat-staging|DataProtection__Provider=FileSystem|DataProtection__KeysDirectory=/tmp/katalogcu-dp-keys|FileStorage__Provider=GoogleCloudStorage|FileStorage__BucketName=$ASSETS_BUCKET|FileStorage__PublicBaseUrl=https://storage.googleapis.com/$ASSETS_BUCKET|CatalogAiProcessing__MaxAttempts=4|CatalogAiProcessing__BaseRetryDelaySeconds=15|CatalogAiProcessing__HangfireWorkerCount=1|BackgroundProcessing__EnableCatalogAiServer=true|BackgroundProcessing__EnableExternalSiteCrawlServer=false|BackgroundProcessing__EnableDefaultServer=false|BackgroundProcessing__EnableRecurringJobs=false" \
      --set-secrets="ConnectionStrings__DefaultConnection=$API_DB_SECRET:latest,JwtSettings__SecretKey=$JWT_SECRET_NAME:latest,PublicLink__SecretKey=$PUBLIC_LINK_SECRET_NAME:latest,DataProtection__KeyEncryptionKey=$DATA_PROTECTION_SECRET_NAME:latest" \
      --memory=1Gi \
      --cpu=1 \
      --min-instances=0 \
      --max-instances=1
  fi

  API_URL="$("$GCLOUD" run services describe "$API_SERVICE" --region="$REGION" --format='value(status.url)')"
  export API_URL
}

deploy_web() {
  log "Deploying web service: $WEB_SERVICE"
  "$GCLOUD" run deploy "$WEB_SERVICE" \
    --image="$WEB_IMAGE" \
    --region="$REGION" \
    --allow-unauthenticated \
    --set-env-vars="API_PROXY_URL=$API_URL" \
    --memory=512Mi \
    --cpu=1 \
    --min-instances=0 \
    --max-instances=3

  WEB_URL="$("$GCLOUD" run services describe "$WEB_SERVICE" --region="$REGION" --format='value(status.url)')"
  export WEB_URL

  log "Updating API CORS/base URL to web staging URL"
  "$GCLOUD" run services update "$API_SERVICE" \
    --region="$REGION" \
    --update-env-vars="Frontend__BaseUrl=$WEB_URL,Cors__AllowedOrigins__0=$WEB_URL"
}

print_summary() {
  log "Staging deploy summary"
  printf "API_URL=%s\n" "$API_URL"
  printf "WEB_URL=%s\n" "$WEB_URL"
  printf "AI_URL=%s\n" "$AI_URL"
  printf "USE_REDIS=%s\n" "$USE_REDIS"
  printf "REDIS_HOST=%s\n" "${REDIS_HOST:-}"
  printf "TAG=%s\n" "$TAG"
}

ensure_project
ensure_required_apis
ensure_existing_basics
ensure_sql_running
ensure_vpc_connector
ensure_redis
ensure_iam
build_images
deploy_ai
deploy_api
deploy_web
print_summary
