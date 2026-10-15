# C34 / D-126 — Classroom consumer cutover

**2026-10-15.** This is the second implementation delivery, covering C3 after the C33 catalogue/teacher-selection foundation. It reuses existing classroom IDs and routes. C4 assignment presentation and C5 saved-revision assignment PDF remain separate, unimplemented phases.

## Active workflows

- New and editable V2 visits select one school classroom from the shared grouped/searchable picker. The server requires its ID and resolves the academic year from the visit's calendar date and the school's associated academic years. Missing, ambiguous, foreign-school/year, deleted or newly selected inactive references fail before mutation. An existing inactive selection may be retained. Teacher/date changes clear the selection; stale teacher autofill cannot restore a previous selection.
- The server derives a new visit's `GradeClass` snapshot from its ID. Updating the same ID preserves the recorded label even after a catalogue rename. Changing the ID explicitly records its new label. Legacy null-ID reports remain readable, exportable and reviewable. An editable legacy draft must explicitly choose its classroom before saving/finalizing; no automatic guessed backfill is applied to visits.
- Manual timetable cells use the same picker, scoped to the timetable's academic year. Generated requirement/subject/room metadata retains its existing read-only behavior. Existing clipboard imports resolve exact current or retained names within the school/year; they cannot introduce unknown classrooms. Existing unresolved legacy entries can remain unresolved rather than being guessed into a roster.
- Timetable validation normalizes resolvable classroom references to IDs/current labels before checking classroom conflicts. Configured subject IDs still require valid requirements. A classroom ID alone does not invent a subject quota. Co-teaching occurrence grouping, split quotas, whole pairs, rooms, substitutions, version restore, atomic saves and concurrency remain governed by their existing services.
- Student-affairs lookup helpers fetch all bounded pages, rather than truncating the catalogue at100. Live operational screens default to the school's active term/year; an explicit academic-year lookup remains available for history. Existing enrollment, attendance, messaging, permit and duty APIs continue using classroom/enrollment IDs and their existing authorization rules. No student enrollment spreadsheet importer exists in the current runtime; biometric attendance imports do not create classrooms.

## Retained timetable names

Migration `20261015115204_AddClassroomConsumerReferences` adds nullable `Visits.ClassroomId` and an append-only `ClassroomLabelHistory` binding with school/year/classroom composite FK and unique school/year/label index. It seeds current names for retained nondeleted classrooms. It does not rewrite visits, timetable entries, serialized versions, enrollments or signed artifacts.

Catalogue create/rename atomically retains names against the same classroom ID. A name reserved for another ID cannot be reused through the catalogue. This supersedes C33's temporary rename block for null-ID timetable entries. Published entries remain untouched; current-lesson, teacher-roster and gate-pass readers resolve their old labels through an exact school/year binding. Ambiguous/corrupt references fail closed. Caller-provided labels do not establish classroom access. Renaming does not transfer self-selected associations, enrollment or teacher identity to another class.

Application saves reject modifications/deletions of retained name bindings. Down migration refuses to discard used visit references or previous names. C33's migration is unchanged. Local schema is71; existing classroom fields/bridges, timetable entries and versions, visit/duty artifacts, storage/archive records, Drive credentials/roots/flags were hash-checked unchanged during migration. Operational classes had grown to16 before this migration; no classes or teacher associations were added for verification. No operational Drive write was made.

## Runtime contracts

| Surface | Contract |
|---|---|
| `GET /api/v1/classroom-catalog` | Existing paged/member-scoped endpoint additionally accepts optional `onDate=YYYY-MM-DD`. Date resolves exactly one school-associated academic year. An explicit conflicting `academicYearId` is rejected. |
| V2 visit create/update/detail | Add nullable `classroomId` in the existing DTOs. It is required by new/edit/finalize business rules; old retained records may return null. `gradeClass` remains the historical display/export snapshot, not identity supplied by the client. |
| `GET /api/v1/classrooms` | Optional `currentYearOnly=true`, used only when no explicit year is supplied. Existing school, paging, search and enrollment-term predicates remain. |
| Timetable saves/imports | Existing `classroomId` field is authoritative. Bounded exact-name compatibility resolves old inputs; configured subject/requirement checks remain. New unknown free text is rejected. |

No new permissions, roles, archive obligation or storage frontend were introduced.

## Semantic inventory disposition

The [C34 lexical inventory](verification/consumers/source-inventory.json) indexes384 matching files. It is a navigation index, not a claim of384 individually executed browser tests. Remaining text comparisons were reviewed as follows:

| Remaining surface | Disposition |
|---|---|
| `ClassroomCatalogService.ApplyTeacherSelectionAsync` | Exact compatibility echo/retained legacy association handling, scoped to the current school/year. New replacements require ID/revision. Not a text authorization join. |
| `TimetableClassroomReferences`, `SubjectAssignmentService` | Explicit school/year compatibility adapter. Ambiguity rejected; existing unresolved source can be retained but cannot grant an ID roster. |
| `StudentWorkflowRepository.ClassroomLabelExistsAsync` | School/year duplicate-name validation; not access identity. Existing classroom writers already delegate to the catalogue policy. |
| `VisitV2Repository` search / document projections | Search and immutable report labels; not authorization. Signed/archived bytes are retained. |
| VisitsV1 form and `VisitService` | Retired write API: create/update/submit/review mutations remain410. Historical reading/export stays. V1 was not reactivated to add another live classroom editor. |
| `InstructorClass.ClassLabel`, old timetable JSON, duty/visit snapshots | Retained historical/compatibility text. Live class authorization uses IDs; no destructive text rewrite. |
| Development seeders, tests and migrations | Explicit fixtures or historical schema. Not live free-text classroom creation. Historical SQL fixtures seed columns that actually existed at their baseline before applying current migrations. |
| Subjects/requirements, teaching assignments, enrollment/transfer, duty roster/student search, conduct/support/guardian/security/messaging/dashboards | Already ID-based. Existing school, term, user-role and enrollment constraints retained and regression-tested; no second classroom list added. |

## Verification

See [C34 evidence and limits](verification/consumers/README.md). Business mutation tests use newly created, prefix-guarded LocalDB databases and synthetic accounts. Real application browser checks use existing test-school sessions without business writes. Provider recovery tests use the existing fake Drive implementation; they are not a claim of live Google uploads. Physical-device/pilot acceptance remains C6.
