# Implementation Verification and Rollout Runbook

**Status:** N1 local implementation/verification instructions; N2/N3 staging and deployment steps remain planned. No production deployment has run.

## Selected direction - VPS

The owner chooses VPS and prefers Hostinger (2026-10-17). KVM 4 is the proposed starting point when the API, workers, SQL Server and PDF generation share a machine; KVM 2 is the budget alternative for verified light load. Plan/region purchase is not recorded as completed. See [the hosting decision](plan.md#owner-hosting-decision---2026-10-17) and [source follow-up](research.md#vps-selection-follow-up---2026-10-17).

Before application deployment, select compatible supported Linux/.NET/SQL versions and a production SQL edition, rehearse the application and Arabic PDF/export flows on Linux, configure supervised startup/restart and HTTPS/WebSockets, and test an independent SQL backup/restore. Provider weekly server backups are supplemental. Confirm term/prepaid/renewal totals at checkout; the monthly equivalent is not a monthly billing commitment.

Read the [actual Linux preflight findings](verification/linux-preflight/README.md) before moving data or publishing a release. In particular, copying a Windows DPAPI key ring is insufficient for Linux credential continuity, and the existing production initialization flag does not disable migrations/seeding. Resolve the documented file-containment and frontend release-build defects before publishing.

## Before buying or provisioning hosting

Choose a plan that demonstrably supports the application, not merely one advertised as ASP.NET hosting. A nearby European region can be suitable for Gulf users, but measure representative connections rather than assuming a distance-based response time.

| Check | Evidence required from the selected plan / operator |
|---|---|
| Supported runtime | Exact supported .NET/runtime/OS versions and upgrade path; current application is .NET 8. |
| Worker lifetime | Supervised startup after reboot/deploy, restart on failure and documented idle/recycle behavior. Both Student Affairs and Duty workers already need a running process. |
| Realtime | WebSocket support through the actual reverse proxy and idle timeout settings; tested reconnect/fallback. |
| Database | SQL Server compatibility, secure connectivity, sufficient storage/IO, explicit licensing cost and restore access. Do not silently replace SQL Server with a cheaper engine. |
| Push egress | HTTPS outbound access to verified browser push services and key/secret storage. The browser's push provider is not selected by geographic proximity alone. |
| Resources | Declared CPU/RAM/storage and load evidence including database and worker overhead; no arbitrary minimum sizing is promised. |
| Operations | Monitoring, logs with secret redaction, updates, backups and a person accountable for incidents. Managed service versus self-managed VPS is an operating responsibility decision. |
| Region | Measured Gulf latency under a realistic network, database location and backup/data residency acceptable to the customer. |

Shared hosting was considered earlier; the active owner decision is VPS. A VPS grants control but still needs an operator. Public Hostinger/OVH capabilities have been reviewed in research R14-R18; actual account provisioning, provider uptime, workload capacity and final checkout prices have not been verified.

## Domain and HTTPS preparation

1. Record the customer's registrar/DNS administrator and obtain authorized access through their normal process. Domain ownership does not imply access is already available.
2. Select the final application origin, such as a dedicated subdomain, before real PWA installation/subscription enrollment. Keep API/hub on the same origin initially where practical.
3. Inventory existing DNS, especially email MX/TXT records; change only the required records during an authorized deployment. Configure the hosting certificate, HTTPS redirect and automatic renewal.
4. Verify certificate chain, public reachability, secure WebSocket and direct serving of manifest, worker scripts and worker configuration. These paths must not redirect to login or return the SPA HTML in place of JSON/JavaScript.
5. Keep staging on a separate origin, database and key set with synthetic users. Never restore production data into an enabled staging push dispatcher.

## N1 developer verification

N1 journal and realtime default **off**. Existing inbox REST counts/windows work independently of realtime. For an explicitly migrated staging database, set `Inbox__JournalEnabled=true`, `Inbox__RealtimeEnabled=true`, `Inbox__RetentionDays=7`, and the exact `Inbox__AllowedOrigins__0` / CORS origin; restart the API. Use one supervised API instance for N1 (the live connection registry is in-process). Multiple replicas need a backplane/shared routing design and separate verification before use.

Apply the additive migration to the selected database through a controlled migration step before enabling the journal. Do not infer the tooling database from the API settings: `AlFalahDbContextFactory` reads `ALFALAH_MIGRATIONS_CONNECTION`. Keep production credentials outside source/output. Establish the current state on first client load; do not backfill historical inbox rows. Startup fixture/seeding saves explicitly suppress projection. Stable Data Protection keys must survive API restart or clients must obtain a fresh baseline after cursor rejection.

Rollback disables realtime dispatch, then journal production if necessary, and restarts the process. Retain the four additive tables and business history. The REST fallback remains available; an expired/invalid cursor recovers through full state/resource reads. Do not enable future push from N1 generic intents. The operational Development/customer database has not been migrated or enabled by this implementation task.

Focused backend verification (repository root; both names must be fresh):

```powershell
$env:ALFALAH_INBOX_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahInboxTests_' + [Guid]::NewGuid().ToString('N') + ';Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False'
$env:ALFALAH_DUTY_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_N1_' + [Guid]::NewGuid().ToString('N') + ';Trusted_Connection=True;TrustServerCertificate=True'
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --filter 'FullyQualifiedName~InboxSqlTests|FullyQualifiedName~InboxWindowTests|FullyQualifiedName~InboxSecurityTests|FullyQualifiedName~Inbox_N1_actual|FullyQualifiedName~MessagingDeliveryTests|FullyQualifiedName~GatePassAndMessagingMediatRTests'
dotnet build backend/AlFalah.Api/AlFalah.Api.csproj --configuration Release
```

Focused frontend verification (`frontend/`):

```powershell
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.inbox-spec.json --include=src/app/core/services/inbox-sync.service.spec.ts
$env:E2E_DATABASE_NAME = 'AlFalahN1E2E_' + [Guid]::NewGuid().ToString('N')
$env:E2E_API_PORT = '5365'
$env:E2E_WEB_PORT = '4305'
$env:E2E_ENROLL_SIGNATURES = '1'
$env:Inbox__JournalEnabled = 'true'
$env:Inbox__RealtimeEnabled = 'true'
npm run test:e2e -- inbox-realtime.spec.ts messaging-send-regression.spec.ts responsive-and-dialog-regression.spec.ts
```

The E2E runner supplies fresh synthetic JWT/password settings, guarded local SQL scope and actual fixture signature enrollment. HTTP and WebSocket test connections both target the isolated API. Existing listeners on 5264/4200 are not stopped or reused. The focused test TypeScript config avoids unrelated, already-broken storage-spec compilation without changing the normal test target. [Actual outcomes and limitations](verification/n1/README.md) remain the acceptance record.

Read the constitution and current package first. Establish the database and test environment using existing repository instructions; do not run migration/update commands against production during development.

Existing baseline commands, from the repository root:

```powershell
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --filter "FullyQualifiedName~MessagingDeliveryTests|FullyQualifiedName~GatePassAndMessagingMediatRTests" --verbosity minimal
```

Frontend commands from `frontend/`:

```powershell
npm ci
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

The broad baseline commands above are not all passing: current production CSS budgets and unrelated storage-spec typing failures remain documented. Use the named N1 outcomes and preserve these distinctions; do not weaken budgets or claim EF InMemory establishes SQL correctness.

N1 smoke sequence:

1. Create two synthetic permitted users in one school plus another-school negative case. Seed a 60-message thread and >=120 unread conversations in an isolated fixture.
2. Verify latest 50, complete older history, exact server counts and explicit read transitions with realtime disabled.
3. Start the authenticated hub, exchange messages in two sessions and verify counters/chat/notification surfaces refresh from committed state.
4. Disconnect, create/release multiple changes including a queued low-ID message, reconnect and drain all journal pages. Repeat after cursor retention expires.
5. Exercise initial connection failure, token expiry, logout, school change and permission revoked while connected. Retain scoped diagnostic evidence.
6. Run SQL rollback, reversed commit order, stale lease, duplicate key and concurrent first-stream initialization tests.

## N2 HTTPS staging verification

Create VAPID credentials only in the authorized staging setup, store the private key as an operator secret and record a key ID. Back up required encryption/signing keys securely. No credentials belong in this documentation or the frontend.

Enable registration/dispatch only for synthetic eligible accounts. Use the selected Angular production-style build served over HTTPS; `ng serve`/localhost alone does not prove phone install/background behavior. Confirm the actual deployed frontend output and backend publishing paths from build configuration rather than assuming the Angular application builder's directory layout.

### Device evidence matrix

Each row starts **pending**. Record exact OS/browser versions, permission settings, install context and network, then actual outcomes.

| Environment | Required checks | Status |
|---|---|---|
| iPhone/iPad with supported iOS/iPadOS, Home Screen app | Add to Home Screen, explicit permission, background/no-tab notification, click/login and Focus settings | Pending |
| Same Apple device in ordinary tab / without required setup | Explain unavailable/install-required state; usable inbox; no false enable success | Pending |
| Android Chrome on a representative phone | Install behavior, explicit permission, background delivery/click, battery restrictions | Pending |
| Desktop Chrome and Edge | Foreground realtime, permission, closed-tab click and subscription lifecycle | Pending |
| Safari/macOS and Firefox, if advertised | Actual subscription/provider behavior and lifecycle; unsupported state where necessary | Pending |
| Denied permission / private browsing / unsupported browser | Accurate fallback, no repeated prompt, clear settings instructions | Pending |

For every supported positive row test: approved immediate message; queued office-hour message before/after release; behavior notice before/after approval; one Duty event with two inbox surfaces; duplicate/retry; multiple devices; read on another device; revoked guardian; stale target; key rotation; reinstall; account/school change; network loss and offline logout.

Record server commit, eligibility, worker start, provider acceptance and observed device arrival separately. Disable DevTools during a real background check because attached tooling can alter service-worker lifetime. Do not present a simulated notification as a delivered live push.

## N3 operations, failure and load checks

- Expose a protected heartbeat and oldest eligible queue age, provider acceptance latency, throughput, retries, expiry, no-device outcomes, invalid subscriptions and dead-letter counters. Public health probes must not expose school data or secrets.
- Alert on a stopped worker or growing eligible queue, not merely on HTTP uptime. Route the alert to a named operator using the agreed monitoring system; this planning task sends no messages.
- Kill/restart a staging worker during dispatch, including after provider acceptance but before persistence. Verify durable recovery and possible duplicate banner handling without duplicate business effects.
- Simulate 429/`Retry-After`, transient failures, signing errors, invalid endpoint, oversize payload and a blocked provider. Check retry limits, expiry and per-device isolation.
- Load-test an explicit workload: number of active schools, connected users, sends/sec, fan-out size and retained rows. Report p95/p99 where useful, SQL query plans and queue drain time. Replace synthetic assumptions with actual expected usage before sizing production.
- Test from representative Gulf networks. Separate DNS/TLS, static asset, API/database, worker wait, provider acceptance and observed phone delay. A CDN can help public assets but does not remove database/worker latency.
- Restore a backup into isolated infrastructure with outbound push disabled. Verify data-protection/VAPID key recovery, school isolation, read state and expired/cancelled work before any dispatcher is allowed to run.

## Pilot and channel rollback

Proposed controls: `InboxJournalEnabled`, `RealtimeDispatchEnabled`, `WebPushRegistrationEnabled`, `WebPushDispatchEnabled` and `PwaUiEnabled`. These are design names, not existing configuration keys. Switches must have documented defaults and tested effects; turning off UI alone does not deactivate an installed worker.

1. Start with synthetic accounts and a stable HTTPS deployment. Enable journal/intents and verify the no-history-backfill baseline before client enrollment.
2. Move to a specifically agreed school/user cohort only during an authorized rollout. Explain installation/permission, delivery limits and where the authoritative inbox lives.
3. Watch queue age, errors, latency, device opt-in/failures and school feedback during a declared observation window.
4. Pause broader rollout for any data leak, missed durable intent, repeated stale alert, material queue growth, broken click navigation or unsafe cached content. Disable affected dispatch first; retain the inbox and audit records.
5. Expire/cancel ineligible queued targets before re-enabling; replay only audited eligible delivery jobs, never the underlying business action.

### Installed-worker rollback

Disabling push dispatch stops new provider submissions; it does not recall accepted pushes. Disable registration/UI separately as needed. A frontend deployment rollback alone can leave old installed workers active.

For Angular worker removal, rehearse the [official procedure](https://angular.dev/ecosystem/service-workers/devops): return an actual 404 for `ngsw.json`, or serve the package's `safety-worker.js` contents at the old worker script URL when that stronger removal procedure is needed. Keep that endpoint available for stale clients as prescribed by the selected Angular version. Exclude it from SPA fallback and redirects; do not register a different safety-worker URL and assume old clients will see it.

Test with a previously installed, stale client, then confirm cache cleanup, authenticated network behavior and re-enrollment guidance. Keep additive database tables during rollback; dropping them while workers/clients reference them is unsafe. A return to a non-PWA build must still support the normal web inbox and bounded foreground refresh.

## Completion report

Record task/requirement evidence, outstanding version/support exceptions, actual deployment and device matrix, approved operating settings, alert owner, restore/rollback results and native-contract boundary. Update [the checklist](checklists/requirements.md) and [central tracker](../../phases/PHASE-NOTIFY-PWA.md). A planned or staged rollout is not a completed production launch.
