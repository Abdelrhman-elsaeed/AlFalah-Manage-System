# Independent School Duty navigation — 2026-10-14

The owner clarified that School Duty needs its **own sidebar entry and page**, rather than an internal tab beside lessons. The supplied screenshot confirmed that the current duty frontend was present; the mismatch was navigation placement. This supersedes the earlier timetable-tab UX decision without changing duty business scope, authorization, data or document contracts. See C18 / D-112.

## Delivered navigation

- A top-level `المناوبة` / `School Duty` sidebar item for school managers and teachers with an active school and existing Duty.View or Duty.Manage capability.
- Independent canonical `/school-duty` route reusing the existing manager/teacher duty component. It does not load the lesson timetable or require timetable administration.
- `/timetable` renders the lesson timetable. Old `/timetable?tab=duty` bookmarks, retained notifications and saved-signature return links redirect to `/school-duty`, preserving date, execution and fragment context. Query changes on an already open timetable re-evaluate this compatibility guard.
- Existing role guards and backend school/actual-author/manager checks remain. Secretary/moderator/general-manager roles are not granted duty access by this UI change. A timetable permission alone does not authorize the duty page.
- Existing unsaved-work protection applies to the independent page. Teachers can enter their duty workspace before signature setup; acknowledgement/submission still require D2's actual saved signature. No signature, draft, document, fact or archive history is rewritten.

## Reproduction and verification

`node scripts/check-duty-navigation.mjs` was run against the actual Angular server at localhost4200 before the fix. It failed with **independent top-level sidebar count 0, expected 1**. After the fix it passed **nine browser journeys**: manager/teacher at 390/768/1440, a separate lesson-page/sidebar-click case and two denied role/capability cases. The browser uses synthetic API fixtures and prevents all requests from reaching the operational API. This proves rendered current navigation and route behavior, not a new authenticated operational data workflow.

[Browser results](navigation-screens/browser-results.json), principal synthetic captures:

| Role | Phone | Tablet | Desktop |
|---|---|---|---|
| Manager | [390px](navigation-screens/SchoolManager-390.png) | [768px](navigation-screens/SchoolManager-768.png) | [1440px](navigation-screens/SchoolManager-1440.png) |
| Teacher | [390px](navigation-screens/Instructor-390.png) | [768px](navigation-screens/Instructor-768.png) | [1440px](navigation-screens/Instructor-1440.png) |

The nine new focused navigation checks cover capability/school isolation, legacy document/date/fragment retention, secretary isolation, unsaved-work cancellation, saved-work exit and pre-signature teacher entry. Focused Angular verification passed **105/105**, including the nine new checks: [executed log](navigation-tests/angular-focused.txt). The [development build](navigation-tests/development-build.txt) passed. Existing rendered D1 scheduling, D2 signature/draft/picker/history and D3 preview/download/recovery journeys passed again: [D1](navigation-tests/d1-browser.txt), [D2](navigation-tests/d2-browser.txt), [D3](navigation-tests/d3-browser.txt). The current live development server additionally compiled accessible collapsed-navigation labels and passed the nine navigation journeys. Existing D3 source/PDF/backend verification remains in [d3.md](d3.md); [local migration/startup](local-manual-testing.md) remains valid. No backend code, migration, account role, permission mapping, Drive connection or delivery enablement was changed.

Run from `frontend`:

```powershell
node scripts/check-duty-navigation.mjs
npm.cmd test -- --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.d2-spec.json --include='src/app/features/timetables/**/*.spec.ts' --include='src/app/features/intelligent-timetable/**/*.spec.ts' --include='src/app/shared/components/clearable-select/**/*.spec.ts' --include='src/app/features/student-affairs/messaging-chat/**/*.spec.ts' --include='src/app/features/student-affairs/guardian-dashboard/**/*.spec.ts'
npx.cmd ng build --configuration development
```

The original browser journeys continue to open legacy duty URLs, exercising the redirect; the old hidden lesson-panel assertion now checks lesson separation, and the old Home/End internal-tab check is replaced with keyboard entry through the independent sidebar item. They retain their original scheduling/signing/document checks. Test traffic uses synthetic fixtures; no operational data or parent notices are created. Owner visual and physical-device acceptance remain pending. External Drive delivery remains disabled in the running local API.
