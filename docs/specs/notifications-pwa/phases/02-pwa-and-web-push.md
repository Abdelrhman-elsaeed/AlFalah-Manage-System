# N2 - Personal Notification Center, PWA and Opt-in Web Push

**Status:** Implemented and locally verified; T020 actual-device acceptance and final T021 exit remain pending. N3 not started. See [local results / gates](../verification/n2/README.md).
**Stories:** US3/US5. **Tasks:** T012-T021 plus T012A-T012D in [tasks.md](../tasks.md).
**Dependency:** Accepted N1 exit and compatible dependency decision.

## Outcome

Supported users can install the website where appropriate, opt into browser notifications and reach the current authorized message/notice from a device notification. Permission denial or unsupported devices retain a usable inbox.

Owner scope extension: first complete the mandatory event inventory and missing approved producers, unified top-bar bell/history and exact-item targets in [notification-catalog.md](../notification-catalog.md) and [notification-center.md](../notification-center.md). These are additional N2 deliverables; N1 generic realtime evidence does not establish their completion.

## Included work

- T012A-T012D: transition-level required coverage, current recipient policies, durable canonical center/read/count state, shared bell/history and target selection with category/unread filters and pending/resolved status.
- Resolve complaint administrator access and remaining attendance/report technical mappings before affected producers; preserve original decision permissions. Owner confirms student alerts per completed classroom/date, separate staff attendance alerts, and Duty completion alerts at committed signing/submission of the report to manager/entitled administrators. Optional reminders/NS suggestions remain proposals.
- Version-matched Angular PWA, stable manifest identity and one root service worker.
- Public app-shell allowlist and honest offline state; no private API/file caching.
- Contextual Arabic/RTL installation and permission UX, including iOS Home Screen guidance.
- Standard Web Push/VAPID adapter, protected subscriptions and independent per-device delivery targets.
- Ownership, endpoint validation, revocation/rotation, TTL, retries and channel switches.
- Generic notification payload, authenticated deep link, safe app updates and draft protection.

This phase does not require Firebase browser SDKs, Apple Developer Program enrollment, native builds or offline message submission. Preserve business office hours/approval and do not push old seeded/history records.

## Required checks

- Every mandatory NC row: entitled/excluded recipients, bell/history/count, correct destination, current task state, reassignment/revocation and source deduplication. No alerts produced by dashboard reads.
- Per-classroom student completion (including all-present) versus partial staff batches; separate staff school/date scope; signed-report submission versus drafts/letter signature/last-student departure, archive failure and replay. Complaint creation/reply visibility and existing role hard denials; manager approval task distinct from pending guardian dispatch.

- Real HTTPS staging iPhone/iPad Home Screen and Android Chrome delivery with app backgrounded/no open tab; record OS/browser and settings.
- User-gesture permission; denied/unsupported/install-required states; no automatic or repeated prompts.
- Multiple devices, reinstall, stale subscription, revoked endpoint, key change and no-active-device outcome.
- Correct provider response mapping: acceptance, invalid subscription, signing/configuration failure, throttling, timeout and invalid payload.
- Account/school changes and owned-device deletion; verify queued target cancellation and document the offline/in-flight revocation window.
- Endpoint SSRF/ownership/rate-limit negative tests; inspect logs, bundles and caches for secrets/private data.
- Closed-client click, expired login, wrong active school, unauthorized/deleted target and unsent draft during an update.
- Arabic/RTL review at mobile/desktop widths, keyboard access and clear enable/disable/status controls.

## Exit evidence

- [ ] US3 and N2 portions of SC-004/SC-005/SC-006 pass with device evidence.
- [x] US5/FR-015-FR-018 and SC-009/SC-010 pass with transition-level catalog evidence; CP-01-CP-04 are resolved for all required affected producers, or an explicit owner scope revision records the exclusion. Optional suggestions are not silently implemented.
- [x] Per-event expiry, target support matrix, active-school binding and generic content defaults are reviewed and recorded.
- [ ] Q01/Q04/Q05/Q07/Q08 N2 inputs are resolved for staging; production-only gaps remain explicitly gated.
- [ ] No competing root worker; cache policy and notification click work in the selected Angular version.
- [x] Channel-disable and service-worker removal procedures are concrete and ready for the N3 stale-client rehearsal.
- [x] Source/API/schema/security documentation and central records match actual implementation.

Stop and report after the phase exit. A desktop DevTools push simulation alone does not satisfy the actual-phone requirement.

## Local implementation result

T012A–T012D and T013–T019 are implemented locally: durable personal center, transition policy/missing approved producers, exact authenticated targets, bell/history, matched Angular worker, protected subscriptions and source-aware per-device delivery. See [verification report](../verification/n2/README.md), [concrete catalog](../notification-catalog.md#n2-concrete-transition-inventory--2026-10-17) and [implementation decisions](../n2-implementation.md). There is no operational database migration or provider traffic. The report distinguishes InMemory, actual SQL, intercepted API UI, local worker and intercepted provider HTTP.

Exit review: local catalog/center/source documentation and cache/ownership checks are complete. Compatible pins/expiry/generic-content/single-binding staging defaults are recorded, but final origin/VAPID/provider configuration and actual iOS/Android results remain unresolved. Consequently the device-dependent exit boxes above remain unchecked; N2 is implemented but not fully accepted. T021 local reconciliation is complete, with final exit contingent on T020. N3 has not started.

## Android-only trial follow-up — 2026-10-17

Owner has no current HTTPS deployment and only an Android device. [Isolated trial preparation](../verification/n2/android-device-trial.md) supplies an operator launcher, locally generated private VAPID identity and synthetic proxy checks. Automatic review rejected trial API startup (`blocked by policy`); full launcher, HTTPS origin, provider traffic and phones remain unverified. Trial DB has not been created and trial push flags stay off. T020 and final T021 stay unchecked; iPhone/stable-origin/release gates remain pending. This is N2 preparation only; N3 is not started.

Windows PowerShell retry correction: the first operator invocation failed before SQL/API startup because dictionary-adapted `ConnectionString` assignment was interpreted as an SQL keyword. Constructor parsing fixes the reproduced exception; eight actual preflight regression cases, actual private-config validation without SQL/process/network access, and synthetic proxy checks pass. Full HTTPS/device acceptance remains pending; T020/T021 unchanged. [Report](../verification/n2/android-device-trial.md#windows-powershell-startup-fix--2026-10-17).

SQL retry correction: the owner's launcher reached completed isolated E2E initialization (79 migrations, 15 synthetic users), then failed because it assumed AspNetUsers instead of the mapped dbo.Users. Corrected the SQL and proved the exact restriction query against actual isolated SQL with rollback/all-account-state preservation. Preflight/proxy regressions pass. This supersedes initial trial-database-not-created status only; full HTTPS/provider/device acceptance remains pending, and N3 unstarted. [Report](../verification/n2/android-device-trial.md#sql-account-table-startup-fix--2026-10-17).

Actual Android receipt/click follow-up: temporary HTTPS is running, with independent guardian/staff bindings and an actual provider-accepted new guardian message. Owner explicitly confirms outside-app Android notification and click opening the correct conversation after reopening/login. This supersedes the earlier failed enrollment/no-provider snapshot only for this synthetic basic test. Initial root cause is not proven; desktop activation is not a prerequisite. Frontend opt-in messages now distinguish browser/permission/registration failures and protect account changes; 10 Angular checks and separate optimized build pass (not deployed into the active phone session). Private logging restored. [Evidence/limits](../verification/n2/android-trial-diagnostics.json). Full T020/T021 remain unchecked for remaining lifecycle/context/iPhone/stable-origin gates; N3 unstarted.
