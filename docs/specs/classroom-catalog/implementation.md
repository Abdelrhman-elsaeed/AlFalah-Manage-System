# C33 / D-125 — Classroom catalogue foundation

**2026-10-15. Owner authorized implementation after C32 review.** This delivery covers additive identity and the usable catalogue/teacher-selection slice (C1–C2). C3 consumer completion, C4 assignment presentation and C5 assignment PDF remain separate phases. The C32 prototype and its sample PDF are design artifacts, not the production assignment screen.

## Delivered behavior

- The existing `/student-affairs/classrooms` route is the single classroom-management page. It is now visible to the school manager alongside existing classroom staff. There is no second catalogue frontend or table. Server writes require the actual school manager, a global administrator or existing `Classroom.Manage` / `StudentEnrollment.Manage` authority; ordinary active school members can only read options.
- A grade navigator groups numbered sections within the school's stage: primary1–6, intermediate1–3, secondary1–3. The school stage is authoritative for new records. Existing records from another stage remain visible under an explicitly marked previous-data stage selector; they are not relabeled into the current school stage.
- A manager enters a section range and a physical location. One transaction creates the whole batch, derives names, normalizes Arabic/Persian/ASCII digits and rejects duplicates. A database unique index also protects concurrent creates. The shared location can subsequently be edited per section. Context fields (school, year, stage, grade) are not editable in-place.
- An explicit edit can map an old letter section to its correct number while retaining its ClassroomId, enrollments and ID associations. New arbitrary display names are not accepted. Inactive sections remain retained; this UI uses activation instead of destructive deletion. Previous years are read-only.
- C33 initially blocked rename when legacy timetable entries used names alone. C34 supersedes that temporary restriction with retained school/year name bindings; see [consumer cutover](consumers.md). Published rows are not rewritten.
- Account settings and the manager's teacher-profile editor use the same searchable grouped multi-select. Options are fetched in bounded SQL pages of100 and the client reads every page, including selected values beyond the first100. Lookup failure cannot silently clear selection or enable Save.
- Self-save is immediate, with no approval queue. The teacher's current-year ID set and its `Self` / `Manager` audit are committed together, using `TeachingRevision` to reject stale replacement. Subject-only requests preserve associations. Prior-year rows remain retained. Selected inactive sections are shown and removable; a teacher cannot add a new inactive section.
- Old `Classes` arrays may echo the existing association labels for compatibility. They cannot replace an ID set without the new revision contract or introduce new text associations. Changing associations uses the account or teacher teaching endpoint with IDs. User create/update continues to preserve the set when classes are omitted; no free-text class editor is reintroduced there.
- Teacher classroom list/access membership now uses school-scoped ClassroomId joins. Existing role, active-user/classroom/year and enrollment conditions remain. Self-selection does not create a timetable assignment or change the published current lesson.

## Runtime contracts

| Endpoint | Contract |
|---|---|
| `GET /api/v1/classroom-catalog?academicYearId=&page=&includeInactive=` | Active school membership; page1-based,100 items plus `hasMore`; school stage, manager authority and school-associated academic years in context. Omitted year requires exactly one active school year. |
| `POST /api/v1/classroom-catalog/batch` | `{ academicYearId, gradeLevel, sections: string[], physicalLocation }`;1–100 numeric sections; atomic create in active year. School/stage/actor are derived server-side. |
| `PATCH /api/v1/classroom-catalog/{id}` | `{ section, isActive, physicalLocation, revision }`; timestamp revision, existing school/year identity;400 invalid/duplicate,403 denied,404 unavailable,409 stale/concurrent. |
| Existing `PUT /api/v1/account/teaching` and `/teachers/{userId}/teaching` | Optional subject/stage; `classroomIds`, `academicYearId`, `teachingRevision` for a replacement. Existing self/manager permission gates remain. Missing class fields mean leave alone. |
| Existing `/api/v1/classrooms` writers | Compatibility handlers delegate create/edit policy to the same catalogue service; supplied display labels cannot create a second naming policy. Existing read/enrollment/delete endpoints retain their scopes. |

New backend work follows Controller → Application service → repository → existing DbContext. No business queries were added to controllers. Options use SQL `Skip/Take(101)` rather than loading the whole school for each page. The multi-select keeps complete selected IDs across its local search.

## Additive migration and mapping

Migration `20261015101631_AddStructuredClassroomCatalog` adds nullable Classroom.SectionNumber, nullable school/classroom references on InstructorClass and revision1 on InstructorProfile. It retains the existing profile FK and adds the school/classroom composite FK. The profile remains one row per user in this codebase and may move schools, so a composite profile-school FK would make retained associations unmovable; service validation and teacher authorization check the profile's current school, and old-school rows are excluded from live selections. A future profile-per-school redesign is outside this slice.

The migration maps only a single exact active classroom name in a single active school year. Unambiguous numeric sections are normalized into SectionNumber; existing Section and ClassLabel are never rewritten by migration. Letter/unknown/ambiguous labels remain unresolved. The new UI displays their original labels and requires explicit selection/mapping. Historical labels are retained on bridge rows and signed snapshots.

Read-only local discovery found10 classrooms, all letter sections, and4 teacher label rows with0 exact matches. All10 classroom IDs/fields and all4 label rows were preserved; no letter was guessed to mean a number. Local schema advanced from69 to70 migrations only after fresh isolated SQL migration verification. Private before/after hashes verified unchanged classroom fields, old bridge fields, duty/visit artifacts, signatures, archive/storage records, timetable entries, school Drive roots/credentials and enablement. No Drive write was performed by this task.

Down migration refuses to discard structured section IDs, linked teacher associations or revised teaching sets; SQL verification exercises that refusal. Before adoption, the guard permits reversal; after adoption, export/review is required. Repeated Up is idempotent through EF migration history. Existing data is never deleted to make migration pass.

## Verification and limits

See [implementation evidence](verification/implementation/README.md). Actual manager/teacher pages were inspected at390/768/1440 against the existing test-school API without business writes. SQL tests write only to newly created, prefix-guarded LocalDB databases. Operational signed documents and Drive objects were not used as test payloads.

The migration is installed in the local manual-test environment, not deployed elsewhere. Full current-lesson/import/visitV1+V2/rollover consumer conversion remains C3. Current assignment editing and exports retain their existing behavior pending C4/C5. Do not infer completion of all354 original consumer surfaces from this foundation delivery.

## Subsequent implementation

C34 now delivers C3 locally, including date/year-bound visit selection, manual timetable normalization, retained-name resolution and complete student lookup paging. Schema71 is additive; C4 assignment UX and C5 assignment PDF remain pending. [Current consumer contracts and verification](consumers.md).
