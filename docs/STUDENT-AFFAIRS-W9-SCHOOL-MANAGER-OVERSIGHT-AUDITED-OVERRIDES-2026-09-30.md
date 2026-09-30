# W9 — School Manager Oversight & Audited Overrides

Date: 2026-09-30

## Goal and scope

W9 completes the school-manager surface without turning the manager into a Student Affairs Officer. It adds aggregate oversight, read-only settings access evidence, audited office-hours overrides, exceptional Gate Pass cancellation/annotation, metadata-only messaging audit, school-scoped role administration, and immediate token/session invalidation. The existing general landing at `/school-manager/dashboard` remains unchanged.

Baseline before W9 was commit `86e0750` (phase 8): Release build with zero warnings, 713 backend tests, 213 frontend tests, successful production build, clean EF model, and latest migration `20260929185322_AddW8SocialWorkerCaseWorkflow`.

## Architecture and principal files

The implementation retains `Controller → MediatR handler/service → repository → EF Core/database`. Controllers only check the coarse permission and dispatch; exact role, permission, authenticated identity, and active school are rechecked in handlers/services. Repositories own filtered/projection queries and persistence.

Principal backend changes:

- dashboard handler/controller and `StudentWorkflowRepository` aggregate projection;
- messaging/office-hours contracts, handlers, controller, and `MessagingWorkflowRepository`;
- Gate Pass contracts, manager handlers, controller, and `GatePassWorkflowRepository`;
- JWT/authentication validation and `UserSessionInvalidator`;
- school, user, and user-school-role services.

Principal frontend changes:

- manager office-hours, Gate Pass audit, and messaging-audit standalone pages;
- routes, exact-role permission guards, shell navigation, shared Phase 5/Gate Pass services and models;
- operational role options and reauthentication feedback in role-assignment screens.

## SchoolManager permission matrix

| Surface/action | Exact permission | Result |
|---|---|---|
| School oversight | `StudentAffairsDashboard.SchoolOversight` | Aggregate-only read |
| Student Affairs settings/history | `StudentAffairsSettings.View` | Read-only; mutation handlers remain Officer-only |
| Timetable/Bell Schedule | existing `Timetable.View` / `Timetable.Manage` | Existing subsystem and routes only |
| School office hours | `OfficeHours.ManageSchool` | Same-school instructor read/override |
| Gate Pass audit | `GatePass.ViewAudit` | Minimal paged audit DTO/history |
| Exceptional Gate Pass action | `GatePass.Override` | Active cancellation or Exited incident annotation only |
| Messaging audit | `Messaging.ViewAudit` | Metadata-only paged table |
| Role assignment | existing user-management permission plus service scope checks | Operational school roles only |

Injected permissions do not substitute for the exact role. Officer approval/rejection, teacher acknowledgement, security acknowledgement, and exit execution retain their original exact-role handlers.

## Oversight definitions and local date

The dashboard resolves the reporting date with `ISchoolLocalDateResolver` at the server instant from `TimeProvider`; an absent/ambiguous publication or invalid timezone fails closed. Attendance is joined to an active, date-valid enrollment in the same school/classroom/term. Only an attendance fact on that local date is counted; a missing record is not treated as present. `Present`, `Absent`, and `AbsentExcused` are mutually exclusive status predicates. Classroom rows are ordered by label then identifier and school totals are sums of those rows.

Threshold counts come from satisfied automation-trigger ledger occurrences, grouped by metric and distinct student, within the active term. Referral and summon sections group only by status, with non-identifying operational summon counts. The supplied server timestamp is returned as `GeneratedAt`.

The relational SQLite regression test exercises same-school/date filtering, cross-school exclusion, mutually exclusive totals, classroom aggregation, ordering, and freshness.

## Privacy contract

`SchoolOversightDashboardDto` contains only counts, classroom labels/identifiers, severities, and generation time. It contains no student identity, guardian identity, case/referral/summon identifiers, notes, evidence, attachments, storage keys, message subjects, or message bodies.

`MessagingAuditThreadDto` is a separate projection, not the participant conversation DTO. Allowed fields are thread identifier/type/status, participant **role labels**, timestamps, and aggregate message/delivery counts. It deliberately excludes subject, body, student/guardian/user identifiers, referral/case identifiers, notes, evidence, and attachment metadata. The manager UI has no content/detail link. A serialized-JSON regression assertion protects this boundary.

## Settings, timetable, and Bell Schedule boundaries

The existing Student Affairs settings page already disables editing and omits save/reset controls for the manager; server mutations continue to require exact `StudentAffairsOfficer` plus management permission. Timetable and Bell Schedule links use the existing permission-gated routes; W9 does not duplicate or broaden that subsystem.

## Office-hours override

The manager surface is `/student-affairs/office-hours/manage`. A new minimal same-school instructor option query returns only instructor profile id, display name, and subject. The existing teacher page remains dedicated to `ManageOwn`.

The override requires exact manager role, `OfficeHours.ManageSchool`, an active same-school instructor, a trimmed reason of 1–2000 characters, unique non-empty eligible slot keys, an effective date inside the active term, and the latest row version. Eligibility is rebuilt from the one effective published timetable, Bell Schedule periods/breaks, and teacher lesson/standby occupancy at save time.

The previous configuration becomes history; the new configuration uses `ManagerOverride`. Configuration, slots, before/after audit snapshots, actor, reason, server time, correlation id, and queued-message recalculation execute under the existing relational transaction. A 409 causes a refetch while preserving the manager draft; uncertain network failure is reconciled by a read and is never retried automatically.

## Gate Pass state matrix and false-exit handling

| Current state | Manager cancel | False-exit annotation |
|---|---:|---:|
| Requested | Yes | No |
| Approved | Yes | No |
| SecurityAcknowledged | Yes | No |
| Exited | No | Yes |
| Rejected / Cancelled / Expired | No | No |

Cancellation requires exact `SchoolManager` plus `GatePass.Override`, a trimmed reason up to 1000 characters, same-school aggregate, and current row version. It appends the existing audited transition and preserves prior acknowledgement history. Guardian cancellation remains limited to an owned `Requested` pass; an Officer with an injected override permission is denied.

False exit uses an append-only `Exited → Exited` transition annotated with `MetadataJson.Kind = FalseExitIncident`. It preserves `Exited`, `ExitedAt`, and the original transition, creates no second exit event, and performs no attendance mutation. It records actor, exact manager role, reason, server time, correlation id, and concurrency guard.

The manager query is independently paged/projected and includes only the pass id, minimal student number/display name, classroom label, requested/approved windows, current status, last transition time, exception indicator, and row version. Guardian pickup/contact and case data are excluded. The UI route is `/student-affairs/gate-passes/audit` and exposes history plus permission/status-aware exceptional actions.

## Role assignment and session invalidation

School managers are restricted to their active school, cannot change themselves or another manager, and cannot assign `SchoolManager`, `MainManager`, or `SuperAdmin`. Supported operational roles are Secretary, Moderator, Instructor, Guardian, StudentAffairsOfficer, SocialWorker, and SecurityGuard. Instructor and Guardian assignment require their active same-school domain profiles; W9 does not synthesize profiles or guardian links.

Every affected user (including the prior manager/secretary during reassignment) receives a new Identity `SecurityStamp`, and every active unexpired refresh token is revoked in the same DbContext unit of work as the role mutation. Access tokens now carry `security_stamp`; JWT validation loads the active user and rejects a missing/stale stamp. Thus old access tokens fail immediately, old refresh tokens fail, and a new login receives only current role/permission claims. The actor's session is not invalidated unless the actor's own assignment changed. Frontend success messages state that the target user must sign in again.

## Concurrency, transactions, and HTTP behavior

No W9 mutation accepts school or actor identity from the client. Office-hours and Gate Pass changes use opaque row versions and server timestamps. Stale versions and invalid state transitions map to HTTP 409, missing/out-of-scope objects to scoped 404, and role/permission failure to 403. UI actions require confirmation, validate the reason, disable double submission, retain drafts on conflict, and reconcile before any human retry.

## Tests added

- exact-role/permission tests for oversight, messaging audit, manager Gate Pass audit, and instructor-option lookup;
- school-local-date/server-instant forwarding test;
- relational SQLite oversight aggregation/scope/date/order test;
- metadata-only messaging JSON regression test;
- JWT security-stamp claim regression test;
- MediatR registration checks for all new handlers;
- exact-role/permission route matrix tests for all three new manager pages.

Existing W1–W8 tests remain enabled and unchanged in strength.

## Migration decision

No migration was created. Existing `TeacherOfficeHourAudit`, Gate Pass append-only transitions/metadata, Identity `SecurityStamp`, and refresh-token storage cover W9. `dotnet ef migrations has-pending-model-changes` reports no model changes.

## Verification results

Final verification is recorded after the implementation:

- `dotnet build backend/AlFalah.slnx -c Release`: succeeded, 0 warnings, 0 errors.
- `dotnet test backend/AlFalah.slnx -c Release --no-build`: 721 passed, 0 failed, 0 skipped.
- `npm test -- --watch=false --browsers=ChromeHeadless`: 216 passed.
- `npm run build -- --configuration production`: succeeded.
- EF pending-model check: clean (`No changes have been made to the model since the last migration.`).
- `git diff --check`: succeeded.

The production build retains the pre-existing two CSS budget warnings and three PrimeNG selector-parser warnings. The EF command retains the documented global-query-filter relationship warnings and the `Visit.ExperienceVersion` sentinel warning; none is introduced by W9.

## Assumptions and deferred work

The current model has no separate required domain profile for StudentAffairsOfficer, SocialWorker, or SecurityGuard, so no artificial prerequisite was invented. “False exit correction” is implemented strictly as an audit annotation because the documents do not define a safe operational rollback. Full cross-role browser E2E remains W10.

No commit or push was performed.
