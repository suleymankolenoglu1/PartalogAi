#!/usr/bin/env bash
set -euo pipefail

REGION="${REGION:-europe-west1}"
REPOSITORY="${REPOSITORY:-partalog}"

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

warn() {
  echo "WARN: $*" >&2
}

info() {
  echo "INFO: $*"
}

if command -v gcloud >/dev/null 2>&1; then
  GCLOUD="gcloud"
elif [[ -x ".tools/google-cloud-sdk/bin/gcloud" ]]; then
  GCLOUD=".tools/google-cloud-sdk/bin/gcloud"
else
  fail "gcloud CLI bulunamadı. Google Cloud SDK kur, Cloud Shell kullan veya .tools/google-cloud-sdk/bin/gcloud kurulumunu tamamla."
fi
command -v git >/dev/null 2>&1 || fail "git bulunamadı."

ACCOUNT="$("$GCLOUD" auth list --filter=status:ACTIVE --format='value(account)' | head -n 1 || true)"
if [[ -z "$ACCOUNT" ]]; then
  fail "Aktif gcloud hesabı yok. Önce: gcloud auth login"
fi
info "gcloud account: $ACCOUNT"

PROJECT_ID="$("$GCLOUD" config get-value project 2>/dev/null || true)"
if [[ -z "$PROJECT_ID" || "$PROJECT_ID" == "(unset)" ]]; then
  fail "gcloud project seçili değil. Önce: gcloud config set project <PROJECT_ID>"
fi
info "project: $PROJECT_ID"
info "region: $REGION"

if ! "$GCLOUD" billing projects describe "$PROJECT_ID" --format='value(billingEnabled)' >/tmp/katalogcu-billing-enabled.txt 2>/dev/null; then
  warn "Billing durumu okunamadı. Yetki eksik olabilir; Cloud Console'dan billing açık mı kontrol et."
else
  BILLING_ENABLED="$(cat /tmp/katalogcu-billing-enabled.txt)"
  if [[ "$BILLING_ENABLED" != "True" && "$BILLING_ENABLED" != "true" ]]; then
    fail "Project billing kapalı görünüyor. Cloud SQL / Cloud Run / Memorystore için billing gerekli."
  fi
  info "billing: enabled"
fi

REQUIRED_SERVICES=(
  run.googleapis.com
  sqladmin.googleapis.com
  artifactregistry.googleapis.com
  secretmanager.googleapis.com
  cloudbuild.googleapis.com
  storage.googleapis.com
  redis.googleapis.com
  vpcaccess.googleapis.com
  aiplatform.googleapis.com
)

for SERVICE in "${REQUIRED_SERVICES[@]}"; do
  if "$GCLOUD" services list --enabled --filter="config.name:$SERVICE" --format='value(config.name)' | grep -Fxq "$SERVICE"; then
    info "enabled: $SERVICE"
  else
    warn "not enabled: $SERVICE"
  fi
done

if "$GCLOUD" artifacts repositories describe "$REPOSITORY" --location="$REGION" >/dev/null 2>&1; then
  info "artifact registry exists: $REGION/$REPOSITORY"
else
  warn "artifact registry missing: $REGION/$REPOSITORY"
fi

info "Ön koşul kontrolü tamamlandı. Eksik API/repo varsa deploy dokümanındaki kurulum adımlarıyla oluştur."
