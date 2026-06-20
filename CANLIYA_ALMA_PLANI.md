# Katalogcu Catalog-Only Canliya Alma Plani

Bu planin amaci projeyi hizli ama kontrollu sekilde ilk canli kullanima almaktir.
Ilk release tam platform degil, catalog-only MVP olarak cikmalidir.

## 1) V1 Kapsami

Canliya acik olacaklar:

- Kullanici kayit/giris
- Dashboard
- Katalog olusturma, listeleme ve detay goruntuleme
- PDF/katalog yukleme ve yayinlama
- Katalog sayfasi ve hotspot/parca goruntuleme
- Public katalog linki
- Temel profil/ayarlar
- Iceride kullanilmak uzere platform admin

V1'de kapali kalacaklar:

- AI chatbot
- E-ticaret, musteri checkout ve siparis akislari
- Plan/abonelik yonetimi
- Upgrade promptlari
- WooCommerce sepet/stok/fiyat modu
- External site crawling ve otomatik urun eslestirme vaadi

## 2) Zorunlu Feature Flag Seti

Backend production:

```json
"ProductFeatures": {
  "EnableChatbot": false,
  "EnableCatalogAnalysis": true,
  "EnableEcommerce": false,
  "EnableUpgradePrompts": false,
  "EnablePlanManagement": false
}
```

Frontend production:

```ts
features: {
  enableChatbot: false,
  enableCatalogAnalysis: true,
  enableEcommerce: false,
  enableUpgradePrompts: false,
  enablePlanManagement: false
}
```

Prod secret/env sablonu:

- `backend/.env.production.catalog-only.example`

## 3) Canli Oncesi Teknik Gate

Bu komutlar temiz gecmeden canliya cikilmaz:

```bash
dotnet test backend/Katalogcu.sln --no-restore
```

```bash
cd frontend/katalogcu-frontend
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
./backend/scripts/preflight_catalog_only.sh --skip-runtime
```

Staging veya canli API ayaga kalkinca:

```bash
./backend/scripts/postdeploy_catalog_only_check.sh --api-url https://api-domain.example
```

Public AI/chat ve e-ticaret modulleri acilmadan once kontrollu yuk kaniti icin manuel workflow calistirilir:

- GitHub Actions: `Public E2E Load Smoke`
- Gerekli secretlar: `PARTALOG_BASE_URL`, `PARTALOG_PUBLIC_TOKEN`
- Varsayilan akista browse/chat/SSE test edilir, checkout/siparis senaryosu `checkout_weight=0` ile kapali kalir.
- Her etkin senaryonun esiklerle degerlendirilebilmesi icin varsayilan olarak en az `5` tamamlanmis istek uretmesi gerekir.
- Varsayilan p95 gecikme butceleri browse icin `5 sn`, chat ve SSE icin `15 sn`, checkout icin `10 sn` olarak ayri ayri uygulanir.
- SSE akisi icin ilk bos olmayan tokenin p95 suresi ayrica olculur ve varsayilan `5 sn` esigini asarsa workflow fail olur.
- SSE upstream hata fallback orani varsayilan `%5` esigini asarsa workflow fail olur; normal arama fallbackleri bu esige dahil edilmez.
- Checkout senaryosu sadece siparis olusturma yan etkisi kabul edildiginde manuel olarak acilir.

Yerel catalog-only staging provasi:

```bash
cd backend
docker compose -f docker-compose.catalog-only.yml up -d --build
```

Yerel adresler:

- Frontend: `http://localhost:4200`
- API: `http://localhost:5160`

Yerel runtime smoke:

```bash
./backend/scripts/postdeploy_catalog_only_check.sh \
  --api-url http://localhost:5160 \
  --admin-bearer-token "<ADMIN_JWT>"
```

## 4) Staging Prova Akisi

Staging'de su akisin ekran goruntusu veya log ile kanitla:

1. Temiz database migration basariyla uygulanir.
2. Admin/kullanici girisi yapilir.
3. Bir PDF katalog yuklenir.
4. Katalog detay sayfasi acilir.
5. Public link uretilir.
6. Public link masaustu ve mobil viewport'ta acilir.
7. Chatbot/e-ticaret/plan endpointleri catalog-only modda kapali kalir.
8. `health/live`, `health/ready`, `health/migrations` 200 doner.
9. Frontend nginx proxy uzerinden `/api/system/features` 200 doner.

## 5) Canli Deploy Sirasi

1. Son commit ve branch not edilir.
2. Veritabani backup alinir.
3. Backend prod env degerleri uygulanir.
4. Backend deploy edilir.
5. Migration/health kontrol edilir.
6. Frontend prod build deploy edilir.
7. Domain, SSL ve CORS kontrol edilir.
8. Ilk canli katalogla smoke test yapilir.
9. Release raporu uretilir.
10. Ilk 24 saat log ve health endpointleri izlenir.

## 6) Go / No-Go Kurali

GO icin:

- Backend testleri gecer.
- Frontend build ve testleri gecer.
- Catalog-only preflight gecer.
- Staging smoke akisi gecer.
- Production secretlar placeholder degildir.
- Rollback icin son stabil artifact veya image hazirdir.

NO-GO icin:

- Migration health 200 donmez.
- Public katalog acilmaz.
- Upload veya katalog detay ana akisi calismaz.
- Catalog-only kapali olmasi gereken modul acik gorunur.
- CORS/JWT kaynakli login sorunu vardir.

## 7) Ilk 24 Saat Izleme

Takip edilecekler:

- 5xx hata orani
- Login/register hatalari
- PDF upload hatalari
- Public katalog acilis suresi
- Static dosya servis hatalari
- `/health/migrations` durumu
- Disk kullanimi ve upload klasoru

Ilk sorun aninda once feature flag ve deploy env kontrol edilir, sonra gerekirse onceki stabil artifact'e donulur.
