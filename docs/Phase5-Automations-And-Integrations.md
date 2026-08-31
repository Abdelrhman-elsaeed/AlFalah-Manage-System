# Phase 5 — Automations and Integrations

**Status:** Locked technical specification — no implementation authorized  
**Scope:** Core event-driven automations plus explicit future-integration boundaries

## 1. Goals and non-negotiable constraints

The Student Affairs module must automate thresholds and communication without placing long-running work in HTTP controllers.

- A controller records one valid business command and returns after the database transaction commits.
- Domain events and an outbox make automation durable; notification or worker failure never rolls back a valid attendance, incident, permit, or pass.
- Every event, metric, rule, trigger, query, and worker execution is isolated by `SchoolId` and guarded by the same tenant rules as synchronous requests.
- Counts are bounded by `(SchoolId, StudentId, AcademicTermId, MetricCode)` and never leak across schools or terms.
- Thresholds come from `SchoolStudentAffairsSettings`; no handler, controller, worker, or UI may embed `10`, `3/5/10`, `3`, or `5` as behavioral magic numbers.
- Corrections and soft deletion cause deterministic recalculation. Counters are rebuildable projections, not irreversible increments.
- Retries are idempotent and cannot create duplicate referrals, summons, committee recommendations, or guardian notifications.
- Biometric devices, Noor export, and the 14 quality forms are blocked and excluded from the core design in sections 9–11.

## 2. Event-driven architecture

### 2.1 Transactional event/outbox flow

Every fact-creating or fact-changing use-case handler follows this sequence:

1. Resolve actor and allowed school through `SchoolScopeGuard`.
2. Load the active term and the relevant aggregate under the same school predicate.
3. Validate permission, object ownership, state, and concurrency version.
4. Store the business fact or audited correction/soft deletion.
5. Append one or more domain events carrying IDs and minimal immutable facts, including `SchoolId`, `StudentId`, `AcademicTermId`, event ID, correlation ID, and occurred time.
6. Serialize the events to `OutboxMessage` in the same EF Core transaction.
7. Commit once and return the API result.
8. A background dispatcher claims outbox rows with a lease, invokes focused handlers, and records success/retry/dead-letter state.

No domain event contains guardian message text, provider secrets, or an authoritative client-supplied school ID. Handlers reload protected data using the event's tenant key and reapply school scope.

### 2.2 Handler decomposition

One event may fan out to several small handlers:

- metric rebuild/update;
- threshold evaluation;
- internal referral creation;
- pending summons creation or attachment to an active summons;
- Child Rights Committee recommendation creation;
- guardian dispatch request creation;
- immediate in-app notification creation;
- dashboard projection refresh;
- audit/observability update.

These are separate MediatR notification handlers or equivalent focused application handlers. There is no `StudentAffairsAutomationService` god class and no controller-owned branching.

### 2.3 Background execution model

The implementation phase should use a persistent scheduler such as Hangfire with SQL Server storage for durable retries and recurring reconciliation. If introducing Hangfire is not approved, an `IHostedService` is acceptable only with database-backed leases, retry metadata, and idempotency; an in-memory timer alone is not production-safe.

Logical workers:

| Worker | Responsibility |
|---|---|
| `StudentAffairsOutboxWorker` | Claims and dispatches committed domain events |
| `StudentTermMetricReconciliationWorker` | Rebuilds metrics after corrections/settings changes and performs scheduled drift checks |
| `StudentAffairsRuleEvaluationWorker` | Evaluates the effective settings version and creates idempotent actions |
| `NotificationDeliveryWorker` | Delivers approved/immediate in-app notifications and retries failures |
| `StudentAffairsDeadlineWorker` | Gate-pass expiry, unacknowledged-pass reminders, summons/observation reminders |
| `OfficeHoursReleaseWorker` | Releases teacher notifications queued for the next eligible office hour |
| `ExcuseAttachmentScanWorker` | Completes malware/content validation before an uploaded excuse can be reviewed |
| `RecognitionAggregationWorker` | Maintains weekly/monthly/term positive-recognition statistics; it does not imply guardian dispatch |

Each scheduled execution enumerates school IDs first, establishes an explicit school scope for one unit of work, and records `(JobType, SchoolId, PeriodKey)` idempotency. A failure in one school cannot stop or contaminate another school's run.

## 3. Domain-event catalog

| Event | Raised when | Core asynchronous consumers |
|---|---|---|
| `StudentAttendanceRosterSubmittedEvent` | Secretary commits a classroom/date roster | Attendance aggregates and audit projection |
| `StudentAbsenceLoggedEvent` | A roster row becomes `Absent` | Immediate guardian notice; absence metric recalculation |
| `StudentAttendanceCorrectedEvent` | Authorized correction changes a daily fact | Metric recalculation; dashboard refresh |
| `AbsenceExcuseSubmittedEvent` | Guardian submits excuse/PDF | Attachment scan; Officer review queue |
| `AbsenceExcuseAcceptedEvent` | Officer accepts and status becomes `AbsentExcused` | Penalty metric recalculation; dashboard refresh |
| `AbsenceExcuseRejectedEvent` | Officer rejects excuse | Guardian decision notice; attendance remains `Absent` |
| `MorningArrivalDelayLoggedEvent` | Core morning delay fact is saved | Immediate guardian notice; morning-delay metric evaluation |
| `SessionDelayLoggedEvent` | Teacher saves a class delay | Immediate guardian notice; dashboard projection |
| `AcademicConcernLoggedEvent` | Teacher saves concern | Pending Officer dispatch decision; three-occurrence metric evaluation |
| `AcademicConcernChangedEvent` | Concern corrected/soft-deleted | Metric recalculation and trigger-impact review |
| `BehaviorIncidentLoggedEvent` | Incident is saved/classified | Pending Officer dispatch decision; behavior metric evaluation |
| `BehaviorIncidentChangedEvent` | Incident is corrected, reclassified, severity-reduced, upheld/rejected, or soft-deleted | Full behavior metric recalculation and automatic-summons impact review |
| `ClassroomEntryPermitIssuedEvent` | Officer issues entry permit | Teacher Top Priority/status notification; guardian permit-status notice; metric evaluation |
| `GatePassRequestedEvent` | Guardian submits request | Officer queue notification |
| `GatePassApprovedEvent` | Officer approves | Guardian, current teacher, and Security status notifications |
| `GatePassSecurityAcknowledgedEvent` | Guard acknowledges | Officer operational dashboard |
| `StudentExitedSchoolEvent` | Guard records physical exit | Guardian/Officer/current-teacher completion notice |
| `GuardianSummonScheduledEvent` | Social Worker schedules pending summons | Guardian appointment notification |
| `SchoolStudentAffairsSettingsChangedEvent` | Officer changes/reset settings | Compile policy snapshot; recalculate active-term metrics and impacts |

Gate-pass, entry-permit, summon, excuse-decision, and other workflow status messages are transactional status notifications. They are distinct from the incident/concern guardian-dispatch policy in section 6.

## 4. School settings and rule compilation

### 4.1 Authoritative settings

`SchoolStudentAffairsSettings` is a one-school aggregate edited only through its scoped CRUD handlers and `StudentAffairsSettings.Manage` permission.

| Setting | Locked default |
|---|---:|
| Morning arrival delays per term | `10` |
| Behavior incident multiple per term | `10` |
| Academic concerns per term | `3` |
| Classroom entry permits per term | `5` |
| Absence visual-alert level | `3` |
| Absence referral/summons level | `5` |
| Absence Child Rights level | `10` |

The aggregate also carries the school arrival cutoff/grace and behavior category/severity countability policy. `AbsentExcused` exclusion is a locked domain invariant and cannot be disabled through settings.

Validation rules:

- all thresholds are positive integers;
- absence thresholds must be strictly ordered (`visual < referral < child rights`);
- the behavior multiple cannot be zero;
- effective dates cannot overlap another active version for the same school;
- updates require a concurrency token and mandatory audit reason;
- create/update/reset derives `SchoolId` from the current scoped actor;
- delete means soft-delete custom values and atomically restore defaults, so a school never runs without an effective policy.

### 4.2 Versioning and effect of changes

An update produces an immutable compiled `AutomationRuleDefinition` snapshot. Business facts and trigger ledgers store the effective settings/rule version used at evaluation time.

After an update:

1. Commit the settings version and `SchoolStudentAffairsSettingsChangedEvent`.
2. Rebuild all affected active-term metrics for that school.
3. Evaluate newly crossed thresholds idempotently.
4. Mark source-no-longer-satisfied triggers and flag linked unresolved automatic summons for Officer review.
5. Do not erase completed triggers, delivered notifications, attended summons, or case history.

This permits school-wide configurability without rewriting history or allowing two competing configuration surfaces.

## 5. Metric definitions and dynamic recalculation

### 5.1 Canonical metric sources

| Metric | Source query | Exclusions/boundary |
|---|---|---|
| `MorningArrivalDelayCount` | Active `MorningArrivalDelay` rows | Current school/student/term only |
| `BehaviorIncidentCount` | Active, upheld incidents that satisfy the current severity/category countability policy | Corrected-away, soft-deleted, or downgraded-below-policy incidents excluded |
| `AcademicConcernCount` | Active `AcademicConcern` rows | Current school/student/term only |
| `ClassroomEntryPermitCount` | Active/issued historical permit facts | Current school/student/term only |
| `PenaltyAbsenceDayCount` | Distinct daily attendance rows with `Status=Absent` | `AbsentExcused` always excluded; `Present` excluded |

`StudentTermMetric` is a performance projection. The source query is authoritative. Every projection stores count, source watermark, effective settings version, recalculated-at time, and concurrency token.

### 5.2 Recalculation algorithm

1. Claim a lease for `(SchoolId, StudentId, AcademicTermId, MetricCode)`.
2. Query active source facts with global soft-delete filters and an explicit `SchoolId` predicate.
3. Compute the canonical count and compare it with the stored projection.
4. Persist the new count and a reason (`FactAdded`, `FactCorrected`, `FactDeleted`, `SeverityChanged`, `ExcuseAccepted`, `SettingsChanged`, `Reconciliation`).
5. Evaluate thresholds from the effective settings snapshot.
6. Create or update `AutomationTriggerLedger` rows.
7. Queue actions/review flags through the outbox and release the lease.

Scheduled reconciliation rebuilds metrics from facts and reports drift; it never trusts accumulated `+1/-1` operations as the sole source of truth.

### 5.3 Behavior multiples and downward changes

- For count `c` and configured multiple `m`, satisfied occurrences are `1..floor(c/m)`; with the default, ledgers represent 10, 20, 30, and so on.
- A jump across more than one multiple evaluates every newly crossed occurrence exactly once.
- Ledger uniqueness is `(SchoolId, StudentId, AcademicTermId, RuleVersionId, Threshold, OccurrenceNumber)`.
- A correction, soft-delete, or severity/category downgrade invokes a full source rebuild. If the count falls from 20 to 19, the 20-trigger becomes `SourceNoLongerSatisfied`; the 10-trigger remains satisfied.
- Linked completed history is retained. A linked unresolved automatic summons receives `RequiresOfficerReview=true`, the cause, previous/current count, and flag timestamp.
- The Officer records `Retain` or `AcknowledgeSourceNoLongerSatisfied` with a mandatory welfare/audit rationale. This resolves the review flag but does not invent a fifth summons state. Recalculation never silently deletes, improves, or rewrites a summons.
- If the count later rises and satisfies the same occurrence again, the original ledger is re-evaluated/reactivated; no duplicate action is created.

### 5.4 Accepted-excuse recalculation

- Secretary submission initially records `Absent`, which counts toward `PenaltyAbsenceDayCount` and sends the immediate daily absence notice.
- Pending or rejected excuses do not change the attendance status or penalty count.
- Officer acceptance atomically changes the row to `AbsentExcused` and emits `AbsenceExcuseAcceptedEvent`.
- Recalculation removes that day from the penalty metric while preserving it in total-absence and excused-absence reporting.
- Official/future Noor consumers must treat both `Absent` and `AbsentExcused` as absence facts; only the internal penalty rules distinguish them.

## 6. Locked action and notification matrix

| Fact/rule | Trigger | Internal automated action | Guardian communication | Dashboard/UI |
|---|---|---|---|---|
| Daily absence | Each new `Absent` row | Recalculate penalty absence metric | Fully automatic and immediate | Present/Absent/Excused aggregates refresh |
| Morning arrival delay | Each occurrence | Recalculate; at effective threshold default `10`, create/open referral and pending summons | Fully automatic and immediate for each occurrence | Escalates to critical at threshold |
| Session delay | Each occurrence | Store fact and refresh projections | Fully automatic and immediate | Teacher/Officer visibility |
| Academic concern | Each occurrence; escalation at default `3`/term | Pending dispatch decision; at threshold create/open referral and pending summons | Requires Officer approval | High-priority Officer review at threshold |
| Behavior incident | Each occurrence; escalation at every default multiple of `10`/term | Pending dispatch decision; each satisfied multiple creates/attaches referral and pending summons | Requires Officer approval | Critical/Top Priority at threshold; dynamic current count |
| Penalty absence level 1 | `3` distinct `Absent` days/term | No referral/summons | The original daily messages were already automatic | Visual/color-coded alert |
| Penalty absence level 2 | `5` distinct `Absent` days/term | Internal referral plus pending guardian summons | Guardian receives summons only after Social Worker schedules it | Higher priority/color |
| Penalty absence level 3 | `10` distinct `Absent` days/term | Internal referral + pending summons/active-summons attachment + Child Rights Committee recommendation | Controlled summons workflow | Maximum priority/color |
| Classroom entry permit repetition | Default `5`/term | Officer/Social Worker alert/referral per compiled rule | Permit status notice only | Repetition badge/high priority |

The automated guardian-notification list is closed: daily absence, morning arrival delay, and session delay. Behavior incidents and academic concerns always require Officer approval. Recognition statistics do not create guardian notifications unless a future approved policy explicitly adds one.

### 6.1 Approval-gated dispatch

For behavior and academic concerns:

1. Fact handler commits the fact and a `PendingOfficerDecision` dispatch record without creating a guardian-recipient notification.
2. Officer queue shows a safe fact summary, proposed template, linked guardian, current metric, and source audit.
3. Officer approves or suppresses with permission, row version, and mandatory reason for suppression.
4. Approval creates one guardian notification using a deterministic deduplication key and the approved template snapshot.
5. `NotificationDeliveryWorker` delivers/retries it. Suppression remains auditable.

For daily absence, morning delay, and session delay, the event consumer creates the guardian-recipient notification immediately after outbox dispatch; there is no approval row or approval endpoint in their path.

## 7. Idempotency, concurrency, and failure handling

### 7.1 Required keys

- `OutboxMessage.EventId` is globally unique.
- `Notification.DeduplicationKey` is unique per recipient and fact/policy action.
- `AutomationTriggerLedger` uses the school/student/term/rule/threshold/occurrence key.
- Internal referral and summons creation use the trigger-ledger ID as their idempotency source.
- Attendance roster submission uses school/classroom/date/idempotency key plus roster revision.

### 7.2 Retries and dead letters

- Transient failures use exponential backoff with jitter.
- A claimed row has lease owner/expiry so another node can recover abandoned work.
- Permanent validation failures enter an authorized failure queue with redacted payload and correlation ID.
- Manual retry invokes the same idempotent handler; it cannot bypass tenant, state, or permission rules.
- Poison events do not block later outbox rows for other schools/students.

### 7.3 Concurrency races

- Concurrent facts for the same metric serialize through a metric lease/concurrency token and then rebuild from source.
- Concurrent Officer notification decisions use row version; exactly one decision wins.
- A correction racing threshold evaluation ends with a second recalculation event; reconciliation guarantees convergence.
- Settings updates racing fact creation store their respective effective versions and enqueue a final settings-driven rebuild.

## 8. Dashboards, privacy, and observability

### 8.1 School Manager projection

The School Manager dashboard is a dedicated aggregate projection, not a filtered Social Worker DTO. It may contain:

- total present, absent, and `AbsentExcused` per classroom/date;
- school/class attendance percentages;
- non-identifying counts by threshold level;
- aggregate open/referral/summons statuses and ageing.

It must not contain student-level Social Worker case notes, counseling/session notes, message bodies, evidence attachments, guardian identifiers, or confidential event narratives. Projection and contract tests must assert these fields do not exist in the DTO.

### 8.2 Operational metrics

- outbox age, attempts, failures, and dead letters by school/event type;
- metric drift detected/repaired;
- thresholds crossed and actions created/deduplicated;
- pending behavior/academic dispatch age;
- automatic summons flagged after source-count decrease;
- guardian delivery/read age without exposing message content;
- overdue gate passes, summons appointments, observation reviews, and queued office-hour messages.

Logs include event/correlation/aggregate IDs and school ID, but no guardian message body, confidential case narrative, identity document, or excuse PDF content.

## 9. Biometric Devices — `[BLOCKED - PENDING CLIENT INPUT]`

No biometric adapter, polling/webhook choice, device table, identity mapping, inbox/checkpoint schema, endpoint, permission, worker, credential model, or payload contract is authorized in this Spec Kit.

Future plug-in readiness is limited to one rule: morning-delay core commands/events remain source-neutral and contain no provider-specific fields. After the client supplies device make/model, connectivity, ownership, identity key, timestamp semantics, security mechanism, and sample payload/schema, a separate integration specification and migration set may be designed. That future module must translate verified source data into the existing core morning-delay use case without changing threshold, notification, or tenant logic.

## 10. Noor Export Format — `[BLOCKED - PENDING CLIENT INPUT]`

No Noor adapter/API client, batch/item tables, file layout, PDF/XLSX export, endpoint, schedule, confirmation status, or retry job is authorized.

The core is ready only at the semantic boundary: `Absent` and `AbsentExcused` are both official absence facts; `AbsentExcused` is excluded solely from internal penalties. Once the client provides the required format/API, field mapping, identifiers, schedule, and confirmation process, the export can be specified as an isolated consumer without changing core attendance data.

## 11. Fourteen Quality Forms — `[BLOCKED - PENDING CLIENT INPUT]`

No dynamic JSON schema, template/version/instance/artifact tables, QuestPDF renderer, import endpoint, form permission set, routing worker, or placeholder seed is authorized. The number “14” is a business inventory count, not enough information to infer a data model.

The core referral, summons, and case workflows therefore cannot require a quality-form foreign key to operate. After the client supplies all originals, field definitions, signatures, routing/ownership, retention rules, and required output fidelity, the forms require their own specification. QuestPDF versus another rendering strategy will be decided only then.

## 12. Verification plan for the future implementation phase

### 12.1 Unit tests

- Default and custom settings validation; absence threshold ordering.
- Per-school/per-student/per-term isolation and term reset.
- Morning `10`, academic concern `3`, entry permit `5`, and behavior `10/20/30` evaluation.
- Absence `3/5/10` actions and unconditional exclusion of `AbsentExcused`.
- Behavior correction, soft-delete, and countability-changing severity downgrade.
- Trigger-ledger invalidation/reactivation and unresolved-summons review flag.
- Notification policy: only daily absence/morning delay/session delay are immediate; behavior/academic are approval-gated.
- Idempotent referrals, summons, committee recommendations, and notifications.

### 12.2 Integration tests

- Secretary absent-only roster submission writes all enrolled students atomically and rejects foreign/non-roster IDs.
- Excuse acceptance transitions `Absent → AbsentExcused`, preserves total absence, and decreases the penalty metric.
- Fact transaction writes outbox atomically; worker retry creates one action.
- Settings update recalculates current-term metrics without rewriting historical trigger snapshots.
- Cross-school events, routes, and worker claims fail closed under `SchoolScopeGuard`.
- School Manager aggregate DTO contains class totals and cannot expose confidential Social Worker fields.
- Behavior/academic guardian notification does not exist before Officer approval.

### 12.3 End-to-end scenarios

- Tenth morning delay → one referral + pending summons + immediate occurrence notice; no invented appointment.
- Tenth and twentieth behavior incident → separate ledgers, no duplicate active summons; downgrade/delete then flags the unresolved summons for Officer review.
- Third/fifth/tenth penalty absence → visual alert, referral/pending summons, then Child Rights recommendation respectively.
- Accepted excuse drops a student from 5 to 4 penalty absences while the day remains `AbsentExcused` in attendance aggregates.
- Guardian gate request → Officer approval → teacher/security notifications → guard manual verification → physical exit.
- Guardian message outside teacher office hours → stored immediately and released at the next eligible window.

No integration test is designed for biometric, Noor export, or the 14 forms until their blockers are resolved through a new client-approved specification.

## 13. Rollout order for future implementation

1. Add settings, source facts, outbox, metrics, trigger ledger, and notification-approval schema in additive migrations.
2. Seed default school settings and permissions; do not seed blocked integration permissions or form placeholders.
3. Deploy read/query paths and aggregate dashboards.
4. Deploy core writes with workers disabled; validate school scope, policy versions, and source counts.
5. Enable outbox/notification delivery, then rule evaluation and reconciliation per school with monitoring.
6. Enable deadline/office-hour/recognition workers.
7. Treat biometric, Noor export, and quality forms as separate future rollout tracks with their own approved specifications and migrations.

## 14. Locked traceability summary

| Final business decision | Technical realization |
|---|---|
| Thresholds are configurable, not magic numbers | `SchoolStudentAffairsSettings` CRUD + immutable compiled policy version |
| Morning delay exactly 10/term | Default/effective settings rule and term metric |
| Behavior every multiple of 10 and decreases after source change | Source rebuild, occurrence ledger, invalidation/reactivation, Officer review flag |
| Absence 3/5/10 | Exact visual/referral+summons/committee action matrix |
| Accepted excuse excluded from penalties | `AbsentExcused`; official absence preserved, penalty query excludes it |
| Academic concerns at 3 | Dedicated term metric and rule |
| Entry permit repetition at 5 | Dedicated term metric and default setting |
| Immediate guardian notifications | Closed list: daily absence, morning delay, session delay |
| Officer-approved guardian notifications | Behavior incidents and academic concerns only |
| Secretary absent-only attendance UX | Roster command derives every unmarked enrollment as present |
| School Manager privacy | Aggregate-only projection with no Social Worker case/session content |
| Gate pickup identity | Guardian text hint + guard visual/manual/screenshot verification; no delegate registry |
| Three external features blocked | No adapters, schemas, endpoints, permissions, or jobs until client input |
