# Student Affairs Roles, Workflows, Timetable and UI — W0 Baseline Audit

**Audit date:** 2026-09-28  
**Source of truth:** `docs/STUDENT-AFFAIRS-ROLES-TIMETABLE-REVIEW-PLAN.md`  
**Scope:** W0 only — read-only architecture, implementation, routing, persistence, and test audit  
**Behavior/database changes:** None. No migration was created or changed.

## 1. Executive conclusion

The repository builds and its current automated suites pass, but that green baseline does not mean the Student Affairs surface is complete. The normalized Intelligent Timetable and the Gate Pass lesson-resolution path are the strongest parts of the implementation. The largest risks are incomplete MediatR coverage, object-scope authorization leaks, placeholder dashboard projections, non-persisting office-hours commands, messaging that bypasses office-hour delivery policy, and an attendance-excuse state that contradicts the approved business model.

Baseline result:

- **Working:** normalized Bell Schedule revisions; published-revision lookup; break/gap-safe canonical period resolution; Gate Pass timetable snapshot and substitution-aware teacher lookup; core attendance, referrals, summons, settings, Gate Pass, notifications, and much of the messaging API plumbing; realistic Student Affairs development accounts and a revision-linked timetable draft.
- **Incomplete:** 30 declared Student Affairs requests have no handler; all Classroom Entry Permit requests are among them; office-hours update/override persistence is a no-op; most role dashboards are placeholders; social-worker dashboard scoping is incomplete; guardian notifications and student timelines return empty pages; there is no Student Affairs end-to-end suite and almost no component-level UI coverage.
- **Contradictory or unsafe:** accepted excuses remain `Absent`; messaging mutations do not prove thread participation and new conversations accept raw target identifiers; Social Worker and management routes expose identifiable school-wide student records despite narrower role boundaries; School Manager is seeded ordinary Gate Pass approval/rejection and Entry Permit issue/revoke permissions; login and shell landing routes do not use the role dashboards that already exist.

W0 therefore passes as an audit workstream, but the implementation is **not ready for W2+ workflow expansion** until the W1 authorization/time foundation and its contract tests are complete.

## 2. Build and test baseline

| Area | Command | Result |
|---|---|---|
| Backend build | `dotnet build AlFalah.slnx --no-restore` from `backend/` | **Passed**, 0 errors, 1 warning, 16.58 s |
| Backend tests | `dotnet test AlFalah.slnx --no-build --no-restore --logger "console;verbosity=minimal"` from `backend/` | **Passed**, 605/605, 0 failed, 0 skipped, 28 s |
| Frontend production build | `npm run build` from `frontend/` | **Passed**, output `frontend/dist/al-falah-app`, about 104.45 s |
| Frontend unit tests | `npm test -- --watch=false --browsers=ChromeHeadless` from `frontend/` | **Passed**, 148/148, 0 failed |

Warnings observed:

- Backend: `AlFalah.Tests/StudentAffairs/SocialWorkerWorkflowTests.cs:41` emits CS8602 (possible null dereference).
- Frontend: `dashboard-live.component.css` is 28.81 kB and `visit-workspace.component.css` is 29.05 kB against the 16 kB warning budget.
- Frontend: three PrimeNG selectors using `:nth-child(... of ...)` are skipped by the CSS parser.

No live database, browser E2E, or deployed API smoke test was available in the repository workflow. The API trace below is based on controller/request/handler/repository/EF inspection plus the passing unit/integration-style test projects.

## 3. Source-of-truth and architectural verification

### 3.1 Bell Schedule ownership is correct

The implementation preserves the required normalized model:

`BellScheduleTemplate → BellScheduleRevision → BellScheduleDay → BellPeriod / Break`

`SchoolStudentAffairsSettings` does not contain a duplicate Bell Schedule JSON graph. Published `SchoolTimetable` rows reference a `BellScheduleRevisionId`. This matches the review plan and must remain the only timing authority.

`BellScheduleResolver.CurrentPeriod`:

- converts the instant with the schedule's `SchoolTimeZoneId`;
- applies the configured effective study day;
- rejects an active break before testing lessons;
- uses half-open lesson boundaries: `StartLocalTime <= now && now < EndLocalTime`;
- returns no period for holidays, gaps, breaks, and out-of-hours times.

`BellScheduleRepository.GetPublishedAsync` locates the published timetable/revision that is active for the local school date and fails closed when there is not exactly one match.

### 3.2 The opaque identifier is not a Gate Pass handler

`AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz` currently represents **two unrelated generated/name-collision artifacts**, neither of which is a Gate Pass handler:

1. `AlFalah.Domain/Events/AttendanceEvents.cs:38` — the domain event appended when an absence excuse is submitted (`SubmitAbsenceExcuseCommandHandler.cs:130`). Its payload contains the excuse, attendance, student, guardian, type, and submission time.
2. Migration `20260909024900_AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz` — the Intelligent Timetable teaching-assignment migration. It creates `TeachingAssignments` and `TeachingAssignmentMembers` and related keys/indexes. `docs/phases/PHASE-TT-06-TEACHING-ASSIGNMENTS.md:48` identifies the same purpose.

The actual Gate Pass approval trace is:

`GatePassesController` → `ApproveGatePassCommand` → `ApproveGatePassCommandHandler` → `IGatePassWorkflowRepository` → `GatePassWorkflowRepository` → `GatePasses`, `GatePassTransitions`, guardian/enrollment data, the published `SchoolTimetable`/`BellScheduleRevision`, timetable entries, and date-specific substitution movements.

The repository resolves the lesson at `RequestedExitAt`, pins the Bell revision, applies the active substitution, and snapshots the timetable, entry, instructor, and period identities on approval. It returns no active teacher in gaps/breaks/out-of-hours cases.

### 3.3 Seed timetable is realistic but is not published on a fresh database

`StudentAffairsDataSeeder` creates role accounts for Manager, Officer, Social Worker, Secretary, Guard, Instructor, and Guardian. Its timetable fixture has:

- `Africa/Cairo` school timezone;
- six 45-minute periods;
- a 20-minute mid-day gap after period 3;
- Sunday–Thursday study days;
- instructor entries in periods 1 and 3;
- a `SchoolTimetable` linked to the Bell revision.

However, the misleadingly named `EnsurePublishedTimetableAsync` creates a new timetable with `IsPublished = false` and never runs the real validation/publication boundary or creates a `SchoolTimetableVersion` publication snapshot. It only returns early when an already published timetable with a published snapshot exists. On a fresh database the canonical published-schedule resolver therefore has no operational schedule. This is materially better than hard-coded “current period” data as a draft, but it is not yet an E2E fixture. W1 needs a fixed test clock and explicit publication through the real service so the fixture is deterministic and operational.

## 4. Role, permission, route, and navigation baseline

The canonical permission map is actively reconciled by `DatabaseSeeder.GetRolePermissionMap`; stale role-permission rows are removed at seed time. The following table summarizes the seven in-scope roles.

| Role | Seeded Student Affairs capability | Route/navigation coverage | Landing behavior | W0 assessment |
|---|---|---|---|---|
| Secretary | Student/class operational management, classroom management, attendance view/manage, biometric import, timetable view/review/manage | Classroom, student, attendance, biometric, and timetable links exist | `/student-affairs/attendance/sheet` | Operational coverage exists. Full `Student.Manage` remains broader than the Phase 2 “roster-minimum” description and needs owner confirmation. No approval permissions were found. |
| Instructor | Current-context actions, incident/concern/delay/recognition creation, permit and Gate Pass acknowledgement, own messaging/office hours, teacher dashboard | Teacher workspace, office hours, messages, and wider instructor links exist | `/instructor/dashboard`, not `/student-affairs/teacher` | Student Affairs workspace exists, but current lesson lookup is not routed through the single canonical resolver method and entry permits are nonfunctional. |
| Student Affairs Officer | Broad student operations, excuse review, delays/conduct/recognition, permits, Gate Pass decisions, referrals, settings, automation, notifications, dashboard | Most workflow routes and an Officer dashboard route exist | **Incorrect:** `/student-affairs/settings` | Central role is substantially mapped, but many mapped endpoints have no handler and Student CRUD UI is Secretary-only. |
| Guardian | Linked-student attendance/excuses, own Gate Pass, own messaging, office-hours view, notifications, Guardian dashboard | Excuse, Gate Pass request, messages, and Guardian dashboard routes exist | **Incorrect:** generic `/dashboard` | Core linked-student queries are scoped, but conversation creation accepts free identifiers and the role dashboard repository is a placeholder. |
| Security Guard | Gate Pass view, security acknowledgement, execution, Security dashboard | Execution and Security dashboard routes exist | **Incorrect:** generic `/dashboard` | Execution workflow exists; dashboard repository is a placeholder. |
| Social Worker | Student/guardian/read permissions, assigned referral and summons workflow, messaging, notifications, Social Worker dashboard permission | Cases, summons, messages, and broad student-record routes exist; **no dedicated Social Worker dashboard route** | **Incorrect:** generic `/dashboard` | Referral/summons functions exist, but school-wide records and dashboard counts are not restricted to assigned cases. |
| School Manager | General management plus Student Affairs oversight, exceptional Gate Pass override/audit, settings/office-hours oversight | General dashboard, settings, identifiable student records, and oversight route exist; oversight has no clear shell link | `/school-manager/dashboard` | Oversight route exists, but normal Gate Pass approve/reject and Entry Permit issue/revoke grants contradict the “aggregate oversight + audited exception” boundary. Identifiable records are exposed beyond aggregate oversight. |

Additional route findings:

- `/student-affairs/teacher`, `/security`, `/guardian`, `/officer`, and `/oversight` components/routes exist.
- `/student-affairs/social-worker` does not exist; the CRM routes are `/student-affairs/cases` and `/student-affairs/summons`.
- The generic `/dashboard` fallback loads `MainManagerDashboardComponent` without role-specific Student Affairs routing.
- No Classroom Entry Permit frontend route/component was found.
- The shell has operational links but no clear role-dashboard link for Officer, Guardian, Security Guard, or Social Worker. The Officer “dashboard” target is settings.
- `student-affairs/records` and `records/:id` permit Officer, Social Worker, School Manager, Main Manager, and Super Admin. The detail projection contains identifiable history and is not an aggregate-only manager view.

## 5. API-to-database traceability

Legend: **Complete** means a request handler and concrete repository path exist, not that every business rule is correct. **Partial** means mixed real/placeholder behavior or missing operations. **Dead contract** means the controller sends a request for which no handler is registered.

| API controller/workflow | Request/handler state | Repository → database trace | Assessment |
|---|---|---|---|
| `TeacherStudentAffairsController` | Current context, roster, top priority handlers exist | `TeacherContextRepository` → instructor profile, published timetable/revision, entries, substitutions, enrollments, active Gate Pass/permit data | **Partial:** useful implementation; resolver logic is duplicated and ambiguity can throw. |
| `StudentAttendanceController` | Sheet, correction, excuse submit/list/download/review, Noor export handlers exist | `AttendanceWorkflowRepository` / `NoorExportRepository` → daily attendance, excuses, attachments, term metrics, export batches/items | **Partial:** accepted-state contradiction and sheet resave can overwrite excused status. |
| `GatePassesController` | Full request/decision/acknowledgement/execution/cancel/history handlers exist | `GatePassWorkflowRepository` → Gate Pass/transition, guardian/enrollment, published timetable/Bell revision, entries/substitutions, notifications/outbox | **Complete core trace;** strongest Student Affairs workflow. Boundary and recipient tests remain incomplete. |
| `ClassroomEntryPermitsController` | Create/list/detail/acknowledge/revoke requests declared; **no handlers** | Entity and `DbSet<ClassroomEntryPermit>` exist; no workflow repository | **Dead contract — all five endpoints.** |
| `OfficeHoursController` | All handlers exist | `MessagingWorkflowRepository` → `TeacherOfficeHours` | **Partial/placeholder:** reads work, but update and manager override do not mutate or save. |
| `ConversationsController` | All handlers exist | `MessagingWorkflowRepository` → threads, participants, messages, receipts, instructor/user lookup | **Unsafe/incomplete:** no office-hour queue/release; send/close do not enforce participant membership; target/student IDs are caller supplied. |
| `ReferralsController` | Main create/list/detail/assign/action/close handlers exist | `ReferralWorkflowRepository` → referrals, case actions, enrollment, users | **Complete core trace;** assignment-scope negative tests needed. |
| `SummonsController` | Main lifecycle and history handlers exist | `SummonWorkflowRepository` → summons/status history, referrals, guardians, assigned worker | **Complete core trace;** role/object-scope tests remain thin. |
| `NotificationsController` | List/detail/approve/suppress/retry/delivery handlers exist | `NotificationWorkflowRepository` → notifications and pending dispatch/delivery data | **Complete core trace;** recipient derivation and timetable-aware notification tests are missing. |
| `StudentAffairsSettingsController` | Get/update/history handlers exist | `StudentAffairsSettingsRepository` → settings and history/audit | **Complete trace.** No Bell Schedule duplication found. |
| `StudentsController` / `ClassroomsController` | CRUD, lookup, roster, guardian/enrollment handlers exist | `StudentWorkflowRepository` → students, guardians, enrollments, classrooms and related history | **Partial:** broad aliases/object scope conflict with approved role boundaries; timeline is placeholder. |
| `GuardianController` | Linked students and summary handlers exist | `StudentWorkflowRepository` → guardian link, student/enrollment/attendance/workflows | **Partial:** linked-student path exists; guardian notifications return an empty page. |
| `StudentAffairsDashboardController` | Six dashboard handlers exist | `StudentWorkflowRepository` → mixed projections | **Placeholder-heavy:** Teacher, Officer, Security, Guardian, and School Oversight return fixed/empty data; Social Worker queries real data but ignores the supplied worker ID. |
| `BiometricsController` | Import handler exists | `BiometricImportRepository` → arrival-delay/import data | **Complete trace.** |
| `MorningDelaysController` | Biometric record handler exists; list/detail/reason/correct lack handlers | `MorningDelayWorkflowRepository` for record path | **Partial/dead contracts.** |
| `SessionDelaysController` | Create handler exists; list/detail/correct lack handlers | `TeacherActionWorkflowRepository` for create path | **Partial/dead contracts.** |
| `AcademicConcernsController` | Create handler exists; list/detail/correct/dispatch-decision lack handlers | `TeacherActionWorkflowRepository` for create path | **Partial/dead contracts.** |
| `BehaviorsController` | Create handler exists; list/detail/classify/correct/refer/dispatch-decision lack handlers | `TeacherActionWorkflowRepository` for create path | **Partial/dead contracts.** |
| `RecognitionsController` | Create handler exists; list/detail/statistics/correct lack handlers | `TeacherActionWorkflowRepository` for create path | **Partial/dead contracts.** |
| `StudentAffairsAutomationsController` | Rule/trigger/failure list and retry requests have no handlers | Automation engine/outbox entities and workers exist, but controller query/command path is absent | **Dead contract — all four endpoint requests.** |

### 5.1 Exact unhandled request inventory

Static comparison found **133** Student Affairs `IRequest` declarations, **103** with an `IRequestHandler`, and these **30 without a handler**:

1. `AcknowledgeClassroomEntryPermitCommand`
2. `ClassifyBehaviorIncidentCommand`
3. `CorrectAcademicConcernCommand`
4. `CorrectBehaviorIncidentCommand`
5. `CorrectMorningDelayCommand`
6. `CorrectRecognitionCommand`
7. `CorrectSessionDelayCommand`
8. `CreateClassroomEntryPermitCommand`
9. `DecideAcademicConcernDispatchCommand`
10. `DecideBehaviorDispatchCommand`
11. `GetAcademicConcernByIdQuery`
12. `GetAcademicConcernsQuery`
13. `GetAutomationFailuresQuery`
14. `GetAutomationRulesQuery`
15. `GetAutomationTriggersQuery`
16. `GetBehaviorIncidentByIdQuery`
17. `GetBehaviorIncidentsQuery`
18. `GetClassroomEntryPermitByIdQuery`
19. `GetClassroomEntryPermitsQuery`
20. `GetMorningDelayByIdQuery`
21. `GetMorningDelaysQuery`
22. `GetRecognitionByIdQuery`
23. `GetRecognitionsQuery`
24. `GetRecognitionStatisticsQuery`
25. `GetSessionDelayByIdQuery`
26. `GetSessionDelaysQuery`
27. `ProvideMorningDelayReasonCommand`
28. `ReferBehaviorIncidentCommand`
29. `RetryAutomationFailureCommand`
30. `RevokeClassroomEntryPermitCommand`

Existing handler-registration tests are feature-selective. No test asserts that every controller-sent request resolves from the application container; that is why the suite remains green.

## 6. Timetable-dependent consumer audit

| Consumer | Published revision | School timezone / half-open | Break/gap/holiday safe | Active substitution | Historical identity | Result |
|---|---:|---:|---:|---:|---:|---|
| Canonical `BellScheduleResolver` | Yes | Yes | Yes | N/A | Returns revision/period DTO identity | **Canonical foundation works.** |
| Teacher current/top-priority context | Yes | Yes | Usually: no period means no lesson | Yes, date-specific | Carries timing/timetable data in lookup/DTO | **Partial:** `TeacherContextSchedule` duplicates `EffectivePeriods(...).SingleOrDefault` instead of invoking `CurrentPeriod`; it does not explicitly test breaks and duplicate matches may throw. |
| Gate Pass teacher/notification resolution | Yes | Yes | Yes | Yes, latest active movement | Timetable, entry, instructor and period snapshot fields | **Working and closest to required design.** |
| Classroom Entry Permit | No executable workflow | No | No | No | Entity fields only | **Missing handlers/repository.** |
| Teacher Office Hours | No | No | No | No | Effective dates exist in DTO/entity only | **Not derived from the timetable; writes are no-op.** |
| Guardian–Teacher Messaging release | No | Uses `DateTimeOffset.UtcNow` | No | No | No timing decision snapshot | **Always immediate; required queue/release policy absent.** |
| Substitutions | Published timetable entries and dated movements exist | Date-aware | N/A | Canonical query support exists | Movements retain source/effective identities | **Infrastructure exists; consumer adoption is inconsistent.** |
| Role dashboards/current lesson | Teacher dashboard returns a fabricated null-period context | Hard-coded `Asia/Riyadh` and `UtcNow` in repository placeholder | Not meaningful | No | No | **Not implemented.** Handler date creation also uses UTC date instead of school-local date. |
| Seeded QA timetable | Linked Bell revision, but fresh seed remains **draft** | `Africa/Cairo`; six periods | Gap present | No dedicated substitution fixture | No published version snapshot on a fresh database | **Realistic draft, not an operational E2E fixture.** |

The W1 implementation should expose one deep resolver interface returning a result such as `NoPublishedSchedule`, `NonStudyDay`, `Break`, `Gap`, `OutsideHours`, or `ActiveLesson`, with timetable/revision/entry/effective-instructor identity when active. Consumers should not recreate period selection.

## 7. Persistence and behavior defects verified

### 7.1 Office Hours

- `GetEligibleOfficeHoursAsync` returns already stored office-hour rows; it does not calculate non-teaching eligible slots from the published timetable.
- `UpdateMyOfficeHoursAsync` immediately calls and returns `GetMyOfficeHoursAsync`; it performs no insert/update/delete and no `SaveChangesAsync`.
- `OverrideTeacherOfficeHoursAsync` does the same; the reason and effective dates are not persisted or audited.
- The Angular screen can appear to save successfully because the API returns the unchanged rows.

### 7.2 Messaging

- Conversation creation and message send use `DateTimeOffset.UtcNow` and return `OfficeHoursDisposition.SentImmediately`.
- No office-hours eligibility, queue, next-release calculation, or timetable-republication reconciliation is present.
- Receipts are created as `Pending` while `DeliveredAt` is populated immediately, and the returned message DTO reports `Delivered`.
- `SendMessageAsync` and `CloseConversationAsync` load a school-scoped thread but do not require the caller to be a participant before mutation.
- Creation accepts raw `StudentId`, `TargetInstructorProfileId`, and `TargetStaffUserId`; it does not prove that a guardian is linked to the student or that the instructor teaches the student.
- No safe teacher/recipient lookup endpoint for this flow was found.

### 7.3 Attendance excuse state

Approved documents require accepted excuses to make the official attendance state `AbsentExcused`. Current code instead:

- changes `AbsenceExcuse.Status` to `Accepted`;
- changes `DailyStudentAttendance.ExcuseStatus` to `Accepted`;
- deliberately leaves `DailyStudentAttendance.Status` as `Absent`.

`AttendanceAndDelayWorkflowTests.AcceptExcuse_UpdatesExcuseSnapshotButPreservesOfficialAbsentStatus` asserts this contradictory behavior. Consequences include accepted absences remaining eligible for absence-count automations and potentially being omitted from output that expects `AbsentExcused`. A subsequent roster save also recomputes each row as `Present` or `Absent` and does not preserve `AbsentExcused` as an official state.

### 7.4 Dashboard and query placeholders

- Teacher dashboard: current UTC instant, hard-coded `Asia/Riyadh`, revision `1`, null period, empty roster/alerts.
- Officer, Security, Guardian, and School Oversight dashboards: zero/empty projections.
- Social Worker dashboard: real queries, but counts school-wide referrals/summons and does not use `socialWorkerUserId` to restrict assignments.
- Student timeline and guardian notification page: empty results.

## 8. Automated-test coverage

### 8.1 What is covered

- Bell schedule day inheritance, holidays, gaps/breaks, current-period selection, and timing validation.
- Published timetable/Bell revision persistence paths.
- Gate Pass handler behavior, exact-period teacher lookup, no teacher in a gap, state transitions, and snapshot fields in selected tests.
- Attendance, excuse submission/review as currently implemented, biometric delay, referrals, summons, settings, notification, permission, and selected MediatR registration behavior.
- Angular baseline bootstrapping and 148 unit tests across the wider application.

### 8.2 High-risk missing coverage

1. A universal application-container test that resolves every declared/controller-sent `IRequest`.
2. Controller/runtime tests proving the API does not throw for every advertised endpoint.
3. Role × endpoint × object-scope denial tests for all seven roles.
4. Guardian linked-student-only, Social Worker assigned-case-only, and School Manager aggregate-only negative tests.
5. Messaging participant checks, safe recipient lookup, office-hour queue/release, idempotency, and delivery-state consistency.
6. Accepted-excuse transition to `AbsentExcused`, automation recalculation, Noor export, and roster-resubmission preservation.
7. Classroom Entry Permit creation/resolution/acknowledgement/revocation and timetable snapshot tests.
8. Teacher context at exact start/end boundaries, during a break, in a gap, on a non-study day, without a published revision, under substitution, and across timezone/DST boundaries.
9. Gate Pass exact end boundary, substitution notification recipient, schedule republish/history, and ambiguity fail-closed behavior.
10. Office-hours persistence, manager override audit, timetable-derived eligibility, and republish reconciliation.
11. Dashboard projections, school-local dates, and role-specific privacy.
12. Frontend route-guard and navigation contract snapshots for the seven roles.
13. Student Affairs component tests. Only `attendance-sheet.component.spec.ts` was found under the feature; it contains an RTL alignment test rather than workflow behavior.
14. Browser E2E coverage for scenarios A–G from the review plan. No E2E runner/script was found, and Angular schematics set `skipTests: true` by default.

## 9. Documentation/code contradictions

| Topic | Approved/documented position | Current code | Disposition |
|---|---|---|---|
| Accepted absence excuse | Official state becomes `AbsentExcused` | Official state remains `Absent` | Code/test contradiction; requires correction after confirmation. |
| Gate Pass weekday rule | Configured Bell Schedule study days are authoritative | Backend resolver is configured-day based; old FE Phase 4 doc says Saturday–Thursday and rejects Friday | Backend matches the new decision; FE Phase 4 text is stale. |
| Office-hour messaging | Queue outside allowed hours, calculate next release, retain policy | Always sent immediately | Missing implementation. |
| Entry Permit | Officer issues, current teacher resolves/acknowledges, snapshot retained | Contracts/controller/entity only; no handlers | Missing implementation. |
| Social Worker scope | Assigned cases and summons | Broad student records plus school-wide dashboard counts | Privacy/authorization contradiction. |
| School Manager scope | Aggregate oversight and audited exception | Identifiable student history route; normal Gate Pass approve/reject and permit issue/revoke permissions | Role-boundary contradiction. |
| Secretary scope | Attendance roster entry and operational setup, not approvals | No workflow approval grants; broad Student/Classroom manage aliases | Approval boundary works; CRUD breadth needs confirmation. |
| Current lesson | One canonical local-time, published-revision, substitution-aware resolver | Gate Pass uses canonical resolver; Teacher context duplicates part of it; dashboards fabricate/omit context | Inconsistent adoption. |
| Phase documents marked “no implementation authorized” | Historical planning status | A substantial implementation now exists | Documentation lifecycle drift, not a business-rule change. |

The main review plan already identifies these as audit targets and does not require a factual correction from W0. It was therefore left unchanged rather than silently rewriting its approved decisions.

## 10. Prioritized gap register

Severity definitions: **P0** = authorization/data-integrity/runtime blocker; **P1** = core workflow materially incomplete or misleading; **P2** = important coverage, UX, or maintainability gap.

| ID | Severity | Affected roles | Workflow | Evidence | Recommended workstream |
|---|---|---|---|---|---|
| W0-G01 | P0 | All callers of affected APIs | 30 request contracts | Controller-sent requests have no handler; all Entry Permit and Automation API requests are affected | W1 containment/contract test, then owning W3–W9 workstream implementation |
| W0-G02 | P0 | Guardian, Instructor, Officer, Social Worker | Messaging | Send/close mutation lacks participant proof; create accepts raw student/recipient IDs | W1 RBAC/object-scope foundation; W5 policy implementation |
| W0-G03 | P0 | Social Worker, School/Main Manager, students/guardians | Records/dashboards | School-wide identifiable records and counts conflict with assigned/aggregate scope | W1 RBAC and projection split |
| W0-G04 | P0 | Guardian, Officer, Secretary, student | Attendance excuses/automation/export | Accepted excuse remains `Absent`; passing test enshrines contradiction | W2 after business confirmation |
| W0-G05 | P0 | Officer, Guardian, Instructor | Classroom Entry Permit | Five public requests have no handlers/repository | Contract containment in W1; implement in W3 |
| W0-G06 | P1 | Instructor, Gate Pass consumers, future permit/messaging/dashboard consumers | Current lesson | Resolver adoption is split; teacher path duplicates period resolution; dashboards do not use it | W1 shared time/current-lesson service |
| W0-G07 | P1 | Instructor, School Manager, Guardian | Office Hours | Update and override return unchanged data; no timetable derivation | W4 |
| W0-G08 | P1 | Guardian, Instructor | Messaging delivery | Always immediate; inconsistent receipt state; no outbox release policy | W5 |
| W0-G09 | P1 | Officer, Guard, Guardian, Manager | Dashboards | Five repository methods return fixed/empty data | W8 after W1 projections/security |
| W0-G10 | P1 | Officer, Guardian, Guard, Social Worker | Landing/navigation | Existing role dashboards are not used as canonical landing targets; Social Worker dashboard route absent | W1 route/RBAC contract, finish UI in W8 |
| W0-G11 | P1 | School Manager, Officer | Gate Pass/Entry Permit | Manager seeded normal approval/issue permissions despite oversight-only boundary | W1 permission-map correction after confirmation |
| W0-G12 | P1 | Guardian, Instructor, Officer | Safe lookups | No proven guardian-linked-student/actual-teacher lookup for conversation creation; Student Affairs management route ownership mismatches permissions | W1/W5 |
| W0-G13 | P1 | Social Worker | Assigned work | Dashboard ignores worker ID; broad records query is school scoped | W1/W6 |
| W0-G14 | P1 | All roles | Regression safety | No universal request-handler test, role matrix, or Student Affairs E2E suite | W1 and W9 |
| W0-G15 | P2 | Officer, Manager | Automation UI/API | Engine/outbox exists but four controller requests have no handlers | W7 |
| W0-G16 | P2 | Officer, Social Worker | Conduct/delay/recognition review | Create paths exist; list/detail/correction/decision handlers are missing | W6/W7 by domain |
| W0-G17 | P2 | Dashboard users | Local date | Dashboard handlers/repository use UTC date/instant or hard-coded timezone | W1 shared school clock; W8 |
| W0-G18 | P2 | Developers/QA | UI verification | 148 tests pass but Student Affairs feature coverage is effectively absent; `skipTests` is the scaffold default | W9, with W1 route tests immediately |
| W0-G19 | P1 | All timetable-dependent roles, QA | Seeded current lesson | `EnsurePublishedTimetableAsync` leaves a fresh timetable as a draft and creates no published version snapshot | W1 deterministic published fixture through the real publication boundary |

## 11. Recommended precise scope for W1

W1 should be a narrow **Shared School-Time + Authorization Safety Foundation**. It should not implement the later workflows.

1. Introduce one application-facing current-lesson resolver backed by the existing normalized Bell Schedule and published timetable. Its result must explicitly distinguish no-published-schedule, non-study day, break, gap, outside-hours, ambiguous schedule, and active lesson; active results include Bell revision, timetable, entry, classroom, original/effective instructor, and substitution identity.
2. Move Teacher current/top-priority context and Gate Pass resolution onto that interface without changing their approved outcomes. Add fixed-clock tests for school timezone, `[StartTime, EndTime)`, breaks, gaps, holidays, missing/ambiguous publication, and substitutions.
3. Add a universal MediatR registration/dispatch contract test. During W1, unimplemented contracts must be explicitly classified and prevented from masquerading as working UI/API paths; their business implementations remain in their assigned later workstreams.
4. Enforce the approved object scopes at query/mutation boundaries: linked students for Guardian, current class for Instructor, assigned cases for Social Worker, minimum Gate Pass projection for Guard, and aggregate-only oversight for School Manager.
5. Close the immediate messaging authorization holes (participant requirement and safe linked recipient/student selection) while deferring office-hour queue/release behavior to W5.
6. Align the permission map, route guards, login redirects, and shell dashboard route for the seven roles. Add role-route-navigation contract tests. Do not redesign the dashboards in W1.
7. Establish deterministic QA infrastructure: fixed `TimeProvider`, publish the seeded timetable through the real validation/publication boundary, verify its immutable version snapshot, and add substitutions plus break/gap cases and one account per role. Reuse the existing seeded timetable model; do not duplicate timing settings.
8. Produce a W1 evidence report and stop before W2.

Recommended W1 exclusions: absence-state correction (W2), Entry Permit implementation (W3), Office Hours persistence/derivation (W4), messaging release engine (W5), Social Worker business expansion (W6), automations/notifications expansion (W7), dashboard/UI modernization (W8), and full cross-role E2E (W9).

## 12. Business decisions requiring confirmation before implementation

1. **Accepted excuse state:** confirm the approved model remains `DailyStudentAttendance.Status = AbsentExcused` on acceptance. Recommended: yes; update metrics/export/resubmission behavior and replace the contradictory test in W2.
2. **School Manager grants:** confirm removal of ordinary `GatePass.Approve`, `GatePass.Reject`, `ClassroomEntryPermit.Issue`, and `ClassroomEntryPermit.Revoke`, retaining aggregate oversight, audit, and an explicitly audited `GatePass.Override`. Recommended: remove ordinary operational grants.
3. **Secretary roster breadth:** confirm whether Secretary keeps full student/class CRUD or receives roster-minimum plus operational enrollment/setup capabilities. Recommended: replace broad `Student.Manage` aliases with explicit operational permissions.
4. **Canonical landings:** proposed targets are Officer `/student-affairs/officer`, Guardian `/student-affairs/guardian`, Guard `/student-affairs/security`, and a new Social Worker dashboard route (with cases/summons as work queues). Confirm.
5. **Social Worker records:** confirm that school-wide `/student-affairs/records` must be removed for Social Worker and replaced by assigned-case student summaries. Recommended: enforce assigned-only.
6. **Manager records:** confirm that Manager/Main Manager access is aggregate-only by default and identifiable drill-down requires an audited exceptional action. Recommended: enforce aggregate-only.
7. **Safe messaging lookup:** confirm that a Guardian may select only linked students and teachers currently teaching those students, while staff recipients come from a role-filtered school directory. Recommended: yes; do not accept arbitrary IDs from the UI.
8. **Gate Pass during break/end-of-day:** confirm whether a valid requested exit with no active lesson may still be approved with `TeacherAcknowledgementNotRequired`, or must be rejected. Current code can approve without a resolved teacher; the review plan flags this for confirmation.
9. **Office-hours source boundaries:** confirm which non-teaching periods are eligible (planning/duty periods, minimum gap, before/after school, and break exclusion) before W4.
10. **QA clock strategy:** confirm a fixed clock for automated tests and a development-only controllable clock for manual role walkthroughs. Recommended: fixed `TimeProvider` in tests; any UI-visible clock override must be development-only and audited/config gated.

## 13. Files inspected

### Business and architecture documents

- `docs/STUDENT-AFFAIRS-ROLES-TIMETABLE-REVIEW-PLAN.md` (read in full)
- `CONTEXT.md`
- `docs/Phase1-Domain-And-Database-Schema.md`
- `docs/Phase2-Identity-Roles-And-Permissions.md`
- `docs/Phase3-Core-API-Contracts.md`
- `docs/Phase4-Workflows-And-State-Machines.md`
- `docs/Phase5-Automations-And-Integrations.md`
- `FE-Phase1-Foundation-Shared-Models.md`
- `FE-Phase2-Secretary-Workflows.md`
- `FE-Phase3-Attendance-Absence-Delay.md`
- `FE-Phase4-GatePass-Workflows.md`
- `FE-Phase5-Remaining-Workflows-And-Dashboards.md`
- `docs/specs/intelligent-timetable/02-timings.md`
- `docs/phases/PHASE-TT-02-TIMINGS.md`
- `docs/phases/PHASE-TT-06-TEACHING-ASSIGNMENTS.md`
- `docs/02-DOMAIN-MODEL.md`
- `docs/14-DECISIONS-AND-DEVIATIONS.md`

### Backend implementation

- `backend/AlFalah.slnx` and all project files participating in the build
- `backend/AlFalah.Shared/Constants/RoleNames.cs`
- `backend/AlFalah.Shared/Constants/PermissionNames.cs`
- `backend/AlFalah.Domain/Events/AttendanceEvents.cs`
- Bell Schedule, timetable, substitution, attendance, Gate Pass, Entry Permit, office-hours, messaging, referral, summons, notification, settings, automation, student, guardian, and classroom entities under `backend/AlFalah.Domain/Entities/`
- All controllers under `backend/AlFalah.Api/Controllers/StudentAffairs/`
- All request/DTO/handler declarations under `backend/AlFalah.Application/StudentAffairs/`
- `backend/AlFalah.Application/IntelligentTimetable/BellScheduleResolver.cs`
- `backend/AlFalah.Application/StudentAffairs/TeacherContext/TeacherContextSchedule.cs`
- `backend/AlFalah.Infrastructure/DependencyInjection.cs`
- `backend/AlFalah.Infrastructure/Data/AlFalahDbContext.cs`
- `backend/AlFalah.Infrastructure/Data/Seeders/DatabaseSeeder.cs`
- `backend/AlFalah.Infrastructure/Data/Seeders/StudentAffairsDataSeeder.cs`
- `backend/AlFalah.Infrastructure/Repositories/BellScheduleRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/TeacherContextRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/GatePassWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/AttendanceWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/NoorExportRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/MessagingWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/StudentWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/TeacherActionWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/MorningDelayWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/ReferralWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/SummonWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/NotificationWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/StudentAffairsSettingsRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/BiometricImportRepository.cs`
- `backend/AlFalah.Infrastructure/Data/Migrations/20260909024900_AcbXX3KgvqD7B8Y4WjCu6yNx1Prfu5cNHz.cs`
- Relevant current model snapshot/designer references under `backend/AlFalah.Infrastructure/Data/Migrations/`

### Frontend implementation

- `frontend/package.json`
- `frontend/angular.json`
- `frontend/src/app/app.routes.ts`
- `frontend/src/app/core/guards/role.guard.ts`
- `frontend/src/app/features/auth/school-login/school-login.component.ts`
- `frontend/src/app/shared/layout/shell/shell.component.ts`
- `frontend/src/app/shared/layout/shell/shell-navigation.spec.ts`
- Student Affairs API/model services under `frontend/src/app/core/services/` and `frontend/src/app/core/models/`
- All components/templates under `frontend/src/app/features/student-affairs/`
- `frontend/src/app/app.routes.spec.ts`
- `frontend/src/app/features/student-affairs/attendance-sheet/attendance-sheet.component.spec.ts`

### Tests inspected

- All files under `backend/AlFalah.Tests/StudentAffairs/`
- Relevant Bell Schedule, timetable publication, and substitution tests under `backend/AlFalah.Tests/IntelligentTimetable/` and `backend/AlFalah.Tests/Timetables/`
- Relevant security/permission tests under `backend/AlFalah.Tests/Security/`
- Frontend `*.spec.ts` inventory, with detailed review of routing, shell navigation, login, and Student Affairs specs

## 14. Commands executed

Read-only discovery/audit commands included:

```text
git status --short
rg --files
Get-Content <review-plan> -Encoding utf8 (chunked until EOF)
Get-Content <referenced-documents> -Encoding utf8 (full or targeted sections)
rg -n <role|permission|route|handler|repository|resolver|entity|test patterns> <scoped paths>
Get-ChildItem backend/AlFalah.Api/Controllers/StudentAffairs -Filter *.cs
PowerShell IRequest/IRequestHandler declaration comparison across backend/AlFalah.Application/StudentAffairs
```

Verification commands:

```text
cd backend
dotnet build AlFalah.slnx --no-restore
dotnet test AlFalah.slnx --no-build --no-restore --logger "console;verbosity=minimal"

cd frontend
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

No mutation command, migration command, package installation, database update, or destructive Git command was executed.

## 15. W0 exit

W0 is complete. The evidence supports proceeding only to the W1 scope in section 11 after the decisions in section 12 are confirmed. No W1 implementation has started.
