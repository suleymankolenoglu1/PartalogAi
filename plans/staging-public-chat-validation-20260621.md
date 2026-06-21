# Staging Public Chat Validation — 2026-06-21

## Ortam

- Project: `partalog`
- Region: `europe-west1`
- API: `https://partalog-api-staging-851093992319.europe-west1.run.app`
- AI: `https://partalog-ai-chat-staging-851093992319.europe-west1.run.app`
- Web: `https://partalog-web-staging-851093992319.europe-west1.run.app`
- Aktif API revision: `partalog-api-staging-00007-dn5`

## Bulgular ve Düzeltmeler

1. İlk gerçek public-token chat smoke `500` verdi.
   - Kök neden: `AiUsageQuotaService`, EF Core'a ait `DbConnection` nesnesini dispose ediyordu.
   - Düzeltme: connection dispose edilmiyor; servis sadece kendisi açtıysa kapatıyor.

2. Exact-code sorguları ürün bulmasına rağmen kalite zayıftı.
   - Kök neden: düşük-confidence intent clarification'ı ürün kodu fallback'inden önce dönüyordu.
   - Düzeltme: ürün kodu cümleden çıkarılıyor ve direct DB fallback ile zenginleştiriliyor.

3. Exact-code sorguları AI round trip yüzünden gereksiz yavaştı.
   - Düzeltme: API direct-code fast path eklendi; görselsiz mesajda ürün kodu katalogda varsa AI çağrısı yapılmadan cevap dönüyor.

4. Ürün bulunduğu halde reply metni `Üzgünüm, sonuç bulunamadı` kalabiliyordu.
   - Düzeltme: `finalProducts` doluysa ve AI answer boşsa reply metni ürün kodlarıyla üretiliyor.

## Doğrulama

- Backend tests: `69/69` geçti.
- Production readiness smoke: geçti.
  - API live/ready
  - migrations ready
  - private AI ready
  - invalid public token `400`
  - real public chat `200`
- Chat eval:
  - total: `4`
  - success: `4/4`
  - exact-code Hit@1/Hit@3/Hit@5: `100% / 100% / 100%`
  - MRR: `1.000`
  - hallucination rate: `0%`
  - quality issue cases: `0`
  - latency avg/p95: `215.3 ms / 354.4 ms`
- Controlled public load smoke:
  - status: `passed`
  - concurrency: `2`
  - total requests: `20`
  - overall success: `100%`
  - browse: `16/16`, p95 `3738.3 ms`
  - chat: `4/4`, p95 `151.0 ms`

## Notlar

- Staging katalogda `CatalogItems.Embedding = 0`; bu yüzden bu rapor exact-code ve public-token readiness kanıtıdır, tam semantic relevance kanıtı değildir.
- Daha agresif lokal load denemesi public chat `429` üretti; distributed rate-limit'in aktif olduğunu doğruladı.
- AI loglarında Vertex `gemini-2.0-flash` için model erişim/bölge uyarısı görüldü. Direct-code fast path bu yolu bypass ediyor, ancak natural-language chat kalitesi için bu ayar ayrıca kapatılmalı.
