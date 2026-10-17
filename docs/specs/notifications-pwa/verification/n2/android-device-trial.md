# Android HTTPS trial preparation — 2026-10-17

The owner has no stable HTTPS deployment and only an Android phone. The operator has now started the temporary HTTPS trial; Android opt-in/delivery is failing as recorded below. This is a bounded N2/T020 test setup, not hosting, production activation or N3. No iPhone evidence can be collected yet.

## Actual result

- Generated a fresh P-256 VAPID pair/key ID, random JWT secret and strong synthetic-account password **outside the repository**. Verified that the public key matches the private scalar; no secrets printed or committed.
- Private configuration is under `C:\Users\abdelrhman\AppData\Local\AlFalah\N2DeviceTrial\20261017-212712`. The operator-run trial now has registration/dispatch/UI enabled **only for its isolated database**. That `AlFalahN2DeviceE2E_` database is created and seeded: read-only checks confirm the exact configured target, 79 migrations and 15 synthetic users. See the corrections/diagnosis below; this supersedes the initial preparation-only state.
- Downloaded the official Windows amd64 cloudflared 2026.10.1 executable to the user-local tools directory; verified its release SHA-256, version and supported flags. Digest: `4d2490e74082c5d48dbfedaf92576fa5bd1702379ab88eb7faa0cfbe8dc891b6`.
- Prepared an operator launcher and loopback proxy. Synthetic proxy checks pass for API authentication forwarding/no-store headers, denied files/Swagger/hubs/reset routes, SPA versus missing worker routing, manifest MIME, worker scope and Windows UTF-8 BOM configuration. PowerShell syntax validation passes.
- **Execution history:** automatic approval review initially rejected the agent launching the isolated API with `blocked by policy`, without a more specific reason. The owner subsequently ran the launcher locally after its corrections. Actual public HTTPS manifest/worker return 200 with expected MIME types. Initial Android enrollment failed; a subsequent retry registered the guardian device, the provider accepted a fresh message, and the owner confirms a notification outside the app and click opening the correct conversation. **Basic receipt/click passed by owner report; full T020 acceptance remains open.** Existing main local API/database activation is unchanged by this preparation and diagnosis.

[Sanitized preparation/progress evidence](android-trial-preparation.json), [current enrollment/delivery diagnosis](android-trial-diagnostics.json). A serving HTTPS trial does not establish successful enrollment/provider acceptance/phone delivery.

## Operator start on this workstation

From the repository root in a fresh PowerShell session, run:

```powershell
powershell -NoProfile -File .\frontend\scripts\start-n2-device-trial.ps1 -TrialDirectory 'C:\Users\abdelrhman\AppData\Local\AlFalah\N2DeviceTrial\20261017-212712'
```

The launcher validates the isolated local database name and free ports 5487/4487. It uses the existing Debug API and optimized `pwa-verification` artifact; these are not release-build acceptance. It migrates/seeds only the new synthetic database with push off, disables every account except strongly passworded `parent.test`/`officer.test`, restarts without startup seeding, and verifies their actual logins **before** opening the tunnel. It never copies the live application's configuration or data and refuses inherited application configuration overrides.

If it succeeds, the console prints the HTTPS URL and the path to the private `device-login.txt`. Keep the console open. The default trial lasts two hours, with a maximum of four. Ctrl+C stops the processes created by that invocation and disables trial push flags. The proxy also stops serving at expiry. The synthetic SQL database/key directory are retained for diagnosis, not deleted automatically. Do not publish or attach the private directory/logs.

Only the isolated trial enables push; the existing application remains externally disabled. Android Chrome's exact `fcm.googleapis.com` provider host is the only configured provider. Other browsers are outside this trial. Realtime hubs/files/Swagger/password recovery are deliberately unavailable through the temporary proxy; this does not verify the deployed application's realtime or file workflows. Normal application permissions and signature enrollment remain enforced.

[Cloudflare Quick Tunnels](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/) assign a temporary public HTTPS hostname, require no hosting purchase and are intended for testing. This hostname is not a stable production origin. Stop the tunnel after testing. A new hostname is a different browser origin: install/enroll again there; do not count it as the original installation.

If rebuilding is necessary:

```powershell
dotnet build backend/AlFalah.Api -m:1 /nodeReuse:false
Set-Location frontend
npx ng build --configuration=pwa-verification --output-path=dist/n2-pwa-verification
```

To reproduce only the local proxy check from the repository root:

```powershell
node frontend/scripts/check-n2-device-trial.mjs
```

### Windows PowerShell startup fix — 2026-10-17

The operator's first invocation stopped at `Keyword not supported: 'ConnectionString'`, before process startup or database access. Reproduced the exact exception using the launcher's actual configuration-validation statements. Windows PowerShell's dictionary adapter interpreted assignment to `SqlConnectionStringBuilder.ConnectionString` as an unsupported SQL keyword. The launcher now passes the connection string to the constructor, preserving all isolation checks.

The regression command failed with the same exception before the fix and now passes **eight cases** on Windows PowerShell 5.1.19041.6456: the reported valid connection, equivalent SQL keyword aliases, and rejection of operational database names, identity mismatch, remote SQL, SQL authentication, weak passwords and the operational API port. The existing private trial configuration also passes this actual validation path without SQL/process/network operations. Synthetic proxy checks still pass. This fixes the reported startup exception; full launch/HTTPS/provider/phone outcomes remain unverified. Retry the same operator start command above.

```powershell
powershell -NoProfile -File .\frontend\scripts\check-n2-device-trial-preflight.ps1
```

### SQL account-table startup fix — 2026-10-17

The next operator retry reached the SQL restriction step, then failed with `Invalid object name 'AspNetUsers'`. Reproduced that exact exception against the guarded configured E2E database. This is **not a missing migration**: the isolated target has 79 applied migrations, the `dbo.Users` table and 15 seeded synthetic users. `AlFalahDbContext` and the initial migration explicitly use `Users`; the launcher's assumed default ASP.NET Identity name was wrong. Use the mapped, schema-qualified `dbo.Users` in both update/count statements. No schema migration is required.

Added a regression that extracts the launcher's actual SQL and executes it in a transaction against only the guarded isolated local E2E database. It failed with the reported exception before the fix; after the fix it proves exactly `parent.test`/`officer.test` stay enabled, all other accounts are disabled, then rolls back and verifies every account's original active state was restored. It starts no API/tunnel and sends no provider request. Eight preflight cases and the synthetic proxy checks still pass. Full launch/HTTPS/phone acceptance remains unverified; retry the operator command above.

```powershell
powershell -NoProfile -File .\frontend\scripts\check-n2-device-trial-database.ps1 -TrialDirectory 'C:\Users\abdelrhman\AppData\Local\AlFalah\N2DeviceTrial\20261017-212712'
```

## Actual Android actions and evidence still required

### First actual Android attempt — enrollment not persisted

Owner reports Chrome on Android, a displayed browser permission prompt and selecting Allow, but sees `لم تُفعّل الإشعارات. تحقق من إذن المتصفح وحالة الاتصال ثم أعد المحاولة.` System notification did not appear in foreground or with the screen locked; the in-app bell displays the message. Phone model/OS/browser versions are not supplied. This is owner-reported actual-device failure, not a simulation or successful provider delivery.

Direct checks of the exact isolated database find zero subscription rows, zero push targets/attempts, and all three personal Message entries have `NoActiveSubscription`. Authenticated guardian `/push/config` is enabled with a key matching the private trial identity; `/push/subscriptions` returns no devices. Actual HTTPS manifest/worker are 200 with correct MIME. There are no observed push-sweep warning lines. The failure is before a persisted device binding/provider send; its exact client-versus-registration reason is not established yet. Granting browser permission alone does not establish a server registration. No replay of these historical messages is authorized or needed; after successful enrollment use a **new** workflow message.

A temporary private MVC result trace can distinguish whether the actual Android retry reaches the existing registration action, without printing body/token/endpoint/key content or restarting the trial. Optional `tracePushRegistration: true` in the private proxy settings enables similarly restricted status-only events **on a future proxy start**; it is not claimed active in the already-running proxy. Its synthetic check verifies only event/time/status are printed, with no Authorization/body material. Regression ports 5488/4488 are separate from the active phone trial's 5487/4487, so checks do not stop the phone session. This remains N2 trial troubleshooting, not N3 telemetry.

### Subsequent actual Android result — basic receipt and click passed

The owner reopened the app and logged in again, then reports activation succeeded. Direct SQL now shows separate active bindings for `officer.test` and `parent.test`. Guardian enrollment is confirmed at 19:01:21 UTC; a **new** guardian Message entry commits at 19:01:59 UTC; its actual target/attempt is `ProviderAccepted` at 19:02:00 UTC. The owner explicitly confirms the notification appeared outside the Android app and tapping it opened the correct conversation. That last observation is **actual phone evidence supplied by the owner**, not direct device instrumentation or a screenshot. Do not infer the exact locked/no-tab state, OS/browser versions, delivery latency or the remaining lifecycle rows from it.

Desktop activation is not a prerequisite: every browser/device has its own explicit binding and permission. The exact initial failure cause remains unknown; no Android exception was captured. Recovery occurred on the originally served build, so it is **not** evidence that the subsequent copy change fixed enrollment. The three pre-enrollment messages retain `NoActiveSubscription`; no historical replay, fabricated subscription or agent-authored test message was performed. Temporary private logging has been restored to its original settings without changing the API/tunnel/keys/push flags.

Frontend follow-up uses the existing Arabic/RTL accessible status area: distinguish browser permission failure, browser push-service failure and server registration failure with actionable text; discard an old pending failure after an account change. Four targeted cases fail before the change, and all **ten N2 Angular tests** pass afterward in ChromeHeadless 155. DOMExceptions there are **simulated**, not captured phone errors. Separate optimized `dist/n2-device-diagnostic-build` passes; it has **not** replaced the active phone-trial artifact. Production CSS/runtime gates remain unchanged. T020 is partly evidenced, not complete; iPhone and remaining device/stable-origin gates stay pending. N3 is unstarted.

1. Record phone model, Android version, Chrome version, Wi-Fi/mobile network, notification permission and battery restrictions. Open the printed HTTPS URL in Chrome. Log in to **Al-Falah E2E Test School** as `parent.test` using the private file, install the app, and explicitly enable device notifications in the existing notification controls. Do not share the password or subscription endpoint/keys.
2. Confirm the real authenticated registration succeeds and the owned device appears. A granted browser permission alone is insufficient. Inspect actual worker activation/public CacheStorage; authenticated API data must remain unavailable offline.
3. On desktop, use a separate browser profile and log in as `officer.test`. Complete normal signature enrollment if requested. Through the existing messaging workflow, send a new immediate message to the synthetic guardian/student. Do not insert a fake center row or call a simulated DevTools push. The message must be created **after** the phone enrolled; enrollment does not replay history.
4. Background/close the phone app without DevTools attached, leave Chrome allowed to run, and observe the actual generic lock-screen notification. Record commit/worker/provider acceptance separately from phone arrival. Provider acceptance alone does not prove arrival. Tap it, authenticate if requested, and confirm the exact authorized conversation/message opens; compare center unread count/read behavior.
5. Exercise foreground, denied permission, offline/reconnect, logout/revocation, expired login and reinstall on the actual device, retaining sanitized outcomes. Complete the broader [device matrix](../../quickstart.md#device-evidence-matrix) before accepting T020; one Android arrival cannot close every lifecycle/multi-device row.

For the iPhone row, retain **pending — no device available**. Supported iOS/iPadOS needs the Home Screen installation and user-initiated permission flow documented by [WebKit](https://webkit.org/blog/13878/web-push-for-web-apps-on-ios-and-ipados/). No desktop emulation or Android result substitutes for it. Stable deployment/signing identity and existing release/runtime gates also remain open. T021 final acceptance remains conditional; N3 has not started.
