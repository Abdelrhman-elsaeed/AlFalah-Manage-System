# N1 implementation and verification

**Date:** 2026-10-17. **Scope:** reliable REST inbox and authenticated website realtime. N2/PWA/Web Push and N3 deployment are not implemented here.

## Implemented behavior

- Additive latest/history message window with protected user/school/conversation/purpose cursors and `(viewer visibility time, ID)` ordering. Sender queue time remains stable; a recipient sees the actual release time. The legacy ascending page endpoint is retained.
- Current database grants and active school/user membership independently govern message and notification aggregates. SQL fixtures cover 121 unread conversations, cross-school exclusion, pending/suppressed/approval exclusion and explicit read/read-all behavior.
- Business saves atomically project generic recipient journal changes and realtime intents. Sequence allocation is recipient-scoped, safely initializes under key/range locks, and is held until the enclosing transaction commits. Startup seed/history traffic is excluded. Both SaveChanges paths are covered by the projection; no event body/student data is stored in the new transport payload.
- Snapshot reads validate the committed recipient sequence around their count queries, retry crossed commits and use a Serializable fallback. Real SQL tests cover a commit deliberately inserted between message and notification counts. This avoids holding broad count locks on every live refresh.
- Canonical Duty surfaces share one intent identity. A later surface re-arms that identity and fences any older lease, including when the first hint was already sent. Independent read transitions still have their own invalidations.
- Application connection/delivery services back a thin hub, outbound publisher and supervised worker. Claims explicitly set Read Committed so a pooled connection's previous isolation level cannot break READPAST. Conditional completion, expiry, attempts, leases and bounded cleanup recover transport failures independently of business processing.
- One Angular coordinator owns connection/recovery/fallback, drains protected cursors, clears state on context/grant changes and ignores stale responses. Chat, sidebar counters and guardian notifications use it. It retries initial connection failures, refreshes after focus/online/reconnection, retains unsent Arabic drafts and pauses polling in hidden tabs.
- The chat follows latest messages when the user is at the bottom. Reading older history preserves the position and shows an explicit new-message action; incoming messages are not marked read until the latest view is shown. Visual checks verify actual viewport placement, not merely DOM presence.

## Evidence

| Check | Final outcome |
|---|---|
| Focused backend + real SQL + actual Duty producer | **43 passed, 0 failed, 0 skipped**; [sanitized named results](backend-results.json), including cleanup racing a freshly re-armed intent |
| Angular coordinator unit checks | **2 passed** in Chrome Headless 155, including old-school response rejection and expired-cursor recovery |
| Browser/protocol/regression checks | **8 passed, 0 failed, 0 skipped**; [sanitized results and latency samples](browser-results.json) |
| Desktop request-start-to-viewport p95 | **2,089 ms**, ten sequential messages / two sessions |
| Mobile-width request-start-to-viewport p95 | **1,948 ms**, ten sequential messages / two sessions |
| Backend Debug/Release builds | Pass; pre-existing `WeeklyStandbyRepository` CS9107 warning remains |
| Angular Development compile/build and application typecheck | Pass; latest code also compiles in the actual E2E development server |
| EF model consistency | No pending model changes after the additive migration |
| Production Angular build | Pre-existing two CSS-budget failures remain; see limits below |

Desktop and mobile screenshots were visually inspected: [desktop](inbox-desktop.png), [mobile width](inbox-mobile.png). Latest messages appear inside the timeline viewport and the Arabic draft remains. These are Chromium desktop/emulated-width checks, not real iPhone or Android Web Push evidence. No auth artifacts, synthetic bearer tokens, raw network traces or secrets are retained in this documentation.

Acceptance mapping: SC-001 -> `InboxWindowTests`; SC-002 -> aggregate/read-all SQL tests and live read receipts; SC-003 -> both viewport latency profiles above; N1 SC-004 -> SQL rollback/claim/lease/deadlock/canonical re-arm tests; N1 SC-005 -> protected cursor tests, current-grant/stamp checks, active-connection deactivation/expiry and Origin/query-bearer protocol cases. Device/subscription/worker-cache assertions belong to N2/N3.

Backend suites: `InboxWindowTests`, `InboxSecurityTests`, `InboxSqlTests`, the new `Inbox_N1_actual_Duty...` case in `DutyExecutionSqlTests`, and existing `MessagingDeliveryTests` / `GatePassAndMessagingMediatRTests`. The original 60-message expectation was run against the old API first and failed; the new window passes while a legacy compatibility assertion keeps the old page behavior.

SQL tests use fresh guarded `AlFalahInboxTests_*` and `AlFalahDutyTests_N1_*` LocalDB databases; existing names are refused. They rehearse all migrations, preserve a pre-N1 sentinel and run migration twice without historical journal rows. The four new tables are additive; no operational Development/customer database is updated. The existing LocalDB guards are not weakened. Isolated databases are retained for review rather than silently resetting another database.

The browser runner uses a fresh guarded `AlFalahN1E2E_*` database, API 5365 and Angular 4305. Existing listeners on 5264/4200 are left running. Signature-dependent synthetic fixtures enroll through the real signature API. Both HTTP and WebSocket test traffic use the isolated API. Browser cases cover two sessions, a failed first negotiate, missed-event reconnection/focus recovery, read receipts, retained drafts, history reading, token expiry, hostile Origin rejection and deactivation of a dedicated connected test account.

The latency profile is **two local sessions, ten sequential messages, LocalDB and a healthy local network**, repeated at desktop/mobile widths. It is a request-start-to-viewport upper bound, including HTTP write time; it is not a Gulf network or Hostinger capacity benchmark. Detailed independent queue/server/network instrumentation and realistic peak/fan-out tests remain N3. JWT expiration uses real protocol time; inbox production/dispatch uses the same injected TimeProvider, verified by a regression after the frozen E2E clock exposed a mixed-clock scheduling defect.

## Existing failures and launch limits

- Development Angular build and focused N1 unit checks are separate from the production build. The production build still fails the pre-existing 32 kB component CSS budget for `visit-workspace.component.css` (34.50 kB) and `storage-page.component.css` (39.34 kB). No budget was relaxed.
- The normal broad Angular test compilation is already broken by storage specs calling `ngOnChanges()` without its required argument and a visit archive fixture missing `schoolEnabled`. An additional isolated shell-navigation run has 16/17 passing, with a pre-existing people-route list expectation (six expected versus ten existing). The two new coordinator tests pass under an explicit focused TypeScript config; none of these unrelated failures is hidden by editing their assertions.
- Windows targets .NET 8 but this machine executes ASP.NET tests with the existing .NET 10 roll-forward setting. Earlier Linux preflight verified the pre-feature baseline on .NET 8; it is not evidence for running this new N1 code on the purchased VPS. Rehearse the final supported target runtime in the deployment work.
- Angular 17 is unsupported and .NET 8 support ends 2026-11-10. No support exception, broad framework upgrade, VPS purchase, domain change, production migration or deployment is approved by this result. The Linux preflight's key/proxy/startup/storage/configuration gates still apply.
- Journal/realtime flags default off. [Activation, rollback and reproduction](../../quickstart.md) require a selected migrated environment, correct origins, stable Data Protection keys and one supervised API process. The in-process live connection registry does not claim multi-instance delivery.
- Customer workload, legal retention and operational ownership remain Q03/Q06/N3 gates. The seven-day recovery buffer is technical configuration; original business messages/notifications are retained. Existing complaint notifications that remain Pending are not silently promoted to Delivered.

N2 must add source-aware external delivery/subscription protection before sending Web Push; it must not reinterpret N1 generic invalidation rows as historical phone alerts.
