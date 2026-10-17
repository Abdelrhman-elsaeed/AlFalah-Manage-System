# Notifications and PWA - Delivery Tracker

**Last updated:** 2026-10-17  
**Package:** [Reliable messaging, realtime notifications and PWA](../specs/notifications-pwa/README.md)  
**Current state:** English Spec Kit planning artifacts created and reviewed; implementation not started.

| Phase | Scope | Status | Evidence |
|---|---|---|---|
| N1 | [Reliable inbox and authenticated realtime](../specs/notifications-pwa/phases/01-reliable-inbox-and-realtime.md) | Implemented; locally verified | [43 backend/SQL + 2 Angular + 8 browser checks pass](../specs/notifications-pwa/verification/n1/README.md); local viewport p95 2,089/1,948 ms; deployment flags remain off |
| N2 | [Installable PWA and opt-in Web Push](../specs/notifications-pwa/phases/02-pwa-and-web-push.md) | Not started | No implementation/device evidence |
| N3 | [Operational verification and controlled rollout](../specs/notifications-pwa/phases/03-verification-and-rollout.md) | Not started | No hosting/rollout evidence |

Research, clarification and design are planning activities; they do not add a fourth implementation phase. Each phase includes meaningful tests, security review and Arabic/RTL verification. Stop and report after each completed implementation phase under the project constitution.

## Planning record

2026-10-17: Recorded current polling, the reproduced oldest-50 pagination defect, potentially capped unread aggregate, existing transactional outbox/worker and separate Duty notice producers. Checked official Spec Kit, Angular, WebKit, Microsoft, Web Push and Firebase sources. Proposed three phases with explicit requirements/tasks/contracts, protected per-device delivery, supported-stack decision gates and actual-device/hosting acceptance.

Only Markdown documentation changed. No runtime feature, migration, package, provider credential, notification traffic, domain configuration or deployment was changed. Earlier audit tests are baseline evidence only; no new runtime tests were executed for this planning task. All implementation tasks and operational checks remain unchecked.

Planning validation: 13 new Markdown files including this tracker; exactly three phase files; 29 unchecked ordered tasks; coverage for 14 requirements; 41 local relative links and three JSON examples validated. The tracked Markdown diff passes `git diff --check`. These are artifact checks, not runtime/device acceptance.

2026-10-17 hosting follow-up: Owner chooses VPS and prefers Hostinger. Recorded KVM 4 as the proposed all-in-one starting point and KVM 2 as the light-load budget alternative, with sizing unmeasured. Added Linux application/PDF compatibility, production SQL edition, supervised worker startup, region/latency, prepaid/renewal and independent backup gates. Public-source comparison only; no purchase, provisioning, runtime test or phase implementation. N1/N2/N3 remain not started.

Follow-up artifact validation passes: 44 relative links/anchors, exactly three phase files and 29 unchecked ordered tasks; JSON examples and tracked diff checks remain valid. No runtime checks ran.

2026-10-17 Linux preflight (C48/D-140): [Retained report and evidence](../specs/notifications-pwa/verification/linux-preflight/README.md). Actual isolated Linux Release publication passes; 52 focused tests pass; SQL Server 2022 Express applies 76 migrations twice safely with zero pending, 153 application tables and Arabic roundtrip. Production API HTTP smoke returns Swagger 200 and protected routes 401; seven synthetic Arabic PDFs generated and samples inspected. Current frontend production build fails on two CSS budgets. Historical Windows DPAPI keys, case-insensitive file containment on Linux, startup migration/seeding policy, trusted proxy handling, configuration/persistent storage and frontend packaging need disposition before release. No application source/config/schema was changed and no operational data/provider traffic was used. This is a preflight, not N1 implementation or a fourth delivery phase; all feature phases/tasks remain unstarted.
