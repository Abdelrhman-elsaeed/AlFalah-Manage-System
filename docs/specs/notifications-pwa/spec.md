# Feature Specification: Reliable Notifications and PWA

**Date:** 2026-10-17  
**Status:** N1 implemented and locally verified; N2/N3 remain unstarted. See [N1 evidence and deployment limits](verification/n1/README.md).  
**Input:** School messaging must be useful without repeatedly opening the website; prepare for a possible future mobile application; use three phases and researched Spec Kit artifacts.

## Scope and non-goals

Deliver a correct server inbox, live updates in an open website, opt-in Web Push, installation guidance and operational controls. Reuse the existing messages, notification records, approval workflow and durable automation outbox. Include Student Affairs, School Duty notices and other explicitly classified existing notification writers.

Native Android/iOS builds, store publication, SMS, WhatsApp, email fallback, full offline school operations, a database replacement and a microservices migration are outside these three phases. No notification is evidence that a guardian has read or acknowledged a school instruction.

## User stories and independent acceptance

### US1 - Current, reliable inbox (priority P1; phase N1)

As a parent or staff member, I can see the latest permitted messages and accurate unread counts, load older history and recover after losing connectivity.

Independent test: seed a conversation with more than 50 messages and another account with more than 100 unread conversations; verify current content, complete history and server counts through REST with realtime disabled.

1. **Given** 60 visible messages, **when** the conversation opens, **then** the initial page contains the latest 50 in display order; loading older history reveals the remaining ten once each.
2. **Given** a message queued outside teacher office hours, **when** the existing policy releases it, **then** it becomes discoverable even if its message ID is below IDs already seen by the client.
3. **Given** duplicate send requests with the same idempotency key and payload, **then** one business message is stored; a changed payload retains the existing conflict behavior.
4. **Given** unread conversations beyond the old first-page cap, **then** the badge equals a school/user-scoped server aggregate.

### US2 - Live website updates (priority P1; phase N1)

As a signed-in user, I receive current inbox and notification state without navigating or waiting for a 25/30-second timer.

Independent test: two authenticated sessions exchange messages; disconnect one, release additional messages, reconnect and compare its state to REST.

1. **When** a business transaction rolls back, **then** no realtime change referring to that uncommitted fact is published.
2. **When** a realtime hint is duplicated or lost, **then** resynchronization recovers the authoritative inbox without duplicating messages or marking them read.
3. **When** a user signs out, changes school, loses school membership or reaches token expiry, **then** the old connection stops receiving that context's updates; reconnect requires current authorization.
4. **When** realtime is unavailable, **then** bounded foreground refresh remains available with an honest connection status.

### US3 - Notifications outside the website (priority P1; phase N2)

As a parent or staff member, I can deliberately enable notifications on a supported device and open the correct authorized item from a notification.

Independent test: on a real supported phone, install where required, opt in, background the app, trigger a permitted synthetic school event and follow the notification to the current inbox.

1. **Given** permission is undecided, **then** explain the benefit before a user gesture triggers the browser prompt; opening a page alone never requests permission.
2. **Given** permission is denied or support is unavailable, **then** keep the web inbox usable and show actionable instructions without repeated prompts.
3. **Given** a pending approval or an unreleased message, **then** no external push is attempted.
4. **Given** logout, account change, school change, subscription rotation or endpoint invalidation, **then** old queued jobs cannot intentionally target the former binding.
5. **Given** a locked phone or an expired login, **then** the notification reveals generic text only, and opening it requires current authentication and school authorization.

### US4 - Operable delivery and future mobile compatibility (priority P2; phase N3)

As the operator, I can see failures, recover a stopped worker and disable a broken delivery channel without losing the school inbox. A future native client can reuse the same notification identity and read APIs.

Independent test: restart the worker during delivery, simulate provider failures, restore a staging backup and exercise the rollout/rollback runbook with synthetic accounts.

1. A provider timeout or crash may cause another delivery attempt, but never repeats the underlying attendance, approval or messaging business operation.
2. Expired, cancelled, suppressed and no-longer-authorized notices are not submitted on retry; already accepted external notifications cannot be recalled reliably.
3. Operators can distinguish pending business release, in-app visibility, provider acceptance, failed attempts and explicit user read state.
4. Native adapter contracts are documented; a native app, FCM/APNs credentials and device acknowledgements are not implemented in this scope.

## Functional requirements

| ID | Requirement | Phase |
|---|---|---|
| FR-001 | Retrieve latest messages correctly; use stable bounded history pagination and recover missed visibility changes, including late release of older IDs. | N1 |
| FR-002 | Compute authoritative unread counts on the server without a first-100-conversations cap. Preserve explicit, scoped read actions. | N1 |
| FR-003 | Publish committed changes through authenticated realtime; REST remains the source of truth; reconnect/focus/online resynchronize. | N1 |
| FR-004 | Enforce school, user, participant, active relationship, permission and session validity at all entry points and before outbound delivery. | N1-N3 |
| FR-005 | Preserve office-hour release, approval, suppression, guardian eligibility and System conversation policies; integrate all classified producer paths without duplicate alerts. | N1-N2 |
| FR-006 | Persist delivery intent atomically with visibility; isolate channel retries from business outbox processing; use idempotent claims and bounded retries. | N1-N2 |
| FR-007 | Provide installable app metadata and one compatible root service worker with Arabic/RTL installation and permission guidance. | N2 |
| FR-008 | Provide opt-in Web Push and subscription lifecycle controls; support denial/unavailable states, multiple devices and safe account/school changes. | N2 |
| FR-009 | Minimize lock-screen payloads; authenticate deep links; prevent private API, message, student and file data from entering service-worker caches. | N1-N3 |
| FR-010 | Separate provider acceptance, in-app availability and explicit read semantics; use expiry, retry classification and safe dead-letter replay. | N1-N3 |
| FR-011 | Provide safe application updates, independent channel switches, stale-client handling and a tested worker/cache rollback. | N2-N3 |
| FR-012 | Validate browser/OS/device behavior, SQL concurrency, restart recovery, Gulf network latency and deployment prerequisites before broad rollout. | N1-N3 |
| FR-013 | Keep provider interfaces extensible for future native FCM/APNs without coupling business rules or requiring Google-hosted application servers. | N3 |
| FR-014 | Pin a compatible supported deployment stack, or record an owner-accepted, time-bounded version exception and remediation date before production. | N1/N3 |

## Measurable success criteria

These are proposed acceptance targets for a declared staging workload, not measured results or external-provider SLAs.

| ID | Criterion |
|---|---|
| SC-001 | The 60-message regression and late-release scenario pass; paginating history recovers every authorized seeded message once. |
| SC-002 | Counts match database truth with at least 120 unread conversations and remain correct after read/read-all and reconnect. |
| SC-003 | On a healthy worker and connected client, commit-to-visible-update p95 <= 5 seconds under the agreed workload; measure worker wait, server processing and network separately. |
| SC-004 | No missing durable intents and no duplicate business effects in SQL Server concurrent-claim, lease-expiry, timeout and crash-recovery tests. External duplicate banners remain possible. |
| SC-005 | No cross-school/user content in negative authorization, forged cursor, subscription ownership, logout/account-switch and cache-inspection tests. |
| SC-006 | Every supported device-matrix row passes actual permission, background delivery, click and reconnect checks. Failed/unsupported rows have an explicit fallback and support statement. |
| SC-007 | At the agreed workload, eligible-intent-to-provider-acceptance p95 <= 10 seconds with a healthy provider. Report actual phone-arrival timing separately; no guaranteed arrival deadline. |
| SC-008 | Worker restart recovers eligible queued work; a rehearsed channel rollback preserves inbox/read state; an isolated database restore is documented. |

## Edge cases that must be exercised

Concurrent sends; empty/history-only conversations; ties in visibility timestamps; a queued low-ID message released after a newer one; long disconnects exceeding journal retention; out-of-order commit; school change with an open tab; inactive guardian links; approval revoked during retry; one failed device among several; duplicate producer surfaces for one Duty notice; changed VAPID key; denied permission; reinstall/cleared browser storage; Focus/battery restrictions; app update with an unsent draft; deep link into a different active school; stale browser after rollback; first hub connection failure; cancelled coverage assignment before provider submission.

## Assumptions and decisions requiring confirmation

The default is standard Web Push with VAPID, one deployment initially, generic notification text and one active school binding per installation. These are design proposals. Final domain access, hosting capabilities, workload, support matrix, retention, expiry values and version exception/migration decisions remain recorded inputs in [plan.md](plan.md). They do not prevent creating this specification; they must be resolved at their stated implementation gates.
