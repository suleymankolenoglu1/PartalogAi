#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND_DIR="$ROOT_DIR/backend"
COMPOSE_FILE="$BACKEND_DIR/docker-compose.yml"
SMOKE_SCRIPT="$BACKEND_DIR/scripts/smoke_public_checkout.py"

BASE_URL="${BASE_URL:-http://localhost:5159}"
WAIT_TIMEOUT_SECONDS="${WAIT_TIMEOUT_SECONDS:-240}"
AI_URL="${AI_URL:-http://localhost:8000/}"
FRONTEND_URL="${FRONTEND_URL:-http://localhost:4200/}"
PUBLIC_TOKEN="${PARTALOG_PUBLIC_TOKEN:-}"
ADMIN_TOKEN="${PARTALOG_ADMIN_TOKEN:-}"

SKIP_UP=false
NO_BUILD=false
DOWN_AFTER=false
SKIP_AI_CHECK=false
SKIP_FRONTEND_CHECK=false

print_usage() {
  cat <<'USAGE'
Usage:
  smoke_all.sh [options]

Options:
  --base-url <url>        API base url (default: http://localhost:5159)
  --ai-url <url>          AI health URL (default: http://localhost:8000/)
  --frontend-url <url>    Frontend health URL (default: http://localhost:4200/)
  --public-token <token>  Public token (or PARTALOG_PUBLIC_TOKEN env)
  --admin-token <token>   Optional admin JWT (or PARTALOG_ADMIN_TOKEN env)
  --wait-timeout <sec>    Wait timeout seconds (default: 240)
  --skip-up               Do not run docker compose up
  --skip-ai-check         Do not wait AI health URL
  --skip-frontend-check   Do not wait frontend health URL
  --no-build              Use docker compose up without --build
  --down-after            Run docker compose down after test
  -h, --help              Show help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)
      BASE_URL="$2"
      shift 2
      ;;
    --public-token)
      PUBLIC_TOKEN="$2"
      shift 2
      ;;
    --ai-url)
      AI_URL="$2"
      shift 2
      ;;
    --frontend-url)
      FRONTEND_URL="$2"
      shift 2
      ;;
    --admin-token)
      ADMIN_TOKEN="$2"
      shift 2
      ;;
    --wait-timeout)
      WAIT_TIMEOUT_SECONDS="$2"
      shift 2
      ;;
    --skip-up)
      SKIP_UP=true
      shift
      ;;
    --no-build)
      NO_BUILD=true
      shift
      ;;
    --down-after)
      DOWN_AFTER=true
      shift
      ;;
    --skip-ai-check)
      SKIP_AI_CHECK=true
      shift
      ;;
    --skip-frontend-check)
      SKIP_FRONTEND_CHECK=true
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

cleanup() {
  if [[ "$DOWN_AFTER" == "true" ]]; then
    echo "[cleanup] docker compose down"
    docker compose -f "$COMPOSE_FILE" down || true
  fi
}
trap cleanup EXIT

wait_for_url() {
  local url="$1"
  local timeout="$2"
  local elapsed=0

  until curl -fsS "$url" >/dev/null 2>&1; do
    sleep 2
    elapsed=$((elapsed + 2))
    if (( elapsed >= timeout )); then
      echo "Timeout waiting for: $url" >&2
      return 1
    fi
  done
}

if [[ "$SKIP_UP" == "false" ]]; then
  echo "[1/4] Starting compose stack"
  if [[ "$NO_BUILD" == "true" ]]; then
    docker compose -f "$COMPOSE_FILE" up -d
  else
    docker compose -f "$COMPOSE_FILE" up -d --build
  fi
else
  echo "[1/4] Skipping compose up (--skip-up)"
fi

echo "[2/4] Waiting for services"
wait_for_url "$BASE_URL/swagger/index.html" "$WAIT_TIMEOUT_SECONDS"
if [[ "$SKIP_AI_CHECK" == "false" ]]; then
  wait_for_url "$AI_URL" "$WAIT_TIMEOUT_SECONDS"
fi
if [[ "$SKIP_FRONTEND_CHECK" == "false" ]]; then
  wait_for_url "$FRONTEND_URL" "$WAIT_TIMEOUT_SECONDS"
fi

if [[ -z "$PUBLIC_TOKEN" ]]; then
  echo "Public token gerekli (--public-token veya PARTALOG_PUBLIC_TOKEN)." >&2
  echo "Self-service bootstrap kapalıdır; owner hesabını backend/scripts/create_initial_user.py ile oluşturup portal linkini panelden üretin." >&2
  exit 1
fi

echo "[3/4] Running public checkout smoke"
SMOKE_ARGS=(--base-url "$BASE_URL" --public-token "$PUBLIC_TOKEN")

if [[ -n "$ADMIN_TOKEN" ]]; then
  SMOKE_ARGS+=(--admin-token "$ADMIN_TOKEN")
fi

python "$SMOKE_SCRIPT" "${SMOKE_ARGS[@]}"

echo "[4/4] Done"
echo "Smoke checks passed."
