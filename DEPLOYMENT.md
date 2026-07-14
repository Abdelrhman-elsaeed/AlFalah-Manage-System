# Deployment Guide — Al-Falah Schools Evaluation System

**Last updated:** 2026-07-12 · **Audience:** DevOps / release engineers

This guide covers everything required to take the Al-Falah backend + frontend
from a clean checkout to a production-ready deployment. It is the canonical
companion to the spec kit (docs/) and is updated whenever Phase 10 hardening
changes the configuration surface.

---

## 1. Architecture at a glance

| Layer | Tech | Source | Production artefact |
|-------|------|--------|---------------------|
| Backend API | .NET 8 (ASP.NET Core Web API) | `backend/AlFalah.Api` | `dotnet publish` → self-contained exe or framework-dependent dll |
| Persistence | SQL Server (LocalDB for dev, SQL Server 2019+ for prod) | `backend/AlFalah.Infrastructure/Data` | EF Core migrations on first run |
| Frontend | Angular 17.3 + PrimeNG 17 (Saudi light-green theme) | `frontend/src` | `ng build` → static bundle under `frontend/dist/al-falah-app` |
| Reverse proxy | nginx / IIS / Azure App Service | — | Routes `/api/*` → backend, `/` → frontend |
| Email / SMS | NOT IN SCOPE for MVP (D-04) | — | Forgot/reset-password returns the token in Development only |

The system is a **modular monolith**, not microservices. There is one backend
process per environment.

---

## 2. Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| .NET SDK | 8.0.x | Required by all backend csproj files |
| Node.js | 20.x LTS | Required by Angular 17 |
| SQL Server | 2019+ (or LocalDB for dev) | The migrations apply to either |
| Browser | evergreen Chrome/Edge/Firefox | RTL Arabic + LTR English parity |

Optional:

- **dotnet-ef** tool (`dotnet tool install -g dotnet-ef`) for migration management.
- **IIS** with the ASP.NET Core Hosting Bundle if hosting on Windows.

---

## 3. Configuration

All deployment-tunable configuration lives in `appsettings.json` +
`appsettings.{Environment}.json` and can be overridden by environment variables
(double-underscore notation, e.g. `Jwt__Secret`).

### 3.1 Required settings

| Key | Required in prod? | Default | Notes |
|-----|-------------------|---------|-------|
| `ConnectionStrings:DefaultConnection` | YES | LocalDB for dev | Set via env var in production. |
| `Jwt:Secret` | YES | placeholder (dev only) | **MUST be ≥ 32 chars (256-bit)**. HS256 enforces this — startup fails otherwise. Use `dotnet user-secrets` or env var. |
| `Jwt:Issuer` | YES | `AlFalahApi` | |
| `Jwt:Audience` | YES | `AlFalahClient` | |
| `Jwt:AccessTokenExpiryMinutes` | NO | `60` | Refresh tokens last 30 days (rotation tracked via `ReplacedByToken`). |
| `Cors:AllowedOrigins` | YES | `http://localhost:4200` | **MUST** be the production frontend origin(s). Comma-separated. The backend fails closed if `Jwt:Secret` is missing. |

### 3.2 File upload settings

| Key | Default | Notes |
|-----|---------|-------|
| `FileUploads:SignatureMaxBytes` | `524288` (512 KiB) | Max length of the **base64-encoded** signature data URL. The decoded PNG is ~75% of this. |
| `FileUploads:ImageMagicBytesRequired` | `true` | If true, every signature is checked against the PNG magic bytes (`89 50 4E 47 0D 0A 1A 0A`). |

### 3.3 Example: production env vars

```bash
export ConnectionStrings__DefaultConnection="Server=prod-sql;Database=AlFalahDb;User Id=al_falah_app;Password=***;Encrypt=true;TrustServerCertificate=false"
export Jwt__Secret="$(openssl rand -base64 48)"
export Jwt__Issuer="AlFalahApi"
export Jwt__Audience="AlFalahClient"
export Cors__AllowedOrigins__0="https://eval.alfalah.example.com"
export ASPNETCORE_ENVIRONMENT="Production"
export ASPNETCORE_URLS="http://0.0.0.0:8080"   # behind a reverse proxy
```

For Windows + IIS, use `setx` / `Environment.SetEnvironmentVariable` or the
IIS Manager "Environment Variables" editor.

### 3.4 Secrets management

- **NEVER** commit real secrets to git. The dev `Jwt:Secret` in
  `appsettings.Development.json` is a clearly-labelled placeholder
  (`"AlFalah-Dev-Secret-Key-Must-Be-At-Least-32-Characters-Long!"`).
- For local dev, prefer `dotnet user-secrets` (init per project) over
  appsettings overrides.
- For production, inject via your secret manager (Azure Key Vault, AWS
  Secrets Manager, HashiCorp Vault, Kubernetes secrets, etc.). The
  standard ASP.NET Core configuration provider pattern is to chain
  `AddEnvironmentVariables()` → `AddAzureKeyVault()` etc.

---

## 4. Database migration + seed

The backend runs migrations + seed on startup (`Program.cs:209` +
`DatabaseSeeder.SeedAsync`). For a clean environment:

```bash
cd backend
dotnet tool restore                              # one-time
dotnet ef database update --project AlFalah.Infrastructure --startup-project AlFalah.Api
dotnet run --project AlFalah.Api                 # also runs seed
```

The seed is idempotent (`SyncRolePermissionsAsync` etc.) — running it
multiple times is safe. It inserts:

- 4 ASP.NET Identity roles (`SuperAdmin`, `MainManager`, `SchoolManager`,
  `Moderator`, `Instructor`).
- ~25 permission rows wired to the roles per `docs/03`.
- 1 active `RubricVersion` with 5 domains / 25 standards (D-21).
- 1 dev `superadmin` account with a clearly-labelled dev password
  (rotate before any non-dev deploy).

To rotate the dev creds: `reset_mgr.ps1` (in repo root) or the existing
`POST /api/v1/auth/forgot-password` + `/reset-password` flow.

---

## 5. Backend — build & run

### 5.1 Dev

```bash
cd backend
dotnet build
dotnet run --project AlFalah.Api      # listens on http://localhost:5264
```

Swagger UI: `http://localhost:5264/swagger`

### 5.2 Production

```bash
cd backend
dotnet publish AlFalah.Api -c Release -o ./publish
cd publish
ASPNETCORE_ENVIRONMENT=Production \
ASPNETCORE_URLS=http://0.0.0.0:8080 \
dotnet AlFalah.Api.dll
```

Or run as a service via systemd / IIS / Windows Service.

### 5.3 Health check

The backend has no dedicated `/health` endpoint in MVP. For a quick
readiness check, hit `GET /api/v1/auth/schools` (anonymous) — it should
return the active schools JSON within ~200 ms on a healthy environment.

---

## 6. Frontend — build & deploy

### 6.1 Dev

```bash
cd frontend
npm ci
npm start                            # ng serve on http://localhost:4200
```

### 6.2 Production

```bash
cd frontend
npm ci
npm run build                        # → dist/al-falah-app
```

The static bundle in `frontend/dist/al-falah-app` is what you deploy.
Upload it to any static host (nginx, S3, Azure Static Web Apps, Cloudflare Pages, …).

Configure the backend URL via `frontend/src/environments/environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://api.alfalah.example.com/api/v1'
};
```

### 6.3 Runtime requirements

- The frontend is a SPA — the host must rewrite all unknown routes to
  `index.html` (Angular router needs it).
- CORS on the backend **MUST** include the frontend origin in
  `Cors:AllowedOrigins` (Section 3.1).
- The bundle is large (~1 MB initial, ~30 lazy chunks). Enable gzip/brotli
  at the host layer.

---

## 7. Reverse proxy

A minimal `nginx` example for a single host serving both:

```nginx
server {
    listen 443 ssl http2;
    server_name eval.alfalah.example.com;

    ssl_certificate     /etc/letsencrypt/live/eval.alfalah.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/eval.alfalah.example.com/privkey.pem;

    # Frontend (static)
    root /var/www/al-falah-app;
    index index.html;
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Backend (API + Swagger)
    location /api/ {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 60s;
    }
    location /swagger/ {
        proxy_pass         http://127.0.0.1:8080;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

HTTPS is mandatory in production. Terminate TLS at the proxy and forward
plain HTTP to the backend on localhost. The backend already records
`X-Forwarded-For` for audit (D-30 + Phase 10).

---

## 8. Production checklist

Before going live, verify every box:

### 8.1 Security

- [ ] `Jwt:Secret` is a random ≥ 32-char value, **NOT** the dev placeholder.
- [ ] `ConnectionStrings:DefaultConnection` uses a dedicated low-privilege
      SQL user with `db_datareader` + `db_datawriter` only.
- [ ] SQL Server is reachable only from the backend (firewall / VNet rules).
- [ ] CORS `AllowedOrigins` lists ONLY the production frontend origin(s).
- [ ] The dev `superadmin` password has been rotated (D-07).
- [ ] No `appsettings.Development.json` is shipped to the production server.
- [ ] HTTPS is enforced end-to-end (HSTS at the proxy).
- [ ] `QUESTPDF_DEBUG` env var is unset (it enables layout-debug dumps).
- [ ] ASPNETCORE_ENVIRONMENT=Production (so stack traces aren't leaked in
      500 responses — GlobalExceptionMiddleware honours this).

### 8.2 Database

- [ ] `__EFMigrationsHistory` is up to date — last entry should be the
      Phase 8 / 9 / 10 migrations if those phases were applied.
- [ ] Soft-delete query filters are active (default — verify via
      `SELECT * FROM Schools WHERE IsDeleted = 1` returns expected rows).
- [ ] DB backups are scheduled (transaction-log every 15 min, full nightly).

### 8.3 Application

- [ ] `dotnet build -c Release` is green, 0 warnings.
- [ ] `npm run build` is green.
- [ ] `dotnet test AlFalah.Tests` is green (64/64 in Phase 10).
- [ ] `GET /api/v1/auth/schools` returns the active schools.
- [ ] A non-admin login (school_manager_1 / dev creds) succeeds and the
      `/api/v1/auth/me` response carries the right `active_school_id`.
- [ ] Role matrix (`role_matrix.ps1`) is exercised manually in a staging
      environment at least once per release.

### 8.4 Observability

- [ ] Application logs flow to your central aggregator (stdout → journald
      → Loki / CloudWatch / AppInsights — whatever you use).
- [ ] EF Core SQL logging is at `Warning` (production) or `Information`
      (dev) — see `appsettings.json` `Logging:LogLevel`.
- [ ] Failed-login attempts are monitored (`logger.LogWarning("Failed
      login attempt…")` in `AuthService`).
- [ ] AuditLog table growth is monitored — expected to grow ~100s of
      rows per day for a school of 50–200 staff.

---

## 9. Backup / restore

```sql
-- Daily full backup
BACKUP DATABASE [AlFalahDb] TO DISK = N'/var/backups/AlFalahDb_full.bak'
WITH COMPRESSION, INIT;

-- Transaction-log every 15 minutes
BACKUP LOG [AlFalahDb] TO DISK = N'/var/backups/AlFalahDb_log.bak'
WITH COMPRESSION;
```

Restoring to a point-in-time:

```sql
RESTORE DATABASE [AlFalahDb] FROM DISK = N'/var/backups/AlFalahDb_full.bak'
WITH NORECOVERY;
RESTORE LOG [AlFalahDb] FROM DISK = N'/var/backups/AlFalahDb_log.bak'
WITH RECOVERY, STOPAT = '2026-07-12 14:30:00';
```

---

## 10. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Jwt:Secret must be at least 32 characters` at startup | `Jwt:Secret` not set or too short | Set a 32+ char value via env var or user-secrets. |
| `IDX208` / signature validation error on JWT validation | Wrong issuer/audience OR clock skew | Match issuer + audience across backend & frontend; `ClockSkew = TimeSpan.Zero` is intentional (Program.cs). |
| 403 on every API call from frontend | CORS origin mismatch | Add the exact origin (scheme + host + port) to `Cors:AllowedOrigins`. |
| Arabic text shows as `???????` in DB | D-30 — request body arrived without `charset=utf-8` and a non-UTF-8 client. **Already-mangled rows are unrecoverable** — fix the client and re-create. The backend middleware re-encodes the body in D-30 middleware as a safety net. |
| CORS preflight fails silently | Browser blocked OPTIONS | Backend `AllowAnyHeader` + `AllowAnyMethod` are set (Program.cs); verify no upstream proxy strips OPTIONS. |
| `dotnet ef` fails on a CI machine with `Microsoft.Data.Sqlite` errors | Wrong package; LocalDB is Windows-only | Install SQL Server locally or use a connection string to a remote dev DB. |
| AuditLog table is empty | Seeder/permissions issue OR a write that should log silently swallowed | Check `_audit.Write` exceptions — they're logged at Warning, never rethrown. |
| Visit list endpoint slow | Pre-Phase-10 N+1 | D-56 fix applied — list is now a single projected query. Confirm the deployed DLL includes the `VisitService.ListAsync` rewrite. |

---

## 11. What Phase 10 changed (deployment-impacting)

| Area | Change | Backwards-compatible? |
|------|--------|------------------------|
| JWT secret length | Startup rejects secrets < 32 chars. | NO — production must set a proper secret before next deploy. |
| `FileUploads` config section | New; defaults are safe. | YES — omitting the section uses sensible defaults. |
| Signature magic-byte validation | Mandatory by default. | YES — opt-out via `FileUploads:ImageMagicBytesRequired=false`. |
| AuditLog coverage | Schools / users / UserSchoolRoles / improvement plans / follow-ups / signature all write audit rows now. | YES — purely additive. |
| Visit list projection (D-56) | Single SQL query instead of Include+in-memory count. | YES — same JSON shape. |
| Analysis engine extraction | New `AlFalah.Application.Analysis.VisitAnalysisEngine` static class. | YES — `VisitService.ComputeAnalysis` now delegates to it. |

---

## 12. References

- Spec kit: `docs/README.md` + the 14 numbered docs in `docs/`.
- Phase 10 file: `docs/phases/PHASE-10-HARDENING.md`.
- Deviations log: `docs/14-DECISIONS-AND-DEVIATIONS.md`.
- Constitution (must-read for any agent): `.spec/constitution.md`.
- Tests: `backend/AlFalah.Tests/` (64 tests, xUnit + FluentAssertions).