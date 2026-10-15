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
- Browser route titles resolve matched `ROUTE_TITLES.*` ar/en keys instead of
  embedding Arabic copy in `app.routes.ts`.

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
  `roleGuard` (SchoolManager/Moderator/MainManager/SuperAdmin) and the narrow
  `Instructor.View` permission. Moderator sees the `المعلمون` navigation item,
  but no `/users` item or teacher edit/deactivate controls.
- The teacher profile combines identity/contact/subject/stage/classes, the
  caller's in-scope visit history, an Approved-visit PrimeNG radar on dynamic
  active-rubric axes, and a first-versus-latest delta table using `▲/▼/─` with
  two-decimal scores. `زيارة جديدة` carries and locks the teacher selection.

## Unified controls and localization (2026-07-15)

- Feature pages use `app-clearable-select` instead of consuming PrimeNG
  dropdowns or native selects directly. Optional values and filters are
  clearable; required values explicitly disable clearing. The wrapper supports
  `inputId` and Angular disabled-state propagation and is full-width by default.
- Date fields use PrimeNG `p-calendar`; there are no native `type="date"`
  controls in application templates.
- Static desktop-parity closure scans also report zero direct feature
  `p-dropdown`/`p-select`, native `<select>`, and `p-button-info` usages.
- User-facing copy is sourced from Arabic/English i18n resources. The completed
  whole-app pass has 623/623 leaf-key parity, no missing literal translation
  keys, and no duplicate top-level keys (D-19).
- Improvement-plan and follow-up pages consume the shared Saudi design tokens,
  PrimeNG buttons/tags, the unified select, and calendar controls while keeping
  RTL layout and the dynamic rubric behavior (D-65).

## School File Storage S2 UI (2026-10-03)

Lazy standalone storage feature under shell: `/school-manager/storage` for actual manager/direct delegate, `/instructor/my-files` for Instructor own files. The school-library route uses a live storage API guard before the manager-only branch so a valid delegate needs no copied manager role. Shell navigation probes the live scoped context; flags OFF hides new links. Backend checks remain authoritative.

Shared RTL/PrimeNG page includes school/year context, lazy paginated folder tree/breadcrumbs, SQL search/global search/sort/pagination, list/cards, drop/select upload/progress/cancel-before-send, durable retry key in sessionStorage, explicit reconciliation, detail/version/protection state, authorized blob preview/download and internal links. Connection unavailable/no folder/uninitialized/loading/empty/error states are explicit. Read-only users do not receive folder mutation controls; 403 clears loaded data/preview. Previews cap at20 MiB and revoke object URLs; Office/unsupported/large files offer download fallback. No static prototype names/counts or Windows paths.

Angular API/component/shell tests and desktop/mobile RTL Playwright contracts are in [S2 verification](specs/school-file-storage/verification/s2-library-and-uploads.md). Browser fixtures mock API and start Angular only, preserving the actual database/credentials. S3 review/link/change UI has not started.

## School File Storage S4 RTL workspace (2026-10-04)

Lazy routes `/school-manager/storage/readiness`, `/standards/:code`, `/gaps`, `/tracker`, `/manual`, `/reports` and `/digital-index` use the live storage guard, so an explicitly delegated non-manager can use the workspace without a role-only shortcut. School-level reads still require current server authorization. The readiness API owns percentages; the page renders 4 domain comparison bars, 11 standard links, independent counts, critical gaps and no-requirements states. Tracker views group the current server page by responsible member/domain or show table/cards, with URL filters and stable server pagination. Required year/version selectors appear after context initialization to avoid selecting a zero-year scope.

Unified clearable-select, PrimeNG calendar/dialog/paginator and matched Arabic/English strings preserve platform controls/RTL. Follow-up/manual mutations carry rowversion and required reasons; 409 reloads current data, 401/403 clears rows/files/metrics/judgments/dialogs. No completed checkbox or localStorage decision exists. Optional S3 catalog rows are distinct from mandatory template items. Actual member names and file metadata come from scoped APIs.

Requirement completion and preview reuse S2/S3 library/evidence routes, preserving the selected year and requirement ID. The digital index opens authenticated previews/downloads, including existing image/video handling and unsupported-format fallback. No stock media, reference-path downloads or fabricated counters. CSV/Excel/PDF use the same filter object, including gapsOnly for the gaps route; the comprehensive report includes the file index. [S4 browser and actual export evidence](specs/school-file-storage/verification/s4-self-evaluation-and-reports.md).

## C30 / D-122 — One school-file presenter (2026-10-15)

School files always renders `StorageDriveBrowserComponent` or its visual map. `folder`/`file`/year/search query parameters no longer select a second `StoragePageComponent` browser; numeric links resolve through the authenticated Application service. Native preview hosts `StorageFileInspectorComponent`, reusing the existing library/evidence/version/change APIs and components. Evidence review and teacher-private workflows retain their separate scoped purpose.

`browserTransferInterceptor` precedes auth interception, negotiates binary application transfer only for this API's Blob requests, restores response Blob/filename metadata after receipt, and never turns a network error into success. Available map cards are semantic full-card links; collapse controls remain separate. The administrator's personal bookmark is guarded outside the Instructor role subtree and redirects using live school-management state. [Evidence](specs/school-file-storage/verification/download-access.md).

## C31 / D-123 ? File preview and destination-safe write controls (2026-10-15)

Owner follow-up delivered: full-width PDF preview, compact metadata, document-only scrolling, Fit width and Expand/Restore. Dedicated layout classes isolate the native viewer from legacy global styles. Protected archives show a library shortcut instead of disabled write tools; current library writes remain on existing services. A real verification exposed and corrected writes during destination loading; late file selections and retries are guarded.27 focused Angular checks, Development build and actual/synthetic390/768/1440 browser checks passed. Real manager create/upload/read-back/cleanup passed in the test library; the first race-reproduction objects were also cleaned. No original files, grants, archive settings or schema69 changes. [Evidence](specs/school-file-storage/verification/preview-layout.md). Owner/physical/installed IDM acceptance remains pending.

## C32 / D-124 ? Classroom identity and assignment design (2026-10-15)

Design/prototype only, explicitly selected by the owner. Existing structured Classroom/subject-allocation models are reused in the proposal; legacy InstructorClass text identity, teacher self/manager editors, both visit frontends, student/classroom scope, timetable import/generation/review/history and exports are covered in a semantic impact map. The owner confirms direct teacher association self-save without approval, stage-local grade ordinals and numeric sections. An isolated theme-aligned prototype demonstrates the manager catalogue, teacher multi-selection, assignment matrix/co/split/load and source-structured statement. No production code/schema/permissions/provider change or feature cutover. [Design, decisions and evidence](specs/classroom-catalog/visual-direction.md). Implementation and physical/printer acceptance remain future phases.


## C33 / D-125 - Classroom catalogue foundation (2026-10-15)

C33 / D-125 upgrades the existing /student-affairs/classrooms component into the manager catalogue: grade/stage navigation, bulk numeric sections, generated names, location, retained legacy labels, edit/activation, search and paging. A shared CVA grouped multi-select replaces chips in account teaching and manager teacher-profile editors; every options page is read and failed/stale lookups cannot clear selection or enable Save. No duplicate classroom frontend was added. Assignment UX/PDF remain C4/C5. [Implementation](specs/classroom-catalog/implementation.md).
