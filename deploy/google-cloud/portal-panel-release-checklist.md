# Portal + Panel Release Checklist

Bu kontrol listesi tek ana isletme modeli icindir:

- `DOMAIN` (`domain.com`): musteri portali, `/p/:token`, katalog ve chat.
- `PANEL_DOMAIN` (`panel.domain.com`): panel girisi, dashboard ve platform admin.

## Deploy Oncesi

- Self-service panel kaydi kapali oldugu icin ilk kullanicilari kontrollu script ile olustur:

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

Platform yonetimi gerekiyorsa ayri bir `PlatformAdmin` hesabi olustur:

```bash
./backend/scripts/create_initial_user.py \
  --email "platform-admin@example.com" \
  --name "Platform Admin" \
  --role PlatformAdmin \
  --plan ai
```

- `DOMAIN` ve `PANEL_DOMAIN` ayni `katalogcu-web` Cloud Run servisine map edildi.
- `katalogcu-web` env degerleri set edildi:
  - `PORTAL_HOST=$DOMAIN`
  - `PANEL_HOST=$PANEL_DOMAIN`
  - `API_PROXY_URL=$API_URL`
- `katalogcu-api` CORS iki origin'i de iceriyor:
  - `Cors__AllowedOrigins__0=https://$DOMAIN`
  - `Cors__AllowedOrigins__1=https://$PANEL_DOMAIN`
- `Frontend__BaseUrl=https://$DOMAIN` olarak set edildi.
- API service ayarlari katalog + chat moduna uygun:
  - `ProductFeatures__EnableChatbot=true`
  - `ProductFeatures__EnableCatalogAnalysis=true`
  - `ProductFeatures__EnableEcommerce=false`
  - `ProductFeatures__EnableUpgradePrompts=false`
  - `ProductFeatures__EnablePlanManagement=false`

## Postdeploy Smoke

Once API ve web servis URL'lerini al:

```bash
API_URL="$(gcloud run services describe katalogcu-api --region="$REGION" --format='value(status.url)')"
WEB_URL="$(gcloud run services describe katalogcu-web --region="$REGION" --format='value(status.url)')"
```

API hazirligini kontrol et:

```bash
./backend/scripts/postdeploy_catalog_only_check.sh \
  --api-url "$API_URL" \
  --admin-bearer-token "<ADMIN_JWT>"
```

Portal/panel domain ayrimini kontrol et:

```bash
./backend/scripts/postdeploy_portal_panel_check.sh \
  --portal-url "https://$DOMAIN" \
  --panel-url "https://$PANEL_DOMAIN" \
  --public-token "$PARTALOG_PUBLIC_TOKEN"
```

Chat release'i de aktifse:

```bash
./backend/scripts/smoke_chat_prod_readiness.sh \
  --api-base-url "$API_URL" \
  --ai-base-url "$AI_URL" \
  --public-token "$PARTALOG_PUBLIC_TOKEN" \
  --catalog-ids "$PARTALOG_CATALOG_IDS" \
  --rate-limit-check
```

## Beklenen Davranis

- `https://$DOMAIN/` portal token giris ekranini acar.
- `https://$DOMAIN/login` -> `https://$PANEL_DOMAIN/login`.
- `https://$DOMAIN/dashboard` -> `https://$PANEL_DOMAIN/dashboard`.
- `https://$PANEL_DOMAIN/` panel SPA'sini acar; Angular giris durumuna gore `/login` veya `/dashboard` yonlendirir.
- `https://$PANEL_DOMAIN/p/:token` -> `https://$DOMAIN/p/:token`.
- Panelden kopyalanan davet linkleri `https://$DOMAIN/p/:token` formatindadir.
- Public katalog/chat endpointleri musteri oturumu olmadan katalog icerigi veya chat cevabi dondurmez.
