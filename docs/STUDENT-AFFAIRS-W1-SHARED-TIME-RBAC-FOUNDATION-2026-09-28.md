# Student Affairs W1 — Shared School-Time + RBAC Foundation

Date: 2026-09-28

Status: Complete; stopped before W2

Inputs: `STUDENT-AFFAIRS-ROLES-TIMETABLE-REVIEW-PLAN.md` and `STUDENT-AFFAIRS-W0-BASELINE-AUDIT-2026-09-28.md`

## 1. Outcome

W1 now has one school-time resolution boundary, shared by Teacher current/top-priority context and Gate Pass approval. The resolver is pinned to a unique published timetable and its Bell revision, uses the revision's school timezone, distinguishes every required non-lesson state, applies date-specific substitutions, and fails closed for missing, ambiguous, or invalid-timezone publication.

The same workstream also closes the identified Guardian/Messaging authorization holes, removes broad operational permissions from School Manager and Social Worker, contains all 30 handlerless contracts behind explicit HTTP 501 responses, establishes the seven-role landing matrix, and repairs the development QA fixture. No migration was created.

## 2. Canonical current-lesson design

The Application boundary is `ICurrentLessonResolver`, backed by `IBellScheduleRepository.GetPublishedCandidatesAsync` and `ICurrentLessonEntryRepository`.

Resolution flow:

1. Select published timetable candidates effective for the instant's school-local date and active academic term.
2. Require exactly one candidate. Zero becomes `NoPublishedSchedule`; more than one becomes `AmbiguousPublishedSchedule`.
3. Convert the instant with the Bell revision's `SchoolTimeZoneId`. Unknown/invalid timezone fails closed as `NoPublishedSchedule` with an explicit reason.
4. Resolve the effective study-day definition, including default/day-specific periods and breaks.
5. Apply half-open lesson boundaries `[start, end)` and classify `Break`, `Gap`, `OutsideSchoolHours`, or `NonStudyDay` when no lesson can be active.
6. Query lesson entries for the pinned timetable, day, period, and requested classroom/instructor scope.
7. Overlay the latest date-specific `TimetableSubstitutionMovement`, preserving both original and effective instructor identities.
8. Return `ActiveLesson` only for one unambiguous match.

An active result contains school-local date/time, timezone, academic year/semester, Bell revision, timetable/revision, entry, period sequence and boundaries, classroom, original instructor, effective instructor, and substitution identity. Bell data remains in the existing timetable model; no parallel JSON or Student Affairs settings copy was added.

## 3. Unified consumers

- `GetTeacherCurrentContextQueryHandler` and `GetTeacherTopPriorityQueryHandler` now use the canonical resolver. An original teacher displaced by a substitution receives no current lesson; the substitute receives the lesson and roster lookup.
- `ApproveGatePassCommandHandler` now resolves the student's active classroom through the canonical resolver and snapshots the effective teacher.
- Active lessons retain Teacher Acknowledgement behavior.
- `Break`, `Gap`, and `OutsideSchoolHours` approvals are allowed without a teacher, entry, or period snapshot. Their transition/audit note records `TeacherAcknowledgementNotRequired:<resolution>` and the resolution reason; the emitted event has a null teacher so no unrelated teacher can be notified.
- Missing/ambiguous publication, non-study day, enrollment mismatch, and invalid timezone fail safely.
- Gate Pass retains its guardian-link, active-enrollment, window, status, permission, and optimistic-concurrency checks.

## 4. RBAC and object scope

- Guardian detail, timeline, and guardians queries verify an active dated `StudentGuardian` link and return not-found semantics for unlinked students.
- Messaging send and close require a live conversation participant in both handler and repository mutation queries.
- Guardian conversation creation requires a linked student. Guardian–Teacher targets must be an active instructor on that student's classroom in the unique current published timetable/term. Guardian–Social Worker targets must own an assigned referral; Guardian–Administration targets must have the Officer role.
- Social Worker dashboard counts are restricted to referrals assigned to that worker and summons scheduled by that worker. Broad student-record navigation/endpoints are denied.
- School Manager loses routine student, guardian, attendance-correction, normal Gate Pass approval/rejection, referral, and summons permissions. Aggregate oversight, audited Gate Pass override/audit, messaging audit, settings, automation, and notification administration remain.
- Social Worker loses broad student/guardian/attendance/conduct/Gate Pass/permit permissions; assigned referral/summons, own messaging, notifications, and dashboard permissions remain.
- Security continues to receive the dedicated minimal Gate Pass queue projection only.
- Student, Gate Pass, and conversation controller paths touched by W1 now translate handler failures to appropriate 401/403/404/409 responses rather than returning a successful HTTP status with a failed envelope.

Office-hours persistence and message release/queue behavior were not implemented.

## 5. Role landing and navigation matrix

| Role | Login/dashboard contract | Student Affairs link |
|---|---|---|
| Student Affairs Officer | `/student-affairs/officer` | Operational dashboard |
| Guardian | `/student-affairs/guardian` | Linked-student dashboard |
| Security Guard | `/student-affairs/security` | Minimal gate queue |
| Social Worker | `/student-affairs/social-worker` | Dedicated assigned cases/summons workspace |
| Instructor | `/instructor/dashboard` | Explicit `/student-affairs/teacher` link |
| Secretary | `/student-affairs/attendance/sheet` | Attendance roster |
| School Manager | `/school-manager/dashboard` | Explicit `/student-affairs/oversight` aggregate link |

The mapping is centralized in `roleLandingFor`. Each Student Affairs workspace uses both role and permission guards. The cross-student records route is limited to Officer and Super Admin; backend authorization remains authoritative.

## 6. MediatR contract containment

`StudentAffairsRequestContractTests` reflects over every Student Affairs `IRequest<T>` and requires either a concrete handler or an entry in `DeferredStudentAffairsRequests`. Controllers for deferred requests return HTTP 501 and do not dispatch to MediatR.

| Later workstream | Deferred contracts | Count |
|---|---|---:|
| W3 Classroom Entry Permit | create, list, detail, acknowledge, revoke | 5 |
| W6 Academic Concern Review | list, detail, dispatch decision, correct | 4 |
| W6 Conduct Review | list, detail, classify, dispatch decision, refer, correct | 6 |
| W6 Delay Review | morning list/detail/reason/correct; session list/detail/correct | 7 |
| W6 Recognition Review | list, statistics, detail, correct | 4 |
| W7 Automations and Notifications | rules, triggers, failures, retry failure | 4 |
| **Total** |  | **30** |

The already implemented create operations for behavior, academic concern, session delay, and recognition remain executable; only the handlerless contracts are contained.

## 7. Deterministic QA fixture

`StudentAffairsDataSeeder` now:

- derives the fixture date from injected `TimeProvider` in `Africa/Cairo`;
- provisions all seven core role accounts plus a substitute instructor;
- creates a pinned Bell revision with six periods, explicit recess, and real gaps;
- creates the subject, teacher timetable profiles, requirement, and teaching assignment required by the validation engine;
- publishes through `TimetableReviewService.PublishAsync`, producing the immutable published version and analysis boundary;
- adds a deterministic date-specific substitution tied to the published timetable/version;
- remains development-only and idempotent.

## 8. Tests added or strengthened

- Current lesson: exact start, exact end, break, gap, before/after hours, non-study day, missing publication, ambiguous publication, school timezone, invalid timezone, active substitution, and displaced-original-teacher behavior.
- Teacher context: canonical timetable/entry lookup with the effective substitute.
- Gate Pass: effective substitute snapshot; outside-lesson approval without acknowledgement; null teacher/event projection and auditable reason.
- Guardian: unlinked-student detail is denied before student data is loaded.
- Messaging: non-participant send is denied without repository mutation; creation target rules are enforced in the real repository.
- Contract safety: all Student Affairs requests have a handler or an explicit deferred workstream.
- Frontend: seven-role landing redirects, seven guarded workspace routes, and least-privilege navigation.
- Existing Social Worker assignment, Security queue projection, School Manager dashboard, timetable publication/break, and workflow tests remain green.

## 9. Build and test evidence

Commands executed from a clean command invocation against the current working tree:

| Command | Result |
|---|---|
| `cd backend; dotnet build AlFalah.slnx -c Release --no-restore` | Passed, 0 errors; one pre-existing nullable warning in `SocialWorkerWorkflowTests.cs:41` |
| `cd backend; dotnet test AlFalah.slnx -c Release` | Passed: 620/620, 0 failed, 0 skipped |
| `cd frontend; npm run build` | Passed; pre-existing CSS budget/selector warnings only |
| `cd frontend; npm test -- --watch=false --browsers=ChromeHeadless` | Passed: 161/161 |
| `git diff --check` | Passed; line-ending notices only |

## 10. Files changed by W1

### New files

- `backend/AlFalah.Application/IntelligentTimetable/CurrentLessonResolver.cs`
- `backend/AlFalah.Application/StudentAffairs/DeferredStudentAffairsRequests.cs`
- `backend/AlFalah.Infrastructure/Repositories/CurrentLessonEntryRepository.cs`
- `backend/AlFalah.Tests/StudentAffairs/StudentAffairsRequestContractTests.cs`
- `backend/AlFalah.Tests/Timetables/CurrentLessonResolverTests.cs`
- `frontend/src/app/core/utils/role-landing.ts`
- `docs/STUDENT-AFFAIRS-W1-SHARED-TIME-RBAC-FOUNDATION-2026-09-28.md`

### Modified backend files

- `backend/AlFalah.Api/Controllers/StudentAffairs/AcademicConcernsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/BehaviorsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/ClassroomEntryPermitsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/ConversationsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/GatePassesController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/MorningDelaysController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/RecognitionsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/SessionDelaysController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/StudentAffairsAutomationsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/StudentAffairsControllerBase.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/StudentsController.cs`
- `backend/AlFalah.Application/IntelligentTimetable/IBellScheduleRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/GatePasses/Handlers/ApproveGatePassCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Messaging/Handlers/CloseConversationCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Messaging/Handlers/CreateConversationCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Messaging/Handlers/SendConversationMessageCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Messaging/IMessagingWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/GetStudentByIdQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/GetStudentGuardiansQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/GetStudentTimelineQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/IStudentWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/Handlers/GetTeacherCurrentContextQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/Handlers/GetTeacherTopPriorityQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/ITeacherContextRepository.cs`
- `backend/AlFalah.Domain/Events/GatePassEvents.cs`
- `backend/AlFalah.Infrastructure/Data/Seeders/DatabaseSeeder.cs`
- `backend/AlFalah.Infrastructure/Data/Seeders/StudentAffairsDataSeeder.cs`
- `backend/AlFalah.Infrastructure/DependencyInjection.cs`
- `backend/AlFalah.Infrastructure/Repositories/BellScheduleRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/GatePassWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/MessagingWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/StudentWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/TeacherContextRepository.cs`
- `backend/AlFalah.Tests/StudentAffairs/GatePassAndMessagingMediatRTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/GatePassWorkflowTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/StudentWorkflowAndGuardianTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/TeacherTopPriorityHandlerRegistrationTests.cs`
- `backend/AlFalah.Tests/Timetables/BellSchedulePhase2Tests.cs`
- `backend/AlFalah.Tests/Timetables/ScheduleBreakPhase3Tests.cs`

### Modified frontend files

- `frontend/src/app/app.routes.ts`
- `frontend/src/app/app.routes.spec.ts`
- `frontend/src/app/features/auth/school-login/school-login.component.ts`
- `frontend/src/app/features/auth/school-login/school-login.component.spec.ts`
- `frontend/src/app/features/errors/unauthorized/unauthorized.component.ts`
- `frontend/src/app/shared/layout/shell/shell.component.ts`
- `frontend/src/app/shared/layout/shell/shell-navigation.spec.ts`

The two input documents were preserved. No files were added under `Data/Migrations`.

## 11. Deferred decisions and residual risks

- W2 must resolve the documented `Absent` versus `AbsentExcused` contradiction; W1 intentionally did not change attendance state.
- W3 owns Classroom Entry Permit implementation; W1 only returns explicit 501 containment.
- Office-hours persistence/derivation and the delivery queue/release engine remain deferred.
- The development substitution is a deterministic QA overlay referencing the immutable published version. Production substitution commands continue to use the existing substitution service and its own version/audit flow.
- Existing databases containing an old already-published E2E timetable are not structurally rewritten, preserving published immutability; the seeder adds the dated substitution and new accounts safely. A fresh development database receives the complete repaired fixture.
- The one backend nullable warning and Angular CSS budget/third-party selector warnings pre-date W1 and do not affect W1 behavior.

W2 has not been started.
