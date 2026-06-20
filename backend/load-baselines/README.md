# Public Load Baseline

The `Public E2E Load Smoke` workflow compares successful request throughput
against an approved baseline when this file exists:

`backend/load-baselines/public-e2e-load-baseline.json`

To promote a baseline from staging:

1. Run `Public E2E Saturation Smoke` against staging with the intended duration,
   concurrency levels, scenario weights, and chat queries.
2. Confirm that success, latency, degraded-stream, first-token, and saturation
   analysis gates pass.
3. Download `public-e2e-saturation-<run number>` and review
   `public-e2e-load-baseline.candidate.md` plus
   `public-e2e-load-baseline.candidate.json`.
4. If the candidate is acceptable, add it as
   `backend/load-baselines/public-e2e-load-baseline.json` in a dedicated PR.

You can also regenerate the candidate locally from downloaded reports:

```bash
python backend/scripts/promote_public_load_baseline.py \
  --saturation-summary-json backend/reports/public-e2e-saturation-summary.json \
  --reports-dir backend/reports \
  --output-json backend/load-baselines/public-e2e-load-baseline.json \
  --output-md backend/load-baselines/public-e2e-load-baseline.md \
  --require-base-url-contains staging
```

For a single passed load-smoke report:

```bash
python backend/scripts/promote_public_load_baseline.py \
  --report-json backend/reports/public-e2e-load-smoke.json \
  --output-json backend/load-baselines/public-e2e-load-baseline.json \
  --output-md backend/load-baselines/public-e2e-load-baseline.md \
  --require-base-url-contains staging
```

Comparisons require matching report schema, duration, concurrency, timeout,
scenario weights, and chat queries. By default, the workflow fails when overall
or enabled-scenario successful RPS regresses by more than 20 percent. Set the
`PARTALOG_MAX_THROUGHPUT_REGRESSION_RATE` Actions variable to change that limit.
