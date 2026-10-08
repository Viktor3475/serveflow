#!/usr/bin/env python3
"""Verify the disposable production container through HTTPS with real staff cookies and SQL persistence."""
import argparse
import html
import http.cookiejar
import json
import os
import re
import secrets
import ssl
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("origin")
parser.add_argument("certificate")
parser.add_argument("environment_file")
parser.add_argument("state_directory")
parser.add_argument("--resume", action="store_true")
args = parser.parse_args()
context = ssl.create_default_context(cafile=args.certificate)
values = dict(line.split("=", 1) for line in Path(args.environment_file).read_text().splitlines() if line and not line.startswith("#"))

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

class Client:
    def __init__(self, cookie_file=None):
        self.cookies = http.cookiejar.LWPCookieJar(cookie_file)
        self.opener = urllib.request.build_opener(NoRedirect(), urllib.request.HTTPSHandler(context=context), urllib.request.HTTPCookieProcessor(self.cookies))

    def request(self, path, expected=200, data=None, headers=None):
        request = urllib.request.Request(args.origin + path, data=data, headers=headers or {})
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        code = response.code
        body = response.read().decode()
        assert code in (expected if isinstance(expected, tuple) else (expected,)), f"Unexpected HTTP {code} for {path}"
        return body, response.headers

    def token(self, path):
        page, _ = self.request(path)
        match = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
        assert match, "Missing CSRF field"
        return html.unescape(match.group(1))

    def login(self, email, password):
        token = self.token("/Account/Login")
        self.request("/Account/Login", 302, urllib.parse.urlencode({"Email": email, "Password": password, "__RequestVerificationToken": token}).encode(), {"Content-Type": "application/x-www-form-urlencoded"})
        auth = next(cookie for cookie in self.cookies if cookie.name == "__Host-ServeFlow.Auth")
        assert auth.secure and auth.path == "/", "Production staff cookie flags"

    def api(self, path, data=None, expected=200, token=None, headers=None):
        request_headers = {"Content-Type": "application/json", **(headers or {})}
        if token:
            request_headers["X-CSRF-TOKEN"] = token
        body, _ = self.request(path, expected, json.dumps(data).encode() if data is not None else None, request_headers)
        return json.loads(body) if body else None

state = Path(args.state_directory)
cookie_file = state / "admin.cookies"
admin = Client(str(cookie_file))
if args.resume:
    admin.cookies.load(ignore_discard=True)
    admin.request("/Dashboard")
    print("Existing production staff cookie remains valid after container restart.")
    raise SystemExit(0)

anonymous = Client()
anonymous.request("/Dashboard", 302)
admin.login(values["Bootstrap__Email"], values["Bootstrap__Password"])
admin.request("/Dashboard")
waiter = Client(); waiter.login(values["Demo__WaiterEmail"], values["Demo__WaiterPassword"])
kitchen = Client(); kitchen.login(values["Demo__KitchenEmail"], values["Demo__KitchenPassword"])
for client in (waiter, kitchen):
    _, headers = client.request("/Dashboard", (302, 403))
    if "Location" in headers:
        assert "/Account/AccessDenied" in headers["Location"]
tables, _ = waiter.request("/Tables")
table = re.search(r'/WaiterOrder/Index/([a-f0-9-]{36})', tables).group(1)
token = waiter.token("/WaiterOrder/Index/" + table)
menu = waiter.api(f"/api/staff/tables/{table}/menu")
item = menu["items"][0]
receipt = secrets.token_hex(32)
payload = {"idempotencyKey": str(uuid.uuid4()), "receiptToken": receipt, "notes": "Disposable container verification", "lines": [{"itemId": item["id"], "version": item["version"], "quantity": 2, "instructions": None, "options": []}]}
path = f"/api/staff/tables/{table}/orders"
waiter.request(path, 400, json.dumps(payload).encode(), {"Content-Type": "application/json"})
created = waiter.api(path, payload, 201, token)
replayed = waiter.api(path, payload, 200, token)
assert created["id"] == replayed["id"] and replayed["replayed"]
assert created["total"] == item["price"] * 2
waiter_csrf = waiter.token("/Operations")
kitchen_csrf = kitchen.token("/Kitchen")
order_id = created["id"]

def change(client, csrf, status):
    queue = client.api("/api/operations/queue")
    order = next(order for order in queue["orders"] if order["id"] == order_id)
    client.api(f"/api/operations/orders/{order_id}/status", {"status": status, "version": order["version"], "reason": None}, token=csrf)

for status in (1, 2, 3):
    change(kitchen, kitchen_csrf, status)
for status in (4, 5):
    change(waiter, waiter_csrf, status)
assert not any(order["id"] == order_id for order in waiter.api("/api/operations/queue")["orders"])
saved = anonymous.api(f"/api/dining/receipts/{order_id}", headers={"X-Receipt-Token": receipt})
assert saved["status"] == 5 and saved["total"] == created["total"]
assert waiter.api(f"/api/staff/tables/{table}/menu")["tableId"] == table
dashboard, _ = admin.request("/Dashboard")
assert "Collected in this range" in dashboard
capture_directory = os.environ.get("SERVEFLOW_SMOKE_REVIEW_DIR")
if capture_directory:
    capture = Path(capture_directory)
    capture.mkdir(parents=True, exist_ok=True)
    for name, path in (("dashboard", "/Dashboard"), ("tables", "/Tables"), ("menu", "/Menu")):
        page, _ = admin.request(path)
        (capture / (name + ".html")).write_text(page)
admin.cookies.save(ignore_discard=True)
print("Persisted HTTPS workflow passed: three staff roles, CSRF rejection, server totals, idempotency, kitchen-to-waiter lifecycle and private receipt.")
