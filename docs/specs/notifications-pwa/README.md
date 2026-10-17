# Reliable Messaging, Realtime Notifications and PWA

**Created / researched:** 2026-10-17  
**Status:** N1 implemented and locally verified; N2/N3 not started. Production deployment is gated separately.  
**Delivery:** Exactly three implementation phases.  
**Authority:** [Project constitution](../../../.spec/constitution.md), existing school-scoping and business policies.

## Intended outcome

Parents and staff receive current messages while using the website and can opt into device notifications on supported browsers without waiting for a native mobile application. The server retains an authoritative inbox even when realtime or push delivery fails. A later native application consumes the same business workflows through a separate delivery adapter.

This package plans changes to the existing Angular / ASP.NET Core / SQL Server modular monolith. It does not implement PWA, fix the discovered messaging defect, deploy a server, purchase hosting, register push credentials or send notifications.

## Read in this order

| Artifact | Purpose |
|---|---|
| [spec.md](spec.md) | User stories, requirements, acceptance scenarios and measurable outcomes |
| [research.md](research.md) | Repository findings, official sources, conclusions and verification limits |
| [plan.md](plan.md) | Architecture, constitution check, decisions and unresolved launch inputs |
| [data-model.md](data-model.md) | Proposed additive persistence and delivery state semantics |
| [contracts/api.md](contracts/api.md) | Proposed REST, realtime and push contracts; compatibility rules |
| [tasks.md](tasks.md) | Ordered, unchecked implementation tasks with requirement traceability |
| [quickstart.md](quickstart.md) | Staging verification, hosting checklist, rollout and rollback |
| [checklists/requirements.md](checklists/requirements.md) | Specification quality check and implementation evidence gates |
| [Linux VPS preflight](verification/linux-preflight/README.md) | Actual local Linux/SQL checks, publication blockers and limits of the hosting assessment |

## Three phases

| Phase | Deliverable | Status |
|---|---|---|
| N1 | [Reliable inbox and authenticated realtime](phases/01-reliable-inbox-and-realtime.md) | Implemented; local checks pass; [evidence](verification/n1/README.md) |
| N2 | [Installable PWA and opt-in Web Push](phases/02-pwa-and-web-push.md) | Not started |
| N3 | [Device verification, operations and controlled rollout](phases/03-verification-and-rollout.md) | Not started |

Research and design are Spec Kit planning activities, not extra delivery phases. Testing, security review and Arabic/RTL review belong inside each phase. N3 adds cross-device and operational evidence; it does not postpone N1/N2 testing.

## Spec Kit adaptation

The workflow follows constitution -> specification -> research/clarification -> technical plan/data model/contracts -> traceable tasks -> implementation -> evidence and consistency review. The package adapts [GitHub Spec Kit](https://github.com/github/spec-kit) templates to this repository's existing `docs/specs/` and `.spec/constitution.md`. N1 is implemented directly in the existing projects; no Spec Kit CLI invocation is claimed. See the phase evidence for actual checks.

At the end of each implementation phase, update the artifacts, [central tracker](../../phases/PHASE-NOTIFY-PWA.md), [project changelog](../../README.md) and [decision log](../../14-DECISIONS-AND-DEVIATIONS.md), then stop and report as required by the constitution. Do not start later-phase implementation early.

## Important constraints

- PWA installation, live website updates and background device notifications are separate capabilities.
- iPhone/iPad Web Push requires a supported OS/browser environment; Apple's baseline is iOS/iPadOS 16.4+, a Home Screen web app and user-initiated permission. Validate actual target devices.
- Permission, connectivity, Focus settings, browser/OS policy and expired subscriptions can prevent or delay notifications. Neither web nor future native push guarantees immediate delivery.
- Keep message release timing, approval, recipient eligibility and school isolation unchanged.
- Angular 17 is outside current official support; .NET 8 support ends 2026-11-10. Resolve the version decision before production rollout; do not install the latest PWA package into Angular 17 blindly.

Sources and the distinction between verified facts, proposed defaults and remaining tests are in [research.md](research.md) and [plan.md](plan.md).
