# Deployment

Capitrack ships as **two containers** managed by a single `docker-compose.yml`: an
ASP.NET Core API and an nginx-served Blazor WebAssembly frontend.

## Quick start

```bash
docker compose up -d
```

This builds both images and starts both services. When they're up, open:

```
http://localhost:3000
```

and log in as **`admin`**. The database starts **empty** (no demo data). On first run the
admin password comes from `CAPITRACK_INIT_PASSWORD`; if that is not set, a **strong random
password is generated and written to the logs**:

```bash
docker compose logs api
```

> **Change the password** after first login from **Settings → Security**. To set your own
> initial password, copy `.env.example` to `.env` and set `CAPITRACK_INIT_PASSWORD` before
> the first run (see the env-var table below).

Check status and logs:

```bash
docker compose ps
docker compose logs -f
```

Stop (keeping data) / tear down:

```bash
docker compose down            # stops containers; named volume is preserved
docker compose down -v         # also deletes the data volume (wipes the database!)
```

## Services

| Service | Image / build | Port | Notes |
|---------|---------------|------|-------|
| **api** (`capitrack-api`) | `docker/api.Dockerfile` (.NET SDK build → `aspnet:10.0` runtime) | `8080` (**`expose` only — internal**) | EF Core + SQLite; listens on `http://+:8080`; has a healthcheck. |
| **web** (`capitrack-web`) | `docker/web.Dockerfile` (.NET SDK publishes WASM → `nginx:alpine`) | **`3000:80`** (published) | Serves the SPA and reverse-proxies `/api/*` to the api container. `depends_on` the api healthcheck. |

The browser only ever talks to **web** (nginx) on port 3000. nginx serves the static WASM app
and forwards API calls to **api** on the internal Docker network, so the auth cookie stays
same-origin.

### API container details

- Built multi-stage: restore + `dotnet publish -c Release`, then run on the ASP.NET runtime
  image.
- `ENV ASPNETCORE_URLS=http://+:8080`, `ENV DB_PATH=/app/data/capitrack.db`.
- Installs `curl` for the healthcheck:
  `HEALTHCHECK ... CMD curl -fsS http://127.0.0.1:8080/health || exit 1`
  (the `GET /health` endpoint is anonymous and returns `{"status":"ok"}`).
- `restart: unless-stopped`.

### Web container details

- Built multi-stage: `dotnet publish` of the Blazor WASM project, then the published
  `wwwroot` is copied into `nginx:alpine`, with `docker/nginx.conf` as the site config.
- `restart: unless-stopped`; waits for `api` to be healthy before starting.

## Environment variables

Set on the **api** service in `docker-compose.yml`:

| Variable | Description | Compose default |
|----------|-------------|-----------------|
| `DB_PATH` | Path to the SQLite database file inside the container | `/app/data/capitrack.db` |
| `CAPITRACK_INIT_USERNAME` | Admin username created on **first run** (empty DB only) | `admin` |
| `CAPITRACK_INIT_PASSWORD` | Admin password on **first run**. If empty, a strong random password is generated and written to the logs | _(empty → random)_ |
| `CAPITRACK_BASE_CURRENCY` | Base/main currency on **first run** | `EUR` |

Additional variables the API understands (not set by default):

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_URLS` | Listen address (the image sets `http://+:8080`). |
| `CORS_ORIGINS` | Comma-separated origins to enable a credentialed dev CORS policy. Unused in the single-origin Docker setup. |

> The `CAPITRACK_INIT_*` values are only applied when the database has no user yet (the
> database starts **empty** — no demo accounts, transactions or currency rates). To set your
> own initial password, copy `.env.example` to `.env` (gitignored) and set
> `CAPITRACK_INIT_PASSWORD`. To change credentials later, use **Settings → Security** (or
> reset by clearing the data volume: `docker compose down -v`).
>
> No password is stored in the source tree or the compose file: an unset `CAPITRACK_INIT_PASSWORD`
> causes a random password to be generated and printed to the logs on first run.

## Data persistence

The api service mounts a **named volume** for all persistent state:

```yaml
volumes:
  - capitrack-dotnet-data:/app/data
```

`/app/data` contains:

- `capitrack.db` — the SQLite database (accounts, transactions, goals, tags, rates, price
  cache, daily wealth).
- `dp-keys/` — the **DataProtection key ring**. Persisting this is what lets the auth cookie
  survive container restarts (otherwise the keys would rotate and log everyone out).
- `settings.json` — written when you change the database path from Settings → Database.

The volume survives `docker compose down`. Use `docker compose down -v` to delete it (which
permanently erases the database).

## Transactions mount

The api service also mounts a host folder **read-only** at `/app/transactions`:

```yaml
volumes:
  - ./transactions:/app/transactions:ro
```

This is a convenient place to stage CSV files for manual import. Capitrack does **not**
auto-import from it; you import through the UI (account → Import) or the API. See
[csv-import.md](csv-import.md).

## The nginx /api proxy

`docker/nginx.conf` does three things:

1. **Proxies the API** — `location /api/` forwards to `http://api:8080`, passing through
   `Host`, `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`.
   Because this is same-origin from the browser's perspective, the `capitrack.sid` auth cookie
   is sent on every API call. (The API runs `UseForwardedHeaders()` to honour these headers.)
2. **Caches framework assets** — `location /_framework/` is served with
   `Cache-Control: public, max-age=31536000, immutable`.
3. **SPA fallback** — `location /` uses `try_files $uri $uri/ /index.html`, so client-side
   routes resolve on refresh.

## Ports & changing the published port

By default the app is published on host port **3000** (`3000:80` on the web service). To
publish on a different host port, change the left-hand side of the mapping, e.g. to use
**8088**:

```yaml
services:
  web:
    ports:
      - "8088:80"
```

The repo also includes a local-only `docker-compose.override.yml` that does exactly this
(remapping web to `8088:80`) because port 3000 is taken in that particular dev environment.
Docker Compose automatically merges an `override` file when present — so if it exists, the app
will come up on the override's port. Edit or delete that file to control the published port for
your environment. The internal API port (8080) does not need to change.

## Upgrading / rebuilding

After pulling new code, rebuild and restart:

```bash
docker compose up -d --build
```

The named data volume is reused, so your data is preserved. Because EF Core uses
`EnsureCreated()` (no migrations), an existing database schema is **not** altered on upgrade —
see [development.md](development.md) and [migration.md](migration.md).
