# Student Affairs W3 — Instructor Current Context, Quick Actions, and Acknowledgements

Date: 2026-09-28

Status: Complete; stopped before W4

Inputs:

- `STUDENT-AFFAIRS-ROLES-TIMETABLE-REVIEW-PLAN.md`
- `STUDENT-AFFAIRS-W0-BASELINE-AUDIT-2026-09-28.md`
- `STUDENT-AFFAIRS-W1-SHARED-TIME-RBAC-FOUNDATION-2026-09-28.md`
- `STUDENT-AFFAIRS-W2-SECRETARY-TIMETABLE-DATA-ATTENDANCE-2026-09-28.md`

## 1. Outcome

W3 is complete. The Instructor workspace now obtains its live lesson, effective teacher, classroom, and roster from the W1 `ICurrentLessonResolver`; it has no manual-classroom fallback and exposes only Behavior, Academic Concern, Session Delay, and Recognition actions. Every action is authorized again in its application handler using exact Instructor role plus the action permission and is constrained to the server-resolved current roster.

Teacher acknowledgement was hardened for Gate Passes and implemented for Classroom Entry Permits. Both use a snapshotted target instructor, server time, row-version concurrency, school scope, and fail-closed transition rules. The Instructor workspace now displays the two pending acknowledgement queues without exposing Officer or guardian-private content.

The five Classroom Entry Permit contracts are fully implemented through Controller → Handler → Repository → EF Core. Issue, list, detail, teacher acknowledgement, and revoke no longer return deferred HTTP 501 responses. Issuance snapshots the live published timetable context and emits reliable outbox effects for notifications and repetition automation.

No migration was created. W4 was not started.

## 2. Final Instructor RBAC matrix

| Operation | Exact role | Required permission | Object scope |
|---|---|---|---|
| Current context / top priority / current roster | `Instructor` | `TeacherQuickAction.View` | Active school and current effective instructor |
| Create Behavior Incident | `Instructor` | `Behavior.Create` | Student in current resolved roster and matching current entry |
| Create Academic Concern | `Instructor` | `AcademicConcern.Create` | Student in current resolved roster and matching current entry |
| Create Session Delay | `Instructor` | `SessionDelay.Create` | Student in current resolved roster and matching current entry |
| Create Recognition | `Instructor` | `Recognition.Create` | Student in current resolved roster; current entry is derived server-side |
| Acknowledge Gate Pass | `Instructor` | `GatePass.AcknowledgeTeacher` | Snapshotted target instructor only |
| View Entry Permits | `Instructor` | `ClassroomEntryPermit.View` | Permits snapshotted to that instructor only |
| Acknowledge Entry Permit | `Instructor` | `ClassroomEntryPermit.Acknowledge` | Snapshotted target instructor only |
| Issue Entry Permit | `StudentAffairsOfficer` | `ClassroomEntryPermit.Issue` | Active school and active dated student enrollment |
| Revoke Entry Permit | `StudentAffairsOfficer` | `ClassroomEntryPermit.Revoke` | Same-school permit only |
| Officer list/detail | `StudentAffairsOfficer` | `ClassroomEntryPermit.View` | Active school |
| Guardian list/detail | `Guardian` | `ClassroomEntryPermit.View` | Currently linked students only |

The handlers reject role-only and permission-only callers. Injecting an Instructor permission into Secretary, School Manager, or another role does not authorize the operation. `TeacherQuickActionOverride` is not used as a fallback when live context resolution fails.

## 3. Current-context resolution contract

All Instructor current-context entry points call `ICurrentLessonResolver.ResolveForInstructorAsync` with the active school, authenticated user, and server `TimeProvider` instant. The returned contract includes:

- resolution kind and reason;
- school-local time and timezone;
- published Bell schedule revision ID;
- published timetable ID and revision;
- timetable entry and period sequence;
- half-open `startsAt` / `endsAt` interval;
- classroom and subject;
- original and effective instructor;
- active substitution ID;
- actually permitted quick actions.

The repository uses the resolver's timetable, Bell revision, entry, period, local date, and effective-instructor identity; it does not resolve Bell periods independently. Break, gap, outside-hours, non-study-day, missing/ambiguous publication, overlapping periods, invalid timezone, and draft-only conditions return an explicit fail-closed resolution with no actionable roster. The current-roster endpoint additionally proves that the requested entry is the currently resolved entry; an arbitrary or stale entry receives not-found scope semantics.

At a date-specific substitution, only the effective substitute receives the active period and roster. The displaced original instructor cannot use the entry. Exact start is active and exact end is inactive through the W1 half-open boundary.

## 4. Roster object scope

The roster query is constrained in EF to:

- the caller's active school;
- the current resolver-selected classroom;
- the resolver's school-local date;
- the published timetable's academic year and semester;
- active, non-deleted student and classroom records;
- an active academic term containing the local date;
- an active dated enrollment whose enrollment/withdrawal interval contains the local date.

Withdrawn, inactive, out-of-date, cross-school, wrong-term, and wrong-year enrollments are excluded before projection. Quick-action scope resolution repeats these trusted constraints and requires the effective instructor profile, current timetable, entry, and period. No client classroom, term, teacher, school, or historical-entry value can widen scope.

## 5. Quick-action semantics

### Behavior Incident

- Created only for the current roster and current entry.
- Starts with `GuardianDispatchDecision.PendingOfficerDecision`.
- Snapshots school, term, classroom, reporter, entry, actor, and server audit time.
- Emits the existing behavior domain event and preserves existing metric/automation processing.
- The response and UI say that the item is awaiting Officer decision; they do not claim guardian delivery.

### Academic Concern

- Uses the same canonical current-lesson and roster proof.
- Stores the current `SchoolTimetableEntryId`.
- Starts with `GuardianDispatchDecision.PendingOfficerDecision` and leaves dispatch ownership with the Officer.
- Does not claim guardian delivery before that decision.

### Session Delay

- Snapshots current timetable, entry, period, classroom, academic term, and reporting instructor.
- Starts with the model's truthful `GuardianNotificationStatus.Pending` and emits the existing outbox event.
- The UI renders the delivery status returned by the server; it does not equate an outbox record with delivery.

### Recognition

- Resolves the current lesson even though the request contract has no entry ID.
- Requires the effective instructor and student membership in the current roster.
- Preserves the existing recognition projection and does not create a mandatory guardian-notification claim.

For all four actions, a supplied occurrence instant may not be more than five minutes in the future and must fall in the current lesson's half-open interval. A context boundary, wrong entry, break/gap/non-study state, displaced instructor, or roster mismatch fails before persistence. Angular prevents double submission and freezes a stale dialog while preserving entered form data.

## 6. Gate Pass teacher acknowledgement

Teacher acknowledgement is an append-only receipt transition and does not change `GatePass.Status`.

- Exact Instructor role plus `GatePass.AcknowledgeTeacher` is required.
- The authenticated Instructor profile must equal `CurrentInstructorProfileId`, which remains the approval-time snapshot even after later timetable publication.
- The Gate Pass must be `Approved`, require a target teacher, and be inside `[ApprovedWindowStartsAt, ApprovedWindowEndsAt)`.
- A matching row version is mandatory; stale state returns 409 without mutation.
- A repeated acknowledgement returns the existing result and does not append another receipt.
- Break/gap/outside-hours approvals with no target instructor cannot receive a fabricated teacher acknowledgement.

The pending queue excludes already acknowledged, not-yet-active, and expired windows and is filtered to the exact target teacher in SQL.

## 7. Classroom Entry Permit lifecycle

### Issue

Only a Student Affairs Officer with `ClassroomEntryPermit.Issue` may issue. The request requires an active student, active dated same-school enrollment, non-empty reason, `ValidUntil > ValidFrom`, and a server issue instant inside `[ValidFrom, ValidUntil)`.

The handler selects exactly one current published Bell/timetable candidate, derives the school-local date, resolves the student's enrolled classroom through `ResolveForClassroomAsync`, and requires `ActiveLesson`. The persisted snapshot contains academic term, classroom, published timetable, timetable entry, and effective target instructor. A live substitution therefore targets the substitute, and later republishing cannot rewrite the historical snapshot.

The initial state is `Issued`. An equivalent active permit is defined by school, student, timetable entry, and validity interval. The repository repeats the equivalence check inside a serializable relational transaction so racing requests do not commit duplicate permits/outbox effects. A persistence race reloads the winning equivalent record.

### List and detail

All role/object-scope predicates, search/filter predicates, ordering, and pagination remain server-side:

- Officer: active-school permits;
- Instructor: permits whose snapshotted target profile belongs to the caller;
- Guardian: permits for students with a currently active same-school guardian link;
- every other role: deny.

Cross-school and out-of-scope detail IDs return not-found semantics without revealing record existence. API responses are DTO projections, not EF entities.

### Teacher acknowledgement

The only normal transition is `Issued → AcknowledgedByTeacher`. It requires the exact target Instructor, `ClassroomEntryPermit.Acknowledge`, a valid row version, and `serverNow < ValidUntil`. Actor and acknowledgement time are server-generated. Duplicate acknowledgement is idempotent. Revoked or effectively expired permits cannot be acknowledged.

### Revoke and expiry

Only a Student Affairs Officer with `ClassroomEntryPermit.Revoke` can revoke. A non-empty reason and current row version are mandatory. A valid revocation records actor, server time, and reason; repeated, expired, or already revoked transitions fail without rewriting the record.

There is intentionally no broad expiry writer or scheduler. Persisted status remains an audit fact, while list/detail projection returns effective `Expired` once `ValidUntil <= serverNow` unless the permit is already `Revoked`.

## 8. Notification and metric effects

`ClassroomEntryPermitIssuedEvent` is captured in the existing transactional outbox after the database-generated permit ID is available. The processor:

- creates deduplicated in-app notifications for active guardians linked on the issue date;
- creates a deduplicated notification for the snapshotted target teacher;
- rebuilds the student's `ClassroomEntryPermit` term metric from canonical permit rows;
- uses `SchoolStudentAffairsSettings.ClassroomEntryPermitThresholdPerTerm` and includes that threshold in the policy snapshot;
- uses the automation trigger ledger to prevent duplicate threshold referral/summon effects.

The create response does not pretend asynchronous work has completed. Later projections return the actual stored guardian delivery/read status when the outbox notification exists; before processing, the delivery projection is null. In-app notifications are marked Delivered only when the dispatcher actually creates the recipient's in-app record.

## 9. API and error behavior

The five Entry Permit contracts are registered with MediatR and removed—only those five—from `DeferredStudentAffairsRequests`. Controllers remain thin and all touched W3 endpoints now use the common response mapper:

- 201 for successful creates;
- 200 for reads and successful transitions;
- 400 for invalid input or transition;
- 401 for missing authentication/active-school context;
- 403 for missing exact role or permission;
- 404 for missing or out-of-object-scope records;
- 409 for stale row version or persistence/concurrency conflict.

No touched W3 controller returns HTTP 200/201 with `IsSuccess=false`, and the Entry Permit controller no longer returns 501.

## 10. Instructor frontend

`/student-affairs/teacher` retains its Instructor role and permission guards. Its workspace now provides:

- an explicit current-context header with local time, timezone, classroom, subject, period, start/end, timetable revision, and substitution state;
- explicit server resolution reasons instead of a manual classroom picker;
- current-roster-only Behavior, Academic, Delay, and Recognition actions; no direct Referral action;
- separate validated forms with associated labels, keyboard-focus behavior, RTL layout, and narrow-viewport styles;
- automatic context refresh at `endsAt`, plus focus and visibility-return refresh with timer/subscription cleanup;
- background refresh without workspace flicker;
- stale-context submission blocking while preserving the draft;
- removal of a selected student who leaves the refreshed roster, with an explanatory message;
- disabled quick actions while context is refreshing or cannot be revalidated after an error;
- explicit initial loading, empty roster, no-lesson resolution, 401/403/404/409, and retryable network/server-error behavior;
- pending Gate Pass and Entry Permit panels with student, classroom, window, reason, status, row version, and guarded acknowledgement actions;
- 409 acknowledgement handling that reports the conflict and reloads without automatic retry or false success.

The component tests cover the missing fallback/referral controls, all four accessible action buttons, current student/entry payload, double-submit protection, stale context with retained draft, real no-lesson state, acknowledgement conflict reload, refresh network failure, initial 403, focus refresh, and boundary refresh.

## 11. Verification

| Command | Result |
|---|---|
| `cd backend; dotnet build AlFalah.slnx -c Release --no-restore` | Passed; 0 errors, 0 warnings in the final required run |
| `cd backend; dotnet test AlFalah.slnx -c Release` | Passed: 677/677, 0 failed, 0 skipped |
| Focused W3 backend Gate Pass / quick-action / Entry Permit suite | Passed: 52/52 |
| `cd frontend; npm run build` | Passed; only the pre-existing CSS-budget and PrimeNG selector warnings |
| `cd frontend; npm test -- --watch=false --browsers=ChromeHeadless` | Passed: 187/187, 0 failed |
| Focused Instructor workspace component suite | Passed: 8/8 |
| `git diff --check` | Passed after report creation; line-ending conversion notices only, no whitespace errors |

One nullable warning in the unchanged `SocialWorkerWorkflowTests.cs:41` appeared when a focused run forced recompilation; the final required incremental build reported zero warnings. No W3 source introduced a compiler warning. The Angular warnings are unchanged from W0–W2: the two existing component CSS budgets and three third-party PrimeNG organization-chart selectors.

The full backend run includes the W1 resolver/timetable tests and W2 school-data, attendance, excuse, Zajel, and timetable regression tests; all remain green.

## 12. Migration decision

No migration or model-snapshot change was created. The existing `ClassroomEntryPermits` table already contains validity, term/classroom/timetable/entry/target-teacher snapshot fields, acknowledgement/revocation audit fields, and `RowVersion`. Existing notification, metric, automation-ledger, and outbox tables are reused.

## 13. Risks and retained decisions

- Effective expiry is a read projection rather than a persisted background transition. This is deliberate and avoids hidden query writes or a new W3 scheduler.
- Entry Permit notification and metric effects are asynchronous through the existing outbox. A synchronous create response may therefore have no delivery or recalculated-metric projection yet; it never claims completion.
- The production relational duplicate boundary is serializable and the handler has retry-winner recovery. Current automated tests prove deterministic replay and effect idempotency at handler/outbox level; there is no multi-connection SQL Server race test in this repository.
- Existing opaque generated type names in a few legacy contracts/events were not renamed because that would be an unrelated compatibility change.

## 14. Deferred work

- W4: Office Hours persistence/derivation and Messaging delivery/release policy.
- W5: Officer operational dashboard and Entry Permit issue/manage screen.
- W6 and later: conduct, academic, delay, and recognition review handlers; automation administration; broader cross-role E2E scenarios.
- Guardian Entry Permit UI, Security UI redesign, Social Worker expansion, and direct teacher referral/case-management UI remain out of W3.

W4 has not started.

## 15. Files changed by W3

### New

- `backend/AlFalah.Application/StudentAffairs/Permits/IClassroomEntryPermitWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/Permits/Handlers/ClassroomEntryPermitHandlers.cs`
- `backend/AlFalah.Domain/Events/ClassroomEntryPermitEvents.cs`
- `backend/AlFalah.Infrastructure/Repositories/ClassroomEntryPermitWorkflowRepository.cs`
- `backend/AlFalah.Tests/StudentAffairs/ClassroomEntryPermitWorkflowTests.cs`
- `frontend/src/app/features/student-affairs/teacher-top-priority/teacher-top-priority.component.spec.ts`
- `docs/STUDENT-AFFAIRS-W3-INSTRUCTOR-CURRENT-CONTEXT-QUICK-ACTIONS-2026-09-28.md`

### Modified — API and Application

- `backend/AlFalah.Api/Controllers/StudentAffairs/AcademicConcernsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/BehaviorsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/ClassroomEntryPermitsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/RecognitionsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/SessionDelaysController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/TeacherStudentAffairsController.cs`
- `backend/AlFalah.Application/StudentAffairs/DTOs/Teacher/TeacherStudentAffairsContracts.cs`
- `backend/AlFalah.Application/StudentAffairs/DeferredStudentAffairsRequests.cs`
- `backend/AlFalah.Application/StudentAffairs/GatePasses/Handlers/AcknowledgeGatePassByTeacherCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/GatePasses/IGatePassWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherActions/Handlers/CreateAcademicConcernCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherActions/Handlers/CreateBehaviorIncidentCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherActions/Handlers/CreateRecognitionCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherActions/Handlers/CreateSessionDelayCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherActions/Handlers/TeacherActionHandlerSupport.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherActions/ITeacherActionWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/Handlers/GetTeacherCurrentContextQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/Handlers/GetTeacherPeriodRosterQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/Handlers/GetTeacherTopPriorityQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/ITeacherContextRepository.cs`

### Modified — Domain and Infrastructure

- `backend/AlFalah.Domain/Entities/StudentAffairs/WorkflowEntities.cs`
- `backend/AlFalah.Infrastructure/Automations/StudentAffairsAutomationRuleEngine.cs`
- `backend/AlFalah.Infrastructure/Automations/StudentAffairsOutboxProcessor.cs`
- `backend/AlFalah.Infrastructure/DependencyInjection.cs`
- `backend/AlFalah.Infrastructure/Notifications/StudentAffairsNotificationDispatcher.cs`
- `backend/AlFalah.Infrastructure/Repositories/GatePassWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/StudentWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/TeacherActionWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/TeacherContextRepository.cs`

### Modified — tests

- `backend/AlFalah.Tests/Security/SecretaryAttendanceAuthorizationTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/GatePassAndMessagingMediatRTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/GatePassWorkflowTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/StudentAffairsRequestContractTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/StudentWorkflowAndGuardianTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/TeacherActionsAndSummonsWorkflowTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/TeacherTopPriorityHandlerRegistrationTests.cs`

### Modified — frontend

- `frontend/src/app/core/models/student-affairs-dashboard.models.ts`
- `frontend/src/app/core/services/student-affairs-dashboard.service.ts`
- `frontend/src/app/features/student-affairs/teacher-top-priority/teacher-top-priority.component.ts`
- `frontend/src/app/features/student-affairs/teacher-top-priority/teacher-top-priority.component.html`
- `frontend/src/app/features/student-affairs/teacher-top-priority/teacher-top-priority.component.css`
