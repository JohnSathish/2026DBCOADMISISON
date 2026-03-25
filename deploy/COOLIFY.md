# Deploying ERP on Coolify (Ubuntu 24.04)

Production domain: **https://admissionsdbctura.com**

## Architecture

| Piece | Role |
|--------|------|
| **Coolify** | Control panel on Ubuntu 24.04; Docker; Traefik; TLS |
| **PostgreSQL** | Coolify “Database” resource or managed Postgres |
| **ERP.Api** | `Dockerfile` (repo root) or `deploy/docker/api/Dockerfile` — ASP.NET Core 8 on port **8080** |
| **Applicant portal** | `deploy/docker/web/Dockerfile` — nginx static files on port **80** |

## Build context

- **API Dockerfile:** repository root = `E:\Projects\ERP` (the folder containing `ERP.sln` and `src/server`).
- **Web Dockerfile:** same context (so `COPY src/client` works).

In Coolify: set **Build context** to the repo root. **API Dockerfile path:** `Dockerfile` or `deploy/docker/api/Dockerfile` (identical). **Web:** `deploy/docker/web/Dockerfile`.

### If Coolify uses Nixpacks instead of your Dockerfile

If the build log shows `railwayapp/nixpacks` and **.NET SDK 6**, the app will fail (`NETSDK1045` for net8.0). **Fix:** switch the service to **Dockerfile** build and point at `Dockerfile` (repo root) or `deploy/docker/api/Dockerfile`. As a fallback, this repo includes `nixpacks.toml` (`NIXPACKS_CSHARP_SDK_VERSION=8.0`) and `global.json` so Nixpacks can use **.NET 8** if you stay on auto-build.

## 1. PostgreSQL

1. Create a **PostgreSQL** database in Coolify (or use an external host).
2. Create database/user and note the connection string.

## 2. API service

1. New resource → **Dockerfile** (or Docker Compose pointing at `deploy/docker-compose.yml` for local tests only).
2. **Dockerfile:** `Dockerfile` (repo root) or `deploy/docker/api/Dockerfile`
3. **Port:** `8080` (internal). Expose publicly only if you use a **subdomain** (e.g. `api.admissionsdbctura.com`); otherwise keep it internal and route via Traefik on the **same** domain (see below).

### Required environment variables (Coolify)

Set these in the API service (do **not** commit secrets to git):

| Variable | Example / note |
|----------|------------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Host=host;Port=5432;Database=erp;Username=...;Password=...` |
| `Authentication__Jwt__Secret` | Long random string (32+ chars) |
| `Razorpay__KeySecret` | From Razorpay dashboard |
| `Notifications__Email__Password` | SMTP app password |
| Any other keys from `appsettings.json` you rely on in prod | Override with `Section__Key` format |

Optional overrides:

- `Admissions__ApplicantPortalBaseUrl` = `https://admissionsdbctura.com`
- `Cors__AllowedOrigins__0` = `https://admissionsdbctura.com` (array index syntax for env vars)

`appsettings.Production.json` already sets CORS and portal URL for **admissionsdbctura.com**; env vars override.

**Health check path:** `GET /health` → `200` body `ok`.

## 3. Web (Angular) service

1. New resource → **Dockerfile**: `deploy/docker/web/Dockerfile`
2. Port **80** internally.
3. **Domain:** `admissionsdbctura.com` (and `www` if you use it — add CORS for `www` as in `appsettings.Production.json`).
4. **Environment variable:** `API_UPSTREAM_HOST` = Docker DNS name of the **API** container (same network). The web image nginx **proxies `/api`** to `http://$API_UPSTREAM_HOST:8080`, avoiding **405** when `POST /api/...` would otherwise hit nginx only.

**API URL in the app:** `src/client/apps/src/environments/environment.prod.ts` uses  
`https://admissionsdbctura.com/api`.  
That works when Traefik routes **same host** `https://admissionsdbctura.com/api` → API container, **or** when nginx proxies `/api` to the API using `API_UPSTREAM_HOST`.

### Same-domain routing (recommended)

- **Frontend:** Traefik → nginx container (port 80) for path `/`).
- **API:** Traefik → API container (port 8080) for path **`/api`** (prefix strip if your proxy adds it).

If you prefer **api.admissionsdbctura.com** for the API instead:

1. Point the API resource at that subdomain.
2. Change `environment.prod.ts` to `https://api.admissionsdbctura.com/api`.
3. Set CORS `AllowedOrigins` to `https://admissionsdbctura.com` only.

Rebuild the web image after changing `environment.prod.ts`.

## 4. SSL

Coolify / Traefik issues Let’s Encrypt certificates for **`admissionsdbctura.com`**. Ensure DNS **A** record points to the server IP.

## 5. After deploy

- Run migrations (EF Core) against production DB (from CI or a one-off job: `dotnet ef database update` with production connection string).
- Confirm `GET https://admissionsdbctura.com/health` (if routed to API) or the API health URL.
- Test applicant login and a full application submit.

## 6. Local Docker smoke test

From repo root:

```bash
docker compose -f deploy/docker-compose.yml build
docker compose -f deploy/docker-compose.yml up
```

Adjust env vars in `deploy/docker-compose.yml` (JWT secret, DB password, etc.) before real use.
