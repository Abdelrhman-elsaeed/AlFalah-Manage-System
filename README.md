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

## Teacher Evidence Files / Google Drive

Teachers upload evidence documents into a folder on the **school's** Google Drive; every
successful upload is recorded in the evidence ledger, which ticks the matching cell in the
evidence matrix (`/*/evidence-matrix`). The instructor route is `/instructor/evidence-files`.

**Teachers do not need a Google account and never sign in twice.** The school supplies one
credential, the application acts as that credential for all Drive traffic, and it enforces
per-teacher folder isolation itself. Authorship is recorded by the application, not by Drive:
the `TeacherEvidenceSubmission` row plus the audit-log entry are what attribute a file to a
teacher, because Drive shows the school credential as the uploader.

### 1. Create the Drive credential

Pick whichever fits the school's Google setup — both end up as a short-lived OAuth token:

| | Service account (recommended) | School Google account |
|---|---|---|
| Needs Google Workspace | Only for domain-wide delegation | No (works with a free account) |
| Setup | Google Cloud → enable the **Google Drive API** → create a service account → create a JSON key | Google Cloud OAuth client → obtain a refresh token once for the school's account |
| Storage | A **shared drive** the service account is a member of, *or* domain-wide delegation impersonating a Workspace user | The account's own My Drive (15 GB free) |

`SharedDriveId` and the impersonation email are both **optional** — a service account can also
be pointed at an ordinary My Drive folder that has been shared with it.

Be aware of one Google constraint when you do that: a service account owns **no storage quota**,
so while it can browse and download from such a folder perfectly well, a file it *creates* there
is refused with `storageQuotaExceeded`. If uploads matter, give it a shared drive or an
impersonated Workspace user (or use the school-Google-account option instead). The settings
screen warns about this combination but does not block it, and the API surfaces the quota reason
verbatim rather than disguising it as a permission error.

### 2. Create the folder tree

```
ملفات الإنجاز/          ← the school-wide evidence root (its id goes in settings)
├── المعلم أ/           ← granted to teacher A
└── المعلم ب/           ← granted to teacher B
```

Take a folder id from its URL: `drive.google.com/drive/folders/<folder-id>`.

### 3. Connect the school (School Manager → `/school-manager/evidence-settings`)

Paste the credential and the root folder id. The credential is encrypted at rest with
ASP.NET Core Data Protection and is **never** returned by any API — the settings response only
reports `hasStoredCredential`. Leaving a secret field blank on a later save keeps the stored
value, so the root folder can be renamed without re-pasting the key.

No API configuration is required for a normal deployment. `GoogleDrive:ApiBaseUrl`,
`GoogleDrive:UploadBaseUrl` and `GoogleDrive:TokenEndpoint` exist only to point the client at a
test double and default to Google's real endpoints.

### 4. Grant each teacher a folder

An administrator with `Instructor.Edit` calls
`PUT /api/v1/teacher-drive-admin/teachers/{teacherId}/folder` with the teacher's folder id
(`DELETE` withdraws it; already-uploaded evidence stays in the matrix). A grant is validated
against Google before it is stored — the folder must exist, be a folder, sit **inside** the
school root, and not already belong to another teacher. The school root itself cannot be
granted, since it contains every teacher's folder.

Teacher clients never receive `DriveId` or `RootItemId`.

### Why file links go through the API

Files belong to the school's Google account, and neither teachers nor reviewing managers hold
a Google session — following Drive's own `webViewLink` would only ever show Google's "Request
access" page. Both surfaces therefore stream bytes through the API
(`GET /api/v1/teacher-drive/items/{itemId}/content` for a teacher, restricted to their granted
folder, and `GET /api/v1/evidence-matrix/submissions/{submissionId}/content` for a supervisor,
restricted to their school).

Apply the EF migration with `dotnet ef database update --project backend/AlFalah.Infrastructure --startup-project backend/AlFalah.Api`. The runtime also applies pending migrations on API startup in development. The Google Drive migration drops the OneDrive tables and deactivates any surviving folder grant, because a OneDrive item id resolves to nothing on Google — grants must be re-issued deliberately.
