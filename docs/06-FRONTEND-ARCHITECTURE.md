# 06 — Frontend Architecture

**Status:** Baseline · **Last updated:** 2026-07-10

## Angular structure
```
src/app/core
├── auth
├── guards
├── interceptors
├── services
├── layout
└── localization

src/app/shared
├── components
├── pipes
├── validators
└── directives

src/app/features
├── auth
├── main-manager
├── school-manager
├── moderator
├── instructor
├── schools
├── users
├── roles
├── settings
└── dashboard
```

## Required libraries
- Angular **standalone components** (Angular 17+)
- **PrimeNG**
- **PrimeIcons**
- **PrimeFlex** (or equivalent layout helper)
- **@ngx-translate**
- **RTL** support
- **Arabic default**, English support

## Required Phase 1 pages
- **School user login page:** School dropdown, Username, Password, Login button.
- **Main Manager login page:** Username, Password, Login button.
- **Basic role-based layout:** Sidebar, Header, user info, active-school info (if school context exists).
- **Placeholder pages:** Main Manager dashboard, School Manager dashboard,
  Moderator dashboard, Instructor dashboard, Super Admin.

## Required Phase 2 pages
- **Schools list** (p-table: paging + filters by city/stage/isActive)
- **School create/edit form** (reactive form, manager assignment)
- **Assign-manager dialog** (searchable manager picker)
- **Activate / Deactivate** (blocked without manager, with toast feedback)
- **Users list + create/edit** (filter by role/school/isActive)
- **UserSchoolRole management** (list/create/remove assignments, filter by school)
- All Phase 2 routes are gated by `permissionGuard` reading `route.data.permissions`.

## Required services
- AuthService
- TokenStorageService
- SchoolService
- CurrentUserService
- TranslationService (or setup)
- Error handling service / toast

## Required guards
- AuthGuard
- RoleGuard
- PermissionGuard

## Required interceptors
- Auth token interceptor
- Error interceptor

## Behavior
- Role-based redirect after login for all 5 roles.
- Keep Arabic UI labels in i18n; technical names in English.
