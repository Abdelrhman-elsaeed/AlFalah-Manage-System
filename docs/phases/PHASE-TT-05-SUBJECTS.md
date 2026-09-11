# Intelligent Timetable — Phase 5: Subjects & Requirements

Open `/intelligent-timetable/subjects` under **الجدول الذكي → المواد**. The previous `/intelligent-timetable/subject-rules` placeholder redirects here.

## Delivered

- School subject catalog with editable names/colors; configuration selects existing active subjects through a searchable dropdown.
- Per-setup, per-class quotas: individual periods plus twice the number of paired blocks. Different class rules remain individually visible and editable.
- Stage/grade filtering, multiple-class selection, filtered select-all, configuration filters and summary cards.
- Early/late preferences with period windows are soft; permitted days and fixed slots are hard rules. Empty allowed days means every study day.
- Simple teaching room catalog, interchangeable room selections and a preferred room per class requirement. `SchoolLocation` remains the geographic location catalog.
- Explicit bulk keep/overwrite dialog. The API preserves existing rows by default, checks revisions before replacement, and reports created/updated/skipped classes with reasons. Stale changes return HTTP 409.
- Validation of school/year scope, active subjects/classes/rooms, counts, effective periods, paired blocks without time gaps, weekly class capacity and fixed class-slot conflicts.
- School-scoped foreign keys, filtered uniqueness, audit records and optimistic concurrency. Requirement removal is soft and blocked when referenced by any historical/current timetable entry.
- Manual save/import/restore resolves configured class-subject identities and validates days, load and rooms. Publishing checks complete quotas, fixed slots, and adjacent doubles sharing teacher and room; early/late preference never rejects an otherwise valid placement.
- Requirement changes invalidate setup readiness and mark linked timetables for revalidation through the existing revalidation mechanism. Subject colors are exposed for timetable display; historical subject text remains in snapshots.
- Arabic RTL responsive PrimeNG forms, visible API errors, draft preservation after conflicts and unsaved-navigation protection.

## API

Base: `/api/v1/intelligent-timetable/settings/profiles/{setupId}/subjects`.
All endpoints require authentication, an active school and `Timetable.Manage`. Instructor/Guardian roles remain excluded.

| Method | Suffix | Purpose |
| --- | --- | --- |
| GET | `/` | Catalog, classrooms, rooms, per-class requirements and effective schedule |
| POST | `/` | Create master subject |
| PUT | `/{subjectId}` | Edit master name/color with revision |
| POST | `/rooms` | Create a simple teaching room |
| POST | `/allocations` | Allocate subject rules to classes; default keep-existing behavior |
| PUT | `/requirements/{id}` | Update an individual class requirement with revision |
| DELETE | `/requirements/{id}?revision=N` | Remove the class requirement, preserving the master subject |

Allocation accepts `subjectId`, `classes: [{ classroomId, revision }]`, `overwriteExisting`, and `rules` with counts, preference/window, allowed days, fixed slots, room IDs and preferred room ID. Revision zero means the caller saw no existing requirement. Shared validation errors reject the request; individual class capacity/fixed-slot failures are reported as skipped. Any revision conflict aborts the save.

## Verification

- Migration `20260908150439_AddTimetableSubjects` generated and applied to development LocalDB `AlFalahDb`. It adds six subject/room/rule tables and nullable source/room links to existing timetable entries without deleting existing data.
- Backend build and complete test project pass; Phase 5 tests cover quotas, preferences, gap rejection, isolation/permissions, room persistence, stale updates, history protection and manual/publication constraints.
- Angular production build and five targeted ChromeHeadless tests pass.
- Real Chrome browser + local SQL/API smoke passed catalog creation, allocation, keep-existing confirmation, reload, permissions, and concurrent updates (200/409), with desktop/mobile checks and no browser errors.
- Existing unrelated Angular CSS budget/PrimeNG selector warnings remain.

## Later phases

Automatic timetable generation and Phase 6 teaching allocations do not exist yet. The generator must consume `SubjectSchedulingPolicy.PreferencePenalty` for ranking and `SubjectSchedulingPolicy.Adjacent` for paired candidates, and validate final placements through `SubjectAssignmentService`. Teacher availability validation remains in the existing timetable workflow. This phase does not add a generation algorithm or room capacity/equipment rules.

Legacy free-text timetable entries remain supported where no matching class-subject requirement exists. Configured entries receive normalized subject/class/requirement/room IDs; new generated entries must use these identities.
