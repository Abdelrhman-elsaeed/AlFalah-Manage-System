# Research — Client Evidence and Existing Application

**Reviewed:** 2026-10-13. The original research below is a historical static baseline. D1, [D2](verification/d2.md) and [D3](verification/d3.md) implementation now address the relevant seams. Original gap descriptions below are preserved as research history; they do not describe the current implemented surface or establish actual external Drive delivery.

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

No Noor import, biometric integration, subject-assignment rebuild, waiting-period feature, morning-attendance changes, disciplinary escalation or general permission-distribution UI is requested in this feature. The later 2026-10-13 amendment adds dismissal-lateness guardian notices, student-record facts and existing manager statistics in D2; it supersedes the original guardian-messaging exclusion only for this narrow workflow. C17 resolves delivery to both guardian dashboard and existing internal Messages, authored by System; external WhatsApp/SMS is outside the requirement.

## Owner amendment research — 2026-10-13

Reopened all five original images and verified their fingerprints against this inventory. [references.md](references.md) records clickable original paths and mandatory report/letter mapping; originals remain unchanged and outside the repository. `4.jpeg` controls exact report division; `5.jpeg` controls the letter. Images 2/3 do not expand duty into subject assignment or waiting-period execution.

[dismissal-lateness.md](dismissal-lateness.md) captures selected-student entry, guardian notices, history and manager count/list scope. Static inspection confirms effective `StudentGuardian` notification links, in-app `StudentAffairsNotificationDispatcher` recipient selection and `SchoolManagerOperationsDto`/overview seams. `StudentWorkflowRepository.GetStudentTimelineAsync` is an empty placeholder requiring a real scoped dismissal projection in D2. This planning change claims no new runtime delivery or visual acceptance.

C17 follow-up: `guardian-dashboard.component.ts` links messages to `/student-affairs/messages` and notifications to guardian notifications. `MessagingEntities.cs` requires `ConversationMessage.SenderUserId`/`SenderUser`; current thread types and `SendConversationMessageCommandHandler` support human conversations. `MessagingDeliveryPolicy` handles teacher office hours, while `MessageReceipt` retains delivery/read evidence. No system-origin sender capability is established by this review. D2 must add it with trusted server creation, additive contracts/UI, recipient isolation and independent deduplicated dashboard/message results; do not impersonate the recorder or relax human authorization.

## Waiting-period follow-up — 2026-10-13

**Current follow-up, 2026-10-16:** The owner corrected the new assessment's source from `2.jpeg` to `3.jpeg`. [C37 standby discovery](../intelligent-timetable/10-standby-periods-discovery.md) records a fresh visual inspection/current file fingerprint, detailed code inventory, generation/grant/gap-label findings, paper-to-system gaps and unresolved business questions. This does not expand or change School Duty behavior. The source fingerprints and static findings below remain historical evidence for their original date.

The owner asked whether the pictured waiting-period register already exists and whether it should be added alongside duty. Current support is established by static code review, not a new live runtime test:

- `TimetableEntryType.Standby = 2` exists. The timetable supports editing/displaying standby cells as `منتظر`, showing standby totals, and including them in existing timetable Excel/PDF paths (`SchoolTimetableService`, `SchoolTimetableDocumentService`, and `school-timetable.component`).
- `/intelligent-timetable/substitutions` implements user-selected, confirmed lesson cover for one date. Candidates are validated against scheduling constraints, and teachers recorded Absent/Excused are disqualified (`TimetableSubstitutionService` and `TimetableSubstitutionRepository`).
- A scheduled standby slot can be used for cover; effective daily simulation suppresses the overlapping standby entry without replacing the permanent weekly lesson assignment (`TimetableSwapEngine.SuppressCoveredStandby`). This is period-specific lesson cover, not whole-school dismissal duty.
- Confirmed cover retains original/replacement teacher IDs, date, affected periods, requester/approver and audit/version history. Existing operational teacher queries resolve date-specific ownership (`TimetableSubstitution` and `EffectiveTimetableQueries`).
- The inspected flow requires selecting a lesson, reviewing a candidate and confirming it. It does not establish automatic whole-day absence allocation or fair distribution of all actual cover assignments. No equal automatic standby generator was identified in the inspected timetable generation code.
- The inspected cover records/contracts do not include the pictured `ما تم تنفيذه في حصة الانتظار` execution narrative or the covering teacher's execution signature. Existing timetable export is not evidence of a source-faithful signed `QF71-B-2-1` daily register.

Recommendation, not an approved scope addition: finish D1–D3 duty first, then extend the existing standby/cover workflow with the official execution register, teacher acknowledgement/signature, and its document/archive needs after clarifying its rules. Reuse attendance and existing timetable cover instead of creating a second substitution system. Both features can react to the same staff absence while retaining separate assignment, fairness, execution and document records. Existing standby allocations/cover counts do not weight the owner's confirmed equal duty rotation.

## D2 implementation findings

Reopened actual originals before editing, especially report `4.jpeg` and letter `5.jpeg`, preserving their logical sections and private source files. Current execution snapshots retain original/substitute identity/signature positions, separate notes, student table order/selected-enrollment mappings, observations, added last departure and handover/footer.

Replaced the real `GetStudentTimelineAsync` empty seam with retained dismissal revision projection and added the same facts to the actual analytics profile. Extended existing manager operations/overview rather than creating a replacement dashboard. Existing student-record authorization remains intact.

Current messaging now has a narrow System origin with null human sender, trusted correlation and recipient-only SystemNotice threads; no login account impersonation. Hosted worker dispatch is independently verified through actual synthetic SQL/HTTP, including both surfaces and independent reads. Browser captures use mocked synthetic APIs and prove rendering/request behavior only. See [D2 verification](verification/d2.md) for implemented file boundaries, migration/refinements, regressions, real versus mocked results and limitations. D3 remains unstarted.

## D3 implementation findings - 2026-10-13

Reopened original roster/report/letter images and matched the recorded fingerprints before template composition. The report needs five original-duty columns, a substitute identity span plus its own signature/notes, separate six-column student categories, seven short-form numbered rows, observations and next-day handover. The last departure is adjacent to handover rather than replacing a source section. The letter keeps its last-student obligation and distinct manager/teacher positions; roster closure/exam rows do not manufacture attendance or signatures.

Existing QuestPDF/Amiri infrastructure supports Arabic shaping and embedded licensed fonts. The final renderer freezes bytes/provenance on the D2 document seam. Longer reports repeat table headers; the paired RTL roster keeps whole week groups with readable continuation pages. Independent Poppler rasterization and actual PDF.js previews were used for final visual inspection; source samples/signatures remain outside the repository. See [source/output evidence](verification/d3.md).

Narrow storage seams reuse IStorageProvider/GoogleDriveClient, trusted root metadata, reserved provider IDs and StoredFile/Version ledger. Dedicated duty kinds and SQL protection prevent evidence/visit impersonation and new/legacy bypasses. Fresh SQL tests establish trigger/constraint, race/lease and rollback behavior independently of provider doubles; actual Drive uncertainty verification still requires the designated synthetic external destination. No operational settings or unrelated workers were enabled.

## C26 — Archive controls, school-root navigation and library presentation (2026-10-14)

The owner explicitly requested this School Duty/storage follow-up. The two existing local letters had already reached verified archive completion; the manager's SQL-paged table retained its old presentation because it had no refresh timer. It now refreshes while visible, excluding busy preview/history actions and stopping after denied access. Enablement is presented **only in Storage administration**, with independent on/off switches for duty and visits. Duty-only delegates retain document/retry access without acquiring general storage administration. Signing/submission remains independent of external delivery.

The running local process had `SchoolFileStorage:ArchiveProcessingEnabled=false` from isolated D3 verification, despite the owner's per-school visit flag being true. This was a process stop, not an OAuth re-login side effect. The process now explicitly enables both archive processors, preserving owner-selected school flags: school18 duty/visits ON, school1 OFF. Startup initialization stays false and the operational schema stays at68 migrations. No credential/key/root/grant change or new migration.

Library setup includes the protected duty root through the same `DutyArchiveDriveService.EnsureRootAsync` identity, session lock, provider ID reservation and missing/moved-folder rules used by the worker. It never detaches/rebinds signed history through generic recovery consent. Year/term/actual-author/date descendants remain document-driven; no guessed year or teacher directories. Reapplication reuses the root and never enables either archive implicitly.

The Files entry shows School Library, Teacher Files, School Duty and Visit Archive under the verified authorized root. Each enters its existing scoped workflow. Duty and visit documents retain their distinct authorization/mutation boundaries; arbitrary Drive-only folders and the designated `D3_Test` verification area are not adopted into the operational library or counted as evidence. Deep library/review/file/year links are preserved; overview Library shortcuts now target the actual library folder directly.

A switchable committee presentation includes root branches, colored connected library sections, nested folder links, retained-ledger file counts including deeper descendants, search, collapse, keyboard focus, English labels, reduced motion and native fullscreen. Canonical template sections can be described as domains/standards; arbitrary existing folders are described as library sections/subfolders. No invented approvals/readiness percentages or sample personal data. The existing teacher storage/duty workspace is retained.

Provider metadata returned by a live list is reused only within the scoped request. Mutation outcomes, including lost responses, invalidate the changed ID. New request/verification boundaries reset the cache; connectivity checks stay fresh. An internal verified-metadata streaming seam removes redundant content metadata reads while legacy adapters keep their safe default. Scoped SQL grouping counts library files, excludes both archive source kinds and bounds tree depth/size. All new API responses retain ApiResponse<T>, thin controllers, Application authorization and Infrastructure queries.

See [storage/archive verification](../school-file-storage/verification/storage-duty-amendment.md), current read-only performance/readiness, bounded real Drive recovery and actual/synthetic browser captures. Owner appearance and physical-device review remain pending. No production deployment, destructive history removal, waiting-register feature, external messaging or general feature delegation.


## C27 / D-119 — Native school storage and confirmed removal (2026-10-15)

The owner's latest storage correction supersedes C26's feature shortcuts. Files has one School files entry and browses actual Drive folders/files inside the verified configured school root, including native/unindexed items. The committee map opens storage folders. The actual current school manager can review linked evidence/subtree impact, explicitly trash files/folders and recover recorded deletions. Rename/move remain protected for signed/system identities. Existing teacher/delegate scope remains enforced; duty delegation does not grant general storage administration.

Durable operations, expiring actor/school/root-bound confirmations, SQL locking, provider reconciliation and atomic evidence/legacy task updates preserve audit and review history. Removing a signed external copy never deletes or rewrites its snapshot/artifact/version identity/completed history; it records intentional deletion, retains local PDF access and excludes automatic recreation. Explicit recovery restores recorded descendants parent first and verifies names, parents and known hashes. Withdrawn evidence approvals are not automatically restored. Native managed signed reads reject altered bytes before streaming.

The owner explicitly authorized clearing the existing Al-Falah E2E Test School root, including D3_Test. All five original branches were trashed through real manager HTTP operations and the empty root verified. Fresh guarded isolated SQL then provisioned all four branches and delivered five real files through acknowledgement, signed report submission, visit approval, library upload and teacher upload. Sixteen live checks passed without messages. Test artifacts are cleaned through their retained isolated ledger, registered history is preserved and D3_Test remains trashed. No new migration, original credential/key/root/grant change, data reset or production deployment. Owner visual and physical-device acceptance remain pending. See the C27 verification report for current final recovery receipts, commands, exact test results and baseline limitations.
