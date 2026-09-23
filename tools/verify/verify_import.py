"""
Checks a running, LOCAL Capitrack's import of Trezor Suite CSV exports against the independent
reference script (tools/reference/trezor_reference.py):

1. imports the files into a new crypto account and compares every asset's balance and total fees
   with the reference, exactly, and the rows read / rejected;
2. imports the same files twice more, then an overlapping subset, and checks nothing changes;
3. previews the files into a second new account, imports the previewed rows, and checks the
   balances the preview announced are the ones the import produced.

Usage (password of the Capitrack user in the environment; the user defaults to "admin"):

    CAPITRACK_PASSWORD=... python tools/verify/verify_import.py path/to/*_1_*.csv [--base http://localhost:8088]

Only talks to localhost, and only adds accounts; it never deletes anything. Exit code 1 on any mismatch.
"""
import argparse
import glob
import http.cookiejar
import json
import os
import subprocess
import sys
import urllib.parse
import urllib.request
import uuid
from decimal import Decimal as D

REFERENCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "reference", "trezor_reference.py")

parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
parser.add_argument("files", nargs="+", help="Trezor Suite CSV exports (globs allowed)")
parser.add_argument("--base", default="http://localhost:8088", help="Capitrack URL (local only)")
args = parser.parse_args()

FILES = sorted({f for pattern in args.files for f in (glob.glob(pattern) or [pattern])})
if urllib.parse.urlparse(args.base).hostname not in ("localhost", "127.0.0.1", "::1"):
    sys.exit("Refusing a non-local Capitrack: this check imports your exports.")
password = os.environ.get("CAPITRACK_PASSWORD") or sys.exit("Set CAPITRACK_PASSWORD.")
API = args.base.rstrip("/") + "/api"

opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(method, path, body=None, form=None):
    headers, data = {}, None
    if form is not None:
        boundary = uuid.uuid4().hex
        parts = []
        for name, value in form:
            if isinstance(value, tuple):
                fname, content = value
                parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{fname}"\r\n'
                             f'Content-Type: text/csv\r\n\r\n'.encode() + content + b"\r\n")
            else:
                parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
        data = b"".join(parts) + f"--{boundary}--\r\n".encode()
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    elif body is not None:
        data, headers["Content-Type"] = json.dumps(body).encode(), "application/json"
    with opener.open(urllib.request.Request(API + path, data=data, method=method, headers=headers)) as r:
        raw = r.read()
        return json.loads(raw, parse_float=D) if raw else None  # JSON numbers as exact decimals, never floats


def new_account(name):
    return call("POST", "/accounts", {"name": name, "type": "crypto", "currency": "USD", "icon": "wallet", "color": "#6366f1"})["id"]


def files_form(account_id, files, extra=()):
    """The multipart form: the account, extra fields, then each file (a path, or a (name, bytes) pair)."""
    form = [("account_id", str(account_id)), *extra]
    for f in files:
        form.append(("files", (os.path.basename(f), open(f, "rb").read()) if isinstance(f, str) else f))
    return form


def snapshot(account_id):
    holdings = call("GET", f"/accounts/{account_id}/holdings")
    txs = call("GET", f"/transactions?account_id={account_id}&limit=100000")
    fees = {}
    for t in txs:
        if t["type"] == "fee":
            fees[t["symbol"]] = fees.get(t["symbol"], D(0)) + D(str(t["quantity"]))
    balances = {h["symbol"]: D(str(h["quantity"])) for h in holdings}
    keys = sorted((t.get("import_key") or "", t["type"], str(t["quantity"])) for t in txs)
    return balances, fees, keys


call("POST", "/auth/login", {"username": os.environ.get("CAPITRACK_USER", "admin"), "password": password})
print("files:", [os.path.basename(f) for f in FILES])
ok = True

# 1. import vs reference
account = new_account("Trezor (import check)")
first = call("POST", "/transactions/import/csv/bulk", form=files_form(account, FILES))
print(f"import: rows read {first['total']}, added {first['imported']}, rejected rows {first['rejected']}")
for reason in first.get("rejections") or []:
    print("   rejected:", reason[:160])
reference = json.loads(subprocess.run([sys.executable, REFERENCE, *FILES, "--json"], capture_output=True, text=True, check=True).stdout,
                       parse_float=D)
balances, fees, keys = snapshot(account)
print(f"\n{'asset':<10}{'reference balance':>26}{'capitrack balance':>26}{'reference fees':>24}{'capitrack fees':>24}")
for unit, a in reference["assets"].items():
    symbol = f"{unit.upper()}-USD"
    rb, rf, cb, cf = D(str(a["balance"])), D(str(a["fees"])), balances.get(symbol, D(0)), fees.get(symbol, D(0))
    ok &= rb == cb and rf == cf
    print(f"{unit:<10}{str(rb):>26}{str(cb):>26}{str(rf):>24}{str(cf):>24}  {'OK' if (rb, rf) == (cb, cf) else 'MISMATCH'}")
extra = set(balances) - {f"{u.upper()}-USD" for u in reference["assets"]}
if extra:
    print("only in Capitrack:", extra)
    ok = False
rows_read = sum(f["rows_read"] for f in reference["files"])
excluded = sum(sum(f["excluded"].values()) for f in reference["files"])
rows_ok = (rows_read, excluded) == (first["total"], first["rejected"])
ok &= rows_ok
print(f"rows: reference {rows_read} read, {excluded} excluded | Capitrack {first['total']} read, {first['rejected']} rejected  "
      f"{'OK' if rows_ok else 'MISMATCH'}")

# 2. idempotency: the same files twice more, then the first half of each file
before = (balances, fees, keys)
for attempt in (2, 3):
    r = call("POST", "/transactions/import/csv/bulk", form=files_form(account, FILES))
    same = snapshot(account) == before
    ok &= same and r["imported"] == 0
    print(f"re-import #{attempt}: added {r['imported']}, updated {r.get('updated')} | unchanged: {same}")
subset = []
for path in FILES:
    lines = open(path, "rb").read().splitlines(keepends=True)
    subset.append(("subset-" + os.path.basename(path), b"".join(lines[: 1 + max(1, (len(lines) - 1) // 2)])))
r = call("POST", "/transactions/import/csv/bulk", form=files_form(account, subset))
same = snapshot(account) == before
ok &= same and r["imported"] == 0
print(f"overlapping subset: added {r['imported']}, updated {r.get('updated')} | unchanged: {same}")

# 3. preview == import
account = new_account("Trezor (preview check)")
preview = call("POST", "/transactions/import/preview", form=files_form(account, FILES))
selection, expected = [], {}
for f in preview["files"]:
    s = f["summary"]
    ok &= s["new"] + s["duplicates"] + s["updates"] + s["rejected"] + s["unselected"] == s["rows_read"]
    selection.append({"rows": [r["row"] for r in f["rows"] if r["status"] in ("new", "update")], "staked": []})
    expected.update({a["symbol"]: D(str(a["resulting_balance"])) for a in s["assets"]})
call("POST", "/transactions/import/selected", form=files_form(account, FILES, [("selection", json.dumps(selection))]))
actual = {h["symbol"]: D(str(h["quantity"])) for h in call("GET", f"/accounts/{account}/holdings")}
equal = all(actual.get(s, D(0)) == b for s, b in expected.items()) and set(actual) <= set(expected)
again = call("POST", "/transactions/import/preview", form=files_form(account, FILES))
new_after = sum(f["summary"]["new"] for f in again["files"])
ok &= equal and new_after == 0
print(f"preview == import: {'YES' if equal else 'NO'} | re-preview shows {new_after} new rows")

print("\nALL MATCH" if ok else "\nMISMATCHES FOUND")
sys.exit(0 if ok else 1)
