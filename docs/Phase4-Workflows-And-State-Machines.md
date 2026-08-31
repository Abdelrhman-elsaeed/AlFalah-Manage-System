# Phase 4 — Workflows and State Machines

**Status:** Locked technical specification — no implementation authorized

## 1. Workflow implementation pattern

Every transition is implemented as a dedicated application command handler, not as controller branching and not as a large workflow service.

Each handler follows this order:

1. Authenticate and resolve the current actor.
2. Load the aggregate inside the caller's school scope.
3. Enforce granular permission plus object-level authority.
4. Validate current state and `RowVersion`.
5. Apply the state change and append an immutable transition/history row.
6. Append domain event(s) and `OutboxMessage` row(s) in the same database transaction.
7. Commit once.
8. Return the new state immediately; background workers perform notifications, PDF generation, and other external work.

Notification delivery failure does not roll back an already valid business transition. The outbox retries and exposes failure state to authorized staff.

## 2. Gate Pass (استئذان الخروج)

### 2.1 State model

```text
Requested ──approve──> Approved ──security acknowledges──> SecurityAcknowledged ──physical exit──> Exited
    │                      │
    ├──reject──────────> Rejected
    ├──guardian cancel──> Cancelled
    │                      ├──officer cancel/revoke──────> Cancelled
    └──window lapses────> Expired
                           └──window lapses before exit──> Expired
```

Teacher notification/acknowledgement is recorded independently and is not a gate-pass state. This prevents delivery retries or a teacher's unread notice from creating combinatorial workflow states.

### 2.2 Step-by-step workflow

#### Step 1 — Guardian request

1. Guardian opens only students returned by `GET /guardian/students`.
2. Guardian submits student, desired exit time, reason, pickup person's text name, and optional relationship/identity hint. The request does not require a registered delegate or driver.
3. `RequestGatePassHandler` verifies:
   - active `Guardian` role and `active_school_id`;
   - active `GuardianProfile` and `StudentGuardian` link;
   - `CanRequestGatePass=true` and link validity dates;
   - student is active and enrolled in the active term;
   - requested time belongs to a school day and acceptable request window;
   - no other active overlapping gate pass exists;
   - idempotency key has not already created a request.
4. The handler derives `SchoolId`, term, student/class context, actor, and creates `GatePass(Requested)` plus the first transition.
5. It raises `GatePassRequestedEvent` and commits.
6. Background notification handler creates a high-priority in-app notice for Student Affairs Officers. No WhatsApp is used.

#### Step 2 — Student Affairs Officer review

1. Officer sees the Requested queue sorted by requested time and priority.
2. On approve, `ApproveGatePassHandler` reloads the request under scope and locks by row version.
3. It verifies current state `Requested`, valid guardian link, active student/enrollment, non-expired request, and a usable guardian-entered pickup name/hint.
4. It resolves the student's current classroom and teacher from:
   - active `StudentEnrollment`;
   - published `SchoolTimetable` for the term;
   - school-local day and current/approved exit period;
   - `SchoolTimetableEntry.ClassroomId` with `ClassLabel` fallback only during migration.
5. It snapshots the resolved timetable/entry/instructor/period into the pass and changes state to `Approved`.
6. It raises `GatePassApprovedEvent`; the transaction commits.
7. On reject, `RejectGatePassHandler` requires a rejection reason, changes Requested → Rejected, writes transition, and raises `GatePassRejectedEvent`.

#### Step 3 — Event-driven notifications

`GatePassApprovedEvent` fans out through independent idempotent handlers:

- Guardian notification: approval, approved time window, pickup details and current state.
- Current teacher notification: student identity, class, approval, requested release time, Officer identity, acknowledge action.
- Security notification: minimal gate queue entry and acknowledge action.
- Audit projection: delivery targets, event correlation, no confidential case data.

Each handler creates one deduplicated notification row. A worker retries delivery. The HTTP approval call never waits for these fan-out operations.

If current teacher cannot be resolved:

- The pass may remain approved only if an authorized officer confirms a fallback route.
- The system creates an operational warning and routes to the classroom/School Affairs fallback queue.
- It must not silently select an arbitrary instructor.

#### Step 4 — Teacher acknowledgement

1. The current teacher opens the top-priority notice.
2. `AcknowledgeGatePassByTeacherHandler` proves the user matches the snapshotted/current authorized teacher or has an override permission.
3. It records acknowledged by/at and a delivery receipt without changing the gate-pass state.
4. Failure to acknowledge remains visible to the Officer and is retried/escalated; it does not allow the guard to fabricate acknowledgement.

#### Step 5 — Security acknowledgement

1. Guard sees only approved, non-expired passes for the active school and relevant date/window.
2. Guard compares the person at the gate with the guardian-entered text/hint and performs visual/manual verification or checks a screenshot presented/provided by the guardian. There is no delegate registry lookup.
3. `AcknowledgeGatePassBySecurityHandler` verifies `Approved`, time window, guard school/role, and row version.
4. State changes Approved → SecurityAcknowledged; acknowledgement timestamp is server-generated.
5. `GatePassSecurityAcknowledgedEvent` notifies the Officer operational dashboard.

Acknowledgement means “the guard has reviewed and is ready”; it does **not** mean the student has left.

#### Step 6 — Physical exit execution

1. Only when the student physically crosses the gate does the guard press `تم الخروج ✅`.
2. `ExecuteGatePassHandler` verifies `SecurityAcknowledged`, active exit window, correct school, `GatePass.Execute`, row version, and a recorded verification method (`Visual`, `Manual`, or `GuardianScreenshot`) with note.
3. It records `ActualExitAt` from server time and changes state to `Exited`. The screenshot itself is not required to be uploaded or retained by this locked core blueprint.
4. It appends transition and raises `StudentExitedSchoolEvent`.
5. Background handlers notify Guardian, Officer, and—where useful—the current teacher that exit completed.

The system explicitly detects the two failures described by the client:

- Guard acknowledged but student did not exit: pass remains `SecurityAcknowledged`, becomes overdue/expired, and appears in an exception queue.
- Guard marked exited when student did not leave: this is an auditable security action requiring School Manager correction/incident handling; the original transition is never erased.

### 2.3 Transition authorization and invariants

| Transition/action | Actor | Required permission | Invariants |
|---|---|---|---|
| Create Requested | linked Guardian | `GatePass.Request` | active authorized link; no overlap |
| Requested → Approved | Student Affairs Officer | `GatePass.Approve` | valid request/student/window; timetable resolution/fallback |
| Requested → Rejected | Officer | `GatePass.Reject` | reason required |
| Requested → Cancelled | requesting Guardian | `GatePass.CancelOwn` | not reviewed |
| Approved → SecurityAcknowledged | Security Guard | `GatePass.AcknowledgeSecurity` | non-expired, correct school/window |
| SecurityAcknowledged → Exited | same/authorized Guard | `GatePass.Execute` | physical exit; server time; manual verification method/note |
| Active → Cancelled | Officer/School Manager | approve/override policy | mandatory reason; notify all parties |
| Active → Expired | background worker | internal action | window elapsed and not exited |

### 2.4 Timeout jobs

- Requested passes after requested time plus configured tolerance become `Expired` if never reviewed.
- Approved/SecurityAcknowledged passes become `Expired` after the execution window if no exit occurs.
- Unacknowledged Security/Teacher notices create reminders/escalation, not fake state transitions.
- All jobs use a database lease/idempotency key so multiple app instances cannot expire the same pass twice.

## 3. Guardian Summons (استدعاء ولي الأمر)

### 3.1 Required state model

```text
Pending ──guardian attends──> Attended ──observation begins──> UnderObservation ──outcome verified──> Improved
```

These are the four client-mandated business states. Appointment rescheduling/no-show is kept as appointment/history data while the summons remains `Pending`; additional terminal states such as Cancelled/ClosedWithoutImprovement require product approval before adding them to the enum.

### 3.2 Creation sources

A `Pending` summons can originate from:

- 10th morning-arrival delay in a term.
- Each 10-behavior threshold occurrence (10, 20, 30, …) in a term.
- Third academic concern in a term.
- Fifth penalty-eligible absence in a term and the tenth if no unresolved summons is already active for that source metric.
- Manual Student Affairs Officer or Social Worker decision.

Automation does two internal actions idempotently:

1. Create/open `StudentReferral` for the source threshold.
2. Create `GuardianSummon(Pending)` linked to the referral when the rule calls for it.

It notifies the Social Worker/Officer internally. It does **not** choose an appointment or notify the guardian with an invented “tomorrow” date.

### 3.3 Pending and scheduling

1. Social Worker opens the pending referral/summons queue.
2. The detail displays the immutable source snapshot: metric, term, threshold, occurrence number, relevant facts, and priority.
3. Social Worker selects a valid linked guardian and sets appointment time, location, and instructions.
4. `ScheduleGuardianSummonHandler` validates the state is still Pending and the guardian link is active.
5. It records schedule data and raises `GuardianSummonScheduledEvent`.
6. A background handler sends the in-app summons to the guardian. Delivery/read state is visible to staff.
7. Rescheduling writes a new history record and sends a replacement notice; old schedule history remains.

### 3.4 Pending → Attended

1. Social Worker verifies actual guardian attendance.
2. Handler requires attendance time, attendee guardian, and meeting summary.
3. State becomes `Attended`; `AttendedAt` and actor are server/audit recorded.
4. It raises `GuardianSummonAttendedEvent` for dashboards and optional next-action reminders.

A missed appointment does not become Attended. It remains Pending with a no-show appointment-history entry and can be rescheduled.

### 3.5 Attended → UnderObservation

1. Social Worker records an observation plan: goals, start/end dates or review date, responsible staff, and measurable indicators.
2. Handler requires the current state `Attended` and at least one observation indicator.
3. State becomes `UnderObservation` and raises `StudentObservationStartedEvent`.
4. Follow-up actions/notes are appended as `StudentCaseAction` records. The original incident/absence/delay rows are not modified.

### 3.6 UnderObservation → Improved

1. Social Worker records evidence/outcome notes and the improvement verification date.
2. Handler verifies `UnderObservation`, required outcome detail, and actor assignment/permission.
3. State becomes `Improved`, history is appended, and related referral may be resolved by a separate explicit command.
4. `StudentImprovementConfirmedEvent` queues guardian/officer notifications and dashboard refresh projections.

The case is not auto-marked improved merely because no new incidents occurred for a number of days unless the school later defines and approves such a rule.

### 3.7 Dynamic-count impact review and duplicate prevention

- A threshold occurrence has one `AutomationTriggerLedger` row.
- Do not create two simultaneous `Pending`/`UnderObservation` summons for the same student and metric. A later threshold trigger attaches to the active referral/summons and raises priority while keeping its own trigger-ledger row.
- Behavior counts are recomputed from current active/countable facts. Correction, soft-delete, or a severity/category downgrade below the configured countability policy may reduce the count.
- When a decrease means the original threshold is no longer satisfied, the trigger ledger is marked `SourceNoLongerSatisfied`. Any linked unresolved summons is marked `RequiresOfficerReview`; it is never silently deleted or automatically moved to `Improved`.
- The Officer reviews the original trigger snapshot, current count, cause of decrease, current summons state, and Social Worker activity. The Officer records an audited `Retain` or `AcknowledgeSourceNoLongerSatisfied` review decision; the four-state summons lifecycle is unchanged, and already attended/history facts remain immutable.

## 4. Office Hours messaging

### 4.1 Actors and thread types

- Guardian ↔ Teacher, always linked to a student.
- Guardian ↔ Student Affairs Officer, optionally linked to a student.
- Guardian ↔ Social Worker, linked to a student/referral when confidential case context is involved.

The platform is the official communication record; routine workflow messages are not sent over WhatsApp.

### 4.2 How teacher office hours are derived

1. Load the active term and published school timetable.
2. Load the teacher's lessons and standby entries for Sunday–Thursday/approved school workdays.
3. Load configured school presence/work boundaries and period times.
4. Eligible office slots are periods inside school duty time that do not contain a lesson or other blocking assignment.
5. Teacher selects/accepts eligible slots; School Manager may override with an audit reason.
6. The system stores effective `TeacherOfficeHour` rows. A timetable publish event recalculates eligibility and flags conflicting selected slots for review.

This implements the client explanation: if a teacher teaches periods 1–3, periods 4–7 may be office hours, but only while the teacher is on duty at school.

### 4.3 Message send policy

| Sender/recipient | Submission behavior | Delivery/response behavior |
|---|---|---|
| Guardian → Teacher | Guardian may submit at any time | Stored immediately; teacher notification can be queued to next office hour |
| Teacher → Guardian | Allowed only in an active office-hour window, unless explicit emergency/manager override | Sent immediately and audited |
| Guardian ↔ Student Affairs Officer | Not restricted by teacher office hours | Normal in-app delivery |
| Guardian ↔ Social Worker | Not restricted by teacher office hours | Normal in-app delivery, case privacy applies |

This closes both gaps cited in the call: a teacher cannot claim “I was teaching,” and routine communication is not pushed outside the teacher's on-campus duty time.

### 4.4 Thread creation authorization

- Guardian can start a teacher thread only for a linked student and a teacher who currently teaches that student's classroom/subject or is explicitly allowed by school policy.
- Teacher sees only threads in which they are a participant.
- Officer/Social Worker visibility follows school scope and case assignment.
- A School Manager audit view, where granted, is metadata-only (thread identifiers, participants' role labels, timestamps, delivery state). It does not expose confidential Social Worker case notes, counseling/session detail, evidence, or message bodies.
- Threads/messages include an optional provider-neutral context reference so a future homework/assignment module can link a conversation, reflecting the client's request to connect guardian communication closely with teacher assignments. The current repository has no homework domain, so this Spec Kit does not invent or implement one.

### 4.5 Delivery, receipts, and audit

- Message creation is transactional with an outbox event.
- One `MessageReceipt` per recipient records queued, delivered, read, and failure timestamps.
- `QueuedForOfficeHours` stores the next eligible local instant.
- Edits after delivery are not permitted. Corrections are a new reply referencing the prior message.
- Soft-hiding a message from a participant does not erase the audit copy.
- Notification contents avoid sensitive case details on lock screens; users open the authenticated thread for full text.

### 4.6 Failure and timetable edge cases

- No published timetable: Guardian may submit; teacher delivery is queued and Officer sees a configuration warning. The system does not infer that every period is free.
- Newly published timetable invalidates a selected office hour: mark it conflicted, calculate next valid window, notify teacher/manager.
- School holiday/closure: calendar suppresses teacher windows.
- Urgent safeguarding communication uses an explicit audited override path, not a hidden bypass.

## 5. Classroom Entry Permit workflow

Although not one of the two requested state-machine headings, this flow is required by the transcript.

1. Student is already with the Student Affairs Officer/administration due to a problem, injury, or another justified reason.
2. Officer searches the student and enters reason plus validity time.
3. The handler resolves active enrollment, current school-local period, published timetable entry, and current teacher automatically.
4. It creates `ClassroomEntryPermit(Issued)` and emits `ClassroomEntryPermitIssuedEvent`.
5. Background handlers notify the current teacher and guardian directly according to the final meeting clarification.
6. Teacher sees the permit in Top Priority and acknowledges; state becomes `AcknowledgedByTeacher`.
7. A worker expires unused permits at `ValidUntil`.
8. Per-term repetition counter updates and alerts Officer/Social Worker when the effective school threshold is crossed; the default is exactly `5` per term.

This workflow is not a request to leave a classroom for the bathroom and is not a school gate exit.

## 6. Daily attendance and absence-excuse workflow

1. The Secretary opens a classroom/date roster and submits only the absent student IDs.
2. In one scoped transaction, the handler validates the IDs against active enrollment, marks them `Absent`, and marks all other enrolled roster students `Present`.
3. `StudentAbsenceLoggedEvent` immediately queues a guardian in-app message with student/date.
4. Guardian uploads a PDF excuse and type for the linked absence.
5. The excuse stays `Pending`; daily attendance remains `Absent` and continues to count toward penalties.
6. The Student Affairs Officer accepts or rejects with a mandatory review audit. Rejection leaves `Absent` unchanged.
7. Acceptance changes daily status to `AbsentExcused`, retains the excuse/PDF/history, and emits recalculation. The row remains an absence for official reporting and the future Noor seam but no longer counts toward penalties.
8. The worker applies the exact term matrix to penalty-eligible `Absent` days only:
   - `3`: visual/color-coded dashboard alert;
   - `5`: internal referral plus `GuardianSummon(Pending)`;
   - `10`: internal referral plus pending summon (or attach to the active one) plus Child Rights Committee recommendation.

`[BLOCKED - PENDING CLIENT INPUT]` Noor export format/API processing stops at the stable `Absent`/`AbsentExcused` core facts. This specification defines no Noor batch, file, endpoint, adapter, or confirmation workflow.

## 7. Teacher quick-action workflow

1. Teacher opens the Top Priority area.
2. Backend resolves current timetable/roster; UI does not allow arbitrary school/class selection.
3. Teacher chooses exactly one action: session delay, academic concern, behavior incident, or recognition.
4. Each action calls its own endpoint/command and stores one domain fact.
5. The fact raises its own domain event.
6. Counter/notification/referral handlers run asynchronously.
7. Response reports the saved fact and current metric badge, but does not block while referrals/notices are sent.

Notification policy is fixed: daily absence, morning arrival delay, and session delay notify the Guardian automatically and immediately through the outbox. Behavior incidents and academic concerns create a pending Officer dispatch decision and reach the Guardian only after approval. Recording HTTP requests never wait for delivery.

## 8. Workflow observability

Authorized operational dashboards expose:

- State age and overdue status.
- Last transition actor/time.
- Notification delivery/read status without leaking content.
- Outbox retry/failure count.
- Correlation ID linking domain fact, trigger, referral, summons, and notification.
- Concurrency conflicts and override reasons.

Metrics/alerts:

- Requested gate passes awaiting review near requested exit time.
- Approved passes not acknowledged by Security.
- Security-acknowledged passes not executed.
- Pending summons without appointment.
- Scheduled summons unread by guardian.
- Under-observation summons past review date.
- Queued teacher messages with no valid future office hour.

## 9. State-machine acceptance scenarios

- Guardian retries the same request after a timeout: one gate pass exists.
- Two Officers approve simultaneously: one succeeds, one receives `409`.
- Notification provider fails after approval: pass remains Approved; delivery retries.
- Guard tries Exited before acknowledgement: rejected.
- Guard acknowledges but never executes: overdue/expired exception appears.
- Teacher tries to acknowledge another teacher's notice: rejected.
- Rule creates a Pending summons: no guardian appointment notice is sent until Social Worker schedules it.
- Social Worker tries UnderObservation before Attended: rejected.
- Improved transition lacks outcome evidence/note: rejected.
- Accepting an excuse changes `Absent` to `AbsentExcused` and recalculates the penalty metric without erasing the absence.
- A corrected/soft-deleted/downgraded behavior incident drops the eligible count; an affected unresolved summons is flagged for Officer review and is not silently deleted.
- Guardian sends teacher message outside office hour: stored and queued, not lost.
- Teacher tries routine reply outside office hour: rejected/queued according to approved UI policy, never silently sent.
- Timetable changes while a pass is Approved: snapshotted teacher/class remains audit history; active exception routing can update recipients without rewriting the snapshot.
