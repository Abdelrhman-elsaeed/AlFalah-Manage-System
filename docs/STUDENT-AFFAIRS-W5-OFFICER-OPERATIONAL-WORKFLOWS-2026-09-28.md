# Student Affairs W5 — Officer Operational Workflows

Date: 2026-09-28

## Result

W5 is implemented without starting W6. `StudentAffairsOfficer` now lands on a real operational dashboard and can work the pending absence-excuse, Gate Pass, Classroom Entry Permit, Behavior/Academic notification, referral-assignment, automation-impact, and operational-record queues through school-scoped APIs and guarded Angular routes. W1–W4 behavior remains intact, including immediate Guardian↔Officer messaging from W4.

The implementation follows Controller → MediatR handler → repository → EF Core. Controllers remain thin, repositories retain `IQueryable`, list endpoints page on the server, mutations use the existing rowversion contracts, and no entity is returned directly by an API.

## Main changed and new files

- API: the Student Affairs dashboard, attendance, Gate Pass, notification, referral, summons, academic concern, behavior, morning delay, session delay, and recognition controllers under `backend/AlFalah.Api/Controllers/StudentAffairs/`.
- Attendance: `AttendanceContracts.cs`, `IAttendanceWorkflowRepository.cs`, new `GetPendingAbsenceExcusesQueryHandler.cs`, and `AttendanceWorkflowRepository.cs`.
- Dashboard: `DashboardQueryHandlers.cs`, `IStudentWorkflowRepository.cs`, and the real Officer projection in `StudentWorkflowRepository.cs`.
- Gate Pass: `ApproveGatePassCommandHandler.cs` and `GetGatePassesQueryHandler.cs`.
- Notifications: `NotificationCommandHandlers.cs` and `NotificationWorkflowRepository.cs`.
- Referrals: `ReferralContracts.cs`, `IReferralWorkflowRepository.cs`, create/assign/list/detail handlers, new safe-worker lookup handler, new `ReferralIdempotencyConflictException.cs`, and `ReferralWorkflowRepository.cs`.
- Automation review: `SummonContracts.cs`, new `GetAutomationImpactReviewsQueryHandler.cs`, `ReviewSummonAutomationImpactCommandHandler.cs`, and `SummonWorkflowRepository.cs`.
- Operational reads: new `OfficerOperations/IOfficerOperationalReadRepository.cs`, `OfficerOperations/Handlers/OfficerOperationalReadHandlers.cs`, and `OfficerOperationalReadRepository.cs`.
- Settings/RBAC: create/update/reset settings handlers and the Angular settings route guard.
- Domain/EF: `WorkflowEntities.cs`, `WorkflowEntityConfigurations.cs`, DI registration, model snapshot, and migration `20260928181703_AddW5OfficerReferralIdempotency` plus its generated Designer.
- Angular contracts/services: `daily-operations.models.ts`, `phase5.models.ts`, new `officer-operations.models.ts`, `daily-operations.service.ts`, and new `officer-operations.service.ts`.
- Angular UI: routes, shell navigation, Officer dashboard, absence-excuse management, and the new `officer-workflows` component (TS/HTML/CSS/spec).
- Tests: attendance/delay, attendance MediatR, Gate Pass, Social Worker/referral, request-contract, and dashboard/guardian test suites.

## Final Officer RBAC matrix

| Use case | Exact role | Required permission(s) | Authority boundary |
|---|---|---|---|
| Officer dashboard | `StudentAffairsOfficer` | `StudentAffairsDashboard.Officer` | Active school and current Officer user |
| Pending excuse queue | `StudentAffairsOfficer` | `Attendance.ViewStudents` + `Attendance.ReviewExcuse` | Excuse and attendance in active school |
| Accept/reject excuse | `StudentAffairsOfficer` | `Attendance.ReviewExcuse` | Pending record, school scope, rowversion |
| Entry Permit list/issue | `StudentAffairsOfficer` | `ClassroomEntryPermit.View` / `.Issue` | Active-school student and canonical lesson resolution |
| Gate Pass list/approve/reject | `StudentAffairsOfficer` | `GatePass.View` / `.Approve` / `.Reject` | Requested active-school pass and rowversion |
| Notification decision queue | `StudentAffairsOfficer` | `Notification.ApproveDispatch` and/or `.SuppressDispatch` | Behavior/Academic pending notifications only |
| Referral view/create/assign | `StudentAffairsOfficer` | `Referral.View` / `.Create` / `.Assign` | Active-school student, unassigned Open referral, safe worker option |
| Automation-impact review | `StudentAffairsOfficer` | `Summon.ReviewAutomationImpact` | Flagged active-school summon and rowversion |
| Operational lists | `StudentAffairsOfficer` | matching `MorningDelay`, `SessionDelay`, `Behavior`, `AcademicConcern`, or `Recognition.View` | Active-school rows only |
| Settings mutation | `StudentAffairsOfficer` | `StudentAffairsSettings.Manage` | Active-school aggregate, validation, history, rowversion |
| Messaging | `StudentAffairsOfficer` | W4 `Messaging.ViewOwn`/send contract | Participant-scoped GuardianStudentAffairs threads only |

Permission-only arbitrary roles are rejected in application handlers. Officer access does not grant attendance-roster submission, Instructor/Security acknowledgements, exit execution, or Social Worker case lifecycle actions.

## Object scope and privacy

| Surface | Enforcement |
|---|---|
| Tenant boundary | Every repository query includes the active `SchoolId`; foreign/out-of-scope objects resolve as not found where object existence is sensitive. |
| Excuses | The queue joins only the scoped attendance/excuse/student/guardian/attachment projection; PDF bytes are obtained through the authorized download endpoint and rendered as a sandboxed Blob URL. |
| Referrals | Officer list/detail/assignment responses remove case actions and resolution notes. Assignment is permitted only while the referral is unassigned and `Open`; an Officer cannot take over an in-progress or completed Social Worker case. |
| Worker lookup | Returns at most 25 minimal `{ userId, displayName }` options for active users with an active exact `SocialWorker` school role. Free IDs are revalidated server-side. |
| Operational records | Purpose-specific DTOs omit identity number, photo storage key, guardian data, case notes, and raw entities. |
| Messaging | Dashboard counts unread delivered threads without reading message bodies. Existing W4 participant/thread-type restrictions remain unchanged. |

## Canonical landing and navigation

The canonical `StudentAffairsOfficer` landing remains the guarded Officer dashboard, not Settings. Dashboard cards and shell navigation now link directly to:

- `/student-affairs/officer/excuses`
- `/student-affairs/gate-passes`
- `/student-affairs/officer/entry-permits`
- `/student-affairs/notification-approvals`
- `/student-affairs/officer/referrals`
- `/student-affairs/officer/automation-reviews`
- `/student-affairs/officer/operations`
- `/student-affairs/messages`

Each new route requires the exact Officer role and all permissions needed by the screen. Existing deep links were preserved.

## Dashboard projection

The dashboard returns active-school counts for:

1. pending absence excuses;
2. requested Gate Passes;
3. currently active Entry Permits;
4. pending Behavior decisions;
5. pending Academic decisions;
6. open referrals;
7. unassigned referrals;
8. automation-impact reviews; and
9. unread delivered Guardian↔Officer threads.

Counts are database-side, use a fixed bounded set of scalar projections, do not materialize full entity sets, and do not issue a query inside an item loop. Time-sensitive permit counts use injected server time. No message body or case note is present in the dashboard DTO.

## Absence-excuse workflow

`GET /api/v1/student-attendance/excuses/pending` supplies one deterministic, server-paged projection containing the attendance, student/class, guardian summary, notes, attachment metadata, and rowversions. This replaces the former client N+1 pattern.

The existing decision aggregate remains canonical:

- Accepting a pending excuse preserves the official absence record and changes attendance to `AbsentExcused`.
- Accepted excuses are excluded from the unexcused penalty metric during recalculation.
- Reject requires a reason and leaves attendance unexcused.
- Reviewer, decision time/reason, rowversion, recalculation, and the deduplicated guardian outbox notification remain atomic in the established W1/W2 workflow.
- Duplicate/stale decisions are blocked; stale versions map to 409.

The Officer UI is paged, prevents double submit, keeps the local reason on conflict, reloads the latest queue item, treats attachment names/notes as text, and previews PDFs through a sandboxed authorized Blob URL. No new Guardian upload workflow was added in W5.

## Entry Permit time resolution

W5 reuses the W3 backend rather than creating a parallel flow. The new Officer screen uses safe student search, active enrollment/class summary, issue/revoke forms, current/history paging, status, validity, target actual instructor, and acknowledgement state.

Issuance continues to resolve the unique effective published timetable and pinned Bell revision. Exact lesson start is inside; exact end, break, gap, non-study day, missing publication, and ambiguity fail closed. A valid substitution selects the substitute. The classroom/timetable entry/instructor snapshot is persisted and is not rewritten by later publication. No manual teacher field or Officer acknowledgement control exists.

## Gate Pass desired-exit-time behavior

The Officer queue remains Requested-only and server-paged/searchable. Approval now resolves at `RequestedExitAt`, never at button-click time. It requires a future requested exit, a half-open valid approval window containing that exit, one active published study-day lesson, matching enrollment, and an effective instructor. Break/gap/non-study/missing/ambiguous schedules fail closed; substitutions select the effective substitute. The approval persists timetable, entry, classroom, period, and instructor snapshots. Officer actions stop at approve/reject; teacher/security acknowledgement and exit execution remain separately authorized.

## Notification approval and suppression

The decision queue is restricted to pending `BehaviorIncident` and `AcademicConcern` notification facts. It has deterministic paging/search and excludes Session Delay and Recognition. Approval activates the original durable, deduplicated guardian notification and records actor/time/source decision without creating a second row. Suppression requires a reason, records actor/time/reason, preserves the original fact, and does not expose it to the guardian. Rowversion conflicts return 409 and concurrent/duplicate decisions cannot dispatch twice.

## Referral assignment and safe worker lookup

Manual create requires a caller-supplied `Idempotency-Key`. A SHA-256 payload fingerprint and the database unique key ensure same-key/same-payload replay returns the original referral while same-key/different-payload returns 409. The Officer UI retains one stable key across a retry.

The safe lookup derives active same-school exact Social Workers and the assign handler revalidates the selected user. Assignment is allowed only for an unassigned `Open` referral, uses rowversion, records actor/time plus a case action audit, and returns an Officer-safe DTO. It does not accept, start, resolve, close, or write internal case notes for the Social Worker.

## Automation-impact review

`GET /api/v1/summons/automation-impact-reviews` lists only `RequiresOfficerReview` summons and includes the source count snapshot, threshold snapshot, current metric count, source/review reason, and rowversion. Review records decision, rationale, actor/time, and a same-state history entry, then clears only the review flag. It never deletes the historical summon/referral or changes a Social Worker case state. Recalculation deduplication and historical preservation continue to use the existing automation ledger/domain model; repeated or stale review is a 409 conflict.

## Operational lists and settings

Academic concerns, behavior incidents, morning delays, session delays, and recognitions now have Officer list/detail handlers. Lists are active-school scoped, filter/search on the server, use stable descending event-time/ID ordering, and issue a fixed number of page/metric/referral queries rather than per-row queries. W6 mutations remain explicit deferred contracts.

Settings were not made the landing page. Existing read/history/reset contracts were retained; create/update/reset now additionally require exact `StudentAffairsOfficer` plus manage permission, while reads retain their intended Manager/Officer access. Bell/timetable timing was not duplicated in Student Affairs settings.

## Frontend behavior and coverage

- RTL responsive Officer dashboard with loading/error/empty states, safe refresh, real counts, and queue links.
- One reusable Officer workflow screen for permits, referral assignment, automation review, and operational lists.
- Safe student/worker lookup; no free student/teacher/worker authority fields.
- Server paging, operational-record search, explicit loading/empty/error states, mutation spinners, and no optimistic success.
- Inline validated revoke/assign/review forms; no browser `prompt` dialog.
- Existing dedicated Gate Pass and notification queues remain guarded and linked from the dashboard.
- Frontend tests cover queue loading, safe worker options, and stable referral idempotency in addition to the prior route, Gate Pass, excuse, notification, messaging, and dashboard suites.

## API status and error mapping

Touched Student Affairs controllers use `FromResponse`: reads/updates return 200, creation returns 201, validation/policy violations 400, missing authentication 401, missing exact role/permission 403, missing or out-of-scope objects 404, and concurrency/stale/idempotency conflicts 409. No touched endpoint intentionally returns HTTP 200 with `IsSuccess=false`.

## Migration and schema

Generated with EF tooling: `20260928181703_AddW5OfficerReferralIdempotency`.

The additive migration adds nullable `IdempotencyKey` (`varchar(200)`) and `IdempotencyPayloadHash` (`varchar(64)`) columns to `StudentReferrals` plus a filtered unique index on `(SchoolId, CreatedByUserId, IdempotencyKey)` for non-deleted rows. No data is removed or renamed. The Designer and snapshot were generated by EF and the migration was not applied to a production database.

`dotnet ef migrations has-pending-model-changes` reports: `No changes have been made to the model since the last migration.`

## Verification

- Backend Release build: passed, 0 errors; one pre-existing nullable warning at `SocialWorkerWorkflowTests.cs:41`.
- Backend Release tests: 684 passed, 0 failed, 0 skipped. This includes the W1–W4 regression suite and new W5 role, Gate Pass policy, pending-queue, referral idempotency/ownership, and request-registration coverage.
- Frontend production build: passed; only the existing dashboard/visit CSS budget warnings and third-party organization-chart selector warnings.
- Frontend tests: 187 passed, 0 failed.
- EF model/migration consistency: passed with no pending model changes; the existing global-query-filter/default-sentinel model warnings remain unchanged.
- `git diff --check`: passed; only Git's informational LF→CRLF working-tree notices were emitted.
- No commit or push was performed.

## Remaining deployment risks and deferred work

- Apply the additive migration and exercise the new filtered uniqueness, rowversion conflicts, and operational EF projections against the target SQL Server/staging database before production deployment; this workspace verification did not mutate an external database.
- W6 Guardian dashboard/upload/request/new-conversation expansion was not started. Existing earlier Guardian surfaces were preserved only where the shared W5 screen depends on them.
- W7 Security redesign/exit execution, W8 Social Worker CRM/Summons expansion, W9 Manager oversight/Office Hours UI, and later W10 work remain deferred.
- SignalR/WebSockets, urgent messaging override, broad audit viewing, Bell Schedule redesign, and unrelated Student Affairs redesign remain out of scope.

