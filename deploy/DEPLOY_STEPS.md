# Step-by-step: make ERP live (Coolify + Ubuntu 24.04)

Domain: **admissionsdbctura.com** · Example server IP: **82.25.110.120**

---

## Phase 0 — Before you start

1. **Code is in Git** (GitHub/GitLab/Bitbucket) so Coolify can clone it. Push this repo if it is only on your PC.
2. **Secrets ready** (not in git): DB password, JWT secret (long random string), Razorpay secret, SMTP password.
3. **SSH access** to the server: `ssh root@YOUR_SERVER_IP`

---

## Phase 1 — DNS

1. Open your domain panel for **admissionsdbctura.com** (where `ns1.dns-parking.com` / `ns2.dns-parking.com` point, or your registrar).
2. Create **A records**:
   - **@** (or root) → `82.25.110.120`
   - **www** → `82.25.110.120` (optional)
3. Wait until they resolve (check with `ping admissionsdbctura.com` or an online DNS checker).

---

## Phase 2 — Install Coolify (once per server)

1. On a fresh **Ubuntu 24.04** VPS, follow the official install: [coolify.io/docs](https://coolify.io/docs) (one-line installer).
2. Open Coolify in the browser (`http://SERVER_IP:8000` or the URL Coolify shows).
3. Create an admin account and log in.

---

## Phase 3 — PostgreSQL database

1. In Coolify: **Databases** → **+ New** → **PostgreSQL** (or use an existing Postgres on the server).
2. Create database **`dbc_admission_db`** and user **`Dbct_DBAdmin`** with your password (or use Coolify’s generated DB and note name/user/password).
3. **Copy the internal connection details** Coolify shows (host, port, database, user). You will build the connection string in Phase 5.

---

## Phase 4 — Connect Git repo to Coolify

1. **Projects** → **+ New** → add your Git source (GitHub App, deploy key, or token).
2. Select the repository that contains this ERP codebase.

---

## Phase 5 — Deploy the API (ERP.Api)

**Important:** In Coolify, set the build type to **Dockerfile**, not the default **Nixpacks** auto-detect. Nixpacks may pick **.NET 6** and fail with `NETSDK1045` for .NET 8 projects. The repo includes `nixpacks.toml` + `global.json` to pin SDK 8 if you must use Nixpacks.

1. **New resource** → **Dockerfile** (not “Build Pack” / Nixpacks only).
2. **Repository:** your ERP repo  
3. **Build context:** repository **root** (folder that contains `ERP.sln`).  
4. **Dockerfile path:** `Dockerfile` (repo root) **or** `deploy/docker/api/Dockerfile` — same image. Leave blank only if Coolify defaults to `./Dockerfile` (the root file is provided for that).
5. **Port:** `8080` (container listens on 8080; Coolify/Traefik will map HTTPS to it).
6. **Environment variables** (minimum):

   | Variable | Value |
   |----------|--------|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | `Host=...;Port=5432;Database=dbc_admission_db;Username=Dbct_DBAdmin;Password=...` |
   | `Authentication__Jwt__Secret` | Long random string (32+ characters) |

   Add any other overrides from `appsettings.json` (Razorpay, SMTP, etc.) using `Section__Key` names.

7. **Deploy** and wait until the build succeeds.

---

## Phase 6 — Deploy the web (Angular applicant portal)

1. **New resource** → **Dockerfile** in the **same project**.
2. **Build context:** same repo **root**.
3. **Dockerfile path:** `deploy/docker/web/Dockerfile`
4. **Port:** `80`
5. **Domain:** `admissionsdbctura.com` (and `www` if you use it).
6. **Deploy** and wait until the build succeeds.

Production build uses `environment.prod.ts` with `apiBaseUrl: https://admissionsdbctura.com/api`.  
That only works if the browser can reach the API at **the same host** under `/api`.

---

## Phase 7 — Routing (same domain: `/` → web, `/api` → API)

1. In Coolify/Traefik, configure the **public** domain **admissionsdbctura.com**:
   - **Path `/`** → **web** container (nginx, port 80).
   - **Path `/api`** → **API** container (port **8080**).
2. Enable **HTTPS** (Let’s Encrypt) for `admissionsdbctura.com`.
3. If Coolify uses a **single service per domain**, use **two domains** instead:
   - `admissionsdbctura.com` → web only  
   - `api.admissionsdbctura.com` → API only  
   Then change `src/client/apps/src/environments/environment.prod.ts` to `https://api.admissionsdbctura.com/api`, rebuild web, and set **CORS** on the API to allow `https://admissionsdbctura.com`.

---

## Phase 8 — Database migrations

1. From your **dev machine** (with production connection string) or a **CI job**, run EF Core migrations against **dbc_admission_db**:

   ```bash
   dotnet ef database update --project src/server/Infrastructure/ERP.Infrastructure.csproj --startup-project src/server/Api/ERP.Api.csproj
   ```

   Set `ConnectionStrings__DefaultConnection` in environment or user secrets for that command.

2. Alternatively run a **one-off** container on the server with the same image as the API and the same env vars, executing `dotnet ef database update` if you install the EF tools there.

---

## Phase 9 — Smoke tests

1. **API health:** open `https://admissionsdbctura.com/health` (if routed to API) or the API URL Coolify shows → should return `ok`.
2. **Portal:** open `https://admissionsdbctura.com` → login page loads.
3. **API + CORS:** log in as applicant; if the browser blocks requests, check **CORS** in `appsettings.Production.json` and Coolify env for `Cors__AllowedOrigins__0`.

---

## Phase 10 — After go-live

1. **Backups:** enable DB backups in Coolify or your host.
2. **Monitoring:** use Coolify logs and server uptime alerts.
3. **Secrets:** rotate any password that was ever shared in chat/email.

---

## Troubleshooting — API image build fails early (exit 255)

If logs stop right after **Determining projects to restore…** or **dotnet publish**, the build worker often **ran out of memory** or hit a **time limit**. Actions:

1. **Coolify / server:** Increase **RAM** for Docker builds (and ensure the server isn’t swapping to death during `dotnet restore`).
2. **Redeploy** after pulling the latest `Dockerfile`: it restores with `--disable-parallel` and publishes with `-m:1` to lower peak memory.

If it still fails, open the **full build log** (download or raw) — the UI sometimes truncates the real MSBuild/NuGet error.

---

## Quick reference (paths in this repo)

| Item | Path |
|------|------|
| API Dockerfile | `Dockerfile` (root) or `deploy/docker/api/Dockerfile` |
| Web Dockerfile | `deploy/docker/web/Dockerfile` |
| Env template | `deploy/production.env.example` |
| Coolify details | `deploy/COOLIFY.md` |
| Domain + DB notes | `deploy/PRODUCTION_ADMISSIONSDBCTURA.md` |
