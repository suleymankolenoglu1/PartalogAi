#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND_DIR="$ROOT_DIR/backend"
REPORTS_DIR="$BACKEND_DIR/reports"
PRECHECK_SCRIPT="$BACKEND_DIR/scripts/preflight_catalog_only.sh"
POSTDEPLOY_SCRIPT="$BACKEND_DIR/scripts/postdeploy_catalog_only_check.sh"

API_URL="${API_URL:-http://localhost:5159}"
PREFLIGHT_EXTRA_ARGS="${PREFLIGHT_EXTRA_ARGS:-}"
WITH_PREFLIGHT=false
WITH_POSTDEPLOY=false
OUTPUT_PATH=""

print_usage() {
  cat <<'USAGE'
Usage:
  generate_catalog_release_report.sh [options]

Options:
  --output <path>       Output markdown path
  --api-url <url>       API base URL (default: http://localhost:5159)
  --with-preflight      Run preflight (runtime checks skipped)
  --with-postdeploy     Run post-deploy checks
  -h, --help            Show help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output)
      OUTPUT_PATH="$2"
      shift 2
      ;;
    --api-url)
      API_URL="$2"
      shift 2
      ;;
    --with-preflight)
      WITH_PREFLIGHT=true
      shift
      ;;
    --with-postdeploy)
      WITH_POSTDEPLOY=true
      shift
      ;;
    -h|--help)
      print_usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      print_usage
      exit 1
      ;;
  esac
done

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

safe_eval_config_status() {
  python - "$ROOT_DIR/backend/Katalogcu.API" <<'PY'
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
p = root / "appsettings.json"
if not p.exists():
    p = root / "appsettings.example.json"
data = json.loads(p.read_text(encoding="utf-8"))
pf = data.get("ProductFeatures") or {}
ok = (
    pf.get("EnableAi") is False
    and pf.get("EnableEcommerce") is False
    and pf.get("EnableUpgradePrompts") is False
)
print("GO" if ok else "NO-GO")
PY
}

run_and_capture() {
  local command="$1"
  local log_file="$2"
  local result_file="$3"

  set +e
  bash -lc "$command" >"$log_file" 2>&1
  local exit_code=$?
  set -e

  if [[ $exit_code -eq 0 ]]; then
    echo "GO" > "$result_file"
  else
    echo "NO-GO" > "$result_file"
  fi
}

require_cmd git
require_cmd python

mkdir -p "$REPORTS_DIR"
timestamp_utc="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
timestamp_compact="$(date -u +"%Y%m%d_%H%M%S")"

if [[ -z "$OUTPUT_PATH" ]]; then
  OUTPUT_PATH="$REPORTS_DIR/catalog_only_release_${timestamp_compact}.md"
fi

git_branch="$(git -C "$ROOT_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "unknown")"
git_sha="$(git -C "$ROOT_DIR" rev-parse --short HEAD 2>/dev/null || echo "unknown")"

config_status="$(safe_eval_config_status)"
preflight_status="NOT-RUN"
postdeploy_status="NOT-RUN"
preflight_log="-"
postdeploy_log="-"

if [[ "$WITH_PREFLIGHT" == "true" ]]; then
  preflight_log="$REPORTS_DIR/preflight_${timestamp_compact}.log"
  preflight_result="$REPORTS_DIR/preflight_${timestamp_compact}.result"
  run_and_capture "\"$PRECHECK_SCRIPT\" --api-url \"$API_URL\" --skip-runtime $PREFLIGHT_EXTRA_ARGS" "$preflight_log" "$preflight_result"
  preflight_status="$(cat "$preflight_result")"
fi

if [[ "$WITH_POSTDEPLOY" == "true" ]]; then
  postdeploy_log="$REPORTS_DIR/postdeploy_${timestamp_compact}.log"
  postdeploy_result="$REPORTS_DIR/postdeploy_${timestamp_compact}.result"
  run_and_capture "\"$POSTDEPLOY_SCRIPT\" --api-url \"$API_URL\"" "$postdeploy_log" "$postdeploy_result"
  postdeploy_status="$(cat "$postdeploy_result")"
fi

migration_status="NOT-RUN"
frontend_status="NOT-RUN"
health_status="NOT-RUN"
gate_status="NOT-RUN"

if [[ "$preflight_status" == "GO" ]]; then
  if [[ " $PREFLIGHT_EXTRA_ARGS " == *" --skip-migration-check "* ]]; then
    migration_status="NOT-RUN"
  else
    migration_status="GO"
  fi
  frontend_status="GO"
elif [[ "$preflight_status" == "NO-GO" ]]; then
  if [[ " $PREFLIGHT_EXTRA_ARGS " == *" --skip-migration-check "* ]]; then
    migration_status="NOT-RUN"
  else
    migration_status="NO-GO"
  fi
  frontend_status="NO-GO"
fi

if [[ "$postdeploy_status" == "GO" ]]; then
  health_status="GO"
  gate_status="GO"
elif [[ "$postdeploy_status" == "NO-GO" ]]; then
  health_status="NO-GO"
  gate_status="NO-GO"
fi

overall="GO"
for s in "$config_status" "$migration_status" "$frontend_status" "$health_status" "$gate_status"; do
  if [[ "$s" == "NO-GO" ]]; then
    overall="NO-GO"
    break
  fi
done
if [[ "$overall" == "GO" ]]; then
  for s in "$migration_status" "$frontend_status" "$health_status" "$gate_status"; do
    if [[ "$s" == "NOT-RUN" ]]; then
      overall="CONDITIONAL-GO"
      break
    fi
  done
fi

cat > "$OUTPUT_PATH" <<EOF
# Catalog-Only Release Report

## Release Meta

| Alan | Değer |
|---|---|
| Olusturma zamani (UTC) | ${timestamp_utc} |
| Branch | ${git_branch} |
| Commit SHA | ${git_sha} |
| API URL | ${API_URL} |
| Otomatik karar | ${overall} |
| Release owner | <doldur> |
| On-call engineer | <doldur> |

## Go / No-Go Sonuclari

| Alan | Sonuc | Kanit |
|---|---|---|
| Konfig (ProductFeatures) | ${config_status} | \`backend/Katalogcu.API/appsettings.json\` |
| Migration/model check | ${migration_status} | ${preflight_log} |
| Frontend type-check | ${frontend_status} | ${preflight_log} |
| API health | ${health_status} | ${postdeploy_log} |
| AI/Ecommerce gate | ${gate_status} | ${postdeploy_log} |

## Komut Durumlari

| Komut | Durum |
|---|---|
| preflight_catalog_only.sh | ${preflight_status} |
| postdeploy_catalog_only_check.sh | ${postdeploy_status} |

## Notlar

- \`CONDITIONAL-GO\`: tum zorunlu kontroller kosulmadi, manuel karar gerekir.
- Son karar icin: \`backend/CATALOG_ONLY_RELEASE_GO_NO_GO.md\`
EOF

echo "Release report created: $OUTPUT_PATH"
