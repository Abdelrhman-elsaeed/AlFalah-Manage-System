# Frontend Phase 4 — Gate Pass Request, Approval, and Physical Exit

## 1. Canonical workflow

Arabic domain term: `استئذان خروج`.

The client must render the backend state machine exactly:

`Requested → Approved → SecurityAcknowledged → Exited`

Terminal/alternate states: `Rejected`, `Cancelled`, `Expired`.

Teacher acknowledgement is a receipt/action outside the gate-pass status enum. The Guard cannot jump directly from `Approved` to `Exited`; security acknowledgement is a required preceding transition.

| Status | Arabic label | Allowed next UI actions |
|---|---|---|
| `Requested` | بانتظار المراجعة | Officer Approve/Reject; requesting Guardian Cancel |
| `Approved` | معتمد | Security acknowledge/readiness; authorized cancellation |
| `SecurityAcknowledged` | تمت المطابقة — بانتظار الخروج | Guard physical Exit |
| `Exited` | تم الخروج | Read-only receipt/history |
| `Rejected` | مرفوض | Read-only; show safe reason when returned |
| `Cancelled` | ملغي | Read-only |
| `Expired` | منتهي | Read-only/exception state |

All transition requests carry the current `rowVersion`. Never calculate or decode it; treat it as an opaque Base64 token.

## 2. Guardian request form

### 2.1 Access and supporting data

Frontend route: `/student-affairs/gate-passes/mine/new`  
Role: exact `Guardian`  
Permission: `GatePass.Request`

| Purpose | Endpoint | DTO |
|---|---|---|
| Linked students/capability | `GET /api/v1/guardian/students` | `GuardianStudentDto[]` |
| Existing own requests | `GET /api/v1/gate-passes/mine?date=&status=&pageNumber=&pageSize=` | `PagedResult<GatePassDto>` |
| Create | `POST /api/v1/gate-passes` with `Idempotency-Key` | `CreateGatePassRequestDto` → `201 GatePassDto` |

Student selection contains only active returned links with `canRequestGatePass=true`. If none are available, block the form and show `لا يوجد طالب مرتبط مخول بطلب استئذان خروج`.

### 2.2 Request fields

| Arabic field | DTO field | Rule |
|---|---|---|
| الطالب | `studentId` | Required linked/capable student; never a free ID. |
| وقت الخروج المطلوب | `desiredExitTime` | Required future `DateTimeOffset`; must be a school day. Backend recognizes Saturday–Thursday and rejects Friday. |
| سبب الاستئذان | `reason` | Required non-blank. |
| اسم مستلم الطالب | `pickupPersonName` | Required free-text snapshot; no delegate registry ID exists. |
| صلة القرابة/العلاقة | `pickupRelationship` | Optional text. |
| علامة تحقق من الهوية | `pickupIdentityHint` | Optional minimal hint; warn not to enter a full sensitive identity-document copy unless school policy requires it. |

Do not submit Guardian profile ID, school ID, class ID, term ID, reviewer, teacher, or guard. The backend derives them.

### 2.3 Overlap prevention and idempotency

The backend rejects another active pass in a ±30-minute window around `desiredExitTime`. The UI should provide an early warning by comparing the selected time with own active `Requested`, `Approved`, and `SecurityAcknowledged` items returned by `GET /gate-passes/mine`, but this is advisory only. Submit is still validated server-side to cover other tabs/devices and stale data.

Generate one UUID `Idempotency-Key` when the user begins the final submission. Preserve it across timeouts/retries. If the response is unknown, refresh `GET /gate-passes/mine` before allowing a new request. A duplicate idempotency key can return the existing pass as a successful response.

### 2.4 Success and own-request list

Use returned `GatePassDto` as the receipt:

- Student, requested/exit times, reason, pickup snapshot.
- Current status.
- Approval window/review/exit timestamps when present.
- Current classroom/teacher when authorized and resolved.
- Notification delivery states.
- Latest `rowVersion`.

Do not say `تمت الموافقة` after creation; initial status is `Requested`. The list route uses `GET /api/v1/gate-passes/mine`. A Guardian with `GatePass.CancelOwn` may cancel a valid own request through `POST /api/v1/gate-passes/{id}/cancel` with `CancelGatePassRequestDto { reason, rowVersion }`; expose the action only for states the server accepts.

## 3. Officer approval queue

### 3.1 Access and list

Frontend route: `/student-affairs/gate-passes`  
Mutation role: exact `StudentAffairsOfficer`  
Permissions: `GatePass.View`, `GatePass.Approve`, `GatePass.Reject`

| Purpose | Endpoint | Response/request |
|---|---|---|
| Pending queue | `GET /api/v1/gate-passes?status=Requested&date=&classroomId=&pageNumber=&pageSize=&sortBy=requestedExitAt&sortDirection=asc` | `PagedResult<GatePassDto>` |
| Detail | `GET /api/v1/gate-passes/{id}` | `GatePassDto` |
| Approve | `POST /api/v1/gate-passes/{id}/approve` | `ApproveGatePassRequestDto` |
| Reject | `POST /api/v1/gate-passes/{id}/reject` | `RejectGatePassRequestDto` |
| Audit | `GET /api/v1/gate-passes/{id}/history` | `GatePassHistoryDto` |

The seeded `SchoolManager` has several gate permissions, but the approval handler explicitly requires the `StudentAffairsOfficer` role. Manager access, if exposed, must therefore remain a read-only oversight/audit view; do not show Approve/Reject based on seeded permission alone.

### 3.2 Data table

Columns, right-to-left:

- Requested exit time and urgency/overdue indicator.
- Student display name/number.
- Current class.
- Pickup person and relationship.
- Short reason.
- Requested-at age.
- Status.
- `مراجعة` action.

Keep full identity hint and notifications inside the authorized detail drawer. Sort by requested exit time; pagination/filter values go to the endpoint, not client-only filtering over one page.

### 3.3 Approve modal

Load `GET /gate-passes/{id}` immediately before opening or submitting to obtain the latest `rowVersion` and status.

Request `ApproveGatePassRequestDto`:

| Field | UI rule |
|---|---|
| `windowStartsAt` | Required ISO `DateTimeOffset`; must be before end. |
| `windowEndsAt` | Required; must still be in the future. |
| `approvalNote` | Optional. |
| `rowVersion` | Latest detail token. |

The requested exit time must fall inside the approved window. Show this relationship graphically and validate locally. The backend also requires a valid guardian link, active enrollment, pickup name, a school day, and safely resolved published timetable/current teacher. If timetable resolution fails, show the server failure and leave the pass Requested; do not let the client select an arbitrary teacher.

On success replace the row with returned `GatePassDto` and remove it from a Requested-only queue.

### 3.4 Reject modal

Endpoint request: `RejectGatePassRequestDto { reason, rowVersion }`.

- Reason is mandatory.
- Confirmation copy names the student and requested time.
- Reject is available only for `Requested`.
- After success remove from pending queue and show a reversible UI toast only in the visual sense; there is no undo endpoint, so do not offer an actual Undo action.

### 3.5 Concurrency

On `409`/concurrency failure, close neither modal nor draft. Fetch the detail, show the winning state/action, and disable any transition no longer valid. Never resubmit approval with a refreshed token automatically.

## 4. Security execution UI

### 4.1 Access and queue

Frontend route: `/student-affairs/gate-passes/security`  
Role: exact `SecurityGuard`  
Permissions: `GatePass.AcknowledgeSecurity`, `GatePass.Execute`

Endpoint: `GET /api/v1/gate-passes/security-queue?date={today}&pageNumber=&pageSize=` → `PagedResult<SecurityGatePassQueueItemDto>`.

This operational queue is distinct from the read-only security dashboard. It may contain `Approved` and `SecurityAcknowledged` items appropriate to the current date/window.

Each card displays only the safe queue DTO:

- Student name, number, photo, class.
- Approved time window.
- Pickup person name, relationship, identity hint.
- Approving Officer and approval time.
- Status and current `rowVersion`.

No attendance/behavior/referral/summon/message links are permitted.

### 4.2 Two-step physical workflow

#### Step A — match and acknowledge

For `Approved` within its active window, the Guard checks the pickup person and clicks `تمت المطابقة — جاهز للخروج`.

Endpoint: `POST /api/v1/gate-passes/{id}/security-acknowledgement`  
Request: `AcknowledgeGatePassRequestDto { rowVersion }`  
Response: `GatePassDto` with status `SecurityAcknowledged` and a new row version.

Acknowledgement must not be hidden inside the Exit call. It means the Guard has reviewed the pass; it does not mean the student has left.

#### Step B — physical exit

Only when the student physically crosses the gate does the Guard click the large destructive/committing button:

`تم الخروج ✅`

Open a compact verification sheet before final commit:

| Field | DTO field | Rule |
|---|---|---|
| طريقة التحقق | `verificationMethod` | Required: `Visual` (تحقق بصري), `Manual` (تحقق يدوي), `GuardianScreenshot` (لقطة شاشة ولي الأمر). |
| ملاحظة التحقق | `verificationNote` | Required non-blank. |
| ملاحظة البوابة | `gateNote` | Optional. |
| وقت الخروج | `exitedAt` | Display server-current time as preview; submit null. The current handler records server time and ignores client authority. |
| إصدار السجل | `rowVersion` | Latest token from acknowledgement/detail. |

Endpoint: `POST /api/v1/gate-passes/{id}/exit` with `ExecuteGatePassRequestDto`.

The server requires status `SecurityAcknowledged`, an active approved window, valid verification method/note, correct school/role, and current row version. On success, replace the card with a receipt showing returned `exitedAt` and status `Exited`, then remove it from the active queue after a brief confirmation.

### 4.3 Guard usability and safety

- Use large high-contrast cards/buttons suitable for a gate tablet.
- Show a live countdown to window end using server timestamps; refresh server state before commit if the card is older than 30 seconds.
- Disable acknowledgement outside the approved window and label why.
- Require a distinct final confirmation for Exit; this action represents a physical event and has no undo endpoint.
- Do not chain acknowledgement and exit from one click.
- Do not optimistically mark Exited.
- If the device reconnects after timeout, refetch the queue/detail before offering retry. If status is already `Exited`, show the server receipt as success.

## 5. Optional teacher acknowledgement surface

The Teacher Top Priority alert may expose:

- `POST /api/v1/gate-passes/{id}/teacher-acknowledgement`.
- Request `AcknowledgeGatePassRequestDto { rowVersion }`.
- Role/permission: `Instructor` + `GatePass.AcknowledgeTeacher` and backend-resolved teacher scope.

This acknowledgement never changes `GatePassStatus`, must not block Security execution in the UI unless the backend explicitly rejects, and is separate from Guard acknowledgement.

## 6. Error/state behavior

| Failure | UI outcome |
|---|---|
| `400` future/school-day/overlap/window/state validation | Keep form; show rule; refresh record after invalid-state response. |
| `403` link, school, exact role, or teacher/guard scope | Enumeration-safe denial; remove mutation controls. |
| `404` | Remove stale queue row after refresh. |
| `409` row version | Preserve draft, refetch, show new state, require explicit decision. |
| Network timeout on create | Reuse same idempotency key and check Mine before retry. |
| Network timeout on exit | Refetch queue/detail first; never blindly repeat a physical-exit command. |

## 7. Phase 4 acceptance criteria

- Guardian can select only linked students with `canRequestGatePass=true`.
- Form sends pickup text snapshot fields and no invented delegate ID.
- Overlap warning uses ±30 minutes, while the backend remains authoritative.
- Officer queue uses Requested status and exact `GatePassDto` row version.
- School Manager never receives nonfunctional approval controls.
- Guard must acknowledge before `تم الخروج ✅`.
- Exit records the server-returned time and requires method plus verification note.
- No Security component renders confidential student-affairs data.
