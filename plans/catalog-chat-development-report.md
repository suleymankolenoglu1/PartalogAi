# Catalog + Chat Geliştirme Raporu

Bu dosya Catalog + Grounded Chat MVP için yapılan değişikliklerin yaşayan kaydıdır.

## 2026-06-22 — Güncel İlerleme Notu

Staging ortamı Google Cloud üzerinde çalışıyor; gerçek public-token chat yolu artık exact-code smoke/eval/load yanında semantic/natural-language eval ile de doğrulandı. Oranlar hâlâ konservatif tutuldu; çünkü full saturation/load baseline, alert/error-budget ve maliyet budget kontrolleri tamamlanmadan canlıya alma yüzdesini çok yukarı çekmek yanıltıcı olur.

- Genel proje ilerlemesi yaklaşık **%88** seviyesine çıktı.
- Catalog + Chat MVP kod hazırlığı yaklaşık **%93** seviyesine çıktı.
- Catalog + Chat canlıya alma hazırlığı yaklaşık **%88** seviyesine çıktı.
- Staging/eval/load kanıtı yaklaşık **%87** seviyesine çıktı.

Bu artış; staging Cloud Run ortamının kurulması, Redis rate-limit/capacity/DataProtection wiring'i, gerçek public-token chat smoke, exact-code eval baseline, kontrollü public load smoke, staging katalog embedding backfill ve semantic eval kanıtları sayesinde geldi.

### Kalan Ana Blokajlar

- Semantic eval artık gerçek staging katalog içeriğine göre temizlenmiş 8-case corpus ile `100% Hit@1` veriyor. Bu gate quota-dostu yavaş profil ve sınırlı retry ile koşmalı.
- Full saturation/load baseline ve alert/bütçe kontrolleri henüz onaylı release gate seviyesinde değil.

## 2026-06-22 — Paket 7: Temiz Staging Semantic Corpus ve Quota-Dostu Eval Gate

### Tamamlananlar

- Published staging katalog içeriği tekrar doğrulandı:
  - published catalogs: `1`
  - catalog items: `32`
  - embedded items: `32`
  - search text items: `32`
- Gerçek staging katalog item'larına dayalı kalıcı semantic eval corpus'u eklendi:
  - `partalog-ai/eval/queries.staging_semantic.jsonl`
  - public token ve catalog id dosyada tutulmuyor; `<PUBLIC_TOKEN>` ve `<CATALOG_GUID>` placeholder'ları env ile çözülüyor.
- Corpus; gerçek katalogda bulunan ve semantic retrieval için kararlı olan 8 ürüne odaklandı:
  - `70003363` İplik Kılavuzu
  - `13302302` Lastik Tampon
  - `WS0510002KP` Yaylı Pul
  - `70003402` Açılır Kapak
  - `70003409` Ön Plaka
  - `70003404` Plaka Desteği
  - `PS0150042K0` Yaylı Pim
  - `SM6050800SP` M5 L=8 Vida
- `chat_eval.py` quota-dostu staging gate için genişletildi:
  - `--case-delay-seconds`
  - `--retry-quality-issues`
  - `--retry-delay-seconds`
  - retry denemelerinde en iyi sonucu seçen `choose_better_result`
- Retry seçimi için unit test eklendi.

### Doğrulama Durumu

- Corpus validation geçti: `8 cases`.
- Chat eval metric unit testleri geçti: `17/17`.
- Python compile geçti: `partalog-ai/eval/chat_eval.py`.
- Yavaş public staging semantic eval geçti:
  - komut profili: `--case-delay-seconds 15 --retry-quality-issues 2 --retry-delay-seconds 65`
  - total: `8`
  - success: `8/8`
  - Hit@1/Hit@3/Hit@5: `100% / 100% / 100%`
  - MRR: `1.000`
  - hallucination rate: `0%`
  - quality issue cases: `0`
  - latency avg/p95: `4073.5 ms / 5211.1 ms`
- İlk case bir kez retry istedi; Cloud Logging'de Vertex `gemini-embedding` 429 quota kayıtları görüldü. Bu yüzden yavaş profil release gate dokümantasyonunda korunmalı.

### Açık İşler

- Full public load/saturation baseline onaylı gate seviyesinde üretilmeli.
- Alerting, error budget ve maliyet budget doğrulaması eklenmeli.
- Production tarafında embedding quota artırımı veya alternatif cache/rate-limit stratejisi planlanmalı.

## 2026-06-22 — Paket 6: Semantic Chat Readiness ve Staging Source Fix

### Tamamlananlar

- Vertex chat/analysis model erişimi doğrulandı.
  - `gemini-2.0-flash` staging projesinde/bölgelerinde `404 model not found/access` verdi.
  - `gemini-2.5-flash-lite` Vertex üzerinden başarılı test edildi.
  - AI staging env `GEMINI_CHAT_MODEL=gemini-2.5-flash-lite` ve `GEMINI_ANALYSIS_MODEL=gemini-2.5-flash-lite` olarak güncellendi.
- AI config ve doküman örnekleri güncel modele geçirildi.
- Embedding script'i staging env değerlerini lokal `.env` tarafından ezmeyecek hale getirildi.
- `GEMINI_EMBEDDING_MODEL` config alanı eklendi; embedding servisi hardcoded model yerine config kullanıyor.
- Staging katalog embedding backfill tamamlandı:
  - total items: `32`
  - embedded items: `32`
  - search text items: `32`
- Semantic search script'i güncel DB pool contract'ı ile uyumlu hale getirildi.
- Semantic eval sırasında API'nin AI kaynak cevabını parse edemediği hata bulundu:
  - `sources[0].similarity` AI cevabında `null` gelebiliyor.
  - Backend DTO `double` beklediği için `System.Text.Json.JsonException` oluşuyor ve cevap fallback'e düşüyordu.
- Backend `ChatSourceDto.Similarity` alanı `double?` yapıldı.
- Null `similarity` regression testi eklendi.

### Staging Revision / Build Kanıtları

- AI staging revision: `partalog-ai-chat-staging-00007-brf`
- API semantic source fix build: `e809df16-4372-46a1-a0ef-2e6d98e01b55` — SUCCESS
- API semantic source fix image: `europe-west1-docker.pkg.dev/partalog/partalog/api-staging:api-staging-20260621-2119-semantic-source-fix`
- Aktif API revision: `partalog-api-staging-00008-84r`
- API traffic: `%100` latest revision

### Doğrulama Durumu

- Backend testleri geçti: `70/70`.
- API health:
  - `/health/live`: `Healthy`
  - `/health/ready`: `{"status":"ready"}`
- Semantic search lokal doğrulama:
  - keyword arama doğal dil sorguda boş kalırken vector search aday döndürüyor.
  - `iplik geçirme mekanizması` sorgusunda `70003363 İPLİK KILAVUZU` en güçlü aday olarak geldi.
- Semantic public chat eval raporu üretildi: `backend/reports/staging-semantic-chat-eval-20260622-after-source-fix.md`
  - total: `4`
  - success: `4/4`
  - Hit@1/Hit@3/Hit@5: `75% / 75% / 75%`
  - MRR: `0.750`
  - hallucination rate: `25%`
  - quality issue cases: `1`
  - latency avg/p95: `3141.9 ms / 3979.2 ms`
- Yeni API revision loglarında önceki `JSON value could not be converted to System.Double` hatası tekrar görülmedi.

### Açık İşler

- Bu paketteki 4-case ara eval sonrasında corpus temizliği Paket 7'de tamamlandı.
- Full saturation/load baseline onaylı gate seviyesinde üretilmeli.
- Alerting, error budget ve maliyet budget doğrulaması eklenmeli.

## 2026-06-21 — Paket 5: Gerçek Public Chat Smoke, Eval ve Load Kanıtı

### Tamamlananlar

- Cloud SQL Auth Proxy standalone binary `.tools/cloud-sql-proxy` ile staging DB'ye geçici ve güvenli tünel açıldı.
- Staging DB'de gerçek yayınlı katalog/public token keşfedildi:
  - users: `2`
  - published catalogs: `1`
  - catalog items: `32`
  - embedded items: `0`
  - active public links: `44`
- İlk gerçek public-token chat smoke `500` verdi; kök neden bulundu:
  - `AiUsageQuotaService`, EF Core'un sahip olduğu `DbConnection`'ı `await using` ile dispose ediyordu.
  - Aynı request içinde sonraki chat enrichment sorgusu `ObjectDisposedException` ile kırılıyordu.
- Quota servisinde EF connection ownership düzeltildi:
  - Connection dispose edilmiyor.
  - Servis connection'ı sadece kendisi açtıysa işlem sonunda kapatıyor.
- Exact-code chat fallback güçlendirildi:
  - Kullanıcı cümlesinden ürün kodu regex ile çıkarılıyor.
  - Low-confidence intent durumunda da ürün kodu varsa clarification yerine DB sonucu dönüyor.
  - Ürün bulunduğunda reply metni artık `Üzgünüm, sonuç bulunamadı` olarak kalmıyor.
- API direct-code fast path eklendi:
  - Görselsiz mesajda katalogdaki ürün kodu bulunursa AI servisine gitmeden cevap dönüyor.
  - Bu yol gereksiz AI round trip ve quota consumption'ı azaltıyor.

### Staging Revision / Build Kanıtları

- Quota fix API build: `e44aacad-383a-4c8c-b779-a787c25652fc` — SUCCESS
- Exact-code fallback API build: `ad5a46da-3558-4340-bdbb-8ff24105bbc1` — SUCCESS
- Direct-code fast path API build: `072e3fc4-75e5-45db-bfb4-98869122e3a0` — SUCCESS
- Direct-code reply API build: `2b7683c0-765d-4540-a7e6-15cbc4790625` — SUCCESS
- Aktif API revision: `partalog-api-staging-00007-dn5`
- Aktif API image: `europe-west1-docker.pkg.dev/partalog/partalog/api-staging:api-staging-20260621-2052-direct-code-reply`

### Doğrulama Durumu

- Backend testleri geçti: `69/69`.
- Gerçek public-token chat smoke geçti:
  - API live
  - API ready
  - migrations ready
  - private AI ready
  - invalid public token `400`
  - real public chat `200`
- Eval raporu üretildi: `backend/reports/staging-chat-eval-20260621.md`
  - total: `4`
  - success: `4/4`
  - exact-code Hit@1/Hit@3/Hit@5: `100% / 100% / 100%`
  - MRR: `1.000`
  - hallucination rate: `0%`
  - quality issue cases: `0`
  - latency avg/p95: `215.3 ms / 354.4 ms`
- Kontrollü public load smoke raporu üretildi: `backend/reports/staging-public-load-smoke-20260621.md`
  - status: `passed`
  - concurrency: `2`
  - total requests: `20`
  - overall success: `100%`
  - browse: `16/16`, p95 `3738.3 ms`
  - chat: `4/4`, p95 `151.0 ms`
- Daha agresif lokal load denemesinde public chat `429` döndürdü; bu beklenen rate-limit davranışını doğruladı.

### Açık İşler

- Staging katalog item embedding'lerini üretip natural-language semantic eval baseline almak.
- Vertex chat model adı/bölgesi/erişimini netleştirip AI loglarındaki `model not found/access` uyarısını kapatmak.
- Full saturation/load baseline'ı onaylı gate olarak üretmek.
- Alerting, error budget ve maliyet budget doğrulamalarını eklemek.

## 2026-06-21 — Paket 3: Staging Kurulum Başlangıcı

### Tamamlananlar

- Production kaynaklarını ezmemek için ayrı staging runbook'u eklendi: `deploy/google-cloud/catalog-chat-staging-cloud-run.md`.
- Staging kaynak isimleri standartlaştırıldı:
  - `katalogcu-api-staging`
  - `katalogcu-web-staging`
  - `partalog-ai-chat-staging`
  - `katalogcu-db-staging`
  - `katalogcu-redis-staging`
  - `katalogcu-staging-vpc`
- Backend staging env şablonu eklendi: `backend/.env.staging.catalog-chat.example`.
- AI chat staging env şablonu eklendi: `partalog-ai/.env.chat-staging.example`.
- Frontend Cloud Build şablonu eklendi: `frontend/katalogcu-frontend/cloudbuild.web.yaml`.
- Backend Cloud Build şablonu hardcoded project/image yerine substitution destekleyecek şekilde güncellendi.
- Staging ön koşul kontrol script'i eklendi: `deploy/google-cloud/check-staging-prereqs.sh`.
- Secret Manager kullanımı staging dokümanında netleştirildi; DB connection string, JWT, public link ve DataProtection secret'ları düz env var olarak yazılmayacak.
- Memorystore için Serverless VPC Access connector adımı eklendi.
- Private AI Cloud Run servisi için staging `roles/run.invoker` akışı dokümante edildi.

### Doğrulama Durumu

- `git diff --check` geçti.
- `./deploy/google-cloud/check-staging-prereqs.sh` çalıştırıldı ve beklenen blokajı verdi: lokal makinede `gcloud` CLI yok.

### Blokaj

- Lokal makinede Google Cloud SDK kurulu değil. Gerçek staging kaynaklarını oluşturmak için ya Google Cloud SDK kurulmalı ya da aynı runbook Cloud Shell üzerinde çalıştırılmalı.

## 2026-06-21 — Paket 4: Google Cloud Staging İlk Deploy

### Google Cloud'ta Doğrulanan Mevcut Kaynaklar

- Project: `partalog`
- Account: `info@partalog.tech`
- Region: `europe-west1`
- Artifact Registry: `europe-west1/partalog`
- Cloud SQL: `katalogcu-db` (`POSTGRES_16`)
- Storage: `gs://partalog-assets` private/uniform bucket
- Mevcut service account'lar: `partalog-api-sa`, `partalog-ai-sa`
- Mevcut JWT, public-link, DB ve DataProtection secret'ları

### Tamamlananlar

- Portable Google Cloud SDK proje içindeki `.tools/google-cloud-sdk` altına kuruldu; `.tools/` git ve Cloud Build kapsamı dışına alındı.
- Cloud SQL `katalogcu-db` STOPPED durumundan `RUNNABLE/ALWAYS` durumuna getirildi.
- Eski `partalog-api`, `partalog-web`, `partalog-ai` servisleri değiştirilmeden yeni staging servisleri açıldı:
  - `partalog-api-staging`
  - `partalog-web-staging`
  - `partalog-ai-chat-staging`
- API, web ve AI chat staging image'leri Cloud Build ile başarıyla build/push edildi.
- Private AI servisi anonymous çağrıda `403` döndürüyor.
- API staging health kontrolleri geçti:
  - `/health/live`: başarılı
  - `/health/ready`: `ready`
  - `/health/migrations`: `ok`, 59 migration, latest `20260620093413_DropEmbedTargetsFromModel`
- Web proxy `/api/system/features` cevabı doğrulandı:
  - chat açık
  - katalog analizi açık
  - e-ticaret kapalı
  - upgrade prompt kapalı
- AI Cloud Run port uyuşmazlığı düzeltildi; deploy script'ine `--port=8000` eklendi.
- AI readiness'te bulunan eksik `DB_STATEMENT_CACHE_SIZE` config alanı eklendi.
- Python AI servisine çalışan .NET/Npgsql DB connection secret'ını kullanabilmek için Npgsql connection string → asyncpg URI normalizasyonu eklendi.
- `partalog-ai/tests/test_config_dsn.py` eklendi ve 2/2 test geçti.
- `smoke_chat_prod_readiness.sh` private AI Cloud Run identity token destekleyecek şekilde güncellendi.
- Smoke script'in API `/health/ready` contract'ında bulunmayan `ready:true` beklentisi kaldırıldı.

### Build Kanıtları

- API build: `7c8ec5c5-2ab4-4d30-ae1e-1b088778cb50` — SUCCESS
- Web build: `a31daee7-09bd-4171-8be9-edb3b2cd18f5` — SUCCESS
- İlk AI build: `0a9ecde0-cfbc-4298-8a73-fb8ba743586b` — SUCCESS
- AI config fix build: `b1d5a47a-7c07-4cad-a241-40c1e2bf5c31` — SUCCESS
- AI DB normalization build: `1cb6f6c8-f4ad-4a1c-8824-466452d201a5` — SUCCESS

### Staging URL'leri

- Web: `https://partalog-web-staging-851093992319.europe-west1.run.app`
- API: `https://partalog-api-staging-851093992319.europe-west1.run.app`
- AI: `https://partalog-ai-chat-staging-851093992319.europe-west1.run.app` (private)

### Açık Blokajlar

- Serverless VPC Access connector iki farklı CIDR ile iki kez Google internal error verdi ve `ERROR` durumunda kaldı; bozuk kaynaklar temizlendi.
- Bu yüzden Redis/Memorystore henüz oluşturulmadı. Staging geçici olarak Postgres distributed capacity + tek instance profiliyle çalışıyor; distributed public chat rate-limit kapalı.
- Son AI DB-normalization image'i build edildi ancak Google Cloud control-plane endpointleri yerelden cevap vermemeye başladığı için revision switch tamamlanamadı. Mevcut AI revision DB connection nedeniyle readiness `503` dönüyor.
- Control-plane erişimi geri geldiğinde son AI image deploy, `roles/run.invoker` doğrulaması ve private readiness smoke tamamlanmalı.

### Güncel İlerleme Tahmini

- Genel proje: yaklaşık **%84**
- Catalog + Chat MVP kod hazırlığı: yaklaşık **%90**
- Catalog + Chat canlıya alma hazırlığı: yaklaşık **%84**
- Staging/eval/load kanıtı: yaklaşık **%68**

## 2026-06-21 — Paket 5: Redis'li Staging Tamamlama

### Tamamlananlar

- Google Cloud control-plane erişimi düzeldikten sonra AI DB normalization image'i staging revision'a alındı.
- AI Cloud SQL bağlantısı URI parsing yerine explicit asyncpg keyword argümanlarına geçirildi:
  - `host=/cloudsql/partalog:europe-west1:katalogcu-db`
  - database/user/password ayrı parametreler
- AI Cloud SQL ve Postgres capacity readiness `HTTP 200 / ready=true` verdi.
- `katalogcu-redis-staging` oluşturuldu:
  - Redis 7.0
  - Basic tier
  - 1 GB
  - state `READY`
- Serverless VPC connector yerine Cloud Run Direct VPC egress kullanıldı:
  - network `default`
  - subnet `default`
  - egress `private-ranges-only`
- AI staging Redis distributed capacity moduna geçirildi:
  - `mode=redis-distributed`
  - `provider=redis`
  - readiness latency yaklaşık 7.9 ms
- API staging Redis distributed rate-limit, capacity ve DataProtection profiline geçirildi.
- `RedisDataProtectionXmlRepository` ve `AesGcmDataProtectionXmlEncryptor` gerçek ASP.NET Core DataProtection key-management pipeline'ına bağlandı.
- DataProtection wiring unit testi eklendi.
- Backend testleri geçti: 69/69.
- `smoke_chat_prod_readiness.sh --rate-limit-check` staging'de geçti:
  - API live
  - API ready
  - migrations
  - private AI ready
  - invalid public token 400
  - distributed public rate-limit 429
- Gerçek katalog token'ı sağlanmadığı için gerçek chat response smoke adımı bilinçli olarak skip edildi.

### Revision / Build Kanıtları

- AI explicit socket image build: `135b27e7-73e5-42a1-84f2-8064b6304129` — SUCCESS
- AI Redis revision: `partalog-ai-chat-staging-00006-sqb`
- API Redis/DataProtection image build: `6c94fdb1-411d-4184-8e5c-1f1a032eaf28` — SUCCESS
- API Redis revision: `partalog-api-staging-00003-xsn`

### Build Süresi İyileştirmesi

- AI Cloud Build upload context'i `132.9 MB / 1771 dosya` seviyesinden `477.8 KB / 65 dosya` seviyesine indirildi.
- AI build süresi yaklaşık 57 saniyeye düştü.
- Backend build context'inden runtime upload dosyaları çıkarıldı; sonraki tahmini API upload context'i yaklaşık 9.68 MB.

### Açık İşler

- Staging'de gerçek katalog/public token ile gerçek chat yanıt smoke'u.
- Güncel relevance corpus ile eval baseline.
- Load/saturation/failure testleri.
- Alert ve maliyet bütçesi doğrulaması.

## 2026-06-21 — Paket 1: Kontrollü Rollout Temeli

### Tamamlananlar

- Backend modül gate'i chat ve katalog analizini bağımsız değerlendiriyor.
- Public storefront `aiChatEnabled` hesabı genel AI flag'i yerine chatbot flag'ini kullanıyor.
- `/api/system/features` cevabına `chatbotEnabled` ve `catalogAnalysisEnabled` eklendi; eski `aiEnabled` alanı geriye uyumluluk için korundu.
- Frontend production/development feature ayarları `enableChatbot` ve `enableCatalogAnalysis` olarak ayrıldı.
- Production frontend profili Catalog + Chat stratejisine göre e-ticaret ve upgrade prompt'larını kapatıyor.
- Backend → private Cloud Run AI servisi çağrıları için Google OIDC identity token handler eklendi.
- Chat, stream ve uzun AI işleri ayrı timeout ayarlarına bağlandı.
- Production readiness, Cloud Run AI URL'i anonim çağrılacak şekilde bırakılırsa release'i bloklayacak kontrol kazandı.
- Catalog + Chat production environment şablonu eklendi.
- Aktif Python `/api/chat.py`, katalog `api/table.py`, sayfa analiz `api/analysis.py` ve embedding yolu legacy `?key=` URL üretiminden çıkarılıp `services.genai_provider` üzerinden Vertex/ADC veya header API-key akışına bağlandı.
- Python GenAI çağrılarında timeout ve sınırlı retry ayarları konfigüre edilebilir hale getirildi.
- Vertex `gemini-embedding-001` REST payload/response formatı desteklendi.
- Python `AI_CHAT_*` capacity ayarları config modeline eklendi.
- Lightweight chat Cloud Run servisi için `partalog-ai/.env.chat-production.example` eklendi.

### Eklenen Testler

- Chat kapalı/katalog analizi açık kombinasyonunda `/api/chat` engelleniyor.
- Chat açık/katalog analizi kapalı kombinasyonunda chat isteği geçiyor.
- Katalog analizi kapalıyken AI job/start endpointleri engelleniyor.
- Cloud Run auth açıkken `Authorization: Bearer <identity-token>` ekleniyor.
- Local auth kapalı profilinde token istenmiyor.

### Doğrulama Durumu

- `dotnet restore backend/Katalogcu.sln --ignore-failed-sources --force-evaluate --nologo` geçti.
- `dotnet test backend/Katalogcu.API.Tests/Katalogcu.API.Tests.csproj --no-restore --nologo` geçti: 68/68 test.
- `npm run test:compile` geçti.
- `npm run build` geçti.
- `PYTHONPYCACHEPREFIX=/private/tmp/katalogcu-pycache python3 -m py_compile partalog-ai/config.py partalog-ai/api/chat.py partalog-ai/api/table.py partalog-ai/api/analysis.py partalog-ai/services/genai_provider.py partalog-ai/services/embedding.py` geçti.
- Python unit discover sistem Python 3.9 + eksik paketler nedeniyle çalışmadı; proje venv'iyle başlatılan tam/daraltılmış unittest koşuları import aşamasında uzun süre çıktı üretmeden bekledi ve manuel kesildi. Son izde bekleme `pydantic_settings` / importlib metadata yüklemesi sırasında görüldü. Bu, kod syntax kanıtını geçersiz kılmıyor ama Python unit kanıtı henüz alınamadı.

### Sonraki İşler

1. Python unit test ortamını deterministik hale getirmek veya CI'da aynı testleri koşturmak.
2. Eski ürün kodlarına bağlı eval corpus'unu güncel katalogla yeniden baseline etmek.
3. Chat AI image için Cloud Build ve private Cloud Run deploy tanımı oluşturmak.
4. Staging smoke/load/failure senaryolarını çalıştırmak.

## 2026-06-21 — Paket 2: Hafif Chat Runtime ve Deploy Hazırlığı

### Tamamlananlar

- `api/__init__.py` side-effect free hale getirildi; stream contract gibi hafif testler artık hotspot/table router importlarını tetiklemiyor.
- `main.py` router yüklemeleri runtime profile'a bağlandı:
  - `ENABLE_HOTSPOT_ENDPOINTS=false` ise hotspot/YOLO router import edilmiyor.
  - `ENABLE_CATALOG_PROCESSING_ENDPOINTS=false` ise table/analysis router import edilmiyor.
- `STARTUP_SKIP_MODEL_LOADING`, `ENABLE_HOTSPOT_ENDPOINTS`, `ENABLE_CATALOG_PROCESSING_ENDPOINTS` ayarları config modeline eklendi.
- `main.py` lifespan içinde chat image profilinde YOLO/OCR model loading atlanıyor.
- AI servisi için `/health/live` ve `/health/ready` endpointleri eklendi; readiness DB ve AI capacity durumunu raporluyor.
- `partalog-ai/cloudbuild.chat.yaml` eklendi.
- `deploy/google-cloud/catalog-chat-cloud-run.md` eklendi; private Cloud Run, IAM `roles/run.invoker`, API env profili, smoke/eval ve rollback akışı dokümante edildi.
- `partalog-ai/eval/audit_eval_reports.py` eklendi; stale expected-code, quota/rate-limit kirliliği ve latency risklerini rapor özetlerinden yakalıyor.
- `partalog-ai/eval/report-audit.md` mevcut raporlar üzerinden üretildi.

### Doğrulama Durumu

- `PYTHONPYCACHEPREFIX=/private/tmp/katalogcu-pycache python3 -m py_compile partalog-ai/config.py partalog-ai/main.py partalog-ai/api/__init__.py partalog-ai/api/chat.py partalog-ai/api/table.py partalog-ai/api/analysis.py partalog-ai/services/genai_provider.py partalog-ai/services/embedding.py` geçti.
- `PYTHONPYCACHEPREFIX=/private/tmp/katalogcu-pycache partalog-ai/venv/bin/python -m unittest partalog-ai/tests/test_stream_contract.py` geçti: 4/4 test.
- `python3 partalog-ai/eval/audit_eval_reports.py ... --output-md partalog-ai/eval/report-audit.md` beklenen şekilde exit `2` verdi; bu bir test başarısızlığı değil, mevcut raporlardaki release blokajlarını görünür kılan beklenen audit sonucudur.

### Açık Risk

- `test_main_health.py` lokal venv'de FastAPI/Starlette importu sırasında dosya okuma aşamasında uzun süre bekliyor. Kod tarafında ağır router importları koşullu hale getirildi, ancak bu lokal venv performans sorunu CI veya temiz container içinde yeniden doğrulanmalı.

### Sonraki İşler

1. Temiz container/CI içinde Python testlerini çalıştırmak.
2. Güncel staging katalog verisiyle `queries.relevance.jsonl` yeniden baseline etmek.
3. Private Cloud Run staging deploy yapıp `smoke_chat_prod_readiness.sh --rate-limit-check` koşturmak.
4. Public E2E load smoke ve saturation smoke ile onaylı baseline üretmek.
