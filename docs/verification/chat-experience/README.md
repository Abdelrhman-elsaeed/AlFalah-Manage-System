# Chat refresh and mobile experience — 2026-10-17

The participant chat keeps the existing conversation creation, recipient restrictions, people/history view, delivery/read receipts, office-hours notices, older-message pagination, close reason and idempotent sending.

Changes:

- Publish InboxSync state and invalidations inside Angular's NgZone. The regression reproduced an outside-zone publication that left rendering dependent on later interaction.
- Retain inbox rows during background requests instead of replacing them with a spinner. Failed background requests retain the last successful list and provide retry.
- Merge refreshed messages, coalesce concurrent thread refreshes and reject headers/list snapshots started before a successful send, close or read update. A regression reproduced the older header overwriting the new send row version.
- Use a viewport-sized chat with independently scrolling messages, content-sized bubbles, date dividers, concise connection status and expandable quick replies. Enter retains its multiline writing behavior.
- Replace the mobile rail with a topbar menu and overlay drawer across the shell, including backdrop dismissal, Escape, focus restoration/trapping and closure on navigation. Desktop collapse remains independent. VisualViewport resize/scroll updates accommodate the available mobile screen area.

## Verification

From `frontend`:

```powershell
$env:CHROME_BIN='C:/Program Files/Google/Chrome/Application/chrome.exe'
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.chat-tests.json --include=src/app/features/student-affairs/messaging-chat/messaging-chat.component.spec.ts --include=src/app/core/services/inbox-sync.service.spec.ts
node scripts/check-chat-experience.mjs
npx ng build --configuration development
```

- 17 focused Angular tests pass. Initial regressions failed on row removal and outside-zone publication; the additional send/background race failed on row version `v1` replacing `v2` before its fix.
- The browser script uses the actual Angular app and SignalR client with intercepted HTTP and a synthetic WebSocket handshake/event. At 320/390/768/1440px it verifies incoming messages without clicking, immediate send rendering, no row removal, preserved drafts, quick replies, composer bounds, and closing without losing history. Mobile checks cover drawer geometry/focus/Escape, a shorter viewport and people/history navigation. See [results](results.json) and the screenshots alongside this file.
- This is synthetic browser evidence, not an operational two-account delivery test or a physical iOS/Android keyboard test. No operational messages or database changes were made.
- Development build passes. Production compilation reaches the existing CSS budget failures in unchanged `storage-page.component.css` (39.34 kB) and `visit-workspace.component.css` (34.50 kB), against 32 kB. Budget thresholds were preserved. Chat contact styles are kept in a separate stylesheet to keep the primary chat stylesheet within its warning budget.
- The default test TypeScript configuration also includes unrelated storage specs with existing compile errors (`ngOnChanges` arguments and missing `ArchiveOperations.schoolEnabled`). `tsconfig.chat-tests.json` scopes this verification without modifying those tests.

Visual direction uses the existing Tajawal font and school identity: forest `#123e31`, green `#14634b`, mint `#e7f3ed`, white `#ffffff`, paper `#f4f7f3`, and line `#e2ebe6`. The full-height conversation and compact mobile composer take priority over decoration.

## Mobile header refinement

When a conversation is open at widths up to 860px, its 60px header replaces the page heading and always-visible context rows. Back, subject, student name and connection indicator remain visible. The information button expands participant names, student/class context, status, refresh and the existing close action; selecting another conversation collapses it again. Desktop headers retain their existing presentation.

The browser script passes again at 320/390/768/1440px, including a header height assertion, expanding/collapsing details, closing through the mobile details panel, quick replies, incoming updates and the existing navigation/composer checks. Updated conversation screenshots and `conversation-details-*.png` show both states. Development build passes after the refinement.
