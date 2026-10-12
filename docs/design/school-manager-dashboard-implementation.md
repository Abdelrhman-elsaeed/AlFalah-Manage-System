# School manager dashboard implementation

Design reference: [Al-Falah School Manager Dashboard, Figma Make](https://www.figma.com/make/VaaEkitKutAededfugbS41/Al-Falah-School-Manager-Dashboard).

The implementation uses the supplied screenshots and the exported Make source. The feather illustration is the original `PulseArt.tsx` artwork, exported to a local SVG; the downloaded React project is unchanged. Angular, PrimeNG, Chart.js, Tajawal and the existing green/gold design tokens remain the application stack.

## User-approved layout changes

- Compact green banner with the original feather artwork and navigation to dashboard sections.
- Preserve the existing ten tool cards and their routes; use category colors, clear boundaries and hover/focus feedback instead of white cards with accent strips. Four oversight/audit tools remain available through an expandable section.
- Student and staff attendance appear in two equal columns, using matching summaries, recording completeness, tables and historical charts.
- Recent activity, guardian summons and the school day appear in three equal columns in one row.
- Recent classroom visits remain available below the overview. The manager's visits-status chart, subject-performance chart, plan analytics and moderator ranking have been removed following the revised request. Smaller viewports stack the new panels.

## Data and access

`GET /api/v1/dashboard/school-manager/operations` supplies one aggregated operational snapshot. The existing dashboard summary endpoint still supplies evaluation statistics and tool-card metrics. This replaces separate overview calls for student oversight, staff totals, gate passes and conversations.

Controller → service → repository → EF Core queries. Queries are sequential on the scoped DbContext, use projections/aggregates with no tracking, and bound recent lists and attendance history. No schema changes or migrations are required.

Access requires an authenticated school manager, the manager dashboard permission, and an active manager assignment to the selected school. Student summaries and summons require school oversight permission; audit counts require their respective permissions. Summons expose appointment metadata without case notes, guardian contact details or private message bodies. Scores are exposed only for approved visits.

The school day uses the published bell schedule and its timezone. The countdown runs locally without network requests. Automatic refresh runs once per minute and skips a hidden manager dashboard. A failed operational refresh retains the previous snapshot with its timestamp and a retry action.

Attendance percentages use recorded attendance as their denominator; recording completeness is shown separately. Unrecorded people are not counted as absent. Charts require sufficient history and use an honest 0–100% scale. Empty states use real absence of data rather than generated statistics.

## Validation

- API project build succeeds without errors or warnings.
- Eight targeted operations and dashboard scope tests pass, covering school isolation, revoked assignment, role/permission boundaries, local dates, duplicate staff roles and unrecorded attendance.
- Angular development build succeeds.
- `node frontend/scripts/check-manager-dashboard.mjs` exercises the built Angular application with isolated API fixtures: original artwork, compact banner, equal panel sizes, one initial operations request, tool navigation, refresh failure/retry, and mobile layout. It does not validate a live SQL Server or use a live account.
- Browser screenshots are generated under `frontend/test-results/manager-dashboard-*.png`.
- The production build currently fails on existing stylesheet error budgets for `storage-page.component.css` (39.34 kB) and `visit-workspace.component.css` (34.50 kB), both above the existing 32 kB limit. These files and budget settings are outside this redesign and remain unchanged. The new overview stylesheet is below that error limit, with a warning against the 16 kB warning threshold.

The running API process must pick up the new build before the new operations endpoint is available. Deployment and database writes are not part of this change.
