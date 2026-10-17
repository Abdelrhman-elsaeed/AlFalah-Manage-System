# Implementation Tasks

**Status:** T001-T011 and N2 implementation tasks T012A-T012D/T013-T019 locally verified. T012 dependency/local review done with external inputs gated; T020 pending actual devices; T021 documentation reconciled with final acceptance pending T020. N3 T022-T029 not started. 33 tasks: 22 checked, 11 gated/pending. See [N2 evidence](verification/n2/README.md).
**Inputs:** [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts](contracts/api.md).  
**Notation:** `US` links to user stories; `FR` links to requirements. New source filenames are proposed implementation targets, not files that already exist.

Exactly three phases. Execute N1 -> N2 -> N3. Within each phase, respect task dependencies; no implicit parallel delegation. Tests below are required by this feature specification because visibility, authorization and durable delivery are material behavior changes. No separate testing or design phase is added.

## N1 - Reliable inbox and realtime

- [x] T001 [US1/US2] Reconfirmed producer/permission inventory and Q02/Q03/Q06 assessment in `plan.md`; existing complaint Pending behavior is preserved. FR-004, FR-005, FR-012, FR-014.
- [x] T002 [US1] Reproduced the 60-message old first-page failure, then covered latest/history equal-time order, sender pending and lower-ID late release in `InboxWindowTests`. FR-001.
- [x] T003 [US1] Added the window under Application `Notifications/InboxService`, Infrastructure `Notifications/InboxRepository`, and thin `InboxController` while retaining the existing route/controller behavior. Angular uses the new window and protected history cursor. Target-file refinement is recorded in C49/D-141. FR-001.
- [x] T004 [US1] Implemented current-permission nullable aggregates and removed the first-100 client count. Real SQL tests cover 121 conversations, hidden notifications and explicit read/read-all behavior. FR-002, FR-004.
- [x] T005 [US1/US2] Added four tables through `20261017125332_AddRecipientInboxRealtime`; real SQL proves migration sentinel preservation, empty historical journal, commit order and count/version interleaving consistency. FR-001, FR-003, FR-006.
- [x] T006 [US1/US2] Integrated classified producers through the shared SaveChanges persistence projection, leaving Application release/approval/Duty policy in place. Actual Duty dual-surface test passes; later surfaces re-arm the same realtime identity and fence stale completion. Seed/history projection is disabled. FR-004, FR-005, FR-006, FR-010.
- [x] T007 [US2] Implemented thin authenticated hub, Application connection/delivery orchestration, scoped publisher and bounded worker. Expiry, active-account revocation, origin and query-bearer boundaries have real protocol checks; no newer-runtime-only APIs. FR-003, FR-004, FR-006, FR-009.
- [x] T008 [US1/US2] Implemented one Angular coordinator for chat/sidebar/guardian notifications with initial retry, reconnect/focus/online, bounded draining, stale-context discard and draft retention. Scroll/history viewport verification is included in T010. FR-001, FR-002, FR-003, FR-004.
- [x] T009 [US1/US2] Real SQL tests prove commit order, rollback, uniqueness, claim races, timeout/retry, stale lease completion, actual deadlock/fresh-scope retry and crossed-count snapshot recovery. Existing office-hour/permission regressions and actual Duty producer checks pass. FR-004, FR-005, FR-006, FR-010, FR-012.
- [x] T010 [US1/US2] 43 backend/SQL, 2 Angular and 8 browser checks pass. Actual viewport/draft/history behavior is checked at desktop/mobile widths; declared local request-to-viewport p95 is 2,089/1,948 ms. Retained visual/result evidence distinguishes pre-existing production CSS and unrelated broad test failures. Detailed independent queue/network telemetry and actual customer/Gulf load remain N3; no VPS capacity claim. FR-003, FR-009, FR-012.
- [x] T011 [US1/US2] Reconciled package, central API/schema/security docs, tracker and C49/D-141 memory with actual N1 implementation/evidence and deployment gates. N2/N3 are not started; stop after reporting N1. FR-001-FR-006, FR-010, FR-014.

## N2 - PWA and Web Push

- [ ] T012 [US3] **Local dependency/adapter work complete; final origin/provider/device inputs pending.** Select/pin compatible Angular PWA and C# Web Push dependencies; verify adapter maintenance/license/runtime compatibility and actual provider endpoint/error semantics. Resolve N2-gated Q01/Q04/Q05/Q07/Q08; record in `research.md`/`plan.md`. Depends accepted N1 exit. FR-007, FR-008, FR-010, FR-014.
- [x] T012A [US5] Inventory every required NC-01-NC-18 transition in `notification-catalog.md`: source/event, current recipient/resource policy, target, canonical identity, missing producer and evidence. Resolve CP-01-CP-04 for affected producers; preserve hard complaint denials unless explicitly changed. Record concrete center schema/count/read/API and source lifecycle choices in `data-model.md`/`contracts/api.md`. Optional NS suggestions remain unselected. Depends accepted N1 baseline; precedes T012B and source-aware push. FR-004, FR-005, FR-015, FR-017, FR-018.
  Owner clarification for T012A/T012B/T012D: NC-15 includes student classroom/date completion (including all-present) and separate complete staff school/date attendance, not one whole-school student event. Retain existing full-roster staff validation and reject partial saves without notifying. NC-14 triggers on committed signed Duty report submission to manager/entitled administrators; prove drafts, letter signatures, last-student departure, archive failure and replay do not create false/duplicate submission notices. Define correction identities without repeated completion banners.
- [x] T012B [US5] Implement the approved recipient center projection and missing mandatory producers through Application services and Infrastructure persistence/outbox. Preserve N1 compatibility, atomically invalidate center/count/read changes, canonicalize Duty/System/dashboard surfaces and apply current recipient/assignment/approval policies. No false completion or historical push backfill; unresolved CP definitions block their affected producer. Depends T012A. FR-002, FR-004-FR-006, FR-010, FR-015, FR-018.
- [x] T012C [US5] Implement shared top-bar bell/panel and bounded full history with unread/category filters, explicit read/read-all, independent center count and Arabic/RTL keyboard/mobile UX. Add authenticated target resolution and necessary exact-item selection in existing destinations; maintain pending/resolved state independently of read and preserve original decision commands. Integrate with the one N1 coordinator. Depends T012B. FR-004, FR-009, FR-015-FR-018.
- [x] T012D [US5] Verify transition-level required-category matrix, excluded/currently revoked recipients, completion partial-save/replay, complaint response/privacy, dashboard dedup, counts/history/reconnect and correct target selection. Retain source -> recipient -> center -> action evidence for NC-01-NC-18; no family-only coverage claims. Depends T012B/T012C; required before N2 exit. FR-004, FR-005, FR-012, FR-015-FR-018.
- [x] T013 [US3] Add public app manifest/icons, version-matched worker registration and `frontend/ngsw-config.json`; update `frontend/angular.json`, `frontend/src/index.html` and current app bootstrap. Cache public app shell only; no competing root worker. Depends T012. FR-007, FR-009, FR-011.
- [x] T014 [US3] Add protected subscription/per-device target persistence, migrations and repositories; implement endpoint validation/SSRF controls, unique binding and revocation/cancellation. Confirm data-protection key persistence across restart. Depends T012 and N1 intents. FR-004, FR-008, FR-009.
- [x] T015 [US3] Implement thin proposed `backend/AlFalah.Api/Controllers/PushSubscriptionsController.cs` and Application subscription service with current-user binding, idempotency, device listing and owned deletion. Test forged installation IDs and simultaneous account binding. Depends T014. FR-004, FR-008, FR-009.
- [x] T016 [US3/US5] Implement proposed `backend/AlFalah.Infrastructure/Notifications/WebPushDeliveryClient.cs` plus per-device fan-out/attempt orchestration over approved canonical center/source identities. Enforce live eligibility, per-category TTL, timeout/lease policy, retry classification, no-active-device outcome and generic payload. Read/count/invalidation changes never become phone alerts. Verify no historical backlog. Depends T012B/T014/T015. FR-005, FR-006, FR-008, FR-009, FR-010, FR-015, FR-018.
- [x] T017 [US3] Implement contextual Arabic/RTL opt-in and installation guidance, unsupported/denied states, device management and login/resume/logout/school-switch reconciliation through proposed `frontend/src/app/core/services/web-push.service.ts` and shared notification controls. Do not auto-prompt. Depends T013/T015. FR-007, FR-008.
- [x] T018 [US3/US5] Reuse T012C's authenticated allowlisted target resolver for notification-entry navigation and update UX in Angular routing/core services. Handle closed-tab clicks, login, explicit school switch, stale/deleted/resolved targets, unsent drafts and unrecoverable worker versions. No action/approval on a click. Depends T012C/T013/T016/T017. FR-004, FR-009, FR-011, FR-017.
- [x] T019 [US3] Add effective independent flags for journal production, realtime dispatch, push registration, push dispatch and installation/update UI. Verify disabling push does not strand or unexpectedly replay stale targets on re-enable. Document worker removal procedure in `quickstart.md`. Depends T013-T018. FR-010, FR-011.
- [ ] T020 [US3] **Not run: no actual staging phones/provider credentials.** Verify HTTPS staging on actual iPhone/iPad Home Screen and Android Chrome plus target desktop browsers; exercise foreground/background, permission denial, revoked endpoints, multiple devices, offline logout window and account change. Inspect caches/logs and correct RTL layouts. Depends T016-T019. FR-004, FR-007, FR-008, FR-009, FR-012.
- [ ] T021 [US3/US5] **Docs/central memory reconciled; final phase acceptance depends on T020.** Reconcile actual payload schema, provider mapping, source eligibility, catalog coverage, center/target/migration/API/security docs and evidence. Complete N2 exit checklist, update central records, stop and report. Depends T012D/T020. FR-005-FR-011, FR-014-FR-018.

## N3 - Operational verification and rollout readiness

- [ ] T022 [US4] Verify the owner-selected VPS direction (Hostinger preferred), actual plan/region, supported Linux/runtime/SQL matrix, Linux PDF/export/application compatibility, final origin, DNS permissions, TLS, WebSocket proxying, supervised startup/restart, production SQL edition/license and independent backup/restore. Resolve compatibility prerequisites before deploying N1/N2 to the VPS; N3 validates the final operational setup. Complete Q01/Q02/Q03/Q06/Q09 in `plan.md` and `quickstart.md`. Depends accepted N2 exit for final acceptance. FR-012, FR-014.
- [ ] T023 [US4] Implement protected operational summaries/actions using existing administration permission conventions: heartbeat, oldest eligible age, provider acceptance latency, retries, invalid subscriptions, dead letters and audited safe replay. Finalize exact routes/contracts before code. Depends T022. FR-004, FR-010, FR-012.
- [ ] T024 [US4] Run failure/recovery drills on isolated SQL/HTTPS staging: stop/restart, crash after acceptance, lease overrun, unavailable provider, rate limits, expired/cancelled assignment and multi-device partial failure. Ensure no duplicate business effect or lost intent. Depends T023. FR-005, FR-006, FR-010, FR-012.
- [ ] T025 [US4/US5] Load-test declared concurrency/fan-out, bounded database queries, center counts/history and dispatcher throughput across mandatory event categories; measure Gulf client latency and SC-003/SC-007 separately from device arrival. Rehearse database restore and secret/key recovery. Depends T022-T024. FR-006, FR-012, FR-015, FR-018.
- [ ] T026 [US3/US4/US5] Finish actual device/OS and catalog recipient/target matrices, old/new client compatibility, bell/history Arabic/RTL/accessibility review and SW update/rollback with a stale installed client. Retest negative authorization, resolved tasks/delegation revocation and cache isolation after account switches. Depends T024/T025. FR-004, FR-007-FR-009, FR-011, FR-012, FR-015-FR-018.
- [ ] T027 [US4] Validate a provider-neutral Application delivery interface and versioned target/read contract for a future native client. Document FCM/APNs registration/token lifecycle as future work only; no native SDK, build, credential or store enrollment. Depends T023. FR-013.
- [ ] T028 [US4] Prepare operator/school onboarding and a pilot plan: synthetic accounts first, explicitly selected school/users next, observable rollback triggers and named incident owner. Execute external rollout only under actual deployment authorization. Depends T022-T027. FR-008, FR-010-FR-014.
- [ ] T029 [US1-US5] Review requirement -> task -> evidence consistency, including each required event/recipient/target and SC-009/SC-010 at the final workload. Record measured outcomes and unresolved exceptions, update central memory and stop with a self-contained handoff. Do not call the feature complete if required checks remain pending. Depends T028. FR-001-FR-018.

## Requirement coverage

| Requirement | Main implementation / acceptance tasks |
|---|---|
| FR-001 | T002, T003, T005, T008, T009 |
| FR-002 | T004, T008, T010 |
| FR-003 | T005, T007, T008, T010 |
| FR-004 | T001, T004, T006, T007, T009, T014, T015, T018, T020, T023, T026 |
| FR-005 | T001, T006, T009, T016, T024 |
| FR-006 | T005-T007, T009, T016, T024, T025 |
| FR-007 | T012, T013, T017, T020, T026 |
| FR-008 | T012, T014-T017, T020, T028 |
| FR-009 | T007, T013-T016, T018, T020, T026 |
| FR-010 | T006, T009, T012, T016, T019, T023, T024 |
| FR-011 | T013, T018, T019, T026 |
| FR-012 | T001, T009, T010, T020, T022-T026 |
| FR-013 | T027, T028 |
| FR-014 | T001, T012, T022, T028 |
| FR-015 | T012A-T012D, T016, T021, T026, T029 |
| FR-016 | T012C, T012D, T021, T026, T029 |
| FR-017 | T012A, T012C, T012D, T018, T026, T029 |
| FR-018 | T012A-T012D, T016, T021, T024-T026, T029 |

US1/US2 can be accepted with Web Push disabled. US5 center/coverage remains usable without push opt-in; it is now part of N2 acceptance. US3 requires N1's reliable visibility/delivery seams and N2's source-aware center. US4 assumes working N1/N2 and proves operational behavior. Neither N3 nor future native work is a hidden fourth implementation phase.
