"""
Times each Capitrack screen's API calls against a running, LOCAL instance: the first load and the
median of three warm loads. Calls are replayed as the original client made them (parallel groups,
then sequential steps), so numbers compare across versions; today's client parallelizes more, so
real screens are at least this fast.

Usage (password of the Capitrack user in the environment; the user defaults to "admin"):

    CAPITRACK_PASSWORD=... python tools/perf/measure_screens.py [--base http://localhost:8088] [--label "after"]

Read-only: it only GETs, plus the quotes endpoint the dashboard posts to.
"""
import argparse
import http.cookiejar
import json
import os
import statistics
import sys
import time
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor

parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
parser.add_argument("--base", default="http://localhost:8088", help="Capitrack URL (local only)")
parser.add_argument("--label", default="", help="a label printed with the results")
args = parser.parse_args()
if urllib.parse.urlparse(args.base).hostname not in ("localhost", "127.0.0.1", "::1"):
    sys.exit("Refusing a non-local Capitrack.")
password = os.environ.get("CAPITRACK_PASSWORD") or sys.exit("Set CAPITRACK_PASSWORD.")
BASE = args.base.rstrip("/")

opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method, headers={"Content-Type": "application/json"})
    with opener.open(req, timeout=300) as r:
        payload = r.read()
    return json.loads(payload) if payload else None


def get(path):
    return call("GET", path)


def parallel(*paths):
    with ThreadPoolExecutor(len(paths)) as ex:
        return list(ex.map(get, paths))


def timed(fn):
    start = time.perf_counter()
    fn()
    return (time.perf_counter() - start) * 1000


call("POST", "/api/auth/login", {"username": os.environ.get("CAPITRACK_USER", "admin"), "password": password})
accounts = get("/api/accounts")
if not accounts:
    sys.exit("No accounts to measure.")
main = next((a for a in accounts if a.get("type") == "crypto"), accounts[0])


def all_holdings():
    symbols = []
    for a in accounts:
        symbols += [h["symbol"] for h in get(f"/api/accounts/{a['id']}/holdings")]
    if symbols:
        call("POST", "/api/prices/quotes", {"symbols": sorted(set(symbols))})


def dashboard():
    parallel("/api/prices/dashboard/summary", "/api/accounts", "/api/goals", "/api/currencies")
    get("/api/prices/portfolio/history?period=3m")
    all_holdings()


def account_detail():
    i = main["id"]
    _, holdings, *_ = parallel(f"/api/accounts/{i}", f"/api/accounts/{i}/holdings", f"/api/transactions?account_id={i}",
                               "/api/currencies", "/api/auth/session")
    if holdings:
        call("POST", "/api/prices/quotes", {"symbols": sorted({h["symbol"] for h in holdings})})
    get(f"/api/prices/portfolio/history?account_id={i}&period=3m")


def symbol_detail():
    i = main["id"]
    holdings = get(f"/api/accounts/{i}/holdings")
    symbol = urllib.parse.quote(holdings[0]["symbol"] if holdings else "BTC-USD")
    parallel(f"/api/prices/quote/{symbol}", f"/api/transactions?account_id={i}&symbol={symbol}", f"/api/accounts/{i}/holdings")
    get("/api/prices/dashboard/summary")
    get(f"/api/prices/history/{symbol}?period=1y")


screens = [
    ("Dashboard", dashboard),
    ("Holdings", all_holdings),
    ("Accounts", lambda: parallel("/api/accounts", "/api/prices/dashboard/summary")),
    ("Account detail", account_detail),
    ("Symbol detail", symbol_detail),
    ("Activity", lambda: (get("/api/accounts"), get("/api/transactions?page=1&page_size=50"))),
    ("Calendar", lambda: (get("/api/transactions?limit=5000"), get("/api/prices/daily-wealth?start=2026-09-01&end=2026-09-30"))),
    ("Goals", lambda: parallel("/api/goals", "/api/prices/dashboard/summary")),
    ("Value history (all)", lambda: get("/api/prices/portfolio/history?period=all")),
]
print(f"== {args.label}  accounts={len(accounts)}")
for name, fn in screens:
    first = timed(fn)
    warm = [timed(fn) for _ in range(3)]
    print(f"{name:22s} first {first:8.0f} ms   warm median {statistics.median(warm):7.0f} ms")
