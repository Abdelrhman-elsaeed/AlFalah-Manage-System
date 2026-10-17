# Notifications and PWA - Delivery Tracker

**Last updated:** 2026-10-17  
**Package:** [Reliable messaging, realtime notifications and PWA](../specs/notifications-pwa/README.md)  
**Current state:** N1/N2 implemented and locally verified. N2 actual-device acceptance remains pending; N3 not started (C52/D-144).

| Phase | Scope | Status | Evidence |
|---|---|---|---|
| N1 | [Reliable inbox and authenticated realtime](../specs/notifications-pwa/phases/01-reliable-inbox-and-realtime.md) | Implemented; locally verified | [43 backend/SQL + 2 Angular + 8 browser checks pass](../specs/notifications-pwa/verification/n1/README.md); local viewport p95 2,089/1,948 ms; deployment flags remain off |
| N2 | [Personal center, required event coverage, PWA and opt-in Web Push](../specs/notifications-pwa/phases/02-pwa-and-web-push.md) | Implemented; partial actual Android evidence; full acceptance gated | [Local results and follow-up](../specs/notifications-pwa/verification/n2/README.md): 76 backend baseline, now 10 Angular tests; actual temporary HTTPS/provider acceptance and owner-confirmed Android outside-app receipt/correct click. Remaining lifecycle/versions/iPhone/stable-origin gates pending. |
| N3 | [Operational verification and controlled rollout](../specs/notifications-pwa/phases/03-verification-and-rollout.md) | Not started | No hosting/rollout evidence |

Research, clarification and design are planning activities; they do not add a fourth implementation phase. Each phase includes meaningful tests, security review and Arabic/RTL verification. Stop and report after each completed implementation phase under the project constitution.

## Planning record

The entries below are chronological historical records; earlier "unstarted" and task/link counts apply to their recorded planning revision, not the current N1 result or expanded scope.

2026-10-17: Recorded current polling, the reproduced oldest-50 pagination defect, potentially capped unread aggregate, existing transactional outbox/worker and separate Duty notice producers. Checked official Spec Kit, Angular, WebKit, Microsoft, Web Push and Firebase sources. Proposed three phases with explicit requirements/tasks/contracts, protected per-device delivery, supported-stack decision gates and actual-device/hosting acceptance.

Only Markdown documentation changed. No runtime feature, migration, package, provider credential, notification traffic, domain configuration or deployment was changed. Earlier audit tests are baseline evidence only; no new runtime tests were executed for this planning task. All implementation tasks and operational checks remain unchecked.

Planning validation: 13 new Markdown files including this tracker; exactly three phase files; 29 unchecked ordered tasks; coverage for 14 requirements; 41 local relative links and three JSON examples validated. The tracked Markdown diff passes `git diff --check`. These are artifact checks, not runtime/device acceptance.

2026-10-17 hosting follow-up: Owner chooses VPS and prefers Hostinger. Recorded KVM 4 as the proposed all-in-one starting point and KVM 2 as the light-load budget alternative, with sizing unmeasured. Added Linux application/PDF compatibility, production SQL edition, supervised worker startup, region/latency, prepaid/renewal and independent backup gates. Public-source comparison only; no purchase, provisioning, runtime test or phase implementation. N1/N2/N3 remain not started.

Follow-up artifact validation passes: 44 relative links/anchors, exactly three phase files and 29 unchecked ordered tasks; JSON examples and tracked diff checks remain valid. No runtime checks ran.

2026-10-17 Linux preflight (C48/D-140): [Retained report and evidence](../specs/notifications-pwa/verification/linux-preflight/README.md). Actual isolated Linux Release publication passes; 52 focused tests pass; SQL Server 2022 Express applies 76 migrations twice safely with zero pending, 153 application tables and Arabic roundtrip. Production API HTTP smoke returns Swagger 200 and protected routes 401; seven synthetic Arabic PDFs generated and samples inspected. Current frontend production build fails on two CSS budgets. Historical Windows DPAPI keys, case-insensitive file containment on Linux, startup migration/seeding policy, trusted proxy handling, configuration/persistent storage and frontend packaging need disposition before release. No application source/config/schema was changed and no operational data/provider traffic was used. This is a preflight, not N1 implementation or a fourth delivery phase; all feature phases/tasks remain unstarted.

2026-10-17 N1 (C49/D-141): Completed generic inbox/realtime foundation with 43 backend/SQL, 2 Angular and 8 browser checks; see the phase table/evidence. Operational flags remain off and deployment gates remain separate. No unified bell or comprehensive missing-producer coverage is implied.

2026-10-17 owner coverage extension (C50/D-142): Recorded nine mandatory groups as NC-01-NC-18, top-bar bell/history, exact-item navigation and independent task/read state. Source review identifies complaint submission/default-Pending gaps, real complaint role restrictions and unresolved attendance/dismissal completion boundaries. Added optional NS-01-NS-06 suggestions and CP-01-CP-05 questions. Expanded N2 with T012A-T012D and FR-015-FR-018/US5/SC-009-SC-010; exactly three phases remain, with 33 tasks (11 complete, 22 pending). This update changes Markdown only; no runtime implementation, permission, migration, provider traffic or deployment.

2026-10-17 trigger clarification (C51/D-143): Owner resolves CP-02/CP-03. Attendance includes both students and staff: notify SchoolManager per completed student classroom/date sheet and separately for the complete staff school/date sheet. Classroom means a student class/group, not an academic term. Duty notice triggers on committed signing/submission of the report to SchoolManager/entitled Duty administrators, not final-student departure or whole-school completion. Source review confirms existing full-roster staff validation and atomic signed-report submission; canonical replay/correction integration remains N2 work. Documentation only; phase/task completion states unchanged.


2026-10-17 N2 (C52/D-144): Implemented the personal center, confirmed source coverage, exact-item navigation, PWA/public cache and protected opt-in Web Push. [Retained local evidence](../specs/notifications-pwa/verification/n2/README.md) distinguishes SQL, InMemory, intercepted API/provider and real local worker checks. Two additive migrations applied only to fresh test databases; flags remain off. T012A-T012D/T013-T019 locally complete; final origin/provider/device inputs, T020 actual HTTPS phones and final T021 exit remain pending. N3 not started; stop after N2 report.


2026-10-17 local runtime follow-up: existing localhost:5264 API returns 404 for new inbox/center/push routes and omits them from Swagger. Diagnosis indicates an old running backend, independently of migration/activation gates. Read-only probes only; no server/database/flag changes. N2 acceptance gates and unstarted N3 remain unchanged.


2026-10-17 owner-authorized local activation: confirmed local AlFalahDb, checksum-verified COPY_ONLY backup, applied three additive N1/N2 migrations (76 -> 79), rebuilt/replaced stale audit API at 5264, enabled center/journal/realtime locally with external push off and no startup seed. Authenticated center/list/inbox checks now pass. [Evidence](../specs/notifications-pwa/verification/n2/local-activation.json). This changes local activation status, not actual-device acceptance: next gate T020 then final T021; N3 remains unstarted.

2026-10-17 N2 Android trial preparation (C53/D-145): owner has no current HTTPS deployment and only Android. Generated/verified isolated trial VAPID keys outside source and prepared a guarded launcher with a time-bounded loopback proxy; synthetic routing/auth/cache checks and PowerShell parsing pass. Automatic review rejected starting the isolated API (`blocked by policy`, no specific reason), so no trial DB, public HTTPS URL, provider submission or phone receipt exists; the full launcher is not validated. [Actual result/operator next step](../specs/notifications-pwa/verification/n2/android-device-trial.md). T020/iPhone/stable-origin and final T021 remain gated; N3 remains unstarted.

2026-10-17 N2 trial launcher follow-up (C53/D-145): fixed reproduced Windows PowerShell dictionary-adapter `ConnectionString` startup exception before SQL/process operations. Eight actual preflight regression cases and the real private configuration now pass; synthetic proxy checks pass. Full HTTPS launch/provider/phone outcomes remain unverified, so T020/T021 stay pending and N3 unstarted. [Correction](../specs/notifications-pwa/verification/n2/android-device-trial.md#windows-powershell-startup-fix--2026-10-17).

2026-10-17 trial SQL correction (C53/D-145): owner retry initialized the isolated DB (79 migrations, 15 synthetic users), then hit the launcher's incorrect AspNetUsers table name. Fixed to mapped dbo.Users; actual launcher SQL passes a rollback-isolated regression proving exactly both synthetic actors enabled, all others disabled and all original states restored. No main DB/schema change; full HTTPS/provider/device results remain pending. T020/T021 stay gated; N3 unstarted. [Evidence](../specs/notifications-pwa/verification/n2/android-device-trial.md#sql-account-table-startup-fix--2026-10-17).

2026-10-17 actual Android follow-up (C53/D-145): operator's temporary HTTPS trial initially had zero subscriptions; later reopen/login registered independent guardian/staff bindings. Actual provider accepted a fresh guardian message; owner confirms outside-app Android notification and click opening the correct conversation. Exact first failure cause remains unknown; desktop activation is independent. Arabic opt-in error/stale-account regressions now pass all 10 N2 Angular tests; separate optimized build passes but is not deployed into live phone session. Private diagnostic logging restored. [Evidence](../specs/notifications-pwa/verification/n2/android-trial-diagnostics.json). T020/T021 still gated by exact versions/settings, remaining lifecycle/multi-device, iPhone and stable-origin/release checks; N3 unstarted.
