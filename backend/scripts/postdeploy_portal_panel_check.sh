#!/usr/bin/env bash
set -euo pipefail

PORTAL_URL="${PORTAL_URL:-}"
PANEL_URL="${PANEL_URL:-}"
PUBLIC_TOKEN="${PARTALOG_PUBLIC_TOKEN:-}"
RESPONSE_FILE="/tmp/postdeploy_portal_panel_body.$$"
HEADER_FILE="/tmp/postdeploy_portal_panel_headers.$$"

print_usage() {
  cat <<'USAGE'
Usage:
  postdeploy_portal_panel_check.sh --portal-url <url> --panel-url <url> [options]

Options:
  --portal-url <url>    Customer-facing portal origin, for example https://domain.com
  --panel-url <url>     Owner panel origin, for example https://panel.domain.com
  --public-token <tok>  Optional: verify public storefront through /api/catalogs/public-storefront
  -h, --help            Show help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --portal-url)
      PORTAL_URL="$2"
      shift 2
      ;;
    --panel-url)
      PANEL_URL="$2"
      shift 2
      ;;
    --public-token)
      PUBLIC_TOKEN="$2"
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

normalize_origin() {
  local value="$1"
  value="${value%/}"
  echo "$value"
}

cleanup() {
  rm -f "$RESPONSE_FILE" "$HEADER_FILE" 2>/dev/null || true
}
trap cleanup EXIT

http_get_status() {
  local url="$1"
  curl -sS -o "$RESPONSE_FILE" -D "$HEADER_FILE" -w "%{http_code}" "$url"
}

location_header() {
  awk '
    tolower($1) == "location:" {
      $1 = "";
      sub(/^ /, "");
      sub(/\r$/, "");
      print;
      exit;
    }
  ' "$HEADER_FILE"
}

assert_status() {
  local got="$1"
  local expected="$2"
  local label="$3"

  if [[ "$got" != "$expected" ]]; then
    echo "[FAIL] $label -> expected $expected, got $got" >&2
    echo "--- response headers ---" >&2
    cat "$HEADER_FILE" >&2 || true
    echo "--- response body ---" >&2
    cat "$RESPONSE_FILE" >&2 || true
    exit 1
  fi

  echo "[OK] $label -> $got"
}

assert_redirect() {
  local source_url="$1"
  local expected_location="$2"
  local label="$3"
  local code
  local location

  code="$(http_get_status "$source_url")"
  case "$code" in
    301|302|307|308) ;;
    *)
      echo "[FAIL] $label -> expected redirect, got $code" >&2
      echo "--- response headers ---" >&2
      cat "$HEADER_FILE" >&2 || true
      echo "--- response body ---" >&2
      cat "$RESPONSE_FILE" >&2 || true
      exit 1
      ;;
  esac

  location="$(location_header)"
  if [[ "$location" != "$expected_location" ]]; then
    echo "[FAIL] $label -> expected Location $expected_location, got ${location:-<empty>}" >&2
    echo "--- response headers ---" >&2
    cat "$HEADER_FILE" >&2 || true
    exit 1
  fi

  echo "[OK] $label -> $code $location"
}

is_placeholder_token() {
  local token="$1"
  [[ -z "$token" ]] && return 0
  [[ "$token" =~ ^\<.*\>$ ]] && return 0
  [[ "$token" == "CHANGE_ME" ]] && return 0
  [[ "$token" == "PARTALOG_PUBLIC_TOKEN" ]] && return 0
  return 1
}

require_cmd curl

if [[ -z "$PORTAL_URL" || -z "$PANEL_URL" ]]; then
  echo "--portal-url and --panel-url are required." >&2
  print_usage
  exit 1
fi

PORTAL_URL="$(normalize_origin "$PORTAL_URL")"
PANEL_URL="$(normalize_origin "$PANEL_URL")"

echo "[1/5] SPA roots"
code="$(http_get_status "$PORTAL_URL/")"
assert_status "$code" "200" "portal root"

code="$(http_get_status "$PANEL_URL/")"
assert_status "$code" "200" "panel root"

echo "[2/5] API proxy"
code="$(http_get_status "$PORTAL_URL/api/system/features")"
assert_status "$code" "200" "portal /api/system/features"

code="$(http_get_status "$PANEL_URL/api/system/features")"
assert_status "$code" "200" "panel /api/system/features"

echo "[3/5] Portal-to-panel redirects"
assert_redirect "$PORTAL_URL/login" "$PANEL_URL/login" "portal /login canonical host"
assert_redirect "$PORTAL_URL/dashboard" "$PANEL_URL/dashboard" "portal /dashboard canonical host"
assert_redirect "$PORTAL_URL/platform" "$PANEL_URL/platform" "portal /platform canonical host"

echo "[4/5] Panel-to-portal redirects"
assert_redirect "$PANEL_URL/p/smoke-token" "$PORTAL_URL/p/smoke-token" "panel /p canonical host"
assert_redirect "$PANEL_URL/public-view/smoke-token" "$PORTAL_URL/public-view/smoke-token" "panel /public-view canonical host"
assert_redirect "$PANEL_URL/view/smoke-catalog" "$PORTAL_URL/view/smoke-catalog" "panel /view canonical host"

echo "[5/5] Optional public storefront"
if is_placeholder_token "$PUBLIC_TOKEN"; then
  echo "[WARN] --public-token verilmedi, public storefront kontrolu atlandi."
else
  code="$(http_get_status "$PORTAL_URL/api/catalogs/public-storefront?token=$PUBLIC_TOKEN")"
  assert_status "$code" "200" "portal public storefront"
fi

echo "Portal/panel post-deploy check passed."
