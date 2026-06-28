# Katalogcu - Dosya Envanteri ve Temizlik Raporu

Oluşturma zamanı: 2026-06-24 10:25:07

Bu rapor, repodaki kaynak kodu, dokümantasyon, operasyon scriptleri ve test varlıklarını görünür kılmak için hazırlandı. GitHub vitrini açısından amacı şudur: projede hangi dosyaların bilinçli olarak tutulduğunu, hangi eski/geçici dosyaların temizlendiğini ve yayın öncesi hangi adayların tekrar gözden geçirilmesi gerektiğini açıkça göstermek.

## Kısa Sonuç

- Ana envanter dosya sayısı: **1332**
- Ana envanter toplam boyutu: **6.6MB**
- Git tarafından takip edilen dosya: **1332**
- Runtime upload klasörü boşaltıldı; `backend/Katalogcu.API/wwwroot/uploads` 0 dosya.
- Eski WooCommerce entegrasyonu, geçici çıktılar, manuel test/debug dosyaları, güncel olmayan YOLO dokümanı ve eski plan raporları temizlik kapsamına alındı.
- Ana kod gövdesi `backend`, `frontend`, `partalog-ai`, `deploy` ve `plans` altında toplanmış durumda.

## Vitrin İçin Okuma Notu

Bu dosya ürün tanıtımı değil, envanter ve hijyen raporudur. Projeyi ilk kez inceleyen biri için önerilen sıra:

1. `README.md`: ürün ve hızlı başlangıç.
2. `PROJE_SUNUM_RAPORU.md`: teknik/ürün sunum özeti.
3. `PROJE_YAPISI.md`: klasörler ve modül haritası.
4. `PROJECT_FILE_REPORT.md`: dosya envanteri ve kalan temizlik adayları.

## Modül Haritası

| Klasör | Rol | Not |
|---|---|---|
| `backend` | `.NET 9` API, domain, application, infrastructure, EF migrations, smoke/load scriptleri. | Ana iş mantığı ve veritabanı omurgası. Dosya: 866 |
| `partalog-ai` | FastAPI AI servisi. | Chatbot, embedding, OCR, YOLO/hotspot, Gemini analiz/eval akışları. Dosya: 307 |
| `frontend` | Angular müşteri/admin arayüzü. | Public katalog, panel, chat ve yönetim ekranları. Dosya: 142 |
| `deploy` | Deploy yardımcıları. | Canlı/staging operasyon dosyaları. Dosya: 12 |
| `CANLIYA_ALMA_PLANI.md` | Tekil/root proje dosyası. | Beraber incelenecek. Dosya: 1 |
| `CATALOG_CHAT_CANLIYA_ALMA_PLANI.md` | Tekil/root proje dosyası. | Beraber incelenecek. Dosya: 1 |
| `plans` | Ürün roadmap dokümanı. | Eski raporlar kaldırıldı; sadece roadmap kaldı. Dosya: 1 |
| `PROJE_YAPISI.md` | Tekil/root proje dosyası. | Beraber incelenecek. Dosya: 1 |
| `README.md` | Tekil/root proje dosyası. | Beraber incelenecek. Dosya: 1 |

## Kalan İnceleme Adayları

| Öncelik | Dosya/Klasör | Neden | Öneri |
|---|---|---|---|
| Yüksek | `partalog-ai/.env` | Gerçek secret/env olabilir. | Commit edilmemeli; lokal kullanım gerekiyorsa `.gitignore` kapsamında kalmalı. |
| Orta | `partalog-ai/train_dictionary.py` | Eski sanayi sözlüğü üreticisi; aktif runtime kullanmıyor gibi. | Sözlük eğitimi artık kullanılmayacaksa sonraki tur silinebilir. |
| Orta | `backend/pre_smoke_cleanup.sql` | Büyük DB dump/cleanup dosyası; referansı yok gibi. | Reset fixture olarak kullanılmıyorsa silinebilir. |

## Yayın Öncesi Netleştirme

- `LICENSE` dosyası seçilip eklenmeli.
- Demo ekran görüntüleri veya GIF'ler eklenecekse secretsız ve küçük boyutlu tutulmalı.
- Runtime çıktıları, lokal `.env`, `node_modules`, `venv`, geçici PDF/görsel çıktıları ve kişisel test dosyaları commit dışında kalmalı.
- Eski entegrasyon silmeleri ve dokümantasyon yenilemeleri ayrı commit'lerde tutulursa GitHub geçmişi daha okunur olur.

## Kategori Sayıları

| Kategori | Adet |
|---|---:|
| Görsel asset/test verisi | 369 |
| Application CQRS feature | 366 |
| EF migration | 89 |
| Backend servis | 80 |
| Backend C# kodu | 72 |
| Frontend Angular/TS | 35 |
| Frontend stil | 30 |
| Frontend template/static HTML | 29 |
| Domain entity | 27 |
| Backend API controller | 25 |
| Python AI servis | 25 |
| Dokümantasyon | 21 |
| Python AI test | 21 |
| Backend repository | 17 |
| JSON veri/konfig/eval | 15 |
| Python AI kodu | 15 |
| Deploy/Container/CI | 13 |
| Frontend test | 13 |
| Proje/Build tanımı | 12 |
| Script/operasyon aracı | 11 |
| Frontend servis | 10 |
| Shell script/operasyon | 8 |
| Python AI API endpoint | 7 |
| Diğer | 5 |
| Python AI eval aracı/verisi | 5 |
| Frontend guard | 4 |
| SQL/DB script | 2 |
| Frontend environment | 2 |
| Backend test | 1 |
| Üretilmiş/binary artifact | 1 |
| Frontend model | 1 |
| Frontend/helper JS | 1 |

## Uzantı Sayıları

| Uzantı | Adet |
|---|---:|
| `.cs` | 677 |
| `.jpg` | 297 |
| `.py` | 84 |
| `.png` | 68 |
| `.ts` | 65 |
| `.css` | 30 |
| `.html` | 29 |
| `.md` | 21 |
| `.json` | 12 |
| `.jsonl` | 9 |
| `.sh` | 8 |
| `.csproj` | 5 |
| `.yaml` | 4 |
| `.yml` | 3 |
| `Dockerfile` | 3 |
| `.svg` | 3 |
| `.txt` | 3 |
| `.sql` | 2 |
| `.zip` | 1 |
| `.sln` | 1 |
| `.cloudrun` | 1 |
| `.template` | 1 |
| `.conf` | 1 |
| `.ico` | 1 |
| `.js` | 1 |
| `.chat` | 1 |
| `.chat-local` | 1 |

## Ana Dosya Envanteri

| Dosya | Tür | Boyut | Satır | Ne yapıyor | Öneri | Git |
|---|---|---:|---:|---|---|---|
| `backend/CATALOG_ONLY_PROD_CHECKLIST.md` | Dokümantasyon | 3.5KB | 134 | Dokümantasyon/runbook: Catalog-Only Prod Checklist. | KORU/INCELE. | tracked |
| `backend/CATALOG_ONLY_RELEASE_GO_NO_GO.md` | Dokümantasyon | 2.9KB | 84 | Dokümantasyon/runbook: Catalog-Only Release Go / No-Go. | KORU/INCELE. | tracked |
| `backend/cloudbuild.api.yaml` | Deploy/Container/CI | 405B | 17 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `backend/db/read_heavy_index_audit.sql` | SQL/DB script | 2.9KB | 98 | SQL/DB script dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/docker-compose.catalog-only.yml` | Deploy/Container/CI | 3.3KB | 115 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `backend/docker-compose.chat-local.yml` | Deploy/Container/CI | 5.8KB | 181 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `backend/docker-compose.yml` | Deploy/Container/CI | 2.5KB | 102 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `backend/DOCKER_ORCHESTRATION.md` | Dokümantasyon | 1.0KB | 28 | Dokümantasyon/runbook: Docker Orchestration (MVP). | KORU/INCELE. | tracked |
| `backend/Katalogcu.API.Tests/Controllers/PolicyThresholdsControllerTests.cs` | Backend API controller | 2.1KB | 67 | HTTP endpointlerini yönetir. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Controllers/SelfServiceRegistrationControllerTests.cs` | Backend API controller | 704B | 28 | HTTP endpointlerini yönetir. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Features/Auth/SelectPlanCommandHandlerTests.cs` | Application CQRS feature | 3.0KB | 85 | Command/query/validator/dto işlevi. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Features/Customers/PortalCustomerAccessTests.cs` | Application CQRS feature | 7.7KB | 189 | Command/query/validator/dto işlevi. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Features/ExternalSites/ExternalSiteUrlSecurityValidatorTests.cs` | Application CQRS feature | 1.1KB | 33 | Command/query/validator/dto işlevi. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Features/PolicyThresholds/PolicyThresholdAccessServiceTests.cs` | Application CQRS feature | 6.2KB | 164 | Command/query/validator/dto işlevi. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Features/PolicyThresholds/PolicyThresholdAuditWriterTests.cs` | Application CQRS feature | 4.6KB | 119 | Command/query/validator/dto işlevi. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Features/PolicyThresholds/PolicyThresholdRulesTests.cs` | Application CQRS feature | 1.5KB | 46 | Command/query/validator/dto işlevi. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Infrastructure/PolicyRegressionCaseFileStoreTests.cs` | Backend test | 3.0KB | 74 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Katalogcu.API.Tests.csproj` | Proje/Build tanımı | 749B | 23 | Proje/Build tanımı dosyası. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/AesGcmDataProtectionXmlEncryptorTests.cs` | Backend servis | 1.2KB | 32 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/AiCapacityGuardTests.cs` | Backend servis | 3.1KB | 101 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/ChatStreamProxyServiceTests.cs` | Backend servis | 11.3KB | 294 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/CloudRunIdentityTokenHandlerTests.cs` | Backend servis | 3.1KB | 87 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/DataProtectionServiceCollectionExtensionsTests.cs` | Backend servis | 1.7KB | 39 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/DistributedPublicChatRateLimiterTests.cs` | Backend servis | 833B | 26 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/ModuleFeatureGateMiddlewareTests.cs` | Backend servis | 3.9KB | 115 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/PartalogAiServiceTests.cs` | Backend servis | 3.2KB | 90 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/PolicyThresholdActorContextTests.cs` | Backend servis | 3.7KB | 127 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/PolicyThresholdEvaluationTokenServiceTests.cs` | Backend servis | 2.9KB | 98 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/SafeExternalHttpClientTests.cs` | Backend servis | 670B | 22 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API.Tests/Services/SigningSecretPolicyTests.cs` | Backend servis | 1.2KB | 37 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/Katalogcu.API/appsettings.example.json` | JSON veri/konfig/eval | 1.6KB | 70 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Contracts/Chat/AiChatRequestWithHistoryDto.cs` | Backend C# kodu | 259B | 9 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/AuthController.cs` | Backend API controller | 11.7KB | 300 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/CatalogItemsController.cs` | Backend API controller | 3.9KB | 130 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/CatalogPagesController.cs` | Backend API controller | 2.9KB | 90 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/CatalogsController.cs` | Backend API controller | 29.9KB | 766 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ChatController.cs` | Backend API controller | 20.0KB | 496 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ChatFeedbackController.cs` | Backend API controller | 2.9KB | 82 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/CompatibilityController.cs` | Backend API controller | 3.4KB | 103 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/CustomersController.cs` | Backend API controller | 18.3KB | 490 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ErpWebhookController.cs` | Backend API controller | 3.4KB | 98 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ExternalLinksController.cs` | Backend API controller | 2.0KB | 66 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ExternalMatchesController.cs` | Backend API controller | 9.8KB | 283 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ExternalSitesController.cs` | Backend API controller | 13.3KB | 371 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/FilesController.cs` | Backend API controller | 1.8KB | 53 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/FolderController.cs` | Backend API controller | 3.2KB | 101 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/HotspotsController.cs` | Backend API controller | 11.9KB | 327 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/OrdersController.cs` | Backend API controller | 13.0KB | 335 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/PlatformAuthController.cs` | Backend API controller | 2.1KB | 67 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/PlatformTenantsController.cs` | Backend API controller | 49.3KB | 1363 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/PolicyThresholdsController.cs` | Backend API controller | 11.6KB | 337 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/ProductsController.cs` | Backend API controller | 15.1KB | 395 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/SystemController.cs` | Backend API controller | 812B | 29 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/UsersController.cs` | Backend API controller | 4.6KB | 138 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Controllers/VisualFeedbackController.cs` | Backend API controller | 2.2KB | 62 | HTTP endpointlerini yönetir. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Dockerfile` | Deploy/Container/CI | 919B | 28 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `backend/Katalogcu.API/DTOs/AnalysisDtos.cs` | Backend C# kodu | 703B | 28 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Katalogcu.API.csproj` | Proje/Build tanımı | 2.2KB | 43 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `backend/Katalogcu.API/Program.cs` | Backend C# kodu | 23.6KB | 604 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Properties/launchSettings.json` | JSON veri/konfig/eval | 644B | 23 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/AesGcmDataProtectionXmlEncryptor.cs` | Backend servis | 3.4KB | 103 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/AiCapacityGuard.cs` | Backend servis | 25.7KB | 689 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/AiServiceOptions.cs` | Backend servis | 1.0KB | 23 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/AiUsageQuotaService.cs` | Backend servis | 11.4KB | 347 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogAiBackgroundProcessor.cs` | Backend servis | 1.5KB | 38 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogAiHangfireFilter.cs` | Backend servis | 5.3KB | 176 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogAiHangfireJob.cs` | Backend servis | 1.4KB | 39 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogAiProcessingOptions.cs` | Backend servis | 1.0KB | 33 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogCoverMetadataService.cs` | Backend servis | 2.0KB | 60 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogPageAccessTokenService.cs` | Backend servis | 5.9KB | 160 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogPageFileService.cs` | Backend servis | 1.7KB | 64 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogPdfPageService.cs` | Backend servis | 1.4KB | 49 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogPlanLimitMiddleware.cs` | Backend servis | 2.1KB | 72 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CatalogProcessorService.cs` | Backend servis | 971B | 33 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ChatFeedbackJsonlStore.cs` | Backend servis | 859B | 25 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ChatStreamEventContract.cs` | Backend servis | 10.8KB | 302 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ChatStreamProxyService.cs` | Backend servis | 6.0KB | 156 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CloudRunIdentityTokenHandler.cs` | Backend servis | 2.6KB | 76 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/CurrentUserService.cs` | Backend servis | 1.0KB | 36 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/DataProtectionKeyRingOptions.cs` | Backend servis | 502B | 13 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/DataProtectionServiceCollectionExtensions.cs` | Backend servis | 2.2KB | 55 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/DistributedPublicChatRateLimiter.cs` | Backend servis | 7.8KB | 229 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ErpGatewayOptions.cs` | Backend servis | 522B | 17 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ErpGatewayService.cs` | Backend servis | 4.0KB | 130 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ExcelService.cs` | Backend servis | 12.5KB | 323 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ExcludedStaticFileProvider.cs` | Backend servis | 1.8KB | 67 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ExternalLinkRecheckHangfireJob.cs` | Backend servis | 2.4KB | 60 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ExternalSiteCrawlBackgroundProcessor.cs` | Backend servis | 1.7KB | 48 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ExternalSiteCrawlHangfireJob.cs` | Backend servis | 602B | 22 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/FileStorageOptions.cs` | Backend servis | 322B | 11 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/FileStoragePath.cs` | Backend servis | 858B | 30 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/FileStoragePathResolver.cs` | Backend servis | 2.6KB | 86 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/FormFileUploadAdapter.cs` | Backend servis | 434B | 18 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/GoogleCloudFileStorageService.cs` | Backend servis | 3.4KB | 102 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/HotspotDetectionService.cs` | Backend servis | 2.0KB | 66 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/IFileStorageService.cs` | Backend servis | 479B | 10 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/JwtSecretResolver.cs` | Backend servis | 638B | 27 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/JwtTokenService.cs` | Backend servis | 1.8KB | 49 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/LocalFileStorageService.cs` | Backend servis | 2.9KB | 82 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ModuleFeatureGateMiddleware.cs` | Backend servis | 2.4KB | 73 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PartalogAiService.cs` | Backend servis | 17.9KB | 477 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PdfService.cs` | Backend servis | 3.1KB | 79 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PlanLimitRules.cs` | Backend servis | 995B | 38 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PolicyThresholdActorContext.cs` | Backend servis | 2.1KB | 65 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PolicyThresholdEvaluationTokenService.cs` | Backend servis | 5.4KB | 184 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ProductFeatureOptions.cs` | Backend servis | 1.5KB | 40 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/ProductionReadinessService.cs` | Backend servis | 16.9KB | 419 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PublicAccessTokenService.cs` | Backend servis | 762B | 29 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PublicCatalogLinkService.cs` | Backend servis | 771B | 23 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/PublicLinkService.cs` | Backend servis | 16.7KB | 497 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/RedisDataProtectionXmlRepository.cs` | Backend servis | 1.7KB | 54 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/SigningSecretPolicy.cs` | Backend servis | 742B | 29 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/StoredFile.cs` | Backend servis | 275B | 13 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/UploadValidation.cs` | Backend servis | 4.2KB | 130 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/UserSuspensionMiddleware.cs` | Backend servis | 2.4KB | 73 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/Services/VisualFeedbackService.cs` | Backend servis | 1.5KB | 45 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/downloads/partalog-woocommerce.zip` | Üretilmiş/binary artifact | 15.1KB | - | Üretilmiş/binary artifact dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/1-081da3f6983842f48c7bfd34547408cd.png` | Görsel asset/test verisi | 48.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/11-4c489abe5694450b8ecfcc92ec5eff0a.png` | Görsel asset/test verisi | 14.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/12-9786ece046334592918ebcba68e7e2dc.png` | Görsel asset/test verisi | 17.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/13-08c463d2c935494f8ded416c653b9937.png` | Görsel asset/test verisi | 16.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/14-d590003a1bb74102b05b638f1e591341.png` | Görsel asset/test verisi | 42.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/17-f0e5886427934ceaa535ea5d9d17b784.png` | Görsel asset/test verisi | 70.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/18-a95909c757c94b26b64bf45097ff40d9.png` | Görsel asset/test verisi | 54.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/2-53661b045f524c499cd6d24af9df028d.png` | Görsel asset/test verisi | 14.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/2-80dc87af4fe04bbe9337a658e64d0c5f.png` | Görsel asset/test verisi | 15.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/20-78af83adeb224b80aba701f30b4a1b98.png` | Görsel asset/test verisi | 36.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/21-9547be1d5cd749b6b04ef3c05cd2d57c.png` | Görsel asset/test verisi | 9.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/21-af0411ccb2a4461b998a5d769229d336.png` | Görsel asset/test verisi | 4.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/22-82ad6c93653a4fb09d00a1fbc15610b5.png` | Görsel asset/test verisi | 20.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/23-fee5deacdbe44a40b99cdd41eee2d259.png` | Görsel asset/test verisi | 36.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/25-b450bf7217ae4adcb6d7dc22c7d3221f.png` | Görsel asset/test verisi | 21.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/28-55bd5728b7214493b68e3bef1e002299.png` | Görsel asset/test verisi | 19.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/32-a62e0689ed2c4314a7d8c6b4bdecf291.png` | Görsel asset/test verisi | 9.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/34-af838d1bcd9847faacf467025e033628.png` | Görsel asset/test verisi | 40.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/38-41b21417d10d406da56365f5c47a7be7.png` | Görsel asset/test verisi | 11.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/39-0e6506f62396471ea25a6e7f9556eb5c.png` | Görsel asset/test verisi | 9.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/4-6e41c38cee074a8cb433f621ac630d17.png` | Görsel asset/test verisi | 43.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/42-59d396ac8877454f92b7c51a6df34282.png` | Görsel asset/test verisi | 8.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/7-68eaceb330cc4b01814e2f927c4cb22d.png` | Görsel asset/test verisi | 46.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/3/9-c12e2db5105945eab38639f781f718aa.png` | Görsel asset/test verisi | 18.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/48-020ff58d2c324496bac84ec00a160888.png` | Görsel asset/test verisi | 4.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/51-a8d95262e0844627857a6fd198527ae1.png` | Görsel asset/test verisi | 6.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/52-02c89e2ff7934c2fb475a3428e9d5d94.png` | Görsel asset/test verisi | 24.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/54-299b665f0fef4768ae9fdb28f1e7a696.png` | Görsel asset/test verisi | 12.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/55-b0324dc346f747ae8a881a2e1980f930.png` | Görsel asset/test verisi | 18.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/56-52e30e8291f7484da21329653bf56220.png` | Görsel asset/test verisi | 22.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/57-401733a4550d4799944d866595efb5cb.png` | Görsel asset/test verisi | 24.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/58-6f2501ac141c407b8a87ee2677563d00.png` | Görsel asset/test verisi | 38.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/60-123163a72c344858a2a9cb5d25ba146e.png` | Görsel asset/test verisi | 28.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/63-783797c10ff940aab7d264813ab0518a.png` | Görsel asset/test verisi | 23.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/66-f0f60a03976f432385e54543f7f8953b.png` | Görsel asset/test verisi | 5.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/67-b4c329af96854bd58ddb16c29a4c961a.png` | Görsel asset/test verisi | 29.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/69-f5f7dc01e27c41bcb2df62bfa0776ba4.png` | Görsel asset/test verisi | 8.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/70-65f9bb50463a4bb091f1c4415d5705ed.png` | Görsel asset/test verisi | 19.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/71-9fc5661f4a7f4833841c778e64a1bb44.png` | Görsel asset/test verisi | 6.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/73-ed8f695108914efab6b910dcdbff6e3f.png` | Görsel asset/test verisi | 8.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/75-ed4a3fd6d7364b9f9a8094d77bbed19c.png` | Görsel asset/test verisi | 21.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/76-36951d6c21244b1ba5d1b1583e2461c3.png` | Görsel asset/test verisi | 30.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/80-123c6ff4570b499e89316deba2d32a76.png` | Görsel asset/test verisi | 37.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/81-94c7b3c3626e43f39961d6d06b15ed05.png` | Görsel asset/test verisi | 39.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/82-3939e806f4c541bebf92ffc313de013c.png` | Görsel asset/test verisi | 19.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/83-ea1e868bca6b4b208c4cf76381694420.png` | Görsel asset/test verisi | 25.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/5/86-bd993dbe54e546b18fcd293b3569d576.png` | Görsel asset/test verisi | 17.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/01-fdcdcdc5b4f341f2b4641bfa3f70a669.png` | Görsel asset/test verisi | 15.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/12-46bfbf880da744d28d7fbfa4c916ff45.png` | Görsel asset/test verisi | 6.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/14-e089c004b72344c6bd2fdb171d80138a.png` | Görsel asset/test verisi | 63.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/25-261bf308a8c04154bf005c458ea81b38.png` | Görsel asset/test verisi | 8.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/31-c435f9e94bd74629ba8073da41f70e94.png` | Görsel asset/test verisi | 48.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/32-7bd30553a0ec49ad8b1ff6db85e74732.png` | Görsel asset/test verisi | 9.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/33-dd718c0960a74204a34c89235be948b6.png` | Görsel asset/test verisi | 19.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/34-3c31240566ba4da7a925eff0debc8b44.png` | Görsel asset/test verisi | 42.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/35-198436006d46429e9ff879c9dedef95f.png` | Görsel asset/test verisi | 16.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/36-b940861a0994476ca658e4daf9bef470.png` | Görsel asset/test verisi | 33.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/37-ae9702ced7a7453b8be03ebbe6cbb902.png` | Görsel asset/test verisi | 6.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/38-5a059a9e7ad84df29a19917a692a17b1.png` | Görsel asset/test verisi | 29.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/39-e5ae1b93ae4b45bdb83cee21f80b72ec.png` | Görsel asset/test verisi | 22.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/40-12286127c2304f129b72e3dcf65daac7.png` | Görsel asset/test verisi | 26.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/41-255a498fb7b342bca177b003d67bd5a5.png` | Görsel asset/test verisi | 51.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/43-d71d619aafec4937acf54a662728af48.png` | Görsel asset/test verisi | 2.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/44-4a6664f42d034374b1954791abef4028.png` | Görsel asset/test verisi | 2.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/5-b60646e3d4914859bf0bfb6214d11b11.png` | Görsel asset/test verisi | 27.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/6-9d66e5edf0384ed3bb0d1bfadfbf3a64.png` | Görsel asset/test verisi | 3.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/7-1d0cf75122b84291954a79816e1fadb1.png` | Görsel asset/test verisi | 22.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/1b021349-a79e-4364-ba86-7775bee2b457/7/8-acd73e4d00bc4d678ecaeaf05724f9df.png` | Görsel asset/test verisi | 2.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/10-5fe02b071f894693b82eb4cccd8f21f0.jpg` | Görsel asset/test verisi | 827B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/11-3bfc095b634349f9b1ec08baf4442902.jpg` | Görsel asset/test verisi | 717B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/12-1d22f9da1d874e498e2b843007a378cd.jpg` | Görsel asset/test verisi | 707B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/13-c13d3a533e1b42f981b7602592a991c8.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/14-c9164e80a8f744f9a1669149526b05c7.jpg` | Görsel asset/test verisi | 1.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/15-508243a0a54b4f339e2043f409cd1a2e.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/16-84f34fc1dc7d49399552aee50e150386.jpg` | Görsel asset/test verisi | 2.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/17-22ab19facfbc476e89f25a47fd53c560.jpg` | Görsel asset/test verisi | 1.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/18-54efd6f840a646bdab917a3422be4218.jpg` | Görsel asset/test verisi | 1014B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/19-4c96e2d91aa5430eb2dcc25743111855.jpg` | Görsel asset/test verisi | 2.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/2-2a26304bba46407fb6690d0bccd455e7.jpg` | Görsel asset/test verisi | 2.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/20-1aeaa8d87a8140148304124fcc62e8dc.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/21-b807b27f9a1b4cd09d011a4c9dcc912e.jpg` | Görsel asset/test verisi | 771B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/22-0406e163cbb041638771263461b0c0d8.jpg` | Görsel asset/test verisi | 1.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/23-643cf225283b4947b5c5f2426c29ab2e.jpg` | Görsel asset/test verisi | 1.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/23-b31efa5e3cd94c309b53b44ee71b49b5.jpg` | Görsel asset/test verisi | 1.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/24-2bec9849e8bd4c1ab42e7e194ab6f7e5.jpg` | Görsel asset/test verisi | 891B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/25-a5dbe9d0d69b4379994208c07d52ea03.jpg` | Görsel asset/test verisi | 11.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/26-f20e9538c25e4b70bef0eadc17c9cae7.jpg` | Görsel asset/test verisi | 11.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/3-4-370e945aad25425f9438663244539af1.jpg` | Görsel asset/test verisi | 11.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/5-7fb13eb4b55344bd89ba6670624094db.jpg` | Görsel asset/test verisi | 908B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/5-dd88feb741ac4128b077e011ec189caa.jpg` | Görsel asset/test verisi | 1.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/6-aae6ae8e9bc54eeeb309f25c1562d2f7.jpg` | Görsel asset/test verisi | 759B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/7-d8d2fee701d1429983dd13e563af01b3.jpg` | Görsel asset/test verisi | 1.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/3/8-a1273efcd3aa4da29691b286b72a1498.jpg` | Görsel asset/test verisi | 3.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/1-bb80ed5fbd354ad7a97b2a49215bd123.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/10-a3ca06aecee84f8d99e170048ba281f9.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/11-a473336508bd479c8a74b6f5697e97c1.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/12-d586ab24e7d34cd090428760c68d0f16.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/13-2dde71d1f52b4fed8ff488c0b52ccfc6.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/14-4b01d9f3f49444f3b321d4ff448974f3.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/15-c65829955f044e42993952aff3067ea1.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/16-cfc1b6acc82142ecb2d62a2fa8760712.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/17-517fe3b3527347f49e96024af8d2f990.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/18-265d03359c804aabadd156cd7fa19789.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/19-6355a36b29e948d49061d6454bcb3269.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/2-a5f04a8ccb1c45ac8cbdd92feff6c334.jpg` | Görsel asset/test verisi | 1009B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/20-c54d11091a464b3795f74cbeb5041d82.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/21-2280d0fcb1e340b19309814e36fa7c55.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/22-a4a4dbb624544e64a744ba55c4e2071b.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/23-a18eb1b8ad10448d9c2df43f7b6c414c.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/24-20d78508d68b45f2890a313df6cf6d77.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/25-15be37a82f3049c1823629d1bdffdf68.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/26-b55766b91f2c4853beb498b34887061c.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/3-17bc9c945461442e9bfa2768122dc378.jpg` | Görsel asset/test verisi | 979B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/4-406dbd614c6343bebd9a317091ff251d.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/5-f29b5ba1b0624a3d83de939cdbe96f37.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/6-24fd0b4ff9204fe28eee1eeb9e301370.jpg` | Görsel asset/test verisi | 1016B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/7-cb4dd83f42944750b024b969fc691dc3.jpg` | Görsel asset/test verisi | 1012B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/8-68f4ec0f3fb84e6fa961a56259257343.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/4/9-a0df5cb3b8c6471b8b176380e3e9043f.jpg` | Görsel asset/test verisi | 961B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/1.2-55be8b0a5877434c880eb926b17c6543.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/1.2-c0845574631947e5a449568be57e06cf.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/10-60ee246a98724cef975a65f203c6c957.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/11-8883d77cc9fb43ccb67ecb0000dc6ef4.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/12-e9646d04e1d145d3a43e7bbb111e9385.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/13-cef3575aed9b49309b7aa6f6aef38279.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/14-69627d6cc681478a98b8fae98c6c412a.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/15-760de9fb8c954bbe86fc9193ece18369.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/16-5f1e8200687d4cb69e889964aa1cca89.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/17-245be8eaead04b68bb2c7113c50fa523.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/18-5c17ee4f411b45878ecd9c44705c64e1.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/19-632cce849f074fe6907fd3c0d1fd1727.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/20-8f3f58cf12c54eee90ac5e40d34acbb4.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/21-6d6431bb479a462fa53922615a4fa9a9.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/22-a15a1dfcb2fc4d1d8f0e58f6065f2120.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/23-7624f598b47b40ad9b3df92152120208.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/24-0f8eec311d31468d8ca8057243b6f9eb.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/25-78c97cac8b5f4d10884b33389c5c8e59.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/26-e731429b0b654aca8ca5904e17291622.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/3-4-24387025001d425489ce67451c615da9.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/3-4-c32a0fcc064143b396340a93634e9d12.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/5-3b9897f0d09741f7ac0df796c489c16a.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/6-b5689a8da1514c58bb3c6069c9ae0642.jpg` | Görsel asset/test verisi | 639B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/7-114b8b5c33bd420a832b982d9f2fbd8a.jpg` | Görsel asset/test verisi | 639B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/8-67c850325686421292a16df4e485d180.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.API/wwwroot/static/visual-parts/536821b6-532e-4185-89dd-69629b8e83b4/6/9-7951aca57738412885205b434790bba5.jpg` | Görsel asset/test verisi | 639B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Behaviors/ValidationBehavior.cs` | Backend C# kodu | 1.2KB | 41 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Exceptions/CatalogAiRetryableException.cs` | Backend C# kodu | 344B | 12 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IAuthRepository.cs` | Backend C# kodu | 519B | 16 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogAiBackgroundProcessor.cs` | Backend C# kodu | 179B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogAiJobRepository.cs` | Backend C# kodu | 954B | 21 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogCoverMetadataService.cs` | Backend C# kodu | 250B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogExternalMatchRepository.cs` | Backend C# kodu | 3.0KB | 29 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogExternalMatchReviewService.cs` | Backend C# kodu | 741B | 25 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogExternalMatchService.cs` | Backend C# kodu | 794B | 23 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogPageFileService.cs` | Backend C# kodu | 190B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogPdfPageService.cs` | Backend C# kodu | 219B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogProcessingRepository.cs` | Backend C# kodu | 844B | 20 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICatalogRepository.cs` | Backend C# kodu | 5.6KB | 135 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IChatFeedbackStore.cs` | Backend C# kodu | 225B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IChatQueryService.cs` | Backend C# kodu | 1.0KB | 29 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICompatibilityRepository.cs` | Backend C# kodu | 846B | 22 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICurrentUserService.cs` | Backend C# kodu | 182B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ICustomerRepository.cs` | Backend C# kodu | 1.6KB | 42 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IErpGatewayService.cs` | Backend C# kodu | 298B | 10 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IErpInventorySnapshotRepository.cs` | Backend C# kodu | 1011B | 33 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalLinkPublishingService.cs` | Backend C# kodu | 746B | 13 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalProductNormalizer.cs` | Backend C# kodu | 306B | 11 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalProductUpsertService.cs` | Backend C# kodu | 504B | 17 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalSiteCrawlBackgroundProcessor.cs` | Backend C# kodu | 198B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalSiteCrawlOrchestrator.cs` | Backend C# kodu | 178B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalSiteFetchCrawler.cs` | Backend C# kodu | 242B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalSitePlaywrightCrawler.cs` | Backend C# kodu | 247B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IExternalSiteRepository.cs` | Backend C# kodu | 1.2KB | 18 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IFolderRepository.cs` | Backend C# kodu | 842B | 22 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IHotspotDetectionService.cs` | Backend C# kodu | 326B | 12 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IHotspotRepository.cs` | Backend C# kodu | 1002B | 26 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IJwtTokenService.cs` | Backend C# kodu | 162B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IManualImportFileRepository.cs` | Backend C# kodu | 426B | 10 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IManualImportService.cs` | Backend C# kodu | 373B | 13 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IOrderRepository.cs` | Backend C# kodu | 1.9KB | 49 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPartalogAiService.cs` | Backend C# kodu | 1.0KB | 25 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPolicyRegressionCaseStore.cs` | Backend C# kodu | 763B | 24 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPolicyThresholdAccessService.cs` | Backend C# kodu | 400B | 13 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPolicyThresholdAuditWriter.cs` | Backend C# kodu | 320B | 13 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPolicyThresholdRepository.cs` | Backend C# kodu | 1.3KB | 36 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPublicAccessTokenService.cs` | Backend C# kodu | 195B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IPublicCatalogLinkService.cs` | Backend C# kodu | 300B | 7 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/ISafeExternalHttpClient.cs` | Backend C# kodu | 278B | 10 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IStockRepository.cs` | Backend C# kodu | 2.2KB | 56 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IUserRepository.cs` | Backend C# kodu | 571B | 17 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Interfaces/IVisualFeedbackService.cs` | Backend C# kodu | 259B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/CatalogCoverMetadataDto.cs` | Backend C# kodu | 284B | 9 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/CatalogExternalMatchScoring.cs` | Backend C# kodu | 487B | 13 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/CatalogListItemDto.cs` | Backend C# kodu | 719B | 21 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/CatalogRecentSummary.cs` | Backend C# kodu | 324B | 10 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/CatalogTopViewedSummary.cs` | Backend C# kodu | 275B | 9 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/ErpGatewayModels.cs` | Backend C# kodu | 884B | 24 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/ExternalLinkHealthRefreshResult.cs` | Backend C# kodu | 471B | 13 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/ExternalSiteFetchResult.cs` | Backend C# kodu | 1.5KB | 41 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/OperationResult.cs` | Backend C# kodu | 694B | 22 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/PublicAccessPayloadDto.cs` | Backend C# kodu | 196B | 7 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/PublicLinkStateDto.cs` | Backend C# kodu | 188B | 7 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/PublishedExternalLinkDto.cs` | Backend C# kodu | 618B | 16 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Models/UploadedFile.cs` | Backend C# kodu | 1.8KB | 63 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Options/RegistrationOptions.cs` | Backend C# kodu | 214B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Common/Security/UserPasswordHasher.cs` | Backend C# kodu | 1.7KB | 50 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/DependencyInjection.cs` | Backend C# kodu | 931B | 25 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Ai/Common/AiDtos.cs` | Application CQRS feature | 3.4KB | 131 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/CancelPlan/CancelPlanCommand.cs` | Application CQRS feature | 258B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/CancelPlan/CancelPlanCommandHandler.cs` | Application CQRS feature | 2.2KB | 59 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/CancelPlan/CancelPlanCommandValidator.cs` | Application CQRS feature | 235B | 10 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/Login/LoginCommand.cs` | Application CQRS feature | 231B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/Login/LoginCommandHandler.cs` | Application CQRS feature | 2.5KB | 66 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/Login/LoginCommandValidator.cs` | Application CQRS feature | 422B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/Login/LoginResponse.cs` | Application CQRS feature | 259B | 9 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/SelectPlan/SelectPlanCommand.cs` | Application CQRS feature | 272B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/SelectPlan/SelectPlanCommandHandler.cs` | Application CQRS feature | 2.5KB | 67 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/SelectPlan/SelectPlanCommandValidator.cs` | Application CQRS feature | 353B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/UpdateMe/UpdateMeCommand.cs` | Application CQRS feature | 352B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/UpdateMe/UpdateMeCommandHandler.cs` | Application CQRS feature | 2.5KB | 65 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Commands/UpdateMe/UpdateMeCommandValidator.cs` | Application CQRS feature | 434B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Common/AuthUserDto.cs` | Application CQRS feature | 766B | 19 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Queries/GetMe/GetMeQuery.cs` | Application CQRS feature | 245B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Auth/Queries/GetMe/GetMeQueryHandler.cs` | Application CQRS feature | 1.8KB | 50 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/CreateCatalogItem/CreateCatalogItemCommand.cs` | Application CQRS feature | 438B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/CreateCatalogItem/CreateCatalogItemCommandHandler.cs` | Application CQRS feature | 2.2KB | 57 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/CreateCatalogItem/CreateCatalogItemCommandValidator.cs` | Application CQRS feature | 772B | 28 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/DeleteCatalogItem/DeleteCatalogItemCommand.cs` | Application CQRS feature | 256B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/DeleteCatalogItem/DeleteCatalogItemCommandHandler.cs` | Application CQRS feature | 1.1KB | 28 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/DeleteCatalogItem/DeleteCatalogItemCommandValidator.cs` | Application CQRS feature | 384B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/UpdateCatalogItem/UpdateCatalogItemCommand.cs` | Application CQRS feature | 422B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/UpdateCatalogItem/UpdateCatalogItemCommandHandler.cs` | Application CQRS feature | 1.5KB | 35 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Commands/UpdateCatalogItem/UpdateCatalogItemCommandValidator.cs` | Application CQRS feature | 725B | 27 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/CatalogItems/Common/CatalogItemMapper.cs` | Application CQRS feature | 636B | 23 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/ClearCatalogPageData/ClearCatalogPageDataCommand.cs` | Application CQRS feature | 421B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/ClearCatalogPageData/ClearCatalogPageDataCommandHandler.cs` | Application CQRS feature | 1.6KB | 38 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/ClearCatalogPageData/ClearCatalogPageDataCommandValidator.cs` | Application CQRS feature | 441B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/CompleteCatalogUpload/CompleteCatalogUploadCommand.cs` | Application CQRS feature | 490B | 16 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/CompleteCatalogUpload/CompleteCatalogUploadCommandHandler.cs` | Application CQRS feature | 2.4KB | 62 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/CompleteCatalogUpload/CompleteCatalogUploadCommandValidator.cs` | Application CQRS feature | 392B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/CreateCatalog/CreateCatalogCommand.cs` | Application CQRS feature | 373B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/CreateCatalog/CreateCatalogCommandHandler.cs` | Application CQRS feature | 1.7KB | 45 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/CreateCatalog/CreateCatalogCommandValidator.cs` | Application CQRS feature | 385B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/DeleteCatalog/DeleteCatalogCommand.cs` | Application CQRS feature | 240B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/DeleteCatalog/DeleteCatalogCommandHandler.cs` | Application CQRS feature | 1.7KB | 38 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/DeleteCatalog/DeleteCatalogCommandValidator.cs` | Application CQRS feature | 360B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/FailCatalogUpload/FailCatalogUploadCommand.cs` | Application CQRS feature | 416B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/FailCatalogUpload/FailCatalogUploadCommandHandler.cs` | Application CQRS feature | 1.3KB | 34 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/FailCatalogUpload/FailCatalogUploadCommandValidator.cs` | Application CQRS feature | 376B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/MoveCatalog/MoveCatalogCommand.cs` | Application CQRS feature | 435B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/MoveCatalog/MoveCatalogCommandHandler.cs` | Application CQRS feature | 1.6KB | 42 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/MoveCatalog/MoveCatalogCommandValidator.cs` | Application CQRS feature | 485B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/ProcessCatalogPages/ProcessCatalogPagesCommand.cs` | Application CQRS feature | 603B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/ProcessCatalogPages/ProcessCatalogPagesCommandHandler.cs` | Application CQRS feature | 10.6KB | 266 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/ProcessCatalogPages/ProcessCatalogPagesCommandValidator.cs` | Application CQRS feature | 331B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/PublishCatalog/PublishCatalogCommand.cs` | Application CQRS feature | 434B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/PublishCatalog/PublishCatalogCommandHandler.cs` | Application CQRS feature | 1.2KB | 33 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/PublishCatalog/PublishCatalogCommandValidator.cs` | Application CQRS feature | 364B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/RevokePublicToken/RevokePublicTokenCommand.cs` | Application CQRS feature | 302B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/RevokePublicToken/RevokePublicTokenCommandHandler.cs` | Application CQRS feature | 1.3KB | 36 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/RevokePublicToken/RevokePublicTokenCommandValidator.cs` | Application CQRS feature | 320B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/RotatePublicToken/RotatePublicTokenCommand.cs` | Application CQRS feature | 353B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/RotatePublicToken/RotatePublicTokenCommandHandler.cs` | Application CQRS feature | 2.4KB | 63 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/RotatePublicToken/RotatePublicTokenCommandValidator.cs` | Application CQRS feature | 320B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/StartCatalogAiProcess/StartCatalogAiProcessCommand.cs` | Application CQRS feature | 568B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/StartCatalogAiProcess/StartCatalogAiProcessCommandHandler.cs` | Application CQRS feature | 1.8KB | 42 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/StartCatalogAiProcess/StartCatalogAiProcessCommandValidator.cs` | Application CQRS feature | 392B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/TrackCatalogView/TrackCatalogViewCommand.cs` | Application CQRS feature | 447B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/TrackCatalogView/TrackCatalogViewCommandHandler.cs` | Application CQRS feature | 1.6KB | 44 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/TrackCatalogView/TrackCatalogViewCommandValidator.cs` | Application CQRS feature | 803B | 27 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/TrackStorefrontView/TrackStorefrontViewCommand.cs` | Application CQRS feature | 439B | 16 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/TrackStorefrontView/TrackStorefrontViewCommandHandler.cs` | Application CQRS feature | 1.6KB | 43 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Commands/TrackStorefrontView/TrackStorefrontViewCommandValidator.cs` | Application CQRS feature | 522B | 20 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Common/CatalogAiJobDtos.cs` | Application CQRS feature | 1.1KB | 32 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Common/CatalogPageItemDto.cs` | Application CQRS feature | 547B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Common/CatalogStatsDto.cs` | Application CQRS feature | 859B | 20 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Common/PublicStorefrontDto.cs` | Application CQRS feature | 465B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Common/PublicTokenStatusDto.cs` | Application CQRS feature | 181B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Common/RotatePublicTokenDto.cs` | Application CQRS feature | 236B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogAiJobs/GetCatalogAiJobsQuery.cs` | Application CQRS feature | 312B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogAiJobs/GetCatalogAiJobsQueryHandler.cs` | Application CQRS feature | 1.1KB | 28 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogAiJobs/GetCatalogAiJobsQueryValidator.cs` | Application CQRS feature | 459B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogById/GetCatalogByIdQuery.cs` | Application CQRS feature | 357B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogById/GetCatalogByIdQueryHandler.cs` | Application CQRS feature | 1.1KB | 33 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogById/GetCatalogByIdQueryValidator.cs` | Application CQRS feature | 494B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogPageItems/GetCatalogPageItemsQuery.cs` | Application CQRS feature | 463B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogPageItems/GetCatalogPageItemsQueryHandler.cs` | Application CQRS feature | 2.6KB | 69 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogPageItems/GetCatalogPageItemsQueryValidator.cs` | Application CQRS feature | 560B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogStats/GetCatalogStatsQuery.cs` | Application CQRS feature | 290B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogStats/GetCatalogStatsQueryHandler.cs` | Application CQRS feature | 3.4KB | 67 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetCatalogStats/GetCatalogStatsQueryValidator.cs` | Application CQRS feature | 369B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetMyCatalogs/GetMyCatalogsQuery.cs` | Application CQRS feature | 272B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetMyCatalogs/GetMyCatalogsQueryHandler.cs` | Application CQRS feature | 867B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetMyCatalogs/GetMyCatalogsQueryValidator.cs` | Application CQRS feature | 361B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetMyCatalogsPage/GetMyCatalogsPageQuery.cs` | Application CQRS feature | 349B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetMyCatalogsPage/GetMyCatalogsPageQueryHandler.cs` | Application CQRS feature | 1.3KB | 38 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetMyCatalogsPage/GetMyCatalogsPageQueryValidator.cs` | Application CQRS feature | 467B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicCatalogs/GetPublicCatalogsQuery.cs` | Application CQRS feature | 267B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicCatalogs/GetPublicCatalogsQueryHandler.cs` | Application CQRS feature | 871B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicCatalogsByUser/GetPublicCatalogsByUserQuery.cs` | Application CQRS feature | 342B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicCatalogsByUser/GetPublicCatalogsByUserQueryHandler.cs` | Application CQRS feature | 988B | 26 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicCatalogsByUser/GetPublicCatalogsByUserQueryValidator.cs` | Application CQRS feature | 395B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicStorefront/GetPublicStorefrontQuery.cs` | Application CQRS feature | 302B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicStorefront/GetPublicStorefrontQueryHandler.cs` | Application CQRS feature | 1.7KB | 43 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicStorefront/GetPublicStorefrontQueryValidator.cs` | Application CQRS feature | 379B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicToken/GetPublicTokenQuery.cs` | Application CQRS feature | 276B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicToken/GetPublicTokenQueryHandler.cs` | Application CQRS feature | 2.1KB | 57 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicToken/GetPublicTokenQueryValidator.cs` | Application CQRS feature | 301B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicTokenStatus/GetPublicTokenStatusQuery.cs` | Application CQRS feature | 305B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicTokenStatus/GetPublicTokenStatusQueryHandler.cs` | Application CQRS feature | 1.2KB | 31 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Catalogs/Queries/GetPublicTokenStatus/GetPublicTokenStatusQueryValidator.cs` | Application CQRS feature | 325B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/AskChat/AskChatCommand.cs` | Application CQRS feature | 724B | 21 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/AskChat/AskChatCommandHandler.cs` | Application CQRS feature | 22.6KB | 564 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/AskChat/AskChatCommandValidator.cs` | Application CQRS feature | 375B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/SaveChatFeedback/SaveChatFeedbackCommand.cs` | Application CQRS feature | 608B | 23 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/SaveChatFeedback/SaveChatFeedbackCommandHandler.cs` | Application CQRS feature | 2.1KB | 51 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/SaveChatFeedback/SaveChatFeedbackCommandValidator.cs` | Application CQRS feature | 523B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/SaveVisualFeedback/SaveVisualFeedbackCommand.cs` | Application CQRS feature | 489B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/SaveVisualFeedback/SaveVisualFeedbackCommandHandler.cs` | Application CQRS feature | 1.3KB | 34 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Commands/SaveVisualFeedback/SaveVisualFeedbackCommandValidator.cs` | Application CQRS feature | 744B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/ChatCompareGroupDto.cs` | Application CQRS feature | 225B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/ChatFeedbackEntry.cs` | Application CQRS feature | 708B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/ChatFeedbackRequestDto.cs` | Application CQRS feature | 459B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/ChatSourceInput.cs` | Application CQRS feature | 484B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/EnrichedPartDto.cs` | Application CQRS feature | 554B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/VisualFeedbackInputDto.cs` | Application CQRS feature | 526B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Common/VisualFeedbackResultDto.cs` | Application CQRS feature | 225B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Queries/ResolveChatCatalogAccess/ResolveChatCatalogAccessQuery.cs` | Application CQRS feature | 486B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Queries/ResolveChatCatalogAccess/ResolveChatCatalogAccessQueryHandler.cs` | Application CQRS feature | 2.3KB | 62 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Queries/ResolveChatCatalogAccess/ResolveChatCatalogAccessQueryValidator.cs` | Application CQRS feature | 352B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Queries/ResolveChatUser/ResolveChatUserQuery.cs` | Application CQRS feature | 405B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Queries/ResolveChatUser/ResolveChatUserQueryHandler.cs` | Application CQRS feature | 1.8KB | 48 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Chat/Queries/ResolveChatUser/ResolveChatUserQueryValidator.cs` | Application CQRS feature | 454B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Commands/CreateMachineModel/CreateMachineModelCommand.cs` | Application CQRS feature | 409B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Commands/CreateMachineModel/CreateMachineModelCommandHandler.cs` | Application CQRS feature | 1.6KB | 44 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Commands/CreateMachineModel/CreateMachineModelCommandValidator.cs` | Application CQRS feature | 507B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Commands/CreatePartCompatibilityRule/CreatePartCompatibilityRuleCommand.cs` | Application CQRS feature | 474B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Commands/CreatePartCompatibilityRule/CreatePartCompatibilityRuleCommandHandler.cs` | Application CQRS feature | 2.2KB | 52 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Commands/CreatePartCompatibilityRule/CreatePartCompatibilityRuleCommandValidator.cs` | Application CQRS feature | 875B | 20 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Common/CompatibilityDtos.cs` | Application CQRS feature | 833B | 23 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Common/CompatibilityMapper.cs` | Application CQRS feature | 1.2KB | 42 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Queries/GetMachineModels/GetMachineModelsQuery.cs` | Application CQRS feature | 308B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Queries/GetMachineModels/GetMachineModelsQueryHandler.cs` | Application CQRS feature | 1022B | 25 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Queries/GetPartCompatibilityRules/GetPartCompatibilityRulesQuery.cs` | Application CQRS feature | 355B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Compatibility/Queries/GetPartCompatibilityRules/GetPartCompatibilityRulesQueryHandler.cs` | Application CQRS feature | 1.1KB | 25 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/ConfirmPasswordReset/ConfirmCustomerPasswordResetCommand.cs` | Application CQRS feature | 422B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/ConfirmPasswordReset/ConfirmCustomerPasswordResetCommandHandler.cs` | Application CQRS feature | 3.2KB | 76 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/ConfirmPasswordReset/ConfirmCustomerPasswordResetCommandValidator.cs` | Application CQRS feature | 1.0KB | 28 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/CreatePublicCustomerMachine/CreatePublicCustomerMachineCommand.cs` | Application CQRS feature | 522B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/CreatePublicCustomerMachine/CreatePublicCustomerMachineCommandHandler.cs` | Application CQRS feature | 2.8KB | 75 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/CreatePublicCustomerMachine/CreatePublicCustomerMachineCommandValidator.cs` | Application CQRS feature | 749B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/DeletePublicCustomerMachine/DeletePublicCustomerMachineCommand.cs` | Application CQRS feature | 299B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/DeletePublicCustomerMachine/DeletePublicCustomerMachineCommandHandler.cs` | Application CQRS feature | 2.0KB | 56 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/DeletePublicCustomerMachine/DeletePublicCustomerMachineCommandValidator.cs` | Application CQRS feature | 451B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/PublicLogin/PublicCustomerLoginCommand.cs` | Application CQRS feature | 378B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/PublicLogin/PublicCustomerLoginCommandHandler.cs` | Application CQRS feature | 3.9KB | 92 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/PublicLogin/PublicCustomerLoginCommandValidator.cs` | Application CQRS feature | 841B | 24 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/PublicRegisterAccount/PublicRegisterCustomerAccountCommand.cs` | Application CQRS feature | 415B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/PublicRegisterAccount/PublicRegisterCustomerAccountCommandHandler.cs` | Application CQRS feature | 3.4KB | 86 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/PublicRegisterAccount/PublicRegisterCustomerAccountCommandValidator.cs` | Application CQRS feature | 923B | 26 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/RequestPasswordReset/RequestCustomerPasswordResetCommand.cs` | Application CQRS feature | 331B | 10 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/RequestPasswordReset/RequestCustomerPasswordResetCommandHandler.cs` | Application CQRS feature | 2.4KB | 59 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/RequestPasswordReset/RequestCustomerPasswordResetCommandValidator.cs` | Application CQRS feature | 771B | 20 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/RequestPasswordReset/RequestCustomerPasswordResetResponse.cs` | Application CQRS feature | 284B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/SetPortalCustomerAccess/SetPortalCustomerAccessCommand.cs` | Application CQRS feature | 369B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/SetPortalCustomerAccess/SetPortalCustomerAccessCommandHandler.cs` | Application CQRS feature | 2.4KB | 67 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/SetPortalCustomerAccess/SetPortalCustomerAccessCommandValidator.cs` | Application CQRS feature | 535B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/UpdatePublicCustomerMachine/UpdatePublicCustomerMachineCommand.cs` | Application CQRS feature | 540B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/UpdatePublicCustomerMachine/UpdatePublicCustomerMachineCommandHandler.cs` | Application CQRS feature | 2.7KB | 67 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/UpdatePublicCustomerMachine/UpdatePublicCustomerMachineCommandValidator.cs` | Application CQRS feature | 795B | 19 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/UpsertPortalCustomer/UpsertPortalCustomerCommand.cs` | Application CQRS feature | 490B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/UpsertPortalCustomer/UpsertPortalCustomerCommandHandler.cs` | Application CQRS feature | 4.4KB | 119 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Commands/UpsertPortalCustomer/UpsertPortalCustomerCommandValidator.cs` | Application CQRS feature | 963B | 26 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Common/CustomerAuthHelpers.cs` | Application CQRS feature | 2.3KB | 69 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Common/CustomerDtos.cs` | Application CQRS feature | 3.9KB | 104 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Common/CustomerMachineMapper.cs` | Application CQRS feature | 661B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Common/PublicCustomerAuthResponse.cs` | Application CQRS feature | 274B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetMyCustomers/GetMyCustomersQuery.cs` | Application CQRS feature | 296B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetMyCustomers/GetMyCustomersQueryHandler.cs` | Application CQRS feature | 2.0KB | 48 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerMachines/GetPublicCustomerMachinesQuery.cs` | Application CQRS feature | 366B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerMachines/GetPublicCustomerMachinesQueryHandler.cs` | Application CQRS feature | 1.4KB | 37 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerMe/GetPublicCustomerMeQuery.cs` | Application CQRS feature | 332B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerMe/GetPublicCustomerMeQueryHandler.cs` | Application CQRS feature | 1.4KB | 40 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerMe/GetPublicCustomerMeQueryValidator.cs` | Application CQRS feature | 500B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerOrderDetail/GetPublicCustomerOrderDetailQuery.cs` | Application CQRS feature | 388B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerOrderDetail/GetPublicCustomerOrderDetailQueryHandler.cs` | Application CQRS feature | 3.4KB | 87 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerOrderDetail/GetPublicCustomerOrderDetailQueryValidator.cs` | Application CQRS feature | 654B | 21 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerOrders/GetPublicCustomerOrdersQuery.cs` | Application CQRS feature | 367B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerOrders/GetPublicCustomerOrdersQueryHandler.cs` | Application CQRS feature | 1.8KB | 45 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Customers/Queries/GetPublicCustomerOrders/GetPublicCustomerOrdersQueryValidator.cs` | Application CQRS feature | 516B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/MarkApprovedExternalMatchBroken/MarkApprovedExternalMatchBrokenCommand.cs` | Application CQRS feature | 511B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/MarkApprovedExternalMatchBroken/MarkApprovedExternalMatchBrokenCommandHandler.cs` | Application CQRS feature | 1.6KB | 39 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/MarkApprovedExternalMatchBroken/MarkApprovedExternalMatchBrokenCommandValidator.cs` | Application CQRS feature | 372B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/RefreshApprovedExternalLinkHealth/RefreshApprovedExternalLinkHealthCommand.cs` | Application CQRS feature | 745B | 19 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/RefreshApprovedExternalLinkHealth/RefreshApprovedExternalLinkHealthCommandHandler.cs` | Application CQRS feature | 2.0KB | 47 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/RefreshApprovedExternalLinkHealth/RefreshApprovedExternalLinkHealthCommandValidator.cs` | Application CQRS feature | 380B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/RestoreApprovedExternalMatch/RestoreApprovedExternalMatchCommand.cs` | Application CQRS feature | 499B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/RestoreApprovedExternalMatch/RestoreApprovedExternalMatchCommandHandler.cs` | Application CQRS feature | 1.5KB | 39 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Commands/RestoreApprovedExternalMatch/RestoreApprovedExternalMatchCommandValidator.cs` | Application CQRS feature | 360B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Queries/GetPublishedExternalLinkByCatalogItem/GetPublishedExternalLinkByCatalogItemQuery.cs` | Application CQRS feature | 454B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Queries/GetPublishedExternalLinkByCatalogItem/GetPublishedExternalLinkByCatalogItemQueryHandler.cs` | Application CQRS feature | 1.5KB | 34 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Queries/GetPublishedExternalLinkByCatalogItem/GetPublishedExternalLinkByCatalogItemQueryValidator.cs` | Application CQRS feature | 395B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Queries/GetPublishedExternalLinksByCatalog/GetPublishedExternalLinksByCatalogQuery.cs` | Application CQRS feature | 459B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Queries/GetPublishedExternalLinksByCatalog/GetPublishedExternalLinksByCatalogQueryHandler.cs` | Application CQRS feature | 1.4KB | 34 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalLinks/Queries/GetPublishedExternalLinksByCatalog/GetPublishedExternalLinksByCatalogQueryValidator.cs` | Application CQRS feature | 379B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ApproveCatalogExternalMatch/ApproveCatalogExternalMatchCommand.cs` | Application CQRS feature | 516B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ApproveCatalogExternalMatch/ApproveCatalogExternalMatchCommandHandler.cs` | Application CQRS feature | 2.3KB | 50 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ApproveCatalogExternalMatch/ApproveCatalogExternalMatchCommandValidator.cs` | Application CQRS feature | 414B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ApproveCatalogExternalProductByUrl/ApproveCatalogExternalProductByUrlCommand.cs` | Application CQRS feature | 678B | 19 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ApproveCatalogExternalProductByUrl/ApproveCatalogExternalProductByUrlCommandHandler.cs` | Application CQRS feature | 4.0KB | 91 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ApproveCatalogExternalProductByUrl/ApproveCatalogExternalProductByUrlCommandValidator.cs` | Application CQRS feature | 987B | 21 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/BulkApproveCatalogExternalMatches/BulkApproveCatalogExternalMatchesCommand.cs` | Application CQRS feature | 471B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/BulkApproveCatalogExternalMatches/BulkApproveCatalogExternalMatchesCommandHandler.cs` | Application CQRS feature | 2.3KB | 58 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/BulkApproveCatalogExternalMatches/BulkApproveCatalogExternalMatchesCommandValidator.cs` | Application CQRS feature | 488B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/RejectCatalogExternalMatch/RejectCatalogExternalMatchCommand.cs` | Application CQRS feature | 467B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/RejectCatalogExternalMatch/RejectCatalogExternalMatchCommandHandler.cs` | Application CQRS feature | 2.0KB | 47 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/RejectCatalogExternalMatch/RejectCatalogExternalMatchCommandValidator.cs` | Application CQRS feature | 410B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ReplaceCatalogExternalMatchCandidates/ReplaceCatalogExternalMatchCandidatesCommand.cs` | Application CQRS feature | 595B | 16 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ReplaceCatalogExternalMatchCandidates/ReplaceCatalogExternalMatchCandidatesCommandHandler.cs` | Application CQRS feature | 4.2KB | 104 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/ReplaceCatalogExternalMatchCandidates/ReplaceCatalogExternalMatchCandidatesCommandValidator.cs` | Application CQRS feature | 451B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/StartCatalogExternalMatching/StartCatalogExternalMatchingCommand.cs` | Application CQRS feature | 515B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/StartCatalogExternalMatching/StartCatalogExternalMatchingCommandHandler.cs` | Application CQRS feature | 4.4KB | 112 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Commands/StartCatalogExternalMatching/StartCatalogExternalMatchingCommandValidator.cs` | Application CQRS feature | 415B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogApprovedExternalMatches/GetCatalogApprovedExternalMatchesQuery.cs` | Application CQRS feature | 1.2KB | 29 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogApprovedExternalMatches/GetCatalogApprovedExternalMatchesQueryHandler.cs` | Application CQRS feature | 2.2KB | 49 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogApprovedExternalMatches/GetCatalogApprovedExternalMatchesQueryValidator.cs` | Application CQRS feature | 377B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogAutoMatchedExternalMatches/GetCatalogAutoMatchedExternalMatchesQuery.cs` | Application CQRS feature | 1.2KB | 30 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogAutoMatchedExternalMatches/GetCatalogAutoMatchedExternalMatchesQueryHandler.cs` | Application CQRS feature | 2.4KB | 60 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogExternalMatchQueue/GetCatalogExternalMatchQueueQuery.cs` | Application CQRS feature | 1013B | 26 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogExternalMatchQueue/GetCatalogExternalMatchQueueQueryHandler.cs` | Application CQRS feature | 2.0KB | 46 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalMatches/Queries/GetCatalogExternalMatchQueue/GetCatalogExternalMatchQueueQueryValidator.cs` | Application CQRS feature | 357B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/CreateExternalSite/CreateExternalSiteCommand.cs` | Application CQRS feature | 365B | 10 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/CreateExternalSite/CreateExternalSiteCommandHandler.cs` | Application CQRS feature | 2.8KB | 77 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/CreateExternalSite/CreateExternalSiteCommandValidator.cs` | Application CQRS feature | 1.1KB | 24 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/DeleteExternalSite/DeleteExternalSiteCommand.cs` | Application CQRS feature | 239B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/DeleteExternalSite/DeleteExternalSiteCommandHandler.cs` | Application CQRS feature | 1.4KB | 35 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/DeleteExternalSite/DeleteExternalSiteCommandValidator.cs` | Application CQRS feature | 319B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/ExternalSiteUrlSecurityValidator.cs` | Application CQRS feature | 4.1KB | 136 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/ImportExternalSiteProductsFromFile/ImportExternalSiteProductsFromFileCommand.cs` | Application CQRS feature | 388B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/ImportExternalSiteProductsFromFile/ImportExternalSiteProductsFromFileCommandHandler.cs` | Application CQRS feature | 2.6KB | 72 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/ImportExternalSiteProductsFromFile/ImportExternalSiteProductsFromFileCommandValidator.cs` | Application CQRS feature | 874B | 19 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/MarkMissingExternalProductsInactive/MarkMissingExternalProductsInactiveCommand.cs` | Application CQRS feature | 325B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/MarkMissingExternalProductsInactive/MarkMissingExternalProductsInactiveCommandHandler.cs` | Application CQRS feature | 1.7KB | 39 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/MarkMissingExternalProductsInactive/MarkMissingExternalProductsInactiveCommandValidator.cs` | Application CQRS feature | 442B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/StartExternalSiteCrawl/StartExternalSiteCrawlCommand.cs` | Application CQRS feature | 462B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/StartExternalSiteCrawl/StartExternalSiteCrawlCommandHandler.cs` | Application CQRS feature | 2.1KB | 49 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/StartExternalSiteCrawl/StartExternalSiteCrawlCommandValidator.cs` | Application CQRS feature | 335B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/UpdateExternalSite/UpdateExternalSiteCommand.cs` | Application CQRS feature | 401B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/UpdateExternalSite/UpdateExternalSiteCommandHandler.cs` | Application CQRS feature | 2.9KB | 76 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Commands/UpdateExternalSite/UpdateExternalSiteCommandValidator.cs` | Application CQRS feature | 1.3KB | 29 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Common/ExternalSiteDtos.cs` | Application CQRS feature | 2.9KB | 81 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalProductsBySite/GetExternalProductsBySiteQuery.cs` | Application CQRS feature | 372B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalProductsBySite/GetExternalProductsBySiteQueryHandler.cs` | Application CQRS feature | 3.3KB | 76 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalProductsBySite/GetExternalProductsBySiteQueryValidator.cs` | Application CQRS feature | 453B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalSiteById/GetExternalSiteByIdQuery.cs` | Application CQRS feature | 308B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalSiteById/GetExternalSiteByIdQueryHandler.cs` | Application CQRS feature | 2.6KB | 62 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalSites/GetExternalSitesQuery.cs` | Application CQRS feature | 304B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetExternalSites/GetExternalSitesQueryHandler.cs` | Application CQRS feature | 2.5KB | 58 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetManualImportHistory/GetManualImportHistoryQuery.cs` | Application CQRS feature | 326B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetManualImportHistory/GetManualImportHistoryQueryHandler.cs` | Application CQRS feature | 1.8KB | 47 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/ExternalSites/Queries/GetManualImportHistory/GetManualImportHistoryQueryValidator.cs` | Application CQRS feature | 328B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Commands/CreateFolder/CreateFolderCommand.cs` | Application CQRS feature | 456B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Commands/CreateFolder/CreateFolderCommandHandler.cs` | Application CQRS feature | 1.9KB | 52 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Commands/CreateFolder/CreateFolderCommandValidator.cs` | Application CQRS feature | 353B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Commands/DeleteFolder/DeleteFolderCommand.cs` | Application CQRS feature | 360B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Commands/DeleteFolder/DeleteFolderCommandHandler.cs` | Application CQRS feature | 1.7KB | 42 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Commands/DeleteFolder/DeleteFolderCommandValidator.cs` | Application CQRS feature | 373B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Queries/GetMyFolders/GetMyFoldersQuery.cs` | Application CQRS feature | 407B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Queries/GetMyFolders/GetMyFoldersQueryHandler.cs` | Application CQRS feature | 1.5KB | 37 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Queries/GetPublicFoldersByUser/GetPublicFoldersByUserQuery.cs` | Application CQRS feature | 500B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Queries/GetPublicFoldersByUser/GetPublicFoldersByUserQueryHandler.cs` | Application CQRS feature | 1.9KB | 52 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Folders/Queries/GetPublicFoldersByUser/GetPublicFoldersByUserQueryValidator.cs` | Application CQRS feature | 370B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/CreateHotspot/CreateHotspotCommand.cs` | Application CQRS feature | 444B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/CreateHotspot/CreateHotspotCommandHandler.cs` | Application CQRS feature | 1.7KB | 44 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/CreateHotspot/CreateHotspotCommandValidator.cs` | Application CQRS feature | 730B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/DeleteHotspot/DeleteHotspotCommand.cs` | Application CQRS feature | 240B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/DeleteHotspot/DeleteHotspotCommandHandler.cs` | Application CQRS feature | 1.0KB | 28 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/DeleteHotspot/DeleteHotspotCommandValidator.cs` | Application CQRS feature | 495B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/DetectHotspots/DetectHotspotsCommand.cs` | Application CQRS feature | 528B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/DetectHotspots/DetectHotspotsCommandHandler.cs` | Application CQRS feature | 3.2KB | 83 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/DetectHotspots/DetectHotspotsCommandValidator.cs` | Application CQRS feature | 374B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/UpdateHotspot/UpdateHotspotCommand.cs` | Application CQRS feature | 399B | 16 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/UpdateHotspot/UpdateHotspotCommandHandler.cs` | Application CQRS feature | 1.4KB | 35 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Hotspots/Commands/UpdateHotspot/UpdateHotspotCommandValidator.cs` | Application CQRS feature | 736B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/CreateOrder/CreateOrderCommand.cs` | Application CQRS feature | 860B | 29 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/CreateOrder/CreateOrderCommandHandler.cs` | Application CQRS feature | 14.3KB | 356 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/CreateOrder/CreateOrderCommandValidator.cs` | Application CQRS feature | 1.3KB | 40 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/CreateOrder/CreateOrderResponse.cs` | Application CQRS feature | 265B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/UpdateOrderStatus/UpdateOrderStatusCommand.cs` | Application CQRS feature | 325B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/UpdateOrderStatus/UpdateOrderStatusCommandHandler.cs` | Application CQRS feature | 2.2KB | 58 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Commands/UpdateOrderStatus/UpdateOrderStatusCommandValidator.cs` | Application CQRS feature | 526B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/GetIncomingOrders/GetIncomingOrdersQuery.cs` | Application CQRS feature | 263B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/GetIncomingOrders/GetIncomingOrdersQueryHandler.cs` | Application CQRS feature | 1.2KB | 29 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/GetOrderDetails/GetOrderDetailsQuery.cs` | Application CQRS feature | 258B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/GetOrderDetails/GetOrderDetailsQueryHandler.cs` | Application CQRS feature | 1.8KB | 46 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/GetOrderDetails/GetOrderDetailsQueryValidator.cs` | Application CQRS feature | 377B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/ResolveCartItemQuote/ResolveCartItemQuoteQuery.cs` | Application CQRS feature | 922B | 26 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/ResolveCartItemQuote/ResolveCartItemQuoteQueryHandler.cs` | Application CQRS feature | 5.5KB | 150 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Orders/Queries/ResolveCartItemQuote/ResolveCartItemQuoteQueryValidator.cs` | Application CQRS feature | 342B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/EvaluatePolicyThreshold/EvaluatePolicyThresholdCommand.cs` | Application CQRS feature | 470B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/EvaluatePolicyThreshold/EvaluatePolicyThresholdCommandHandler.cs` | Application CQRS feature | 4.7KB | 102 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/PromoteRegressionCases/PromoteRegressionCasesCommand.cs` | Application CQRS feature | 442B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/PromoteRegressionCases/PromoteRegressionCasesCommandHandler.cs` | Application CQRS feature | 2.5KB | 69 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/SetPolicyThresholdActive/SetPolicyThresholdActiveCommand.cs` | Application CQRS feature | 386B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/SetPolicyThresholdActive/SetPolicyThresholdActiveCommandHandler.cs` | Application CQRS feature | 3.9KB | 102 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/UpsertPolicyThreshold/UpsertPolicyThresholdCommand.cs` | Application CQRS feature | 401B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Commands/UpsertPolicyThreshold/UpsertPolicyThresholdCommandHandler.cs` | Application CQRS feature | 4.8KB | 124 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyRegressionCaseParser.cs` | Application CQRS feature | 6.5KB | 182 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdAccessService.cs` | Application CQRS feature | 1.5KB | 44 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdAuditWriter.cs` | Application CQRS feature | 1.2KB | 41 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdDtos.cs` | Application CQRS feature | 5.8KB | 165 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdEvalHelpers.cs` | Application CQRS feature | 2.6KB | 79 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdMapper.cs` | Application CQRS feature | 1.3KB | 42 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdOperationParser.cs` | Application CQRS feature | 5.5KB | 160 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Common/PolicyThresholdRules.cs` | Application CQRS feature | 3.4KB | 99 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/GetPolicyOperations/GetPolicyOperationsQuery.cs` | Application CQRS feature | 379B | 10 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/GetPolicyOperations/GetPolicyOperationsQueryHandler.cs` | Application CQRS feature | 3.7KB | 92 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/GetPolicyRegressionCases/GetPolicyRegressionCasesQuery.cs` | Application CQRS feature | 346B | 8 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/GetPolicyRegressionCases/GetPolicyRegressionCasesQueryHandler.cs` | Application CQRS feature | 1.0KB | 28 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/GetPolicyThresholds/GetPolicyThresholdsQuery.cs` | Application CQRS feature | 405B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/GetPolicyThresholds/GetPolicyThresholdsQueryHandler.cs` | Application CQRS feature | 1.7KB | 46 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/ValidatePolicyThresholdScopeAccess/ValidatePolicyThresholdScopeAccessQuery.cs` | Application CQRS feature | 400B | 11 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/PolicyThresholds/Queries/ValidatePolicyThresholdScopeAccess/ValidatePolicyThresholdScopeAccessQueryHandler.cs` | Application CQRS feature | 939B | 27 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/AdjustStock/AdjustStockCommand.cs` | Application CQRS feature | 277B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/AdjustStock/AdjustStockCommandHandler.cs` | Application CQRS feature | 2.7KB | 71 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/AdjustStock/AdjustStockCommandValidator.cs` | Application CQRS feature | 576B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/AdjustStock/AdjustStockResponse.cs` | Application CQRS feature | 337B | 10 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ApplyErpInventoryWebhook/ApplyErpInventoryWebhookCommand.cs` | Application CQRS feature | 1009B | 31 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ApplyErpInventoryWebhook/ApplyErpInventoryWebhookCommandHandler.cs` | Application CQRS feature | 6.3KB | 158 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ApplyErpInventoryWebhook/ApplyErpInventoryWebhookCommandValidator.cs` | Application CQRS feature | 1.2KB | 29 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/CreateProduct/CreateProductCommand.cs` | Application CQRS feature | 456B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/CreateProduct/CreateProductCommandHandler.cs` | Application CQRS feature | 3.0KB | 78 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/CreateProduct/CreateProductCommandValidator.cs` | Application CQRS feature | 715B | 25 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/CreateProduct/CreateProductResponse.cs` | Application CQRS feature | 733B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/DeleteProduct/DeleteProductCommand.cs` | Application CQRS feature | 227B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/DeleteProduct/DeleteProductCommandHandler.cs` | Application CQRS feature | 2.0KB | 52 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/DeleteProduct/DeleteProductCommandValidator.cs` | Application CQRS feature | 378B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportProducts/ImportProductsCommand.cs` | Application CQRS feature | 662B | 19 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportProducts/ImportProductsCommandHandler.cs` | Application CQRS feature | 2.3KB | 60 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportProducts/ImportProductsCommandValidator.cs` | Application CQRS feature | 417B | 14 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportProducts/ImportProductsResponse.cs` | Application CQRS feature | 159B | 6 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportStock/ImportStockCommand.cs` | Application CQRS feature | 663B | 21 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportStock/ImportStockCommandHandler.cs` | Application CQRS feature | 10.6KB | 275 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportStock/ImportStockCommandValidator.cs` | Application CQRS feature | 1.3KB | 40 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Commands/ImportStock/ImportStockResponse.cs` | Application CQRS feature | 496B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/Common/PagedOwnedProductsResponse.cs` | Application CQRS feature | 305B | 9 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/Common/ProductListItemDto.cs` | Application CQRS feature | 579B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetCatalogProducts/GetCatalogProductsQuery.cs` | Application CQRS feature | 425B | 12 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetCatalogProducts/GetCatalogProductsQueryHandler.cs` | Application CQRS feature | 1.5KB | 42 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetCatalogProducts/GetCatalogProductsQueryValidator.cs` | Application CQRS feature | 516B | 17 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetOwnedProducts/GetOwnedProductsQuery.cs` | Application CQRS feature | 305B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetOwnedProducts/GetOwnedProductsQueryHandler.cs` | Application CQRS feature | 1.7KB | 44 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetOwnedProductsPage/GetOwnedProductsPageQuery.cs` | Application CQRS feature | 439B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetOwnedProductsPage/GetOwnedProductsPageQueryHandler.cs` | Application CQRS feature | 1.7KB | 48 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetOwnedProductsPage/GetOwnedProductsPageQueryValidator.cs` | Application CQRS feature | 602B | 16 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetStockMovements/GetStockMovementsQuery.cs` | Application CQRS feature | 275B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetStockMovements/GetStockMovementsQueryHandler.cs` | Application CQRS feature | 1.9KB | 52 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetStockMovements/GetStockMovementsQueryValidator.cs` | Application CQRS feature | 396B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Products/Queries/GetStockMovements/StockMovementDto.cs` | Application CQRS feature | 745B | 18 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Commands/CreateUser/CreateUserCommand.cs` | Application CQRS feature | 392B | 15 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Commands/CreateUser/CreateUserCommandHandler.cs` | Application CQRS feature | 2.2KB | 58 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Commands/CreateUser/CreateUserCommandValidator.cs` | Application CQRS feature | 676B | 25 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Queries/GetAllUsers/GetAllUsersQuery.cs` | Application CQRS feature | 252B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Queries/GetAllUsers/GetAllUsersQueryHandler.cs` | Application CQRS feature | 800B | 22 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Queries/GetUserById/GetUserByIdQuery.cs` | Application CQRS feature | 250B | 7 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Queries/GetUserById/GetUserByIdQueryHandler.cs` | Application CQRS feature | 911B | 27 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Features/Users/Queries/GetUserById/GetUserByIdQueryValidator.cs` | Application CQRS feature | 362B | 13 | Command/query/validator/dto işlevi. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Application/Katalogcu.Application.csproj` | Proje/Build tanımı | 670B | 23 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `backend/Katalogcu.Domain/Class1.cs` | Backend C# kodu | 57B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Common/BaseEntity.cs` | Backend C# kodu | 480B | 15 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/AppUser.cs` | Domain entity | 1.0KB | 25 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/Catalog.cs` | Domain entity | 1.3KB | 33 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/CatalogAiJob.cs` | Domain entity | 738B | 24 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/CatalogItem.cs` | Domain entity | 2.3KB | 74 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/CatalogItemExternalMatch.cs` | Domain entity | 1.3KB | 33 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/CatalogPage.cs` | Domain entity | 927B | 26 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/Customer.cs` | Domain entity | 1.2KB | 29 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/CustomerMachine.cs` | Domain entity | 549B | 17 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ErpInventorySnapshot.cs` | Domain entity | 812B | 21 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ExternalProduct.cs` | Domain entity | 1.2KB | 29 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ExternalProductLinkCheck.cs` | Domain entity | 499B | 16 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ExternalProductOemNumber.cs` | Domain entity | 366B | 12 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ExternalSite.cs` | Domain entity | 659B | 18 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ExternalSiteCrawl.cs` | Domain entity | 704B | 20 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/Folder.cs` | Domain entity | 448B | 15 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/Hotspot.cs` | Domain entity | 1.2KB | 33 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/MachineModel.cs` | Domain entity | 446B | 14 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/ManualImportFile.cs` | Domain entity | 668B | 19 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/Order.cs` | Domain entity | 1.8KB | 44 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/OrderItem.cs` | Domain entity | 534B | 19 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/OrderStatusHistory.cs` | Domain entity | 431B | 14 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/PartCompatibilityRule.cs` | Domain entity | 523B | 16 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/PlatformAuditLog.cs` | Domain entity | 572B | 19 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/PolicyThreshold.cs` | Domain entity | 703B | 22 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/Product.cs` | Domain entity | 1.2KB | 36 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/RegistrationInviteCode.cs` | Domain entity | 584B | 17 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Entities/StockMovement.cs` | Domain entity | 938B | 27 | Domain/veritabanı varlığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Enums/SubscriptionPlan.cs` | Backend C# kodu | 146B | 8 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Domain/Katalogcu.Domain.csproj` | Proje/Build tanımı | 299B | 13 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `backend/Katalogcu.Infrastructure/Class1.cs` | Backend C# kodu | 65B | 6 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/DependencyInjection.cs` | Backend C# kodu | 2.6KB | 42 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Katalogcu.Infrastructure.csproj` | Proje/Build tanımı | 917B | 26 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251123122058_InitialCreate.cs` | EF migration | 1.6KB | 41 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251123122058_InitialCreate.Designer.cs` | EF migration | 2.3KB | 70 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251123124011_AddCatalogDomain.cs` | EF migration | 7.0KB | 158 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251123124011_AddCatalogDomain.Designer.cs` | EF migration | 9.4KB | 281 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251210144736_AddPageNumberToProduct.cs` | EF migration | 1.1KB | 40 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251210144736_AddPageNumberToProduct.Designer.cs` | EF migration | 9.7KB | 290 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251210144942_AddRefNoToProduct.cs` | EF migration | 450B | 22 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251210144942_AddRefNoToProduct.Designer.cs` | EF migration | 9.7KB | 290 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251218181058_UpdateHotspotForYolo.cs` | EF migration | 2.9KB | 103 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20251218181058_UpdateHotspotForYolo.Designer.cs` | EF migration | 10.1KB | 302 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260121125247_AddHotspotDimensions.cs` | EF migration | 453B | 22 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260121125247_AddHotspotDimensions.Designer.cs` | EF migration | 10.1KB | 302 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260122114412_AddAiDescription.cs` | EF migration | 791B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260122114412_AddAiDescription.Designer.cs` | EF migration | 10.2KB | 306 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260122183807_AddPageIdToProduct.cs` | EF migration | 748B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260122183807_AddPageIdToProduct.Designer.cs` | EF migration | 10.3KB | 309 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260126202051_AddProductNewFields.cs` | EF migration | 1.0KB | 38 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260126202051_AddProductNewFields.Designer.cs` | EF migration | 10.5KB | 315 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260127103131_AddOrderTables.cs` | EF migration | 3.7KB | 84 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260127103131_AddOrderTables.Designer.cs` | EF migration | 14.0KB | 416 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260128162143_AddCatalogItemsTable.cs` | EF migration | 1.6KB | 41 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260128162143_AddCatalogItemsTable.Designer.cs` | EF migration | 15.3KB | 456 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260130203754_AddFolderStructure.cs` | EF migration | 2.1KB | 66 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260130203754_AddFolderStructure.Designer.cs` | EF migration | 16.5KB | 493 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131113256_AddFolderTable.cs` | EF migration | 1.9KB | 66 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131113256_AddFolderTable.Designer.cs` | EF migration | 16.5KB | 493 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131130920_AddCatalogItemsRelation.cs` | EF migration | 1.5KB | 49 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131130920_AddCatalogItemsRelation.Designer.cs` | EF migration | 17.0KB | 508 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131220439_AddVectorSupport.cs` | EF migration | 1007B | 35 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131220439_AddVectorSupport.Designer.cs` | EF migration | 17.2KB | 513 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131224043_ChangeVectorSizeTo768.cs` | EF migration | 1.1KB | 37 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260131224043_ChangeVectorSizeTo768.Designer.cs` | EF migration | 17.2KB | 513 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260205231057_AddMachineDetails.cs` | EF migration | 1.6KB | 58 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260205231057_AddMachineDetails.Designer.cs` | EF migration | 17.6KB | 525 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260205235726_AddMachineDetails2.cs` | EF migration | 756B | 28 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260205235726_AddMachineDetails2.Designer.cs` | EF migration | 17.7KB | 528 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260206141058_ExpandVectorSize.cs` | EF migration | 1.1KB | 37 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260206141058_ExpandVectorSize.Designer.cs` | EF migration | 17.7KB | 528 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260208151655_AddVisualFieldsToCatalogItems.cs` | EF migration | 2.0KB | 69 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260208151655_AddVisualFieldsToCatalogItems.Designer.cs` | EF migration | 18.3KB | 543 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260208164538_AddCatalogItemVisualImageUrl.cs` | EF migration | 770B | 28 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260208164538_AddCatalogItemVisualImageUrl.Designer.cs` | EF migration | 18.4KB | 546 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260223190000_AddPublicLinkVersioning.cs` | EF migration | 1.2KB | 41 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260224120000_AddCustomers.cs` | EF migration | 2.6KB | 57 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260224133000_AddCustomerAuthAndOrderCustomerId.cs` | EF migration | 3.1KB | 97 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260224170000_AddCustomerPasswordAuth.cs` | EF migration | 1.1KB | 39 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260224174000_AddCustomerLoginSecurity.cs` | EF migration | 1.2KB | 40 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260224182000_AddOrderCheckoutFields.cs` | EF migration | 2.1KB | 72 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260225190000_ConsolidateRuntimeSchemaUpdates.cs` | EF migration | 5.7KB | 133 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260225233000_AddOrderStatusHistory.cs` | EF migration | 2.1KB | 50 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260226001000_AddOrderStatusHistoryVisibilityFlag.cs` | EF migration | 981B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260226014500_AddAppUserPasswordSalt.cs` | EF migration | 895B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260226025000_NormalizeAppUserRolesToOwner.cs` | EF migration | 916B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260226040000_AddCatalogViews.cs` | EF migration | 2.5KB | 60 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260227021000_AddIsTechnicalDrawingToCatalogPages.cs` | EF migration | 910B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260227183000_AddSubscriptionPlanToUsers.cs` | EF migration | 2.2KB | 74 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260227213000_AddUserAiUsageMonthly.cs` | EF migration | 1.6KB | 41 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260301110000_AddPublicAccessLinks.cs` | EF migration | 1.9KB | 51 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260303201317_AddPlatformAuditLogs.cs` | EF migration | 2.5KB | 58 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260303201317_AddPlatformAuditLogs.Designer.cs` | EF migration | 33.1KB | 957 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260304150000_AddPublicStorefrontViews.cs` | EF migration | 2.4KB | 57 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260306174000_AddPublicStoreSlugToUsers.cs` | EF migration | 1.3KB | 41 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260306193000_AddCatalogPageReviewFields.cs` | EF migration | 1.6KB | 52 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260307003000_RepairPublicStoreSlugColumn.cs` | EF migration | 1.2KB | 39 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260308213227_AddErpInventorySnapshots.cs` | EF migration | 3.7KB | 74 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260308213227_AddErpInventorySnapshots.Designer.cs` | EF migration | 37.3KB | 1066 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260315002208_AddExternalSiteCrawlingSchema.cs` | EF migration | 21.5KB | 386 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260315002208_AddExternalSiteCrawlingSchema.Designer.cs` | EF migration | 62.4KB | 1744 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260315133437_AddManualImportPipeline.cs` | EF migration | 1.1KB | 38 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260315133437_AddManualImportPipeline.Designer.cs` | EF migration | 62.4KB | 1745 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260420075055_AddAppUserApproval.cs` | EF migration | 776B | 29 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260420075055_AddAppUserApproval.Designer.cs` | EF migration | 62.6KB | 1750 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260422114932_AddRegistrationInviteCodes.cs` | EF migration | 2.6KB | 55 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260422114932_AddRegistrationInviteCodes.Designer.cs` | EF migration | 64.7KB | 1806 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260422122056_AddCustomerPasswordResetSecurity.cs` | EF migration | 1.2KB | 40 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260422122056_AddCustomerPasswordResetSecurity.Designer.cs` | EF migration | 65.1KB | 1814 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260517155220_AddCustomerMachines.cs` | EF migration | 2.5KB | 54 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260517155220_AddCustomerMachines.Designer.cs` | EF migration | 67.5KB | 1882 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260517160540_AddCompatibilityGraph.cs` | EF migration | 4.3KB | 91 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260517160540_AddCompatibilityGraph.Designer.cs` | EF migration | 71.6KB | 1993 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260526094000_AddCatalogItemSearchText.cs` | EF migration | 2.4KB | 73 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260528090000_AddCatalogItemsEmbeddingHnswIndex.cs` | EF migration | 1.2KB | 37 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260528103000_AddPolicyThresholds.cs` | EF migration | 4.2KB | 101 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260606214500_AddAiCapacityLeases.cs` | EF migration | 2.3KB | 58 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260613075733_SyncAiServiceProductionModel.cs` | EF migration | 1.7KB | 54 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260613075733_SyncAiServiceProductionModel.Designer.cs` | EF migration | 74.2KB | 2061 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260620093413_DropEmbedTargetsFromModel.cs` | EF migration | 4.3KB | 87 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/20260620093413_DropEmbedTargetsFromModel.Designer.cs` | EF migration | 64.4KB | 1834 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | EF migration | 64.2KB | 1831 | EF Core migration/model geçmişi; DB şemasını taşır. | KORU: migration geçmişi. | tracked |
| `backend/Katalogcu.Infrastructure/Persistence/AppDbContext.cs` | Backend C# kodu | 10.9KB | 291 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Persistence/AppDbContextFactory.cs` | Backend C# kodu | 818B | 20 | Backend C# kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/AuthRepository.cs` | Backend repository | 1.3KB | 45 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/CatalogAiJobRepository.cs` | Backend repository | 7.1KB | 217 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/CatalogExternalMatchRepository.cs` | Backend repository | 9.8KB | 249 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/CatalogProcessingRepository.cs` | Backend repository | 2.0KB | 56 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/CatalogRepository.cs` | Backend repository | 21.8KB | 601 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/ChatQueryService.cs` | Backend repository | 8.7KB | 230 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/CompatibilityRepository.cs` | Backend repository | 2.2KB | 67 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/CustomerRepository.cs` | Backend repository | 4.8KB | 140 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/ErpInventorySnapshotRepository.cs` | Backend repository | 4.4KB | 137 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/ExternalSiteRepository.cs` | Backend repository | 3.8KB | 104 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/FolderRepository.cs` | Backend repository | 2.3KB | 67 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/HotspotRepository.cs` | Backend repository | 2.7KB | 82 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/ManualImportFileRepository.cs` | Backend repository | 1.3KB | 37 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/OrderRepository.cs` | Backend repository | 6.6KB | 182 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/PolicyThresholdRepository.cs` | Backend repository | 4.2KB | 125 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/StockRepository.cs` | Backend repository | 7.6KB | 230 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Repositories/UserRepository.cs` | Backend repository | 1.6KB | 50 | EF/PostgreSQL veri erişim katmanı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/CatalogExternalMatchReviewService.cs` | Backend servis | 3.0KB | 82 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/CatalogExternalMatchService.cs` | Backend servis | 7.4KB | 212 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalLinkPublishingService.cs` | Backend servis | 5.8KB | 154 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalProductNormalizer.cs` | Backend servis | 3.3KB | 94 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalProductUpsertService.cs` | Backend servis | 5.7KB | 159 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalSiteCrawlOrchestrator.cs` | Backend servis | 9.3KB | 212 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalSiteFetchCrawler.cs` | Backend servis | 4.5KB | 110 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalSitePlaywrightCrawler.cs` | Backend servis | 7.7KB | 227 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ExternalSitePlaywrightCrawlerDisabled.cs` | Backend servis | 1.1KB | 34 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/ManualImportService.cs` | Backend servis | 18.0KB | 533 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/PolicyRegressionCaseFileStore.cs` | Backend servis | 8.9KB | 257 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.Infrastructure/Services/SafeExternalHttpClient.cs` | Backend servis | 4.1KB | 113 | Backend iş mantığı veya dış servis entegrasyonu. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/Katalogcu.sln` | Proje/Build tanımı | 6.1KB | 90 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `backend/load-baselines/README.md` | Dokümantasyon | 2.6KB | 60 | Dokümantasyon/runbook: Public Load Baseline. | KORU/INCELE. | tracked |
| `backend/load-baselines/staging-public-browse-baseline.json` | JSON veri/konfig/eval | 4.7KB | 170 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/load-baselines/STAGING_SATURATION_DISPATCH.md` | Dokümantasyon | 2.2KB | 69 | Dokümantasyon/runbook: Staging Saturation Dispatch. | KORU/INCELE. | tracked |
| `backend/MIGRATION_DISCIPLINE.md` | Dokümantasyon | 1.5KB | 49 | Dokümantasyon/runbook: Backend Migration Discipline. | KORU/INCELE. | tracked |
| `backend/pre_smoke_cleanup.sql` | SQL/DB script | 113.6KB | 2863 | SQL/DB script dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/analyze_public_load_saturation.py` | Script/operasyon aracı | 9.8KB | 257 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/check_cqrs_handlers.py` | Script/operasyon aracı | 2.9KB | 101 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/create_initial_user.py` | Script/operasyon aracı | 8.6KB | 269 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/e2e_catalog_ai_load_test.py` | Script/operasyon aracı | 16.8KB | 478 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/e2e_public_load_test.py` | Script/operasyon aracı | 36.5KB | 1004 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/generate_catalog_release_report.sh` | Shell script/operasyon | 5.6KB | 222 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/postdeploy_catalog_only_check.sh` | Shell script/operasyon | 4.2KB | 162 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/postdeploy_portal_panel_check.sh` | Shell script/operasyon | 4.7KB | 184 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/preflight_catalog_only.sh` | Shell script/operasyon | 7.3KB | 253 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/promote_public_load_baseline.py` | Script/operasyon aracı | 11.7KB | 298 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/smoke_all.sh` | Shell script/operasyon | 3.9KB | 160 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/smoke_chat_prod_readiness.sh` | Shell script/operasyon | 5.6KB | 199 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/smoke_public_checkout.py` | Script/operasyon aracı | 12.6KB | 315 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/test_analyze_public_load_saturation.py` | Python AI test | 6.7KB | 186 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/scripts/test_e2e_public_load_test.py` | Python AI test | 20.0KB | 544 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/scripts/test_promote_public_load_baseline.py` | Python AI test | 5.1KB | 138 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/scripts/test_validate_public_load_baseline.py` | Python AI test | 3.2KB | 92 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/scripts/test_validate_public_load_saturation_inputs.py` | Python AI test | 1.7KB | 50 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `backend/scripts/validate_public_load_baseline.py` | Script/operasyon aracı | 5.3KB | 145 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/scripts/validate_public_load_saturation_inputs.py` | Script/operasyon aracı | 2.4KB | 70 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `backend/SMOKE_TESTS.md` | Dokümantasyon | 2.4KB | 73 | Dokümantasyon/runbook: Smoke Tests (MVP). | KORU/INCELE. | tracked |
| `CANLIYA_ALMA_PLANI.md` | Dokümantasyon | 5.8KB | 179 | Dokümantasyon/runbook: Katalogcu Catalog-Only Canliya Alma Plani. | KORU/INCELE. | tracked |
| `CATALOG_CHAT_CANLIYA_ALMA_PLANI.md` | Dokümantasyon | 4.7KB | 106 | Dokümantasyon/runbook: Catalog + Grounded Chat Canlıya Alma Planı. | KORU/INCELE. | tracked |
| `deploy/google-cloud/bootstrap-existing-partalog-staging.sh` | Shell script/operasyon | 15.4KB | 328 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `deploy/google-cloud/catalog-chat-cloud-run.md` | Dokümantasyon | 7.1KB | 227 | Dokümantasyon/runbook: Google Cloud Catalog + Chat Deploy. | KORU/INCELE. | tracked |
| `deploy/google-cloud/catalog-chat-staging-cloud-run.md` | Dokümantasyon | 17.2KB | 465 | Dokümantasyon/runbook: Google Cloud Catalog + Chat Staging Kurulumu. | KORU/INCELE. | tracked |
| `deploy/google-cloud/catalog-only-cloud-run.md` | Dokümantasyon | 8.4KB | 253 | Dokümantasyon/runbook: Google Cloud Catalog-Only Deploy. | KORU/INCELE. | tracked |
| `deploy/google-cloud/check-staging-prereqs.sh` | Shell script/operasyon | 2.4KB | 78 | Shell script/operasyon dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `deploy/google-cloud/monitoring/create_email_notification_channel.py` | Python AI kodu | 7.6KB | 244 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `deploy/google-cloud/monitoring/notification-channel-runbook.md` | Dokümantasyon | 3.0KB | 100 | Dokümantasyon/runbook: Monitoring Notification Channel Runbook. | KORU/INCELE. | tracked |
| `deploy/google-cloud/monitoring/staging-cloud-run-reliability-policy.json` | JSON veri/konfig/eval | 2.7KB | 81 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `deploy/google-cloud/monitoring/staging-public-availability-policy.json` | JSON veri/konfig/eval | 1.7KB | 53 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `deploy/google-cloud/portal-panel-release-checklist.md` | Dokümantasyon | 3.0KB | 95 | Dokümantasyon/runbook: Portal + Panel Release Checklist. | KORU/INCELE. | tracked |
| `deploy/google-cloud/staging-observability.md` | Dokümantasyon | 2.9KB | 63 | Dokümantasyon/runbook: Staging Observability. | KORU/INCELE. | tracked |
| `deploy/google-cloud/vertex-ai-ai-plan.md` | Dokümantasyon | 5.5KB | 136 | Dokümantasyon/runbook: Vertex AI ve AI Servis Canli Plani. | KORU/INCELE. | tracked |
| `frontend/cloudbuild.web.yaml` | Deploy/Container/CI | 318B | 11 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/angular.json` | Proje/Build tanımı | 2.8KB | 108 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/cloudbuild.web.yaml` | Deploy/Container/CI | 401B | 18 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/Dockerfile` | Deploy/Container/CI | 321B | 18 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/Dockerfile.cloudrun` | Deploy/Container/CI | 491B | 23 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/nginx.cloudrun.conf.template` | Diğer | 1.1KB | 43 | Diğer dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/nginx.conf` | Diğer | 651B | 26 | Diğer dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/package-lock.json` | Proje/Build tanımı | 345.7KB | 10377 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/package.json` | Proje/Build tanımı | 1.4KB | 57 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/public/assets/brand/partalog-logo-dark.svg` | Görsel asset/test verisi | 1.6KB | 34 | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/public/assets/brand/partalog-logo-light.svg` | Görsel asset/test verisi | 1.6KB | 34 | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/public/assets/brand/partalog-mark.svg` | Görsel asset/test verisi | 1.1KB | 21 | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/public/favicon.ico` | Görsel asset/test verisi | 14.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/README.md` | Dokümantasyon | 1.7KB | 67 | Dokümantasyon/runbook: KatalogcuFrontend. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/src/_variables.css` | Frontend stil | 35B | 1 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/app.config.ts` | Frontend Angular/TS | 631B | 13 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/app.css` | Frontend stil | 0B | 0 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/app.html` | Frontend template/static HTML | 93B | 4 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/app.routes.ts` | Frontend Angular/TS | 5.7KB | 91 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/app.spec.ts` | Frontend test | 781B | 25 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/app.ts` | Frontend Angular/TS | 1.4KB | 51 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/catalog-detail/catalog-detail.css` | Frontend stil | 27.9KB | 1465 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/catalog-detail/catalog-detail.html` | Frontend template/static HTML | 21.2KB | 506 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/catalog-detail/catalog-detail.spec.ts` | Frontend test | 778B | 24 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/catalog-detail/catalog-detail.ts` | Frontend Angular/TS | 44.2KB | 1379 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/guards/domain-host.guard.ts` | Frontend guard | 1.0KB | 32 | Frontend guard dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/guards/plan-selection.guard.ts` | Frontend guard | 606B | 23 | Frontend guard dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/guards/plan.guard.ts` | Frontend guard | 627B | 22 | Frontend guard dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/guards/platform-admin.guard.ts` | Frontend guard | 480B | 18 | Frontend guard dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/interceptors/auth.interceptors.ts` | Frontend Angular/TS | 770B | 28 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/models/plan.model.ts` | Frontend model | 1003B | 31 | Frontend model dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/ai-stream-contract.spec.ts` | Frontend test | 1.5KB | 52 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/ai-stream-contract.ts` | Frontend servis | 2.9KB | 114 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/ai.service.spec.ts` | Frontend test | 3.4KB | 99 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/ai.service.ts` | Frontend servis | 6.5KB | 199 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/auth.service.ts` | Frontend servis | 7.3KB | 266 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/cart.service.ts` | Frontend servis | 8.3KB | 276 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/catalog.service.ts` | Frontend servis | 12.8KB | 437 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/customer.service.ts` | Frontend servis | 5.8KB | 195 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/domain-context.service.ts` | Frontend servis | 3.0KB | 106 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/order.service.ts` | Frontend servis | 1.8KB | 78 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/platform-admin.service.ts` | Frontend servis | 5.0KB | 206 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/core/services/product.service.ts` | Frontend servis | 3.6KB | 111 | Frontend servis dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/catalogs/catalog-add/catalog-add.css` | Frontend stil | 7.5KB | 276 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/catalogs/catalog-add/catalog-add.html` | Frontend template/static HTML | 6.7KB | 134 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/catalogs/catalog-add/catalog-add.ts` | Frontend Angular/TS | 4.4KB | 140 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/catalogs/catalogs.css` | Frontend stil | 16.7KB | 795 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/catalogs/catalogs.html` | Frontend template/static HTML | 11.2KB | 244 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/catalogs/catalogs.ts` | Frontend Angular/TS | 17.7KB | 582 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/chat-quality/chat-quality.css` | Frontend stil | 5.3KB | 369 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/chat-quality/chat-quality.html` | Frontend template/static HTML | 5.2KB | 158 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/chat-quality/chat-quality.ts` | Frontend Angular/TS | 8.1KB | 278 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/customers/customers.css` | Frontend stil | 14.0KB | 567 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/customers/customers.html` | Frontend template/static HTML | 13.8KB | 278 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/customers/customers.spec.ts` | Frontend test | 2.6KB | 65 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/customers/customers.ts` | Frontend Angular/TS | 12.8KB | 420 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/dashboard.css` | Frontend stil | 16.7KB | 829 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/dashboard.html` | Frontend template/static HTML | 8.2KB | 220 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/dashboard.spec.ts` | Frontend test | 749B | 24 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/dashboard.ts` | Frontend Angular/TS | 9.9KB | 343 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/orders/orders.css` | Frontend stil | 9.5KB | 535 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/orders/orders.html` | Frontend template/static HTML | 9.9KB | 241 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/orders/orders.ts` | Frontend Angular/TS | 6.7KB | 238 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts-add/parts-add.css` | Frontend stil | 2B | 2 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts-add/parts-add.html` | Frontend template/static HTML | 6.9KB | 102 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts-add/parts-add.ts` | Frontend Angular/TS | 3.0KB | 101 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts-import/parts-import.css` | Frontend stil | 2B | 2 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts-import/parts-import.html` | Frontend template/static HTML | 9.0KB | 188 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts-import/parts-import.ts` | Frontend Angular/TS | 5.3KB | 164 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts.css` | Frontend stil | 9.4KB | 303 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts.html` | Frontend template/static HTML | 12.6KB | 284 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/parts/parts.ts` | Frontend Angular/TS | 7.1KB | 240 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/policy-thresholds/policy-thresholds.css` | Frontend stil | 6.7KB | 473 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/policy-thresholds/policy-thresholds.html` | Frontend template/static HTML | 11.1KB | 302 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/policy-thresholds/policy-thresholds.ts` | Frontend Angular/TS | 18.6KB | 613 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/settings/settings.css` | Frontend stil | 9.4KB | 439 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/settings/settings.html` | Frontend template/static HTML | 18.9KB | 391 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/settings/settings.ts` | Frontend Angular/TS | 13.9KB | 454 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/visual-feedback/visual-feedback.css` | Frontend stil | 3.7KB | 219 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/visual-feedback/visual-feedback.html` | Frontend template/static HTML | 3.0KB | 101 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/dashboard/visual-feedback/visual-feedback.ts` | Frontend Angular/TS | 1.9KB | 72 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/home/home.component.css` | Frontend stil | 9.1KB | 556 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/home/home.component.html` | Frontend template/static HTML | 1.7KB | 47 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/home/home.component.ts` | Frontend Angular/TS | 1.3KB | 47 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/admin-layout/admin-layout.css` | Frontend stil | 15.0KB | 730 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/admin-layout/admin-layout.html` | Frontend template/static HTML | 7.9KB | 173 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/admin-layout/admin-layout.ts` | Frontend Angular/TS | 12.3KB | 383 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/header/header.css` | Frontend stil | 0B | 0 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/header/header.html` | Frontend template/static HTML | 2.2KB | 34 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/header/header.spec.ts` | Frontend test | 731B | 24 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/layout/header/header.ts` | Frontend Angular/TS | 728B | 21 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/login/login.css` | Frontend stil | 6.1KB | 331 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/login/login.html` | Frontend template/static HTML | 2.7KB | 89 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/login/login.spec.ts` | Frontend test | 721B | 24 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/login/login.ts` | Frontend Angular/TS | 1.9KB | 63 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/panel-access/panel-access.html` | Frontend template/static HTML | 4.9KB | 100 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/panel-access/panel-access.ts` | Frontend Angular/TS | 3.0KB | 108 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-dashboard/platform-dashboard.css` | Frontend stil | 9.7KB | 631 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-dashboard/platform-dashboard.html` | Frontend template/static HTML | 11.6KB | 299 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-dashboard/platform-dashboard.ts` | Frontend Angular/TS | 25.4KB | 827 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-login/platform-login.css` | Frontend stil | 2.1KB | 139 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-login/platform-login.html` | Frontend template/static HTML | 1.3KB | 43 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-login/platform-login.ts` | Frontend Angular/TS | 1.6KB | 60 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-tenant-detail/platform-tenant-detail.css` | Frontend stil | 9.9KB | 644 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-tenant-detail/platform-tenant-detail.html` | Frontend template/static HTML | 16.4KB | 419 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/platform-tenant-detail/platform-tenant-detail.ts` | Frontend Angular/TS | 15.7KB | 502 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/shared/action-reason-modal/action-reason-modal.css` | Frontend stil | 2.6KB | 164 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/shared/action-reason-modal/action-reason-modal.html` | Frontend template/static HTML | 1.8KB | 60 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/platform/shared/action-reason-modal/action-reason-modal.ts` | Frontend Angular/TS | 1.8KB | 70 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-catalog-detail/public-catalog-detail.html` | Frontend template/static HTML | 10.3KB | 165 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-catalog-detail/public-catalog-detail.ts` | Frontend Angular/TS | 6.6KB | 212 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-catalog-showcase/public-catalog-showcase.css` | Frontend stil | 8.7KB | 463 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-catalog-showcase/public-catalog-showcase.html` | Frontend template/static HTML | 3.3KB | 98 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-catalog-showcase/public-catalog-showcase.spec.ts` | Frontend test | 835B | 24 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/public-catalog-showcase/public-catalog-showcase.ts` | Frontend Angular/TS | 4.0KB | 141 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-auth-identity.spec.ts` | Frontend test | 1.0KB | 34 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-auth-identity.ts` | Frontend Angular/TS | 1.5KB | 42 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-catalog-viewer/public-catalog-viewer.css` | Frontend stil | 26.8KB | 1376 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-catalog-viewer/public-catalog-viewer.html` | Frontend template/static HTML | 12.6KB | 290 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-catalog-viewer/public-catalog-viewer.spec.ts` | Frontend test | 824B | 24 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-catalog-viewer/public-catalog-viewer.ts` | Frontend Angular/TS | 41.5KB | 1185 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-checkout/public-checkout.css` | Frontend stil | 6.4KB | 458 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-checkout/public-checkout.html` | Frontend template/static HTML | 14.6KB | 275 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-checkout/public-checkout.ts` | Frontend Angular/TS | 16.9KB | 525 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-view.css` | Frontend stil | 31.2KB | 1556 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-view.html` | Frontend template/static HTML | 40.4KB | 649 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-view.stream-replay.spec.ts` | Frontend test | 4.3KB | 127 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-view.stream-state.spec.ts` | Frontend test | 5.4KB | 162 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-view.stream-state.ts` | Frontend Angular/TS | 2.4KB | 103 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/app/public-view/public-view.ts` | Frontend Angular/TS | 32.3KB | 948 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/environments/environment.development.ts` | Frontend environment | 346B | 18 | Frontend environment dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/environments/environment.ts` | Frontend environment | 323B | 16 | Frontend environment dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/index.html` | Frontend template/static HTML | 852B | 22 | Frontend template/static HTML dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/main.ts` | Frontend Angular/TS | 240B | 6 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/styles.css` | Frontend stil | 470B | 13 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/styles/_admin-panel-overrides.css` | Frontend stil | 12.0KB | 449 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/styles/_variables.css` | Frontend stil | 2.3KB | 88 | Frontend stil dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/src/testing/standalone-component-test-providers.ts` | Frontend Angular/TS | 438B | 12 | Frontend Angular/TS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/tailwind.config.js` | Frontend/helper JS | 647B | 27 | Frontend/helper JS dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `frontend/katalogcu-frontend/tsconfig.app.json` | Proje/Build tanımı | 429B | 15 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/tsconfig.json` | Proje/Build tanımı | 992B | 34 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `frontend/katalogcu-frontend/tsconfig.spec.json` | Proje/Build tanımı | 408B | 14 | Proje/Build tanımı dosyası. | KORU/INCELE. | tracked |
| `partalog-ai/api/__init__.py` | Python AI API endpoint | 273B | 8 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/api/analysis.py` | Python AI API endpoint | 5.2KB | 116 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/api/chat.py` | Python AI API endpoint | 71.3KB | 1828 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/api/hotspot.py` | Python AI API endpoint | 7.2KB | 242 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/api/ingestion.py` | Python AI API endpoint | 1.2KB | 36 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/api/stream_contract.py` | Python AI API endpoint | 3.3KB | 110 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/api/table.py` | Python AI API endpoint | 12.0KB | 328 | FastAPI endpoint modülü. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/cloudbuild.chat.yaml` | Deploy/Container/CI | 410B | 18 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `partalog-ai/config.py` | Python AI kodu | 11.7KB | 289 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/core/__init.__.py` | Python AI kodu | 220B | 7 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/core/dependencies.py` | Python AI kodu | 517B | 19 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/core/detector.py` | Python AI kodu | 7.3KB | 243 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/core/ocr.py` | Python AI kodu | 15.8KB | 408 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/core/rate_limiter.py` | Python AI kodu | 120B | 4 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/Dockerfile` | Deploy/Container/CI | 523B | 26 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `partalog-ai/Dockerfile.chat` | Deploy/Container/CI | 557B | 24 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `partalog-ai/Dockerfile.chat-local` | Deploy/Container/CI | 545B | 26 | Deploy/Container/CI dosyası. | KORU/INCELE. | tracked |
| `partalog-ai/domain/__init__.py` | Python AI kodu | 63B | 1 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/domain/assembly_rules.py` | Python AI kodu | 3.1KB | 92 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/domain/chat_lexicon.py` | Python AI kodu | 5.4KB | 96 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/audit_eval_reports.py` | Python AI eval aracı/verisi | 3.7KB | 118 | Python AI eval aracı/verisi dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/build_cases_from_feedback.py` | Python AI eval aracı/verisi | 7.3KB | 220 | Python AI eval aracı/verisi dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/build_context_cases_from_db.py` | Python AI eval aracı/verisi | 3.9KB | 124 | Python AI eval aracı/verisi dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/chat_eval.py` | Python AI eval aracı/verisi | 37.2KB | 976 | Python AI eval aracı/verisi dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.behavior_smoke.jsonl` | JSON veri/konfig/eval | 10.8KB | 33 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.catalog_smoke.jsonl` | JSON veri/konfig/eval | 2.4KB | 11 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.context.jsonl` | JSON veri/konfig/eval | 1.9KB | 10 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.feedback_regressions.jsonl` | JSON veri/konfig/eval | 503B | 6 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.hard.jsonl` | JSON veri/konfig/eval | 1.5KB | 12 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.nightly.jsonl` | JSON veri/konfig/eval | 3.0KB | 31 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.relevance.jsonl` | JSON veri/konfig/eval | 4.3KB | 29 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.sample.jsonl` | JSON veri/konfig/eval | 1.3KB | 13 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/queries.staging_semantic.jsonl` | JSON veri/konfig/eval | 2.0KB | 8 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/eval/README.md` | Dokümantasyon | 4.5KB | 148 | Dokümantasyon/runbook: Chat Eval v1. | KORU/INCELE. | tracked |
| `partalog-ai/eval/update_trend_history.py` | Python AI eval aracı/verisi | 6.9KB | 185 | Python AI eval aracı/verisi dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/main.py` | Python AI kodu | 8.3KB | 236 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/rate_limit_load_test.py` | Python AI kodu | 5.2KB | 140 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/requirements.chat-local.txt` | Diğer | 892B | 50 | Diğer dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/requirements.chat.txt` | Diğer | 757B | 37 | Diğer dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/requirements.txt` | Diğer | 800B | 52 | Diğer dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/sanayi_sozlugu.json` | JSON veri/konfig/eval | 25.8KB | 1187 | JSON veri/konfig/eval dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/schemas/__init__.py` | Python AI kodu | 35B | 1 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/schemas/detection.py` | Python AI kodu | 2.5KB | 53 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/scripts/eval_visual_hints.py` | Script/operasyon aracı | 4.8KB | 157 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/scripts/populate_embeddings.py` | Script/operasyon aracı | 9.8KB | 274 | Script/operasyon aracı dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/scripts/test_semantic_search.py` | Python AI test | 6.1KB | 183 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/services/ai_capacity.py` | Python AI servis | 16.2KB | 450 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_context.py` | Python AI servis | 40.4KB | 1021 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_feedback.py` | Python AI servis | 6.1KB | 177 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_intent.py` | Python AI servis | 11.0KB | 246 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_matching.py` | Python AI servis | 10.8KB | 326 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_memory.py` | Python AI servis | 14.6KB | 410 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_parts.py` | Python AI servis | 5.2KB | 146 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_policy.py` | Python AI servis | 16.2KB | 457 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_prompt.py` | Python AI servis | 13.4KB | 252 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_request.py` | Python AI servis | 2.1KB | 68 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_responses.py` | Python AI servis | 18.5KB | 405 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_retrieval.py` | Python AI servis | 24.5KB | 636 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_sources.py` | Python AI servis | 7.4KB | 204 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_terms.py` | Python AI servis | 4.0KB | 128 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_visual.py` | Python AI servis | 9.9KB | 218 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/chat_visual_search.py` | Python AI servis | 9.6KB | 215 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/embedding.py` | Python AI servis | 7.6KB | 246 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/genai_provider.py` | Python AI servis | 7.3KB | 204 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/policy_thresholds.py` | Python AI servis | 3.8KB | 118 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/search_text_builder.py` | Python AI servis | 8.4KB | 247 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/search_trace.py` | Python AI servis | 5.2KB | 146 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/storage/local_storage.py` | Python AI servis | 406B | 14 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/storage/s3_storage.py` | Python AI servis | 593B | 20 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/storage/storage_factory.py` | Python AI servis | 351B | 8 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/services/vector_db.py` | Python AI servis | 44.0KB | 1365 | AI/chat/search/embedding iş mantığı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/10-44094617ae2b46418f1685e975cba77f.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/11-1456b630cad745f099cdb502b04127b1.jpg` | Görsel asset/test verisi | 803B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/12-01047da51169489c89f2a00abe3e5c35.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/13-e51b6b980ccf4ffb99e0e6d4e722e3ce.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/14-5e8dd7ecfe2147aa857d997b2457f328.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/15-a3ededa924a24861bfc264c3735b9aa8.jpg` | Görsel asset/test verisi | 828B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/16-9080aaf09e5445cb8694238d39e3f606.jpg` | Görsel asset/test verisi | 7.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/17-ddb45c4c0f7c455faef4ac1d23175a98.jpg` | Görsel asset/test verisi | 2.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/18-38344d88f47645a4addebc8fb91ba45a.jpg` | Görsel asset/test verisi | 840B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/19-0ce43ab1ae184ad4be71a104847b32af.jpg` | Görsel asset/test verisi | 2.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/2-e380c7778bb046ba951d736e98815f8c.jpg` | Görsel asset/test verisi | 1.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/20-4b7fd356b9b34491adf5444eba2a1aae.jpg` | Görsel asset/test verisi | 803B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/21-8179760ef45f45cda0ad0e749dd82371.jpg` | Görsel asset/test verisi | 936B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/22-9ce36e79a5f84b18860917f9080b6a00.jpg` | Görsel asset/test verisi | 1.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/23-22c42021de394647b85c16e1e9c93e00.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/23-677b1dd02b4345be8dd70990bff422ee.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/24-fe92eee48b2b429090e79816f8d61dae.jpg` | Görsel asset/test verisi | 862B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/25-44f144e575534ba49ca954d8e4857b68.jpg` | Görsel asset/test verisi | 12.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/26-6f50a00ec38646869cd7ebf47d30c3ae.jpg` | Görsel asset/test verisi | 8.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/3-4-c5189011a57b42f0b2d61538f6ae4db8.jpg` | Görsel asset/test verisi | 4.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/5-569745643e9e4c2081b5bc9ed04fb5e6.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/5-93a0d9f67f55406593d9b669632f99a9.jpg` | Görsel asset/test verisi | 894B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/6-13d023afd18a4f6f9d25ab3fdc4f857f.jpg` | Görsel asset/test verisi | 759B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/7-f8883fda42154ae7ae300fbab930b761.jpg` | Görsel asset/test verisi | 1.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/3/8-76b3ec59547347b9a3c30c9a9c214a07.jpg` | Görsel asset/test verisi | 3.8KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/1-1aa9a6f2a3da4b5bb8f04871adef10bc.jpg` | Görsel asset/test verisi | 892B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/10-34b9e0c555dd4a93b3326bd17d1ea5ec.jpg` | Görsel asset/test verisi | 919B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/11-113e65ca594e420b9ec970690e98ca84.jpg` | Görsel asset/test verisi | 979B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/12-75e08d92eeb74e14a9a63932acc405e6.jpg` | Görsel asset/test verisi | 1009B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/13-e683dcc7786c471f9883cc4d7c8e0b62.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/14-9f2bf6795dad45f88ad86e186df52f8e.jpg` | Görsel asset/test verisi | 938B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/15-c68bcfee0b014bbcb64200ff9b20d4bd.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/16-13485636fb054d3ab64e1c9c7849795d.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/17-9a28362c3b95417f851d71b949009356.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/18-cf0b4b6480f34c55896d53142d76b9b1.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/19-5754092d7d364c318a512ca4396cda4d.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/2-19191bde73874be3a76cf9cdeb1974ed.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/20-66c2e292230d494691df45fb04c862f2.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/21-624c38d8325f47c4ab75d172913b78e7.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/22-eb914cc92abc458ba2397bb8f88aa95a.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/23-0a55d6d4edad4c77a9ac9b2f37189f1c.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/24-835a101baf2343a2af0578cfbee2a90a.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/25-929e5cceff524e3fbff55d876849bb72.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/26-af85c7a1a9184834b9b5637d69f54c7f.jpg` | Görsel asset/test verisi | 643B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/3-4eb8b1fe7c614613ad6c091fc7f08059.jpg` | Görsel asset/test verisi | 851B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/4-2458d0f2b86b46628a006af16ec9cf84.jpg` | Görsel asset/test verisi | 892B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/5-0d5698273c6b48b0b3cccbd40c066a90.jpg` | Görsel asset/test verisi | 944B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/6-d2dcf4f4f5a44087b5875b37ce427195.jpg` | Görsel asset/test verisi | 988B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/7-b4ad5346a57f479c8ce5acac534ea8b5.jpg` | Görsel asset/test verisi | 981B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/8-23950b86c28e42b3828beef29f309078.jpg` | Görsel asset/test verisi | 957B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/4/9-c67ccd17522c42f2973511f40e20a494.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/1.2-1ee2975dcb554a3185659da62089cfac.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/1.2-4bc1c560ee244ad081a6cb21f940588c.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/10-390183a490274bb4b35cb59695485407.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/11-6710d8a9b1d14befb3230a1b651d0b1d.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/12-f8836d0b29df464e8b51841a49763af9.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/13-bf88b4ed1aac4791a8c981b8dac699b2.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/14-199f392db4f946569b136232ffa4aba3.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/15-70ce4ec6b3e347139dff7b02aeb281bf.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/16-f747c77bfead43f8b9b55ea223c591ac.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/17-e5e1e0d3b90b4c94b3bcee2d400ce19c.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/18-54a4c93e963b42f2bacfc9422ad4b533.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/19-011e9bf1d4b54cffbb44ed010a46ad14.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/20-6a242b47dc554ea783087224f4093a7f.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/21-b5bbf335f6e54c03b2b504430f1856d6.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/22-4563e8489e944aeaaea2746d4101944d.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/23-db6bd69266bf43b7ba22c8bec040c6e2.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/24-a3abfb3b08d54072bb49d69c7ae41d02.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/25-eeedb893565643cfbe52484da37d504a.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/26-801c13a6e1eb4f438010676069e1c0d2.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/3-4-88e9375f84e94735a183ea4fc8ffd950.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/3-4-c3432e05ffc343979db1bfcbccf1f091.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/5-386d97b6b58f4531bebcd4de8d2024ae.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/6-675803c04d1d47d7a2d19f1a4053181f.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/7-ab7cc075d6d14e168fb053b86e493f63.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/8-33b769fe07c84153a198773ca9b44804.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/28db4c90-26f7-454b-a2f3-10c3f7ceb7f6/6/9-a9103058840741ab9afcdadaf70e479c.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/10-86fd01bc8aba4d71a2612dcae32a2180.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/11-034b793be0fc49d6a0ddd70bb19b517d.jpg` | Görsel asset/test verisi | 899B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/12-84395b41b9014c1f95e6f57f4326906d.jpg` | Görsel asset/test verisi | 707B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/13-7a93169248d04e0499fd21fbb35f5544.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/14-203825be96d54f16afb741b333f406c2.jpg` | Görsel asset/test verisi | 667B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/16-6ee745ae37aa47d1a8113741edc13f97.jpg` | Görsel asset/test verisi | 811B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/2-43265d0b65204629a109fd5daa4591a7.jpg` | Görsel asset/test verisi | 3.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/23-5cfd873543fb42fba373c3b44c1e8c9a.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/23-bc106b21bf604a06bec7e1d72ed399cd.jpg` | Görsel asset/test verisi | 1009B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/23-dfc5413e6da5449091344f27ff69a17d.jpg` | Görsel asset/test verisi | 1.4KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/5-2cd92fc364f04855b4337801e7923ca1.jpg` | Görsel asset/test verisi | 1.5KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/5-f95feef9b9f54ffc8a0c032bf949f5f0.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/3/6-4bd6993736254aa08fbd739c883d96bf.jpg` | Görsel asset/test verisi | 851B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/1-c39ad2b01adb4351875540bc0f92abd3.jpg` | Görsel asset/test verisi | 1017B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/10-e39d141df21f45539099bc33e2629e87.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/11-185801db9fdc42cba9bf10cb4836c165.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/12-d1598bdca5084abc821b98be08c002e9.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/13-4d58239b53e84d038aa9c5e6c1b3cb95.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/14-1cf7ad886f234cbcadae5a87ed3efd78.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/15-11da3a1b134542669c079fd85da982d3.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/16-92c58659c2ed4e1faa1a0fcf5102b013.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/17-de8dd31620da46ffbeb05ab49236d01d.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/18-4ecaa8ff81284914a1ac4069d892b4c5.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/19-219d454cfcee47949bbf02c1f8714933.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/2-494fc1dc3cfd4141972c6634b0820798.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/20-b0f9773d54224354ac4d94ab0aa7c15d.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/21-b9736e9daf3a40dabd0715f8f0a4360e.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/22-862412526fd749f0853e0ab58f15593d.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/23-7a0e680473a943a38b5eeda7bb036de1.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/24-2eb0bec954404a25add71d93a3ab6203.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/25-459f9da1740b421da10d8e8d1383381e.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/26-819f7c78b2f74308b09b9ec7fa26c0cc.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/3-7b1f83d00cf84416bbd9c6b32e01ce01.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/4-a945572227e845238c0e6dbd33d9437a.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/5-11b3415a3e6d46c68894a1db985209ea.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/6-85602e1297214fcca8c2af1c805b7683.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/7-e7d4259071684c208ce168be71036e68.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/8-a1af654eeb3343938660adbbe090fe39.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/4/9-777dd16f3d654cdd9c44b5b7f306edff.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/1.2-2fb9aaccbaaf4db3bf5831115b7cd2e1.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/1.2-7462d2c011ab49e69183f8e5c3965941.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/10-cd9ba26ad67e4559b0bcbebb11e7c95a.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/11-af95e8b8667547fc9770f3030e2741f3.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/12-254e0db353254a8ab75f66eb4cdfe070.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/13-36630219d1b048a3ae2f0fc405d51898.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/14-5bea1328a5084098991c86233ee3a32d.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/15-7c3eae61b9a04b7aa4244649564d3b99.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/16-e74271d0bc0747b2a795b37e3fd51f39.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/17-be19680f1d5c47819146d4c07105f002.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/18-5e9a67ef5f0443d9811e10789d2c92cb.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/19-86c1855e16b540b88082ef58b3f21a78.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/20-1d8109a515774f7493b13e63d4bb761e.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/21-047a5949f28545378517f92467ce60fa.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/22-936b44cc8f9140ea991da2f39de58080.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/23-71e49b926cd1413c908d8fe1a3131950.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/24-5ed8e5f76b23415e9ed569a843654e08.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/25-b388c67197d0411e9aa42b33e3b389ce.jpg` | Görsel asset/test verisi | 691B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/26-05d1002b80884fce88409819e70b5166.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/3-4-194369dc6a9a4dd592950441c2067c88.jpg` | Görsel asset/test verisi | 707B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/3-4-afe128ca99784727b253a8a1fdbef5aa.jpg` | Görsel asset/test verisi | 687B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/5-29f1289c61fd416198982d91270218d9.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/6-2519ec0eab4244e19af1a2b2a44c5441.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/7-764925a0e7234a5d99ff80698e15d770.jpg` | Görsel asset/test verisi | 659B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/8-544c774be3774ec28ac9e08dc3b4c949.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/c9012a78-7f28-4963-aff6-9850fa886365/6/9-dd86c28f2b284befb7930c8090d51f7e.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/10-fc2c2f1b1c594c7f9316417199b644ce.jpg` | Görsel asset/test verisi | 779B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/11-25787a321b604dcd8fb18c2983140e7f.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/12-c06463f682264343b4e7bbbdccaa015b.jpg` | Görsel asset/test verisi | 675B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/13-4c2cf0f69b8b483a9b2c03a745f98bae.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/14-fd040a19673d4f3abc759cff76a171dc.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/15-6a17d55239c2474fa320d1ab29cfe6ac.jpg` | Görsel asset/test verisi | 670B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/16-03f63686908142a1ba04dc0122bd5acd.jpg` | Görsel asset/test verisi | 1.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/17-265f2f5df1af43f79cfa4b6866ad11b4.jpg` | Görsel asset/test verisi | 2.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/18-b0a44fdfc67d410681fb5f2829f74c91.jpg` | Görsel asset/test verisi | 1004B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/19-cfbdf686e5684ecb8be2c23d450c8643.jpg` | Görsel asset/test verisi | 2.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/2-0205cbef39e44188a106413bfde50f50.jpg` | Görsel asset/test verisi | 2.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/20-80fc319d352a482089768a6d57db2268.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/21-9ef6f032149a44ce86de801a07b0c9a8.jpg` | Görsel asset/test verisi | 727B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/22-c8f19b9b79334276a8050900b3835491.jpg` | Görsel asset/test verisi | 1.6KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/23-37058054691a45858655e4976292e080.jpg` | Görsel asset/test verisi | 861B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/23-5d83041a5ece4f28bd2d22518ab28d16.jpg` | Görsel asset/test verisi | 926B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/24-6865cc5522fb430fa7d4e80f0f94bab1.jpg` | Görsel asset/test verisi | 830B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/25-cc7db7bfd87e410eb6040a5fc8582cdf.jpg` | Görsel asset/test verisi | 7.7KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/26-11646234d7804344b0d3d5b2e585d54f.jpg` | Görsel asset/test verisi | 8.9KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/3-4-427e6bd03b5144f1835c284fafd43ba6.jpg` | Görsel asset/test verisi | 772B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/5-06ff3f18e58744cb83918909fec7c3fe.jpg` | Görsel asset/test verisi | 741B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/5-d2942d0c76bd409aa9ae790adee91764.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/6-9516d90207ad4c9b81bb15f720c4772c.jpg` | Görsel asset/test verisi | 759B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/7-6a6cd5a4d024484692bce2fffa3772a9.jpg` | Görsel asset/test verisi | 956B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/8-19f0d8700647450285c739520609bf3f.jpg` | Görsel asset/test verisi | 4.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/3/9-b613986d0a6c4a0b99f8f095ac3e94a0.jpg` | Görsel asset/test verisi | 731B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/1-7823c15ccd744297a1bfd1df3a1856e5.jpg` | Görsel asset/test verisi | 972B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/10-48f3e205267d43a2b90b51e9997bfaa7.jpg` | Görsel asset/test verisi | 1.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/11-fb6681fc61904b7cb7d3211f31c3d003.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/12-7cbb713f77ee49ccb79883a2fbf11839.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/13-3935b067ec4747878c7730f6c570e958.jpg` | Görsel asset/test verisi | 1.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/14-23afe94560fe4e598c4cbdc90769f1f4.jpg` | Görsel asset/test verisi | 1.3KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/15-a45c55c89ce74b1aa1c2c658c7cde0f2.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/16-d615af53eee8400aae828781ed0cacee.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/17-eedfbc78167c444dabf192ed62bba510.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/18-d7166accb5d44c8f934585c7bb4364e7.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/19-c5fa92ee2c984b4dbbde0ed5bdda1187.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/2-f70cd2c5fae548d483b3e4b1d4914229.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/20-ba5601846ac64e03aecf3de0c488d840.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/21-dea4073520214b1698be491482001d53.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/22-75b845f1e13b4f28b214525707285ae4.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/23-009591e01665407b9e8c6b94a89657f1.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/24-6143121c343e428f9e92ff751d3d2e63.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/25-47d64a0b0f8a414fa49f15a7110e27e3.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/26-5ae1aaa215fd4775b9b5ba26f5c4b02c.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/3-610692f6ba074902877e4874fa9d7163.jpg` | Görsel asset/test verisi | 1004B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/4-1a87b62dc55149529631469c31e6a073.jpg` | Görsel asset/test verisi | 957B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/5-a4df23aac2cb41dfa6129f1d3e4cf377.jpg` | Görsel asset/test verisi | 1.0KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/6-66a9e2f8e89848a5a6628745a2e94abb.jpg` | Görsel asset/test verisi | 1.2KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/7-457cbae543ac4a0cb6a2770d01cfd733.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/8-5163a41ece5349a39d6d092a8801aab7.jpg` | Görsel asset/test verisi | 1021B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/4/9-1a1a9481dfd8455687a82391b9590f05.jpg` | Görsel asset/test verisi | 1.1KB | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/1.2-2bf2bd0807ab4d2598807e420acb4a56.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/1.2-ec63a5f3f99a4871999e755ea07289fc.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/10-3f3660b9bd5a4ecb8cc1197d55a4c94f.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/11-ac4b47b92a1f49958acd6ac0b0d0c93d.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/12-3496e4e57c834440b3d9c6334f068570.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/13-171376cedad34435942875f067c10a4a.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/14-1218aca3a40c43a7b92b4c28b3896a74.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/15-decde862eb5b43f8895263db0dc703ff.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/16-79c385ab50a14453a6a63197757cff0f.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/17-1ac81666ba3842d8871bfc80d9c37f85.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/18-fd80b8f8bc4a4cee95e1077a55b4a984.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/19-8acbc694b12c4586897748a231d56b39.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/20-df06680dc88d464fbca5606f8df17945.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/21-50a403fe86b74076b5ad0653486f3868.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/22-bd818440611b4210a4afb2ebbf43e53f.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/23-b97186f83b784872a756bd40e36daa7c.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/24-5177b7982df248a7ba59cd97cfc614be.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/25-d49825da46254474b986c25e6d90067f.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/26-f3d5111531af4f27a82d0746529f3d22.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/3-4-18385e07dbf346e19fa24f7ada5a2496.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/3-4-36e9bf743da14388ab5366a1b154f248.jpg` | Görsel asset/test verisi | 663B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/5-dd3bd3f616b04f2ba3e322254d0ccc14.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/6-929aa6f9273c4fc3a0100af006b49eaf.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/7-00b1cf20975f4a9d9278bb653c2e19a6.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/8-e538e9db06094d249bcc317e243087a2.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/static/visual-parts/d9753d37-7d37-40a7-97e2-eb550f7b5e99/6/9-5adb3a9137da456c81ae781a7c33c4a1.jpg` | Görsel asset/test verisi | 651B | - | Otomatik test kapsamı. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `partalog-ai/tests/test_ai_capacity.py` | Python AI test | 2.0KB | 64 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_build_cases_from_feedback.py` | Python AI test | 2.2KB | 64 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_chat_behavior.py` | Python AI test | 51.6KB | 1279 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_chat_eval_metrics.py` | Python AI test | 20.6KB | 621 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_chat_eval_trend.py` | Python AI test | 3.3KB | 107 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_chat_runtime_fast_path.py` | Python AI test | 1.5KB | 46 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_chat_terms_adversarial.py` | Python AI test | 2.1KB | 59 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_config_dsn.py` | Python AI test | 1.4KB | 44 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_embedding_cache_retry.py` | Python AI test | 6.0KB | 159 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_main_health.py` | Python AI test | 2.9KB | 80 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_rate_limit_load.py` | Python AI test | 4.3KB | 112 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_search_text_builder.py` | Python AI test | 6.9KB | 177 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_stream_contract.py` | Python AI test | 3.3KB | 90 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_table_translation.py` | Python AI test | 2.3KB | 74 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/tests/test_vector_db_lifecycle.py` | Python AI test | 4.8KB | 163 | Otomatik test kapsamı. | KORU: regresyon güvenliği sağlar. | tracked |
| `partalog-ai/train_dictionary.py` | Python AI kodu | 7.1KB | 188 | Python AI kodu dosyası. | KORU: aktif kod/asset gibi görünüyor. | tracked |
| `plans/next-product-roadmap.md` | Dokümantasyon | 13.1KB | 476 | Dokümantasyon/runbook: Partalog Sonraki Urun Roadmap'i. | KORU/INCELE. | tracked |
| `PROJE_YAPISI.md` | Dokümantasyon | 2.7KB | 76 | Dokümantasyon/runbook: Katalogcu - Guncel Proje Yapisi. | KORU/INCELE. | tracked |
| `README.md` | Dokümantasyon | 3.2KB | 117 | Dokümantasyon/runbook: Katalogcu. | KORU/INCELE. | tracked |

## Lokal/Ignored Eki

| Dosya/Klasör | Dosya adedi | Boyut | Not |
|---|---:|---:|---|
| `partalog-ai/.env` | 1 | 1.4KB | Gerçek env/secret olabilir; commit edilmemeli. |
| `backend/Katalogcu.API/wwwroot/uploads` | 0 | 0B | Runtime upload klasoru; bos tutuluyor. |
| `partalog-ai/venv` | 29867 | 973.8MB | Bağımlılık klasörü; git dışı. |
| `frontend/katalogcu-frontend/node_modules` | 22587 | 246.0MB | Bağımlılık klasörü; git dışı. |
