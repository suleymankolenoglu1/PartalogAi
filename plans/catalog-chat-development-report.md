# Catalog + Chat Geliştirme Raporu

Bu dosya Catalog + Grounded Chat MVP için yapılan değişikliklerin yaşayan kaydıdır.

## 2026-06-21 — Güncel İlerleme Notu

Staging ortamı şu an olmadığı için canlıya alma oranı konservatif hesaplandı.

- Genel proje ilerlemesi önceki yaklaşık `%72` seviyesinden yaklaşık **%78** seviyesine çıktı.
- Catalog + Chat MVP kod hazırlığı yaklaşık **%84** seviyesinde.
- Catalog + Chat canlıya alma hazırlığı yaklaşık **%74** seviyesinde.
- Staging/eval/load kanıtı yaklaşık **%35** seviyesinde; bu alan ana blokaj.

Bu artış; chat ve katalog analiz feature gate'lerinin ayrılması, private Cloud Run OIDC auth akışı, Vertex/provider geçişi, timeout/retry ayarları, hafif chat runtime profili, health endpointleri, deploy şablonları ve audit tooling sayesinde geldi. Staging kurulmadan canlıya hazırlığı **%80+** göstermek yanıltıcı olur.

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
