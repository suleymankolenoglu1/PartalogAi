#!/usr/bin/env bash
set -euo pipefail

API_BASE_URL="${API_BASE_URL:-http://127.0.0.1:5159}"
AI_BASE_URL="${AI_BASE_URL:-http://127.0.0.1:8000}"
AI_IDENTITY_TOKEN="${AI_IDENTITY_TOKEN:-}"
PUBLIC_TOKEN="${PARTALOG_PUBLIC_TOKEN:-}"
CATALOG_IDS="${PARTALOG_CATALOG_IDS:-[]}"
CHAT_QUERY="${PARTALOG_CHAT_SMOKE_QUERY:-Yamato VG2500-8F için yağ deposu contası var mı? Kodunu söyler misin?}"
WAIT_TIMEOUT_SECONDS="${WAIT_TIMEOUT_SECONDS:-90}"
CURL_CONNECT_TIMEOUT_SECONDS="${CURL_CONNECT_TIMEOUT_SECONDS:-3}"
CURL_MAX_TIME_SECONDS="${CURL_MAX_TIME_SECONDS:-30}"
RUN_RATE_LIMIT_CHECK=false

usage() {
  cat <<'USAGE'
Usage:
  smoke_chat_prod_readiness.sh [options]

Options:
  --api-base-url <url>       API base URL. Default: API_BASE_URL or http://127.0.0.1:5159
  --ai-base-url <url>        AI base URL. Default: AI_BASE_URL or http://127.0.0.1:8000
  --ai-identity-token <jwt>  Identity token for private AI Cloud Run. Or AI_IDENTITY_TOKEN env.
  --public-token <token>     Public chat token. Or PARTALOG_PUBLIC_TOKEN env.
  --catalog-ids <json>       Catalog IDs JSON array. Or PARTALOG_CATALOG_IDS env.
  --chat-query <text>        Real chat query. Or PARTALOG_CHAT_SMOKE_QUERY env.
  --wait-timeout <seconds>   Wait timeout. Default: 90
  --rate-limit-check         Also verify invalid-token public chat rate limit reaches 429.
  -h, --help                 Show help.
USAGE
}

curl_with_timeout() {
  curl --connect-timeout "$CURL_CONNECT_TIMEOUT_SECONDS" \
    --max-time "$CURL_MAX_TIME_SECONDS" \
    "$@"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --api-base-url)
      API_BASE_URL="$2"
      shift 2
      ;;
    --ai-base-url)
      AI_BASE_URL="$2"
      shift 2
      ;;
    --ai-identity-token)
      AI_IDENTITY_TOKEN="$2"
      shift 2
      ;;
    --public-token)
      PUBLIC_TOKEN="$2"
      shift 2
      ;;
    --catalog-ids)
      CATALOG_IDS="$2"
      shift 2
      ;;
    --chat-query)
      CHAT_QUERY="$2"
      shift 2
      ;;
    --wait-timeout)
      WAIT_TIMEOUT_SECONDS="$2"
      shift 2
      ;;
    --rate-limit-check)
      RUN_RATE_LIMIT_CHECK=true
      shift
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

wait_for_url() {
  local url="$1"
  local name="$2"
  local elapsed=0
  local last_error
  last_error="$(mktemp -t katalogcu-smoke-curl.XXXXXX)"

  until curl_with_timeout -fsS "$url" >/dev/null 2>"$last_error"; do
    sleep 2
    elapsed=$((elapsed + 2))
    if (( elapsed >= WAIT_TIMEOUT_SECONDS )); then
      echo "Timeout waiting for ${name}: ${url}" >&2
      cat "$last_error" >&2
      rm -f "$last_error"
      return 1
    fi
  done

  rm -f "$last_error"
}

expect_body_contains() {
  local body="$1"
  local expected="$2"
  local label="$3"
  if ! grep -Fq "$expected" <<<"$body"; then
    echo "${label} did not contain expected text: ${expected}" >&2
    echo "$body" >&2
    return 1
  fi
}

echo "[1/6] API liveness"
wait_for_url "${API_BASE_URL}/health/live" "API live"

echo "[2/6] API readiness"
api_ready="$(curl_with_timeout -fsS "${API_BASE_URL}/health/ready")"
expect_body_contains "$api_ready" "\"status\":\"ready\"" "API readiness"

echo "[3/6] Migration readiness"
migrations="$(curl_with_timeout -fsS "${API_BASE_URL}/health/migrations")"
expect_body_contains "$migrations" "\"status\":\"ok\"" "Migration readiness"

echo "[4/6] AI readiness"
ai_auth_args=()
if [[ -n "$AI_IDENTITY_TOKEN" ]]; then
  ai_auth_args=(-H "Authorization: Bearer ${AI_IDENTITY_TOKEN}")
fi
ai_ready="$(curl_with_timeout -fsS "${ai_auth_args[@]}" "${AI_BASE_URL}/health/ready")"
expect_body_contains "$ai_ready" "\"ready\":true" "AI readiness"
expect_body_contains "$ai_ready" "\"capacity\"" "AI capacity readiness"

echo "[5/6] Invalid public token smoke"
invalid_status="$(
  curl_with_timeout -o /tmp/katalogcu_invalid_chat_smoke.txt -sS -w "%{http_code}" \
    -X POST "${API_BASE_URL}/api/chat/ask" \
    -H "X-Forwarded-For: 203.0.113.221" \
    -F "text=smoke" \
    -F "publicToken=invalid-smoke-token" \
    -F "catalog_ids=[]" \
    -F "history=[]"
)"
if [[ "$invalid_status" != "400" ]]; then
  echo "Invalid token smoke expected 400, got ${invalid_status}" >&2
  cat /tmp/katalogcu_invalid_chat_smoke.txt >&2
  exit 1
fi

if [[ -n "$PUBLIC_TOKEN" ]]; then
  echo "[6/6] Real public chat smoke"
  chat_status="$(
    curl_with_timeout -o /tmp/katalogcu_real_chat_smoke.json -sS -w "%{http_code}" \
      -X POST "${API_BASE_URL}/api/chat/ask" \
      -H "X-Forwarded-For: 203.0.113.222" \
      -F "text=${CHAT_QUERY}" \
      -F "publicToken=${PUBLIC_TOKEN}" \
      -F "catalog_ids=${CATALOG_IDS}" \
      -F "history=[]"
  )"
  if [[ "$chat_status" != "200" ]]; then
    echo "Real public chat smoke expected 200, got ${chat_status}" >&2
    cat /tmp/katalogcu_real_chat_smoke.json >&2
    exit 1
  fi
  expect_body_contains "$(cat /tmp/katalogcu_real_chat_smoke.json)" "\"replySuggestion\"" "Real chat response"
else
  echo "[6/6] Real public chat smoke skipped: PARTALOG_PUBLIC_TOKEN not provided"
fi

if [[ "$RUN_RATE_LIMIT_CHECK" == "true" ]]; then
  echo "[extra] Public chat distributed rate-limit check"
  saw_429=false
  for _ in {1..25}; do
    status="$(
      curl_with_timeout -o /dev/null -sS -w "%{http_code}" \
        -X POST "${API_BASE_URL}/api/chat/ask" \
        -H "X-Forwarded-For: 203.0.113.223" \
        -F "text=smoke" \
        -F "publicToken=invalid-smoke-token" \
        -F "catalog_ids=[]" \
        -F "history=[]"
    )"
    if [[ "$status" == "429" ]]; then
      saw_429=true
      break
    fi
  done

  if [[ "$saw_429" != "true" ]]; then
    echo "Rate-limit check expected at least one 429 response." >&2
    exit 1
  fi
fi

echo "Prod chat readiness smoke passed."
