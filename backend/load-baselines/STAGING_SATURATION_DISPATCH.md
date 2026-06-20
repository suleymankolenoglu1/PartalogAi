# Staging Saturation Dispatch

Use this checklist to generate the first approved public load baseline from
staging. This does not deploy to production.

## Preconditions

- `main` branch checks are green.
- GitHub Actions secrets are configured:
  - `PARTALOG_BASE_URL` points to staging.
  - `PARTALOG_PUBLIC_TOKEN` is a staging public catalog token.
- `PARTALOG_BASE_URL` contains the selected `baseline_base_url_marker`.
- Checkout/order side effects are not desired; keep checkout weight at `0`.

## Workflow Dispatch

Open GitHub Actions and run `Public E2E Saturation Smoke` on `main`.

| input | value |
|---|---|
| `duration_seconds` | `60` |
| `concurrency_levels` | `[4,8,16]` |
| `min_throughput_gain_rate` | `0.10` |
| `max_throughput_drop_rate` | `0.10` |
| `baseline_base_url_marker` | `staging` |

The workflow validates these inputs before load starts. `concurrency_levels`
must be a sorted JSON integer array with at least two unique positive values.

## Expected Artifact

Download `public-e2e-saturation-<run number>` and review:

- `public-e2e-saturation-summary.json`
- `public-e2e-load-baseline.candidate.md`
- `public-e2e-load-baseline.candidate.json`

The candidate is only generated when the run passes and the report base URL
contains the configured staging marker.

## Review Decision

Promote the baseline only when all of these are true:

- Overall and enabled-scenario successful throughput look stable.
- Overall and scenario p95 latency are below the configured thresholds.
- Stream degraded fallback rate is acceptable.
- Stream first-token p95 is acceptable.
- The recommended concurrency matches the intended baseline profile.
- No bottleneck scenario blocks the intended rollout profile.

## Promote

If accepted, add the candidate JSON as:

`backend/load-baselines/public-e2e-load-baseline.json`

Open a dedicated PR. `Public Load Baseline Gate` validates that committed
baseline before merge.

## Reject

If rejected, do not commit the candidate. Keep the artifact attached to the
workflow run and capture the reason in the PR or release notes, for example:

- staging returned degraded SSE fallback responses,
- p95 latency exceeded the threshold,
- throughput regressed at higher concurrency,
- the staging URL or token was misconfigured.
