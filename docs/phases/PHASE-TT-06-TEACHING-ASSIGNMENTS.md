# Intelligent Timetable — Phase 6: Teaching Assignments

The Secretary can open **الجدول الذكي → إسنادات التدريس** at `/intelligent-timetable/assignments`. Select the academic year, semester and setup, then add teachers in the class-by-subject matrix. Teachers need a saved Phase 4 profile in that setup. Confirm each edited cell and choose **حفظ التغييرات** to persist the batch.

## Delivered behavior

- RTL PrimeNG matrix, disabled cells for subjects a class does not study, sticky headings, class/subject/stage/grade/status/search filters, pending count, draft banner, explicit save/discard and navigation protection.
- Server-side teacher search, specialization/activity/visiting/capacity filters, sorting and pagination. Selections survive search and pagination. The picker and optional teacher summary show allocated/max/remaining periods plus subject/classroom counts. Draft changes across all cells contribute to the displayed load.
- Single-teacher, co-teaching and split-quota modes. Multiple teachers require an explicit mode. Specialization mismatches are warnings. Co-teachers receive the entire weekly quota each; role labels are omitted.
- Double periods belong whole to the same teacher. Split members store both total periods and owned pair counts; pair counts must add up exactly, and each owned pair consumes two of that teacher's periods. Co-teachers stay together for both periods.
- School/setup composite foreign keys, unique active assignment per requirement, unique member per assignment, allocation check constraints, audit records and revision-based concurrency. A batch validates final capacity, allowing swaps without temporary overload, and persists in one SQL transaction with its audit.
- Assignment changes mark existing timetables for review. Manual save/import/restore/publication validate assigned teachers, per-member quota, co-teaching occurrence membership and double-period ownership. Logical occurrence identity uses timetable/requirement/day/period, with the same room for co-teachers.
- Phase 4 prevents reducing a teacher's maximum below planned assignments. Phase 5 prevents changing the quota/pair count or removing a requirement while it has active assignments; unassign first. Assignments left behind by deactivated classrooms/teachers can be removed.

## API

Base: `/api/v1/intelligent-timetable/settings/profiles/{setupId}/assignments`.

| Method | Suffix | Purpose |
| --- | --- | --- |
| GET | `/` | Setup revision, matrix requirements, current assignments and teacher workload |
| GET | `/teachers` | Search/filter/sort/page teachers |
| PUT | `/` | Atomically save changed cells using the setup revision |
| DELETE | `/{requirementId}?revision=N` | Unassign a subject requirement |

All handlers require authenticated school scope and `Timetable.Manage`; Instructor/Guardian roles are excluded. Permission/scope/concurrency/validation failures return HTTP 403/404/409/400 respectively.

Example split of five periods, including two complete doubles:

```json
{
  "revision": 7,
  "changes": [{
    "classSubjectRequirementId": 12,
    "mode": "SplitQuota",
    "members": [
      { "teacherTimetableProfileId": 21, "allocatedPeriodCount": 3, "allocatedPairedBlockCount": 1 },
      { "teacherTimetableProfileId": 22, "allocatedPeriodCount": 2, "allocatedPairedBlockCount": 1 }
    ]
  }]
}
```

Empty `members` explicitly unassigns a cell. Omitted cells remain unchanged. Teacher search accepts `search`, `specialization`, nullable `isActive`/`isVisiting`, `hasCapacity`, `sort` (`name`, `load`, `remaining`), `descending`, `page`, and `pageSize` (1–100). Server sorting/capacity filtering uses saved load; displayed load also includes the browser's draft.

## Verification

- Migration `20260909024900_AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz` generated and applied to development LocalDB **AlFalahDb**. Adds two assignment tables and school/setup alternate keys; existing subject/teacher data is preserved.
- Full backend test project: **410 passing tests**, including **18 Phase 6 cases**. Backend build, Angular production build and six focused ChromeHeadless tests pass.
- Real LocalDB smoke verified query translation, populated search, assignment save/audit, unassign and reassign inside a rolled-back transaction.
- Chrome desktop/mobile smoke with API fixtures verified the matrix, teacher selection, explicit multi-teacher mode, draft confirmation/save and browser errors. This browser test uses fixture responses; SQL persistence was checked separately.
- Existing unrelated nullable-reference test warning, dashboard CSS budget warning and PrimeNG selector warnings remain.

## Scope and compatibility

Automatic timetable generation remains a later phase. Its output must pass the existing availability and subject/teaching-assignment validators. Legacy setups with no Phase 6 assignment history retain Phase 5 placement behavior; once Phase 6 is used, all configured requirements must have valid assignments before publication. Mid-term effective dates and primary/assistant labels are not introduced. More than two teachers are accepted subject to the mode/allocation rules, as the blueprint does not specify a numeric maximum.

## C32 / D-124 ? Classroom identity and assignment design (2026-10-15)

Design/prototype only, explicitly selected by the owner. Existing structured Classroom/subject-allocation models are reused in the proposal; legacy InstructorClass text identity, teacher self/manager editors, both visit frontends, student/classroom scope, timetable import/generation/review/history and exports are covered in a semantic impact map. The owner confirms direct teacher association self-save without approval, stage-local grade ordinals and numeric sections. An isolated theme-aligned prototype demonstrates the manager catalogue, teacher multi-selection, assignment matrix/co/split/load and source-structured statement. No production code/schema/permissions/provider change or feature cutover. [Design, decisions and evidence](../specs/classroom-catalog/README.md). Implementation and physical/printer acceptance remain future phases.

## C35 / D-127 - Production assignment workspace (2026-10-15)

The existing Phase6 route/API now hosts the approved grade-grouped ledger, independently paged teacher summary and focused server-paged picker. Single/co/split/whole pairs, complete draft-aware load and atomic revisioned save remain intact; no-op confirmation, stale requests,409 recovery and keyboard focus return are covered.19 Angular/50 backend checks and actual/synthetic browser verification pass; Development build passes. Existing production CSS errors remain disclosed. [Behavior](../specs/classroom-catalog/assignments.md), [evidence](../specs/classroom-catalog/verification/assignments/README.md). C5 assignment PDF is not implemented in this slice.

## C36 / D-128 - Saved assignment statement (2026-10-16)

C5 now exports the saved assignment revision through the existing page/API and C30 transfer policy. The manager explicitly classifies specialization/other loads; no text guessing or automatic timetable rewrite. Frozen school-scoped snapshots/PDFs, A4 merged headings, whole teacher rows, co/split/pair annotations, stage-aware grades and an other-material appendix are implemented. Fresh SQL checks preceded additive local schema72;37 original-table column hashes are preserved.67 final backend plus one distinct SQL check and26 Angular checks pass, with real PDF pixels and isolated SQL browser downloads at390/768/1440. Development build passes; unchanged VisitsV2/storage CSS budgets still block Production. C6 appearance/assets/physical/printer/pilot acceptance remains. [Behavior](../specs/classroom-catalog/statement.md), [evidence](../specs/classroom-catalog/verification/statement/README.md).
