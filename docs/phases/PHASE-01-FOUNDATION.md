# Phase 1 — Foundation

**Status:** COMPLETED ✅ (gap-fix DONE; Development data baseline verified) · **Last updated:** 2026-07-15

## Goal
Establish the full backend + frontend skeleton, identity, JWT + refresh tokens,
roles/permissions, school model, UserSchoolRole model, login flows, Angular shell,
RTL/i18n, basic guards/interceptors, and basic seed data.

## Scope
### In
- Backend solution skeleton (5 projects)
- Frontend Angular skeleton
- Identity setup
- JWT + refresh tokens
- Roles and permissions
- School model + UserSchoolRole model
- Login flows (school + main manager)
- Angular shell, RTL/i18n setup
- Basic guards/interceptors, basic seed data

### Out (NOT implemented in Phase 1)
- Visits
- Rubric full workflow
- Reports
- Improvement Plans
- Follow-ups
- Complaints
- Dashboards beyond placeholders

## Backend deliverables
- Solution structure: AlFalah.Api, AlFalah.Application, AlFalah.Domain, AlFalah.Infrastructure, AlFalah.Shared
- SQL Server LocalDB for dev
- EF Core Code First
- ASP.NET Core Identity
- JWT + refresh token setup
- Entities: ApplicationUser, ApplicationRole, Permission, RolePermission, UserSchoolRole, School, SchoolReportSettings, UserSignature, RefreshToken, AuditLog
- DbContext + EF configurations
- Seed data
- Auth services, JWT service, CurrentUserService
- Global exception middleware, ApiResponse wrapper
- AuthController, basic school lookup endpoint for login

## Frontend deliverables
- Angular app, PrimeNG, PrimeIcons, PrimeFlex (or equivalent)
- @ngx-translate, Arabic and English setup, RTL support
- School login page, Main Manager login page
- Basic shell layout, sidebar, header
- Auth interceptor, error interceptor (or unified error handling)
- Auth guard, role guard, permission guard
- Role-based redirect after login
- Placeholder pages for each role

## Entities involved
See [../02-DOMAIN-MODEL.md](../02-DOMAIN-MODEL.md).

---

## Phase 1 Acceptance Checklist (verbatim)

### Backend
- [x] Solution builds.
- [x] Projects are correctly separated.
- [x] SQL Server LocalDB works.
- [x] EF migration exists.
- [x] Identity is configured.
- [x] JWT login works.
- [x] Refresh token works.
- [x] School login validates UserSchoolRole.
- [x] Main Manager login works separately.
- [x] Token includes ActiveSchoolId for school users.
- [x] Token has roles and permissions.
- [x] ApiResponse is used.
- [x] Global exception middleware exists.
- [x] Seed data exists.
- [x] No business logic in controllers.

### Frontend
- [x] Angular app runs.
- [x] PrimeNG installed.
- [x] Arabic RTL works.
- [x] @ngx-translate works.
- [x] School login page exists.
- [x] Main Manager login page exists.
- [x] Auth interceptor exists.
- [x] Auth guard exists.
- [x] Role/permission guards exist.
- [x] Role-based redirect works.
- [x] Shell layout exists.
- [x] Placeholder pages exist for roles.

### Security
- [x] No hardcoded real credentials.
- [x] Passwords hashed.
- [x] School context enforced in backend.
- [x] Inactive users cannot login.
- [x] User not assigned to selected school cannot login.

---

## Implemented (per README)
- Backend solution: 5 projects (Api, Application, Domain, Infrastructure, Shared)
- All domain entities (ApplicationUser, School, UserSchoolRole, Permission, RolePermission, RefreshToken, AuditLog, etc.)
- EF Core DbContext with Identity, configurations, indexes
- Database seeder (roles, permissions, role-permission mapping, SuperAdmin, sample data)
- JWT + Refresh Token auth
- Auth endpoints: school-login, main-manager-login, refresh, logout, me, schools
- Global exception middleware with ApiResponse
- Angular 17 project with RTL, Arabic-primary i18n
- Auth service, JWT interceptor, auth guard, role guard
- School login page (school selector + credentials)
- Main Manager login page
- Placeholder dashboard pages (all 4 roles)
- EF Core migration: InitialCreate
- Unauthorized error page

## Gap-fix — COMPLETED (2026-07-10)
- [x] PrimeNG + PrimeIcons + PrimeFlex installed and wired (primeflex@^3.3.1; theme + primeng + primeicons + primeflex in `angular.json` styles[]; CDN duplicate removed from `index.html`)
- [x] PermissionGuard implemented (`src/app/core/guards/permission.guard.ts`; reads `route.data.permissions`; redirects to `/unauthorized`) — created & exported, not yet wired to a route (no permission-gated pages exist yet)
- [x] Error interceptor implemented (`src/app/core/interceptors/error.interceptor.ts`; 401→login, 403→`/unauthorized`, 4xx/5xx→PrimeNG toast; auth endpoints silenced) + `ToastService` + global `<p-toast>`
- [x] Basic shell layout implemented (`ShellComponent`: header with user/role/school + logout + RTL sidebar; wraps all role dashboards via `app.routes.ts`)
- [x] forgot-password / reset-password endpoints implemented (Identity token flow; email is a dev placeholder returning token in Development only)
- [x] JWT claims verified complete (`sub`, `unique_name`, `role`, 31 permission claims, `active_school_id`, `preferred_language`) + refresh-token rotation with `ReplacedByToken` (old token `IsRevoked=True`, reuse → 401)
- [x] `School.IsActive` + `Role.IsActive` checks added to school-login & refresh; inactive user/role/school cannot login

## Verification evidence
- Swagger lists all **8** endpoints at `http://localhost:5264/swagger`.
- Deactivating school #1 → login rejected.
- Reuse of an old (rotated) refresh token → **401**.

## Dev credentials (DEV ONLY — see README)
| Role | Username |
|------|----------|
| Super Admin | superadmin |
| School Manager | school_manager_1 |
| Moderator | moderator_1 |
> Sample School: مدرسة الفلاح النموذجية — الرياض (Id: 1). Change all credentials in production.

### Development data baseline verification (2026-07-15)

The completed Foundation phase was re-verified against `AlFalahDb` without
starting a new phase. EF migrations were already fully applied. The idempotent
Development seeder now ensures all five documented roles have working accounts:
SuperAdmin, MainManager, SchoolManager, Moderator, and Instructor. The three
school-scoped accounts have active `UserSchoolRole` rows for sample school ID 1;
the Instructor also has an `InstructorProfile`. Passwords remain Identity-hashed.
No existing visits or manually-created data were restored or deleted.

## Dependencies
None (foundation).
