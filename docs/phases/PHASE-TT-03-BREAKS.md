# Intelligent Timetable — Phase 03: الاستراحات

Implemented on 2026-09-06 from [the Phase 03 blueprint](../specs/intelligent-timetable/03-breaks.md) and its finalized decisions.

Breaks are independent exact local intervals. They may precede lessons, follow lessons, touch boundaries, or leave intentional gaps. Names are required free text; optional categories are Recess, Prayer, Meal, Assembly, and Other. The “after period” dropdown only fills the start time once and has no API/database field.

## Implementation

- Domain/EF: `ScheduleBreakDefinition` and its one-to-one `ScheduleBreakWindow`, attached to the existing revision/day hierarchy. Exact times use SQL `time`, with `StartLocalTime < EndLocalTime` enforced by a check constraint. Each day variation is a separate record. `UsesDefaultBreaks` is independent of period inheritance. Published revisions and their break names/times remain immutable.
- Application/API: school-scoped MediatR commands/queries and FluentValidation, atomic replacement of break collections, conflict protection using the template revision, and dependent timetable revalidation. Updating periods also validates retained breaks, including when an older client omits the new fields.
- Frontend: RTL PrimeNG editor, **الحصص / الاستراحة** tabs, all-days and study-day tabs, count stepper, categories, names, exact times, placement helper, delete confirmation, selected-day apply, independent day overrides, unsaved-state protection, and visible save actions. Conflicting lessons and breaks are highlighted; all effective days are validated before saving.
- Views: ordered break intervals in the existing general/teacher grid and PDF, with no editable teacher assignment cells for breaks. The unified effective API is available to future class views. A separate class-timetable view does not exist in the current module. Excel assignment import/export retains lesson columns only.

## REST contract

All routes start with `/api/v1/intelligent-timetable/timings/{templateId}`.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/breaks` | Current scoped schedule with default/day breaks |
| PUT | `/breaks` | Atomically create, update, or remove breaks by replacing the collection |
| GET | `/breaks/effective/{day}` | Ordered `Lesson`/`Break` intervals for day 1–7 |

PUT accepts `{ revision, defaultBreaks, days }`. Each day contains `{ day, usesDefaultBreaks, breaks }`; each break contains `{ name, category, startLocalTime, endLocalTime }`. Supply all seven day records. Holidays and inherited days have empty explicit break collections. An empty default collection removes default breaks in the new revision. Writes require the same `Timetable.Manage` or school-scoped editor grant as Phase 02; excluded teacher/guardian roles cannot edit. Errors use existing 400/403/404/409 conventions.

The existing complete timing save API accepts these fields too, allowing coordinated lesson and break changes in one transaction. Omitted default breaks preserve the existing break configuration for older clients.

## Verification

- Migration `20260906143403_AddTimetableBreaks` generated, reviewed (only break tables and inheritance column), and applied to local development `AlFalahDb`.
- `dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --no-restore -v minimal`: **372 passed**. The test project is not in the solution, so it is run explicitly. Includes PDF generation with breaks, lesson-only Excel columns, and gate-pass resolution during a published break.
- `dotnet build backend/AlFalah.slnx --no-restore -v quiet`: **0 warnings, 0 errors**.
- `npm run build`: passed.
- Targeted Angular ChromeHeadless tests: **17 passed**, covering the timing editor and breaks editor.
- Live local SQL/API and Chrome desktop/mobile: overlap rejection, permissions, independent placement helper, isolated day variants, multi-day application, save/reload persistence, chronological effective intervals, and concurrent saves returning **200/409** all passed with zero browser errors and no editor horizontal overflow. Temporary QA templates were soft-deleted after verification; their immutable revisions and audit history remain.

Existing non-failing warnings remain in `SocialWorkerWorkflowTests.cs` and the frontend dashboard CSS budget/PrimeNG selector optimizer.

## Try it

1. Sign in as Secretary or an authorized timetable editor and open `/intelligent-timetable/breaks` (**الجدول الذكي → الاستراحة**).
2. Select a template, choose **كل الأيام** or a study day, and add a break. Enter its name, optional category, and exact start/end times. Optionally fill the start using **بعد الحصة**.
3. Check highlighted overlaps and day-specific messages. Intentional gaps and adjacent boundaries are valid. Select target days and apply; any target conflict blocks the entire operation.
4. Save all changes, then reload. The day overrides remain separate. A timetable already using a previous revision continues to show its historical times until reviewed and republished.
