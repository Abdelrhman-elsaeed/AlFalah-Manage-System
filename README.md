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
