# Catalog + Grounded Chat Canlıya Alma Planı

**Başlangıç tarihi:** 2026-06-21
**Durum:** Google Cloud staging aktif; gerçek chat eval/load doğrulaması sürüyor
**Hedef:** Public katalog ile kaynak gösteren metin/SSE chat'i güvenli ve geri alınabilir biçimde canlıya açmak.

## 0. İlerleme Değerlendirmesi

**Son güncelleme:** 2026-06-21

Staging ortamı henüz olmadığı için canlıya alma güveni sınırlı tutuldu. Kod, konfigürasyon, deploy şablonları ve yerel doğrulama tarafındaki ilerleme güçlü; ancak production-benzeri eval, smoke, load ve rollback provaları henüz çalıştırılamadı.

Tahmini durum:

- Genel proje ilerlemesi: yaklaşık **%84**
- Catalog + Chat MVP kod hazırlığı: yaklaşık **%90**
- Catalog + Chat canlıya alma hazırlığı: yaklaşık **%84**
- Staging/eval/load kanıtı: yaklaşık **%68**

Staging kurulup smoke, eval ve load kapıları yeşile dönmeden canlıya hazır oranı sorumlu şekilde **%80 üstüne taşınmamalıdır**.

## 1. MVP Kapsamı

Canlıya açık olacaklar:

- Kullanıcı kayıt/giriş ve katalog yönetimi
- PDF yükleme, katalog yayınlama ve public katalog bağlantısı
- Katalog verisine dayalı metin chat
- SSE yanıt akışı, kaynak kartları ve kullanıcı feedback'i
- Kota, Redis tabanlı public rate limit ve distributed AI capacity guard
- Katalog AI analiz iş akışı

İlk sürümde kapalı kalacaklar:

- E-ticaret, checkout ve sipariş
- Plan/abonelik yönetimi ve upgrade prompt'ları
- External site crawling
- Görsel chat, GCS ve görsel eval kanıtı tamamlanana kadar kontrollü beta

## 2. Zorunlu Feature Flag Profili

```text
ProductFeatures__EnableChatbot=true
ProductFeatures__EnableCatalogAnalysis=true
ProductFeatures__EnableEcommerce=false
ProductFeatures__EnableUpgradePrompts=false
ProductFeatures__EnablePlanManagement=false
```

Üretim şablonu: `backend/.env.production.catalog-chat.example`
AI chat servis şablonu: `partalog-ai/.env.chat-production.example`

Rollback yalnızca `ProductFeatures__EnableChatbot=false` yaparak chat'i kapatabilmeli; katalog analizi bundan etkilenmemelidir.

## 3. Altyapı Kararı

- API ve chat AI servisi ayrı Cloud Run servisleridir.
- Chat AI image'i `partalog-ai/Dockerfile.chat` ile oluşturulur; YOLO/model loading bu serviste kapalıdır.
- AI servisi public/anonymous değildir.
- API service account, AI servisinde yalnızca `roles/run.invoker` rolüne sahiptir.
- API çağrıları Google imzalı OIDC identity token taşır.
- Vertex erişimi AI servisinin kendi service account'u ile yapılır; statik service-account key kullanılmaz.
- Redis rate limit, capacity ve DataProtection key-ring için zorunludur.
- Kalıcı dosyalar Google Cloud Storage'da tutulur.

## 4. Kalite Kapıları

- Backend, frontend ve Python testleri yeşil
- Güncel production-benzeri katalogla relevance corpus yeniden baseline edilmiş
- Success rate `>= %99`
- Hit@1 `>= %80`, Hit@3 `>= %90`
- Hallucination rate `<= %5`
- Yanıt p95 `<= 8 sn`
- İlk SSE token p95 `<= 5 sn`
- Degraded fallback `<= %5`
- Context, yanlış kod, out-of-domain, kota ve rate-limit senaryoları yeşil
- `/health/ready` ve `/health/migrations` başarılı
- `smoke_chat_prod_readiness.sh --rate-limit-check` başarılı
- Staging yük testi onaylı baseline ile karşılaştırılmış

## 5. Rollout

1. Production-benzeri staging deploy edilir.
2. Bir eval tenant/token ile tüm kalite ve yük kapıları çalıştırılır.
3. Production'da global chat flag açılır fakat tenant/storefront `AiChatEnabled` yalnızca pilot kataloglarda etkinleştirilir.
4. Pilot 24–48 saat izlenir.
5. 5xx, 429, latency, fallback, negatif feedback ve Vertex maliyeti kabul edilebilir ise kapsam genişletilir.
6. Eşik ihlalinde chat flag kapatılır; katalog yayınlama ve analiz akışı açık kalır.

## 6. Açık İşler

- [x] Backend chat ve katalog analizi feature gate'lerini ayır
- [x] Frontend chat görünürlüğünü ayrı flag'e bağla
- [x] Private Cloud Run identity token handler ekle
- [x] Catalog+Chat production env şablonu ekle
- [x] Python Gemini çağrılarını provider/Vertex uyumlu timeout ve sınırlı retry akışına bağla
- [x] AI chat service production env şablonu ekle
- [x] AI chat Cloud Build ve private Cloud Run deploy dokümanı ekle
- [x] Eval rapor audit script'i ekle
- [x] Staging Cloud Run / Cloud SQL / Redis runbook ve env şablonlarını ekle
- [x] Google Cloud SDK kur ve API/web/private-AI staging servislerini oluştur
- [x] AI DB normalization image'ini staging revision'a al ve readiness'i yeşile çevir
- [x] Connector hatasını Direct VPC ile aş, Redis'i kur ve distributed rate-limit'i aç
- [ ] Güncel eval tenant/corpus oluştur ve baseline al
- [ ] Staging smoke, load ve failure testlerini çalıştır
- [ ] Alert ve maliyet bütçesini doğrula
