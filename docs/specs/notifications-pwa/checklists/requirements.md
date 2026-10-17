# Specification Quality and Delivery Checklist

**Reviewed:** 2026-10-17. Checked planning items describe document review only. Delivery items remain unchecked until implementation evidence exists.

## Specification quality

- [x] Business outcome and independent US1-US5 acceptance scenarios are explicit.
- [x] Exactly three delivery phases; testing/security/RTL review are embedded in each phase.
- [x] FR-001-FR-018 map to ordered tasks and phase exits; the 2026-10-17 owner extension adds T012A-T012D to N2 without reopening historical N1 evidence.
- [x] Existing behavior, confirmed defects, source-backed capability and proposed design are distinguished.
- [x] Official sources and verification date are recorded; current Angular/.NET support risks are explicit.
- [x] School/recipient authorization, approval, office hours and System conversation policies are retained.
- [x] PWA installation, foreground realtime, background push and native-mobile scope are distinct.
- [x] No guaranteed device arrival, push-driven read receipt or unsupported exactly-once claim is made.
- [x] Durable intent, per-device retries, cursor correctness and failure/rollback behavior are specified.
- [x] Private caching, lock-screen content, endpoint SSRF and subscription ownership are covered.
- [x] Clarifications have defaults and resolution gates; unavailable customer/device/hosting inputs are not invented.
- [x] Constitution check and project-memory update requirements are included.
- [x] Initial plan/VPS artifact validation passed: 13 Markdown files including the central tracker, exactly three phase files, 29 ordered unchecked tasks, all 14 requirements covered, 44 relative links/anchors valid and three JSON examples parsed successfully. The later Linux audit adds its own report and evidence; it does not complete feature delivery gates.
- [x] Linux preflight evidence is retained separately: Release publish and 52 focused tests pass; synthetic SQL migration/HTTP/PDF probes and deployment blockers are recorded. Local relative-link/anchor checks and tracked documentation diff checks pass. Operational credentials are excluded from retained artifacts.
- [x] Owner coverage update artifact review: 18 mandatory families, 6 optional suggestions, 5 policy questions, 18 requirements and 33 unique tasks (11 complete / 22 pending), with exactly three phase files. Checked 282 relative file links across the 14 changed/new Markdown files: no introduced broken links; three pre-existing broken links in the historical decision log remain outside this update. `git diff --check` passes. No runtime/device tests ran for this documentation-only update; previous N1 evidence is unchanged.

## N1 delivery evidence - locally verified

- [x] Latest/history and >=120 unread regression; late-release and commit-order tests.
- [x] SQL transaction/claim/lease, crossed-count consistency, actual deadlock retry and canonical surface re-arm tests.
- [x] Current producer classification and actual Duty dual-surface canonical realtime identity; no seed/history broadcast. Web Push source deduplication remains N2.
- [x] Existing-connection deactivation/expiry, initial failure/reconnect, protected-cursor and Angular scope-change checks.
- [x] [43 backend/SQL + 2 Angular + 8 browser checks](../verification/n1/README.md), local viewport p95 and retained Arabic/RTL screenshots. Production build/version/hosting gates are explicit.

## N2 delivery evidence - local implementation verified, device acceptance pending

- [x] Mandatory catalog NC-01-NC-18 has concrete transition/source/recipient/target/expiry and positive/negative evidence; optional NS suggestions remain separately selected scope.
- [x] Top-bar bell/history, independent authoritative center count, filters, read/read-all, current task state and exact-item navigation pass Arabic/RTL/mobile/keyboard checks.
- [x] CP-01-CP-04 are resolved for required producers; complaint privacy, completion partial-save/replay and source/dashboard/System deduplication checks pass (SC-009/SC-010).

- [x] Compatible PWA dependency matrix and one worker; private cache inspection.
- [ ] Actual supported iOS/Android install/permission/background/click results.
- [ ] Denied/unsupported fallback, multiple devices, invalid endpoint and key/subscription lifecycle on actual providers/devices; local ownership, SQL multi-device isolation and key-purpose restart checks are recorded separately.
- [x] Ownership/SSRF tests, safe generic payload and authenticated cross-school target handling.
- [x] Local draft-aware update confirmation, channel controls and no historical opt-in backlog; actual stale installed-client update/removal remains pending.

Stable N2 run: 76 backend tests, 6 Angular tests, 3 responsive browser profiles and the actual local Angular worker pass (207 public prefetch assets, zero private cached paths). Local results and precise boundaries: [N2 verification report](../verification/n2/README.md). Checked local items do not close the actual-device rows or final N2 exit.

## N3 delivery evidence - pending

- [ ] Actual hosting capability and final DNS/TLS ownership evidence; no unsupported provider-plan assumption.
- [ ] Healthy-worker/provider latency separated from device arrival timing and declared load.
- [ ] Restart/failure/restore, safe replay and stale-worker rollback drills.
- [ ] Version, retention, workload, support and incident-owner decisions resolved for rollout.
- [ ] Native adapter compatibility documented; no native delivery/build claim.
- [ ] All success criteria, central records and pilot gates reconciled.

## Evidence record format

For each implemented check record: task/requirement, date, source revision, environment, database provider, device/OS/browser, relevant configuration flags, steps, expected/actual outcome and retained artifact path. Redact identities/secrets. Label simulated, desktop-only, SQL integration and actual device checks separately. A build pass alone is not notification acceptance.
