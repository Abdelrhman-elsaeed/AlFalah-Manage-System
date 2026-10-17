# Session persistence and notification retest — 2026-10-17

The owner requested retained login after closing the phone app, direct platform entry from the homepage for a signed-in user, and correction of the apparent duplicate story navigation. Existing chat changes are included in the rebuilt frontend.

## Causes and changes

- Authentication used `sessionStorage`, so a closed browser/app context could lose its token pair. Save the existing token pair/user snapshot in `localStorage`; migrate an existing tab session together, prefer an already saved persistent session, and clear both stores on explicit logout. The server's existing token expiry/revocation and authoritative `/auth/me` check remain in force. No password is saved. Storage events propagate logout and token updates between tabs.
- Both login routes always rendered their forms. A guest guard now redirects an existing identity to its role landing page and attempts session restoration when a refresh credential is available.
- Startup and refresh errors previously cleared authentication even for connection loss/server outages. Preserve credentials on temporary HTTP failures while withholding the startup identity until server verification succeeds. Rejected sessions still clear credentials.
- The story's narrow-screen CSS arranged ten chapter buttons in five columns, producing two rows. Retain all ten chapters in one row with the existing pause/replay controls. Update the HyperFrames source and generated site assets together.

## Repeatable local verification

From `frontend/`:

```powershell
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.session-tests.json --include='src/app/core/services/{auth,inbox-sync,notification-center,web-push}.service.spec.ts' --include=src/app/core/guards/guest.guard.spec.ts --include=src/app/features/auth/school-login/school-login.component.spec.ts --include=src/app/features/landing/landing-routes.spec.ts --include=src/app/features/student-affairs/messaging-chat/messaging-chat.component.spec.ts
node scripts/check-session-landing.mjs
node scripts/check-chat-experience.mjs
npx ng build --configuration=pwa-verification --output-path=dist/n2-session-chat-build
```

The session and layout browser harness defaults to `http://localhost:4200`; override `ALFALAH_SESSION_ORIGIN` for a built frontend. It checks one navigation row at 320/390/768/1440 px, legacy-session migration, homepage entry, restoration in a fresh browser context containing only persistent storage, and logout propagation through actual browser storage events between two tabs. Its identity/API replies are synthetic. The chat harness also uses synthetic API/WebSocket replies. These checks do not establish phone receipt.

Before the fix, all six original session cases failed; three of four routing cases failed; the layout harness reported two rows at 320 px. Two added temporary-failure cases also reproduced credential loss before their correction.

## Verified result and active trial

- **58 Angular tests passed**, covering authentication with its real HTTP interceptor, login routing, existing role redirects, inbox synchronization, notification center, Web Push and chat behavior.
- Browser session/layout checks passed against both the development server and the rebuilt optimized artifact. Chat browser checks passed at 1440/768/390/320 px with synthetic API/WebSocket responses. The actual public HTTPS page also passed the 390 px single-row/ten-chapter check.
- The optimized `pwa-verification` build passed. The real Angular worker check passed with 207 public prefetch assets/210 cache paths, zero private cached paths, an available offline public shell and an unavailable private API probe. This is the verification configuration; existing production CSS budget gates are not closed by it.
- Rebuilt frontend files were copied into the existing trial artifact after a private backup. Existing hashed files were retained for older clients, and the index/worker manifest were updated after the assets. Actual public HTTPS index and `ngsw.json` match the new artifact. No backend rebuild/restart, migration, VAPID change, trial database replacement or subscription change was needed.
- A separate Chromium check used the **real isolated API** and `parent.test`: actual login, homepage re-entry, a fresh browser context containing only persistent storage, and three successful authoritative `/auth/me` responses. It sent zero chat messages and changed no device registrations. This is desktop browser evidence, not an Android app-close observation.
- Existing temporary origin: `https://infants-carbon-opera-glow.trycloudflare.com`. The owner's existing launcher expires at **23:40 Cairo time, 2026-10-17**; keep its window running. This origin is temporary and is not the stable-origin N2 gate.

The owner subsequently reports that both requested session checks passed on the updated version: removing the phone app from recent apps and reopening retained login, and returning to the homepage and pressing platform entry reopened the existing account. This is owner-reported actual-device/user-flow evidence, without direct device instrumentation. Exact OS/browser versions and elapsed closure time were not supplied. It does not establish expired-token renewal or push receipt after closure.

The owner then confirms the next instructed actual-phone scenario passed: notifications enabled, app removed from recent apps, phone screen locked, a new desktop-to-phone message sent, and the resulting notification tapped. The reported successful outcome covers external notification arrival, opening the correct conversation and retained login. This establishes the combined removed-app/locked-screen/click scenario for this rebuilt version by owner report. No new provider-log inspection, measured delivery latency, exact OS/browser versions or independent background-only test is claimed.

The remaining notification/lifecycle cases still need to be recorded using the cases below. N2 full acceptance remains open and N3 is not started.

## Device setup

Use the same temporary HTTPS origin on phone and computer. Keep its launcher open. Use `parent.test` on Android and `officer.test` on desktop in **Al-Falah E2E Test School**, with the existing private login instructions. Existing registration is independent for each browser; desktop permission is unnecessary for phone delivery. To receive on the desktop as well, explicitly enroll that desktop browser.

Apply the offered application update, or close and reopen the app after the new worker version becomes ready. Check the new chat UI and the single story navigation row to confirm the new frontend loaded. A session lost before migration cannot be recovered: log in once on the updated version. Keep this same origin; a new temporary hostname has separate storage and subscription permissions.

Send a **new** normal chat message for each row, after the receiving device has enrolled. Record sender, recipient, time, exact app state, in-app result, external notification result and click destination. Never treat an old message, browser permission alone, or provider acceptance alone as proof of new phone delivery.

| Case | Action | Expected observation |
| --- | --- | --- |
| Retained phone login — passed by owner report | Sign in on the updated app, remove it from recent apps, reopen | Owner confirms the existing session restored without another login |
| Homepage entry — passed by owner report | Return to the public homepage and press platform entry | Owner confirms the existing account opened directly |
| Closed desktop browser | Close browser windows and reopen the same profile/origin | Saved session is verified and restored |
| Explicit logout | Log out, then reopen; repeat with another tab open | Login is required; other tabs discard the old identity; device cleanup uses the existing logout hook |
| Startup connection failure | Open while disconnected, reconnect and re-enter/reload | No private identity is accepted without verification; saved credentials survive for retry |
| Foreground chat | Send desktop → phone while the phone conversation is open | New message appears without manual refresh; draft/composer stays usable |
| Other conversation/page | Send to phone while another screen is open | Correct unread/bell state and correct conversation when opened |
| Background app | Put installed phone app in the background and send | Record actual external arrival and click opening the correct conversation |
| Removed recent app — passed by owner report in combined locked-screen scenario | Remove app from recents, keep Chrome allowed to run, send | Owner confirms external arrival and tapping it opened the correct conversation without another login |
| Locked phone — passed by owner report in the same combined scenario | Lock the phone and send | Owner confirms the instructed locked-screen receipt/click scenario succeeded; not a separate second trial |
| Reply direction | Reply phone → desktop | Chat/unread state updates; external desktop receipt is assessed only if that browser enrolled |
| Multiple messages/read sync | Send several distinct messages, open/read, revisit both devices | No duplicate chat rows; unread state reconciles; old events are not replayed on enrollment |
| Offline receiver | Disconnect receiver, send, reconnect | Authoritative chat/inbox catches up; record external push separately |
| Permission/disable | Deny or disable this device's notifications, then send | No new pushes to the disabled binding; in-app chat/inbox remains available; explicit re-enrollment uses a new message |
| Expired login | Use a legitimately expired/revoked test session | Valid refresh renews silently; rejected refresh requires login and preserves notification authorization checks |
| Same account, two devices | Enroll both, disable one, send | Remaining enrolled device remains independent; record each device's result |

iPhone/Home Screen, stable HTTPS origin, reinstall/key rotation and the remaining full T020 matrix stay pending until actually exercised. This work does not start N3 or claim guaranteed delivery.
