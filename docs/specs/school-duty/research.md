# Research — Client Evidence and Existing Application

**Reviewed:** 2026-10-13. The original research below is a static baseline. D1 implementation and isolated verification are now recorded in [D1 verification](verification/d1.md); they do not establish D2/D3 or live Google Drive delivery.

## Source inventory

External source directory: `C:\Users\abdelrhman\Desktop\المناوبة\New folder`.

Images were visually inspected. Orientation-corrected temporary inspection copies were used for reading; originals were preserved. No OCR-derived text is treated as verified without visual inspection. The private source files were not copied into the repository.

| Source | Bytes | Interpretation |
|---|---:|---|
| `1.jpeg` | 161012 | `جدول المناوبة`, form `QF71-B-2-2`: term/year, numbered weeks, day/date, teacher, signature and notes; includes holiday/exam rows and administrative sign-off |
| `2.jpeg` | 127579 | Teacher subject/class assignments and teaching loads; timetable reference, not the duty report |
| `3.jpeg` | 103673 | `سجل توزيع حصص الانتظار`, form `QF71-B-2-1`: period-specific lesson cover; a separate process from dismissal duty |
| `4.jpeg` | 96737 | `تقرير المناوبة اليومي`, form `QF71-B-2-3`: original teacher and signature, `بدلاً عنه الأستاذ`, substitute signature, late students, violating students and general observations |
| `5.jpeg` | 64202 | Responsibility letter: teacher, day/date, supervision until the last student leaves, manager name and teacher's acknowledgement/signature |
| `رسائل بينى وبين العميل وشرح البيزنس.txt` | 3230 | Whole-school daily duty at dismissal, exclusions, rotation, absence exchange, mobile report, last departure, acknowledgement and automatic archive |

SHA-256 fingerprints, in the same order:

```text
1.jpeg  242FF09D0CE830E5592A05E3FD785F67B66C77CF3E68B998532B63465B05B997
2.jpeg  756160157D620C9E0E89A2E18EE0FC7C71CC25902B76259F7045985DF1262C5F
3.jpeg  073C78E1087C58ECE728909EE92CE954AFBFD9ECDB8A80F57A365891E318398A
4.jpeg  573C985BF08EBB01B429F1CACE5FB4D5CEC6CC8F749B51CBF644E04DDA4F7E88
5.jpeg  EBCFFB19BBC9F72C1094CBADA324ADCB40AE462AA4314C2A3CE40921812D1661
conversation  1137562D659B90D8DC3F14DE96A1095E1A61B5024B23F5617F0E8EB2056FE554
```

The historical school name, staff names, year and handwritten examples are sample content, not runtime defaults. The sources do not contain a screenshot of the storage folder hierarchy. They establish teacher/date naming; the owner subsequently approved the school/year/term/actual-teacher/date hierarchy and two separate PDFs in `plan.md` on 2026-10-13.

## Paper-to-digital mapping

| Evidence | Digital requirement | Qualification |
|---|---|---|
| Weekly duty roster | Term schedule with weeks, real dates, teacher, acknowledgement and notes; printable official roster | Rotation repeats over the term and can renew for the next term/year |
| Original/substitute report rows | Preserve original teacher and actual substitute separately, including signature ownership | Never manufacture an absent teacher's signature |
| `أسماء الطلاب المتأخرين` | Late-student report lines with student, class, time, action and notes | In this workflow, dismissal lateness; not automatic morning-delay penalties |
| `أسماء الطلاب المخالفين` | Violating-student report lines with the same fields | No automatic disciplinary case/penalty inferred from the paper |
| Seven printed rows per section | Unlimited practical digital lines, paginated PDF | Seven is paper capacity, not a business limit |
| Paper's next-day handover instruction | Report available to management the following operating day | Does not establish a separate manager-approval/signature workflow |
| Client last-student requirement | Explicit last departure time and teacher certification | Added from conversation, although no dedicated field is visible on the form |
| Client acknowledgement | Intentional acknowledgement action using the saved signature | Owner clarification overrides the conversation's ambiguous “opening signs” wording |

## Existing code and integration implications

| Area / source | Verified current behavior | Required addition |
|---|---|---|
| `frontend/src/app/features/timetables/school-timetable.component.{ts,html,css}` | School timetable, personal/school views, published/draft state, clones, inline swaps and moderator-grant dialog; no duty tab | Add a focused duty component through workspace tabs, retaining the current timetable behavior |
| `frontend/src/app/app.routes.ts` | `/timetable` permits Instructor; parent `/intelligent-timetable` excludes Instructor | Put the teacher flow behind a reachable timetable entry/deep link, not an inaccessible parent |
| `backend/AlFalah.Infrastructure/Services/SchoolTimetableService.cs` | Current editor grants are limited to active moderators and authorize timetable editing | Do not extend these grants to duty or repurpose them as general feature access |
| `backend/AlFalah.Domain/Entities/AttendanceRecord.cs` | One staff result per school/user/date: Present, Absent or Excused | Reuse this source for known duty unavailability |
| `backend/AlFalah.Infrastructure/Services/AttendanceService.cs` | Secretary submits a complete staff sheet; records can be corrected; no duty event/hook | Add a durable, same-transaction duty reconciliation signal; preserve attendance access policy |
| `backend/AlFalah.Infrastructure/Repositories/TimetableSubstitutionRepository.cs` | Existing lesson-cover lookup considers non-Present records unavailable | Reuse the same fact interpretation for duty; do not reuse lesson-cover movements or change weekly lessons |
| `backend/AlFalah.Domain/Entities/{TimetableSetupProfile,SchoolTimetable,BellScheduleTemplate}.cs` | School/year/semester context, revision-bound timing and school timezone; clones can exist | One operational duty schedule per school/term; timetable clones must not duplicate duty |
| `backend/AlFalah.Domain/Entities/StudentAffairs/AcademicEntities.cs` | AcademicTerm has school/year/semester/date bounds; students and enrollments exist | Reuse term bounds and a narrowly authorized student/class lookup |
| `backend/AlFalah.Domain/Entities/AcademicYear.cs` | Academic year has real start/end dates | Term must lie within year bounds |
| `backend/AlFalah.Domain/Entities/UserSignature.cs` | Saved image/drawn signature per user | Capture immutable signature bytes at each explicit acknowledgement/submission |
| `backend/AlFalah.Application/Storage/StorageLibraryContracts.cs` | Scoped library, stored-file/version ledger and provider abstraction | Reuse infrastructure while adding duty-specific authorization/provenance |
| `backend/AlFalah.Application/Storage/VisitArchive*.cs` | Frozen snapshot, durable operation, retries, leases and provider identity recovery | Follow reliability patterns; visit-specific services require VisitId and cannot archive duty unchanged |
| `backend/AlFalah.Domain/Enums/StorageEnums.cs` | Teacher/SchoolLibrary/VisitArchive folder kinds and corresponding sources; no duty kind | Add duty archive identity/protection, including legacy browse/download boundaries |
| `frontend/src/styles/{design-tokens,intelligent-timetable}.css` | Established green/gold Arabic RTL visual system | Use existing tokens and focused feature styles |

## Gaps to resolve in this feature

- Staff attendance currently rejects Friday/Saturday; timetable study days are configurable. If configured duty days exceed attendance-supported days, show the mismatch and require an explicit calendar decision. Do not silently invent absence facts or broadly rewrite attendance.
- No holiday calendar was established by this review. Propose explicit duty non-operating date ranges rather than a new school-wide calendar subsystem.
- An absent teacher may miss several consecutive dates; a repeatable exchange must operate on the current effective queue, retain previous exchanges and avoid double compensation.
- Duty ownership and all-school management need independent authorization. Existing storage delegation and timetable management are separate capabilities.
- Current source code contains newer storage controls than older storage planning notes. Implementation must inspect current options and per-school settings; neither an OAuth connection nor historical documentation proves external writes are active.

## Scope exclusions

No Noor import, biometric integration, subject-assignment rebuild, waiting-period feature, guardian messaging, morning-attendance changes, disciplinary escalation or general permission-distribution UI is requested in this feature.

## Waiting-period follow-up — 2026-10-13

The owner asked whether the pictured waiting-period register already exists and whether it should be added alongside duty. Current support is established by static code review, not a new live runtime test:

- `TimetableEntryType.Standby = 2` exists. The timetable supports editing/displaying standby cells as `منتظر`, showing standby totals, and including them in existing timetable Excel/PDF paths (`SchoolTimetableService`, `SchoolTimetableDocumentService`, and `school-timetable.component`).
- `/intelligent-timetable/substitutions` implements user-selected, confirmed lesson cover for one date. Candidates are validated against scheduling constraints, and teachers recorded Absent/Excused are disqualified (`TimetableSubstitutionService` and `TimetableSubstitutionRepository`).
- A scheduled standby slot can be used for cover; effective daily simulation suppresses the overlapping standby entry without replacing the permanent weekly lesson assignment (`TimetableSwapEngine.SuppressCoveredStandby`). This is period-specific lesson cover, not whole-school dismissal duty.
- Confirmed cover retains original/replacement teacher IDs, date, affected periods, requester/approver and audit/version history. Existing operational teacher queries resolve date-specific ownership (`TimetableSubstitution` and `EffectiveTimetableQueries`).
- The inspected flow requires selecting a lesson, reviewing a candidate and confirming it. It does not establish automatic whole-day absence allocation or fair distribution of all actual cover assignments. No equal automatic standby generator was identified in the inspected timetable generation code.
- The inspected cover records/contracts do not include the pictured `ما تم تنفيذه في حصة الانتظار` execution narrative or the covering teacher's execution signature. Existing timetable export is not evidence of a source-faithful signed `QF71-B-2-1` daily register.

Recommendation, not an approved scope addition: finish D1–D3 duty first, then extend the existing standby/cover workflow with the official execution register, teacher acknowledgement/signature, and its document/archive needs after clarifying its rules. Reuse attendance and existing timetable cover instead of creating a second substitution system. Both features can react to the same staff absence while retaining separate assignment, fairness, execution and document records. Existing standby allocations/cover counts do not weight the owner's confirmed equal duty rotation.
