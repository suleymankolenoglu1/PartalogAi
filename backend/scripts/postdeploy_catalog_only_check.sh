#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:5159}"
ADMIN_BEARER_TOKEN="${ADMIN_BEARER_TOKEN:-}"
RESPONSE_FILE="/tmp/postdeploy_response_body.$$"

print_usage() {
  cat <<'USAGE'
Usage:
  postdeploy_catalog_only_check.sh [options]

Options:
  --api-url <url>   API base URL (default: http://localhost:5159)
  --admin-bearer-token <token> Optional: verify short public token format with authenticated call
  -h, --help        Show help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-url)
      API_URL="$2"
      shift 2
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

assert_status() {
  local got="$1"
  local expected="$2"
  local label="$3"
  if [[ "$got" != "$expected" ]]; then
    echo "[FAIL] $label -> expected $expected, got $got" >&2
    echo "--- response body ---" >&2
    cat "$RESPONSE_FILE" >&2 || true
    echo "---------------------" >&2
    exit 1
  fi
  echo "[OK] $label -> $got"
}

http_status() {
  local method="$1"
  local url="$2"
  curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" -X "$method" "$url"
}

http_status_auth() {
  local method="$1"
  local url="$2"
  local token="$3"
  curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" \
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

cleanup() {
  rm -f "$RESPONSE_FILE" 2>/dev/null || true
}
trap cleanup EXIT

require_cmd curl
require_cmd python

echo "[1/3] Health checks"
code="$(http_status GET "$API_URL/health/live")"
assert_status "$code" "200" "health/live"

code="$(http_status GET "$API_URL/health/ready")"
assert_status "$code" "200" "health/ready"

code="$(http_status GET "$API_URL/health/migrations")"
assert_status "$code" "200" "health/migrations"

echo "[2/3] Feature checks"
code="$(http_status GET "$API_URL/api/system/features")"
assert_status "$code" "200" "system/features"

POSTDEPLOY_RESPONSE_FILE="$RESPONSE_FILE" python - <<'PY'
import json
from pathlib import Path
import sys
import os

response_file = os.environ["POSTDEPLOY_RESPONSE_FILE"]
data = json.loads(Path(response_file).read_text(encoding="utf-8"))
required = ("aiEnabled", "ecommerceEnabled", "upgradePromptsEnabled")
wrong = [k for k in required if data.get(k) is not False]
if wrong:
    print(f"[FAIL] Not catalog-only at runtime: {wrong}", file=sys.stderr)
    sys.exit(1)
print("[OK] Runtime features are catalog-only")
PY

echo "[3/3] Gate checks (403 expected)"
code="$(http_status GET "$API_URL/api/chat")"
assert_status "$code" "403" "AI module gate"

code="$(http_status GET "$API_URL/api/orders")"
assert_status "$code" "403" "Orders gate"

code="$(http_status GET "$API_URL/api/products")"
assert_status "$code" "403" "Products gate"

code="$(http_status GET "$API_URL/api/customers")"
assert_status "$code" "403" "Customers gate"

if [[ -n "$ADMIN_BEARER_TOKEN" ]] && ! is_placeholder_token "$ADMIN_BEARER_TOKEN"; then
  echo "[INFO] Optional short public token check enabled"
  code="$(http_status_auth GET "$API_URL/api/catalogs/public-token" "$ADMIN_BEARER_TOKEN")"
  assert_status "$code" "200" "public-token auth"

  POSTDEPLOY_RESPONSE_FILE="$RESPONSE_FILE" python - <<'PY'
import json
from pathlib import Path
import sys
import os

response_file = os.environ["POSTDEPLOY_RESPONSE_FILE"]
data = json.loads(Path(response_file).read_text(encoding="utf-8"))
token = data.get("token")
if not isinstance(token, str) or not token.startswith("pk_"):
    print(f"[FAIL] Expected short public token prefix 'pk_', got: {token!r}", file=sys.stderr)
    sys.exit(1)
print("[OK] Public link token format is short (pk_)")
PY
else
  echo "[WARN] Geçerli --admin-bearer-token verilmedi, kısa public token format kontrolü atlandı."
fi

echo "Post-deploy catalog-only check passed."
