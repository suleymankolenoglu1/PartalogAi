#!/usr/bin/env python3
"""
Public-view checkout smoke test.

Flow:
1) Resolve public token (from arg/env or auto bootstrap)
2) Get public catalogs by token
3) Pick a catalog and product
4) Register/login public customer account
5) Create order
6) Verify privileged endpoints require auth/role
7) Verify order list + detail from customer endpoints (header session token)
8) Re-send same order with same idempotency key and verify replay flag
9) (Optional) Verify admin incoming orders contains created order
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


class SmokeError(RuntimeError):
    pass


@dataclass
class HttpResponse:
    status: int
    body: Any


@dataclass
class BootstrapResult:
    public_token: str
    admin_token: str
    catalog_id: str
    product_id: str


def _trim_slash(value: str) -> str:
    return value[:-1] if value.endswith("/") else value


def _request_json(
    method: str,
    url: str,
    *,
    payload: dict[str, Any] | None = None,
    headers: dict[str, str] | None = None,
    timeout: int = 20,
) -> HttpResponse:
    request_headers = {"Accept": "application/json"}
    if headers:
        request_headers.update(headers)

    data = None
    if payload is not None:
        request_headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")

    req = Request(url=url, data=data, method=method.upper(), headers=request_headers)

    try:
        with urlopen(req, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8")
            body = json.loads(raw) if raw else {}
            return HttpResponse(status=resp.status, body=body)
    except HTTPError as e:
        raw = e.read().decode("utf-8", errors="replace")
        try:
            body = json.loads(raw) if raw else {"error": raw}
        except json.JSONDecodeError:
            body = {"error": raw}
        return HttpResponse(status=e.code, body=body)
    except URLError as e:
        raise SmokeError(f"HTTP erişim hatası: {e}") from e


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise SmokeError(message)


def _require_status_in(actual: int, expected: tuple[int, ...], message: str) -> None:
    if actual not in expected:
        raise SmokeError(f"{message} (status={actual}, expected={expected})")


def _first_non_empty(*values: Any) -> Any:
    for v in values:
        if v not in (None, "", [], {}):
            return v
    return None


def _bootstrap_public_fixture(
    api: str,
    *,
    timeout: int,
    admin_email: str,
    admin_password: str,
    admin_name: str,
    company_name: str,
) -> BootstrapResult:
    register_payload = {
        "fullName": admin_name,
        "email": admin_email,
        "password": admin_password,
    }
    register_resp = _request_json("POST", f"{api}/auth/register", payload=register_payload, timeout=timeout)
    if register_resp.status not in (200, 400):
        raise SmokeError(f"Bootstrap register başarısız: {register_resp.status} {register_resp.body}")

    login_payload = {"email": admin_email, "password": admin_password}
    login_resp = _request_json("POST", f"{api}/auth/login", payload=login_payload, timeout=timeout)
    _require(login_resp.status == 200, f"Bootstrap login başarısız: {login_resp.status} {login_resp.body}")
    admin_token = login_resp.body.get("token", "")
    _require(bool(admin_token), f"Bootstrap login token dönmedi: {login_resp.body}")
    auth_headers = {"Authorization": f"Bearer {admin_token}"}

    me = login_resp.body.get("user", {}) if isinstance(login_resp.body, dict) else {}
    first_name = _first_non_empty(me.get("firstName"), "Smoke")
    last_name = _first_non_empty(me.get("lastName"), "Admin")
    _request_json(
        "PUT",
        f"{api}/auth/me",
        payload={
            "firstName": first_name,
            "lastName": last_name,
            "companyName": company_name,
            "phoneNumber": "+905550000000",
        },
        headers=auth_headers,
        timeout=timeout,
    )

    suffix = datetime.utcnow().strftime("%Y%m%d%H%M%S")
    catalog_payload = {
        "name": f"Smoke Catalog {suffix}",
        "description": "smoke generated catalog",
        "imageUrl": None,
        "pdfUrl": None,
        "folderId": None,
    }
    create_catalog_resp = _request_json(
        "POST",
        f"{api}/catalogs",
        payload=catalog_payload,
        headers=auth_headers,
        timeout=timeout,
    )
    _require(
        create_catalog_resp.status in (200, 201),
        f"Bootstrap catalog create başarısız: {create_catalog_resp.status} {create_catalog_resp.body}",
    )
    catalog_id = create_catalog_resp.body.get("id")
    _require(bool(catalog_id), f"Bootstrap catalog id dönmedi: {create_catalog_resp.body}")

    publish_resp = _request_json(
        "POST",
        f"{api}/catalogs/{catalog_id}/publish",
        payload={},
        headers=auth_headers,
        timeout=timeout,
    )
    _require(publish_resp.status == 200, f"Bootstrap catalog publish başarısız: {publish_resp.status} {publish_resp.body}")

    product_code = f"SMOKE-{suffix[-6:]}"
    create_product_resp = _request_json(
        "POST",
        f"{api}/products",
        payload={
            "catalogId": catalog_id,
            "name": f"Smoke Product {suffix}",
            "code": product_code,
            "price": 10,
            "stockQuantity": 10,
            "category": "Smoke",
            "description": "smoke generated product",
            "pageNumber": "1",
            "refNo": 1,
        },
        headers=auth_headers,
        timeout=timeout,
    )
    _require(create_product_resp.status == 200, f"Bootstrap product create başarısız: {create_product_resp.status} {create_product_resp.body}")
    product_id = create_product_resp.body.get("id")
    _require(bool(product_id), f"Bootstrap product id dönmedi: {create_product_resp.body}")

    q = urlencode({"catalogIds": json.dumps([catalog_id])})
    rotate_resp = _request_json(
        "POST",
        f"{api}/catalogs/public-token/rotate?{q}",
        headers=auth_headers,
        timeout=timeout,
    )
    _require(rotate_resp.status == 200, f"Bootstrap public-token başarısız: {rotate_resp.status} {rotate_resp.body}")
    public_token = rotate_resp.body.get("token", "")
    _require(bool(public_token), f"Bootstrap public token dönmedi: {rotate_resp.body}")

    return BootstrapResult(
        public_token=public_token,
        admin_token=admin_token,
        catalog_id=str(catalog_id),
        product_id=str(product_id),
    )


def run(args: argparse.Namespace) -> None:
    base = _trim_slash(args.base_url)
    api = f"{base}/api"
    now = datetime.utcnow().strftime("%Y%m%d%H%M%S")

    public_token = args.public_token or os.getenv("PARTALOG_PUBLIC_TOKEN", "")
    admin_token = args.admin_token or os.getenv("PARTALOG_ADMIN_TOKEN", "")

    bootstrap_catalog_id = ""
    bootstrap_product_id = ""
    if not public_token:
        if args.no_bootstrap:
            raise SmokeError("Public token gerekli (--public-token veya PARTALOG_PUBLIC_TOKEN).")

        admin_email = args.bootstrap_admin_email or f"smoke.admin.{now}@example.com"
        print("[bootstrap] Public token yok, fixture hazırlanıyor...")
        bootstrap = _bootstrap_public_fixture(
            api,
            timeout=args.timeout,
            admin_email=admin_email,
            admin_password=args.bootstrap_admin_password,
            admin_name=args.bootstrap_admin_name,
            company_name=args.bootstrap_company_name,
        )
        public_token = bootstrap.public_token
        admin_token = admin_token or bootstrap.admin_token
        bootstrap_catalog_id = bootstrap.catalog_id
        bootstrap_product_id = bootstrap.product_id
        print(f"[bootstrap] catalogId={bootstrap_catalog_id} productId={bootstrap_product_id}")

    phone = args.phone or f"90555{now[-7:]}"
    email = args.email or f"smoke+{now}@example.com"
    password = args.password
    full_name = args.name

    print(f"[1/9] Public kataloglar alınıyor: {api}/catalogs/public-by-token")
    q = urlencode({"token": public_token})
    catalogs_resp = _request_json("GET", f"{api}/catalogs/public-by-token?{q}", timeout=args.timeout)
    _require(catalogs_resp.status == 200, f"public-by-token başarısız: {catalogs_resp.status} {catalogs_resp.body}")

    catalogs = catalogs_resp.body if isinstance(catalogs_resp.body, list) else []
    _require(len(catalogs) > 0, "Public token için katalog bulunamadı.")

    catalog_id = args.catalog_id or bootstrap_catalog_id or catalogs[0].get("id")
    _require(bool(catalog_id), "Katalog ID alınamadı.")
    print(f"      seçilen catalogId={catalog_id}")

    print(f"[2/9] Katalog ürünleri alınıyor: {api}/products/catalog/{catalog_id}")
    q = urlencode({"token": public_token})
    products_resp = _request_json("GET", f"{api}/products/catalog/{catalog_id}?{q}", timeout=args.timeout)
    _require(products_resp.status == 200, f"products/catalog başarısız: {products_resp.status} {products_resp.body}")

    products = products_resp.body if isinstance(products_resp.body, list) else []
    _require(len(products) > 0, "Seçilen katalogda ürün bulunamadı.")

    selected_product = None
    wanted_product = args.product_id or bootstrap_product_id
    if wanted_product:
        selected_product = next((p for p in products if str(p.get("id")) == wanted_product), None)
    if selected_product is None:
        selected_product = products[0]

    product_id = selected_product.get("id")
    product_code = _first_non_empty(selected_product.get("code"), selected_product.get("partCode"), "SMOKE-PRODUCT")
    price = selected_product.get("price")
    if not isinstance(price, (int, float)):
        price = 1
    print(f"      seçilen productId={product_id} code={product_code} price={price}")

    print("[3/9] Public müşteri hesabı register/login")
    register_payload = {
        "publicToken": public_token,
        "name": full_name,
        "phone": phone,
        "email": email,
        "password": password,
    }
    register_resp = _request_json("POST", f"{api}/customers/public-auth/register", payload=register_payload, timeout=args.timeout)

    session_token = ""
    customer = {}
    if register_resp.status == 200:
        session_token = register_resp.body.get("sessionToken", "")
        customer = register_resp.body.get("customer", {}) if isinstance(register_resp.body, dict) else {}
        print("      register başarılı")
    elif register_resp.status == 409:
        print("      müşteri zaten var, login denenecek")
    else:
        raise SmokeError(f"register başarısız: {register_resp.status} {register_resp.body}")

    if not session_token:
        login_payload = {
            "publicToken": public_token,
            "phone": phone,
            "email": email,
            "password": password,
        }
        login_resp = _request_json("POST", f"{api}/customers/public-auth/login", payload=login_payload, timeout=args.timeout)
        _require(login_resp.status == 200, f"login başarısız: {login_resp.status} {login_resp.body}")
        session_token = login_resp.body.get("sessionToken", "")
        customer = login_resp.body.get("customer", {}) if isinstance(login_resp.body, dict) else {}

    _require(bool(session_token), "sessionToken alınamadı.")

    customer_name = _first_non_empty(customer.get("name"), full_name)
    customer_phone = _first_non_empty(customer.get("phone"), phone)
    customer_email = _first_non_empty(customer.get("email"), email)

    print("[4/9] Sipariş oluşturuluyor")
    idempotency_key = str(uuid.uuid4())
    order_payload = {
        "customerName": customer_name,
        "customerEmail": customer_email,
        "customerPhone": customer_phone,
        "deliveryAddress": "Smoke Test Mah. No:1",
        "deliveryCity": "Istanbul",
        "deliveryDistrict": "Kadikoy",
        "deliveryNote": "smoke-test",
        "paymentMethod": "KapidaOdeme",
        "publicToken": public_token,
        "publicSessionToken": session_token,
        "idempotencyKey": idempotency_key,
        "items": [
            {
                "productId": product_id,
                "partCode": product_code,
                "quantity": 1,
                "price": price,
            }
        ],
    }
    order_resp = _request_json(
        "POST",
        f"{api}/orders",
        payload=order_payload,
        headers={"Idempotency-Key": idempotency_key},
        timeout=args.timeout,
    )
    _require(order_resp.status == 200, f"sipariş oluşturma başarısız: {order_resp.status} {order_resp.body}")
    order_id = order_resp.body.get("orderId")
    _require(bool(order_id), f"orderId dönmedi: {order_resp.body}")
    print(f"      orderId={order_id} orderNumber={order_resp.body.get('orderNumber')}")

    print("[5/9] Privileged endpoint auth kontrolü")
    no_auth_orders = _request_json("GET", f"{api}/orders", timeout=args.timeout)
    _require_status_in(no_auth_orders.status, (401, 403), f"Anonim /api/orders erişimi engellenmedi: {no_auth_orders.body}")

    no_auth_users = _request_json("GET", f"{api}/users", timeout=args.timeout)
    _require_status_in(no_auth_users.status, (401, 403), f"Anonim /api/users erişimi engellenmedi: {no_auth_users.body}")

    no_auth_upload = _request_json("POST", f"{api}/files/upload", timeout=args.timeout)
    _require_status_in(no_auth_upload.status, (401, 403), f"Anonim /api/files/upload erişimi engellenmedi: {no_auth_upload.body}")

    print("[6/9] Müşteri sipariş listesi kontrol ediliyor")
    q = urlencode({"publicToken": public_token})
    public_session_headers = {"X-Public-Session": session_token}
    orders_resp = _request_json(
        "GET",
        f"{api}/customers/public-auth/orders?{q}",
        headers=public_session_headers,
        timeout=args.timeout,
    )
    _require(orders_resp.status == 200, f"public-auth/orders başarısız: {orders_resp.status} {orders_resp.body}")
    orders = orders_resp.body if isinstance(orders_resp.body, list) else []
    _require(any(str(o.get("id")) == str(order_id) for o in orders), "Yeni sipariş müşteri sipariş listesinde görünmüyor.")

    print("[7/9] Müşteri sipariş detayı kontrol ediliyor")
    detail_resp = _request_json(
        "GET",
        f"{api}/customers/public-auth/orders/{order_id}?{q}",
        headers=public_session_headers,
        timeout=args.timeout,
    )
    _require(detail_resp.status == 200, f"sipariş detayı başarısız: {detail_resp.status} {detail_resp.body}")
    detail_items = detail_resp.body.get("items", []) if isinstance(detail_resp.body, dict) else []
    _require(len(detail_items) > 0, "Sipariş detayında kalem bulunamadı.")

    print("[8/9] Idempotent replay kontrolü")
    replay_resp = _request_json(
        "POST",
        f"{api}/orders",
        payload=order_payload,
        headers={"Idempotency-Key": idempotency_key},
        timeout=args.timeout,
    )
    _require(replay_resp.status == 200, f"idempotent replay çağrısı başarısız: {replay_resp.status} {replay_resp.body}")
    replay_flag = replay_resp.body.get("idempotentReplay")
    _require(replay_flag is True, f"idempotentReplay true değil: {replay_resp.body}")

    print("[9/9] (Opsiyonel) Admin sipariş listesi kontrolü")
    if admin_token:
        admin_resp = _request_json(
            "GET",
            f"{api}/orders",
            headers={"Authorization": f"Bearer {admin_token}"},
            timeout=args.timeout,
        )
        _require(admin_resp.status == 200, f"admin /orders başarısız: {admin_resp.status} {admin_resp.body}")
        admin_orders = admin_resp.body if isinstance(admin_resp.body, list) else []
        _require(any(str(o.get("id")) == str(order_id) for o in admin_orders), "Yeni sipariş admin listesinde görünmüyor.")
        print("      admin doğrulaması başarılı")
    else:
        print("      admin token verilmedi, bu adım atlandı")

    print("\n✅ Smoke test başarılı.")
    print(f"   orderId={order_id}")
    print(f"   customerPhone={customer_phone}")
    print(f"   idempotencyKey={idempotency_key}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Public checkout smoke test runner")
    parser.add_argument("--base-url", default="http://localhost:5159", help="API base URL (default: http://localhost:5159)")
    parser.add_argument("--public-token", default=os.getenv("PARTALOG_PUBLIC_TOKEN", ""), help="Public access token")
    parser.add_argument("--admin-token", default=os.getenv("PARTALOG_ADMIN_TOKEN", ""), help="Optional admin JWT for /api/orders verification")
    parser.add_argument("--catalog-id", default="", help="Optional fixed catalog ID")
    parser.add_argument("--product-id", default="", help="Optional fixed product ID")
    parser.add_argument("--name", default="Smoke Customer", help="Customer full name")
    parser.add_argument("--phone", default="", help="Customer phone; boşsa otomatik üretilir")
    parser.add_argument("--email", default="", help="Customer email; boşsa otomatik üretilir")
    parser.add_argument("--password", default="SmokeP@ssw0rd!", help="Customer password")
    parser.add_argument("--timeout", type=int, default=25, help="HTTP timeout seconds")
    parser.add_argument("--no-bootstrap", action="store_true", help="Do not auto-bootstrap admin/catalog/product when public token is missing")
    parser.add_argument("--bootstrap-admin-email", default="", help="Bootstrap admin email (optional, auto-generated if empty)")
    parser.add_argument("--bootstrap-admin-password", default="SmokeAdm1nP@ss!", help="Bootstrap admin password")
    parser.add_argument("--bootstrap-admin-name", default="Smoke Admin", help="Bootstrap admin full name")
    parser.add_argument("--bootstrap-company-name", default="Smoke Machine", help="Company name written to admin profile for storefront")
    return parser.parse_args()


if __name__ == "__main__":
    try:
        run(parse_args())
    except SmokeError as e:
        print(f"\n❌ Smoke test başarısız: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:  # pragma: no cover
        print(f"\n❌ Beklenmeyen hata: {e}", file=sys.stderr)
        sys.exit(2)
