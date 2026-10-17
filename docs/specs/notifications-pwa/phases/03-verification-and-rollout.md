# N3 - Device Verification, Operations and Controlled Rollout

**Status:** Not started.  
**Story:** US4 plus cross-story acceptance. **Tasks:** T022-T029 in [tasks.md](../tasks.md).  
**Dependency:** Accepted N2 exit. This phase extends earlier verification; N1/N2 tests are not deferred here.

## Outcome

The operator has evidence that the selected deployment can run the workers, handle the expected school load, recover safely and support a limited rollout. Native-mobile compatibility is documented without implementing an application.

## Included work

- Confirm actual hosting/DNS/HTTPS/WebSocket/worker/SQL/backup capabilities and the version-support decision.
- Protected delivery telemetry, actionable alerts and audited replay that rechecks eligibility/expiry.
- Restart, provider failure, lease overrun, fan-out, restore and stale-installed-client rollback drills.
- Complete real device/browser/OS matrix and Gulf connection measurements.
- Provider-neutral native adapter contract and future token lifecycle notes; retain current APIs and SQL Server.
- Operator and school onboarding, pilot cohorts, rollout observations and rollback triggers.

Do not turn this into a native mobile development phase, implement a new scheduler without evidence, silently scale out workers or change existing notification business policies.

## Required checks

- Crash before/after intent commit and after provider acceptance; no lost intent or duplicate business operation.
- Worker stopped/restarted and process supervised; a keep-alive HTTP ping is not accepted as proof of durable hosting.
- Rate limits/timeouts and a single failed device do not block healthy devices or replay successful targets.
- Expired/cancelled coverage alerts and no-longer-eligible recipients are not submitted on retry.
- Database/secret-key restore on an isolated environment; old subscriptions are not accidentally broadcast during restore tests.
- Declared workload, p95 server/acceptance measurements and observed actual device timing; do not mix those metrics.
- Old installed service worker, app update with a draft and rollback using the documented genuine 404/safety-worker mechanism.
- Final school/user/cache isolation review and responsive Arabic/RTL usability checks.

## Exit evidence

- [ ] SC-001-SC-008 are linked to actual evidence or explicit, owner-accepted exceptions; mandatory failures block general rollout.
- [ ] Hosting and version decisions, support matrix, capacity assumptions, retention/expiry settings and incident owner are recorded.
- [ ] Independent channel switches and restore/rollback drills have measured outcomes.
- [ ] Pilot instructions identify users/school, observation window, stop conditions and authorized external actions.
- [ ] Native contract remains future work; no native app capability is advertised as delivered.
- [ ] Requirements/tasks/checklists, API/schema/security docs, changelog, decisions and central tracker are reconciled.

Stop and report actual readiness. Planning a pilot does not mean production deployment, provider traffic or broad rollout has happened.
