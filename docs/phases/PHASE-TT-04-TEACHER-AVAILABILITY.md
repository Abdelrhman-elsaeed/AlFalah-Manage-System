# Intelligent Timetable — Phase 4: Teacher Availability

Teacher Settings is available at `/intelligent-timetable/teachers` under **الجدول الذكي → إعدادات المعلمين**.
Choose an academic year, semester, setup and active teacher. The setup must have a selected Phase 2 bell schedule.

## Delivered

- Searchable teacher picker, compact RTL profile form, maximum/assigned/remaining load, visiting and print flags with helper text.
- Weekly matrix uses effective per-day periods, including different period counts and times. All cells default to available. Non-study days and missing periods have no checkbox.
- Whole-grid and per-day select/clear, native keyboard controls, sticky teacher/day labels, unsaved-navigation protection and before-unload warning.
- Save leaves the grid visible, disables editing during the request and retains user changes on errors or stale-version conflicts.
- Immutable period references, optimistic profile concurrency, school-scoped foreign keys and audit history. A shared setup revision serializes availability changes against existing timetable writes, including first-time profiles.
- A single aggregate PUT saves the profile and bulk-edited matrix together. Invalid or missing cells, foreign teachers/periods, duplicate cells, negative loads, excess capacity and stale timing revisions are rejected.
- Every timing revision change requires explicit review; exact day/time matches are proposed and unmatched prior slots are listed.
- Hard constraints are integrated into timetable save, import, restore and publish. Closures on existing assignments remain visible as review violations. The maximum cannot be lowered beneath current assigned lessons.

## API

All endpoints require authentication, an active school and `Timetable.Manage`; Instructor and Guardian roles are explicitly excluded even when they carry this permission.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/v1/intelligent-timetable/settings/profiles/{setupId}/teachers` | Active teachers in the setup's school |
| GET | `/api/v1/intelligent-timetable/settings/profiles/{setupId}/teachers/{teacherId}` | Profile, effective grid, current load and review issues |
| PUT | `/api/v1/intelligent-timetable/settings/profiles/{setupId}/teachers/{teacherId}` | Atomic profile and bulk matrix update |

PUT includes `revision`, `bellScheduleRevisionId`, `shortDisplayName`, `maximumWeeklyPeriods`, `isVisiting`, `hideFromPrint`, `confirmScheduleReview`, and `slots: [{ day, bellPeriodId, isAvailable }]`.
Responses use the existing `ApiResponse<T>` envelope with 400 validation, 403 permission, 404 school-scoped not-found and 409 concurrency responses.

## Boundaries and defaults

The initial maximum equals the effective weekly period count; it is never silently reduced when checkboxes change. A zero maximum is a hard prohibition on lesson assignment.
Current allocated load counts non-deleted lesson entries in the setup's current timetable. Standby must respect availability but is not teaching load. Future co-teaching allocations must count each teacher's participation.

The visiting flag identifies part-time teachers whose day/time restrictions are recorded in the same hard availability matrix. No additional unstated part-time quota is imposed.
The print flag is persisted for the later print/public-board phase; existing output code is not changed in Phase 4.
Automatic generation, Phase 6 allocation records and swaps have not been built yet; their future entry points must use the shared `TeacherAvailabilityService` constraints.

## Verification

- `dotnet build backend/AlFalah.slnx`
- `dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj` (the solution does not include the test project)
- `npm run build` in `frontend`
- Targeted ChromeHeadless teacher-settings tests cover per-day periods, bulk edits, load validation, remapping confirmation, save without grid reload, conflict preservation and obsolete responses.
- Migration `20260907042123_AddTeacherAvailability` creates only the two Phase 4 tables and their indexes/constraints and has been applied to local `AlFalahDb`.

## Manual acceptance

1. Open the screen as Secretary/Management with `Timetable.Manage`; select a setup with timings and a teacher.
2. Verify all real periods are checked on first load and holidays/missing cells cannot be edited.
3. Set a suitable maximum, close a day, enter a short name, enable visiting/print flags, save and reload.
4. Try assigning an unavailable slot or exceeding the maximum through the existing timetable editor: the backend must reject it.
5. Change the timing template, return to Teacher Settings and verify review is required before saving or scheduling.
6. Edit in two sessions and verify stale saves report a conflict while retaining the local draft.
