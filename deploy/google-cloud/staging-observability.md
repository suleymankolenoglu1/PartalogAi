# Staging Observability

## Active Google Cloud Resources

- API readiness uptime check: `Partalog staging API readiness`
- Web availability uptime check: `Partalog staging web availability`
- Availability policy: `Partalog staging public availability`
- Cloud Run reliability policy: `Partalog staging Cloud Run reliability`
- Billing budget: `Partalog project monthly guardrail`

Uptime checks run every minute from Europe, Iowa and Asia Pacific. The API check
requires HTTP 200 from `/health/ready` and a response containing `ready`.

The reliability policy opens an incident when API or AI 5xx rate is above
`0.01/s`, or API p95 request latency stays above `5000 ms`. The availability
policy opens an incident when either public uptime check is below 100% for two
minutes.

The monthly billing budget is scoped to project number `851093992319`, uses the
previous calendar period as its amount, and evaluates current spend at 50%, 80%
and 100%, plus forecasted spend at 100%.

The private AI chat service keeps one minimum instance because request logs
showed an approximately 6.3 second container cold start. API and web remain at
zero minimum instances. Review this incremental cost through the active monthly
budget before changing the minimum or maximum scale.

## Apply Policies

These policy definitions are intentionally version controlled:

```bash
.tools/google-cloud-sdk/bin/gcloud monitoring policies create \
  --project=partalog \
  --policy-from-file=deploy/google-cloud/monitoring/staging-public-availability-policy.json

.tools/google-cloud-sdk/bin/gcloud monitoring policies create \
  --project=partalog \
  --policy-from-file=deploy/google-cloud/monitoring/staging-cloud-run-reliability-policy.json
```

Before recreating an uptime check, update the `metric.label.check_id` values in
the availability policy. Do not run the create commands repeatedly; update the
existing policies when changing thresholds.

## Notification Channel Gap

Cloud Monitoring notification channels require an explicitly selected and,
for email, verified recipient. The policies still create incidents in Cloud
Monitoring without a channel. Add the production on-call channel before the
production go-live gate; do not assume a personal address in automation.

2026-06-22 read-back found `0` notification channels in the `partalog` project.
Use `deploy/google-cloud/monitoring/notification-channel-runbook.md` once the
production on-call recipient is selected. The helper script
`deploy/google-cloud/monitoring/create_email_notification_channel.py` creates or
reuses an email channel and attaches it to the staging availability/reliability
policies.
