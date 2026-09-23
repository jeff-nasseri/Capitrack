# Development

How to build, test, and run Capitrack locally.

## Prerequisites

- **.NET 10 SDK** — all three projects target `net10.0`. The API is an `Microsoft.NET.Sdk.Web`
  project and the frontend is an `Microsoft.NET.Sdk.BlazorWebAssembly` project, so a single
  .NET 10 SDK install covers both.
- **Docker** (with Compose) — for the containerized run, and the simplest way to try the full
  stack end-to-end.
- No Node.js, npm, or native build tools are required (the original Node/TypeScript stack was
  replaced; see [migration.md](migration.md)).

## Repo layout

```
Capitrack/
├─ Capitrack.sln                 # solution referencing the three projects
├─ src/Capitrack.Api/            # ASP.NET Core Web API (EF Core + SQLite)
├─ src/Capitrack.Web/            # Blazor WebAssembly SPA
├─ tests/Capitrack.Tests/        # xUnit tests
├─ docker/                       # api.Dockerfile, web.Dockerfile, nginx.conf
├─ docker-compose.yml            # api + web services
└─ docs/                         # this documentation
```

Key NuGet dependencies:

| Project | Packages |
|---------|----------|
| `Capitrack.Api` | `Microsoft.EntityFrameworkCore.Sqlite`, `BCrypt.Net-Next`, `CsvHelper` |
| `Capitrack.Web` | `Microsoft.AspNetCore.Components.WebAssembly` (+ DevServer), `Microsoft.Extensions.Http` |
| `Capitrack.Tests` | `xunit`, `Microsoft.NET.Test.Sdk`, `Microsoft.EntityFrameworkCore.Sqlite`, project ref to the API |

## Build

Build the whole solution:

```bash
dotnet build Capitrack.sln
```

Or build a single project:

```bash
dotnet build src/Capitrack.Api/Capitrack.Api.csproj
dotnet build src/Capitrack.Web/Capitrack.Web.csproj
```

## Test

Two xUnit projects, run with the .NET 10 SDK (or its Docker image):

- **`tests/Server.Tests`**: domain, import and market-data logic against an in-memory SQLite
  database with the real EF model: exact decimal arithmetic and the schema upgrade, number and
  time parsing, the Trezor ledger rules, import keys and idempotency, preview == import,
  average-cost holdings, the value history and dashboard in the base currency, and every price
  provider against recorded responses in `Fixtures/prices` (tests never call a live service).
- **`tests/Client.Tests`**: bUnit tests of the Blazor components (Check & Import, icons,
  loading states).

```bash
dotnet test tests/Server.Tests/Server.Tests.csproj
dotnet test tests/Client.Tests/Client.Tests.csproj
```

Two server tests only run when pointed at local data that must never enter the repository:

| Variable | Test |
|----------|------|
| `CAPITRACK_LEGACY_DB` | Upgrades a copy of a real pre-decimal database and checks no value changed. |
| `CAPITRACK_PRICE_SAMPLES` | A JSON file of `[{symbol, utc, price}]` from real transactions; compares each provider's price at that moment (live calls). |

## Verification tools

- **`tools/reference/trezor_reference.py`**: computes each asset's balance and total fees from
  Trezor Suite CSV exports in plain Python decimals, independently of Capitrack's importer.
- **`tools/verify/verify_import.py`**: imports exports into a local Capitrack and checks the
  result against the reference exactly, that re-imports and overlapping subsets change nothing,
  and that the preview equals the import. It only adds accounts.
- **`tools/perf/measure_screens.py`**: times each screen's API calls (first and warm loads).

Both scripts take the user's password from `CAPITRACK_PASSWORD` and refuse a non-local URL:

```bash
CAPITRACK_PASSWORD=... python tools/verify/verify_import.py ~/Desktop/*_1_*.csv
CAPITRACK_PASSWORD=... python tools/perf/measure_screens.py --label before
```

## Run the API standalone

```bash
dotnet run --project src/Capitrack.Api/Capitrack.Api.csproj
```

On startup the API:

1. resolves the SQLite path (`DbPathResolver`): a persisted `settings.json` `db_path`
   overrides the `DB_PATH` env var, which defaults to `<app>/data/capitrack.db`;
2. creates the data directory and persists DataProtection keys under `<dataDir>/dp-keys`
   (so the auth cookie survives restarts);
3. calls `Database.EnsureCreated()` to create the schema (no migrations);
4. seeds first-run data via `SeedService` — **only** a single admin user, **only if** no user
   exists yet. The database otherwise starts empty (no demo accounts, transactions, goals or
   currency rates). The admin password comes from `CAPITRACK_INIT_PASSWORD`, or is randomly
   generated and printed to the logs if that variable is empty.

Useful environment variables for local runs:

| Variable | Purpose | Default |
|----------|---------|---------|
| `DB_PATH` | SQLite file location | `<app>/data/capitrack.db` |
| `CAPITRACK_INIT_USERNAME` | admin username (first run) | `admin` |
| `CAPITRACK_INIT_PASSWORD` | admin password (first run); empty → random, logged | _(empty)_ |
| `CAPITRACK_BASE_CURRENCY` | base currency (first run) | `EUR` |
| `CORS_ORIGINS` | comma-separated origins to enable a dev CORS policy (credentials allowed) | unset |
| `ASPNETCORE_URLS` | listen address | (SDK default; `http://+:8080` in the container) |

The seed values are only read when the database is empty; to re-seed, delete the SQLite file
(and `dp-keys`) and start again.

## Run the frontend standalone

```bash
dotnet run --project src/Capitrack.Web/Capitrack.Web.csproj
```

This serves the Blazor WASM app via the dev server. The SPA calls the API on **its own
origin** (the `HttpClient` base address is the host origin) and forwards the auth cookie via
`CookieHandler`. In the Docker setup nginx makes the API same-origin; when running the two
projects separately on different ports you'll need to bridge origins — set `CORS_ORIGINS` on
the API to the frontend's origin so the credentialed CORS policy is enabled. For everyday
development, running the full stack with Docker (below) avoids cross-origin cookie issues
entirely.

## Run via Docker

The closest thing to production, and the simplest way to run both pieces together:

```bash
docker compose up -d
```

This builds and starts the **api** (internal, port 8080) and **web** (nginx, host port 3000)
containers. Open <http://localhost:3000> and log in as `admin` (first-run password is printed
to `docker compose logs api`, or set `CAPITRACK_INIT_PASSWORD`). See
[deployment.md](deployment.md) for the full breakdown (volumes, env vars, ports, the nginx
proxy, and how to change the published port).

To rebuild after code changes:

```bash
docker compose up -d --build
```

## Database & EF Core notes

- Capitrack uses EF Core with **`EnsureCreated()`** and **no migrations**. The schema is
  derived from the entity model (`Models/Entities.cs`) and `OnModelCreating` configuration in
  `Data/CapitrackDbContext.cs`. Changing an entity will **not** alter an existing database —
  during development, delete the SQLite file to recreate the schema from scratch.
- Dates on `Transaction`, `Goal`, and `DailyWealth` are stored as `YYYY-MM-DD` **text**
  (matching the original app), not as native date columns.
- `CreatedAt` / `UpdatedAt` columns default to `CURRENT_TIMESTAMP` and are generated on
  insert.
