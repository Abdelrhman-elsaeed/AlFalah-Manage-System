# Frontend Phase 5 — Social Worker CRM, Summons, Notification Approval, Office Hours, and Messaging

## 1. Social Worker CRM — referrals

### 1.1 Access and route

Frontend route: `/student-affairs/cases`  
Primary role: `SocialWorker`  
Permissions:

- `Referral.View` to list/detail.
- `Referral.Manage` to accept, add actions, resolve, and reopen.
- `Referral.ViewConfidential` for confidential projection.

The seeded `StudentAffairsOfficer` can view/create/assign referrals but does not have `Referral.Manage`; the CRM should give the Officer an assignment queue and the Social Worker the case-work actions.

### 1.2 API contracts

| Purpose | Endpoint | Request/response |
|---|---|---|
| List/Kanban source | `GET /api/v1/referrals` | `ReferralListQuery` → `PagedResult<ReferralDto>` |
| Detail | `GET /api/v1/referrals/{id}` | `ReferralDto` |
| Manual create | `POST /api/v1/referrals` with `Idempotency-Key` | `CreateReferralRequestDto`; Officer permission `Referral.Create` |
| Assign/reassign | `POST /api/v1/referrals/{id}/assign` | `AssignReferralRequestDto`; Officer permission `Referral.Assign` |
| Accept/start case | `POST /api/v1/referrals/{id}/accept` | `AcceptReferralRequestDto`; Social Worker `Referral.Manage` |
| Add case action | `POST /api/v1/referrals/{id}/actions` | `AddReferralActionRequestDto` |
| Resolve | `POST /api/v1/referrals/{id}/resolve` | `ResolveReferralRequestDto` |
| Reopen | `POST /api/v1/referrals/{id}/reopen` | `ReopenReferralRequestDto` |

`ReferralListQuery` filters: `status`, `priority`, `studentId`, `assignedWorkerUserId`, `isAssigned`, plus paging/search/sort.

### 1.3 Kanban/list model

Provide a view switch without changing the underlying query/cache.

Kanban columns map exactly to `StudentReferralStatus`:

| Enum | Arabic column | Typical action |
|---|---|---|
| `Open` | مفتوحة | Officer assigns |
| `Assigned` | مسندة | Assigned Social Worker accepts |
| `InProgress` | قيد المتابعة | Add action / resolve |
| `Resolved` | تم الحل | Review / exceptional reopen |
| `Closed` | مغلقة | Read-only unless backend allows reopen |

Do not implement drag-and-drop as an automatic state mutation: there is no generic status endpoint. Dragging may open the specific allowed action modal, but the state changes only after the corresponding endpoint succeeds.

List columns/card content from `ReferralDto`:

- Student summary.
- Priority: `Normal`, `High`, `Critical`.
- Status.
- Immutable `sourceSnapshot { sourceType, sourceEntityId, countSnapshot, thresholdSnapshot }`.
- Separate current `metric`, if returned; do not replace historical source counts with it.
- Assigned Social Worker.
- Created time and action count.
- Latest `rowVersion`.

### 1.4 Case drawer and mutations

Actions are `StudentCaseActionDto` entries and may include confidential narratives. Render them only after authorized detail load; never include them in Manager dashboards or notifications.

| Action | Request fields | Validation |
|---|---|---|
| Assign | `socialWorkerUserId`, optional `reason`, `rowVersion` | Officer-only. Never free-text a user ID. No current Student Affairs/API contract gives an Officer an authorized “assignable Social Workers” lookup, so keep assignment selection unavailable until that lookup is added or supplied by a trusted launch context. |
| Accept | `rowVersion` | Assigned Social Worker; refresh if assignment changed. |
| Add action | `actionType`, `description`, optional `actionAt`, optional `result`, `rowVersion` | Description required by UI; enum is `CounselingSession`, `GuardianSummon`, `GradeDeductionRecommendation`, `SuspensionRecommendation`, `ChildRightsCommitteeReferral`, `Other`. |
| Resolve | `resolutionNote`, `rowVersion` | Required meaningful resolution. |
| Reopen | `reason`, `rowVersion` | Required audited reason. |

After every success, replace the entire `ReferralDto` and its row version. On conflict, preserve the case-note draft locally, refetch detail, and require the Worker to confirm the note still applies. Never automatically duplicate a confidential action.

## 2. Guardian Summons CRM

### 2.1 Access and data

Frontend route: `/student-affairs/summons`  
Role: exact `SocialWorker` for lifecycle mutations  
Permissions: `Summon.View`, `Summon.Create`, `Summon.Schedule`, `Summon.MarkAttended`, `Summon.StartObservation`, `Summon.MarkImproved`, `Summon.ViewHistory`.

| Purpose | Endpoint | DTO |
|---|---|---|
| List | `GET /api/v1/summons` | `SummonListQuery` → `PagedResult<SummonDto>` |
| Detail | `GET /api/v1/summons/{id}` | `SummonDto` |
| Manual create | `POST /api/v1/summons` with `Idempotency-Key` | `CreateSummonRequestDto` |
| Schedule/reschedule | `POST /api/v1/summons/{id}/schedule` | `ScheduleSummonRequestDto` |
| Mark attended | `POST /api/v1/summons/{id}/attend` | `AttendSummonRequestDto` |
| Start observation | `POST /api/v1/summons/{id}/start-observation` | `StartSummonObservationRequestDto` |
| Mark improved | `POST /api/v1/summons/{id}/mark-improved` | `MarkSummonImprovedRequestDto` |
| History | `GET /api/v1/summons/{id}/history` | `SummonHistoryDto` |

Officer-only exception review:

- `POST /api/v1/summons/{id}/automation-impact-review`.
- Permission `Summon.ReviewAutomationImpact`.
- Request `ReviewSummonAutomationImpactRequestDto { decision, rationale, rowVersion }`, decision `Retain`, `Cancel`, or `Close`.

### 2.2 State machine visualization

The persisted enum has four states only:

`Pending → Attended → UnderObservation → Improved`

Arabic stepper:

1. `بانتظار الموعد/الحضور`.
2. `تم الحضور`.
3. `تحت الملاحظة`.
4. `تحسّن`.

Scheduling does not create a `Scheduled` enum state. Derive the visual substate:

- `Pending` + `scheduledAt=null` → `بانتظار تحديد موعد`.
- `Pending` + `scheduledAt!=null` → `موعد محدد — بانتظار الحضور`.

No-show/reschedule remains Pending; do not invent Cancelled or Missed statuses.

### 2.3 List/filter views

`SummonListQuery` supports `status`, `priority`, `appointmentDate`, `assignedWorkerUserId`, `studentId`, paging/search/sort.

Cards show only `SummonDto` data:

- Student, priority, status.
- Source/referral link and immutable count/threshold snapshots.
- Scheduled time/location.
- Selected guardian summary.
- Assigned Social Worker.
- `requiresOfficerReview` and safe `officerReviewReason` badge.
- `guardianNotifiedAt` actual timestamp; do not infer notification from scheduling alone.
- Row version.

### 2.4 Lifecycle forms

#### Create Pending summons

Request `CreateSummonRequestDto`:

- `studentId` from authorized Student/Referral context.
- Optional `referralId`.
- Required `reason`.
- `priority`: `Normal`, `High`, `Critical`.
- Required `guardianProfileId` from an active link.

Load linked guardians with `GET /api/v1/students/{studentId}/guardians` → `StudentGuardianLinkDto[]`; use `guardian.id` from the returned summary and only active links. The Social Worker is seeded with `Guardian.View` for this read.

#### Schedule or reschedule while Pending

Request `ScheduleSummonRequestDto`:

- Future `appointmentAt` as `DateTimeOffset`.
- Required `location`.
- Optional `instructions`.
- Active linked `guardianProfileId`.
- Latest `rowVersion`.

The handler requires the Worker to be assigned unless an override/assignment permission applies. Scheduling returns status Pending with updated schedule and row version.

#### Mark Attended

Request `AttendSummonRequestDto { attendanceNotes, rowVersion }`.

- Current status must be Pending.
- `attendanceNotes` is a required meeting summary.
- The selected summons guardian must still be actively linked.
- Attendance time/actor are server-generated.

#### Start Observation

Request `StartSummonObservationRequestDto { observationPlan, rowVersion }`.

- Current status must be Attended.
- One required structured narrative contains plan and measurable indicators; the current DTO does not expose separate goal/date/indicator fields.
- Do not collect fields the endpoint cannot persist.

#### Mark Improved

Request `MarkSummonImprovedRequestDto { outcomeEvidence, rowVersion }`.

- Current status must be UnderObservation.
- Outcome evidence is required.
- Improvement time/actor are server-generated.
- Resolving a linked referral remains a separate explicit referral action; do not silently resolve it.

### 2.5 History and conflict behavior

History comes only from `GET /summons/{id}/history` and its `TransitionDto[]`. Show from/to state, actor, timestamp, reason. Scheduling may appear as Pending→Pending and must be labeled `تم تحديد/تعديل الموعد`, not ignored as a no-op.

All transitions use the latest row version. On `409`, preserve notes/evidence, fetch detail and history, identify the winning transition, and disable impossible actions. A Worker must never force a stale state backward.

## 3. Officer notification approval queue

### 3.1 Access and endpoints

Frontend route: `/student-affairs/notification-approvals`  
Role: `StudentAffairsOfficer`  
Permissions: `Notification.ApproveDispatch`, `Notification.SuppressDispatch`

| Operation | Endpoint | Contract |
|---|---|---|
| List pending | `GET /api/v1/notifications/pending-dispatch?pageNumber=&pageSize=&search=&sortDirection=` | `PagedResult<PendingDispatchDto>` |
| Approve | `POST /api/v1/notifications/{id}/approve` | `ApproveNotificationRequestDto { rowVersion }` |
| Suppress | `POST /api/v1/notifications/{id}/suppress` | `SuppressNotificationRequestDto { reason, rowVersion }` |

This queue is only for behavior and academic-concern guardian dispatch. Daily absence, morning delay, and session delay are automatically dispatched and must never be routed here.

### 3.2 Queue UI

`PendingDispatchDto` columns:

- `factType` localized as Behavior or Academic Concern.
- `summary`.
- `studentId` as a navigation key only; display a student identity only if fetched via an authorized detail endpoint.
- `queuedAt` and waiting age.
- `rowVersion` hidden.

To review underlying context, open the matching endpoint based on `factType`/`factId`:

- Behavior: `GET /api/v1/behaviors/{factId}` → `BehaviorIncidentDto`.
- Academic concern: `GET /api/v1/academic-concerns/{factId}` → `AcademicConcernDto`.

Do not interpolate `summary` as HTML. Show category, severity/metric, reporter, occurrence time, and current dispatch decision from the fact DTO. Do not expose Social Worker confidential case notes.

### 3.3 Approve/suppress flows

Approve requires confirmation and the latest `rowVersion`; it has no editable message/template field in the current DTO. Do not present an editor whose content the API cannot persist.

Suppress requires a non-blank reason and latest row version. Confirmation copy must clarify that the fact remains recorded while Guardian dispatch is suppressed.

After success, remove the item from the pending page, refetch counts, and show the returned `PendingDispatchDto`. On conflict or a source fact already decided, refetch the queue/fact and present the winning state.

## 4. Teacher office-hours configuration

### 4.1 Teacher screen

Frontend route: `/student-affairs/office-hours`  
Role: `Instructor`  
Permission: `OfficeHours.ManageOwn`

| Purpose | Endpoint | Response/request |
|---|---|---|
| Eligible derived slots | `GET /api/v1/office-hours/me/eligible` | `OfficeHourSlotDto[]` |
| Current selection | `GET /api/v1/office-hours/me` | `OfficeHourSlotDto[]` |
| Save selection | `PUT /api/v1/office-hours/me` | `UpdateMyOfficeHoursRequestDto` |

`OfficeHourSlotDto` fields: `id`, `dayOfWeek`, `startsAt`, `endsAt`, `effectiveFrom`, optional `effectiveTo`, `source`, `isEligible`, `rowVersion`.

Group Sunday–Thursday/returned days in RTL day panels. A slot is selectable only when `isEligible=true`. Explain source:

- `DerivedFromPublishedTimetable` → `مستخرجة من الجدول المنشور`.
- `TeacherSelected` → `مختارة من المعلم`.
- `ManagerOverride` → `معتمدة بتعديل مدير المدرسة`.

Save request:

`UpdateMyOfficeHoursRequestDto { eligibleSlotIds, effectiveFrom, rowVersion }`.

- `eligibleSlotIds` contains unique IDs selected from the current eligible response.
- `effectiveFrom` is required.
- `rowVersion` must come from current server data, never an eligible slot that is not part of the current configuration.

Contract caution: the GET response is a list of per-slot DTOs while the update accepts one row version and no aggregate settings DTO. The UI must assert that current selected rows expose one consistent configuration token. If no current row or multiple different tokens are returned, disable Save and report `تعذر تحديد إصدار إعدادات الساعات المكتبية` rather than guessing. Backend contract clarification should eventually provide an aggregate row version.

### 4.2 Guardian view and Manager override

Guardian-visible hours:

- `GET /api/v1/office-hours/teachers/{instructorId}`.
- Permission `OfficeHours.View`.
- Show only returned slots for a teacher relevant to the Guardian's linked student/conversation; do not permit arbitrary directory enumeration.

Manager override:

- Route `/student-affairs/office-hours/teachers/{instructorId}`.
- Role `SchoolManager`, permission `OfficeHours.ManageSchool`.
- Teacher discovery uses `GET /api/v1/teachers?page=&pageSize=&search=` → `PagedResult<TeacherListItemDto>` (`Instructor.View`), followed by `GET /api/v1/teachers/{userId}` → `TeacherProfileDto`; pass its non-null `instructorProfileId` as `{instructorId}`. Do not pass the application `userId` to the office-hours route.
- `PUT /api/v1/office-hours/teachers/{instructorId}`.
- Request `OverrideTeacherOfficeHoursRequestDto { eligibleSlotIds, effectiveFrom, reason, rowVersion }`.
- Reason is required and audit-visible.

The same row-version consistency rule applies. After success replace the complete slot list with the response.

## 5. Messaging/chat UI

### 5.1 Access and contract map

Frontend route: `/student-affairs/messages`  
Roles with seeded `Messaging.ViewOwn`: `Guardian`, `Instructor`, `StudentAffairsOfficer`, `SocialWorker`.

| Purpose | Endpoint | Contract |
|---|---|---|
| Inbox | `GET /api/v1/conversations?studentId=&isUnread=&pageNumber=&pageSize=` | `PagedResult<ConversationDto>` |
| Start thread | `POST /api/v1/conversations` | `CreateConversationRequestDto` → `201 ConversationDto` |
| Header | `GET /api/v1/conversations/{id}` | `ConversationDto` |
| Messages | `GET /api/v1/conversations/{id}/messages?beforeMessageId=&pageNumber=&pageSize=` | `PagedResult<ConversationMessageDto>` |
| Send/reply | `POST /api/v1/conversations/{id}/messages` | `SendMessageRequestDto` → `SendMessageResultDto` |
| Mark read | `POST /api/v1/conversations/{id}/read` | `MarkConversationReadRequestDto` → boolean |
| Close | `POST /api/v1/conversations/{id}/close` | `CloseConversationRequestDto` → updated conversation |

The controller permits starting a thread only with `Messaging.StartGuardianTeacher` or `Messaging.StartGuardianAdministration`, both seeded for Guardian. Instructor/Officer/Social Worker can reply to participating threads but should not see a New Conversation button under the current grants.

### 5.2 Inbox

`ConversationDto` shows student, subject, thread type, status, participants, unread count, updated time, and row version.

Arabic thread types:

- `GuardianTeacher` → `ولي الأمر والمعلم`.
- `GuardianStudentAffairs` → `ولي الأمر وشؤون الطلاب`.
- `GuardianSocialWorker` → `ولي الأمر والموجه الطلابي`.

Filter by linked student and unread. Participant authorization is server-enforced. `SchoolManager` is not seeded with `Messaging.ViewOwn`; `Messaging.ViewAudit` alone does not pass the current conversation controller checks, so no Manager chat route should be exposed.

### 5.3 Start conversation

Request `CreateConversationRequestDto`:

| Field | Rule |
|---|---|
| `studentId` | Guardian-linked student only. |
| `threadType` | Allowed enum based on start permission. |
| `targetInstructorProfileId` | Required for `GuardianTeacher`; select from an authorized student/teacher context, never a free ID. |
| `targetStaffRole` / `targetStaffUserId` | Used only for an allowed administration/social-worker target; do not send arbitrary recipients. |
| `subject` | Required concise subject. |
| `initialBody` | Required initial message. |

The current Student Affairs contracts do not expose a dedicated “teachers for linked student” lookup. The New Teacher Conversation control must therefore be launched from a trusted context that already supplies an instructor profile ID, or remain unavailable until such a lookup is provided. Do not misuse `ActorSummaryDto.userId` where an instructor profile ID is required.

### 5.4 Thread timeline and cursor paging

- Load header and first message page in parallel after authorization.
- Older pages use `beforeMessageId` from the oldest loaded message; keep server order deterministic.
- Message row uses `ConversationMessageDto`: sender, body, optional reply reference, created time, delivery state, and authorized receipts.
- Render body as plain text with preserved newlines; no raw HTML/Markdown execution.
- Isolate IDs/times LTR, but message text follows its detected/declared direction.
- Mark read using the highest actually rendered message ID: `MarkConversationReadRequestDto { throughMessageId }`.

No WebSocket/SignalR contract is present. Use revalidation on focus and restrained polling (for example 20–30 seconds while an open thread is visible), with request cancellation and no duplicate announcements.

### 5.5 Sending and “Queued for Office Hours”

Endpoint request:

`SendMessageRequestDto { body, replyToMessageId, idempotencyKey }`.

The idempotency key is inside this JSON DTO, not an HTTP header. Generate it when the draft is submitted and preserve it until a definitive response.

Response `SendMessageResultDto` contains:

- `message: ConversationMessageDto`.
- `disposition: OfficeHoursDisposition`.
- Optional `nextEligibleSendAt`.

Map disposition:

| Enum | Arabic UI state |
|---|---|
| `SentImmediately` | تم الإرسال |
| `QueuedUntilOfficeHours` | مجدولة للساعات المكتبية |
| `BypassedForUrgency` | أرسلت كحالة عاجلة (مسجلة للتدقيق) |

For `QueuedUntilOfficeHours`, append the returned message once, show its actual `deliveryState` (normally Pending), display `سيتم التنبيه في أقرب ساعة مكتبية: {nextEligibleSendAt}`, and do not label it Delivered. The message is stored; the composer may clear after the returned DTO is safely in the timeline.

Guardian→Teacher may be queued outside office hours. Guardian↔Student Affairs/Social Worker is not restricted by teacher hours. The UI must display server disposition and must not independently decide a message was urgent or bypass hours.

### 5.6 Close thread

Endpoint: `POST /api/v1/conversations/{id}/close`  
Permission: `Messaging.CloseThread`  
Request: `CloseConversationRequestDto { reason, rowVersion }`.

Require reason and latest conversation row version. After success make the composer read-only and render status `Closed`. On conflict refetch header before allowing any further message/action.

## 6. Notification/inbox shell integration

All messaging roles also have in-app notification access through:

- `GET /api/v1/notifications` → `PagedResult<StudentAffairsNotificationDto>`.
- `GET /api/v1/notifications/unread-count` → integer.
- `POST /api/v1/notifications/{id}/read`.
- `POST /api/v1/notifications/read-all`.

Use these for the global bell and deep links. Validate every deep-linked route/object server-side; notification content is not authorization proof.

## 7. Phase 5 RBAC summary

| Capability | Guardian | Instructor | Officer | Social Worker | School Manager |
|---|---:|---:|---:|---:|---:|
| View own conversations/reply | Yes | Yes | Yes | Yes | No through current controller |
| Start Guardian–Teacher/Admin thread | Yes | No | No | No | No |
| Manage own office hours | No | Yes | No | No | No |
| View relevant teacher hours | Yes | Yes | No seeded permission | No seeded permission | Yes |
| Override teacher hours | No | No | No | No | Yes |
| Referral case work | No | No | Assign/view | Manage assigned cases | No confidential CRM |
| Summons lifecycle | View own through `GET /api/v1/summons/mine` → `PagedResult<SummonDto>` | No | View/review automation impact | Create/schedule/transition | Read-only scope if exposed |
| Approve/suppress guardian notices | No | No | Yes | No | No |

## 8. Phase 5 acceptance criteria

- Kanban transitions invoke dedicated endpoints; no generic status mutation is invented.
- Historical referral source snapshots remain distinct from current metrics.
- Summons UI has exactly four persisted states; scheduling is a Pending substate.
- Each summons transition validates current state and latest row version.
- Notification approval queue contains only Behavior/Academic Concern and has no editable outbound body.
- Office-hours Save is disabled when the API cannot supply one unambiguous current configuration row version.
- Chat participants see only their own threads; no Manager chat is exposed from audit permission alone.
- Queued teacher messages are visibly Pending/queued with `nextEligibleSendAt`, never falsely Delivered.
