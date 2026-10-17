# N1 - Reliable Inbox and Authenticated Realtime

**Status:** Implemented and locally verified (2026-10-17); production deployment gates remain.  
**Stories:** US1, US2. **Tasks:** T001-T011 in [tasks.md](../tasks.md).  
**Dependency:** Read constitution, [spec](../spec.md), [research](../research.md) and [plan](../plan.md).

## Outcome

Current messages and counts are correct through REST, and signed-in users see committed changes promptly while the website is open. Background browser push remains disabled.

## Included work

- Repair initial latest-window/history retrieval and the first-100 unread-count cap; preserve legacy API compatibility.
- Introduce scoped, commit-safe inbox synchronization; recover delayed release of old message IDs and long disconnects.
- Persist channel intents with visibility transitions, preserving existing outbox/business policy.
- Integrate classified notification producers, including Duty's separate System-message/dashboard paths, with canonical-event deduplication.
- Add a thin authenticated SignalR hub and one frontend synchronization coordinator with reconnect and fallback behavior.
- Assess supported framework/package matrix, realistic workload and retention requirements before locking migrations.

Do not implement browser subscription endpoints, install a root service worker, send device push or add native SDKs in N1.

## Required checks

- 60-message regression, equal-time ordering, older-page completeness and sender-pending state.
- Late office-hour release with a lower ID than already observed messages; concurrent commit order and stale cursor recovery.
- >=120 unread conversations; accurate read/read-all behavior without push-driven reads.
- Real SQL Server rollback, key uniqueness, claim races, deadlock/retry and stale-lease completion. EF InMemory alone is insufficient.
- Approval, suppression, inactive guardian/membership and cancellation regressions; one delivery identity for a Duty fact/revision/recipient.
- Two-session live updates, initial connect failure, reconnect after missed events, token expiry and authorization revoked on an existing connection.
- Arabic/RTL mobile/desktop review for chat, counters, disconnected status and retained drafts; no duplicate timers or accumulating connection handlers.

## Exit evidence

- [x] SC-001, SC-002, SC-003 and N1 portions of SC-004/SC-005 have [named tests/results](../verification/n1/README.md): 43 backend/SQL, 2 coordinator and 8 browser checks pass; local viewport p95 2,089/1,948 ms.
- [x] Migration rehearsal and journal activation/baseline strategy are documented; historical rows do not become new push events.
- [x] Actual version assessment and remaining Q02/Q03/Q06 decisions are recorded, with production implications stated.
- [x] Backend/frontend build and focused checks have actual outcomes; pre-existing production CSS/broad storage-test failures are distinguished.
- [x] Requirement/API/schema/security/central tracker/changelog/decision records are updated (C49/D-141).

Stop after reporting the phase result. An unimplemented item remains unchecked; do not start N2 simply because N1 code compiles.
