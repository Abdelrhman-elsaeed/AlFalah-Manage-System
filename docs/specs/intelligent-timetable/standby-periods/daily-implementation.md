# Daily coverage implementation — phase 2

**Change:** C39 / D-131, 2026-10-16. Implementation and verification record; official register PDF remains phase 3.

## Journey and authority

Secretary attendance writes an independent durable coverage reconciliation signal in the same transaction as attendance. A retryable worker and explicit authorized refresh reconcile the effective, unique published school/term timetable. Absence prepares needs, never approvals. Current full-day Absent/Excused records are the input; partial-day absence times do not exist in the attendance model and are not inferred.

The existing substitution ledger remains the sole effective-lesson ownership source. A coverage need links a dated original lesson member/block to its approved ledger entry. There is no second effective-assignment table. Manual and fair previews require scheduled standby for every period of the member/block. Both use the existing availability/resource validator and attendance revalidation. Weekly teaching maximums apply to original scheduled teaching; dated cover cannot alter those permanent quotas.

Daily administration uses the existing manager/management-permission/delegated-editor scope. Existing privileged soft-warning approval remains explicit. A constrained, dated ledger replaces the previous all-lesson card flow for coverage; structural swaps keep their separate existing UI and APIs. Reuse the existing route and notification infrastructure.

## States and evidence

Needed → Assigned → Acknowledged → Executed. Unable and ReviewRequired expose administrator action. Withdrawn needs remain historical. Assignment changes invalidate drafts and old assignment revisions; signed evidence is retained. Corrected absence does not silently release an approved teacher. Explicit administrator release restores original ownership only while operationally valid and cannot erase completed execution. Keeping a reviewed assignment records its reason. Reopening a completed execution preserves prior signed submissions and requires a correction reason and a new genuine signature.

The owner confirms two signatures: acknowledgement at receipt, and execution after reporting what happened. Each action snapshots the authenticated assigned teacher's saved signature and the then-current lesson/assignment data. Missing signatures block signing without deleting drafts. Narrative/notes save as drafts, submission follows the actual lesson end, and submission request IDs provide safe retries. Notification delivery is never acknowledgement or execution.

## Fairness and interface

Fair counters use actual signed executed periods in the term and pending approved commitments, including the current proposal. Only eligible scheduled standby can be selected. Simultaneous needs share one occupancy plan; uncovered work remains visible. Candidate availability and constraints dominate fairness. Preview explains teacher counts and changes before explicit approval.

Manual choices reserve their teachers before fair filling. Remaining needs are processed by fewest eligible candidates, then start time and need identity; teacher ties use executed plus pending/proposed periods, executed periods, then teacher identity. This is a deterministic constrained greedy allocation, not a claim of globally optimal matching. Corrections count one actual execution per need, using the latest retained signed execution; an open correction does not count a second pending commitment. A pair contributes its actual number of periods.

Platform palette: deep green #14583e, mint #edf7f0, gold #a77918, white #ffffff, ink #21382d, muted #728177. Typography remains Tajawal. Use a compact date/context toolbar, full-width lesson ledger grouped by absent teacher, restrained state labels, and one approval review. Avoid large repeated cards or a solid green navigation strip. Teacher tasks use concise dashboard rows with period/time/class/state and a direct details action. Execution uses clear receipt/draft/review/submitted stages, including conflict recovery and explicit final signing. Inspect responsive captures at390/768/1440/1920.

## Persistence and API

Additive migration `20261016181024_AddDailyStandbyCoverage` creates `DailyCoverageNeeds`, append-only `CoverageRecords` and leased/retryable `CoverageReconciliationSignals`. Composite school references, a unique source/date key, revision concurrency and unique school/request identifiers protect scope and retries. Existing timetable entries, cover movements, versions and outbox remain authoritative. Assignment/reassignment records preserve the previous draft before resetting the new teacher's receipt and draft. Signed execution prevents reassignment; administrator correction preserves earlier versions.

| Endpoint under `/api/v1/timetable-coverage` | Purpose |
|---|---|
| `GET board?date=` / `POST reconcile?date=` | Read the ledger / explicitly reconcile attendance |
| `GET needs/{id}/options` | Current eligible and rejected candidates with explanations and term counts |
| `POST preview` / `POST approve` | Non-mutating manual/fair proposal / atomic reviewed approval |
| `GET my-tasks?page=&pageSize=` / `GET tasks/{id}` | Paged actual-owner tasks / scoped detail and retained versions |
| `PUT tasks/{id}/draft` | Revision-safe narrative/notes draft |
| `POST tasks/{id}/acknowledge` / `submit` / `unable` | Signed receipt / signed execution / inability report |
| `POST needs/{id}/reopen` / `keep` / `release` | Reasoned administrator correction/reconciliation |

All endpoints use `ApiResponse<T>`. Missing/foreign needs are not found; unrelated teachers cannot read or edit; management does not confer permission to sign for a teacher. The legacy direct substitution endpoint enforces the same standby, absence, publication and retained-execution rules. Structural swaps retain their existing UI/API in a separate tab. Teacher tasks are available at `/standby-tasks`, linked from teacher navigation and dashboard. Existing notification dispatch is reused; notification delivery is not execution.

The receipt may be signed before the lesson. Execution requires receipt, a saved nonempty narrative, a genuine saved signature, the current assignment and the actual school-local period end. Missing signature enrollment permits task reading/drafting but signing remains blocked. Dirty navigation is conditional; successful reads do not install a permanent reload warning. A failed draft save retains typed text. A lost signed-submit/approval response retries the exact request identifier and reviewed body.

Approval fingerprints include timetable/setup/teacher revisions, current absence, need revisions/states, counters and the chosen plan. Serializable SQL encloses every assignment, version, record, audit and outbox insertion in the batch. Corrected attendance, an absent substitute, publication changes or changed lesson snapshots require review. Explicit release restores original ownership through another immutable ledger entry; it is unavailable after the lesson ends or any signed execution. Past-date records remain readable and teachers can complete retained old assignments; new past-date coverage cannot be created.

## Verification and boundary

See [phase 2 evidence](../verification/standby-phase2/README.md) for real SQL transaction fault injection, focused regression results, browser captures and acceptance limits. The current attendance model supports full-day Present/Absent/Excused only. No deputy signing or fabricated attendance ranges were added. The official QF71-B-2-1 artifact, PDF storage/archive integration and physical print acceptance remain phase 3.
