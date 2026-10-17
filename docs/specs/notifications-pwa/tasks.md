# Implementation Tasks

**Status:** T001-T011 implemented and locally verified; T012-T029/N2/N3 remain unstarted. See [evidence and production exceptions](verification/n1/README.md).  
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

- [ ] T012 [US3] Select/pin compatible Angular PWA and C# Web Push dependencies; verify adapter maintenance/license/runtime compatibility and actual provider endpoint/error semantics. Resolve N2-gated Q01/Q04/Q05/Q07/Q08; record in `research.md`/`plan.md`. Depends accepted N1 exit. FR-007, FR-008, FR-010, FR-014.
- [ ] T013 [US3] Add public app manifest/icons, version-matched worker registration and `frontend/ngsw-config.json`; update `frontend/angular.json`, `frontend/src/index.html` and current app bootstrap. Cache public app shell only; no competing root worker. Depends T012. FR-007, FR-009, FR-011.
- [ ] T014 [US3] Add protected subscription/per-device target persistence, migrations and repositories; implement endpoint validation/SSRF controls, unique binding and revocation/cancellation. Confirm data-protection key persistence across restart. Depends T012 and N1 intents. FR-004, FR-008, FR-009.
- [ ] T015 [US3] Implement thin proposed `backend/AlFalah.Api/Controllers/PushSubscriptionsController.cs` and Application subscription service with current-user binding, idempotency, device listing and owned deletion. Test forged installation IDs and simultaneous account binding. Depends T014. FR-004, FR-008, FR-009.
- [ ] T016 [US3] Implement proposed `backend/AlFalah.Infrastructure/Notifications/WebPushDeliveryClient.cs` plus per-device fan-out/attempt orchestration. Enforce live eligibility, TTL, timeout/lease policy, retry classification, no-active-device outcome and generic payload. Verify no historical backlog. Depends T014/T015. FR-005, FR-006, FR-008, FR-009, FR-010.
- [ ] T017 [US3] Implement contextual Arabic/RTL opt-in and installation guidance, unsupported/denied states, device management and login/resume/logout/school-switch reconciliation through proposed `frontend/src/app/core/services/web-push.service.ts` and shared notification controls. Do not auto-prompt. Depends T013/T015. FR-007, FR-008.
- [ ] T018 [US3] Implement authenticated allowlisted notification-entry navigation and update UX in Angular routing/core services. Handle closed-tab clicks, login, explicit school switch, stale/deleted targets, unsent drafts and unrecoverable worker versions. Depends T013/T016/T017. FR-004, FR-009, FR-011.
- [ ] T019 [US3] Add effective independent flags for journal production, realtime dispatch, push registration, push dispatch and installation/update UI. Verify disabling push does not strand or unexpectedly replay stale targets on re-enable. Document worker removal procedure in `quickstart.md`. Depends T013-T018. FR-010, FR-011.
- [ ] T020 [US3] Verify HTTPS staging on actual iPhone/iPad Home Screen and Android Chrome plus target desktop browsers; exercise foreground/background, permission denial, revoked endpoints, multiple devices, offline logout window and account change. Inspect caches/logs and correct RTL layouts. Depends T016-T019. FR-004, FR-007, FR-008, FR-009, FR-012.
- [ ] T021 [US3] Reconcile actual payload schema, provider mapping, source eligibility, migration/API/security docs and evidence. Complete N2 exit checklist, update central records, stop and report. Depends T020. FR-005-FR-011, FR-014.

## N3 - Operational verification and rollout readiness

- [ ] T022 [US4] Verify the owner-selected VPS direction (Hostinger preferred), actual plan/region, supported Linux/runtime/SQL matrix, Linux PDF/export/application compatibility, final origin, DNS permissions, TLS, WebSocket proxying, supervised startup/restart, production SQL edition/license and independent backup/restore. Resolve compatibility prerequisites before deploying N1/N2 to the VPS; N3 validates the final operational setup. Complete Q01/Q02/Q03/Q06/Q09 in `plan.md` and `quickstart.md`. Depends accepted N2 exit for final acceptance. FR-012, FR-014.
- [ ] T023 [US4] Implement protected operational summaries/actions using existing administration permission conventions: heartbeat, oldest eligible age, provider acceptance latency, retries, invalid subscriptions, dead letters and audited safe replay. Finalize exact routes/contracts before code. Depends T022. FR-004, FR-010, FR-012.
- [ ] T024 [US4] Run failure/recovery drills on isolated SQL/HTTPS staging: stop/restart, crash after acceptance, lease overrun, unavailable provider, rate limits, expired/cancelled assignment and multi-device partial failure. Ensure no duplicate business effect or lost intent. Depends T023. FR-005, FR-006, FR-010, FR-012.
- [ ] T025 [US4] Load-test declared concurrency/fan-out, bounded database queries and dispatcher throughput; measure Gulf client latency and SC-003/SC-007 separately from device arrival. Rehearse database restore and secret/key recovery. Depends T022-T024. FR-006, FR-012.
- [ ] T026 [US3/US4] Finish the actual device/OS matrix, old/new client compatibility, Arabic/RTL/accessibility review and SW update/rollback with a stale installed client. Retest negative authorization and cache isolation after account switches. Depends T024/T025. FR-004, FR-007-FR-009, FR-011, FR-012.
- [ ] T027 [US4] Validate a provider-neutral Application delivery interface and versioned target/read contract for a future native client. Document FCM/APNs registration/token lifecycle as future work only; no native SDK, build, credential or store enrollment. Depends T023. FR-013.
- [ ] T028 [US4] Prepare operator/school onboarding and a pilot plan: synthetic accounts first, explicitly selected school/users next, observable rollback triggers and named incident owner. Execute external rollout only under actual deployment authorization. Depends T022-T027. FR-008, FR-010-FR-014.
- [ ] T029 [US1-US4] Review requirement -> task -> evidence consistency, record measured outcomes and unresolved exceptions, update central memory and stop with a self-contained handoff. Do not call the feature complete if required checks remain pending. Depends T028. FR-001-FR-014.

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

US1/US2 can be accepted with Web Push disabled. US3 requires N1's reliable visibility/delivery seams. US4 assumes working N1/N2 and proves operational behavior. Neither N3 nor future native work is a hidden fourth implementation phase.
