# Katalogcu - Proje Yapısı

Bu doküman repodaki ana modüllerin güncel rolünü açıklar. Amaç, projeyi ilk kez inceleyen bir geliştiricinin hangi klasörde ne olduğunu hızlıca anlamasıdır.

## Üst Seviye Harita

```text
Katalogcu/
├── backend/                         # .NET 9 API, domain/application/infrastructure, testler, scriptler
├── frontend/katalogcu-frontend/     # Angular 20 panel, portal ve public katalog arayüzü
├── partalog-ai/                     # FastAPI AI servisi, chat/OCR/embedding/vector/eval
├── deploy/google-cloud/             # Cloud Run, staging, monitoring ve release runbook'ları
├── plans/                           # Ürün roadmap'i
├── README.md                        # GitHub ana vitrin dokümanı
├── PROJE_SUNUM_RAPORU.md            # Ürün ve teknik sunum özeti
└── PROJECT_FILE_REPORT.md           # Envanter ve temizlik raporu
```

## Backend

`backend/` .NET 9 tabanlı ana API ve iş mantığı katmanlarını içerir.

```text
backend/
├── Katalogcu.API/             # HTTP API, middleware, servis kayıtları, proxy/ops servisleri
├── Katalogcu.Application/     # CQRS command/query handler'ları, DTO'lar, validator'lar
├── Katalogcu.Domain/          # Entity, enum ve domain modelleri
├── Katalogcu.Infrastructure/  # EF Core DbContext, repository'ler, migration'lar
├── Katalogcu.API.Tests/       # xUnit backend regresyon testleri
├── scripts/                   # smoke, preflight, postdeploy, load ve release araçları
└── load-baselines/            # Public load baseline promotion ve review dokümanları
```

Başlıca backend sorumlulukları:

- Kullanıcı, müşteri, katalog, ürün, sipariş ve platform yönetimi.
- PDF yükleme, sayfa görseli üretme, dosya saklama ve katalog sayfası işleme.
- Public katalog linkleri, public müşteri oturumu ve portal erişim kontrolleri.
- Chat proxy, SSE stream contract, AI kapasite/rate limit guard'ları.
- External site crawling, external product matching ve review akışları.
- Policy threshold, feedback regression ve audit altyapısı.
- Production readiness, data protection, signing secret ve güvenlik kontrolleri.

## Frontend

`frontend/katalogcu-frontend/` Angular 20 uygulamasıdır. Aynı build hem panel hem public portal deneyimini taşır; domain/route guard'ları ile panel ve portal alanları ayrıştırılır.

Başlıca alanlar:

- Dashboard: katalog, ürün, müşteri, sipariş, ayar ve platform yönetim ekranları.
- Public portal: public katalog görüntüleme, token/giriş akışları, katalog chat ve checkout yüzeyleri.
- Catalog chat: stream contract parser, kullanıcı mesaj akışı ve hata/fallback durumları.
- Domain context: portal host ve panel host ayrımı.
- Feature flags: katalog-only, catalog-chat ve ileri paket davranışları.

Önemli frontend komutları:

```bash
cd frontend/katalogcu-frontend
npm run test:compile
npm run test:ci
npm run build
```

## Partalog AI Servisi

`partalog-ai/` FastAPI tabanlı AI servisidir. Backend bu servise katalog analizi, chat, embedding ve görsel/semantik arama işleri için bağlanır.

```text
partalog-ai/
├── api/          # HTTP endpoint modülleri
├── core/         # OCR, detector, rate limiter gibi düşük seviye bileşenler
├── services/     # chat, embedding, vector search, prompt/policy, visual search
├── eval/         # kalite eval case'leri ve ölçüm dokümantasyonu
└── tests/        # Python servis testleri
```

Başlıca AI yetenekleri:

- Katalog parçaları için exact-code, metin ve embedding tabanlı arama.
- Public katalog chat akışı ve streaming cevaplar.
- OCR ve hotspot/visual search destekleri.
- Embedding cache, retry, in-flight request coalescing ve quota guard davranışları.
- Eval dosyaları ile semantic, behavior, context ve feedback regression ölçümü.

## Deploy ve Operasyon

`deploy/google-cloud/` canlıya alma ve staging akışlarını belgeleyen runbook'ları içerir:

- `catalog-only-cloud-run.md`: catalog-only Cloud Run deploy.
- `catalog-chat-cloud-run.md`: catalog + chat production deploy.
- `catalog-chat-staging-cloud-run.md`: staging kurulumu.
- `portal-panel-release-checklist.md`: portal/panel domain ayrımı ve postdeploy smoke.
- `monitoring/notification-channel-runbook.md`: monitoring notification channel kurulumu.
- `staging-observability.md`: staging gözlemlenebilirlik kontrolleri.

Backend altında ayrıca şu operasyon dokümanları bulunur:

- `DOCKER_ORCHESTRATION.md`: lokal Docker Compose servisleri.
- `SMOKE_TESTS.md`: public checkout ve full-stack smoke testleri.
- `MIGRATION_DISCIPLINE.md`: EF migration güvenlik disiplini.
- `CATALOG_ONLY_PROD_CHECKLIST.md`: catalog-only release kontrol listesi.
- `CATALOG_ONLY_RELEASE_GO_NO_GO.md`: release karar formu.

## CI ve Kalite Kapıları

`.github/workflows/` altında proje kalitesini koruyan workflow'lar yer alır:

- `backend-migration-discipline.yml`
- `catalog-only-preflight.yml`
- `chat-eval-gate.yml`
- `chat-eval-nightly.yml`
- `cqrs-handler-gate.yml`
- `public-checkout-smoke.yml`
- `public-load-baseline-gate.yml`
- `regression-smoke-gate.yml`

Bu kapılar migration drift, catalog-only readiness, chat eval, public smoke ve load baseline doğrulamalarını ayrı ayrı denetlemek için tasarlanmıştır.

## Envanter ve Temizlik Durumu

Detaylı dosya envanteri için `PROJECT_FILE_REPORT.md` kullanılır. Güncel temizlik hedefi:

- Kaynak kodu, operasyon scriptleri ve gerekli test asset'leri repoda kalır.
- Runtime çıktıları, lokal secret/env dosyaları, geçici PDF/görsel çıktıları ve eski debug dosyaları repodan uzak tutulur.
- Eski WooCommerce entegrasyonu ve güncel olmayan raporlar temizlenmiştir.

## İlk Bakılacak Dosyalar

| İhtiyaç | Dosya |
|---|---|
| Projeyi anlamak | `README.md` |
| Teknik modülleri gezmek | `PROJE_YAPISI.md` |
| GitHub vitrin özeti | `PROJE_SUNUM_RAPORU.md` |
| Dosya envanteri | `PROJECT_FILE_REPORT.md` |
| Lokal servisleri başlatmak | `backend/DOCKER_ORCHESTRATION.md` |
| Release güvenliği | `backend/CATALOG_ONLY_PROD_CHECKLIST.md` |
| Portal/panel deploy | `deploy/google-cloud/portal-panel-release-checklist.md` |
