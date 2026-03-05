# Catalog-Only Release Go / No-Go

Amaç: Canlıya sadece katalog paketi ile çıkış kararını tek sayfada netleştirmek.

Otomatik rapor üretimi:

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/generate_catalog_release_report.sh --with-preflight --with-postdeploy --api-url http://localhost:5159
```

## 1) Release Günü Formu

| Alan | Değer |
|---|---|
| Release tarihi (UTC) | `YYYY-MM-DD HH:mm` |
| Sürüm / Tag | `vX.Y.Z` |
| Branch | `main` / `master` |
| Commit SHA | `<git-sha>` |
| Release owner | `<isim>` |
| On-call engineer | `<isim>` |
| DB migration owner | `<isim>` |
| Değerlendirme sonucu | `GO` / `NO-GO` |
| Not | `<kısa not>` |

## 2) Go/No-Go Tablosu (Doldurulabilir)

| Alan | Kontrol | Kanıt | Sonuç | Sorumlu | Zaman |
|---|---|---|---|
| Konfig | `ProductFeatures` tamamı `false` | `appsettings.json` + `/api/system/features` çıktısı |  |  |  |
| Migration | Pending migration/model change yok | `/health/migrations=200` + `dotnet ef` çıktısı |  |  |  |
| API Sağlık | `live` ve `ready` 200 dönüyor | `/health/live`, `/health/ready` çıktısı |  |  |  |
| Modül Gate | AI/Ecommerce endpointleri `403` | `/api/chat`, `/api/orders`, `/api/products`, `/api/customers` |  |  |  |
| Public Link | Üretilen public token kısa formatta (`pk_`) | `/api/catalogs/public-token` (auth) |  |  |  |
| Frontend | Type-check/build temiz | `npx tsc` + build logu |  |  |  |
| Smoke | Kritik katalog akışı çalışıyor | katalog açma, sayfa görüntüleme, hotspot |  |  |  |
| Güvenlik | JWT secret / CORS doğru | env/secrets checklist |  |  |  |
| İzlenebilirlik | Hata logu ve health endpoint izleniyor | log panel / alert ekran görüntüsü |  |  |  |

## 3) Kanıt Linkleri

- Preflight çıktısı: `<link veya dosya yolu>`
- Post-deploy check çıktısı: `<link veya dosya yolu>`
- Build artifact linki: `<link>`
- Observability dashboard linki: `<link>`

## 4) Çıkış Öncesi Komutlar

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/preflight_catalog_only.sh --api-url http://localhost:5159
```

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/postdeploy_catalog_only_check.sh --api-url http://localhost:5159
```

## 5) Karar Kuralı

- Tüm satırlar `GO` ise: release onay.
- Herhangi bir satır `NO-GO` ise: release durdur, düzeltme aç, yeniden preflight çalıştır.

## 6) Onay İmzaları

| Rol | İsim | Onay |
|---|---|---|
| Release owner |  | ✅ / ❌ |
| Tech lead |  | ✅ / ❌ |
| QA |  | ✅ / ❌ |
| Ops/DevOps |  | ✅ / ❌ |

## 7) Hızlı Rollback Kuralı

- Son stabil backend + frontend artifact’e geri dön.
- DB migration geri alma yapmadan önce etki analizi zorunlu.
- Rollback sonrası `postdeploy_catalog_only_check.sh` tekrar çalıştır.

## 8) Çıkış Sonrası 24 Saat İzleme

- 5xx oranı
- `/health/migrations` durumu
- Katalog görüntüleme hataları
- Public view yüklenme süresi
