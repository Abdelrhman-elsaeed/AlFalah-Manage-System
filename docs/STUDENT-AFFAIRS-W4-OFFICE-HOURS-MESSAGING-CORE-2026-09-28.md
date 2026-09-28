# Student Affairs W4 — Office Hours and Messaging Core

Date: 2026-09-28

## Result

W4 is implemented without starting W5. Office hours are derived exclusively from the single effective published timetable and its pinned Bell revision, persisted as a versioned configuration aggregate, reconciled after publication/substitution events, and used by a durable messaging release policy. The existing Angular Office Hours and Messages screens now consume the truthful aggregate and message delivery state.

## Main changed and new files

- Domain: `MessagingEntities.cs`, `SchoolTimetable.cs`, and new `MessagingEvents.cs`.
- Application contracts/handlers: `MessagingContracts.cs`, `IMessagingWorkflowRepository.cs`, Office Hours/Create/Send handlers, and new `GetGuardianTeacherOptionsQueryHandler.cs`.
- API: `OfficeHoursController.cs` and `ConversationsController.cs`.
- Infrastructure: `MessagingWorkflowRepository.cs`, messaging EF configurations/DbContext, outbox processor, notification dispatcher, timetable publication event, and migration `20260928171519_AddW4OfficeHoursMessagingCore`.
- Angular: Phase 5 models/service, Office Hours settings, Messages chat, and model tests.
- Tests: `StudentAffairsModelTests.cs` and `GatePassAndMessagingMediatRTests.cs`.

## Final Office Hours RBAC

| Operation | Exact role | Permission | Object scope |
|---|---|---|---|
| Read/save `/me` | Instructor | `OfficeHours.ManageOwn` | Active instructor profile for current user and school |
| Read teacher | SchoolManager, own Instructor, or linked Guardian | `OfficeHours.View` | Same school; Guardian only for a teacher of an actively linked student |
| Override teacher | SchoolManager | `OfficeHours.ManageSchool` | Active instructor in the same school; non-empty reason |

Object denial is intentionally indistinguishable from not found. The client cannot provide instructor identity for `/me`, eligibility, times, timetable identity, or Bell revision as authority.

## Office Hours aggregate and derivation

`OfficeHoursAggregateDto` contains configuration/instructor/term identity, timetable and Bell provenance, effective range, one aggregate rowversion, source/update actor/time, status reason, and candidate slots. Each slot has a stable key, day, Bell sequence, actual local boundaries, eligibility/selection/conflict state, source, and provenance.

Candidate stable keys bind timetable ID/revision, Bell revision, day, and Bell period. Derivation fails closed for zero/ambiguous publication, invalid timezone, or zero/ambiguous active term. It uses study days and effective day overrides, excludes breaks, overlapping structures, lessons, standby, gaps, and all before/after-school time. Occurrence calculation uses school time, half-open `[start,end)` boundaries, term/effective ranges, day overrides, non-study days, invalid/ambiguous DST boundaries, and date-specific substitutions. Date/substitution data is loaded before the occurrence loop.

## Persistence, history, override, and reconciliation

- A `TeacherOfficeHourConfiguration` header owns the aggregate rowversion and selected slot rows.
- A save re-derives eligibility, validates stable keys and effective date, checks the aggregate token, closes the previous current version, retains history, writes the new version/audit, and recalculates queued messages in one relational transaction.
- Empty selections are valid and retain a configuration-level token.
- Manager override uses the same safety rules, records `ManagerOverride`, and persists actor, reason, before/after snapshots, timestamp, and correlation ID.
- Timetable publication emits a durable `TimetablePublishedEvent`; existing `TeacherTimetableChangedEvent` handles affected substitution teachers.
- Reconciliation keeps valid selections with updated provenance, marks removed/new lesson/standby selections conflicted with a reason, never silently deletes history, audits the change, excludes conflicts from delivery, and recalculates pending messages. Replaying reconciliation is state-idempotent.

## Messaging RBAC and object scope

| Thread | Allowed participant roles | Delivery policy |
|---|---|---|
| GuardianTeacher | Guardian, Instructor | Guardian messages follow office hours; routine teacher replies require an active office-hour occurrence |
| GuardianStudentAffairs | Guardian, StudentAffairsOfficer | Immediate |
| GuardianSocialWorker | Guardian, assigned SocialWorker | Immediate |

Every list/detail/messages/send/read/close path is school-scoped, participant-scoped, and checked against the active role allowed for the thread type. Arbitrary injected permissions, SchoolManager audit access, and unrelated officers/workers do not expose bodies. Reply targets must belong to the same thread, and closed threads reject sends.

The safe Guardian teacher endpoint derives recipients only from active Guardian links, active enrollment classrooms, and Lesson teachers in the unique published timetable. It accepts no free recipient user ID.

## Delivery policy, scheduling, and truth semantics

- Guardian → Teacher inside a selected valid occurrence is delivered immediately; outside it is persisted as `QueuedUntilOfficeHours` with the next eligible instant, or `null` when no valid future occurrence exists.
- The exact start is inside and the exact end is outside.
- Teacher routine replies outside office hours are rejected without creating a message; there is no automatic urgent bypass.
- Officer and Social Worker threads remain immediate.
- Pending receipts have `DeliveredAt = null`. Mark-read updates only already-delivered receipts and cannot promote a pending receipt. Unread counts exclude unreleased messages, and recipients cannot list a delayed message until release.
- Paging is deterministic by message time and ID.

Each releasable message has a durable outbox event. The leased outbox worker revalidates the current occurrence at execution time, reschedules when configuration/timetable changed, delivers receipts once, and dispatches a deduplicated notification. Failed external processing retries without corrupting message state. A message with no current valid occurrence remains pending with a null next time and is reconsidered after configuration/publication/substitution reconciliation.

## Idempotency

Send and initial-message requests require an idempotency key. The database has a filtered unique constraint on `(SchoolId, SenderUserId, IdempotencyKey)` plus a payload hash. Same key/same payload returns the stored result; same key/different payload is a 409. Message, receipt, schedule event, and event payload are committed atomically. Create-thread plus initial message is also transactional.

## Frontend behavior

- Office Hours renders server-returned days dynamically, Bell period/time, selected/source/conflict states, no-publication/ambiguity reason, and Manager override notice. It sends only stable keys, effective date, and aggregate rowversion; disables invalid slots/double submit; preserves local selection and refetches after 409.
- Messages uses server delivery state, disposition, and next time after polling/focus refresh. A queued message remains visibly pending across reloads, a null next time explicitly says it is waiting for a valid configuration, retries retain the key, merge-by-ID prevents duplicate appends, rejected teacher replies retain the draft, and closed threads are read-only. Bodies remain plain text; there is no urgent control or free instructor-ID field.

## API and error behavior

Student Affairs controllers use `FromResponse`: success reads/updates return 200 and creation returns 201; invalid input/policy returns 400; missing authentication 401; missing role/permission 403; absent/out-of-scope object 404; stale aggregate/concurrency/idempotency conflicts 409. Controllers remain thin and no EF entity or `IQueryable` crosses the API boundary.

## Migration

Generated with EF tooling: `20260928171519_AddW4OfficeHoursMessagingCore`.

It additively introduces the configuration/audit tables; aggregate rowversion; slot stable provenance/conflict fields and nullable legacy configuration link; message idempotency hash/key, next release, release time, and outbox event identity; filtered uniqueness for current configuration and idempotency; and pending/due indexes. Existing Office Hour rows remain readable because the new aggregate FK is nullable and legacy stable keys default safely. No Designer or snapshot file was edited manually.

`dotnet ef migrations has-pending-model-changes` reports: `No changes have been made to the model since the last migration.`

## Verification

- Backend Release build: passed; one pre-existing nullable warning in `SocialWorkerWorkflowTests.cs:41`, no new warning.
- Backend tests: 680 passed, 0 failed, 0 skipped. This includes the prior W1/W2/W3 suite plus W4 handler/model constraint coverage.
- Frontend production build: passed; only the existing CSS budget/third-party selector warnings.
- Frontend tests: 185 passed, 0 failed (the repository currently discovers 185 tests; the plan's 187 baseline was not reproducible from this checkout).
- EF model/migration consistency: passed.
- `git diff --check`: passed; only Git's informational LF→CRLF working-tree notices were emitted.

## Remaining constraints and deferred work

- Apply the migration and exercise worker timing against the target SQL Server/staging queue before production deployment; no database was mutated during W4 implementation.
- The existing polling/focus-refresh transport remains in place; SignalR/WebSockets are intentionally deferred.
- W5 Officer workflows/dashboard were not started.
- W6 Guardian dashboard and full new-conversation wizard remain deferred; only the safe backend teacher lookup was added.
- Manager Office Hours UI, broad audit viewer, urgent override, and cross-role end-to-end browser suites remain deferred to their planned later workstreams.

