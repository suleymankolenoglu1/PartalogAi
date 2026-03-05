# Katalogcu

Katalog yönetim ve ürün tanıma sistemi. AI destekli katalog oluşturma ve hotspot yönetimi.

## 📋 Proje Hakkında

Katalogcu, dijital kataloglar oluşturmak ve yönetmek için geliştirilmiş modern bir web uygulamasıdır. YOLO (You Only Look Once) AI modeli kullanarak katalog sayfalarındaki ürünleri otomatik olarak tanıyabilir ve tıklanabilir alanlar (hotspot) oluşturabilir.

## 🏗️ Mimari

Bu proje Clean Architecture prensiplerine uygun olarak 3 ana bileşenden oluşur:

- **Backend**: .NET 9 Web API
- **Frontend**: Angular uygulaması  
- **YOLO Service**: Python tabanlı AI servisi

## 📚 Detaylı Dokümantasyon

Proje dosya yapısı ve tüm bileşenlerin detaylı açıklaması için:
👉 **[PROJE_YAPISI.md](./PROJE_YAPISI.md)** dosyasına bakınız.

Backend migration süreç disiplini için:
👉 **[backend/MIGRATION_DISCIPLINE.md](./backend/MIGRATION_DISCIPLINE.md)** dosyasına bakınız.

Docker orkestrasyonu için:
👉 **[backend/DOCKER_ORCHESTRATION.md](./backend/DOCKER_ORCHESTRATION.md)** dosyasına bakınız.

MVP smoke testleri için:
👉 **[backend/SMOKE_TESTS.md](./backend/SMOKE_TESTS.md)** dosyasına bakınız.

Tek paket (Catalog Only) canlıya çıkış kontrol listesi için:
👉 **[backend/CATALOG_ONLY_PROD_CHECKLIST.md](./backend/CATALOG_ONLY_PROD_CHECKLIST.md)** dosyasına bakınız.

Catalog-only Go/No-Go karar formu için:
👉 **[backend/CATALOG_ONLY_RELEASE_GO_NO_GO.md](./backend/CATALOG_ONLY_RELEASE_GO_NO_GO.md)** dosyasına bakınız.

Catalog-only release rapor scripti:
👉 **[backend/scripts/generate_catalog_release_report.sh](./backend/scripts/generate_catalog_release_report.sh)**

## 🚀 Hızlı Başlangıç

### Gereksinimler
- .NET 9 SDK
- Node.js ve npm
- Python 3.8+
- Docker ve Docker Compose
- PostgreSQL

### Kurulum

1. **Tüm servisleri Docker Compose ile başlatın (önerilen):**
```bash
cd backend
docker compose up -d --build
```

Servisler:
- Frontend: `http://localhost:4200`
- Backend API: `http://localhost:5159`
- Swagger: `http://localhost:5159/swagger`
- Partalog AI: `http://localhost:8000`

2. **Alternatif: Servisleri manuel çalıştırın**

**Backend:**
```bash
cd backend/Katalogcu.API
dotnet restore
dotnet run
```

**Frontend:**
```bash
cd frontend/katalogcu-frontend
npm install
npm start
```

**Partalog AI servisi:**
```bash
cd partalog-ai
pip install -r requirements.txt
python main.py
```

## 🔑 Özellikler

- ✅ Katalog yönetimi (Oluştur, Güncelle, Sil)
- ✅ Ürün yönetimi
- ✅ Kullanıcı kimlik doğrulama (JWT)
- ✅ PDF yükleme ve işleme
- ✅ Excel export
- ✅ **YOLO AI entegrasyonu** - Backend ile tam entegre
- ✅ **Otomatik hotspot tespiti** - YOLO servisi üzerinden
- ✅ OCR desteği

## 🛠️ Teknolojiler

- **Backend**: .NET 9, Entity Framework Core, PostgreSQL, HttpClient
- **Frontend**: Angular, TypeScript
- **AI**: Python, YOLO, FastAPI, OpenCV
- **DevOps**: Docker, Docker Compose

## 📖 API Dokümantasyonu

Backend çalıştığında Swagger UI'a erişebilirsiniz:
```
http://localhost:5000/swagger
```

## 🤝 Katkıda Bulunma

Pull request'ler memnuniyetle karşılanır. Büyük değişiklikler için lütfen önce bir issue açarak neyi değiştirmek istediğinizi tartışın.

## 📄 Lisans

[MIT](https://choosealicense.com/licenses/mit/)
