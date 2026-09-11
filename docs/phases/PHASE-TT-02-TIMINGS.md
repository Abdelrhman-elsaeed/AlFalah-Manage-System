# Intelligent Timetable — Phase 02: توقيتات الجدول

**Status:** Completed — ready for testing
**Last updated:** 2026-09-06

Blueprint: [Study Days and Bell-Schedule Timings](../specs/intelligent-timetable/02-timings.md).

Implemented normalized bell templates/revisions/days/periods, school-scoped MediatR management, FluentValidation, immutable published revision resolution, setup invalidation, teacher/gate-pass integration, dynamic timetable grid/exports, development seeding, and the RTL timing editor with comparison and confirmation.

Migration `20260905163146_AddTimetableTimings` is applied to local development `AlFalahDb`.

## Verification

- `dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --no-restore -v minimal`: **354 passed, 0 failed, 0 skipped**. Tests are run explicitly because the solution does not include the test project.
- `dotnet build backend/AlFalah.slnx --no-restore -v quiet`: **0 warnings, 0 errors**.
- From `frontend`, `npm run build`: **passed**.
- From `frontend`, `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/intelligent-timetable/timings/timetable-timings.component.spec.ts`: **9 passed** (local Chrome supplied through `CHROME_BIN`).
- Live development SQL/API + Chrome desktop/mobile: validation, duplicate names, permissions, template/profile selection, day inheritance/override isolation, selected-day comparison, save/reload persistence, revision conflicts and zero page errors **passed**. Mobile editor has no horizontal overflow with the shell's sidebar collapsed.
- Simultaneous SQL-backed saves return **200 and 409**. The published grid renders its configured 30 weekly slots; PDF and Excel downloads return **200**. Creation requires a setup with a selected timing template.
- Migration history confirms `20260905163146_AddTimetableTimings`. Temporary QA templates/profiles were soft-deleted; seeded operational data and historical revisions/audits are retained.

Existing warnings: test-project nullable dereference at `SocialWorkerWorkflowTests.cs:41`; frontend dashboard-live CSS exceeds its component budget by 1.97 kB; three PrimeNG organization-chart selectors cannot be parsed by the optimizer. These do not fail the required checks.

## Testing walkthrough

1. Sign in as Secretary or an authorized timetable editor and open **الجدول الذكي → توقيتات الجدول** (`/intelligent-timetable/timings`).
2. Select the academic year/semester, create a named template, choose study days and timezone, and adjust period counts/times. Touching boundaries are valid; overlaps and reversed/overnight times block saving.
3. Change an individual study day, switch back to **كل الأيام**, and verify the default remains independent. Select other days, open the before/after comparison, and confirm application.
4. Save and reload; verify overrides persist. Try **تجاهل** and navigation with unsaved changes.
5. Choose a setup profile and **استخدام هذا القالب**. New timetable creation explicitly selects a configured setup. Existing dependent timetables show that timings require review; published operational times remain pinned until explicit successful republishing.
6. Verify different daily period counts, Friday, and exports. Teacher context and gate-pass approval resolve the configured published lesson only; gaps and holidays produce no current lesson.

Compatibility: legacy timetables without timing revisions need a selected template and explicit publication; no historical timing is guessed. Breaks remain the Phase 03 placeholder. Phase 03 is not started.
