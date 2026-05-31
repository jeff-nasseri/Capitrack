# Capitrack Documentation

**Capitrack** is a self-hosted, single-user personal wealth and investment-portfolio
tracker. It tracks holdings across multiple accounts (stocks, crypto, commodities),
pulls live prices from Yahoo Finance, computes portfolio value and gains, and stores
everything in a local SQLite database. The application ships as two Docker containers
managed by one `docker compose` file: an **ASP.NET Core Web API** (.NET 10) backend and
a **Blazor WebAssembly** single-page frontend served by **nginx**, which reverse-proxies
API calls to the backend so the authentication cookie works same-origin.

This folder contains the full documentation set. Start with whichever document matches
what you want to do.

## Documents

| Document | What it covers |
|----------|----------------|
| [architecture.md](architecture.md) | Technical architecture: the two-container topology, request flow, backend layering, the EF Core entity/schema list, cookie authentication + DataProtection, the snake_case JSON contract, the Yahoo Finance client, the holdings/wealth calculation rules, and the project layout. |
| [api.md](api.md) | Complete REST API reference. Every endpoint grouped by resource, with method, path, auth requirement, query/body parameters, representative JSON responses, and status codes. Documents the snake_case convention and the password endpoint's camelCase exception. |
| [usage.md](usage.md) | End-user guide. Walks through every page — Login, Dashboard, Holdings, Accounts, Activity, Account detail, Symbol detail, Calendar, Goals — and all nine Settings panels, with screenshots. |
| [csv-import.md](csv-import.md) | The CSV import feature: how auto-detection and de-duplication work, plus the recognized headers, an example file, and the row→transaction mapping for each of the four supported formats (revolut-stocks, revolut-commodities, trezor, generic). |
| [development.md](development.md) | Local development: prerequisites, repo layout, building with `dotnet build`, running the 17-test xUnit suite with `dotnet test`, running each project standalone, running via Docker, and the EF Core `EnsureCreated` approach. |
| [deployment.md](deployment.md) | Docker deployment: `docker compose up -d`, the two services, the environment-variable table, the data volume, the read-only transactions mount, the nginx `/api` proxy, ports, and how to change the published port. |
| [migration.md](migration.md) | Notes on the migration from the original Node/TypeScript + Express + vanilla-JS stack to .NET 10 + Blazor WebAssembly: what stayed the same (REST contract, math, CSV formats, UI), the deliberately-preserved quirks, what changed operationally, and what was dropped. |

## Quick start

```bash
docker compose up -d
```

Then open <http://localhost:3000> and log in as **`admin`**. The database starts empty and a
random admin password is printed to the logs on first run (`docker compose logs api`) unless
you set `CAPITRACK_INIT_PASSWORD`. Change it from **Settings → Security**. See
[deployment.md](deployment.md) for details.
