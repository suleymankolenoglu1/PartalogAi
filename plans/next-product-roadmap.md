# Partalog Sonraki Urun Roadmap'i

**Tarih:** 2026-05-17
**Kapsam:** Katalog + AI chat + gorsel arama + B2B siparis/servis akislarinin bir sonraki urun fazi

---

## 1. Urun Hedefi

Partalog'un bir sonraki fazdaki hedefi yalnizca katalog gosteren veya soruya cevap veren bir arayuz olmak degil:

1. Musterinin hangi makineye sahip oldugunu bilen,
2. O makine icin uygun parcayi guvenilir sekilde daraltan,
3. Ariza bilgisini katalog verisiyle birlestiren,
4. Konusmayi teklif, siparis veya servis aksiyonuna ceviren

bir **aftermarket servis platformu** olmaktir.

---

## 2. Mevcut Temel

Bugun projede zaten kullanilabilecek guclu taslar var:

| Alan | Mevcut durum |
|---|---|
| Katalog verisi | Parca kodu, ref no, sayfa, marka, model, machine group, mekanizma alanlari mevcut |
| AI chat | Streaming, intent, teshiş, katalog kaynaklari, feedback, gorsel yukleme mevcut |
| Gorsel altyapi | Visual embedding, OCR, shape tags, visual feedback mevcut |
| Ticaret altyapisi | Sepet, fiyat/stok cozumleme, siparis, musteri hesabi mevcut |
| Harici veri | External site crawling, external product match, manual review akislari mevcut |
| Analitik | Katalog goruntuleme, storefront trafik, chat feedback ekranlari mevcut |

Bu nedenle yeni faz sifirdan platform kurmak degil; bu taslari dogru sirayla birbirine baglamaktir.

---

## 3. Onceliklendirme Ozeti

| Seviye | Tema | Neden simdi |
|---|---|---|
| P0 | Makine baglami + guvenilir bilgi | Yanlis parca onerme riskini dusurur, chat kalitesini kokten iyilestirir |
| P0 | Chat'ten aksiyona gecis | Musteriyi yalnizca bilgilendirmez, is sonucuna goturur |
| P1 | Gorsel arama v2 + B2B satis akisleri | Mevcut altyapinin gelir ve kullanim degerini aciga cikarir |
| P1 | Operasyon analitigi | Urun ve servis kararlarini veriye baglar |
| P2 | Harici pazar zekasi + cok dillilik | Platformu farklilastirir, ama temel veri modeli oturduktan sonra gelmelidir |
| P3 | Predictive maintenance | Degerli ama servis gecmisi ve makine verisi olusmadan erken olur |

---

## 4. P0 Backlog

### 4.1 Makine Profili / Installed Base

**Amac:** Musteri makinesini bir kez tanitsin, sistem sonraki sorularda ayni bilgiyi tekrar sormasin.

**Kullanici hikayeleri**

1. Musteri kendi hesabina makine ekleyebilir.
2. Musteri birden fazla makine kaydedebilir.
3. Public chat oturumunda aktif makine secilebilir.
4. Chat cevap verirken aktif makineyi kaynak baglami olarak kullanir.
5. Model belirsizse sistem once makineyi netlestirir, kesin uyumluluk iddiasi kurmaz.

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `CustomerMachines` | `Id`, `CustomerId`, `Brand`, `Model`, `Variant`, `MachineGroup`, `SerialNumber`, `DisplayName`, `IsActive`, `CreatedDate` |
| `ChatSessionMachines` | `Id`, `ChatSessionId`, `CustomerMachineId`, `SnapshotBrand`, `SnapshotModel`, `SnapshotVariant`, `CreatedDate` |

**Mevcut kodla baglanti**

- `CatalogItems.MachineBrand`, `MachineModel`, `MachineGroup` alanlari ile eslesir.
- Public chat, secili makineyi prompt/context icine tasir.
- Musteri siparis gecmisinde makine bazli tekrar siparis raporu tutulabilir.

**Kabul kriterleri**

- Musteri aktif makine secerse chat cevaplarinda o model gorunur.
- Model eslesmiyorsa sistem "aday parca" dili kullanir.
- Musteri ayni soruyu tekrar sordugunda marka/model tekrar sorulmaz.

### 4.2 Uyumluluk Grafigi

**Amac:** Katalog satirini tek basina degil, uyumluluk iliskileriyle birlikte anlamak.

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `MachineModels` | `Id`, `Brand`, `Model`, `Variant`, `MachineGroup`, `AliasesJson` |
| `PartCompatibilityRules` | `Id`, `CatalogItemId`, `MachineModelId`, `CompatibilityLevel`, `SourceType`, `Confidence`, `Notes` |
| `PartReplacements` | `Id`, `FromPartCode`, `ToPartCode`, `ReplacementType`, `Source`, `EffectiveFromUtc` |
| `AssemblyGroups` | `Id`, `Name`, `MachineModelId`, `PageNumber`, `Notes` |
| `AssemblyGroupItems` | `Id`, `AssemblyGroupId`, `CatalogItemId`, `Role` |

**CompatibilityLevel onerisi**

- `Exact`
- `Likely`
- `SameAssembly`
- `Unknown`
- `Incompatible`

**Neden ayri tablo gerekli**

- `MachineModel` alani tek basina "kesin uyumlu" demek icin yetersizdir.
- Ayni parca farkli varyantlarda kullanilabilir.
- Eski/yeni kod, muadil kod ve montaj grubu bilgisi ayri iliski olarak tutulmalidir.

**Kabul kriterleri**

- Chat "kesin uyumlu", "muhtemel aday", "ayni montaj grubunda" ayrimini yapabilir.
- Eski kod soruldugunda yeni kod onerilebilir.
- Model belirtilmediginde sistem kesinlik yerine daraltici soru sorar.

### 4.3 Onayli Ariza Rehberleri

**Amac:** Chat'in sadece genel model bilgisine degil, isletmenin onayladigi servis bilgisina dayanmasi.

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `TroubleshootingGuides` | `Id`, `Title`, `Symptom`, `MachineGroup`, `Severity`, `Status`, `Version`, `CreatedBy`, `ApprovedBy`, `PublishedAtUtc` |
| `TroubleshootingGuideSteps` | `Id`, `GuideId`, `StepNo`, `Title`, `Instruction`, `SafetyLevel`, `ExpectedObservation` |
| `TroubleshootingGuideParts` | `Id`, `GuideId`, `CatalogItemId`, `Role`, `Required`, `Notes` |
| `TroubleshootingGuideMachineModels` | `Id`, `GuideId`, `MachineModelId` |

**Yeni yetenekler**

- Güvenlik adimlari
- Kontrol sirasi
- "Hangi durumda ustaya birakilmali" kismi
- Model bazli farkli varyasyonlar
- Taslak, onayli, arsivli versiyonlama

**Kabul kriterleri**

- Chat ariza sorusunda once rehber arar.
- Rehber varsa cevapta adim sirasi korunur.
- Rehber yoksa sistem bunu acikca belirtir ve genel usta yorumu olarak ayirir.

### 4.4 Chat'ten Aksiyona Gecis

**Amac:** Cevap sonunda kullaniciya dogru sonraki adimi vermek.

**Yeni aksiyonlar**

| Aksiyon | Ne yapar |
|---|---|
| `Teklif iste` | Secili parca veya aday liste icin RFQ olusturur |
| `Ustaya aktar` | Konusma ozeti ve baglamla servis kaydi acar |
| `Sepete ekle` | Uygun ve satisa acik urunu sepete koyar |
| `WhatsApp mesaji olustur` | Musteriye gonderilecek net servis metni hazirlar |

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `QuoteRequests` | `Id`, `CustomerId`, `CustomerMachineId`, `Status`, `RequestedBy`, `Notes`, `CreatedDate` |
| `QuoteRequestItems` | `Id`, `QuoteRequestId`, `CatalogItemId`, `PartCode`, `Quantity`, `Reason` |
| `ServiceCases` | `Id`, `CustomerId`, `CustomerMachineId`, `SourceChatId`, `Status`, `Priority`, `Summary`, `CreatedDate` |
| `ServiceCaseEvents` | `Id`, `ServiceCaseId`, `EventType`, `PayloadJson`, `CreatedDate` |

**Kabul kriterleri**

- Chat cevabindan dogrudan RFQ veya servis kaydi acilabilir.
- Operasyon ekibi chat ozeti ve onerilen parcalari gorur.
- Aksiyon kaydi raporlanabilir hale gelir.

---

## 5. P1 Backlog

### 5.1 Visual Search v2

**Yeni yetenekler**

1. Cekim rehberi: "parcayi ustten cek", "kod gorunur olsun", "arka plan sade olsun"
2. Otomatik crop ve kalite kontrol
3. Top-k benzer parca kartlari
4. Guven skoru + nedenleri
5. Yanlis sonuc duzeltme kuyruğu
6. Duzeltmeden sonra embedding guncelleme

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `VisualSearchSessions` | `Id`, `UserId`, `ImageUrl`, `MachineContextJson`, `CreatedDate` |
| `VisualSearchCandidates` | `Id`, `SessionId`, `CatalogItemId`, `Score`, `Rank`, `ReasonJson` |
| `VisualSearchCorrections` | `Id`, `SessionId`, `SelectedCatalogItemId`, `CorrectCatalogItemId`, `Reason`, `ReviewedBy` |

**Ekranlar**

- Public image search sonuc ekrani
- Admin "yanlis eslesmeler" kuyruğu
- Visual feedback detay ekrani

### 5.2 B2B Satis Akislari

**Mevcut altyapi uzerine eklenecekler**

1. RFQ ile siparis ayrimi
2. Musteri bazli fiyat
3. Stok gorunurlugu
4. Tekrar siparis
5. Son siparisten sepete ekle
6. Minimum siparis miktari
7. Teslim suresi

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `CustomerPriceLists` | `Id`, `CustomerId`, `Currency`, `ValidFromUtc`, `ValidToUtc` |
| `CustomerPriceListItems` | `Id`, `PriceListId`, `PartCode`, `UnitPrice`, `MinQuantity` |
| `SavedCarts` | `Id`, `CustomerId`, `Name`, `CreatedDate` |
| `SavedCartItems` | `Id`, `SavedCartId`, `PartCode`, `Quantity` |

**Ekranlar**

- Musteri portalinda "tekrar siparis ver"
- Admin "teklif talepleri"
- Fiyat listesi yonetimi

### 5.3 Operasyon Analitigi

**Yeni dashboard metrikleri**

1. No-result oranı
2. Low-confidence oranı
3. En cok sorulan makine modelleri
4. En cok cevapsiz kalan semptomlar
5. Chat -> teklif donusumu
6. Chat -> siparis donusumu
7. Gorsel arama kabul/red oranı
8. Ustaya aktarim oranı

**Yeni tablolar**

| Tablo | Temel alanlar |
|---|---|
| `ChatOutcomeEvents` | `Id`, `ChatId`, `OutcomeType`, `PayloadJson`, `CreatedDate` |
| `SearchQualitySnapshots` | `Id`, `Date`, `NoResultRate`, `LowConfidenceRate`, `TopMissingTermsJson` |

---

## 6. P2 Backlog

### 6.1 Harici Pazar Zekasi

**Ozellikler**

- Rakip fiyat takibi
- Link saglik takibi
- OEM esdegerleri
- Muadil alternatifler
- Tedarikci bazli stok farklari

**Mevcut altyapi avantaji**

- External site crawling
- External product match
- Manual review

Bu nedenle sifirdan baslamak yerine var olan external match modulu genisletilir.

### 6.2 Cok Dillilik

**Ozellikler**

- UI metinleri
- Rehber metinleri
- Chat sistem mesajlari
- Katalog parca adlarinda cok dilli alias

**Neden P2**

- Veri modeli ve bilgi yonetimi oturmadan ceviri eklemek kaliteyi carpabilir.

### 6.3 Sesli / Mobil Usta Modu

**Ozellikler**

- Sesle soru sorma
- Cevabi sesli okutma
- Kamera ile hizli parca arama
- Düşük bant genisligi modu

---

## 7. P3 Backlog

### Predictive Maintenance

**Gerekli on kosullar**

1. Musteri makine profili
2. Servis gecmisi
3. Parca tuketim gecmisi
4. Ariza olaylari
5. Mumkunse telemetri veya kullanim saati

**Daha sonra eklenecekler**

- Ariza olasiligi
- Tavsiye edilen bakım zamani
- Muhtemel parca ihtiyaci
- Filoya gore anomali tespiti

---

## 8. Ekran Listesi

| Ekran | Hedef kullanici | Faz |
|---|---|---|
| Makine Parkim | Musteri | P0 |
| Aktif Makine Secimi | Public chat kullanicisi | P0 |
| Uyumluluk Kural Yoneticisi | Admin | P0 |
| Ariza Rehberi Yoneticisi | Admin / servis lideri | P0 |
| Teklif Talepleri | Operasyon | P0 |
| Servis Vakasi Detayi | Operasyon | P0 |
| Visual Search Sonuclari | Musteri | P1 |
| Yanlis Gorsel Eslesmeler | Admin | P1 |
| Fiyat Listesi Yoneticisi | Admin | P1 |
| Tekrar Siparis | Musteri | P1 |
| AI Operasyon Dashboard | Admin | P1 |
| Harici Pazar Karsilastirma | Admin | P2 |

---

## 9. API Taslagi

### Makine profili

- `GET /api/customer-machines`
- `POST /api/customer-machines`
- `PATCH /api/customer-machines/{id}`
- `DELETE /api/customer-machines/{id}`
- `POST /api/chat/sessions/{id}/machine`

### Uyumluluk

- `GET /api/compatibility/parts/{partCode}`
- `POST /api/compatibility/rules`
- `PATCH /api/compatibility/rules/{id}`
- `GET /api/machines/{machineModelId}/parts`

### Ariza rehberi

- `GET /api/troubleshooting-guides`
- `POST /api/troubleshooting-guides`
- `PATCH /api/troubleshooting-guides/{id}`
- `POST /api/troubleshooting-guides/{id}/publish`

### Teklif / servis

- `POST /api/quote-requests`
- `GET /api/quote-requests`
- `POST /api/service-cases`
- `GET /api/service-cases`
- `PATCH /api/service-cases/{id}`

### Analitik

- `GET /api/analytics/chat-outcomes`
- `GET /api/analytics/search-quality`

---

## 10. Sprint Plani

### Sprint 1 - Makine Baglami

**Cikti**

- `CustomerMachines`
- Public chat'te aktif makine secimi
- Chat context'e machine snapshot
- Ilk compatibility smoke testleri

### Sprint 2 - Uyumluluk Temeli

**Cikti**

- `MachineModels`
- `PartCompatibilityRules`
- `PartReplacements`
- Chat'te exact / likely / unknown dili
- Admin uyumluluk kurali CRUD

### Sprint 3 - Ariza Rehberi

**Cikti**

- Guide tabloları
- Admin guide editor
- Chat retrieval sirasina guide katmani
- "Genel yorum" ile "onayli rehber" ayrimi

### Sprint 4 - Chat'ten Aksiyona

**Cikti**

- RFQ
- Service case
- Chat cevabinda aksiyon CTA'lari
- Operasyon panelinde gelen is listesi

### Sprint 5 - Visual Search v2

**Cikti**

- Gorsel kalite kontrol
- Top-k adaylar
- Correction queue
- Feedback'ten ogrenme metrigi

### Sprint 6 - B2B Satis Pilotu

**Cikti**

- E-ticaret flag pilot ortamda acik
- Tekrar siparis
- Musteri fiyat listesi
- RFQ -> siparis gecisi

### Sprint 7 - Operasyon Analitigi

**Cikti**

- AI operasyon dashboard
- No-result / low-confidence / conversion metrikleri
- Haftalik kalite raporu

---

## 11. Basari Metrikleri

| Metrik | Hedef |
|---|---|
| Model bilinmeyen sorgularda gereksiz parca onerme | Azalis |
| Makine secildikten sonra tekrar model sorma | Azalis |
| Low-confidence cevap oranı | Azalis |
| Chat -> teklif donusumu | Artis |
| Chat -> siparis donusumu | Artis |
| Gorsel arama kabul orani | Artis |
| No-result oranı | Azalis |
| Ustaya aktarimdan cozum suresi | Azalis |

---

## 12. Simdilik Yapilmamasi Gerekenler

1. Veri modeli oturmadan cok dillilik.
2. Servis gecmisi olmadan predictive maintenance.
3. Sadece prompt degisikligiyle kaliteyi sonsuza kadar arttirmaya calismak.
4. Uyum bilgisi olmadan agresif capraz satis onerileri.
5. Her problemi chatbot uzerinden cozmeye calisip operasyon ekranlarini ihmal etmek.

---

## 13. Onerilen Ilk Uygulama Sirasi

1. Makine profili
2. Uyumluluk grafigi
3. Onayli ariza rehberi
4. Chat'ten teklif / servis aksiyonu
5. Visual search v2
6. B2B satis pilotu
7. AI operasyon dashboard

Bu sira, yanlis cevap riskini en hizli dusuren ve ayni zamanda urunu gelir ureten bir servis platformuna en hizli yaklastiran siradir.
