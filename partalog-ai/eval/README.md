# Chat Eval v1

Bu klasör chatbot kalitesini ölçmek için hızlı bir eval aracı içerir.

## Ölçülen metrikler

- `SuccessRate`
- `Latency avg/p95`
- `Hit@1`, `Hit@3`, `Hit@5`, `MRR` (`expected_codes` verilen case'lerde)
- `No-code pass rate` (`expect_no_codes=true` verilen case'lerde)
- `Required-term pass rate`
- `Forbidden-term pass rate`
- `Hallucination rate` (cevapta geçen kod benzeri token'lar, dönen ürün/model/isim-açıklama içindeki identifier setinde yoksa)

## Case formatı (JSONL)

Her satır bir case:

```json
{
  "id": "Q1",
  "text": "yamato vida arıyorum",
  "public_token": "<PUBLIC_TOKEN>",
  "catalog_ids": [],
  "expected_codes": ["160000"],
  "expect_no_codes": false,
  "required_terms": ["vida"],
  "forbidden_terms": ["elimizde yok"]
}
```

## Çalıştırma

`partalog-ai` klasöründe:

```bash
python eval/chat_eval.py \
  --base-url http://localhost:5159 \
  --cases eval/queries.sample.jsonl \
  --output-json eval/report.json \
  --output-md eval/report.md
```

Zorlayıcı mixed set:

```bash
python eval/chat_eval.py \
  --base-url http://localhost:5159 \
  --cases eval/queries.hard.jsonl \
  --output-json eval/report.hard.json \
  --output-md eval/report.hard.md
```

Nightly regression set:

```bash
python eval/chat_eval.py \
  --base-url http://localhost:5159 \
  --cases eval/queries.nightly.jsonl \
  --output-json eval/report.nightly.json \
  --output-md eval/report.nightly.md
```

## Placeholder kullanımı (önerilen)

`queries.*.jsonl` içindeki `<PUBLIC_TOKEN>` değerini elle değiştirmek yerine:

```bash
export PARTALOG_PUBLIC_TOKEN="..."
export PARTALOG_CATALOG_IDS="guid-1,guid-2"
```

`PARTALOG_CATALOG_IDS` boş bırakılırsa katalog filtresi uygulanmaz.

## CI threshold (opsiyonel)

Kalite eşiği koymak için script'e limit verebilirsin. Eşik sağlanmazsa çıkış kodu `3` döner.

```bash
python eval/chat_eval.py \
  --base-url http://localhost:5159 \
  --cases eval/queries.hard.jsonl \
  --min-success-rate 1.0 \
  --min-hit-at-1 0.9 \
  --max-latency-p95-ms 8000 \
  --max-hallucination-rate 0.05 \
  --min-no-code-pass-rate 0.9
```

## GitHub Actions (CI Gate)

Workflow dosyası: `.github/workflows/chat-eval-gate.yml`

Gerekli repository secrets:

- `PARTALOG_BASE_URL` (örn: `https://api.senin-domainin.com`)
- `PARTALOG_PUBLIC_TOKEN` (public-view token)

Bu workflow PR/push sırasında `queries.hard.jsonl` setini eşiklerle çalıştırır; eşik geçilmezse pipeline fail olur.

Nightly workflow: `.github/workflows/chat-eval-nightly.yml`

- Her gece otomatik çalışır (`cron`).
- `queries.nightly.jsonl` setini koşar.
- Çıktı olarak JSON/Markdown + kısa summary artifact yükler.

## Notlar

- Public senaryo için `public_token` kullan.
- `catalog_ids` opsiyoneldir. Verirsen aramayı daraltır.
- `--fail-on-error` kullanılırsa mantıksal fallback/hata cevapları da hata sayılır (çıkış kodu `2`).
