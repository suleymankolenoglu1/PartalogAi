# Docker Orchestration (MVP)

Bu dosya `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/docker-compose.yml` için çalışma notudur.

## Ayağa Kaldırma

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend
docker compose up -d --build
```

## Servisler

- `db` (PostgreSQL + pgvector): `localhost:5432`
- `partalog-ai` (FastAPI): `localhost:8000`
- `api` (.NET): `localhost:5159`
- `frontend` (Nginx + Angular build): `localhost:4200`

## Notlar

- `partalog-ai` servisi `../partalog-ai/.env` dosyasını kullanır.
- API, compose içinde `AiService__BaseUrl=http://partalog-ai:8000` ile AI servisine bağlanır.
- API bağlantı dizesi compose içinde `db` servisini hedefler.
- `visual_parts_data` volume'u `partalog-ai` ve `api` arasında paylaşılır.
  - AI tarafı: `/shared/visual-parts`
  - API tarafı: `/app/wwwroot/static/visual-parts`
- Frontend Nginx `/api/*` isteklerini compose içindeki `api:8080` servisine proxy eder.
- Tüm servisler aynı `katalogcu_net` network'ündedir.
