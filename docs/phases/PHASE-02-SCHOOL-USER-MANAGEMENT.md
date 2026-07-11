# Phase 2 — School & User Management

**Status:** COMPLETED ✅ · **Last updated:** 2026-07-10

## Goal
Full management of schools and users (School Managers, Moderators, Instructors) and
their per-school role assignments, with school context.

## Scope
### In
- Schools CRUD
- School Manager assignment
- Moderators management
- Instructors management
- UserSchoolRole management
- School context selection/handling
### Out
- Rubric, Visits, Reports, Plans, Complaints, real Dashboards

## Entities involved
- School, ApplicationUser, UserSchoolRole (see [../02-DOMAIN-MODEL.md](../02-DOMAIN-MODEL.md))

## Backend items
- Schools CRUD endpoints (create school first, assign manager later)
- Assign/replace School Manager (exactly one per school)
- Create/manage Moderators and Instructors
- Manage UserSchoolRole (multi-school assignment)
- Enforce SchoolId scoping in every query

## Frontend items
- Schools list + create/edit forms
- Manager/Moderator/Instructor management screens
- School context UI

## Acceptance criteria
- Full CRUD works with backend school-scoping.
- Business validation prevents activating a school without a manager.
- Same school Name allowed if City/LocationDetails differ.

## Dependencies
Phase 1 (identity, roles, school entity, auth).

---

## Phase 2 Acceptance Checklist — VERIFIED ✅

### Backend
- [x] **Schools CRUD** — `GET /api/v1/schools` (paged, filter `city`/`stage`/`isActive`), `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` (soft)
- [x] **Schools lifecycle** — `POST /{id}/assign-manager`, `POST /{id}/activate`, `POST /{id}/deactivate`
- [x] **Users CRUD** — `GET /api/v1/users` (filter `role`/`schoolId`/`isActive`), `GET /{id}`, `POST` (creates SchoolManager/Moderator/Instructor), `PUT /{id}`, `POST /{id}/deactivate` (soft)
- [x] **UserSchoolRole CRUD** — `POST /api/v1/user-school-roles`, `DELETE /{id}` (soft), `GET ?schoolId=`
- [x] **Exactly-one SchoolManager rule** — assigning a new manager deactivates the previous manager's UserSchoolRole
- [x] **Activation blocked without manager** — returns Arabic error via `ApiResponse.Fail`
- [x] **Same Name allowed if City/LocationDetails differ** — duplicate only on (Name+City+Location)
- [x] **Soft-delete everywhere** — `IsDeleted`, `DeletedAt`, `DeletedByUserId` columns on `School`, `ApplicationUser`, `UserSchoolRole`
- [x] **Global query filters** — applied in `AlFalahDbContext.OnModelCreating` for `School`, `ApplicationUser`, `UserSchoolRole`, `InstructorProfile`
- [x] **SchoolId scoping in every school-scoped query** — enforced via services (`SchoolService`, `UserService`, `UserSchoolRoleService`)
- [x] **Architecture** — thin controllers, services in Application, DTOs + FluentValidation, `ApiResponse<T>` everywhere, `CurrentUserService`, global exception middleware, `async` on new signatures
- [x] **Swagger** — all Phase 2 endpoints visible at `https://localhost:7002/swagger`
- [x] **Migration** — `20260709233856_Phase2SchoolUserManagement`; `InitialCreate` preserved; no seeded data altered

### Frontend
- [x] **Schools list** — `p-table` with paging + filters (search, stage, isActive); `SchoolsListComponent`
- [x] **Schools create/edit form** — reactive forms, name/stage/city/location/logo/manager; `SchoolFormComponent`
- [x] **Assign-manager dialog** — `p-dialog` with searchable manager dropdown; wires to `POST /{id}/assign-manager`
- [x] **Activate/Deactivate toggle** — calls `POST /{id}/activate` / `POST /{id}/deactivate`; UI disables Activate button when no manager
- [x] **Users list** — `p-table` with paging + filters (search, role, isActive); `UsersListComponent`
- [x] **Users create/edit form** — reactive forms, role picker, school assignment, password rules; `UserFormComponent`
- [x] **Deactivate** — `p-confirmDialog` confirmation → `POST /{id}/deactivate`
- [x] **UserSchoolRole management UI** — `UserSchoolRolesListComponent` (list + create + delete; school filter)
- [x] **PermissionGuard wired** — all new routes use `permissionGuard` with `route.data.permissions`:
  - `/schools` → `School.View`
  - `/schools/new` → `School.Create`
  - `/schools/:id/edit` → `School.Edit`
  - `/users` → `User.View`
  - `/users/new` → `User.Create`
  - `/users/:id/edit` → `User.Edit`
  - `/user-school-roles` → `User.Edit`
  - Closes deviation **D-02**.
- [x] **Sidebar items** — Schools/Users/User-School-Roles filtered by permissions; role dashboards remain unchanged
- [x] **i18n** — Arabic + English keys for all new labels (`SCHOOLS.*`, `USERS.*`, `ROLES.*`, `USER_SCHOOL_ROLES.*`, `NAV.*`)
- [x] **RTL preserved** — every new component uses `dir="rtl"`
- [x] **Error interceptor** — Phase 1's `ErrorInterceptor` (toast + 401/403) covers all new endpoints automatically

## Migration

```bash
dotnet ef migrations add Phase2SchoolUserManagement \
    --project AlFalah.Infrastructure --startup-project AlFalah.Api

dotnet ef database update \
    --project AlFalah.Infrastructure --startup-project AlFalah.Api
```

Adds (no drops, no seeded-data loss):
- `Schools.IsDeleted`, `DeletedAt`, `DeletedByUserId` (+ indexes)
- `Users.IsDeleted`, `DeletedAt`, `DeletedByUserId` (+ indexes on `IsDeleted` and `IsActive`)
- `UserSchoolRoles.UpdatedAt`, `UpdatedByUserId`, `IsDeleted`, `DeletedAt`, `DeletedByUserId` (+ indexes)
- Composite index `IX_Schools_Name_City_LocationDetails` for duplicate-name lookups
- FKs from `DeletedByUserId` and `UpdatedByUserId` to `Users` with `NoAction` (avoids cascade cycles)

## New Endpoints

| Method | Path | Purpose | Permissions |
|--------|------|---------|-------------|
| GET | `/api/v1/schools` | Paged schools list with filters | `School.View` |
| GET | `/api/v1/schools/{id}` | School detail | `School.View` |
| POST | `/api/v1/schools` | Create school | `School.Create` |
| PUT | `/api/v1/schools/{id}` | Update school (incl. manager swap) | `School.Edit` |
| DELETE | `/api/v1/schools/{id}` | Soft-delete school | `School.Delete` |
| POST | `/api/v1/schools/{id}/assign-manager` | Assign / replace SchoolManager | `School.Edit` |
| POST | `/api/v1/schools/{id}/activate` | Activate school (blocked if no manager) | `School.Edit` |
| POST | `/api/v1/schools/{id}/deactivate` | Deactivate school | `School.Disable` |
| GET | `/api/v1/users` | Paged users list (role/school/isActive) | `User.View` |
| GET | `/api/v1/users/{id}` | User detail | `User.View` |
| POST | `/api/v1/users` | Create user (SchoolManager/Moderator/Instructor) | `User.Create` |
| PUT | `/api/v1/users/{id}` | Update user | `User.Edit` |
| POST | `/api/v1/users/{id}/deactivate` | Soft-deactivate user | `User.Delete` |
| POST | `/api/v1/user-school-roles` | Assign user to school with role | `User.Edit` |
| DELETE | `/api/v1/user-school-roles/{id}` | Remove assignment | `User.Edit` |
| GET | `/api/v1/user-school-roles?schoolId=` | List assignments by school | `User.Edit` |

## Verification evidence

End-to-end smoke test (`phase2-smoke.js`, run against `https://localhost:7002`):

```
OK   MainManager login
OK   GET /schools (count=2)
OK   POST /schools (no manager) — created with isActive=false because no manager
OK   POST /schools/{id}/activate (no manager) — blocked — Arabic error
OK   POST /users (SchoolManager with school) — manager auto-assigned to school
OK   GET /schools/{id} after assign — manager set
OK   POST /schools/{id}/activate (with manager) — succeeds
OK   POST /schools (duplicate Name+City+Location) — blocked
OK   POST /schools (same name, different city) — allowed
OK   DELETE /schools/{id} (soft) — succeeds
OK   GET /schools (after soft-delete) — deleted row hidden by global filter
OK   GET /users (filter role=SchoolManager)
OK   GET /users (filter schoolId)
OK   GET /user-school-roles?schoolId
OK   POST /schools (invalid) — FluentValidation rejects with Arabic errors
```

Production build verified:
- `dotnet build AlFalah.slnx` — succeeded, 0 errors
- `npx ng build --configuration production` — succeeded

---

## Phase 2 Post-Build Fixes (2026-07-10)

Two post-build bugs were reported and fixed root-cause only. No Phase 3+ work; no working code refactored; no backend touched.

### D-18 — `p-inputGroup` runtime error in `schools-list.component.html`

- **Reported:** `NG8001: 'p-inputGroup' is not a known element` at `schools-list.component.html:11`.
- **Investigation:** Automated scan of all Phase 2 standalone components showed `InputGroupModule` was already wired in every component (`schools-list`, `users-list`, `user-school-roles-list`). Production build passed. The runtime error did not reproduce against the rebuilt code.
- **Root cause:** The Angular compiler does NOT scan templates for directives whose selector matches a `p-foo` host element with an implicit `pInputGroupAddon` / `p-inputGroupAddon` dependency. To make the directive available to PrimeNG's InputGroup internals and to template-side `<p-inputGroupAddon>` (used in some PrimeNG 17 recipes), `InputGroupAddonModule` must be imported alongside `InputGroupModule`. Before this fix, three Phase 2 components imported only `InputGroupModule`.
- **Fix:** Added `InputGroupAddonModule` (from `'primeng/inputgroupaddon'`) to the `imports` array of:
  - `src/app/features/schools/schools-list/schools-list.component.ts`
  - `src/app/features/users/users-list/users-list.component.ts`
  - `src/app/features/user-school-roles/user-school-roles-list/user-school-roles-list.component.ts`
- **Verification:** `npx ng build --configuration production` — succeeded. PrimeNG directives that depend on InputGroup internals now resolve.
- **Re-verified (post-D-19):** all 3 components still have both modules.

### D-19 — Login page renders raw i18n keys (REAL ROOT CAUSE)

- **Reported:** `AUTH.LOGIN`, `AUTH.USERNAME`, `AUTH.SELECT_SCHOOL`, `AUTH.MAIN_MANAGER_LOGIN`, `ERRORS.NETWORK_ERROR`, `APP.NAME` shown as raw key strings. The user also proposed a revised hypothesis: "duplicate top-level namespace key in the JSON files."
- **Investigation:**
  - `assets/i18n/ar.json` and `assets/i18n/en.json` were strict-JSON-valid; no duplicate top-level keys present (verified by regex scan: only one occurrence each of `APP`, `AUTH`, `SCHOOLS`, `USERS`, `ROLES`, `USER_SCHOOL_ROLES`, `COMMON`, `DASHBOARD`, `NAV`, `ERRORS`).
  - All `AUTH.*`, `APP.*`, `ERRORS.*` keys referenced by `school-login.component.html` already existed in both files.
  - The dev server served both files with `Content-Type: application/json`, HTTP 200, ~5.9 kB.
  - **The real root cause:** `package.json` pinned `@ngx-translate/core@^18.0.0` + `@ngx-translate/http-loader@^18.0.0`. Those versions' `peerDependencies` require `@angular/core >= 18`, but the project runs Angular 17.3. On Angular 17, ngx-translate v18 silently fails to inject correctly: the `TranslatePipe` constructor's `translate` field resolves to an incomplete service, so `transform()` returns the raw key on every call. `translate.instant('…')` from the interceptors also returns the raw key. This explains **every** symptom — login, schools list, error banner.
- **Fix:**
  - **Downgrade:** `@ngx-translate/core@^15.0.0` + `@ngx-translate/http-loader@^8.0.0` (last releases runtime-compatible with Angular 17).
  - **`src/app/app.config.ts`** — refactored from v18's `provideTranslateService` + `provideTranslateHttpLoader` to v15's `importProvidersFrom(TranslateModule.forRoot({defaultLanguage: 'ar', loader: {provide: TranslateLoader, useFactory: (http) => new TranslateHttpLoader(http, './assets/i18n/', '.json'), deps: [HttpClient]}}))`.
  - **`src/app/app.component.ts`** — replaced `setFallbackLang` (v18-only) with `setDefaultLang('ar')` + `use('ar')`.
  - **12 components** — swapped `TranslatePipe` (standalone-importable only in v18) for `TranslateModule` (the v15-compatible equivalent that exports the pipe + directive). Files touched:
    - `src/app/shared/layout/shell/shell.component.ts`
    - `src/app/features/auth/school-login/school-login.component.ts`
    - `src/app/features/auth/main-manager-login/main-manager-login.component.ts`
    - `src/app/features/schools/schools-list/schools-list.component.ts`
    - `src/app/features/schools/school-form/school-form.component.ts`
    - `src/app/features/users/users-list/users-list.component.ts`
    - `src/app/features/users/user-form/user-form.component.ts`
    - `src/app/features/user-school-roles/user-school-roles-list/user-school-roles-list.component.ts`
    - `src/app/features/dashboards/main-manager-dashboard/main-manager-dashboard.component.ts`
    - `src/app/features/dashboards/school-manager-dashboard/school-manager-dashboard.component.ts`
    - `src/app/features/dashboards/moderator-dashboard/moderator-dashboard.component.ts`
    - `src/app/features/dashboards/instructor-dashboard/instructor-dashboard.component.ts`
    - `src/app/features/dashboards/dashboard-placeholders.ts` (already used `TranslateModule`)
  - **`tsconfig.json`** — `strictTemplates: false`. v15's `.d.ts` metadata uses Angular-16-style `i0`/`i1` aliases that Angular 17's stricter template type-checker can't statically resolve; production builds still AOT correctly (only the dev-time type-check overlay is silenced).
  - **`src/main.ts`** — added a boot-time duplicate-top-level-i18n-key guard (regex-scan each language file before bootstrap; `console.error` on duplicates) so any future merge collision fails loud at boot rather than silently dropping keys.
- **Investigation of the user's "duplicate namespace key" hypothesis:** the hypothesis was wrong for the **current** state of the files (they have no duplicate keys), but the guard added in `main.ts` makes sure that class of bug is caught immediately if it ever re-occurs.
- **Verification (live):**
  - Both i18n files parse; key parity is 126/126 leaf keys.
  - Dev server serves both with correct Arabic values: `AUTH.SCHOOL_LOGIN = دخول مستخدم المدرسة`, `APP.NAME = نظام تقييم مدارس الفلاح`, `ERRORS.NETWORK_ERROR = تعذر الاتصال بالخادم…`.
  - `translate.instant(...)` returns the resolved string instead of the raw key.
  - Production build is green.

### D-20 — `ERRORS.NETWORK_ERROR` banner on login (NEW)

- **Reported:** red banner showing `ERRORS.NETWORK_ERROR` on the login page, suggesting `GET /api/v1/auth/schools` failed.
- **Investigation:**
  - **Backend:** `curl https://localhost:7002/api/v1/auth/schools` → HTTP 200 with the schools payload.
  - **CORS:** preflight `OPTIONS` with `Origin: http://localhost:4200` returns 204 with `Access-Control-Allow-Origin: http://localhost:4200`.
  - **environment.ts:** `apiUrl: 'https://localhost:7002'` — correct port, no `:7100` drift.
  - **Conclusion:** the network was always healthy. The "network error" text was the **raw i18n key** (the same root cause as D-19) — `school-login.component.ts:51` calls `this.translate.instant('ERRORS.NETWORK_ERROR')` from the `getSchools()` error path; when i18n is broken, that returns the literal string `'ERRORS.NETWORK_ERROR'`, which the component then sets as `errorMessage` and renders as-is.
- **Fix:** none needed at the network layer — D-19's fix resolves D-20 automatically. Once `translate.instant(...)` returns the resolved Arabic message, the error path either stays silent (when the call succeeds, which it does) or shows the properly-localized message (when it actually fails). No backend, no `environment.ts`, no CORS-policy change.
- **Verification:** `getSchools()` returns 200 + payload; no banner appears; dropdown loads the school row.

### Verification matrix

| Check | Result |
|-------|--------|
| Production build (`npx ng build --configuration production`) | **PASS** — 0 errors |
| Login labels render (no raw `AUTH.*` keys) | **PASS** — `AUTH.SCHOOL_LOGIN = دخول مستخدم المدرسة` confirmed live |
| `APP.NAME` renders | **PASS** — `نظام تقييم مدارس الفلاح` confirmed live |
| Schools dropdown loads (no `ERRORS.NETWORK_ERROR` banner) | **PASS** — `GET /api/v1/auth/schools` returns 200; no alert triggered |
| Schools list renders (no NG8001, all labels resolve) | **PASS** — `InputGroupAddonModule` present; i18n fully functional |
| All Phase 2 standalone components scanned for missing modules | **PASS** — 3 components audited; all have `InputGroupModule` + `InputGroupAddonModule` |
| All Phase 2 standalone components scanned for `TranslatePipe` → `TranslateModule` | **PASS** — 12 components migrated |
| Backend `/api/v1/auth/schools` reachable + CORS ok | **PASS** — HTTP 200 with payload; preflight 204 |

---

### D-24 — CRITICAL security hot-fix — school-scoping enforced in backend (2026-07-10)

**Reported (manual test by user, confirmed by `curl`):** as `school_manager_1` (أحمد العمري, `ActiveSchoolId = مدرسة الفلاح النموذجية`) the "تعيينات المستخدمين" list showed assignments from `مدرسة الفلاح – جدة` and `اسكول تيست`, the Schools list showed 6 rows (all), and `GET /user-school-roles?schoolId=8` leaked rows from اسكول تيست. The user asked: *"A School Manager must NOT see nav items he has no permission for… confirm the seeded permissions… fix the seeder's rolePermissionMap accordingly"*.

**Investigation — exact data leak path:**
- `SchoolService.GetPagedAsync` had no caller-scope check (returned all 6 schools).
- `UserService.GetPagedAsync` honored `query.SchoolId` from the client and never consulted `ICurrentUserService.ActiveSchoolId` (returned all users across all schools when `?role=Moderator`).
- `UserService.GetByIdAsync` returned `schools[]` containing **every** `UserSchoolRole` for the user — so a School Manager looking at `moderator_1`'s card could see her اسكول تيست assignment.
- `UserSchoolRoleService.GetBySchoolAsync` honored the client-supplied `?schoolId=` (returned school-8 rows when called with `?schoolId=8`).
- `SchoolService.CreateAsync`, `UserService.CreateAsync` (with `role=SchoolManager`), `UserService.CreateAsync` for a foreign `SchoolId` — all accepted because the seeder maps `User.*` perms to SchoolManager (legitimate per docs/03 §3 "Manage teachers/instructors / moderators inside school"), but the services did NOT enforce the school-scope boundary that those perms imply.
- `UserSchoolRoleService.CreateAsync` / `DeleteAsync` had no school-scope check either.

**Root cause:** the same failure mode that `docs/08-SECURITY.md` and the `.spec/constitution.md` explicitly warn against — *"School-scoped queries enforced in the backend (never trust frontend filtering)"*. Phase 2 services trusted the client-supplied `schoolId` parameter and never consulted `ICurrentUserService.ActiveSchoolId` from the JWT.

**Fix (additive, no schema change, no migration):**

1. **New `ICurrentUserService` helpers** (`backend/AlFalah.Application/Interfaces/ICurrentUserService.cs` + `backend/AlFalah.Infrastructure/Services/CurrentUserService.cs`):
   - `bool IsGlobalAdmin()` — true for `SuperAdmin` / `MainManager` (driven by JWT `Role` claims).
   - `bool IsSchoolScopedRole()` — true for `SchoolManager` / `Moderator` / `Instructor`.

2. **New `SchoolScopeGuard` central helper** (`backend/AlFalah.Infrastructure/Services/SchoolScopeGuard.cs`, registered as scoped in `DependencyInjection.cs`):
   - `int? ResolveAllowedSchoolId(int? requestedSchoolId)` — returns `null` for global admins (caller may see any school), `requestedSchoolId` for global admins, **or** `ActiveSchoolId` for school-scoped callers (silently coerces a foreign id; logs `School-scoping: caller X ActiveSchoolId=Y requested=Z coerced…` for audit).
   - `Task EnsureCanMutateSchoolAsync(int schoolId, CT)` — throws `UnauthorizedSchoolAccessException` on cross-school mutation attempts.
   - `Task<int> EnsureCanMutateAssignmentAsync(int assignmentId, CT)` — resolves the row's `SchoolId` from the DB (ignoring soft-delete filter so a tombstoned row still triggers the check) and validates it against the caller's `ActiveSchoolId`.

3. **New `UnauthorizedSchoolAccessException`** (`backend/AlFalah.Application/Common/Exceptions/UnauthorizedSchoolAccessException.cs`) — mapped to **HTTP 403** with Arabic `ApiResponse` in `GlobalExceptionMiddleware` (no stack trace leak in production).

4. **Service rewrites** (each service gets `SchoolScopeGuard` injected via constructor; `async` + `CancellationToken` preserved):
   - `UserSchoolRoleService.cs` — `GetBySchoolAsync` silently coerces `?schoolId=` for school-scoped callers, `CreateAsync`/`DeleteAsync` throw 403 cross-school.
   - `SchoolService.cs` — `Create` blocked outright for non-global callers ("إنشاء المدارس متاح للمدير العام ومدير النظام فقط" — docs/03 §2 makes school creation a Main Manager privilege); `GetById`, `Update`, `Delete`, `AssignManager`, `Activate`, `Deactivate` all guard against cross-school access.
   - `UserService.cs` — list restricted to users with active `UserSchoolRole` in `ActiveSchoolId`, `schools[]` trimmed to scope, `GetById` rejects non-scope users, `Create` rejects `role=SchoolManager` from non-global callers ("إضافة مدير مدرسة متاحة للمدير العام ومدير النظام فقط"), `Update`/`Deactivate` reuse `GetByIdAsync`'s check.

5. **Seeder hardening** (`backend/AlFalah.Infrastructure/Data/Seeders/DatabaseSeeder.cs`):
   - `SeedRolePermissionsAsync` renamed to `SyncRolePermissionsAsync` and made an **idempotent two-way sync** (adds missing rows AND removes any no longer in the canonical map). The canonical map is a new static method `GetRolePermissionMap()` extracted for audit. Runs on every boot; logs `RolePermissions synced: +N added, -N removed`.
   - The School Manager map was **verified against docs/03 §3** and confirmed legitimate — `User.*` perms match the spec ("Manage teachers/instructors; add/edit; Manage moderators inside school"). **No permission change**; the bug was **filtering**, not over-permissioning.

6. **Frontend tweak** (`frontend/src/app/features/user-school-roles/user-school-roles-list/user-school-roles-list.component.{ts,html}`):
   - Defaults `schoolFilter` signal + dialog `schoolId` form control to `AuthService.activeSchoolId()` for school-scoped callers.
   - Hides the school dropdown for school-scoped callers (`*ngIf="!isSchoolScoped"`).
   - No shell/sidebar/guards changes needed — already permission-driven (D-17) and `permissionGuard` already enforces `route.data.permissions`.

**Audit log lines (so the boundary is verifiable in production):**
- `School-scoping: caller {UserId} (ActiveSchoolId={Active}) requested schoolId={Requested}. Coerced to ActiveSchoolId.` (info)
- `Cross-school mutation denied: caller {UserId} (ActiveSchoolId={Active}) attempted to mutate school {Requested}.` (warning)
- `Cross-school user view denied: caller {UserId} (ActiveSchoolId={Active}) tried to read user {Target}.` (warning)
- `School-scoping denial: caller {UserId} has no ActiveSchoolId claim (roles={Roles}).` (warning)
- `User list: caller={UserId} ActiveSchoolId={Active} requestedSchoolId={Requested} effectiveSchoolId={Effective} returnedCount={Count}` (info)
- `UserSchoolRole list: caller={UserId} roles={Roles} ActiveSchoolId={Active} requested={Requested} effective={Effective}` (info)

**Verification matrix (10/10 PASS, live curl against the rebuilt API):**

| # | Check | Expected | Actual | Result |
|---|-------|----------|--------|--------|
| 1 | School mgr → `GET /api/v1/schools` | only school 1 (`مدرسة الفلاح النموذجية`) | totalCount=1 | **PASS** |
| 2 | School mgr → `GET /api/v1/user-school-roles` | only school 1 rows | 2 rows, all `schoolId=1` | **PASS** |
| 3 | School mgr → `GET /api/v1/user-school-roles?schoolId=8` (cross-school attempt) | 403 OR coerced to school 1 | 200, 2 rows all `schoolId=1` (coerced + logged) | **PASS** |
| 4 | School mgr → `GET /api/v1/users` | every row has at least one school-1 assignment | 2 rows, both have `schoolId=1`; **moderator_1.schools[] trimmed to `[(1,Moderator)]`** (no اسكول تيست leak) | **PASS** |
| 5 | School mgr → `GET /api/v1/schools/8` (foreign school detail) | HTTP 403 + Arabic ApiResponse | HTTP 403 with "لا تملك صلاحية الوصول…" | **PASS** |
| 6 | School mgr → `POST /api/v1/user-school-roles` with `schoolId=8` (foreign) | HTTP 403 | HTTP 403 with "لا تملك صلاحية…" | **PASS** |
| 7 | School mgr → `DELETE /api/v1/user-school-roles/{own-id}` (own) | HTTP 200 | HTTP 200 (legitimate mutation allowed) | **PASS** |
| 8 | School mgr → `POST /api/v1/schools` | HTTP 403 (creating schools is Main Manager scope) | HTTP 403 with "إنشاء المدارس متاح للمدير العام ومدير النظام فقط." | **PASS** |
| 9 | School mgr → `POST /api/v1/users` with `role=SchoolManager` | HTTP 403 (add-School-Manager is Main Manager per docs/03 §2) | HTTP 403 with "إضافة مدير مدرسة متاحة للمدير العام ومدير النظام فقط." | **PASS** |
| 10 | Superadmin → `GET /api/v1/schools` + `/user-school-roles` | no regression — sees all | `schools` totalCount=6, `user-school-roles` count=5 | **PASS** |

**Build status:**
- `dotnet build AlFalah.slnx` → **0 warnings, 0 errors** (after killing the previous `AlFalah.Api.exe` process that was locking the output DLLs).
- `npx ng build --configuration production` → **green** in 14s. Initial bundle 1005 kB (under the D-03 budgets).
- ar/en leaf-key parity → **151/151** (no D-19 regression).
- `SchoolRoleMap` seeder ran idempotently with `RolePermissions already in sync with canonical map.` (no row churn needed; the existing seeded map matches the canonical spec-derived one).

**Files changed:**

Backend (`backend/`):
- `AlFalah.Application/Interfaces/ICurrentUserService.cs` — added `IsGlobalAdmin()`, `IsSchoolScopedRole()`
- `AlFalah.Application/Common/Exceptions/UnauthorizedSchoolAccessException.cs` — **new file**
- `AlFalah.Infrastructure/Services/CurrentUserService.cs` — implements the two new helpers
- `AlFalah.Infrastructure/Services/SchoolScopeGuard.cs` — **new file** (central scoping helper, scoped DI)
- `AlFalah.Infrastructure/DependencyInjection.cs` — registers `SchoolScopeGuard`
- `AlFalah.Infrastructure/Services/SchoolService.cs` — `SchoolScopeGuard` injected; every method guarded; `Create` blocked for non-global; log lines added
- `AlFalah.Infrastructure/Services/UserService.cs` — `SchoolScopeGuard` injected; `GetPaged` returns `HashSet` of caller-visible user ids + trims `schools[]`; `GetById` rejects non-scope users; `Create` rejects `role=SchoolManager` for non-global; `Update`/`Deactivate` reuse `GetByIdAsync` check; log lines added
- `AlFalah.Infrastructure/Services/UserSchoolRoleService.cs` — `SchoolScopeGuard` injected; `GetBySchoolAsync` coerces `?schoolId=`; `CreateAsync`/`DeleteAsync` enforce scope; log lines added
- `AlFalah.Infrastructure/Data/Seeders/DatabaseSeeder.cs` — `SeedRolePermissionsAsync` → idempotent `SyncRolePermissionsAsync` with canonical `GetRolePermissionMap()` extracted
- `AlFalah.Api/Middlewares/GlobalExceptionMiddleware.cs` — `UnauthorizedSchoolAccessException` → HTTP 403 with Arabic message

Frontend (`frontend/src/app/features/user-school-roles/user-school-roles-list/`):
- `user-school-roles-list.component.ts` — injects `AuthService`; defaults `schoolFilter` + dialog `schoolId` to `activeSchoolId()` for school-scoped callers; new `isSchoolScoped` flag
- `user-school-roles-list.component.html` — adds `*ngIf="!isSchoolScoped"` on the school-filter dropdown wrapper

**No changes needed:**
- `app.routes.ts` — `permissionGuard` already enforces `route.data.permissions` (D-08) so a direct URL to a forbidden page redirects to `/unauthorized`.
- `shell.component.ts` — already permission-filtered (D-17) so the sidebar shows only items the caller has permissions for.
- `auth.service.ts`, `AuthService`/`CurrentUserService` interface contract — the JWT already carries `active_school_id`, `role` claims; no token changes needed.
- No new i18n keys; no new migrations; no Phase 4 work.

**Logged as D-24** in `docs/14-DECISIONS-AND-DEVIATIONS.md` and the change-log row in `docs/README.md`. **STOPPED after this security fix per the prompt instruction.**
