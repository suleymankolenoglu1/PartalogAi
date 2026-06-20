# Vertex AI ve AI Servis Canli Plani

Bu dokuman catalog-only canli cikisin arkasindan VLM, YOLO ve OCR tarafini Google Cloud uzerinde nasil canliya alacagimizi tarif eder.

## Kisa Karar

Ilk canli cikis icin AI servisleri kapali kalabilir. Musteriye acilan katalog akisi stabil olduktan sonra AI katmanini ayri bir faz olarak acmak daha dogru.

AI fazinda onerilen ayrim:

- VLM / Gemini: Vertex AI Gemini API.
- Embedding: Vertex AI veya mevcut Gemini embedding API'den Vertex AI uyumlu cagrilara gecis.
- YOLO hotspot detection: once Cloud Run GPU ile mevcut FastAPI servisinin ayri servis olarak deploy edilmesi.
- YOLO uzun vadeli MLOps: Vertex AI custom container endpoint.

## Neden Boyle?

Mevcut `partalog-ai` servisi sadece model inference yapmiyor; dosya aliyor, YOLO calistiriyor, OCR ile etiket okuyor, Gemini ile gorsel analiz yapiyor, embedding uretiyor ve DB/storage ile konusuyor. Bu yuzden tamamini dogrudan Vertex AI endpoint'e koymak ilk asamada pahali ve yavas bir refactor olur.

En hizli guvenli yol:

1. Catalog-only production'u Cloud Run + Cloud SQL ile yayina almak.
2. Backend dosya kaliciligini Cloud Storage'a tasimak.
3. VLM cagrilarini API key yerine Vertex AI IAM/Service Account ile calistirmak.
4. YOLO icin ayri `partalog-ai` Cloud Run GPU servisi acmak.
5. Trafik ve maliyet netlesince YOLO modelini Vertex AI Model Registry + Endpoint'e tasimak.

## Faz 1: Vertex AI Gemini / VLM

Mevcut kodda Gemini cagrilari `generativelanguage.googleapis.com` ve API key uzerinden gidiyor. Production icin bunu Vertex AI'a tasiyacagiz.

Gerekli ortam degiskenleri:

```text
AI_PROVIDER=vertex
GOOGLE_CLOUD_PROJECT=<PROJECT_ID>
VERTEX_AI_LOCATION=global
VERTEX_AI_GEMINI_MODEL=<selected-flash-model>
VERTEX_AI_EMBEDDING_MODEL=<selected-embedding-model>
```

Gerekli kod isleri:

- `partalog-ai` icinde Gemini cagrilarini tek bir provider abstraction arkasina almak.
- Mevcut API key yolunu local/dev icin korumak.
- Production'da Vertex AI icin Application Default Credentials veya Cloud Run service account kullanmak.
- VLM response JSON sozlesmesini testlerle sabitlemek.
- Rate limit, timeout ve retry politikasi eklemek.

Cloud Run service account icin minimum roller:

- Vertex AI User
- Secret Manager Secret Accessor
- Cloud SQL Client, sadece AI servisi DB'ye baglanacaksa
- Storage Object Viewer/Creator, gorsel dosyalar Cloud Storage'dan okunup yazilacaksa

## Faz 2: YOLO Icin Cloud Run GPU

Mevcut FastAPI servisi Cloud Run GPU'ya daha hizli tasinir. Bu yolda mevcut endpointler buyuk oranda korunur:

- `/api/hotspot/detect`
- `/api/hotspot/read-label`
- `/api/chat/visual-feedback`
- `/health`

Gerekli isler:

- `partalog-ai` Docker image'ini GPU uyumlu hale getirmek.
- `models/best.pt` dosyasini container'a gommek yerine Cloud Storage'dan indirmek veya image artifact olarak versiyonlamak.
- `STARTUP_SKIP_MODEL_LOADING=false` ile production readiness saglamak.
- `FAIL_STARTUP_ON_UNREADY=true` yapmak.
- Backend `PartalogAiService` base URL'ini `partalog-ai` Cloud Run URL'ine cevirmek.
- Cloud Run ingress'i internal yapip sadece backend service account'unun cagirmasini tercih etmek.

Onerilen baslangic ayarlari:

```text
CPU=4
MEMORY=16Gi
GPU=1 x L4
MIN_INSTANCES=0
MAX_INSTANCES=1 veya 2
CONCURRENCY=1-4, model performansina gore
```

Bu yolun avantaji hizdir. Dezavantaji model registry, traffic split ve endpoint governance tarafinda Vertex AI kadar duzenli olmamasidir.

## Faz 3: YOLO Icin Vertex AI Custom Endpoint

Model MLOps ihtiyaci netlesince YOLO'yu Vertex AI'a tasiyabiliriz.

Gerekli isler:

- YOLO model artifact'ini `gs://.../models/yolo/best.pt` gibi versiyonlu saklamak.
- Vertex AI custom container uyumlu predict/health route eklemek.
- Input sozlesmesini JSON/base64 veya GCS URI olarak standartlastirmak.
- Output sozlesmesini mevcut hotspot schema ile uyumlu tutmak.
- Artifact Registry'ye custom inference image push etmek.
- Vertex AI Model upload, Endpoint create, Deploy model akisini kurmak.
- Backend ya dogrudan Vertex AI endpoint cagirir ya da `partalog-ai` adapter servisi Vertex endpoint'i arkasinda saklar.

Bu yolun avantaji model versiyonlama, endpoint yonetimi ve daha kurumsal MLOps. Dezavantaji ilk kurulum/refactor maliyetidir.

## Storage Blokeri

AI icin Cloud Storage daha da kritik hale geliyor. VLM ve YOLO ikisi de katalog sayfa gorsellerine erismek zorunda. Cloud Run lokal diskinde uretilen `wwwroot/uploads` dosyalari kalici olmadigi icin once backend dosya storage katmani GCS'e tasinmali.

Yapilacaklar:

- Backend upload/PDF page dosyalarini Cloud Storage'a yazmak.
- Public katalog gorselleri icin signed URL veya public-read CDN stratejisi secmek.
- AI servisine GCS URI veya HTTPS URL gondermek.
- `partalog-ai` icindeki mevcut S3/GCS uyumlu storage ayarlarini production secret/env ile calistirmak.

## Canliya Alma Sirasi

1. Catalog-only Cloud Run production.
2. Cloud Storage backend entegrasyonu.
3. Vertex AI Gemini provider.
4. AI servisini Cloud Run CPU modunda smoke test, model loading kapali.
5. YOLO Cloud Run GPU smoke test, tek katalog uzerinde.
6. Backend feature flag ile sadece admin/internal kullanicilara acmak.
7. Maliyet/latency gozlemi.
8. Tum kullanicilara acmak.
9. Gerekirse YOLO'yu Vertex AI custom endpoint'e tasimak.

## Go / No-Go

AI fazi canliya acilmadan once:

- VLM JSON parse basari orani kabul edilebilir olmali.
- YOLO model loading production'da deterministik olmali.
- Ortalama hotspot detection suresi olculmeli.
- Cloud Storage dosyalari restart/redeploy sonrasinda kaybolmamali.
- AI hata alirsa katalog yayinlama akisi bozulmamali.
- AI maliyeti icin Cloud Billing budget alert kurulmus olmali.
