# Public Load Baseline

The `Public E2E Load Smoke` workflow compares successful request throughput
against an approved baseline when this file exists:

`backend/load-baselines/public-e2e-load-baseline.json`

To promote a baseline:

1. Run the workflow against staging with the intended duration, concurrency,
   scenario weights, and chat queries.
2. Confirm that success, latency, degraded-stream, and first-token gates pass.
3. Download `public-e2e-load-smoke-<run number>` and review the JSON report.
4. Add the approved report with the baseline filename above in a dedicated PR.

Comparisons require matching report schema, duration, concurrency, timeout,
scenario weights, and chat queries. By default, the workflow fails when overall
or enabled-scenario successful RPS regresses by more than 20 percent. Set the
`PARTALOG_MAX_THROUGHPUT_REGRESSION_RATE` Actions variable to change that limit.
