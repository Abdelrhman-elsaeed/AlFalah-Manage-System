# C35 / D-127 — Assignment workspace verification

Date:2026-10-15. Scope: C4 existing Angular assignment route. [Runtime behavior](../../assignments.md), [acceptance plan](../../implementation-plan.md).

## Results

| Check | Result and practical limit |
|---|---|
| Focused Angular suite |19/19 pass: rendered controls/draft markers, selection across pages, off-filter load, single/co/split/whole pairs, no-op/revert, orphan removal, indexed projection, stale requests, failed search, save lock,409/reload and actual-edit guards. |
| Existing backend regression |50/50 pass across TeachingAssignmentPhase6Tests, SubjectPhase5Tests and SchoolTimetableServiceTests; no backend changes. [Counters](backend-results.json). |
| Development build |Pass after final focus-return change. |
| Production build |Blocked by existing unchanged VisitsV2 CSS34.50kB and legacy storage CSS39.34kB against32kB error budget. Assignment CSS18.33kB exceeds the16kB warning threshold. Other existing style/PrimeNG selector warnings remain. No budget override. [Build/test summary](frontend-results.json). |
| Real manager,390/768/1440 |Existing test-school API/settings/teacher data; keyboard Enter opens the picker, Tab stays inside it, Escape closes and returns focus, reduced-motion computed transition is0s, no page errors or document overflow. These sessions issue no business writes. |
| Real teacher |Existing role guard redirects the assignment route; direct assignment API returns403. |
| Synthetic editing,390/768/1440 |24 classrooms/110 teachers: selected members survive paging/failed search; split5=3+2 with1 whole pair;409 retains draft; explicit refresh; atomic fixture save/reload;10-row teacher summary. Writes are intercepted fixture responses, not operational SQL writes. |
| Synthetic state/large school |Loading,503/retry, empty guidance, long names, unsaved navigation cancellation;180 classrooms/12 subjects/500 teachers/2070 requirements;10 classroom rows, at most120 cell buttons,10 picker results and10 summary rows. The recorded render interval includes browser navigation/local API-fixture response and is not a production throughput/latency benchmark. |

Complete role/viewport assertions and the measured interval are in [browser-results.json](browser-results.json). Screenshot data belongs to the existing test school or explicitly synthetic fixtures. No tokens, private configuration or session files are included.

## Visual review

| Surface | Phone390 | Tablet768 | Desktop1440 |
|---|---|---|---|
| Real test-school ledger | [Image](live-ledger-390.png) | [Image](live-ledger-768.png) | [Image](live-ledger-1440.png) |
| Real picker | [Image](live-picker-390.png) | [Image](live-picker-768.png) | [Image](live-picker-1440.png) |
| Dense fixture ledger | [Image](ledger-390.png) | [Image](ledger-768.png) | [Image](ledger-1440.png) |
| Split allocation | [Image](split-390.png) | [Image](split-768.png) | [Image](split-1440.png) |
| Teacher summary | [Image](teachers-390.png) | [Image](teachers-768.png) | [Image](teachers-1440.png) |

Desktop ledger and phone split allocation were visually inspected. The real school currently has only one configured requirement, so dense/long-name evidence uses fixtures rather than inventing operational assignments. The approved ledger remains dominant, subject columns scroll internally, phone selection stacks vertically with a visible confirmation footer, and compact grade/progress controls preserve working space. Numeric fractions were isolated LTR after screenshot review; focus return was corrected after an actual browser assertion exposed its absence.

Semantic table headers/caption, native labelled controls, alert/status regions and modal keyboard behavior were checked. No claim is made that an actual screen reader or physical device was tested.

## Reproduce

From `frontend`:

```powershell
npx ng test --watch=false --browsers=ChromeHeadless --ts-config=tsconfig.assignment-spec.json --include='**/assignments/*.spec.ts'
npx ng build --configuration development --output-path=../.audit/classroom-assignments/development-build
npx ng build --configuration production --output-path=../.audit/classroom-assignments/production-build
```

From the repository root:

```powershell
dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --no-build --filter 'FullyQualifiedName~TeachingAssignmentPhase6Tests|FullyQualifiedName~SubjectPhase5Tests|FullyQualifiedName~SchoolTimetableServiceTests'
node frontend/scripts/check-assignment-ledger-live.mjs
```

Browser verification requires the existing local app on4200/5264 and uncommitted short-lived manager/teacher session files under `.audit/classroom-consumers/`. Generate them using the existing local private verification harness or provide authorized test sessions; never commit credentials. The browser script derives the test setup from settings and does not modify real assignments. Synthetic fixtures live in `frontend/scripts/fixtures/assignment-ledger.mjs`.

No C35 SQL/schema/provider/archive mutation, migration or deployment was performed. Historical signed bytes and storage settings are untouched by this frontend slice. C5 saved-revision PDF, owner appearance review, physical/assistive-technology/printer checks and production release acceptance remain separate gates.
