#!/usr/bin/env python3
"""
Create or reuse a Google Cloud Monitoring email notification channel and attach
it to the Partalog alert policies.

The script intentionally does not guess a recipient. Pass an explicit
--email-address selected by the operator/on-call owner.
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request
from typing import Any


DEFAULT_POLICY_DISPLAY_NAMES = [
    "Partalog staging public availability",
    "Partalog staging Cloud Run reliability",
]


def _redact_email(value: str) -> str:
    value = (value or "").strip()
    if "@" not in value:
        return "***"
    local, domain = value.split("@", 1)
    if len(local) <= 2:
        safe_local = local[:1] + "***"
    else:
        safe_local = local[:2] + "***" + local[-1:]
    return f"{safe_local}@{domain}"


def _access_token(gcloud_bin: str) -> str:
    proc = subprocess.run(
        [gcloud_bin, "auth", "print-access-token"],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    if proc.returncode != 0:
        sys.stderr.write(proc.stderr)
        raise SystemExit(proc.returncode)
    token = proc.stdout.strip()
    if not token:
        raise SystemExit("gcloud did not return an access token")
    return token


def _request(
    *,
    method: str,
    url: str,
    token: str,
    body: dict[str, Any] | None = None,
) -> dict[str, Any]:
    data = None
    headers = {"Authorization": f"Bearer {token}"}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"

    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as response:
            raw = response.read().decode("utf-8")
            return json.loads(raw) if raw else {}
    except urllib.error.HTTPError as exc:
        error_body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {url} failed: {exc.code} {error_body}") from exc


def _list_channels(project_id: str, token: str) -> list[dict[str, Any]]:
    url = f"https://monitoring.googleapis.com/v3/projects/{project_id}/notificationChannels"
    data = _request(method="GET", url=url, token=token)
    return list(data.get("notificationChannels") or [])


def _create_email_channel(
    *,
    project_id: str,
    token: str,
    display_name: str,
    email_address: str,
    dry_run: bool,
) -> dict[str, Any]:
    body = {
        "type": "email",
        "displayName": display_name,
        "enabled": True,
        "labels": {"email_address": email_address},
    }
    if dry_run:
        print(
            "DRY RUN: would create email notification channel "
            f"display={display_name!r} email={_redact_email(email_address)}"
        )
        return {"name": "DRY_RUN_CHANNEL", **body}

    url = f"https://monitoring.googleapis.com/v3/projects/{project_id}/notificationChannels"
    return _request(method="POST", url=url, token=token, body=body)


def _find_or_create_channel(
    *,
    project_id: str,
    token: str,
    display_name: str,
    email_address: str,
    dry_run: bool,
) -> dict[str, Any]:
    channels = _list_channels(project_id, token) if not dry_run else []
    for channel in channels:
        if channel.get("type") != "email":
            continue
        labels = channel.get("labels") or {}
        if labels.get("email_address", "").strip().lower() == email_address.lower():
            print(
                "Reusing existing email notification channel "
                f"name={channel.get('name')} email={_redact_email(email_address)}"
            )
            return channel

    channel = _create_email_channel(
        project_id=project_id,
        token=token,
        display_name=display_name,
        email_address=email_address,
        dry_run=dry_run,
    )
    action = "Planned email notification channel" if dry_run else "Created email notification channel"
    print(f"{action} name={channel.get('name')} email={_redact_email(email_address)}")
    return channel


def _list_policies(project_id: str, token: str) -> list[dict[str, Any]]:
    url = f"https://monitoring.googleapis.com/v3/projects/{project_id}/alertPolicies"
    data = _request(method="GET", url=url, token=token)
    return list(data.get("alertPolicies") or [])


def _attach_channel_to_policy(
    *,
    token: str,
    policy: dict[str, Any],
    channel_name: str,
    dry_run: bool,
) -> None:
    policy_name = policy["name"]
    display_name = policy.get("displayName", policy_name)
    channels = list(policy.get("notificationChannels") or [])
    if channel_name in channels:
        print(f"Policy already attached: {display_name}")
        return

    channels.append(channel_name)
    if dry_run:
        print(f"DRY RUN: would attach channel to policy: {display_name}")
        return

    encoded_name = urllib.parse.quote(policy_name, safe="/")
    url = (
        f"https://monitoring.googleapis.com/v3/{encoded_name}"
        "?updateMask=notification_channels"
    )
    _request(
        method="PATCH",
        url=url,
        token=token,
        body={"notificationChannels": channels},
    )
    print(f"Attached channel to policy: {display_name}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", default="partalog")
    parser.add_argument("--gcloud-bin", default=".tools/google-cloud-sdk/bin/gcloud")
    parser.add_argument("--email-address", required=True)
    parser.add_argument(
        "--display-name",
        default="Partalog production on-call email",
    )
    parser.add_argument(
        "--policy-display-name",
        action="append",
        default=[],
        help="Alert policy display name to attach. Defaults to staging policies.",
    )
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    email_address = args.email_address.strip()
    if "@" not in email_address:
        raise SystemExit("--email-address must be a valid email-like address")

    policy_names = args.policy_display_name or DEFAULT_POLICY_DISPLAY_NAMES
    token = "DRY_RUN_TOKEN" if args.dry_run else _access_token(args.gcloud_bin)
    channel = _find_or_create_channel(
        project_id=args.project,
        token=token,
        display_name=args.display_name,
        email_address=email_address,
        dry_run=args.dry_run,
    )
    channel_name = channel.get("name")
    if not channel_name:
        raise SystemExit("notification channel response did not include a name")

    policies = _list_policies(args.project, token) if not args.dry_run else []
    policies_by_display_name = {item.get("displayName"): item for item in policies}
    for display_name in policy_names:
        if args.dry_run:
            print(f"DRY RUN: would look up policy {display_name!r}")
            continue
        policy = policies_by_display_name.get(display_name)
        if not policy:
            raise SystemExit(f"alert policy not found: {display_name}")
        _attach_channel_to_policy(
            token=token,
            policy=policy,
            channel_name=channel_name,
            dry_run=args.dry_run,
        )

    verification_status = channel.get("verificationStatus", "unknown")
    print(f"Channel verification status: {verification_status}")
    if verification_status not in {"VERIFIED", "unknown"}:
        print(
            "Reminder: email notification channels may require recipient "
            "verification in Google Cloud before they can page reliably."
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
