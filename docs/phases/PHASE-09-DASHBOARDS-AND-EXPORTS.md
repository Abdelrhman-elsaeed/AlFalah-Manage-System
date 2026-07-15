# Phase 9 — Dashboards & Exports

**Status:** COMPLETED ✅ · **Last updated:** 2026-07-15

## Goal
Role-based dashboards with analytics and Excel/PDF exports.

## Scope
### In
- Main Manager dashboard
- School Manager dashboard
- Moderator dashboard
- Instructor dashboard
- Excel/PDF exports
- Filters
### Out
- Hardening (Phase 10)

## Filters
- Academic year, Semester, School, Subject, Stage, Moderator.

## Dashboard contents
Per-role dashboard contents are defined in
[../03-ROLES-AND-PERMISSIONS.md](../03-ROLES-AND-PERMISSIONS.md).
> Remember: Main Manager dashboard must not expose complaint details.

## Acceptance criteria
- Each role dashboard renders its defined metrics. ✅
- Exports (Excel/PDF) work through the same role-scoped service path. ✅

## Dependencies
Phases 4–8 (data to aggregate).

## Completion evidence — 2026-07-15

- `main-manager-dashboard` calls `GET /api/v1/dashboard/main-manager` and
  renders global counts, visit status, school comparison, plan analytics, and
  Excel/PDF controls. It has no complaint field or widget.
- `school-manager-dashboard` calls `GET /api/v1/dashboard/school-manager` and
  renders only the caller's `ActiveSchoolId` data, including the permitted
  school complaint count.
- `moderator-dashboard` calls `GET /api/v1/dashboard/moderator`; the service
  fixes its visit query to `CreatedByUserId == currentUserId` (D-37). Its DTO,
  Excel workbook, and PDF contain no complaint data at all (D-75).
- `instructor-dashboard` calls `GET /api/v1/dashboard/instructor` and renders
  only the caller's approved evaluations, trend, strengths, improvement
  points, plans, follow-ups, and report-view metrics (D-36). Its school is
  resolved through `SchoolScopeGuard`; a client-supplied cross-school filter
  is coerced to the token's `ActiveSchoolId` before querying.
- All four pages share a standalone Angular 17 component with PrimeNG
  `p-chart` (doughnut plus role-appropriate bar/line chart), responsive RTL
  cards/tables, Saudi design tokens, refresh, and scoped Excel/PDF buttons.
- The Main Manager school comparison includes a zoomable/pannable Saudi map.
  Marker coordinates come from each school's managed `SchoolLocation`, school
  names stay visible on their markers, and dashboard data refreshes every 60 seconds.
- The obsolete `dashboard-placeholders.ts` file was deleted.
- No migration or rubric/scoring change. D-24/D-28/D-36/D-37/D-53/D-65/D-75
  gates remain in place.
- Desktop-parity closure verification: backend Release build 0 warnings/errors;
  97/97 tests pass (including EF-backed Instructor school coercion and
  complaint-free restricted contracts); frontend production build green;
  ar/en 683/683 leaf-key parity; zero duplicate top-level i18n keys.
