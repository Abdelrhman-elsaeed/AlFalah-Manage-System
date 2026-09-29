# W7 — Security Gate Two-Step Exit Workflows

Date: 2026-09-29  
Scope: W7 only (`SecurityGuard`)  
Canonical landing: `/student-affairs/security`

## Executive summary

W7 replaces the placeholder/read-only Security landing with one real, server-paged operational queue and preserves `/student-affairs/gate-passes/security` as a redirect. The enforced workflow is:

`Requested → Approved → SecurityAcknowledged → Exited`

Security acknowledgement and physical exit are separate writes. Both require the exact `SecurityGuard` role, the operation permission, active-school scope, the latest opaque RowVersion, and server validation of the half-open execution window. Exit cannot be recorded directly from `Approved`, and `ExitedAt` was removed from the client request contract; only `TimeProvider` supplies the official time.

No W8 work was implemented.

## Main implementation changes

- API contracts now expose a dedicated minimum `SecurityGatePassDetailDto` and `SecurityGatePassQueuePageDto`, including server time and latest RowVersion.
- `GET /api/v1/gate-passes/security-queue/{id}` provides the same minimal projection for conflict/timeout reconciliation; Security no longer uses the broad Gate Pass detail endpoint.
- queue and dashboard queries are implemented in `GatePassWorkflowRepository` with database filtering, projection, stable ordering, and paging.
- acknowledgement and exit handlers enforce exact role, permission, active school, state, `[start,end)`, and concurrency inside the application layer.
- state conflicts map through the existing `FromResponse` policy to HTTP 409; out-of-scope IDs remain 404.
- broad detail/history/cancel checks were tightened so an injected Security permission cannot grant Officer/Guardian authority.
- the Angular Security screen now owns the canonical route, uses the server clock offset for display, separates acknowledgement from exit, and reconciles uncertain results before allowing retry.
- the old execution URL redirects to the canonical route; the shell exposes one Security surface only.

## Security authority matrix

| Use case | Exact role | Required permission | Scope/result |
|---|---|---|---|
| Landing/queue/dashboard | `SecurityGuard` | `StudentAffairsDashboard.Security` (action permissions may also read the queue API) | active school; Approved/Acknowledged only |
| Safe reconciliation detail | `SecurityGuard` | dashboard, acknowledge, or execute permission | active school; minimal Approved/Acknowledged/Exited detail only |
| Acknowledge pickup match | `SecurityGuard` | `GatePass.AcknowledgeSecurity` | `Approved → SecurityAcknowledged` |
| Record physical exit | `SecurityGuard` | `GatePass.Execute` | `SecurityAcknowledged → Exited` |
| General detail/history/cancel/approve/reject | role-specific Guardian/Officer authority | existing role-specific permission | Security is denied even with an injected unrelated permission |

Authentication and `ActiveSchoolId` are checked in handlers. The existing authenticated identity/role/permission claims are the authority for an active assignment; no parallel Security profile or schema was invented.

## Queue, ordering, and privacy

The server query always fixes school and state before applying paging. Client `status`, date, or classroom values cannot widen or replace the Security scope. The optional inherited search contract only narrows by student name/number or pickup-person name.

Stable ordering is:

1. active execution window first;
2. `ApprovedWindowEndsAt` ascending;
3. approval/review timestamp ascending;
4. Gate Pass ID ascending.

The projection contains only Gate Pass ID, student display name/number, safe class label, approved window, status, minimum pickup name/relationship/identity hint, officer display summary, approval/acknowledgement timestamps, and RowVersion. It deliberately excludes Guardian IDs, full identity numbers, attendance/excuse/conduct/case/referral/summon data, messages, notification bodies, automation metadata, arbitrary staff IDs, and entity graphs. No storage key is exposed as a public photo URL.

The officer-name lookup is a scalar subquery in the single projected page query; there is no query inside a row loop. A SQLite relational test executes the query, with a test-only UTC-ticks conversion to compensate for SQLite's lack of native `DateTimeOffset` ordering.

## State-transition matrix

| Current state | Action | Next state | Result otherwise |
|---|---|---|---|
| `Approved` | Security acknowledgement | `SecurityAcknowledged` | wrong state is a 409 state conflict |
| `SecurityAcknowledged` | Record physical exit | `Exited` | direct `Approved → Exited` is rejected |
| any terminal/alternative state | either write | unchanged | 409 conflict or scoped 404 |

Teacher acknowledgement remains an independent receipt and does not change Gate Pass state.

## Execution-window contract

Both writes use `TimeProvider.GetUtcNow()` and the same server helper:

`ApprovedWindowStartsAt <= now && now < ApprovedWindowEndsAt`

- exact start: allowed;
- exact end: rejected;
- before start, after end, or a missing boundary: rejected.

The frontend countdown uses `ServerNow` to calculate an offset, disables obviously invalid actions, and remains display-only. The server always revalidates the command.

## Acknowledgement contract

Successful acknowledgement stores server actor/time, updates `UpdatedByUserId`, appends one immutable `Approved → SecurityAcknowledged` transition, and appends one `GatePassSecurityAcknowledgedEvent`. It returns the minimal updated detail and latest RowVersion. Repeated or stale submissions do not create a second transition/event.

The UI requires a distinct confirmation dialog and describes acknowledgement as pickup verification, not physical exit.

## Exit and verification contract

Exit accepts only:

- a defined `PickupVerificationMethod`;
- a required, trimmed, non-whitespace verification note (maximum 1000 characters);
- an optional normalized gate note (maximum 1000 characters);
- the latest RowVersion.

`ExecuteGatePassRequestDto` no longer contains `ExitedAt`. On success the server sets `ExitedAt`, `ExitRecordedByUserId`, verification metadata, one immutable transition, and one `StudentExitedSchoolEvent`. Different guards may perform the two steps; each actor is recorded independently.

## Concurrency and timeout recovery

- pre-save stale RowVersion is rejected before mutation;
- EF concurrency failures return 409 and the relational SaveChanges/outbox transaction is rolled back;
- the frontend never retries a mutation automatically;
- 409 and status/network uncertainty trigger the minimal detail refetch;
- while reconciliation is in flight, all repeat actions and dialog closes are locked;
- if the desired state already exists, success is shown from server data without a second mutation;
- otherwise the queue receives the winner's RowVersion and the exit-form draft is preserved for an explicit retry.

## Events, outbox, and audit evidence

Each successful transition records FromStatus, ToStatus, ActorUserId, ActorRole, server timestamp, and correlation ID. Exit also records verification metadata. `AlFalahDbContext.SaveChangesAsync` persists the aggregate/transition and outbox messages within the existing relational transaction, then clears domain events only after commit. Tests assert exactly one acknowledgement outbox message and one exit outbox message, with repeated commands leaving one transition/event.

## Frontend UX and accessibility

- responsive RTL card queue with loading, empty, 401/403-aware error, and success receipt states;
- manual refresh plus one lifecycle-bound 30-second/focus/visibility refresh stream;
- server paging and stable server projection;
- status badges, window state, countdown, and disabled out-of-window actions;
- separate acknowledgement confirmation and physical-exit form;
- safe enum dropdown, labelled required note, optional gate note, and 1000-character limits;
- double-submit and reconciliation locks;
- `aria-live` operation receipt, modal focus management through PrimeNG, keyboard-capable native controls, and tablet-sized primary actions;
- dashboard-only permission remains read-only; action visibility also checks the exact role and action permission.

## Test coverage added

Backend coverage includes exact role/permission/active-school checks, same-school hiding, queue states/order/paging, real dashboard counts, relational query execution, half-open boundaries, missing windows, state restrictions, RowVersion conflicts, invalid enum/notes, server-owned time, duplicate prevention, safe detail access, injected-permission regressions, and event/outbox counts.

Frontend coverage includes canonical route/alias and navigation, load/empty/error/success, dashboard-only action hiding, `[start,end)` behavior, latest RowVersion, no client timestamp, double-submit protection, 409 refetch, locked reconciliation, achieved-after-timeout behavior, and draft preservation. Timer tests use `fakeAsync`.

## Final verification

The repository contains `backend/AlFalah.slnx`; the prompt's `backend/AlFalah.sln` path does not exist, so the real solution file was used.

| Command | Result |
|---|---|
| `dotnet build backend/AlFalah.slnx -c Release --no-restore` | passed; 0 errors, 1 pre-existing nullable warning |
| `dotnet test backend/AlFalah.slnx -c Release --no-build` | passed; 710 passed, 0 failed, 0 skipped |
| `npm test -- --watch=false --browsers=ChromeHeadless` | passed; 209 passed, 0 failed |
| `npm run build -- --configuration production` | passed |
| `dotnet ef migrations has-pending-model-changes ... --context AlFalahDbContext --no-build` | no model changes since the last migration |
| `git diff --check` | passed |

The frontend build retains the pre-existing CSS budget warnings for `dashboard-live.component.css` and `visit-workspace.component.css`, plus three PrimeNG organization-chart selector parsing warnings. EF emits pre-existing global-query-filter relationship warnings during design-time model creation. The backend build retains the previously documented `SocialWorkerWorkflowTests.cs:41 CS8602` nullable warning.

## Migration decision

W7 changes contracts, authorization, queries, UI, and tests only. No entity/model/schema change was required, the EF pending-model check is clean, and no migration was created or modified. The latest existing migration remains `20260928181703_AddW5OfficerReferralIdempotency`.

## Deferred risks and later work

- W8 Social Worker CRM and all W9/W10 work remain untouched.
- QR/barcode scanning, SignalR, generic student directories, and broad audit viewers remain out of scope.
- Existing EF global-query-filter relationship warnings and unrelated frontend CSS budgets remain repository-wide maintenance items, not W7 regressions.
- A cross-role deployed-environment E2E remains W10 scope; W7 is covered by application, relational repository, route, and component tests.

## Completion statement

W7 is implemented as one two-step, server-authoritative Security workflow. There is no direct `Approved → Exited` path, exact end is rejected, client exit time is absent, cross-school/general-detail leakage is blocked, and uncertain writes reconcile before retry. No commit or push was performed.
