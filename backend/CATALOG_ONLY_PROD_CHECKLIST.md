# Catalog-Only Prod Checklist

Bu runbook, sistemi sadece `Katalog` paketi ile canlıya almak için kısa kontrol listesidir.

Go/No-Go tek sayfa karar formu:
- `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/CATALOG_ONLY_RELEASE_GO_NO_GO.md`

## 1) Feature Flags (zorunlu)

`backend/Katalogcu.API/appsettings.json` içinde:

```json
"ProductFeatures": {
  "EnableAi": false,
  "EnableEcommerce": false,
  "EnableUpgradePrompts": false
}
```

Frontend prod build için:

`frontend/katalogcu-frontend/src/environments/environment.ts`

```ts
features: {
  enableAi: false,
  enableEcommerce: false,
  enableUpgradePrompts: false
}
```

Prod ortam değişkenleri için hazır şablon:
- `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/.env.production.catalog-only.example`

## 2) Deploy öncesi teknik kontroller

Tek komutta preflight:
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/preflight_catalog_only.sh --api-url http://localhost:5159
```

Opsiyonel kısa public link format doğrulaması (`pk_`):
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/preflight_catalog_only.sh --api-url http://localhost:5159 --admin-bearer-token "<ADMIN_JWT>"
```

Tek komutta release raporu:
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/generate_catalog_release_report.sh --with-preflight --with-postdeploy --api-url http://localhost:5159
```

Not:
- `dotnet-ef` yoksa script migration/model check adımını warning ile atlar.
- Gerekirse: `dotnet tool install --global dotnet-ef`
- Migration check timeout ayarı: `DOTNET_EF_TIMEOUT_SECONDS` (default: 90)

Adım adım kontrol:

1. Backend build:
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/Katalogcu.API
dotnet build
```

2. Frontend type-check:
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/frontend/katalogcu-frontend
npx tsc -p tsconfig.app.json --noEmit
```

3. Migration senkronu:
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/Katalogcu.API
dotnet ef migrations list
```

## 3) Runtime doğrulama (deploy sonrası)

Tek komutta post-deploy kontrol:
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/postdeploy_catalog_only_check.sh --api-url http://localhost:5159
```

Opsiyonel kısa public link format doğrulaması (`pk_`):
```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/postdeploy_catalog_only_check.sh --api-url http://localhost:5159 --admin-bearer-token "<ADMIN_JWT>"
```

Adım adım kontrol:

1. Health endpoint:
```bash
curl -s http://<api-host>/health/live
curl -s http://<api-host>/health/ready
curl -s http://<api-host>/health/migrations
```

2. Feature endpoint:
```bash
curl -s http://<api-host>/api/system/features
```

Beklenen:
- `aiEnabled=false`
- `ecommerceEnabled=false`
- `upgradePromptsEnabled=false`

3. API gate doğrulaması (403 beklenir):
```bash
curl -i http://<api-host>/api/chat/health
curl -i http://<api-host>/api/orders
```

## 4) Test için geçici açma (lokal/staging)

Sadece test ortamında:
- `EnableAi=true`
- `EnableEcommerce=true`
- `EnableUpgradePrompts=true`

Not: Prod için aynı değişiklikler kalıcı bırakılmamalı.

## 5) CI gate

Repo workflow:
- `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/.github/workflows/catalog-only-preflight.yml`

Bu workflow her PR/push'ta catalog-only preflight'ı (`--skip-runtime`) çalıştırır.
Ek olarak `backend/reports/` altındaki release raporunu artifact olarak yükler.
