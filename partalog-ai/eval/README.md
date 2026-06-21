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
- `Quality issue counts` (`logical_error`, `expected_code_missing`, `expected_code_not_rank1`, `required_term_missing`, `forbidden_term_present`, `hallucinated_code` gibi kırılım nedenleri)
- `Category metrics` (kategori bazinda success, `Hit@1/3/5`, `MRR`, no-code ve quality issue sayisi)

## Case formatı (JSONL)

Her satır bir case:

```json
{
  "id": "Q1",
  "category": "specification",
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

Relevance corpus formatını secrets veya çalışan API olmadan doğrulamak için:

```bash
python eval/chat_eval.py \
  --cases eval/queries.relevance.jsonl \
  --validate-only
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
  --min-hit-at-1 0.80 \
  --min-hit-at-3 0.90 \
  --min-hit-at-5 0.95 \
  --min-mrr 0.85 \
  --max-latency-p95-ms 8000 \
  --max-hallucination-rate 0.05 \
  --min-no-code-pass-rate 0.9 \
  --min-required-term-pass-rate 0.95 \
  --min-forbidden-term-pass-rate 1.0 \
  --min-category-hit-at-1 exact_code=0.80 \
  --min-category-no-code-pass-rate negative=1.0
```

Kategori bazlı eşikler `CATEGORY=RATE` formatındadır ve tekrar edilebilir. Örneğin `exact_code`
kategorisinin `Hit@1` metriğini, `negative` kategorisinin no-code başarısını veya `model_typo`
kategorisinin `MRR` değerini ayrı ayrı gate edebilirsin.

## GitHub Actions (CI Gate)

Workflow dosyası: `.github/workflows/chat-eval-gate.yml`

Gerekli repository secrets:

- `PARTALOG_BASE_URL` (örn: `https://api.senin-domainin.com`)
- `PARTALOG_PUBLIC_TOKEN` (public-view token)

Bu workflow PR/push sırasında önce `queries.relevance.jsonl` formatını offline doğrular. Secrets varsa ayni sabit relevance benchmarkini `Hit@1/3/5`, `MRR`, no-code, latency ve hallucination esikleriyle calistirir; esik gecilmezse pipeline fail olur.

Nightly workflow: `.github/workflows/chat-eval-nightly.yml`

- Her gece otomatik çalışır (`cron`).
- `queries.nightly.jsonl` setini koşar.
- Çıktı olarak JSON/Markdown + kısa summary artifact yükler.

## Notlar

- Public senaryo için `public_token` kullan.
- `catalog_ids` opsiyoneldir. Verirsen aramayı daraltır.
- `--fail-on-error` kullanılırsa mantıksal fallback/hata cevapları da hata sayılır (çıkış kodu `2`).

## Release öncesi rapor audit'i

Eski ürün kodlarına bağlı stale corpus veya kota/rate-limit kaynaklı kirli raporları hızlı görmek için:

```bash
python eval/audit_eval_reports.py \
  eval/report.after.hard.json \
  eval/report.catalog_smoke.json \
  --output-md eval/report-audit.md
```

Script bağımlılıksızdır. `stale_expected_codes_suspected`, `quota_or_rate_limit_pollution`
veya `latency_gate_risk` bulursa çıkış kodu `2` döndürür. Bu durum release'i tek başına
fail etmek zorunda değildir; ama güncel katalogla yeniden baseline alınmadan GO verilmemelidir.
