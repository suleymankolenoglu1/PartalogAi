#!/usr/bin/env bash
set -euo pipefail

API_URL="http://localhost:5159"
APP_URL="http://localhost:4200"
ORIGIN="http://localhost:3000"
PUBLIC_TOKEN=""
OWNER_BEARER_TOKEN=""
RESPONSE_FILE="/tmp/embed_smoke_response_body.$$"

usage() {
  cat <<EOF
Embed SDK smoke test.

Usage:
  embed_smoke.sh [options]

Options:
  --api-url URL               API base URL (default: ${API_URL})
  --app-url URL               Frontend app URL (default: ${APP_URL})
  --origin ORIGIN             Test origin (default: ${ORIGIN})
  --public-token TOKEN        Public token (pk_...) for embed tests
  --owner-bearer-token TOKEN  Owner JWT. If provided, protected endpoints are tested too.
  -h, --help                  Show this help

Notes:
  - If --public-token verilmez ama --owner-bearer-token verilirse, script tokeni
    /api/catalogs/public-token endpointinden otomatik alır.
  - Owner token yoksa sadece anonim embed kontrolleri yapılır.
EOF
}

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[FAIL] Command not found: $cmd" >&2
    exit 1
  fi
}

call_api() {
  local method="$1"
  local url="$2"
  local body="${3:-}"
  local auth="${4:-}"
  local origin="${5:-}"

  local -a curl_cmd
  curl_cmd=(curl -sS -o "$RESPONSE_FILE" -w "%{http_code}" -X "$method" "$url")

  if [[ -n "$auth" ]]; then
    curl_cmd+=(-H "Authorization: Bearer $auth")
  fi

  if [[ -n "$origin" ]]; then
    curl_cmd+=(-H "Origin: $origin")
  fi

  if [[ -n "$body" ]]; then
    curl_cmd+=(-H "Content-Type: application/json" --data "$body")
  fi

  "${curl_cmd[@]}"
}

assert_status() {
  local expected="$1"
  local got="$2"
  local label="$3"
  if [[ "$got" == "$expected" ]]; then
    echo "[OK] $label -> $got"
    return
  fi

  echo "[FAIL] $label -> expected $expected, got $got" >&2
  echo "--- response body ---" >&2
  cat "$RESPONSE_FILE" >&2 || true
  echo -e "\n---------------------" >&2
  exit 1
}

json_field() {
  local expr="$1"
  python3 - "$RESPONSE_FILE" "$expr" <<'PY'
import json, sys
path = sys.argv[2].split(".")
with open(sys.argv[1], "r", encoding="utf-8") as f:
    data = json.load(f)
cur = data
for seg in path:
    if not isinstance(cur, dict) or seg not in cur:
        print("")
        sys.exit(0)
    cur = cur[seg]
if cur is None:
    print("")
elif isinstance(cur, (dict, list)):
    print(json.dumps(cur, ensure_ascii=False))
else:
    print(cur)
PY
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-url)
      API_URL="${2:-}"
      shift 2
      ;;
    --app-url)
      APP_URL="${2:-}"
      shift 2
      ;;
    --origin)
      ORIGIN="${2:-}"
      shift 2
      ;;
    --public-token)
      PUBLIC_TOKEN="${2:-}"
      shift 2
      ;;
    --owner-bearer-token)
      OWNER_BEARER_TOKEN="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

trap 'rm -f "$RESPONSE_FILE"' EXIT

require_cmd curl
require_cmd python3

API_URL="${API_URL%/}"
APP_URL="${APP_URL%/}"
ORIGIN="${ORIGIN%/}"

echo "[1/8] Health"
status="$(call_api GET "$API_URL/health/live")"
assert_status "200" "$status" "health/live"

if [[ -z "$PUBLIC_TOKEN" && -n "$OWNER_BEARER_TOKEN" ]]; then
  echo "[2/8] Auto-fetch public token"
  status="$(call_api GET "$API_URL/api/catalogs/public-token" "" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "catalogs/public-token"
  PUBLIC_TOKEN="$(json_field token)"
fi

if [[ -z "$PUBLIC_TOKEN" ]]; then
  echo "[FAIL] Public token gerekli. --public-token verin veya --owner-bearer-token ile otomatik alın." >&2
  exit 1
fi

if [[ -n "$OWNER_BEARER_TOKEN" ]]; then
  echo "[3/8] Embed settings (GET)"
  status="$(call_api GET "$API_URL/api/embed/settings" "" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "embed/settings GET"

  echo "[4/8] Embed settings (PUT, allow origin)"
  put_body="$(cat <<JSON
{"allowedOrigins":["$ORIGIN"],"theme":"default","mode":"catalog"}
JSON
)"
  status="$(call_api PUT "$API_URL/api/embed/settings" "$put_body" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "embed/settings PUT"
else
  echo "[3/8] Owner token yok, protected settings testleri atlandı."
  echo "[4/8] Owner token yok, protected settings testleri atlandı."
fi

echo "[5/8] verify-origin"
verify_body="$(cat <<JSON
{"publicToken":"$PUBLIC_TOKEN","origin":"$ORIGIN"}
JSON
)"
status="$(call_api POST "$API_URL/api/embed/verify-origin" "$verify_body" "" "$ORIGIN")"
assert_status "200" "$status" "embed/verify-origin"
allowed="$(json_field allowed)"
white_label="$(json_field whiteLabel)"
if [[ -n "$OWNER_BEARER_TOKEN" && "$allowed" != "True" && "$allowed" != "true" ]]; then
  echo "[FAIL] verify-origin allowed=false döndü. Body:" >&2
  cat "$RESPONSE_FILE" >&2
  exit 1
fi
echo "[INFO] verify-origin allowed=$allowed"
if [[ -z "$white_label" ]]; then
  echo "[FAIL] verify-origin whiteLabel alanı dönmedi. Body:" >&2
  cat "$RESPONSE_FILE" >&2
  exit 1
fi
echo "[INFO] verify-origin whiteLabel=$white_label"

echo "[6/8] embed/events ingest"
event_body='{"eventName":"part:viewed","source":"smoke-script","pageUrl":"'"$APP_URL"'/embed-smoke","payload":{"partCode":"SMOKE-001","partName":"Smoke Part"}}'
status="$(call_api POST "$API_URL/api/embed/events?token=$PUBLIC_TOKEN" "$event_body" "" "$ORIGIN")"
if [[ -n "$OWNER_BEARER_TOKEN" ]]; then
  assert_status "200" "$status" "embed/events"
else
  if [[ "$status" == "200" ]]; then
    echo "[OK] embed/events -> 200"
  else
    echo "[WARN] embed/events -> $status (owner token yok, origin allowlist'te olmayabilir)"
  fi
fi

if [[ -n "$OWNER_BEARER_TOKEN" ]]; then
  echo "[7/8] domain challenge create/verify/delete"
  challenge_body='{"origin":"'"$ORIGIN"'","method":"file"}'
  status="$(call_api POST "$API_URL/api/embed/domains/challenge" "$challenge_body" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "embed/domains/challenge"
  verification_id="$(json_field id)"
  if [[ -z "$verification_id" ]]; then
    echo "[FAIL] Domain challenge id parse edilemedi." >&2
    cat "$RESPONSE_FILE" >&2
    exit 1
  fi

  status="$(call_api POST "$API_URL/api/embed/domains/$verification_id/verify-now" "{}" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "embed/domains/{id}/verify-now"

  status="$(call_api DELETE "$API_URL/api/embed/domains/$verification_id" "" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "embed/domains/{id} DELETE"

  echo "[8/8] dashboard stats contains embed metrics"
  status="$(call_api GET "$API_URL/api/catalogs/stats" "" "$OWNER_BEARER_TOKEN")"
  assert_status "200" "$status" "catalogs/stats"
  python3 - "$RESPONSE_FILE" <<'PY'
import json, sys
with open(sys.argv[1], "r", encoding="utf-8") as f:
    data = json.load(f)
required = [
    "embedEventsTotal",
    "embedEventsLast7Days",
    "embedPartViewedCount",
    "embedCartAddCount",
    "embedCheckoutStartCount",
]
missing = [k for k in required if k not in data]
if missing:
    print("[FAIL] stats missing embed fields:", ", ".join(missing), file=sys.stderr)
    sys.exit(1)
print("[OK] stats embed fields present")
PY
else
  echo "[7/8] Owner token yok, domain challenge testleri atlandı."
  echo "[8/8] Owner token yok, dashboard embed metrik kontrolü atlandı."
fi

echo "Embed smoke checks passed."
