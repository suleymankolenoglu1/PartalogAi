from __future__ import annotations

import json
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse


ROOT = Path(__file__).resolve().parent


class PartalogTestHandler(SimpleHTTPRequestHandler):
    cart_count = 0

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def end_headers(self) -> None:
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_OPTIONS(self) -> None:
        if self.path.startswith("/mock-cart-add") or self.path.startswith("/mock-availability"):
            self.send_response(204)
            self.send_header("Access-Control-Allow-Origin", self.headers.get("Origin", "*"))
            self.send_header("Access-Control-Allow-Credentials", "true")
            self.send_header("Access-Control-Allow-Methods", "GET,POST,OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type")
            self.end_headers()
            return

        super().do_OPTIONS()

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path == "/mock-cart-add":
            payload = {
                "success": True,
                "message": "Mock cart GET endpoint cagrildi.",
                "cartCount": self.cart_count,
                "query": parse_qs(parsed.query),
            }
            self._write_json(payload)
            return

        if parsed.path == "/mock-availability":
            items_raw = parse_qs(parsed.query).get("items", ["[]"])[0]
            try:
                items = json.loads(items_raw)
            except json.JSONDecodeError:
                items = []
            payload = {"items": self._build_availability_items(items)}
            self._write_json(payload)
            return

        super().do_GET()

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path == "/mock-cart-add":
            data = self._read_json()
            quantity = int(data.get("quantity") or 1)
            self.__class__.cart_count += max(1, quantity)
            payload = {
                "success": True,
                "message": f"{data.get('partCode') or 'Parca'} host sepete eklendi.",
                "cartCount": self.cart_count,
                "lastItem": data,
            }
            self._write_json(payload)
            return

        if parsed.path == "/mock-availability":
            data = self._read_json()
            items = data.get("items") or []
            payload = {"items": self._build_availability_items(items)}
            self._write_json(payload)
            return

        super().do_POST()

    def _read_json(self) -> dict:
        content_length = int(self.headers.get("Content-Length", "0") or "0")
        raw = self.rfile.read(content_length) if content_length > 0 else b"{}"
        try:
            return json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError:
            return {}

    def _write_json(self, payload: dict) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", self.headers.get("Origin", "*"))
        self.send_header("Access-Control-Allow-Credentials", "true")
        self.end_headers()
        self.wfile.write(body)

    @staticmethod
    def _build_availability_items(items: list[dict]) -> list[dict]:
        response = []
        for index, item in enumerate(items):
            response.append(
                {
                    "catalogItemId": item.get("catalogItemId"),
                    "partCode": item.get("partCode"),
                    "stockStatus": "available_to_order" if index % 3 == 0 else "in_stock",
                    "availabilityLabel": "Available to order" if index % 3 == 0 else "In stock",
                    "unitPrice": round(15.5 + (index * 9.75), 2),
                    "currency": "TRY",
                    "canAddToCart": True,
                }
            )
        return response


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", 5500), PartalogTestHandler)
    print("Serving Partalog test host at http://127.0.0.1:5500")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
