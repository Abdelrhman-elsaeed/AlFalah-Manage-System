# Phase 1 — Domain and Database Schema

**Status:** Locked technical specification — no implementation authorized  
**Module:** Student Affairs Platform  
**Target:** ASP.NET Core 8, EF Core 8, SQL Server, layered modular monolith  
**Sources:** `طلبات العميل قبل المكالمة.txt`, `المكالمة مع العميل.txt`, the current repository, and the project constitution

## 1. Purpose and non-negotiable constraints

This phase introduces the academic and student-affairs data model that the current system does not have. It must be additive and must not change the meaning of existing features.

- `AttendanceRecord` remains **staff attendance**. Student attendance uses a separate aggregate and table family.
- `StudentAnalyzerReport` remains an AI analysis of uploaded tabular files. It is not a student master record and must not become one.
- `InstructorProfile` remains the teacher identity/profile. No second teacher table is allowed.
- The published `SchoolTimetable` is the authoritative source for resolving the current teacher and period. The initial migration keeps `SchoolTimetableEntry.ClassLabel` for compatibility while introducing a real `Classroom` link.
- Every student-affairs read and mutation passes through `SchoolScopeGuard`. A client-supplied `SchoolId` never establishes authority.
- Every mutable business row uses the established soft-delete columns: `IsDeleted`, `DeletedAt`, and `DeletedByUserId`, plus a global query filter.
- Workflow history, outbox rows, and audit ledgers are immutable records. They are retained rather than soft-deleted.
- All foreign keys use `DeleteBehavior.Restrict`; business-level soft-delete cascades are explicit and transactional.
- Arabic text is Unicode and uses the repository's `Arabic_CI_AS` collation. Technical keys and enum names remain English.
- All business timestamps are `DateTimeOffset` in UTC. School-local dates and periods are also stored explicitly where counting depends on the Cairo/Riyadh calendar.

## 2. Architectural placement

The module remains inside the existing modular monolith:

| Layer | Student Affairs responsibility |
|---|---|
| `AlFalah.Domain` | Entities, value concepts, states, domain events, invariants that do not require I/O |
| `AlFalah.Application` | Feature folders, commands, queries, DTOs, validators, use-case handlers, and provider-neutral ports for storage and notifications |
| `AlFalah.Infrastructure` | EF configurations and repositories, `SchoolScopeGuard`, outbox/inbox workers, biometric adapters, file storage, QuestPDF rendering |
| `AlFalah.Api` | Thin REST controllers, authentication/permission checks, multipart binding, `ApiResponse<T>` |
| `AlFalah.Shared` | Existing response/paging primitives only; no student-affairs business rules |

Complex workflows use one command/query handler per use case. There must not be a single `StudentAffairsService` or other god class.

## 3. Canonical domain language

These terms are deliberately separate because the transcript uses several kinds of “delay” and “permission” interchangeably.

| Canonical term | Arabic business term | Definition | Do not conflate with |
|---|---|---|---|
| `MorningArrivalDelay` | التأخر الصباحي | A student biometric arrival after the configured school arrival cutoff | Daily absence or delay entering a lesson |
| `DailyStudentAttendance` | الغياب اليومي | The school's daily presence/absence record created by the morning classroom round | Staff `AttendanceRecord` or biometric punches |
| `SessionDelay` | التأخر عن الحصة | A teacher-recorded late arrival to a particular lesson/period | Morning arrival delay |
| `AcademicConcern` | التأخر الدراسي | A teacher-recorded academic-performance concern counted toward the three-occurrence referral rule | `SessionDelay` |
| `BehaviorIncident` | مخالفة سلوكية | A recorded behavior violation, with category and severity | Academic concern |
| `StudentRecognition` | التميز | A positive recognition recorded by a teacher | A behavior incident with a positive score |
| `ClassroomEntryPermit` | إذن دخول القاعة | Permission issued by Student Affairs to enter the student's current lesson after a justified delay | A gate pass to leave school or a bathroom pass |
| `GatePass` | استئذان الخروج | Guardian-requested and officer-approved authorization for a student to leave the school gate | Classroom entry permit |
| `StudentReferral` | تحويل للموجه الطلابي | An internal case handed to the Social Worker for action | A guardian summons |
| `GuardianSummon` | استدعاء ولي الأمر | A scheduled request for the guardian to attend school and its follow-up lifecycle | A notification only |

## 4. Common persistence contract

Unless identified as immutable, every new tenant-owned entity has:

- `Id` (`int` or `long` for high-volume ledgers)
- `SchoolId` (required FK to `School`)
- `CreatedAt`, `CreatedByUserId`
- `UpdatedAt`, `UpdatedByUserId`
- `IsDeleted`, `DeletedAt`, `DeletedByUserId`
- `RowVersion` (`rowversion`) on aggregates with state transitions or concurrent edits

Tenant-owned principals expose an alternate key `(SchoolId, Id)`. Tenant-owned dependants use composite foreign keys including `SchoolId` wherever EF Core can enforce them. This makes a cross-school relationship invalid in SQL even if application validation is accidentally bypassed.

High-volume facts use an immutable append model. Corrections create a new revision/status-history row or mark the fact superseded; they do not erase the original event.

## 5. Academic foundation entities

### 5.1 `Student`

The master student record for one school.

| Field | Type/constraint | Purpose |
|---|---|---|
| `SchoolId` | required | Tenant owner |
| `StudentNumber` | `nvarchar(50)`, required | School-visible student number |
| `NationalId` | `nvarchar(30)`, nullable, protected | Optional government identifier; never returned in list DTOs |
| `FirstName`, `MiddleName`, `LastName` | Unicode, required as configured | Student legal/display name |
| `DateOfBirth` | `DateOnly?` | Optional profile detail |
| `Gender` | enum/nullable | Optional demographic detail |
| `IsActive` | required | Enrollment eligibility |
| `ProfilePhotoStorageKey` | nullable | Provider-neutral file reference |
| common audit/soft-delete fields | required | Lifecycle |

Indexes and invariants:

- Filtered unique index `(SchoolId, StudentNumber)` where `IsDeleted = 0`.
- Optional filtered unique `(SchoolId, NationalId)` where the value is non-null and `IsDeleted = 0`.
- A student cannot be active when all current enrollments are closed; deactivation closes future access but preserves history.

### 5.2 `GuardianProfile`

A school-scoped guardian persona attached to an existing `ApplicationUser`.

| Field | Type/constraint | Purpose |
|---|---|---|
| `SchoolId` | required | Allows one Identity account to have children in different schools without mixing tenant data |
| `ApplicationUserId` | required FK | Login identity |
| `NationalId` | optional/protected | Guardian verification when required |
| `PreferredContactLanguage` | `ar`/`en` | Notification rendering |
| `IsActive` | required | Access gate |
| common audit/soft-delete fields | required | Lifecycle |

- Filtered unique `(SchoolId, ApplicationUserId)` for active rows.
- Guardian permissions never imply access to every student in the school; access comes only from an active `StudentGuardian` link.

### 5.3 `StudentGuardian`

Explicit many-to-many association between students and guardians.

| Field | Purpose |
|---|---|
| `SchoolId`, `StudentId`, `GuardianProfileId` | Composite tenant-safe links |
| `RelationshipType` | Father, Mother, LegalGuardian, Other |
| `IsPrimary` | Primary guardian for default notifications |
| `ReceivesNotifications` | Whether routine student notices are sent |
| `CanSubmitExcuses` | Allows absence-excuse upload |
| `CanRequestGatePass` | Allows exit requests |
| `ValidFrom`, `ValidTo` | Historical authorization window |
| common audit/soft-delete fields | Lifecycle |

- Filtered unique `(SchoolId, StudentId, GuardianProfileId)`.
- At most one active primary guardian per student, enforced with a filtered unique index.

### 5.4 `AcademicTerm`

The counting boundary explicitly confirmed in the meeting: thresholds reset each semester/term.

| Field | Purpose |
|---|---|
| `SchoolId` | Tenant |
| `AcademicYearId` | FK to the existing global `AcademicYear` catalog |
| `Semester` | Existing `TimetableSemester` value |
| `StartsOn`, `EndsOn` | Exact local-date counting window |
| `IsActive` | One current term per school |
| audit/soft-delete fields | Lifecycle |

- Unique `(SchoolId, AcademicYearId, Semester)` where active/not deleted.
- Exactly one active term per school is enforced by the application and a filtered unique strategy.

### 5.5 `SchoolStudentAffairsSettings`

One school-scoped active settings aggregate is the authoritative source for Student Affairs thresholds. Application handlers and workers must never embed threshold magic numbers.

| Field | Default | Purpose |
|---|---:|---|
| `SchoolId` | — | Tenant key; one active row per school |
| `MorningDelayThresholdPerTerm` | `10` | Morning-arrival-delay escalation |
| `BehaviorIncidentMultiplePerTerm` | `10` | Escalate at 10, 20, 30, … countable incidents |
| `AcademicConcernThresholdPerTerm` | `3` | Academic-concern escalation |
| `ClassroomEntryPermitThresholdPerTerm` | `5` | Repeated classroom-entry-permit escalation |
| `AbsenceVisualAlertThresholdPerTerm` | `3` | Dashboard color/visual alert |
| `AbsenceReferralThresholdPerTerm` | `5` | Internal referral plus pending guardian summons |
| `AbsenceChildRightsThresholdPerTerm` | `10` | Referral, pending summons, and Child Rights Committee recommendation |
| `BehaviorCountabilityPolicy` | school default catalog policy | Determines whether the current severity/category is countable; a downgrade below the policy boundary removes the incident from the count |
| `ArrivalCutoffLocalTime`, `ArrivalGraceMinutes` | school configured | Determines morning delay without a code constant |
| `Version`, `EffectiveFrom` | initial version | Provides an immutable policy snapshot for trigger audit |
| common audit/soft-delete/concurrency fields | — | Settings lifecycle and concurrent-edit protection |

- Filtered unique `(SchoolId)` for the active, non-deleted settings row.
- The Student Affairs Officer may update the school-wide values through a dedicated audited use case. An update creates a new effective policy version/snapshot; it does not rewrite the settings snapshot on already completed trigger actions.
- Settings changes enqueue recalculation for active-term metrics. Any unresolved action invalidated by the new value is flagged for Officer review rather than silently deleted.
- `AbsentExcused` is always excluded from the absence penalty metric. This is a locked invariant, not an editable switch.

### 5.6 `Classroom`

A real academic class/section, replacing string-only identity for new student workflows.

| Field | Purpose |
|---|---|
| `SchoolId`, `AcademicYearId` | Tenant/year |
| `Stage`, `GradeLevel`, `Section` | Structured placement |
| `ClassLabel` | Existing display label such as `3/1` |
| `IsActive` | Current availability |
| audit/soft-delete fields | Lifecycle |

- Filtered unique `(SchoolId, AcademicYearId, ClassLabel)`.
- `SchoolTimetableEntry` gains nullable `ClassroomId`; the original `ClassLabel` remains a historical display snapshot and import/export compatibility field.

### 5.7 `StudentEnrollment`

The student-to-classroom relationship for a specific term.

| Field | Purpose |
|---|---|
| `SchoolId`, `StudentId`, `ClassroomId`, `AcademicTermId` | Tenant-safe relationship |
| `RollNumber` | Optional classroom ordering |
| `EnrolledOn`, `WithdrawnOn` | History |
| `Status` | Active, Transferred, Withdrawn, Graduated |
| audit/soft-delete fields | Lifecycle |

- One active classroom per `(SchoolId, StudentId, AcademicTermId)`.
- A classroom and term must reference the same academic year and school.

## 6. Attendance, delay, conduct, and recognition entities

### 6.1 `DailyStudentAttendance`

One authoritative Al-Falah attendance fact per student and school day. It records the classroom-round result; biometric data does not create absence automatically.

Required fields:

- `SchoolId`, `StudentId`, `AcademicTermId`, `ClassroomId`, `AttendanceDate`
- `Status`: `Present`, `Absent`, `AbsentExcused`
- `ArrivedAfterAttendanceRecordedAt?`: optional physical-arrival observation; it does not rewrite the official absence fact
- `RecordedByUserId`, `RecordedAt`, `Source`: `SecretaryRoster`, `Correction`
- `CorrectionReason`, `CorrectedByUserId`, `CorrectedAt`
- common audit/soft-delete/concurrency fields

Constraints:

- Filtered unique `(SchoolId, StudentId, AttendanceDate)`.
- The Secretary submits only absent student IDs for a classroom/date. In one transaction the handler resolves the active enrollment roster, marks submitted students `Absent`, and marks every other enrolled student `Present`.
- Accepting a guardian excuse changes `Absent` to `AbsentExcused`. Both values remain absence facts for official reporting and the future Noor seam, but only `Absent` participates in the 3/5/10 penalty metric.
- A student may remain absent in the classroom round and also have a `MorningArrivalDelay` later that day. The two facts are intentionally independent.

### 6.2 `AbsenceExcuse`

Guardian-submitted explanation associated with one `DailyStudentAttendance` row.

- `SchoolId`, `DailyStudentAttendanceId`, `GuardianProfileId`
- `ExcuseType`: Medical, Family, Official, Other
- `GuardianNotes`
- `Status`: Pending, Accepted, Rejected
- `ReviewedByUserId`, `ReviewedAt`, `ReviewReason`
- `SubmittedAt`
- common audit/soft-delete/concurrency fields

Multiple submissions are retained for audit; only one may be the current accepted excuse. A rejected excuse is never overwritten.

### 6.3 `AbsenceExcuseAttachment`

- One or more PDF attachments for an excuse.
- Fields: `SchoolId`, `AbsenceExcuseId`, `OriginalFileName`, `ContentType`, `SizeBytes`, `Sha256`, `StorageProvider`, `StorageKey`, `UploadedByUserId`, `UploadedAt`, soft-delete fields.
- Only actual PDF content is accepted: extension, MIME type, magic bytes, size, and malware scan must all pass.
- The storage adapter may use the existing school Google Drive integration or a future object store; business code never depends on a provider-specific file ID.

### 6.4 Noor boundary

`[BLOCKED - PENDING CLIENT INPUT]` The Noor export format/API is not represented by an adapter, batch entity, item entity, or provider-specific status field in this schema. Core attendance preserves the required semantics (`Absent` and `AbsentExcused`) so a future isolated integration module can consume them without changing the attendance aggregate.

### 6.5 `MorningArrivalDelay`

One school morning late-arrival fact. Its core schema is independent of how arrival data is supplied.

- `SchoolId`, `StudentId`, `AcademicTermId`
- `ArrivalAt`, `SchoolLocalDate`
- `CutoffTimeSnapshot`, `DelayMinutes`
- `Reason`, `ReasonProvidedByGuardianAt`
- `NotificationPolicySnapshot` (`ImmediateGuardian`)
- audit/soft-delete fields

- Filtered unique `(SchoolId, StudentId, SchoolLocalDate)` unless a later business decision explicitly permits multiple morning-delay facts per day.
- Counting is by term.
- A future arrival source may invoke the same application use case, but no biometric provider identifiers or tables enter the core model while that integration is blocked.

### 6.6 `SessionDelay`

Teacher-recorded late entry to a lesson.

- `SchoolId`, `StudentId`, `AcademicTermId`, `ClassroomId`
- `SchoolTimetableId`, `SchoolTimetableEntryId` (nullable for emergency/manual fallback)
- `Period`, `OccurredAt`, `DelayMinutes?`, `Reason?`
- `ReportedByInstructorProfileId`, `ReportedAt`
- `GuardianNotificationStatus`
- audit/soft-delete/concurrency fields

The handler proves that the reporter teaches the selected classroom/period in the current published timetable, unless the caller has an explicit override permission.

### 6.7 `AcademicConcern`

The transcript's “academic delay” quick action and three-occurrence escalation.

- `SchoolId`, `StudentId`, `AcademicTermId`, `ClassroomId?`
- `Category`, `Description`, `OccurredAt`
- `ReportedByInstructorProfileId`, `SchoolTimetableEntryId?`
- `GuardianDispatchDecision`: PendingOfficerDecision, Approved, Suppressed
- audit/soft-delete fields

### 6.8 `BehaviorIncident`

- `SchoolId`, `StudentId`, `AcademicTermId`, `ClassroomId?`
- `CategoryId`/code, `Severity`: Low, Medium, High, Critical
- `Description`, `OccurredAt`, `Location`
- `ReportedByInstructorProfileId` or authorized staff user
- `ImmediateActionTaken`
- `GuardianDispatchDecision`
- audit/soft-delete/concurrency fields

The guardian notification is never sent directly from incident recording. It begins as `PendingOfficerDecision` and reaches the guardian only after Officer approval.

The escalation count is dynamically recomputed from current, active, upheld, countable incidents in the term. Correction, soft-delete, or a severity/category downgrade that makes an incident non-countable decreases the metric. The 10/20/30/… trigger ledger is retained for audit; any unresolved automatically created summons whose source count is no longer satisfied is marked `RequiresOfficerReview` and surfaced to the Officer instead of being silently deleted or reversed.

### 6.9 `StudentRecognition`

- `SchoolId`, `StudentId`, `AcademicTermId`, `ClassroomId?`
- `RecognitionType`, `Title`, `Description`, `RecognizedAt`
- `ReportedByInstructorProfileId`
- `GuardianNotificationStatus`
- audit/soft-delete fields

Indexes support weekly, monthly, and term filters: `(SchoolId, AcademicTermId, RecognizedAt)` and `(SchoolId, StudentId, RecognizedAt)`.

## 7. Permits, referrals, and summons

### 7.1 `ClassroomEntryPermit`

- `SchoolId`, `StudentId`, `AcademicTermId`, `ClassroomId`
- `IssuedByStudentAffairsUserId`, `IssuedAt`
- `Reason`, `ValidFrom`, `ValidUntil`
- resolved `SchoolTimetableId`, `SchoolTimetableEntryId`, `TargetInstructorProfileId`
- `Status`: Issued, AcknowledgedByTeacher, Expired, Revoked
- teacher acknowledgement and revocation fields
- audit/soft-delete/concurrency fields

This is automatically routed to the teacher currently responsible for the student's classroom and is also visible to the guardian. Repetition is counted per term and escalates at the school setting, default `5`.

### 7.2 `GatePass`

- `SchoolId`, `StudentId`, `AcademicTermId`
- `RequestedByGuardianProfileId`, `RequestedAt`, `RequestedExitAt`
- `Reason`
- guardian-provided pickup text: `PickupPersonName`, optional `PickupRelationship`, optional `PickupIdentityHint`
- `Status`: Requested, Approved, Rejected, SecurityAcknowledged, Exited, Cancelled, Expired
- officer review: reviewer, reviewed at, approval note/rejection reason
- current teacher snapshot: timetable, entry, instructor, period
- security acknowledgement and actual exit fields
- cancellation/expiry fields
- `RowVersion`, audit/soft-delete fields

`GatePassTransition` is an immutable one-to-many ledger containing from/to state, actor, role, timestamp, reason, correlation ID, and metadata JSON. Notification delivery is not encoded as a gate-pass state.

No pickup delegate/driver registration table or foreign key is required. The Security Guard uses the guardian's text hint plus visual/manual verification or a guardian-provided screenshot; the guard records the verification method and note in the transition audit.

### 7.3 `StudentReferral`

An internal Social Worker case.

- `SchoolId`, `StudentId`, `AcademicTermId`
- `SourceType`: MorningDelay, SessionDelay, AcademicConcern, Behavior, Absence, RepeatedEntryPermit, Manual
- `SourceEntityId?`, `RuleTriggerId?`, `CountSnapshot`, `ThresholdSnapshot`
- `Priority`: Normal, High, Critical
- `AssignedSocialWorkerUserId?`
- `Status`: Open, Assigned, InProgress, Resolved, Closed
- `RecommendedActions`, `ResolutionNotes`
- audit/soft-delete/concurrency fields

The core referral remains usable without the blocked quality-form package. No quality-form foreign key/schema is introduced until the client supplies the originals.

### 7.4 `GuardianSummon`

- `SchoolId`, `StudentId`, `AcademicTermId`, optional `StudentReferralId`
- `CreatedReason`, `Priority`, source count/threshold snapshots
- `Status`: Pending, Attended, UnderObservation, Improved
- `ScheduledAt?`, `ScheduledBySocialWorkerUserId?`, `Location?`, `Instructions?`
- `GuardianProfileId`, `GuardianNotifiedAt?`
- `RequiresOfficerReview`, `OfficerReviewReason?`, `OfficerReviewFlaggedAt?`, `OfficerReviewedAt?`, `OfficerReviewDecision?`
- attendance, observation, and improvement timestamps/notes
- audit/soft-delete/concurrency fields

`GuardianSummonStatusHistory` is immutable and stores every transition. The automation may create `Pending`, but it must never choose “tomorrow” or another appointment automatically; the Social Worker schedules it.

### 7.5 `StudentCaseAction`

Records outcomes mentioned in the call without bloating `StudentReferral`:

- `SchoolId`, `StudentReferralId`, `ActionType`: CounselingSession, GuardianSummon, GradeDeductionRecommendation, SuspensionRecommendation, ChildRightsCommitteeReferral, Other
- description, actor, action date, result, attachment/form link
- audit/soft-delete fields

This captures the Social Worker's possible actions: meeting/listening to the student, guardian summons, suspension recommendation, grade deduction recommendation, or referral to the Child Rights Committee.

## 8. Messaging and office-hours entities

### 8.1 `ConversationThread`

- `SchoolId`, optional `StudentId`
- `ThreadType`: GuardianTeacher, GuardianStudentAffairs, GuardianSocialWorker
- `Subject`, `Status`: Open, Closed, Archived
- creator and timestamps, audit/soft-delete/concurrency fields

### 8.2 `ConversationParticipant`

Explicit many-to-many association:

- `SchoolId`, `ConversationThreadId`, `ApplicationUserId`
- `ParticipantRoleSnapshot`, joined/left timestamps
- unique active participant per thread/user

Guardian membership also requires an active guardian-to-student link when the thread is student-specific.

### 8.3 `ConversationMessage` and `MessageReceipt`

- Message: thread, sender, body, sent/queued timestamp, office-hours disposition, optional reply-to ID, audit/soft-delete fields.
- Receipt: message, recipient, delivery/read timestamps, delivery state and failure reason.
- Messages are append-only after delivery. “Delete” is a user-facing soft-hide only and cannot erase the audit copy.

### 8.4 `TeacherOfficeHour`

- `SchoolId`, `InstructorProfileId`, `AcademicTermId`
- day, period or explicit local start/end time
- source: DerivedFromPublishedTimetable, TeacherSelected, ManagerOverride
- effective dates, is active, audit/soft-delete fields

Teacher-selected hours must be a subset of on-campus working time and must not overlap a lesson in the published timetable. Student Affairs and Social Worker messaging is not restricted by teacher office hours.

## 9. Core automation and notification entities

### 9.1 Rules and reliable events

| Entity | Purpose |
|---|---|
| `AutomationRuleDefinition` | Immutable compiled policy version derived from `SchoolStudentAffairsSettings`; never an independently editable source of conflicting thresholds |
| `StudentTermMetric` | Transactionally maintained count/read model per student, term, and metric |
| `AutomationTriggerLedger` | Idempotency proof that a threshold occurrence produced its actions once; retains current validity/review state after recalculation |
| `OutboxMessage` | Domain event serialized in the same transaction as the business fact |
| `InboxMessage` | Optional idempotency record for externally delivered messages/webhooks |

Unique keys:

- `StudentTermMetric`: `(SchoolId, StudentId, AcademicTermId, MetricCode)`.
- `AutomationTriggerLedger`: `(SchoolId, StudentId, AcademicTermId, RuleVersionId, Threshold, OccurrenceNumber)`.
- `OutboxMessage.EventId` unique.

Recalculation updates `StudentTermMetric` from source facts rather than applying irreversible increments. A ledger can become `SourceNoLongerSatisfied`; linked unresolved summons are flagged for Officer review, while immutable history remains intact.

### 9.2 Existing `Notification` evolution

Reuse the current `Notification` table rather than creating a competing notification model. Add:

- `Priority`, `TemplateKey`, `CorrelationId`, `DeduplicationKey`
- `DeliveryStatus`, `DeliveredAt`, `FailedAt`, `FailureReason`, `RetryCount`
- `RequiresApproval`, `ApprovedByUserId`, `ApprovedAt`
- audit and soft-delete fields

There is one notification row per recipient. The existing `IsRead`/`ReadAt` fields remain the in-app receipt.

### 9.3 Deferred integration boundaries

- `[BLOCKED - PENDING CLIENT INPUT]` Biometric devices: no device, mapping, punch inbox, checkpoint, webhook, or polling schema is authorized.
- `[BLOCKED - PENDING CLIENT INPUT]` Noor export: no batch/item or provider-state schema is authorized.
- `[BLOCKED - PENDING CLIENT INPUT]` Fourteen quality forms: no template/version/instance/artifact schema is authorized.

The core domain exposes stable facts and events but has no foreign-key dependency on any of these future modules.

## 10. EF Core relationship map

| Principal | Dependant | Cardinality | Delete behavior / tenant rule |
|---|---|---|---|
| `School` | every tenant entity | 1:N | Restrict; all queries scope `SchoolId` |
| `ApplicationUser` | `GuardianProfile` | 1:N across schools | Restrict; unique per school/user |
| `Student` ↔ `GuardianProfile` | `StudentGuardian` | M:N via explicit bridge | Both composite FKs include `SchoolId` |
| `AcademicYear` | `AcademicTerm` | 1:N | Restrict |
| `AcademicYear` | `Classroom` | 1:N | Restrict |
| `School` | `SchoolStudentAffairsSettings` | 1:1 active | Restrict; filtered unique `SchoolId` |
| `Student`, `Classroom`, `AcademicTerm` | `StudentEnrollment` | each 1:N | Composite tenant-safe FKs |
| `Student` | attendance/delay/incident/recognition/permit/pass/referral/summon | 1:N | Restrict; same-school composite FK |
| `DailyStudentAttendance` | `AbsenceExcuse` | 1:N | Restrict; history retained |
| `AbsenceExcuse` | attachment | 1:N | Explicit soft-delete cascade |
| `SchoolTimetableEntry` | session delay/entry permit/gate pass | 1:N optional | Restrict; snapshot fields preserve history |
| `StudentReferral` | `GuardianSummon` | 1:N optional | Restrict |
| `StudentReferral` | `StudentCaseAction` | 1:N | Restrict |
| `GuardianSummon` | status history | 1:N | Immutable |
| `GatePass` | transitions | 1:N | Immutable |
| `ConversationThread` ↔ `ApplicationUser` | `ConversationParticipant` | M:N | Tenant-checked bridge |
| `ConversationThread` | `ConversationMessage` | 1:N | Restrict |
| `ConversationMessage` ↔ recipient user | `MessageReceipt` | M:N | Immutable delivery/read record |
| template version | form instance | 1:N | Historical version cannot be deleted while referenced |

## 11. Index and constraint checklist

Every high-volume query begins with `SchoolId`. Required indexes include:

- Student search: `(SchoolId, IsActive, StudentNumber)` and normalized-name search strategy.
- Current enrollment: `(SchoolId, AcademicTermId, ClassroomId, Status)`.
- Attendance dashboard: `(SchoolId, AttendanceDate, Status)` and `(SchoolId, StudentId, AcademicTermId)`.
- Threshold counts: each fact table indexed `(SchoolId, StudentId, AcademicTermId, OccurredAt/AttendanceDate)`.
- Teacher quick actions: `(SchoolId, ClassroomId, OccurredAt)` and reporter indexes.
- Gate queue: `(SchoolId, Status, RequestedExitAt)` filtered on active states.
- Summons/referrals: `(SchoolId, AssignedSocialWorkerUserId, Status, Priority)`.
- Message inbox: `(SchoolId, ApplicationUserId, ReadAt)` through receipts.
- Notification inbox: `(SchoolId, UserId, IsRead, CreatedAt DESC)`.
- Outbox: filtered `(ProcessedAt, NextAttemptAt)` and unique event ID.
- Form archive: `(SchoolId, TemplateId, Status, CreatedAt)`.

SQL check constraints validate enum ranges, `ValidUntil > ValidFrom`, `EndsOn >= StartsOn`, non-negative delay minutes, accepted/rejected review metadata, and state-dependent required timestamps.

## 12. Soft-delete and retention rules

- Mutable catalogs and business aggregates are soft-deleted only.
- Soft-deleting a `Student` must explicitly soft-delete active enrollments, guardian links, office-facing open access grants, and future requests. Historical attendance, incidents, passes, summons, and messages remain retained and become read-only.
- A guardian/profile soft delete revokes login access to that school's students but does not remove messages, submissions, or audit history.
- A classroom cannot be soft-deleted while it has an active enrollment or a published timetable entry.
- Gate passes, summons, and referrals are normally closed/cancelled through state transitions, not deleted.
- `IgnoreQueryFilters()` is allowed only in explicit restore/audit handlers followed by a school-scope predicate.

## 13. Migration strategy

Migrations are additive, deployable in small batches, and reversible before destructive cleanup.

### Migration SA-01 — Academic core

- Add `AcademicTerms`, `Students`, `GuardianProfiles`, `StudentGuardians`, `SchoolStudentAffairsSettings`, `Classrooms`, `StudentEnrollments`.
- Add nullable `ClassroomId` to `SchoolTimetableEntries`.
- Backfill classrooms from distinct `(SchoolId, AcademicYearId, ClassLabel)` values in existing timetables; log ambiguous labels instead of guessing.
- Backfill `SchoolTimetableEntry.ClassroomId`; keep `ClassLabel` unchanged as a snapshot.

### Migration SA-02 — Attendance and observations

- Add daily attendance, excuses/attachments, morning delays, session delays, academic concerns, behavior incidents, and recognition.
- Add filtered unique indexes and all global query filters before exposing endpoints.

### Migration SA-03 — Workflows and communication

- Add entry permits, gate passes/transitions, referrals/actions, summons/history, conversations/messages/receipts, and office hours.
- Add `rowversion` to stateful aggregate roots.

### Migration SA-04 — Reliability and automation

- Add automation policy snapshots, counters, trigger ledger, outbox/inbox, and recalculation-review state.
- Add notification reliability/approval columns in a backward-compatible nullable/defaulted form.

### Migration SA-05 — Backfill and enforcement

- Seed one default `SchoolStudentAffairsSettings` row per school and compile the initial immutable policy version.
- Validate orphan and cross-school rows with deployment SQL.
- Make staged nullable relationships required only after successful backfill.
- Add final composite alternate keys/FKs and performance indexes online where SQL Server edition permits.

Operational rules:

- Generate migration scripts in CI and review them; production deploys reviewed SQL, not an unreviewed startup-generated schema delta.
- `Program.cs` currently calls `MigrateAsync()` at startup. Before this large module reaches production, restrict automatic migration to development/test or place it behind an explicit deployment flag.
- Back up the production database and run row-count/cross-tenant validation before and after every batch.
- Roll out API readers before writers, then workers, then UI; workers remain disabled until seed/configuration validation passes.

## 14. Locked decisions and explicit blockers

The following decisions supersede all earlier pending notes:

1. Morning delay escalates at exactly 10 occurrences per term; academic concerns at 3; classroom entry permits at 5; behavior at each multiple of 10.
2. Absence penalty actions are exactly: 3 visual alert, 5 internal referral plus pending guardian summons, and 10 the same plus a Child Rights Committee recommendation. `AbsentExcused` never contributes to this counter.
3. Behavior metrics dynamically recalculate downward after correction, soft-delete, or a countability-changing severity reduction. Unresolved automatic summons affected by the decrease are flagged for Officer review.
4. Daily absence, morning arrival delay, and session delay guardian notices are automatic and immediate. Behavior and academic-concern notices require Officer approval.
5. Gate-pass pickup uses guardian-entered text and guard visual/manual/screenshot verification; no pre-registered delegate is required.
6. The Secretary owns roster-based daily student attendance. The School Manager sees aggregates only and never confidential Social Worker notes/session details.
7. Biometric integration, Noor export format, and the 14 quality forms remain `[BLOCKED - PENDING CLIENT INPUT]`; no schemas or adapters for them are part of this locked core blueprint.
8. No student login was requested. `Student` remains a domain entity, not a new Identity role.
