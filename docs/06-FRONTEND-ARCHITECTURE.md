# 06 — Frontend Architecture

**Status:** Implemented · **Last updated:** 2026-07-15

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

## Phase 9 dashboard implementation (2026-07-15)

- The four former placeholders are live standalone components backed by the
  scoped dashboard API endpoints.
- A shared `DashboardLiveComponent` renders role-specific KPI cards,
  PrimeNG doughnut/bar/line charts, tables/insights, refresh, and Excel/PDF
  export actions while each route remains a distinct lazy-loaded component.
- Main Manager has no complaint widget; Moderator has no complaint surface;
  Instructor data is own + Approved. These are server contracts, not UI-only
  filters.

## D-73 categorized sidebar (2026-07-15)

- Top: **الرئيسية** (one role-resolved dashboard only).
- **التقييم:** الزيارات، أداة التقييم.
- **الأشخاص:** المعلمون، المستخدمون، تعيينات المستخدمين.
- **الإدارة:** المدارس، الشكاوى.
- **الإعدادات:** إعدادات الحساب.
- Every item is role/permission-filtered; an empty category is not rendered.
  The active route's category is automatically expanded.
- Instructor-only navigation is the documented exception: exactly الرئيسية،
  تقاريري، إعدادات الحساب, with no category headers or supervisor/complaint
  items.
- `/teachers` and `/teachers/:userId` are lazy standalone routes protected by
  `roleGuard` (SchoolManager/MainManager/SuperAdmin) and `User.View`.

## Unified controls and localization (2026-07-15)

- Feature pages use `app-clearable-select` instead of consuming PrimeNG
  dropdowns or native selects directly. Optional values and filters are
  clearable; required values explicitly disable clearing. The wrapper supports
  `inputId` and Angular disabled-state propagation and is full-width by default.
- Date fields use PrimeNG `p-calendar`; there are no native `type="date"`
  controls in application templates.
- User-facing copy is sourced from Arabic/English i18n resources. The completed
  whole-app pass has 623/623 leaf-key parity, no missing literal translation
  keys, and no duplicate top-level keys (D-19).
- Improvement-plan and follow-up pages consume the shared Saudi design tokens,
  PrimeNG buttons/tags, the unified select, and calendar controls while keeping
  RTL layout and the dynamic rubric behavior (D-65).
