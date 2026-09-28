# Student Affairs W2 — Secretary: School Data + Timetable + Attendance

Date: 2026-09-28

Status: Complete; stopped before W3

Inputs:

- `STUDENT-AFFAIRS-ROLES-TIMETABLE-REVIEW-PLAN.md`
- `STUDENT-AFFAIRS-W0-BASELINE-AUDIT-2026-09-28.md`
- `STUDENT-AFFAIRS-W1-SHARED-TIME-RBAC-FOUNDATION-2026-09-28.md`

## 1. Outcome

W2 closes the Secretary school-data, attendance, Zajel, and timetable-verification scope without replacing the W1 time/RBAC foundation. School-data mutations are permission- and school-scoped, academic relationships are validated at their effective date, classroom transfers preserve enrollment history, and overlapping enrollment periods are rejected.

Attendance now has one explicit roster contract: a checked student is `Absent`, an unchecked student is `Present`, and an empty absent list marks the active roster present. The roster is derived from active dated enrollments only. A deterministic roster/content revision protects concurrent changes, the existing inbox table provides durable request idempotency, and accepted `AbsentExcused` rows cannot be overwritten by roster resubmission.

Accepting an excuse now changes the official state to `AbsentExcused`; rejection retains `Absent`. Existing automation metrics exclude that state, accepted excuses are exported to Noor, and the UI presents accepted excuses as protected official absences. Zajel uses the Student Affairs arrival cutoff plus grace in the published Bell revision's school timezone, independently of Bell period times.

No migration was created. W3 was not started.

## 2. School-data integrity

### Scope and academic relationships

- Classroom, student, academic-year lookup, enrollment target, and mutation repository queries use the caller's active `SchoolId`; client-supplied school scope is never trusted.
- Academic years shown to the Secretary are derived from that school's non-deleted `AcademicTerm` rows instead of the platform-global `AcademicYears` table.
- Creating a classroom requires an academic year actually referenced by the active school.
- Student creation/update with a classroom now resolves exactly one active term whose school, academic year, and effective date match the classroom. Zero or ambiguous matches fail safely.
- Explicit enrollment creation validates the requested school, classroom, academic term, academic year, student state, and effective date as one target.
- Enrollment periods are checked for overlap before creation/update.
- Dated active-enrollment lookup includes enrollment and term boundaries and excludes soft-deleted records.

### Historical preservation

- Moving a student to another classroom closes the prior enrollment as `Transferred` and creates a new active enrollment; the old classroom identity is not rewritten.
- Withdrawing or transferring records an effective end date.
- An enrollment cannot be closed while silently changing its historical classroom.
- Student update uses the same close-and-create behavior when its current classroom changes.
- Student/classroom soft deletion continues to retain historical attendance and workflow records.

### Permissions and HTTP results

- Classroom and student mutation handlers require explicit permissions; role-name bypasses were removed.
- Controllers touched by W2 translate failed `ApiResponse` results to 400/401/403/404/409 rather than returning HTTP 200 with `IsSuccess=false`.
- Cross-school classroom/student identifiers produce not-found semantics without mutating the foreign record.
- No raw EF entity is returned from an API.

The current `Student`, `Classroom`, and `StudentEnrollment` schema does not contain a real row-version field. W2 did not invent a cosmetic concurrency token or create a migration. Existing real concurrency boundaries—attendance/excuse row versions, roster revision, idempotency key, and timetable revisions—remain enforced.

## 3. Final attendance semantics

For `PUT /api/v1/student-attendance/sheet`:

1. Require an authenticated Secretary with `AttendanceManageStudents`.
2. Validate the idempotency key and request shape.
3. Replay a completed identical idempotency key safely; using the key for different content returns 409.
4. Compare the submitted revision with a deterministic SHA-256 revision over the current dated roster and saved row state. A stale value returns 409 before mutation.
5. Load only active, non-deleted students, classrooms, terms, and enrollments valid on the requested date.
6. Require a single unambiguous academic term for the roster.
7. Treat IDs in `absentStudentIds` as `Absent` and every other roster student as `Present`; an empty array therefore means all present.
8. Preserve every existing `AbsentExcused` row and accepted excuse state.
9. Skip unchanged rows, timestamps, and domain events.
10. Persist the sheet changes and idempotency receipt in the same `SaveChanges` transaction.

The durable receipt reuses the existing `InboxMessages` schema: the normalized idempotency key is deterministically hashed to its message ID, and the request fingerprint is stored in the message type. This avoids a new table/migration while distinguishing a legitimate replay from key reuse with different content. A racing identical request can replay the winner; a different request receives an idempotency conflict.

The sheet projection always starts with the current active roster and then merges saved attendance. This fixes the prior case where a newly enrolled student disappeared once any attendance row already existed.

## 4. `AbsentExcused` decision and downstream effects

- Accepting an excuse sets both `AbsenceExcuse.Status = Accepted` and `DailyStudentAttendance.Status = AbsentExcused`.
- Rejecting an excuse sets/keeps the official attendance state `Absent`.
- The reviewer, review time, reason, entity update actor/time, expected excuse row version, and accepted-event audit remain recorded.
- A stale excuse row version fails without mutation or a success-shaped response.
- Roster resubmission cannot convert `AbsentExcused` to either `Present` or `Absent`.
- The attendance UI labels the record "غياب بعذر", locks its checkbox, includes it in a protected-excuse count, and does not count it as present.
- `StudentAffairsAutomationRuleEngine` counts only official `Absent` rows for penalty thresholds; the existing accepted-excuse event forces recalculation, so `AbsentExcused` is excluded.
- The Noor repository explicitly selects same-school, in-range rows whose official state is `AbsentExcused` and excuse decision is `Accepted`. A repository test now proves rejected and cross-school rows are excluded.

There is no accepted-excuse cancellation contract in the current model. W2 therefore did not introduce an unaudited reversal path. Existing Officer attendance correction remains the auditable correction boundary; a future explicit excuse-cancellation product decision would require its own transition and tests.

## 5. Zajel import behavior

- Only a Secretary with the dedicated `BiometricImport` permission can import.
- The late boundary is exactly `SchoolStudentAffairsSettings.ArrivalCutoffLocalTime + ArrivalGraceMinutes`.
- Before and exactly at that boundary are on time; strictly after it is late. Workbook status text cannot override the configured cutoff.
- The instant is reconstructed from workbook school-local date/time using the timezone on the latest published timetable's Bell revision. Missing or invalid published timezone fails closed.
- Bell periods are not read as an arrival cutoff; changing their lesson times does not alter lateness classification.
- Enrollment lookup is school-scoped and requires a non-deleted active student, classroom, term, matching academic year, and dated enrollment.
- Zero matching enrollments produces a row issue; multiple matches produce `AmbiguousEnrollment` rather than an exception or arbitrary selection.
- Exact repeat rows do not mutate timestamps or call `SaveChanges` again. A later punch for the same student/date updates the one existing delay deterministically.
- Invalid/unmatched rows are reported while valid rows are committed. This is the retained, now-tested partial-success policy.
- The Angular page validates one non-empty `.xlsx` file up to 20 MB, renders validation/server errors in-page, retains the selected file for retry, and displays partial-success counters and row issues.

## 6. Timetable validation and publication evidence

W2 reused the Intelligent Timetable implementation and the deterministic W1 fixture; it did not create another timing source or copy Bell periods into Student Affairs settings.

The verified backend suite covers:

- Secretary access through independent timetable permissions rather than attendance permission;
- academic-year/semester scope and overlapping-term validation;
- Bell templates, all study days, day overrides, flexible periods, gaps, breaks, and school timezone;
- append-only Bell revisions and immutable persisted/published revisions;
- profile selection and `SchoolTimetable.BellScheduleRevisionId` linkage;
- invalid, ambiguous, missing, draft-only, and stale inputs failing safely;
- timing/setup changes invalidating draft dependants for revalidation without rewriting a published timetable;
- publication through the review boundary with a pinned immutable snapshot;
- the W1 canonical resolver finding the expected published fixture lesson, substitutions, breaks, gaps, and half-open boundaries.

The focused timetable verification passed 193/193 tests. Angular timing/review tests now also prove that stale timing edits are retained for recovery and publication conflicts remain visible without discarding the review.

## 7. Secretary RBAC regression

Positive coverage proves that a Secretary with the matching permission can manage in-school classrooms/students, read/write attendance, import Zajel, and use independently granted timetable operations.

Negative coverage proves that the Secretary cannot:

- accept or reject absence excuses, even if a review permission is incorrectly injected;
- approve, reject, or execute a Gate Pass, even if the relevant workflow permission is injected;
- read referral case actions/confidential case data;
- manage summons;
- execute Classroom Entry Permit contracts. A normal Secretary receives 403; an explicitly granted deferred contract remains contained at 501 and is never dispatched;
- mutate school data without its explicit permission.

W2 also fixed a Gate Pass rejection guard that used `not-role AND not-permission`; it now requires both the Officer role and rejection permission, matching approval and Security execution boundaries.

## 8. Frontend screens and states

- Secretary landing remains `/student-affairs/attendance/sheet` and the W1 role-plus-permission guards/navigation matrix remain intact.
- Classroom and student screens retain loading, empty, populated table, validation, retry/error, responsive scroll, accessible labels, and school-safe payload behavior. New tests cover empty/error states, required validation, normalized payloads, and absence of a client `schoolId`.
- Attendance adds protected excused-state messaging, correct present/absent/excused totals, defensive double-submit blocking, durable selection across 409 reload, explicit 403/404 error rendering, and a horizontally scrollable roster on narrow layouts.
- A 409 reload intersects the user's prior absence selection with the new roster and names students removed from the roster; it does not silently clear still-valid choices.
- Zajel adds an in-page error/validation banner and retry-preserving upload behavior.
- Timetable timing/review tests cover stale save and publication failure recovery.
- Existing login redirect, route-guard, Secretary navigation, RTL, and least-privilege tests remain green.

## 9. Tests added or strengthened

### Backend

- School scope: in-school classroom management, cross-school classroom/student denial, school-derived academic years, exact term/classroom/year/date target, overlap rejection, explicit-permission denial, and dated enrollment roster boundaries.
- Attendance: absent/present mapping, deterministic active roster revision, stale rejection before mutation, durable replay idempotency, unchanged-row/event suppression, `AbsentExcused` preservation, acceptance/rejection state, Secretary review denial, and HTTP 409 mapping.
- Metrics/Noor: accepted excuses excluded from the 3/5/10 penalty counts and only accepted `AbsentExcused` rows exported for the requested school/range.
- Zajel: before/at/after grace boundary, Cairo offset, workbook-status independence, exact duplicate no-op, updated duplicate behavior, role denial, and unmatched/cross-school-safe row reporting.
- RBAC: Gate Pass approve/reject/execute denial, referral/summon denial, and deferred permit containment.
- Timetable: the existing comprehensive suite plus W1 resolver regression all remain green.

### Frontend

- Attendance: checked/unchecked, empty absent list, success, double submit, 409 selection preservation, protected `AbsentExcused`, 403/404 state, RTL controls, and roster overflow.
- Classroom: loading completion, empty result, server error, validation, and school-safe create payload.
- Student: loading completion, empty result, server error, identity validation, and school-safe payload.
- Zajel: extension validation, visible HTTP failure/retry state, and partial-success issues.
- Timetable: timing revision conflict and publication/revalidation conflict.

## 10. Build and test evidence

| Command | Result |
|---|---|
| `cd backend; dotnet build AlFalah.slnx -c Release --no-restore` | Passed; 0 errors, 0 warnings |
| `cd backend; dotnet test AlFalah.slnx -c Release` | Passed: 642/642, 0 failed, 0 skipped |
| Focused backend W2 school/attendance/Zajel/RBAC suite | Passed: 78/78 |
| Focused backend timetable/resolver suite | Passed: 193/193 |
| `cd frontend; npm run build` | Passed; only the pre-existing CSS budget and third-party selector warnings |
| `cd frontend; npm test -- --watch=false --browsers=ChromeHeadless` | Passed: 179/179, 0 failed |
| Focused frontend W2 screens/timetable suite | Passed: 27/27 |
| `git diff --check` | Passed; line-ending conversion notices only, no whitespace errors |

Compared with the W1 baseline, backend tests increased from 620 to 642 and frontend tests from 161 to 179. No required test was skipped or could not be run.

The Angular build warnings are unchanged from W0/W1:

- `dashboard-live.component.css` and `visit-workspace.component.css` exceed the existing 16 kB component-style warning budget.
- three PrimeNG organization-chart selectors are skipped by the bundler parser.

## 11. Files changed by W2

### Backend behavior

- `backend/AlFalah.Api/Controllers/StudentAffairs/StudentAffairsControllerBase.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/StudentAttendanceController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/BiometricsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/ClassroomsController.cs`
- `backend/AlFalah.Api/Controllers/StudentAffairs/StudentsController.cs`
- `backend/AlFalah.Application/StudentAffairs/Attendance/IAttendanceWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/Attendance/Handlers/GetStudentAttendanceSheetQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Attendance/Handlers/SaveStudentAttendanceSheetCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Attendance/Handlers/ReviewAbsenceExcuseCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Biometrics/BiometricContracts.cs`
- `backend/AlFalah.Application/StudentAffairs/Biometrics/Handlers/ImportZajelBiometricCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Classrooms/Handlers/CreateClassroomCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Classrooms/Handlers/UpdateClassroomCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Classrooms/Handlers/DeleteClassroomCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Classrooms/Handlers/GetClassroomAcademicYearsQueryHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/IStudentWorkflowRepository.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/CreateStudentCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/UpdateStudentCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/DeleteStudentCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/CreateStudentEnrollmentCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/Students/Handlers/UpdateStudentEnrollmentCommandHandler.cs`
- `backend/AlFalah.Application/StudentAffairs/GatePasses/Handlers/RejectGatePassCommandHandler.cs`
- `backend/AlFalah.Infrastructure/Repositories/AttendanceWorkflowRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/BiometricImportRepository.cs`
- `backend/AlFalah.Infrastructure/Repositories/StudentWorkflowRepository.cs`

### Backend tests

- `backend/AlFalah.Tests/Security/SecretaryAttendanceAuthorizationTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/AttendanceAndDelayWorkflowTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/AttendanceMediatRTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/BiometricImportWorkflowTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/GatePassWorkflowTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/Phase5AutomationsAndIntegrationsTests.cs`
- `backend/AlFalah.Tests/StudentAffairs/StudentWorkflowAndGuardianTests.cs`

### Frontend behavior and tests

- `frontend/src/app/features/student-affairs/attendance-sheet/attendance-sheet.component.ts`
- `frontend/src/app/features/student-affairs/attendance-sheet/attendance-sheet.component.html`
- `frontend/src/app/features/student-affairs/attendance-sheet/attendance-sheet.component.css`
- `frontend/src/app/features/student-affairs/attendance-sheet/attendance-sheet.component.spec.ts`
- `frontend/src/app/features/student-affairs/biometric-import/biometric-import.component.ts`
- `frontend/src/app/features/student-affairs/biometric-import/biometric-import.component.html`
- `frontend/src/app/features/student-affairs/biometric-import/biometric-import.component.css`
- `frontend/src/app/features/student-affairs/biometric-import/biometric-import.component.spec.ts`
- `frontend/src/app/features/student-affairs/classrooms-management/classrooms-management.component.spec.ts`
- `frontend/src/app/features/student-affairs/students-management/students-management.component.spec.ts`
- `frontend/src/app/features/intelligent-timetable/timings/timetable-timings.component.spec.ts`
- `frontend/src/app/features/intelligent-timetable/review/timetable-review.component.spec.ts`
- `docs/STUDENT-AFFAIRS-W2-SECRETARY-TIMETABLE-DATA-ATTENDANCE-2026-09-28.md`

All pre-existing user and W1 changes were preserved.

## 12. Migrations, risks, and deferred work

- No file was added or changed under `backend/AlFalah.Infrastructure/Data/Migrations`; the existing schema was sufficient.
- Student/classroom/enrollment optimistic concurrency remains limited by their current schema, which has no real row version. Adding one is a separate, explicit schema decision rather than a W2 cosmetic token.
- Durable attendance idempotency returns the current canonical sheet on replay rather than storing a serialized historical HTTP response. The request fingerprint prevents the same key from being reused for different content.
- Zajel fails closed when no valid published Bell timezone is available. This is intentional: the arrival cutoff comes from Student Affairs settings, while conversion authority comes from the published school-time source.
- The current model has no accepted-excuse cancellation transition. Any future cancellation must define official-state restoration, audit, Noor correction, and metric recalculation explicitly.
- W3 Instructor quick actions and Classroom Entry Permit implementation remain deferred, as do Office Hours persistence, messaging delivery/release, dashboard redesigns, later-role workflow expansions, and full cross-role E2E coverage.

W3 has not been started. Approval is required before continuing.
