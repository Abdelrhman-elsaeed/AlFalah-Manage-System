# Phase 3 — Core API Contracts

**Status:** Locked technical specification — no implementation authorized  
**Base route:** `/api/v1`  
**Response:** existing `ApiResponse<T>`; file responses use the appropriate binary content type

## 1. API and application-layer rules

- Controllers are HTTP adapters only. They bind input, perform a fast permission check, send one command/query, and return `ApiResponse<T>` or `File(...)`.
- Complex use cases are implemented as MediatR commands/queries or equivalent single-purpose handlers under `AlFalah.Application/StudentAffairs/<Feature>`.
- The current repository does not reference MediatR. Introducing it is an explicit implementation task: add it to Application/API composition without moving business logic into controllers or Infrastructure.
- Commands never accept an authoritative `SchoolId`, actor user ID, approval user ID, count, state, or derived current teacher from the client. Handlers derive them from `ICurrentUserService`, `SchoolScopeGuard`, persisted relationships, and the published timetable.
- Global-admin list queries may accept `schoolId`; school-scoped callers are coerced by `SchoolScopeGuard`.
- Every write supports `CancellationToken`. Transition commands also carry `RowVersion`/opaque version to detect concurrent actions.
- Create endpoints accept `Idempotency-Key` for guardian gate requests, attendance roster submissions, excuses, and manual referrals/summons.
- List endpoints extend the existing `PagedQuery` and return `PagedResult<T>` inside `ApiResponse<T>`.
- Dates are ISO `yyyy-MM-dd`; instants are ISO-8601 UTC `DateTimeOffset`. The API returns the school timezone when presenting local schedule data.
- DTOs are endpoint-specific records. Raw entities and provider-specific storage IDs never cross the API boundary.

## 2. Shared contract shapes

| DTO | Fields |
|---|---|
| `StudentAffairsPageQuery` | `pageNumber`, `pageSize`, `search`, `sortBy`, `sortDirection`, optional `schoolId` for global callers |
| `StudentSummaryDto` | `id`, `studentNumber`, `displayName`, `classroomId`, `classLabel`, `isActive`, optional photo URL through authorized file endpoint |
| `StudentContextDto` | student summary, active term, classroom, primary guardian summary, current attendance/risk badges |
| `ActorSummaryDto` | user ID, display name, role snapshot |
| `AttachmentDto` | attachment ID, original name, content type, size, uploaded at/by; download URL is an authorized API route |
| `TransitionDto` | from state, to state, actor, timestamp, reason |
| `MetricBadgeDto` | metric code, current eligible term count, effective settings version, next threshold, severity, last occurrence, recalculation timestamp |
| `NotificationDeliveryDto` | recipient role/user label as authorized, delivery status, delivered/read timestamps |

## 3. `StudentsController`

Route: `/api/v1/students`

| Method and route | Permission | Request | Response / behavior |
|---|---|---|---|
| `GET /students` | `Student.View` | `StudentListQuery`: search, active, term, classroom, grade, risk metric/severity | `PagedResult<StudentListItemDto>`; school scope always applied |
| `GET /students/{studentId}` | `Student.View` or relationship-scoped guardian path | — | `StudentDetailsDto`; excludes confidential identifiers unless specifically permitted |
| `POST /students` | `Student.Create` | `CreateStudentRequestDto` | `201 StudentDetailsDto` |
| `PATCH /students/{studentId}` | `Student.Edit` | `UpdateStudentRequestDto`, version | Updated detail |
| `DELETE /students/{studentId}` | `Student.Archive` | `ArchiveStudentRequestDto`: reason, version | `204` or success envelope; soft delete only |
| `GET /students/{id}/timeline` | role-specific timeline read | from/to, event types | Unified, filtered student-affairs timeline; confidential events projected by permission |
| `POST /students/{id}/enrollments` | `Student.EnrollmentManage` | term ID, classroom ID, enrolled date, roll number | Enrollment DTO |
| `PATCH /students/{id}/enrollments/{enrollmentId}` | same | transfer/withdraw command with reason/version | Updated enrollment |
| `GET /students/{id}/guardians` | `Guardian.View` | — | Linked guardian summaries/capabilities |
| `POST /students/{id}/guardians` | `Guardian.LinkStudent` | guardian profile ID, relationship and capabilities | Link DTO |
| `DELETE /students/{id}/guardians/{linkId}` | `Guardian.LinkStudent` | reason/version | Soft-revoked link |

Key DTOs:

- `CreateStudentRequestDto`: student number, names, optional national ID/date of birth/gender, initial term/classroom, optional roll number. No `SchoolId` for school callers.
- `UpdateStudentRequestDto`: editable profile fields only; enrollment is a separate use case.
- `StudentDetailsDto`: identity/display fields, current enrollment, active guardian summaries, term metrics, recent permitted events, audit summary.

## 4. `ClassroomsController`

Route: `/api/v1/classrooms`

| Method and route | Behavior |
|---|---|
| `GET /classrooms?academicYearId=&termId=` | Scoped classroom catalog with enrollment counts |
| `POST /classrooms` | Create grade/section/class label |
| `PATCH /classrooms/{id}` | Edit inactive/display fields with concurrency check |
| `DELETE /classrooms/{id}` | Soft-delete only when no active enrollment/published timetable dependency |
| `GET /classrooms/{id}/students` | Roster for an authorized officer or currently assigned teacher |

DTOs include classroom ID, label, stage, grade, section, academic year, active state, and counts. The API does not expose `SchoolTimetableEntry.ClassLabel` as an identity.

## 5. Guardian self-service contracts

Route: `/api/v1/guardian`

| Method and route | Permission | Response |
|---|---|---|
| `GET /guardian/students` | `Guardian.ViewLinkedStudents` | Only active linked students and per-link capabilities |
| `GET /guardian/students/{id}/summary` | same + link check | Attendance/delay/recognition/summon/gate summary allowed to guardian |
| `GET /guardian/students/{id}/notifications` | `Notification.ViewOwn` | Student-filtered notifications belonging to current guardian user |

All other guardian actions use their feature controller but repeat the link/capability check.

## 6. Student attendance APIs

The existing `/api/v1/attendance` remains staff attendance. New route: `/api/v1/student-attendance` and controller `StudentAttendanceController`.

| Method and route | Permission | Request / response |
|---|---|---|
| `GET /student-attendance/sheet?date=&classroomId=` | `Attendance.ViewStudents` | `StudentAttendanceSheetDto` with all roster rows and saved status |
| `PUT /student-attendance/sheet` | `Attendance.ManageStudents` (Secretary baseline) | `SubmitAbsentRosterRequestDto`; atomically marks submitted IDs absent and every other actively enrolled roster student present |
| `PATCH /student-attendance/{attendanceId}` | `Attendance.OverrideCorrection` | status, correction reason, version |
| `GET /student-attendance/records` | `Attendance.ViewStudents` | paged filters: dates, class, student, `Present`/`Absent`/`AbsentExcused`, excuse state, severity |
| `GET /student-attendance/students/{studentId}` | authorized role/guardian relationship | Term history and absence metrics |
| `POST /student-attendance/{attendanceId}/excuses` | `Attendance.SubmitExcuse` | multipart PDF + excuse type + notes; `202/201` pending excuse |
| `GET /student-attendance/{attendanceId}/excuses` | related guardian or staff permission | Excuse history with review status |
| `GET /student-attendance/excuses/{excuseId}/attachments/{attachmentId}` | related aggregate authorization | `application/pdf` stream |
| `POST /student-attendance/excuses/{excuseId}/accept` | `Attendance.ReviewExcuse` | review note, version |
| `POST /student-attendance/excuses/{excuseId}/reject` | same | required rejection reason, version |

Important DTOs:

- `StudentAttendanceSheetRowDto`: student summary, `Present`/`Absent`/`AbsentExcused`, current excuse review state, recorded metadata, penalty-eligible absence badge.
- `SubmitAbsentRosterRequestDto`: `date`, `classroomId`, `absentStudentIds`, `rosterRevision`, and idempotency key. It never accepts a “present” list.
- The handler loads the authoritative active enrollment roster inside `SchoolScopeGuard`, rejects duplicate/non-roster IDs, and writes the full classroom/date sheet in one transaction. An empty `absentStudentIds` list means every enrolled student is present; it does not mean “no changes.”
- Accepting an excuse transitions the daily attendance row from `Absent` to `AbsentExcused`, emits a recalculation event, and preserves the absence for official/future Noor reporting. Rejecting it leaves `Absent` unchanged.
- `AbsenceExcuseDto`: excuse ID/type/status, guardian summary, submitted/review metadata, attachments.
- Upload validation errors use `400`; oversize uses `413`; unsupported/non-PDF uses `415`; unauthorized relationship uses safe `403/404` policy.

## 7. `MorningDelaysController`

Route: `/api/v1/morning-delays`

| Method and route | Behavior |
|---|---|
| `GET /morning-delays` | Filter by date, term, class, student, and threshold severity |
| `GET /morning-delays/{id}` | Delay details, cutoff snapshot, arrival time, allowed delivery audit |
| `POST /morning-delays/{id}/reason` | Guardian or authorized staff supplies reason; actor authority is derived |
| `POST /morning-delays/{id}/correct` | Authorized correction/supersession with mandatory reason; core audit retained |

`MorningDelayDto` contains arrival instant/local time, cutoff snapshot, delay minutes, term count, next threshold (`10` by default), reason, and immediate guardian notification status. It exposes no blocked-provider metadata.

## 8. Teacher Top-Priority API

Controller: `TeacherStudentAffairsController`  
Route: `/api/v1/teacher/student-affairs`

| Method and route | Permission | Purpose |
|---|---|---|
| `GET /teacher/student-affairs/current-context` | `TeacherQuickAction.View` | Resolves current published timetable entry, class, period, and roster from current time |
| `GET /teacher/student-affairs/periods/{entryId}/roster` | same | Explicit entry fallback for a current/today lesson |
| `GET /teacher/student-affairs/top-priority` | teacher dashboard | Pending acknowledgements and rapid-action context |

`TeacherCurrentContextDto` contains teacher, school local time, timetable revision, entry ID, period, classroom, roster, and permitted quick actions. It is the backend contract for the prominent Top Priority UI; the UI must not invent a roster or current teacher.

## 9. `SessionDelaysController`

Route: `/api/v1/session-delays`

| Method and route | Permission | Request / response |
|---|---|---|
| `POST /session-delays` | `SessionDelay.Create` | `CreateSessionDelayRequestDto`; returns detail and current metric badge |
| `GET /session-delays` | `SessionDelay.View` | scoped filters |
| `GET /session-delays/{id}` | same | detail/delivery audit |
| `POST /session-delays/{id}/correct` | `SessionDelay.Correct` | corrected values, mandatory reason, version |

Create DTO: student ID, timetable entry ID, occurred time (defaults server-side), optional delay minutes/reason. The server derives classroom, period, term, and reporter.

## 10. `AcademicConcernsController`

Route: `/api/v1/academic-concerns`

| Method and route | Behavior |
|---|---|
| `POST /academic-concerns` | Teacher quick action: student, timetable entry, category, description, occurred time |
| `GET /academic-concerns` | Scoped/paged; teacher own/current scope, Officer school scope, Social Worker assigned-case scope |
| `GET /academic-concerns/{id}` | Detail, metric, referral link, dispatch decision |
| `POST /academic-concerns/{id}/dispatch-decision` | Officer approves/suppresses guardian communication when required |
| `POST /academic-concerns/{id}/correct` | Authorized audited correction |

Each concern is queued for Officer dispatch review; the term escalation threshold is the effective setting, default `3`.

## 11. `BehaviorsController`

Route: `/api/v1/behaviors`

| Method and route | Behavior |
|---|---|
| `POST /behaviors` | Record incident from teacher/staff quick action |
| `GET /behaviors` | Paged filters by term, class, student, category, severity, referral, threshold |
| `GET /behaviors/{id}` | Detail with privacy projection |
| `POST /behaviors/{id}/classify` | Officer changes category/severity with reason/version |
| `POST /behaviors/{id}/dispatch-decision` | Officer approves/suppresses the guardian message; approval is always required |
| `POST /behaviors/{id}/refer` | Manual referral when permitted; automation uses internal command, not this HTTP call |
| `POST /behaviors/{id}/correct` | Supersede/correct with audit reason |

`CreateBehaviorIncidentRequestDto`: student ID, timetable entry ID optional, category, severity, description, occurred time, location, immediate action. The response includes the current dynamically recalculated eligible term count and any asynchronous actions queued, never claims a summon was sent until delivery state confirms it. Classification, correction, and soft-delete commands enqueue recalculation; a countability-changing downgrade can reduce the count and flag unresolved linked summons.

## 12. `RecognitionsController`

Route: `/api/v1/recognitions`

| Method and route | Behavior |
|---|---|
| `POST /recognitions` | Teacher records excellence/recognition |
| `GET /recognitions` | filters: week, month, term, class, type, teacher, student |
| `GET /recognitions/statistics` | counts/trends by week/month/term for Officer/School Manager |
| `GET /recognitions/{id}` | detail and guardian notification state |
| `POST /recognitions/{id}/correct` | authorized correction |

Statistics DTO contains period boundaries, total students recognized, total recognitions, by category/class, comparison to previous period, and generation time.

## 13. `ClassroomEntryPermitsController`

Route: `/api/v1/classroom-entry-permits`

| Method and route | Behavior |
|---|---|
| `POST /classroom-entry-permits` | Officer supplies student, reason, validity; server resolves current class/teacher |
| `GET /classroom-entry-permits` | filters by status/date/student/class/teacher/repetition severity |
| `GET /classroom-entry-permits/{id}` | full authorized detail |
| `POST /classroom-entry-permits/{id}/acknowledge` | resolved/current teacher acknowledges |
| `POST /classroom-entry-permits/{id}/revoke` | Officer revokes with reason/version |

`ClassroomEntryPermitDto` includes reason/time, resolved timetable/class/teacher, status, acknowledgements, guardian delivery state, and repeated-permit metric badge.

The metric is per term and uses the effective school setting, default `5`.

## 14. `GatePassesController`

Route: `/api/v1/gate-passes`

| Method and route | Permission | Request / response |
|---|---|---|
| `POST /gate-passes` | `GatePass.Request` | Guardian request; server proves student link/capability; `201 GatePassDto` |
| `GET /gate-passes/mine` | `GatePass.ViewOwn` | Guardian's requests for active linked students |
| `GET /gate-passes` | `GatePass.View` | officer/manager queue filters by status/date/class |
| `GET /gate-passes/security-queue` | security dashboard | Minimal approved queue DTO only |
| `GET /gate-passes/{id}` | role-specific | Detail projected by actor role |
| `POST /gate-passes/{id}/approve` | `GatePass.Approve` | approved exit window, optional approval note, version |
| `POST /gate-passes/{id}/reject` | `GatePass.Reject` | required reason/version |
| `POST /gate-passes/{id}/cancel` | `GatePass.CancelOwn` or override | reason/version; only valid states |
| `POST /gate-passes/{id}/teacher-acknowledgement` | current teacher | acknowledgement/version |
| `POST /gate-passes/{id}/security-acknowledgement` | `GatePass.AcknowledgeSecurity` | acknowledgement/version |
| `POST /gate-passes/{id}/exit` | `GatePass.Execute` | actual exit timestamp defaults server-side, verification method (`Visual`, `Manual`, `GuardianScreenshot`), verification note, optional gate note/version |
| `GET /gate-passes/{id}/history` | `GatePass.ViewAudit` | transition + notification delivery history |

Key DTOs:

- `CreateGatePassRequestDto`: linked student ID, desired exit time, reason, pickup person text name, optional relationship and free-text identity hint. There is no delegate/driver registration ID and no officer/teacher/guard ID.
- `GatePassDto`: request/decision/execution timestamps, role-appropriate student/pickup detail, current state, current teacher/class snapshot, notification states, version.
- `SecurityGatePassQueueItemDto`: pass ID, student number/name/photo, class, approved window, pickup snapshot, officer name/time, security state; no behavior/absence/summon data.

## 15. `ReferralsController`

Route: `/api/v1/referrals`

| Method and route | Behavior |
|---|---|
| `POST /referrals` | Manual referral with student, reason, source, priority; automated referrals use internal command |
| `GET /referrals` | Officer school queue or Social Worker assigned/unassigned queue |
| `GET /referrals/{id}` | Case detail, source snapshot, actions, and permission-filtered confidential notes |
| `POST /referrals/{id}/assign` | Assign/reassign worker |
| `POST /referrals/{id}/accept` | Assigned worker starts case |
| `POST /referrals/{id}/actions` | Add counseling/recommendation/committee action |
| `POST /referrals/{id}/resolve` | Resolution note and version |
| `POST /referrals/{id}/reopen` | Authorized exceptional reopen with reason |

`ReferralDto` never calculates its source count from today's mutable data; it returns stored source/threshold snapshots plus the current metric separately.

## 16. `SummonsController`

Route: `/api/v1/summons`

| Method and route | Behavior |
|---|---|
| `POST /summons` | Manual pending summons; auto rule invokes same application command internally |
| `GET /summons` | role-scoped list with status, priority, appointment, assigned worker |
| `GET /summons/mine` | Guardian's summons for linked students |
| `GET /summons/{id}` | role-specific detail |
| `POST /summons/{id}/schedule` | Social Worker sets/reschedules appointment; this queues guardian notice |
| `POST /summons/{id}/attend` | Pending → Attended with attendance notes |
| `POST /summons/{id}/start-observation` | Attended → UnderObservation; observation plan required |
| `POST /summons/{id}/mark-improved` | UnderObservation → Improved; outcome evidence/note required |
| `POST /summons/{id}/automation-impact-review` | Officer-only review of an unresolved automatic summons flagged after source-count decrease; records retain/acknowledge-source-change decision and mandatory rationale without inventing a fifth state |
| `GET /summons/{id}/history` | Immutable state history |

Schedule DTO: appointment `DateTimeOffset`, location, instructions, guardian profile ID from valid linked guardians, version. It does not allow “send tomorrow automatically.”

## 17. Messaging and office-hours APIs

### `ConversationsController` — `/api/v1/conversations`

| Method and route | Behavior |
|---|---|
| `GET /conversations` | Participant-only paged inbox, unread counts, student filter |
| `POST /conversations` | Start permitted thread; server validates guardian/student/teacher relationship |
| `GET /conversations/{id}` | Thread header and participants |
| `GET /conversations/{id}/messages` | Cursor-paged messages and receipts visible to caller |
| `POST /conversations/{id}/messages` | Send or queue under office-hour rules |
| `POST /conversations/{id}/read` | Mark through message/cursor as read |
| `POST /conversations/{id}/close` | Authorized close with reason |

`CreateConversationRequestDto`: linked student ID, thread type, target instructor profile ID or target staff role/user as allowed, subject, initial body. The server validates that a teacher currently teaches the student's class or is otherwise approved.

`SendMessageRequestDto`: body, optional reply-to message ID, idempotency key. Response includes `Sent` or `QueuedForOfficeHours` and `nextEligibleSendAt`.

### `OfficeHoursController` — `/api/v1/office-hours`

| Method and route | Behavior |
|---|---|
| `GET /office-hours/me/eligible` | Teacher's free periods derived from published timetable and work schedule |
| `GET /office-hours/me` | Current selections/effective dates |
| `PUT /office-hours/me` | Teacher selects eligible slots |
| `GET /office-hours/teachers/{instructorId}` | Guardian-visible contact windows for a relevant teacher |
| `PUT /office-hours/teachers/{instructorId}` | School Manager override with reason |

## 18. Notifications API

Route: `/api/v1/notifications`

| Method and route | Behavior |
|---|---|
| `GET /notifications` | Current user's paged in-app inbox |
| `GET /notifications/unread-count` | Lightweight badge count |
| `POST /notifications/{id}/read` | Mark one read |
| `POST /notifications/read-all` | Scoped current-user bulk read |
| `GET /notifications/pending-dispatch` | Officer queue containing behavior incidents and academic concerns only |
| `POST /notifications/{id}/approve` | Approve and queue dispatch |
| `POST /notifications/{id}/suppress` | Suppress with mandatory reason |

No endpoint lets a client provide an arbitrary recipient user ID for an automated student notice.

Daily absence, morning arrival delay, and session delay dispatch immediately to the linked guardian after the fact transaction commits. They never enter the approval endpoints. Behavior incidents and academic concerns always enter the Officer queue. No other fact type gains an implicit guardian dispatch policy from this specification.

## 19. Deferred integration APIs

- `[BLOCKED - PENDING CLIENT INPUT]` Biometric device endpoints are not specified or exposed.
- `[BLOCKED - PENDING CLIENT INPUT]` Noor export endpoints are not specified or exposed.
- `[BLOCKED - PENDING CLIENT INPUT]` The 14 quality-form endpoints are not specified or exposed.

Future integration controllers must consume stable core application use cases/events and remain separately permissioned; none may require changes to the core attendance, delay, referral, or summons contracts.

## 20. Settings, automation, and dashboard APIs

### `StudentAffairsSettingsController`

Route: `/api/v1/student-affairs/settings`

| Method and route | Permission | Behavior |
|---|---|---|
| `GET /student-affairs/settings` | `StudentAffairsSettings.View` | Returns current effective school settings, defaults, version, and effective date |
| `POST /student-affairs/settings` | `StudentAffairsSettings.Manage` | Creates the school row only when missing; validates ordered absence thresholds and positive values |
| `PUT /student-affairs/settings` | `StudentAffairsSettings.Manage` | Updates all school-wide values with row version and mandatory audit reason; creates a new effective policy snapshot and queues recalculation |
| `DELETE /student-affairs/settings` | `StudentAffairsSettings.Manage` | Soft-deletes customization and restores the locked defaults atomically; mandatory reason and row version |
| `GET /student-affairs/settings/history` | `StudentAffairsSettings.View` | Immutable settings-version history and actor/reason audit |

`SchoolStudentAffairsSettingsDto` contains morning delay `10`, behavior multiple `10`, academic concerns `3`, classroom-entry permits `5`, absence levels `3/5/10`, behavior-countability policy, arrival cutoff/grace, effective version, and concurrency token. Values shown are defaults; the current school row is authoritative.

### `StudentAffairsAutomationsController`

- `GET /student-affairs/automations/rules`
- `GET /student-affairs/automations/triggers`
- `GET /student-affairs/automations/failures`
- `POST /student-affairs/automations/failures/{id}/retry`

Compiled rules are read-only projections of the effective settings version. Threshold mutation occurs only through `StudentAffairsSettingsController`; the trigger/failure endpoints are operational reads/retries, not a second configuration surface.

### `StudentAffairsDashboardController`

- `GET /student-affairs/dashboard/teacher`
- `GET /student-affairs/dashboard/officer`
- `GET /student-affairs/dashboard/social-worker`
- `GET /student-affairs/dashboard/security`
- `GET /student-affairs/dashboard/guardian`
- `GET /student-affairs/dashboard/school-oversight`

Each is a distinct query/DTO so one role's confidential fields cannot leak by reusing a broad dashboard contract.

`SchoolOversightDashboardDto` is aggregate-only: totals for present, absent, and excused-absent students per class and school, plus non-identifying threshold/case counts. It contains no student case-note text, Social Worker counseling/session details, message bodies, guardian identifiers, or evidence attachment metadata.

## 22. Domain error to HTTP mapping

| Condition | HTTP status |
|---|---|
| Validation, invalid transition, term/cutoff configuration missing | `400` or `422` per existing middleware convention |
| Unauthenticated | `401` |
| Permission, tenant, relationship, current-teacher, assignment denial | `403` with enumeration-safe messaging |
| Missing/soft-deleted or intentionally hidden resource | `404` |
| Row version/idempotency conflict | `409` |
| Oversized file | `413` |
| Unsupported/non-PDF excuse | `415` |
| Accepted asynchronous work | `202` with operation/status resource |

## 23. Contract tests required before UI integration

- OpenAPI snapshot tests for every controller and DTO.
- Permission + tenant + object-scope matrix tests for each endpoint.
- Guardian route-ID tampering tests.
- Teacher current-class/timetable revision tests.
- Gate/summon invalid-transition and concurrent-version tests.
- Multipart PDF signature/size/malware-failure tests.
- Roster tests proving omitted enrolled students become `Present`, submitted roster students become `Absent`, empty lists mark the complete class present, and non-roster IDs fail atomically.
- Excuse tests proving acceptance changes `Absent` to `AbsentExcused`, preserves official absence semantics, and removes the penalty count.
- Idempotent create/retry tests for gate passes, attendance sheets, notifications, and automation actions.
- Projection tests proving Security Guard, Guardian, Main Manager, School Manager, and Instructor DTOs omit confidential fields; School Manager oversight must contain aggregates only.
- `ApiResponse<T>` consistency tests; binary endpoints verify content type and safe filename.
