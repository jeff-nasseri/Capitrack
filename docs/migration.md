# Migration: Node/TypeScript → .NET 10 + Blazor

Capitrack was migrated from a **Node.js / TypeScript + Express + vanilla-JS** application to
**.NET 10 (ASP.NET Core Web API) + Blazor WebAssembly**. The migration was deliberately a
**like-for-like reimplementation**: same REST contract, same calculations, same CSV formats,
and the same UI (the original stylesheet was ported verbatim). This document records what
changed and what was intentionally kept.

## Stack: before vs. after

| Concern | Before (Node/TS) | After (.NET/Blazor) |
|---------|------------------|---------------------|
| Backend | Express (TypeScript) | ASP.NET Core Web API (.NET 10, C#) |
| Frontend | Server-served vanilla JS + HTML | Blazor WebAssembly SPA |
| Frontend hosting | Served by the Express server | Static WASM bundle served by **nginx** |
| Database access | `better-sqlite3` (raw SQL) | EF Core with the SQLite provider |
| Schema management | SQL on startup | EF Core `EnsureCreated()` (no migrations) |
| Auth | Express session cookie | ASP.NET Core cookie auth (`capitrack.sid`), BCrypt, 7-day cookie |
| Password hashing | bcrypt | `BCrypt.Net-Next` (work factor 12) |
| Prices | `yahoo-finance2` | hand-rolled `YahooFinanceClient` (crumb handshake + v8 fallback) |
| CSV | `csv-parse` | `CsvHelper` |
| Charts / icons | Chart.js + Font Awesome | Chart.js (via JS interop) + Font Awesome |
| Packaging | single container (npm) | two containers via one `docker compose` |
| Tests | Jest | xUnit (17 tests) |

## What stayed the same (behavioral parity)

- **REST contract.** The same endpoints, paths, query/body parameters, and **snake_case**
  JSON. The one camelCase exception — `PUT /api/auth/password` taking
  `{ currentPassword, newPassword }` — was preserved on purpose. See [api.md](api.md).
- **Holdings & wealth math.** Quantity = Σ(buy + transfer_in) − Σ(sell + transfer_out);
  weighted average cost; the total-cost (buy + fee − sell) rule; the dashboard's conversion of
  each holding to the base currency via `currency_rates`. The math was extracted into pure,
  unit-tested services (`HoldingsCalculator`, `WealthService`). See
  [architecture.md](architecture.md).
- **CSV import.** The same four formats (revolut-stocks, revolut-commodities, trezor,
  generic), the same header-based auto-detection, the same symbol mappings (e.g. `XAU→GC=F`,
  `BTC→BTC-USD`), the dividend-as-amount rule, and the same fingerprint
  (`account|symbol|type|qty|price|date`) de-duplication. See [csv-import.md](csv-import.md).
- **UI.** The same pages and layout, and the **original CSS ported verbatim**, so the look and
  feel are unchanged.
- **First-run seed (changed).** The original seeded one admin user **plus** three demo accounts
  and four currency rates. The .NET version seeds **only the admin user** — the database starts
  empty. The admin password is no longer a hard-coded default: set `CAPITRACK_INIT_PASSWORD`, or
  leave it empty and a strong random password is generated and printed to the logs on first run.

## Deliberately preserved quirks

These are not bugs introduced by the migration — they were carried over to match the original
behaviour exactly:

- **Portfolio history has no FX conversion.** `GET /api/prices/portfolio/history` replays
  transactions over historical prices and sums values/costs **in their native currencies**,
  without converting to the base currency. (The dashboard summary, by contrast, *does*
  convert.) This intentional inconsistency is preserved.
- **The Symbol "Quotes" tab shows N/A.** The quote source only returns
  `{ symbol, price, currency, name, change_percent }`. Fields like P/E ratio, market cap,
  52-week high/low, average volume, and dividend yield are not fetched, so the Quotes tab
  renders them as **N/A / 0** — exactly as before.

## What changed operationally

- **Two containers instead of one.** The original ran a single Express process that served
  both the API and the static frontend. Now an **nginx** container serves the Blazor WASM app
  and reverse-proxies `/api/*` to a separate **API** container. The proxy keeps everything
  same-origin so the auth cookie works without CORS. See [deployment.md](deployment.md).
- **Fresh SQLite volume.** Data now lives in the named volume **`capitrack-dotnet-data`** (the
  `.NET` build's volume), not the original `capitrack-data`. Existing data from the Node
  version is **not** migrated automatically — the new stack starts with a fresh database (and
  seeds first-run data).
- **DataProtection keys are persisted** to the data volume (`/app/data/dp-keys`) so the auth
  cookie survives container restarts. This has no equivalent in the original session setup and
  is new infrastructure required by ASP.NET Core's cookie auth.
- **The Database-path setting applies on restart.** Changing the SQLite path from
  **Settings → Database** writes `settings.json` and takes effect **after a restart** (the UI
  prompts a refresh), rather than hot-swapping the open database connection as the original
  attempted. EF Core also uses `EnsureCreated()` (no migrations), so changing entities does not
  alter an existing database — recreate it during development by deleting the file.
- **Some env vars are inert.** The carried-over `.env.template` still lists `PORT` and
  `SESSION_SECRET`, which the .NET stack does not use. The published host port is controlled by
  the compose port mapping (default `3000:80`), and cookie protection is handled by
  DataProtection.

## What was not carried over

The original codebase contained two features that were **dormant** (present but effectively
unused) and were intentionally left out of the migration:

- **The Categories API.** A `Category` entity still exists in the schema (with `parent_id` for
  hierarchy, referenced by `Goal.category_id`), but there is **no Categories controller** —
  categories cannot be created or managed through the API/UI. Goals retain a nullable
  `category_id` field for compatibility, but nothing populates it.
- **Folder auto-import.** The original had a dormant mechanism to auto-import transactions from
  a watched folder. The new app mounts `./transactions` read-only into the API container for
  convenience, but does **not** auto-import from it — importing is explicit, via the UI or the
  `POST /api/transactions/import/csv` endpoint.

## Notes for upgraders

- Use `docker compose up -d` with the new compose file. The app comes up on host port 3000 by
  default (a local `docker-compose.override.yml` may remap it).
- Because the data volume name changed, your previous Node-era data won't appear. If you need
  it, export your transactions from the old app (CSV) and re-import them into the new one — the
  generic CSV format round-trips cleanly with Capitrack's own export.
- The username defaults to `admin`; the first-run password is generated and printed to the
  logs (`docker compose logs api`) unless you set `CAPITRACK_INIT_PASSWORD`. Change it after
  the first login.
