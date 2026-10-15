# Technical Plan — School Duty

**Status:** D1–D3 implemented and verified, including the designated synthetic real Drive gate; [D3 delivery](verification/d3.md) and [workspace amendment](verification/workspace-amendment.md) record final contracts/evidence and pending owner/physical acceptance. The owner-confirmed [dismissal-lateness amendment](dismissal-lateness.md) and [source references](references.md) govern implemented data/UI and official PDFs. Earlier phase-only implementation notes below are historical.
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

## Subsequent local migration/startup authorization - 2026-10-14

The owner requested necessary migrations and startup for manual testing. The configured local Development database is now at D3 after a verified backup and preservation/CHECKDB checks; API5264/Angular4200 run with automatic startup initialization and external duty/visit processing disabled. This authorization supersedes earlier no-original-migration statements only for this local operation. Existing credentials/roots/grants/settings/keys are retained; no production or external-provider activation occurred. See [local operation](verification/local-manual-testing.md), D-111.

## C18 independent navigation implementation - 2026-10-14

Expose the existing SchoolDutyComponent directly at `/school-duty`, guarded by school manager/instructor role, active school and existing Duty.View/Duty.Manage capability. A top-level sidebar entry reuses existing menu tokens/controls and accessible label/current-page state. TimetableWorkspaceComponent now hosts lessons only; a compatibility guard redirects old duty query links before timetable loading, preserving their parameters/fragment and re-evaluating query changes. Retain existing unsaved duty work checks and pre-signature teacher entry; old signature return links remain compatible. No backend/schema/permission or Drive change is required. [Verification](verification/navigation.md), D-112.

## C19 implementation amendment (2026-10-14)

Extend the existing settings JSON with optional permanent teacher IDs and normalize active teacher participation in Application services. Derive trusted study weekdays/timing revision via the scoped repository and reject stale previews. Do not mutate retained plan history, execution snapshots or PDFs. Frontend replaces opt-in selection/manual weekdays with all-by-default exclusions, linked calendar and equal-height date controls. No schema migration or external activation. [Verification](verification/settings-amendment.md).

## C20–C23 implementation and verified provider seam (2026-10-14)

Route SchoolDutyComponent as a retained parent with focused guarded child sections; shared injected feedback service owns duty error presentation via native dialogs. Scoped Application services remain the authority for live role/school/author/revision and future-date validation, using the frozen plan time zone. Main frontend execution loads current context independently of archive term and ignores arbitrary future selection. Archive inspection/correction/explicit overdue completion preserve frozen artifacts and server drafts; receipt replay re-evaluates ownership.

GET/PUT `/api/v1/school-duty/archive/enablement` are thin service adapters. Manager readiness probes the actual trusted root; enable validates processing/readiness, pause preserves artifacts/intents. No new migration/schema or general storage grant is required. Existing IStorageProvider/GoogleDriveClient and duty processor handle actual delivery. The standalone live harness starts no workers, rejects existing SQL before migrations, clones encrypted connection into synthetic SQL, uses existing keys without automatic generation, persists trusted destination reservation and confines writes to user-designated D3_Test/run root. Lost response and SQL rollback recover by reserved identity without duplicate uploads/ledger versions. A separate isolated API/browser host verifies authenticated archived/roster reads, including local artifact preparation and verification bookkeeping, with business submissions and provider writers disabled. Original school flags stay false; process-level manual duty readiness is true, Visit processing false. [Evidence and non-destructive rollout](verification/workspace-amendment.md), D-114–115.

## C24 administrator amendment — 2026-10-14

Implemented the owner's administrator-only refinement; the approved ordinary teacher workspace stays intact. Setup navigation is outside its content card, with separate visible buttons and a highlighted active button; the continuous green bar was explicitly rejected in the follow-up. Teacher management and rotation order show ten entries per page with search and existing permanent/date exclusion, restore and move actions. Fairness and change-history pages also show ten entries, avoiding an unbounded administrative page. Calendar and preview layouts use the available width, bounded lists and readable RTL spacing. Refreshing untouched setup/search/stage navigation no longer triggers a browser unsaved-changes prompt; actual changed settings, pending operations and unsaved teacher drafts retain protection.

The administrative archive uses a SQL-paginated index with ten teacher/date/revision groups per page: responsibility letter on the RTL right, daily report on the left, independent truthful availability/delivery states, authenticated preview/download/retry and retained history. Teacher/date/term filters are school scoped; changing filters resets the page. Unsigned future duties are omitted by default, while retained signed future artifacts remain readable. Explicit date filters can inspect future assignments. Rows select the newest letter/report for their assignment/actual-author/revision; earlier correction documents remain in history. The query loads at most twenty latest documents/artifacts for a page rather than all signed history. Listing uses the last committed verification state, without a provider request for every row; existing archive reconciliation and explicit archived downloads verify the provider. Access denial clears historical/preview data.

DutySettings now has nullable DismissalAt in its existing versioned JSON. Null retains the final timetable-period default. An explicit school-local time is reviewed/published using the existing revision/digest lock and regeneration range. Append-only SettingsTimingPreserved events freeze earlier base times for past, signed/locked, manual/exchanged or range-protected assignments when the plan setting or calendar revision changes. An explicit daily timing exception still wins. Timing preservation is not an exception that bypasses lateness validation. Existing frozen signed snapshots/PDF bytes are not rewritten; later reports freeze their actual resolved time. This is additive JSON/history behavior, with no schema migration.

The general account dashboard, outside the School Duty workspace, shows today's effective teacher and the preceding seven calendar dates with an explicit range. It is shown for an active school account with live database Duty.Manage, regardless of primary manager identity/role label. The backend checks active user/current-school membership and live database permission before and after reads. Signing still requires the actual effective teacher; teacher-only viewers cannot read the administrative archive/dashboard. Existing permission administration is reused; no grants, delegation UI or general storage rights are created. The dashboard refreshes while visible and clears stale data when school context changes or access is revoked.

[Actual verification, principal captures, reproducible commands and remaining gates](verification/admin-amendment.md), D-116. No operational migration, school activation, new provider write, credential/root/grant change or later feature. Rollback disables new duty processing while retaining all facts/snapshots/artifacts/history. Owner final appearance and physical-device/keyboard acceptance remain pending.


C24 owner placement follow-up (2026-10-14): the duty summary belongs at the **end of the general account dashboard**, after its header, tools and entire routed content. The shell renders it after router-outlet/routed component with top spacing, preserving all existing duty scope/role checks. Actual browser checks passed at390/768/1440 for the manager, plus the authorized officer and ordinary-teacher isolation. [Placement results](verification/dashboard-footer-screens/browser-results.json). No backend, migration, configuration or provider write change.

## C25 — School Duty administrator delegation and dashboard width (2026-10-14)

The owner explicitly authorizes **School Duty only** delegation, superseding its earlier deferral for this feature. General feature delegation remains outside scope. The summary stays at the general dashboard page end and now matches the existing dashboard's1440px maximum width and centered margins, including1920px screens.

A manager-only /school-duty/permissions page groups account selection by role, prioritizing moderator grouping while allowing every active role in the current school. Existing usernames disambiguate duplicate display names. The manager grants/revokes duty administration; delegated accounts receive all six administrative duty pages, actual signed-document/archive access and their general dashboard summary. Only actual school managers with active current-school membership and live Duty.Manage may delegate. Delegates cannot grant/revoke others, administer general storage or sign for a different assigned teacher. Role-inherited management is shown separately and cannot be revoked through this page. Inactive/out-of-school targets and stale mutations are rejected; explicit revoke confirmation, failures/selection retention, ten-row admin paging and audit history are provided.

Reuse existing UserSchoolRoles/RolePermissions/AuditLogs. A fixed SchoolDutyAdministrator role has exactly Duty.View and Duty.Manage, provisioned transactionally only on the first explicit authorized grant, protected by a shared role lock. No global UserRoles assignment/base role change, schema/migration, credential/root/grant change or automatic operational grant occurs. Per-school serialized transactions plus the existing unique membership index prevent duplicate grants/audits; the retained UpdatedAt version prevents stale regrant/revoke overwrites. Audit and membership commit/rollback together. A grant alone is insufficient after ordinary school membership/user/role deactivation. Auth/me derives Duty permissions from live active school membership without changing the account's primary role or other permission families; page refresh exposes the new sidebar/dashboard. API authorization/revocation is immediate even for an old token.

[Verification](verification/delegation-amendment.md):82 backend (74 real SQL),146 Angular,16 actual isolated authenticated HTTP checks, actual UI at390/768/1440/1920 and existing teacher/workspace regression. Backend remains Controller → Application service → scoped Infrastructure repository. No general permission-framework refactor, waiting-register or external messaging feature. Owner appearance/physical acceptance remains pending. Stop after this authorized School Duty amendment.

## C26 — Archive controls, school-root navigation and library presentation (2026-10-14)

The owner explicitly requested this School Duty/storage follow-up. The two existing local letters had already reached verified archive completion; the manager's SQL-paged table retained its old presentation because it had no refresh timer. It now refreshes while visible, excluding busy preview/history actions and stopping after denied access. Enablement is presented **only in Storage administration**, with independent on/off switches for duty and visits. Duty-only delegates retain document/retry access without acquiring general storage administration. Signing/submission remains independent of external delivery.

The running local process had `SchoolFileStorage:ArchiveProcessingEnabled=false` from isolated D3 verification, despite the owner's per-school visit flag being true. This was a process stop, not an OAuth re-login side effect. The process now explicitly enables both archive processors, preserving owner-selected school flags: school18 duty/visits ON, school1 OFF. Startup initialization stays false and the operational schema stays at68 migrations. No credential/key/root/grant change or new migration.

Library setup includes the protected duty root through the same `DutyArchiveDriveService.EnsureRootAsync` identity, session lock, provider ID reservation and missing/moved-folder rules used by the worker. It never detaches/rebinds signed history through generic recovery consent. Year/term/actual-author/date descendants remain document-driven; no guessed year or teacher directories. Reapplication reuses the root and never enables either archive implicitly.

The Files entry shows School Library, Teacher Files, School Duty and Visit Archive under the verified authorized root. Each enters its existing scoped workflow. Duty and visit documents retain their distinct authorization/mutation boundaries; arbitrary Drive-only folders and the designated `D3_Test` verification area are not adopted into the operational library or counted as evidence. Deep library/review/file/year links are preserved; overview Library shortcuts now target the actual library folder directly.

A switchable committee presentation includes root branches, colored connected library sections, nested folder links, retained-ledger file counts including deeper descendants, search, collapse, keyboard focus, English labels, reduced motion and native fullscreen. Canonical template sections can be described as domains/standards; arbitrary existing folders are described as library sections/subfolders. No invented approvals/readiness percentages or sample personal data. The existing teacher storage/duty workspace is retained.

Provider metadata returned by a live list is reused only within the scoped request. Mutation outcomes, including lost responses, invalidate the changed ID. New request/verification boundaries reset the cache; connectivity checks stay fresh. An internal verified-metadata streaming seam removes redundant content metadata reads while legacy adapters keep their safe default. Scoped SQL grouping counts library files, excludes both archive source kinds and bounds tree depth/size. All new API responses retain ApiResponse<T>, thin controllers, Application authorization and Infrastructure queries.

See [storage/archive verification](../school-file-storage/verification/storage-duty-amendment.md), current read-only performance/readiness, bounded real Drive recovery and actual/synthetic browser captures. Owner appearance and physical-device review remain pending. No production deployment, destructive history removal, waiting-register feature, external messaging or general feature delegation.
