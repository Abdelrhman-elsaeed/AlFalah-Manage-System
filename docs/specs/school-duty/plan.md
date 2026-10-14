# Technical Plan — School Duty

**Status:** D1–D3 implemented and independently verified; [D3 delivery](verification/d3.md) records final contracts, evidence and pending external/owner/physical acceptance. The owner-confirmed [dismissal-lateness amendment](dismissal-lateness.md) and [source references](references.md) govern implemented data/UI and official PDFs. Earlier phase-only implementation notes below are historical.
**Architecture:** Existing modular monolith; .NET 8, Angular, SQL Server/EF Core, current PDF and Drive infrastructure.

## Integration boundaries

```text
School-duty HTTP controller
  → application services / existing MediatR conventions where appropriate
    → duty repository + staff attendance reader + term/timetable reader
      → EF Core / existing database

Attendance save
  → durable school/date reconciliation signal in the same SQL transaction
    → duty reconciliation service

Teacher acknowledgement / report submission
  → frozen signed document snapshot + durable archive intent in SQL
    → archive processor → existing PDF infrastructure + storage provider
```

Use thin controllers, separate request/response DTOs and `ApiResponse<T>` with meaningful HTTP status codes. The project constitution's response envelope takes precedence over the architecture skill's generic `ResponseViewModel` example. Put business decisions in Application services and data access in Infrastructure repositories. Existing attendance code is a scoped integration seam, not a reason to refactor the entire module.

## Proposed domain model

Names are provisional; relationships and invariants must survive implementation refinements.

| Record | Important fields and responsibility |
|---|---|
| `DutyPlan` | SchoolId, AcademicTermId, associated operational timetable/setup, date bounds, frozen timing/timezone reference, status, revision, audit; one active operational plan per school/term |
| `DutyParticipant` | PlanId, school-scoped InstructorProfileId/UserId mapping, order, eligibility/effective interval, exclusion reason and audit; separate from lesson availability |
| `DutyClosedDate` | PlanId, date/range and closure reason; minimal duty-specific non-operating dates |
| `DutyAssignment` | SchoolId, PlanId, LocalDate, immutable original teacher identity, current effective teacher, revision, execution state and manual-protection marker; one operational assignment per school/date |
| `DutyAssignmentChange` | Append-only before/after identities and revisions, both affected assignment IDs when exchanged, attendance evidence, reason, actor and timestamp; pending compensation state belongs to the exchange trail |
| `DutyAcknowledgement` | AssignmentId/revision, authenticated actual teacher, acknowledged timestamp, immutable letter snapshot, signature asset/hash and template version; historical acknowledgements survive reassignment |
| `DutyReport` / `DutyReportRevision` | Assignment/revision, author, draft revision/concurrency, submitted-at, last departure, observations, correction reason and previous submitted revision link; retain submitted versions |
| `DutyReportLine` | Report revision, category Late/Violation, required selected StudentId/EnrollmentId, frozen name/class, time/action/notes; late lines link a stable dismissal-lateness fact/revision; child rows cannot cross schools |
| `StudentDismissalLateness` (provisional name) | School/student/enrollment, duty date/assignment revision, author, observed time, dismissal timing reference, departure when known, action/notes, active/corrected/retracted status and audit; one active school/student/date fact |
| Dismissal guardian notice intent | Stable fact/revision/recipient correlation, dashboard-notice and internal-message delivery results, attempts/read state and correction linkage; durable delivery separate from report/archive state |
| System message provenance (additive D2 messaging seam) | Server-owned system origin, source fact/revision and recipient-scoped thread/message correlation; visible `النظام` / `System`, no personal impersonation or browser-forged origin; preserve existing human sender FKs/history |
| `DutyReconciliationOperation` | Unique school/date/attendance-change evidence, state/attempts; durable processing signal from staff attendance |
| `DutyArchiveOperation` / artifact link | School, assignment revision, report revision if applicable, Letter/Report kind, frozen snapshot/hash, state, reserved provider ID, lease/retry data and StoredFile/Version IDs |

A user can have multiple duty dates over a term; a duty date has one effective teacher. A date has many historical assignment revisions; each acknowledgement belongs to one actual teacher and revision. A report has many retained revisions; only the current draft is mutable. Do not store the original teacher and the substitute as unrelated display strings.

Keep school-scoped composite keys/FKs, uniqueness for operational dates, acknowledgement identity, report revision and archive intent, and concurrency tokens on mutable records. Active-plan uniqueness and school/date uniqueness must also prevent duplicate plans caused by clones or overlapping terms. All new rows are additive; no historical timetable/storage data is rewritten.

Store identities plus frozen display values in signed snapshots. Resolve InstructorProfile → UserId explicitly for attendance/signatures and validate current school membership on every action. Avoid assuming profile IDs and user IDs are interchangeable.

## Services and transaction boundaries

- `DutyPlanService`: validation, roster/closure setup, deterministic preview, publication and future regeneration.
- `DutyAssignmentService`: manager manual decisions, revision validation and atomic date exchanges.
- `DutyReconciliationService`: read current staff facts, resolve eligible next assignments, honor manual/locked records, apply one exchange, retain unresolved obligations.
- `DutyReportService`: own draft, acknowledgement, validation, submission and retained corrections.
- Focused dismissal-lateness application service: explicit confirmation/correction, scoped roster validation and fact + notice-intent transaction; existing delivery/statistics/student-record readers project the same fact.
- `DutyDocumentService`: official roster/letter/report rendering from frozen data; no scheduling decisions.
- `DutyArchiveProcessor`: reliable storage delivery using existing provider abstractions and duty-specific authorization.

Repositories expose scoped query/projection/transaction capabilities; they do not rank teachers or decide compensation. Read projections should be no-tracking and paginated; batch candidate attendance/membership instead of querying inside a loop. Avoid introducing a generic event bus, policy engine or scheduling framework for this one feature.

An automatic/manual two-date exchange locks or concurrency-checks both assignments, the plan/queue revision and relevant compensation trail, then commits changes/audit/notifications together. A conflict never commits half the exchange. Reconciliation uses current effective assignments, so subsequent absence works on the already exchanged queue.

Add the attendance signal to `AttendanceService.SaveSheetAsync` within the transaction saving the sheet. Record only relevant changed facts, including corrections, and process after commit. A durable worker retries failed reconciliation; a write-path fast attempt can reduce delay but cannot be the sole guarantee. Attendance retains its existing secretary-only management policy. Re-read facts during reconciliation and before acknowledgement/submission; never rely on browser absence flags.

Preload/freeze the authenticated teacher's signature through existing trusted server mechanisms, then revalidate assignment revision inside the acknowledgement/submission transaction. Snapshot, audit and archive intent are committed together. A snapshot must retain signature bytes/assets with a hash, not a URL to a mutable current account signature. Use safe existing signature handling; do not fetch an arbitrary user-supplied URL.

### D2 student facts and projections

Use a minimal whole-school term roster reader for the duty author, rather than limiting lookup to classes they teach or granting broad Student Affairs permission. Search/grade/class/pagination are server enforced. Confirmed lateness fact, audit and durable guardian intent commit together with assignment/request/concurrency checks; report drafts reference the fact. Reuse existing notification infrastructure after auditing durability. Correct/retract with retained history and truthful notice state. Delivery failure cannot remove the incident or signed report.

Extend `SchoolManagerOperationsDto`/existing overview and student-record/profile seams with scoped count/list/history projections. The inspected `GetStudentTimelineAsync` returns an empty placeholder; D2 must supply the relevant real projection. Preserve morning-delay/attendance/penalty metrics. The owner confirmed both guardian-dashboard notice and a system-authored message in existing internal Messages; no external provider is required.

Current `ConversationMessage` requires a real `SenderUserId`/FK, thread types are human-oriented, and the public send handler requires an authenticated human participant with messaging permission. Add a narrow trusted system-origin model/delivery seam and DTO/UI support; do not call that handler as an impersonated teacher or merely rename a human sender. Browser requests cannot claim system origin. Preserve existing participant/recipient isolation, human office-hour rules and unread/receipt behavior; system fact notices dispatch promptly without the teacher's office-hour gate. Link one dashboard notice to the corresponding internal message; track/retry each surface separately and suppress any duplicate generic new-message alert for that same fact. Correction history retains both artifacts. See the amendment for detailed invariants and verification.

## Proposed API surface

Base route: `/api/v1/school-duty`. School context is resolved from the current session; any optional support selector must follow existing approved school-scope policy. No general global-admin bypass is introduced in this feature.

| Method / route | Behavior / access |
|---|---|
| GET `/context?termId=` | Capabilities, term/timing/plan and operating/storage states; minimal scoped projection |
| PUT `/plans/{id}/draft` | Manager roster, exclusions and closures as one revision-checked edit |
| POST `/plans/preview` | Manager validates setup and receives deterministic distribution with fairness/delta; support first setup without an existing plan |
| POST `/plans/publish` | Manager creates/publishes the validated plan with input digest/revision and request ID |
| GET `/assignments?termId=&from=&to=&teacherId=&status=` | Manager list, or school-teacher schedule-only projection; pagination |
| POST `/assignments/{id}/change` | Manager direct replacement/exchange/exception resolution with reason, affected revisions and request ID |
| GET `/assignments/{id}` | Capability-specific day details; report/signature fields own-or-manager only |
| POST `/assignments/{id}/acknowledge` | Actual teacher's saved-signature acknowledgement; revision and request ID |
| GET `/students?termId=&search=&gradeId=&classroomId=&page=` | Minimal whole-school active roster/class lookup for current duty author/manager; server filters, classroom grouping and pagination |
| PUT `/assignments/{id}/report/draft` | Own current draft with concurrency token; other-teacher edits denied |
| POST `/assignments/{id}/report/submit` | Own signed submission with revision/request ID |
| POST `/assignments/{id}/report/correction` | Manager authorizes a retained correction with reason; actual teacher edits/re-signs the resulting draft |
| POST `/assignments/{id}/dismissal-lateness` | Actual duty author confirms selected-student fact + durable guardian intent with request/revision validation; no autosave dispatch |
| POST `/dismissal-lateness/{id}/correction` | Authorized correction/retraction with reason and retained fact/notice history |
| GET `/dismissal-lateness?termId=&from=&to=&classroomId=&page=` | Actual manager scoped count/list drill-down; own author-specific status through own-duty projection |
| Existing manager overview / student record APIs | Extend authorized projections with dismissal counts/names and history; duty lookup does not grant full profile access |
| GET `/plans/{id}/roster.pdf` | Official schedule-only roster |
| GET `/assignments/{id}/documents/{kind}` | Own/manager frozen letter/report or specific retained revision; authenticated stream |
| POST `/assignments/{id}/archive/retry` | Duty manager retries an identified artifact/revision; no browser-selected provider destination |

Return server-derived capabilities. Use 403 for authorization failure, 404 for unavailable in-scope objects, 409 for stale revisions/idempotency conflicts, and 400 for invalid business input. Mutation retries with the same request ID return the same result; reusing an ID with different content conflicts. Errors include stable machine codes rather than branching on Arabic strings. DTOs expose no provider IDs, credentials, raw signature assets of other users, or unrelated staff attendance.

Frontend route intent: `/timetable?tab=duty&date=YYYY-MM-DD` plus a focused own-duty view within the panel. Do not let the outer `Timetable.View`/role guard become the future bottleneck for duty-only delegation; centralize panel-specific capability evaluation without weakening the existing timetable guard in v1.

## Initial authorization and future feature assignment

Add focused database catalog permissions (provisional names): `Duty.View`, `Duty.Manage`, `Duty.SubmitOwn`, and `Duty.ArchiveRetry`. Manager management requires actual current school manager identity and active membership in addition to current DB permissions. Teacher submission always requires actual assignment ownership. Route/button hiding is only presentation; use the same live authorization on APIs and document reads.

No current `TimetableEditorGrant` or `StorageDelegation` confers Duty.Manage. The duty manager can retry duty artifacts without being granted general storage administration. A narrow internal archive writer must validate the school root and duty destination, never impersonate the teacher to upload under their ordinary grant.

After D3, design the separate `توزيع الصلاحيات` feature with a finite feature catalog and school/user grants distinguishing View, Manage and Own execution where relevant. Grant/revoke affects backend access and visible tabs, supports active school users of appropriate roles, retains audit, and is controlled by the actual manager. Do not implement this schema/UI now or grant broad permissions as a shortcut.

## Official document and storage integration

Owner-approved human-readable hierarchy (2026-10-13):

```text
{authorized school root}/
  المناوبات/
    {academic year}/
      {term}/
        {actual teacher display name}/
          {yyyy-MM-dd}/
            خطاب المناوبة - {teacher} - {yyyy-MM-dd}.pdf
            تقرير المناوبة - {teacher} - {yyyy-MM-dd}.pdf
```

Maintain stable SQL folder identities and school/teacher/date keys; duplicate display names receive a readable disambiguator. Use actual duty date, not upload/retry date. Source is protected generated duty output, not a teacher evidence upload. Add DutyArchive folder/source kinds and extend every relevant storage protection path, filtered index/check constraint and legacy browse/download boundary; never masquerade as VisitArchive or create a fake VisitId.

Reuse `IStorageProvider`, stored-file/version ledger, root containment, ID reservation, streaming downloads and proven lease/retry/recovery patterns. Visit archive orchestration is visit-specific; extract only a small shared primitive if reuse actually requires it. Retain protected artifacts and correction versions, with no direct library rename/delete/move path that invalidates provenance. Duty files do not automatically satisfy an evidence requirement or readiness count.

Archive key includes school, assignment revision, document kind and submission revision. Create one durable operation before external I/O; reserve the provider ID and byte hash, reconcile uncertain success against that identity, and mark completed only when the ledger links verified output. Folder provisioning is also idempotent. A provider timeout is not proof of a missing/deleted file.

Use per-school duty archive readiness/enablement rather than enabling unrelated visit archives or every school's external writes. Preserve current options, credentials, Google root/grants and legacy artifacts. Signing is a local business commit even when storage is unavailable; expose pending/configuration state and permit authenticated PDF generation from the same immutable snapshot. Acknowledgement can therefore archive before a report exists.

## PDF fidelity

- Reproduce the source letter obligation and report labels, including `بدلاً عنه الأستاذ`, original and actual signature fields, year/term/date and school identity.
- Use current school settings for administrative headings/logo/manager identity, frozen at signing; do not hardcode the sample school, historical year or manager.
- Add last-student departure from the client conversation. Preserve the report's next-day handover instruction without adding an unrequested manager signature.
- Use actual Arabic font shaping, right-to-left tables, repeating page headers, sensible page breaks and overflow pages. A signature belongs to the teacher who actually performed the action.
- Store a canonical Gregorian DateOnly key; display the school's established Arabic/Hijri date convention consistently after checking existing formatting. Do not infer historical Hijri dates from image years or the host timezone.
- Follow [references.md](references.md), reopening the original `4.jpeg` and `5.jpeg` during implementation. Visually compare real letter/report/roster PDFs: exact report section order, table divisions, labels, columns and signature positions are mandatory. Mobile editors may improve controls without changing the logical division.

## Rollout and verification

The owner's 2026-10-13 request authorizes D1 implementation and isolated migration/runtime verification using its documented defaults. Stop after D1. Creation of the initial planning kit did not itself authorize implementation; the later implementation request supersedes that planning pause. D2/D3, live Drive writes, production deployment and general feature assignment remain outside this execution scope.

Validate additive migration on an isolated database with actual constraints and existing data preserved. Activate only for an explicitly configured school after attendance, signature, PDF and storage checks. Record rollback as disabling new duty entry/write processing while retaining plans, signed snapshots and stored artifacts; do not remove history to roll back.

## D2 implemented contracts and refinements

The D2 request explicitly authorizes C1-C17 and relevant engineering defaults. It supersedes the historical D1-only rollout scope above. Thin `DutyExecutionController` actions use `ApiResponse<T>`; `DutyExecutionService` owns execution/validation/transactions and `DutyExecutionRepository` owns scoped EF reads/writes. `DutyNoticeProcessor` is the narrow internal communication seam, invoked by a scoped hosted worker.

The provisional acknowledgement/report/revision/line outline is implemented as one mutable per-author `DutyReportDraft` with typed content JSON, immutable Letter/Report `DutySignedDocument` snapshots and retained prior-document links. SQL protects immutability/scope. Emergency change history additionally freezes displaced unsigned drafts, including repeat ownership. This avoids duplicate independent line/acknowledgement truth while retaining all source sections, selected enrollment identity, rows and stable fact revisions. Archive intents are local `PendingD3` rows only.

`StudentDismissalLateness`, append-only `DismissalLatenessRevision`, `DismissalNoticeBatch` and per-recipient results provide one fact per school/student/date, correction/retraction history and independent dashboard/message delivery results. Existing D1 serializable school application locks protect facts, audit, intents and request receipts. System messages use nullable human sender, constrained trusted provenance and correlated recipient-only closed threads; public human sends cannot forge origin. Existing human office hours are unchanged. Generic notifications and Messages both revalidate current effective guardian scope.

Manager statistics query active facts, and real student timeline/profile projections query retained revisions. Full-record authorization is preserved, including the manager aggregate-only boundary. Roster search projects only active term enrollment identity/name/class/number/grade. Draft concurrency, signing ownership, school-local timing, explicit empty categories and reasoned correction/re-signature are validated server-side. Existing D1 reconciliation/regeneration respects execution activity locks.

Additive migration `20261013135411_AddSchoolDutyD2` is verified only on fresh guarded synthetic SQL databases. Previous-document pointers use scalar EF FKs plus SQL scope triggers to avoid a composite metadata cycle. Embedded/local branding bytes are frozen; unavailable/remote logo state is recorded without network fetches. See D-108 and [verification](verification/d2.md). Stop after D2; archive processors/PDFs/storage operations remain D3.

## D3 authorization and implementation status — 2026-10-13

The owner explicitly authorized D3 through prompts/d3-implementation.md, including C1–C17 and the reviewed archive hierarchy. D3 implementation and independent isolated verification are delivered; see [verification/d3.md](verification/d3.md). Historical D1/D2-only authorization is superseded for this phase. Operational migration, production deployment, external archive writes outside a designated synthetic destination, and later features remain outside this authorization. External Drive verification, owner visual acceptance and physical-device review remain pending.

## D3 implemented seams

`DutyDocumentService` owns current-school/actual-author/manager access, integrity validation, immutable preparation, roster revision binding and duty-specific readiness/retry. `DutyArchiveProcessor` and `DutyArchiveDriveService` own delivery decisions, uncertainty reconciliation and trusted hierarchy validation. Scoped `DutyDocumentRepository` owns SQL leases, session/ledger locks and persistence; `DutyPdfRenderer` owns QuestPDF composition. Thin file/JSON controllers retain existing response conventions. D2 submission/signature/internal-notice transactions are unchanged.

`DutyPdfArtifact` freezes PDF bytes/hash, renderer version and provenance. Existing signed documents and archive intents carry the durable operation/reservation/lease/version links; no alternate signing system or roster archive obligation is added. Genuine DutyArchive folder/source kinds have scoped FKs, filtered unique indexes and immutability/protection triggers. Two logical letter/report files retain immutable signed correction versions. Existing provider/streaming abstractions are reused through narrow real-delivery and protected-ID seams.

API routes are under `/api/v1/school-duty`: assignment document list, signed-document local/archived PDF, revision-bound roster PDF, duty-manager retry and enablement; readiness is returned with the document collection/enablement result. Local PDF reads survive disabled/disconnected storage. Global processing and per-school enablement default false. Pending D2 intents may prepare local artifacts without external writes; a letter is independent of its report. Completion requires verified provider output plus ledger commit; non-network providers produce TestVerified only.

The additive D3 migration was verified on fresh guarded LocalDB, including a twice-applied idempotent D2-to-D3 script and sentinel preservation. No operational migration occurred. Rollback disables new external processing and retains all snapshots/facts/artifacts/reservations/history; destructive Down is rejected. Reproducible commands, source/PDF comparisons, actual check totals and safe activation gates are in D3 verification. Stop after D3.
