# Production: admissionsdbctura.com

**Server (VPS):** `82.25.110.120`  
**SSH:** `ssh root@82.25.110.120` (use your SSH key or password — never share keys in chat)

## DNS / nameservers (`ns1.dns-parking.com`, `ns2.dns-parking.com`)

“DNS parking” nameservers usually mean the **registrar’s** panel controls DNS. To point the site at Coolify:

1. Log in where you manage **admissionsdbctura.com** (registrar or DNS-parking panel).
2. Add an **A record**:
   - **Host:** `@` (or blank) → **Value:** `82.25.110.120`
   - Optional **www** → `82.25.110.120`
3. If your hosting provider asked you to use **their** nameservers instead, switch NS to those and add the same **A** records there.

SSL (Let’s Encrypt) in Coolify will work only after this **A** record propagates (often minutes–hours).

## PostgreSQL (your identifiers)

| Setting    | Value |
|-----------|--------|
| Database  | `dbc_admission_db` |
| Username  | `Dbct_DBAdmin` |
| Password  | Set **only** in Coolify env vars (not in git). |

**Connection string format** (replace host and password):

```text
Host=YOUR_POSTGRES_HOST;Port=5432;Database=dbc_admission_db;Username=Dbct_DBAdmin;Password=YOUR_PASSWORD
```

- If PostgreSQL is a **Coolify-managed** database on the same server, use the **internal hostname** Coolify shows (not always `localhost` from inside containers).
- If Postgres is **on the host** and API runs in Docker, you may need `host.docker.internal` or the host IP — see Coolify docs for “connect to host database”.

Set in Coolify API service as:

`ConnectionStrings__DefaultConnection` = (full line above)

## Coolify checklist (Ubuntu 24.04)

1. Install Coolify on `82.25.110.120` (if not already): [coolify.io](https://coolify.io/docs).
2. **Project** → connect this Git repo (or push to GitHub/GitLab and link).
3. **PostgreSQL:** create DB/user matching `dbc_admission_db` / `Dbct_DBAdmin`, or import credentials from your host panel.
4. **API** (Dockerfile `deploy/docker/api/Dockerfile`, context = repo root):
   - Port **8080**
   - Env: see `deploy/production.env.example` + real password + JWT secret + Razorpay/SMTP overrides as needed.
5. **Web** (Dockerfile `deploy/docker/web/Dockerfile`):
   - Domain **admissionsdbctura.com** (and **www** if needed).
6. **Reverse proxy:** route `https://admissionsdbctura.com/api` → API container; `/` → web container (Traefik labels in Coolify).
7. Run **EF migrations** against `dbc_admission_db` (from CI or a one-off job with production connection string).
8. Test: `https://admissionsdbctura.com/health` (if exposed) and applicant login.

## Security

- **Rotate** DB password and any other secrets if they were ever pasted in email/chat.
- Do not commit `appsettings.json` production secrets; use Coolify environment variables only.
