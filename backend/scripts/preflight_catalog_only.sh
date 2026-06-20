#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND_API_DIR="$ROOT_DIR/backend/Katalogcu.API"
FRONTEND_DIR="$ROOT_DIR/frontend/katalogcu-frontend"

API_URL="${API_URL:-http://localhost:5159}"
DOTNET_EF_TIMEOUT_SECONDS="${DOTNET_EF_TIMEOUT_SECONDS:-90}"
ADMIN_BEARER_TOKEN="${ADMIN_BEARER_TOKEN:-}"
SKIP_BUILD=false
SKIP_MIGRATION_CHECK=false
SKIP_TYPECHECK=false
SKIP_RUNTIME=false

print_usage() {
  cat <<'USAGE'
Usage:
  preflight_catalog_only.sh [options]

Options:
  --api-url <url>       API base URL (default: http://localhost:5159)
  --skip-build          Skip backend dotnet build
  --skip-migration-check Skip EF migration/model checks
  --skip-typecheck      Skip frontend TypeScript check
  --skip-runtime        Skip runtime HTTP checks
  --admin-bearer-token <token> Optional: verify short public token format with authenticated call
  -h, --help            Show help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-url)
      API_URL="$2"
      shift 2
      ;;
    --skip-build)
      SKIP_BUILD=true
      shift
      ;;
    --skip-migration-check)
      SKIP_MIGRATION_CHECK=true
      shift
      ;;
    --skip-typecheck)
      SKIP_TYPECHECK=true
      shift
      ;;
    --skip-runtime)
      SKIP_RUNTIME=true
      shift
      ;;
    --admin-bearer-token)
      ADMIN_BEARER_TOKEN="$2"
      shift 2
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

http_status() {
  local method="$1"
  local url="$2"
  curl -sS -o /tmp/preflight_response_body.$$ -w "%{http_code}" -X "$method" "$url"
}

http_status_auth() {
  local method="$1"
  local url="$2"
  local token="$3"
  curl -sS -o /tmp/preflight_response_body.$$ -w "%{http_code}" \
    -X "$method" \
    -H "Authorization: Bearer $token" \
    "$url"
}

is_placeholder_token() {
  local token="$1"
  [[ "$token" =~ ^\<.*\>$ ]] && return 0
  [[ "$token" == "ADMIN_JWT" ]] && return 0
  [[ "$token" == "<ADMIN_JWT>" ]] && return 0
  [[ "$token" == "CHANGE_ME" ]] && return 0
  return 1
}

assert_status() {
  local got="$1"
  local expected="$2"
  local label="$3"
  if [[ "$got" != "$expected" ]]; then
    echo "[FAIL] $label -> expected $expected, got $got" >&2
    echo "--- response body ---" >&2
    cat /tmp/preflight_response_body.$$ >&2 || true
    echo "---------------------" >&2
    exit 1
  fi
  echo "[OK] $label -> $got"
}

cleanup() {
  rm -f /tmp/preflight_response_body.$$ 2>/dev/null || true
}
trap cleanup EXIT

run_with_timeout() {
  local timeout_seconds="$1"
  shift
  python - "$timeout_seconds" "$@" <<'PY'
import subprocess
import sys

timeout = int(sys.argv[1])
cmd = sys.argv[2:]

try:
    subprocess.run(cmd, check=True, timeout=timeout)
except subprocess.TimeoutExpired:
    print(f"[FAIL] Command timed out after {timeout}s: {' '.join(cmd)}", file=sys.stderr)
    sys.exit(124)
except subprocess.CalledProcessError as exc:
    sys.exit(exc.returncode)
PY
}

require_cmd curl

echo "[1/5] Catalog-only config sanity"
python -c '
import json, pathlib, sys
p = pathlib.Path("'"$BACKEND_API_DIR"'") / "appsettings.json"
if not p.exists():
    p = pathlib.Path("'"$BACKEND_API_DIR"'") / "appsettings.example.json"
data = json.loads(p.read_text(encoding="utf-8"))
pf = data.get("ProductFeatures") or {}
expected = {"EnableAi": False, "EnableEcommerce": False, "EnableUpgradePrompts": False}
for k, v in expected.items():
    if pf.get(k) is not v:
        print(f"[FAIL] appsettings.json ProductFeatures.{k} expected {v}, got {pf.get(k)!r}")
        sys.exit(1)
print(f"[OK] {p.name} ProductFeatures catalog-only defaults are correct")
'

if [[ "$SKIP_BUILD" == "false" ]]; then
  require_cmd dotnet
  echo "[2/5] Backend build"
  (cd "$ROOT_DIR" && dotnet build backend/Katalogcu.API/Katalogcu.API.csproj --no-restore /nr:false)
else
  echo "[2/5] Backend build skipped"
fi

if [[ "$SKIP_MIGRATION_CHECK" == "false" ]]; then
  require_cmd dotnet
  echo "[3/5] EF migration/model checks"
  if dotnet ef --version >/dev/null 2>&1; then
    (cd "$ROOT_DIR" && run_with_timeout "$DOTNET_EF_TIMEOUT_SECONDS" dotnet ef migrations has-pending-model-changes --project backend/Katalogcu.Infrastructure/Katalogcu.Infrastructure.csproj --startup-project backend/Katalogcu.API/Katalogcu.API.csproj --context AppDbContext --no-build)
    (cd "$ROOT_DIR" && run_with_timeout "$DOTNET_EF_TIMEOUT_SECONDS" dotnet ef migrations list --project backend/Katalogcu.Infrastructure/Katalogcu.Infrastructure.csproj --startup-project backend/Katalogcu.API/Katalogcu.API.csproj --context AppDbContext --no-build)
    echo "[OK] EF migration/model checks passed"
  else
    echo "[WARN] dotnet-ef bulunamadı, design-time migration check atlandı."
  fi
else
  echo "[3/5] EF migration/model checks skipped"
fi

if [[ "$SKIP_TYPECHECK" == "false" ]]; then
  require_cmd npx
  echo "[4/5] Frontend type-check"
  (cd "$FRONTEND_DIR" && npx tsc -p tsconfig.app.json --noEmit)
else
  echo "[4/5] Frontend type-check skipped"
fi

if [[ "$SKIP_RUNTIME" == "false" ]]; then
  echo "[5/5] Runtime checks on $API_URL"

  code="$(http_status GET "$API_URL/health/live")"
  assert_status "$code" "200" "health/live"

  code="$(http_status GET "$API_URL/health/ready")"
  assert_status "$code" "200" "health/ready"

  code="$(http_status GET "$API_URL/health/migrations")"
  assert_status "$code" "200" "health/migrations"

  code="$(http_status GET "$API_URL/api/system/features")"
  assert_status "$code" "200" "system/features"

  python -c '
import json, pathlib, sys
body = pathlib.Path("/tmp/preflight_response_body.'$$'").read_text(encoding="utf-8")
data = json.loads(body)
expected_false = ("aiEnabled", "ecommerceEnabled", "upgradePromptsEnabled")
bad = [k for k in expected_false if data.get(k) is not False]
if bad:
    print(f"[FAIL] Feature flags are not catalog-only at runtime: {bad}")
    sys.exit(1)
print("[OK] Runtime feature flags are catalog-only")
'

  code="$(http_status GET "$API_URL/api/chat/health")"
  assert_status "$code" "403" "AI gate"

  code="$(http_status GET "$API_URL/api/orders")"
  assert_status "$code" "403" "Ecommerce gate"

  if [[ -n "$ADMIN_BEARER_TOKEN" ]] && ! is_placeholder_token "$ADMIN_BEARER_TOKEN"; then
    echo "[INFO] Optional short public token check enabled"
    code="$(http_status_auth GET "$API_URL/api/catalogs/public-token" "$ADMIN_BEARER_TOKEN")"
    assert_status "$code" "200" "public-token auth"

    PRECHECK_RESPONSE_FILE="/tmp/preflight_response_body.$$" python - <<'PY'
import json
import pathlib
import sys
import os

body = pathlib.Path(os.environ["PRECHECK_RESPONSE_FILE"]).read_text(encoding="utf-8")
try:
    data = json.loads(body)
except json.JSONDecodeError:
    print("[FAIL] public-token response is not valid JSON", file=sys.stderr)
    sys.exit(1)

token = data.get("token")
if not isinstance(token, str) or not token.startswith("pk_"):
    print(f"[FAIL] Expected short public token prefix 'pk_', got: {token!r}", file=sys.stderr)
    sys.exit(1)
print("[OK] Public link token format is short (pk_)")
PY
  else
    echo "[WARN] Geçerli --admin-bearer-token verilmedi, kısa public token format kontrolü atlandı."
  fi
else
  echo "[5/5] Runtime checks skipped"
fi

echo "Catalog-only preflight passed."
