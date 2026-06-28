# Katalogcu - Proje Sunum Raporu

## Kısa Tanım

Katalogcu, teknik katalogları dijital müşteri portalına dönüştüren full-stack bir katalog ve AI asistan platformudur. İşletme paneli katalog, ürün, müşteri ve erişim yönetimini sağlar; public portal ise müşterinin yetkili katalogları görüntülemesini ve katalog üzerinden soru sormasını mümkün kılar.

## Çözdüğü Problem

Teknik kataloglar çoğu işletmede PDF, Excel ve manuel müşteri iletişimi arasında dağılır. Bu durum parça bulmayı, müşteriyle doğru katalog paylaşmayı, sipariş öncesi doğrulamayı ve katalog kalitesini izlemeyi zorlaştırır.

Katalogcu bu süreci şu şekilde toparlar:

- PDF kataloglar yönetilebilir dijital kataloglara çevrilir.
- Parçalar, katalog sayfaları ve hotspot alanları panelden düzenlenir.
- Müşteriler yalnızca yetkili oldukları public kataloglara erişir.
- AI chat, katalog içeriği üzerinden parça arama ve yönlendirme yapar.
- Smoke, eval, load ve release kapıları ile canlıya alma riski düşürülür.

## Ürün Yetenekleri

| Alan | Yetenek |
|---|---|
| Katalog yönetimi | Katalog oluşturma, PDF yükleme, sayfa işleme, ürün/hotspot yönetimi |
| Public portal | Token bazlı katalog paylaşımı, müşteri giriş/tamamlama, portal/panel domain ayrımı |
| AI chat | Katalog bağlamlı soru-cevap, SSE streaming, fallback reason ve stream contract |
| Arama | Exact-code, metin, embedding/vector ve görsel ipucu destekleri |
| Operasyon | Docker Compose, Cloud Run runbook'ları, smoke/preflight/postdeploy scriptleri |
| Kalite | xUnit backend testleri, Angular compile/test, Python testleri, chat eval ve load baseline gate |
| Güvenlik | JWT, public token, self-service kayıt kapatma, SSRF kontrolleri, rate limit ve secret policy |

## Teknik Mimari

```text
Müşteri / Panel Kullanıcısı
          |
          v
Angular 20 Web Uygulaması
          |
          v
.NET 9 API
          |
          +--> PostgreSQL + pgvector
          |
          +--> Redis / Data protection / rate limit opsiyonları
          |
          v
FastAPI AI Servisi
          |
          +--> OCR / YOLO / visual search
          +--> embedding / vector search
          +--> Gemini provider entegrasyonu
```

## Kod Organizasyonu

- `backend/`: .NET API, Clean Architecture katmanları, EF Core, Hangfire, operasyon scriptleri.
- `frontend/katalogcu-frontend/`: Angular panel, portal, public viewer ve katalog chat ekranları.
- `partalog-ai/`: FastAPI AI servisi, embedding/vector/search/chat/eval modülleri.
- `deploy/google-cloud/`: production, staging, monitoring ve portal/panel release runbook'ları.
- `plans/`: ürün yol haritası.

Detaylı teknik harita için `PROJE_YAPISI.md` kullanılmalıdır.

## Kalite ve Doğrulama Kanıtları

Projede sadece özellik kodu değil, üretime hazırlık katmanı da tutulur:

- Backend xUnit regresyon testleri.
- Angular compile ve CI test scriptleri.
- Python AI servis testleri.
- Public checkout smoke ve full-stack smoke scriptleri.
- Catalog-only preflight ve postdeploy kontrolleri.
- Chat eval gate ve nightly eval akışı.
- Public load baseline promotion ve validation dokümanları.
- Migration discipline workflow'u.

Örnek komutlar:

```bash
dotnet test backend/Katalogcu.sln
cd frontend/katalogcu-frontend && npm run test:compile
./backend/scripts/smoke_all.sh
./backend/scripts/preflight_catalog_only.sh --api-url http://localhost:5159
```

## Canlıya Alma Modeli

Proje farklı yayın modlarını dokümante eder:

- Catalog-only: AI/e-commerce kapalı, temel katalog ürünü.
- Catalog-chat: katalog + AI chat akışı.
- Portal/panel ayrımı: `domain.com` müşteri portalı, `panel.domain.com` yönetim paneli.
- Staging: chat, observability, load ve rollback doğrulamaları için ayrı ortam.

İlgili runbook'lar:

- `backend/CATALOG_ONLY_PROD_CHECKLIST.md`
- `backend/CATALOG_ONLY_RELEASE_GO_NO_GO.md`
- `deploy/google-cloud/catalog-only-cloud-run.md`
- `deploy/google-cloud/catalog-chat-cloud-run.md`
- `deploy/google-cloud/portal-panel-release-checklist.md`

## Güçlü Yanlar

- Ürün, backend, frontend, AI ve operasyon tarafları aynı repoda tutarlı şekilde bağlanmış.
- Public portal ve panel domain ayrımı gerçek deploy senaryosunu hedefliyor.
- AI akışları sadece demo seviyesinde değil; eval, stream contract, fallback, quota ve load kontrolleriyle destekleniyor.
- Release dokümanları komut, beklenen çıktı ve reject/go-no-go mantığı içeriyor.
- Eski/dağınık dosyalar temizlenmiş; kalan envanter `PROJECT_FILE_REPORT.md` içinde izleniyor.

## Yayın Öncesi GitHub Vitrin Kontrol Listesi

- `LICENSE` dosyası eklenmeli.
- Secret içeren lokal `.env` dosyalarının repoda olmadığından emin olunmalı.
- README'de yer alacak ekran görüntüleri veya kısa demo GIF'leri eklenirse proje çok daha hızlı anlaşılır.
- GitHub repository description ve topics alanları doldurulmalı.
- Public sunum için gerekiyorsa demo verisi/fixture ayrı ve secretsız bir klasörde tutulmalı.
- Mevcut çalışma ağacındaki silinmiş eski dosyalar ve yeni eklenen dosyalar tek amaçlı commit'lere ayrılmalı.

## Değerlendirme Özeti

Katalogcu, yalnızca katalog yükleyen bir CRUD uygulaması değil; katalog verisini müşteri erişimi, AI destekli arama/chat ve üretime hazırlık disiplinleriyle birleştiren uçtan uca bir platformdur. GitHub vitrini için en güçlü mesaj şudur: proje, gerçek bir B2B katalog operasyonunun teknik, ürün ve deploy gereksinimlerini birlikte ele alır.
