# Google Cloud Catalog-Only Deploy

Bu dokuman ilk canli cikis icin catalog-only mimariyi tarif eder.

## Mimari

- `katalogcu-api`: Cloud Run servisidir, .NET API container calistirir.
- `katalogcu-web`: Cloud Run servisidir, Angular build'i Nginx ile sunar.
- `katalogcu-db`: Cloud SQL for PostgreSQL instance.
- `katalogcu-assets`: Cloud Storage bucket; katalog PDF ve sayfa gorselleri burada kalici tutulur.
- Container image deposu: Artifact Registry.
- Secretlar: Secret Manager.

Ilk canli cikista AI servisi deploy edilmez. Chatbot/e-ticaret/plan yonetimi feature flag ile kapali kalir.

AI fazi icin ayri plan: `deploy/google-cloud/vertex-ai-ai-plan.md`.

## Gerekli Bilgiler

Deploy oncesi su degerler netlesmeli:

- `PROJECT_ID`
- `REGION`, onerilen: `europe-west1`
- `DOMAIN`, ornek: `partalog.com`
- `PANEL_DOMAIN`, onerilen: `panel.$DOMAIN`
- `API_SERVICE`, onerilen: `katalogcu-api`
- `WEB_SERVICE`, onerilen: `katalogcu-web`
- Cloud SQL instance adi, onerilen: `katalogcu-db`
- Cloud Storage bucket adi, onerilen: `<PROJECT_ID>-katalogcu-assets`
- Production DB kullanici/sifre
- JWT, PublicLink ve DataProtection secretlari

## Google Cloud Servisleri

```bash
gcloud services enable \
  run.googleapis.com \
  sqladmin.googleapis.com \
  artifactregistry.googleapis.com \
  secretmanager.googleapis.com \
  cloudbuild.googleapis.com \
  storage.googleapis.com
```

## Artifact Registry

```bash
gcloud artifacts repositories create katalogcu \
  --repository-format=docker \
  --location="$REGION"
```

```bash
gcloud auth configure-docker "$REGION-docker.pkg.dev"
```

## Cloud SQL

```bash
gcloud sql instances create katalogcu-db \
  --database-version=POSTGRES_16 \
  --region="$REGION" \
  --tier=db-custom-1-3840 \
  --storage-size=20GB \
  --storage-type=SSD \
  --backup-start-time=03:00
```

```bash
gcloud sql databases create KatalogcuDb --instance=katalogcu-db
gcloud sql users create katalogcu_app --instance=katalogcu-db --password="$DB_PASSWORD"
```

Cloud SQL PostgreSQL 13+ pgvector destekler; migration tarafinda `vector` extension ihtiyaci varsa instance icinde extension yetkisinin dogrulanmasi gerekir.

Cloud Run connection string Unix socket uzerinden:

```text
Host=/cloudsql/PROJECT_ID:REGION:katalogcu-db;Database=KatalogcuDb;Username=katalogcu_app;Password=DB_PASSWORD
```

## Cloud Storage

Cloud Run dosya sistemi kalici degildir. Bu yuzden upload edilen PDF'ler ve PDF'ten uretilen sayfa gorselleri Cloud Storage bucket'inda tutulur.

```bash
ASSETS_BUCKET="$PROJECT_ID-katalogcu-assets"
```

```bash
gcloud storage buckets create "gs://$ASSETS_BUCKET" \
  --location="$REGION" \
  --uniform-bucket-level-access
```

Katalog sayfa gorselleri artik API uzerinden imzali erisim token'i ile servis ediliyor. Bu yuzden bucket objelerini public-read yapma.
Bucket private kalmali; tarayici dogrudan GCS yerine API endpoint'inden gorsel okuyacak.

API service account'ina bucket yazma/okuma yetkisi ver:

```bash
PROJECT_NUMBER="$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')"
RUN_SERVICE_ACCOUNT="$PROJECT_NUMBER-compute@developer.gserviceaccount.com"

gcloud storage buckets add-iam-policy-binding "gs://$ASSETS_BUCKET" \
  --member="serviceAccount:$RUN_SERVICE_ACCOUNT" \
  --role="roles/storage.objectAdmin"
```

## Secret Manager

```bash
printf "%s" "$JWT_SECRET" | gcloud secrets create katalogcu-jwt-secret --data-file=-
printf "%s" "$PUBLIC_LINK_SECRET" | gcloud secrets create katalogcu-public-link-secret --data-file=-
printf "%s" "$DATA_PROTECTION_KEY_ENCRYPTION_KEY" | gcloud secrets create katalogcu-data-protection-key-encryption-key --data-file=-
printf "%s" "$DB_PASSWORD" | gcloud secrets create katalogcu-db-password --data-file=-
```

## Image Build

```bash
API_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/katalogcu/api:$(git rev-parse --short HEAD)"
WEB_IMAGE="$REGION-docker.pkg.dev/$PROJECT_ID/katalogcu/web:$(git rev-parse --short HEAD)"
```

```bash
docker build -t "$API_IMAGE" -f backend/Katalogcu.API/Dockerfile backend
docker push "$API_IMAGE"
```

```bash
docker build -t "$WEB_IMAGE" -f frontend/katalogcu-frontend/Dockerfile.cloudrun frontend/katalogcu-frontend
docker push "$WEB_IMAGE"
```

## API Deploy

```bash
INSTANCE_CONNECTION_NAME="$PROJECT_ID:$REGION:katalogcu-db"
DB_CONNECTION="Host=/cloudsql/$INSTANCE_CONNECTION_NAME;Database=KatalogcuDb;Username=katalogcu_app;Password=$DB_PASSWORD"
REDIS_CONNECTION="YOUR_REDIS_HOST:6379,abortConnect=false"
```

```bash
gcloud run deploy katalogcu-api \
  --image="$API_IMAGE" \
  --region="$REGION" \
  --allow-unauthenticated \
  --add-cloudsql-instances="$INSTANCE_CONNECTION_NAME" \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,ASPNETCORE_URLS=http://+:8080,ConnectionStrings__DefaultConnection=$DB_CONNECTION,Frontend__BaseUrl=https://$DOMAIN,Cors__AllowedOrigins__0=https://$DOMAIN,Cors__AllowedOrigins__1=https://$PANEL_DOMAIN,FileStorage__Provider=GoogleCloudStorage,FileStorage__BucketName=$ASSETS_BUCKET,FileStorage__PublicBaseUrl=https://storage.googleapis.com/$ASSETS_BUCKET,DataProtection__Provider=Redis,DataProtection__RedisConnectionString=$REDIS_CONNECTION,DataProtection__RedisKey=partalog:data-protection:keys,ProductFeatures__EnableChatbot=false,ProductFeatures__EnableCatalogAnalysis=true,ProductFeatures__EnableEcommerce=false,ProductFeatures__EnableUpgradePrompts=false,ProductFeatures__EnablePlanManagement=false" \
  --set-secrets="JwtSettings__SecretKey=katalogcu-jwt-secret:latest,PublicLink__SecretKey=katalogcu-public-link-secret:latest,DataProtection__KeyEncryptionKey=katalogcu-data-protection-key-encryption-key:latest" \
  --memory=1Gi \
  --cpu=1 \
  --min-instances=0 \
  --max-instances=3
```

API URL:

```bash
API_URL="$(gcloud run services describe katalogcu-api --region="$REGION" --format='value(status.url)')"
```

## Frontend Deploy

```bash
gcloud run deploy katalogcu-web \
  --image="$WEB_IMAGE" \
  --region="$REGION" \
  --allow-unauthenticated \
  --set-env-vars="API_PROXY_URL=$API_URL,PORTAL_HOST=$DOMAIN,PANEL_HOST=$PANEL_DOMAIN" \
  --memory=512Mi \
  --cpu=1 \
  --min-instances=0 \
  --max-instances=3
```

Frontend URL:

```bash
WEB_URL="$(gcloud run services describe katalogcu-web --region="$REGION" --format='value(status.url)')"
```

## Postdeploy Kontrol

Self-service panel kaydi kapali oldugu icin ilk owner hesabi dogrudan DB bootstrap script'i ile olusturulur:

```bash
export DATABASE_URL="postgresql://katalogcu_app:CHANGE_ME@HOST:5432/KatalogcuDb"
export BOOTSTRAP_USER_PASSWORD="CHANGE_ME_STRONG_PASSWORD"

./backend/scripts/create_initial_user.py \
  --email "owner@example.com" \
  --name "Ana Isletme Sahibi" \
  --company-name "Ana Isletme" \
  --role Owner \
  --plan ai
```

```bash
./backend/scripts/postdeploy_catalog_only_check.sh \
  --api-url "$API_URL" \
  --admin-bearer-token "<ADMIN_JWT>"
```

Frontend proxy kontrolu:

```bash
curl -sS "$WEB_URL/api/system/features"
```

Custom domain mapping sonrasi portal/panel ayrimini kontrol et:

```bash
./backend/scripts/postdeploy_portal_panel_check.sh \
  --portal-url "https://$DOMAIN" \
  --panel-url "https://$PANEL_DOMAIN" \
  --public-token "$PARTALOG_PUBLIC_TOKEN"
```

## Domain

Ilk canli icin iki custom domain'i de `katalogcu-web` servisine bagla:

- `DOMAIN` (`domain.com`): musteri portali, `/p/:token`, katalog ve chat girisi.
- `PANEL_DOMAIN` (`panel.domain.com`): panel girisi, dashboard ve platform admin.

Browser tum API isteklerini frontend uzerinden `/api` olarak yapacak.

Nginx template'i yanlis host/path kombinasyonlarini server tarafinda duzeltir:

- `https://$DOMAIN/dashboard`, `https://$DOMAIN/login`, `https://$DOMAIN/platform` -> `https://$PANEL_DOMAIN/...`
- `https://$PANEL_DOMAIN/p/:token`, `https://$PANEL_DOMAIN/public-view/:token`, `https://$PANEL_DOMAIN/view/:id` -> `https://$DOMAIN/...`

API servisini ayri domain'e acmak zorunda degiliz; ama `postdeploy_catalog_only_check.sh` icin Cloud Run URL'i ile test edecegiz.

Detayli kontrol listesi: `deploy/google-cloud/portal-panel-release-checklist.md`.

## Secret ve Credential Kurali

- Repo veya image icine service account JSON dosyasi koyma.
- Cloud Run'da dogrudan servis hesabi/IAM kullan.
- Lokal test icin gerekiyorsa `GOOGLE_APPLICATION_CREDENTIALS` repo disindaki bir path'i gostersin.
- Gercek bir key dosyasi daha once olusturulduysa release oncesi revoke/rotate edilmelidir.

## Rollback

Bir onceki image tag'i sakla:

```bash
gcloud run services update katalogcu-api --image="$PREVIOUS_API_IMAGE" --region="$REGION"
gcloud run services update katalogcu-web --image="$PREVIOUS_WEB_IMAGE" --region="$REGION"
```
