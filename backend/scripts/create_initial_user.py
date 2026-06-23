#!/usr/bin/env python3
"""Create the first panel owner or platform admin directly in PostgreSQL.

This script intentionally bypasses public registration endpoints. Use it for
first setup or controlled recovery only.
"""

from __future__ import annotations

import argparse
import base64
import getpass
import hashlib
import os
import secrets
import subprocess
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone


ITERATIONS = 120_000
SALT_SIZE = 16
HASH_SIZE = 32


class BootstrapError(RuntimeError):
    pass


@dataclass(frozen=True)
class PasswordHash:
    hash: str
    salt: str


def create_password_hash(password: str) -> PasswordHash:
    if not password.strip():
        raise BootstrapError("Password cannot be empty.")

    salt = secrets.token_bytes(SALT_SIZE)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, ITERATIONS, dklen=HASH_SIZE)
    return PasswordHash(
        hash=base64.b64encode(digest).decode("ascii"),
        salt=base64.b64encode(salt).decode("ascii"),
    )


def plan_to_int(value: str) -> int:
    normalized = value.strip().lower()
    aliases = {
        "1": 1,
        "catalog": 1,
        "catalog-only": 1,
        "catalogonly": 1,
        "2": 2,
        "ai": 2,
        "catalog-ai": 2,
        "catalogwithai": 2,
        "3": 3,
        "enterprise": 3,
        "ecommerce": 3,
        "catalogwithaiandecommerce": 3,
    }
    if normalized not in aliases:
        raise BootstrapError(f"Unknown plan: {value!r}")
    return aliases[normalized]


def normalize_role(value: str) -> str:
    normalized = value.strip().lower()
    if normalized == "owner":
        return "Owner"
    if normalized in {"platform-admin", "platformadmin"}:
        return "PlatformAdmin"
    raise BootstrapError("Role must be Owner or PlatformAdmin.")


def split_name(full_name: str, first_name: str, last_name: str) -> tuple[str, str]:
    if first_name.strip():
        return first_name.strip(), last_name.strip()

    parts = full_name.strip().split(maxsplit=1)
    if not parts:
        raise BootstrapError("Name is required.")
    return parts[0], parts[1] if len(parts) > 1 else ""


def psql_base_cmd(args: argparse.Namespace) -> list[str]:
    cmd = ["psql", "-v", "ON_ERROR_STOP=1"]
    if args.database_url:
        cmd.append(args.database_url)
    return cmd


def psql_vars(values: dict[str, object]) -> list[str]:
    vars_: list[str] = []
    for key, value in values.items():
        vars_.extend(["-v", f"{key}={value}"])
    return vars_


def run_psql(args: argparse.Namespace, sql: str, values: dict[str, object], *, capture: bool = False) -> str:
    cmd = psql_base_cmd(args) + psql_vars(values)
    if capture:
        cmd.extend(["-At", "-c", sql])
        result = subprocess.run(cmd, check=True, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        return result.stdout.strip()

    cmd.extend(["-c", sql])
    if args.dry_run:
        print("-- dry-run: psql command not executed")
        print(sql)
        return ""

    subprocess.run(cmd, check=True)
    return ""


def existing_user_id(args: argparse.Namespace, email: str) -> str | None:
    sql = 'SELECT "Id" FROM "Users" WHERE lower("Email") = lower(:\'email\') LIMIT 1;'
    output = run_psql(args, sql, {"email": email}, capture=True)
    return output or None


def upsert_user(args: argparse.Namespace) -> None:
    role = normalize_role(args.role)
    first_name, last_name = split_name(args.name, args.first_name, args.last_name)
    email = args.email.strip().lower()
    if not email:
        raise BootstrapError("Email is required.")

    password = args.password or os.getenv("BOOTSTRAP_USER_PASSWORD") or getpass.getpass("Password: ")
    password_confirm = args.password or os.getenv("BOOTSTRAP_USER_PASSWORD") or getpass.getpass("Password again: ")
    if password != password_confirm:
        raise BootstrapError("Passwords do not match.")
    password_hash = create_password_hash(password)

    plan = plan_to_int(args.plan)
    now = datetime.now(timezone.utc).isoformat()
    values = {
        "id": str(uuid.uuid4()),
        "first_name": first_name,
        "last_name": last_name,
        "email": email,
        "password_hash": password_hash.hash,
        "password_salt": password_hash.salt,
        "role": role,
        "company_name": args.company_name.strip(),
        "phone_number": args.phone_number.strip(),
        "subscription_plan": plan,
        "max_catalog_count": args.max_catalog_count,
        "max_page_per_catalog": args.max_page_per_catalog,
        "public_link_enabled": str(args.public_link_enabled).lower(),
        "now": now,
    }

    existing = existing_user_id(args, email) if not args.dry_run else None
    if existing and not args.update_existing:
        raise BootstrapError(
            f"User already exists for {email}. Use --update-existing to rotate password/profile intentionally."
        )

    if existing:
        values["existing_id"] = existing
        sql = """
UPDATE "Users"
SET
  "FirstName" = :'first_name',
  "LastName" = :'last_name',
  "PasswordHash" = :'password_hash',
  "PasswordSalt" = :'password_salt',
  "Role" = :'role',
  "CompanyName" = NULLIF(:'company_name', ''),
  "PhoneNumber" = NULLIF(:'phone_number', ''),
  "SubscriptionPlan" = :'subscription_plan'::int,
  "MaxCatalogCount" = :'max_catalog_count'::int,
  "MaxPagePerCatalog" = :'max_page_per_catalog'::int,
  "PublicLinkEnabled" = :'public_link_enabled'::boolean,
  "PlanActivatedAt" = COALESCE("PlanActivatedAt", :'now'::timestamptz),
  "UpdatedDate" = :'now'::timestamptz
WHERE "Id" = :'existing_id'::uuid;
"""
        run_psql(args, sql, values)
        action = "Would update" if args.dry_run else "Updated"
        print(f"{action} {role} user: {email}")
        return

    sql = """
INSERT INTO "Users" (
  "Id",
  "FirstName",
  "LastName",
  "Email",
  "PasswordHash",
  "PasswordSalt",
  "Role",
  "CompanyName",
  "PhoneNumber",
  "SubscriptionPlan",
  "MaxCatalogCount",
  "MaxPagePerCatalog",
  "PublicLinkVersion",
  "PublicLinkEnabled",
  "PlanActivatedAt",
  "CreatedDate",
  "UpdatedDate"
) VALUES (
  :'id'::uuid,
  :'first_name',
  :'last_name',
  :'email',
  :'password_hash',
  :'password_salt',
  :'role',
  NULLIF(:'company_name', ''),
  NULLIF(:'phone_number', ''),
  :'subscription_plan'::int,
  :'max_catalog_count'::int,
  :'max_page_per_catalog'::int,
  1,
  :'public_link_enabled'::boolean,
  :'now'::timestamptz,
  :'now'::timestamptz,
  NULL
);
"""
    run_psql(args, sql, values)
    action = "Would create" if args.dry_run else "Created"
    print(f"{action} {role} user: {email}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create initial Katalogcu owner/platform admin user.")
    parser.add_argument("--database-url", default=os.getenv("DATABASE_URL", ""), help="PostgreSQL connection string. Defaults to DATABASE_URL or psql env.")
    parser.add_argument("--email", required=True, help="Login email.")
    parser.add_argument("--password", default="", help="Password. Prefer BOOTSTRAP_USER_PASSWORD env or prompt.")
    parser.add_argument("--name", default="", help="Full name. Ignored if --first-name is set.")
    parser.add_argument("--first-name", default="", help="First name.")
    parser.add_argument("--last-name", default="", help="Last name.")
    parser.add_argument("--company-name", default="", help="Owner company name shown on portal.")
    parser.add_argument("--phone-number", default="", help="Owner phone number.")
    parser.add_argument("--role", default="Owner", choices=["Owner", "PlatformAdmin", "platform-admin", "platformadmin"], help="User role.")
    parser.add_argument("--plan", default="ai", help="catalog, ai, enterprise, or numeric 1/2/3.")
    parser.add_argument("--max-catalog-count", type=int, default=100)
    parser.add_argument("--max-page-per-catalog", type=int, default=500)
    parser.add_argument("--public-link-enabled", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--update-existing", action="store_true", help="Update password/profile when email already exists.")
    parser.add_argument("--dry-run", action="store_true", help="Print SQL without executing insert/update.")
    return parser.parse_args()


def main() -> int:
    try:
        upsert_user(parse_args())
        return 0
    except subprocess.CalledProcessError as exc:
        print(f"psql failed with exit code {exc.returncode}", file=sys.stderr)
        if exc.stderr:
            print(exc.stderr, file=sys.stderr)
        return exc.returncode
    except BootstrapError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
