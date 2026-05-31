# REST API Reference

All endpoints are mounted under **`/api`**. In the normal Docker setup the browser calls
them on the same origin as the web app (port 3000), and nginx proxies them to the API
container.

## Conventions

- **JSON is snake_case.** Request and response bodies use snake_case property names
  (`base_currency`, `account_id`, `tag_ids`, `change_percent`, …). The one exception is the
  change-password endpoint, which uses **camelCase** (`currentPassword`, `newPassword`).
  Dictionary-keyed responses (the `/prices/quotes` map keyed by symbol) keep their original
  keys.
- **Authentication is a cookie.** Log in via `POST /api/auth/login`; the server sets the
  `capitrack.sid` cookie. All endpoints require the cookie **except** `POST /auth/login`,
  `POST /auth/logout`, `GET /auth/session`, and `GET /health`, which are anonymous.
- **Unauthenticated calls return `401`** (not a redirect) with `{ "error": "..." }`.
- **Status codes:** `200 OK` for reads/updates/deletes, `201 Created` for creates,
  `400 Bad Request` for validation errors, `401 Unauthorized` for auth failures,
  `404 Not Found` for missing resources/symbols, `500` for upstream/price errors.
- **Error shape:** failures return `{ "error": "human-readable message" }`. Simple successes
  often return `{ "message": "..." }`.
- **IDs** in paths are integers (`{id:int}`). Dates are `YYYY-MM-DD` strings.

---

## Auth — `/api/auth`

### POST /api/auth/login  *(anonymous)*

Authenticate and receive the session cookie.

Body:

```json
{ "username": "admin", "password": "your-password" }
```

`200`:

```json
{ "username": "admin", "base_currency": "EUR" }
```

`400` if username/password missing; `401` `{ "error": "Invalid credentials" }` otherwise.

### POST /api/auth/logout  *(anonymous)*

Clears the session cookie. `200` → `{ "message": "Logged out" }`.

### GET /api/auth/session  *(anonymous)*

Returns the current user if the cookie is valid, else `401`.

`200`:

```json
{ "username": "admin", "base_currency": "EUR" }
```

`401` → `{ "error": "Not authenticated" }`.

### PUT /api/auth/password

Change the password. **camelCase body** (the exception to the snake_case rule).

```json
{ "currentPassword": "admin", "newPassword": "N3w!Secret" }
```

The new password must be ≥ 8 characters and contain an uppercase letter, a lowercase
letter, a digit, and one of `! @ # $ % ^ & *`.

`200` → `{ "message": "Password updated" }`. `400` if missing or too weak; `401`
`{ "error": "Current password is incorrect" }`.

### PUT /api/auth/currency

Set the user's base/main currency (drives dashboard conversions).

```json
{ "base_currency": "USD" }
```

`200` → `{ "message": "Base currency updated" }`. `400` if missing.

---

## Accounts — `/api/accounts`

An account DTO:

```json
{
  "id": 1,
  "name": "Crypto Portfolio",
  "type": "crypto",
  "currency": "USD",
  "description": "Main crypto holdings",
  "icon": "bitcoin",
  "color": "#f59e0b",
  "created_at": "2026-05-31T10:00:00",
  "updated_at": "2026-05-31T10:00:00",
  "tags": []
}
```

### GET /api/accounts

List all accounts (newest first). `200` → array of account DTOs.

### POST /api/accounts

Create an account. Only `name` is required; the rest default
(`type=general`, `currency=EUR`, `icon=wallet`, `color=#6366f1`).

```json
{ "name": "Stocks", "type": "stock", "currency": "USD",
  "description": "", "icon": "chart-line", "color": "#10b981",
  "tag_ids": [1, 2] }
```

`201` → the created account DTO. `400` if `name` is empty.

### GET /api/accounts/{id}

`200` → account DTO. `404` if not found.

### PUT /api/accounts/{id}

Partial update — omitted/empty fields keep their current value; `tag_ids`, when supplied,
**replaces** the account's tag set. `200` → updated DTO. `404` if not found.

### DELETE /api/accounts/{id}

Deletes the account (and, via cascade, its transactions and tag links). `200` →
`{ "message": "Account deleted" }`. `404` if not found.

### GET /api/accounts/{id}/holdings

Per-symbol holdings for the account (quantity, average cost, total cost, transaction count,
first/last dates), ordered by total cost descending.

```json
[
  {
    "symbol": "BTC-USD",
    "quantity": 0.5,
    "avg_cost": 30000.0,
    "total_cost": 15010.0,
    "transaction_count": 2,
    "first_transaction": "2024-01-10",
    "last_transaction": "2024-03-02"
  }
]
```

`404` if the account does not exist.

### DELETE /api/accounts/purge/all

Danger-zone wipe: deletes **all** accounts, transactions, goals, tags, and cached prices.
`200` → `{ "message": "All accounts, transactions, goals, and cached prices have been purged." }`.

---

## Transactions — `/api/transactions`

A transaction DTO:

```json
{
  "id": 12,
  "account_id": 1,
  "symbol": "AAPL",
  "type": "buy",
  "quantity": 10.0,
  "price": 150.0,
  "fee": 1.0,
  "currency": "USD",
  "date": "2024-02-01",
  "notes": "",
  "created_at": "2024-02-01T09:00:00",
  "account_name": "Stock Portfolio",
  "tags": []
}
```

`type` is one of `buy`, `sell`, `transfer_in`, `transfer_out`, `dividend`, `interest`, `fee`.

### GET /api/transactions

List transactions (newest first by date, then id). Query parameters (all optional):

| Param | Meaning |
|-------|---------|
| `account_id` | filter to one account |
| `symbol` | filter to one symbol |
| `limit` | max rows to return |
| `offset` | rows to skip (paging) |

`200` → array of transaction DTOs.

### GET /api/transactions/{id}

`200` → transaction DTO. `404` if not found.

### POST /api/transactions

Create a transaction. `account_id`, `symbol`, `type`, and `date` are required; numeric
fields default to 0 and `currency` defaults to `EUR`. The symbol is upper-cased.

```json
{ "account_id": 1, "symbol": "aapl", "type": "buy",
  "quantity": 10, "price": 150, "fee": 1, "currency": "USD",
  "date": "2024-02-01", "notes": "", "tag_ids": [] }
```

`201` → created DTO. `400` if required fields missing; `404` if the account doesn't exist.

### PUT /api/transactions/{id}

Partial update (same omit-keeps-value semantics as accounts; `tag_ids` replaces the set).
`200` → updated DTO. `404` if not found.

### DELETE /api/transactions/{id}

`200` → `{ "message": "Transaction deleted" }`. `404` if not found.

### GET /api/transactions/export/csv

Download transactions as CSV (optionally filtered by `account_id`). Returns a `text/csv`
file (`transactions.csv`) with columns:
`id, account_name, symbol, type, quantity, price, fee, currency, date, notes`.

### POST /api/transactions/import/csv

Import a CSV file. **`multipart/form-data`** with:

| Field | Required | Meaning |
|-------|----------|---------|
| `file` | yes | the CSV file |
| `account_id` | yes | target account |
| `format` | no | force a format (`revolut-stocks`, `revolut-commodities`, `trezor`, `generic`); auto-detected if omitted |

`200`:

```json
{ "imported": 8, "skipped": 2, "total": 10, "errors": [], "format": "revolut-stocks" }
```

`400` if `file` missing or the CSV cannot be parsed; `404` if the account doesn't exist.
See [csv-import.md](csv-import.md) for format details.

### POST /api/transactions/import/detect

Detect the format of a CSV without importing. `multipart/form-data` with `file`.

`200`:

```json
{ "format": "trezor", "headers": ["Transaction ID", "Type", "Amount", "Amount unit", "Date"] }
```

`format` is `unknown` when no parser matches.

---

## Goals — `/api/goals`

A goal DTO:

```json
{
  "id": 1,
  "title": "Emergency fund",
  "target_amount": 10000.0,
  "target_date": "2026-12-31",
  "description": "",
  "achieved": 0,
  "category_id": null,
  "created_at": "2026-01-01T00:00:00",
  "updated_at": "2026-01-01T00:00:00",
  "tags": []
}
```

### GET /api/goals

List goals (ordered by target date). Optional query: `category_id`, `tag_id`. `200` → array.

### GET /api/goals/{id}

`200` → goal DTO. `404` if not found.

### POST /api/goals

`title` and `target_date` required.

```json
{ "title": "Emergency fund", "target_amount": 10000, "target_date": "2026-12-31",
  "description": "", "achieved": false, "tag_ids": [] }
```

`201` → created DTO. `400` if title/target date missing.

### PUT /api/goals/{id}

Partial update. `achieved` is a boolean in the request but stored as 0/1 and returned as an
integer. `200` → updated DTO. `404` if not found.

### DELETE /api/goals/{id}

`200` → `{ "message": "Goal deleted" }`. `404` if not found.

### DELETE /api/goals

Delete **all** goals. `200` → `{ "message": "All goals deleted" }`.

---

## Tags — `/api/tags`

A tag DTO: `{ "id": 1, "name": "long-term", "color": "#6366f1", "created_at": "..." }`.

### GET /api/tags

List tags (alphabetical). `200` → array.

### GET /api/tags/{id}

`200` → tag DTO. `404` if not found.

### POST /api/tags

`name` required; `color` defaults to `#6366f1`. `201` → created DTO. `400` if name missing
or a tag with that name already exists.

### PUT /api/tags/{id}

Update name/color. `200` → updated DTO. `400` if the new name collides with another tag;
`404` if not found.

### DELETE /api/tags/{id}

`200` → `{ "message": "Tag deleted" }`. `404` if not found.

---

## Currencies — `/api/currencies`

A rate DTO:
`{ "id": 1, "from_currency": "USD", "to_currency": "EUR", "rate": 0.92, "updated_at": "..." }`.

### GET /api/currencies

List rates (ordered by from/to). `200` → array.

### POST /api/currencies

Upsert a rate by (`from_currency`, `to_currency`). Currencies are upper-cased.

```json
{ "from_currency": "USD", "to_currency": "EUR", "rate": 0.92 }
```

`201` → the saved rate DTO. `400` if any field missing. (If the pair already exists its rate
is updated in place.)

### PUT /api/currencies/{id}

Update a rate by id. `200` → updated DTO. `404` if not found.

### DELETE /api/currencies/{id}

`200` → `{ "message": "Rate deleted" }`. `404` if not found.

### GET /api/currencies/convert

Convert an amount. Query: `from`, `to`, `amount` (all required).

```
GET /api/currencies/convert?from=USD&to=EUR&amount=100
```

`200` → `{ "result": 92.0, "rate": 0.92 }`. If `from == to`, returns the amount unchanged
with `rate: 1`. `400` if a param is missing; `404` if no rate exists for the pair.

---

## Prices — `/api/prices`

A quote DTO carries only the fields Yahoo's quote/chart-meta provides:

```json
{ "symbol": "AAPL", "price": 187.4, "currency": "USD", "name": "Apple Inc.", "change_percent": 0.83 }
```

A `stale` boolean is added only when the value came from a stale cache. (Richer fields such
as P/E or market cap are not fetched — see [architecture.md](architecture.md).)

### GET /api/prices/quote/{symbol}

Live quote for one symbol (5-minute cache, stale fallback). `200` → quote DTO. `404`
`{ "error": "Could not fetch price for X" }` if nothing is available.

### POST /api/prices/quotes

Batch quotes. Body `{ "symbols": ["AAPL", "BTC-USD"] }`. Returns a **map keyed by symbol**
(keys are not snake_cased):

```json
{
  "AAPL":    { "symbol": "AAPL", "price": 187.4, "currency": "USD", "name": "Apple Inc.", "change_percent": 0.83 },
  "BTC-USD": { "symbol": "BTC-USD", "price": 0, "error": "Service unavailable" }
}
```

`400` if `symbols` is missing.

### GET /api/prices/history/{symbol}

Historical OHLCV. Query `period` (default `1y`): `1w`, `1m`, `3m`, `6m`, `1y`, `5y`, or
`max`. (The interval is chosen automatically: hourly for `1w`, daily for `1m`, otherwise
weekly.) `200` → array of points with a non-null close:

```json
[ { "date": "2024-01-05T00:00:00", "close": 181.2, "open": 180.0, "high": 182.1, "low": 179.5, "volume": 51000000 } ]
```

`404` if history can't be fetched.

### GET /api/prices/search/{query}

Symbol search via Yahoo. `200`:

```json
[ { "symbol": "AAPL", "name": "Apple Inc.", "type": "EQUITY", "exchange": "NMS" } ]
```

`500` on upstream error.

### GET /api/prices/dashboard/summary

Total wealth, cost, and gain in the base currency, plus a per-account breakdown.

```json
{
  "total_wealth": 25340.55,
  "total_cost": 21000.0,
  "total_gain": 4340.55,
  "total_gain_percent": 20.67,
  "base_currency": "EUR",
  "accounts": [
    { "account_id": 1, "account_name": "Crypto Portfolio", "market_value": 12000.0, "cost_basis": 9000.0, "holdings_count": 2 }
  ],
  "holdings_count": 5
}
```

`500` on error.

### GET /api/prices/portfolio/history

Portfolio value over time (transaction replay over historical prices, **no FX conversion**).
Query `account_id` (optional, scope to one account) and `period` (default `3m`): `1w`, `1m`,
`3m`, `6m`, `ytd`, `1y`, `5y`, `all`.

```json
[ { "date": "2024-03-01", "value": 18250.0, "cost": 17000.0, "gain": 1250.0 } ]
```

`500` on error.

### GET /api/prices/daily-wealth

Saved daily-wealth snapshots between two dates. Query `start`, `end` (`YYYY-MM-DD`, both
required).

```json
[ { "date": "2026-05-30", "total_wealth": 25000.0, "total_cost": 21000.0,
    "base_currency": "EUR", "details": { "accounts": [], "holdings_count": 5 } } ]
```

`400` if `start`/`end` missing.

### POST /api/prices/daily-wealth

Compute and upsert **today's** snapshot using cached prices. `200`:

```json
{ "date": "2026-05-31", "total_wealth": 25340.55, "total_cost": 21000.0, "base_currency": "EUR" }
```

---

## Settings — `/api/settings`

### GET /api/settings

```json
{ "db_path": "/app/data/capitrack.db", "version": "1.0.0",
  "app_name": "Capitrack", "repository": "https://github.com/jeff-nasseri/Capitrack",
  "license": "MIT" }
```

### GET /api/settings/database

```json
{ "path": "/app/data/capitrack.db", "exists": true }
```

### PUT /api/settings/database

Set the SQLite path (persisted to `settings.json`; takes effect on restart). Body
`{ "path": "/app/data/capitrack.db" }`. Creates the directory if needed.

`200` → `{ "message": "Database path updated successfully", "path": "...", "exists": false }`.
`400` if the path is missing or the directory can't be created.

### POST /api/settings/refresh

A no-op "refresh" hook. `200` → `{ "message": "Application refreshed successfully", "db_path": "..." }`.

### GET /api/settings/about

```json
{ "name": "Capitrack",
  "description": "Personal wealth tracking and investment portfolio management platform",
  "version": "1.0.0", "license": "MIT",
  "repository": "https://github.com/jeff-nasseri/Capitrack",
  "author": "Jeff Nasseri", "open_source": true }
```

---

## Health — `/health`

### GET /health  *(anonymous, not under /api)*

`200` → `{ "status": "ok" }`. Used by the API container's Docker healthcheck.
