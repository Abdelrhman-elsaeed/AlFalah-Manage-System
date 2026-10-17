# N2 local verification and acceptance gates

Date: 2026-10-17. Scope: T012–T021 and T012A–T012D. N3 is not started. Implementation is present; N2 acceptance remains conditional on T020 and the deployment inputs below. This directory contains synthetic evidence, never operational subscriptions, credentials or student records.

## Evidence levels

| Check | Environment / evidence | What it establishes |
|---|---|---|
| Backend and SQL | `NotificationCenterTests`, `NotificationCenterTransitionsTests`, `NotificationCenterSqlTests`, `NotificationPushAdapterTests`, selected existing attendance/Duty/inbox/Data Protection regressions | Actual Application/Infrastructure source projection, recipient gates, canonical identity, reads/counts, targets, transaction rollback and SQL claims. See final result below. EF InMemory transition checks are explicitly separate from SQL concurrency checks. |
| Signed Duty trigger | `DutyExecutionSqlTests.N2_actual_Duty_dismissal_surfaces_share_center_identity_and_signed_submission_is_the_completion_trigger` | Real Duty service, actual synthetic signatures, dual surfaces, draft/letter/no-report boundary, committed report submission, archive failure, correction and replay. |
| Angular coordinator | `tsconfig.n2-spec.json`, `notification-center.service.spec.ts`, `web-push.service.spec.ts`, existing `inbox-sync.service.spec.ts` | Scoped stale-response discard, authoritative count independent of first page, bounded filters/cursor, explicit reads, no automatic permission prompt, draft-aware update confirmation. |
| Browser UI | [browser-results.json](browser-results.json), [desktop](center-1440.png), [390 px](center-390.png), [320 px](center-320.png); `frontend/scripts/check-notification-center.mjs` | Real running Angular/Chromium at 1440/390/320 px with **intercepted synthetic API responses**. Bell/read separation, filters, independent counters, focus/Escape/Tab, selected navigation and horizontal containment. These are not live backend E2E or actual mobile-device checks. |
| Angular root worker | [worker-results.json](worker-results.json), `frontend/scripts/check-notification-worker.mjs` | Real Angular 17 optimized worker/manifest/assets and CacheStorage, public shell on simulated network failure, private API unavailable offline. Loopback Node fixture; no live ASP.NET header, phone, provider or HTTPS staging evidence. |
| Build | `pwa-verification` optimized application, matching `@angular/service-worker` 17.3.12 | Local worker artifact can be built. This dedicated verification configuration is **not** the production release build. Original production CSS budgets remain unchanged and fail on existing visit/storage component styles. |
| Push transport | `NotificationPushAdapterTests` | Real library encryption and VAPID signing with generated test keys, intercepted 201/400/401/403/404/410/429/503 HTTP responses, TTL and Retry-After; no external provider request or device receipt. |
| Subsequent Android trial | [Actual trial result](android-trial-diagnostics.json), [procedure/history](android-device-trial.md) | Operator-run temporary HTTPS; actual independent device bindings and provider-accepted fresh guardian message. Owner confirms outside-app Android notification and correct click navigation. Actual phone report, not a simulation; exact versions/settings and remaining matrix unverified. |

## Reproduce

Use fresh guarded LocalDB names as in [quickstart](../../quickstart.md). SQL fixtures reject arbitrary databases and clean up their own synthetic databases. Run from the repository root:

```powershell
$env:ALFALAH_INBOX_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahInboxTests_N2_' + [Guid]::NewGuid().ToString('N') + ';Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=False'
$env:ALFALAH_DUTY_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=AlFalahDutyTests_N2_' + [Guid]::NewGuid().ToString('N') + ';Trusted_Connection=True;TrustServerCertificate=True'
dotnet test backend/AlFalah.Tests --filter 'FullyQualifiedName~NotificationCenter|FullyQualifiedName~NotificationPushAdapter|FullyQualifiedName~N2_actual_Duty|FullyQualifiedName~InboxWindowTests|FullyQualifiedName~InboxSecurityTests|FullyQualifiedName~InboxSqlTests|FullyQualifiedName~AttendanceAndDelayWorkflowTests|FullyQualifiedName~DataProtectionPersistenceTests' -m:1 /nodeReuse:false --logger 'trx;LogFileName=n2-final.trx'
```

From `frontend/`:

```powershell
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.n2-spec.json --include='src/app/core/services/{inbox-sync,notification-center,web-push}.service.spec.ts'
npx ng serve --host 127.0.0.1 --port 4316
# Separate terminal, after the server is ready:
node scripts/check-notification-center.mjs
npx ng build --configuration=pwa-verification --output-path=dist/n2-pwa-verification
node scripts/check-notification-worker.mjs
```

The UI harness sends no business writes to an operational server. The worker harness starts/stops its own loopback HTTP fixture. Test results and screenshots are evidence of this working-tree implementation, not a committed release revision.

## Remaining N2 gates

- **T020:** supported iOS/iPadOS Home Screen and remaining Android Chrome/desktop lifecycle matrix, exact OS/browser/settings and no-tab/locked context. Android basic outside-app receipt/correct click is now explicitly confirmed by the owner on temporary HTTPS with actual provider acceptance; this does not complete the matrix or provide unavailable iPhone evidence.
- Final stable origin, VAPID public/private key pair/key ID/operator subject, exact verified provider host allowlist, durable existing Data Protection ring and secure key backup/recovery. A separate synthetic Android trial pair was subsequently generated outside the repository; it is not a production signing identity. [Trial preparation and execution gate](android-device-trial.md). No secrets are committed here.
- Actual multi-device partial failure, reinstall, key rotation/re-enrollment, denied/unsupported contexts, expired login, explicit active-school switch and offline/in-flight revocation window on the chosen devices/providers. Local tests do not close these rows.
- Production frontend CSS budgets, supported Angular/runtime/Node decision, and existing Linux preflight blockers remain release gates. The test host rolls net8 tests forward to the installed .NET 10 runtime; that is not a native .NET 8 deployment check.
- Historical center backfill is deliberately absent. Existing legacy inbox history remains in its original screens; newly projected events populate the unified center. Opt-in never replays older events.

T021 documentation is reconciled locally, but its final exit acceptance depends on T020. N3 operations UI, fleet telemetry, load/region benchmarks, restore/rollback drills, provisioning and rollout are not implemented or run.

## Final local result

Stable-source rerun: **76 backend tests passed, zero failed/skipped** ([sanitized case results](backend-results.json)); **6 Angular tests passed** in ChromeHeadless 155 ([case record](frontend-results.json)); **3 intercepted-API browser profiles passed** at 1440/390/320 px; **real local Angular worker passed**, with all **207 public prefetch assets** present (210 total cache paths including worker metadata), zero private paths and a controlled same-origin navigation. Optimized `pwa-verification` build passes. Production build was rerun with the unchanged production configuration and fails only the existing 32 kB component-style errors: visit-workspace **34.50 kB** and storage-page **39.34 kB**; budgets were not relaxed. See [build results](build-results.json).

The final SQL checks include concurrent cross-account endpoint registration (only one winner), independent accepted/410 device outcomes, disabled-channel cancellation/no replay and target/attempt fencing. Data Protection tests include push purpose separation and reopening the material with a fresh provider using the existing persistent ring. The sixth Angular check renders the real center component, changes context while the next request is pending and proves old private previews disappear immediately. The final worker harness awaits asynchronous registration/cache probes with `expect.poll`, observes actual activation/claim, verifies all 207 prefetch assets before cutting the simulated network and checks controlled normal navigation. Earlier partial-cache counts are superseded; it does not manually claim/register a replacement worker. These baseline local checks alone do not establish actual phone receipt. No result establishes guaranteed delivery, exactly-once presentation or supported production deployment.

## Subsequent actual-device and error-handling follow-up

The baseline local checks above remain historical. The [operator's subsequent Android trial](android-device-trial.md#subsequent-actual-android-result--basic-receipt-and-click-passed) adds actual provider acceptance plus the owner's actual outside-app receipt/correct-click confirmation. This supersedes the earlier absence of all phone/provider evidence for this synthetic trial only. Initial opt-in failed without a persisted binding; after reopening/login the guardian registered and a new workflow message produced one accepted target. Initial failure cause is not proven, and desktop enrollment is not an Android prerequisite.

Frontend follow-up adds actionable Arabic opt-in errors and rejects an old-account failure after context changes. Four regression cases fail before the change, then **all ten N2 Angular tests pass**; separate optimized `n2-device-diagnostic-build` passes. These browser failure cases use simulated DOMExceptions. The new build is not deployed into the active phone session, so actual phone recovery is not attributed to it. Temporary trial request logging has been restored. No backend schema/main database/permissions or production CSS budgets changed; T020/T021 full acceptance and N3 boundaries remain intact.
