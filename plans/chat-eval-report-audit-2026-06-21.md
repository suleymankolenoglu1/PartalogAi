# Chat Eval Report Audit — 2026-06-21

Bu rapor, mevcut eval JSON çıktılarını release-readiness açısından hızlıca yorumlamak için üretildi.

Komut:

```bash
python3 partalog-ai/eval/audit_eval_reports.py \
  partalog-ai/eval/report.after.hard.json \
  partalog-ai/eval/report.behavior_smoke.latest.json \
  partalog-ai/eval/report.catalog_smoke.json \
  --output-md partalog-ai/eval/report-audit.md
```

`partalog-ai/eval/report-audit.md` generated artifact olarak `.gitignore` kapsamındadır; kalıcı özet bu dosyadır.

## Özet

| Rapor | Bulgular | Release yorumu |
|---|---|---|
| `report.after.hard.json` | Success `%100`, hallucination `%0`, Hit@1 `%0`, p95 `12153.8ms` | Eski expected code / stale corpus şüphesi ve latency gate riski var. |
| `report.behavior_smoke.latest.json` | `403=20`, `429=9` | Eval token/kota/rate-limit kirliliği var; kalite kanıtı sayılmaz. |
| `report.catalog_smoke.json` | Success `%100`, Hit@1 `%50`, p95 `6718.1ms` | Özet seviyesinde bariz blokaj yok, ama release eşiği için yeterli değil. |

## Karar

Mevcut raporlarla Catalog + Chat production GO verilemez.

GO için yeni staging tenant/token ile:

1. Güncel katalogdaki gerçek ürün kodlarıyla `queries.relevance.jsonl` yeniden baseline edilmeli.
2. Eval kullanıcısında kota/rate-limit kirliliği olmayacak ayrı token kullanılmalı.
3. Threshold'lar tekrar çalıştırılmalı:
   - Success rate `>= %99`
   - Hit@1 `>= %80`
   - Hit@3 `>= %90`
   - Hallucination `<= %5`
   - p95 latency `<= 8000ms`
   - No-code pass `>= %90`
