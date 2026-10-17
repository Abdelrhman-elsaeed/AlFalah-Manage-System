# Research and Evidence

**Checked:** 2026-10-17. Recheck version support and hosting capabilities when implementation starts.

N1 implementation follow-up: the official SignalR JavaScript 8.x package was verified from the npm registry and pinned at 8.0.29. .NET 8 APIs were checked against Microsoft's [SignalR authentication guidance](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0), [JavaScript connection lifecycle](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-8.0) and [EF transaction guidance](https://learn.microsoft.com/en-us/ef/core/saving/transactions). No .NET 11 authentication-refresh API is used. Initial connection retry is implemented explicitly. Actual N1 runtime evidence is kept [separately](verification/n1/README.md); the baseline observations below are historical, not a claim that N1 has no implementation.

## Evidence levels

**Observed** means inspected repository behavior; **reproduced** means a specific isolated check; **documented** means an official source describes a capability; **proposed** means a design decision in this package. None of these establishes real-device or production readiness. No new runtime tests were executed for this documentation change.

## Repository baseline

Paths below are repository-relative. Line numbers are intentionally omitted because subsequent implementation will move them.

| Finding | Evidence | Consequence |
|---|---|---|
| Angular 17.3, PrimeNG 17 and ASP.NET Core / EF Core 8 with SQL Server | `frontend/package.json`, `backend/AlFalah.Api/AlFalah.Api.csproj`, Infrastructure project | Select compatible packages; resolve current support risk. |
| Chat refresh is foreground polling, not push | `frontend/src/app/features/student-affairs/messaging-chat/messaging-chat.component.ts`: 25-second timer and visibility/focus refresh | Open website updates and background device notifications both need implementation. |
| Initial page requests ascending page 1; repository takes oldest 50 | `frontend/src/app/core/services/phase5.service.ts`; `backend/AlFalah.Infrastructure/Repositories/MessagingWorkflowRepository.cs`, `GetConversationMessagesAsync` | Confirmed latest-message defect, not fixed by installing PWA. |
| A 60-message isolated reproduction returned IDs 1-50, no older page and no latest message | Prior audit in this session, EF InMemory executable outside the repository | Reproduce as a checked-in regression in N1; this is not SQL concurrency evidence. |
| Message badge sums only the first 100 listed unread conversations | `frontend/src/app/core/services/messaging-unread.service.ts` | Static finding: server aggregate and >100 regression needed. |
| Shell message count polls about every 30 seconds; guardian notification list is refreshed on navigation/manual actions | `frontend/src/app/shared/layout/shell/shell.component.ts`; `frontend/src/app/features/student-affairs/guardian-notifications/guardian-notifications.component.ts` | Both message and notification surfaces must consume resynchronization. |
| Existing durable automation work runs inside the API process | `backend/AlFalah.Infrastructure/Automations/StudentAffairsOutboxWorker.cs`, `StudentAffairsOutboxProcessor.cs`, Infrastructure DI | Requires a running supervised host; a database queue does not wake a stopped application. |
| Outbox has SQL conditional claims, retry/backoff and dead letters; defaults include 25-item batches, 8 attempts and a 120-second lease | Same processor | Reuse existing business outbox; verify lease-overrun behavior before concurrency or topology changes. |
| Domain facts/outbox and message send/release use transactional persistence | `backend/AlFalah.Infrastructure/Data/AlFalahDbContext.cs`; messaging repository | Extend existing transaction seams; do not send to providers during database transactions. |
| `Notification.Delivered` and delivered message receipts indicate in-app availability | `backend/AlFalah.Infrastructure/Notifications/StudentAffairsNotificationDispatcher.cs`; notification/messaging repositories | They do not prove phone delivery; retain stored enum values and introduce separate channel results. |
| Guardian-to-teacher release obeys office hours; behavior/academic notices can require approval | Messaging repository; notification dispatcher and notification repository | Push intent must follow visibility, not mere creation or approval request. |
| Duty dismissal has two delivery surfaces with a separate worker | `backend/AlFalah.Application/Duty/DutyNoticeProcessor.cs`; `backend/AlFalah.Infrastructure/Services/DutyNoticeWorker.cs`; `backend/AlFalah.Infrastructure/Repositories/DutyExecutionRepository.cs` | Message path explicitly emits no generic `MessageReleased` event; integrate directly and deduplicate against the dashboard notice. |
| Other notification writers exist | `DutyRepository.Notify`, `StudentAffairsAutomationRuleEngine`, `ComplaintService`, seeders | Inventory all writers; classify eligibility. Complaint notifications need explicit visibility review, not automatic promotion to external push. Never push seeded/history rows. |
| No implemented PWA, Web Push subscription store or SignalR hub found in the audited source/configuration | Searches across frontend packages/config and backend registrations | Planned capabilities are unimplemented. Search results are not a deployment inspection. |

Earlier focused baseline checks passed 24 messaging tests and two selected notification-policy tests. They do not cover PWA, push, a live provider, production load or SQL Server claim races. The old pagination reproduction remains a known unfixed defect despite those passing tests.

## Official-source register

Each row states the narrow fact used by this design. Sources were opened during planning; exact package APIs must still match the selected implementation versions.

| ID | Primary source | Verified fact and design implication |
|---|---|---|
| R01 | [GitHub Spec Kit](https://github.com/github/spec-kit), [spec template](https://raw.githubusercontent.com/github/spec-kit/main/templates/spec-template.md), [plan template](https://raw.githubusercontent.com/github/spec-kit/main/templates/plan-template.md), [tasks template](https://raw.githubusercontent.com/github/spec-kit/main/templates/tasks-template.md) | Specifications, technical planning and story-linked tasks are separate artifacts. Adapt the advisory templates to the repository; template example phases are not mandatory extra delivery phases. |
| R02 | [Angular push notifications](https://angular.dev/ecosystem/service-workers/push-notifications) | Angular's worker displays notification payloads and supports click operations through notification data. Default click handling alone does not establish a closed-app navigation flow. Explicitly test the selected click operation. |
| R03 | [Angular service-worker configuration](https://angular.dev/ecosystem/service-workers/config) | Cache configuration distinguishes asset and data groups. Use a public app-shell allowlist; exclude authenticated API/files rather than adding broad data caching. |
| R04 | [Angular worker operations](https://angular.dev/ecosystem/service-workers/devops), [SwUpdate](https://angular.dev/api/service-worker/SwUpdate) | Worker versions require update handling. Emergency removal can use an actual `ngsw.json` 404 or the documented safety worker at the old script URL. SPA fallback must not disguise the 404 as HTML 200. Rehearse with stale clients. |
| R05 | [WebKit: Web Push on iOS/iPadOS](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/) | Apple's baseline is iOS/iPadOS 16.4+ Home Screen web apps and permission requested following user interaction. Web Push uses standards and does not require Apple Developer Program membership. This is a baseline capability, not proof for every device/setting. |
| R06 | [MDN Push API](https://developer.mozilla.org/en-US/docs/Web/API/Push_API), [subscription options](https://developer.mozilla.org/en-US/docs/Web/API/PushManager/subscribe) | Push subscriptions use service workers and expose sensitive endpoint material. Subscription application-server keys bind VAPID signing identity. Treat endpoint/key records as secrets, retain key associations during rotation and keep ownership server-controlled. Browser support must be tested. |
| R07 | [SignalR authentication](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0), [groups](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-8.0), [background services](https://learn.microsoft.com/en-us/aspnet/core/signalr/background-services?view=aspnetcore-10.0) | Hubs and `IHubContext` support authenticated delivery from background services. Groups are routing, not authorization; reconnect restores group membership explicitly. Token expiry/revocation requires deliberate handling. Do not copy newer-runtime authentication-refresh APIs into .NET 8. |
| R08 | [RFC 8030](https://www.rfc-editor.org/rfc/rfc8030.html), sections 5.2 and 7.3 | Push has expiry/TTL semantics. Expired push subscriptions return 404 under the standard. Status interpretation depends on the request/resource and provider; a successful submission is not an application read receipt. |
| R09 | [Web Push C# library](https://github.com/web-push-libs/web-push-csharp) | A C# standard Web Push/VAPID adapter is feasible. This repository is a candidate, not a completed package/security/compatibility review; pin and validate the selected dependency in N2. |
| R10 | [Angular release policy](https://angular.dev/reference/releases) | The current support table excludes Angular 17; it lists versions 2-19 as out of support. A version-matched Angular 17 PWA build can be a staging experiment, not a claim of supported production software. |
| R11 | [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | .NET 8 support ends 2026-11-10. .NET 10 LTS is a candidate upgrade target; compatibility and migration effort have not been validated here. |
| R12 | [FCM server environments](https://firebase.google.com/docs/cloud-messaging/server-environment), [delivery reporting](https://firebase.google.com/docs/cloud-messaging/understand-delivery) | Future native push can originate in a trusted existing server. Provider acceptance and device delivery are different observations. These sources do not require migrating the application host/database to Google. |
| R13 | [IIS advanced hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/advanced?view=aspnetcore-8.0) | IIS startup/idle behavior needs explicit configuration. This does not establish that a particular shared-hosting plan grants those controls or durable background uptime. |

## VPS selection follow-up - 2026-10-17

The owner selects a VPS approach and prefers Hostinger; KVM 4 is the proposed all-in-one starting size, with KVM 2 as a budget/light-load alternative. No VPS benchmark, Linux compatibility run, purchase or provisioning was performed. The comparison establishes deployment control and documented inclusions, not a claim that one provider is faster.

| ID | Primary source | Verified fact and implication |
|---|---|---|
| R14 | [Hostinger Egypt VPS plans](https://www.hostinger.com/eg/vps-hosting) | KVM 2 lists 2 vCPU / 8 GB / 100 GB NVMe; KVM 4 lists 4 vCPU / 16 GB / 200 GB NVMe. The page includes weekly backups and states term plans are prepaid. Live offers differ from the owner's 12-month screenshot; compare final checkout and renewal for the same term. |
| R15 | [Hostinger VPS operation](https://www.hostinger.com/support/8852150-what-is-a-self-managed-vps-at-hostinger/), [OS capabilities](https://www.hostinger.com/support/which-server-capabilities-are-supported-at-hostinger/) | Customers manage server software; Hostinger maintains infrastructure. VPS templates are Linux, with no Windows VPS offer. This permits configuring supervised workers but does not provide automatic application maintenance or establish Linux compatibility of this repository. |
| R16 | [Hostinger locations](https://www.hostinger.com/support/1583267-where-are-hostinger-servers-located/) | Documented VPS candidates include France, Germany and India. Choose based on availability and measured customer-network latency. Changing the initialized region requires backup/reinstall; no universal Gulf-latency winner is established. |
| R17 | [OVH VPS](https://www.ovhcloud.com/en/vps/) | OVH advertises daily backup and included anti-DDoS. Hostinger's default weekly backup is a concrete comparison point; raw vCPU numbers and marketing claims do not establish a throughput or support-quality ranking. |
| R18 | [SQL Server 2022 Linux editions](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-editions-and-components-2022?view=sql-server-ver16), [ASP.NET Core Linux hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-8.0) | Linux deployment is supported at the framework/database level. Production SQL edition and licensing still require selection: Developer is non-production; Express has version-specific capacity/feature limits. Validate deployment and application-specific dependencies on the selected OS before rollout. |

## Decisions from the evidence

1. **Fix correctness first.** PWA cannot repair pagination, counts or missing producer integration. N1 owns these prerequisites.
2. **Use standard Web Push + VAPID for the browser.** Keep one Angular root worker. Do not add a competing Firebase root worker. FCM/APNs remain future native adapters.
3. **Store truth before dispatch.** Reuse the business outbox and add durable, channel-specific intents at visibility transitions. Retry a provider job independently from the business operation that created the notice.
4. **Do not use a maximum message ID as a release cursor.** Queued older messages and concurrent commits invalidate that assumption. N1 must prove the scoped change journal design in [data-model.md](data-model.md) using SQL Server.
5. **Use a supervised deployment with verified capabilities.** Provider name and geographic proximity alone cannot prove worker uptime or Gulf performance. Test the actual plan and region.

## Remaining verification boundaries

The later [Linux preflight](verification/linux-preflight/README.md) supersedes the original planning-only boundary for local Linux publication, 52 focused tests, synthetic SQL migration/HTTP probes and sample PDF rendering. It also records confirmed release blockers. It does not establish hosting-account, production, real-device push or customer migration readiness. The bullets below describe the original documentation-only planning scope where applicable.

- No provider credentials, live subscriptions, Apple/Android devices, production DNS or hosting control panel were tested.
- No package installation, framework migration, migration rehearsal or runtime benchmark was performed for this documentation change.
- Actual household OS/browser distribution, school count, peak fan-out and hosting budget are not supplied.
- Provider-specific endpoint hosts, error mapping, rate limits and VAPID rotation behavior must be verified against the selected adapter during N2.
- Push is a convenience channel. Urgent school procedures requiring guaranteed acknowledgement need a separately agreed escalation policy; SMS/WhatsApp/native apps are not implied solutions in this scope.
