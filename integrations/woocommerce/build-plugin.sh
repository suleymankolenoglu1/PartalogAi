#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLUGIN_DIR="$ROOT_DIR/partalog-woocommerce"
DIST_DIR="$ROOT_DIR/dist"
ZIP_PATH="$DIST_DIR/partalog-woocommerce.zip"

mkdir -p "$DIST_DIR"
rm -f "$ZIP_PATH"

if command -v ditto >/dev/null 2>&1; then
  ditto -c -k --sequesterRsrc --keepParent "$PLUGIN_DIR" "$ZIP_PATH"
else
  (
    cd "$ROOT_DIR"
    zip -r "$ZIP_PATH" "partalog-woocommerce"
  )
fi

echo "Plugin paketi hazir:"
echo "$ZIP_PATH"
