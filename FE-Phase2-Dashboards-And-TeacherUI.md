# Frontend Phase 2 — Dashboards and Teacher Top-Priority UI

## 1. Phase-wide rules

All dashboard calls use the authenticated active school. Dashboard DTOs are deliberately role-specific and must not be merged into a single broad client model.

| Dashboard | Route | Role | Permission | Endpoint / DTO |
|---|---|---|---|---|
| Teacher | `/student-affairs/teacher` | `Instructor` | `StudentAffairsDashboard.Teacher` and quick actions use `TeacherQuickAction.View` | `GET /api/v1/student-affairs/dashboard/teacher` → `TeacherStudentAffairsDashboardDto` |
| Security summary | `/student-affairs/security` | `SecurityGuard` | `StudentAffairsDashboard.Security` | `GET /api/v1/student-affairs/dashboard/security` → `SecurityStudentAffairsDashboardDto` |
| Guardian | `/student-affairs/guardian` | `Guardian` | `StudentAffairsDashboard.Guardian` | `GET /api/v1/student-affairs/dashboard/guardian` → `GuardianStudentAffairsDashboardDto` |
| School oversight | `/student-affairs/oversight` | `SchoolManager` | `StudentAffairsDashboard.SchoolOversight` | `GET /api/v1/student-affairs/dashboard/school-oversight` → `SchoolOversightDashboardDto` |

Loading, empty, denied, and failed states must occupy the dashboard region without showing stale data from a previous school/user. Background refresh must not replace a newer response with an older one.

## 2. Teacher Top Priority — `أولوية المعلم الآن`

### 2.1 Data acquisition

The prominent teacher panel is driven by backend timetable resolution. The UI must not infer the current class from browser time or let an ordinary Instructor choose an arbitrary school/class.

| Purpose | Endpoint | Response |
|---|---|---|
| Current class/roster | `GET /api/v1/teacher/student-affairs/current-context` | `ApiResponse<TeacherCurrentContextDto>` |
| Top priority with acknowledgements | `GET /api/v1/teacher/student-affairs/top-priority` | `ApiResponse<TeacherTopPriorityDto>` |
| Dashboard counts | `GET /api/v1/student-affairs/dashboard/teacher` | `ApiResponse<TeacherStudentAffairsDashboardDto>` |
| Explicit eligible period fallback | `GET /api/v1/teacher/student-affairs/periods/{entryId}/roster` | `ApiResponse<TeacherCurrentContextDto>` |

Recommended load sequence:

1. Fetch `GET /teacher/student-affairs/top-priority` as the single initial screen payload when available. Its `context` is the same current-context shape and it adds gate-pass/entry-permit acknowledgement counts and alerts.
2. If only the quick-action panel is mounted, fetch `GET /current-context` directly.
3. Refresh at period boundaries, on window focus, and after a successful quick action. Use `schoolLocalTime` and `currentPeriod.endsAt` to schedule the boundary refresh.
4. Use `schoolTimeZone` for display. Do not calculate school time from the device timezone.

`TeacherCurrentContextDto` fields to render:

- `teacher: ActorSummaryDto` — display name and role snapshot.
- `schoolLocalTime`, `schoolTimeZone`, `timetableRevision`.
- Optional `currentPeriod: TeacherPeriodContextDto` with `timetableEntryId`, `period`, `startsAt`, `endsAt`, and `classroom`.
- `roster: StudentSummaryDto[]`.
- `permittedQuickActions: string[]` — the final component-level action allowlist.

### 2.2 Context states

| State | UI behavior |
|---|---|
| Current period present, roster non-empty | Show class label, period/time, searchable roster, and allowed actions. |
| Current period present, empty roster | Show `لا يوجد طلاب مسجلون في هذه الحصة`; keep actions disabled. |
| `currentPeriod=null` | Show `لا توجد حصة حالية حسب الجدول المنشور`; hide roster actions. Do not fall back to all school students. |
| Timetable revision changes during an open form | Keep the draft, refetch context, and require the teacher to reselect a student if the student/entry is no longer in scope. |
| `403` | Full route denial; Instructor role alone is insufficient without `TeacherQuickAction.View`. |

The explicit period-roster endpoint is only a fallback when the UI already has a trusted `entryId` from a current/today timetable or notification. It is not an arbitrary entry-ID browser.

### 2.3 Layout and interaction

- Header: `الحصة الحالية`, class label, `الحصة {period}`, school-local time range.
- Priority strip: `pendingGatePassAcknowledgements`, `pendingEntryPermitAcknowledgements`, and textual `alerts` from `TeacherTopPriorityDto`.
- Roster: large student rows/cards with photo fallback, display name, student number in LTR isolation, and a single selected state.
- Quick-action rail: exactly four actions, filtered by both permission and `permittedQuickActions`.
- Mobile/tablet: selecting a student opens a bottom sheet; desktop: a side panel. The selected student and timetable entry remain visible above the form.

### 2.4 Quick-action forms

Every form must submit the selected `student.id` and the backend-resolved `currentPeriod.timetableEntryId` where its DTO requires one. A successful create replaces the form with a receipt showing the returned fact ID and status/metric; it does not claim notifications or referrals completed unless the returned DTO says so.

#### A. Behavior incident — `مخالفة سلوكية`

Endpoint: `POST /api/v1/behaviors`  
Permission/role: `Behavior.Create`, exact role `Instructor`  
Request: `CreateBehaviorIncidentRequestDto`  
Response: `201 ApiResponse<BehaviorIncidentDto>`

| Field | Source/control | Validation |
|---|---|---|
| `studentId` | Selected roster student | Required positive ID; read-only in form. |
| `schoolTimetableEntryId` | `currentPeriod.timetableEntryId` | DTO is nullable, but the current handler requires a positive value; never submit null from Top Priority. |
| `category` | Controlled school/domain category code | Required non-blank. Label is localized; submit stable code. |
| `severity` | `Low`, `Medium`, `High`, `Critical` | Required enum string. Arabic: منخفضة، متوسطة، عالية، حرجة. |
| `description` | Multiline narrative | Required; trim; do not place confidential counseling detail here. |
| `occurredAt` | Default now in school time; optional edit | May be omitted; if supplied it cannot be more than five minutes in the future. |
| `location` | Optional text | Trim. |
| `immediateAction` | Optional text | Label `الإجراء الفوري المتخذ`. |

Returned `BehaviorIncidentDto` supplies `dispatchDecision` (initially `PendingOfficerDecision`), `metric`, optional `referralId`, `queuedActions`, and `rowVersion`. Show `بانتظار اعتماد إشعار ولي الأمر`; do not say the guardian was notified.

#### B. Academic concern — `ملاحظة أكاديمية`

Endpoint: `POST /api/v1/academic-concerns`  
Permission/role: `AcademicConcern.Create`, exact role `Instructor`  
Request: `CreateAcademicConcernRequestDto`  
Response: `201 ApiResponse<AcademicConcernDto>`

Fields: `studentId`, required positive `schoolTimetableEntryId`, required category, required description, and optional `occurredAt`. Apply the same school-time and five-minute future rule. Returned `dispatchDecision` starts at `PendingOfficerDecision`; display the returned `metric` and any `referralId` without exposing internal case notes.

#### C. Session delay — `تأخر عن الحصة`

Endpoint: `POST /api/v1/session-delays`  
Permission/role: `SessionDelay.Create`, exact role `Instructor`  
Request: `CreateSessionDelayRequestDto`  
Response: `201 ApiResponse<SessionDelayDto>`

| Field | Rule |
|---|---|
| `studentId` | From current roster. |
| `schoolTimetableEntryId` | Required current entry. |
| `occurredAt` | Optional; server defaults now; no more than five minutes in future. |
| `delayMinutes` | Optional by DTO; if supplied it must be an integer `>= 0`. |
| `reason` | Optional short explanation. |

The returned DTO includes period, reporter, metric, and optional `guardianNotification`. The domain policy sends session-delay notifications automatically; display actual delivery status (`Pending`, `Processing`, `Delivered`, `Failed`, `Suppressed`) rather than assuming delivery.

#### D. Recognition — `إشادة وتميّز`

Endpoint: `POST /api/v1/recognitions`  
Permission: `Recognition.Create` (seeded for `Instructor`)  
Request: `CreateRecognitionRequestDto`  
Response: `201 ApiResponse<RecognitionDto>`

| Field | Rule |
|---|---|
| `studentId` | Selected current-roster student. |
| `recognitionType` | Stable configured code; required. |
| `title` | Required concise Arabic title. |
| `description` | Required evidence/description. |
| `recognizedAt` | Optional; default server time. |

This DTO has no timetable-entry field. Keep the student selection anchored to the resolved roster for UX/scope safety, but send only the DTO fields. Show `guardianNotification` only when returned; the locked notification matrix does not guarantee automatic recognition dispatch.

### 2.5 Submission safeguards

- One action modal may be active at a time.
- Disable submit after the first click; do not optimistically append a fact before the server returns its ID.
- On `400`, retain fields and show the error summary.
- On `403` object-scope denial, refresh current context because the period/roster may have changed.
- These create DTOs do not carry `rowVersion`; a create failure is not resolved by inventing one.

## 3. Security Guard dashboard — read-only summary

This phase's dashboard is a minimalist, read-only view. Mutation controls belong to the Phase 4 execution screen.

Endpoint: `GET /api/v1/student-affairs/dashboard/security`  
Role/permission: `SecurityGuard` + `StudentAffairsDashboard.Security`  
Response: `SecurityStudentAffairsDashboardDto`

Render:

- `approvedGatePasses: SecurityGatePassQueueItemDto[]`, limited to the backend's safe gate projection.
- `counts: DashboardCountDto[]` with backend `code`, localized/fallback `label`, `count`, and `severity`.

Each queue item may display only:

- Student display name/number/photo from `student`.
- `classLabel`.
- Approved start/end window.
- Pickup person's name, relationship, and identity hint.
- Officer name and approval time.
- Gate status.

Do not link to attendance, behavior, academic concerns, referrals, summons, guardian messages, or case notes. Sort visually by the approved window start and clearly label `الآن`, `قريبًا`, or `انتهت المهلة` from server timestamps. Refresh every 30 seconds while visible and on focus; suspend when hidden.

Empty state: `لا توجد استئذانات خروج معتمدة لليوم`. A failed fetch must not reuse yesterday's cached queue.

## 4. Guardian dashboard — `أبنائي`

### 4.1 Endpoints and DTOs

| Purpose | Endpoint | Response |
|---|---|---|
| Dashboard aggregate | `GET /api/v1/student-affairs/dashboard/guardian` | `GuardianStudentAffairsDashboardDto { students, actions }` |
| Authoritative links/capabilities | `GET /api/v1/guardian/students` | `GuardianStudentDto[]` |
| Per-student card detail | `GET /api/v1/guardian/students/{studentId}/summary` | `GuardianStudentSummaryDto` |
| Student notices | `GET /api/v1/guardian/students/{studentId}/notifications` | `PagedResult<GuardianNotificationDto>` |

Role/permissions: exact role `Guardian`; dashboard permission plus `Guardian.ViewLinkedStudents`. The server performs the object-level link check for every `studentId`.

### 4.2 Card composition

Start with the dashboard payload for a fast shell, then join it in memory with `GET /guardian/students` by `student.id` to obtain `canSubmitExcuses`, `canRequestGatePass`, and `receivesNotifications`. Fetch a per-student summary on card expansion or detail navigation rather than issuing unbounded requests for every linked student.

Each collapsed card shows:

- Name, student number, class label, active status, photo fallback.
- Metric badges from `StudentContextDto.metrics`: count, severity, next threshold; localize each `StudentTermMetricCode`.
- Capability actions only when the corresponding booleans are true: `رفع عذر غياب`, `طلب استئذان خروج`.

Expanded summary uses `GuardianStudentSummaryDto`:

- `pendingSummons` → `استدعاءات بانتظار الإجراء`.
- `activeGatePasses` → `استئذانات خروج نشطة`.
- `recentRecognitions` → `إشادات حديثة`.
- Context active term/class/primary guardian and metrics.

Dashboard-level `actions: DashboardCountDto[]` are aggregate calls to action, not per-student values. Do not attach an aggregate count to an arbitrary child card.

### 4.3 Privacy and empty states

- The frontend must never allow a typed/edited `studentId`; navigation IDs come only from returned linked students.
- A `403/404` after a link is revoked removes the card after refresh without revealing the student's new relationship state.
- No linked students: `لا يوجد طلاب مرتبطون بحساب ولي الأمر. راجع إدارة المدرسة.`
- Confidential referral actions, Social Worker notes, Officer audits, and other guardians are never rendered.

## 5. School Manager oversight dashboard

Endpoint: `GET /api/v1/student-affairs/dashboard/school-oversight`  
Role/permission: `SchoolManager` + `StudentAffairsDashboard.SchoolOversight`  
Response: `SchoolOversightDashboardDto`

### 5.1 Visual sections

1. Attendance totals: `present`, `absent`, `absentExcused`.
2. Classroom comparison table/chart from `byClassroom`, where each row is `ClassroomAttendanceAggregateDto { classroomId, classLabel, present, absent, absentExcused }`.
3. Non-identifying threshold counts from `thresholdCounts: DashboardCountDto[]`.
4. Aggregate case/summons counts from `caseCounts: DashboardCountDto[]`.
5. Freshness indicator using `generatedAt`.

Percentages are client-derived only when the denominator is non-zero and must be labeled as a presentation calculation. Raw API totals remain visible for auditability.

### 5.2 Confidentiality boundary

This route must consume only `SchoolOversightDashboardDto`. It must not call referral/summons/message detail endpoints to enrich the dashboard. Never show:

- Student names or student numbers in case/threshold sections.
- Social Worker actions, counseling notes, resolution notes, or summon narratives.
- Message bodies or conversation participants.
- Guardian identities.
- Excuse attachments/evidence.

Drill-down may navigate only to another Manager-authorized aggregate/list surface. A case-count card must not link to confidential case detail merely because `SchoolManager` has `Referral.View` in the seed map.

## 6. Refresh, cache, and accessibility rules

- Role dashboards are cached only per `(userId, activeSchoolId, route)` and cleared on logout/school change.
- Teacher context and security queue are short-lived operational data; revalidate on focus.
- Guardian and Manager dashboards may use stale-while-revalidate, but visibly show `generatedAt` where supplied.
- Announce count changes through a polite live region; do not repeatedly announce every polling refresh.
- Charts require equivalent tables and text summaries.
- Severity colors must be paired with Arabic labels/icons.

## 7. Phase 2 acceptance criteria

- Teacher roster/class always originates from `TeacherCurrentContextDto`.
- Quick actions send the exact endpoint-specific DTO and no authoritative school/actor fields.
- Behavior and academic creates say `بانتظار الاعتماد`, not `تم إشعار ولي الأمر`.
- Security summary renders only `SecurityGatePassQueueItemDto` fields and has no mutations.
- Guardian cards are restricted to server-returned linked students and link capabilities.
- Manager dashboard is aggregate-only and contains no confidential case/message content.
