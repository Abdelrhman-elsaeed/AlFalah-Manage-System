# Al-Falah Schools Evaluation System

نظام تقييم مدارس الفلاح — A comprehensive school evaluation system built with ASP.NET Core + Angular.

---

## Project Structure

```
D:\AlFalah-Manage-System\
├── backend\                    ← ASP.NET Core Web API
│   ├── AlFalah.Api\            ← Controllers, middleware, Program.cs
│   ├── AlFalah.Application\    ← Services, DTOs, interfaces
│   ├── AlFalah.Domain\         ← Entities, enums, constants
│   ├── AlFalah.Infrastructure\ ← EF Core, Identity, seeders, JWT
│   └── AlFalah.Shared\         ← ApiResponse<T>, constants
└── frontend\                   ← Angular 17+ SPA
    └── src\
        ├── app\
        │   ├── core\           ← Auth, guards, interceptors, models
        │   ├── features\       ← Login pages, dashboards
        │   └── shared\         ← Reusable components
        └── assets\
            └── i18n\           ← Arabic (ar.json) + English (en.json)
```

---

## How to Run

### Backend

```bash
cd backend

# Apply migrations and seed (auto-runs on startup)
dotnet run --project AlFalah.Api

# Or manually:
dotnet ef database update --project AlFalah.Infrastructure --startup-project AlFalah.Api
dotnet run --project AlFalah.Api
```

- **API**: https://localhost:7100
- **Swagger**: https://localhost:7100/swagger

### Frontend

```bash
cd frontend
npm install
npm start
# or: npx ng serve
```

- **Frontend**: http://localhost:4200

---

## Development Credentials

> ⚠ These are DEVELOPMENT ONLY. Change immediately in production.

| Role | Username | Password |
|------|----------|----------|
| Super Admin | `superadmin` | `AlFalah@SuperAdmin2024!` |
| School Manager | `school_manager_1` | `AlFalah@Manager2024!` |
| Moderator | `moderator_1` | `AlFalah@Moderator2024!` |

**Sample School**: مدرسة الفلاح النموذجية — الرياض (Id: 1)

---

## Environment Variables

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server connection string |
| `Jwt__Secret` | JWT signing secret (min 32 chars) |
| `Jwt__Issuer` | JWT issuer |
| `Jwt__Audience` | JWT audience |
| `DEV_SUPER_ADMIN_PASSWORD` | Override SuperAdmin password in dev |

---

## Phase 1 — Implemented

- [x] Backend solution: 5 projects (Api, Application, Domain, Infrastructure, Shared)
- [x] All domain entities (ApplicationUser, School, UserSchoolRole, Permission, RolePermission, RefreshToken, AuditLog, etc.)
- [x] EF Core DbContext with Identity, configurations, indexes
- [x] Database seeder (roles, permissions, role-permission mapping, SuperAdmin, sample data)
- [x] JWT + Refresh Token auth
- [x] Auth endpoints: school-login, main-manager-login, refresh, logout, me, schools
- [x] Global exception middleware with ApiResponse<T>
- [x] Angular 17 project with RTL, Arabic-primary i18n
- [x] Auth service, JWT interceptor, auth guard, role guard
- [x] School login page (school selector + credentials)
- [x] Main Manager login page
- [x] Placeholder dashboard pages (all 4 roles)
- [x] EF Core migration: `InitialCreate`
- [x] Unauthorized error page

## Pending (Phase 2+)

- [x] Schools CRUD (Phase 2)
- [x] Users/Moderators CRUD (Phase 2)
- [x] Rubric with 5 domains + 25 standards (Phase 3)
- [x] Visits and scoring (Phase 4)
- [x] Approval workflow (Phase 5 — incl. D-36 close: backend instructor gate on `GetByIdAsync` + frontend wiring to `/report`)
- [x] PDF reports with signatures (Phase 6)
- [x] Improvement plans + follow-ups (Phase 7)
- [x] Complaints workflow (Phase 8)
- [x] Dashboards with analytics (Phase 9)
- [x] Excel/PDF exports (Phase 9)
- [x] Full shell layout with categorized sidebar (Phase 2 + D-73)

## Teacher Evidence Files / OneDrive

The instructor route is `/instructor/evidence-files`. It uses Microsoft Entra access tokens for this feature only; the existing local administrator login remains unchanged.

1. Create a single-tenant Entra app registration and add the SPA redirect URI (for local development, `http://localhost:4200`). Expose an API scope such as `api://<api-client-id>/access_as_user`.
2. Add delegated Microsoft Graph permission `Files.ReadWrite`, grant consent, and ensure each configured root folder is in the teacher's own OneDrive and accessible to that teacher. Do not add application permissions or `Files.ReadWrite.All` for this feature.
3. Configure the API with user secrets or environment variables: `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret` (or certificate settings), `AzureAd__Domain`, `AzureAd__Audience`, `AzureAd__ApiScope`, and optionally `MicrosoftGraph__Scopes__0=Files.ReadWrite`.
4. Inject public SPA values before Angular boots, for example in the deployment HTML: `window.__alfalahEntra={clientId:'...',tenantId:'...',apiScope:'api://.../access_as_user',redirectUri:'https://app.example'};`. Never add a client secret to this object or source control.
5. An administrator with `Instructor.Edit` configures the expected Microsoft email and the DriveId/RootItemId through `/api/v1/teacher-drive-admin/teachers/{teacherId}` endpoints. The teacher only sees the mapped folder, never these identifiers.

Apply the EF migration with `dotnet ef database update --project backend/AlFalah.Infrastructure --startup-project backend/AlFalah.Api`. The runtime also applies pending migrations on API startup in development.
