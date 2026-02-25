# Smoke Tests (MVP)

## Public Checkout Smoke

Dosya:
- `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/scripts/smoke_public_checkout.py`

Ne test eder:
1. Public token ile katalogların gelmesi
2. Katalog ürünlerinin gelmesi
3. Public müşteri register/login
4. Sipariş oluşturma
5. Anonim kullanıcı için privileged endpoint blokları (`/api/orders`, `/api/users`, `/api/files/upload`)
6. Müşteri sipariş listesi + sipariş detayı (`X-Public-Session` header ile)
7. Idempotency replay (`idempotentReplay=true`)
8. (Opsiyonel) Admin `/api/orders` kontrolü

### Çalıştırma

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu

export PARTALOG_PUBLIC_TOKEN="...public_token..."
# opsiyonel:
# export PARTALOG_ADMIN_TOKEN="...admin_jwt..."

python /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/scripts/smoke_public_checkout.py \
  --base-url http://localhost:5159
```

Opsiyonel sabit ürün/katalog:

```bash
python /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/backend/scripts/smoke_public_checkout.py \
  --base-url http://localhost:5159 \
  --catalog-id "<catalog-guid>" \
  --product-id "<product-guid>"
```

## Full Stack Smoke (Compose + Checkout)

Tek komutla compose ayağa kaldırır, servis health bekler, checkout smoke koşar:

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
export PARTALOG_PUBLIC_TOKEN="...public_token..."
./backend/scripts/smoke_all.sh
```

`PARTALOG_PUBLIC_TOKEN` verilmezse script otomatik bootstrap yapar:
- admin kullanıcı oluşturur/giriş yapar
- katalog + ürün oluşturur ve yayınlar
- public token üretir
- checkout smoke adımlarını bununla çalıştırır

Opsiyonel:

```bash
./backend/scripts/smoke_all.sh --skip-up --base-url http://localhost:5159
./backend/scripts/smoke_all.sh --down-after
./backend/scripts/smoke_all.sh --no-build
./backend/scripts/smoke_all.sh --no-bootstrap --public-token "<token>"
./backend/scripts/smoke_all.sh --skip-up --skip-ai-check --skip-frontend-check
```

## CI Gate

PR/PUSH için otomatik smoke gate:
- `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/.github/workflows/regression-smoke-gate.yml`
- `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/.github/workflows/public-checkout-smoke.yml`

Workflow `smoke_all.sh` scriptini remote API üstünde `--skip-up` modunda çalıştırır.
`regression-smoke-gate` workflow'u deterministik olması için `PARTALOG_PUBLIC_TOKEN` secret'ını zorunlu ister ve `--no-bootstrap` ile çalışır.
